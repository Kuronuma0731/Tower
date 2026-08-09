using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Tower.Core.Combat;
using Tower.Core.Commands;
using Tower.Core.Data;
using Tower.Core.Floors;
using Tower.Core.Grid;
using Tower.Core.Simulation;

namespace Tower.Verify
{
    /// <summary>
    /// 樓層驗收——**對 `data/floors/*.json` 裡的每一層自動生效**。
    ///
    /// 以前是三段手寫（F00Checks / F01Checks / F02Checks）加上手寫的三組樓梯配對。
    /// 那個形狀到 F03 就開始抄，到 D6 的 30 層要抄 30 段——而漏抄的那一層不會有人發現。
    /// 現在只要把 JSON 丟進 `data/floors/`，下面所有檢查自動涵蓋它。
    ///
    /// 逐層固定檢查四項：
    ///   ① 引用的怪與道具都在 Catalog 裡（錯字不會流到玩家手上）
    ///   ② 入口與出口都存在
    ///   ③ 可解（從入口到上樓梯）
    ///   ④ 守衛有效性——這層有東西是要用血換的嗎（可解性答不出來的問題）
    /// 樓梯座標對齊由 <see cref="FloorRegistry"/> 在建構時對全部相鄰樓層強制。
    /// </summary>
    internal static class FloorChecks
    {
        /// <summary>
        /// 最佳剩餘 HP 的回歸錨點。**入場血量沿著塔串下去**（前一層的最佳出場血 = 下一層的入場血），
        /// 這比每層各給一個獨立起點誠實得多——難度曲線是累積的，孤立驗每一層看不出整體在變鬆還是變緊。
        ///
        /// 已知限制：`SolverResult` 只回傳 HP，不含攻防，所以串起來的是血量而非完整狀態。
        /// 目前只有 F02 有寶石且它是最後一層，還不影響；擴到 F03+ 時要讓 solver 回傳完整出場狀態。
        /// </summary>
        private const int StartHp = 1000;   // data/balance.csv

        private static readonly Dictionary<string, int> ExpectedExitHp = new()
        {
            ["F00"] = 1168,   // 1000 −32（綠史萊姆）+200（血瓶）
            ["F01"] = 1524,   // 入場 1168，+600（三瓶）−244（蝙蝠132＋綠32＋紅80）
            ["F02"] = 1812,   // 入場 1524，淨 +288
        };

        public static FloorRegistry Run(string repo, Catalog catalog, Action<string, bool> check)
        {
            // 開發用樓層（設定層）也要驗——它不出貨，但**它是功能的測試場**，
            // 資料寫壞了會讓人以為是功能壞了。FloorRegistry 不會把它排進塔（id 非 F##），
            // 所以樓梯配對與難度錨點都不受影響。
            var files = Directory.GetFiles(Path.Combine(repo, "data", "floors"), "*.json")
                .Concat(SafeGlob(Path.Combine(repo, "data", "dev")))
                .OrderBy(f => f).ToArray();

            Console.WriteLine($"== 樓層（{files.Length} 層，含開發用）==");

            var floors = new List<FloorDefinition>();
            foreach (var f in files)
            {
                try { floors.Add(FloorJson.Parse(File.ReadAllText(f))); }
                catch (Exception ex) { check($"{Path.GetFileName(f)} 解析失敗：{ex.Message}", false); }
            }

            // 建構本身就是檢查：座標對齊規約對所有相鄰樓層強制執行
            FloorRegistry registry = null;
            try
            {
                registry = new FloorRegistry(floors);
                check($"樓梯座標對齊（{string.Join(" → ", registry.Order)}）", true);
            }
            catch (Exception ex) { check($"樓梯座標對齊：{ex.Message}", false); }

            int carriedHp = StartHp;
            CrossFloorSwitchTargets(floors, check);

            foreach (var floor in floors)
            {
                var bad = floor.Entities
                    .Where(e => e.Type == EntityType.Monster && !catalog.Monsters.ContainsKey(e.Ref))
                    .Select(e => e.Ref)
                    .Concat(floor.Entities
                        .Where(e => e.Type == EntityType.Item && !catalog.Items.ContainsKey(e.Ref))
                        .Select(e => e.Ref))
                    .Distinct().ToArray();
                check($"{floor.Id} 引用的 id 都在資料表內" + (bad.Length > 0 ? $"（缺：{string.Join(",", bad)}）" : ""),
                    bad.Length == 0);

                var entry = EntryOf(floor);
                var exit = floor.FindStairs(StairsDirection.Up);
                if (entry == null || exit == null)
                {
                    check($"{floor.Id} 有入口與上樓梯", false);
                    continue;
                }

                int expect = ExpectedExitHp.TryGetValue(floor.Id, out var a) ? a : -1;
                var result = new FloorSolver(floor, catalog.Monsters, catalog.Items)
                    .Solve(new GameState { Atk = 10, Def = 10, Hp = carriedHp }, entry.Pos, exit.Pos);

                bool ok = result.Status == SolverStatus.Solvable && (expect < 0 || result.BestExitHp == expect);
                check($"{floor.Id} 可解 {result.Status} 入場 {carriedHp} → 出場 {result.BestExitHp}" +
                      (expect >= 0 ? $"（錨點 {expect}）" : "（無錨點）"), ok);

                if (result.Status == SolverStatus.Solvable) carriedHp = result.BestExitHp;

                GuardEffectiveness(floor, entry.Pos, check);
            }

            return registry;
        }

