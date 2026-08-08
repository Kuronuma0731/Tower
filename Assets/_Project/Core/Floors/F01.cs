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
                "W....W......W", // y1   頂廊：上樓梯 (10,1) 右上角、攻擊寶石 (1,1) 左上角
                "W.WW.W.WWW..W", // y2
                "W.WW.....WW.W", // y3   血瓶 (11,3) 右側走廊
                "W......W....W", // y4   骷髏 (10,4) 貴但可選
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

                // 三扇門、兩把鑰匙——主門必開，口袋只能選一個
                new FloorEntity("F01_d01", EntityType.Door, new GridPos(6, 6), doorTier: KeyTier.Yellow),
                new FloorEntity("F01_d02", EntityType.Door, new GridPos(2, 9), doorTier: KeyTier.Yellow),
                new FloorEntity("F01_d03", EntityType.Door, new GridPos(10, 9), doorTier: KeyTier.Yellow),

                new FloorEntity("F01_i01", EntityType.Item, new GridPos(3, 11), @ref: "key_yellow"),
                new FloorEntity("F01_i02", EntityType.Item, new GridPos(9, 11), @ref: "key_yellow"),
                new FloorEntity("F01_i03", EntityType.Item, new GridPos(1, 11), @ref: "potion_s"),
                new FloorEntity("F01_i04", EntityType.Item, new GridPos(11, 11), @ref: "potion_s"),
                // 四角探索獎勵——原版的密度來自這種「走到底就有東西」
                new FloorEntity("F01_i05", EntityType.Item, new GridPos(1, 1), @ref: "gem_atk"),
                new FloorEntity("F01_i06", EntityType.Item, new GridPos(11, 3), @ref: "potion_s"),

                // 主路上的怪都可繞過——第一層不強迫戰鬥
                new FloorEntity("F01_m01", EntityType.Monster, new GridPos(6, 9), @ref: "slime_green"),
                new FloorEntity("F01_m02", EntityType.Monster, new GridPos(4, 3), @ref: "slime_green"),
                new FloorEntity("F01_m05", EntityType.Monster, new GridPos(8, 3), @ref: "slime_red"),
                // 骷髏：打得動（防 0）但一刀 60——「打得動不代表該打」的活教材
                new FloorEntity("F01_m06", EntityType.Monster, new GridPos(10, 4), @ref: "skel_gray"),
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
        /// 1F 怪物（數值取自原版總表，見 docs/reference-classic-mt.md）。
        /// 原版格式 `血 攻 防 敏 exp gold`；敏捷是迴避率，我們 D1 禁機率故不採用。
        /// </summary>
        public static Dictionary<string, MonsterDefinition> Monsters() => new Dictionary<string, MonsterDefinition>
        {
            // 綠史萊姆 40/18/1 exp1 gold1 —— 損 32，最便宜的練習對象
            ["slime_green"] = new MonsterDefinition("slime_green", 18, 1, 40, TraitSet.None, 1, 1, false, "綠史萊姆"),
            // 蝙蝠 55/32/2 exp1 gold3 —— 損 132，划算的交易（換 200 血瓶淨賺 68）
            ["bat_cave"] = new MonsterDefinition("bat_cave", 32, 2, 55, TraitSet.None, 3, 1, false, "蝙蝠"),
            // 黑史萊姆 80/37/9 exp1 gold5 —— 防 9 只低攻擊 1 點，每輪只削 1 血：
            // 損 2133 遠超 1000 血 → D13 判定為牆。攻擊力上去後才打得起，回頭殺的鉤子。
            ["slime_black"] = new MonsterDefinition("slime_black", 37, 9, 80, TraitSet.None, 5, 1, false, "黑史萊姆"),
            // 紅史萊姆 50/20/4 exp1 gold2 —— 綠色的進階版，讓玩家比較兩個預覽數字
            ["slime_red"] = new MonsterDefinition("slime_red", 20, 4, 50, TraitSet.None, 2, 1, false, "紅史萊姆"),
            // 骷髏 95/70/0 exp1 gold5 —— 防禦是零，一刀卻 60：打得動不代表該打
            ["skel_gray"] = new MonsterDefinition("skel_gray", 70, 0, 95, TraitSet.None, 5, 1, false, "骷髏"),
        };

        /// <summary>1F 用到的道具（鏡像 data/items.csv）。</summary>
        public static Dictionary<string, ItemDefinition> Items() => new Dictionary<string, ItemDefinition>
        {
            ["key_yellow"] = new ItemDefinition("key_yellow", ItemCategory.Key, KeyTier.Yellow),
            ["potion_s"] = new ItemDefinition("potion_s", ItemCategory.Potion, healHp: 200), // 原版商人：15 金幣 200 血
            ["gem_atk"] = new ItemDefinition("gem_atk", ItemCategory.Gem, atkBonus: 2),
        };
    }
}
