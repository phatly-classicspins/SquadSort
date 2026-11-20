using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Views
{
    public sealed class ElementUnlockOffer
    {
        public string ElementName { get; }
        public string Description { get; }
        public MoneySortElementType MoneySortElementType { get; }
        public System.Action Continue { get; }

        public ElementUnlockOffer(
            string elementName,
            string description,
            MoneySortElementType elementType,
            System.Action continueAction)
        {
            ElementName = elementName;
            Description = description;
            MoneySortElementType = elementType;
            Continue = continueAction;
        }
    }

    public sealed class ElementUnlockUI : UIBase
    {
        public void SetPreviewClip(UnityEngine.Video.VideoClip clip) {if(previewVideo!=null)previewVideo.OverrideClip=clip;}
        private bool _moneySortInitialized;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private Image elementIcon;
        [SerializeField] private Sprite rainbowIconSprite;
        [SerializeField] private Sprite hiddenMoneyIconSprite;
        [SerializeField] private TextMeshProUGUI elementNameText;
        [SerializeField] private TextMeshProUGUI unlockedText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [Tooltip("Reserved layout container for the authored board preview image.")]
        [SerializeField] private RectTransform boardPreviewPlaceholder;
        [SerializeField] private ElementUnlockPreviewVideo previewVideo;
        [SerializeField] private Button claimButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private RectTransform cardRoot;

        private ElementUnlockOffer _offer;
        private Image _overlay;
        private float _overlayAlpha = 1f;   // the prefab's dim (D-126: 245/255); restored on every Show after the fly-out fades it
        private bool _closing;

        public RectTransform BoardPreviewPlaceholder => boardPreviewPlaceholder;

        public void ShowIllustration(int element)
        {
            foreach (Transform child in boardPreviewPlaceholder)
            {
                bool visible = element switch
                {
                    0 => child.name == "HiddenStackIllustration",
                    1 => child.name == "HiddenPoleIllustration",
                    2 => child.name == "LockedChipIllustration",
                    _ => false
                };
                child.gameObject.SetActive(visible);
            }
        }

        public override void Init(object manager)
        {
            if (_moneySortInitialized) return;
            _moneySortInitialized = true;
            base.Init(manager);
            claimButton.onClick.AddListener(Continue);
            closeButton.onClick.AddListener(Continue);
            _overlay = GetComponent<Image>();
            if (_overlay != null) _overlayAlpha = _overlay.color.a;
        }

        public override void SetData(params object[] data)
        {
            _offer = data.Length > 0 ? data[0] as ElementUnlockOffer : null;
            if (_offer == null)
                return;

            titleText.text = _offer.ElementName;   // WF Popup-Element: the element's name is the ribbon title (prefab: upper case)
            elementNameText.text = _offer.ElementName;
            unlockedText.text = "Unlocked!";
            descriptionText.text = _offer.Description;
            var icon = GetIconSprite(_offer.MoneySortElementType);
            elementIcon.sprite = icon;
            elementIcon.enabled = icon != null;
        }

        public override void Show()
        {
            _closing = false;
            if (cardRoot != null)
            {
                cardRoot.gameObject.SetActive(true);
                cardRoot.localScale = Vector3.one;
            }
            if (_overlay != null)
            {
                var color = _overlay.color;
                color.a = _overlayAlpha;
                _overlay.color = color;
            }
            claimButton.interactable = true;
            closeButton.interactable = true;
            base.Show();
            if (_offer != null)
                previewVideo?.Play(_offer.MoneySortElementType);
        }

        public override void Hide()
        {
            previewVideo?.Stop();
            _offer = null;
            base.Hide();
        }

        // Unlike boosters, elements have no gameplay-toolbar button to fly a reward
        // icon toward, so closing this popup is just the standard UIAnimation hide
        // tween on the prefab -- no follow-up animation.
        private void Continue()
        {
            if (_closing || _offer == null)
                return;

            _closing = true;
            claimButton.interactable = false;
            closeButton.interactable = false;
            var action = _offer.Continue;
            _offer = null;
            action?.Invoke();
        }

        // private Sprite GetIconSprite(MoneySortElementType type) => type switch
        // {
        //     MoneySortElementType.Rainbow => rainbowIconSprite,
        //     MoneySortElementType.HiddenMoney => hiddenMoneyIconSprite,
        //     _ => null
        // };  
        private Sprite GetIconSprite(MoneySortElementType type)
        {
            return null;
        }

        private void OnDestroy()
        {
            if (claimButton != null)
                claimButton.onClick.RemoveListener(Continue);
            if (closeButton != null)
                closeButton.onClick.RemoveListener(Continue);
        }
    }
}
