using System;

namespace Tower.Core.Combat
{
    /// <summary>
    /// 唯一的傷害公式所在地（docs/boss-test-8f.md 是本類的紙上規格與驗收向量）。
    /// 所有特性的數值語義都在這裡結算——CombatResolver 只負責跑迴圈。
    /// </summary>
    public static class DamageFormula
    {
        /// <summary>我方單擊 = max(0, 我方攻 − 敵方防)。歸零即「打不動」。</summary>
        public static int PlayerHit(in PlayerStats player, MonsterDefinition monster)
            => Math.Max(0, player.Atk - monster.Def);

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
