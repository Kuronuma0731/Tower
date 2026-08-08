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
        public FloorGrid Grid { get; }
        public IReadOnlyList<FloorEntity> Entities { get; }

        private readonly Dictionary<GridPos, FloorEntity> _byPos = new Dictionary<GridPos, FloorEntity>();

        public FloorDefinition(string id, FloorGrid grid, IReadOnlyList<FloorEntity> entities)
        {
            Id = id;
            Grid = grid ?? throw new ArgumentNullException(nameof(grid));
            Entities = entities ?? throw new ArgumentNullException(nameof(entities));

            foreach (var e in entities)
            {
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
