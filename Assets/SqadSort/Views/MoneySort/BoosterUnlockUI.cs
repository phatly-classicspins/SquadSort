using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Views
{
    public sealed class BoosterUnlockOffer
    {
        public string BoosterName { get; }
        public bool Guide { get; }
        public string Description { get; }
        public Sprite Icon { get; }
        public MoneySortBoosterType MoneySortBoosterType { get; }
        public RectTransform TargetButton { get; }
        public Action Continue { get; }

        public BoosterUnlockOffer(
            string boosterName,
            string description,
            Sprite icon,
            MoneySortBoosterType boosterType,
            RectTransform targetButton,
            Action continueAction, bool guide = false)
        {
            Guide = guide;
            BoosterName = boosterName;
            Description = description;
            Icon = icon;
            MoneySortBoosterType = boosterType;
            TargetButton = targetButton;
            Continue = continueAction;
        }
    }

    public sealed class BoosterUnlockUI : UIBase
    {
        public Sprite Icon(MoneySortBoosterType type)=>previewVideo?.Icon(type)??boosterIcon.sprite;
        private bool _moneySortInitialized;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private Image boosterIcon;
        [SerializeField] private TextMeshProUGUI boosterNameText;
        [SerializeField] private TextMeshProUGUI unlockedText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [Tooltip("Reserved layout container for the authored board preview image.")]
        [SerializeField] private RectTransform boardPreviewPlaceholder;
        [SerializeField] private BoosterUnlockPreviewVideo previewVideo;
        [SerializeField] private Button claimButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private RectTransform cardRoot;
        [SerializeField, Min(32f)] private float flyStartSize = 220f;
        [SerializeField, Min(0f)] private float flyHoldDuration = 0.35f;
        [SerializeField, Min(0.1f)] private float flyDuration = 0.65f;
        [SerializeField, Range(0.1f, 1f)] private float flyEndScale = 0.42f;

        [Header("Booster guide card")]
        [SerializeField] private RectTransform _guideCard;
        [SerializeField] private TextMeshProUGUI _guideTitle, _guideDescription;
        [SerializeField] private Image _guideIcon;
        [SerializeField] private Button _guideOk, _guideClose;

        private BoosterUnlockOffer _offer;
        private Sequence _flySequence;
        private Image _overlay;
        private float _overlayAlpha = 1f;   // the prefab's dim (D-126: 245/255); restored on every Show after the fly-out fades it
        private bool _closing;

        public RectTransform BoardPreviewPlaceholder => boardPreviewPlaceholder;

        public override void Init(object manager)
        {
            if (_moneySortInitialized) return;
            _moneySortInitialized = true;
            base.Init(manager);
            claimButton.onClick.AddListener(Continue);
            closeButton.onClick.AddListener(Continue);
            _overlay = GetComponent<Image>();
            if (_overlay != null) _overlayAlpha = _overlay.color.a;
            if (_guideOk != null) _guideOk.onClick.AddListener(Continue);
            if (_guideClose != null) _guideClose.onClick.AddListener(Continue);
        }

        public override void SetData(params object[] data)
        {
            _offer = data.Length > 0 ? data[0] as BoosterUnlockOffer : null;
            if (_offer == null)
                return;

            titleText.text = "New Booster";
            boosterNameText.text = _offer.BoosterName;
            unlockedText.text = "Unlocked!";
            descriptionText.text = _offer.Description;
            boosterIcon.sprite = _offer.Icon;
            boosterIcon.enabled = _offer.Icon != null;
            if (_guideTitle != null) _guideTitle.text = _offer.BoosterName;
            if (_guideDescription != null) _guideDescription.text = _offer.Description;
            if (_guideIcon != null) _guideIcon.sprite = _offer.Icon;
        }

        public override void Show()
        {
            StopFlyAnimation();
            _closing = false;
            if (cardRoot != null)
            {
                cardRoot.gameObject.SetActive(_offer?.Guide != true);
                cardRoot.localScale = Vector3.one;
            }
            if (_overlay != null)
            {
                var color = _overlay.color;
                color.a = _overlayAlpha;
                _overlay.color = color;
            }
            if (_guideCard != null) _guideCard.gameObject.SetActive(_offer?.Guide == true);
            if (_guideOk != null) _guideOk.interactable = true;
            if (_guideClose != null) _guideClose.interactable = true;
            claimButton.interactable = true;
            closeButton.interactable = true;
            base.Show();
            if (_offer != null && !_offer.Guide && (boardPreviewPlaceholder == null || boardPreviewPlaceholder.gameObject.activeInHierarchy))
                previewVideo?.Play(_offer.MoneySortBoosterType);
        }

        public override void Hide()
        {
            previewVideo?.Stop();
            _offer = null;
            StopFlyAnimation();
            base.Hide();
        }

        private void Continue()
        {
            if (_closing || _offer == null)
                return;

            _closing = true;
            claimButton.interactable = false;
            closeButton.interactable = false;
            var action = _offer.Continue;
            if (_offer.Guide)
            {
                _offer = null;
                action?.Invoke();
                return;
            }
            var target = _offer.TargetButton;
            if (target == null || boosterIcon.sprite == null)
            {
                _offer = null;
                action?.Invoke();
                return;
            }

            if (cardRoot != null)
                cardRoot.gameObject.SetActive(false);

            // Keep the large icon centered briefly so the reward reads clearly before it flies to the booster button.
            _flySequence = BoosterRewardFlyAnimation.Play(
                boosterIcon.sprite,
                transform,
                target,
                flyStartSize,
                flyHoldDuration,
                flyDuration,
                flyEndScale,
                () =>
                {
                    _flySequence = null;
                    _offer = null;
                    action?.Invoke();
                });
            if (_overlay != null)
            {
                // Tween the color directly so this animation does not depend on the optional DOTween UI module.
                _flySequence.Insert(flyHoldDuration, DOTween.To(
                    () => _overlay.color,
                    color => _overlay.color = color,
                    new Color(_overlay.color.r, _overlay.color.g, _overlay.color.b, 0f),
                    flyDuration * 0.45f));
            }
        }

        private void StopFlyAnimation()
        {
            _flySequence?.Kill();
            _flySequence = null;
        }

        private void OnDestroy()
        {
            StopFlyAnimation();
            if (_guideOk != null) _guideOk.onClick.RemoveListener(Continue);
            if (_guideClose != null) _guideClose.onClick.RemoveListener(Continue);
            if (claimButton != null)
                claimButton.onClick.RemoveListener(Continue);
            if (closeButton != null)
                closeButton.onClick.RemoveListener(Continue);
        }
    }
}
