using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using Tower.Core.Combat;
using Tower.Core.Commands;
using Tower.Core.Data;
using Tower.Core.Floors;
using Tower.Core.Grid;
using Tower.Core.Save;

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
        private FloorRegistry _floors;

        private FloorDefinition _floor;
        /// <summary>
        /// 存檔就是遊戲狀態本身（D7）——快照、指令流、回溯全在裡面。
        /// 不另外拿一份 _state，否則兩份會漂移。
        /// </summary>
        private SaveGame _save;
        private GameState _state => _save.State;

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
            _floors = LoadFloors();

            _view = new ViewFactory();
            _hud = new HudView(_view, _text, this);

            // 虛擬方向鍵（D9）：左下角，橫向雙手持握時落在左拇指下
            TouchPad.Create(this, new Vector2(150, 580)).Stepped += Step;

            var loaded = SaveFile.Read();
            if (loaded != null && _floors.Has(loaded.State.CurrentFloor))
            {
                _save = loaded;
                LoadFloor(_save.State.CurrentFloor, _save.State.Position);
                _hud.Toast(_text["msg_loaded"], 2.0);
            }
            else
            {
                _save = new SaveGame(new GameState { Atk = 10, Def = 10, Hp = 1000 }); // data/balance.csv 鏡像
                LoadFloor(_floors.Order[0]);      // 從最低層（序章）開場，照原版
                StartDialogue("dlg_f00_prologue");
            }
        }

        /// <summary>
        /// 讀 res://data/floors/*.json 建索引。加一層 = 丟一個 JSON 進去，程式不用改。
        /// FloorRegistry 的建構子會強制檢查樓梯座標對齊，接錯會當場擲例外而不是靜默生出接錯的塔。
        /// </summary>
        private static FloorRegistry LoadFloors()
        {
            var dir = DirAccess.Open("res://data/floors");
            if (dir == null) throw new System.InvalidOperationException("找不到 res://data/floors");

            var floors = new List<FloorDefinition>();
            foreach (var file in dir.GetFiles())
            {
                // 匯出後 .json 會被打包成 .json（Godot 不轉換），編輯器內另有 .import 之類要略過
                if (!file.EndsWith(".json")) continue;
                floors.Add(FloorJson.Parse(
                    Godot.FileAccess.GetFileAsString($"res://data/floors/{file}")));
            }
            return new FloorRegistry(floors);
        }

        /// <summary>入口＝下樓梯（座標對齊規約的落點）；沒有下樓梯的層用 spawn（僅序章層）。</summary>
        private static GridPos EntryOf(FloorDefinition floor)
        {
            var down = floor.FindStairs(StairsDirection.Down);
            if (down != null) return down.Pos;
            foreach (var e in floor.Entities)
                if (e.Type == EntityType.Spawn) return e.Pos;
            throw new System.InvalidOperationException($"{floor.Id} 既沒有下樓梯也沒有 spawn");
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
            _busy = false;
            _activeDialogue = null;
            _hud.HideDialogue();
            _hud.HideBattle();

            _floor = _floors[id];
            _state.Position = entryPos ?? EntryOf(_floor);

            // 進入樓層＝拍快照、清空指令流（D7 外層防護的建立點），然後落地存檔。
            // 屬性與道具留在 _save.State 裡，換層自然帶著走。
            _save.EnterFloor(id);
            SaveFile.Write(_save);

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
                // 只吃「按下」，不吃放開——否則一次敲鍵會翻兩頁
                if (ev is InputEventKey { Pressed: true, Echo: false } || ev.IsActionPressed("confirm"))
                    AdvanceDialogue();
                return;
            }
            if (_busy) return;

            // 用事件判斷而不是輪詢 Input：_UnhandledInput 是事件回呼，
            // 在裡面問全域輸入狀態會和事件流錯開（同一幀多事件時可能重複觸發或漏掉）
            if (ev.IsActionPressed("undo")) { UndoStep(); return; }

            if (ev.IsActionPressed("move_up")) Step(0, -1);
            else if (ev.IsActionPressed("move_down")) Step(0, 1);
            else if (ev.IsActionPressed("move_left")) Step(-1, 0);
            else if (ev.IsActionPressed("move_right")) Step(1, 0);
        }

        /// <summary>方向 → 走一步。鍵盤與虛擬方向鍵（D9）共用這個入口。</summary>
        private void Step(int dx, int dy)
        {
            if (_busy || _activeDialogue != null) return;
            int dir = (dx, dy) switch
            {
                (0, -1) => SpriteMap.HeroDirUp,
                (0, 1) => SpriteMap.HeroDirDown,
                (-1, 0) => SpriteMap.HeroDirLeft,
                _ => SpriteMap.HeroDirRight,
            };
            TryStep(dx, dy, dir);
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

            // 座標對齊規約由 FloorRegistry 統一處理——樓層編號相鄰即相接，
            // 落點自動取對面那道樓梯。加樓層不必碰這裡。
            if (_floors.TryTravel(_state.CurrentFloor, here.Stairs, out string toId, out var landing))
            {
                LoadFloor(toId, landing);
                return;
            }
            _hud.Toast(_text["msg_demo_end"], 5);
        }

        private void Apply(IGameCommand cmd)
        {
            _save.Apply(cmd);            // D7：所有狀態變更都是指令，回溯才有東西可退
            SaveFile.Write(_save);       // 指令流也要落地，否則「快照＋重放」的下半截等於不存在
            RefreshHud();
            RefreshPreviews();
        }

        /// <summary>
        /// 行動平台會在沒有預警的情況下殺掉背景 App，桌面版則有關窗事件。
        /// 兩者都在這裡補一次存檔——每步都存已經涵蓋大部分情況，這是保險。
        /// </summary>
        public override void _Notification(int what)
        {
            if (what == NotificationWMCloseRequest
                || what == NotificationApplicationPaused
                || what == NotificationWMGoBackRequest)
            {
                if (_save != null) SaveFile.Write(_save);
            }
        }

        /// <summary>
        /// 回溯一步（D7 內層）——**消耗一顆沙漏**。
        ///
        /// 收費的閘門在這裡，不在 Core：`SaveGame.UndoOne` 只提供機制。這條分工是刻意的，
        /// 因為驗證器要在不談收費的前提下推演路徑。
        ///
        /// D7「誤觸不設防」：不問「確定要回溯嗎」。沙漏沒了就是沒了。
        /// </summary>
        private void UndoStep()
        {
            if (_busy || _activeDialogue != null) return;

            if (_state.Hourglasses <= 0) { _hud.Toast(_text["msg_no_hourglass"]); return; }
            if (_save.UndoDepth == 0) { _hud.Toast(_text["msg_nothing_to_undo"]); return; }

            _state.Hourglasses--;
            _save.UndoOne();

            // 回溯可能讓已消耗的實體復活（怪、道具、門），整層重建最單純也最不會出錯
            RebuildBoard();
            SaveFile.Write(_save);
            _hud.Toast(_text["msg_undone"]);
        }

        /// <summary>就地重建棋盤與主角（回溯後用）——不動 _save，只重畫。</summary>
        private void RebuildBoard()
        {
            _boardRoot?.QueueFree();
            _entityViews.Clear();
            _previewLabels.Clear();
            _idle.Clear();

            _boardRoot = new Node2D { Position = BoardOrigin, Scale = new Vector2(BoardScale, BoardScale) };
            AddChild(_boardRoot);
            _view.Board = _boardRoot;
            BuildBoard();
            BuildHero();
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
            if (finished == "dlg_f00_gate" && _floors.TryTravel(_state.CurrentFloor, StairsDirection.Up, out var toId, out var landing))
                LoadFloor(toId, landing);
        }
    }
}
