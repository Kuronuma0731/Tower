namespace Tower.Core.Combat
{
    /// <summary>結算輸入用的玩家屬性快照。可變狀態在 GameState，這裡只是唯讀視圖。</summary>
    public readonly struct PlayerStats
    {
        public readonly int Atk;
        public readonly int Def;

        public PlayerStats(int atk, int def)
        {
            Atk = atk;
            Def = def;
        }
    }
}
