using Godot;
using Tower.Core.Save;

namespace Tower.Game
{
    /// <summary>
    /// 存檔檔案的讀寫。Core 的 <see cref="SaveGame"/> 只管機制，這裡只管落地。
    ///
    /// 位置用 `user://`——Godot 會依平台展開成正確的可寫目錄
    /// （Windows 的 AppData、Android 的 app 私有空間、iOS 的 Documents）。
    /// 這也是為什麼存檔不能放 `res://`：那是唯讀的打包內容。
    /// </summary>
    public static class SaveFile
    {
        private const string Path = "user://save.json";

        public static bool Exists() => Godot.FileAccess.FileExists(Path);

        public static void Write(SaveGame game)
        {
            using var f = Godot.FileAccess.Open(Path, Godot.FileAccess.ModeFlags.Write);
            if (f == null)
            {
                GD.PushError($"[Save] 寫不進 {Path}：{Godot.FileAccess.GetOpenError()}");
                return;
            }
            f.StoreString(game.ToData().ToJson());
        }

        /// <summary>讀存檔；壞檔或版本不符時回 null，由呼叫端當作新遊戲處理。</summary>
        public static SaveGame Read()
        {
            if (!Exists()) return null;
            try
            {
                return SaveGame.FromData(SaveData.FromJson(Godot.FileAccess.GetFileAsString(Path)));
            }
            catch (System.Exception e)
            {
                // 壞檔不該讓遊戲開不起來——記下來，當新遊戲開始
                GD.PushWarning($"[Save] 存檔無法載入（{e.Message}），以新遊戲開始");
                return null;
            }
        }

        public static void Delete()
        {
            if (Exists()) DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(Path));
        }
    }
}
