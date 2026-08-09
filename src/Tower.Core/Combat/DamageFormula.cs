using System;

namespace Tower.Core.Combat
{
    /// <summary>
    /// 唯一的傷害公式所在地（docs/boss-test-8f.md 是本類的紙上規格與驗收向量）。
    /// 所有特性的數值語義都在這裡結算——CombatResolver 只負責跑迴圈。
    /// </summary>
    public static class DamageFormula
    {
        /// <summary>
        /// 我方單擊 = max(0, 我方攻 − 敵方**有效**防禦)。歸零即「打不動」。
        /// <paramref name="atkPenalty"/> 是本場戰鬥累積的衰弱量（見 <see cref="TraitSet.Weaken"/>）。
        /// </summary>
        public static int PlayerHit(in PlayerStats player, MonsterDefinition monster, int atkPenalty = 0)
        {
            int atk = Math.Max(1, player.Atk - atkPenalty);   // 衰弱最低壓到 1，不歸零
            return Math.Max(0, atk - EffectiveDefense(atk, monster));
        }

        /// <summary>
        /// 敵方有效防禦。**適應性防禦**：防禦跟著我方攻擊漲，把我方單擊壓在 trait_value 以內
        /// ——攻擊堆過門檻之後就再也砍不快，只能拼耐久。這是唯一一種「堆攻擊無效」的牆。
        /// </summary>
        public static int EffectiveDefense(int playerAtk, MonsterDefinition monster)
            => monster.Traits.HasFlag(TraitSet.AdaptiveDefense)
                ? Math.Max(monster.Def, playerAtk - Math.Max(1, monster.TraitValue))
                : monster.Def;

        /// <summary>敵方單擊 = max(0, 敵方攻 − 我方防)；魔攻無視防禦，永不歸零。</summary>
        public static int MonsterHit(in PlayerStats player, MonsterDefinition monster)
            => monster.Traits.HasFlag(TraitSet.Pierce)
                ? monster.Atk
                : Math.Max(0, monster.Atk - player.Def);

        /// <summary>連擊：每次出手打 2 下，否則 1 下。先攻的那次出手同樣適用。</summary>
        public static int HitsPerOccasion(MonsterDefinition monster)
            => monster.Traits.HasFlag(TraitSet.MultiHit) ? 2 : 1;

        /// <summary>敵方每次出手造成的總傷害。</summary>
        public static int DamagePerOccasion(in PlayerStats player, MonsterDefinition monster)
            => MonsterHit(player, monster) * HitsPerOccasion(monster);

        /// <summary>吸血：每次出手後回復等同該次總傷害的 HP；無吸血則為 0。</summary>
        public static int HealPerOccasion(in PlayerStats player, MonsterDefinition monster)
            => monster.Traits.HasFlag(TraitSet.Lifesteal)
                ? DamagePerOccasion(player, monster)
                : 0;

        /// <summary>
        /// 衰弱：每挨一次，我方有效攻擊 −trait_value（**本場戰鬥內**累積）。
        /// 刻意不做成永久 debuff——永久版需要解除手段與一個驗證器狀態維度，會動到 D11 的封閉經濟。
        /// 戰鬥內版本一樣達到「拖越久越砍不動」，而且完全可算。
        /// </summary>
        public static int WeakenPerHit(MonsterDefinition monster)
            => monster.Traits.HasFlag(TraitSet.Weaken) ? Math.Max(1, monster.TraitValue) : 0;

        /// <summary>
        /// D15 迴避：命中 <paramref name="hitsNeeded"/> 次所需的**總出手數**（含落空）。
        /// 落空次數 = 總出手 − 命中數，由敏捷算死；順序隨機由表現層決定，總帳不受影響。
        /// 敏捷上限鎖在 90，避免除以零。
        /// </summary>
        public static int AttacksNeeded(int hitsNeeded, MonsterDefinition monster)
        {
            int agi = Math.Clamp(monster.Agility, 0, 90);
            if (agi <= 0 || hitsNeeded <= 0) return hitsNeeded;
            // ceil(hitsNeeded / (1 − agi/100)) 用整數運算避免浮點誤差
            return (int)(((long)hitsNeeded * 100 + (100 - agi) - 1) / (100 - agi));
        }
    }
}
