namespace Tower.Core.Grid
{
    /// <summary>
    /// 地形只有三類：可走、牆、單向（含方向）。其他一切都是實體。
    /// 字元對應（floor JSON 的 tiles 列）：'.' Floor、'W' Wall、'^v&lt;&gt;' 單向。
    /// 箭頭方向 = 通行方向（只能順著箭頭走）。
    /// </summary>
    public enum TerrainType
    {
        Floor,
        Wall,
        OneWayNorth, // '^'：只能由南向北通過
        OneWaySouth, // 'v'
        OneWayWest,  // '<'
        OneWayEast,  // '>'
    }
}
