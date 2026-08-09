using System.Collections.Generic;

namespace Tower.Core.Commands
{
    /// <summary>
    /// 觸發機關：把自己與所有目標實體標成已消耗（目標通常是門，於是門開了）。
    ///
    /// 目標 eid **可以跨層**（`docs/data-schema.md`：「跨層結構用」）——這是本作唯一
    /// 能製造跨層依賴的機制，也因此是**驗證器最需要盯的東西**：跨層 dangling reference
    /// 是這類結構的頭號事故，匯入期必須檢查目標 eid 真的存在。
    ///
    /// 回溯要能精確還原，所以記下「哪些 eid 是**我**加進去的」——
    /// 若目標早就被消耗過（玩家已用鑰匙開了那扇門），撤銷時不該把它一起復活。
    /// </summary>
    public sealed class SwitchCommand : IGameCommand
    {
        private readonly string _eid;
        private readonly string[] _targets;
        private readonly List<string> _actuallyAdded = new List<string>();

        public string Eid => _eid;
        public string[] Targets => _targets;

        public SwitchCommand(string eid, string[] targets)
        {
            _eid = eid;
            _targets = targets ?? System.Array.Empty<string>();
        }

        public void Apply(GameState state)
        {
            _actuallyAdded.Clear();
            if (state.ConsumedEids.Add(_eid)) _actuallyAdded.Add(_eid);
            foreach (var t in _targets)
                if (state.ConsumedEids.Add(t)) _actuallyAdded.Add(t);
        }

        public void Undo(GameState state)
        {
            foreach (var t in _actuallyAdded) state.ConsumedEids.Remove(t);
            _actuallyAdded.Clear();
        }
    }
}
