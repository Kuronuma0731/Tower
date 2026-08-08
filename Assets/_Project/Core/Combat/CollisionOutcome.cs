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

        /// <summary>我方出手次數。</summary>
        public readonly int Rounds;

        public static CollisionOutcome Unwinnable => new CollisionOutcome(false, 0, 0);

        public static CollisionOutcome Win(int expectedLoss, int rounds)
            => new CollisionOutcome(true, expectedLoss, rounds);

        private CollisionOutcome(bool winnable, int expectedLoss, int rounds)
        {
            Winnable = winnable;
            ExpectedLoss = expectedLoss;
            Rounds = rounds;
        }
    }
}
