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

        // ---- 以下四種取自原版的確定性特性（CONTEXT D1 的擴充清單）----

        /// <summary>
        /// 適應性防禦：有效防禦 = max(防, 我方攻 − trait_value)。
        /// 攻擊堆過門檻之後，牠的防禦跟著漲——**你永遠砍不快，只能撐得久**。
        /// 這是唯一一種「堆攻擊無效」的牆，與「打不動」不同：它可解，只是很貴。
        /// </summary>
        AdaptiveDefense = 1 << 4,

        /// <summary>
        /// 特殊戰鬥：這一戰固定損血 trait_value，與雙方數值無關，且必定可勝。
        /// 用來做「劇情性關卡」——代價是寫死的，不受成長影響。
        /// </summary>
        FixedLoss = 1 << 5,

        /// <summary>
        /// 衰弱攻擊：每挨一次，我方有效攻擊 −trait_value（本場戰鬥內，最低到 1）。
        /// **刻意做成戰鬥內**而不是永久 debuff：永久版需要解除手段與一個驗證器狀態維度，
        /// 那會動到 D11 的封閉經濟；戰鬥內版本一樣達到「拖越久越虧」的效果，且完全可算。
        /// </summary>
        Weaken = 1 << 6,

        /// <summary>
        /// 擊殺後再生：死亡時原地變成 revive_into 指定的較弱同系怪。
        /// 這不是公式而是**實體層**效果——牠佔的格子不會清空，資源帳本要記兩次。
        /// </summary>
        ReviveAsWeaker = 1 << 7,
    }
}
