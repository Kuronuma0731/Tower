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

        /// <summary>4f9b5b586a945e8f521753168b8053d6Ff08CommandCodecFf093002</summary>
        public string Eid => _eid;
        public int DKeyY => _dKy;
        public int DKeyB => _dKb;
        public int DKeyR => _dKr;
        public int DHp => _dHp;
        public int DAtk => _dAtk;
        public int DDef => _dDef;
        public int DHourglass => _dHourglass;

        /// <summary>
        /// 5f9e5df25b5876845dee503c91cd5efaFf088f0951655b586a94Ff093002**4e0d7d93904e ItemDefinition** 20142014
        /// 820a5b586a9491cd653e7684662f75766642767c751f76844e8bFf0c4e0d662f73fe572876846578503c88683002
        /// </summary>
        public static PickupItemCommand FromDeltas(string eid, int ky, int kb, int kr, int hp, int atk, int def, int hourglass)
            => new PickupItemCommand(eid, ky, kb, kr, hp, atk, def, hourglass);

        private PickupItemCommand(string eid, int ky, int kb, int kr, int hp, int atk, int def, int hourglass)
        {
            _eid = eid;
            _dKy = ky; _dKb = kb; _dKr = kr;
            _dHp = hp; _dAtk = atk; _dDef = def;
            _dHourglass = hourglass;
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
