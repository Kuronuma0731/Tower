using System;
using System.Collections.Generic;
using System.Linq;
using Tower.Core.Commands;

namespace Tower.Core.Save
{
    /// <summary>
    /// 存檔與防軟鎖的機制層（D7）。**只提供機制，不管收費**——回溯要不要花沙漏是遊戲層的事。
    ///
    /// 三件事在這個結構下變成同一件事（見 docs/architecture.md）：
    /// - **回溯**（內層，付費）＝ 從指令流尾端 pop 一個 command 執行 Undo
    /// - **當前狀態** ＝ 入口快照 + 重放指令流
    /// - **退回樓層入口**（外層，免費）＝ 丟掉指令流
    ///
    /// **單一時間軸規則**：退回第 N 層入口時，所有**晚於**該快照的快照一併作廢。
    /// 否則玩家可以退回 5F 重新配置資源，再「跳回」9F 的舊快照，兩條時間線的資源憑空疊加
    /// ——那是套利漏洞，不是防軟鎖。快照帶單調遞增序號，回退即截斷。
    /// </summary>
    public sealed class SaveGame
    {
        /// <summary>存檔格式版本。改變欄位語義時 +1，載入端據此決定能不能吃。</summary>
        public const int FormatVersion = 1;

        private readonly Dictionary<string, Snapshot> _snapshots = new Dictionary<string, Snapshot>();
        private readonly List<IGameCommand> _since = new List<IGameCommand>();
        private int _nextSeq;

        public GameState State { get; private set; }

        /// <summary>入口快照之後累積的指令數（＝可回溯的步數上限）。</summary>
        public int UndoDepth => _since.Count;

        /// <summary>已建立快照、可免費退回的樓層。</summary>
        public IEnumerable<string> VisitedFloors => _snapshots.Keys;

        private readonly struct Snapshot
        {
            public readonly GameState State;
            public readonly int Seq;
            public Snapshot(GameState state, int seq) { State = state; Seq = seq; }
        }

        public SaveGame(GameState initial)
        {
            State = initial ?? throw new ArgumentNullException(nameof(initial));
        }

        /// <summary>
        /// 進入樓層：拍快照、清空指令流（D7 外層防護的建立點）。
        /// 指令流在這裡清空，所以存檔不會無限長。
        /// </summary>
        public void EnterFloor(string floorId)
        {
            State.CurrentFloor = floorId;
            _snapshots[floorId] = new Snapshot(State.Clone(), _nextSeq++);
            _since.Clear();
        }

        /// <summary>套用一個指令並記錄，供回溯。</summary>
        public void Apply(IGameCommand cmd)
        {
            cmd.Apply(State);
            _since.Add(cmd);
        }

        /// <summary>
        /// 回溯一步：pop 尾端指令並 Undo。回傳 false = 已經在樓層入口，沒有東西可退。
        /// **不檢查也不消耗沙漏**——那是遊戲層的閘門（D7：回溯是付費資源）。
        /// </summary>
        public bool UndoOne()
        {
            if (_since.Count == 0) return false;
            var cmd = _since[^1];
            _since.RemoveAt(_since.Count - 1);
            cmd.Undo(State);
            return true;
        }

        /// <summary>
        /// 免費退回某層入口（D7 外層）。同時執行**單一時間軸規則**：
        /// 序號晚於目標快照的樓層快照全部作廢。
        /// </summary>
        public void RevertToFloor(string floorId)
        {
            if (!_snapshots.TryGetValue(floorId, out var target))
                throw new ArgumentException($"沒有 {floorId} 的樓層快照——沒去過的樓層退不回去");

            foreach (var stale in _snapshots.Where(kv => kv.Value.Seq > target.Seq).Select(kv => kv.Key).ToList())
                _snapshots.Remove(stale);

            State = target.State.Clone();
            _since.Clear();
            _nextSeq = target.Seq + 1;
        }

        // ---- 序列化 ----

        public SaveData ToData() => new SaveData
        {
            Version = FormatVersion,
            CurrentFloor = State.CurrentFloor,
            NextSeq = _nextSeq,
            Current = StateData.From(State),
            Snapshots = _snapshots.ToDictionary(
                kv => kv.Key,
                kv => new SnapshotData { Seq = kv.Value.Seq, State = StateData.From(kv.Value.State) }),
            // 不用 method group：RecordData.From 吃 in 參數，LINQ 推不出型別
            Commands = _since.Select(c => RecordData.From(CommandCodec.ToRecord(c))).ToList(),
        };

        public static SaveGame FromData(SaveData data)
        {
            if (data.Version != FormatVersion)
                throw new NotSupportedException($"存檔版本 {data.Version} 與目前的 {FormatVersion} 不符");

            var game = new SaveGame(data.Current.ToState(data.CurrentFloor))
            {
                _nextSeq = data.NextSeq,
            };
            foreach (var kv in data.Snapshots)
                game._snapshots[kv.Key] = new Snapshot(kv.Value.State.ToState(kv.Key), kv.Value.Seq);

            // 指令只還原、不重放——Current 已經是套用後的狀態
            foreach (var r in data.Commands)
                game._since.Add(CommandCodec.FromRecord(r.ToRecord()));

            return game;
        }
    }
}
