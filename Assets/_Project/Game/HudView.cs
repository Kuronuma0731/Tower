using System.Collections.Generic;
using Tower.Core.Combat;
using Tower.Core.Commands;
using UnityEngine;

namespace Tower.Game
{
    /// <summary>
    /// 經典魔塔三欄 HUD 的視圖層：樓層橫幅、狀態欄、鑰匙欄、戰報、提示、對話框。
    ///
    /// 全部是**世界空間**物件（TextMesh＋底板），不是螢幕空間 IMGUI——首次遊測教訓：
    /// 螢幕空間 UI 會被 Game 視窗縮放裁掉或偏移，世界空間則與棋盤共存亡。
    ///
    /// 本類只管畫，不含任何規則；資料由 GamePreviewBootstrap 餵進來。
    /// </summary>
    public sealed class HudView
    {
        private readonly ViewFactory _view;
        private readonly TextBank _text;

        private TextMesh _floorBanner;
        private SpriteRenderer _portrait;
        private readonly TextMesh[] _statValues = new TextMesh[StatRows];
        private readonly TextMesh[] _keyCounts = new TextMesh[4];

        private const int StatRows = 5; // 生命/攻擊/防禦/金幣/經驗

        private GameObject _receipt;
        private TextMesh _receiptTitle, _receiptLeft, _receiptRight, _receiptLoss, _receiptReward;
        private SpriteRenderer _receiptIcon;
        private float _receiptUntil;

        private TextMesh _toast;
        private float _toastUntil;

        private GameObject _dialogueBox;
        private TextMesh _dialogueSpeaker, _dialogueText;

        public bool ReceiptOpen => _receipt != null && _receipt.activeSelf;
        public bool ReceiptExpired => Time.time >= _receiptUntil;

        public HudView(ViewFactory view, TextBank text)
        {
            _view = view;
            _text = text;
            Build();
        }

        private void Build()
        {
            // 樓層橫幅（棋盤正上方，經典魔塔的招牌位置）
            _view.MakeBackplate(new Vector3(0, 6.85f, 0), 4.2f, 0.95f, 0.9f, 90, "banner_bg");
            _floorBanner = _view.MakeText(null, new Vector3(0, 6.85f, 0), 0.55f, TextAnchor.MiddleCenter, 100);

            BuildCharacterPanel();

            // 鑰匙欄（左下）
            _view.MakeBackplate(new Vector3(-10.25f, -3.55f, 0), 5.9f, 5.5f, 0.84f, 90, "panel_keys");
            string[] icons =
            {
                SpriteMap.Item["key_yellow"], SpriteMap.Item["key_blue"],
                SpriteMap.Item["key_red"], SpriteMap.Item["hourglass"],
            };
            for (int i = 0; i < icons.Length; i++)
            {
                float y = -1.75f - i * 1.15f;
                var icon = _view.MakeSprite(icons[i], new Vector3(-12.2f, y, 0), 95, $"key_icon_{i}");
                icon.transform.localScale = new Vector3(0.85f, 0.85f, 1f);
                _keyCounts[i] = _view.MakeText(null, new Vector3(-11.45f, y, 0), 0.55f, TextAnchor.MiddleLeft, 100);
                _keyCounts[i].alignment = TextAlignment.Left;
            }

            BuildReceipt();

            _toast = _view.MakeText(null, new Vector3(0, 2.5f, 0), 0.55f, TextAnchor.MiddleCenter, 100);
            _toast.gameObject.SetActive(false);

            BuildDialogue();
        }

