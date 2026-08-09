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
            // 版面取法原版（見 docs/reference-classic-mt.md）：**不是迷宮，是開放大廳配牆塊障礙**，
            // 密度高、四角有探索獎勵、寶物室用門封死。
            // 上半＝開放式大廳＋牆塊；主門是一整道橫牆的唯一開口；下半＝入口廳＋兩側封閉壁龕。
            var rows = new[]
            {
                "WWWWWWWWWWWWW", // y0
                "W.W..W......W", // y1   頂廊：上樓梯 (10,1)；(2,1) 封牆讓左上角成死路
                "W.WW.W.WWW..W", // y2
                "W.WW.....WW.W", // y3   右上角血瓶 (11,3)
                "W......W...WW", // y4   (11,4) 封牆讓右上角成死路
                "W.WWW...WW..W", // y5
                "WWWWWW.WWWWWW", // y6   主門 (6,6)——整道橫牆的唯一開口
                "W...........W", // y7   下層走廊
                "WWW.WWWWW.WWW", // y8
                "W...........W", // y9   壁龕門 (2,9) 左、(10,9) 右
                "W.W.......W.W", // y10  左壁龕 蝙蝠(1,10)／右壁龕 黑史萊姆(11,10)
                "W.W.......W.W", // y11  入口廳：鑰匙 (3,11)(9,11)、守衛 (5,11)、spawn (6,11)
                "WWWWWWWWWWWWW", // y12
            };

            var entities = new List<FloorEntity>
            {
                new FloorEntity("F01_sp1", EntityType.Spawn, SpawnPos),
                new FloorEntity("F01_n01", EntityType.Npc, new GridPos(5, 11), dialogueId: "dlg_f01_intro"),
                new FloorEntity("F01_s01", EntityType.Stairs, StairsUpPos, stairs: StairsDirection.Up),

                // 主門不上鎖——它是唯一通往上層的路，鎖住它等於讓玩家有機會把自己關死
                // （無頭遊玩實證：貪心玩家把兩把鑰匙都花在壁龕上，主門就開不了了）。
                // 三扇門改成「兩扇壁龕門、兩把鑰匙」：兩邊都開得了，但右邊那間的黑史萊姆
                // 現在打不過——鑰匙不會白費，只是那瓶血得之後回來拿。
                new FloorEntity("F01_d02", EntityType.Door, new GridPos(2, 9), doorTier: KeyTier.Yellow),
                new FloorEntity("F01_d03", EntityType.Door, new GridPos(10, 9), doorTier: KeyTier.Yellow),

                new FloorEntity("F01_i01", EntityType.Item, new GridPos(3, 11), @ref: "key_yellow"),
                new FloorEntity("F01_i02", EntityType.Item, new GridPos(9, 11), @ref: "key_yellow"),
                new FloorEntity("F01_i03", EntityType.Item, new GridPos(1, 11), @ref: "potion_s"),
                new FloorEntity("F01_i04", EntityType.Item, new GridPos(11, 11), @ref: "potion_s"),
                // 角落獎勵**由怪看守**——原版的規矩：所有值錢的東西都要用血換。
                // 主路仍然零強制戰鬥（可以直接上樓），但不打就什麼也拿不到。
                // 這裡刻意不放寶石：寶石是 2F 的機制（mechanics.md 引入表）。
                new FloorEntity("F01_m02", EntityType.Monster, new GridPos(1, 2), @ref: "slime_green"),
                new FloorEntity("F01_i05", EntityType.Item, new GridPos(1, 1), @ref: "potion_s"),
                new FloorEntity("F01_m05", EntityType.Monster, new GridPos(11, 2), @ref: "slime_red"),
                new FloorEntity("F01_i06", EntityType.Item, new GridPos(11, 3), @ref: "potion_s"),

                // 上層大廳的可繞過怪：純粹的金幣/經驗來源
                new FloorEntity("F01_m01", EntityType.Monster, new GridPos(6, 9), @ref: "slime_green"),
                // 骷髏：打得動（防 0）但一刀 60——「打得動不代表該打」的活教材
                new FloorEntity("F01_m06", EntityType.Monster, new GridPos(8, 3), @ref: "skel_gray"),
                // 左壁龕：好交易（虧 132 血換 200 血瓶）
                new FloorEntity("F01_m03", EntityType.Monster, new GridPos(1, 10), @ref: "bat_cave"),
                // 右壁龕：預覽顯示紅色致死數字，D13 判定為牆——開這扇門等於白費一把鑰匙。
                // 這是刻意的第一課：花錢之前先看數字
                new FloorEntity("F01_m04", EntityType.Monster, new GridPos(11, 10), @ref: "slime_black"),
            };

            return new FloorDefinition("F01", FloorGrid.Parse(rows), entities, nameZh: "塔門之下");
        }

        /// <summary>1F 怪物數值（鏡像 data/monsters.csv）。</summary>
        /// <summary>
        /// 本層引用到的怪物 id。數值一律來自 <see cref="Data.Catalog"/>（data/monsters.csv）——
        /// 樓層只認 id，不存數值，否則兩邊會漂移（曾經發生過）。
        /// </summary>
        public static readonly string[] MonsterRefs =
        {
            "slime_green", "slime_red", "slime_black", "bat_cave", "skel_gray",
        };

        /// <summary>本層引用到的道具 id。數值同樣來自 Catalog（data/items.csv）。</summary>
        public static readonly string[] ItemRefs =
        {
            "key_yellow", "potion_s",
        };
    }
}
