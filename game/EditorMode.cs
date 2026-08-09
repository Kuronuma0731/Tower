using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;
using Tower.Core.Combat;
using Tower.Core.Commands;
using Tower.Core.Data;
using Tower.Core.Floors;
using Tower.Core.Grid;
using Tower.Core.Simulation;

namespace Tower.Game
{
    /// <summary>
    /// 關卡編輯器（floor-authoring.md ③ 的六項 MVP 規格）。
    ///
    /// **做成遊戲內的模式，不是 Godot 的 EditorPlugin**：
    /// 直接重用 `ViewFactory`／`SpriteMap`／`FloorSolver`，所見即遊戲實際長相；
    /// 而且在手機尺寸下就能編，不必在編輯器與遊戲之間來回猜版面。
    /// EditorPlugin 要另外處理一套 UI 與資源生命週期，對 D12 的一人開發不划算。
    ///
    /// 六項規格：
    ///   ① 13×13 畫地形（W . ^ v &lt; &gt; 六種筆刷）
    ///   ② 從 CSV 下拉選 ref 放實體
    ///   ③ eid 自動生成，人手不可編輯
    ///   ④ 讀寫 floors/*.json
    ///   ⑤ 一鍵跑驗證器並顯示結果
    ///   ⑥ 樓梯配對檢查
    /// 明確不做：復原/重做、多層同開、框選、美化預覽。
    /// </summary>
    public sealed class EditorMode
    {
        private readonly Node _host;
        private readonly ViewFactory _view;
        private readonly TextBank _text;
        private readonly Catalog _catalog;
        private readonly FloorRegistry _floors;

        private Node2D _board;
        private CanvasLayer _ui;
        private Label _status, _brushLabel;
        private OptionButton _floorPick, _brushPick;

        private string _floorId;
        private char[,] _terrain;
        private readonly List<FloorEntity> _entities = new List<FloorEntity>();
        private string _nameZh = "";

        /// <summary>目前筆刷。地形是字元；實體是 "type:ref" 形式。</summary>
        private string _brush = "W";

        public bool Active { get; private set; }

        public EditorMode(Node host, ViewFactory view, TextBank text, Catalog catalog, FloorRegistry floors)
        {
            _host = host;
            _view = view;
            _text = text;
            _catalog = catalog;
            _floors = floors;
        }

        // ---- 開關 ----

        public void Open(string floorId)
        {
            Active = true;
            Load(floorId);
            BuildUi();
            Redraw();
        }

        public void Close()
        {
            Active = false;
            _board?.QueueFree(); _board = null;
            _ui?.QueueFree(); _ui = null;
        }

        private void Load(string floorId)
        {
            _floorId = floorId;
            var f = _floors[floorId];
            _nameZh = f.NameZh;

            _terrain = new char[FloorGrid.Size, FloorGrid.Size];
            for (int y = 0; y < FloorGrid.Size; y++)
            for (int x = 0; x < FloorGrid.Size; x++)
                _terrain[x, y] = f.Grid[new GridPos(x, y)] == TerrainType.Wall ? 'W' : '.';

            _entities.Clear();
            _entities.AddRange(f.Entities);
        }

        // ---- ③ eid 自動生成 ----

        /// <summary>
        /// eid 格式 `F08_m01`。**人手永不編輯**——手打 eid 是撞號與 dangling reference
        /// 的頭號來源，而 `GameState` 的封閉經濟帳本（D11）完全靠 eid 記帳。
        /// </summary>
        private string NextEid(EntityType type)
        {
            string tag = type switch
            {
                EntityType.Monster => "m", EntityType.Item => "i", EntityType.Door => "d",
                EntityType.Stairs => "s", EntityType.Npc => "n", EntityType.Shop => "sh",
                EntityType.Altar => "a", EntityType.Switch => "sw", _ => "sp",
            };
            for (int n = 1; n < 100; n++)
            {
                string candidate = $"{_floorId}_{tag}{n:D2}";
                if (_entities.All(e => e.Eid != candidate)) return candidate;
            }
            throw new InvalidOperationException($"{_floorId} 的 {tag} 實體超過 99 個");
        }

