using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using Tower.Core.Grid;

namespace Tower.Core.Floors
{
    /// <summary>
    /// 樓層 JSON → <see cref="FloorDefinition"/>（格式見 docs/data-schema.md 第 3 節）。
    ///
    /// 為什麼樓層要變成資料：手寫 C# 樓層類別每層約 100 行，D6 要 25–30 層、D12 只有一個人，
    /// 這在數學上就不可行；而且 `floor-authoring.md` 的五步流程本來就寫著「編輯器 → floor JSON
    /// → 驗證器」，只是載入器一直沒做，於是「能跑的」與「文件承諾的」走成兩條路。
    ///
    /// 與 <see cref="Data.Catalog"/> 同樣的形狀：**吃 JSON 文字，不吃路徑**。Core 因此不需要
    /// 知道 `res://` 或檔案系統，遊戲與驗證器可以從不同來源餵同一個函式。
    ///
    /// 壞資料一律在載入期擲例外——樓層資料是人（或編輯器）產生的，錯字不能流到玩家手上。
    /// </summary>
    public static class FloorJson
    {
        public static FloorDefinition Parse(string json)
        {
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            });
            var root = doc.RootElement;

            string id = Str(root, "id");
            string nameZh = root.TryGetProperty("name_zh", out var n) ? n.GetString() : "";

            if (!root.TryGetProperty("terrain", out var terrainEl) || terrainEl.ValueKind != JsonValueKind.Array)
                throw new ArgumentException($"{id}: 缺少 terrain（字元列陣列）");
            var rows = new List<string>();
            foreach (var row in terrainEl.EnumerateArray()) rows.Add(row.GetString());

            var entities = new List<FloorEntity>();
            if (root.TryGetProperty("entities", out var entsEl))
                foreach (var e in entsEl.EnumerateArray())
                    entities.Add(ParseEntity(id, e));

            return new FloorDefinition(id, FloorGrid.Parse(rows.ToArray()), entities, nameZh);
        }

        private static FloorEntity ParseEntity(string floorId, JsonElement e)
        {
            string eid = Str(e, "eid");
            string typeName = Str(e, "type");
            var pos = new GridPos(Int(e, "x"), Int(e, "y"));

            var type = typeName switch
            {
                "monster" => EntityType.Monster,
                "item" => EntityType.Item,
                "door" => EntityType.Door,
                "stairs" => EntityType.Stairs,
                "npc" => EntityType.Npc,
                "shop" => EntityType.Shop,
                "altar" => EntityType.Altar,
                "switch" => EntityType.Switch,
                "spawn" => EntityType.Spawn,
                _ => throw new ArgumentException($"{floorId}/{eid}: 未知的實體型別 '{typeName}'"),
            };

            string @ref = e.TryGetProperty("ref", out var r) ? r.GetString() : null;
            string dialogue = e.TryGetProperty("dialogue", out var d) ? d.GetString() : null;

            var tier = KeyTier.Yellow;
            if (type == EntityType.Door)
            {
                string t = Str(e, "tier");
                tier = t switch
                {
                    "yellow" => KeyTier.Yellow,
                    "blue" => KeyTier.Blue,
                    "red" => KeyTier.Red,
                    _ => throw new ArgumentException($"{floorId}/{eid}: 未知的門等級 '{t}'"),
                };
            }

            var dir = StairsDirection.Up;
            if (type == EntityType.Stairs)
            {
                string s = Str(e, "dir");
                dir = s switch
                {
                    "up" => StairsDirection.Up,
                    "down" => StairsDirection.Down,
                    _ => throw new ArgumentException($"{floorId}/{eid}: 未知的樓梯方向 '{s}'"),
                };
            }

            return new FloorEntity(eid, type, pos, @ref: @ref, doorTier: tier, stairs: dir, dialogueId: dialogue);
        }

        private static string Str(JsonElement e, string name)
            => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
                ? v.GetString()
                : throw new ArgumentException($"缺少字串欄位 '{name}'");

        private static int Int(JsonElement e, string name)
            => e.TryGetProperty(name, out var v) && v.TryGetInt32(out int i)
                ? i
                : throw new ArgumentException($"缺少整數欄位 '{name}'");
    }
}
