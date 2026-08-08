using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Tower.Core.Combat;
using Tower.Core.Commands;
using Tower.Core.Data;
using Tower.Core.Floors;
using Tower.Core.Grid;
using Tower.Core.Simulation;

namespace Tower.Verify
{
    /// <summary>
    /// 引擎外驗收：Core 是純 C#，所以規則、公式、驗證器全部能用 dotnet 幾秒跑完。
    /// 數值一律從 data/*.csv 讀（唯一真相），不在此重複定義。
    /// </summary>
    internal static class Program
    {
        private static int _passed, _failed;

        private static void Check(string name, bool ok)
        {
            if (ok) { _passed++; Console.WriteLine($"  PASS  {name}"); }
            else { _failed++; Console.WriteLine($"  FAIL  {name}"); }
        }

        private static int Main()
        {
            string repo = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
            string dataDir = Path.Combine(repo, "data");
            if (!Directory.Exists(dataDir))
            {
                Console.WriteLine($"找不到資料夾 {dataDir}");
                return 2;
            }

            var catalog = Catalog.Load(
                File.ReadAllText(Path.Combine(dataDir, "monsters.csv")),
                File.ReadAllText(Path.Combine(dataDir, "items.csv")));

            DataChecks(catalog);
            FormulaChecks();
            EvasionChecks();
            CommandChecks();
            GridChecks();
            SolverChecks(catalog);
            F01Checks(catalog);

            Console.WriteLine($"\n{_passed} passed, {_failed} failed");
            return _failed == 0 ? 0 : 1;
        }

        private static void DataChecks(Catalog catalog)
        {
            Console.WriteLine("== 資料表（唯一真相）==");
            Check($"monsters.csv {catalog.Monsters.Count} 隻、items.csv {catalog.Items.Count} 件",
                catalog.Monsters.Count >= 15 && catalog.Items.Count >= 8);
            Check($"敏捷欄有讀到（史萊姆人 敏={catalog.Monsters["slimeman"].Agility}）",
                catalog.Monsters["slimeman"].Agility == 30);
            Check($"特性欄有讀到（紅蝙蝠 {catalog.Monsters["vampbat_king"].Traits}）",
                catalog.Monsters["vampbat_king"].Traits == TraitSet.MultiHit);
            Check("F01 引用的怪都在表內", F01.MonsterRefs.All(catalog.Monsters.ContainsKey));
            Check("F01 引用的道具都在表內", F01.ItemRefs.All(catalog.Items.ContainsKey));

            bool threw = false;
            try { Catalog.ParseTraits("first_stirke"); } catch (ArgumentException) { threw = true; }
            Check("未知特性名在載入期擲例外（錯字不會流到玩家手機）", threw);
        }

        // boss-test-8f.md 的驗收向量
        private static MonsterDefinition Gatekeeper() => new MonsterDefinition(
            "gatekeeper_biped", 30, 24, 300,
            TraitSet.FirstStrike | TraitSet.MultiHit, 200, 250, true, "門衛雙足獸");

        private static void FormulaChecks()
        {
            Console.WriteLine("== DamageFormula（boss-test-8f 向量）==");
            var g = Gatekeeper();
            var a = CombatResolver.ResolveCollision(new PlayerStats(30, 20), g);
            Check($"A 直接打 loss={a.ExpectedLoss} rounds={a.Rounds}（期望 1000/50）", a.ExpectedLoss == 1000 && a.Rounds == 50);
            var b = CombatResolver.ResolveCollision(new PlayerStats(36, 20), g);
            Check($"B 全攻 loss={b.ExpectedLoss}（期望 500）", b.ExpectedLoss == 500 && b.Rounds == 25);
            var c = CombatResolver.ResolveCollision(new PlayerStats(30, 26), g);
            Check($"C 全防 loss={c.ExpectedLoss}（期望 400）", c.ExpectedLoss == 400 && c.Rounds == 50);
            var d = CombatResolver.ResolveCollision(new PlayerStats(33, 23), g);
            Check($"D 均衡 loss={d.ExpectedLoss}（期望 476）", d.ExpectedLoss == 476 && d.Rounds == 34);

            Check("打不動 → 無法戰勝（不除零）",
                !CombatResolver.ResolveCollision(new PlayerStats(24, 20), g).Winnable);

            var mage = new MonsterDefinition("mage", 20, 10, 60, TraitSet.Pierce, 0, 0, false);
            var pm = CombatResolver.ResolveCollision(new PlayerStats(30, 999), mage);
            Check($"魔攻無視防禦 loss={pm.ExpectedLoss}（期望 40）", pm.ExpectedLoss == 40 && pm.Rounds == 3);

            var bat = new MonsterDefinition("vampbat", 16, 10, 30, TraitSet.Lifesteal, 0, 0, false);
            var ls = CombatResolver.ResolveCollision(new PlayerStats(20, 10), bat);
            Check($"吸血淨削減 loss={ls.ExpectedLoss} rounds={ls.Rounds}（期望 30/6）", ls.ExpectedLoss == 30 && ls.Rounds == 6);
            Check("吸血淨零 → 無法戰勝",
                !CombatResolver.ResolveCollision(new PlayerStats(16, 10), bat).Winnable);
        }

