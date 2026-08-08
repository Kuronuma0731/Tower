using System.Collections.Generic;
using NUnit.Framework;
using Tower.Core.Combat;
using Tower.Core.Commands;
using Tower.Core.Floors;
using Tower.Core.Grid;
using Tower.Core.Simulation;

namespace Tower.Core.Tests
{
    public class FloorSolverTests
    {
        // ---- 佈景工具 ----

        private static string[] OpenRows()
        {
            var rows = new string[FloorGrid.Size];
            rows[0] = rows[FloorGrid.Size - 1] = new string('W', FloorGrid.Size);
            for (int y = 1; y < FloorGrid.Size - 1; y++)
                rows[y] = "W" + new string('.', FloorGrid.Size - 2) + "W";
            return rows;
        }

        private static string[] WithWall(string[] rows, int x, int y)
        {
            var chars = rows[y].ToCharArray();
            chars[x] = 'W';
            rows[y] = new string(chars);
            return rows;
        }

        private static readonly GridPos Entry = new GridPos(1, 1);
        private static readonly GridPos Exit = new GridPos(11, 11);

        private static Dictionary<string, ItemDefinition> BaseItems() => new Dictionary<string, ItemDefinition>
        {
            ["key_yellow"] = new ItemDefinition("key_yellow", ItemCategory.Key, KeyTier.Yellow),
            ["potion_s"] = new ItemDefinition("potion_s", ItemCategory.Potion, healHp: 150),
        };

        private static MonsterDefinition Gatekeeper() => new MonsterDefinition(
            "gatekeeper_biped", 30, 24, 300,
            TraitSet.FirstStrike | TraitSet.MultiHit, 200, 250, isGuardian: true);

        // ---- 基本可解性 ----

        [Test]
        public void OpenFloor_IsSolvable()
        {
            var floor = new FloorDefinition("T01", FloorGrid.Parse(OpenRows()), new List<FloorEntity>());
            var solver = new FloorSolver(floor, new Dictionary<string, MonsterDefinition>(), BaseItems());
            var result = solver.Solve(new GameState { Hp = 100 }, Entry, Exit);
            Assert.AreEqual(SolverStatus.Solvable, result.Status);
            Assert.AreEqual(100, result.BestExitHp);
        }

        [Test]
        public void LockedDoor_NoKey_Unsolvable_WithKeyItem_Solvable()
        {
            // 出口關進口袋，唯一入口是一扇黃門
            var rows = OpenRows();
            WithWall(rows, 11, 10); // 出口上方封牆 → 唯一通道 (10,11)
            var door = new FloorEntity("T_d1", EntityType.Door, new GridPos(10, 11), doorTier: KeyTier.Yellow);

            var floorNoKey = new FloorDefinition("T02a", FloorGrid.Parse(rows), new List<FloorEntity> { door });
            var r1 = new FloorSolver(floorNoKey, new Dictionary<string, MonsterDefinition>(), BaseItems())
                .Solve(new GameState { Hp = 100 }, Entry, Exit);
            Assert.AreEqual(SolverStatus.Unsolvable, r1.Status);

            var key = new FloorEntity("T_i1", EntityType.Item, new GridPos(3, 3), @ref: "key_yellow");
            var floorWithKey = new FloorDefinition("T02b", FloorGrid.Parse(rows), new List<FloorEntity> { door, key });
            var r2 = new FloorSolver(floorWithKey, new Dictionary<string, MonsterDefinition>(), BaseItems())
                .Solve(new GameState { Hp = 100 }, Entry, Exit);
            Assert.AreEqual(SolverStatus.Solvable, r2.Status);
        }

