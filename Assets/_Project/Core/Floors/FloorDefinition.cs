using System;
using System.Collections.Generic;
using Tower.Core.Grid;

namespace Tower.Core.Floors
{
    /// <summary>
    /// 一層的完整靜態定義：地形 + 實體表（floor JSON 的 POCO 形式）。
    /// 執行期唯讀；會變的一切在 GameState。
    /// </summary>
    public sealed class FloorDefinition
    {
        public string Id { get; }

        /// <summary>樓層顯示名（floor JSON 的 name_zh）。</summary>
        public string NameZh { get; }

        public FloorGrid Grid { get; }
        public IReadOnlyList<FloorEntity> Entities { get; }

        private readonly Dictionary<GridPos, FloorEntity> _byPos = new Dictionary<GridPos, FloorEntity>();

        public FloorDefinition(string id, FloorGrid grid, IReadOnlyList<FloorEntity> entities, string nameZh = "")
        {
            Id = id;
            NameZh = nameZh;
            Grid = grid ?? throw new ArgumentNullException(nameof(grid));
            Entities = entities ?? throw new ArgumentNullException(nameof(entities));

            foreach (var e in entities)
            {
                if (!grid.InBounds(e.Pos))
                    throw new ArgumentException($"{id}: 實體 {e.Eid} 在界外 {e.Pos}");
                // 擺在牆上的實體永遠碰不到——曾經發生過（守衛被擺進牆裡，佈局看起來卻正常）
                if (grid[e.Pos] == TerrainType.Wall)
                    throw new ArgumentException($"{id}: 實體 {e.Eid} 被擺在牆上 {e.Pos}");
                if (_byPos.ContainsKey(e.Pos))
                    throw new ArgumentException($"{id}: 兩個實體佔據同一格 {e.Pos}（{_byPos[e.Pos].Eid} / {e.Eid}）");
                _byPos[e.Pos] = e;
            }
        }

        public FloorEntity EntityAt(in GridPos pos)
            => _byPos.TryGetValue(pos, out var e) ? e : null;

        public FloorEntity FindStairs(StairsDirection dir)
        {
            foreach (var e in Entities)
                if (e.Type == EntityType.Stairs && e.Stairs == dir)
                    return e;
            return null;
        }

        public FloorEntity FindSpawn()
        {
            foreach (var e in Entities)
                if (e.Type == EntityType.Spawn)
                    return e;
            return null;
        }
    }
}
