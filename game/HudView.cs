using Godot;
using Tower.Core.Combat;
using Tower.Core.Commands;

namespace Tower.Game
{
    /// <summary>
    /// 經典魔塔三欄 HUD：樓層橫幅、角色欄、鑰匙欄、戰鬥面板、提示、對話框。
    ///
    /// 全部是 CanvasLayer 底下的 Control——螢幕座標，與棋盤縮放無關。
    /// 本類只管畫，不含任何規則；資料由 GameRoot 餵進來。
    /// </summary>
    public sealed class HudView
    {
        private const int StatRows = 5; // 生命/攻擊/防禦/金幣/經驗

        /// <summary>
        /// 居中元素的水平修正：棋盤是「左欄右側空間」的中心，不是螢幕中心。
        /// 橫幅與對話框跟著移半個左欄寬，才會對齊棋盤——否則在 21:9 上看起來歪一邊。
        /// </summary>
        private const float BoardCenterShift = 136f;   // = 左欄寬 272 的一半

        private readonly ViewFactory _view;
        private readonly TextBank _text;
        private readonly CanvasLayer _layer;

        private Label _floorBanner, _toast;
        private Sprite2D _portrait;
        private readonly Label[] _statValues = new Label[StatRows];
        private readonly Label[] _keyCounts = new Label[4];

        private Control _battle;
        private Label _battleTitle, _battleLeft, _battleRight, _battleLoss, _battleReward;
        private Sprite2D _battleMonIcon, _battleHeroIcon;
        private MonsterDefinition _battleMonster;

        private Control _dialogueBox;
        private Label _dialogueSpeaker, _dialogueText;

        private double _toastUntil;

        /// <summary>
        /// 戰鬥面板上兩位對手的圖示座標，供演出層放爆閃與傷害數字。
        /// 讀圖示節點的**實際位置**而不是寫死常數——面板現在是錨定的，
        /// 螢幕比例一變位置就跟著動，寫死的座標會立刻對不上。
        /// </summary>
        public Vector2 BattleMonsterAnchor => _battleMonIcon.Position;
        public Vector2 BattleHeroAnchor => _battleHeroIcon.Position;
        public Node BattleLayer => _battle;

        public HudView(ViewFactory view, TextBank text, Node root)
        {
            _view = view;
            _text = text;
            _layer = new CanvasLayer { Layer = 10 };
            root.AddChild(_layer);
            Build();
        }

        private void Build()
        {
            // 每一塊都錨到自己該貼的邊，子節點座標一律相對於容器。
            // 這樣 16:9 / 19.5:9 / 21:9 / 平板 4:3 都不會離邊或被裁——
            // stretch 是 keep_height，邏輯高固定 720、寬隨比例變。
            var banner = ViewFactory.Anchored(_layer, ViewFactory.Side.Middle, ViewFactory.Side.Start,
                new Vector2(BoardCenterShift, 8), new Vector2(340, 40));
            _view.MakePanel(banner, Vector2.Zero, new Vector2(340, 40), 0.9f);
            _floorBanner = _view.MakeLabel(banner, Vector2.Zero, 22, HorizontalAlignment.Center, Colors.White);
            _floorBanner.Size = new Vector2(340, 40);

            BuildCharacterPanel();
            BuildKeyPanel();
            BuildBattlePanel();
            BuildDialogue();

            var toastBox = ViewFactory.Anchored(_layer, ViewFactory.Side.Middle, ViewFactory.Side.Start,
                new Vector2(BoardCenterShift, 116), new Vector2(480, 34));
            _toast = _view.MakeLabel(toastBox, Vector2.Zero, 22, HorizontalAlignment.Center, Colors.White);
            _toast.Size = new Vector2(480, 34);
            _toast.Visible = false;
        }

