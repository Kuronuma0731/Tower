using NUnit.Framework;
using Tower.Core.Combat;
using Tower.Core.Commands;
using Tower.Core.Grid;

namespace Tower.Core.Tests
{
    public class CommandAndGridTests
    {
        [Test]
        public void CollisionCommand_ApplyThenUndo_RestoresStateExactly()
        {
            var state = new GameState { Atk = 30, Def = 20, Hp = 520, Gold = 150, Exp = 120 };
            var before = state.Clone();

            var monster = new MonsterDefinition(
                "gatekeeper_biped", 30, 24, 300,
                TraitSet.FirstStrike | TraitSet.MultiHit, 200, 250, true);
            var outcome = CombatResolver.ResolveCollision(state.CombatStats, monster);
            var cmd = new CollisionBattleCommand("F08_m01", outcome, monster);

            cmd.Apply(state);
            Assert.AreEqual(520 - 1000, state.Hp); // 會死，但 D13 下遊戲層根本不會下這個指令——Core 只管數學
            Assert.AreEqual(350, state.Gold);
            Assert.AreEqual(370, state.Exp);
            Assert.IsTrue(state.ConsumedEids.Contains("F08_m01"));

            cmd.Undo(state);
            Assert.AreEqual(before.Hp, state.Hp);
            Assert.AreEqual(before.Gold, state.Gold);
            Assert.AreEqual(before.Exp, state.Exp);
            Assert.IsFalse(state.ConsumedEids.Contains("F08_m01"));
        }

        [Test]
        public void SnapshotReplay_EqualsDirectApply()
        {
            // 架構合約：當前狀態 = 入口快照 + 重放指令流
            var origin = new GameState { Atk = 12, Def = 8, Hp = 550 };
            var snapshot = origin.Clone();

            var slime = new MonsterDefinition("slime_green", 12, 6, 40, TraitSet.None, 3, 5, false);
            var outcome = CombatResolver.ResolveCollision(origin.CombatStats, slime);
            var cmd = new CollisionBattleCommand("F01_m01", outcome, slime);

            cmd.Apply(origin);            // 直接前進
            cmd.Apply(snapshot);          // 快照重放

            Assert.AreEqual(origin.Hp, snapshot.Hp);
            Assert.AreEqual(origin.Gold, snapshot.Gold);
            Assert.AreEqual(origin.ConsumedEids.Count, snapshot.ConsumedEids.Count);
        }

        [Test]
        public void FloorGrid_Parse_RejectsBadInput()
        {
            Assert.Throws<System.ArgumentException>(() => FloorGrid.Parse(new[] { "W.W" }));
            var rows = ValidRows();
            rows[5] = "WWWWWWXWWWWWW"; // 非法字元
            Assert.Throws<System.ArgumentException>(() => FloorGrid.Parse(rows));
        }

        [Test]
        public void OneWay_OnlyExitsAlongArrow()
        {
            var rows = ValidRows();
            rows[6] = "W.....^.....W"; // (6,6) 是向北單向
            var grid = FloorGrid.Parse(rows);

            var onArrow = new GridPos(6, 6);
            Assert.IsTrue(grid.CanStep(onArrow, new GridPos(6, 5)));  // 北：順箭頭
            Assert.IsFalse(grid.CanStep(onArrow, new GridPos(6, 7))); // 南：逆箭頭
            Assert.IsFalse(grid.CanStep(onArrow, new GridPos(5, 6))); // 西：不行
            Assert.IsTrue(grid.CanStep(new GridPos(6, 7), onArrow));  // 進入單向格不限方向
        }

        [Test]
        public void Wall_BlocksStep()
        {
            var grid = FloorGrid.Parse(ValidRows());
            Assert.IsFalse(grid.CanStep(new GridPos(1, 1), new GridPos(1, 0))); // 邊界牆
            Assert.IsTrue(grid.CanStep(new GridPos(1, 1), new GridPos(2, 1)));
        }

        private static string[] ValidRows()
        {
            var rows = new string[FloorGrid.Size];
            rows[0] = rows[FloorGrid.Size - 1] = new string('W', FloorGrid.Size);
            for (int y = 1; y < FloorGrid.Size - 1; y++)
                rows[y] = "W" + new string('.', FloorGrid.Size - 2) + "W";
            return rows;
        }
    }
}
