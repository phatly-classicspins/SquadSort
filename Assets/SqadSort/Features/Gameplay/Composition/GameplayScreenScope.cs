using Game.Infrastructure;
using Game.Application;
using ClassicSpins.PrototypeFramework.Domain;
using System.Linq;
using System;
using VContainer;
using ClassicSpins.PrototypeFramework.Composition;
using Game.Presentation;

namespace Game.Composition
{
    /// <summary>
    /// The Gameplay screen's scope (&lt;X&gt;ScreenScope : SceneLifetimeScope). Sits on the
    /// Gameplay.unity screen scene's single scope GameObject and registers GameplayScreen as the scope's
    /// entry screen. The parent (Root) and the typed GameplayParam are supplied by the scene service when
    /// the scene loads. It lives in Game.Composition — the only SKU tier that names a container
    /// type — while GameplayScreen lives in Game.Presentation. Scaffolded by Scaffold.Sync
    /// (generate-once).
    /// </summary>
    public sealed class GameplayScreenScope : SceneLifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.Register<ResourcesLevelCatalog>(Lifetime.Singleton).As<ILevelCatalog>();

            // Rule #14: the screen's one random source is seeded here; GameplayScreen logs the seed.
            builder.RegisterInstance<IRandom>(new Pcg32(Environment.TickCount));

            var worldRoot = gameObject.scene.GetRootGameObjects().First(g => g.name == "WorldRoot").transform;
            builder.RegisterInstance(new ScreenWorldRoot(worldRoot));

            builder.RegisterEntryScreen<GameplayScreen>();
            builder.Register<SqadSortRating>(Lifetime.Scoped);   // disposed with the screen (drops its focus subscription), as CardSlot
        }
    }
}
