namespace Tower.Core.Combat
{
    /// <summary>
    /// 一場碰撞戰的完整結算結果。傷害預覽直接顯示這個物件——
    /// 預覽與實戰是同一個函式的同一個輸出，永遠不會騙人。
    /// </summary>
    public readonly struct CollisionOutcome
    {
        /// <summary>false = 「無法戰勝」：打不動，或吸血怪的淨削減 ≤ 0。D13：此時該格視同牆壁。</summary>
        public readonly bool Winnable;

        /// <summary>預期損血（Winnable 為 false 時無意義，UI 顯示「無法戰勝」）。</summary>
        public readonly int ExpectedLoss;

        /// <summary>我方出手次數（含被閃掉的）。</summary>
        public readonly int Rounds;

        /// <summary>其中被閃掉的次數（D15）。表現層據此決定哪幾下跳 MISS。</summary>
        public readonly int Misses;

        public static CollisionOutcome Unwinnable => new CollisionOutcome(false, 0, 0, 0);

        public static CollisionOutcome Win(int expectedLoss, int rounds, int misses = 0)
            => new CollisionOutcome(true, expectedLoss, rounds, misses);

        private CollisionOutcome(bool winnable, int expectedLoss, int rounds, int misses)
        {
            Winnable = winnable;
            ExpectedLoss = expectedLoss;
            Rounds = rounds;
            Misses = misses;
        }
    }
}
