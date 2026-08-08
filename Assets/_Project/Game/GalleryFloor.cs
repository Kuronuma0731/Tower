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

            return new FloorDefinition("F00", FloorGrid.Parse(rows), e, nameZh: "");
        }

        /// <summary>展示佔位數值——僅供陳列室預覽標籤有數字可算，非正式平衡。</summary>
        public static Dictionary<string, MonsterDefinition> Monsters()
        {
            var m = F01.Monsters(); // 1F 三隻用真數值
            m["slime_red"] = new MonsterDefinition("slime_red", 16, 6, 38, TraitSet.None, 3, 4, false);
            m["slime_blue"] = new MonsterDefinition("slime_blue", 20, 8, 45, TraitSet.None, 4, 6, false);
            m["rat_giant"] = new MonsterDefinition("rat_giant", 18, 5, 40, TraitSet.None, 5, 6, false);
            m["bandit"] = new MonsterDefinition("bandit", 22, 10, 55, TraitSet.None, 12, 7, false);
            m["skel_soldier"] = new MonsterDefinition("skel_soldier", 26, 13, 60, TraitSet.None, 8, 14, false);
            m["wasp_striker"] = new MonsterDefinition("wasp_striker", 24, 9, 48, TraitSet.FirstStrike, 8, 10, false);
            m["duelist_twin"] = new MonsterDefinition("duelist_twin", 26, 12, 66, TraitSet.MultiHit, 10, 13, false);
            m["vampbat_king"] = new MonsterDefinition("vampbat_king", 30, 12, 80, TraitSet.Lifesteal, 14, 18, false);
            m["gatekeeper_biped"] = new MonsterDefinition("gatekeeper_biped", 30, 24, 300,
                TraitSet.FirstStrike | TraitSet.MultiHit, 200, 250, true);
            m["warden_10"] = new MonsterDefinition("warden_10", 45, 30, 600, TraitSet.None, 500, 600, true);
            return m;
        }

        /// <summary>道具全套（鏡像 data/items.csv）。</summary>
        public static Dictionary<string, ItemDefinition> Items() => new Dictionary<string, ItemDefinition>
        {
            ["key_yellow"] = new ItemDefinition("key_yellow", ItemCategory.Key, KeyTier.Yellow),
            ["key_blue"] = new ItemDefinition("key_blue", ItemCategory.Key, KeyTier.Blue),
            ["key_red"] = new ItemDefinition("key_red", ItemCategory.Key, KeyTier.Red),
            ["potion_s"] = new ItemDefinition("potion_s", ItemCategory.Potion, healHp: 150),
            ["potion_l"] = new ItemDefinition("potion_l", ItemCategory.Potion, healHp: 400),
            ["gem_atk"] = new ItemDefinition("gem_atk", ItemCategory.Gem, atkBonus: 2),
            ["gem_def"] = new ItemDefinition("gem_def", ItemCategory.Gem, defBonus: 2),
            ["hourglass"] = new ItemDefinition("hourglass", ItemCategory.Undo, undoSteps: 5),
        };
    }
}
