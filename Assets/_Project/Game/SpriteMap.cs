using System.Collections.Generic;
using Tower.Core.Floors;

namespace Tower.Game
{
    /// <summary>
    /// 遊戲概念 → sprite 檔名的**唯一**對照表。
    ///
    /// 換素材時只改這裡：遊戲邏輯只認左邊的概念 id，永遠不碰檔名。
    /// D14 的素材為第三方授權品，未來很可能整批替換——此表就是那個接縫。
    /// 檔案位於 StreamingAssets/sprites/，切片來源見 tools/slice-sheets.ps1。
    /// </summary>
    public static class SpriteMap
    {
        public const int PixelsPerCell = 32; // D14：素材格子尺寸

        public static readonly Dictionary<string, string> Monster = new Dictionary<string, string>
        {
            ["slime_green"] = "mon_slime_g",
            ["slime_red"] = "mon_slime_r",
            ["slime_blue"] = "mon_slime_b",
            ["bat_cave"] = "mon_bat_cave",
            ["rat_giant"] = "mon_rat_giant",
            ["bandit"] = "mon_bandit",
            ["skel_gray"] = "mon_skel_gray",
            ["skel_soldier"] = "mon_skel_soldier",
            ["wasp_striker"] = "mon_wasp_striker",
            ["duelist_twin"] = "mon_duelist_twin",
            ["mage_void"] = "mon_mage_void",
            ["ghost_pale"] = "mon_ghost_pale",
            ["vampbat_king"] = "mon_vampbat_king",
            ["gatekeeper_biped"] = "boss_gate_01",
            ["warden_10"] = "boss_warden_10",
        };

        public static readonly Dictionary<string, string> Item = new Dictionary<string, string>
        {
            ["key_yellow"] = "item_key_y",
            ["key_blue"] = "item_key_b",
            ["key_red"] = "item_key_r",
            ["potion_s"] = "item_potion_s",
            ["potion_l"] = "item_potion_l",
            ["gem_atk"] = "item_gem_atk",
            ["gem_def"] = "item_gem_def",
            ["hourglass"] = "item_hourglass",
        };

        // 地形配色（懷舊配色 A）：地板＝灰石磚 `Wall_r0_c3`、牆＝紫藍石 `地形_r0_c3`
        // ——刻意複製原版的紫框灰地。換配色只要換這兩個檔，見 docs/art-assets.md
        public const string TileFloor = "tile_floor";
        public const string TileWall = "tile_wall";
        public const string Shop = "ent_shop";
        public const string Altar = "ent_altar";
        public const string Switch = "ent_switch";
        public const string Npc = "npc_guard_old";

        public static string Door(KeyTier tier) => tier switch
        {
            KeyTier.Yellow => "ent_door_y",
            KeyTier.Blue => "ent_door_b",
            _ => "ent_door_r",
        };

        public static string Stairs(StairsDirection dir)
            => dir == StairsDirection.Up ? "ent_stairs_up" : "ent_stairs_down";

        /// <summary>主角行走圖：4 方向 × 4 幀（RPG Maker 列序：0 下 / 1 左 / 2 右 / 3 上）。</summary>
        public static string Hero(int dirRow, int frame) => $"hero_d{dirRow}_f{frame}";

        public const int HeroDirDown = 0;
        public const int HeroDirLeft = 1;
        public const int HeroDirRight = 2;
        public const int HeroDirUp = 3;
        public const int HeroFrames = 4;

        public static string For(FloorEntity e) => e.Type switch
        {
            EntityType.Door => Door(e.DoorTier),
            EntityType.Stairs => Stairs(e.Stairs),
            EntityType.Npc => Npc,
            EntityType.Shop => Shop,
            EntityType.Altar => Altar,
            EntityType.Switch => Switch,
            EntityType.Monster => Monster.TryGetValue(e.Ref, out var m) ? m : null,
            EntityType.Item => Item.TryGetValue(e.Ref, out var i) ? i : null,
            _ => null,
        };
    }
}
