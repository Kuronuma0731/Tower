namespace Tower.Core.Combat
{
    /// <summary>
    /// 怪物靜態定義（monsters.csv 的 POCO 形式，SO 在 Bootstrap 邊界轉換）。
    /// 怪物沒有執行期狀態——存活與否由 GameState.ConsumedEids 表達。
    /// </summary>
    public sealed class MonsterDefinition
    {
        public string Id { get; }
        public int Atk { get; }
        public int Def { get; }
        public int Hp { get; }
        public TraitSet Traits { get; }
        public int GoldDrop { get; }
        public int ExpDrop { get; }
        public bool IsGuardian { get; }

        public MonsterDefinition(
            string id, int atk, int def, int hp,
            TraitSet traits, int goldDrop, int expDrop, bool isGuardian)
        {
            Id = id;
            Atk = atk;
            Def = def;
            Hp = hp;
            Traits = traits;
            GoldDrop = goldDrop;
            ExpDrop = expDrop;
            IsGuardian = isGuardian;
        }
    }
}
