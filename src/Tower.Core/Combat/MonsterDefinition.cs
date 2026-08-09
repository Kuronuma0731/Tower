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

        /// <summary>
        /// 怪物手冊上的一句話（monsters.csv 的 bestiary_note）。
        /// 不是風味文字——它負責把**這隻怪的取捨**講給玩家聽（「打得動不代表該打」），
        /// 這是 D1 純碰撞戰下傳達設計意圖的主要管道。玩家可見字串，故來自資料表。
        /// </summary>
        public string BestiaryNote { get; }

        public MonsterDefinition(
            string id, int atk, int def, int hp,
            TraitSet traits, int goldDrop, int expDrop, bool isGuardian,
            string nameZh = "", int agility = 0, string bestiaryNote = "")
        {
            Id = id;
            NameZh = nameZh;
            BestiaryNote = bestiaryNote;
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
