using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using Tower.Core.Combat;
using Tower.Core.Commands;
using Tower.Core.Data;
using Tower.Core.Floors;
using Tower.Core.Grid;

namespace Tower.Game
{
    /// <summary>
    /// 遊戲的協調者：載入資料、組裝樓層、處理輸入與規則。
    ///
    /// 繪圖交給 <see cref="ViewFactory"/>、UI 交給 <see cref="HudView"/>、
    /// 文字交給 <see cref="TextBank"/>、檔名對照交給 <see cref="SpriteMap"/>——
    /// 本類只留「遊戲怎麼運作」。**規則一律來自 Tower.Core，這裡不做任何數值判斷。**
    /// </summary>
    public partial class GameRoot : Node2D
    {
        private const int Cell = SpriteMap.PixelsPerCell;
        private const float IdleFrameSeconds = 0.42f;

        private ViewFactory _view;
        private HudView _hud;
        private TextBank _text;
        private Catalog _catalog;

        private FloorDefinition _floor;
        private GameState _state;
        private readonly List<IGameCommand> _commands = new List<IGameCommand>();

        private Node2D _boardRoot;
        private readonly Dictionary<string, Node2D> _entityViews = new Dictionary<string, Node2D>();
        private readonly Dictionary<string, Label> _previewLabels = new Dictionary<string, Label>();
        private readonly List<(Sprite2D node, string monsterId, float phase)> _idle = new List<(Sprite2D, string, float)>();
        private Sprite2D _hero;
        private int _heroDir, _heroStep;

        private bool _busy;
        private List<TextBank.Line> _activeDialogue;
        private int _dialogueIndex;
        private string _dialogueCurrentId;
        private readonly HashSet<string> _dialogueSeen = new HashSet<string>();

        public override void _Ready()
        {
            _text = TextBank.Load();
            _catalog = Catalog.Load(TextBank.ReadCsv("monsters.csv"), TextBank.ReadCsv("items.csv"));

            _view = new ViewFactory();
            _hud = new HudView(_view, _text, this);

            LoadFloor("F00");                    // 從序章層開場，照原版
            StartDialogue("dlg_f00_prologue");
        }

        // ---- 樓層 ----

        /// <summary>
        /// 載入/切換樓層。<paramref name="entryPos"/> 指定落點——座標對齊規約下，
        /// 上樓落在該層的下樓梯、下樓落在該層的上樓梯，不給則用該層預設起點。
        /// </summary>
        private void LoadFloor(string id, GridPos? entryPos = null)
        {
            _boardRoot?.QueueFree();
            _entityViews.Clear();
            _previewLabels.Clear();
            _idle.Clear();
            _commands.Clear();
            _busy = false;
            _activeDialogue = null;
            _hud.HideDialogue();
            _hud.HideBattle();

            var carried = _state;                // 換樓層時屬性與道具要帶著走
            _floor = id switch
            {
                "F00" => F00.Build(),
                "F02" => F02.Build(),
                _ => F01.Build(),
            };

            _state = carried ?? new GameState { Atk = 10, Def = 10, Hp = 1000 }; // data/balance.csv 鏡像
            _state.CurrentFloor = id;
            _state.Position = entryPos ?? id switch
            {
                "F00" => F00.SpawnPos,
                "F02" => F02.StairsDownPos,
                _ => F01.SpawnPos,
            };

            _boardRoot = new Node2D { Position = BoardOrigin, Scale = new Vector2(BoardScale, BoardScale) };
            AddChild(_boardRoot);
            _view.Board = _boardRoot;
            BuildBoard();
            BuildHero();

            RefreshHud();
            RefreshPreviews();
        }

        /// <summary>
        /// 棋盤內的**區域**座標；整體位移與放大由 _boardRoot 的 transform 處理。
        /// 13 格 × 32px = 416px，在 1280x720 上太小（下方會空掉四分之一），
        /// 故放大 1.5 倍成 624px，右側區域正好裝得下。
        /// </summary>
        private static Vector2 LocalOf(in GridPos p)
            => new Vector2(p.X * Cell + Cell / 2f, p.Y * Cell + Cell / 2f);

        private const float BoardScale = 1.5f;
        private static readonly Vector2 BoardOrigin = new Vector2(468, 44);

