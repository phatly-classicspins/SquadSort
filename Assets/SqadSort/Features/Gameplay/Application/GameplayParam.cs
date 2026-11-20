namespace Game.Application
{
    /// <summary>
    /// The typed per-screen param for GameplayScreen (the three-file pattern: &lt;X&gt;Param immutable
    /// record). Injected into the screen scope <b>before</b> Configure by the scene service, so
    /// GameplayScreen ctor-injects it type-safely. Immutable positional <c>record</c> (C# 9 — no
    /// <c>record struct</c> on the SKU; the Game.Application IsExternalInit shim covers <c>init</c>
    /// setters). Engine-free by construction — that is what puts it inside the headless gate.
    /// Scaffolded by Scaffold.Sync (generate-once) — replace the placeholder field with the real params.
    /// </summary>
    /// <param name="Level">The level to play (1-based, a catalog number).</param>
    public sealed record GameplayParam(int Level);
}
