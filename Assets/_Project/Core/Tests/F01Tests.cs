using System.IO;
using NUnit.Framework;
using Tower.Core.Combat;
using Tower.Core.Commands;
using Tower.Core.Data;
using Tower.Core.Floors;
using Tower.Core.Simulation;
using UnityEngine;

namespace Tower.Core.Tests
{
    /// <summary>F01 工程測試層：設計意圖的可執行驗收（佈局改了，這裡要跟著紅）。</summary>
    public class F01Tests
    {
        private static GameState Start() => new GameState
        {
            Atk = 10, Def = 10, Hp = 1000, // data/balance.csv（對齊原版初始值）
        };

        /// <summary>數值來自 CSV——測試與遊戲讀同一份，不可能漂移。</summary>
        private static Catalog Data()
        {
            string dir = Path.Combine(Application.streamingAssetsPath, "data");
            return Catalog.Load(
                File.ReadAllText(Path.Combine(dir, "monsters.csv")),
                File.ReadAllText(Path.Combine(dir, "items.csv")));
        }

        [Test]
        public void F01_IsSolvable_MainPathHasNoForcedFight()
        {
            var floor = F01.Build();
            var result = new FloorSolver(floor, Data().Monsters, Data().Items)
                .Solve(Start(), F01.SpawnPos, F01.StairsUpPos);

            Assert.AreEqual(SolverStatus.Solvable, result.Status);
            // 最佳路線是「先探索再開打」：左上角撿攻擊寶石（攻 10→12）後，蝙蝠只損 110 而非 132。
            // 1000 − 110 + 200（左壁龕）+ 200（右側走廊）= 1290。
            // 這個誘因是佈局自然長出來的，非刻意設計——由驗證器發現。
            Assert.AreEqual(1290, result.BestExitHp);
        }

        [Test]
        public void F01_BlackSlime_IsLethalAtStart_ComebackHook()
        {
            var black = Data().Monsters["slime_black"];
            var start = Start();
            var outcome = CombatResolver.ResolveCollision(start.CombatStats, black);

            // 防 9 只低攻擊 1 點 → 每輪削 1 血 → 損血遠超上限：D13 判定此格為牆
            Assert.IsTrue(outcome.Winnable);                       // 數學上打得死…
            Assert.Greater(outcome.ExpectedLoss, start.Hp);        // …但會死，所以進不去

            // 攻擊力上去後才划算——回頭殺成立
            var stronger = CombatResolver.ResolveCollision(new PlayerStats(20, 10), black);
            Assert.Less(stronger.ExpectedLoss, start.Hp);
        }

        [Test]
        public void F01_PreviewNumbers_MatchTeachingIntent()
        {
            var start = Start().CombatStats;
            var slime = CombatResolver.ResolveCollision(start, Data().Monsters["slime_green"]);
            var bat = CombatResolver.ResolveCollision(start, Data().Monsters["bat_cave"]);

            Assert.AreEqual(32, slime.ExpectedLoss);   // 最便宜的練習對象（原版數值）
            Assert.AreEqual(132, bat.ExpectedLoss);    // 貴四倍——預覽對比有戲
            Assert.Less(slime.ExpectedLoss, bat.ExpectedLoss);
        }

        [Test]
        public void F01_KeyEconomy_TwoKeysThreeDoors()
        {
            var floor = F01.Build();
            int keys = 0, doors = 0;
            foreach (var e in floor.Entities)
            {
                if (e.Type == EntityType.Item && e.Ref == "key_yellow") keys++;
                if (e.Type == EntityType.Door) doors++;
            }
            Assert.AreEqual(2, keys);
            Assert.AreEqual(3, doors); // 鑰匙永遠比門少——第一個取捨在第一層
        }

        [Test]
        public void F01_BudgetSheet_MatchesLayout()
        {
            // 預算表 F01 行（data/floor-budget.csv）：金 16、經 21、藥水 300
            var floor = F01.Build();
            var monsters = Data().Monsters;
            int gold = 0, exp = 0, potionHp = 0;
            foreach (var e in floor.Entities)
            {
                if (e.Type == EntityType.Monster)
                {
                    gold += monsters[e.Ref].GoldDrop;
                    exp += monsters[e.Ref].ExpDrop;
                }
                if (e.Type == EntityType.Item && e.Ref == "potion_s") potionHp += 150;
            }
            Assert.AreEqual(16, gold);
            Assert.AreEqual(21, exp);
            Assert.AreEqual(300, potionHp);
        }
    }
}
