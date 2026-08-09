using Tower.Core.Floors;

namespace Tower.Core.Commands
{
    /// <summary>
    /// 商店購買（D8：金幣買道具）。**遞增價**——同一品項買第 n 次要 `base + (n−1)×step`，
    /// 計數鍵 `shop_id:item_id` 存在 <see cref="GameState.PurchaseCounts"/>。
    ///
    /// 存的是**當時的價格與效果差值**，不是 ShopOffer 的參照：舊存檔重放的是當時發生的事，
    /// 日後調整價格不會讓已存檔的帳目錯亂（與其他指令同一原則）。
    ///
    /// D7：即點即發，沒有二段確認——收費的閘門在遊戲層，Core 只提供機制。
    /// </summary>
    public sealed class PurchaseCommand : IGameCommand
    {
        private readonly string _countKey;
        private readonly int _price;
        private readonly int _dKy, _dKb, _dKr, _dHp, _dAtk, _dDef, _dHourglass;

        public string CountKey => _countKey;
        public int Price => _price;
        public int DKeyY => _dKy;
        public int DKeyB => _dKb;
        public int DKeyR => _dKr;
        public int DHp => _dHp;
        public int DAtk => _dAtk;
        public int DDef => _dDef;
        public int DHourglass => _dHourglass;

        public PurchaseCommand(string shopId, ItemDefinition item, int price)
            : this($"{shopId}:{item.Id}", price,
                   item.Category == ItemCategory.Key && item.KeyTier == KeyTier.Yellow ? 1 : 0,
                   item.Category == ItemCategory.Key && item.KeyTier == KeyTier.Blue ? 1 : 0,
                   item.Category == ItemCategory.Key && item.KeyTier == KeyTier.Red ? 1 : 0,
                   item.HealHp, item.AtkBonus, item.DefBonus,
                   item.Category == ItemCategory.Undo ? 1 : 0)
        {
        }

        public static PurchaseCommand FromDeltas(string countKey, int price,
            int ky, int kb, int kr, int hp, int atk, int def, int hourglass)
            => new PurchaseCommand(countKey, price, ky, kb, kr, hp, atk, def, hourglass);

        private PurchaseCommand(string countKey, int price,
            int ky, int kb, int kr, int hp, int atk, int def, int hourglass)
        {
            _countKey = countKey;
            _price = price;
            _dKy = ky; _dKb = kb; _dKr = kr;
            _dHp = hp; _dAtk = atk; _dDef = def;
            _dHourglass = hourglass;
        }

        public void Apply(GameState state)
        {
            state.Gold -= _price;
            state.KeysYellow += _dKy; state.KeysBlue += _dKb; state.KeysRed += _dKr;
            state.Hp += _dHp; state.Atk += _dAtk; state.Def += _dDef;
            state.Hourglasses += _dHourglass;
            state.PurchaseCounts.TryGetValue(_countKey, out int n);
            state.PurchaseCounts[_countKey] = n + 1;
        }

        public void Undo(GameState state)
        {
            state.Gold += _price;
            state.KeysYellow -= _dKy; state.KeysBlue -= _dKb; state.KeysRed -= _dKr;
            state.Hp -= _dHp; state.Atk -= _dAtk; state.Def -= _dDef;
            state.Hourglasses -= _dHourglass;
            state.PurchaseCounts.TryGetValue(_countKey, out int n);
            if (n <= 1) state.PurchaseCounts.Remove(_countKey);
            else state.PurchaseCounts[_countKey] = n - 1;
        }
    }
}
