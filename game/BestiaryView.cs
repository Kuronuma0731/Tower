using System.Collections.Generic;
using System.Linq;
using Godot;
using Tower.Core.Combat;
using Tower.Core.Commands;
using Tower.Core.Data;

namespace Tower.Game
{
    /// <summary>
    /// 怪物手冊。與**傷害預覽**共同構成玩家的計算依據（CONTEXT 詞條）。
    ///
    /// 為什麼它是必要而不是 QoL：D1 砍掉指令戰之後，怪物的獨特性**全部由特性承擔**。
    /// 但特性在棋盤上是看不見的——魔攻怪和普通怪長得一樣，只有預覽數字不同。
    /// 玩家若不知道「這隻無視防禦」，就無法理解為什麼堆防禦沒用，
    /// 那條設計意圖等於沒有傳達出去。手冊就是那個管道。
    ///
    /// 只列**已遭遇**的怪（`GameState.SeenMonsters`）：沒見過的還不該有情報。
    /// </summary>
    public sealed class BestiaryView
    {
        private const int RowHeight = 92;

        private readonly ViewFactory _view;
        private readonly TextBank _text;
        private readonly Catalog _catalog;
        private readonly CanvasLayer _layer;

        private Control _panel;

        public bool Open => _panel != null && _panel.Visible;

        public BestiaryView(ViewFactory view, TextBank text, Catalog catalog, Node root)
        {
            _view = view;
            _text = text;
            _catalog = catalog;
            _layer = new CanvasLayer { Layer = 16 };
            root.AddChild(_layer);
        }

        public void Close()
        {
            _panel?.QueueFree();
            _panel = null;
        }

        public void Toggle(GameState state)
        {
            if (Open) { Close(); return; }
            Show(state);
        }

        private void Show(GameState state)
        {
            Close();

            // 依「先看到的排前面」不可行（沒記順序），改用資料表順序——同系怪會排在一起
            var seen = _catalog.Monsters.Keys
                .Where(state.SeenMonsters.Contains)
                .ToList();

            const float w = 620;
            float h = Mathf.Min(660, 90 + Mathf.Max(1, seen.Count) * RowHeight);

            _panel = ViewFactory.Anchored(_layer, ViewFactory.Side.Middle, ViewFactory.Side.Middle,
                new Vector2(136, 0), new Vector2(w, h));
            _panel.MouseFilter = Control.MouseFilterEnum.Stop;
            _view.MakePanel(_panel, Vector2.Zero, new Vector2(w, h), 0.95f, 110);

            var title = _view.MakeLabel(_panel, new Vector2(0, 10), 22, HorizontalAlignment.Center,
                new Color(1f, 0.85f, 0.4f), 120);
            title.Text = $"{_text["lbl_bestiary"]}　{seen.Count}/{_catalog.Monsters.Count}";
            title.Size = new Vector2(w, 30);

            if (seen.Count == 0)
            {
                var empty = _view.MakeLabel(_panel, new Vector2(0, 60), 18, HorizontalAlignment.Center,
                    Colors.White, 120);
                empty.Text = _text["msg_bestiary_empty"];
                empty.Size = new Vector2(w, 30);
            }
            else
            {
                // 捲動容器：怪物種類會長到十幾隻，固定高度裝不下
                var scroll = new ScrollContainer
                {
                    Position = new Vector2(8, 48),
                    Size = new Vector2(w - 16, h - 100),
                };
                _panel.AddChild(scroll);

                var list = new VBoxContainer { CustomMinimumSize = new Vector2(w - 40, 0) };
                scroll.AddChild(list);

                foreach (var id in seen) AddEntry(list, _catalog.Monsters[id], state);
            }

            var close = new Button
            {
                Position = new Vector2(w / 2 - 50, h - 42), Size = new Vector2(100, 32),
                Text = _text["lbl_leave"],
            };
            close.Pressed += Close;
            _panel.AddChild(close);
        }

        private void AddEntry(Node list, MonsterDefinition m, GameState state)
        {
            var row = new Control { CustomMinimumSize = new Vector2(0, RowHeight) };
            list.AddChild(row);

            var icon = _view.MakeSprite(SpriteMap.MonsterFrame(m.Id, 0), new Vector2(28, 30), 120, row);
            icon.Scale = new Vector2(1.5f, 1.5f);

            var name = _view.MakeLabel(row, new Vector2(56, 4), 19, HorizontalAlignment.Left,
                m.IsGuardian ? new Color(1f, 0.6f, 0.5f) : new Color(1f, 0.9f, 0.6f), 120);
            name.Text = m.IsGuardian ? $"{m.NameZh}　【{_text["lbl_guardian"]}】" : m.NameZh;

            var stats = _view.MakeLabel(row, new Vector2(56, 28), 16, HorizontalAlignment.Left,
                new Color(0.85f, 0.88f, 0.95f), 120);
            stats.Text = $"{_text["lbl_hp"]} {m.Hp}　{_text["lbl_atk"]} {m.Atk}　{_text["lbl_def"]} {m.Def}" +
                         (m.Agility > 0 ? $"　{_text["lbl_agility"]} {m.Agility}" : "");

            // 現在打得如何——手冊與預覽是同一個結算函式，數字永遠一致
            var outcome = CombatResolver.ResolveCollision(state.CombatStats, m);
            var cost = _view.MakeLabel(row, new Vector2(380, 28), 16, HorizontalAlignment.Left,
                outcome.Winnable && outcome.ExpectedLoss < state.Hp
                    ? new Color(1f, 0.95f, 0.5f) : new Color(1f, 0.4f, 0.35f), 120);
            cost.Text = outcome.Winnable ? $"-{outcome.ExpectedLoss}" : "✖";

            var traits = _view.MakeLabel(row, new Vector2(56, 50), 15, HorizontalAlignment.Left,
                new Color(0.7f, 0.9f, 1f), 120);
            traits.Text = TraitText(m);

            var note = _view.MakeLabel(row, new Vector2(56, 68), 14, HorizontalAlignment.Left,
                new Color(0.72f, 0.72f, 0.78f), 120);
            note.Text = m.BestiaryNote;
            note.Size = new Vector2(520, 20);
        }

        /// <summary>特性的中文名一律來自 ui-strings.csv——玩家可見字串不寫在程式裡。</summary>
        private string TraitText(MonsterDefinition m)
        {
            var parts = new List<string>();
            if (m.Traits.HasFlag(TraitSet.FirstStrike)) parts.Add(_text["lbl_trait_first_strike"]);
            if (m.Traits.HasFlag(TraitSet.MultiHit)) parts.Add(_text["lbl_trait_multi_hit"]);
            if (m.Traits.HasFlag(TraitSet.Pierce)) parts.Add(_text["lbl_trait_pierce"]);
            if (m.Traits.HasFlag(TraitSet.Lifesteal)) parts.Add(_text["lbl_trait_lifesteal"]);
            if (m.Agility > 0) parts.Add(_text["lbl_trait_evasion"]);
            return parts.Count == 0 ? _text["lbl_trait_none"] : string.Join("・", parts);
        }
    }
}
