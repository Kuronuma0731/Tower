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
    /// 場景執行期自建；任何空場景按 Play 即可。素材與文本從 StreamingAssets 讀。
    ///
    /// UI 全部是「世界空間」物件（TextMesh + 背板），壓在棋盤頂/底的牆排上——
    /// 首次遊測教訓：螢幕空間 IMGUI 會被 Game 視窗縮放裁掉或偏移，世界空間與棋盤共存亡。
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
        private readonly Dictionary<string, TextMesh> _previewLabels = new Dictionary<string, TextMesh>();
        private GameObject _boardRoot;
        private GameObject _hero;
        private SpriteRenderer _heroRenderer;
        private Camera _cam;
        private AudioBank _audio;
        private bool _busy;   // 戰鬥演出中：凍結輸入
        private Font _font;

        // sprite 對照集中在 SpriteMap——換素材只改那裡

        private TextMesh _statusText;
        private TextMesh _floorBanner;
        private readonly TextMesh[] _keyCounts = new TextMesh[4]; // 黃/藍/紅鑰匙、沙漏
        private GameObject _receipt;
        private TextMesh _receiptTitle;
        private TextMesh _receiptLeft;
        private TextMesh _receiptRight;
        private TextMesh _receiptLoss;
        private TextMesh _receiptReward;
        private SpriteRenderer _receiptIcon;
        private float _receiptUntil;
        private TextMesh _toastText;
        private float _toastUntil;
        private GameObject _dialogueBox;
        private TextMesh _dialogueSpeaker;
        private TextMesh _dialogueText;
        private List<(string speaker, string text)> _activeDialogue;
        private int _dialogueIndex;
        private readonly HashSet<string> _dialogueSeenAll = new HashSet<string>();
        private string _dialogueCurrentId;

        // ---- 建置 ----

        private void Start()
        {
            _font = LoadCjkFont();

            LoadStrings();
            LoadDialogues();

            BuildCamera();
            BuildHud();
            _audio = AudioBank.Create(transform);
            LoadFloor("F01");
        }

        /// <summary>載入/切換樓層（F01 = 工程測試層；F00 = 展示層，按 G/F 切換）。</summary>
        private void LoadFloor(string id)
        {
            if (_boardRoot != null) Destroy(_boardRoot);
            _entityViews.Clear();
            _previewLabels.Clear();
            _idleAnims.Clear();
            _commands.Clear();
            _busy = false;
            _activeDialogue = null;
            if (_dialogueBox != null) _dialogueBox.SetActive(false);
            if (_receipt != null) _receipt.SetActive(false);

            bool gallery = id == "F00";
            _floor = gallery ? GalleryFloor.Build() : F01.Build();
            _monsters = gallery ? GalleryFloor.Monsters() : F01.Monsters();
            _items = gallery ? GalleryFloor.Items() : F01.Items();

            _state = new GameState { Atk = 10, Def = 10, Hp = 1000 }; // data/balance.csv 鏡像（原版初始值）
            _state.CurrentFloor = id;
            _state.Position = gallery ? GalleryFloor.SpawnPos : F01.SpawnPos;
            if (gallery) { _state.KeysYellow = 1; _state.KeysBlue = 1; _state.KeysRed = 1; } // 陳列室開門用

            _boardRoot = new GameObject("board");
            BuildBoard();
            BuildHero();
            RefreshPreviews();
            RefreshHud();

            Toast(gallery
                ? $"{S("gallery_name")}　{S("msg_gallery_hint")}"
                : $"{FloorLabel()}　{_floor.NameZh}", 3.2f);
        }

        private static Font LoadCjkFont()
        {
            var installed = Font.GetOSInstalledFontNames();
            string[] preferred =
            {
                "Microsoft JhengHei UI", "Microsoft JhengHei", "微軟正黑體",
                "Noto Sans TC", "Noto Sans CJK TC", "Microsoft YaHei", "微軟雅黑",
                "MingLiU", "PMingLiU", "新細明體",
            };
            foreach (var want in preferred)
                foreach (var have in installed)
                    if (string.Equals(have, want, System.StringComparison.OrdinalIgnoreCase))
                        return Font.CreateDynamicFontFromOSFont(have, 64);

            string[] fuzzy = { "JhengHei", "正黑", "YaHei", "雅黑", "Noto Sans", "Ming", "明體", "Gothic" };
            foreach (var pat in fuzzy)
                foreach (var have in installed)
                    if (have.IndexOf(pat, System.StringComparison.OrdinalIgnoreCase) >= 0)
                        return Font.CreateDynamicFontFromOSFont(have, 64);

            Debug.LogWarning("[TowerPreview] 找不到 CJK 字型，中文可能無法顯示");
            return null;
        }

        private void BuildCamera()
        {
            // 清掉預設場景殘留的攝影機與燈（Untitled 場景自帶一組，會跟自建的打架）
            foreach (var c in FindObjectsByType<Camera>(FindObjectsSortMode.None)) Destroy(c.gameObject);
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) Destroy(l.gameObject);

            var camGo = new GameObject("Main Camera");
            var cam = _cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 7.2f;
            cam.backgroundColor = new Color(0.07f, 0.06f, 0.09f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            camGo.transform.position = new Vector3(-3.6f, 0, -10); // 左移騰出側欄（經典魔塔佈局）
            camGo.tag = "MainCamera";
        }

        private Vector3 WorldOf(in GridPos p) => new Vector3(p.X - 6f, 6f - p.Y, 0);

        private void BuildBoard()
        {
            for (int y = 0; y < FloorGrid.Size; y++)
            for (int x = 0; x < FloorGrid.Size; x++)
            {
                var pos = new GridPos(x, y);
                bool isWall = _floor.Grid[pos] == TerrainType.Wall;
                // 像素素材本身已有明確的牆/地差異，不再額外染色
                MakeSprite(isWall ? SpriteMap.TileWall : SpriteMap.TileFloor, WorldOf(pos), 0, $"t_{x}_{y}");
            }

            foreach (var e in _floor.Entities)
            {
                string sprite = SpriteMap.For(e);
                if (sprite == null) continue;
                var go = MakeSprite(sprite, WorldOf(e.Pos), 10, e.Eid);
                _entityViews[e.Eid] = go;

                if (e.Type == EntityType.Monster)
                {
                    // 傷害預覽：常駐、掛在怪物身上（怪物消失標籤跟著走）
                    var label = MakeText(go.transform, new Vector3(0, 0.62f, 0), 0.42f, TextAnchor.LowerCenter, 60);
                    _previewLabels[e.Eid] = label;
                    // 待機動畫：每隻起始相位錯開，整個棋盤才不會像節拍器一起跳
                    _idleAnims.Add(new IdleAnim
                    {
                        Renderer = go.GetComponent<SpriteRenderer>(),
                        MonsterId = e.Ref,
                        Phase = Random.Range(0f, 1f),
                    });
                }
            }
        }

        // hero 表格列序（RPG Maker 慣例）：0 下 / 1 左 / 2 右 / 3 上
        private int _heroDir;
        private int _heroStep;   // 走了幾步——決定行走幀序的位置

        private sealed class IdleAnim
        {
            public SpriteRenderer Renderer;
            public string MonsterId;
            public float Phase;
        }

        private readonly List<IdleAnim> _idleAnims = new List<IdleAnim>();
        private const float IdleFrameSeconds = 0.42f;

        private void BuildHero()
        {
            _heroDir = SpriteMap.HeroDirDown;
            _heroStep = 0;
            _hero = MakeSprite(SpriteMap.Hero(_heroDir, SpriteMap.WalkCycle[0]), WorldOf(_state.Position), 20, "hero");
            _heroRenderer = _hero.GetComponent<SpriteRenderer>();
        }

        private void BuildHud()
        {
            // ── 樓層橫幅（棋盤正上方，經典魔塔的招牌位置）──
            MakeBackplate(new Vector3(0, 6.85f, 0), 4.2f, 0.95f, 0.9f, 90, "banner_bg");
            _floorBanner = MakeText(null, new Vector3(0, 6.85f, 0), 0.55f, TextAnchor.MiddleCenter, 100);
            _floorBanner.color = Color.white;

            // ── 狀態欄（左上，經典魔塔佈局）──
            MakeBackplate(new Vector3(-10.25f, 3.1f, 0), 5.9f, 6.9f, 0.84f, 90, "panel_status");
            var portrait = MakeSprite(SpriteMap.Hero(SpriteMap.HeroDirDown, 0), new Vector3(-12.05f, 5.35f, 0), 95, "portrait");
            portrait.transform.localScale = new Vector3(1.5f, 1.5f, 1f);
            _statusText = MakeText(null, new Vector3(-12.9f, 4.15f, 0), 0.52f, TextAnchor.UpperLeft, 100);
            _statusText.alignment = TextAlignment.Left;
            _statusText.lineSpacing = 1.15f;

            // ── 鑰匙欄（左下）──
            MakeBackplate(new Vector3(-10.25f, -3.55f, 0), 5.9f, 5.5f, 0.84f, 90, "panel_keys");
            string[] icons =
            {
                SpriteMap.Item["key_yellow"], SpriteMap.Item["key_blue"],
                SpriteMap.Item["key_red"], SpriteMap.Item["hourglass"],
            };
            for (int i = 0; i < icons.Length; i++)
            {
                float y = -1.75f - i * 1.15f;
                var icon = MakeSprite(icons[i], new Vector3(-12.2f, y, 0), 95, $"key_icon_{i}");
                icon.transform.localScale = new Vector3(0.85f, 0.85f, 1f);
                _keyCounts[i] = MakeText(null, new Vector3(-11.45f, y, 0), 0.55f, TextAnchor.MiddleLeft, 100);
                _keyCounts[i].alignment = TextAlignment.Left;
            }

            // ── 戰報面板（碰撞戰結算後短暫顯示；D7：無撤退選項，戰前判斷交給常駐預覽）──
            _receipt = new GameObject("receipt");
            MakeBackplate(new Vector3(0, 0.2f, 0), 11.4f, 4.9f, 0.93f, 110, "receipt_bg")
                .transform.SetParent(_receipt.transform);
            _receiptTitle = MakeText(_receipt.transform, new Vector3(0, 1.85f, 0), 0.55f, TextAnchor.MiddleCenter, 120);
            _receiptTitle.color = new Color(1f, 0.85f, 0.4f);
            var iconGo = new GameObject("receipt_icon");
            iconGo.transform.SetParent(_receipt.transform);
            iconGo.transform.localPosition = new Vector3(-4.15f, 0.35f, 0);
            iconGo.transform.localScale = new Vector3(1.9f, 1.9f, 1f);
            _receiptIcon = iconGo.AddComponent<SpriteRenderer>();
            _receiptIcon.sortingOrder = 120;
            // 鏡像排版：怪物「標籤 值」在左、玩家「值 標籤」在右，標籤朝內（原版做法）
            _receiptLeft = MakeText(_receipt.transform, new Vector3(-3.05f, 1.2f, 0), 0.46f, TextAnchor.UpperLeft, 120);
            _receiptLeft.alignment = TextAlignment.Left;
            _receiptLeft.lineSpacing = 1.2f;
            var heroIcon = MakeSprite(SpriteMap.Hero(SpriteMap.HeroDirDown, 0), new Vector3(4.15f, 0.35f, 0), 120, "receipt_hero");
            heroIcon.transform.SetParent(_receipt.transform);
            heroIcon.transform.localScale = new Vector3(1.9f, 1.9f, 1f);
            _receiptRight = MakeText(_receipt.transform, new Vector3(3.05f, 1.2f, 0), 0.46f, TextAnchor.UpperRight, 120);
            _receiptRight.alignment = TextAlignment.Right;
            _receiptRight.lineSpacing = 1.2f;
            _receiptLoss = MakeText(_receipt.transform, new Vector3(0, -1.35f, 0), 0.5f, TextAnchor.MiddleCenter, 120);
            _receiptLoss.color = new Color(1f, 0.45f, 0.4f);
            _receiptReward = MakeText(_receipt.transform, new Vector3(0, -2.0f, 0), 0.5f, TextAnchor.MiddleCenter, 120);
            _receiptReward.color = new Color(1f, 0.55f, 0.85f); // 原版的桃紅獎勵列
            _receipt.SetActive(false);

            // 提示：棋盤中上（有訊息才出現）
            _toastText = MakeText(null, new Vector3(0, 2.5f, 0), 0.55f, TextAnchor.MiddleCenter, 100);
            _toastText.color = Color.white;
            _toastText.gameObject.SetActive(false);

            // 對話框：壓在底排牆上（y = −6），兩行
            _dialogueBox = new GameObject("dialogue");
            MakeBackplate(new Vector3(0, -5.95f, 0), 13f, 1.5f, 0.86f, 90, "dlg_bg").transform.SetParent(_dialogueBox.transform);
            _dialogueSpeaker = MakeText(_dialogueBox.transform, new Vector3(-6.1f, -5.62f, 0), 0.42f, TextAnchor.MiddleLeft, 100);
            _dialogueSpeaker.color = new Color(1f, 0.85f, 0.4f);
            _dialogueText = MakeText(_dialogueBox.transform, new Vector3(-6.1f, -6.18f, 0), 0.46f, TextAnchor.MiddleLeft, 100);
            _dialogueBox.SetActive(false);
        }

        /// <summary>戰報：怪物 vs 勇者的結算對照（顯示用，非確認框——D7 不設確認）。</summary>
        private void ShowReceipt(MonsterDefinition m, in CollisionOutcome outcome)
        {
            _receiptTitle.text = $"{m.NameZh}　　{S("lbl_vs")}　　{S("lbl_hero")}";
            _receiptIcon.sprite = GetSprite(SpriteMap.MonsterFrame(m.Id, 0));
            _receiptLeft.text =
                $"{S("lbl_hp")}：{m.Hp}\n{S("lbl_atk")}：{m.Atk}\n{S("lbl_def")}：{m.Def}";
            _receiptRight.text =
                $"{_state.Hp}：{S("lbl_hp")}\n{_state.Atk}：{S("lbl_atk")}\n{_state.Def}：{S("lbl_def")}";
            _receiptLoss.text = $"{S("lbl_hp")} -{outcome.ExpectedLoss}";
            _receiptReward.text =
                $"{S("lbl_victory")}　{S("lbl_reward_exp")} +{m.ExpDrop}　{S("lbl_reward_gold")} +{m.GoldDrop}";
            _receipt.SetActive(true);
            _receiptUntil = Time.time + 2.2f;
        }

        /// <summary>
        /// 碰撞戰演出：衝撞 → 命中閃白 → 鏡頭震動 → 怪物消滅 → 傷害數字 → 戰報。
        /// 刻意不做逐回合動畫（D1 是一次結算）——這是「撞上去的一瞬間」，總長 0.42 秒。
        /// 回合數多的戰鬥衝撞次數也多（上限 3 次），讓硬仗在體感上就是比較久。
        /// </summary>
        private System.Collections.IEnumerator BattleSequence(
            FloorEntity entity, MonsterDefinition monster, CollisionOutcome outcome)
        {
            _busy = true;

            var view = _entityViews.TryGetValue(entity.Eid, out var v) ? v : null;
            var sr = view != null ? view.GetComponent<SpriteRenderer>() : null;
            var heroHome = _hero.transform.position;
            var target = WorldOf(entity.Pos);
            var camHome = _cam.transform.position;

            int bumps = Mathf.Clamp(1 + outcome.Rounds / 12, 1, 3);
            for (int i = 0; i < bumps; i++)
            {
                // 守關怪用暴擊音，一般怪用平 A——聽覺上就分得出這場的份量
                _audio.Play(monster.IsGuardian ? AudioBank.Crit : AudioBank.Attack);
                // 衝撞：朝怪物撞出 35% 格再回來
                yield return Lerp(0.07f, t => _hero.transform.position =
                    Vector3.Lerp(heroHome, Vector3.Lerp(heroHome, target, 0.35f), t));

                // 命中：怪物閃白＋放大，鏡頭一震
                if (sr != null)
                {
                    sr.color = Color.white;
                    view.transform.localScale = Vector3.one * 1.18f;
                }
                float shake = 0.09f;
                yield return Lerp(0.09f, t =>
                {
                    _cam.transform.position = camHome + (Vector3)(Random.insideUnitCircle * shake * (1f - t));
                    if (sr != null) sr.color = Color.Lerp(Color.white, new Color(1f, 0.45f, 0.4f), t);
                });

                if (sr != null) view.transform.localScale = Vector3.one;
                yield return Lerp(0.06f, t => _hero.transform.position =
                    Vector3.Lerp(Vector3.Lerp(heroHome, target, 0.35f), heroHome, t));
            }

            _hero.transform.position = heroHome;
            _cam.transform.position = camHome;

            if (view != null) Destroy(view); // 預覽標籤是子物件，一起走
            FloatDamage(entity.Pos, outcome.ExpectedLoss);
            if (monster.GoldDrop > 0) _audio.Play(AudioBank.Gold, 0.7f);
            ShowReceipt(monster, outcome);
            _busy = false;
        }

        private System.Collections.IEnumerator Lerp(float seconds, System.Action<float> step)
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

        /// <summary>地圖上飄起的傷害數字（原版的紅字回饋）。</summary>
        private void FloatDamage(in GridPos pos, int amount)
        {
            var tm = MakeText(null, WorldOf(pos) + new Vector3(0, 0.3f, 0), 0.62f, TextAnchor.MiddleCenter, 130);
            tm.text = $"-{amount}";
            tm.color = new Color(1f, 0.32f, 0.28f);
            if (_boardRoot != null) tm.transform.SetParent(_boardRoot.transform, true);
            StartCoroutine(FloatUpAndFade(tm));
        }

        private System.Collections.IEnumerator FloatUpAndFade(TextMesh tm)
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

        private GameObject MakeBackplate(Vector3 pos, float w, float h, float alpha, int order, string name)
        {
            var go = new GameObject(name);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SolidSprite();
            sr.color = new Color(0.02f, 0.02f, 0.05f, alpha);
            sr.sortingOrder = order;
            go.transform.position = pos;
            go.transform.localScale = new Vector3(w, h, 1);
            return go;
        }

        private Sprite _solid;
        private Sprite SolidSprite()
        {
            if (_solid != null) return _solid;
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var px = new Color32[16];
            for (int i = 0; i < 16; i++) px[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(px);
            tex.Apply();
            _solid = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
            return _solid;
        }

        private TextMesh MakeText(Transform parent, Vector3 localPos, float unitHeight, TextAnchor anchor, int order)
        {
            var go = new GameObject("text");
            if (parent != null) go.transform.SetParent(parent);
            go.transform.localPosition = localPos;
            var tm = go.AddComponent<TextMesh>();
            tm.fontSize = 64;
            tm.characterSize = unitHeight * 10f / 64f;
            tm.anchor = anchor;
            tm.alignment = TextAlignment.Center;
            tm.color = Color.white;
            if (_font != null)
            {
                tm.font = _font;
                go.GetComponent<MeshRenderer>().material = _font.material;
            }
            var mr = go.GetComponent<MeshRenderer>();
            mr.sortingOrder = order;
            return tm;
        }

        private GameObject MakeSprite(string spriteName, Vector3 pos, int order, string goName)
        {
            var go = new GameObject(goName);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GetSprite(spriteName);
            sr.sortingOrder = order;
            go.transform.position = pos;
            if (_boardRoot != null) go.transform.SetParent(_boardRoot.transform, true);
            return go;
        }

        private Sprite GetSprite(string name)
        {
            if (_sprites.TryGetValue(name, out var s)) return s;
            string path = Path.Combine(Application.streamingAssetsPath, "sprites", name + ".png");
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(File.ReadAllBytes(path));
            // D14 像素風：Point filter + 無壓縮 + PPU 對齊格子，否則整套糊掉
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            float ppu = Mathf.Max(tex.width, tex.height); // 32px 素材 → 每格一單位
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
            for (int i = 1; i < lines.Length; i++)
                if (!string.IsNullOrWhiteSpace(lines[i]))
                    yield return lines[i].Trim();
        }

        private string S(string id) => _strings.TryGetValue(id, out var v) ? v : id;

        // ---- 輸入與規則 ----

        private void Update()
        {
            TickIdleAnimations();

            if (_toastText.gameObject.activeSelf && Time.time >= _toastUntil)
                _toastText.gameObject.SetActive(false);

            if (_busy) return; // 戰鬥演出或走路中

            // 戰報開著：任意鍵或逾時關閉，期間凍結移動
            if (_receipt != null && _receipt.activeSelf)
            {
                if (Input.anyKeyDown || Time.time >= _receiptUntil)
                    _receipt.SetActive(false);
                return;
            }

            if (_activeDialogue != null)
            {
                if (Input.anyKeyDown) AdvanceDialogue();
                return;
            }

            // G＝展示層開/關（再按一次回 1F）、F＝回 1F
            if (Input.GetKeyDown(KeyCode.G)) { LoadFloor(_state.CurrentFloor == "F00" ? "F01" : "F00"); return; }
            if (Input.GetKeyDown(KeyCode.F) && _state.CurrentFloor != "F01") { LoadFloor("F01"); return; }

            // 按住連走：首步立即，按住 0.28s 後每 0.12s 重複（遊測回饋：長按要能連續移動）
            var held = ReadHeldDirection();
            if (held == null) { _heldDelta = null; return; }
            var (delta, facing) = held.Value;
            if (_heldDelta == null || _heldDelta.Value != delta)
            {
                _heldDelta = delta;
                TryStep(delta, facing);
                _nextRepeatAt = Time.time + 0.28f;
            }
            else if (Time.time >= _nextRepeatAt)
            {
                // 重複間隔略短於 tween（0.10s），下一步在上一步剛落定時就接上 → 連續行走
                TryStep(delta, facing);
                _nextRepeatAt = Time.time + 0.02f;
            }
        }

        private (int dx, int dy)? _heldDelta;
        private float _nextRepeatAt;

        /// <summary>怪物待機動畫。相位錯開，整個棋盤才不會像節拍器一起跳。</summary>
        private void TickIdleAnimations()
        {
            for (int i = _idleAnims.Count - 1; i >= 0; i--)
            {
                var a = _idleAnims[i];
                if (a.Renderer == null) { _idleAnims.RemoveAt(i); continue; }
                int step = Mathf.FloorToInt(Time.time / IdleFrameSeconds + a.Phase * SpriteMap.WalkCycle.Length);
                int frame = SpriteMap.WalkCycle[step % SpriteMap.WalkCycle.Length];
                var s = GetSprite(SpriteMap.MonsterFrame(a.MonsterId, frame));
                if (s != null && a.Renderer.sprite != s) a.Renderer.sprite = s;
            }
        }

        private ((int dx, int dy) delta, string facing)? ReadHeldDirection()
        {
            if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W)) return ((0, -1), "up");
            if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S)) return ((0, 1), "down");
            if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) return ((-1, 0), "side_l");
            if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) return ((1, 0), "side_r");
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
                        _audio.Play(AudioBank.Talk);
                        StartDialogue(entity.DialogueId);
                        return;

                    case EntityType.Door:
                        if (!HasKey(entity.DoorTier))
                        {
                            _audio.Play(AudioBank.Blocked);
                            Toast(KeyMsg(entity.DoorTier));
                            return;
                        }
                        _audio.Play(AudioBank.Door);
                        Apply(new OpenDoorCommand(entity.Eid, entity.DoorTier));
                        Destroy(_entityViews[entity.Eid]);
                        return;

                    case EntityType.Monster:
                        var monster = _monsters[entity.Ref];
                        var outcome = CombatResolver.ResolveCollision(_state.CombatStats, monster);
                        // D13：打不贏或會死的格子等同牆壁——用同一個「擋住」音效，語意一致
                        if (!outcome.Winnable) { _audio.Play(AudioBank.Blocked); Toast(S("msg_cannot_win")); return; }
                        if (outcome.ExpectedLoss >= _state.Hp) { _audio.Play(AudioBank.Blocked); Toast(S("msg_lethal_blocked")); return; }
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
                Apply(new PickupItemCommand(here.Eid, _items[here.Ref]));
                Destroy(_entityViews[here.Eid]);
            }
            else if (here.Type == EntityType.Stairs)
            {
                _audio.Play(AudioBank.Stairs);
                Toast(S("msg_demo_end"), 5f);
            }
        }

        /// <summary>
        /// 轉向並推進行走幀（D14 素材自帶 4 方向 × 4 幀）。
        /// 走路幀序採 RPG Maker 慣例的 0-1-2-3 循環；轉向時歸零，讓每次轉身有明確起點。
        /// </summary>
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
            _heroRenderer.sprite = GetSprite(SpriteMap.Hero(_heroDir, frame));
            _heroRenderer.flipX = false; // 素材四方向齊備，不需鏡像
        }

        /// <summary>
        /// 走一格：位置在 0.1 秒內滑過去，中途換一次走路幀。
        /// 連走時每步緊接下一步，看起來就是連續行走（D9 一步一格的邏輯完全不變）。
        /// </summary>
        private System.Collections.IEnumerator WalkStep(GridPos from, GridPos to)
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
                    _heroStep++;          // 半途再換一幀，腳步才有節奏
                    ApplyHeroSprite();
                }
                yield return null;
            }
            _hero.transform.position = b;
            _busy = false;

            AfterArrive(to);
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
            KeyTier.Yellow => S("msg_need_key_y"),
            KeyTier.Blue => S("msg_need_key_b"),
            _ => S("msg_need_key_r"),
        };

        private void Toast(string msg, float seconds = 1.6f)
        {
            _toastText.text = msg;
            _toastText.gameObject.SetActive(true);
            _toastUntil = Time.time + seconds;
        }

        // ---- UI 更新 ----

        private string FloorLabel()
        {
            if (_state.CurrentFloor == "F00") return S("gallery_name");
            int n = int.Parse(_state.CurrentFloor.Substring(1));
            return S("msg_floor_enter").Replace("{n}", n.ToString());
        }

        private void RefreshHud()
        {
            _floorBanner.text = _state.CurrentFloor == "F00"
                ? S("gallery_name")
                : $"{FloorLabel()}　{_floor.NameZh}";
            _statusText.text =
                $"{S("lbl_hp")}　{_state.Hp}\n" +
                $"{S("lbl_atk")}　{_state.Atk}\n" +
                $"{S("lbl_def")}　{_state.Def}\n" +
                $"{S("lbl_gold")}　{_state.Gold}\n" +
                $"{S("lbl_exp")}　{_state.Exp}";
            _keyCounts[0].text = $"×  {_state.KeysYellow}";
            _keyCounts[1].text = $"×  {_state.KeysBlue}";
            _keyCounts[2].text = $"×  {_state.KeysRed}";
            _keyCounts[3].text = $"×  {_state.Hourglasses}";
        }

        private void RefreshPreviews()
        {
            foreach (var e in _floor.Entities)
            {
                if (e.Type != EntityType.Monster) continue;
                if (!_previewLabels.TryGetValue(e.Eid, out var label) || label == null) continue;
                if (_state.ConsumedEids.Contains(e.Eid)) continue;

                var o = CombatResolver.ResolveCollision(_state.CombatStats, _monsters[e.Ref]);
                if (!o.Winnable)
                {
                    label.text = "✖";
                    label.color = new Color(1f, 0.3f, 0.25f);
                }
                else if (o.ExpectedLoss >= _state.Hp)
                {
                    label.text = $"-{o.ExpectedLoss}";
                    label.color = new Color(1f, 0.3f, 0.25f); // D13 致死＝紅字，格子視同牆
                }
                else
                {
                    label.text = $"-{o.ExpectedLoss}";
                    label.color = new Color(1f, 0.95f, 0.5f);
                }
            }
        }

        // ---- 對話 ----

        private void StartDialogue(string id)
        {
            if (!_dialogues.TryGetValue(id, out var seq)) return;
            _activeDialogue = seq;
            _dialogueIndex = _dialogueSeenAll.Contains(id) ? seq.Count - 1 : 0;
            _dialogueCurrentId = id;
            ShowDialogueLine();
        }

        private void ShowDialogueLine()
        {
            var (speaker, text) = _activeDialogue[_dialogueIndex];
            _dialogueSpeaker.text = $"【{speaker}】";
            _dialogueText.text = text;
            _dialogueSpeaker.alignment = TextAlignment.Left;
            _dialogueText.alignment = TextAlignment.Left;
            _dialogueBox.SetActive(true);
        }

        private void AdvanceDialogue()
        {
            _dialogueIndex++;
            if (_dialogueIndex >= _activeDialogue.Count)
            {
                _dialogueSeenAll.Add(_dialogueCurrentId);
                _activeDialogue = null;
                _dialogueBox.SetActive(false);
                return;
            }
            ShowDialogueLine();
        }
    }
}
