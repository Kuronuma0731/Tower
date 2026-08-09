using Tower.Core.Floors;
using Tower.Core.Grid;

namespace Tower.Core.Floors
{
    /// <summary>
    /// 樓層上的一個實體（floor JSON 的 entities[] 一列的 POCO 形式）。
    /// eid 全塔唯一，由關卡編輯器生成；GameState.ConsumedEids 以它記帳。
    /// 不相干的欄位為 null／預設值——匯入器保證每型別必填欄位齊全。
    /// </summary>
    public sealed class FloorEntity
    {
        public string Eid { get; }
        public EntityType Type { get; }
        public GridPos Pos { get; }

        /// <summary>monster/item：對應 CSV 的 ref id。shop/altar：配置表 id。</summary>
        public string Ref { get; }

        /// <summary>door 專用。</summary>
        public KeyTier DoorTier { get; }

        /// <summary>stairs 專用。</summary>
        public StairsDirection Stairs { get; }

        /// <summary>switch 專用：目標 eid 清單（跨層結構）。</summary>
        public string[] SwitchTargets { get; }

        /// <summary>npc 專用：對話 id。</summary>
        public string DialogueId { get; }

        public FloorEntity(
            string eid, EntityType type, GridPos pos,
            string @ref = null,
            KeyTier doorTier = KeyTier.Yellow,
            StairsDirection stairs = StairsDirection.Up,
            string[] switchTargets = null,
            string dialogueId = null)
        {
            Eid = eid;
            Type = type;
            Pos = pos;
            Ref = @ref;
            DoorTier = doorTier;
            Stairs = stairs;
            SwitchTargets = switchTargets ?? System.Array.Empty<string>();
            DialogueId = dialogueId;
        }
    }
}
