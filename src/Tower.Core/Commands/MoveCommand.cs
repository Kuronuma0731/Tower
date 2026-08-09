using System;
using Tower.Core.Grid;

namespace Tower.Core.Commands
{
    /// <summary>
    /// 一步移動。回溯的最小單位。
    ///
    /// **中毒時走一步要扣血（D17）**——這是全遊戲唯一讓移動有代價的地方。
    /// 扣多少記在指令裡而不是重算：回溯必須還原「當時實際扣掉的量」，
    /// 而毒傷在 1 血止住（D13），所以最後一步扣的可能少於毒的強度。
    /// </summary>
    public sealed class MoveCommand : IGameCommand
    {
        private readonly GridPos _from;
        private readonly GridPos _to;

        /// <summary>供存檔序列化讀取（CommandCodec）。</summary>
        public GridPos From => _from;
        public GridPos To => _to;

        /// <summary>這一步實際扣掉的毒傷（0 = 沒中毒）。</summary>
        public int PoisonPaid { get; private set; }

        public MoveCommand(GridPos from, GridPos to, int poisonPaid = 0)
        {
            _from = from;
            _to = to;
            PoisonPaid = poisonPaid;
        }

        public void Apply(GameState state)
        {
            state.Position = _to;

            if (state.PoisonPerStep > 0)
            {
                // D13：毒不會致死。止在 1 血——代價是中毒成為有上限的威脅，
                // 但死亡管線不存在的收益遠大於此（見 D17 已接受代價第 3 項）。
                PoisonPaid = Math.Min(state.PoisonPerStep, Math.Max(0, state.Hp - 1));
                state.Hp -= PoisonPaid;
            }
            else PoisonPaid = 0;
        }

        public void Undo(GameState state)
        {
            state.Position = _from;
            state.Hp += PoisonPaid;
        }
    }
}
