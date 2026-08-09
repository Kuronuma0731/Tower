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
            "slime_green", "slime_red", "bat_cave",
        };

        public static readonly string[] ItemRefs =
        {
            "key_yellow", "potion_s", "gem_atk", "gem_def",
        };

        public static FloorDefinition Build()
        {
            // 對稱雙翼：中央通道貫穿南北，左右各一個由怪看守的寶石室。
            //
            // ⚠ 這層的第一版有致命佈局錯誤：x1/x11 是從 y1 直通 y5 的邊緣走廊，
            // 玩家沿邊走就繞過了守衛，寶石等於白送（無頭遊玩抓到，驗證器抓不到——
            // 它只問可不可解，而繞道「可解且更好解」）。現在邊緣走廊已切斷，
            // 寶石室只剩守衛那一個入口。
            var rows = new[]
            {
                "WWWWWWWWWWWWW", // y0
                "W...........W", // y1   下樓梯 (6,1)＝入口，橫向頂廊
                "WWWWW.W.WWWWW", // y2   只有 x5/x7 能往下——切斷邊緣走廊
                "W...........W", // y3
                "WWWWW.W.WWWWW", // y4   同上，中央雙井貫穿
                "W...W.W.W...W", // y5   左寶石室 (1..3)／右寶石室 (9..11)，門在 (3,5)/(9,5)
                "WWW.W...W.WWW", // y6   x3/x9 是通往兩翼的垂直井
                "W...W...W...W", // y7   左鑰匙龕 (1,7)／右鑰匙龕 (11,7)
                "WWW.W...W.WWW", // y8
                "W...........W", // y9   主橫廊：左血瓶龕 (1,9)／右血瓶龕 (11,9)
                "WWW.WWWWW.WWW", // y10
                "W...........W", // y11  上樓梯 (6,11)
                "WWWWWWWWWWWWW", // y12
            };

            var entities = new List<FloorEntity>
            {
                new FloorEntity("F02_sd1", EntityType.Stairs, StairsDownPos, stairs: StairsDirection.Down),
                new FloorEntity("F02_su1", EntityType.Stairs, StairsUpPos, stairs: StairsDirection.Up),

                // 寶石室的門——鑰匙來自下方鑰匙龕，兩者不循環（門在 y5、鑰匙在 y7）
                new FloorEntity("F02_d01", EntityType.Door, new GridPos(3, 5), doorTier: KeyTier.Yellow),
                new FloorEntity("F02_d02", EntityType.Door, new GridPos(9, 5), doorTier: KeyTier.Yellow),

                // 兩間寶石室的守衛代價要對得起獎勵：+2 攻在 2F 大約值 150–250 血
                // （往後每場戰鬥省下的量）。骷髏損 540 純屬虧本，遊玩器算出來就不打——
                // 換成蝙蝠(132)與黑史萊姆前一階的紅史萊姆(80)，兩筆都划算但不是白送。
                new FloorEntity("F02_m01", EntityType.Monster, new GridPos(2, 5), @ref: "bat_cave"),
                new FloorEntity("F02_i03", EntityType.Item, new GridPos(1, 5), @ref: "gem_atk"),

                new FloorEntity("F02_m02", EntityType.Monster, new GridPos(10, 5), @ref: "slime_red"),
                new FloorEntity("F02_i04", EntityType.Item, new GridPos(11, 5), @ref: "gem_def"),

                // 鑰匙龕：不打就開不了寶石室的門
                new FloorEntity("F02_m03", EntityType.Monster, new GridPos(2, 7), @ref: "slime_red"),
                new FloorEntity("F02_i01", EntityType.Item, new GridPos(1, 7), @ref: "key_yellow"),
                new FloorEntity("F02_m04", EntityType.Monster, new GridPos(10, 7), @ref: "slime_green"),
                new FloorEntity("F02_i02", EntityType.Item, new GridPos(11, 7), @ref: "key_yellow"),

                // 血瓶龕：原版的規矩——所有值錢的東西都要用血換
                new FloorEntity("F02_m05", EntityType.Monster, new GridPos(2, 9), @ref: "slime_green"),
                new FloorEntity("F02_i05", EntityType.Item, new GridPos(1, 9), @ref: "potion_s"),
                new FloorEntity("F02_m06", EntityType.Monster, new GridPos(10, 9), @ref: "slime_red"),
                new FloorEntity("F02_i06", EntityType.Item, new GridPos(11, 9), @ref: "potion_s"),
            };

            return new FloorDefinition("F02", FloorGrid.Parse(rows), entities, nameZh: "寶石迴廊");
        }
    }
}