        private void BuildBoard()
        {
            for (int y = 0; y < FloorGrid.Size; y++)
            for (int x = 0; x < FloorGrid.Size; x++)
            {
                var pos = new GridPos(x, y);
                bool wall = _floor.Grid[pos] == TerrainType.Wall;
                _view.MakeSprite(wall ? SpriteMap.TileWall : SpriteMap.TileFloor, LocalOf(pos), 0);
            }

            foreach (var e in _floor.Entities)
            {
                string sprite = SpriteMap.For(e);
                if (sprite == null) continue;
                var node = _view.MakeSprite(sprite, LocalOf(e.Pos), 10);
                _entityViews[e.Eid] = node;

                if (e.Type == EntityType.Monster)
                {
                    // 傷害預覽常駐、掛在怪物身上（怪物消失標籤跟著走）
                    var lb = _view.MakeLabel(node, new Vector2(-24, -30), 14, HorizontalAlignment.Center, Colors.White, 60);
                    lb.Size = new Vector2(48, 18);
                    _previewLabels[e.Eid] = lb;
                    _idle.Add((node, e.Ref, GD.Randf()));  // 相位錯開，棋盤才不會像節拍器一起跳
                }
            }
        }

        private void BuildHero()
        {
            _heroDir = SpriteMap.HeroDirDown;
            _heroStep = 0;
            _hero = _view.MakeSprite(SpriteMap.Hero(_heroDir, SpriteMap.WalkCycle[0]), LocalOf(_state.Position), 20);
        }

        public override void _Process(double delta)
        {
            _hud.Tick();

            float t = Godot.Time.GetTicksMsec() / 1000f;
            foreach (var (node, monsterId, phase) in _idle)
            {
                int step = Mathf.FloorToInt(t / IdleFrameSeconds + phase * SpriteMap.WalkCycle.Length);
                node.Texture = _view.GetTexture(
                    SpriteMap.MonsterFrame(monsterId, SpriteMap.WalkCycle[step % SpriteMap.WalkCycle.Length]));
            }
        }

        // ---- 輸入 ----

        public override void _UnhandledInput(InputEvent ev)
        {
            if (_activeDialogue != null)
            {
                if (ev.IsActionPressed("confirm") || ev is InputEventKey { Pressed: true }) AdvanceDialogue();
                return;
            }
            if (_busy) return;

            if (Input.IsActionJustPressed("move_up")) TryStep(0, -1, SpriteMap.HeroDirUp);
            else if (Input.IsActionJustPressed("move_down")) TryStep(0, 1, SpriteMap.HeroDirDown);
            else if (Input.IsActionJustPressed("move_left")) TryStep(-1, 0, SpriteMap.HeroDirLeft);
            else if (Input.IsActionJustPressed("move_right")) TryStep(1, 0, SpriteMap.HeroDirRight);
        }

        private async void TryStep(int dx, int dy, int dir)
        {
            if (dir != _heroDir) { _heroDir = dir; _heroStep = 0; }

            var from = _state.Position;
            var to = new GridPos(from.X + dx, from.Y + dy);
            if (!_floor.Grid.CanStep(from, to)) { ApplyHeroSprite(); return; }

            var blocker = _floor.EntityAt(to);
            if (blocker != null && !_state.ConsumedEids.Contains(blocker.Eid))
            {
                await Interact(blocker);
                return;
            }

            await WalkStep(from, to);
            AfterArrive(to);
        }

        private async Task Interact(FloorEntity e)
        {
            switch (e.Type)
            {
                case EntityType.Door:
                    if (!HasKey(e.DoorTier)) { _hud.Toast(KeyMsg(e.DoorTier)); return; }
                    Apply(new OpenDoorCommand(e.Eid, e.DoorTier));
                    _entityViews[e.Eid].QueueFree();
                    RefreshHud();
                    return;

                case EntityType.Npc:
                    StartDialogue(e.DialogueId);
                    return;

                case EntityType.Monster:
                    var m = _catalog.Monsters[e.Ref];
                    var outcome = CombatResolver.ResolveCollision(_state.CombatStats, m);
                    // D13：打不過或會死 —— 那隻怪就是一堵牆，不會發生戰鬥
                    if (!outcome.Winnable) { _hud.Toast(_text["msg_cannot_win"]); return; }
                    if (outcome.ExpectedLoss >= _state.Hp) { _hud.Toast(_text["msg_lethal_blocked"]); return; }
                    await BattleSequence(e, m, outcome);
                    return;
            }
        }