        /// <summary>
        /// 角色欄——原版左欄的長相：頭像＋名字在上，數值對齊成一欄在下。
        /// 頭像跟著實際走路方向與步伐換幀；數字靠右對齊，掃一眼就讀得出來。
        /// </summary>
        private void BuildCharacterPanel()
        {
            var box = ViewFactory.Anchored(_layer, ViewFactory.Side.Start, ViewFactory.Side.Start,
                new Vector2(16, 60), new Vector2(240, 250));
            _view.MakePanel(box, Vector2.Zero, new Vector2(240, 250), 0.84f);

            _portrait = new Sprite2D
            {
                Texture = _view.GetTexture(SpriteMap.Hero(SpriteMap.HeroDirDown, 0)),
                Position = new Vector2(48, 48),
                Scale = new Vector2(2, 2),
                ZIndex = 95,
                TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            };
            box.AddChild(_portrait);

            var name = _view.MakeLabel(box, new Vector2(84, 32), 20, HorizontalAlignment.Left,
                new Color(1f, 0.85f, 0.4f));
            name.Text = _text["lbl_hero"];

            string[] icons = { SpriteMap.Item["potion_s"], SpriteMap.Item["gem_atk"], SpriteMap.Item["gem_def"], null, null };
            string[] labels = { _text["lbl_hp"], _text["lbl_atk"], _text["lbl_def"], _text["lbl_gold"], _text["lbl_exp"] };
            var colors = new[]
            {
                new Color(1f, 0.55f, 0.55f),   // 生命：紅
                new Color(1f, 0.78f, 0.45f),   // 攻擊：橙
                new Color(0.6f, 0.8f, 1f),     // 防禦：藍
                new Color(1f, 0.9f, 0.5f),     // 金幣：金
                new Color(0.8f, 0.75f, 1f),    // 經驗：紫
            };

            for (int i = 0; i < StatRows; i++)
            {
                float y = 92 + i * 30;
                if (icons[i] != null)
                    _view.MakeSprite(icons[i], new Vector2(24, y + 10), 95, box);

                var lb = _view.MakeLabel(box, new Vector2(44, y), 18, HorizontalAlignment.Left,
                    new Color(0.85f, 0.85f, 0.9f));
                lb.Text = labels[i];

                _statValues[i] = _view.MakeLabel(box, new Vector2(104, y), 20, HorizontalAlignment.Right, colors[i]);
                _statValues[i].Size = new Vector2(120, 26);
            }
        }

        private void BuildKeyPanel()
        {
            var box = ViewFactory.Anchored(_layer, ViewFactory.Side.Start, ViewFactory.Side.Start,
                new Vector2(16, 326), new Vector2(240, 176));
            _view.MakePanel(box, Vector2.Zero, new Vector2(240, 176), 0.84f);

            string[] icons =
            {
                SpriteMap.Item["key_yellow"], SpriteMap.Item["key_blue"],
                SpriteMap.Item["key_red"], SpriteMap.Item["hourglass"],
            };
            for (int i = 0; i < icons.Length; i++)
            {
                float y = 24 + i * 38;
                _view.MakeSprite(icons[i], new Vector2(32, y + 12), 95, box);
                _keyCounts[i] = _view.MakeLabel(box, new Vector2(58, y), 20, HorizontalAlignment.Left, Colors.White);
            }
        }

        /// <summary>戰鬥面板：鏡像排版的 VS 面板（原版做法），逐回合更新。</summary>
        private void BuildBattlePanel()
        {
            // 置中：戰鬥面板是全畫面的焦點，任何比例下都該在正中間
            _battle = ViewFactory.Anchored(_layer, ViewFactory.Side.Middle, ViewFactory.Side.Middle,
                new Vector2(BoardCenterShift, -20), new Vector2(660, 240));
            _battle.Visible = false;

            _view.MakePanel(_battle, Vector2.Zero, new Vector2(660, 240), 0.93f, 110);

            _battleTitle = _view.MakeLabel(_battle, new Vector2(0, 10), 22, HorizontalAlignment.Center,
                new Color(1f, 0.85f, 0.4f), 120);
            _battleTitle.Size = new Vector2(660, 30);

            // 錨點是**容器內**的座標；演出層要的是螢幕座標，由 BattleMonsterAnchor 取用時換算
            _battleMonIcon = new Sprite2D
            {
                Position = new Vector2(60, 110), Scale = new Vector2(2, 2), ZIndex = 120,
                TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            };
            _battle.AddChild(_battleMonIcon);

            _battleHeroIcon = new Sprite2D
            {
                Texture = _view.GetTexture(SpriteMap.Hero(SpriteMap.HeroDirDown, 0)),
                Position = new Vector2(600, 110), Scale = new Vector2(2, 2), ZIndex = 120,
                TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            };
            _battle.AddChild(_battleHeroIcon);

            // 鏡像排版：怪物「標籤：值」在左、玩家「值：標籤」在右，標籤朝內（原版做法）
            _battleLeft = _view.MakeLabel(_battle, new Vector2(100, 55), 20, HorizontalAlignment.Left, Colors.White, 120);
            _battleLeft.Size = new Vector2(200, 120);
            _battleLeft.AutowrapMode = TextServer.AutowrapMode.Off;

            _battleRight = _view.MakeLabel(_battle, new Vector2(360, 55), 20, HorizontalAlignment.Right, Colors.White, 120);
            _battleRight.Size = new Vector2(200, 120);

            _battleLoss = _view.MakeLabel(_battle, new Vector2(0, 178), 20, HorizontalAlignment.Center,
                new Color(1f, 0.45f, 0.4f), 120);
            _battleLoss.Size = new Vector2(660, 26);

            _battleReward = _view.MakeLabel(_battle, new Vector2(0, 206), 20, HorizontalAlignment.Center,
                new Color(1f, 0.55f, 0.85f), 120); // 原版的桃紅獎勵列
            _battleReward.Size = new Vector2(660, 26);
        }

