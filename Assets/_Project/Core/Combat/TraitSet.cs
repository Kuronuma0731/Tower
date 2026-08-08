using System;

namespace Tower.Core.Combat
{
    /// <summary>
    /// 怪物特性（D1 衍生規則：一律確定性，禁止機率）。
    /// 資料表字串（first_strike|multi_hit）由匯入層解析成此旗標。
    /// </summary>
    [Flags]
    public enum TraitSet
    {
        None = 0,
        FirstStrike = 1 << 0, // 先攻：開戰前先出手一次（該次同樣套用連擊）
        MultiHit    = 1 << 1, // 連擊：每次出手打 2 下
        Pierce      = 1 << 2, // 魔攻：無視我方防禦，敵方單擊永不歸零
        Lifesteal   = 1 << 3, // 吸血：每次出手後回復等同該次總傷害的 HP
    }
}
