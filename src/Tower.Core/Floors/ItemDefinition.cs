namespace Tower.Core.Floors
{
    public enum ItemCategory
    {
        Key,
        Potion,
        Gem,
        Undo,
    }

    /// <summary>道具靜態定義（items.csv 的 POCO 形式）。稀疏欄位：不相干者為 0。</summary>
    public sealed class ItemDefinition
    {
        public string Id { get; }
        public ItemCategory Category { get; }
        public KeyTier KeyTier { get; }
        public int HealHp { get; }
        public int AtkBonus { get; }
        public int DefBonus { get; }
        public int UndoSteps { get; }

        /// <summary>4e2d6587540dFf08items.csv 7684 name_zhFf0930024f9b95dc53617de88f2f56688207905351776b04986f793a20142014
        /// 73a95bb653ef898b5b574e327684943552474e0d8b8aFf1a9019662f**8cc765998868642c904e4f867684**Ff0c4e0d662f5beb57287a0b5f0f88e13002</summary>
        public string NameZh { get; }

        public ItemDefinition(
            string id, ItemCategory category,
            KeyTier keyTier = KeyTier.Yellow,
            int healHp = 0, int atkBonus = 0, int defBonus = 0, int undoSteps = 0,
            string nameZh = null)
        {
            Id = id;
            NameZh = string.IsNullOrEmpty(nameZh) ? id : nameZh;
            Category = category;
            KeyTier = keyTier;
            HealHp = healHp;
            AtkBonus = atkBonus;
            DefBonus = defBonus;
            UndoSteps = undoSteps;
        }
    }
}
