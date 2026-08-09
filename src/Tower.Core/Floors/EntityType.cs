namespace Tower.Core.Floors
{
    /// <summary>樓層實體類型（floor JSON 的 entities[].type）。會變的一切都是實體。</summary>
    public enum EntityType
    {
        Monster,
        Door,
        Item,
        Stairs,
        Switch,
        Shop,
        Altar,
        Npc,
        Spawn,
    }

    /// <summary>鑰匙/門的三層（D10）。</summary>
    public enum KeyTier
    {
        Yellow,
        Blue,
        Red,
    }

    /// <summary>樓梯方向。</summary>
    public enum StairsDirection
    {
        Up,
        Down,
    }
}
