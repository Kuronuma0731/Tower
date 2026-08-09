using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Tower.EditorDev
{
    /// <summary>
    /// 開發自動化。[InitializeOnLoad]：每次進 Play 自動把 Game 視窗縮放歸 1——
    /// 首次遊測教訓：殘留的 Scale 1.6x 會把棋盤邊緣與 HUD 裁出畫面。
    /// </summary>
    [InitializeOnLoad]
    public static class DevAutomation
    {
        static DevAutomation()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode)
                    ResetZoomSoon();
            };
        }

        /// <summary>命令列 -executeMethod 掛勾：開專案直接進 Play。</summary>
        public static void PlayNow()
        {
            EditorApplication.EnterPlaymode();
        }

        /// <summary>
        /// 命令列編譯把關。**不要用 `-batchmode -quit` 驗編譯**——它會在編譯完成前就退出，
        /// log 看起來乾乾淨淨卻其實沒編（本專案已被這個假陰性騙過兩次）。
        /// 改用：`-batchmode -executeMethod Tower.EditorDev.DevAutomation.CompileOnly`，
        /// Unity 會先完成編譯才執行本方法，之後檢查 log 有無 `error CS` 即可。
        /// </summary>
        public static void CompileOnly()
        {
            Debug.Log("[TowerDev] CompileOnly: 編譯已完成，組件可用。");
            EditorApplication.Exit(0);
        }

        /// <summary>
        /// 無頭截圖：`-batchmode -executeMethod Tower.EditorDev.DevAutomation.Screenshot`
        /// 會在不進 Play 的情況下建好場景、用 RenderTexture 拍一張 PNG 到 Logs/shot.png。
        ///
        /// 為什麼要這個：HUD 是**視覺**工作，而本專案已經三次「程式編得過、畫面上卻看不到」
        /// （字型抓不到、Game 視窗縮放裁切、螢幕空間 UI 偏移）。編譯通過證明不了版面對。
        /// </summary>
        public static void Screenshot()
        {
            const int width = 1920, height = 1080;

            // Start() 在編輯模式不會被呼叫，反射直接叫；場景由 Bootstrap 自己建好（含相機）
            var host = new GameObject("ShotHost");
            var boot = host.AddComponent<Tower.Game.GamePreviewBootstrap>();
            typeof(Tower.Game.GamePreviewBootstrap)
                .GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(boot, null);

            var cam = Object.FindFirstObjectByType<Camera>();
            if (cam == null) { Debug.LogError("[TowerDev] 場景裡沒有相機，截圖中止"); EditorApplication.Exit(1); return; }

            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;

            System.IO.Directory.CreateDirectory("Logs");
            System.IO.File.WriteAllBytes("Logs/shot.png", tex.EncodeToPNG());
            Debug.Log($"[TowerDev] 截圖已存 Logs/shot.png（{width}x{height}）");

            EditorApplication.Exit(0);
        }

        private static int _attempts;

        private static void ResetZoomSoon()
        {
            _attempts = 0;
            EditorApplication.update += TryReset;
        }

        private static void TryReset()
        {
            _attempts++;
            bool done = SnapGameViewZoom(1f);
            if (done || _attempts > 300) // 最多重試 ~5 秒
                EditorApplication.update -= TryReset;
        }

        [MenuItem("Tower/Reset Game View Zoom")]
        public static void ResetGameViewZoomMenu() => SnapGameViewZoom(1f);

        private static bool SnapGameViewZoom(float zoom)
        {
            try
            {
                var t = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
                var gv = EditorWindow.GetWindow(t, false, null, false);
                if (gv == null) return false;

                var snap = t.GetMethod("SnapZoom", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (snap != null)
                {
                    snap.Invoke(gv, new object[] { zoom });
                    gv.Repaint();
                    return true;
                }

                var prop = t.GetProperty("targetScale", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (prop != null)
                {
                    prop.SetValue(gv, zoom);
                    gv.Repaint();
                    return true;
                }

                Debug.LogWarning("[TowerDev] GameView 縮放 API 都不存在，請手動把 Scale 拉到 1x");
                return true; // 放棄重試
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[TowerDev] 縮放重設異常：" + e.Message);
                return true;
            }
        }
    }
}
