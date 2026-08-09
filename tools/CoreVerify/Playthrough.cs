using System;
using System.Collections.Generic;
using System.Linq;
using Tower.Core.Combat;
using Tower.Core.Commands;
using Tower.Core.Data;
using Tower.Core.Floors;
using Tower.Core.Grid;

namespace Tower.Verify
{
    /// <summary>
    /// 無頭遊玩器：用 Core 的真實規則走完整趟，印出玩家實際會看到的東西。
    ///
    /// 這是「我自己先玩一遍」——比在 Unity 裡手動按方向鍵可靠：路徑最佳、
    /// 每個決策點都算得出來、而且可以重跑。它驗的不是「能不能過」（那是驗證器的事），
    /// 是**體驗**：每層的決策密度、資源曲線、預覽數字讀起來合不合理。
    /// </summary>
    internal static class Playthrough
    {
        private sealed class Sim
        {
            public GameState State;
            public FloorDefinition Floor;
            public Catalog Catalog;
            public readonly List<string> Log = new List<string>();
        }

        /// <summary>
        /// 從最底層一路玩到塔頂——**樓層從 registry 來，不寫死**，
        /// 所以新增一層 JSON 之後遊玩器自動涵蓋它。
        /// </summary>
        public static void Run(Catalog catalog, FloorRegistry floors)
        {
            Console.WriteLine($"\n════════ 無頭遊玩：{string.Join(" → ", floors.Order)} ════════");

            var s = new Sim
            {
                Catalog = catalog,
                State = new GameState { Atk = 10, Def = 10, Hp = 1000 },
            };

            foreach (var id in floors.Order)
            {
                var floor = floors[id];
                var entry = floor.FindStairs(StairsDirection.Down)
                            ?? floor.Entities.FirstOrDefault(e => e.Type == EntityType.Spawn);
                var exit = floor.FindStairs(StairsDirection.Up);
                if (entry == null || exit == null) { Console.WriteLine($"  ⊘ {id} 缺入口或出口，跳過"); continue; }

                PlayFloor(s, floor, entry.Pos, exit.Pos, $"{id} {floor.NameZh}");
            }

            Console.WriteLine("\n──── 通關結算 ────");
            Console.WriteLine($"  生命 {s.State.Hp}　攻擊 {s.State.Atk}　防禦 {s.State.Def}　" +
                              $"金幣 {s.State.Gold}　經驗 {s.State.Exp}　黃鑰匙 {s.State.KeysYellow}");
        }

        /// <summary>
        /// 走一層：反覆取「當前可達區」，撿光免費道具，然後挑**最划算的一筆消耗**
        /// （每滴血換到最多價值），直到抵達出口。這模擬的是一個算得清楚的玩家。
        /// </summary>
        private static void PlayFloor(Sim s, FloorDefinition floor, GridPos entry, GridPos exit, string title)
        {
            s.Floor = floor;
            Console.WriteLine($"\n──── {title} ────");
            Console.WriteLine($"  入場：生命 {s.State.Hp} 攻 {s.State.Atk} 防 {s.State.Def} " +
                              $"金 {s.State.Gold} 鑰匙 {s.State.KeysYellow}");

            int startHp = s.State.Hp;
            int guard = 0;

            // 玩到「沒有賺頭的行動可做，且出口可達」為止——魔塔玩家會先清場再上樓
            while (guard++ < 300)
            {
                var reach = Reachable(s, entry);
                CollectFree(s, reach);
                reach = Reachable(s, entry);

                var choice = BestAction(s, reach);
                bool exitOpen = reach.Contains(exit);

                if (choice == null)
                {
                    if (!exitOpen) Console.WriteLine("  ✖ 卡住了——沒有負擔得起的行動，出口也不可達");
                    break;
                }
                // 出口已開時，只做還有正報酬的事
                if (exitOpen && choice.NetHp <= 0) break;
                Perform(s, choice);
            }

            var finalReach = Reachable(s, entry);

            Console.WriteLine($"  離場：生命 {s.State.Hp}（{s.State.Hp - startHp:+#;-#;0}）" +
                              $" 攻 {s.State.Atk} 防 {s.State.Def} 金 {s.State.Gold} 經驗 {s.State.Exp}");
            ReportBlockers(s, finalReach);
        }

        // ---- 玩家視角的判斷 ----

        private sealed class Action
        {
            public FloorEntity Entity;
            public CollisionOutcome Outcome;
            public string Describe;
            public int NetHp;   // 這筆消耗的淨血量變化（正＝賺）
        }

