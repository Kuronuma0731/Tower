namespace Tower.Core.Commands
{
    /// <summary>
    /// 所有狀態變更的唯一形式（D7 硬性要求，第一天就在，事後補裝等於重寫）。
    /// Apply 與 Undo 必須互為精確逆操作：Apply 後 Undo，GameState 與原狀態完全相等。
    /// 快照重放 = 依序 Apply；回溯 = 從尾端 Undo（消耗回溯道具的檢查在遊戲層，Core 只提供機制）。
    /// </summary>
    public interface IGameCommand
    {
        void Apply(GameState state);
        void Undo(GameState state);
    }
}
