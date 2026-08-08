using System.Collections.Generic;
using System.IO;
using Tower.Core.Data;
using UnityEngine;

namespace Tower.Game
{
    /// <summary>
    /// 玩家可見文字的唯一來源（鐵則：程式碼禁止寫死字串）。
    /// `ui-strings.csv` 是 id→文字；`dialogues.csv` 同 id 多列＝依序播放的對話序列。
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

        public static TextBank LoadFrom(string dataDir)
        {
            var bank = new TextBank();

            foreach (var c in Csv.Rows(File.ReadAllText(Path.Combine(dataDir, "ui-strings.csv")), 2))
                if (c[0].Length > 0) bank._strings[c[0].Trim()] = c[1];

            foreach (var c in Csv.Rows(File.ReadAllText(Path.Combine(dataDir, "dialogues.csv")), 3))
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
