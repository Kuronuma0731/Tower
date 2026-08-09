using System.Collections.Generic;
using Godot;
using Tower.Core.Data;

namespace Tower.Game
{
    /// <summary>
    /// 玩家可見文字的唯一來源（鐵則：程式碼禁止寫死字串）。
    /// `ui-strings.csv` 是 id→文字；`dialogues.csv` 同 id 多列＝依序播放的對話序列。
    ///
    /// 用 Godot 的 FileAccess 讀 res://，所以打包後照樣讀得到——**而且沒有第二份副本**。
    /// （Unity 時期要把 data/ 複製一份到 StreamingAssets，那份副本漂移過，害序章劇情
    /// 整段不顯示。res:// 直接指向專案內的 data/，這個病從根上消失了。）
    /// </summary>
    public sealed class TextBank
    {
        private readonly Dictionary<string, string> _strings = new Dictionary<string, string>();
        private readonly Dictionary<string, List<Line>> _dialogues = new Dictionary<string, List<Line>>();

        public readonly struct Line
        {
            public readonly string Speaker;
            public readonly string Text;
            public Line(string speaker, string text) { Speaker = speaker; Text = text; }
        }

        public static string ReadCsv(string name) => Godot.FileAccess.GetFileAsString($"res://data/{name}");

        public static TextBank Load()
        {
            var bank = new TextBank();

            foreach (var c in Csv.Rows(ReadCsv("ui-strings.csv"), 2))
                if (c[0].Length > 0) bank._strings[c[0].Trim()] = c[1];

            foreach (var c in Csv.Rows(ReadCsv("dialogues.csv"), 3))
            {
                string id = c[0].Trim();
                if (id.Length == 0) continue;
                if (!bank._dialogues.TryGetValue(id, out var seq))
                    bank._dialogues[id] = seq = new List<Line>();
                seq.Add(new Line(c[1].Trim(), c[2]));
            }
            return bank;
        }

        /// <summary>查字串；查不到就回 id 本身——缺字在畫面上看得見，不會靜默變空白。</summary>
        public string this[string id] => _strings.TryGetValue(id, out var v) ? v : id;

        public bool TryGetDialogue(string id, out List<Line> lines) => _dialogues.TryGetValue(id, out lines);
    }
}
