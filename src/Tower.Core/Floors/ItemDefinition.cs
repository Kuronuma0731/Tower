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

        public ItemDefinition(
            string id, ItemCategory category,
            KeyTier keyTier = KeyTier.Yellow,
            int healHp = 0, int atkBonus = 0, int defBonus = 0, int undoSteps = 0)
        {
            Id = id;
            Category = category;
            KeyTier = keyTier;
            HealHp = healHp;
            AtkBonus = atkBonus;
            DefBonus = defBonus;
            UndoSteps = undoSteps;
        }
    }
}