        private void BuildDialogue()
        {
            // 貼底置中——原版就是把對話框蓋在地圖上，不是另闢一條空白區
            _dialogueBox = ViewFactory.Anchored(_layer, ViewFactory.Side.Middle, ViewFactory.Side.End,
                new Vector2(BoardCenterShift, 90), new Vector2(620, 110));
            _dialogueBox.Visible = false;

            _view.MakePanel(_dialogueBox, Vector2.Zero, new Vector2(620, 110), 0.92f, 115);
            _dialogueSpeaker = _view.MakeLabel(_dialogueBox, new Vector2(18, 8), 18, HorizontalAlignment.Left,
                new Color(1f, 0.85f, 0.4f), 120);
            _dialogueText = _view.MakeLabel(_dialogueBox, new Vector2(18, 36), 20, HorizontalAlignment.Left,
                Colors.White, 120);
            _dialogueText.Size = new Vector2(584, 66);
            _dialogueText.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        }

        // ---- 更新 ----

        public void SetFloor(string label, string nameZh)
            => _floorBanner.Text = string.IsNullOrEmpty(nameZh) ? label : $"{label}　{nameZh}";

        /// <summary>頭像跟著棋盤上的主角同步轉向與換幀。</summary>
        public void SetPortrait(Texture2D tex)
        {
            if (tex != null) _portrait.Texture = tex;
        }

        public void SetStats(GameState s)
        {
            _statValues[0].Text = s.Hp.ToString();
            _statValues[1].Text = s.Atk.ToString();
            _statValues[2].Text = s.Def.ToString();
            _statValues[3].Text = s.Gold.ToString();
            _statValues[4].Text = s.Exp.ToString();
            _keyCounts[0].Text = $"×  {s.KeysYellow}";
            _keyCounts[1].Text = $"×  {s.KeysBlue}";
            _keyCounts[2].Text = $"×  {s.KeysRed}";
            _keyCounts[3].Text = $"×  {s.Hourglasses}";
        }

        // ---- 戰鬥面板 ----

        public void OpenBattle(MonsterDefinition m, int monsterHp, GameState player)
        {
            _battleMonster = m;
            _battleTitle.Text = $"{m.NameZh}　　{_text["lbl_vs"]}　　{_text["lbl_hero"]}";
            _battleMonIcon.Texture = _view.GetTexture(SpriteMap.MonsterFrame(m.Id, 0));
            SetBattleHp(monsterHp, player.Hp, player);
            _battleLoss.Text = "";
            _battleReward.Text = "";
            _battle.Visible = true;
        }

        /// <summary>逐回合更新雙方體力——原版的體感來自看著數字一格一格掉。</summary>
        public void SetBattleHp(int monsterHp, int playerHp, GameState player)
        {
            var m = _battleMonster;
            _battleLeft.Text = $"{_text["lbl_hp"]}：{Mathf.Max(0, monsterHp)}\n{_text["lbl_atk"]}：{m.Atk}\n{_text["lbl_def"]}：{m.Def}";
            _battleRight.Text = $"{playerHp}：{_text["lbl_hp"]}\n{player.Atk}：{_text["lbl_atk"]}\n{player.Def}：{_text["lbl_def"]}";
        }

        public void CloseBattleRow(MonsterDefinition m, in CollisionOutcome outcome)
        {
            _battleLoss.Text = $"{_text["lbl_hp"]} -{outcome.ExpectedLoss}";
            _battleReward.Text =
                $"{_text["lbl_victory"]}　{_text["lbl_reward_exp"]} +{m.ExpDrop}　{_text["lbl_reward_gold"]} +{m.GoldDrop}";
        }

        public void HideBattle() => _battle.Visible = false;

        // ---- 提示與對話 ----

        public void Toast(string msg, double seconds = 1.6)
        {
            _toast.Text = msg;
            _toast.Visible = true;
            _toastUntil = Godot.Time.GetTicksMsec() / 1000.0 + seconds;
        }

        public void ShowDialogue(in TextBank.Line line)
        {
            // 旁白（序章劇情）沒有說話者——不要印出空的【】
            _dialogueSpeaker.Text = string.IsNullOrEmpty(line.Speaker) ? "" : $"【{line.Speaker}】";
            _dialogueText.Text = line.Text;
            _dialogueBox.Visible = true;
        }

        public void HideDialogue() => _dialogueBox.Visible = false;

        public void Tick()
        {
            if (_toast.Visible && Godot.Time.GetTicksMsec() / 1000.0 >= _toastUntil)
                _toast.Visible = false;
        }
    }
}
