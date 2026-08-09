using System;
using System.Collections.Generic;

namespace Tower.Core.Data
{
    /// <summary>
    /// 極簡 CSV 讀取器。刻意不支援引號跳脫——本專案的表格用全形逗號寫中文，
    /// 且**備註類長文一律排在最後一欄**，故最後一欄之後的所有逗號都併回該欄。
    /// </summary>
    public static class Csv
    {
        /// <summary>切一列；欄數超過 <paramref name="columns"/> 時，多出來的併入最後一欄。</summary>
        public static string[] SplitRow(string line, int columns)
        {
            var raw = line.Split(',');
            if (raw.Length <= columns)
            {
                if (raw.Length == columns) return raw;
                var padded = new string[columns];
                Array.Copy(raw, padded, raw.Length);
                for (int i = raw.Length; i < columns; i++) padded[i] = string.Empty;
                return padded;
            }
            var cells = new string[columns];
            Array.Copy(raw, cells, columns - 1);
            cells[columns - 1] = string.Join(",", raw, columns - 1, raw.Length - columns + 1);
            return cells;
        }

        /// <summary>逐列讀取（跳過表頭與空白列）。</summary>
        public static IEnumerable<string[]> Rows(string csvText, int columns)
        {
            if (string.IsNullOrWhiteSpace(csvText)) yield break;
            var lines = csvText.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int i = 1; i < lines.Length; i++) // [0] 是表頭
            {
                var line = lines[i].Trim();
                if (line.Length == 0) continue;
                yield return SplitRow(line, columns);
            }
        }

        public static int Int(string s, int fallback = 0)
            => int.TryParse(s?.Trim(), out var v) ? v : fallback;

        public static bool Bool(string s)
            => string.Equals(s?.Trim(), "TRUE", StringComparison.OrdinalIgnoreCase);
    }
}
