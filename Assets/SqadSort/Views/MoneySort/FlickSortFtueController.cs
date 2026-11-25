using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Views
{
    public sealed class FlickSortFtueController : MonoBehaviour
    {
        [SerializeField] private RectTransform externalBubble;
        [SerializeField] private Image externalBubbleImage;
        [SerializeField] private TextMeshProUGUI externalLabel;
        [SerializeField] private Sprite tapBubble;
        [SerializeField] private Sprite goalBubble;

        public void ShowExternal(Rect screenRect, string text)
        {
            CancelTutorial();
            bool hasText = !string.IsNullOrEmpty(text);
            externalBubble.gameObject.SetActive(hasText);   // a step with no text (GDD §2.2 "Group") lights the target only
            externalLabel.text = text ?? string.Empty;
            bool pointsAtStack = screenRect.width > 0f && screenRect.height > 0f;
            var sprite = pointsAtStack ? tapBubble : goalBubble;
            externalBubbleImage.sprite = sprite;

            var parent = (RectTransform)externalBubble.parent;
            var bounds = parent.rect;
            float scale = Mathf.Min(bounds.width / 720f, 1.5f);
            Vector2 size = sprite.rect.size * scale;
            externalLabel.fontSizeMax = 34f * scale;
            externalLabel.fontSizeMin = 23f * scale;
            Vector2 position = new Vector2(bounds.center.x, bounds.yMin + 309f * scale);
            // under the target, not over it: the bubble's tail points up at the tube's mouth and everyone inside stays visible
            if (pointsAtStack && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parent, new Vector2(screenRect.center.x, screenRect.yMin), UiCamera, out var target))
            {
                position.x = target.x;
                position.y = target.y - targetPadding - size.y * .5f - 18f * scale;
            }
            if (pointsAtStack) ShowExternalHole(screenRect);
            else SetVisualsVisible(false);   // the goal bubble lights nothing
            position.x = Mathf.Clamp(position.x, bounds.xMin + size.x * .5f + 12f, bounds.xMax - size.x * .5f - 12f);
            position.y = Mathf.Clamp(position.y, bounds.yMin + size.y * .5f + 12f, bounds.yMax - size.y * .5f - 12f);
            SetRect(externalBubble, position - size * .5f, position + size * .5f);
            var label = externalLabel.rectTransform;
            label.offsetMin = new Vector2(18f, 4f) * scale;
            label.offsetMax = new Vector2(-18f, pointsAtStack ? -28f : -4f) * scale;
        }

        /// <summary>The FTUE spotlight for <see cref="ShowExternal"/>: the same shades, white frame and pulsing hand as the
        /// guided tutorial, around one screen rect. The shades take the taps outside the hole; the frame and hand take none.</summary>
        private void ShowExternalHole(Rect screenRect)
        {
            if (visualRoot == null || highlight == null) return;
            transform.SetAsLastSibling();
            var rootRect = visualRoot.rect;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(visualRoot, screenRect.min, UiCamera, out var localMin);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(visualRoot, screenRect.max, UiCamera, out var localMax);
            localMin -= Vector2.one * targetPadding;
            localMax += Vector2.one * targetPadding;
            localMin = new Vector2(Mathf.Clamp(localMin.x, rootRect.xMin, rootRect.xMax), Mathf.Clamp(localMin.y, rootRect.yMin, rootRect.yMax));
            localMax = new Vector2(Mathf.Clamp(localMax.x, rootRect.xMin, rootRect.xMax), Mathf.Clamp(localMax.y, rootRect.yMin, rootRect.yMax));

            SetExtraTargetVisualsVisible(false);
            SetRect(highlight, localMin, localMax);
            SetRect(leftShade, new Vector2(rootRect.xMin, rootRect.yMin), new Vector2(localMin.x, rootRect.yMax));
            SetRect(rightShade, new Vector2(localMax.x, rootRect.yMin), new Vector2(rootRect.xMax, rootRect.yMax));
            SetRect(bottomShade, new Vector2(localMin.x, rootRect.yMin), new Vector2(localMax.x, localMin.y));
            SetRect(topShade, new Vector2(localMin.x, localMax.y), new Vector2(localMax.x, rootRect.yMax));
            foreach (var shade in new[] { leftShade, rightShade, bottomShade, topShade }) SetRaycastTarget(shade, true);
            SetRaycastTargets(highlight, false);
            SetRaycastTargets(hand, false);
            if (spineHand != null) SetRaycastTargets(spineHand, false);
            if (instructionLabel != null) instructionLabel.gameObject.SetActive(false);   // the external bubble carries the text

            // the fingertip on the lower part of the hole, near the mouth, so the hand covers none of the buddies
            var tip = new Vector2((localMin.x + localMax.x) * .5f, Mathf.Lerp(localMin.y, localMax.y, .2f));
            if (spineHand != null)
            {
                hand.gameObject.SetActive(false);
                TutorialHand.Place(spineHand, rootRect, tip, spineHandSize);
            }
            else hand.anchoredPosition = tip + Vector2.one * handOffset * .5f;
            SetVisualsVisible(true);
            StartHandAnimation();
        }

        [SerializeField] private MoneySortTutorialTargets board;
        [SerializeField] private RectTransform visualRoot;
        [SerializeField] private RectTransform leftShade;
        [SerializeField] private RectTransform rightShade;
        [SerializeField] private RectTransform topShade;
        [SerializeField] private RectTransform bottomShade;
        [SerializeField] private RectTransform highlight;
        [SerializeField] private RectTransform hand;
        // Shared tutorial pointer (PointHand sprite); field name retained for prefab compatibility.
        [SerializeField] private RectTransform spineHand;
        [SerializeField, Range(0.05f, 0.4f)] private float spineHandSize = 0.17f;
        [SerializeField] private TextMeshProUGUI instructionLabel;
        [SerializeField] private RectTransform dealButtonTarget;
        [SerializeField, Min(0f)] private float targetPadding = 14f;
        [SerializeField, Min(0f)] private float handOffset = 42f;
        [SerializeField, Min(0.1f)] private float handPulseDuration = 0.65f;

        private Camera UiCamera => transform.parent != null ? transform.parent.GetComponentInParent<Canvas>()?.worldCamera : null;
        private MoneySortTutorialLayout _layout;
        private int _moveIndex;
        private bool _waitingForDestination;
        private bool _moveInProgress;
        private bool _ready;
        private bool _running;
        private bool _showingDeal;
        private Tween _handTween;
        private RectTransform _secondBottomShade;
        private RectTransform _secondTopShade;
        private RectTransform _middleGapShade;
        private RectTransform _secondHighlight;
        private LoadingUI _loadingUi;
        private bool _waitingForLoading;
        private RectTransform _inputBlocker;

        public void PrepareLevel(int levelNumber, MoneySortTutorialLayout layout)
        {
            // Gameplay screens are instantiated at runtime, so keep the tutorial overlay above them.
            transform.SetAsLastSibling();
            RemoveLegacySeparator();
            ConfigureInputBlocking();
            if (dealButtonTarget == null)
            {
                var gameplayUi = FindFirstObjectByType<GameplayUI>(FindObjectsInactive.Include);
                dealButtonTarget = gameplayUi != null ? gameplayUi.DealButtonRect : null;
            }
            _loadingUi = FindFirstObjectByType<LoadingUI>(FindObjectsInactive.Include);
            StopHandAnimation();
            _layout = layout;
            _moveIndex = 0;
            _waitingForDestination = false;
            _moveInProgress = false;
            _ready = false;
            _waitingForLoading = false;
            _showingDeal = false;
            _running = layout != null && layout.levelNumber == levelNumber &&
                       (layout.tutorialMoves.Count > 0 || layout.finishWithDeal);
            SetVisualsVisible(false);
        }

        public void NotifyInitialDealCompleted(int levelNumber)
        {
            if (!_running || _layout == null || _layout.levelNumber != levelNumber)
                return;
            _ready = true;
            // A restored checkpoint can finish synchronously inside board.Init(),
            // before FlickSortBootstrap.Start() has activated LoadingUI. Treat the
            // existence of LoadingUI as an upcoming/current loading cycle and let
            // LateUpdate release the FTUE only after that UI is inactive again.
            _waitingForLoading = _loadingUi != null;
            if (!_waitingForLoading)
                ShowCurrentInstruction();
        }

        public bool AllowsStackTap(int levelNumber, int stackIndex)
        {
            if (!_running || _layout == null || _layout.levelNumber != levelNumber)
                return true;
            if (!_ready || _waitingForLoading || _moveInProgress || _showingDeal ||
                _moveIndex >= _layout.tutorialMoves.Count)
                return false;

            var step = _layout.tutorialMoves[_moveIndex];
            return stackIndex == (_waitingForDestination
                ? step.destinationStackIndex
                : step.sourceStackIndex);
        }

        public bool AllowsDeal(int levelNumber)
        {
            if (!_running || _layout == null || _layout.levelNumber != levelNumber)
                return true;
            return _ready && !_waitingForLoading && _showingDeal;
        }

        public void NotifyStackSelected(int sourceStackIndex)
        {
            if (!_running || !_ready || _waitingForLoading ||
                _moveIndex >= _layout.tutorialMoves.Count ||
                _layout.tutorialMoves[_moveIndex].sourceStackIndex != sourceStackIndex)
                return;
            _waitingForDestination = true;
            ShowCurrentInstruction();
        }

        public void NotifyMoveStarted(int sourceStackIndex, int destinationStackIndex)
        {
            if (!_running || !_ready || _moveIndex >= _layout.tutorialMoves.Count)
                return;

            var step = _layout.tutorialMoves[_moveIndex];
            if (step.sourceStackIndex != sourceStackIndex ||
                step.destinationStackIndex != destinationStackIndex)
                return;
            _moveInProgress = true;
            SetVisualsVisible(false);
        }

        public void NotifyMoveCompleted(int sourceStackIndex, int destinationStackIndex)
        {
            if (!_running || !_moveInProgress || _moveIndex >= _layout.tutorialMoves.Count)
                return;

            var step = _layout.tutorialMoves[_moveIndex];
            if (step.sourceStackIndex != sourceStackIndex ||
                step.destinationStackIndex != destinationStackIndex)
                return;
            _moveIndex++;
            _waitingForDestination = false;
            _moveInProgress = false;
            ShowCurrentInstruction();
        }

        public void NotifyDealStarted(int levelNumber)
        {
            if (_running && _layout != null && _layout.levelNumber == levelNumber &&
                _showingDeal)
                FinishTutorial();
        }

        public void CancelTutorial()
        {
            if (externalBubble != null) externalBubble.gameObject.SetActive(false);
            _running = false;
            _ready = false;
            _showingDeal = false;
            StopHandAnimation();
            SetVisualsVisible(false);
        }

        private void LateUpdate()
        {
            if (_running && _ready && _waitingForLoading &&
                (_loadingUi == null || !_loadingUi.gameObject.activeInHierarchy))
            {
                _waitingForLoading = false;
                ShowCurrentInstruction();
            }

            if (_running && _ready && visualRoot != null && visualRoot.gameObject.activeSelf)
                UpdateTargetVisuals();
        }

        private void ShowCurrentInstruction()
        {
            if (!_running || !_ready)
                return;

            if (_moveIndex < _layout.tutorialMoves.Count)
            {
                _showingDeal = false;
                instructionLabel.text = _waitingForDestination ? "Tap To Move" : "Tap To Select";
            }
            else if (_layout.finishWithDeal)
            {
                _showingDeal = true;
                instructionLabel.text = "Tap To Deal";
            }
            else
            {
                FinishTutorial();
                return;
            }

            if (instructionLabel != null) instructionLabel.gameObject.SetActive(true);   // ShowExternalHole hides it
            SetVisualsVisible(true);
            UpdateTargetVisuals();
            StartHandAnimation();
        }

        private void UpdateTargetVisuals()
        {
            if (!TryGetTargetScreenRect(out var screenMin, out var screenMax))
                return;

            var rootRect = visualRoot.rect;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                visualRoot, screenMin, UiCamera, out var localMin);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                visualRoot, screenMax, UiCamera, out var localMax);
            localMin -= Vector2.one * targetPadding;
            localMax += Vector2.one * targetPadding;
            localMin.x = Mathf.Clamp(localMin.x, rootRect.xMin, rootRect.xMax);
            localMin.y = Mathf.Clamp(localMin.y, rootRect.yMin, rootRect.yMax);
            localMax.x = Mathf.Clamp(localMax.x, rootRect.xMin, rootRect.xMax);
            localMax.y = Mathf.Clamp(localMax.y, rootRect.yMin, rootRect.yMax);

            if (TryGetSelectedSourceLocalRect(
                    rootRect,
                    out var sourceMin,
                    out var sourceMax))
            {
                UpdateDualTargetVisuals(rootRect, sourceMin, sourceMax, localMin, localMax);
            }
            else
            {
                SetExtraTargetVisualsVisible(false);
                SetRect(highlight, localMin, localMax);
                SetRect(leftShade,
                    new Vector2(rootRect.xMin, rootRect.yMin),
                    new Vector2(localMin.x, rootRect.yMax));
                SetRect(rightShade,
                    new Vector2(localMax.x, rootRect.yMin),
                    new Vector2(rootRect.xMax, rootRect.yMax));
                SetRect(bottomShade,
                    new Vector2(localMin.x, rootRect.yMin),
                    new Vector2(localMax.x, localMin.y));
                SetRect(topShade,
                    new Vector2(localMin.x, localMax.y),
                    new Vector2(localMax.x, rootRect.yMax));
            }

            hand.anchoredPosition = new Vector2(
                localMax.x + handOffset * 0.5f,
                localMin.y + handOffset * 0.5f);
            if (spineHand != null)
            {
                hand.gameObject.SetActive(false);
                TutorialHand.Place(spineHand, rootRect, (localMin + localMax) / 2f, spineHandSize);
            }
            instructionLabel.rectTransform.anchoredPosition = new Vector2(
                0f,
                Mathf.Min(rootRect.yMax - 70f, localMax.y + 80f));
        }

        private bool TryGetTargetScreenRect(out Vector2 min, out Vector2 max)
        {
            min = max = default;
            if (_showingDeal)
            {
                if (dealButtonTarget == null)
                    return false;
                var corners = new Vector3[4];
                dealButtonTarget.GetWorldCorners(corners);
                min = RectTransformUtility.WorldToScreenPoint(UiCamera, corners[0]);
                max = RectTransformUtility.WorldToScreenPoint(UiCamera, corners[2]);
                return true;
            }

            var step = _layout.tutorialMoves[_moveIndex];
            var stackIndex = _waitingForDestination
                ? step.destinationStackIndex
                : step.sourceStackIndex;
            if (board == null)
                return false;

            // Keep the tutorial hole tight around the money instead of framing
            // the full slot. Empty destinations still need the slot as a target.
            return board.TryGetStackChipScreenRect(stackIndex, out min, out max) ||
                   board.TryGetStackScreenRect(stackIndex, out min, out max);
        }

        private bool TryGetSelectedSourceLocalRect(
            Rect rootRect,
            out Vector2 localMin,
            out Vector2 localMax)
        {
            localMin = localMax = default;
            if (!_waitingForDestination || _showingDeal || board == null ||
                _moveIndex >= _layout.tutorialMoves.Count)
                return false;

            var sourceIndex = _layout.tutorialMoves[_moveIndex].sourceStackIndex;
            if (!board.TryGetStackChipScreenRect(sourceIndex, out var screenMin, out var screenMax))
                return false;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                visualRoot, screenMin, UiCamera, out localMin);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                visualRoot, screenMax, UiCamera, out localMax);
            localMin -= Vector2.one * targetPadding;
            localMax += Vector2.one * targetPadding;
            localMin.x = Mathf.Clamp(localMin.x, rootRect.xMin, rootRect.xMax);
            localMin.y = Mathf.Clamp(localMin.y, rootRect.yMin, rootRect.yMax);
            localMax.x = Mathf.Clamp(localMax.x, rootRect.xMin, rootRect.xMax);
            localMax.y = Mathf.Clamp(localMax.y, rootRect.yMin, rootRect.yMax);
            return true;
        }

        private void UpdateDualTargetVisuals(
            Rect rootRect,
            Vector2 sourceMin,
            Vector2 sourceMax,
            Vector2 targetMin,
            Vector2 targetMax)
        {
            EnsureExtraTargetVisuals();
            // The selected source remains unobscured, but only the destination
            // receives the white tutorial frame, matching the FTUE reference.
            SetRect(highlight, targetMin, targetMax);
            _secondHighlight.gameObject.SetActive(false);

            var firstMin = sourceMin;
            var firstMax = sourceMax;
            var secondMin = targetMin;
            var secondMax = targetMax;
            if (firstMin.x > secondMin.x)
            {
                (firstMin, secondMin) = (secondMin, firstMin);
                (firstMax, secondMax) = (secondMax, firstMax);
            }

            var gapStartX = firstMax.x;
            var gapEndX = secondMin.x;
            var gapWidth = gapEndX - gapStartX;
            var hasRealSlotGap = gapWidth >= Mathf.Max(24f, targetPadding * 2f);
            if (!hasRealSlotGap)
            {
                // Padded adjacent bounds can overlap. Give both shade groups one
                // exact shared edge so their alpha never stacks into a dark seam.
                var sharedBoundaryX = (gapStartX + gapEndX) * 0.5f;
                firstMax.x = sharedBoundaryX;
                secondMin.x = sharedBoundaryX;
            }

            SetExtraShadeVisualsVisible(true);
            SetRect(leftShade,
                new Vector2(rootRect.xMin, rootRect.yMin),
                new Vector2(firstMin.x, rootRect.yMax));
            SetRect(rightShade,
                new Vector2(secondMax.x, rootRect.yMin),
                new Vector2(rootRect.xMax, rootRect.yMax));
            SetRect(bottomShade,
                new Vector2(firstMin.x, rootRect.yMin),
                new Vector2(firstMax.x, firstMin.y));
            SetRect(topShade,
                new Vector2(firstMin.x, firstMax.y),
                new Vector2(firstMax.x, rootRect.yMax));
            SetRect(_secondBottomShade,
                new Vector2(secondMin.x, rootRect.yMin),
                new Vector2(secondMax.x, secondMin.y));
            SetRect(_secondTopShade,
                new Vector2(secondMin.x, secondMax.y),
                new Vector2(secondMax.x, rootRect.yMax));
            _middleGapShade.gameObject.SetActive(hasRealSlotGap);
            if (hasRealSlotGap)
            {
                SetRect(_middleGapShade,
                    new Vector2(gapStartX, rootRect.yMin),
                    new Vector2(gapEndX, rootRect.yMax));
            }
        }

        private void EnsureExtraTargetVisuals()
        {
            RemoveLegacySeparator();
            if (_secondHighlight != null)
                return;

            _secondBottomShade = Instantiate(bottomShade, visualRoot);
            _secondBottomShade.name = "Second Bottom Shade";
            _secondTopShade = Instantiate(topShade, visualRoot);
            _secondTopShade.name = "Second Top Shade";
            _middleGapShade = Instantiate(leftShade, visualRoot);
            _middleGapShade.name = "Middle Gap Shade";
            _secondHighlight = Instantiate(highlight, visualRoot);
            _secondHighlight.name = "Second Highlight";

            // Shades swallow UI taps outside the target hole; the board gates
            // stack taps through AllowsStackTap.
            SetRaycastTarget(_secondBottomShade, true);
            SetRaycastTarget(_secondTopShade, true);
            SetRaycastTarget(_middleGapShade, true);
            SetRaycastTargets(_secondHighlight, false);
            hand.SetAsLastSibling();
            if (spineHand != null) spineHand.SetAsLastSibling();
            instructionLabel.rectTransform.SetAsLastSibling();
        }

        private void RemoveLegacySeparator()
        {
            if (visualRoot == null)
                return;

            var legacySeparator = visualRoot.Find("Between Targets Shade");
            if (legacySeparator != null)
                Destroy(legacySeparator.gameObject);
        }

        private void SetExtraTargetVisualsVisible(bool visible)
        {
            if (_secondHighlight == null)
                return;
            SetExtraShadeVisualsVisible(visible);
            _secondHighlight.gameObject.SetActive(visible);
        }

        private void SetExtraShadeVisualsVisible(bool visible)
        {
            _secondBottomShade.gameObject.SetActive(visible);
            _secondTopShade.gameObject.SetActive(visible);
            _middleGapShade.gameObject.SetActive(visible);
        }

        private static void SetRaycastTarget(RectTransform target, bool value)
        {
            var graphic = target != null ? target.GetComponent<Graphic>() : null;
            if (graphic != null)
                graphic.raycastTarget = value;
        }

        private static void SetRaycastTargets(RectTransform target, bool value)
        {
            if (target == null)
                return;
            foreach (var graphic in target.GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = value;
        }

        private void ConfigureInputBlocking()
        {
            // Only the target hole may receive taps; everything else is covered.
            SetRaycastTarget(leftShade, true);
            SetRaycastTarget(rightShade, true);
            SetRaycastTarget(topShade, true);
            SetRaycastTarget(bottomShade, true);
            SetRaycastTarget(_secondBottomShade, true);
            SetRaycastTarget(_secondTopShade, true);
            SetRaycastTarget(_middleGapShade, true);
            SetRaycastTargets(highlight, false);
            SetRaycastTargets(_secondHighlight, false);
            SetRaycastTargets(hand, false);
            if (instructionLabel != null)
                instructionLabel.raycastTarget = false;

            if (_inputBlocker != null)
                return;

            // Covers the whole screen while the guide is running but its
            // visuals are hidden (initial deal, loading, move animations).
            var blocker = new GameObject("FTUE Input Blocker", typeof(RectTransform), typeof(Image));
            blocker.layer = gameObject.layer;
            _inputBlocker = (RectTransform)blocker.transform;
            _inputBlocker.SetParent(transform, false);
            _inputBlocker.SetAsFirstSibling();
            _inputBlocker.anchorMin = Vector2.zero;
            _inputBlocker.anchorMax = Vector2.one;
            _inputBlocker.offsetMin = Vector2.zero;
            _inputBlocker.offsetMax = Vector2.zero;
            var image = blocker.GetComponent<Image>();
            image.color = Color.clear;
            image.raycastTarget = true;
            blocker.SetActive(false);
        }

        private void RefreshInputBlocker()
        {
            if (_inputBlocker == null)
                return;
            var visualsShown = visualRoot != null && visualRoot.gameObject.activeSelf;
            _inputBlocker.gameObject.SetActive(_running && !visualsShown);
        }

        private static void SetRect(RectTransform target, Vector2 min, Vector2 max)
        {
            target.anchorMin = target.anchorMax = new Vector2(0.5f, 0.5f);
            target.pivot = new Vector2(0.5f, 0.5f);
            target.anchoredPosition = (min + max) * 0.5f;
            target.sizeDelta = new Vector2(
                Mathf.Max(0f, max.x - min.x),
                Mathf.Max(0f, max.y - min.y));
        }

        private void SetVisualsVisible(bool visible)
        {
            if (visualRoot != null)
                visualRoot.gameObject.SetActive(visible);
            RefreshInputBlocker();
        }

        private void StartHandAnimation()
        {
            StopHandAnimation();
            hand.localScale = Vector3.one;
            _handTween = hand.DOScale(0.88f, handPulseDuration)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        private void StopHandAnimation()
        {
            _handTween?.Kill();
            _handTween = null;
        }

        private void FinishTutorial()
        {
            _running = false;
            _showingDeal = false;
            StopHandAnimation();
            SetVisualsVisible(false);
        }

        private void OnDisable() => StopHandAnimation();
    }
}