        [Test]
        public void BlockingMonster_LethalWithoutPotion_SolvableWithPotion()
        {
            var rows = OpenRows();
            WithWall(rows, 11, 10);
            var monsters = new Dictionary<string, MonsterDefinition>
            {
                ["brute"] = new MonsterDefinition("brute", 30, 5, 60, TraitSet.None, 0, 0, false),
            };
            // 玩家 10/10/100：單擊 5 → 12 回合；敵單擊 20 → 損血 11×20 = 220 ≥ 100 → 致死（D13 不可進）
            var block = new FloorEntity("T_m1", EntityType.Monster, new GridPos(10, 11), @ref: "brute");

            var floorNoPotion = new FloorDefinition("T03a", FloorGrid.Parse(rows), new List<FloorEntity> { block });
            var r1 = new FloorSolver(floorNoPotion, monsters, BaseItems())
                .Solve(new GameState { Atk = 10, Def = 10, Hp = 100 }, Entry, Exit);
            Assert.AreEqual(SolverStatus.Unsolvable, r1.Status);

            // 撒兩瓶血（+300 → HP 400 > 220）→ 可解，且剩餘 HP = 400 − 220 = 180
            var floorWithPotions = new FloorDefinition("T03b", FloorGrid.Parse(rows), new List<FloorEntity>
            {
                block,
                new FloorEntity("T_i1", EntityType.Item, new GridPos(3, 3), @ref: "potion_s"),
                new FloorEntity("T_i2", EntityType.Item, new GridPos(4, 3), @ref: "potion_s"),
            });
            var r2 = new FloorSolver(floorWithPotions, monsters, BaseItems())
                .Solve(new GameState { Atk = 10, Def = 10, Hp = 100 }, Entry, Exit);
            Assert.AreEqual(SolverStatus.Solvable, r2.Status);
            Assert.AreEqual(180, r2.BestExitHp);
        }

        [Test]
        public void AltarConversion_UnlocksUnwinnableFight()
        {
            var rows = OpenRows();
            WithWall(rows, 11, 10);
            var monsters = new Dictionary<string, MonsterDefinition>
            {
                ["turtle"] = new MonsterDefinition("turtle", 5, 10, 5, TraitSet.None, 0, 0, false),
            };
            // 玩家攻 10 vs 防 10 → 單擊 0 = 無法戰勝；祭壇買 +1 攻（20 經驗）後可破
            var floor = new FloorDefinition("T04", FloorGrid.Parse(rows), new List<FloorEntity>
            {
                new FloorEntity("T_m1", EntityType.Monster, new GridPos(10, 11), @ref: "turtle"),
                new FloorEntity("T_a1", EntityType.Altar, new GridPos(3, 3), @ref: "altar_std"),
            });
            var altars = new Dictionary<string, AltarDefinition>
            {
                ["altar_std"] = new AltarDefinition("altar_std", new List<AltarOffer>
                {
                    new AltarOffer(AltarStat.Atk, 20, 1, 5),
                }),
            };

            var noExp = new FloorSolver(floor, monsters, BaseItems(), altars: altars)
                .Solve(new GameState { Atk = 10, Def = 10, Hp = 100, Exp = 0 }, Entry, Exit);
            Assert.AreEqual(SolverStatus.Unsolvable, noExp.Status);

            var withExp = new FloorSolver(floor, monsters, BaseItems(), altars: altars)
                .Solve(new GameState { Atk = 10, Def = 10, Hp = 100, Exp = 20 }, Entry, Exit);
            Assert.AreEqual(SolverStatus.Solvable, withExp.Status);
        }

        // ---- 迷你 8F：重算盤面的迴歸測試 ----

