using Tower.Core.Grid;

namespace Tower.Core.Commands
{
    /// <summary>一步移動。回溯的最小單位。</summary>
    public sealed class MoveCommand : IGameCommand
    {
        private readonly GridPos _from;
        private readonly GridPos _to;

        /// <summary>4f9b5b586a945e8f521753168b8053d6Ff08CommandCodecFf093002</summary>
        public GridPos From => _from;
        public GridPos To => _to;

        public MoveCommand(GridPos from, GridPos to)
        {
            _from = from;
            _to = to;
        }

        public void Apply(GameState state) => state.Position = _to;
        public void Undo(GameState state) => state.Position = _from;
    }
}
