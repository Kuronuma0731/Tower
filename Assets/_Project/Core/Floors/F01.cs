using System.Collections.Generic;
using Tower.Core.Combat;
using Tower.Core.Grid;

namespace Tower.Core.Floors
{
    /// <summary>
    /// F01「塔門之下」——工程測試層（2026-08-08 拷問定案：正式內容仍等編輯器，
    /// 本層以程式碼建構供第 3 步最小可玩版使用，編輯器落地後匯入潤飾）。
    ///
    /// 設計意圖（mechanics.md 1F 引入表）：
    /// - 主題：讀懂傷害預覽——撞得起哪隻怪
    /// - 鑰匙 2 把、門 3 扇：第一個取捨在第一層發生（預算表 F01 行）
    /// - 主路零強制戰鬥；兩個口袋各有守衛：蝙蝠（虧 24 賺 150，好交易）
    ///   vs 小骷髏（現在無法戰勝——D13 視覺語言首演＋「回頭殺」鉤子）
    /// - 上樓梯 (6,1)：座標對齊規約下，F02 的下樓梯被鎖定在 (6,1)
    ///
    /// 數值鏡像自 data/monsters.csv——改任一邊必須同步另一邊（DataPipeline 落地前的過渡措施）。
    /// </summary>
    public static class F01
    {
        public static readonly GridPos SpawnPos = new GridPos(6, 11);
        public static readonly GridPos StairsUpPos = new GridPos(6, 1);

        public static FloorDefinition Build()
        {
            var rows = new[]
            {
                "WWWWWWWWWWWWW", // y0
                "W...........W", // y1  ← 上樓梯 (6,1)
                "W...........W", // y2
                "W...........W", // y3  ← 黃鑰匙 k2 (11,3)、史萊姆 (6,3)
                "W...........W", // y4
                "W...........W", // y5
                "WWWWWW.WWWWWW", // y6  ← 主門 d1 (6,6)
                "W...........W", // y7
                "WWW.......WWW", // y8  ← 史萊姆 (6,8)
                "W...........W", // y9  ← 左口袋 [藥(1,9) 蝙蝠(2,9)] 門(3,9)；右口袋 門(9,9) [骷髏(10,9) 藥(11,9)]
                "WWW.......WWW", // y10
                "W...........W", // y11 ← 鑰匙 k1 (1,11)、守衛 NPC (5,11)、spawn (6,11)
                "WWWWWWWWWWWWW", // y12
            };

            var entities = new List<FloorEntity>
            {
                new FloorEntity("F01_sp1", EntityType.Spawn, SpawnPos),
                new FloorEntity("F01_n01", EntityType.Npc, new GridPos(5, 11), dialogueId: "dlg_f01_intro"),
                new FloorEntity("F01_s01", EntityType.Stairs, StairsUpPos, stairs: StairsDirection.Up),

                new FloorEntity("F01_d01", EntityType.Door, new GridPos(6, 6), doorTier: KeyTier.Yellow),
                new FloorEntity("F01_d02", EntityType.Door, new GridPos(3, 9), doorTier: KeyTier.Yellow),
                new FloorEntity("F01_d03", EntityType.Door, new GridPos(9, 9), doorTier: KeyTier.Yellow),

                new FloorEntity("F01_i01", EntityType.Item, new GridPos(1, 11), @ref: "key_yellow"),
                new FloorEntity("F01_i02", EntityType.Item, new GridPos(11, 3), @ref: "key_yellow"),
                new FloorEntity("F01_i03", EntityType.Item, new GridPos(1, 9), @ref: "potion_s"),
                new FloorEntity("F01_i04", EntityType.Item, new GridPos(11, 9), @ref: "potion_s"),

                new FloorEntity("F01_m01", EntityType.Monster, new GridPos(6, 8), @ref: "slime_green"),
                new FloorEntity("F01_m02", EntityType.Monster, new GridPos(6, 3), @ref: "slime_green"),
                new FloorEntity("F01_m03", EntityType.Monster, new GridPos(2, 9), @ref: "bat_cave"),
                new FloorEntity("F01_m04", EntityType.Monster, new GridPos(10, 9), @ref: "skel_gray"),
            };

            return new FloorDefinition("F01", FloorGrid.Parse(rows), entities, nameZh: "塔門之下");
        }

        /// <summary>1F 怪物數值（鏡像 data/monsters.csv）。</summary>
        public static Dictionary<string, MonsterDefinition> Monsters() => new Dictionary<string, MonsterDefinition>
        {
            ["slime_green"] = new MonsterDefinition("slime_green", 12, 4, 30, TraitSet.None, 2, 3, false, "綠史萊姆"),
            ["bat_cave"] = new MonsterDefinition("bat_cave", 14, 6, 28, TraitSet.None, 4, 5, false, "洞穴蝙蝠"),
            ["skel_gray"] = new MonsterDefinition("skel_gray", 15, 11, 35, TraitSet.None, 8, 10, false, "小骷髏"),
        };

        /// <summary>1F 用到的道具（鏡像 data/items.csv）。</summary>
        public static Dictionary<string, ItemDefinition> Items() => new Dictionary<string, ItemDefinition>
        {
            ["key_yellow"] = new ItemDefinition("key_yellow", ItemCategory.Key, KeyTier.Yellow),
            ["potion_s"] = new ItemDefinition("potion_s", ItemCategory.Potion, healHp: 150),
        };
    }
}
