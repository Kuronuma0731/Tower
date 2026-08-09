using Tower.Core.Floors;

namespace Tower.Core.Commands
{
    /// <summary>撿取地圖道具：效果在建構時凍結成差量，Apply/Undo 精確互逆。</summary>
    public sealed class PickupItemCommand : IGameCommand
    {
        private readonly string _eid;
        private readonly int _dKy, _dKb, _dKr, _dHp, _dAtk, _dDef, _dHourglass;

        public PickupItemCommand(string eid, ItemDefinition item)
        {
            _eid = eid;
            switch (item.Category)
            {
                case ItemCategory.Key:
                    switch (item.KeyTier)
                    {
                        case KeyTier.Yellow: _dKy = 1; break;
                        case KeyTier.Blue: _dKb = 1; break;
                        default: _dKr = 1; break;
                    }
                    break;
                case ItemCategory.Potion: _dHp = item.HealHp; break;
                case ItemCategory.Gem: _dAtk = item.AtkBonus; _dDef = item.DefBonus; break;
                case ItemCategory.Undo: _dHourglass = 1; break;
            }
        }

        public void Apply(GameState state)
        {
            state.KeysYellow += _dKy; state.KeysBlue += _dKb; state.KeysRed += _dKr;
            state.Hp += _dHp; state.Atk += _dAtk; state.Def += _dDef;
            state.Hourglasses += _dHourglass;
            state.ConsumedEids.Add(_eid);
        }

        public void Undo(GameState state)
        {
            state.KeysYellow -= _dKy; state.KeysBlue -= _dKb; state.KeysRed -= _dKr;
            state.Hp -= _dHp; state.Atk -= _dAtk; state.Def -= _dDef;
            state.Hourglasses -= _dHourglass;
            state.ConsumedEids.Remove(_eid);
        }
    }
}
