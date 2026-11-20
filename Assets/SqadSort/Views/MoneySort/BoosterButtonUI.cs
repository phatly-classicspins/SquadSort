using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Views
{
    public sealed class BoosterButtonUI : MonoBehaviour
    {
        [SerializeField] private MoneySortBoosterType _boosterType;
        [SerializeField] private Button _button;
        [SerializeField] private GameObject _unlockedContent;
        [SerializeField] private GameObject _lockedContent;
        [SerializeField] private Image _icon;
        [SerializeField] private TextMeshProUGUI _countText;
        [SerializeField] private Image _badge;
        [SerializeField] private TextMeshProUGUI _unlockLevelText;
        [Header("Optional State Sprites")]
        [SerializeField] private Sprite _lockedButtonSprite;
        [SerializeField] private Sprite _unlockedButtonSprite;
        [SerializeField] private Sprite _countBadgeSprite;
        [SerializeField] private Sprite _addBadgeSprite;

        private Action<MoneySortBoosterType> _clickHandler;

        public MoneySortBoosterType Type => _boosterType;
        public Sprite Icon => _icon != null ? _icon.sprite : _button?.image?.sprite;
        public RectTransform RectTransform => _button != null ? _button.transform as RectTransform : transform as RectTransform;

        public void Bind(Action<MoneySortBoosterType> clickHandler)
        {
            _clickHandler = clickHandler;
            if (_button == null)
                throw new MissingReferenceException($"{name} requires a Button reference.");
            _button.onClick.RemoveListener(HandleClick);
            _button.onClick.AddListener(HandleClick);
        }

        public void SetState(int count, bool unlocked, int unlockLevel, Sprite configuredIcon)
        {
            count = Mathf.Max(0, count);
            if (_unlockedContent != null) _unlockedContent.SetActive(unlocked);
            if (_lockedContent != null) _lockedContent.SetActive(!unlocked);
            if (_button != null) _button.interactable = unlocked;

            if (_countText != null)
            {
                _countText.text = count > 0 ? count.ToString() : "";
            }
            if (_unlockLevelText != null) _unlockLevelText.text = $"{Mathf.Max(1, unlockLevel)}";
            if (_icon != null)
            {
                if (configuredIcon != null) _icon.sprite = configuredIcon;
            }
            if (_badge != null)
            {
                var badgeSprite = count > 0 ? _countBadgeSprite : _addBadgeSprite;
                if (badgeSprite != null) _badge.sprite = badgeSprite;
            }
            if (_button != null && _button.image != null)
            {
                var buttonSprite = unlocked ? _unlockedButtonSprite : _lockedButtonSprite;
                if (buttonSprite != null) _button.image.sprite = buttonSprite;
            }
        }

        private void HandleClick() => _clickHandler?.Invoke(_boosterType);

        private void OnDestroy()
        {
            if (_button != null) _button.onClick.RemoveListener(HandleClick);
            _clickHandler = null;
        }
    }
}
