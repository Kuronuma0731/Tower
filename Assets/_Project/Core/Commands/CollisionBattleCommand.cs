using Tower.Core.Combat;

namespace Tower.Core.Commands
{
    /// <summary>
    /// 一場碰撞戰 = 一個 command（回溯一步就是回溯一整場戰鬥）。
    /// 結算數字在建構時定死（確定性），Apply/Undo 才能互為精確逆操作。
    /// D13：Winnable == false 的戰鬥不會產生本 command——致死格根本進不去。
    /// </summary>
    public sealed class CollisionBattleCommand : IGameCommand
    {
        private readonly string _eid;
        private readonly int _hpLoss;
        private readonly int _goldDrop;
        private readonly int _expDrop;

        public CollisionBattleCommand(string eid, in CollisionOutcome outcome, MonsterDefinition monster)
        {
            _eid = eid;
            _hpLoss = outcome.ExpectedLoss;
            _goldDrop = monster.GoldDrop;
            _expDrop = monster.ExpDrop;
        }

        public void Apply(GameState state)
        {
            state.Hp -= _hpLoss;
            state.Gold += _goldDrop;
            state.Exp += _expDrop;
            state.ConsumedEids.Add(_eid);
        }

        public void Undo(GameState state)
        {
            state.Hp += _hpLoss;
            state.Gold -= _goldDrop;
            state.Exp -= _expDrop;
            state.ConsumedEids.Remove(_eid);
        }
    }
}
