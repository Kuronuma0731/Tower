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

        /// <summary>擊殺後再生的目標怪 id；null＝不再生。</summary>
        private readonly string _reviveInto;

        public CollisionBattleCommand(string eid, in CollisionOutcome outcome, MonsterDefinition monster)
        {
            _eid = eid;
            _hpLoss = outcome.ExpectedLoss;
            _goldDrop = monster.GoldDrop;
            _expDrop = monster.ExpDrop;
            _reviveInto = monster.Traits.HasFlag(TraitSet.ReviveAsWeaker) ? monster.ReviveInto : null;
        }

        /// <summary>供存檔序列化讀取（CommandCodec）。</summary>
        public string Eid => _eid;
        public int HpLoss => _hpLoss;
        public int GoldDrop => _goldDrop;
        public int ExpDrop => _expDrop;
        public string ReviveInto => _reviveInto;

        /// <summary>從已存的差值重建（載入存檔）。</summary>
        public static CollisionBattleCommand FromDeltas(string eid, int hpLoss, int gold, int exp,
                                                        string reviveInto = null)
            => new CollisionBattleCommand(eid, hpLoss, gold, exp, reviveInto);

        private CollisionBattleCommand(string eid, int hpLoss, int gold, int exp, string reviveInto)
        {
            _eid = eid;
            _hpLoss = hpLoss;
            _goldDrop = gold;
            _expDrop = exp;
            _reviveInto = reviveInto;
        }

        public void Apply(GameState state)
        {
            state.Hp -= _hpLoss;
            state.Gold += _goldDrop;
            state.Exp += _expDrop;
            state.ConsumedEids.Add(_eid);

            // 擊殺後再生：格子不清空，換成較弱的同系怪站在原地。
            // 記在 GameState 而不是改樓層資料——樓層是靜態的，變動一律進狀態（存檔才帶得走）。
            state.RevivedMonsters.TryGetValue(_eid, out _previousRevive);
            if (_reviveInto != null) state.RevivedMonsters[_eid] = _reviveInto;
            else state.RevivedMonsters.Remove(_eid);   // 打的是再生出來的那隻 → 這次真的清空
        }

        public void Undo(GameState state)
        {
            state.Hp += _hpLoss;
            state.Gold -= _goldDrop;
            state.Exp -= _expDrop;
            state.ConsumedEids.Remove(_eid);

            if (_previousRevive != null) state.RevivedMonsters[_eid] = _previousRevive;
            else state.RevivedMonsters.Remove(_eid);
        }

        /// <summary>Apply 前這格的再生狀態，供 Undo 精確還原（可能是 null）。</summary>
        private string _previousRevive;
    }
}
