using System.Collections.Generic;
using Tower.Core.Grid;

namespace Tower.Core.Floors
{
    /// <summary>
    /// F00「塔外」——序章層。原版有而我們原本缺的東西（2026-08-09 實機重玩發現）：
    /// 開場不在塔內，而是城牆內的空地，老兵擋在塔門前，說完話才放行。
    ///
    /// 這一層**不教任何機制**。1F 已經要教門/鑰匙/怪物/血瓶/樓梯/預覽/D13 牆，
    /// 序章再塞東西就超載了。它只做三件事：
    /// - 交代「為什麼要爬這座塔」（劇情五頁，`dlg_f00_prologue`）
    /// - 用老兵的一句話**預先給出 D13 的心智模型**：「打不過的怪，牠就是一堵牆」
    ///   ——這樣玩家在 1F 撞到黑史萊姆時，看到的是規則而不是 bug
    /// - 一隻綠史萊姆守著一瓶血：**第一次讀傷害預覽**，代價低到可以放心試錯
    ///
    /// 座標對齊規約（floor-authoring.md）：F00 的上樓梯 == F01 的下樓梯 == (6,11)。
    /// 我們的世界座標 y 往下遞增，所以 (6,11) 在畫面下緣——版面上讀作「從北面的城門
    /// 進場，穿過中庭，走進南面的塔門」。往下走進塔看似違和，但兩層是**上下疊著**的，
    /// 同一格座標本來就該對齊；把 F01 的樓梯改成 (6,1) 反而會連鎖動到 F02、F03。
    /// </summary>
    public static class F00
    {
        public static readonly GridPos SpawnPos = new GridPos(6, 1);
        /// <summary>塔門＝上樓梯，座標對齊 F01 的下樓梯。</summary>
        public static readonly GridPos StairsUpPos = new GridPos(6, 11);

        public static readonly string[] MonsterRefs = { "slime_green" };
        public static readonly string[] ItemRefs = { "potion_s" };

        public static FloorDefinition Build()
        {
            // 序章要一眼看懂，所以是**空曠中庭**而不是迷宮：
            // 上緣城門通道進場 → 大片中庭（老兵站在裡面，擋不住任何路）→ 下方分岔，
            // 左邊通塔門、右邊是死路口袋（史萊姆守著血瓶）。
            //
            // 主路完全不必戰鬥就能進塔；想要那瓶血才需要撞第一隻怪。
            var rows = new[]
            {
                "WWWWWWWWWWWWW", // y0
                "WWWWWW.WWWWWW", // y1   spawn (6,1)：城門通道
                "WWWWWW.WWWWWW", // y2
                "W...........W", // y3   中庭
                "W...........W", // y4
                "W...........W", // y5   老兵 (3,5)
                "W...........W", // y6
                "W...........W", // y7
                "WWWWW.W.WWWWW", // y8   兩個下行口：x5 通塔門、x7 通口袋
                "W.....W.....W", // y9   右臂是死路：史萊姆 (10,9) 守血瓶 (11,9)
                "W.WWWWWWWWWWW", // y10  只有 x1 能下到塔門那條走廊
                "W...........W", // y11  塔門 (6,11)
                "WWWWWWWWWWWWW", // y12
            };

            var entities = new List<FloorEntity>
            {
                new FloorEntity("F00_sp1", EntityType.Spawn, SpawnPos),
                new FloorEntity("F00_su1", EntityType.Stairs, StairsUpPos, stairs: StairsDirection.Up),

                // 老兵站在空曠中庭裡：一定看得到，但不擋任何路（NPC 在我們的規則裡是障礙物）
                new FloorEntity("F00_n01", EntityType.Npc, new GridPos(3, 5), dialogueId: "dlg_f00_veteran"),

                // 第一隻怪＋第一瓶血：讀預覽的練習題，損 32 便宜到可以放心撞。
                // 擺在死路口袋底部，不打就拿不到——原版的規矩，序章就先立起來
                new FloorEntity("F00_m01", EntityType.Monster, new GridPos(10, 9), @ref: "slime_green"),
                new FloorEntity("F00_i01", EntityType.Item, new GridPos(11, 9), @ref: "potion_s"),
            };

            return new FloorDefinition("F00", FloorGrid.Parse(rows), entities, nameZh: "塔外");
        }
    }
}
