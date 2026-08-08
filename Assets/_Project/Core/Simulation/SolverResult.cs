namespace Tower.Core.Simulation
{
    public enum SolverStatus
    {
        /// <summary>存在一條抵達出口的路徑。</summary>
        Solvable,

        /// <summary>窮盡搜索後不存在路徑。</summary>
        Unsolvable,

        /// <summary>超過節點上限，無法斷定——樓層狀態空間過大，設計端應縮減（見 CONTEXT 驗證器詞條）。</summary>
        Inconclusive,
    }

    /// <summary>每層驗證的結果。</summary>
    public readonly struct SolverResult
    {
        public readonly SolverStatus Status;

        /// <summary>Solvable 時：已知抵達出口的最佳剩餘 HP（緊繃度指標的原料）。</summary>
        public readonly int BestExitHp;

        /// <summary>探索的狀態節點數（效能觀測用）。</summary>
        public readonly int NodesExplored;

        public SolverResult(SolverStatus status, int bestExitHp, int nodesExplored)
        {
            Status = status;
            BestExitHp = bestExitHp;
            NodesExplored = nodesExplored;
        }
    }
}
