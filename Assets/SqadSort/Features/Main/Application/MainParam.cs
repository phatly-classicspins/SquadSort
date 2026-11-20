namespace Game.Application
{
    /// <summary>
    /// The typed per-screen param for MainScreen (the three-file pattern: &lt;X&gt;Param immutable
    /// record). Injected into the screen scope <b>before</b> Configure by the scene service, so
    /// MainScreen ctor-injects it type-safely. Immutable positional <c>record</c> (C# 9 — no
    /// <c>record struct</c> on the SKU; the Game.Application IsExternalInit shim covers <c>init</c>
    /// setters). Engine-free by construction — that is what puts it inside the headless gate.
    /// Scaffolded by Scaffold.Sync (generate-once) — replace the placeholder field with the real params.
    /// </summary>
    /// <param name="OutOfLives">Money Sort: the last life went on leaving a level — Home opens with Out of Lives, this being what
    /// asked for it (<c>lose_restart</c>, <c>manual_restart</c>, <c>exit_home</c>); null otherwise.</param>
    public sealed record MainParam(bool ColdBoot, string OutOfLives = null);
}
