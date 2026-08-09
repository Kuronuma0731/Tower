using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Tower.Core.Commands;
using Tower.Core.Grid;

namespace Tower.Core.Save
{
    /// <summary>
    /// 存檔的可序列化形狀。刻意用扁平的 POCO 而不是直接序列化 <see cref="GameState"/>——
    /// 遊戲型別會隨開發變動，存檔格式必須能獨立演進（版本號在 <see cref="SaveGame.FormatVersion"/>）。
    ///
    /// **不存樓層地圖**：那是靜態資料，從 data/floors/*.json 讀。存檔只有數值、旗標與 eid 帳本，
    /// 一層幾 KB。
    /// </summary>
    public sealed class SaveData
    {
        public int Version { get; set; }
        public string CurrentFloor { get; set; }
        public int NextSeq { get; set; }
        public StateData Current { get; set; }
        public Dictionary<string, SnapshotData> Snapshots { get; set; } = new();
        public List<RecordData> Commands { get; set; } = new();

        private static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        public string ToJson() => JsonSerializer.Serialize(this, Options);
        public static SaveData FromJson(string json) => JsonSerializer.Deserialize<SaveData>(json, Options);
    }

    public sealed class SnapshotData
    {
        public int Seq { get; set; }
        public StateData State { get; set; }
    }

    /// <summary><see cref="GameState"/> 的扁平鏡像。</summary>
    public sealed class StateData
    {
        public int Atk { get; set; }
        public int Def { get; set; }
        public int Hp { get; set; }
        public int Gold { get; set; }
        public int Exp { get; set; }
        public int KeysYellow { get; set; }
        public int KeysBlue { get; set; }
        public int KeysRed { get; set; }
        public int Hourglasses { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public List<string> Consumed { get; set; } = new();
        public List<string> SeenMonsters { get; set; } = new();
        public Dictionary<string, int> PurchaseCounts { get; set; } = new();

        public static StateData From(GameState s) => new()
        {
            Atk = s.Atk, Def = s.Def, Hp = s.Hp, Gold = s.Gold, Exp = s.Exp,
            KeysYellow = s.KeysYellow, KeysBlue = s.KeysBlue, KeysRed = s.KeysRed,
            Hourglasses = s.Hourglasses,
            X = s.Position.X, Y = s.Position.Y,
            Consumed = s.ConsumedEids.OrderBy(e => e).ToList(),   // 排序 → 存檔可 diff
            SeenMonsters = s.SeenMonsters.OrderBy(e => e).ToList(),
            PurchaseCounts = new Dictionary<string, int>(s.PurchaseCounts),
        };

        public GameState ToState(string floorId)
        {
            var s = new GameState
            {
                Atk = Atk, Def = Def, Hp = Hp, Gold = Gold, Exp = Exp,
                KeysYellow = KeysYellow, KeysBlue = KeysBlue, KeysRed = KeysRed,
                Hourglasses = Hourglasses,
                CurrentFloor = floorId,
                Position = new GridPos(X, Y),
            };
            foreach (var e in Consumed) s.ConsumedEids.Add(e);
            foreach (var m in SeenMonsters) s.SeenMonsters.Add(m);
            foreach (var kv in PurchaseCounts) s.PurchaseCounts[kv.Key] = kv.Value;
            return s;
        }
    }

    /// <summary><see cref="CommandRecord"/> 的可序列化鏡像（struct 不適合直接當 JSON 目標）。</summary>
    public sealed class RecordData
    {
        public string Kind { get; set; }
        public string Eid { get; set; }
        public int[] Values { get; set; }

        public static RecordData From(in CommandRecord r)
            => new() { Kind = r.Kind, Eid = r.Eid, Values = r.Values };

        public CommandRecord ToRecord() => new(Kind, Eid, Values);
    }
}
