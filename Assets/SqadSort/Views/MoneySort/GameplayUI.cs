using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Views
{
    public sealed class GameplayUI : UIBase
    {
        private bool _moneySortInitialized;
        public System.Action DealRequested, ProgressBarFilled;
        public System.Action<MoneySortBoosterType> BoosterRequested;
        public System.Action<int> ProgressStarLanded;
        [SerializeField] private TextMeshProUGUI _levelText;
        [SerializeField] private TextMeshProUGUI _progressText;
        [SerializeField] private TextMeshProUGUI _moneyText;
        [SerializeField] private TextMeshProUGUI _livesText;
        [SerializeField] private ResourceGroupUI _resourceGroup;
        [SerializeField] private Image _progressFill;
        [SerializeField] private Button _dealButton;
        [SerializeField] private BoosterButtonUI[] _boosterButtons;
        [SerializeField] private Button _settingsButton;
        [SerializeField] private Button _restartButton;   // Abhi's HUD: replay beside settings
        [Tooltip("HUD coin bar: opens the Shop over gameplay.")]
        [SerializeField] private Button _moneyButton;
        [Tooltip("Optional: where reward coins land. Falls back to the coin bar.")]
        [SerializeField] private RectTransform _coinFlyTarget;
        [Tooltip("HUD lives bar, drawn above the lose popups.")]
        [SerializeField] private GameObject _livesHud;
        [SerializeField] private GameplayCheatUI _cheatUI;
        [SerializeField] private BoosterConfigSO _boosterConfig;
        [SerializeField] private BoosterUsePromptUI _boosterUsePrompt;
        // D-136 (Asana "Add the countdown before losing to prompt booster use", WF Group 240): the red "Oops! No more slots
        // available" bar with a stopwatch, under the waiting rail; nothing dims and nothing blocks, so the booster bar stays usable
        [SerializeField] private RectTransform _loseCountdown;
        [SerializeField] private TextMeshProUGUI _loseCountdownText;
        [SerializeField] private TextMeshProUGUI _loseCountdownNumber;
        [SerializeField, Min(0f)] private float _loseCountdownDrop = 0.35f;
        [SerializeField] private RectTransform _loseHintHand;                     // TutorialHand Spine, a child of SafeArea
        [SerializeField, Range(0.05f, 0.4f)] private float _loseHintHandSize = 0.17f;
        private Tween _loseCountdownTween;

        [Header("Progress Bar Segments")]
        [SerializeField, Min(0.05f)] private float _progressFillDuration = 0.4f;
        [Tooltip("Draw dividers between segments (and play the segment complete effect on them). Hidden for now; the GDD asks for them, so confirm with design before shipping.")]
        [SerializeField] private bool _showSegmentDividers;
        [SerializeField] private Color _segmentDividerColor = new Color(1f, 1f, 1f, 0.85f);
        [SerializeField, Min(1f)] private float _segmentDividerWidth = 4f;
        [SerializeField, Min(0f)] private float _segmentCompletePunchScale = 0.6f;
        [SerializeField, Min(0.05f)] private float _segmentCompletePunchDuration = 0.3f;

        [Header("Progress Star Animation")]
        [SerializeField] private RectTransform _starSpawnPoint;
        [SerializeField] private RectTransform _starTarget;
        [SerializeField] private Image[] _flyingStars;
        [SerializeField, Min(0.05f)] private float _starFlyDuration = 0.5f;
        [SerializeField, Min(0f)] private float _starStagger = 0.06f;
        [SerializeField] private Vector2 _starSpawnSpread = new Vector2(150f, 80f);
        [SerializeField, Min(0.05f)] private float _starAppearDuration = 0.12f;
        [SerializeField, Min(0f)] private float _targetPunchScale = 0.12f;

        [Header("Progress Star 3D Flight")]
        [Tooltip("3D star flown from the merged bricks to the level badge. Without it the 2D flying stars are used.")]
        [SerializeField] private GameObject _worldStarPrefab;
        [Tooltip("Level star badge that zooms and glows when the stars arrive.")]
        [SerializeField] private RectTransform _levelBadge;
        [Tooltip("Glow and twinkles played in the world behind the level badge.")]
        [SerializeField] private ParticleSystem _levelBadgeBurstPrefab;
        [SerializeField, Min(1)] private int _worldStarCount = 6;
        [SerializeField, Min(0.01f)] private float _worldStarScale = 0.9f;
        [Tooltip("How far the stars pop out around the merge before flying.")]
        [SerializeField, Min(0f)] private float _worldStarSpawnRadius = 0.55f;
        [SerializeField, Min(0f)] private float _worldStarHoldDuration = 0.08f;
        [Tooltip("Sideways bend of each star's flight path, in world units.")]
        [SerializeField, Min(0f)] private float _worldStarCurve = 0.9f;
        [Tooltip("Distance the flight plane sits in front of the board, so stars pass over the bricks.")]
        [SerializeField, Min(0f)] private float _worldStarCameraOffset = 3f;
        [SerializeField, Min(1f)] private float _levelBadgeZoomScale = 1.3f;
        [SerializeField, Min(0.01f)] private float _levelBadgeZoomInDuration = 0.12f;
        [SerializeField, Min(0.01f)] private float _levelBadgeZoomOutDuration = 0.35f;
        [Tooltip("Peak tilt, in degrees, of the badge's left-right wobble when the stars arrive.")]
        [SerializeField, Min(0f)] private float _levelBadgeWobbleAngle = 14f;
        [SerializeField, Min(0.01f)] private float _levelBadgeWobbleDuration = 1f;
        [Tooltip("How many times the badge swings back and forth during the wobble.")]
        [SerializeField, Min(1)] private int _levelBadgeWobbleVibrato = 10;

        [Header("Progress Complete")]
        [Tooltip("Confetti played in the world behind the progress bar when it fills to 100%.")]
        [SerializeField] private ParticleSystem _progressCompleteConfettiPrefab;
        [SerializeField, Min(0.01f)] private float _progressCompleteConfettiScale = 1f;

        private readonly System.Collections.Generic.List<Transform> _worldStars = new();
        private Transform _worldStarRoot;
        private ParticleSystem _levelBadgeBurst;
        private ParticleSystem _progressCompleteConfetti;
        // Merges can follow each other before earlier stars land, so every flight
        // keeps its own sequence instead of cutting the previous one short.
        private readonly System.Collections.Generic.List<Sequence> _worldStarSequences = new();
        private Sequence _levelBadgeSequence;
        private Tween _progressFillTween;
        private Vector3 _progressSource;
        private bool _hasProgressSource;

        private Sequence _progressSequence;
        private Sequence _boosterRewardSequence;
        private float _displayedProgress;
        private int _displayedLevel = -1;
        private float _rawProgress;
        private int _segmentCount;
        private readonly System.Collections.Generic.List<RectTransform> _segmentDividers = new();
        private System.Action _openSettings;
        private System.Action _restart;
        private System.Action _openShop;
        private Renderer _dealWorldRenderer;
        private Camera _dealWorldCamera;

        public RectTransform DealButtonRect =>
            _dealButton != null ? _dealButton.transform as RectTransform : null;

        public void BindDealWorldTarget(Transform target, Camera sceneCamera)
        {
            if (_dealButton == null)
                return;
            // Deal plays its own sound only when a Deal is accepted; the generic
            // tap sound would otherwise fire on every tap while the board is busy.
            if (target == null || sceneCamera == null)
                return;

            _dealWorldRenderer = target.GetComponent<Renderer>();
            _dealWorldCamera = sceneCamera;
            if (_dealWorldRenderer == null)
                return;

            var graphic = _dealButton.targetGraphic;
            if (graphic != null)
            {
                var color = graphic.color;
                color.a = 0f;
                graphic.color = color;
                graphic.raycastTarget = true;
            }
            foreach (var label in _dealButton.GetComponentsInChildren<TMP_Text>(true))
                label.enabled = false;
            _dealButton.transition = Selectable.Transition.None;
            AlignDealButton();
        }

        private void LateUpdate()
        {
            if (_dealWorldRenderer != null)
                AlignDealButton();
        }

        private void AlignDealButton()
        {
            var rect = DealButtonRect;
            var parent = rect != null ? rect.parent as RectTransform : null;
            if (parent == null || _dealWorldCamera == null)
                return;

            var bounds = _dealWorldRenderer.bounds;
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            for (var corner = 0; corner < 8; corner++)
            {
                var world = bounds.center + new Vector3(
                    (corner & 1) == 0 ? -bounds.extents.x : bounds.extents.x,
                    (corner & 2) == 0 ? -bounds.extents.y : bounds.extents.y,
                    (corner & 4) == 0 ? -bounds.extents.z : bounds.extents.z);
                var screen = _dealWorldCamera.WorldToScreenPoint(world);
                min = Vector2.Min(min, screen);
                max = Vector2.Max(max, screen);
            }

            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, min, null,
                out var localMin);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, max, null,
                out var localMax);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = (localMin + localMax) * 0.5f;
            rect.sizeDelta = localMax - localMin + new Vector2(12f, 12f);
        }

        public override void Init(object manager)
        {
            if (_moneySortInitialized) return;
            _moneySortInitialized = true;
            base.Init(manager);
            _dealButton.onClick.AddListener(OnDealClicked);
            if (_boosterButtons != null)
            {
            if (_boosterButtons != null)
            {
                foreach (var boosterButton in _boosterButtons)
                    boosterButton?.Bind(OnBoosterClicked);
            }
            }
            if (_settingsButton != null)
                _settingsButton.onClick.AddListener(OnSettingsClicked);
            if (_restartButton != null)
                _restartButton.onClick.AddListener(OnRestartClicked);
            if (_moneyButton != null)
                _moneyButton.onClick.AddListener(OnMoneyClicked);
            EnsureLivesTextFallback();
            HideFlyingStars();
        }

        /// <param name="progress">Bar fill, 0..1.</param>
        /// <param name="segmentCount">Equal segments the bar is split into (TargetRequire).</param>
        public void SetProgress(int level, float progress, int segmentCount)
        {
            _levelText.text = Mathf.Max(1, level).ToString();
            var target = Mathf.Clamp01(progress);
            var shouldAnimate =
                level == _displayedLevel &&
                target > _rawProgress &&
                isActiveAndEnabled &&
                _progressFill != null && _progressFill.gameObject.activeInHierarchy;   // Abhi's HUD hides the bar: no bar, no stars flying to it

            _displayedLevel = level;
            _rawProgress = target;
            SetSegmentCount(segmentCount);

            if (shouldAnimate)
                AnimateProgress(target);
            else
                SetProgressImmediate(target);
        }

        private void SetSegmentCount(int segmentCount)
        {
            segmentCount = Mathf.Max(1, segmentCount);
            if (segmentCount == _segmentCount || _progressFill == null)
                return;
            _segmentCount = segmentCount;

            // Dividers sit inside the fill rect so they line up with fillAmount.
            var dividerCount = _showSegmentDividers ? segmentCount - 1 : 0;
            for (var i = 0; i < Mathf.Max(dividerCount, _segmentDividers.Count); i++)
            {
                if (i >= dividerCount)
                {
                    _segmentDividers[i].DOKill();
                    _segmentDividers[i].gameObject.SetActive(false);
                    continue;
                }

                if (i >= _segmentDividers.Count)
                {
                    var dividerObject = new GameObject($"Segment Divider {i + 1}", typeof(RectTransform), typeof(Image));
                    var image = dividerObject.GetComponent<Image>();
                    image.color = _segmentDividerColor;
                    image.raycastTarget = false;
                    var rect = (RectTransform)dividerObject.transform;
                    rect.SetParent(_progressFill.rectTransform, false);
                    _segmentDividers.Add(rect);
                }

                var divider = _segmentDividers[i];
                var x = (i + 1) / (float)segmentCount;
                divider.DOKill();
                divider.gameObject.SetActive(true);
                divider.anchorMin = new Vector2(x, 0f);
                divider.anchorMax = new Vector2(x, 1f);
                divider.pivot = new Vector2(0.5f, 0.5f);
                divider.anchoredPosition = Vector2.zero;
                divider.sizeDelta = new Vector2(_segmentDividerWidth, 0f);
                divider.localScale = Vector3.one;
            }
        }

        private void PlaySegmentComplete(int dividerIndex)
        {
            if (dividerIndex < 0 || dividerIndex >= _segmentDividers.Count)
                return;
            var divider = _segmentDividers[dividerIndex];
            if (!divider.gameObject.activeSelf)
                return;
            divider.DOKill(true);
            divider.localScale = Vector3.one;
            divider.DOPunchScale(
                    new Vector3(_segmentCompletePunchScale, _segmentCompletePunchScale * 0.5f, 0f),
                    _segmentCompletePunchDuration,
                    6,
                    0.5f)
                .SetUpdate(true)
                .SetLink(divider.gameObject);
        }

        public void SetMoney(int amount)
        {
            if (_resourceGroup != null)
            {
                _resourceGroup.SetMoney(amount);
                return;
            }
            if (_moneyText != null)
                _moneyText.text = Mathf.Max(0, amount).ToString("N0");
        }

        public void SetLives(int current, int maximum, int refillSecondsRemaining, int infiniteSecondsRemaining = 0)
        {
            if (_resourceGroup != null)
            {
                _resourceGroup.SetLives(current, maximum, refillSecondsRemaining, infiniteSecondsRemaining);
                return;
            }
            EnsureLivesTextFallback();
            if (_livesText == null)
                return;
            if (infiniteSecondsRemaining > 0)
            {
                _livesText.text = $"∞  {ResourceGroupUI.FormatInfiniteTimer(infiniteSecondsRemaining)}";
                return;
            }
            var max = Mathf.Max(1, maximum);
            var clampedCurrent = Mathf.Clamp(current, 0, max);
            if (clampedCurrent >= max)
            {
                _livesText.text = $"{clampedCurrent}  Max";
                return;
            }
            var remaining = Mathf.Max(0, refillSecondsRemaining);
            _livesText.text = $"{clampedCurrent}  {remaining / 60:00}:{remaining % 60:00}";
        }

        private void EnsureLivesTextFallback()
        {
            if (_livesText != null || _moneyText == null)
                return;
            _livesText = Instantiate(_moneyText, _moneyText.transform.parent);
            _livesText.name = "LivesTextFallback";
            _livesText.rectTransform.anchoredPosition += new Vector2(0f, -48f);
            _livesText.raycastTarget = false;
        }

        public void InitializeCheats(
            System.Action win,
            System.Action lose,
            System.Action<int> goToLevel,
            System.Action<MoneySortBoosterType> addBooster,
            System.Action<int> addCoins,
            System.Action<bool> setNotificationTestEnabled,
            System.Func<bool> getNotificationTestEnabled,
            System.Action<bool> setRandomGiftTestEnabled,
            System.Func<bool> getRandomGiftTestEnabled,
            System.Action<bool> setRevealHiddenEnabled,
            System.Func<bool> getRevealHiddenEnabled,
            System.Action resetSave,
            System.Action addLife)
        {
            if (_cheatUI == null)
                throw new MissingReferenceException("Gameplay UI requires an authored cheat panel.");
            _cheatUI.Initialize(
                win,
                lose,
                goToLevel,
                addBooster,
                addCoins,
                setNotificationTestEnabled,
                getNotificationTestEnabled,
                setRandomGiftTestEnabled,
                getRandomGiftTestEnabled,
                setRevealHiddenEnabled,
                getRevealHiddenEnabled,
                resetSave,
                addLife);
        }

        public void InitializeSettings(System.Action openSettings) =>
            _openSettings = openSettings;

        private void OnSettingsClicked() => _openSettings?.Invoke();

        public void InitializeRestart(System.Action restart) =>
            _restart = restart;

        private void OnRestartClicked() => _restart?.Invoke();

        public void InitializeShop(System.Action openShop) =>
            _openShop = openShop;

        private void OnMoneyClicked() => _openShop?.Invoke();

        // Draws the coin bar above a popup's dark backdrop so it can still open the Shop.
        public void SetMoneyAbovePopups(bool above)
        {
            if (_moneyButton != null)
                CanvasLift.Set(_moneyButton.gameObject, above);
        }

        public RectTransform CoinFlyTarget =>
            _coinFlyTarget != null
                ? _coinFlyTarget
                : _moneyButton != null ? _moneyButton.transform as RectTransform : null;

        // Draws the lives bar above a popup's dark backdrop next to the coin bar.
        public void SetLivesAbovePopups(bool above) => CanvasLift.Set(_livesHud, above);

        public void SetCheatBoosterCounts(int shuffle, int hammer, int freeMove) =>
            _cheatUI?.SetBoosterCounts(shuffle, hammer, freeMove);

        public Sprite GetBoosterIcon(MoneySortBoosterType type)
        {
            var configuredIcon = _boosterConfig != null ? _boosterConfig.GetIcon(type) : null;
            if (configuredIcon != null)
                return configuredIcon;

            return GetBoosterButton(type)?.Icon;
        }

        public RectTransform GetBoosterButtonRect(MoneySortBoosterType type)
        {
            return GetBoosterButton(type)?.RectTransform;
        }

        public void ShowBoosterUsePrompt(MoneySortBoosterType type, System.Action cancel) =>
            _boosterUsePrompt?.Show(type, cancel);

        public void HideBoosterUsePrompt() => _boosterUsePrompt?.Hide();

        /// <summary>D-136: the lose countdown bar drops in with <paramref name="message"/> and the first number.</summary>
        public void ShowLoseCountdown(string message, string number)
        {
            if (_loseCountdown == null) return;
            _loseCountdownTween?.Kill(true);
            if (_loseCountdownText != null) _loseCountdownText.text = message;
            if (_loseCountdownNumber != null) _loseCountdownNumber.text = number;
            _loseCountdown.gameObject.SetActive(true);
            _loseCountdown.localScale = new Vector3(1f, 0f, 1f);
            _loseCountdownTween = _loseCountdown.DOScaleY(1f, _loseCountdownDrop).SetEase(Ease.OutBack).SetUpdate(true).SetLink(gameObject);
        }

        /// <summary>D-136: the stopwatch shows the next number, with a small punch.</summary>
        public void SetLoseCountdown(string number)
        {
            if (_loseCountdownNumber == null) return;
            _loseCountdownNumber.text = number;
            _loseCountdownNumber.transform.DOKill(true);
            _loseCountdownNumber.transform.DOPunchScale(Vector3.one * 0.35f, 0.25f, 1, 0f).SetUpdate(true).SetLink(gameObject);
        }

        /// <summary>D-136 (GDD "Before Revive: booster hint"): the pointing hand on booster tile <paramref name="index"/>
        /// (0 Hand, 1 Shuffle, 2 Magnet); a negative index hides it.</summary>
        public void PointAtBooster(int index)
        {
            if (_loseHintHand == null) return;
            BoosterButtonUI tile = null;
            if (index >= 0 && _boosterButtons != null)
                foreach (var b in _boosterButtons)
                    if (b != null && MoneySortUiValue.BoosterIndex(b.Type) == index) { tile = b; break; }
            if (tile == null) { _loseHintHand.gameObject.SetActive(false); return; }
            var space = (RectTransform)_loseHintHand.parent;
            Vector2 tip = space.InverseTransformPoint(((RectTransform)tile.transform).TransformPoint(((RectTransform)tile.transform).rect.center));
            TutorialHand.Place(_loseHintHand, space.rect, tip, _loseHintHandSize);
            _loseHintHand.SetAsLastSibling();
        }

        public void HideLoseCountdown()
        {
            if (_loseHintHand != null) _loseHintHand.gameObject.SetActive(false);
            if (_loseCountdown == null || !_loseCountdown.gameObject.activeSelf) return;
            _loseCountdownTween?.Kill(true);
            _loseCountdownTween = _loseCountdown.DOScaleY(0f, 0.15f).SetEase(Ease.InBack).SetUpdate(true).SetLink(gameObject)
                .OnComplete(() => _loseCountdown.gameObject.SetActive(false));
        }

        // D-118: the MAX banner sits at the bottom centre over the booster bar; while it is up the bar moves up by its
        // height. MAX banners are 50 dp on a phone (adaptive ones a little more) and 90 dp on a tablet: keep 60 / 90 dp.
        // Returns the space kept, in canvas units (= world units on the rig ruler).
        public float ReserveBannerSpace(bool bannerUp)
        {
            var container = _boosterButtons != null && _boosterButtons.Length > 0 && _boosterButtons[0] != null
                ? _boosterButtons[0].transform.parent as RectTransform : null;
            if (container == null) return 0f;
            if (!_skillBaseSaved) { _skillBaseY = container.anchoredPosition.y; _skillBaseSaved = true; }
            float units = bannerUp ? BannerUnits() : 0f;
            container.anchoredPosition = new Vector2(container.anchoredPosition.x, _skillBaseY + units);
            _boosterUsePrompt?.SetBottomInset(units);
            return units;
        }
        private bool _skillBaseSaved;
        private float _skillBaseY;

        private float BannerUnits()
        {
            float pxPerDp = Screen.dpi > 0f ? Screen.dpi / 160f : Screen.width / 393f;   // no dpi: a 393 dp wide phone
            float dp = Screen.width / pxPerDp >= 728f ? 90f : 60f;
            float px = dp * pxPerDp;
            var cam = GetComponentInParent<Canvas>()?.rootCanvas.worldCamera;
            if (cam != null && cam.orthographic && Screen.height > 0) return px * cam.orthographicSize * 2f / Screen.height;
            var canvas = GetComponentInParent<Canvas>();
            return px / Mathf.Max(0.01f, canvas != null ? canvas.rootCanvas.scaleFactor : 1f);
        }

        /// <summary>World y of the booster bar's top edge (the UI hosts share the world ruler).</summary>
        public float BoosterBarTopWorldY()
        {
            float top = float.MinValue;
            if (_boosterButtons == null) return top;
            var corners = new Vector3[4];
            foreach (var b in _boosterButtons)
            {
                if (b == null || !b.gameObject.activeInHierarchy) continue;
                ((RectTransform)b.transform).GetWorldCorners(corners);
                top = Mathf.Max(top, corners[1].y);
            }
            return top;
        }

        public void SetBannerHeight(float screenPixels)
        {
            if (_boosterUsePrompt == null)
                return;
            var canvas = GetComponentInParent<Canvas>();
            var scale = canvas != null ? canvas.rootCanvas.scaleFactor : 1f;
            _boosterUsePrompt.SetBottomInset(screenPixels / Mathf.Max(0.01f, scale));
        }

        public void PlayBoosterRewardAnimation(MoneySortBoosterType type, System.Action onLanded)
        {
            KillBoosterRewardAnimation();
            _boosterRewardSequence = BoosterRewardFlyAnimation.Play(
                GetBoosterIcon(type),
                transform,
                GetBoosterButtonRect(type),
                BoosterRewardFlyAnimation.DefaultStartSize,
                BoosterRewardFlyAnimation.DefaultHoldDuration,
                BoosterRewardFlyAnimation.DefaultFlyDuration,
                BoosterRewardFlyAnimation.DefaultEndScale,
                () =>
                {
                    _boosterRewardSequence = null;
                    onLanded?.Invoke();
                });
        }

        private void OnDealClicked() => DealRequested?.Invoke();
        private void OnBoosterClicked(MoneySortBoosterType type)
        {
            switch (type)
            {
                case MoneySortBoosterType.Shuffle:
                    BoosterRequested?.Invoke(MoneySortBoosterType.Shuffle);
                    break;
                case MoneySortBoosterType.Hammer:
                    BoosterRequested?.Invoke(MoneySortBoosterType.Hammer);
                    break;
                case MoneySortBoosterType.FreeMove:
                    BoosterRequested?.Invoke(MoneySortBoosterType.FreeMove);
                    break;
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        public void SetBoosterState(
            MoneySortBoosterType type,
            int count,
            bool unlocked,
            int unlockLevel)
        {
            var button = GetBoosterButton(type);
            if (button == null)
                return;

            var icon = _boosterConfig != null ? _boosterConfig.GetIcon(type) : null;
            button.SetState(count, unlocked, unlockLevel, icon);
        }

        private BoosterButtonUI GetBoosterButton(MoneySortBoosterType type)
        {
            if (_boosterButtons == null)
                return null;
            foreach (var boosterButton in _boosterButtons)
            {
                if (boosterButton != null && boosterButton.Type == type)
                    return boosterButton;
            }
            return null;
        }

        private void AnimateProgress(float target)
        {
            if (HasWorldStarAnimation())
            {
                AnimateWorldStars(target);
                return;
            }

            KillProgressAnimation();
            if (!HasStarAnimation())
            {
                _progressFillTween = DOTween.To(
                        () => _displayedProgress,
                        UpdateProgressDisplay,
                        target,
                        _progressFillDuration)
                    .SetEase(Ease.OutQuad)
                    .SetUpdate(true)
                    .SetLink(gameObject);
                return;
            }

            _progressSequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);

            for (var i = 0; i < _flyingStars.Length; i++)
            {
                var starIndex = i;
                var star = _flyingStars[i];
                var rect = star.rectTransform;
                var delay = i * _starStagger;
                var offset = GetSpawnOffset(i);

                star.gameObject.SetActive(true);
                star.color = Color.white;
                rect.position = _starSpawnPoint.position;
                rect.anchoredPosition += offset;
                rect.localScale = Vector3.zero;
                rect.localRotation = Quaternion.Euler(0f, 0f, -25f + i * 11f);

                _progressSequence.Insert(
                    delay,
                    rect.DOScale(1f, _starAppearDuration).SetEase(Ease.OutBack));
                _progressSequence.Insert(
                    delay,
                    rect.DOMove(_starTarget.position, _starFlyDuration)
                        .SetEase(Ease.InQuad)
                        .OnComplete(() =>
                        {
                            star.gameObject.SetActive(false);
                            ProgressStarLanded?.Invoke(starIndex);
                            // Only the level icon reacts; the bar itself stays still.
                            var punchTarget = _levelBadge != null ? _levelBadge : _starTarget;
                            punchTarget.DOKill();
                            punchTarget.localScale = Vector3.one;
                            punchTarget.DOPunchScale(
                                Vector3.one * _targetPunchScale,
                                0.18f,
                                4,
                                0.5f).SetUpdate(true);
                        }));
                _progressSequence.Insert(
                    delay,
                    rect.DORotate(new Vector3(0f, 0f, 180f), _starFlyDuration, RotateMode.FastBeyond360)
                        .SetEase(Ease.Linear));
            }

            _progressSequence.Insert(
                0f,
                DOVirtual.Float(_displayedProgress, target, _progressFillDuration, UpdateProgressDisplay)
                    .SetEase(Ease.OutCubic)
                    .SetUpdate(true));
            _progressSequence.OnComplete(() =>
            {
                UpdateProgressDisplay(target);
                HideFlyingStars();
                _progressSequence = null;
            });
        }

        private Vector2 GetSpawnOffset(int index)
        {
            if (_flyingStars.Length <= 1)
                return Vector2.zero;

            var normalized = index / (float)(_flyingStars.Length - 1);
            return new Vector2(
                Mathf.Lerp(-_starSpawnSpread.x, _starSpawnSpread.x, normalized),
                Mathf.Sin(index * 1.7f) * _starSpawnSpread.y);
        }

        private Camera WorldCamera =>
            _dealWorldCamera != null ? _dealWorldCamera : Camera.main;

        private bool HasWorldStarAnimation() =>
            _worldStarPrefab != null && _levelBadge != null && WorldCamera != null;

        public void OnProgressSourcePositioned(Vector3 position)
        {
            _progressSource = position;
            _hasProgressSource = true;
        }

        // Stars pop out around the merged bricks, stream up to the level badge,
        // and the badge zooms and glows on the first arrival while the bar fills
        // as the rest land.
        private void AnimateWorldStars(float target)
        {
            var worldCamera = WorldCamera;
            var cameraTransform = worldCamera.transform;
            var sourceScreen = _hasProgressSource
                ? worldCamera.WorldToScreenPoint(_progressSource)
                : GetFallbackSourceScreenPoint(worldCamera);
            _hasProgressSource = false;

            // The canvas is Screen Space Overlay, so the 3D stars fly on a plane
            // in front of the board and end right behind the badge.
            var depth = Mathf.Max(worldCamera.nearClipPlane + 0.5f, sourceScreen.z - _worldStarCameraOffset);
            var start = worldCamera.ScreenToWorldPoint(new Vector3(sourceScreen.x, sourceScreen.y, depth));
            var badgeScreen = RectTransformUtility.WorldToScreenPoint(null, _levelBadge.position);
            var end = worldCamera.ScreenToWorldPoint(new Vector3(badgeScreen.x, badgeScreen.y, depth));
            var facing = Quaternion.LookRotation(-cameraTransform.forward, cameraTransform.up);

            var starCount = Mathf.Max(1, _worldStarCount);
            var flightStart = _starAppearDuration + _worldStarHoldDuration;
            var firstLanding = flightStart + _starFlyDuration;
            var sequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);

            for (var i = 0; i < starCount; i++)
            {
                var starIndex = i;
                var star = GetWorldStar();
                var delay = i * _starStagger;
                var angle = i * (360f / starCount) + Random.Range(-25f, 25f);
                var popOffset = Quaternion.AngleAxis(angle, cameraTransform.forward) *
                                cameraTransform.right *
                                (_worldStarSpawnRadius * Random.Range(0.55f, 1f));
                var popPosition = start + popOffset;
                var scale = Vector3.one * (_worldStarScale * Random.Range(0.85f, 1.1f));
                var bend = cameraTransform.right *
                           ((i % 2 == 0 ? 1f : -1f) * _worldStarCurve * Random.Range(0.4f, 1f));
                var control = (popPosition + end) * 0.5f + bend;

                star.position = start;
                star.rotation = facing * Quaternion.Euler(
                    Random.Range(-25f, 25f),
                    Random.Range(-25f, 25f),
                    Random.Range(0f, 360f));
                star.localScale = Vector3.zero;
                star.gameObject.SetActive(true);

                sequence.Insert(delay, star.DOMove(popPosition, _starAppearDuration).SetEase(Ease.OutQuad));
                sequence.Insert(delay, star.DOScale(scale, _starAppearDuration).SetEase(Ease.OutBack));
                sequence.Insert(
                    delay + flightStart,
                    DOVirtual.Float(0f, 1f, _starFlyDuration, progress =>
                        {
                            var inverse = 1f - progress;
                            star.position =
                                inverse * inverse * popPosition +
                                2f * inverse * progress * control +
                                progress * progress * end;
                        })
                        .SetEase(Ease.InQuad));
                // Shrinking on approach makes the stars read as absorbed by the badge.
                sequence.Insert(
                    delay + flightStart,
                    star.DOScale(scale * 0.5f, _starFlyDuration).SetEase(Ease.InQuad));
                sequence.Insert(
                    delay,
                    star.DOLocalRotate(
                            new Vector3(0f, 0f, i % 2 == 0 ? 360f : -360f),
                            flightStart + _starFlyDuration,
                            RotateMode.LocalAxisAdd)
                        .SetEase(Ease.Linear));
                sequence.InsertCallback(delay + firstLanding, () =>
                {
                    star.gameObject.SetActive(false);
                    ProgressStarLanded?.Invoke(starIndex);
                    if (starIndex == 0)
                        PlayLevelBadgeArrival(end, facing);
                });
            }

            _worldStarSequences.Add(sequence);
            sequence.OnKill(() => _worldStarSequences.Remove(sequence));

            // The bar starts moving when the first star arrives.
            KillProgressAnimation();
            _progressFillTween = DOTween.To(
                    () => _displayedProgress,
                    UpdateProgressDisplay,
                    target,
                    _progressFillDuration)
                .SetDelay(firstLanding)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        private Vector3 GetFallbackSourceScreenPoint(Camera worldCamera)
        {
            // Progress without a board source keeps using the authored UI spawn
            // point, projected at the depth of the board centre (world origin).
            var boardDepth = worldCamera.WorldToScreenPoint(Vector3.zero).z;
            var screen = _starSpawnPoint != null
                ? RectTransformUtility.WorldToScreenPoint(null, _starSpawnPoint.position)
                : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            return new Vector3(screen.x, screen.y, boardDepth);
        }

        private void PlayLevelBadgeArrival(Vector3 worldPosition, Quaternion facing)
        {
            _levelBadgeSequence?.Kill();
            _levelBadge.localScale = Vector3.one;
            _levelBadge.localRotation = Quaternion.identity;
            _levelBadgeSequence = DOTween.Sequence()
                .SetUpdate(true)
                .SetLink(gameObject)
                .Append(_levelBadge.DOScale(_levelBadgeZoomScale, _levelBadgeZoomInDuration).SetEase(Ease.OutQuad))
                .Append(_levelBadge.DOScale(1f, _levelBadgeZoomOutDuration).SetEase(Ease.OutBack))
                // The badge swings left and right, settling as the bar fills.
                .Insert(0f, _levelBadge.DOPunchRotation(
                    new Vector3(0f, 0f, _levelBadgeWobbleAngle),
                    _levelBadgeWobbleDuration,
                    _levelBadgeWobbleVibrato,
                    0.5f))
                .OnKill(() =>
                {
                    _levelBadge.localScale = Vector3.one;
                    _levelBadge.localRotation = Quaternion.identity;
                });

            if (_levelBadgeBurstPrefab == null)
                return;
            if (_levelBadgeBurst == null)
            {
                _levelBadgeBurst = Instantiate(_levelBadgeBurstPrefab, GetWorldStarRoot());
                _levelBadgeBurst.name = $"{_levelBadgeBurstPrefab.name} (Reusable)";
            }
            // The overlay canvas draws over the world, so the glow shows around
            // the badge exactly like a halo behind it.
            _levelBadgeBurst.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _levelBadgeBurst.transform.SetPositionAndRotation(worldPosition, facing);
            _levelBadgeBurst.Play(true);
        }

        private Transform GetWorldStar()
        {
            for (var i = 0; i < _worldStars.Count; i++)
            {
                if (_worldStars[i] != null && !_worldStars[i].gameObject.activeSelf)
                    return _worldStars[i];
            }

            var star = Instantiate(_worldStarPrefab, GetWorldStarRoot()).transform;
            star.name = $"{_worldStarPrefab.name} {_worldStars.Count}";
            star.gameObject.SetActive(false);
            _worldStars.Add(star);
            return star;
        }

        private Transform GetWorldStarRoot()
        {
            if (_worldStarRoot == null)
                _worldStarRoot = new GameObject("Progress Star FX").transform;
            return _worldStarRoot;
        }

        private void KillWorldStarFlights()
        {
            for (var i = _worldStarSequences.Count - 1; i >= 0; i--)
                _worldStarSequences[i]?.Kill();
            _worldStarSequences.Clear();
            _levelBadgeSequence?.Kill();
            _levelBadgeSequence = null;
            for (var i = 0; i < _worldStars.Count; i++)
            {
                if (_worldStars[i] == null)
                    continue;
                _worldStars[i].DOKill();
                _worldStars[i].gameObject.SetActive(false);
            }
            if (_levelBadgeBurst != null)
                _levelBadgeBurst.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (_progressCompleteConfetti != null)
                _progressCompleteConfetti.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void SetProgressImmediate(float value)
        {
            KillProgressAnimation();
            // Jumps (level start, restore) do not play segment effects.
            _displayedProgress = Mathf.Clamp01(value);
            UpdateProgressDisplay(value);
            HideFlyingStars();
        }

        private void UpdateProgressDisplay(float value)
        {
            var previous = _displayedProgress;
            _displayedProgress = Mathf.Clamp01(value);
            _progressFill.fillAmount = _displayedProgress;
            // One decimal so small ladder credit is still visible.
            _progressText.text =
                (_displayedProgress * 100f).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "%";

            // A divider plays its effect when the fill crosses it.
            var segments = Mathf.Max(1, _segmentCount);
            var before = Mathf.FloorToInt(previous * segments + 0.0001f);
            var after = Mathf.FloorToInt(_displayedProgress * segments + 0.0001f);
            for (var segment = before + 1; segment <= after && segment < segments; segment++)
                PlaySegmentComplete(segment - 1);

            // Jumps never get here with a change, so only an animated fill completes the bar.
            if (previous < 1f && _displayedProgress >= 1f)
            {
                PlayProgressCompleteConfetti();
                ProgressBarFilled?.Invoke();
            }
        }

        private void PlayProgressCompleteConfetti()
        {
            var worldCamera = WorldCamera;
            if (_progressCompleteConfettiPrefab == null || worldCamera == null)
                return;
            if (_progressCompleteConfetti == null)
            {
                _progressCompleteConfetti = Instantiate(_progressCompleteConfettiPrefab, GetWorldStarRoot());
                _progressCompleteConfetti.name = $"{_progressCompleteConfettiPrefab.name} (Reusable)";
            }

            // Like the badge glow, the confetti flies on a plane in front of the board
            // and bursts out around the overlay bar.
            var barScreen = RectTransformUtility.WorldToScreenPoint(null, _progressFill.rectTransform.position);
            var boardDepth = worldCamera.WorldToScreenPoint(Vector3.zero).z;
            var depth = Mathf.Max(worldCamera.nearClipPlane + 0.5f, boardDepth - _worldStarCameraOffset);
            var cameraTransform = worldCamera.transform;
            _progressCompleteConfetti.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _progressCompleteConfetti.transform.SetPositionAndRotation(
                worldCamera.ScreenToWorldPoint(new Vector3(barScreen.x, barScreen.y, depth)),
                Quaternion.LookRotation(-cameraTransform.forward, cameraTransform.up));
            _progressCompleteConfetti.transform.localScale = Vector3.one * _progressCompleteConfettiScale;
            _progressCompleteConfetti.Play(true);
        }

        private bool HasStarAnimation() =>
            _starSpawnPoint != null &&
            _starTarget != null &&
            _flyingStars != null &&
            _flyingStars.Length > 0;

        private void KillProgressAnimation()
        {
            _progressSequence?.Kill();
            _progressSequence = null;
            _progressFillTween?.Kill();
            _progressFillTween = null;
            _starTarget?.DOKill();
            if (_starTarget != null)
                _starTarget.localScale = Vector3.one;
        }

        private void KillBoosterRewardAnimation()
        {
            _boosterRewardSequence?.Kill();
            _boosterRewardSequence = null;
            var flyingIcon = transform.Find("Flying Booster Icon");
            if (flyingIcon != null)
                Destroy(flyingIcon.gameObject);
        }

        private void HideFlyingStars()
        {
            if (_flyingStars == null)
                return;

            foreach (var star in _flyingStars)
            {
                if (star == null)
                    continue;

                star.rectTransform.DOKill();
                star.gameObject.SetActive(false);
            }
        }

        private void OnEnable()
        {
        }

        private void OnDisable()
        {
            HideBoosterUsePrompt();
            KillProgressAnimation();
            KillWorldStarFlights();
            KillBoosterRewardAnimation();
            HideFlyingStars();
        }

        private void OnDestroy()
        {
            KillProgressAnimation();
            KillWorldStarFlights();
            if (_worldStarRoot != null)
                Destroy(_worldStarRoot.gameObject);
            KillBoosterRewardAnimation();
            if (_dealButton != null) _dealButton.onClick.RemoveListener(OnDealClicked);
            if (_settingsButton != null) _settingsButton.onClick.RemoveListener(OnSettingsClicked);
            if (_restartButton != null) _restartButton.onClick.RemoveListener(OnRestartClicked);
            if (_moneyButton != null) _moneyButton.onClick.RemoveListener(OnMoneyClicked);
        }
    }
}
