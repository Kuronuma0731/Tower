using System;
using Godot;
using Tower.Core.Commands;
using Tower.Core.Data;
using Tower.Core.Floors;

namespace Tower.Game
{
    /// <summary>
    /// 商店與祭壇的面板（D8 雙貨幣：金幣買道具、經驗買屬性）。
    ///
    /// **D7 全域硬核**：即點即發，沒有「確定要買嗎」。按錯就是換錯，沒有救濟。
    /// 允許的緩解只有**布局防護**——按鈕拉開間距，且每一列都印出「買完會變成多少」，
    /// 讓玩家在按下去之前就看得到後果。這是 D7 明文准許的唯一手段。
    ///
    /// 遞增價（D8 衍生規則）：每列顯示的是**這一次**的價格，不是基價。
    /// </summary>
    public sealed class ShopView
    {
        private const int RowHeight = 46;

        private readonly ViewFactory _view;
        private readonly TextBank _text;
        private readonly CanvasLayer _layer;

        private Control _panel;
        private Action _onChanged;

        public bool Open => _panel != null && _panel.Visible;

        public ShopView(ViewFactory view, TextBank text, Node root)
        {
            _view = view;
            _text = text;
            _layer = new CanvasLayer { Layer = 15 };
            root.AddChild(_layer);
        }

        public void Close()
        {
            _panel?.QueueFree();
            _panel = null;
        }

        public void ShowShop(ShopDefinition shop, Catalog catalog, GameState state,
                             Action<IGameCommand> apply, Action onChanged)
        {
            _onChanged = onChanged;
            Build(_text["lbl_shop"], shop.Offers.Count, (i, row) =>
            {
                var offer = shop.Offers[i];
                var item = catalog.Items[offer.ItemId];
                state.PurchaseCounts.TryGetValue($"{shop.Id}:{offer.ItemId}", out int n);
                int price = offer.PriceAt(n);

                bool afford = state.Gold >= price;
                AddRow(row, $"{item.NameZh}　{price} {_text["lbl_gold"]}",
                    $"{_text["lbl_gold"]} {state.Gold} → {state.Gold - price}", afford,
                    () =>
                    {
                        apply(new PurchaseCommand(shop.Id, item, price));
                        Refresh(() => ShowShop(shop, catalog, state, apply, onChanged));
                    });
            });
        }

        public void ShowAltar(AltarDefinition altar, GameState state,
                              Action<IGameCommand> apply, Action onChanged)
        {
            _onChanged = onChanged;
            Build(_text["lbl_altar"], altar.Offers.Count, (i, row) =>
            {
                var offer = altar.Offers[i];
                string key = $"{altar.Id}:{offer.Stat.ToString().ToLowerInvariant()}";
                state.PurchaseCounts.TryGetValue(key, out int n);
                int cost = offer.CostAt(n);

                (string label, int now) = offer.Stat switch
                {
                    AltarStat.Atk => (_text["lbl_atk"], state.Atk),
                    AltarStat.Def => (_text["lbl_def"], state.Def),
                    _ => (_text["lbl_hp"], state.Hp),
                };

                bool afford = state.Exp >= cost;
                AddRow(row, $"{label} +{offer.Gain}　{cost} {_text["lbl_exp"]}",
                    $"{label} {now} → {now + offer.Gain}", afford,
                    () =>
                    {
                        apply(new AltarExchangeCommand(altar.Id, offer, cost));
                        Refresh(() => ShowAltar(altar, state, apply, onChanged));
                    });
            });
        }

        private void Refresh(Action rebuild)
        {
            _onChanged?.Invoke();
            Close();
            rebuild();
        }

        private void Build(string title, int rows, Action<int, Control> fill)
        {
            Close();
            float h = 80 + rows * RowHeight + 50;
            _panel = ViewFactory.Anchored(_layer, ViewFactory.Side.Middle, ViewFactory.Side.Middle,
                new Vector2(136, 0), new Vector2(460, h));
            _panel.MouseFilter = Control.MouseFilterEnum.Stop;
            _view.MakePanel(_panel, Vector2.Zero, new Vector2(460, h), 0.95f, 110);

            var t = _view.MakeLabel(_panel, new Vector2(0, 10), 22, HorizontalAlignment.Center,
                new Color(1f, 0.85f, 0.4f), 120);
            t.Text = title;
            t.Size = new Vector2(460, 30);

            for (int i = 0; i < rows; i++)
            {
                var row = new Control { Position = new Vector2(0, 56 + i * RowHeight), Size = new Vector2(460, RowHeight) };
                _panel.AddChild(row);
                fill(i, row);
            }

            var close = new Button
            {
                Position = new Vector2(180, h - 42), Size = new Vector2(100, 32),
                Text = _text["lbl_leave"],
            };
            close.Pressed += Close;
            _panel.AddChild(close);
        }

        /// <summary>
        /// 一列：左邊「買什麼、多少錢」，右邊「買完會變成多少」。
        /// 右邊那欄是 D7 布局防護的核心——不設確認框，就得讓後果在按下去之前就看得見。
        /// </summary>
        private void AddRow(Control row, string what, string after, bool affordable, Action onBuy)
        {
            var b = new Button
            {
                Position = new Vector2(16, 4), Size = new Vector2(200, 34),
                Text = what,
                Disabled = !affordable,
            };
            b.Pressed += onBuy;
            row.AddChild(b);

            var preview = _view.MakeLabel(row, new Vector2(230, 4), 17, HorizontalAlignment.Left,
                affordable ? new Color(0.75f, 0.9f, 0.75f) : new Color(0.7f, 0.5f, 0.5f), 120);
            preview.Text = affordable ? after : _text["msg_cannot_afford"];
            preview.Size = new Vector2(215, 34);
        }
    }
}