        private void AfterArrive(in GridPos pos)
        {
            var here = _floor.EntityAt(pos);
            if (here == null || _state.ConsumedEids.Contains(here.Eid)) return;

            if (here.Type == EntityType.Item)
            {
                Apply(new PickupItemCommand(here.Eid, _catalog.Items[here.Ref]));
                _entityViews[here.Eid].QueueFree();
                return;
            }

            if (here.Type != EntityType.Stairs) return;

            // 踏上塔門那一刻的宣言——原版的收尾，說完才進塔
            if (_state.CurrentFloor == "F00" && here.Stairs == StairsDirection.Up
                && !_dialogueSeen.Contains("dlg_f00_gate"))
            {
                StartDialogue("dlg_f00_gate");
                return;
            }

            // 座標對齊規約：落在對應的另一道樓梯上
            switch (_state.CurrentFloor, here.Stairs)
            {
                case ("F00", StairsDirection.Up): LoadFloor("F01", F01.StairsDownPos); return;
                case ("F01", StairsDirection.Down): LoadFloor("F00", F00.StairsUpPos); return;
                case ("F01", StairsDirection.Up): LoadFloor("F02", F02.StairsDownPos); return;
                case ("F02", StairsDirection.Down): LoadFloor("F01", F01.StairsUpPos); return;
            }
            _hud.Toast(_text["msg_demo_end"], 5);
        }

        private void Apply(IGameCommand cmd)
        {
            cmd.Apply(_state);
            _commands.Add(cmd);          // D7：所有狀態變更都是指令，回溯才retrofit得進來
            RefreshHud();
            RefreshPreviews();
        }

        // ---- 演出 ----

        /// <summary>
        /// 碰撞戰演出——**照原版逐回合演**（6219_newMT.swf 錄影逐格比對）：
        /// 開 VS 面板 → 每回合雙方各挨一下、體力數字一格一格掉、受擊處放黃色爆閃
        /// 並跳紅色傷害數字向下飄散 → 結算列。
        ///
        /// D1 的一次結算是**規則**不是表現：算術仍一次算完（預覽與實戰同一輸出，
        /// 永遠不會騙人），這裡只是把算好的結果攤開來演。回合數壓在 12 次以內。
        /// </summary>
        private async Task BattleSequence(FloorEntity entity, MonsterDefinition monster, CollisionOutcome outcome)
        {
            _busy = true;

            int playerHit = Mathf.Max(0, _state.Atk - monster.Def);
            int monsterHit = Mathf.Max(0, monster.Atk - _state.Def);
            int hpBefore = _state.Hp;

            _hud.OpenBattle(monster, monster.Hp, _state);
            await ToSignal(GetTree().CreateTimer(0.18), SceneTreeTimer.SignalName.Timeout);

            int shown = Mathf.Clamp(outcome.Rounds, 1, 12);
            double beat = outcome.Rounds > 12 ? 0.16 : 0.26;
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
                    await ToSignal(GetTree().CreateTimer(beat), SceneTreeTimer.SignalName.Timeout);
                    continue;
                }

                // 我方先手：怪先挨
                monsterHp = last ? 0 : Mathf.Max(0, monsterHp - Mathf.CeilToInt(monster.Hp / (float)shown));
                _ = Burst(_hud.BattleMonsterAnchor);
                FloatDamage(_hud.BattleMonsterAnchor, playerHit.ToString(), new Color(1f, 0.25f, 0.2f));
                _hud.SetBattleHp(monsterHp, playerHp, _state);
                await ToSignal(GetTree().CreateTimer(beat * 0.45), SceneTreeTimer.SignalName.Timeout);

                // 怪還手——最後一回合牠已經倒下，不還手
                if (!last && monsterHit > 0)
                {
                    playerHp = Mathf.Max(hpBefore - outcome.ExpectedLoss,
                                         playerHp - Mathf.CeilToInt(outcome.ExpectedLoss / (float)(shown - 1)));
                    _ = Burst(_hud.BattleHeroAnchor);
                    FloatDamage(_hud.BattleHeroAnchor, monsterHit.ToString(), new Color(1f, 0.25f, 0.2f));
                    _hud.SetBattleHp(monsterHp, playerHp, _state);
                }
                await ToSignal(GetTree().CreateTimer(beat * 0.55), SceneTreeTimer.SignalName.Timeout);
            }

            Apply(new CollisionBattleCommand(entity.Eid, outcome, monster));
            _entityViews[entity.Eid].QueueFree();
            _hud.CloseBattleRow(monster, outcome);

