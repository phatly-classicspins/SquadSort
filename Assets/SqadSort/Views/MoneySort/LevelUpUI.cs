using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Views
{
    public sealed class FeatureMilestoneProgress
    {
        public readonly int Current;
        public readonly int Total;
        public readonly Sprite Icon;
        public readonly string FallbackLabel;

        public FeatureMilestoneProgress(int current, int total, Sprite icon, string fallbackLabel)
        {
            Current = Mathf.Max(0, current);
            Total = Mathf.Max(1, total);
            Icon = icon;
            FallbackLabel = fallbackLabel ?? string.Empty;
        }

        public static FeatureMilestoneProgress FromUnlockLevels(
            int previousUnlockLevel,
            int nextUnlockLevel,
            int nextLevel,
            Sprite icon,
            string fallbackLabel)
        {
            var total = Mathf.Max(1, nextUnlockLevel - previousUnlockLevel );
            var current = Mathf.Clamp(nextLevel - previousUnlockLevel, 0, total);
            return new FeatureMilestoneProgress(current, total, icon, fallbackLabel);
        }
    }

    public sealed class LevelUpRewardOffer
    {
        public readonly int BaseCoins;
        // Bonus wheel segments, left to right. The ad multiplies the base reward by the picked one.
        public readonly int[] Multipliers;
        // Called with the picked multiplier; reports whether the ad paid out.
        public readonly Action<int, Action<bool>> RequestBonus;
        // A level skipped with an ad completes without any coin reward.
        public readonly bool Skipped;

        public LevelUpRewardOffer(
            int baseCoins,
            int[] multipliers,
            Action<int, Action<bool>> requestBonus,
            bool skipped = false)
        {
            BaseCoins = Mathf.Max(0, baseCoins);
            Multipliers = multipliers;
            RequestBonus = requestBonus;
            Skipped = skipped;
        }

        // What the ad adds on top of the already granted base: x3 on 40 => +80.
        public int GetBonusCoins(int multiplier) => BaseCoins * Mathf.Max(0, multiplier - 1);
    }

    public sealed class LevelUpUI : UIBase
    {
        [SerializeField] private Sprite[] _featureIcons = Array.Empty<Sprite>();
        [SerializeField] private BoosterConfigSO _featureBoosters;
        public Sprite FeatureBoosterIcon(MoneySortBoosterType type) => _featureBoosters?.GetIcon(type);
        public Sprite FeatureIcon(int index) => index >= 0 && index < _featureIcons.Length ? _featureIcons[index] : null;

        private bool _initializedForCardSlot;
        private bool _externalBonus;
        private bool _externalBonusEnabled;
        public override void Init(object manager){if(_initializedForCardSlot)return;_initializedForCardSlot=true;base.Init(manager);_rewardedCoinButton?.onClick.AddListener(RequestBonusReward);_continueButton?.onClick.AddListener(OnContinueClicked);}
        public void SetExternalBonus(string[] segments,string amount,bool available,Action pressed){_externalBonus=true;_externalBonusEnabled=available;if(_bonusWheel!=null){_bonusWheel.gameObject.SetActive(segments!=null);if(segments!=null){var values=new int[segments.Length];for(int i=0;i<values.Length;i++)values[i]=MoneySortUiValue.Number(segments[i]);_bonusWheel.SetMultipliers(values);_bonusWheel.StopSwing();_bonusWheel.enabled=false;}}OverrideButton("_rewardedCoinButton",pressed,available);SetAd("_rewardedCoinButton",available);if(_rewardedCoinText!=null && amount!=null)_rewardedCoinText.text=amount;}
        public void SetExternalNeedle(float angle,string amount)
        {
            _bonusWheel?.SetExternalAngle(angle);
            if (_rewardedCoinText == null || amount == null || _rewardedCoinText.text == amount) return;
            _rewardedCoinText.text = amount;
            // the amount pops each time it changes, so the number reads as jumping with the needle (Blockery)
            _bonusAmountPunch?.Kill(true);
            _rewardedCoinText.transform.localScale = Vector3.one;
            _bonusAmountPunch = _rewardedCoinText.transform.DOPunchScale(Vector3.one * 0.25f, 0.18f, 1, 0f).SetUpdate(true);
        }
        private Tween _bonusAmountPunch;

        // Money Sort's level complete coin fly (CoinRewardFlyAnimation): coins burst out of the pressed button and stream into
        // the wallet in the corner. onCoinLanded gets (landed, flying); onFinished always runs once, also when killed early.
        public void PlayCoinFly(bool fromBonus, int amount, Action<int, int> onCoinLanded, Action onFinished)
        {
            var source = (fromBonus ? _rewardedCoinButton : _continueButton)?.transform as RectTransform;
            _coinFly?.Kill(true);
            _coinFly = CoinRewardFlyAnimation.Play(_coinFlySprite, source, transform, WalletCoinIcon(), amount, onCoinLanded, onFinished);
        }
        private Sequence _coinFly;
        private RectTransform _walletCoinIcon;
        private RectTransform WalletCoinIcon()
        {
            if (_walletCoinIcon != null) return _walletCoinIcon;
            foreach (var t in GetComponentsInChildren<RectTransform>(true))
                if (t.name == "CoinIcon" && t.parent != null && t.parent.name == "Wallet") return _walletCoinIcon = t;
            return null;
        }
        public void SetExternalBonusEnabled(bool enabled){_externalBonusEnabled=enabled;SetAd("_rewardedCoinButton",enabled);}
        // a press in flight (needle stopped, ad about to play): the button only stops taking taps, it never reads "No ads
        // available" — that look is SetExternalBonusEnabled's, for an ad that is not loaded (as Money Sort's request in flight)
        public void SetExternalBonusInteractable(bool interactable)=>SetRewardedCoinButtonInteractable(interactable);

        [SerializeField] private TextMeshProUGUI _levelText;
        [Header("Coin reward")]
        [SerializeField] private TextMeshProUGUI _coinRewardText;
        [Tooltip("Coin icon that flies into the coin bar when the player leaves the screen.")]
        [SerializeField] private Sprite _coinFlySprite;
        [SerializeField] private Button _rewardedCoinButton;
        [SerializeField] private TextMeshProUGUI _rewardedCoinText;
        [Tooltip("Coin reward visuals hidden when the level was skipped with an ad.")]
        [SerializeField] private GameObject[] _coinRewardObjects;
        [SerializeField] private BonusMultiplierWheelUI _bonusWheel;
        [Tooltip("Pause between stopping the wheel and playing the rewarded ad.")]
        [SerializeField, Min(0f)] private float _bonusAdDelay = 1f;
        [SerializeField] private Button _continueButton;
        [SerializeField] private Sprite _hardContinueSprite;

        public void SetHardContinue(bool hard)
        {
            if (_continueButton.targetGraphic is Image image)
                image.overrideSprite = hard ? _hardContinueSprite : null;
        }
        [Header("Next feature progress")]
        [SerializeField] private GameObject _featureProgressRoot;
        [SerializeField] private Image _featureProgressFill;
        [SerializeField] private TextMeshProUGUI _featureProgressText;
        [SerializeField] private Image _featureIcon;
        [SerializeField] private TextMeshProUGUI _featureFallbackText;
        [Tooltip("Wait for the popup to appear before the bar moves from the previous count.")]
        [SerializeField, Min(0f)] private float _featureProgressDelay = 0.5f;
        [SerializeField, Min(0.05f)] private float _featureProgressFillDuration = 1.2f;
        [Header("Unlocked chip reward")]
        // [SerializeField] private GameObject _unlockRewardRoot;
        [SerializeField] private RectTransform _rotatingGlow;
        [SerializeField] private RectTransform _secondaryRotatingGlow;
        // [SerializeField] private RawImage _chipPreviewImage;
        // [SerializeField] private Camera _chipPreviewCamera;
        // [SerializeField] private Transform _unlockChipPivot;
        // [SerializeField] private ChipView _unlockChipView;
        [SerializeField, Min(0.1f)] private float _glowRotationDuration = 5f;
        [SerializeField, Min(0.05f)] private float _chipPopDuration = 0.35f;
        [SerializeField, Range(0f, 0.5f)] private float _chipPulseStrength = 0.12f;
        [SerializeField, Min(0.1f)] private float _chipRotationDuration = 2.5f;
        [Header("ComfirmIndicator")]
        [SerializeField] private TextMeshProUGUI _confirmIndicator;
        [SerializeField, Min(0.05f)] private float _fadeDuration = 0.65f;
        [SerializeField, Range(0f, 1f)] private float _minimumFadeAlpha = 0.18f;

        private Action _tapAction;
        private LevelUpRewardOffer _rewardOffer;
        private Tween _bonusAdDelayTween;
        private Tween _featureProgressTween;
        private Sequence _rewardSequence;
        private Vector3 _chipBaseScale = Vector3.one;
        private bool _bonusRequestInFlight;
        private bool _bonusRewardClaimed;

        public Sprite CoinFlySprite => _coinFlySprite;
        // The button the player pressed to leave: the coins fly out of it.
        public RectTransform CoinFlySource { get; private set; }

        private void Awake() => Init(null);

        private void OnContinueClicked()
        {
            if (_bonusRequestInFlight)
                return;
            CoinFlySource = _continueButton.transform as RectTransform;
            Continue();
        }

        public override void SetData(params object[] data)
        {
            if (data.Length > 0)
            {
                _levelText.text = data[0] switch
                {
                    int level => $"{level}",
                    string title => title,
                    _ => _levelText.text
                };
            }

            var unlockedChipLevel = data.Length > 1 && data[1] is int value
                ? value
                : -1;
            var chipMaterial = data.Length > 2 ? data[2] as Material : null;
            _tapAction = data.Length > 3 ? data[3] as Action : null;
            SetFeatureProgress(data.Length > 4 ? data[4] as FeatureMilestoneProgress : null);
            SetCoinReward(data.Length > 5 ? data[5] as LevelUpRewardOffer : null);
            SetUnlockReward(unlockedChipLevel, chipMaterial);
        }

        private void SetCoinReward(LevelUpRewardOffer offer)
        {
            _bonusRequestInFlight = false;
            _bonusRewardClaimed = false;
            CoinFlySource = null;
            KillBonusAdDelay();
            _bonusAmountPunch?.Kill(true);
            _rewardOffer = offer;
            var canRequestBonus = offer?.RequestBonus != null;

            var skipped = offer?.Skipped == true;
            if (_coinRewardObjects != null)
            {
                foreach (var coinRewardObject in _coinRewardObjects)
                {
                    if (coinRewardObject != null)
                        coinRewardObject.SetActive(!skipped);
                }
            }
            if (_coinRewardText != null)
                _coinRewardText.text = (offer?.BaseCoins ?? 0).ToString();
            if (_rewardedCoinButton != null)
            {
                _rewardedCoinButton.gameObject.SetActive(canRequestBonus);
                RewardedAdButton.Bind(
                    _rewardedCoinButton,
                    canRequestBonus ? AdsPlacement.LevelCompleteReward : null,
                    canRequestBonus);
            }
            if (_bonusWheel != null)
            {
                _bonusWheel.gameObject.SetActive(canRequestBonus);
                if (canRequestBonus)
                {
                    _bonusWheel.SetMultipliers(offer.Multipliers);
                    _bonusWheel.StartSwing();
                }
            }
            RefreshBonusText();
        }

        private void Update()
        {
            if (_externalBonus) return;
            if (_bonusWheel != null && _bonusWheel.isActiveAndEnabled && !_bonusRequestInFlight)
                RefreshBonusText();
        }

        // Follows the needle, so the button always shows what stopping now would pay.
        private void RefreshBonusText()
        {
            if (_rewardedCoinText == null || _rewardOffer == null)
                return;
            var multiplier = _bonusWheel != null ? _bonusWheel.CurrentMultiplier : 2;
            _rewardedCoinText.text = $"+{_rewardOffer.GetBonusCoins(multiplier)}";
        }

        private void KillBonusAdDelay()
        {
            _bonusAdDelayTween?.Kill();
            _bonusAdDelayTween = null;
        }

        private void SetFeatureProgress(FeatureMilestoneProgress progress)
        {
            KillFeatureProgress();
            if (_featureProgressRoot == null)
                return;

            var visible = progress != null;
            _featureProgressRoot.SetActive(visible);
            if (!visible)
                return;

            // The bar starts from the count before this win (0->1, 1->2) and fills up to it.
            var total = progress.Total;
            var current = Mathf.Clamp(progress.Current, 0, total);
            var previous = Mathf.Max(0, current - 1);
            if (current == previous)
            {
                SetFeatureProgressDisplay(current / (float)total, current, total);
            }
            else
            {
                SetFeatureProgressDisplay(previous / (float)total, previous, total);
                _featureProgressTween = DOVirtual.Float(
                        previous / (float)total,
                        current / (float)total,
                        _featureProgressFillDuration,
                        fill =>
                        {
                            SetFeatureFill(fill);
                        })
                    .SetDelay(_featureProgressDelay)
                    .SetEase(Ease.InOutSine)
                    .SetUpdate(true)
                    .OnComplete(() =>
                    {
                        _featureProgressTween = null;
                        SetFeatureProgressDisplay(current / (float)total, current, total);
                    });
            }

            var hasIcon = progress.Icon != null;
            if (_featureIcon != null)
            {
                _featureIcon.gameObject.SetActive(hasIcon);
                _featureIcon.sprite = progress.Icon;
            }
            if (_featureFallbackText != null)
            {
                _featureFallbackText.gameObject.SetActive(!hasIcon);
                _featureFallbackText.text = progress.FallbackLabel;
            }
        }

        private void SetFeatureProgressDisplay(float fill, int count, int total)
        {
            SetFeatureFill(fill);
            if (_featureProgressText != null)
                _featureProgressText.text = $"{count}/{total}";
        }

        private void SetFeatureFill(float fill)
        {
            if (_featureProgressFill == null) return;
            if (_featureProgressFill.type == Image.Type.Sliced)
            {
                var rect = _featureProgressFill.rectTransform;
                _featureProgressFill.enabled = fill > 0f;
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Max(48f, 256f * fill));
            }
            else
            {
                _featureProgressFill.fillAmount = fill;
            }
        }

        private void KillFeatureProgress()
        {
            _featureProgressTween?.Kill();
            _featureProgressTween = null;
        }

        public override void Show()
        {
            base.Show();
            PlayRewardAnimation();
            StopConfirmAnimation();
        }

        public override void Hide()
        {
            StopConfirmAnimation();
            StopRewardAnimation();
            KillBonusAdDelay();
            KillFeatureProgress();
            _tapAction = null;
            _rewardOffer = null;
            base.Hide();
        }

        private void Continue()
        {
            if (_bonusRequestInFlight)
                return;

            var action = _tapAction;
            _tapAction = null;
            action?.Invoke();
        }

        private void RequestBonusReward()
        {
            var request = _rewardOffer?.RequestBonus;
            // No tap action left: the player already continued and the coins are flying.
            if (_bonusRequestInFlight || _bonusRewardClaimed || request == null || _tapAction == null)
                return;

            _bonusRequestInFlight = true;
            CoinFlySource = _rewardedCoinButton.transform as RectTransform;
            SetRewardedCoinButtonInteractable(false);

            // The needle stops on the picked segment, then the ad plays after a short pause.
            var multiplier = _bonusWheel != null ? _bonusWheel.StopSwing() : 2;
            RefreshBonusText();
            KillBonusAdDelay();
            _bonusAdDelayTween = DOVirtual.DelayedCall(_bonusAdDelay, () =>
            {
                _bonusAdDelayTween = null;
                request(multiplier, OnBonusRewardFinished);
            }, true).SetTarget(this);
        }

        private void OnBonusRewardFinished(bool success)
        {
            if (this == null)
                return;

            _bonusRequestInFlight = false;
            _bonusRewardClaimed = success;
            if (!success)
                _bonusWheel?.ResumeSwing();
            if (_rewardedCoinButton == null)
            {
                if (success)
                    Continue();
                return;
            }

            SetRewardedCoinButtonInteractable(!success);
            // The button stays on screen: the bonus coins fly out of it before the popup closes.
            if (success)
                Continue();
        }

        private void SetRewardedCoinButtonInteractable(bool interactable)
        {
            if (_rewardedCoinButton == null)
                return;
            if (_rewardedCoinButton.TryGetComponent(out RewardedAdButton rewarded))
                rewarded.Interactable = interactable;
            else
                _rewardedCoinButton.interactable = interactable;
        }

        private void SetUnlockReward(int unlockedChipLevel, Material chipMaterial)
        {
            // var showReward =
            //     unlockedChipLevel >= 0 &&
            //     chipMaterial != null &&
            //     _unlockRewardRoot != null &&
            //     _unlockChipView != null;
            // if (_unlockRewardRoot != null)
            //     _unlockRewardRoot.SetActive(showReward);
            // if (_chipPreviewCamera != null)
            //     _chipPreviewCamera.enabled = showReward;
            // if (!showReward)
            // {
            //     StopRewardAnimation();
            //     return;
            // }


            // _unlockChipView.Initialize(token, new[] { chipMaterial });
            FramePreviewCamera();

            PlayRewardAnimation();
        }

        private void FramePreviewCamera()
        {
            // if (_chipPreviewCamera == null || _unlockChipView == null)
            //     return;
            //
            // var renderers = _unlockChipView.GetComponentsInChildren<Renderer>(true);
            // if (renderers.Length == 0)
            //     return;
            //
            // var bounds = renderers[0].bounds;
            // for (var i = 1; i < renderers.Length; i++)
            //     bounds.Encapsulate(renderers[i].bounds);
            //
            // var largestExtent = Mathf.Max(bounds.extents.x, bounds.extents.y);
            // _chipPreviewCamera.orthographicSize = Mathf.Max(0.01f, largestExtent * 1.45f);
            // _chipPreviewCamera.transform.SetPositionAndRotation(
            //     new Vector3(
            //         bounds.center.x,
            //         bounds.center.y,
            //         bounds.min.z - Mathf.Max(0.1f, bounds.size.z * 3f)),
            //     Quaternion.identity);
            // _chipPreviewCamera.nearClipPlane = 0.001f;
            // _chipPreviewCamera.farClipPlane = Mathf.Max(1f, bounds.size.z * 8f);
        }

        private void PlayRewardAnimation()
        {
            StopRewardAnimation();

            if (_rotatingGlow != null)
            {
                _rotatingGlow.localRotation = Quaternion.identity;
                _rotatingGlow
                    .DOLocalRotate(new Vector3(0f, 0f, -360f), _glowRotationDuration, RotateMode.FastBeyond360)
                    .SetEase(Ease.Linear)
                    .SetUpdate(true)
                    .SetLoops(-1);
            }

            if (_secondaryRotatingGlow != null)
            {
                _secondaryRotatingGlow.localRotation = Quaternion.identity;
                _secondaryRotatingGlow
                    .DOLocalRotate(new Vector3(0f, 0f, 360f), _glowRotationDuration, RotateMode.FastBeyond360)
                    .SetEase(Ease.Linear)
                    .SetUpdate(true)
                    .SetLoops(-1);
            }

            // if (_unlockChipPivot == null)
            //     return;
            //
            // _chipBaseScale = _unlockChipPivot.localScale;
            // _unlockChipPivot.localScale = Vector3.zero;
            // _unlockChipView.transform.localRotation = Quaternion.identity;
            // _unlockChipView.transform
                // .DOLocalRotate(
                //     new Vector3(0f, -360f, 0f),
                //     _chipRotationDuration,
                //     RotateMode.FastBeyond360)
                // .SetEase(Ease.Linear)
                // .SetLoops(-1, LoopType.Restart);
            // _rewardSequence = DOTween.Sequence()
            //     .Append(_unlockChipPivot.DOScale(_chipBaseScale, _chipPopDuration).SetEase(Ease.OutBack))
            //     .Append(_unlockChipPivot.DOPunchScale(
            //         _chipBaseScale * _chipPulseStrength,
            //         0.8f,
            //         4,
            //         0.4f))
            //     .SetLoops(-1, LoopType.Restart)
            //     .OnKill(() =>
            //     {
            //         // if (_unlockChipPivot != null)
            //         //     _unlockChipPivot.localScale = _chipBaseScale;
            //     });
        }

        private void PlayConfirmAnimation()
        {
            if (_confirmIndicator == null)
                return;

            StopConfirmAnimation();
            _confirmIndicator.alpha = 1f;
            DOTween.To(
                    () => _confirmIndicator.alpha,
                    value => _confirmIndicator.alpha = value,
                    _minimumFadeAlpha,
                    Mathf.Max(0.05f, _fadeDuration))
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetTarget(_confirmIndicator);
        }

        private void StopConfirmAnimation()
        {
            if (_confirmIndicator == null)
                return;

            DOTween.Kill(_confirmIndicator);
            _confirmIndicator.alpha = 1f;
        }

        private void StopRewardAnimation()
        {
            _rewardSequence?.Kill();
            _rewardSequence = null;
            _rotatingGlow?.DOKill();
            _secondaryRotatingGlow?.DOKill();
            // _unlockChipPivot?.DOKill();
            // _unlockChipView?.transform.DOKill();
        }

        private void OnDisable() => StopRewardAnimation();

        private void OnDestroy()
        {
            _rewardedCoinButton?.onClick.RemoveListener(RequestBonusReward);
            _continueButton?.onClick.RemoveListener(Continue);
            KillBonusAdDelay();
            KillFeatureProgress();
            StopConfirmAnimation();
            StopRewardAnimation();
        }
    }
}
