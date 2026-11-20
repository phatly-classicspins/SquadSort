using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Views
{
    /// <summary>
    /// The booster helper (Figma WF/Flow-Booster: Booster Use / Shuffle Use / Magnet Use). A booster tap first plays the
    /// green banner with the booster's icon across the board. Then a booster that needs a target shows what to pick: the
    /// cream panel at the top for Hand ("Select a chip stack to send up"), the blue pill under the poles for Magnet
    /// ("Select a pole to clear"). The pressed tile turns into a red ✕ that cancels. While the prompt is up, taps pass
    /// through to the board; only the ✕ is a button. The banner holds the input until it is gone.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BoosterHelperUI : UIBase
    {
        public enum Prompt { Stack, Pole }

        [SerializeField] private CanvasGroup _group;
        [SerializeField] private Image _blocker;                  // full screen, catches taps while the banner plays
        [SerializeField] private RectTransform _banner;
        [SerializeField] private CanvasGroup _bannerGroup;
        [SerializeField] private Image _bannerIcon;
        [SerializeField] private Image _bannerGlow;
        [SerializeField] private RectTransform _stackPrompt;
        [SerializeField] private TextMeshProUGUI _stackLabel;
        [SerializeField] private RectTransform _polePrompt;
        [SerializeField] private TextMeshProUGUI _poleLabel;
        [SerializeField] private RectTransform _cancel;
        [SerializeField] private Image _cancelTile;
        [SerializeField] private Button _cancelButton;
        [SerializeField, Min(0f)] private float _poleGap = 12f;

        [Header("Flying banner: Shuffle, Magnet (GDD 3.4, animRef/shuffle)")]
        [Tooltip("Seconds the banner takes to appear (the reference shows it almost at once).")]
        [SerializeField, Min(0f)] private float _shuffleBannerIn = 0.08f;
        [Tooltip("Seconds the booster icon takes to fly in from beyond the left edge of the screen to the middle of the banner.")]
        [SerializeField, Min(0.01f)] private float _iconIn = 0.45f;
        [Tooltip("Seconds the icon stays in the middle of the banner, its light burst glowing behind it.")]
        [SerializeField, Min(0f)] private float _iconHold = 0.35f;
        [Tooltip("Seconds the icon takes to dart out past the right edge of the screen.")]
        [SerializeField, Min(0.01f)] private float _iconOut = 0.45f;
        [Tooltip("Seconds the empty banner takes to fade once the icon is gone.")]
        [SerializeField, Min(0f)] private float _shuffleBannerOut = 0.25f;
        [Tooltip("Ease of the fly-in: fast at first, slowing into the middle.")]
        [SerializeField] private Ease _iconInEase = Ease.OutQuint;
        [Tooltip("Ease of the fly-out: slow at first, speeding off the screen.")]
        [SerializeField] private Ease _iconOutEase = Ease.InQuint;
        [Tooltip("The light burst behind the icon, as a multiple of the icon's size.")]
        [SerializeField, Min(0f)] private float _raysSize = 2.1f;
        [Tooltip("Degrees per second the light burst turns.")]
        [SerializeField] private float _raysSpin = 40f;

        private Sequence _bannerTween;
        private Tween _fade;
        private Action _cancelled;

        public override void Init(object manager)
        {
            base.Init(manager);
            if (_stackLabel != null) _stackLabel.color = DesignTokens.BoosterHintStack;
            if (_poleLabel != null) _poleLabel.color = DesignTokens.BoosterHintPole;
            if (_bannerGlow != null) _bannerGlow.color = DesignTokens.BoosterBannerGlow;
            if (_cancelButton != null)
            {
                _cancelButton.onClick.RemoveListener(Cancel);
                _cancelButton.onClick.AddListener(Cancel);
            }
            SetBanner(false);
            SetPrompt(null);
        }

        /// <summary>The banner with <paramref name="icon"/>: pops in, holds, fades; then <paramref name="done"/>.</summary>
        public void PlayBanner(Sprite icon, Action done)
        {
            SetPrompt(null);
            if (_bannerIcon != null && icon != null) _bannerIcon.sprite = icon;
            ResetIcon();
            _bannerTween?.Kill();   // first: its OnKill hides the banner
            SetBanner(true);
            if (_banner == null || _bannerGroup == null) { SetBanner(false); done?.Invoke(); return; }
            _bannerGroup.alpha = 0f;
            _banner.localScale = Vector3.one * DesignTokens.Motion.BannerFrom;
            _bannerTween = DOTween.Sequence().SetUpdate(true)
                .Append(_bannerGroup.DOFade(1f, DesignTokens.Motion.BannerIn))
                .Join(_banner.DOScale(1f, DesignTokens.Motion.BannerIn).SetEase(Ease.OutBack))
                .AppendInterval(DesignTokens.Motion.BannerHold)
                .Append(_bannerGroup.DOFade(0f, DesignTokens.Motion.BannerOut))
                .OnKill(() => { _bannerTween = null; SetBanner(false); done?.Invoke(); });   // also when cut short
        }

        /// <summary>
        /// Shuffle and Magnet (GDD 3.4, reference animRef/shuffle): the banner appears over the dim; the booster icon flies in from
        /// beyond the left edge of the screen, stops in the middle of the banner with a burst of light behind it, then
        /// darts out past the right edge; the empty banner fades; then <paramref name="done"/>.
        /// </summary>
        public void PlayFlyingBanner(Sprite icon, Action done)
        {
            SetPrompt(null);
            if (_bannerIcon != null && icon != null) _bannerIcon.sprite = icon;
            _bannerTween?.Kill();   // first: its OnKill hides the banner
            SetBanner(true);
            if (_banner == null || _bannerGroup == null || _bannerIcon == null) { SetBanner(false); done?.Invoke(); return; }
            ResetIcon();
            var iconRect = _bannerIcon.rectTransform;
            var rays = Rays();
            // from beyond one screen edge to beyond the other: the screen is the helper's own full-screen rect
            var screen = (RectTransform)transform;
            float off = screen.rect.width / 2f + Mathf.Max(iconRect.rect.width, iconRect.rect.height);
            float y = iconRect.anchoredPosition.y;
            iconRect.anchoredPosition = new Vector2(-off, y);
            float side = Mathf.Max(iconRect.rect.width, iconRect.rect.height) * _raysSize;
            rays.rectTransform.sizeDelta = new Vector2(side, side);
            rays.rectTransform.localEulerAngles = Vector3.zero;
            rays.rectTransform.anchoredPosition = iconRect.anchoredPosition;
            rays.gameObject.SetActive(true);
            _banner.localScale = Vector3.one;
            _bannerGroup.alpha = 0f;
            float spin = _raysSpin;
            _bannerTween = DOTween.Sequence().SetUpdate(true)
                .Append(_bannerGroup.DOFade(1f, _shuffleBannerIn))
                .Append(iconRect.DOAnchorPosX(0f, _iconIn).SetEase(_iconInEase))     // fast in, slowing into the middle
                .AppendInterval(_iconHold)
                .Append(iconRect.DOAnchorPosX(off, _iconOut).SetEase(_iconOutEase))  // slow off, speeding away
                .Append(_bannerGroup.DOFade(0f, _shuffleBannerOut))
                .OnUpdate(() =>
                {
                    rays.rectTransform.anchoredPosition = iconRect.anchoredPosition;
                    rays.rectTransform.localEulerAngles += new Vector3(0f, 0f, spin * Time.unscaledDeltaTime);
                })
                .OnKill(() => { _bannerTween = null; rays.gameObject.SetActive(false); ResetIcon(); SetBanner(false); done?.Invoke(); });
        }

        // the icon back where the prefab put it: the plain banner shows it still, in the middle
        private void ResetIcon()
        {
            if (_bannerIcon == null) return;
            var r = _bannerIcon.rectTransform;
            r.anchoredPosition = new Vector2(0f, r.anchoredPosition.y);
            r.localEulerAngles = Vector3.zero;
        }

        private Image _rays;
        private static Sprite _raysSprite;

        // made once, just under the icon inside the banner (so it fades with it); drawn in code: the burst is light, not art
        private Image Rays()
        {
            if (_rays != null) return _rays;
            var go = new GameObject("Rays", typeof(RectTransform), typeof(Image));
            go.layer = _banner.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(_bannerIcon.rectTransform.parent, false);
            rect.SetSiblingIndex(_bannerIcon.rectTransform.GetSiblingIndex());   // drawn before (under) the icon
            rect.anchorMin = rect.anchorMax = _bannerIcon.rectTransform.anchorMin;
            rect.pivot = new Vector2(0.5f, 0.5f);
            _rays = go.GetComponent<Image>();
            _rays.sprite = RaysSprite();
            _rays.color = DesignTokens.BoosterBannerRays;
            _rays.raycastTarget = false;
            go.SetActive(false);
            return _rays;
        }

        // a soft round glow with 12 tapering rays, white (the Image tints it)
        private static Sprite RaysSprite()
        {
            if (_raysSprite != null) return _raysSprite;
            const int size = 256, rays = 12;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "ShuffleRays" };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Atan2(dy, dx);
                    float ray = Mathf.Pow(Mathf.Abs(Mathf.Cos(a * rays / 2f)), 6f);        // narrow spokes
                    float glow = Mathf.Clamp01(1f - r / 0.55f);                           // the round core
                    float spoke = ray * Mathf.Clamp01(1f - r) * Mathf.Clamp01(1f - r);    // spokes fade to the edge
                    float alpha = Mathf.Clamp01(glow * glow * 0.9f + spoke * 0.8f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            _raysSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 1f);
            return _raysSprite;
        }

        /// <param name="poles">Hand: unused. Magnet: the pole band (world XY rect); the pill sits right under it.</param>
        /// <param name="tile">The pressed booster tile on the HUD; the ✕ takes its place.</param>
        public void ShowPrompt(Prompt kind, string text, Rect poles, RectTransform tile, Action cancelled)
        {
            _cancelled = cancelled;
            SetBanner(false);
            SetPrompt(kind);
            var label = kind == Prompt.Stack ? _stackLabel : _poleLabel;
            if (label != null) label.text = text ?? string.Empty;
            if (kind == Prompt.Pole) PlaceUnder(_polePrompt, poles);
            PlaceCancel(tile);
        }

        public void HidePrompt()
        {
            _cancelled = null;
            SetPrompt(null);
        }

        public override void Show()
        {
            base.Show();
            _fade?.Kill();
            if (_group != null) _group.alpha = 1f;
        }

        public override void Hide()
        {
            _bannerTween?.Kill();
            _bannerTween = null;
            _cancelled = null;
            if (!gameObject.activeInHierarchy || _group == null) { base.Hide(); return; }
            _fade?.Kill();
            _fade = _group.DOFade(0f, DesignTokens.Motion.Reveal).SetUpdate(true).OnComplete(() => { _fade = null; base.Hide(); });
        }

        private void SetBanner(bool shown)
        {
            if (_banner != null) _banner.gameObject.SetActive(shown);
            if (_bannerGlow != null) _bannerGlow.gameObject.SetActive(shown);
            if (_blocker != null) _blocker.raycastTarget = shown;
        }

        private void SetPrompt(Prompt? kind)
        {
            if (_stackPrompt != null) _stackPrompt.gameObject.SetActive(kind == Prompt.Stack);
            if (_polePrompt != null) _polePrompt.gameObject.SetActive(kind == Prompt.Pole);
            if (_cancel != null) _cancel.gameObject.SetActive(kind.HasValue);
        }

        // the pill keeps its authored height unless the poles are known: then it sits just under them
        private void PlaceUnder(RectTransform item, Rect world)
        {
            if (item == null || world.height <= 0f) return;
            var space = (RectTransform)item.parent;
            float bottom = space.InverseTransformPoint(new Vector3(world.center.x, world.yMin, space.position.z)).y;
            float half = item.rect.height * item.localScale.y / 2f;
            var p = item.localPosition;
            item.localPosition = new Vector3(p.x, bottom - _poleGap - half, p.z);
        }

        private void PlaceCancel(RectTransform tile)
        {
            if (_cancel == null || tile == null) return;
            var space = (RectTransform)_cancel.parent;
            var corners = new Vector3[4];
            tile.GetWorldCorners(corners);
            Vector2 min = space.InverseTransformPoint(corners[0]), max = space.InverseTransformPoint(corners[2]);
            _cancel.localPosition = new Vector3((min.x + max.x) / 2f, (min.y + max.y) / 2f, _cancel.localPosition.z);
            _cancel.sizeDelta = new Vector2(Mathf.Abs(max.x - min.x), Mathf.Abs(max.y - min.y));
            var source = tile.GetComponent<Image>();
            if (_cancelTile != null && source != null && source.sprite != null) _cancelTile.sprite = source.sprite;
        }

        private void Cancel()
        {
            var cancelled = _cancelled;
            HidePrompt();
            cancelled?.Invoke();
        }

        private void OnDestroy()
        {
            _bannerTween?.Kill();
            _fade?.Kill();
            if (_cancelButton != null) _cancelButton.onClick.RemoveListener(Cancel);
        }
    }
}
