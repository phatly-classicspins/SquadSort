using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Views
{
    // Every rewarded-ad button binds through here: while no ad is loaded for its placement the
    // button is disabled, turned grey and its content is swapped for "No ads available", and it
    // switches back on its own as soon as an ad loads, even with the popup still open.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class RewardedAdButton : MonoBehaviour
    {
        public const string NoAdsText = "No ads available";
        private const string GrayscaleShaderPath = "UI/UIGrayscale";
        private static readonly Color NoAdsOutlineColor = new Color(0.35f, 0.35f, 0.35f, 1f);

        private static IMoneySortAdAvailability s_ads;
        private static Material s_grayscaleMaterial;
        private static event Action AvailabilityChanged;

        private readonly List<GameObject> _hiddenContent = new List<GameObject>();
        private readonly List<(Graphic graphic, Material material)> _grayedGraphics =
            new List<(Graphic, Material)>();
        private ColorBlock _originalColors;
        private GameObject[] _decorations = Array.Empty<GameObject>();
        private Button _button;
        private TextMeshProUGUI _noAdsLabel;
        private string _placement;
        private bool _interactable = true;
        private bool _showingNoAds;

        // The popup's own rule for the button (e.g. a request in flight); ads availability is added on top.
        public bool Interactable
        {
            get => _interactable;
            set
            {
                _interactable = value;
                Refresh();
            }
        }

        private bool? _availabilityOverride;
        public bool IsAdAvailable => _availabilityOverride ?? IsAvailable(_placement);
        public void SetAvailability(bool available){_availabilityOverride=available;Refresh();}

        public static void SetAdsService(IMoneySortAdAvailability ads)
        {
            if (s_ads != null)
                s_ads.RewardedAvailabilityChanged -= RaiseAvailabilityChanged;
            s_ads = ads;
            if (s_ads != null)
                s_ads.RewardedAvailabilityChanged += RaiseAvailabilityChanged;
            RaiseAvailabilityChanged();
        }

        public static bool IsAvailable(string placement) =>
            s_ads == null || string.IsNullOrEmpty(placement) || s_ads.IsRewardedAdAvailable(placement);

        // A null placement unbinds the button and restores its normal content. `decorations` are
        // ad badges laid out next to (not inside) the button; they hide with the button's content.
        public static RewardedAdButton Bind(
            Button button, string placement, bool interactable = true, params GameObject[] decorations)
        {
            if (button == null)
                return null;
            if (!button.TryGetComponent(out RewardedAdButton rewarded))
                rewarded = button.gameObject.AddComponent<RewardedAdButton>();
            rewarded._placement = placement;
            rewarded._interactable = interactable;
            if (decorations != null && decorations.Length > 0)
                rewarded._decorations = decorations;
            rewarded.Refresh();
            return rewarded;
        }

        private static void RaiseAvailabilityChanged() => AvailabilityChanged?.Invoke();

        private void Awake() => _button = GetComponent<Button>();

        private void OnEnable()
        {
            AvailabilityChanged += Refresh;
            Refresh();
        }

        private void OnDisable() => AvailabilityChanged -= Refresh;

        private void OnDestroy() => AvailabilityChanged -= Refresh;

        public void Refresh()
        {
            if (_button == null)
                _button = GetComponent<Button>();
            var noAds = !string.IsNullOrEmpty(_placement) && !IsAdAvailable;
            SetNoAdsContent(noAds);
            _button.interactable = _interactable && !noAds;
        }

        private void SetNoAdsContent(bool noAds)
        {
            if (noAds == _showingNoAds)
                return;
            _showingNoAds = noAds;
            if (noAds)
            {
                var label = GetOrCreateLabel();
                _hiddenContent.Clear();
                foreach (Transform child in transform)
                {
                    if (child.gameObject == label.gameObject || !child.gameObject.activeSelf)
                        continue;
                    _hiddenContent.Add(child.gameObject);
                    child.gameObject.SetActive(false);
                }
                foreach (var decoration in _decorations)
                {
                    if (decoration == null || !decoration.activeSelf)
                        continue;
                    _hiddenContent.Add(decoration);
                    decoration.SetActive(false);
                }
                label.gameObject.SetActive(true);
                SetGray(true);
                return;
            }

            foreach (var content in _hiddenContent)
            {
                if (content != null)
                    content.SetActive(true);
            }
            _hiddenContent.Clear();
            if (_noAdsLabel != null)
                _noAdsLabel.gameObject.SetActive(false);
            SetGray(false);
        }

        // Greys the button's own background; the disabled tint is neutralised so the grey stays opaque.
        private void SetGray(bool gray)
        {
            if (gray)
            {
                _originalColors = _button.colors;
                var colors = _originalColors;
                colors.disabledColor = colors.normalColor;
                _button.colors = colors;

                var material = GetGrayscaleMaterial();
                _grayedGraphics.Clear();
                foreach (var image in GetComponents<Image>())
                {
                    _grayedGraphics.Add((image, image.material == image.defaultMaterial ? null : image.material));
                    if (material != null)
                        image.material = material;
                }
                return;
            }

            _button.colors = _originalColors;
            foreach (var (graphic, material) in _grayedGraphics)
            {
                if (graphic != null)
                    graphic.material = material;
            }
            _grayedGraphics.Clear();
        }

        private static Material GetGrayscaleMaterial()
        {
            if (s_grayscaleMaterial != null)
                return s_grayscaleMaterial;
            var shader = Resources.Load<Shader>(GrayscaleShaderPath);
            if (shader == null)
                return null;
            s_grayscaleMaterial = new Material(shader) { name = "UIGrayscale (Runtime)" };
            return s_grayscaleMaterial;
        }

        // Built at runtime from the button's own text style so no prefab needs a dedicated label.
        private TextMeshProUGUI GetOrCreateLabel()
        {
            if (_noAdsLabel != null)
                return _noAdsLabel;

            var source = GetComponentInChildren<TextMeshProUGUI>(true);
            var labelObject = new GameObject("NoAdsLabel", typeof(RectTransform));
            labelObject.layer = gameObject.layer;
            var rect = (RectTransform)labelObject.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(16f, 8f);
            rect.offsetMax = new Vector2(-16f, -8f);

            _noAdsLabel = labelObject.AddComponent<TextMeshProUGUI>();
            _noAdsLabel.raycastTarget = false;
            _noAdsLabel.alignment = TextAlignmentOptions.Center;
            _noAdsLabel.textWrappingMode = TextWrappingModes.Normal;
            if (source != null)
            {
                _noAdsLabel.font = source.font;
                _noAdsLabel.fontSharedMaterial = source.fontSharedMaterial;
                _noAdsLabel.fontStyle = source.fontStyle;
                _noAdsLabel.fontSizeMax = source.fontSize;
            }
            else
            {
                _noAdsLabel.fontSizeMax = 48f;
            }
            // One look on every grey button: white face, dark grey outline.
            _noAdsLabel.color = Color.white;
            var material = _noAdsLabel.fontMaterial;
            if (material.HasProperty(ShaderUtilities.ID_OutlineColor))
                material.SetColor(ShaderUtilities.ID_OutlineColor, NoAdsOutlineColor);
            _noAdsLabel.fontSizeMin = 12f;
            _noAdsLabel.enableAutoSizing = true;
            _noAdsLabel.text = NoAdsText;
            labelObject.SetActive(false);
            return _noAdsLabel;
        }
    }
}
