using System;
using System.Collections.Generic;
using Tower.Core.Combat;
using Tower.Core.Commands;
using Tower.Core.Floors;
using Tower.Core.Grid;

namespace Tower.Core.Simulation
{
    /// <summary>
    /// 可達性驗證器（每層獨立驗證：入口預算 → 出口）。D11 封閉經濟下的生命線。
    ///
    /// 模型：
    /// - 免費資源（道具）一到手就撿——全部非負收益，存在性搜索下無損正確
    /// - 分支點：開門（耗鑰匙）、殺怪（耗 HP，D13：致死不可進）、祭壇兌換與商店購買（遞增價，各計數獨立）
    /// - 剪枝：同一消耗集合（bitmask）下，資源向量被既有狀態全面支配者不再展開
    /// - NPC 視同永久障礙；開關（跨層結構）在每層驗證中忽略，由全塔檢查另管
    /// </summary>
    public sealed class FloorSolver
    {
        private readonly FloorDefinition _floor;
        private readonly IReadOnlyDictionary<string, MonsterDefinition> _monsters;
        private readonly IReadOnlyDictionary<string, ItemDefinition> _items;
        private readonly IReadOnlyDictionary<string, ShopDefinition> _shops;
        private readonly IReadOnlyDictionary<string, AltarDefinition> _altars;
        private readonly int _nodeCap;

        private readonly List<FloorEntity> _consumable = new List<FloorEntity>(); // index = bit
        private readonly Dictionary<string, int> _bitByEid = new Dictionary<string, int>();

        private Dictionary<ulong, List<int[]>> _seen;
        private int _nodes;
        private int _bestExitHp;

        public FloorSolver(
            FloorDefinition floor,
            IReadOnlyDictionary<string, MonsterDefinition> monsters,
            IReadOnlyDictionary<string, ItemDefinition> items,
            IReadOnlyDictionary<string, ShopDefinition> shops = null,
            IReadOnlyDictionary<string, AltarDefinition> altars = null,
            int nodeCap = 200_000)
        {
            _floor = floor;
            _monsters = monsters;
            _items = items;
            _shops = shops ?? new Dictionary<string, ShopDefinition>();
            _altars = altars ?? new Dictionary<string, AltarDefinition>();
            _nodeCap = nodeCap;

            foreach (var e in floor.Entities)
            {
                if (e.Type == EntityType.Monster || e.Type == EntityType.Door || e.Type == EntityType.Item)
                {
                    if (_consumable.Count >= 64)
                        throw new InvalidOperationException($"{floor.Id}: 可消耗實體超過 64，超出 bitmask 容量——樓層過擠，違反密度準則");
                    _bitByEid[e.Eid] = _consumable.Count;
                    _consumable.Add(e);
                }
            }
        }

        /// <summary>從 entry 狀態、entryPos 出發，判定能否抵達 exitPos。</summary>
        public SolverResult Solve(GameState entry, GridPos entryPos, GridPos exitPos)
        {
            _seen = new Dictionary<ulong, List<int[]>>();
            _nodes = 0;
            _bestExitHp = -1;

            var s = State.From(entry);
            Explore(s, entryPos, exitPos);

            if (_bestExitHp >= 0)
                return new SolverResult(SolverStatus.Solvable, _bestExitHp, _nodes);
            return new SolverResult(
                _nodes >= _nodeCap ? SolverStatus.Inconclusive : SolverStatus.Unsolvable,
                -1, _nodes);
        }

        // ---- 內部狀態 ----

        private sealed class State
        {
            public ulong Mask;
            public int Hp, Gold, Exp, Atk, Def;
            public int Ky, Kb, Kr;

            /// <summary>
            /// 中毒強度（D17）。0 = 沒中毒，走路免費——此時位置與路徑長度都不影響任何事，
            /// 支配剪枝維持原本的強度。**只有中毒時**路徑才進入成本模型。
            /// </summary>
            public int Poison;
            public Dictionary<string, int> Counters; // 遞增價計數（altar:stat / shop:item）