        // ---- ① ② 編輯 ----

        public void Paint(GridPos pos)
        {
            if (!Active) return;
            if (pos.X < 0 || pos.Y < 0 || pos.X >= FloorGrid.Size || pos.Y >= FloorGrid.Size) return;

            if (_brush.Length == 1)
            {
                // 地形筆刷：改地形，並清掉該格的實體（實體不能站在牆上）
                _terrain[pos.X, pos.Y] = _brush[0];
                if (_brush[0] == 'W') _entities.RemoveAll(e => e.Pos == pos);
            }
            else if (_brush == "erase")
            {
                _entities.RemoveAll(e => e.Pos == pos);
            }
            else
            {
                _entities.RemoveAll(e => e.Pos == pos);       // 一格一個實體
                _entities.Add(MakeEntity(_brush, pos));
                if (_terrain[pos.X, pos.Y] == 'W') _terrain[pos.X, pos.Y] = '.';  // 實體所在必為地板
            }
            Redraw();
        }

        private FloorEntity MakeEntity(string brush, GridPos pos)
        {
            var parts = brush.Split(':');
            string kind = parts[0];
            string arg = parts.Length > 1 ? parts[1] : null;

            return kind switch
            {
                "monster" => new FloorEntity(NextEid(EntityType.Monster), EntityType.Monster, pos, @ref: arg),
                "item" => new FloorEntity(NextEid(EntityType.Item), EntityType.Item, pos, @ref: arg),
                "door" => new FloorEntity(NextEid(EntityType.Door), EntityType.Door, pos,
                    doorTier: arg switch { "blue" => KeyTier.Blue, "red" => KeyTier.Red, _ => KeyTier.Yellow }),
                "stairs" => new FloorEntity(NextEid(EntityType.Stairs), EntityType.Stairs, pos,
                    stairs: arg == "down" ? StairsDirection.Down : StairsDirection.Up),
                "npc" => new FloorEntity(NextEid(EntityType.Npc), EntityType.Npc, pos, dialogueId: arg),
                "spawn" => new FloorEntity(NextEid(EntityType.Spawn), EntityType.Spawn, pos),
                _ => throw new ArgumentException($"未知的筆刷 {brush}"),
            };
        }

        // ---- ④ 讀寫 JSON ----

        /// <summary>
        /// 輸出到 `user://floors_out/`——**不寫回 res://**，那在打包後是唯讀的。
        /// 編輯完的檔案要手動搬進 repo 的 data/floors/，這一步刻意留給人，
        /// 因為它同時是「這一層通過驗證了嗎」的檢查點。
        /// </summary>
        public void Save()
        {
            string json = ToJson();
            DirAccess.MakeDirRecursiveAbsolute("user://floors_out");
            string path = $"user://floors_out/{_floorId}.json";
            using var f = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Write);
            if (f == null) { SetStatus($"寫入失敗：{Godot.FileAccess.GetOpenError()}"); return; }
            f.StoreString(json);
            SetStatus($"已存到 {ProjectSettings.GlobalizePath(path)}");
        }

