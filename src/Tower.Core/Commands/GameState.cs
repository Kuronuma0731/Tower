using System.Collections.Generic;
using Tower.Core.Combat;
using Tower.Core.Grid;

namespace Tower.Core.Commands
{
    /// <summary>
    /// 全部可變遊戲狀態。純數值與集合——樓層地圖是靜態資料，不在這裡。
    /// 只能被 IGameCommand 改變；快照 = 深拷貝（Clone）。
    /// </summary>
    public sealed class GameState
    {
        public int Atk;
        public int Def;
        public int Hp;
        public int Gold;
        public int Exp;
        public int KeysYellow;
        public int KeysBlue;
        public int KeysRed;
        public int Hourglasses;

        public string CurrentFloor = "F01";
        public GridPos Position;

        /// <summary>已擊殺/已拾取/已開門/已觸發的實體（eid）。封閉經濟的帳本。</summary>
        public readonly HashSet<string> ConsumedEids = new HashSet<string>();

        /// <summary>遞增計價的計數。鍵格式："shop_id:item_id" 或 "altar_id:stat"。</summary>
        public readonly Dictionary<string, int> PurchaseCounts = new Dictionary<string, int>();

        /// <summary>
        /// 已遭遇的怪物 id（怪物手冊的內容）。
        ///
        /// **刻意不走指令模式**，是 D7「所有狀態變更都是 IGameCommand」的唯一例外：
        /// 這是**知識不是資源**——回溯一步不該讓玩家「忘記」看過的怪。
        /// 它只增不減，回溯與退回樓層都不動它。
        /// </summary>
        public readonly HashSet<string> SeenMonsters = new HashSet<string>();

        /// <summary>
        /// 擊殺後再生的結果：原 eid → 現在站在那格的怪 id（<see cref="Combat.TraitSet.ReviveAsWeaker"/>）。
        /// 樓層資料是靜態的，所有變動一律進狀態——存檔才帶得走，回溯才還原得回去。
        /// </summary>
        public readonly Dictionary<string, string> RevivedMonsters = new Dictionary<string, string>();

        public PlayerStats CombatStats => new PlayerStats(Atk, Def);

        /// <summary>樓層快照用的深拷貝。</summary>
        public GameState Clone()
        {
            var copy = new GameState
            {
                Atk = Atk, Def = Def, Hp = Hp,
                Gold = Gold, Exp = Exp,
                KeysYellow = KeysYellow, KeysBlue = KeysBlue, KeysRed = KeysRed,
                Hourglasses = Hourglasses,
                CurrentFloor = CurrentFloor,
                Position = Position,
            };
            copy.ConsumedEids.UnionWith(ConsumedEids);
            copy.SeenMonsters.UnionWith(SeenMonsters);
            foreach (var kv in RevivedMonsters) copy.RevivedMonsters[kv.Key] = kv.Value;
            foreach (var kv in PurchaseCounts)
                copy.PurchaseCounts[kv.Key] = kv.Value;
            return copy;
        }
    }
}
