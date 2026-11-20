using System;
using System.Linq;
using ClassicSpins.PrototypeFramework.Composition;
using ClassicSpins.PrototypeFramework.Domain;
using Game.Application;
using Game.Infrastructure;
using Game.Presentation;
using VContainer;

namespace Game.Composition
{
    /// <summary>
    /// The Main screen's scope (&lt;X&gt;ScreenScope : SceneLifetimeScope). Sits on the
    /// Main.unity screen scene's single scope GameObject and registers MainScreen as the scope's
    /// entry screen. The parent (Root) and the typed MainParam are supplied by the scene service when
    /// the scene loads. It lives in Game.Composition — the only SKU tier that names a container
    /// type — while MainScreen lives in Game.Presentation. Scaffolded by Scaffold.Sync
    /// (generate-once).
    /// </summary>
    public sealed class MainScreenScope : SceneLifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.Register<ResourcesLevelCatalog>(Lifetime.Singleton).As<ILevelCatalog>();
            builder.Register<LifeGate>(Lifetime.Scoped);   // Out of Lives on Home (Play with no life, or the last one just went)
            builder.RegisterEntryScreen<MainScreen>();
        }
    }
}
