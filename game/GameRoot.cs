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
        private BattleView _battleView;
        private ShopView _shopView;
        private BestiaryView _bestiary;
        private AudioBank _audio;
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
        private TouchPad _pad;
        private EditorMode _editor;
        private int _heroDir, _heroStep;

        private bool _busy;
        private List<TextBank.Line> _activeDialogue;
        private int _dialogueIndex;
        private string _dialogueCurrentId;
        private readonly HashSet<string> _dialogueSeen = new HashSet<string>();

        public override void _Ready()
        {
            _text = TextBank.Load();
            _catalog = Catalog.Load(
                TextBank.ReadCsv("monsters.csv"), TextBank.ReadCsv("items.csv"),
                TextBank.ReadCsv("shops.csv"), TextBank.ReadCsv("altars.csv"));
            _floors = LoadFloors();

            _view = new ViewFactory();
            _hud = new HudView(_view, _text, this);
            _audio = AudioBank.Create(this);
            _battleView = new BattleView(this, _view, _hud, _text, _audio);
            _shopView = new ShopView(_view, _text, this);
            _bestiary = new BestiaryView(_view, _text, _catalog, this);

            // 虛擬方向鍵（D9）：左下角，橫向雙手持握時落在左拇指下
            _pad = TouchPad.Create(this, PadCenter);
            _pad.Stepped += Step;

            // 視窗/螢幕尺寸變動時，棋盤與方向鍵都要重新定位（旋轉、多視窗、不同機型）
            GetViewport().SizeChanged += Relayout;

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
            var floors = new List<FloorDefinition>();
            ReadFloorsFrom("res://data/floors", floors, required: true);

            // **設定層等開發用樓層只在 debug 建置載入**——正式出貨的包裡不會有它們。
            // 它們的 id 不是 F## 格式，所以就算誤入包中也不會排進塔（見 FloorRegistry）。
            if (OS.IsDebugBuild()) ReadFloorsFrom("res://data/dev", floors, required: false);

            return new FloorRegistry(floors);
        }

        private static void ReadFloorsFrom(string dirPath, List<FloorDefinition> into, bool required)
        {
            var dir = DirAccess.Open(dirPath);
            if (dir == null)
            {
                if (required) throw new System.InvalidOperationException($"找不到 {dirPath}");
                return;
            }
            foreach (var file in dir.GetFiles())
            {
                if (!file.EndsWith(".json")) continue;
                into.Add(FloorJson.Parse(Godot.FileAccess.GetFileAsString($"{dirPath}/{file}")));
            }
        }

        /// <summary>
        /// 開關關卡編輯器。開啟時把遊戲的棋盤與 HUD 收起來——兩套畫面疊在一起沒有意義，
        /// 而且編輯器要的是「這層的原貌」而不是玩到一半的狀態。
        /// </summary>
        private void ToggleEditor()
        {
            _editor ??= new EditorMode(this, _view, _text, _catalog, _floors);

            if (_editor.Active)
            {
                _editor.Close();
                _hud.Visible = true;
                LoadFloor(_state.CurrentFloor, _state.Position);   // 回遊戲，重畫棋盤
                _pad.Visible = true;
                return;
            }

            _boardRoot?.QueueFree();
            _boardRoot = null;
            _pad.Visible = false;
            _hud.Visible = false;
            _editor.Open(_state.CurrentFloor);
        }

        /// <summary>
        /// 跳到設定層——功能的測試場。給足資源，才測得動商店與祭壇。
        /// 只在 debug 建置存在；release 沒有這一層，這裡會靜默不動作。
        /// </summary>
        private void JumpToDevFloor()
        {
            const string id = "DEV_SETTINGS";
            if (!_floors.Has(id)) return;

            _state.Gold = 500;
            _state.Exp = 200;
            _state.Hourglasses = 3;
            LoadFloor(id);
            _hud.Toast(_floors[id].NameZh, 2.0);
        }

        /// <summary>方向鍵中心：貼左下，離邊留出安全距離（瀏海／圓角／手勢列）。</summary>
        private Vector2 PadCenter => new Vector2(150, GetViewportRect().Size.Y - 140);

        /// <summary>螢幕尺寸變了就重新擺位——HUD 由錨點自理，這裡處理非 Control 的部分。</summary>
        private void Relayout()
        {
            if (_boardRoot != null) _boardRoot.Position = BoardOrigin;
            if (_pad != null) _pad.Position = PadCenter;
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
        private const float BoardPixels = FloorGrid.Size * Cell * BoardScale;   // 13 × 32 × 1.5 = 624
        private const float HudColumnWidth = 272f;                              // 左欄 16 + 240 + 16

        /// <summary>
        /// 棋盤置中於「左欄右側」的剩餘空間，依實際視窗寬度算——**不寫死**。
        /// stretch 是 keep_height：邏輯高固定 720，寬度隨螢幕比例變（21:9 手機約 1750，
        /// 平板 4:3 約 960）。寫死 468 只在 1280 寬時剛好。
        /// </summary>
        private Vector2 BoardOrigin
        {
            get
            {
                float viewW = GetViewportRect().Size.X;
                float free = viewW - HudColumnWidth;
                return new Vector2(HudColumnWidth + Mathf.Max(0, (free - BoardPixels) / 2f), 44);
            }
        }

        private void BuildBoard()
        {
            for (int y = 0; y < FloorGrid.Size; y++)
            for (int x = 0; x < FloorGrid.Size; x++)
            {
                var pos = new GridPos(x, y);
                var terrain = _floor.Grid[pos];
                _view.MakeSprite(terrain == TerrainType.Wall ? SpriteMap.TileWall : SpriteMap.TileFloor,
                    LocalOf(pos), 0);

                // 單向格必須看得見。規則早就在 Grid 裡，但棋盤一直畫成普通地板——
                // 玩家踏進去才發現回不來，那不是謎題是陷阱（D13 的視覺語言同理）。
                string arrow = terrain switch
                {
                    TerrainType.OneWayNorth => "↑",
                    TerrainType.OneWaySouth => "↓",
                    TerrainType.OneWayWest => "←",
                    TerrainType.OneWayEast => "→",
                    _ => null,
                };
                if (arrow != null)
                {
                    var lb = _view.MakeLabel(_boardRoot, LocalOf(pos) - new Vector2(16, 16), 20,
                        HorizontalAlignment.Center, new Color(1f, 0.85f, 0.35f), 5);
                    lb.Text = arrow;
                    lb.Size = new Vector2(32, 32);
                }
            }

            foreach (var e in _floor.Entities)
            {
                string sprite = SpriteMap.For(e);
                if (sprite == null) continue;
                var node = _view.MakeSprite(sprite, LocalOf(e.Pos), 10);
                _entityViews[e.Eid] = node;

                if (e.Type == EntityType.Monster)
                {
                    // 踏進這一層就算「遭遇」——手冊記的是見過什麼，不是打過什麼。
                    // 知識只增不減，回溯不會讓玩家忘記（見 GameState.SeenMonsters）。
                    _state.SeenMonsters.Add(e.Ref);

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
            // F2 開關卡編輯器、F3 跳設定層。兩者都是**開發工具**，release 建置不存在
            // （設定層只在 debug 載入；沒載到就跳不過去）。
            if (ev.IsActionPressed("editor_toggle")) { ToggleEditor(); return; }
            if (ev.IsActionPressed("dev_floor")) { JumpToDevFloor(); return; }

            // 怪物手冊：與傷害預覽共同構成玩家的計算依據（D1 下特性是怪物的全部獨特性）
            if (ev.IsActionPressed("bestiary")) { _bestiary.Toggle(_state); return; }

            // D7 外層防護：免費退回本層入口。機制在 SaveGame 寫好了很久，
            // 但一直沒有入口——玩家碰不到的決策等於沒兌現。
            if (ev.IsActionPressed("retreat")) { RetreatToEntry(); return; }

            if (_shopView.Open || _bestiary.Open) return;   // 面板開著時不吃移動

            if (_editor != null && _editor.Active)
            {
                if (ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click)
                    _editor.Paint(_editor.GridAt(click.Position));
                return;
            }

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

        /// <summary>
        /// 一步的完整流程。
        ///
        /// 這是唯一的 `async void`——事件處理器沒有東西能 await 回傳值，只能是它。
        /// 但 `async void` 的例外會直接吞掉（Godot 不會報，玩家看到的是遊戲莫名卡住），
        /// 所以整段包 try/catch 把錯誤送進 Godot 的錯誤流，並解開 _busy 免得永久凍結。
        /// </summary>
        private async void TryStep(int dx, int dy, int dir)
        {
            try
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
            catch (System.Exception e)
            {
                GD.PushError($"[Tower] 走一步時出錯：{e}");
                _busy = false;      // 不解開的話輸入會永遠鎖住
            }
        }

        private async Task Interact(FloorEntity e)
        {
            switch (e.Type)
            {
                case EntityType.Door:
                    if (!HasKey(e.DoorTier)) { _audio.Play(AudioBank.Blocked); _hud.Toast(KeyMsg(e.DoorTier)); return; }
                    _audio.Play(AudioBank.Door);
                    Apply(new OpenDoorCommand(e.Eid, e.DoorTier));
                    _entityViews[e.Eid].QueueFree();
                    RefreshHud();
                    return;

                case EntityType.Npc:
                    StartDialogue(e.DialogueId);
                    return;

                // 商店/祭壇是**互動點不是障礙**：站在旁邊開面板，不佔用移動
                case EntityType.Shop:
                    _audio.Play(AudioBank.Shop);
                    if (_catalog.Shops.TryGetValue(e.Ref, out var shop))
                        _shopView.ShowShop(shop, _catalog, _state, Apply, RefreshHud);
                    return;

                case EntityType.Altar:
                    if (_catalog.Altars.TryGetValue(e.Ref, out var altar))
                        _shopView.ShowAltar(altar, _state, Apply, RefreshHud);
                    return;

                // 機關：把目標實體標成已消耗（通常是門，於是門開了）。
                // 目標可跨層，是本作唯一能製造跨層依賴的機制。
                case EntityType.Switch:
                    _audio.Play(AudioBank.Door);
                    Apply(new SwitchCommand(e.Eid, e.SwitchTargets));
                    RebuildBoard();     // 目標可能在本層，開了要看得見
                    _hud.Toast(_text["msg_switch"]);
                    return;

                case EntityType.Monster:
                    var m = _catalog.Monsters[e.Ref];
                    var outcome = CombatResolver.ResolveCollision(_state.CombatStats, m);
                    // D13：打不過或會死 —— 那隻怪就是一堵牆，不會發生戰鬥
                    if (!outcome.Winnable) { _audio.Play(AudioBank.Blocked); _hud.Toast(_text["msg_cannot_win"]); return; }
                    if (outcome.ExpectedLoss >= _state.Hp) { _audio.Play(AudioBank.Blocked); _hud.Toast(_text["msg_lethal_blocked"]); return; }
                    _busy = true;
                    await _battleView.Play(m, outcome, _state, () =>
                    {
                        Apply(new CollisionBattleCommand(e.Eid, outcome, m));
                        _entityViews[e.Eid].QueueFree();
                    });
                    _busy = false;
                    return;
            }
        }

        private void AfterArrive(in GridPos pos)
        {
            var here = _floor.EntityAt(pos);
            if (here == null || _state.ConsumedEids.Contains(here.Eid)) return;

            if (here.Type == EntityType.Item)
            {
                _audio.Play(AudioBank.Item);
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
            _audio.Play(AudioBank.Stairs);
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

        /// <summary>
        /// 退回本層入口（D7 外層防護）——**免費**。
        ///
        /// 這一層處理的是「這層根本不該進」；同層內的失誤由回溯（付費，Z 鍵）處理。
        /// 兩層的分工是 D7 的核心：外層不收費，所以玩家永遠不會真的卡死；
        /// 內層收費，所以每一步仍然有重量。
        ///
        /// 沿用 D7 全域硬核：不問「確定要退嗎」。退回去就是退回去。
        /// </summary>
        private void RetreatToEntry()
        {
            if (_busy || _activeDialogue != null || _shopView.Open || _bestiary.Open) return;

            _save.RevertToFloor(_state.CurrentFloor);
            _audio.Play(AudioBank.Stairs);
            RebuildBoard();
            SaveFile.Write(_save);
            _hud.Toast(_text["msg_retreated"], 2.0);
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

        /// <summary>
        /// 樓層標籤。開發用樓層（設定層）的 id 不是 F## 格式——
        /// 直接 int.Parse 會擲例外，把整個 RefreshHud 中斷掉，畫面就停在上一層的資訊
        /// （實測踩過：棋盤換了但橫幅還寫著 0F 塔外）。
        /// </summary>
        private string FloorLabel()
        {
            if (!FloorRegistry.IsTowerFloor(_state.CurrentFloor)) return "";
            return _text["msg_floor_enter"]
                .Replace("{n}", FloorRegistry.NumberOf(_state.CurrentFloor).ToString());
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
