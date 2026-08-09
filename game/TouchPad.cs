using Godot;

namespace Tower.Game
{
    /// <summary>
    /// 虛擬方向鍵（D9）——**行動端唯一的移動輸入**。
    ///
    /// 在此之前手機上這個遊戲一步都走不了：只綁了鍵盤。D2 的發行主體是 Android/iOS 買斷，
    /// 所以這不是「行動端優化」，是能不能玩的問題。
    ///
    /// 三個設計約束都來自已鎖定的決策：
    /// - **一步一按**（D9）：按下即走一格，不做長按連走。誤觸率最低，與 D7「誤觸不設防」自洽
    /// - **靠左下**（D9 橫向雙手持握）：左拇指負責方向；右側留給日後的功能鍵
    /// - **按鈕拉開間距**（D7 的布局防護）：D7 不准確認框，只允許用版面把誤觸壓下去
    ///
    /// 用 <see cref="TouchScreenButton"/> 而不是 Control：它天生吃觸控、在桌面版也吃滑鼠，
    /// 且不參與 Control 的焦點系統——不會跟鍵盤輸入打架。
    /// </summary>
    public partial class TouchPad : Node2D
    {
        /// <summary>方向鍵被按下（dx, dy 為格數）。</summary>
        [Signal] public delegate void SteppedEventHandler(int dx, int dy);

        private const int Button = 56;   // 觸控目標邊長；48dp 是 Android 的最小可觸控尺寸，留餘裕
        private const int Gap = 10;      // D7 布局防護：鍵與鍵之間拉開，避免相鄰誤觸

        public static TouchPad Create(Node parent, Vector2 center)
        {
            var pad = new TouchPad { Position = center };
            parent.AddChild(pad);
            pad.Build();
            return pad;
        }

        private void Build()
        {
            int span = Button + Gap;
            Add("up", new Vector2(0, -span), 0, -1, "▲");
            Add("down", new Vector2(0, span), 0, 1, "▼");
            Add("left", new Vector2(-span, 0), -1, 0, "◀");
            Add("right", new Vector2(span, 0), 1, 0, "▶");
        }

        private void Add(string name, Vector2 offset, int dx, int dy, string glyph)
        {
            var btn = new TouchScreenButton
            {
                Name = name,
                Position = offset - new Vector2(Button / 2f, Button / 2f),
                Shape = new RectangleShape2D { Size = new Vector2(Button, Button) },
                // 桌面版也要能點——同一份 UI 在 Steam 上用滑鼠操作
                PassbyPress = false,
            };
            AddChild(btn);

            // 底板與箭頭：純程式繪製，不佔素材（換皮時只動這裡）
            var plate = new ColorRect
            {
                Position = btn.Position,
                Size = new Vector2(Button, Button),
                Color = new Color(0.10f, 0.10f, 0.16f, 0.62f),
                MouseFilter = Control.MouseFilterEnum.Ignore,
                ZIndex = -1,
            };
            AddChild(plate);

            var label = new Label
            {
                Position = btn.Position,
                Size = new Vector2(Button, Button),
                Text = glyph,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            label.AddThemeColorOverride("font_color", new Color(0.92f, 0.92f, 1f, 0.9f));
            label.AddThemeFontSizeOverride("font_size", 24);
            AddChild(label);

            btn.Pressed += () => EmitSignal(SignalName.Stepped, dx, dy);
        }
    }
}