            public static State From(GameState g)
            {
                var s = new State
                {
                    Mask = 0,
                    Hp = g.Hp, Gold = g.Gold, Exp = g.Exp, Atk = g.Atk, Def = g.Def,
                    Ky = g.KeysYellow, Kb = g.KeysBlue, Kr = g.KeysRed,
                    Poison = g.PoisonPerStep,
                    Counters = new Dictionary<string, int>(g.PurchaseCounts),
                };
                return s;
            }

            public State Clone()
            {
                return new State
                {
                    Mask = Mask,
                    Hp = Hp, Gold = Gold, Exp = Exp, Atk = Atk, Def = Def,
                    Ky = Ky, Kb = Kb, Kr = Kr,
                    Poison = Poison,
                    Counters = new Dictionary<string, int>(Counters),
                };
            }

            public int CounterOf(string key) => Counters.TryGetValue(key, out var n) ? n : 0;

            /// <summary>支配比較向量：資源越大越好；遞增價計數越小越好（未來價更便宜）。</summary>
            public int[] DominanceVector(List<string> counterKeys)
            {
                var v = new int[9 + counterKeys.Count];
                v[0] = Hp; v[1] = Gold; v[2] = Exp; v[3] = Atk; v[4] = Def;
                v[5] = Ky; v[6] = Kb; v[7] = Kr;
                v[8] = -Poison;   // 中毒是負資產：毒越輕越好
                for (int i = 0; i < counterKeys.Count; i++)
                    v[9 + i] = -CounterOf(counterKeys[i]);
                return v;
            }
        }

        private readonly List<string> _counterKeys = new List<string>();

        private bool Dominated(State s)
        {
            var vec = s.DominanceVector(CounterKeys(s));
            if (_seen.TryGetValue(s.Mask, out var list))
            {
                foreach (var old in list)
                {
                    if (old.Length != vec.Length) continue;
                    bool allGe = true;
                    for (int i = 0; i < vec.Length && allGe; i++)
                        if (old[i] < vec[i]) allGe = false;
                    if (allGe) return true; // 舊狀態全面 ≥ 新狀態 → 剪枝
                }
                list.Add(vec);
            }
            else
            {
                _seen[s.Mask] = new List<int[]> { vec };
            }
            return false;
        }

        private List<string> CounterKeys(State s)
        {
            _counterKeys.Clear();
            foreach (var k in s.Counters.Keys) _counterKeys.Add(k);
            _counterKeys.Sort(StringComparer.Ordinal);
            return _counterKeys;
        }

        // ---- 搜索 ----

