using System.Collections;
using System.Collections.Generic;
using System.IO;
using Tower.Core.Combat;
using Tower.Core.Commands;
using Tower.Core.Data;
using Tower.Core.Floors;
using Tower.Core.Grid;
using UnityEngine;

namespace Tower.Game
{
    /// <summary>
    /// 第 3 步最小可玩版的協調者：載入資料、組裝樓層、處理輸入與規則。
    ///
    /// 繪圖交給 <see cref="ViewFactory"/>、UI 交給 <see cref="HudView"/>、
    /// 文字交給 <see cref="TextBank"/>、音效交給 <see cref="AudioBank"/>、
    /// 檔名對照交給 <see cref="SpriteMap"/>——本類只留「遊戲怎麼運作」。
    ///
    /// 場景在執行期自建，任何空場景按 Play 即可。
    /// </summary>
    public sealed class GamePreviewBootstrap : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (FindFirstObjectByType<GamePreviewBootstrap>() != null) return;
            new GameObject("TowerPreview").AddComponent<GamePreviewBootstrap>();
        }

        // ---- 相依 ----
        private ViewFactory _view;
        private HudView _hud;
        private TextBank _text;
        private AudioBank _audio;
        private Catalog _catalog;
        private Camera _cam;

        // ---- 遊戲狀態 ----
        private FloorDefinition _floor;
        private GameState _state;
        private readonly List<IGameCommand> _commands = new List<IGameCommand>();
        private IReadOnlyDictionary<string, MonsterDefinition> Monsters => _catalog.Monsters;
        private IReadOnlyDictionary<string, ItemDefinition> Items => _catalog.Items;

        // ---- 場上物件 ----
        private GameObject _boardRoot;
        private readonly Dictionary<string, GameObject> _entityViews = new Dictionary<string, GameObject>();
        private readonly Dictionary<string, TextMesh> _previewLabels = new Dictionary<string, TextMesh>();
        private readonly List<IdleAnim> _idleAnims = new List<IdleAnim>();
        private GameObject _hero;
        private SpriteRenderer _heroRenderer;
        private int _heroDir, _heroStep;

        // ---- 互動狀態 ----
        private bool _busy;                       // 戰鬥演出或走路中：凍結輸入
        private List<TextBank.Line> _activeDialogue;
        private int _dialogueIndex;
        private string _dialogueCurrentId;
        private readonly HashSet<string> _dialogueSeenAll = new HashSet<string>();
        private (int dx, int dy)? _heldDelta;
        private float _nextRepeatAt;

        private sealed class IdleAnim
        {
            public SpriteRenderer Renderer;
            public string MonsterId;
            public float Phase;
        }

        private const float IdleFrameSeconds = 0.42f;

        // ---- 啟動 ----

        private void Start()
        {
            string dataDir = Path.Combine(Application.streamingAssetsPath, "data");
            _text = TextBank.LoadFrom(dataDir);
            _catalog = Catalog.Load(
                File.ReadAllText(Path.Combine(dataDir, "monsters.csv")),
                File.ReadAllText(Path.Combine(dataDir, "items.csv")));

            _view = new ViewFactory(Path.Combine(Application.streamingAssetsPath, "sprites"));
            BuildCamera();
            _hud = new HudView(_view, _text);
            _audio = AudioBank.Create(transform);

            LoadFloor("F01");
        }

        private void BuildCamera()
        {
            // 清掉預設場景殘留的攝影機與燈（Untitled 場景自帶一組，會跟自建的打架）
            foreach (var c in FindObjectsByType<Camera>(FindObjectsSortMode.None)) Destroy(c.gameObject);
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) Destroy(l.gameObject);

            var camGo = new GameObject("Main Camera");
            _cam = camGo.AddComponent<Camera>();
            _cam.orthographic = true;
            _cam.orthographicSize = 7.2f;
            _cam.backgroundColor = new Color(0.07f, 0.06f, 0.09f);
            _cam.clearFlags = CameraClearFlags.SolidColor;
            camGo.transform.position = new Vector3(-3.6f, 0, -10); // 左移騰出側欄（D9 橫向三欄）
            camGo.tag = "MainCamera";
        }

        /// <summary>
        /// 載入/切換樓層（F01、F02；F00 展示層由 G 鍵切換）。
        /// <paramref name="entryPos"/> 指定落點——座標對齊規約下，上樓落在該層的下樓梯、
        /// 下樓落在該層的上樓梯，不給則用該層預設起點。
        /// </summary>
        private void LoadFloor(string id, GridPos? entryPos = null)
        {
            if (_boardRoot != null) Destroy(_boardRoot);
            _entityViews.Clear();
            _previewLabels.Clear();
            _idleAnims.Clear();
            _commands.Clear();
            _busy = false;
            _activeDialogue = null;
            _hud.HideDialogue();
            _hud.HideReceipt();

            bool gallery = id == "F00";
            var carried = _state; // 換樓層時屬性與道具要帶著走

            _floor = id switch
            {
                "F00" => GalleryFloor.Build(),
                "F02" => F02.Build(),
                _ => F01.Build(),
            };

            _state = carried != null && !gallery && carried.CurrentFloor != "F00"
                ? carried
                : new GameState { Atk = 10, Def = 10, Hp = 1000 }; // data/balance.csv 鏡像
            _state.CurrentFloor = id;
            _state.Position = entryPos ?? id switch
            {
                "F00" => GalleryFloor.SpawnPos,
                "F02" => F02.StairsDownPos,
                _ => F01.SpawnPos,
            };
            if (gallery) { _state.KeysYellow = 1; _state.KeysBlue = 1; _state.KeysRed = 1; }

            _boardRoot = new GameObject("board");
            _view.Root = _boardRoot.transform;
            BuildBoard();
            BuildHero();
            _view.Root = null; // HUD 不掛在棋盤下，切樓層不該被銷毀

            RefreshHud();
            RefreshPreviews();
            // 樓層名已常駐在橫幅上，這裡再報一次只是拿字擋住棋盤——展示層才需要額外說明
            if (gallery)
                _hud.Toast($"{_text["gallery_name"]}　{_text["msg_gallery_hint"]}", 3.2f);
        }

        private void BuildBoard()
        {
            for (int y = 0; y < FloorGrid.Size; y++)
            for (int x = 0; x < FloorGrid.Size; x++)
            {
                var pos = new GridPos(x, y);
                bool isWall = _floor.Grid[pos] == TerrainType.Wall;
                _view.MakeSprite(isWall ? SpriteMap.TileWall : SpriteMap.TileFloor, WorldOf(pos), 0, $"t_{x}_{y}");
            }

            foreach (var e in _floor.Entities)
            {
                string sprite = SpriteMap.For(e);
                if (sprite == null) continue;
                var go = _view.MakeSprite(sprite, WorldOf(e.Pos), 10, e.Eid);
                _entityViews[e.Eid] = go;

                if (e.Type == EntityType.Monster)
                {
                    // 傷害預覽常駐、掛在怪物身上（怪物消失標籤跟著走）
                    _previewLabels[e.Eid] = _view.MakeText(go.transform, new Vector3(0, 0.62f, 0), 0.42f, TextAnchor.LowerCenter, 60);
                    _idleAnims.Add(new IdleAnim
                    {
                        Renderer = go.GetComponent<SpriteRenderer>(),
                        MonsterId = e.Ref,
                        Phase = Random.Range(0f, 1f), // 相位錯開，棋盤才不會像節拍器一起跳
                    });
                }
            }
        }

        private void BuildHero()
        {
            _heroDir = SpriteMap.HeroDirDown;
            _heroStep = 0;
            _hero = _view.MakeSprite(SpriteMap.Hero(_heroDir, SpriteMap.WalkCycle[0]), WorldOf(_state.Position), 20, "hero");
            _heroRenderer = _hero.GetComponent<SpriteRenderer>();
        }

        private static Vector3 WorldOf(in GridPos p) => new Vector3(p.X - 6f, 6f - p.Y, 0);

        // ---- 每幀 ----

        private void Update()
        {
            TickIdleAnimations();
            _hud.Tick();

            if (_busy) return;

            if (_hud.ReceiptOpen)
            {
                if (Input.anyKeyDown || _hud.ReceiptExpired) _hud.HideReceipt();
                return;
            }

            if (_activeDialogue != null)
            {
                if (Input.anyKeyDown) AdvanceDialogue();
                return;
            }

            // G＝展示層開/關、F＝回 1F
            if (Input.GetKeyDown(KeyCode.G)) { LoadFloor(_state.CurrentFloor == "F00" ? "F01" : "F00"); return; }
            if (Input.GetKeyDown(KeyCode.F) && _state.CurrentFloor != "F01") { LoadFloor("F01"); return; }

            var held = ReadHeldDirection();
            if (held == null) { _heldDelta = null; return; }
            var (delta, facing) = held.Value;
            if (_heldDelta == null || _heldDelta.Value != delta)
            {
                _heldDelta = delta;
                TryStep(delta, facing);
                _nextRepeatAt = Time.time + 0.28f;   // 首步立即，按住 0.28s 後才連走
            }
            else if (Time.time >= _nextRepeatAt)
            {
                TryStep(delta, facing);
                _nextRepeatAt = Time.time + 0.02f;   // 略短於 tween（0.10s）→ 連續行走
            }
        }

        private void TickIdleAnimations()
        {
            for (int i = _idleAnims.Count - 1; i >= 0; i--)
            {
                var a = _idleAnims[i];
                if (a.Renderer == null) { _idleAnims.RemoveAt(i); continue; }
                int step = Mathf.FloorToInt(Time.time / IdleFrameSeconds + a.Phase * SpriteMap.WalkCycle.Length);
                var s = _view.GetSprite(SpriteMap.MonsterFrame(a.MonsterId, SpriteMap.WalkCycle[step % SpriteMap.WalkCycle.Length]));
                if (s != null && a.Renderer.sprite != s) a.Renderer.sprite = s;
            }
        }

        private static ((int dx, int dy) delta, string facing)? ReadHeldDirection()
        {
            if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W)) return ((0, -1), "up");
            if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S)) return ((0, 1), "down");
            if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) return ((-1, 0), "side_l");
            if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) return ((1, 0), "side_r");
            return null;
        }

        // ---- 規則 ----

        private void TryStep((int dx, int dy) d, string facing)
        {
            SetFacing(facing);
            var from = _state.Position;
            var to = new GridPos(from.X + d.dx, from.Y + d.dy);
            if (!_floor.Grid.CanStep(from, to)) return;

            var entity = _floor.EntityAt(to);
            if (entity != null && !_state.ConsumedEids.Contains(entity.Eid))
            {
                switch (entity.Type)
                {
                    case EntityType.Npc:
                        _audio.Play(AudioBank.Talk);
                        StartDialogue(entity.DialogueId);
                        return;

                    case EntityType.Door:
                        if (!HasKey(entity.DoorTier))
                        {
                            _audio.Play(AudioBank.Blocked);
                            _hud.Toast(KeyMsg(entity.DoorTier));
                            return;
                        }
                        _audio.Play(AudioBank.Door);
                        Apply(new OpenDoorCommand(entity.Eid, entity.DoorTier));
                        Destroy(_entityViews[entity.Eid]);
                        return;

                    case EntityType.Monster:
                        var monster = Monsters[entity.Ref];
                        var outcome = CombatResolver.ResolveCollision(_state.CombatStats, monster);
                        // D13：打不贏或會死的格子等同牆壁——同一個「擋住」音效，語意一致
                        if (!outcome.Winnable) { _audio.Play(AudioBank.Blocked); _hud.Toast(_text["msg_cannot_win"]); return; }
                        if (outcome.ExpectedLoss >= _state.Hp) { _audio.Play(AudioBank.Blocked); _hud.Toast(_text["msg_lethal_blocked"]); return; }
                        Apply(new CollisionBattleCommand(entity.Eid, outcome, monster));
                        StartCoroutine(BattleSequence(entity, monster, outcome));
                        return;
                }
            }

            Apply(new MoveCommand(from, to));
            StartCoroutine(WalkStep(from, to));
        }

        /// <summary>走到格子上之後：撿道具、踩樓梯。</summary>
        private void AfterArrive(GridPos to)
        {
            var here = _floor.EntityAt(to);
            if (here == null) return;

            if (here.Type == EntityType.Item && !_state.ConsumedEids.Contains(here.Eid))
            {
                _audio.Play(AudioBank.Item);
                Apply(new PickupItemCommand(here.Eid, Items[here.Ref]));
                Destroy(_entityViews[here.Eid]);
            }
            else if (here.Type == EntityType.Stairs)
            {
                _audio.Play(AudioBank.Stairs);
                // 樓層間真的走得通了；座標對齊規約：落在對應的另一道樓梯上
                if (here.Stairs == StairsDirection.Up && _state.CurrentFloor == "F01")
                { LoadFloor("F02", F02.StairsDownPos); return; }
                if (here.Stairs == StairsDirection.Down && _state.CurrentFloor == "F02")
                { LoadFloor("F01", F01.StairsUpPos); return; }
                _hud.Toast(_text["msg_demo_end"], 5f);
            }
        }

        private void Apply(IGameCommand cmd)
        {
            cmd.Apply(_state);
            _commands.Add(cmd);
            RefreshHud();
            RefreshPreviews(); // 屬性/血量變了，所有預覽重算
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

        private string FloorLabel()
        {
            if (_state.CurrentFloor == "F00") return _text["gallery_name"];
            int n = int.Parse(_state.CurrentFloor.Substring(1));
            return _text["msg_floor_enter"].Replace("{n}", n.ToString());
        }

        private void RefreshHud()
        {
            _hud.SetFloor(FloorLabel(), _state.CurrentFloor == "F00" ? "" : _floor.NameZh);
            _hud.SetStats(_state);
        }

        private void RefreshPreviews()
        {
            foreach (var e in _floor.Entities)
            {
                if (e.Type != EntityType.Monster) continue;
                if (!_previewLabels.TryGetValue(e.Eid, out var label) || label == null) continue;
                if (_state.ConsumedEids.Contains(e.Eid)) continue;

                var o = CombatResolver.ResolveCollision(_state.CombatStats, Monsters[e.Ref]);
                bool blocked = !o.Winnable || o.ExpectedLoss >= _state.Hp;
                label.text = o.Winnable ? $"-{o.ExpectedLoss}" : "✖";
                label.color = blocked ? new Color(1f, 0.3f, 0.25f) : new Color(1f, 0.95f, 0.5f);
            }
        }

        // ---- 演出 ----

        /// <summary>
        /// 碰撞戰演出：衝撞 → 命中閃白 → 鏡頭震動 → 怪物消滅 → 傷害數字 → 戰報。
        /// 不做逐回合動畫（D1 一次結算）——這是「撞上去的一瞬間」。
        /// 回合數多則衝撞次數多（上限 3），硬仗在體感上就是比較久。
        /// </summary>
        private IEnumerator BattleSequence(FloorEntity entity, MonsterDefinition monster, CollisionOutcome outcome)
        {
            _busy = true;

            var view = _entityViews.TryGetValue(entity.Eid, out var v) ? v : null;
            var sr = view != null ? view.GetComponent<SpriteRenderer>() : null;
            var heroHome = _hero.transform.position;
            var target = WorldOf(entity.Pos);
            var camHome = _cam.transform.position;

            int bumps = Mathf.Clamp(1 + outcome.Rounds / 12, 1, 3);
            // D15：落空次數已算死，這裡只決定「哪幾下」演成閃避
            int missBumps = outcome.Rounds > 0
                ? Mathf.Min(bumps - 1, Mathf.RoundToInt(bumps * (float)outcome.Misses / outcome.Rounds))
                : 0;
            var missAt = new HashSet<int>();
            while (missAt.Count < missBumps) missAt.Add(Random.Range(0, bumps));

            for (int i = 0; i < bumps; i++)
            {
                if (missAt.Contains(i))
                {
                    FloatText(entity.Pos, _text["msg_miss"], new Color(0.95f, 0.95f, 1f), 0.5f, 0.25f);
                    yield return Lerp(0.14f, _ => { }); // 落空：不撞、不震、不閃
                    continue;
                }

                // 守關怪用暴擊音，一般怪用平 A——聽覺上就分得出這場的份量
                _audio.Play(monster.IsGuardian ? AudioBank.Crit : AudioBank.Attack);

                var lunge = Vector3.Lerp(heroHome, target, 0.35f);
                yield return Lerp(0.07f, t => _hero.transform.position = Vector3.Lerp(heroHome, lunge, t));

                if (sr != null) { sr.color = Color.white; view.transform.localScale = Vector3.one * 1.18f; }
                yield return Lerp(0.09f, t =>
                {
                    _cam.transform.position = camHome + (Vector3)(Random.insideUnitCircle * 0.09f * (1f - t));
                    if (sr != null) sr.color = Color.Lerp(Color.white, new Color(1f, 0.45f, 0.4f), t);
                });
                if (sr != null) view.transform.localScale = Vector3.one;

                yield return Lerp(0.06f, t => _hero.transform.position = Vector3.Lerp(lunge, heroHome, t));
            }

            _hero.transform.position = heroHome;
            _cam.transform.position = camHome;

            if (view != null) Destroy(view); // 預覽標籤是子物件，一起走
            FloatText(entity.Pos, $"-{outcome.ExpectedLoss}", new Color(1f, 0.32f, 0.28f), 0.62f, 0f);
            if (monster.GoldDrop > 0) _audio.Play(AudioBank.Gold, 0.7f);
            _hud.ShowReceipt(monster, outcome, _state);
            _busy = false;
        }

        /// <summary>走一格：0.1 秒滑過去，中途換一次走路幀。連走時每步緊接下一步。</summary>
        private IEnumerator WalkStep(GridPos from, GridPos to)
        {
            _busy = true;
            var a = WorldOf(from);
            var b = WorldOf(to);
            _heroStep++;
            ApplyHeroSprite();

            const float dur = 0.10f;
            float t = 0f;
            bool midSwapped = false;
            while (t < dur)
            {
                t += Time.deltaTime;
                _hero.transform.position = Vector3.Lerp(a, b, Mathf.Clamp01(t / dur));
                if (!midSwapped && t >= dur * 0.5f)
                {
                    midSwapped = true;
                    _heroStep++;   // 半途再換一幀，腳步才有節奏
                    ApplyHeroSprite();
                }
                yield return null;
            }
            _hero.transform.position = b;
            _busy = false;
            AfterArrive(to);
        }

        /// <summary>只轉向、不推進走路幀（撞牆／撞門／看 NPC 時用）。</summary>
        private void SetFacing(string facing)
        {
            int dir = facing switch
            {
                "up" => SpriteMap.HeroDirUp,
                "side_l" => SpriteMap.HeroDirLeft,
                "side_r" => SpriteMap.HeroDirRight,
                _ => SpriteMap.HeroDirDown,
            };
            if (dir != _heroDir) { _heroDir = dir; _heroStep = 0; }
            ApplyHeroSprite();
        }

        private void ApplyHeroSprite()
        {
            int frame = SpriteMap.WalkCycle[_heroStep % SpriteMap.WalkCycle.Length];
            var sprite = _view.GetSprite(SpriteMap.Hero(_heroDir, frame));
            _heroRenderer.sprite = sprite;
            _heroRenderer.flipX = false; // 素材四方向齊備，不需鏡像
            _hud?.SetPortrait(sprite);   // 角色欄的頭像＝棋盤上的那個小人，永遠同步
        }

        private void FloatText(in GridPos pos, string text, Color color, float size, float xOffset)
        {
            var tm = _view.MakeText(null, WorldOf(pos) + new Vector3(xOffset, 0.3f, 0), size, TextAnchor.MiddleCenter, 130);
            tm.text = text;
            tm.color = color;
            if (_boardRoot != null) tm.transform.SetParent(_boardRoot.transform, true);
            StartCoroutine(FloatUpAndFade(tm));
        }

        private IEnumerator FloatUpAndFade(TextMesh tm)
        {
            float t = 0f;
            var start = tm.transform.position;
            while (t < 0.9f)
            {
                t += Time.deltaTime;
                float k = t / 0.9f;
                tm.transform.position = start + new Vector3(0, k * 0.85f, 0);
                var c = tm.color;
                c.a = 1f - k * k;
                tm.color = c;
                yield return null;
            }
            if (tm != null) Destroy(tm.gameObject);
        }

        private static IEnumerator Lerp(float seconds, System.Action<float> step)
        {
            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                step(Mathf.Clamp01(t / seconds));
                yield return null;
            }
            step(1f);
        }

        // ---- 對話 ----

        private void StartDialogue(string id)
        {
            if (!_text.TryGetDialogue(id, out var seq)) return;
            _activeDialogue = seq;
            _dialogueCurrentId = id;
            // 播畢後再撞 = 重播最後一句（守衛的提示要能重看）
            _dialogueIndex = _dialogueSeenAll.Contains(id) ? seq.Count - 1 : 0;
            _hud.ShowDialogue(seq[_dialogueIndex]);
        }

        private void AdvanceDialogue()
        {
            _dialogueIndex++;
            if (_dialogueIndex >= _activeDialogue.Count)
            {
                _dialogueSeenAll.Add(_dialogueCurrentId);
                _activeDialogue = null;
                _hud.HideDialogue();
                return;
            }
            _hud.ShowDialogue(_activeDialogue[_dialogueIndex]);
        }
    }
}
