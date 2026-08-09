using System;
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

        /// <summary>這隻怪的毒強度（每步損血）；0 = 不帶毒。</summary>
        private readonly int _poisonPerStep;
        private int _poisonBefore;

        public CollisionBattleCommand(string eid, in CollisionOutcome outcome, MonsterDefinition monster)
        {
            _eid = eid;
            _hpLoss = outcome.ExpectedLoss;
            _goldDrop = monster.GoldDrop;
            _expDrop = monster.ExpDrop;
            _reviveInto = monster.Traits.HasFlag(TraitSet.ReviveAsWeaker) ? monster.ReviveInto : null;
            _poisonPerStep = monster.Traits.HasFlag(TraitSet.Poison) ? Math.Max(1, monster.TraitValue) : 0;
        }

        /// <summary>供存檔序列化讀取（CommandCodec）。</summary>
        public string Eid => _eid;
        public int HpLoss => _hpLoss;
        public int GoldDrop => _goldDrop;
        public int ExpDrop => _expDrop;
        public string ReviveInto => _reviveInto;
        public int PoisonPerStep => _poisonPerStep;

        /// <summary>從已存的差值重建（載入存檔）。</summary>
        public static CollisionBattleCommand FromDeltas(string eid, int hpLoss, int gold, int exp,
                                                        string reviveInto = null, int poison = 0)
            => new CollisionBattleCommand(eid, hpLoss, gold, exp, reviveInto, poison);

        private CollisionBattleCommand(string eid, int hpLoss, int gold, int exp, string reviveInto, int poison)
        {
            _eid = eid;
            _hpLoss = hpLoss;
            _goldDrop = gold;
            _expDrop = exp;
            _reviveInto = reviveInto;
            _poisonPerStep = poison;
        }

        public void Apply(GameState state)
        {
            state.Hp -= _hpLoss;
            state.Gold += _goldDrop;
            state.Exp += _expDrop;
            state.ConsumedEids.Add(_eid);

            // 擊殺後再生：格子不清空，換成較弱的同系怪站在原地。
            // 記在 GameState 而不是改樓層資料——樓層是靜態的，變動一律進狀態（存檔才帶得走）。
            // 中毒（D17）：**取最大值而非累加**——連踩幾隻毒怪不該疊成瞬間致命，
            // 而 D13 下毒本來就不能致死，累加只會做出一個看起來兇但無害的機制。
            _poisonBefore = state.PoisonPerStep;
            if (_poisonPerStep > 0) state.PoisonPerStep = Math.Max(state.PoisonPerStep, _poisonPerStep);

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

            state.PoisonPerStep = _poisonBefore;

            if (_previousRevive != null) state.RevivedMonsters[_eid] = _previousRevive;
            else state.RevivedMonsters.Remove(_eid);
        }

        /// <summary>Apply 前這格的再生狀態，供 Undo 精確還原（可能是 null）。</summary>
        private string _previousRevive;
    }
}
