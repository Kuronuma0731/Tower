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

        /// <summary>顯示名（monsters.csv 的 name_zh）。</summary>
        public string NameZh { get; }

        /// <summary>
        /// 敏捷＝迴避率百分比（0–50，對應原版的敏欄）。D15：落空次數算死、順序隨機——
        /// 玩家看得到閃避，但總傷害仍是定值，預覽與驗證器不受影響。
        /// </summary>
        public int Agility { get; }

        public MonsterDefinition(
            string id, int atk, int def, int hp,
            TraitSet traits, int goldDrop, int expDrop, bool isGuardian,
            string nameZh = "", int agility = 0)
        {
            Id = id;
            NameZh = nameZh;
            Agility = agility;
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