        /// <summary>
        /// 角色欄（左上）——原版左欄的長相：頭像＋名字在上，數值對齊成一欄在下。
        ///
        /// 兩個刻意的取法：
        /// ① 頭像**跟著實際走路方向與步伐換幀**（<see cref="SetPortrait"/>），不是一張固定圖——
        ///    玩家的角色感來自「那個小人就是我」，靜止的大頭貼給不了。
        /// ② 數字**靠右對齊成一欄**。標籤與數值混在同一行時（舊版用全形空白隔開）數字位置
        ///    會隨字數浮動，掃一眼讀不出來；魔塔要頻繁比對數值，對齊比排版好看更重要。
        /// </summary>
        private void BuildCharacterPanel()
        {
            _view.MakeBackplate(new Vector3(-10.25f, 3.1f, 0), 5.9f, 6.9f, 0.84f, 90, "panel_status");

            var portrait = _view.MakeSprite(SpriteMap.Hero(SpriteMap.HeroDirDown, SpriteMap.WalkCycle[0]),
                new Vector3(-12.15f, 5.2f, 0), 95, "portrait");
            portrait.transform.localScale = new Vector3(2f, 2f, 1f);
            _portrait = portrait.GetComponent<SpriteRenderer>();

            var heroName = _view.MakeText(null, new Vector3(-11.15f, 5.35f, 0), 0.5f, TextAnchor.MiddleLeft, 100);
            heroName.text = _text["lbl_hero"];
            heroName.color = new Color(1f, 0.85f, 0.4f);
            heroName.alignment = TextAlignment.Left;

            // 有現成 sprite 的三項戰鬥數值配圖示；金幣/經驗只有文字標籤
            string[] icons =
            {
                SpriteMap.Item["potion_s"], SpriteMap.Item["gem_atk"], SpriteMap.Item["gem_def"], null, null,
            };
            string[] labels =
            {
                _text["lbl_hp"], _text["lbl_atk"], _text["lbl_def"], _text["lbl_gold"], _text["lbl_exp"],
            };
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
                float y = 3.4f - i * 0.76f;
                if (icons[i] != null)
                {
                    var ic = _view.MakeSprite(icons[i], new Vector3(-12.75f, y, 0), 95, $"stat_icon_{i}");
                    ic.transform.localScale = new Vector3(0.7f, 0.7f, 1f);
                }
                var lb = _view.MakeText(null, new Vector3(-12.2f, y, 0), 0.44f, TextAnchor.MiddleLeft, 100);
                lb.text = labels[i];
                lb.alignment = TextAlignment.Left;
                lb.color = new Color(0.85f, 0.85f, 0.9f);

                _statValues[i] = _view.MakeText(null, new Vector3(-7.65f, y, 0), 0.5f, TextAnchor.MiddleRight, 100);
                _statValues[i].alignment = TextAlignment.Right;
                _statValues[i].color = colors[i];
            }
        }

        /// <summary>戰報＝打完的收據，不是詢問框（D7 不設確認，戰前判斷交給常駐預覽）。</summary>
        private void BuildReceipt()
        {
            _receipt = new GameObject("receipt");
            _view.MakeBackplate(new Vector3(0, 0.2f, 0), 11.4f, 4.9f, 0.93f, 110, "receipt_bg")
                .transform.SetParent(_receipt.transform);

            _receiptTitle = _view.MakeText(_receipt.transform, new Vector3(0, 1.85f, 0), 0.55f, TextAnchor.MiddleCenter, 120);
            _receiptTitle.color = new Color(1f, 0.85f, 0.4f);

            var iconGo = new GameObject("receipt_icon");
            iconGo.transform.SetParent(_receipt.transform);
            iconGo.transform.localPosition = new Vector3(-4.15f, 0.35f, 0);
            iconGo.transform.localScale = new Vector3(1.9f, 1.9f, 1f);
            _receiptIcon = iconGo.AddComponent<SpriteRenderer>();
            _receiptIcon.sortingOrder = 120;

            // 鏡像排版：怪物「標籤 值」在左、玩家「值 標籤」在右，標籤朝內（原版做法）
            _receiptLeft = _view.MakeText(_receipt.transform, new Vector3(-3.05f, 1.2f, 0), 0.46f, TextAnchor.UpperLeft, 120);
            _receiptLeft.alignment = TextAlignment.Left;
            _receiptLeft.lineSpacing = 1.2f;

            var heroIcon = _view.MakeSprite(SpriteMap.Hero(SpriteMap.HeroDirDown, 0),
                new Vector3(4.15f, 0.35f, 0), 120, "receipt_hero");
            heroIcon.transform.SetParent(_receipt.transform);
            heroIcon.transform.localScale = new Vector3(1.9f, 1.9f, 1f);

            _receiptRight = _view.MakeText(_receipt.transform, new Vector3(3.05f, 1.2f, 0), 0.46f, TextAnchor.UpperRight, 120);
            _receiptRight.alignment = TextAlignment.Right;
            _receiptRight.lineSpacing = 1.2f;

            _receiptLoss = _view.MakeText(_receipt.transform, new Vector3(0, -1.35f, 0), 0.5f, TextAnchor.MiddleCenter, 120);
            _receiptLoss.color = new Color(1f, 0.45f, 0.4f);
            _receiptReward = _view.MakeText(_receipt.transform, new Vector3(0, -2.0f, 0), 0.5f, TextAnchor.MiddleCenter, 120);
            _receiptReward.color = new Color(1f, 0.55f, 0.85f); // 原版的桃紅獎勵列
            _receipt.SetActive(false);
        }

