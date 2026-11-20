using System;
using ClassicSpins.PrototypeFramework.Domain;
using ClassicSpins.PrototypeFramework.Presentation;
using Game.Application;
using Game.Gen;
using Game.Views;

namespace Game.Presentation
{
    /// <summary>
    /// GDD v2.0 §9, D-043: the sound and haptic of each game moment, behind the player's Sound / Music / Haptics
    /// settings. A clip is an Addressable at its key's value (<c>Content/Audio/</c>); a key with no clip is warned once
    /// by the framework's <c>AudioService</c> and stays quiet. Session-long: the music carries across screens, and
    /// every <see cref="UiKit"/> button click plays <c>sfx_ui_click</c> with a light tap.
    /// </summary>
    public sealed class GameFeedback : IDisposable
    {
        private readonly IAudioService _audio;
        private readonly IHapticService _haptics;
        private readonly IProgressStore _store;
        private AudioKey _music;
        private bool _hasMusic;

        public GameFeedback(IAudioService audio, IHapticService haptics, IProgressStore store)
        {
            _audio = audio; _haptics = haptics; _store = store;
            UiKit.Clicked += Click;
            ApplySettings();
        }

        /// <summary>Push the saved Sound / Music / Haptics toggles to the buses and the haptic engine.</summary>
        public void ApplySettings()
        {
            var s = _store.Settings;
            _audio.Mute(AudioBus.Sfx, !s.Sound);
            _audio.Mute(AudioBus.UI, !s.Sound);
            _audio.Mute(AudioBus.Music, !s.Music);
            _haptics.SetEnabled(s.Haptics);
        }

        /// <summary>Start a track unless it is already the one playing (Home ↔ dialogs keep the music going).</summary>
        public void Music(AudioKey key)
        {
            if (_hasMusic && _music.Equals(key)) return;
            _music = key; _hasMusic = true;
            _audio.PlayMusic(key);
        }

        public void Click() => Play(AudioKeys.SfxUiClick, HapticType.Light);
        public void Tap() => Play(AudioKeys.SfxTap, HapticType.Medium);
        public void ChipsFlew() => Play(AudioKeys.SfxChipFly);
        public void CardLanded() => Play(AudioKeys.SfxCardLandTarget);
        public void TargetCompleted() => Play(AudioKeys.SfxTargetComplete, HapticType.Medium);
        public void Revealed() => Play(AudioKeys.SfxReveal);
        public void Unlocked() => Play(AudioKeys.SfxUnlock);
        public void Shuffled() => Play(AudioKeys.SfxShuffle);
        public void Coins() => Play(AudioKeys.SfxCoin);
        public void BoosterReward() => Play(AudioKeys.SfxBoosterReward);
        public void Won() => Play(AudioKeys.SfxWin, HapticType.Heavy);
        public void Lost() => Play(AudioKeys.SfxLose, HapticType.Heavy);

        private void Play(AudioKey sfx, HapticType? haptic = null)
        {
            _audio.PlaySfx(sfx);
            if (haptic.HasValue) _haptics.Play(haptic.Value);
        }

        public void Dispose() => UiKit.Clicked -= Click;
    }
}
