using System.Collections.Generic;
using ClassicSpins.PrototypeFramework.Application;
using ClassicSpins.PrototypeFramework.Domain;

namespace Game.Application
{
    /// <summary>Analytics fact: <paramref name="Level"/> was won for the first time (a replay is not one).
    /// Hand-authored: the SKU has no analytics codegen manifest yet (D-057). On the wire it is Money Sort's
    /// <c>level_up_N</c>, the name an Adjust event token is mapped by (<c>analytics.adjust.token.level_up_N</c>).</summary>
    public sealed record LevelWon(int Level) : GameEvent
    {
        public override string WireName() => "level_up_" + Level;

        public override void WriteParams(IEventParamWriter w) => w.Write("level", Level);
    }

    /// <summary>
    /// The facts progression reports once they are saved. Progression never calls <see cref="IAnalytics"/>
    /// itself (AR-25); <see cref="GameTracking"/> is the one place that turns them into events.
    /// </summary>
    public interface IProgressFacts
    {
        void FirstClear(int level);
        /// <summary>D-061: <paramref name="value"/> of a currency (<see cref="TrackingNames"/>) came in, and why.</summary>
        void Earned(string currency, long value, string placement);
        /// <summary>D-061: <paramref name="value"/> of a currency went out, and on what.</summary>
        void Spent(string currency, long value, string placement);
        /// <summary>D-061: a booster or element was unlocked (<see cref="TrackingNames.Booster"/> / <see cref="TrackingNames.Element"/>).</summary>
        void FeatureUnlocked(string feature);
    }
}
