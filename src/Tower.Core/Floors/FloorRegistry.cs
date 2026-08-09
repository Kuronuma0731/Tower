using System;
using System.Collections.Generic;
using System.Linq;
using Tower.Core.Grid;

namespace Tower.Core.Floors
{
    /// <summary>
    /// 全塔的樓層索引，並且**自動處理樓梯配對**。
    ///
    /// 取代原本寫死在表現層的 switch：
    /// <code>
    /// case ("F01", Up):   LoadFloor("F02", F02.StairsDownPos);
    /// case ("F02", Down): LoadFloor("F01", F01.StairsUpPos);
    /// </code>
    /// 每加一層要手動加兩個 case，30 層就是 60 個——而且座標對齊規約全靠人工維持，
    /// 寫錯一個字母會讓玩家從 7F 上樓掉到 3F，**沒有任何東西會報錯**。
    ///
    /// 現在改成一條規則：樓層編號相鄰即相接，且**載入時強制驗證** F(n) 的上樓梯座標
    /// 等於 F(n+1) 的下樓梯座標（floor-authoring.md 的座標對齊規約）。違反就擲例外，
    /// 不會靜默生出一座接錯的塔。
    /// </summary>
    public sealed class FloorRegistry
    {
        private readonly Dictionary<string, FloorDefinition> _floors;
        private readonly List<string> _order;

        /// <summary>樓層 id 依編號排序後的順序（F00, F01, F02…）。</summary>
        public IReadOnlyList<string> Order => _order;

        public FloorRegistry(IEnumerable<FloorDefinition> floors)
        {
            _floors = floors.ToDictionary(f => f.Id);
            _order = _floors.Keys.OrderBy(NumberOf).ToList();
            ValidateStairPairing();
        }

        public FloorDefinition this[string id] => _floors.TryGetValue(id, out var f)
            ? f
            : throw new ArgumentException($"沒有這一層：{id}");

        public bool Has(string id) => _floors.ContainsKey(id);

        /// <summary>樓層編號：F00 → 0、F07 → 7。</summary>
        public static int NumberOf(string floorId)
            => int.TryParse(floorId.AsSpan(1), out int n)
                ? n
                : throw new ArgumentException($"樓層 id 必須是 F 加數字：{floorId}");

        /// <summary>
        /// 從 <paramref name="fromId"/> 沿 <paramref name="dir"/> 走樓梯會到哪一層、落在哪一格。
        /// 落點依座標對齊規約：上樓落在該層的下樓梯，下樓落在該層的上樓梯。
        /// 回傳 false = 沒有下一層（塔頂或塔底）。
        /// </summary>
        public bool TryTravel(string fromId, StairsDirection dir, out string toId, out GridPos landing)
        {
            toId = null;
            landing = default;

            int target = NumberOf(fromId) + (dir == StairsDirection.Up ? 1 : -1);
            string candidate = "F" + target.ToString("D2");
            if (!_floors.TryGetValue(candidate, out var to)) return false;

            // 上樓 → 落在目標層的下樓梯；下樓 → 落在目標層的上樓梯
            var arrival = to.FindStairs(dir == StairsDirection.Up ? StairsDirection.Down : StairsDirection.Up);
            if (arrival == null)
                throw new InvalidOperationException(
                    $"{candidate} 缺少{(dir == StairsDirection.Up ? "下" : "上")}樓梯，無法從 {fromId} 接過去");

            toId = candidate;
            landing = arrival.Pos;
            return true;
        }

        /// <summary>
        /// 座標對齊規約的執法者：F(n).上樓梯 == F(n+1).下樓梯。
        /// 這條檢查以前只在驗證器裡手寫三組，現在對**全部**相鄰樓層自動生效。
        /// </summary>
        private void ValidateStairPairing()
        {
            for (int i = 0; i + 1 < _order.Count; i++)
            {
                var lower = _floors[_order[i]];
                var upper = _floors[_order[i + 1]];
                if (NumberOf(upper.Id) != NumberOf(lower.Id) + 1) continue; // 樓層不連號則不強制

                var up = lower.FindStairs(StairsDirection.Up);
                var down = upper.FindStairs(StairsDirection.Down);
                if (up == null || down == null)
                    throw new InvalidOperationException(
                        $"{lower.Id} 與 {upper.Id} 相鄰，但缺少對接的樓梯（{lower.Id} 上樓梯={up?.Pos.ToString() ?? "無"}、{upper.Id} 下樓梯={down?.Pos.ToString() ?? "無"}）");

                if (up.Pos != down.Pos)
                    throw new InvalidOperationException(
                        $"樓梯座標未對齊：{lower.Id} 上樓梯 {up.Pos} ≠ {upper.Id} 下樓梯 {down.Pos}");
            }
        }
    }
}
