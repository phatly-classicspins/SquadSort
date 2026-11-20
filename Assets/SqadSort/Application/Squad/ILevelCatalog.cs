namespace Game.Application
{
    /// <summary>
    /// The shipped levels, by 1-based number. A port so the screen controller never knows where level
    /// text is stored; the Infrastructure adapter reads it off the player build.
    /// </summary>
    public interface ILevelCatalog
    {
        /// <summary>How many levels ship.</summary>
        int Count { get; }

        /// <summary>The level text of level <paramref name="number"/> (1..<see cref="Count"/>), for <c>LevelParser</c>.</summary>
        string GetLevelText(int number);
    }
}
