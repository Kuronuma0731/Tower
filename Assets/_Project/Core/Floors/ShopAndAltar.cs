using System.Collections.Generic;

namespace Tower.Core.Floors
{
    /// <summary>商店一個品項：遞增價（第 n 次 = BasePrice + (n−1)×PriceStep），計數鍵 "shop_id:item_id"。</summary>
    public sealed class ShopOffer
    {
        public string ItemId { get; }
        public int BasePrice { get; }
        public int PriceStep { get; }

        public ShopOffer(string itemId, int basePrice, int priceStep)
        {
            ItemId = itemId;
            BasePrice = basePrice;
            PriceStep = priceStep;
        }

        public int PriceAt(int purchaseCount) => BasePrice + purchaseCount * PriceStep;
    }

    public sealed class ShopDefinition
    {
        public string Id { get; }
        public IReadOnlyList<ShopOffer> Offers { get; }

        public ShopDefinition(string id, IReadOnlyList<ShopOffer> offers)
        {
            Id = id;
            Offers = offers;
        }
    }

    public enum AltarStat
    {
        Atk,
        Def,
        Hp,
    }

    /// <summary>祭壇一個兌換項：遞增價（D8 衍生規則），各屬性獨立計數，鍵 "altar_id:stat"。</summary>
    public sealed class AltarOffer
    {
        public AltarStat Stat { get; }
        public int ExpCost { get; }
        public int Gain { get; }
        public int CostStep { get; }

        public AltarOffer(AltarStat stat, int expCost, int gain, int costStep)
        {
            Stat = stat;
            ExpCost = expCost;
            Gain = gain;
            CostStep = costStep;
        }

        public int CostAt(int exchangeCount) => ExpCost + exchangeCount * CostStep;
    }

    public sealed class AltarDefinition
    {
        public string Id { get; }
        public IReadOnlyList<AltarOffer> Offers { get; }

        public AltarDefinition(string id, IReadOnlyList<AltarOffer> offers)
        {
            Id = id;
            Offers = offers;
        }
    }
}
