using System.Threading;
using Cysharp.Threading.Tasks;
using ClassicSpins.PrototypeFramework.Application;
using Game.Application;
using Game.Gen;
using Game.Views;

namespace Game.Presentation
{
    /// <summary>What the player chose on Settings: back to the level (✕ / Back), or Restart / Home (the caller confirms them).</summary>
    public enum SettingsChoice { Close, Restart, Home }

    /// <summary>features/pause-settings.md — Sound / Music / Haptics, saved immediately on each toggle; Money Sort's SettingsUI opened
    /// directly (Phat, 04-11 / 07-11-2026), with Restart and Home under the toggles in a level, its Home buttons on Home.</summary>
    public sealed class SettingsPopup
    {
        private readonly PopupUi _popups;
        private readonly IProgressStore _store;
        private readonly GameFeedback _feedback;
        private readonly ILocalizationService _loc;
        private readonly EconomyTuning _tuning;

        public SettingsPopup(PopupUi popups, IProgressStore store, GameFeedback feedback, ILocalizationService loc, EconomyTuning tuning)
        {
            _popups = popups; _store = store; _feedback = feedback; _loc = loc; _tuning = tuning;
        }

        /// <param name="home">True on Home: no Restart / Home buttons.</param>
        public UniTask<SettingsChoice> ShowAsync(bool home, CancellationToken ct) =>
            _popups.ShowAsync<SettingsUI, SettingsChoice>(AssetKeys.SettingsUI, () => SettingsChoice.Close, (ui, h) =>
            {
                ui.SetLabels(_loc.Get(LocKeys.SettingsTitle),
                    new[] { _loc.Get(LocKeys.SettingsSound), _loc.Get(LocKeys.SettingsMusic), _loc.Get(LocKeys.SettingsHaptics) },
                    _loc.Get(LocKeys.PauseRestart), _loc.Get(LocKeys.PauseHome));
                var s = _store.Settings;
                // mock-up Setting: on Home the two legal links sit under the toggles (remote config legal.*_url); a level
                // shows Restart / Home there instead
                ui.SetLinkLabels(_loc.Get(LocKeys.SettingsTerms), _loc.Get(LocKeys.SettingsPrivacy));
                ui.SetData(new SettingsPresentation(() => h.Close(SettingsChoice.Restart), () => h.Close(SettingsChoice.Close),
                    () => h.Close(SettingsChoice.Home), home,
                    home ? () => OpenLink(_tuning.TermsUrl) : (System.Action)null,
                    home ? () => OpenLink(_tuning.PrivacyUrl) : (System.Action)null)
                {
                    Sound = s.Sound, Music = s.Music, Vibration = s.Haptics,
                    SettingsChanged = Toggle
                });
            }, ct);

        private static void OpenLink(string url)
        {
            if (!string.IsNullOrEmpty(url)) UnityEngine.Application.OpenURL(url);
        }

        private void Toggle(int row, bool on)
        {
            var s = _store.Settings;
            if (row == 0) s.Sound = on;
            else if (row == 1) s.Music = on;
            else s.Haptics = on;
            try { _store.Save(); } catch (System.Exception) { /* a preference: keep the in-memory value, retry on the next save */ }
            _feedback.ApplySettings();   // CR-012 G: the toggle takes effect at once
        }
    }
}
