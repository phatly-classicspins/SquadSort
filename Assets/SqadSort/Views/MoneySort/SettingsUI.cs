using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Game.Views
{
    public sealed class SettingsPresentation
    {
        public readonly Action Restart;
        public readonly Action Close;
        public readonly Action Home;
        public readonly bool IsHomeScreen;
        public readonly Action TermsOfService;
        public readonly Action PrivacyPolicy;
        public bool Sound = true, Music, Vibration = true;
        public Action<int, bool> SettingsChanged;

        public SettingsPresentation(
            Action restart,
            Action close,
            Action home,
            bool isHomeScreen = false,
            Action termsOfService = null,
            Action privacyPolicy = null)
        {
            Restart = restart;
            Close = close;
            Home = home;
            IsHomeScreen = isHomeScreen;
            TermsOfService = termsOfService;
            PrivacyPolicy = privacyPolicy;
        }
    }

    public sealed class SettingsUI : UIBase
    {
        private const string SfxKey = "settings_sfx";
        private const string MusicKey = "settings_music";
        private const string VibrationKey = "settings_vibration";

        [SerializeField] private Toggle _sfxToggle;
        [SerializeField] private Toggle _musicToggle;
        [SerializeField] private Toggle _vibrationToggle;
        [SerializeField] private Button _restartButton;
        [SerializeField] private Button _homeButton;
        [SerializeField] private Button _closeButton;
        [SerializeField] private Button _termsOfServiceButton;
        [SerializeField] private Button _privacyPolicyButton;
        [SerializeField] private GameObject _gameplayButtons;
        [SerializeField] private GameObject _homeButtons;
        [SerializeField] private Sprite _toggleOnSprite;
        [SerializeField] private Sprite _toggleOffSprite;

        private SettingsPresentation _presentation;
        private bool _initialized;
        [SerializeField] private TMP_Text _titleText, _sfxText, _musicText, _vibrationText, _restartText, _homeText;

        public void SetLabels(string title, string[] rows, string restart, string home)
        {
            _titleText.text = title; _sfxText.text = rows[0]; _musicText.text = rows[1];
            _vibrationText.text = rows[2]; _restartText.text = restart; _homeText.text = home;
        }

        /// <summary>The two legal link buttons on Home (mock-up Setting): their captions, localized by the caller.</summary>
        public void SetLinkLabels(string terms, string privacy)
        {
            SetCaption(_termsOfServiceButton, terms);
            SetCaption(_privacyPolicyButton, privacy);
        }

        private static void SetCaption(Button button, string text)
        {
            if (button == null) return;
            var label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = text ?? string.Empty;
        }

        public override void Init(object manager)
        {
            if (_initialized) return;
            _initialized = true;
            base.Init(manager);
            _restartButton?.onClick.AddListener(OnRestart);
            _homeButton?.onClick.AddListener(OnHome);
            _closeButton?.onClick.AddListener(OnClose);
            _termsOfServiceButton?.onClick.AddListener(OnTermsOfService);
            _privacyPolicyButton?.onClick.AddListener(OnPrivacyPolicy);
            _sfxToggle?.onValueChanged.AddListener(OnSfxChanged);
            _musicToggle?.onValueChanged.AddListener(OnMusicChanged);
            _vibrationToggle?.onValueChanged.AddListener(OnVibrationChanged);
        }

        public override void SetData(params object[] data)
        {
            _presentation = data.Length > 0 ? data[0] as SettingsPresentation : null;
            var homeScreen = _presentation != null && _presentation.IsHomeScreen;
            _gameplayButtons.SetActive(!homeScreen);
            _homeButtons.SetActive(homeScreen);
            // A button without a URL to open would do nothing on tap.
            _termsOfServiceButton?.gameObject.SetActive(_presentation?.TermsOfService != null);
            _privacyPolicyButton?.gameObject.SetActive(_presentation?.PrivacyPolicy != null);
            SetToggleWithoutNotify(_sfxToggle, _presentation?.Sound ?? true);
            // Must match FlickSortSoundManager: music defaults to off.
            SetToggleWithoutNotify(_musicToggle, _presentation?.Music ?? false);
            SetToggleWithoutNotify(_vibrationToggle, _presentation?.Vibration ?? true);
        }

        private void SetToggleWithoutNotify(Toggle toggle, bool value)
        {
            if (toggle == null)
                return;

            toggle.SetIsOnWithoutNotify(value);
            ApplyToggleVisual(toggle, value);
        }

        private void OnSfxChanged(bool enabled)
        {
            ApplyToggleVisual(_sfxToggle, enabled);
            SaveToggle(SfxKey, enabled);
        }

        private void OnMusicChanged(bool enabled)
        {
            ApplyToggleVisual(_musicToggle, enabled);
            SaveToggle(MusicKey, enabled);
        }

        private void OnVibrationChanged(bool enabled)
        {
            ApplyToggleVisual(_vibrationToggle, enabled);
            SaveToggle(VibrationKey, enabled);
        }

        private void ApplyToggleVisual(Toggle toggle, bool enabled)
        {
            if (toggle == null)
                return;

            if (toggle.targetGraphic is Image background)
            {
                background.sprite = enabled ? _toggleOnSprite : _toggleOffSprite;
                background.type = Image.Type.Simple;
                background.preserveAspect = true;
                background.color = Color.white;
            }

            var knob = toggle.transform.Find("Background/Checkmark") as RectTransform;
            if (knob == null)
                return;

            // Both supplied sprites already contain their knob.
            if (toggle.graphic != null)
                toggle.graphic = null;

            knob.gameObject.SetActive(false);
        }

        private void SaveToggle(string key, bool enabled)
        {
            _presentation?.SettingsChanged?.Invoke(key == SfxKey ? 0 : key == MusicKey ? 1 : 2, enabled);


        }

        private void OnRestart() => _presentation?.Restart?.Invoke();
        private void OnHome() => _presentation?.Home?.Invoke();
        private void OnClose() => _presentation?.Close?.Invoke();
        private void OnTermsOfService() => _presentation?.TermsOfService?.Invoke();
        private void OnPrivacyPolicy() => _presentation?.PrivacyPolicy?.Invoke();

        private void OnDestroy()
        {
            _restartButton?.onClick.RemoveListener(OnRestart);
            _homeButton?.onClick.RemoveListener(OnHome);
            _closeButton?.onClick.RemoveListener(OnClose);
            _termsOfServiceButton?.onClick.RemoveListener(OnTermsOfService);
            _privacyPolicyButton?.onClick.RemoveListener(OnPrivacyPolicy);
            _sfxToggle?.onValueChanged.RemoveListener(OnSfxChanged);
            _musicToggle?.onValueChanged.RemoveListener(OnMusicChanged);
            _vibrationToggle?.onValueChanged.RemoveListener(OnVibrationChanged);
        }
    }
}
