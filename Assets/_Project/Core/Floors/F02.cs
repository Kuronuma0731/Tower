using System.Collections.Generic;
using Tower.Core.Grid;

namespace Tower.Core.Floors
{
    /// <summary>
    /// F02「寶石迴廊」——2F 的主題是**攻/防寶石**（mechanics.md 引入表）：
    /// 「繞路的代價：寶石在血虧的怪後面」。
    ///
    /// 座標對齊規約（floor-authoring.md）：F01 的上樓梯在 (6,1)，故 F02 的**下樓梯
    /// 也在 (6,1)**——玩家上樓後就站在回程的樓梯上。
    ///
    /// 教學設計：
    /// - 左翼**攻擊寶石**由骷髏（防 0 攻 70，損 540）看守——貴，但買到的是「以後每一場都更便宜」
    /// - 右翼**防禦寶石**由蝙蝠（損 132）看守——便宜，但防禦對高攻怪才顯價值
    /// - 兩顆都拿得到（不像 1F 的二選一），這層教的是**先後順序**：先拿攻擊寶石，
    ///   蝙蝠就從 132 掉到 110；反過來則貴 22 血。驗證器會證明這件事。
    /// - 上樓梯在 (6,11)，與下樓梯遙遙相對，強迫玩家穿過整層
    /// </summary>
    public static class F02
    {
        /// <summary>下樓梯＝進入本層的位置（座標對齊 F01 的上樓梯）。</summary>
        public static readonly GridPos StairsDownPos = new GridPos(6, 1);
        public static readonly GridPos StairsUpPos = new GridPos(6, 11);

        public static readonly string[] MonsterRefs =
        {
            "slime_green", "slime_red", "bat_cave", "skel_gray", "slime_black",
        };

        public static readonly string[] ItemRefs =
        {
            "key_yellow", "potion_s", "gem_atk", "gem_def",
        };

        public static FloorDefinition Build()
        {
            // 對稱雙翼：中央通道貫穿南北，左右各一個由怪看守的寶石室。
            // 與 F01 的「開放大廳＋壁龕」不同語法，讓兩層一眼分得出來。
            var rows = new[]
            {
                "WWWWWWWWWWWWW", // y0
                "W.....W.....W", // y1   下樓梯 (6,1)＝入口
                "W.WWW.W.WWW.W", // y2
                "W.W.......W.W", // y3   左寶石室門 (3,3)／右寶石室門 (9,3)
                "W.W.WWWWW.W.W", // y4
                "W...W...W...W", // y5   左室 (1..3,5)／右室 (9..11,5)
                "WWW.W...W.WWW", // y6
                "W.......W...W", // y7
                "W.WWWWW.W.W.W", // y8
                "W.W...W...W.W", // y9   黃鑰匙 (3,9)
                "W.W.W.WWW.W.W", // y10
                "W...W.......W", // y11  上樓梯 (6,11)
                "WWWWWWWWWWWWW", // y12
            };

            var entities = new List<FloorEntity>
            {
                new FloorEntity("F02_sd1", EntityType.Stairs, StairsDownPos, stairs: StairsDirection.Down),
                new FloorEntity("F02_su1", EntityType.Stairs, StairsUpPos, stairs: StairsDirection.Up),

                // 兩扇門、兩把鑰匙——這層不搞二選一，改教先後順序
                new FloorEntity("F02_d01", EntityType.Door, new GridPos(3, 3), doorTier: KeyTier.Yellow),
                new FloorEntity("F02_d02", EntityType.Door, new GridPos(9, 3), doorTier: KeyTier.Yellow),
                new FloorEntity("F02_i01", EntityType.Item, new GridPos(3, 9), @ref: "key_yellow"),
                new FloorEntity("F02_i02", EntityType.Item, new GridPos(9, 9), @ref: "key_yellow"),

                // 左翼：骷髏守攻擊寶石——貴，但買的是往後每一場的折扣
                new FloorEntity("F02_m01", EntityType.Monster, new GridPos(2, 5), @ref: "skel_gray"),
                new FloorEntity("F02_i03", EntityType.Item, new GridPos(1, 5), @ref: "gem_atk"),

                // 右翼：蝙蝠守防禦寶石——便宜
                new FloorEntity("F02_m02", EntityType.Monster, new GridPos(10, 5), @ref: "bat_cave"),
                new FloorEntity("F02_i04", EntityType.Item, new GridPos(11, 5), @ref: "gem_def"),

                // 主路上的可繞過怪
                new FloorEntity("F02_m03", EntityType.Monster, new GridPos(6, 7), @ref: "slime_red"),
                new FloorEntity("F02_m04", EntityType.Monster, new GridPos(4, 9), @ref: "slime_green"),

                // 血瓶：走到底的探索獎勵
                new FloorEntity("F02_i05", EntityType.Item, new GridPos(1, 9), @ref: "potion_s"),
                new FloorEntity("F02_i06", EntityType.Item, new GridPos(11, 9), @ref: "potion_s"),
            };

            return new FloorDefinition("F02", FloorGrid.Parse(rows), entities, nameZh: "寶石迴廊");
        }
    }
}
