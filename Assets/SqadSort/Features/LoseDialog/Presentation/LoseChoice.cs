namespace Game.Presentation
{
    /// <summary>The lose offer's answer (Money Sort's Out of Space, opened directly by GameplayScreen). <c>Booster0..2</c> = a HUD booster
    /// tile pressed (bar order: Hand, Shuffle, Remove). <c>Decline</c> = "No thanks", ✕ or Back: the caller shows the "Are you sure?"
    /// confirm (LoseUI), whose ✕ brings this offer back.</summary>
    public enum LoseChoice { ReviveCoins, ReviveAd, RvSlotAd, Decline, Booster0, Booster1, Booster2 }
}
