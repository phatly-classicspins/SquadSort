using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Views
{
    public sealed class GameplayCheatUI : MonoBehaviour
    {
        public Action MediationDebuggerRequested;
        [SerializeField] private GameObject _contentRoot;
        [SerializeField] private Button _toggleButton;
        [SerializeField] private Button _winButton;
        [SerializeField] private Button _loseButton;
        [SerializeField] private Button _goToLevelButton;
        [SerializeField] private TMP_InputField _levelInput;
        [SerializeField] private Button _addShuffleButton;
        [SerializeField] private Button _addHammerButton;
        [SerializeField] private Button _addFreeMoveButton;
        [SerializeField] private Button _addCoinsButton;
        [SerializeField] private TMP_InputField _coinInput;
        [SerializeField] private Toggle _notificationTestToggle;
        [SerializeField] private Toggle _randomGiftTestToggle;
        [SerializeField] private Toggle _revealHiddenToggle;
        [SerializeField] private Button _resetSaveButton;
        [SerializeField] private Button _maxDebuggerButton;
        [SerializeField] private Button _addLifeButton;
        [SerializeField] private TextMeshProUGUI _statusText;

        private Action _win;
        private Action _lose;
        private Action<int> _goToLevel;
        private Action<MoneySortBoosterType> _addBooster;
        private Action<int> _addCoins;
        private Action<bool> _setNotificationTestEnabled;
        private Func<bool> _getNotificationTestEnabled;
        private Action<bool> _setRandomGiftTestEnabled;
        private Func<bool> _getRandomGiftTestEnabled;
        private Action<bool> _setRevealHiddenEnabled;
        private Func<bool> _getRevealHiddenEnabled;
        private Action _resetSave;
        private Action _addLife;
        private bool _initialized;

        public void Initialize(
            Action win,
            Action lose,
            Action<int> goToLevel,
            Action<MoneySortBoosterType> addBooster,
            Action<int> addCoins,
            Action<bool> setNotificationTestEnabled,
            Func<bool> getNotificationTestEnabled,
            Action<bool> setRandomGiftTestEnabled,
            Func<bool> getRandomGiftTestEnabled,
            Action<bool> setRevealHiddenEnabled,
            Func<bool> getRevealHiddenEnabled,
            Action resetSave,
            Action addLife)
        {
#if GAME_IS_CHEAT
            gameObject.SetActive(true);            
#else
            gameObject.SetActive(false);           
#endif
            _win = win;
            _lose = lose;
            _goToLevel = goToLevel;
            _addBooster = addBooster;
            _addCoins = addCoins;
            _setNotificationTestEnabled = setNotificationTestEnabled;
            _getNotificationTestEnabled = getNotificationTestEnabled;
            _setRandomGiftTestEnabled = setRandomGiftTestEnabled;
            _getRandomGiftTestEnabled = getRandomGiftTestEnabled;
            _setRevealHiddenEnabled = setRevealHiddenEnabled;
            _getRevealHiddenEnabled = getRevealHiddenEnabled;
            _resetSave = resetSave;
            _addLife = addLife;

            if (_initialized)
                return;

            ValidateReferences();
            _toggleButton.onClick.AddListener(Toggle);
            _winButton.onClick.AddListener(OnWinClicked);
            _loseButton.onClick.AddListener(OnLoseClicked);
            _goToLevelButton.onClick.AddListener(OnGoToLevelClicked);
            _addShuffleButton.onClick.AddListener(OnAddShuffleClicked);
            _addHammerButton.onClick.AddListener(OnAddHammerClicked);
            _addFreeMoveButton.onClick.AddListener(OnAddFreeMoveClicked);
            _addCoinsButton.onClick.AddListener(OnAddCoinsClicked);
            _notificationTestToggle.SetIsOnWithoutNotify(
                _getNotificationTestEnabled?.Invoke() == true);
            _notificationTestToggle.onValueChanged.AddListener(OnNotificationTestChanged);
            _randomGiftTestToggle.SetIsOnWithoutNotify(
                _getRandomGiftTestEnabled?.Invoke() == true);
            _randomGiftTestToggle.onValueChanged.AddListener(OnRandomGiftTestChanged);
            _revealHiddenToggle.SetIsOnWithoutNotify(
                _getRevealHiddenEnabled?.Invoke() == true);
            _revealHiddenToggle.onValueChanged.AddListener(OnRevealHiddenChanged);
            _resetSaveButton.onClick.AddListener(OnResetSaveClicked);
            if (_addLifeButton != null)
                _addLifeButton.onClick.AddListener(OnAddLifeClicked);
            _maxDebuggerButton.gameObject.SetActive(true);
            _maxDebuggerButton.onClick.AddListener(OnMaxDebuggerClicked);
            _contentRoot.SetActive(false);
            _initialized = true;
        }

        public void SetBoosterCounts(int shuffle, int hammer, int freeMove)
        {
            _statusText.text = $"S:{shuffle}  H:{hammer}  F:{freeMove}";
        }

        private void Toggle() => _contentRoot.SetActive(!_contentRoot.activeSelf);
        private void OnWinClicked() => _win?.Invoke();
        private void OnLoseClicked() => _lose?.Invoke();
        private void OnAddShuffleClicked() => _addBooster?.Invoke(MoneySortBoosterType.Shuffle);
        private void OnAddHammerClicked() => _addBooster?.Invoke(MoneySortBoosterType.Hammer);
        private void OnAddFreeMoveClicked() => _addBooster?.Invoke(MoneySortBoosterType.FreeMove);
        private void OnResetSaveClicked()
        {
            _resetSave?.Invoke();
            _statusText.text = "SAVE RESET - RESTART GAME";
        }
        private void OnAddLifeClicked()
        {
            _addLife?.Invoke();
            _statusText.text = "LIFE ADDED";
        }

        private void OnMaxDebuggerClicked()
        {
            MediationDebuggerRequested?.Invoke();
            _statusText.text = "OPENING MAX DEBUGGER";
        }
        private void OnNotificationTestChanged(bool enabled)
        {
            _setNotificationTestEnabled?.Invoke(enabled);
            _notificationTestToggle.SetIsOnWithoutNotify(
                _getNotificationTestEnabled?.Invoke() == true);
        }
        private void OnRandomGiftTestChanged(bool enabled)
        {
            _setRandomGiftTestEnabled?.Invoke(enabled);
            _randomGiftTestToggle.SetIsOnWithoutNotify(
                _getRandomGiftTestEnabled?.Invoke() == true);
        }
        private void OnRevealHiddenChanged(bool enabled)
        {
            _setRevealHiddenEnabled?.Invoke(enabled);
            _revealHiddenToggle.SetIsOnWithoutNotify(
                _getRevealHiddenEnabled?.Invoke() == true);
        }

        private void OnGoToLevelClicked()
        {
            if (TryReadPositive(_levelInput, out var level))
                _goToLevel?.Invoke(level);
        }

        private void OnAddCoinsClicked()
        {
            if (TryReadPositive(_coinInput, out var amount))
                _addCoins?.Invoke(amount);
        }

        private static bool TryReadPositive(TMP_InputField input, out int value) =>
            int.TryParse(input.text, out value) && value > 0;

        private void ValidateReferences()
        {
            if (_contentRoot == null || _toggleButton == null || _winButton == null ||
                _loseButton == null || _goToLevelButton == null || _levelInput == null ||
                _addShuffleButton == null || _addHammerButton == null ||
                _addFreeMoveButton == null || _addCoinsButton == null ||
                _coinInput == null || _notificationTestToggle == null ||
                _randomGiftTestToggle == null ||
                _revealHiddenToggle == null ||
                _resetSaveButton == null || _maxDebuggerButton == null ||
                _statusText == null)
            {
                throw new MissingReferenceException(
                    $"{nameof(GameplayCheatUI)} requires all authored UI references.");
            }
        }

        private void OnDestroy()
        {
            if (!_initialized)
                return;

            _toggleButton.onClick.RemoveListener(Toggle);
            _winButton.onClick.RemoveListener(OnWinClicked);
            _loseButton.onClick.RemoveListener(OnLoseClicked);
            _goToLevelButton.onClick.RemoveListener(OnGoToLevelClicked);
            _addShuffleButton.onClick.RemoveListener(OnAddShuffleClicked);
            _addHammerButton.onClick.RemoveListener(OnAddHammerClicked);
            _addFreeMoveButton.onClick.RemoveListener(OnAddFreeMoveClicked);
            _addCoinsButton.onClick.RemoveListener(OnAddCoinsClicked);
            _notificationTestToggle.onValueChanged.RemoveListener(OnNotificationTestChanged);
            _randomGiftTestToggle.onValueChanged.RemoveListener(OnRandomGiftTestChanged);
            _revealHiddenToggle.onValueChanged.RemoveListener(OnRevealHiddenChanged);
            _resetSaveButton.onClick.RemoveListener(OnResetSaveClicked);
            if (_addLifeButton != null)
                _addLifeButton.onClick.RemoveListener(OnAddLifeClicked);
            if (_maxDebuggerButton != null)
                _maxDebuggerButton.onClick.RemoveListener(OnMaxDebuggerClicked);
        }
    }
}