        /// <summary>挑最划算的一筆消耗：門優先（開路），怪物則看淨收益。</summary>
        private static Action BestAction(Sim s, HashSet<GridPos> reach)
        {
            Action best = null;

            foreach (var e in s.Floor.Entities)
            {
                if (s.State.ConsumedEids.Contains(e.Eid)) continue;
                if (!AdjacentTo(reach, e.Pos)) continue;

                if (e.Type == EntityType.Door && HasKey(s.State, e.DoorTier))
                {
                    int gain = RewardBehind(s, e, reach);
                    var door = new Action
                    {
                        Entity = e, NetHp = gain,
                        Describe = $"開{TierName(e.DoorTier)}門 {e.Pos}" + (gain > 0 ? $"（門後值 {gain}）" : ""),
                    };
                    if (best == null || door.NetHp > best.NetHp) best = door;
                    continue;
                }

                if (e.Type == EntityType.Monster)
                {
                    var m = s.Catalog.Monsters[e.Ref];
                    var o = CombatResolver.ResolveCollision(s.State.CombatStats, m);
                    if (!o.Winnable || o.ExpectedLoss >= s.State.Hp) continue; // D13：牆

                    // 這隻怪身後有沒有東西？有的話值得打
                    int reward = RewardBehind(s, e, reach);
                    int net = reward - o.ExpectedLoss;
                    if (best == null || net > best.NetHp)
                        best = new Action
                        {
                            Entity = e, Outcome = o, NetHp = net,
                            Describe = $"打 {m.NameZh} {e.Pos}　-{o.ExpectedLoss} 血" +
                                       (o.Misses > 0 ? $"（被閃 {o.Misses} 次）" : "") +
                                       $"　+{m.GoldDrop} 金 +{m.ExpDrop} 經驗",
                        };
                }
            }

            // 只打有正報酬、或能開路的怪
            return best != null && best.NetHp > -9999 ? best : null;
        }

        /// <summary>
        /// 消掉這個障礙後，**新增**多少可拿的價值——真的模擬一次，不是估的。
        /// 這才問得出「這隻怪守著什麼」，也才驗得出佈局有沒有被繞道破解。
        /// </summary>
        private static int RewardBehind(Sim s, FloorEntity blocker, HashSet<GridPos> reachNow)
        {
            // 障礙常常是**成串**的（門後還有守衛，守衛後才是寶石）。只看一層會低估門的價值，
            // 導致「開了門也沒東西」的錯誤判斷——所以連鎖展開：把新可達區內**還能負擔得起**的
            // 障礙一併視為可清除，看最終能拿到什麼。
            var virtuallyCleared = new HashSet<string> { blocker.Eid };
            HashSet<GridPos> after;
            while (true)
            {
                foreach (var id in virtuallyCleared) s.State.ConsumedEids.Add(id);
                after = Reachable(s, s.State.Position);
                foreach (var id in virtuallyCleared) s.State.ConsumedEids.Remove(id);

                // 只跟著**這個障礙新開出來的**那條路走。若不排除「本來就摸得到的」障礙，
                // 連鎖會擴散到整層，把全地圖的怪都算進成本，判斷就全錯了。
                var next = s.Floor.Entities.FirstOrDefault(e =>
                    !virtuallyCleared.Contains(e.Eid) &&
                    !s.State.ConsumedEids.Contains(e.Eid) &&
                    AdjacentTo(after, e.Pos) &&
                    !AdjacentTo(reachNow, e.Pos) &&
                    Affordable(s, e));
                if (next == null) break;
                virtuallyCleared.Add(next.Eid);
            }

            int value = 0;
            foreach (var e in s.Floor.Entities)
            {
                if (s.State.ConsumedEids.Contains(e.Eid)) continue;
                if (e.Type != EntityType.Item) continue;
                if (reachNow.Contains(e.Pos) || !after.Contains(e.Pos)) continue; // 只算「本來拿不到、現在拿得到」

                var item = s.Catalog.Items[e.Ref];
                value += item.Category switch
                {
                    ItemCategory.Potion => item.HealHp,
                    ItemCategory.Gem => 300,   // 永久加成，估高於一瓶血
                    ItemCategory.Key => 150,
                    _ => 100,
                };
            }
            // 扣掉沿途要付的血——這樣「門後有寶石但守衛太貴」才會正確地變成不划算
            foreach (var id in virtuallyCleared)
            {
                var e = s.Floor.Entities.First(x => x.Eid == id);
                if (e.Type != EntityType.Monster || e.Eid == blocker.Eid) continue;
                value -= CombatResolver.ResolveCollision(s.State.CombatStats, s.Catalog.Monsters[e.Ref]).ExpectedLoss;
            }
            return value;
        }

        /// <summary>這個障礙現在清得掉嗎（有鑰匙／打得起）。</summary>
        private static bool Affordable(Sim s, FloorEntity e)
        {
            if (e.Type == EntityType.Door) return HasKey(s.State, e.DoorTier);
            if (e.Type != EntityType.Monster) return false;
            var o = CombatResolver.ResolveCollision(s.State.CombatStats, s.Catalog.Monsters[e.Ref]);
            return o.Winnable && o.ExpectedLoss < s.State.Hp;
        }