        private void Explore(State s, GridPos startPos, GridPos exitPos)
        {
            if (_nodes >= _nodeCap) return;
            _nodes++;

            // 閉包：BFS 可達區 + 自動撿免費道具（撿了可能開出新區域，做到不動點）
            var reach = Closure(s, startPos);

            if (reach.Contains(exitPos) && s.Hp > _bestExitHp)
                _bestExitHp = s.Hp;
            // 注意：出口可達不代表該收手——之後的動作（開口袋、拿血瓶）可能帶著更多 HP 離場。
            // 繼續枚舉，讓支配剪枝負責收斂。

            if (Dominated(s)) return;

            // 中毒（D17）：走路要花血，所以「走到目標旁邊」本身是成本。
            // 用可達區內的**最短步數**——存在性搜索下取下界是正確的：
            // 最短路走得通，繞遠路只會更貴，不會多解出東西。
            var dist = s.Poison > 0 ? StepsFrom(s, startPos, reach) : null;

            // 枚舉分支動作
            foreach (var e in _consumable)
            {
                int bit = _bitByEid[e.Eid];
                if ((s.Mask & (1UL << bit)) != 0) continue;
                if (!AdjacentToReach(reach, e.Pos)) continue;

                // 走過去的毒傷。D13：毒不致死，止在 1 血——所以這裡也不會把 HP 打到 0。
                int travel = 0;
                if (dist != null)
                {
                    int best = int.MaxValue;
                    foreach (var n in Neighbors(e.Pos))
                        if (dist.TryGetValue(n, out int d) && d < best) best = d;
                    if (best == int.MaxValue) continue;
                    travel = Math.Min(best * s.Poison, Math.Max(0, s.Hp - 1));
                }

                if (e.Type == EntityType.Door)
                {
                    if (!HasKey(s, e.DoorTier)) continue;
                    var next = s.Clone();
                    next.Hp -= travel;
                    SpendKey(next, e.DoorTier);
                    next.Mask |= 1UL << bit;
                    Explore(next, e.Pos, exitPos);
                }
                else if (e.Type == EntityType.Monster)
                {
                    var m = _monsters[e.Ref];
                    var outcome = CombatResolver.ResolveCollision(new PlayerStats(s.Atk, s.Def), m);
                    if (!outcome.Winnable) continue;
                    if (outcome.ExpectedLoss >= s.Hp) continue; // D13：致死格視同牆壁
                    var next = s.Clone();
                    next.Hp -= travel + outcome.ExpectedLoss;
                    if (m.Traits.HasFlag(TraitSet.Poison))
                        next.Poison = Math.Max(next.Poison, Math.Max(1, m.TraitValue));
                    next.Gold += m.GoldDrop;
                    next.Exp += m.ExpDrop;
                    next.Mask |= 1UL << bit;
                    Explore(next, e.Pos, exitPos);
                }
                else if (e.Type == EntityType.Item && s.Poison > 0)
                {
                    // 中毒時道具不再是「免費」（Closure 已停止自動撿）——
                    // 走過去要花血，所以它跟門和怪一樣是一筆要算的交易。
                    var next = s.Clone();
                    next.Hp -= travel;
                    ApplyItem(next, _items[e.Ref]);
                    next.Mask |= 1UL << bit;
                    Explore(next, e.Pos, exitPos);
                }
            }

            // 祭壇兌換（一次一點，遞增價；可達的祭壇才可用）
            foreach (var e in _floor.Entities)
            {
                if (e.Type == EntityType.Altar && reach.Contains(e.Pos) && _altars.TryGetValue(e.Ref, out var altar))
                {
                    foreach (var offer in altar.Offers)
                    {
                        string key = altar.Id + ":" + offer.Stat;
                        int cost = offer.CostAt(_counterOf(s, key));
                        if (s.Exp < cost) continue;
                        var next = s.Clone();
                        next.Exp -= cost;
                        next.Counters[key] = _counterOf(s, key) + 1;
                        switch (offer.Stat)
                        {
                            case AltarStat.Atk: next.Atk += offer.Gain; break;
                            case AltarStat.Def: next.Def += offer.Gain; break;
                            case AltarStat.Hp: next.Hp += offer.Gain; break;
                        }
                        Explore(next, startPos, exitPos);
                    }
                }
                else if (e.Type == EntityType.Shop && reach.Contains(e.Pos) && _shops.TryGetValue(e.Ref, out var shop))
                {
                    foreach (var offer in shop.Offers)
                    {
                        string key = shop.Id + ":" + offer.ItemId;
                        int price = offer.PriceAt(_counterOf(s, key));
                        if (s.Gold < price) continue;
                        var item = _items[offer.ItemId];
                        var next = s.Clone();
                        next.Gold -= price;
                        next.Counters[key] = _counterOf(s, key) + 1;
                        ApplyItem(next, item);
                        Explore(next, startPos, exitPos);
                    }
                }
            }
        }

        private static int _counterOf(State s, string key) => s.CounterOf(key);

        /// <summary>BFS 可達區 + 自動撿道具到不動點。會就地修改 s（撿到的道具入帳）。</summary>
        /// <summary>
        /// 可達區 ＋ 自動撿免費道具（撿了可能開出新區域，做到不動點）。
        ///
        /// **中毒時（D17）不自動撿**：走過去要花血，「免費」的前提消失了。
        /// 此時道具改由 <see cref="Explore"/> 當成收費的分支動作枚舉。
        /// 這個分岔是路徑模型與集合模型的接縫——漏掉它，驗證器會把中毒層算得太便宜
        /// （實測：走廊盡頭的血瓶在中毒下仍被算成純賺 +200）。
        /// </summary>
        private HashSet<GridPos> Closure(State s, GridPos start)
        {
            while (true)
            {
                var reach = Bfs(s, start);
                if (s.Poison > 0) return reach;

                bool picked = false;
                foreach (var e in _consumable)
                {
                    if (e.Type != EntityType.Item) continue;
                    int bit = _bitByEid[e.Eid];
                    if ((s.Mask & (1UL << bit)) != 0) continue;
                    if (!reach.Contains(e.Pos)) continue;
                    s.Mask |= 1UL << bit;
                    ApplyItem(s, _items[e.Ref]);
                    picked = true;
                }
                if (!picked) return reach;
            }
        }