        private static void EvasionChecks()
        {
            Console.WriteLine("== D15 迴避 ==");
            var evasive = new MonsterDefinition("slimeman", 79, 24, 90, TraitSet.None, 10, 2, false, "史萊姆人", 30);
            var plain = new MonsterDefinition("plain", 79, 24, 90, TraitSet.None, 10, 2, false, "對照組");
            var p = new PlayerStats(30, 20);

            var e1 = CombatResolver.ResolveCollision(p, evasive);
            var e0 = CombatResolver.ResolveCollision(p, plain);
            Check($"敏30 落空 {e1.Misses}/{e1.Rounds} 次，損 {e1.ExpectedLoss}；敏0 損 {e0.ExpectedLoss}",
                e1.Misses > 0 && e1.ExpectedLoss > e0.ExpectedLoss);

            var again = CombatResolver.ResolveCollision(p, evasive);
            Check("迴避結果可重現（預覽不會騙人）",
                again.ExpectedLoss == e1.ExpectedLoss && again.Misses == e1.Misses);

            var vampEva = new MonsterDefinition("vampEva", 30, 10, 500, TraitSet.Lifesteal, 0, 0, false, "吸血迴避", 50);
            Check("吸血+迴避淨削減不足 → 不可擊殺（不無限迴圈）",
                !CombatResolver.ResolveCollision(new PlayerStats(30, 20), vampEva).Winnable);
        }

        private static void CommandChecks()
        {
            Console.WriteLine("== 指令模式 ==");
            var state = new GameState { Atk = 30, Def = 20, Hp = 520, Gold = 150, Exp = 120 };
            var before = state.Clone();
            var g = Gatekeeper();
            var cmd = new CollisionBattleCommand("F08_m01", CombatResolver.ResolveCollision(state.CombatStats, g), g);

            cmd.Apply(state);
            Check("Apply：hp/gold/exp/eid 全變",
                state.Hp == -480 && state.Gold == 350 && state.Exp == 370 && state.ConsumedEids.Contains("F08_m01"));
            cmd.Undo(state);
            Check("Undo 精確還原",
                state.Hp == before.Hp && state.Gold == before.Gold && state.Exp == before.Exp && state.ConsumedEids.Count == 0);
        }

        private static void GridChecks()
        {
            Console.WriteLine("== Grid ==");
            var rows = new string[FloorGrid.Size];
            rows[0] = rows[FloorGrid.Size - 1] = new string('W', FloorGrid.Size);
            for (int y = 1; y < FloorGrid.Size - 1; y++) rows[y] = "W" + new string('.', FloorGrid.Size - 2) + "W";
            rows[6] = "W.....^.....W";
            var grid = FloorGrid.Parse(rows);
            var arrow = new GridPos(6, 6);
            Check("單向格只能順箭頭離開",
                grid.CanStep(arrow, new GridPos(6, 5)) &&
                !grid.CanStep(arrow, new GridPos(6, 7)) &&
                !grid.CanStep(arrow, new GridPos(5, 6)) &&
                grid.CanStep(new GridPos(6, 7), arrow));
            Check("牆阻擋", !grid.CanStep(new GridPos(1, 1), new GridPos(1, 0)));

            bool threw = false;
            try { FloorGrid.Parse(new[] { "bad" }); } catch (ArgumentException) { threw = true; }
            Check("壞輸入擲例外", threw);
        }

