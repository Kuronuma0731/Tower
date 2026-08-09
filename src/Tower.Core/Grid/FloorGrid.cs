using System;

namespace Tower.Core.Grid
{
    /// <summary>
    /// 一層的靜態地形（13×13 定案）。實體不在這裡——地形歸地形，實體歸實體。
    /// </summary>
    public sealed class FloorGrid
    {
        public const int Size = 13;

        private readonly TerrainType[,] _tiles;

        private FloorGrid(TerrainType[,] tiles) => _tiles = tiles;

        public TerrainType this[in GridPos pos] => _tiles[pos.X, pos.Y];

        public bool InBounds(in GridPos pos)
            => pos.X >= 0 && pos.X < Size && pos.Y >= 0 && pos.Y < Size;

        /// <summary>
        /// 解析字元列格式（schema 的 tiles 欄）。row 0 = 最上列（北）。
        /// 匯入器驗證規則之一：非法字元或尺寸不符即擲例外，不產生半套資料。
        /// </summary>
        public static FloorGrid Parse(string[] rows)
        {
            if (rows == null || rows.Length != Size)
                throw new ArgumentException($"樓層必須恰好 {Size} 列，收到 {rows?.Length ?? 0} 列");

            var tiles = new TerrainType[Size, Size];
            for (int y = 0; y < Size; y++)
            {
                if (rows[y].Length != Size)
                    throw new ArgumentException($"第 {y} 列長度 {rows[y].Length}，必須恰好 {Size}");

                for (int x = 0; x < Size; x++)
                {
                    tiles[x, y] = rows[y][x] switch
                    {
                        '.' => TerrainType.Floor,
                        'W' => TerrainType.Wall,
                        '^' => TerrainType.OneWayNorth,
                        'v' => TerrainType.OneWaySouth,
                        '<' => TerrainType.OneWayWest,
                        '>' => TerrainType.OneWayEast,
                        var c => throw new ArgumentException($"非法地形字元 '{c}' 於 ({x},{y})"),
                    };
                }
            }
            return new FloorGrid(tiles);
        }

        /// <summary>
        /// 純地形層面「能否從 from 走到相鄰的 to」。實體阻擋（怪、門）在遊戲層另判。
        /// 單向格：只能順著箭頭方向「離開」它；進入不限方向。
        /// </summary>
        public bool CanStep(in GridPos from, in GridPos to)
        {
            if (!InBounds(from) || !InBounds(to)) return false;

            int dx = to.X - from.X;
            int dy = to.Y - from.Y;
            if (Math.Abs(dx) + Math.Abs(dy) != 1) return false; // 只允許四方向一步

            if (this[to] == TerrainType.Wall) return false;

            // 站在單向格上時，只能往箭頭方向走
            return this[from] switch
            {
                TerrainType.OneWayNorth => dy == -1,
                TerrainType.OneWaySouth => dy == 1,
                TerrainType.OneWayWest  => dx == -1,
                TerrainType.OneWayEast  => dx == 1,
                _ => true,
            };
        }
    }
}