        private static void Perform(Sim s, Action a)
        {
            if (a.Entity.Type == EntityType.Door)
            {
                new OpenDoorCommand(a.Entity.Eid, a.Entity.DoorTier).Apply(s.State);
                Console.WriteLine($"  {a.Describe}");
                return;
            }
            var m = s.Catalog.Monsters[a.Entity.Ref];
            new CollisionBattleCommand(a.Entity.Eid, a.Outcome, m).Apply(s.State);
            Console.WriteLine($"  {a.Describe}　→ 剩 {s.State.Hp}");
        }

        private static void CollectFree(Sim s, HashSet<GridPos> reach)
        {
            bool picked;
            do
            {
                picked = false;
                foreach (var e in s.Floor.Entities)
                {
                    if (e.Type != EntityType.Item) continue;
                    if (s.State.ConsumedEids.Contains(e.Eid)) continue;
                    if (!reach.Contains(e.Pos)) continue;

                    var item = s.Catalog.Items[e.Ref];
                    new PickupItemCommand(e.Eid, item).Apply(s.State);
                    Console.WriteLine($"  撿 {ItemName(item)} {e.Pos}" +
                        (item.HealHp > 0 ? $"　→ 生命 {s.State.Hp}" :
                         item.AtkBonus > 0 ? $"　→ 攻擊 {s.State.Atk}" :
                         item.DefBonus > 0 ? $"　→ 防禦 {s.State.Def}" : ""));
                    picked = true;
                }
                if (picked) reach = Reachable(s, s.State.Position);
            } while (picked);
        }

        /// <summary>離場時列出還擋著的東西——這就是玩家會記住的「回頭殺」清單。</summary>
        private static void ReportBlockers(Sim s, HashSet<GridPos> reach)
        {
            foreach (var e in s.Floor.Entities)
            {
                if (e.Type != EntityType.Monster || s.State.ConsumedEids.Contains(e.Eid)) continue;
                var m = s.Catalog.Monsters[e.Ref];
                var o = CombatResolver.ResolveCollision(s.State.CombatStats, m);
                if (!o.Winnable)
                    Console.WriteLine($"  ⊘ {m.NameZh} {e.Pos} 打不動（預覽顯示 ✖）");
                else if (o.ExpectedLoss >= s.State.Hp)
                    Console.WriteLine($"  ⊘ {m.NameZh} {e.Pos} 會死（預覽 -{o.ExpectedLoss} vs 生命 {s.State.Hp}）→ D13 視同牆");
            }
        }

        // ---- 可達性（與 FloorSolver 同一套規則，但只看當前狀態）----

        private static HashSet<GridPos> Reachable(Sim s, GridPos from)
        {
            var seen = new HashSet<GridPos> { from };
            var q = new Queue<GridPos>();
            q.Enqueue(from);
            while (q.Count > 0)
            {
                var p = q.Dequeue();
                foreach (var n in Neighbours(p))
                {
                    if (seen.Contains(n) || !s.Floor.Grid.CanStep(p, n)) continue;
                    var e = s.Floor.EntityAt(n);
                    if (e != null && !s.State.ConsumedEids.Contains(e.Eid) &&
                        (e.Type == EntityType.Monster || e.Type == EntityType.Door || e.Type == EntityType.Npc))
                        continue;
                    seen.Add(n);
                    q.Enqueue(n);
                }
            }
            s.State.Position = from;
            return seen;
        }

        private static IEnumerable<GridPos> Neighbours(GridPos p)
        {
            yield return new GridPos(p.X + 1, p.Y);
            yield return new GridPos(p.X - 1, p.Y);
            yield return new GridPos(p.X, p.Y + 1);
            yield return new GridPos(p.X, p.Y - 1);
        }

        private static bool AdjacentTo(HashSet<GridPos> reach, GridPos p)
            => Neighbours(p).Any(reach.Contains);

        private static bool HasKey(GameState s, KeyTier t) => t switch
        {
            KeyTier.Yellow => s.KeysYellow > 0,
            KeyTier.Blue => s.KeysBlue > 0,
            _ => s.KeysRed > 0,
        };

        private static string TierName(KeyTier t) => t switch
        {
            KeyTier.Yellow => "黃", KeyTier.Blue => "藍", _ => "紅",
        };

        private static string ItemName(ItemDefinition i) => i.Category switch
        {
            ItemCategory.Key => TierName(i.KeyTier) + "鑰匙",
            ItemCategory.Potion => $"血瓶(+{i.HealHp})",
            ItemCategory.Gem => i.AtkBonus > 0 ? $"攻擊寶石(+{i.AtkBonus})" : $"防禦寶石(+{i.DefBonus})",
            _ => "沙漏",
        };
    }
}
