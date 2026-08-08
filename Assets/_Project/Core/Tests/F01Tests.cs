using NUnit.Framework;
using Tower.Core.Combat;
using Tower.Core.Commands;
using Tower.Core.Floors;
using Tower.Core.Simulation;

namespace Tower.Core.Tests
{
    /// <summary>F01 工程測試層：設計意圖的可執行驗收（佈局改了，這裡要跟著紅）。</summary>
    public class F01Tests
    {
        private static GameState Start() => new GameState
        {
            Atk = 10, Def = 10, Hp = 550, // data/balance.csv
        };

        [Test]
        public void F01_IsSolvable_MainPathHasNoForcedFight()
        {
            var floor = F01.Build();
            var result = new FloorSolver(floor, F01.Monsters(), F01.Items())
                .Solve(Start(), F01.SpawnPos, F01.StairsUpPos);

            Assert.AreEqual(SolverStatus.Solvable, result.Status);
            // 最佳路線：開左口袋殺蝙蝠（-24）拿血瓶（+150）→ 550 - 24 + 150 = 676
            Assert.AreEqual(676, result.BestExitHp);
        }

        [Test]
        public void F01_Skeleton_IsUnwinnableAtStart_ComebackHook()
        {
            var skel = F01.Monsters()["skel_gray"];
            var outcome = CombatResolver.ResolveCollision(Start().CombatStats, skel);
            Assert.IsFalse(outcome.Winnable); // 攻 10 vs 防 11 → 無法戰勝（D13 視覺語言首演）

            // 2F 拿到攻擊寶石（+2）後可破——回頭殺成立
            var withGem = CombatResolver.ResolveCollision(new PlayerStats(12, 10), skel);
            Assert.IsTrue(withGem.Winnable);
        }

        [Test]
        public void F01_PreviewNumbers_MatchTeachingIntent()
        {
            var start = Start().CombatStats;
            var slime = CombatResolver.ResolveCollision(start, F01.Monsters()["slime_green"]);
            var bat = CombatResolver.ResolveCollision(start, F01.Monsters()["bat_cave"]);

            Assert.AreEqual(8, slime.ExpectedLoss);  // 便宜的練習對象
            Assert.AreEqual(24, bat.ExpectedLoss);   // 貴三倍——預覽對比有戲
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
            var monsters = F01.Monsters();
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