        private static void SolverChecks(Catalog catalog)
        {
            Console.WriteLine("== FloorSolver（迷你 8F）==");
            var entry = new GridPos(1, 1);
            var exit = new GridPos(11, 11);

            string[] Rows()
            {
                var r = new string[13];
                r[0] = r[12] = new string('W', 13);
                for (int y = 1; y < 12; y++) r[y] = "W" + new string('.', 11) + "W";
                var c = r[10].ToCharArray(); c[11] = 'W'; r[10] = new string(c); // 出口口袋
                return r;
            }

            var items = new Dictionary<string, ItemDefinition>
            {
                ["potion_s"] = new ItemDefinition("potion_s", ItemCategory.Potion, healHp: 150),
            };
            var monsters = new Dictionary<string, MonsterDefinition> { ["gatekeeper_biped"] = Gatekeeper() };
            var shops = new Dictionary<string, ShopDefinition>
            {
                ["shop_f03"] = new ShopDefinition("shop_f03", new List<ShopOffer> { new ShopOffer("potion_s", 80, 20) }),
            };
            var altars = new Dictionary<string, AltarDefinition>
            {
                ["altar_std"] = new AltarDefinition("altar_std", new List<AltarOffer>
                {
                    new AltarOffer(AltarStat.Atk, 20, 1, 5),
                    new AltarOffer(AltarStat.Def, 20, 1, 5),
                    new AltarOffer(AltarStat.Hp, 20, 50, 5),
                }),
            };

            FloorDefinition Make(bool withShop)
            {
                var ents = new List<FloorEntity>
                {
                    new FloorEntity("F08_m01", EntityType.Monster, new GridPos(10, 11), @ref: "gatekeeper_biped"),
                    new FloorEntity("F08_a01", EntityType.Altar, new GridPos(3, 3), @ref: "altar_std"),
                };
                if (withShop) ents.Add(new FloorEntity("F08_sh1", EntityType.Shop, new GridPos(5, 5), @ref: "shop_f03"));
                return new FloorDefinition("F08", FloorGrid.Parse(Rows()), ents);
            }

            GameState Arrival() => new GameState { Atk = 30, Def = 20, Hp = 520, Gold = 150, Exp = 120 };

            var withShop = new FloorSolver(Make(true), monsters, items, shops, altars).Solve(Arrival(), entry, exit);
            Check($"有商店 {withShop.Status} exitHp={withShop.BestExitHp} nodes={withShop.NodesExplored}（期望 Solvable/788）",
                withShop.Status == SolverStatus.Solvable && withShop.BestExitHp == 788);

            var noShop = new FloorSolver(Make(false), monsters, items, altars: altars).Solve(Arrival(), entry, exit);
            Check($"無商店 {noShop.Status}（期望 Unsolvable：純經驗最低損血 > 血量）",
                noShop.Status == SolverStatus.Unsolvable);

            var contracts = GuardianContracts.Check(Make(true), monsters, items, altars, Arrival());
            Check($"守關合約①預覽必死={contracts[0].PreviewDeathHolds} ②零傷不可達={contracts[0].ZeroDamageUnreachable}",
                contracts.Count == 1 && contracts[0].Passed);
        }

        private static void F01Checks(Catalog catalog)
        {
            Console.WriteLine("== F01 ==");
            var start = new GameState { Atk = 10, Def = 10, Hp = 1000 }; // data/balance.csv
            var result = new FloorSolver(F01.Build(), catalog.Monsters, catalog.Items)
                .Solve(start, F01.SpawnPos, F01.StairsUpPos);
            Check($"可解 {result.Status} exitHp={result.BestExitHp} nodes={result.NodesExplored}（期望 1290：先撿寶石再打）",
                result.Status == SolverStatus.Solvable && result.BestExitHp == 1290);

            var black = catalog.Monsters["slime_black"];
            var now = CombatResolver.ResolveCollision(new PlayerStats(10, 10), black);
            var later = CombatResolver.ResolveCollision(new PlayerStats(20, 10), black);
            Check($"黑史萊姆開局致死（損 {now.ExpectedLoss} > 1000）、攻20 後可行（損 {later.ExpectedLoss}）",
                now.Winnable && now.ExpectedLoss > 1000 && later.ExpectedLoss < 1000);

            var sl = CombatResolver.ResolveCollision(new PlayerStats(10, 10), catalog.Monsters["slime_green"]);
            var bt = CombatResolver.ResolveCollision(new PlayerStats(10, 10), catalog.Monsters["bat_cave"]);
            Check($"預覽數字 史萊姆 -{sl.ExpectedLoss} / 蝙蝠 -{bt.ExpectedLoss}（期望 32 / 132）",
                sl.ExpectedLoss == 32 && bt.ExpectedLoss == 132);
        }
    }
}