        /// <summary>
        /// 從 start 到可達區各格的**最短步數**。只在中毒時算（D17）——
        /// 沒中毒時走路免費，算了也用不到，白花時間。
        /// </summary>
        private Dictionary<GridPos, int> StepsFrom(State s, GridPos start, HashSet<GridPos> reach)
        {
            var dist = new Dictionary<GridPos, int> { [start] = 0 };
            var queue = new Queue<GridPos>();
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                var pos = queue.Dequeue();
                int d = dist[pos];
                foreach (var next in Neighbors(pos))
                {
                    if (dist.ContainsKey(next)) continue;
                    if (!reach.Contains(next)) continue;
                    if (!_floor.Grid.CanStep(pos, next)) continue;
                    dist[next] = d + 1;
                    queue.Enqueue(next);
                }
            }
            return dist;
        }

        private HashSet<GridPos> Bfs(State s, GridPos start)
        {
            var visited = new HashSet<GridPos> { start };
            var queue = new Queue<GridPos>();
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                var pos = queue.Dequeue();
                foreach (var next in Neighbors(pos))
                {
                    if (visited.Contains(next)) continue;
                    if (!_floor.Grid.CanStep(pos, next)) continue;
                    if (Blocked(s, next)) continue;
                    visited.Add(next);
                    queue.Enqueue(next);
                }
            }
            return visited;
        }

        private bool Blocked(State s, in GridPos pos)
        {
            var e = _floor.EntityAt(pos);
            if (e == null) return false;
            switch (e.Type)
            {
                case EntityType.Npc:
                    return true; // 永久障礙（撞它 = 對話，不佔用消耗位）
                case EntityType.Monster:
                case EntityType.Door:
                    return (s.Mask & (1UL << _bitByEid[e.Eid])) == 0; // 未消耗即阻擋
                default:
                    return false; // 道具/樓梯/商店/祭壇/開關/spawn 可走
            }
        }

        private static IEnumerable<GridPos> Neighbors(GridPos p)
        {
            yield return new GridPos(p.X + 1, p.Y);
            yield return new GridPos(p.X - 1, p.Y);
            yield return new GridPos(p.X, p.Y + 1);
            yield return new GridPos(p.X, p.Y - 1);
        }

        private static bool AdjacentToReach(HashSet<GridPos> reach, in GridPos pos)
        {
            return reach.Contains(new GridPos(pos.X + 1, pos.Y))
                || reach.Contains(new GridPos(pos.X - 1, pos.Y))
                || reach.Contains(new GridPos(pos.X, pos.Y + 1))
                || reach.Contains(new GridPos(pos.X, pos.Y - 1));
        }

        private static bool HasKey(State s, KeyTier tier) => tier switch
        {
            KeyTier.Yellow => s.Ky > 0,
            KeyTier.Blue => s.Kb > 0,
            _ => s.Kr > 0,
        };

        private static void SpendKey(State s, KeyTier tier)
        {
            switch (tier)
            {
                case KeyTier.Yellow: s.Ky--; break;
                case KeyTier.Blue: s.Kb--; break;
                default: s.Kr--; break;
            }
        }

        private static void ApplyItem(State s, ItemDefinition item)
        {
            switch (item.Category)
            {
                case ItemCategory.Key:
                    switch (item.KeyTier)
                    {
                        case KeyTier.Yellow: s.Ky++; break;
                        case KeyTier.Blue: s.Kb++; break;
                        default: s.Kr++; break;
                    }
                    break;
                case ItemCategory.Potion: s.Hp += item.HealHp; break; // HP 無上限
                case ItemCategory.Gem: s.Atk += item.AtkBonus; s.Def += item.DefBonus; break;
                case ItemCategory.Undo: break; // 沙漏對求解無意義（求解 = 不犯錯的路徑）
                case ItemCategory.Antidote: s.Poison = 0; break; // D17：解毒藥讓走路重新變免費
            }
        }
    }
}
