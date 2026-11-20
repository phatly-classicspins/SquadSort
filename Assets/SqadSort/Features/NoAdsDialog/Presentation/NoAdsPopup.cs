using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using ClassicSpins.PrototypeFramework.Application;
using Game.Gen;
using Game.Views;

namespace Game.Presentation
{
    /// <summary>
    /// No ads available — Money Sort's ErrorUI opened directly (Phat, 04-11 / 07-11-2026): shown when the player asked for a rewarded
    /// ad and none could play (not loaded, or it failed — not when they skipped it). ✕ or Back closes and the caller goes back to where
    /// it was. Root: it shows over Gameplay and over the Root popups (Gold Brick, Out of Lives).
    /// </summary>
    public sealed class NoAdsPopup
    {
        private readonly PopupUi _popups;
        private readonly ILocalizationService _loc;

        public NoAdsPopup(PopupUi popups, ILocalizationService loc) { _popups = popups; _loc = loc; }

        public UniTask ShowAsync(CancellationToken ct) =>
            _popups.ShowAsync<ErrorUI, bool>(AssetKeys.UI.ErrorUI, () => true, (ui, h) =>
            {
                ui.SetData(_loc.Get(LocKeys.AdsErrorBody), (Action)(() => h.Close(true)));
                ui.SetLabel("_titleText", _loc.Get(LocKeys.AdsErrorTitle));
            }, ct);
    }
}