        private static (FloorDefinition floor, Dictionary<string, MonsterDefinition> monsters,
                        Dictionary<string, ShopDefinition> shops, Dictionary<string, AltarDefinition> altars)
            Mini8F(bool includeShop)
        {
            var rows = OpenRows();
            WithWall(rows, 11, 10); // 守關怪是通往出口的唯一路
            var entities = new List<FloorEntity>
            {
                new FloorEntity("F08_m01", EntityType.Monster, new GridPos(10, 11), @ref: "gatekeeper_biped"),
                new FloorEntity("F08_a01", EntityType.Altar, new GridPos(3, 3), @ref: "altar_std"),
            };
            if (includeShop)
                entities.Add(new FloorEntity("F08_sh1", EntityType.Shop, new GridPos(5, 5), @ref: "shop_f03"));

            var monsters = new Dictionary<string, MonsterDefinition> { ["gatekeeper_biped"] = Gatekeeper() };
            var shops = new Dictionary<string, ShopDefinition>
            {
                ["shop_f03"] = new ShopDefinition("shop_f03", new List<ShopOffer>
                {
                    new ShopOffer("potion_s", 80, 20),
                }),
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
            return (new FloorDefinition("F08", FloorGrid.Parse(rows), entities), monsters, shops, altars);
        }

        private static GameState Arrival8F() => new GameState
        {
            Atk = 30, Def = 20, Hp = 520, Gold = 150, Exp = 120,
        };

        [Test]
        public void Mini8F_WithShop_Solvable_PotionIsTheHiddenTicket()
        {
            var (floor, monsters, shops, altars) = Mini8F(includeShop: true);
            var result = new FloorSolver(floor, monsters, BaseItems(), shops, altars)
                .Solve(Arrival8F(), Entry, Exit);
            Assert.AreEqual(SolverStatus.Solvable, result.Status);
            // 戰鬥最優是 2攻+3防（120 經驗全額）：損血 532，戰後 138（此組合由驗證器發現，
            // 人工手算的初版盤面表漏了它——見 boss-test-8f.md 發現 2）。
            // 出口 HP 更高：殺守關怪後用戰利品（+200 金 +250 經驗）回頭掃貨——
            // 血瓶 ×2（100+120 金）+ 祭壇 HP ×7（245 經驗）= +650 → 138 + 650 = 788。
            Assert.AreEqual(788, result.BestExitHp);
        }

        [Test]
        public void Mini8F_WithoutShop_Unsolvable_PureExpAlwaysDies()
        {
            var (floor, monsters, _, altars) = Mini8F(includeShop: false);
            var result = new FloorSolver(floor, monsters, BaseItems(), altars: altars)
                .Solve(Arrival8F(), Entry, Exit);
            Assert.AreEqual(SolverStatus.Unsolvable, result.Status); // 純經驗最低損血 600 > 520
        }

        [Test]
        public void Mini8F_GuardianContracts_BothHold()
        {
            var (floor, monsters, _, altars) = Mini8F(includeShop: true);
            var results = GuardianContracts.Check(floor, monsters, BaseItems(), altars, Arrival8F());
            Assert.AreEqual(1, results.Count);
            Assert.IsTrue(results[0].PreviewDeathHolds);     // 1000 ≥ 520
            Assert.IsTrue(results[0].ZeroDamageUnreachable); // 經驗上界 370 → 最多 +9 防 = 29 < 30（差 1，檢查有牙）
        }

        [Test]
        public void GuardianContract_Catches_OverstuffedDefFloor()
        {
            // 反例：樓層撒滿防寶石讓零傷可達 → 合約②必須抓到
            var (floor, monsters, _, altars) = Mini8F(includeShop: false);
            var items = BaseItems();
            items["gem_def_big"] = new ItemDefinition("gem_def_big", ItemCategory.Gem, defBonus: 10);
            var entities = new List<FloorEntity>(floor.Entities)
            {
                new FloorEntity("F08_i9", EntityType.Item, new GridPos(7, 7), @ref: "gem_def_big"),
            };
            var stuffed = new FloorDefinition("F08x", floor.Grid, entities);

            var results = GuardianContracts.Check(stuffed, monsters, items, altars, Arrival8F());
            Assert.IsFalse(results[0].ZeroDamageUnreachable); // 20 + 10 + 祭壇 → 可達 30 → 免費守關戰，抓到
        }
    }
}