        private string ToJson()
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"  \"id\": \"{_floorId}\",");
            sb.AppendLine($"  \"name_zh\": \"{_nameZh}\",");
            sb.AppendLine("  \"terrain\": [");
            for (int y = 0; y < FloorGrid.Size; y++)
            {
                var row = new StringBuilder();
                for (int x = 0; x < FloorGrid.Size; x++) row.Append(_terrain[x, y]);
                sb.AppendLine($"    \"{row}\"{(y == FloorGrid.Size - 1 ? "" : ",")}");
            }
            sb.AppendLine("  ],");
            sb.AppendLine("  \"entities\": [");
            for (int i = 0; i < _entities.Count; i++)
            {
                var e = _entities[i];
                var p = new StringBuilder($"{{ \"eid\": \"{e.Eid}\", \"type\": \"{e.Type.ToString().ToLowerInvariant()}\"");
                if (!string.IsNullOrEmpty(e.Ref)) p.Append($", \"ref\": \"{e.Ref}\"");
                if (e.Type == EntityType.Door) p.Append($", \"tier\": \"{e.DoorTier.ToString().ToLowerInvariant()}\"");
                if (e.Type == EntityType.Stairs) p.Append($", \"dir\": \"{e.Stairs.ToString().ToLowerInvariant()}\"");
                if (!string.IsNullOrEmpty(e.DialogueId)) p.Append($", \"dialogue\": \"{e.DialogueId}\"");
                p.Append($", \"x\": {e.Pos.X}, \"y\": {e.Pos.Y} }}");
                sb.AppendLine($"    {p}{(i == _entities.Count - 1 ? "" : ",")}");
            }
            sb.AppendLine("  ]");
            sb.AppendLine("}");
            return sb.ToString();
        }

        // ---- ⑤ ⑥ 驗證 ----

        /// <summary>
        /// 一鍵驗證：解析 → 可解性 → 守衛有效性 → 樓梯配對。
        /// 不可解時要說出**缺什麼**，不是只說「不可解」——那對修佈局沒有幫助。
        /// </summary>
        public void Verify()
        {
            FloorDefinition floor;
            try { floor = FloorJson.Parse(ToJson()); }
            catch (Exception e) { SetStatus($"✖ 資料不合法：{e.Message}"); return; }

            var lines = new List<string>();

            // ⑥ 樓梯配對（座標對齊規約）
            int n = FloorRegistry.NumberOf(_floorId);
            var up = floor.FindStairs(StairsDirection.Up);
            var down = floor.FindStairs(StairsDirection.Down);
            CheckPair(lines, $"F{n + 1:D2}", up, StairsDirection.Down, "上");
            CheckPair(lines, $"F{n - 1:D2}", down, StairsDirection.Up, "下");

            // 引用檢查
            var badRefs = floor.Entities
                .Where(e => e.Type == EntityType.Monster && !_catalog.Monsters.ContainsKey(e.Ref)).Select(e => e.Ref)
                .Concat(floor.Entities
                    .Where(e => e.Type == EntityType.Item && !_catalog.Items.ContainsKey(e.Ref)).Select(e => e.Ref))
                .Distinct().ToArray();
            if (badRefs.Length > 0) lines.Add($"✖ 資料表沒有這些 id：{string.Join(",", badRefs)}");

            // 可解性
            var entry = down ?? floor.Entities.FirstOrDefault(e => e.Type == EntityType.Spawn);
            if (entry == null) lines.Add("✖ 沒有下樓梯也沒有 spawn，玩家無處進場");
            else if (up == null) lines.Add("✖ 沒有上樓梯，這層出不去");
            else
            {
                var start = new GameState { Atk = 10, Def = 10, Hp = 1000 };
                var r = new FloorSolver(floor, _catalog.Monsters, _catalog.Items).Solve(start, entry.Pos, up.Pos);
                lines.Add(r.Status switch
                {
                    SolverStatus.Solvable => $"✔ 可解（最佳剩血 {r.BestExitHp}，探索 {r.NodesExplored} 節點）",
                    SolverStatus.Unsolvable => "✖ 不可解：" + Diagnose(floor, entry.Pos, up.Pos),
                    _ => "⚠ 無法斷定：狀態空間過大，佈局要收斂（見 CONTEXT 驗證器詞條）",
                });
            }

            // 守衛有效性——可解性答不出來的問題
            int items = floor.Entities.Count(e => e.Type == EntityType.Item);
            if (items > 0 && entry != null)
            {
                int free = FreeItems(floor, entry.Pos);
                lines.Add(free == items
                    ? $"⚠ {items} 個道具全都不必付代價就拿得到——這層沒有東西是用血換的"
                    : $"✔ {items - free}/{items} 個道具要付出代價");
            }

            SetStatus(string.Join("\n", lines));
        }

        private void CheckPair(List<string> lines, string otherId, FloorEntity mine, StairsDirection want, string dirName)
        {
            if (mine == null) return;
            if (!_floors.Has(otherId)) { lines.Add($"⚠ {dirName}樓梯指向不存在的 {otherId}"); return; }

            var other = _floors[otherId].FindStairs(want);
            if (other == null) lines.Add($"✖ {otherId} 沒有對應的樓梯");
            else if (other.Pos != mine.Pos)
                lines.Add($"✖ 樓梯未對齊：本層{dirName}樓梯 {mine.Pos} ≠ {otherId} 的 {other.Pos}");
            else lines.Add($"✔ 與 {otherId} 樓梯對齊於 {mine.Pos}");
        }

        /// <summary>不可解時說出缺口——「差幾把鑰匙、差多少血」比「不可解」有用得多。</summary>
        private string Diagnose(FloorDefinition floor, GridPos entry, GridPos exit)
        {
            int doors = floor.Entities.Count(e => e.Type == EntityType.Door);
            int keys = floor.Entities.Count(e => e.Type == EntityType.Item
                                                 && _catalog.Items.TryGetValue(e.Ref, out var it)
                                                 && it.Category == ItemCategory.Key);
            var walls = floor.Entities
                .Where(e => e.Type == EntityType.Monster && _catalog.Monsters.ContainsKey(e.Ref))
                .Select(e => (e, o: CombatResolver.ResolveCollision(new PlayerStats(10, 10), _catalog.Monsters[e.Ref])))
                .Where(t => !t.o.Winnable || t.o.ExpectedLoss >= 1000)
                .ToArray();

            var bits = new List<string>();
            if (doors > keys) bits.Add($"門 {doors} 扇但鑰匙只有 {keys} 把");
            if (walls.Length > 0)
                bits.Add($"開局打不動/會死的怪 {walls.Length} 隻（{string.Join("、", walls.Take(3).Select(t => $"{_catalog.Monsters[t.e.Ref].NameZh}{t.e.Pos}"))}）");
            if (bits.Count == 0) bits.Add("路被地形封死，或出口不在可達區");
            return string.Join("；", bits);
        }

        private int FreeItems(FloorDefinition floor, GridPos from)
        {
            var seen = new HashSet<GridPos> { from };
            var q = new Queue<GridPos>();
            q.Enqueue(from);
            while (q.Count > 0)
            {
                var p = q.Dequeue();
                foreach (var d in new[] { new GridPos(p.X + 1, p.Y), new GridPos(p.X - 1, p.Y),
                                          new GridPos(p.X, p.Y + 1), new GridPos(p.X, p.Y - 1) })
                {
                    if (seen.Contains(d) || !floor.Grid.CanStep(p, d)) continue;
                    var e = floor.EntityAt(d);
                    if (e != null && (e.Type == EntityType.Monster || e.Type == EntityType.Door || e.Type == EntityType.Npc))
                        continue;
                    seen.Add(d); q.Enqueue(d);
                }
            }
            return floor.Entities.Count(e => e.Type == EntityType.Item && seen.Contains(e.Pos));
        }

        // ---- 畫面 ----

        private void Redraw()
        {
            _board?.QueueFree();
            _board = new Node2D { Position = new Vector2(360, 44), Scale = new Vector2(1.5f, 1.5f) };
            _host.AddChild(_board);
            _view.Board = _board;

            for (int y = 0; y < FloorGrid.Size; y++)
            for (int x = 0; x < FloorGrid.Size; x++)
            {
                var at = new Vector2(x * 32 + 16, y * 32 + 16);
                _view.MakeSprite(_terrain[x, y] == 'W' ? SpriteMap.TileWall : SpriteMap.TileFloor, at, 0);
                if (_terrain[x, y] is '^' or 'v' or '<' or '>')
                {
                    var arrow = _view.MakeLabel(_board, at - new Vector2(16, 16), 18,
                        HorizontalAlignment.Center, new Color(1f, 0.9f, 0.4f), 30);
                    arrow.Text = _terrain[x, y].ToString();
                    arrow.Size = new Vector2(32, 32);
                }
            }

            foreach (var e in _entities)
            {
                string sprite = SpriteMap.For(e);
                var at = new Vector2(e.Pos.X * 32 + 16, e.Pos.Y * 32 + 16);
                if (sprite != null) _view.MakeSprite(sprite, at, 10);
                else
                {
                    var mark = _view.MakeLabel(_board, at - new Vector2(16, 16), 16,
                        HorizontalAlignment.Center, new Color(0.6f, 1f, 0.6f), 30);
                    mark.Text = "S";           // spawn 沒有 sprite
                    mark.Size = new Vector2(32, 32);
                }
            }
        }

        private void SetStatus(string s) { if (_status != null) _status.Text = s; }

        private void BuildUi()
        {
            _ui = new CanvasLayer { Layer = 20 };
            _host.AddChild(_ui);

            var panel = ViewFactory.Anchored(_ui, ViewFactory.Side.Start, ViewFactory.Side.Start,
                new Vector2(8, 8), new Vector2(340, 700));
            _view.MakePanel(panel, Vector2.Zero, new Vector2(340, 700), 0.9f);

            var title = _view.MakeLabel(panel, new Vector2(10, 6), 20, HorizontalAlignment.Left,
                new Color(1f, 0.85f, 0.4f));
            title.Text = _text["lbl_editor"];

            _floorPick = new OptionButton { Position = new Vector2(10, 36), Size = new Vector2(150, 30) };
            foreach (var id in _floors.Order) _floorPick.AddItem(id);
            _floorPick.Selected = Math.Max(0, _floors.Order.ToList().IndexOf(_floorId));
            _floorPick.ItemSelected += i => { Load(_floors.Order[(int)i]); Redraw(); SetStatus(""); };
            panel.AddChild(_floorPick);

            _brushPick = new OptionButton { Position = new Vector2(170, 36), Size = new Vector2(160, 30) };
            foreach (var (label, brush) in Brushes()) _brushPick.AddItem(label);
            _brushPick.ItemSelected += i => { _brush = Brushes()[(int)i].brush; _brushLabel.Text = Brushes()[(int)i].label; };
            panel.AddChild(_brushPick);

            _brushLabel = _view.MakeLabel(panel, new Vector2(10, 74), 16, HorizontalAlignment.Left, Colors.White);
            _brushLabel.Text = Brushes()[0].label;

            AddButton(panel, new Vector2(10, 100), _text["lbl_verify"], Verify);
            AddButton(panel, new Vector2(120, 100), _text["lbl_save"], Save);

            _status = _view.MakeLabel(panel, new Vector2(10, 140), 15, HorizontalAlignment.Left, Colors.White);
            _status.Size = new Vector2(320, 540);
            _status.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _status.VerticalAlignment = VerticalAlignment.Top;
        }

        private void AddButton(Node parent, Vector2 pos, string text, Action onPress)
        {
            var b = new Button { Position = pos, Size = new Vector2(100, 30), Text = text };
            b.Pressed += onPress;
            parent.AddChild(b);
        }

        /// <summary>筆刷清單。實體 ref 直接來自 CSV——資料表加一隻怪，編輯器立刻有得選。</summary>
        private (string label, string brush)[] Brushes()
        {
            var list = new List<(string, string)>
            {
                ("地形：牆 W", "W"), ("地形：地板 .", "."),
                ("地形：單向 ^", "^"), ("地形：單向 v", "v"),
                ("地形：單向 <", "<"), ("地形：單向 >", ">"),
                ("擦除實體", "erase"),
                ("樓梯：上", "stairs:up"), ("樓梯：下", "stairs:down"),
                ("門：黃", "door:yellow"), ("門：藍", "door:blue"), ("門：紅", "door:red"),
                ("出生點", "spawn"),
            };
            list.AddRange(_catalog.Monsters.Keys.OrderBy(k => k).Select(k => ($"怪：{_catalog.Monsters[k].NameZh}", $"monster:{k}")));
            list.AddRange(_catalog.Items.Keys.OrderBy(k => k).Select(k => ($"道具：{_catalog.Items[k].NameZh}", $"item:{k}")));
            return list.ToArray();
        }

        /// <summary>把畫面座標換成格子座標（棋盤位移 360,44、放大 1.5）。</summary>
        public GridPos GridAt(Vector2 screen)
            => new GridPos(
                Mathf.FloorToInt((screen.X - 360) / (32 * 1.5f)),
                Mathf.FloorToInt((screen.Y - 44) / (32 * 1.5f)));
    }
}
