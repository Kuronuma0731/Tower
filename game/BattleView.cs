using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using Tower.Core.Combat;
using Tower.Core.Commands;

namespace Tower.Game
{
    /// <summary>
    /// 碰撞戰的演出——**照原版逐回合演**（6219_newMT.swf 錄影逐格比對）：
    /// 開 VS 面板 → 每回合雙方各挨一下、體力數字一格一格掉、受擊處放黃色爆閃
    /// 並跳紅色傷害數字向下飄散 → 結算列。
    ///
    /// D1 的一次結算是**規則**不是表現：算術由 <see cref="CombatResolver"/> 一次算完
    /// （預覽與實戰同一輸出，永遠不會騙人），這裡只是把算好的結果攤開來演。
    ///
    /// 從 GameRoot 抽出來的理由：那個類別同時是規則、輸入、演出、對話與 HUD 更新，
    /// 已經是「巨型管理者」。演出是最容易切乾淨的一塊——它只讀結果、不做任何判斷。
    /// </summary>
    public sealed class BattleView
    {
        /// <summary>演出的回合數上限。真打起來動輒數十回合，全演會拖垮節奏。</summary>
        private const int MaxShownRounds = 12;

        private readonly Node _host;
        private readonly ViewFactory _view;
        private readonly HudView _hud;
        private readonly TextBank _text;

        public BattleView(Node host, ViewFactory view, HudView hud, TextBank text)
        {
            _host = host;
            _view = view;
            _hud = hud;
            _text = text;
        }

        /// <summary>
        /// 演一場戰鬥。<paramref name="onResolved"/> 在演出結束、面板關閉前呼叫——
        /// 狀態變更（扣血、給金幣、標記已消耗）由呼叫端負責，本類不碰 GameState。
        /// </summary>
        public async Task Play(MonsterDefinition monster, CollisionOutcome outcome, GameState player,
                               System.Action onResolved)
        {
            int playerHit = Mathf.Max(0, player.Atk - monster.Def);
            int monsterHit = Mathf.Max(0, monster.Atk - player.Def);
            int hpBefore = player.Hp;

            _hud.OpenBattle(monster, monster.Hp, player);
            await Wait(0.18);

            int shown = Mathf.Clamp(outcome.Rounds, 1, MaxShownRounds);
            double beat = outcome.Rounds > MaxShownRounds ? 0.16 : 0.26;
            int monsterHp = monster.Hp;
            int playerHp = hpBefore;

            // D15：落空次數已算死，這裡只決定「哪幾下」演成閃避
            var missAt = new HashSet<int>();
            int missShown = outcome.Rounds > 0
                ? Mathf.Min(shown - 1, Mathf.RoundToInt(shown * (float)outcome.Misses / outcome.Rounds))
                : 0;
            while (missAt.Count < missShown) missAt.Add((int)(GD.Randi() % (uint)shown));

            for (int i = 0; i < shown; i++)
            {
                bool last = i == shown - 1;

                if (missAt.Contains(i))
                {
                    FloatDamage(_hud.BattleMonsterAnchor, _text["msg_miss"], new Color(0.95f, 0.95f, 1f));
                    await Wait(beat);
                    continue;
                }

                // 我方先手：怪先挨
                monsterHp = last ? 0 : Mathf.Max(0, monsterHp - Mathf.CeilToInt(monster.Hp / (float)shown));
                Burst(_hud.BattleMonsterAnchor);
                FloatDamage(_hud.BattleMonsterAnchor, playerHit.ToString(), new Color(1f, 0.25f, 0.2f));
                _hud.SetBattleHp(monsterHp, playerHp, player);
                await Wait(beat * 0.45);

                // 怪還手——最後一回合牠已經倒下，不還手
                if (!last && monsterHit > 0)
                {
                    playerHp = Mathf.Max(hpBefore - outcome.ExpectedLoss,
                                         playerHp - Mathf.CeilToInt(outcome.ExpectedLoss / (float)(shown - 1)));
                    Burst(_hud.BattleHeroAnchor);
                    FloatDamage(_hud.BattleHeroAnchor, monsterHit.ToString(), new Color(1f, 0.25f, 0.2f));
                    _hud.SetBattleHp(monsterHp, playerHp, player);
                }
                await Wait(beat * 0.55);
            }

            onResolved();
            _hud.CloseBattleRow(monster, outcome);

            await Wait(1.5);
            _hud.HideBattle();
        }

        /// <summary>
        /// 命中爆閃：8 幀黃星疊在受擊者身上。
        ///
        /// 用 <see cref="Tween"/> 而不是 async 迴圈——原本是 fire-and-forget 的 Task，
        /// 面板若在動畫途中關閉，就會對已釋放的節點呼叫 QueueFree。Tween 掛在節點上，
        /// 節點消失時它自己停，這個競態從結構上消失。
        /// </summary>
        private void Burst(Vector2 at)
        {
            var s = new Sprite2D
            {
                Texture = _view.GetTexture(SpriteMap.Burst(0)),
                Position = at, Scale = new Vector2(1.45f, 1.45f), ZIndex = 130,
                TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            };
            _hud.BattleLayer.AddChild(s);

            var tw = s.CreateTween();
            for (int f = 0; f < SpriteMap.BurstFrames; f++)
            {
                var tex = _view.GetTexture(SpriteMap.Burst(f));
                tw.TweenCallback(Callable.From(() => s.Texture = tex)).SetDelay(0.035);
            }
            tw.TweenCallback(Callable.From(s.QueueFree));
        }

        /// <summary>
        /// 傷害數字：紅字**向下**飄再淡出。
        /// 向下是原版的做法（一般遊戲往上飄）——照抄，懷舊感就在這種小地方。
        /// </summary>
        private void FloatDamage(Vector2 at, string text, Color color)
        {
            var lb = _view.MakeLabel(_hud.BattleLayer, at + new Vector2(-52, 22), 24,
                HorizontalAlignment.Center, color, 135);
            lb.Size = new Vector2(60, 30);
            lb.Text = text;

            var tw = lb.CreateTween().SetParallel();
            tw.TweenProperty(lb, "position", lb.Position + new Vector2(0, 26), 0.55);
            tw.TweenProperty(lb, "modulate:a", 0.0f, 0.55);
            tw.Chain().TweenCallback(Callable.From(lb.QueueFree));
        }

        private SignalAwaiter Wait(double seconds)
            => _host.ToSignal(_host.GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    }
}
