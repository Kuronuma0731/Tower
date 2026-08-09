using System.Collections.Generic;
using Tower.Core.Combat;
using Tower.Core.Floors;
using Tower.Core.Grid;

namespace Tower.Game
{
    /// <summary>
    /// 展示層（F00）——開發用陳列室，不是遊戲內容：全部怪物、道具、門、互動點排開供檢視。
    /// 怪物數值除 1F 三隻與守關怪外皆為**展示佔位**（正式數值屬數值管線，勿引用）。
    /// ghost_pale 與 mage_void 缺圖（重生清單），暫不陳列。
    /// </summary>
    public static class GalleryFloor
    {
        public static readonly GridPos SpawnPos = new GridPos(6, 11);

        public static FloorDefinition Build()
        {
            var rows = new string[FloorGrid.Size];
            rows[0] = rows[FloorGrid.Size - 1] = new string('W', FloorGrid.Size);
            for (int y = 1; y < FloorGrid.Size - 1; y++)
                rows[y] = "W" + new string('.', FloorGrid.Size - 2) + "W";

            var e = new List<FloorEntity>();

            // y=2：一般怪一列排開
            string[] monsterRow =
            {
                "slime_green", "slime_red", "slime_blue", "bat_cave", "rat_giant",
                "bandit", "skel_gray", "skel_soldier", "wasp_striker", "duelist_twin", "vampbat_king",
            };
            for (int i = 0; i < monsterRow.Length; i++)
                e.Add(new FloorEntity($"G_m{i:00}", EntityType.Monster, new GridPos(1 + i, 2), @ref: monsterRow[i]));

            // y=4：守關怪
            e.Add(new FloorEntity("G_b01", EntityType.Monster, new GridPos(4, 4), @ref: "gatekeeper_biped"));
            e.Add(new FloorEntity("G_b02", EntityType.Monster, new GridPos(8, 4), @ref: "warden_10"));

            // y=6：互動點
            e.Add(new FloorEntity("G_npc", EntityType.Npc, new GridPos(2, 6), dialogueId: "dlg_f01_intro"));
            e.Add(new FloorEntity("G_shop", EntityType.Shop, new GridPos(4, 6), @ref: "shop_f03"));
            e.Add(new FloorEntity("G_altar", EntityType.Altar, new GridPos(6, 6), @ref: "altar_std"));
            e.Add(new FloorEntity("G_switch", EntityType.Switch, new GridPos(8, 6)));
            e.Add(new FloorEntity("G_su", EntityType.Stairs, new GridPos(10, 6), stairs: StairsDirection.Up));
            e.Add(new FloorEntity("G_sd", EntityType.Stairs, new GridPos(11, 6), stairs: StairsDirection.Down));

            // y=8：道具全套
            string[] itemRow = { "key_yellow", "key_blue", "key_red", "potion_s", "potion_l", "gem_atk", "gem_def", "hourglass" };
            for (int i = 0; i < itemRow.Length; i++)
                e.Add(new FloorEntity($"G_i{i:00}", EntityType.Item, new GridPos(2 + i, 8), @ref: itemRow[i]));

            // y=10：三色門
            e.Add(new FloorEntity("G_dy", EntityType.Door, new GridPos(4, 10), doorTier: KeyTier.Yellow));
            e.Add(new FloorEntity("G_db", EntityType.Door, new GridPos(6, 10), doorTier: KeyTier.Blue));
            e.Add(new FloorEntity("G_dr", EntityType.Door, new GridPos(8, 10), doorTier: KeyTier.Red));

            e.Add(new FloorEntity("G_sp", EntityType.Spawn, SpawnPos));

            return new FloorDefinition("GALLERY", FloorGrid.Parse(rows), e, nameZh: "");
        }

        // 數值一律來自 Catalog（data/monsters.csv、data/items.csv）——展示層曾因自帶
        // 佔位數值而與 CSV 全面漂移（紅史萊姆 16/6/38 vs 20/4/50 等），現已移除。
    }
}
