using Tower.Core.Floors;

namespace Tower.Core.Commands
{
    /// <summary>開一扇門：消耗對應鑰匙、實體入帳。D7：即撞即開，無確認。</summary>
    public sealed class OpenDoorCommand : IGameCommand
    {
        private readonly string _eid;
        private readonly KeyTier _tier;

        public OpenDoorCommand(string eid, KeyTier tier)
        {
            _eid = eid;
            _tier = tier;
        }

        public void Apply(GameState state)
        {
            switch (_tier)
            {
                case KeyTier.Yellow: state.KeysYellow--; break;
                case KeyTier.Blue: state.KeysBlue--; break;
                default: state.KeysRed--; break;
            }
            state.ConsumedEids.Add(_eid);
        }

        public void Undo(GameState state)
        {
            switch (_tier)
            {
                case KeyTier.Yellow: state.KeysYellow++; break;
                case KeyTier.Blue: state.KeysBlue++; break;
                default: state.KeysRed++; break;
            }
            state.ConsumedEids.Remove(_eid);
        }
    }
}