            await ToSignal(GetTree().CreateTimer(1.5), SceneTreeTimer.SignalName.Timeout);
            _hud.HideBattle();
            _busy = false;
        }

        /// <summary>命中爆閃：8 幀黃星疊在受擊者身上（原版的命中表現）。</summary>
        private async Task Burst(Vector2 at)
        {
            var s = new Sprite2D
            {
                Texture = _view.GetTexture(SpriteMap.Burst(0)),
                Position = at, Scale = new Vector2(1.45f, 1.45f), ZIndex = 130,
                TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            };
            _hud.BattleLayer.AddChild(s);

            for (int f = 0; f < SpriteMap.BurstFrames; f++)
            {
                s.Texture = _view.GetTexture(SpriteMap.Burst(f));
                await ToSignal(GetTree().CreateTimer(0.035), SceneTreeTimer.SignalName.Timeout);
            }
            s.QueueFree();
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

            var tw = CreateTween().SetParallel();
            tw.TweenProperty(lb, "position", lb.Position + new Vector2(0, 26), 0.55);
            tw.TweenProperty(lb, "modulate:a", 0.0f, 0.55);
            tw.Chain().TweenCallback(Callable.From(lb.QueueFree));
        }

        /// <summary>走一格：0.1 秒滑過去，中途換一次走路幀。</summary>
        private async Task WalkStep(GridPos from, GridPos to)
        {
            _busy = true;
            _heroStep++;
            ApplyHeroSprite();

            var tw = CreateTween();
            tw.TweenProperty(_hero, "position", LocalOf(to), 0.10);
            await ToSignal(tw, Tween.SignalName.Finished);

            // D7：**所有**狀態變更都走指令模式，移動也不例外。
            // 直接寫 _state.Position 會讓回溯永遠回不了一步移動——而 D7 明白寫著
            // 「第一個放寬的閥門是純移動一步免費收回」，那個閥門得先存在才談得上放寬。
            Apply(new MoveCommand(from, to));
            _busy = false;
        }

        private void ApplyHeroSprite()
        {
            int frame = SpriteMap.WalkCycle[_heroStep % SpriteMap.WalkCycle.Length];
            var tex = _view.GetTexture(SpriteMap.Hero(_heroDir, frame));
            _hero.Texture = tex;
            _hud.SetPortrait(tex);        // 角色欄的頭像＝棋盤上的那個小人，永遠同步
        }

        // ---- HUD ----

        private void RefreshHud()
        {
            _hud.SetFloor(FloorLabel(), _floor.NameZh);
            _hud.SetStats(_state);
        }

        private string FloorLabel()
        {
            int n = int.Parse(_state.CurrentFloor.Substring(1));
            return _text["msg_floor_enter"].Replace("{n}", n.ToString());
        }

        /// <summary>常駐傷害預覽——D7「不設確認」的前提：戰前就看得到代價。</summary>
        private void RefreshPreviews()
        {
            foreach (var e in _floor.Entities)
            {
                if (e.Type != EntityType.Monster) continue;
                if (!_previewLabels.TryGetValue(e.Eid, out var label)) continue;
                if (_state.ConsumedEids.Contains(e.Eid)) continue;

                var o = CombatResolver.ResolveCollision(_state.CombatStats, _catalog.Monsters[e.Ref]);
                bool blocked = !o.Winnable || o.ExpectedLoss >= _state.Hp;
                label.Text = o.Winnable ? $"-{o.ExpectedLoss}" : "✖";
                label.AddThemeColorOverride("font_color",
                    blocked ? new Color(1f, 0.3f, 0.25f) : new Color(1f, 0.95f, 0.5f));
            }
        }

        private bool HasKey(KeyTier t) => t switch
        {
            KeyTier.Yellow => _state.KeysYellow > 0,
            KeyTier.Blue => _state.KeysBlue > 0,
            _ => _state.KeysRed > 0,
        };

        private string KeyMsg(KeyTier t) => t switch
        {
            KeyTier.Yellow => _text["msg_need_key_y"],
            KeyTier.Blue => _text["msg_need_key_b"],
            _ => _text["msg_need_key_r"],
        };

        // ---- 對話 ----

        private void StartDialogue(string id)
        {
            if (id == null || !_text.TryGetDialogue(id, out var seq)) return;
            _activeDialogue = seq;
            _dialogueCurrentId = id;
            _dialogueIndex = _dialogueSeen.Contains(id) ? seq.Count - 1 : 0;  // 播畢後再撞 = 重播最後一句
            _hud.ShowDialogue(seq[_dialogueIndex]);
        }

        private void AdvanceDialogue()
        {
            _dialogueIndex++;
            if (_dialogueIndex < _activeDialogue.Count)
            {
                _hud.ShowDialogue(_activeDialogue[_dialogueIndex]);
                return;
            }

            string finished = _dialogueCurrentId;
            _dialogueSeen.Add(finished);
            _activeDialogue = null;
            _hud.HideDialogue();

            // 塔門宣言講完就進塔——否則玩家會站在樓梯上，而樓梯只在「踏入」時觸發
            if (finished == "dlg_f00_gate") LoadFloor("F01", F01.StairsDownPos);
        }
    }
}
