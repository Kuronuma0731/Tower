using NUnit.Framework;
using Tower.Core.Combat;

namespace Tower.Core.Tests
{
    /// <summary>
    /// docs/boss-test-8f.md 的驗收測試向量——紙上精算表的每個數字必須被重現。
    /// 這些數字改了，表也要改；表改了，這裡也要改（兩邊同步是文件明定的合約）。
    /// </summary>
    public class CombatResolverTests
    {
        // 門衛雙足獸：30/24/300，先攻+連擊（data/monsters.csv 同步）
        private static MonsterDefinition Gatekeeper() => new MonsterDefinition(
            "gatekeeper_biped", atk: 30, def: 24, hp: 300,
            TraitSet.FirstStrike | TraitSet.MultiHit,
            goldDrop: 200, expDrop: 250, isGuardian: true);

        [Test]
        public void PlanA_Direct_Loss1000_Rounds50()
        {
            var o = CombatResolver.ResolveCollision(new PlayerStats(30, 20), Gatekeeper());
            Assert.IsTrue(o.Winnable);
            Assert.AreEqual(50, o.Rounds);
            Assert.AreEqual(1000, o.ExpectedLoss); // HP 520 → 預覽：死亡
        }

        [Test]
        public void PlanB_AllAtk_Loss500()
        {
            var o = CombatResolver.ResolveCollision(new PlayerStats(36, 20), Gatekeeper());
            Assert.AreEqual(25, o.Rounds);
            Assert.AreEqual(500, o.ExpectedLoss); // 剩 20 HP，貼地通過
        }

        [Test]
        public void PlanC_AllDef_Loss400()
        {
            var o = CombatResolver.ResolveCollision(new PlayerStats(30, 26), Gatekeeper());
            Assert.AreEqual(50, o.Rounds);
            Assert.AreEqual(400, o.ExpectedLoss); // 本層最優
        }

        [Test]
        public void PlanD_Balanced_Loss476()
        {
            var o = CombatResolver.ResolveCollision(new PlayerStats(33, 23), Gatekeeper());
            Assert.AreEqual(34, o.Rounds);
            Assert.AreEqual(476, o.ExpectedLoss); // 剩 44 HP
        }

        [Test]
        public void ZeroPlayerDamage_IsUnwinnable_NoDivideByZero()
        {
            // 攻 24 vs 防 24 → 單擊 0 → 「無法戰勝」，不得除零
            var o = CombatResolver.ResolveCollision(new PlayerStats(24, 20), Gatekeeper());
            Assert.IsFalse(o.Winnable);
        }

        [Test]
        public void Pierce_IgnoresDefense_NeverZeroDamage()
        {
            var mage = new MonsterDefinition("mage_void", 20, 10, 60, TraitSet.Pierce, 0, 0, false);
            // 我方防禦 999 也擋不住魔攻：敵方單擊 = 20
            var o = CombatResolver.ResolveCollision(new PlayerStats(30, 999), mage);
            Assert.IsTrue(o.Winnable);
            Assert.AreEqual(3, o.Rounds);            // ceil(60/20)
            Assert.AreEqual(2 * 20, o.ExpectedLoss); // 出手 2 次 × 單擊 20
        }

        [Test]
        public void Lifesteal_RoundsFromNetReduction()
        {
            // 我方單擊 10，敵方單擊 6（吸血：每輪回復 6），HP 30
            // n×10 − (n−1)×6 ≥ 30 → n ≥ 6 → 6 回合，損血 (6−1)×6 = 30
            var bat = new MonsterDefinition("vampbat", 16, 10, 30, TraitSet.Lifesteal, 0, 0, false);
            var o = CombatResolver.ResolveCollision(new PlayerStats(20, 10), bat);
            Assert.IsTrue(o.Winnable);
            Assert.AreEqual(6, o.Rounds);
            Assert.AreEqual(30, o.ExpectedLoss);
        }

        [Test]
        public void Lifesteal_NetZero_IsUnwinnable()
        {
            // 我方單擊 6 ≤ 每輪回復 6 → 不可擊殺（餵血）
            var bat = new MonsterDefinition("vampbat", 16, 10, 30, TraitSet.Lifesteal, 0, 0, false);
            var o = CombatResolver.ResolveCollision(new PlayerStats(16, 10), bat);
            Assert.IsFalse(o.Winnable);
        }

        [Test]
        public void Preview_IsPure_SameInputSameOutput()
        {
            var p = new PlayerStats(30, 20);
            var m = Gatekeeper();
            var first = CombatResolver.ResolveCollision(p, m);
            var second = CombatResolver.ResolveCollision(p, m);
            Assert.AreEqual(first.ExpectedLoss, second.ExpectedLoss);
            Assert.AreEqual(first.Rounds, second.Rounds); // 無副作用：預覽即實戰
        }
    }
}
