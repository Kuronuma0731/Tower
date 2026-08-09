using System.Collections.Generic;
using Godot;

namespace Tower.Game
{
    /// <summary>
    /// 場景節點工廠：材質載入與快取、Sprite2D、Label、底板。
    /// 純建構，不知道任何遊戲規則——換渲染方式只動這裡。
    ///
    /// 與 Unity 版的差別：Godot 用節點樹＋Control，所以 HUD 可以安心用螢幕座標
    /// （Unity 時期被 Game 視窗縮放裁掉，被迫整套改成世界空間，那個坑不存在了）。
    /// </summary>
    public sealed class ViewFactory
    {
        private readonly Dictionary<string, Texture2D> _textures = new Dictionary<string, Texture2D>();
        private readonly FontFile _font;

        public Node2D Board { get; set; }

        public ViewFactory()
        {
            _font = LoadCjkFont();
        }

        /// <summary>
        /// 中文字型：內嵌於 res://assets/fonts/。
        ///
        /// Unity 時期是列舉系統字型硬撐的，發行版不可靠（曾整條 HUD 隱形）；Godot 把字型
        /// 當資源打包，這才是正解。**目前放的是開發用的微軟正黑體，不可隨商業版散布**
        /// ——發行前必須換成 OFL 授權字型（如 Noto Sans TC）。見 CONTEXT.md 待決事項。
        /// 字型檔未進版控（`.gitignore`）。
        /// </summary>
        private static FontFile LoadCjkFont()
        {
            foreach (var path in new[]
            {
                "res://assets/fonts/cjk.ttf", "res://assets/fonts/cjk.otf", "res://assets/fonts/cjk.ttc",
            })
                if (Godot.FileAccess.FileExists(path))
                    return ResourceLoader.Load<FontFile>(path);

            GD.PushWarning("[Tower] 找不到內嵌中文字型，中文會顯示不出來");
            return null;
        }

        /// <summary>D14 像素風：Nearest filter，否則 32px 素材整套糊掉。</summary>
        public Texture2D GetTexture(string name)
        {
            if (name == null) return null;
            if (_textures.TryGetValue(name, out var t)) return t;

            string path = SpriteMap.Dir + name + ".png";
            if (!Godot.FileAccess.FileExists(path))
            {
                GD.PushWarning($"[View] 缺少素材 {name}");
                return null;
            }
            t = ResourceLoader.Load<Texture2D>(path);
            _textures[name] = t;
            return t;
        }

        public Sprite2D MakeSprite(string textureName, Vector2 pos, int z, Node parent = null)
        {
            var s = new Sprite2D
            {
                Texture = GetTexture(textureName),
                Position = pos,
                ZIndex = z,
                TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            };
            (parent ?? (Node)Board).AddChild(s);
            return s;
        }

        public Label MakeLabel(Node parent, Vector2 pos, int size, HorizontalAlignment align, Color color, int z = 100)
        {
            var l = new Label
            {
                Position = pos,
                ZIndex = z,
                HorizontalAlignment = align,
                VerticalAlignment = VerticalAlignment.Center,
            };
            l.AddThemeColorOverride("font_color", color);
            l.AddThemeFontSizeOverride("font_size", size);
            if (_font != null) l.AddThemeFontOverride("font", _font);
            parent.AddChild(l);
            return l;
        }

        public ColorRect MakePanel(Node parent, Vector2 pos, Vector2 size, float alpha, int z = 90)
        {
            var r = new ColorRect
            {
                Position = pos,
                Size = size,
                Color = new Color(0.02f, 0.02f, 0.05f, alpha),
                ZIndex = z,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            parent.AddChild(r);
            return r;
        }
    }
}