        private void BuildDialogue()
        {
            _dialogueBox = new GameObject("dialogue");
            _view.MakeBackplate(new Vector3(0, -5.95f, 0), 13f, 1.5f, 0.86f, 90, "dlg_bg")
                .transform.SetParent(_dialogueBox.transform);
            _dialogueSpeaker = _view.MakeText(_dialogueBox.transform, new Vector3(-6.1f, -5.62f, 0), 0.42f, TextAnchor.MiddleLeft, 100);
            _dialogueSpeaker.color = new Color(1f, 0.85f, 0.4f);
            _dialogueSpeaker.alignment = TextAlignment.Left;
            _dialogueText = _view.MakeText(_dialogueBox.transform, new Vector3(-6.1f, -6.18f, 0), 0.46f, TextAnchor.MiddleLeft, 100);
            _dialogueText.alignment = TextAlignment.Left;
            _dialogueBox.SetActive(false);
        }

        // ---- 更新 ----

        public void SetFloor(string label, string nameZh)
            => _floorBanner.text = string.IsNullOrEmpty(nameZh) ? label : $"{label}　{nameZh}";

        /// <summary>頭像跟著棋盤上的主角同步轉向與換幀——由 Bootstrap 在每次換圖時呼叫。</summary>
        public void SetPortrait(Sprite sprite)
        {
            if (sprite != null) _portrait.sprite = sprite;
        }

        public void SetStats(GameState s)
        {
            _statValues[0].text = s.Hp.ToString();
            _statValues[1].text = s.Atk.ToString();
            _statValues[2].text = s.Def.ToString();
            _statValues[3].text = s.Gold.ToString();
            _statValues[4].text = s.Exp.ToString();
            _keyCounts[0].text = $"×  {s.KeysYellow}";
            _keyCounts[1].text = $"×  {s.KeysBlue}";
            _keyCounts[2].text = $"×  {s.KeysRed}";
            _keyCounts[3].text = $"×  {s.Hourglasses}";
        }

        /// <summary>開啟 VS 面板並填入起始數值；之後由 <see cref="SetBattleHp"/> 逐回合更新。</summary>
        public void OpenBattle(MonsterDefinition m, int monsterHp, GameState player)
        {
            _receiptTitle.text = $"{m.NameZh}　　{_text["lbl_vs"]}　　{_text["lbl_hero"]}";
            _receiptIcon.sprite = _view.GetSprite(SpriteMap.MonsterFrame(m.Id, 0));
            _battleMonster = m;
            SetBattleHp(monsterHp, player.Hp, player);
            _receiptLoss.text = "";
            _receiptReward.text = "";
            _receipt.SetActive(true);
            _receiptUntil = float.MaxValue; // 演出期間不自動關
        }

        /// <summary>逐回合更新雙方體力——原版的體感來自看著數字一格一格掉。</summary>
        public void SetBattleHp(int monsterHp, int playerHp, GameState player)
        {
            var m = _battleMonster;
            _receiptLeft.text = $"{_text["lbl_hp"]}：{Mathf.Max(0, monsterHp)}\n{_text["lbl_atk"]}：{m.Atk}\n{_text["lbl_def"]}：{m.Def}";
            _receiptRight.text = $"{playerHp}：{_text["lbl_hp"]}\n{player.Atk}：{_text["lbl_atk"]}\n{player.Def}：{_text["lbl_def"]}";
        }

        /// <summary>結算列：損血與獎勵，並開始倒數關閉。</summary>
        public void CloseBattle(MonsterDefinition m, in CollisionOutcome outcome, float seconds = 1.5f)
        {
            _receiptLoss.text = $"{_text["lbl_hp"]} -{outcome.ExpectedLoss}";
            _receiptReward.text =
                $"{_text["lbl_victory"]}　{_text["lbl_reward_exp"]} +{m.ExpDrop}　{_text["lbl_reward_gold"]} +{m.GoldDrop}";
            _receiptUntil = Time.time + seconds;
        }

        /// <summary>面板上的座標：怪物頭像 / 勇者頭像，供演出層放爆閃與傷害數字。</summary>
        public Vector3 BattleMonsterAnchor => _receipt.transform.position + new Vector3(-4.15f, 0.35f, 0);
        public Vector3 BattleHeroAnchor => _receipt.transform.position + new Vector3(4.15f, 0.35f, 0);

        private MonsterDefinition _battleMonster;

        public void HideReceipt() => _receipt.SetActive(false);

        public void Toast(string msg, float seconds = 1.6f)
        {
            _toast.text = msg;
            _toast.gameObject.SetActive(true);
            _toastUntil = Time.time + seconds;
        }

        public void ShowDialogue(in TextBank.Line line)
        {
            _dialogueSpeaker.text = $"【{line.Speaker}】";
            _dialogueText.text = line.Text;
            _dialogueBox.SetActive(true);
        }

        public void HideDialogue() => _dialogueBox.SetActive(false);

        /// <summary>逾時的提示自動關閉。</summary>
        public void Tick()
        {
            if (_toast.gameObject.activeSelf && Time.time >= _toastUntil)
                _toast.gameObject.SetActive(false);
        }
    }
}
