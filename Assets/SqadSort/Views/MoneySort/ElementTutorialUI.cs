using System;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace Game.Views
{
    /// <summary>
    /// The tutorial that follows a new element's unlock popup (mock-ups Popup-Element2 1 / 2 / 21): the board dimmed but for
    /// one rounded spot on the element — the locked chip, the hidden stack, the hidden pole's column — and the hint on the
    /// framed panel low on the screen. It never takes a tap: the player does what it says, and the controller hides it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ElementTutorialUI : UIBase
    {
        [SerializeField] private SpotlightScrim _scrim;
        [SerializeField] private TextMeshProUGUI _label;
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private RectTransform _panel;
        // The PointHand sprite is a child of the scrim, so it shares the hole's space;
        // its fingertip lands on the centre of the spot
        [SerializeField] private RectTransform _hand;
        [SerializeField] private RectTransform _animatedHand;
        [SerializeField, Range(0f, 0.05f)] private float _handPullDistance = 0.035f;
        [SerializeField, Range(1f, 1.15f)] private float _handRaisedScale = 1.04f;
        private Spine.Unity.SkeletonGraphic _handAnimation;
        private Vector3 _handContactPosition, _handContactScale;
        private enum HandPhase { Approach, Click, Lift, MoveOut, Pause }
        private HandPhase _handPhase;
        private float _handPhaseAt;
        private bool _handTapReady;
        private bool _useAnimatedHand;
        private RectTransform ActiveHand => _useAnimatedHand && _animatedHand != null ? _animatedHand : _hand;
        [SerializeField, Range(0.05f, 0.4f)] private float _handSize = 0.17f;   // of the screen's width
        [SerializeField, Min(0f)] private float _padding = 4f;   // Ethan 11-11: the spot hugs the stack (was 18)
        [SerializeField, Min(0f)] private float _panelGap = 16f;
        [Tooltip("Where the panel goes when it cannot stay low or sit under the spot: its centre this far below the top of the screen, over the poles (Ethan 11-11).")]
        [SerializeField, Min(0f)] private float _topBandOffset = 280f;

        [Header("Booster guide presentation")]
        [SerializeField, Range(0.5f, 1f)] private float _guidedPanelScale = 0.92f;
        [SerializeField, Min(0f)] private float _guidedTopOffset = 340f;

        private Tween _fade;
        private float _panelY = float.NaN;   // the authored height of the panel, kept while the spot does not reach it

        public override void Init(object manager)
        {
            base.Init(manager);
            if (_scrim != null)
            {
                _scrim.color = DesignTokens.TutorialScrim;
                _scrim.SpotColor = DesignTokens.TutorialSpot;
            }
            if (_group != null) { _group.blocksRaycasts = false; _group.interactable = false; }
        }

        /// <param name="target">The element, as a world-space XY rectangle (board units, the rig ruler: one per canvas unit).</param>
        /// <param name="alsoClear">Another world-space rectangle the panel must not cover (the hidden stack the lit stack sits
        /// on); empty = only the spot.</param>
        public void SetTarget(Rect target, string hint, Rect alsoClear = default)
        {
            if (_label != null) _label.text = hint ?? string.Empty;
            if (_scrim == null) return;
            if (target.width <= 0f || target.height <= 0f) { _scrim.SetHole(default); if (ActiveHand != null) ActiveHand.gameObject.SetActive(false); return; }
            var space = _scrim.rectTransform;
            Vector2 min = space.InverseTransformPoint(new Vector3(target.xMin, target.yMin, space.position.z));
            Vector2 max = space.InverseTransformPoint(new Vector3(target.xMax, target.yMax, space.position.z));
            var hole = Rect.MinMaxRect(Mathf.Min(min.x, max.x) - _padding, Mathf.Min(min.y, max.y) - _padding,
                Mathf.Max(min.x, max.x) + _padding, Mathf.Max(min.y, max.y) + _padding);
            _scrim.SetHole(hole);
            var keep = hole;
            if (ActiveHand != null)
            {
                var h = TutorialHand.Place(ActiveHand, space.rect, hole.center, _handSize);
                keep = Rect.MinMaxRect(Mathf.Min(keep.xMin, h.xMin), Mathf.Min(keep.yMin, h.yMin), Mathf.Max(keep.xMax, h.xMax), Mathf.Max(keep.yMax, h.yMax));
            }
            if (alsoClear.width > 0f && alsoClear.height > 0f)
            {
                Vector2 a = space.InverseTransformPoint(new Vector3(alsoClear.xMin, alsoClear.yMin, space.position.z));
                Vector2 b = space.InverseTransformPoint(new Vector3(alsoClear.xMax, alsoClear.yMax, space.position.z));
                keep = Rect.MinMaxRect(Mathf.Min(keep.xMin, a.x, b.x), Mathf.Min(keep.yMin, a.y, b.y),
                    Mathf.Max(keep.xMax, a.x, b.x), Mathf.Max(keep.yMax, a.y, b.y));
            }
            PlacePanel(space.rect, keep);
        }

        /// <summary>Hidden-pole guide: the hand points at the chip stack while a second spot lights its pole column.</summary>
        public void SetPairedTargets(Rect stack, Rect pole, string hint)
        {
            SetTarget(stack, hint, pole);
            if (_scrim == null || stack.width <= 0f || pole.width <= 0f) return;
            var space = _scrim.rectTransform;
            Rect Local(Rect r)
            {
                Vector2 a = space.InverseTransformPoint(new Vector3(r.xMin, r.yMin, space.position.z));
                Vector2 b = space.InverseTransformPoint(new Vector3(r.xMax, r.yMax, space.position.z));
                return Rect.MinMaxRect(Mathf.Min(a.x,b.x)-_padding, Mathf.Min(a.y,b.y)-_padding,
                    Mathf.Max(a.x,b.x)+_padding, Mathf.Max(a.y,b.y)+_padding);
            }
            var stackSpot = Local(stack);
            _scrim.SetHoles(stackSpot, Local(pole));
            if (ActiveHand != null) TutorialHand.Place(ActiveHand, space.rect,
                new Vector2(stackSpot.center.x, stackSpot.yMax - 10f), _handSize);
            if (_panel != null) _panel.anchoredPosition = new Vector2(_panel.anchoredPosition.x,
                Mathf.Max(_panel.anchoredPosition.y, _panel.rect.height / 2f + space.rect.height * 0.065f));
        }

        /// <summary>Locked chip: first point at the source stack, then watch the pole and ice without a panel or hand.</summary>
        public void SetLockedChipTargets(Rect target, Rect lockedChip, string hint, bool watching)
        {
            if (_animatedHand != null)
            {
                _useAnimatedHand = true;
                if (_hand != null) _hand.gameObject.SetActive(false);
            }
            _handTapReady = false;
            SetPairedTargets(target, lockedChip, hint);
            if (_panel != null)
            {
                _panel.gameObject.SetActive(!watching);
                if (!watching && _scrim != null)
                    _panel.anchoredPosition = new Vector2(_panel.anchoredPosition.x,
                        _scrim.rectTransform.rect.height - _topBandOffset);
            }
            if (ActiveHand != null) ActiveHand.gameObject.SetActive(!watching);
            if (!watching && _useAnimatedHand && _animatedHand != null)
            {
                _handAnimation = _animatedHand.GetComponent<Spine.Unity.SkeletonGraphic>();
                _handContactPosition = _animatedHand.localPosition;
                _handContactScale = _animatedHand.localScale;
                if (_handAnimation != null)
                {
                    _handAnimation.Initialize(false);
                    _handAnimation.AnimationState.SetAnimation(0, "idle", true).MixDuration = 0f;
                    BeginHandPhase(HandPhase.Approach);
                    SetHandPose(1f, 1f);
                    _handTapReady = true;
                }
            }
            if (_scrim != null) _scrim.BlockOutsideHole(true);
            if (_group != null) { _group.blocksRaycasts = true; _group.interactable = true; }
        }
        /// <summary>Guided input: the spotlight alone passes taps; the existing element tutorials remain non-blocking.</summary>
        public void SetGuidedTarget(Rect target, string hint, bool topPanel, bool pointDown = false)
        {
            SetTarget(target, hint);
            if (_hand != null && _scrim != null && target.width > 0f && target.height > 0f)
            {
                var space = _scrim.rectTransform;
                Vector2 tip = space.InverseTransformPoint(new Vector3(target.center.x, pointDown ? target.yMax + _padding : target.center.y, space.position.z));
                TutorialHand.Place(ActiveHand, space.rect, tip, DesignTokens.BoosterGuideHandSize, pointDown ? 135f : 0f);
            }
            if (_panel != null) _panel.localScale = Vector3.one * _guidedPanelScale;
            if (_scrim != null) _scrim.BlockOutsideHole(true);
            if (_group != null) { _group.blocksRaycasts = true; _group.interactable = true; }
            if (topPanel && _panel != null && _scrim != null)
                _panel.anchoredPosition = new Vector2(_panel.anchoredPosition.x, _scrim.rectTransform.rect.height - _guidedTopOffset);
        }

        private void BeginHandPhase(HandPhase phase)
        {
            _handPhase = phase;
            _handPhaseAt = Time.unscaledTime;
        }

        private void SetHandPose(float away, float raised)
        {
            Vector3 direction = (_animatedHand.localRotation * new Vector3(1f, -1f, 0f)).normalized;
            _animatedHand.localPosition = _handContactPosition + direction * (_scrim.rectTransform.rect.width * _handPullDistance * away);
            _animatedHand.localScale = _handContactScale * Mathf.Lerp(1f, _handRaisedScale, raised);
        }

        private void LateUpdate()
        {
            if (!_handTapReady || _animatedHand == null || !_animatedHand.gameObject.activeInHierarchy) return;
            float elapsed = Time.unscaledTime - _handPhaseAt;
            switch (_handPhase)
            {
                case HandPhase.Approach:
                    float approach = Mathf.SmoothStep(0f, 1f, elapsed / 0.3f);
                    SetHandPose(1f - approach, 1f - approach);
                    if (elapsed < 0.3f) break;
                    // Skip the authored first half (the hand waiting above the chip).
                    // Contact should press immediately, with no extra hold before bending.
                    var press = _handAnimation.AnimationState.SetAnimation(0, "click", false);
                    press.MixDuration = 0f;
                    press.TrackTime = Mathf.Min(press.Animation.Duration, press.Animation.Duration * 0.5f + 0.0001f);
                    _handAnimation.AnimationState.Apply(_handAnimation.Skeleton);
                    BeginHandPhase(HandPhase.Click);
                    break;
                case HandPhase.Click:
                    // Stay exactly on the chip for the entire press/release clip. Moving this
                    // pose while the finger is down reads as click-and-drag.
                    SetHandPose(0f, 0f);
                    var track = _handAnimation.AnimationState.GetCurrent(0);
                    if (track == null || !track.IsComplete) break;
                    _handAnimation.AnimationState.SetAnimation(0, "idle", true).MixDuration = 0f;
                    BeginHandPhase(HandPhase.Lift);
                    break;
                case HandPhase.Lift:
                    SetHandPose(0f, Mathf.SmoothStep(0f, 1f, elapsed / 0.16f));
                    if (elapsed >= 0.16f) BeginHandPhase(HandPhase.MoveOut);
                    break;
                case HandPhase.MoveOut:
                    SetHandPose(Mathf.SmoothStep(0f, 1f, elapsed / 0.3f), 1f);
                    if (elapsed >= 0.3f) BeginHandPhase(HandPhase.Pause);
                    break;
                case HandPhase.Pause:
                    SetHandPose(1f, 1f);
                    if (elapsed >= 0.12f) BeginHandPhase(HandPhase.Approach);
                    break;
            }
        }
        public void ReleaseInput()
        {
            if (_scrim != null) _scrim.BlockOutsideHole(false);
            if (_group != null) _group.blocksRaycasts = false;
        }

        // Ethan 11-11 (wireframe Popup-Element2): the hint never covers what it points at, nor the board around it. In order:
        // its authored place low on the screen; just under the spot, never lower than that place (the booster bar stays
        // clear); up over the poles; only then just above the spot, as before.
        private void PlacePanel(Rect bounds, Rect hole)
        {
            if (_panel == null) return;
            if (float.IsNaN(_panelY)) _panelY = _panel.anchoredPosition.y;
            float half = _panel.rect.height / 2f, low = bounds.yMin + _panelY;
            bool Clear(float c) => hole.yMin >= c + half || hole.yMax <= c - half;
            float centre = low;
            if (!Clear(low))
            {
                float below = hole.yMin - _panelGap - half, top = bounds.yMax - _topBandOffset;
                if (below >= low) centre = below;
                else if (Clear(top) && top + half <= bounds.yMax) centre = top;
                else
                {
                    float above = hole.yMax + _panelGap + half;
                    centre = above + half <= bounds.yMax ? above : below;
                }
            }
            _panel.anchoredPosition = new Vector2(_panel.anchoredPosition.x, centre - bounds.yMin);
        }

        public override void Show()
        {
            base.Show();
            Fade(1f, null);
        }

        public override void Hide()
        {
            if (!gameObject.activeInHierarchy) { base.Hide(); return; }
            Fade(0f, () => base.Hide());
        }

        private void Fade(float to, Action done)
        {
            _fade?.Kill();
            if (_group == null) { done?.Invoke(); return; }
            if (to > 0f) _group.alpha = 0f;
            _fade = _group.DOFade(to, DesignTokens.Motion.Reveal).SetUpdate(true).OnComplete(() => { _fade = null; done?.Invoke(); });
        }

        private void OnDestroy() => _fade?.Kill();
    }
}
