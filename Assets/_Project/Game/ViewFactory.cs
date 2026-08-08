using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Tower.Game
{
    /// <summary>
    /// 場景物件工廠：sprite 載入與快取、TextMesh、底板。
    /// 純建構，不知道任何遊戲規則——換渲染方式只動這裡。
    /// </summary>
    public sealed class ViewFactory
    {
        private readonly Dictionary<string, Sprite> _sprites = new Dictionary<string, Sprite>();
        private readonly string _spriteDir;
        private readonly Font _font;
        private Sprite _solid;

        public Transform Root { get; set; }

        public ViewFactory(string spriteDir)
        {
            _spriteDir = spriteDir;
            _font = LoadCjkFont();
        }

        /// <summary>
        /// 挑一個系統上真的存在的 CJK 字型——硬要名字會因 OS 語系回報名不同而失敗
        /// （首次遊測實證：HUD 整條隱形）。正式 UI 必須改內嵌字型資產。
        /// </summary>
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

        /// <summary>D14 像素風：Point filter + Clamp + PPU 對齊格子，否則整套糊掉。</summary>
        public Sprite GetSprite(string name)
        {
            if (name == null) return null;
            if (_sprites.TryGetValue(name, out var s)) return s;

            string path = Path.Combine(_spriteDir, name + ".png");
            if (!File.Exists(path)) { Debug.LogWarning($"[View] 缺少素材 {name}"); return null; }

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(File.ReadAllBytes(path));
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            float ppu = Mathf.Max(tex.width, tex.height); // 32px 素材 → 每格一單位
            s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), ppu);
            _sprites[name] = s;
            return s;
        }

        public GameObject MakeSprite(string spriteName, Vector3 pos, int order, string goName)
        {
            var go = new GameObject(goName);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GetSprite(spriteName);
            sr.sortingOrder = order;
            go.transform.position = pos;
            if (Root != null) go.transform.SetParent(Root, true);
            return go;
        }

        public TextMesh MakeText(Transform parent, Vector3 localPos, float unitHeight, TextAnchor anchor, int order)
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
            go.GetComponent<MeshRenderer>().sortingOrder = order;
            return tm;
        }

        public GameObject MakeBackplate(Vector3 pos, float w, float h, float alpha, int order, string name)
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
    }
}
