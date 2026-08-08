using System.Collections.Generic;
using System.IO;
using Tower.Core.Combat;
using Tower.Core.Commands;
using Tower.Core.Floors;
using Tower.Core.Grid;
using UnityEngine;

namespace Tower.Game
{
    /// <summary>
    /// 第 3 步最小可玩版（工程預覽）：F01 + 方向鍵移動 + 碰撞戰 + 鑰匙門 + 傷害預覽。
    /// 整個場景在執行期自建——任何空場景按 Play 即可執行，不依賴 .unity 內容。
    /// 素材與文本從 StreamingAssets 讀（正式匯入管線是後續步驟；文字守鐵則：不寫死）。
    /// 所有狀態變更走 IGameCommand（D7），指令流已記錄，回溯 UI 之後接上。
    /// </summary>
    public sealed class GamePreviewBootstrap : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (FindFirstObjectByType<GamePreviewBootstrap>() != null) return;
            new GameObject("TowerPreview").AddComponent<GamePreviewBootstrap>();
        }

        private FloorDefinition _floor;
        private Dictionary<string, MonsterDefinition> _monsters;
        private Dictionary<string, ItemDefinition> _items;
        private GameState _state;
        private readonly List<IGameCommand> _commands = new List<IGameCommand>();

        private readonly Dictionary<string, Sprite> _sprites = new Dictionary<string, Sprite>();
        private readonly Dictionary<string, string> _strings = new Dictionary<string, string>();
        private readonly Dictionary<string, List<(string speaker, string text)>> _dialogues
            = new Dictionary<string, List<(string, string)>>();

        private readonly Dictionary<string, GameObject> _entityViews = new Dictionary<string, GameObject>();
        private GameObject _hero;
        private SpriteRenderer _heroRenderer;

        private string _toast;
        private float _toastUntil;
        private List<(string speaker, string text)> _activeDialogue;
        private int _dialogueIndex;
        private Font _font;
        private bool _reachedStairs;

        // ---- 建置 ----

        private void Start()
        {
            _font = Font.CreateDynamicFontFromOSFont("Microsoft JhengHei", 16);

            LoadStrings();
            LoadDialogues();

            _floor = F01.Build();
            _monsters = F01.Monsters();
            _items = F01.Items();

            _state = new GameState { Atk = 10, Def = 10, Hp = 550 }; // data/balance.csv（DataPipeline 落地前鏡像）
            _state.CurrentFloor = "F01";
            _state.Position = F01.SpawnPos;

            BuildCamera();
            BuildBoard();
            BuildHero();
        }

        private void BuildCamera()
        {
            var camGo = new GameObject("Main Camera");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 8.2f;
            cam.backgroundColor = new Color(0.07f, 0.06f, 0.09f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            camGo.transform.position = new Vector3(0, 0, -10);
            camGo.tag = "MainCamera";
        }

        private Vector3 WorldOf(in GridPos p) // y 軸翻轉：格子 y 向下，世界 y 向上
            => new Vector3(p.X - 6f, 6f - p.Y, 0);

        private void BuildBoard()
        {
            for (int y = 0; y < FloorGrid.Size; y++)
            for (int x = 0; x < FloorGrid.Size; x++)
            {
                var pos = new GridPos(x, y);
                var terrain = _floor.Grid[pos];
                MakeSprite(terrain == TerrainType.Wall ? "tile_wall" : "tile_floor",
                    WorldOf(pos), 0, $"t_{x}_{y}");
            }

            foreach (var e in _floor.Entities)
            {
                string sprite = e.Type switch
                {
                    EntityType.Door => "ent_door_y",
                    EntityType.Stairs => "ent_stairs_up",
                    EntityType.Npc => "npc_guard_old",
                    EntityType.Monster => e.Ref switch
                    {
                        "slime_green" => "mon_slime_g",
                        "bat_cave" => "mon_bat_cave",
                        _ => "mon_skel_gray",
                    },
                    EntityType.Item => e.Ref == "key_yellow" ? "item_key_y" : "item_potion_s",
                    _ => null, // spawn 無視覺
                };
                if (sprite == null) continue;
                var go = MakeSprite(sprite, WorldOf(e.Pos), 10, e.Eid);
                _entityViews[e.Eid] = go;
            }
        }

        private void BuildHero()
        {
            _hero = MakeSprite("hero_down", WorldOf(_state.Position), 20, "hero");
            _heroRenderer = _hero.GetComponent<SpriteRenderer>();
        }

        private GameObject MakeSprite(string spriteName, Vector3 pos, int order, string goName)
        {
            var go = new GameObject(goName);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GetSprite(spriteName);
            sr.sortingOrder = order;
            go.transform.position = pos;
            return go;
        }

        private Sprite GetSprite(string name)
        {
            if (_sprites.TryGetValue(name, out var s)) return s;
            string path = Path.Combine(Application.streamingAssetsPath, "sprites", name + ".png");
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(File.ReadAllBytes(path));
            tex.filterMode = FilterMode.Bilinear;
            // PPU = 較長邊 → 每張 sprite 恰好一格寬
            float ppu = Mathf.Max(tex.width, tex.height);
            s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), ppu);
            _sprites[name] = s;
            return s;
        }

        // ---- 文本載入（鐵則：玩家可見字串一律來自資料） ----

        private void LoadStrings()
        {
            foreach (var line in ReadDataLines("ui-strings.csv"))
            {
                int i = line.IndexOf(',');
                if (i > 0) _strings[line.Substring(0, i)] = line.Substring(i + 1);
            }
        }

        private void LoadDialogues()
        {
            foreach (var line in ReadDataLines("dialogues.csv"))
            {
                int a = line.IndexOf(',');
                int b = line.IndexOf(',', a + 1);
                if (a <= 0 || b <= a) continue;
                string id = line.Substring(0, a);
                string speaker = line.Substring(a + 1, b - a - 1);
                string text = line.Substring(b + 1);
                if (!_dialogues.TryGetValue(id, out var list))
                    _dialogues[id] = list = new List<(string, string)>();
                list.Add((speaker, text));
            }
        }

        private IEnumerable<string> ReadDataLines(string file)
        {
            string path = Path.Combine(Application.streamingAssetsPath, "data", file);
            var lines = File.ReadAllLines(path);
            for (int i = 1; i < lines.Length; i++) // 跳過表頭
                if (!string.IsNullOrWhiteSpace(lines[i]))
                    yield return lines[i].Trim();
        }

        private string S(string id) => _strings.TryGetValue(id, out var v) ? v : id;

        // ---- 輸入與規則 ----

        private void Update()
        {
            if (_activeDialogue != null)
            {
                if (Input.anyKeyDown) AdvanceDialogue();
                return;
            }

            var dir = ReadDirection();
            if (dir == null) return;
            TryStep(dir.Value.delta, dir.Value.facing);
        }

        private ((int dx, int dy) delta, string facing)? ReadDirection()
        {
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) return ((0, -1), "up");
            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) return ((0, 1), "down");
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) return ((-1, 0), "side_l");
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) return ((1, 0), "side_r");
            return null;
        }

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
                        StartDialogue(entity.DialogueId);
                        return;

                    case EntityType.Door:
                        if (!HasKey(entity.DoorTier)) { Toast(KeyMsg(entity.DoorTier)); return; }
                        Apply(new OpenDoorCommand(entity.Eid, entity.DoorTier));
                        Object.Destroy(_entityViews[entity.Eid]);
                        return; // D7：即撞即開；開門這步不進格

                    case EntityType.Monster:
                        var monster = _monsters[entity.Ref];
                        var outcome = CombatResolver.ResolveCollision(_state.CombatStats, monster);
                        if (!outcome.Winnable) { Toast(S("msg_cannot_win")); return; }       // D13
                        if (outcome.ExpectedLoss >= _state.Hp) { Toast(S("msg_lethal_blocked")); return; } // D13
                        Apply(new CollisionBattleCommand(entity.Eid, outcome, monster));
                        Object.Destroy(_entityViews[entity.Eid]);
                        return; // 勝利後不自動進格（經典魔塔節奏）
                }
            }

            // 純移動
            Apply(new MoveCommand(from, to));
            _hero.transform.position = WorldOf(to);

            // 踩上道具即撿
            var item = _floor.EntityAt(to);
            if (item != null && item.Type == EntityType.Item && !_state.ConsumedEids.Contains(item.Eid))
            {
                Apply(new PickupItemCommand(item.Eid, _items[item.Ref]));
                Object.Destroy(_entityViews[item.Eid]);
            }

            // 踩上樓梯
            var stairs = _floor.EntityAt(to);
            if (stairs != null && stairs.Type == EntityType.Stairs)
            {
                _reachedStairs = true;
                Toast(S("msg_demo_end"), 5f);
            }
        }

        private void SetFacing(string facing)
        {
            _heroRenderer.sprite = facing switch
            {
                "up" => GetSprite("hero_up"),
                "side_l" or "side_r" => GetSprite("hero_side"),
                _ => GetSprite("hero_down"),
            };
            // hero_side 素材面向左；向右走鏡像
            _heroRenderer.flipX = facing == "side_r";
        }

        private void Apply(IGameCommand cmd)
        {
            cmd.Apply(_state);
            _commands.Add(cmd);
        }

        private bool HasKey(KeyTier t) => t switch
        {
            KeyTier.Yellow => _state.KeysYellow > 0,
            KeyTier.Blue => _state.KeysBlue > 0,
            _ => _state.KeysRed > 0,
        };

        private string KeyMsg(KeyTier t) => t switch
        {
            KeyTier.Yellow => S("msg_need_key_y"),
            KeyTier.Blue => S("msg_need_key_b"),
            _ => S("msg_need_key_r"),
        };

        private void Toast(string msg, float seconds = 1.6f)
        {
            _toast = msg;
            _toastUntil = Time.time + seconds;
        }

        private void StartDialogue(string id)
        {
            if (!_dialogues.TryGetValue(id, out var seq)) return;
            _activeDialogue = seq;
            _dialogueIndex = _dialogueSeenAll.Contains(id) ? seq.Count - 1 : 0; // 播畢後再撞 = 重播最後一句
            _dialogueCurrentId = id;
        }

        private readonly HashSet<string> _dialogueSeenAll = new HashSet<string>();
        private string _dialogueCurrentId;

        private void AdvanceDialogue()
        {
            _dialogueIndex++;
            if (_dialogueIndex >= _activeDialogue.Count)
            {
                _dialogueSeenAll.Add(_dialogueCurrentId);
                _activeDialogue = null;
            }
        }

        // ---- HUD（工程預覽用 IMGUI；正式 UI 是後續步驟） ----

        private void OnGUI()
        {
            if (_font != null) GUI.skin.font = _font;
            int fs = Mathf.Max(14, Screen.height / 42);
            GUI.skin.label.fontSize = fs;

            // 頂部數值列
            string hud = $"{S("lbl_hp")} {_state.Hp}   {S("lbl_atk")} {_state.Atk}   {S("lbl_def")} {_state.Def}   " +
                         $"{S("lbl_gold")} {_state.Gold}   {S("lbl_exp")} {_state.Exp}   " +
                         $"{S("item_key_label")}×{_state.KeysYellow}";
            GUI.Label(new Rect(12, 8, Screen.width - 24, fs * 2), hud);

            // 怪物傷害預覽（常駐——這是核心 UI，不是 QoL）
            var cam = Camera.main;
            foreach (var e in _floor.Entities)
            {
                if (e.Type != EntityType.Monster || _state.ConsumedEids.Contains(e.Eid)) continue;
                var m = _monsters[e.Ref];
                var o = CombatResolver.ResolveCollision(_state.CombatStats, m);
                string label = !o.Winnable ? "✖" : o.ExpectedLoss >= _state.Hp ? "☠" : $"-{o.ExpectedLoss}";
                var sp = cam.WorldToScreenPoint(WorldOf(e.Pos) + new Vector3(0, 0.55f, 0));
                var style = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = fs,
                    fontStyle = FontStyle.Bold,
                };
                style.normal.textColor = (!o.Winnable || o.ExpectedLoss >= _state.Hp)
                    ? new Color(1f, 0.3f, 0.25f) : new Color(1f, 0.95f, 0.5f);
                GUI.Label(new Rect(sp.x - 60, Screen.height - sp.y - fs * 1.2f, 120, fs * 1.5f), label, style);
            }

            // 提示訊息
            if (_toast != null && Time.time < _toastUntil)
            {
                var style = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter, fontSize = fs + 6, fontStyle = FontStyle.Bold,
                };
                style.normal.textColor = Color.white;
                GUI.Label(new Rect(0, Screen.height * 0.28f, Screen.width, fs * 3), _toast, style);
            }

            // 對話框
            if (_activeDialogue != null)
            {
                var (speaker, text) = _activeDialogue[_dialogueIndex];
                float h = fs * 5f;
                var rect = new Rect(Screen.width * 0.08f, Screen.height - h - 24, Screen.width * 0.84f, h);
                GUI.Box(rect, "");
                GUI.Label(new Rect(rect.x + 16, rect.y + 8, rect.width - 32, fs * 1.6f), $"【{speaker}】");
                GUI.Label(new Rect(rect.x + 16, rect.y + 8 + fs * 1.7f, rect.width - 32, fs * 3f), text);
            }
        }
    }
}