        /// <summary>
        /// 機關目標的 eid 完整性——**全塔範圍**（`data-schema.md` 的匯入驗證閘門）。
        ///
        /// 機關是全遊戲唯一能造成跨層依賴的機制，而 `FloorSolver` 在每層驗證時**忽略**開關
        /// （註解寫著「由全塔檢查另管」）。那個全塔檢查以前只是一句承諾——
        /// 指向不存在 eid 的機關不會被任何東西發現，而 dangling reference 正是
        /// 跨層結構的頭號事故。
        ///
        /// 這裡只驗「目標存在」，不驗「跨層依賴是否可解」——後者要全塔搜索，
        /// 成本是另一個量級（CONTEXT 驗證器詞條：全塔只做資源總量守恆檢查）。
        /// </summary>
        private static string[] SafeGlob(string dir)
            => Directory.Exists(dir) ? Directory.GetFiles(dir, "*.json") : System.Array.Empty<string>();

        private static void CrossFloorSwitchTargets(List<FloorDefinition> floors, Action<string, bool> check)
        {
            var allEids = new HashSet<string>();
            foreach (var f in floors)
                foreach (var e in f.Entities) allEids.Add(e.Eid);

            var dangling = new List<string>();
            int switches = 0, crossFloor = 0;
            foreach (var f in floors)
            foreach (var e in f.Entities)
            {
                if (e.Type != EntityType.Switch) continue;
                switches++;
                foreach (var t in e.SwitchTargets)
                {
                    if (!allEids.Contains(t)) dangling.Add($"{f.Id}/{e.Eid}→{t}");
                    else if (!t.StartsWith(f.Id + "_", StringComparison.Ordinal)) crossFloor++;
                }
            }

            check($"機關目標 eid 全部存在（{switches} 個機關，其中 {crossFloor} 條跨層）" +
                  (dangling.Count > 0 ? $"　斷鏈：{string.Join("、", dangling)}" : ""),
                dangling.Count == 0);
        }

        /// <summary>入口＝下樓梯（座標對齊規約的落點）；沒有下樓梯的層用 spawn（僅序章層）。</summary>
        private static FloorEntity EntryOf(FloorDefinition floor)
            => floor.FindStairs(StairsDirection.Down)
               ?? floor.Entities.FirstOrDefault(e => e.Type == EntityType.Spawn);

        /// <summary>
        /// 守衛有效性：一隻怪若「宣稱」守著某個道具，那在殺掉牠之前該道具就必須拿不到。
        /// 這是可解性抓不到、只有實際遊玩才會現形的一類錯——F02 第一版的邊緣走廊讓玩家
        /// 繞過守衛白拿寶石，可解性檢查全綠（繞道「可解且更好解」）。
        /// </summary>
        private static void GuardEffectiveness(FloorDefinition floor, GridPos entryPos, Action<string, bool> check)
        {
            // 開發用樓層豁免：設定層是**功能的測試場**，道具本來就該隨手拿得到，
            // 要求它「東西都得用血換」是把內容規則套到工具上。
            if (!FloorRegistry.IsTowerFloor(floor.Id)) return;

            int total = floor.Entities.Count(e => e.Type == EntityType.Item);
            if (total == 0) return;

            var free = FreelyReachableItems(floor, entryPos);
            int guarded = total - free.Count;
            check($"{floor.Id} 有 {guarded}/{total} 個道具要付出代價才拿得到" +
                  (free.Count > 0 ? $"（免費：{string.Join(",", free)}）" : ""),
                guarded > 0 && guarded >= total / 2);
        }

        private static List<string> FreelyReachableItems(FloorDefinition floor, GridPos from)
        {
            var seen = new HashSet<GridPos> { from };
            var q = new Queue<GridPos>();
            q.Enqueue(from);
            while (q.Count > 0)
            {
                var p = q.Dequeue();
                foreach (var n in new[]
                {
                    new GridPos(p.X + 1, p.Y), new GridPos(p.X - 1, p.Y),
                    new GridPos(p.X, p.Y + 1), new GridPos(p.X, p.Y - 1),
                })
                {
                    if (seen.Contains(n) || !floor.Grid.CanStep(p, n)) continue;
                    var e = floor.EntityAt(n);
                    if (e != null && (e.Type == EntityType.Monster || e.Type == EntityType.Door || e.Type == EntityType.Npc))
                        continue;
                    seen.Add(n);
                    q.Enqueue(n);
                }
            }
            return floor.Entities
                .Where(e => e.Type == EntityType.Item && seen.Contains(e.Pos))
                .Select(e => e.Ref).ToList();
        }
    }
}
