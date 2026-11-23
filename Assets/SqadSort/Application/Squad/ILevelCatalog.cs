using Game.Domain;

namespace Game.Application
{
    /// <summary>
    /// The shipped levels, by 1-based number. A port so the screen controller never knows where levels are
    /// stored; the Infrastructure adapter reads the level pack off the player build.
    /// </summary>
    public interface ILevelCatalog
    {
        /// <summary>How many levels ship.</summary>
        int Count { get; }

        /// <summary>Level <paramref name="number"/> (1..<see cref="Count"/>).</summary>
        LevelDefinition GetLevel(int number);
    }
}
