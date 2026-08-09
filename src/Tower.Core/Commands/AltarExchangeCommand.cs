using Tower.Core.Floors;

namespace Tower.Core.Commands
{
    /// <summary>
    /// 祭壇兌換（D8：經驗買屬性）。**沒有自動升級、沒有等級門檻**——兌換時機完全是玩家決策，
    /// 「現在換攻擊壓這隻怪，還是存起來」本身就是資源謎題的一部分。
    ///
    /// **遞增價，各屬性獨立計數**（D8 衍生規則，2026-08-08）：計數鍵 `altar_id:stat`。
    /// 這條規則壓制單屬性極端堆疊，強迫兌換組合思考；代價是玩家心算成本上升，
    /// 所以「兌換後預覽」在 UI 上更重要。
    ///
    /// D7：即點即發，按錯方向就是永久換錯——緩解只允許布局防護，不允許確認彈窗。
    /// </summary>
    public sealed class AltarExchangeCommand : IGameCommand
    {
        private readonly string _countKey;
        private readonly int _expCost;
        private readonly int _dAtk, _dDef, _dHp;

        public string CountKey => _countKey;
        public int ExpCost => _expCost;
        public int DAtk => _dAtk;
        public int DDef => _dDef;
        public int DHp => _dHp;

        public AltarExchangeCommand(string altarId, AltarOffer offer, int cost)
            : this($"{altarId}:{offer.Stat.ToString().ToLowerInvariant()}", cost,
                   offer.Stat == AltarStat.Atk ? offer.Gain : 0,
                   offer.Stat == AltarStat.Def ? offer.Gain : 0,
                   offer.Stat == AltarStat.Hp ? offer.Gain : 0)
        {
        }

        public static AltarExchangeCommand FromDeltas(string countKey, int expCost, int atk, int def, int hp)
            => new AltarExchangeCommand(countKey, expCost, atk, def, hp);

        private AltarExchangeCommand(string countKey, int expCost, int atk, int def, int hp)
        {
            _countKey = countKey;
            _expCost = expCost;
            _dAtk = atk; _dDef = def; _dHp = hp;
        }

        public void Apply(GameState state)
        {
            state.Exp -= _expCost;
            state.Atk += _dAtk; state.Def += _dDef; state.Hp += _dHp;
            state.PurchaseCounts.TryGetValue(_countKey, out int n);
            state.PurchaseCounts[_countKey] = n + 1;
        }

        public void Undo(GameState state)
        {
            state.Exp += _expCost;
            state.Atk -= _dAtk; state.Def -= _dDef; state.Hp -= _dHp;
            state.PurchaseCounts.TryGetValue(_countKey, out int n);
            if (n <= 1) state.PurchaseCounts.Remove(_countKey);
            else state.PurchaseCounts[_countKey] = n - 1;
        }
    }
}
