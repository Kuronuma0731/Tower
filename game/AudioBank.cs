using System.Collections.Generic;
using Godot;

namespace Tower.Game
{
    /// <summary>
    /// 音效：**遊戲事件 → 檔名的唯一對照表**（與 <see cref="SpriteMap"/> 同一個角色）。
    ///
    /// 換音效包時只改這裡；遊戲邏輯只認左邊的事件名，永遠不碰檔名。
    /// 素材未進版控（第三方授權，見 .gitignore），來源與重建方式見 docs/art-assets.md。
    ///
    /// 這一層在 Unity → Godot 遷移時整個掉了，遊戲有一段時間完全無聲——
    /// 音效不在任何驗收的涵蓋範圍內，所以沒有東西會提醒你它不見了。
    /// </summary>
    public sealed class AudioBank
    {
        public const string Attack = "sfx_attack";     // 平 A
        public const string Crit = "sfx_crit";         // 守關怪的重擊——聽覺上就分得出這場的份量
        public const string Magic = "sfx_magic";       // 魔攻（無視防禦）
        public const string Gold = "sfx_gold";
        public const string Item = "sfx_item";
        public const string Door = "sfx_door";
        public const string Stairs = "sfx_stairs";
        public const string Blocked = "sfx_blocked";   // 撞牆／打不動／不夠錢
        public const string Shop = "sfx_shop";

        private const string Dir = "res://assets/audio/";
        private const int Voices = 6;   // 同時可播的音效數；逐回合戰鬥會連續觸發

        private readonly Dictionary<string, AudioStream> _cache = new Dictionary<string, AudioStream>();
        private readonly List<AudioStreamPlayer> _players = new List<AudioStreamPlayer>();
        private int _next;

        public static AudioBank Create(Node parent)
        {
            var bank = new AudioBank();
            for (int i = 0; i < Voices; i++)
            {
                var p = new AudioStreamPlayer { Bus = "Master" };
                parent.AddChild(p);
                bank._players.Add(p);
            }
            return bank;
        }

        /// <summary>
        /// 播一個事件音。找不到檔案就靜靜跳過——素材不進版控，
        /// 別人 clone 下來沒有音檔時遊戲仍然要能跑。
        /// </summary>
        public void Play(string eventName, float volumeDb = 0f)
        {
            var stream = Load(eventName);
            if (stream == null) return;

            // 輪流用不同的 player：逐回合戰鬥會在半秒內連放好幾聲，
            // 單一 player 會把前一聲截斷，聽起來像掉音
            var p = _players[_next];
            _next = (_next + 1) % _players.Count;
            p.Stream = stream;
            p.VolumeDb = volumeDb;
            p.Play();
        }

        private AudioStream Load(string eventName)
        {
            if (_cache.TryGetValue(eventName, out var s)) return s;

            foreach (var ext in new[] { ".mp3", ".ogg", ".wav" })
            {
                string path = Dir + eventName + ext;
                if (!Godot.FileAccess.FileExists(path)) continue;
                s = ResourceLoader.Load<AudioStream>(path);
                _cache[eventName] = s;
                return s;
            }
            _cache[eventName] = null;   // 記住「沒有」，免得每次都去查檔案
            return null;
        }
    }
}
