using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Views
{
    /// <summary>What one stack looks like right now. Colours are glossary indices (0..5), top card first.</summary>
    public struct StackVisual
    {
        public int X, Y, W, H, Layer;
        public int PoseOffset, PoseCount, PoseLayer, AuthoredCount, PoseRoot, SpreadDirection, SpreadAngle;
        public int[] Colors;
        public bool Covered;
        /// <summary>Drawn as a fan around a pivot below the stack (CR-004) instead of a straight pile.</summary>
        public bool Fan;
        /// <summary>Draw the wider card spacing of the reference layouts.</summary>
        public bool Spread;
        /// <summary>The stack can be tapped right now (not covered, not empty): draw the hint outline (decided by the controller).</summary>
        public bool Hint;
        /// <summary>CR-012 D (R-24): draw the cards face down.</summary>
        public bool FaceDown;
        /// <summary>CR-012 D (R-26): the counter on the ice, already localized; null = not locked (no ice).</summary>
        public string LockLabel;
        /// <summary>The number of same-colour chips on top of the stack, drawn on its top chip so the player sees at a glance
        /// how many a tap sends (Phat 09-11); null = none (face down, locked, or another stack sits on it).</summary>
        public string CountLabel;
    }

    /// <summary>
    /// One card flying after a tap (CR-007: a run leaves card by card, as in the reference). Indices are
    /// the controller's; the view only looks positions up. A source is a stack card (<see cref="FromStack"/>,
    /// <see cref="FromDepth"/> from the top, as drawn before the tap) or a holding groove (<see cref="FromHeld"/>);
    /// a destination is a peg position (<see cref="ToSlot"/>, <see cref="ToIndex"/>) or a groove
    /// (<see cref="ToHeld"/>, a groove index). <see cref="ToHeldFinal"/> is where that card sits once the tap has settled
    /// (−1 = it left the holding area again), <see cref="After"/> the flight it must wait for, and
    /// <see cref="Completes"/> marks a card whose peg fills up and leaves.
    /// </summary>
    public struct CardFlight
    {
        public int Color, FromStack, FromDepth, FromHeld, ToSlot, ToIndex, ToHeld, ToHeldFinal, After;
        public bool Completes;
        /// <summary>How many times <see cref="ToSlot"/>'s peg filled up earlier in this same change: 0 = the peg on screen,
        /// 1 = the one that replaced it, … A card for a replacement peg waits until the full one has left and the
        /// replacement has moved up (Phat: the next peg only takes cards after the old one is gone).</summary>
        public int SwapGen;
        /// <summary>CR-012 C2 Remove on a waiting pole: the card flies to the pole waiting behind <see cref="ToSlot"/> (the back row),
        /// which leaves with it — nothing on the board takes the card.</summary>
        public bool ToQueued, PlayOnFlight;
        public int QueueDepth, QueueCapacity, QueueTargetId;
        /// <summary>D-098: capacity of the front pole the card lands on (0 = unknown) — its drawn height.</summary>
        public int Capacity;
    }

    public struct TargetVisual
    {
        public bool Has;
        public int Color, Filled, Capacity, Id;
        public string CountLabel;
    }

    /// <summary>Everything the board shows, already decided by the controller (rule: a View never branches on
    /// game state). Labels arrive localized.</summary>
    public sealed class BoardVisual
    {
        public StackVisual[] Stacks = Array.Empty<StackVisual>();
        public TargetVisual[] Targets = Array.Empty<TargetVisual>();
        /// <summary>CR-009: per slot, the colour of the target waiting behind it (−1 = none).</summary>
        public int[] Upcoming = Array.Empty<int>();
        /// <summary>CR-012 D (R-25): per slot, the waiting peg hides its colour (stone base + <see cref="HiddenMark"/>).</summary>
        public bool[] UpcomingHidden = Array.Empty<bool>();
        public TargetVisual[][] WaitingPoles = Array.Empty<TargetVisual[]>();
        public int InitialQueueDepth;
        public string HiddenMark;
        /// <summary>The pole count (<c>gameplay.count</c>) localized with its numbers left as <c>{0}</c> (chips shown) and
        /// <c>{1}</c> (capacity): the 3D board fills it in as each chip lands, instead of jumping to the settled count at the tap.</summary>
        public string PoleCountFormat;
        public int[] Buffer = Array.Empty<int>();
        public int BufferCapacity;
        /// <summary>D-100: holding slots drawn — the open ones (<see cref="BufferCapacity"/>) and, after them, the ones RV
        /// Slots can still unlock, in groups of <see cref="RvSlotGroup"/>. 0 = only the open ones.</summary>
        public int BufferSlots;
        public int BufferBaseCapacity; // initial narrow grooves; RV groups retain their wider pitch when opened
        public int RvSlotGroup;
        /// <summary>The groove of each holding-area card, in <see cref="Buffer"/> order: a card keeps its groove until
        /// it leaves, so the others never shift (Phat).</summary>
        public int[] BufferGrooves = Array.Empty<int>();
        public bool BufferWarn;
        public string HoldingLabel, BufferCountLabel, NextLabel;
        /// <summary>R-23: the RV Slot button label ("+8 slots"), or null to hide the button (none left, or not playing).</summary>
        public string RvSlotLabel;
    }

    /// <summary>
    /// The gameplay board (mock-up <c>docs/mockups/gameplay-v01.png</c>): HUD, target row, holding area and the
    /// tray of stacks, built as runtime uGUI from the approved sprites (art v1) on the 1080-wide rig. It renders
    /// what it is given and relays taps as stack indices; every decision is the controller's.
    /// </summary>
    public sealed class GameplayHudView : MonoBehaviour
    {
        [SerializeField] private GameplayUI _moneySortHud;
        [SerializeField] private CanvasGroup _hardLevelIntro;
        public CanvasGroup HardLevelIntro => _hardLevelIntro;
        [SerializeField] private RewindOverlayView _rewindOverlay;    // prefab: Undo's full-screen "◀◀ REW" tape effect
        public RewindOverlayView RewindOverlay => _rewindOverlay;
        [SerializeField] private ElementTutorialUI _elementTutorial;   // prefab: the spotlight + hint after a new element's popup
        public ElementTutorialUI ElementTutorial => _elementTutorial;
        [SerializeField] private BoosterHelperUI _boosterHelper;       // prefab: the banner + pick hint + ✕ of a booster in use
        public BoosterHelperUI BoosterHelper => _boosterHelper;
        private CanvasGroup _pickTile;                                  // the HUD tile hidden while its pick is up
        [SerializeField] private FlickSortFtueController _moneySortTutorial;
        public event Action ShopPressed;
        /// <summary>D-118: move the booster bar above the bottom banner (or back); the space kept, in world units.</summary>
        public float ReserveBannerSpace(bool bannerUp) => _moneySortHud != null ? _moneySortHud.ReserveBannerSpace(bannerUp) : 0f;
        /// <summary>World y of the booster bar's top edge; float.MinValue when there is no bar.</summary>
        public float BoosterBarTopWorldY() => _moneySortHud != null ? _moneySortHud.BoosterBarTopWorldY() : float.MinValue;
        public void SetProgress(int level,float progress,int total){if(_moneySortHud!=null){_moneySortHud.Init(null);_moneySortHud.SetProgress(level,progress,total);}}

        [Header("Cards — index = glossary colour")]
        [SerializeField] private Sprite[] _cardFace = new Sprite[8];
        [SerializeField] private Sprite[] _cardUnder = new Sprite[8];
        [SerializeField] private Sprite[] _cardMini = new Sprite[8];
        [SerializeField] private Sprite[] _chip = new Sprite[8];
        [SerializeField] private Sprite[] _targetBase = new Sprite[8];
        [Header("Board")]
        [SerializeField] private Sprite _hatch;
        [SerializeField] private Sprite _targetSlot;
        [SerializeField] private Sprite _trayRim;
        [SerializeField] private Sprite _trayInner;
        [SerializeField] private Sprite _bufferRail;
        [SerializeField] private Sprite _bufferRailWarn;
        [SerializeField] private Sprite _bufferCell;
        [SerializeField] private Sprite _bufferCellWarn;
        [SerializeField] private Sprite _ground;
        [Header("HUD, tutorial")]
        [SerializeField] private Sprite _pill;
        [SerializeField] private Sprite _roundButton;
        [SerializeField] private Sprite _iconPause;
        [SerializeField] private Sprite _iconRestart;
        [SerializeField] private Sprite _buttonPrimary;
        [SerializeField] private Sprite _iconPlay;
        [SerializeField] private Sprite _coin;
        [SerializeField] private Sprite _hand;
        [SerializeField] private Sprite _ring;
        [SerializeField] private Sprite _glowRing;   // the active booster tile's ring + halo (glow_ring, B-004)
        [Header("Booster bar (CR-012 C2)")]
        [SerializeField] private Sprite _boostTile;
        [SerializeField] private Sprite[] _boosterIcons = new Sprite[3];
        [SerializeField] private Sprite _badge;
        [SerializeField] private Sprite _priceTag;
        [SerializeField] private Sprite _lock;
        [SerializeField] private TMP_FontAsset _font;
        [Header("Abhi's HUD (Art Assets/UI, his gameplay demo): empty ⇒ the HUD and booster bar above")]
        [SerializeField] private Sprite _artCoinSlab;
        [SerializeField] private Sprite _artLevelSlab;
        [SerializeField] private Sprite _artCoin;
        [SerializeField] private Sprite _artPause;
        [SerializeField] private Sprite _artReplay;
        [Tooltip("Hand, Shuffle, Remove (his magnet): each a whole framed tile.")]
        [SerializeField] private Sprite[] _artBoosters = new Sprite[3];
        [SerializeField] private TMP_FontAsset _hudFont;
        [Tooltip("Abhi's RV badge (Art Assets/UI/RV UI.png): set ⇒ one on each locked holding group instead of the RV pill and locks.")]
        [SerializeField] private Sprite _rvBadge;
        [SerializeField] private Sprite _slotWarn;   // D-138: Figma WF "Rectangle 3979", the red capsule over a free slot
        [Tooltip("False when the board is drawn in 3D (CR-003): this view then draws only the HUD, labels and tap areas.")]
        [SerializeField] private bool _draw2DBoard = true;

        public event Action<int> StackTapped;
        public event Action PausePressed, RestartPressed, RvSlotPressed;
        /// <summary>CR-012 C2: a booster tile was pressed (index in bar order).</summary>
        public event Action<int> BoosterPressed;
        /// <summary>Pick mode (Remove): a pole was tapped — the slot, and 0 for the front pole or 1 for the one waiting behind it.</summary>
        public event Action<int, int> PoleTapped;
        /// <summary>Pick mode: the dim or the active tile was tapped.</summary>
        public event Action PickCancelled;

        private RectTransform _root, _boardLayer, _barLayer, _pickLayer, _tutorialLayer;
        // under _boardLayer, in drawing order: the 2D board (rebuilt per Render), the labels, the tap areas, the element marks.
        // Everything but the 2D board is built once and reused: a redraw only moves, re-labels and hides.
        private RectTransform _board2DLayer, _labelLayer, _hitLayer, _markLayer;
        [SerializeField] private bool _showLockCountdown;

        private readonly UiSlots _hits = new UiSlots(), _marks = new UiSlots(), _rvLocks = new UiSlots();
        private readonly List<int> _hitOrder = new List<int>();
        private TextMeshProUGUI _holdingLabel, _bufferCount, _rvSlotLabel;
        private Image _rvSlot, _rvSlotIcon;
        private string _rvSlotText;
        private BoosterTileVisual[] _tiles = Array.Empty<BoosterTileVisual>();
        private bool _barDrawn;
        private TextMeshProUGUI _levelLabel, _coinsLabel;

        private float Height => _root != null && _root.rect.height > 1f ? _root.rect.height : 1920f;
        private float Tall => Mathf.Max(0f, Height - 1920f);

        private void EnsureBuilt()
        {
            if (_root != null) return;
            _root = UiKit.LegacyRoot("Root", transform);
            if (_draw2DBoard)
            {
                var ground = UiKit.Fill("Ground", _root, Color.white, false);
                ground.sprite = _ground;
            }
            _boardLayer = UiKit.Stretch("Board", _root);
            if (_draw2DBoard) _board2DLayer = UiKit.Stretch("Board2D", _boardLayer);
            _labelLayer = UiKit.Stretch("Labels", _boardLayer);
            _hitLayer = UiKit.Stretch("Hits", _boardLayer);
            _markLayer = UiKit.Stretch("Marks", _boardLayer);
            BuildHud();
            _barLayer = UiKit.Stretch("Boosters", _root);
            _pickLayer = UiKit.Stretch("Pick", _root);
            _tutorialLayer = UiKit.Stretch("Tutorial", _root);
        }

        private bool HasArtHud => _artCoinSlab != null && _artLevelSlab != null && _artCoin != null && _artPause != null && _artReplay != null;
        private bool HasArtBoosters => _artBoosters != null && _artBoosters.Length >= 3 && Array.TrueForAll(_artBoosters, s => s != null);

        // Abhi's gameplay demo, measured at 1080×1920: coin slab top-left with his poker coin over its left end, level slab
        // top-right, pause under the coin and replay under the level, his white type on the brown slabs
        private void BuildArtHud()
        {
            const float slabW = 330f, coinSlabH = 95f, levelSlabH = 86f, top = 52f, button = 112f, buttonTop = 194f, coinD = 112f;
            var font = _hudFont != null ? _hudFont : _font;
            UiKit.Image("CoinSlab", _root, _artCoinSlab, 50f, top, slabW, coinSlabH);
            UiKit.Image("Coin", _root, _artCoin, 38f, top + (coinSlabH - coinD) / 2f, coinD, coinD);
            _coinsLabel = UiKit.Text("Coins", _root, font, string.Empty, DesignTokens.TypeHud, DesignTokens.OnColor, 50f + coinD - 12f, top, slabW - coinD, coinSlabH - 6f);
            var level = UiKit.Image("LevelSlab", _root, _artLevelSlab, 1080f - 50f - slabW, top + (coinSlabH - levelSlabH) / 2f, slabW, levelSlabH);
            _levelLabel = UiKit.Text("Level", level.transform, font, string.Empty, DesignTokens.TypeHud, DesignTokens.OnColor, 0f, 0f, slabW, levelSlabH - 6f);
            var pause = UiKit.Image("Pause", _root, _artPause, 50f, buttonTop, button, button);
            UiKit.Button(pause, () => PausePressed?.Invoke());
            var restart = UiKit.Image("Restart", _root, _artReplay, 1080f - 50f - button, buttonTop, button, button);
            UiKit.Button(restart, () => RestartPressed?.Invoke());
        }

        private void BuildHud()
        {
            if(_moneySortHud!=null){_moneySortHud.Init(null);_moneySortHud.InitializeSettings(()=>PausePressed?.Invoke());_moneySortHud.InitializeRestart(()=>RestartPressed?.Invoke());_moneySortHud.InitializeShop(()=>ShopPressed?.Invoke());_moneySortHud.BoosterRequested=t=>BoosterPressed?.Invoke(MoneySortUiValue.BoosterIndex(t));return;}

            if (HasArtHud) { BuildArtHud(); return; }
            float m = DesignTokens.ScreenMargin, b = DesignTokens.RoundButton;
            _coinsLabel = UiKit.CoinPill(_root, _pill, _coin, _font, string.Empty);   // CR-012 C1
            var pill = UiKit.Image("LevelPill", _root, _pill, 540f - 125f, 70f, 250f, 104f, sliced: true);
            _levelLabel = UiKit.Text("Level", pill.transform, _font, string.Empty, DesignTokens.TypeHud, DesignTokens.Ink, 0f, 0f, 250f, 96f);
            var restart = UiKit.Image("Restart", _root, _roundButton, 1080f - m - 2f * b - 32f, DesignTokens.HudTop, b, b + 10f);
            UiKit.Image("Icon", restart.transform, _iconRestart, 26f, 20f, 52f, 52f).color = DesignTokens.OnColor;
            UiKit.Button(restart, () => RestartPressed?.Invoke());
            var pause = UiKit.Image("Pause", _root, _roundButton, 1080f - m - b, DesignTokens.HudTop, b, b + 10f);
            UiKit.Image("Icon", pause.transform, _iconPause, 28f, 22f, 48f, 48f).color = DesignTokens.OnColor;
            UiKit.Button(pause, () => PausePressed?.Invoke());
        }

        /// <summary>The lives pill of Abhi's HUD: the count on the heart and "Full" or the time to the next life (both localized).</summary>
        public void SetLives(string count, string next)
        {
            if (_moneySortHud == null) return;
            EnsureBuilt();
            _moneySortHud.SetLabel("@ResoureGroup/LivesHud/HeartIcon/LivesText", count);
            _moneySortHud.SetLabel("@ResoureGroup/LivesHud/LifeTimerText", next);
        }

        public void SetHud(string levelLabel, string coinsLabel)
        {
            if(_moneySortHud!=null){EnsureBuilt();_moneySortHud.SetLabel("_levelText",levelLabel);_moneySortHud.SetLabel("_moneyText",coinsLabel);_moneySortHud.transform.SetAsLastSibling();return;}

            EnsureBuilt();
            UiKit.SetText(_levelLabel, levelLabel);
            UiKit.SetText(_coinsLabel, coinsLabel);
        }

        /// <summary>Redraw the whole board. 6a redraws after every tap; 6c replays the model's steps as motion.</summary>
        public void Render(BoardVisual v)
        {
            EnsureBuilt();
            if (!_draw2DBoard) { DrawLabels(v); return; }
            for (int i = _board2DLayer.childCount - 1; i >= 0; i--) Destroy(_board2DLayer.GetChild(i).gameObject);
            DrawTargets(v);
            DrawBuffer(v);
            DrawTray(v);
        }

        /// <summary>
        /// 3D mode (CR-003): invisible tap areas over the stacks the 3D view draws. <paramref name="worldRects"/>
        /// are world-space rectangles (game units); <paramref name="layers"/> orders them so a higher stack sits
        /// on top. Taps relay the stack index; whether the tap counts is the controller's call (R-4).
        /// </summary>
        public void SetHitAreas(Rect[] worldRects, int[] layers)
        {
            EnsureBuilt();
            var order = _hitOrder;
            order.Clear();
            for (int i = 0; i < worldRects.Length; i++) if (worldRects[i].width > 0f) order.Add(i);
            order.Sort((a, b) => layers[a] != layers[b] ? layers[a].CompareTo(layers[b]) : a.CompareTo(b));
            var rect = _root.rect;
            _hits.Begin();
            foreach (int i in order)
            {
                var r = worldRects[i];
                // world → this view's local space (the Ui host may be inset by the safe area), then top-left coordinates
                var min = _root.InverseTransformPoint(new Vector3(r.xMin, r.yMin, _root.position.z));
                var max = _root.InverseTransformPoint(new Vector3(r.xMax, r.yMax, _root.position.z));
                if (!_hits.Fetch("Hit", i, out HitArea hit))
                {
                    hit = _hits.Keep("Hit", i, UiKit.HitArea($"Hit{i}", _hitLayer, 0f, 0f, 0f, 0f));
                    var relay = hit.gameObject.AddComponent<TapRelay>();
                    relay.Index = i;
                    relay.Tapped = index => StackTapped?.Invoke(index);
                }
                UiKit.Place(hit.rectTransform, min.x - rect.xMin, rect.yMax - max.y, max.x - min.x, max.y - min.y);
                hit.transform.SetAsLastSibling();   // in layer order: a higher stack's area sits on top
            }
            _hits.End();
        }

        /// <summary>
        /// CR-012 D (mock-ups gameplay-*-v03): the counter of each locked stack on a deep-ice disc at the centre of its
        /// ice, and the "?" chip beside each waiting peg that hides its colour. Rects are the 3D view's world rects;
        /// labels arrive localized (null / empty rect = nothing there). Drawn above the hit areas, never catching taps.
        /// </summary>
        public void SetElementMarks(Rect[] iceRects, string[] lockLabels, Rect[] backPoles, string[] poleMarks)
        {
            EnsureBuilt();
            _marks.Begin();
            for (int i = 0; i < iceRects.Length && i < lockLabels.Length; i++)
            {
                if (lockLabels[i] == null || iceRects[i].width <= 0f) continue;
                var c = Local(iceRects[i].center);
                const float s = 84f;
                if (!_marks.Fetch("Lock", i, out Image disc))
                {
                    disc = _marks.Keep("Lock", i, UiKit.Image($"Lock{i}", _markLayer, _pill, 0f, 0f, s, s, sliced: true));
                    disc.color = DesignTokens.IceDeep;
                    UiKit.Text("Count", disc.transform, _font, null, DesignTokens.TypeHud, DesignTokens.OnColor, 0f, -4f, s, s);
                }
                UiKit.Place(disc.rectTransform, c.x - s / 2f, c.y - s / 2f, s, s);
                UiKit.SetText(disc.transform.GetChild(0).GetComponent<TextMeshProUGUI>(), lockLabels[i]);
                disc.gameObject.SetActive(_showLockCountdown);
            }
            for (int u = 0; u < backPoles.Length && u < poleMarks.Length; u++)
            {
                if (poleMarks[u] == null || backPoles[u].width <= 0f) continue;
                var r = backPoles[u];
                var c = Local(new Vector2(r.xMin, r.center.y));
                const float s = 50f;
                if (!_marks.Fetch("PoleMark", u, out Image chip))
                {
                    chip = _marks.Keep("PoleMark", u, UiKit.Image($"PoleMark{u}", _markLayer, _pill, 0f, 0f, s, s, sliced: true));
                    chip.color = DesignTokens.Ink;
                    UiKit.Text("Mark", chip.transform, _font, null, DesignTokens.TypeLabel + 4f, DesignTokens.OnColor, 0f, -3f, s, s);
                }
                UiKit.Place(chip.rectTransform, c.x - s * 0.8f, c.y - s / 2f - 8f, s, s);
                UiKit.SetText(chip.transform.GetChild(0).GetComponent<TextMeshProUGUI>(), poleMarks[u]);
            }
            _marks.End();
        }

        /// <summary>
        /// D-100: the locked holding slots, one world rect per RV group, left to right (the 3D view's rects). The RV Slot
        /// pill sits on the first group — the next to unlock, since slots open in order — and a lock on each later one.
        /// </summary>
        public void SetRvSlotGroups(Rect[] groups)
        {
            EnsureBuilt();
            if (_rvBadge != null) { PlaceRvBadges(groups); return; }
            bool pill = _rvSlot != null && _rvSlot.gameObject.activeSelf;
            if (pill && groups.Length > 0 && groups[0].width > 0f)
            {
                var c = Local(groups[0].center);
                var size = _rvSlot.rectTransform.rect.size;
                // the pill is wider than a group of 3: kept inside the screen margins (it ran off the rail's right end)
                float m = DesignTokens.ScreenMargin, x = Mathf.Clamp(c.x - size.x / 2f, m, _root.rect.width - m - size.x);
                UiKit.Place(_rvSlot.rectTransform, x, c.y - size.y / 2f, size.x, size.y);
            }
            _rvLocks.Begin();
            for (int i = pill ? 1 : 0; i < groups.Length; i++)
            {
                if (groups[i].width <= 0f) continue;
                var c = Local(groups[i].center);
                const float s = 56f;
                if (!_rvLocks.Fetch("RvLock", i, out Image icon))
                    icon = _rvLocks.Keep("RvLock", i, UiKit.Image($"RvLock{i}", _markLayer, _lock, 0f, 0f, s, s));
                UiKit.Place(icon.rectTransform, c.x - s / 2f, c.y - s / 2f, s, s);
            }
            _rvLocks.End();
        }

        // Abhi's "Locked RV" mock-up (10-11): his clapper badge with "+3 Slots" in its brown pill on every locked group, each
        // one a button for the next RV (slots open left to right, so any of them unlocks the first group); no badge — the
        // group just stays grey — when no RV is on offer (the label is null)
        private void PlaceRvBadges(Rect[] groups)
        {
            _rvLocks.Begin();
            _nudgeAnchor = default;
            for (int i = 0; i < groups.Length && _rvSlotText != null; i++)
            {
                if (groups[i].width <= 0f) continue;
                var c = Local(groups[i].center);
                var r = Local(new Vector2(groups[i].xMax, groups[i].yMin));
                var l = Local(new Vector2(groups[i].xMin, groups[i].yMin));
                float w = Mathf.Min(DesignTokens.RvBadgeMaxWidth, (r.x - l.x) * DesignTokens.RvBadgeFill);
                float h = w * _rvBadge.rect.height / _rvBadge.rect.width;
                if (!_rvLocks.Fetch("RvBadge", i, out Image badge))
                {
                    badge = _rvLocks.Keep("RvBadge", i, UiKit.Image($"RvBadge{i}", _markLayer, _rvBadge, 0f, 0f, w, h, raycast: true));
                    UiKit.Text("Label", badge.transform, _hudFont != null ? _hudFont : _font, null, DesignTokens.TypeLabel, DesignTokens.OnColor, 0f, 0f, w, h);
                    UiKit.Button(badge, () => RvSlotPressed?.Invoke());
                }
                UiKit.Place(badge.rectTransform, c.x - w / 2f, c.y - h / 2f, w, h);
                if (i == 0) _nudgeAnchor = new Rect(c.x - w / 2f, c.y - h / 2f, w, h);   // D-138: the bubble points here
                // the label sits in the badge's brown pill: its lower third, inside the gold rim
                var label = badge.transform.GetChild(0).GetComponent<TextMeshProUGUI>();
                label.enableAutoSizing = true;
                label.fontSizeMin = 10f;
                label.fontSizeMax = DesignTokens.TypeLabel;
                UiKit.Place(label.rectTransform, w * 0.1f, h * 0.66f, w * 0.8f, h * 0.27f);
                UiKit.SetText(label, _rvSlotText);
            }
            _rvLocks.End();
        }

        /// <summary>Phat 09-11: the same-colour count the 3D board draws on each top chip is set in this view's type.</summary>

        // a world point → this view's top-left coordinates (the Ui host may be inset by the safe area)
        private Vector2 Local(Vector2 world)
        {
            var rect = _root.rect;
            var p = _root.InverseTransformPoint(new Vector3(world.x, world.y, _root.position.z));
            return new Vector2(p.x - rect.xMin, rect.yMax - p.y);
        }

        // 3D mode: the text that sits over the 3D board (counts, holding area, next) — positions from the mock-up
        private void DrawLabels(BoardVisual v)
        {
            if (_holdingLabel == null)
            {
                float m = DesignTokens.ScreenMargin;
                // no x/18 count: a pole is exactly 18 cards tall, so how full it is shows on the peg (Phat)
                _holdingLabel = UiKit.Text("HoldingLabel", _labelLayer, _font, null, DesignTokens.TypeLabel, DesignTokens.InkSoft, m + 30f, DesignTokens.BufferLabelTop, 400f, 40f, TextAlignmentOptions.Left);
                _bufferCount = UiKit.Text("BufferCount", _labelLayer, _font, null, DesignTokens.TypeLabel, DesignTokens.Ink,
                    1080f - m - 30f - 300f, DesignTokens.BufferLabelTop, 300f, 40f, TextAlignmentOptions.Right);
            }
            UiKit.SetText(_holdingLabel, v.HoldingLabel);
            _holdingLabel.gameObject.SetActive(!HasArtHud);   // Abhi's demo has no "Holding area" title: his slot rail says it
            UiKit.SetText(_bufferCount, v.BufferCountLabel);
            _bufferCount.color = v.BufferWarn ? DesignTokens.DangerText : DesignTokens.Ink;
            _bufferCount.gameObject.SetActive(_rvBadge == null); // the new rail mock-up has no external capacity counter
            DrawRvSlot(v.RvSlotLabel);
            // CR-007: the queue is drawn as the back row of pegs in Board3DView, not as chips
        }

        // R-23 (CR-012 stage B): a small primary pill above the holding rail, right of centre, clear of the pegs: ad icon + "+8 slots"
        private void DrawRvSlot(string label)
        {
            if (_rvBadge != null)
            {
                _rvSlotText = label;   // the badges on the locked groups carry it (PlaceRvBadges); no pill above the rail
                if (_rvSlot != null) _rvSlot.gameObject.SetActive(false);
                return;
            }
            if (label == null)
            {
                if (_rvSlot != null) _rvSlot.gameObject.SetActive(false);
                return;
            }
            const float w = 260f, h = 58f, x = 620f, y = DesignTokens.BufferLabelTop - 14f, s = 26f;
            if (_rvSlot == null)
            {
                _rvSlot = UiKit.Image("RvSlot", _labelLayer, _buttonPrimary, x, y, w, h, sliced: true, raycast: true);
                _rvSlotLabel = UiKit.Text("Label", _rvSlot.transform, _font, null, DesignTokens.TypeLabel, DesignTokens.OnColor, 0f, 0f, w, h - 8f);
                _rvSlotIcon = UiKit.Image("Icon", _rvSlot.transform, _iconPlay, 0f, 0f, s, s);
                _rvSlotIcon.color = DesignTokens.OnColor;
                UiKit.Button(_rvSlot, () => RvSlotPressed?.Invoke());
            }
            _rvSlot.gameObject.SetActive(true);
            if (label == _rvSlotText) return;
            _rvSlotText = label;
            _rvSlotLabel.SetText(label);
            float labelWidth = _rvSlotLabel.GetPreferredValues(label).x, total = s + 10f + labelWidth;
            UiKit.Place(_rvSlotLabel.rectTransform, (w - total) / 2f + s + 10f, 0f, labelWidth + 4f, h - 8f);
            UiKit.Place(_rvSlotIcon.rectTransform, (w - total) / 2f, (h - 8f - s) / 2f, s, s);
        }

        private void DrawTargets(BoardVisual v)
        {
            int n = v.Targets.Length;
            float w = n == 2 ? DesignTokens.TargetSlotWidthTwo : DesignTokens.TargetSlotWidth;
            float gap = n == 2 ? DesignTokens.TargetSlotGapTwo : DesignTokens.TargetSlotGap;
            float x0 = 540f - (n * w + (n - 1) * gap) / 2f;
            for (int i = 0; i < n; i++)
            {
                var t = v.Targets[i];
                var slot = UiKit.Image($"Target{i}", _board2DLayer, _targetSlot, x0 + i * (w + gap), DesignTokens.TargetsTop, w, DesignTokens.TargetsHeight + 8f, sliced: true);
                if (!t.Has) continue;
                for (int j = 0; j < t.Filled; j++)
                    UiKit.Image($"Card{j}", slot.transform, _cardFace[t.Color], w / 2f - 75f, DesignTokens.TargetCardTop - j * DesignTokens.TargetCardStep, DesignTokens.CardWidth, DesignTokens.CardHeight);
                UiKit.Image("Base", slot.transform, _targetBase[t.Color], w / 2f - 115f, 296f, 230f, 80f, sliced: true);
                UiKit.Text("Count", slot.transform, _font, t.CountLabel, DesignTokens.TypeCount, DesignTokens.OnColor, 0f, 300f, w, 60f);
            }
            if (v.Upcoming.Length > 0)
            {
                float x = 1080f - DesignTokens.ScreenMargin - 12f - v.Upcoming.Length * 44f;
                UiKit.Text("NextLabel", _board2DLayer, _font, v.NextLabel, 28f, DesignTokens.InkSoft, x - 90f, 194f, 84f, 46f, TextAlignmentOptions.Right);
                for (int i = 0; i < v.Upcoming.Length; i++)
                    if (v.Upcoming[i] >= 0) UiKit.Image($"Next{i}", _board2DLayer, _chip[v.Upcoming[i]], x + i * 44f, 194f, 34f, 50f);
            }
        }

        private void DrawBuffer(BoardVisual v)
        {
            float m = DesignTokens.ScreenMargin;
            UiKit.Text("HoldingLabel", _board2DLayer, _font, v.HoldingLabel, DesignTokens.TypeLabel, DesignTokens.InkSoft, m + 30f, DesignTokens.BufferLabelTop, 400f, 40f, TextAlignmentOptions.Left);
            UiKit.Text("BufferCount", _board2DLayer, _font, v.BufferCountLabel, DesignTokens.TypeLabel, v.BufferWarn ? DesignTokens.DangerText : DesignTokens.Ink,
                1080f - m - 30f - 300f, DesignTokens.BufferLabelTop, 300f, 40f, TextAlignmentOptions.Right);
            var rail = UiKit.Image("Rail", _board2DLayer, v.BufferWarn ? _bufferRailWarn : _bufferRail, m, DesignTokens.BufferTop, 1080f - 2f * m, DesignTokens.BufferHeight, sliced: true);
            int cap = Mathf.Max(1, v.BufferCapacity);
            float inner = DesignTokens.BufferInnerWidth;
            float cw = Mathf.Min(DesignTokens.BufferCellWidth, Mathf.Floor((inner - 8f * (cap - 1)) / cap));
            float step = cap > 1 ? (inner - cw) / (cap - 1) : 0f;   // exact and centred, as the 3D rail (Board3DView.Groove)
            for (int i = 0; i < cap; i++)
            {
                float x = (1080f - 2f * m - inner) / 2f + i * step;
                if (i < v.Buffer.Length)
                    UiKit.Image($"Card{i}", rail.transform, _cardMini[v.Buffer[i]], x, DesignTokens.BufferCellTop, cw, DesignTokens.BufferCellHeight + 6f);
                else
                    UiKit.Image($"Cell{i}", rail.transform, v.BufferWarn ? _bufferCellWarn : _bufferCell, x, DesignTokens.BufferCellTop, cw, DesignTokens.BufferCellHeight);
            }
        }

        private void DrawTray(BoardVisual v)
        {
            float m = DesignTokens.ScreenMargin, tall = Tall;
            float h = DesignTokens.TrayHeight + tall;
            UiKit.Image("TrayRim", _board2DLayer, _trayRim, m, DesignTokens.TrayTop, 1080f - 2f * m, h + 14f, sliced: true);
            float inset = DesignTokens.TrayRimInset;
            var inner = UiKit.Image("TrayInner", _board2DLayer, _trayInner, m + inset, DesignTokens.TrayTop + inset, 1080f - 2f * (m + inset), h - 2f * inset, sliced: true);
            float oy = Mathf.Round(tall / 2f);
            // draw lower layers first so higher layers sit on top
            var order = new List<int>();
            for (int i = 0; i < v.Stacks.Length; i++) order.Add(i);
            order.Sort((a, b) => v.Stacks[a].Layer != v.Stacks[b].Layer ? v.Stacks[a].Layer.CompareTo(v.Stacks[b].Layer) : a.CompareTo(b));
            foreach (int i in order)
            {
                var s = v.Stacks[i];
                if (s.Colors == null || s.Colors.Length == 0) continue;
                var stack = UiKit.Node($"Stack{i}", inner.transform, s.X, s.Y + oy, DesignTokens.CardWidth, DesignTokens.CardHeight + DesignTokens.CardStep * (s.Colors.Length - 1));
                for (int d = s.Colors.Length - 1; d >= 1; d--)
                {
                    var under = UiKit.Image($"Under{d}", stack, _cardUnder[s.Colors[d]], 0f, d * DesignTokens.CardStep, DesignTokens.CardWidth, DesignTokens.CardUnderHeight);
                    if (s.Covered) under.color = DesignTokens.CoveredTint;
                }
                var top = UiKit.Image("Top", stack, _cardFace[s.Colors[0]], 0f, 0f, DesignTokens.CardWidth, DesignTokens.CardHeight, raycast: true);
                if (s.Covered)
                {
                    top.color = DesignTokens.CoveredTint;
                    UiKit.Image("Hatch", top.transform, _hatch, 0f, 0f, DesignTokens.CardWidth, DesignTokens.CardBody);
                }
                var relay = top.gameObject.AddComponent<TapRelay>();
                relay.Index = i;
                relay.Tapped = index => StackTapped?.Invoke(index);
            }
        }

        // ── booster bar and pick mode (CR-012 C2, mock-ups gameplay-boosters-v02, gameplay-pick-*-v02) ───────────

        private BoosterBarArt BarArt => new BoosterBarArt(_boostTile, _boosterIcons, _badge, _priceTag, _lock, _coin, _font, _glowRing,
            HasArtBoosters ? _artBoosters : null);

        /// <summary>The HUD booster tile at <paramref name="index"/> (bar order) — where the unlock popup's booster flies.</summary>
        public RectTransform BoosterTile(int index) =>
            _moneySortHud != null ? _moneySortHud.GetBoosterButtonRect(MoneySortUiValue.Booster(index)) : null;

        /// <summary>Fade the labels, marks and tap areas that sit over the 3D board (they live on the Ui camera, so they
        /// would stay behind while the board camera moves for the shuffle show).</summary>
        public void SetBoardOverlayShown(bool shown)
        {
            EnsureBuilt();
            var group = _boardLayer.GetComponent<CanvasGroup>();
            if (group == null) group = _boardLayer.gameObject.AddComponent<CanvasGroup>();
            group.DOKill();
            group.blocksRaycasts = shown;
            group.DOFade(shown ? 1f : 0f, DesignTokens.Motion.Reveal).SetUpdate(true);
        }

        /// <summary>The booster's icon as the HUD draws it (the booster helper's banner shows it big).</summary>
        public Sprite BoosterIcon(int index) =>
            _moneySortHud != null ? _moneySortHud.GetBoosterIcon(MoneySortUiValue.Booster(index)) : null;

        /// <summary>Copies of the HUD booster tiles (bar order) placed inside <paramref name="popup"/> over the originals, so they draw
        /// and take taps above an open popup's scrim (the lose offer's "use a booster" — the HUD sits on a lower camera). Each copy's
        /// button is returned with its listeners cleared; they go away with the popup.</summary>
        public Button[] CopyBoosterTiles(RectTransform popup)
        {
            var copies = new Button[3];
            for (int i = 0; i < 3; i++)
            {
                var tile = BoosterTile(i);
                if (tile == null) continue;
                var source = tile.GetComponentInParent<BoosterButtonUI>()?.transform ?? tile;
                var copy = Instantiate(source.gameObject, popup, true);
                foreach (var t in copy.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = popup.gameObject.layer;
                copy.transform.position = source.position;
                copy.transform.SetAsLastSibling();
                var button = copy.GetComponentInChildren<Button>(true);
                if (button != null) button.onClick.RemoveAllListeners();
                copies[i] = button;
            }
            return copies;
        }

        /// <summary>Redraw the booster bar under the tray.</summary>
        public void SetBoosters(BoosterTileVisual[] tiles)
        {
            if(_moneySortHud!=null){EnsureBuilt();_tiles=tiles??Array.Empty<BoosterTileVisual>();for(int i=0;i<_tiles.Length&&i<3;i++)_moneySortHud.SetBoosterState(MoneySortUiValue.Booster(i),MoneySortUiValue.Number(_tiles[i].Badge),_tiles[i].Locked==null,MoneySortUiValue.Number(_tiles[i].Locked));return;}

            EnsureBuilt();
            tiles ??= Array.Empty<BoosterTileVisual>();
            if (_barDrawn && SameTiles(tiles, _tiles)) return;   // most taps leave the bar as it is: keep it
            _tiles = tiles;
            _barDrawn = true;
            for (int i = _barLayer.childCount - 1; i >= 0; i--) Destroy(_barLayer.GetChild(i).gameObject);
            BoosterBar.Draw(_barLayer, _tiles, BarArt, i => BoosterPressed?.Invoke(i));
        }

        private static bool SameTiles(BoosterTileVisual[] a, BoosterTileVisual[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i].Name != b[i].Name || a[i].Locked != b[i].Locked || a[i].Badge != b[i].Badge || a[i].Price != b[i].Price || a[i].Active != b[i].Active)
                    return false;
            return true;
        }

        /// <summary>
        /// Pick mode: dim everything outside the horizontal band <paramref name="litWorld"/> (world rect: the tray for Hand,
        /// the poles for Remove), keep the active tile <paramref name="active"/> lit above the dim, and show <paramref name="banner"/>
        /// just above the band or below it. <paramref name="poles"/> (world rects) become tap areas relaying
        /// (<paramref name="poleSlots"/>[k], <paramref name="poleDepths"/>[k]); null for Hand, whose taps go through the
        /// stack tap areas. A tap on the dim or on the active tile cancels.
        /// </summary>
        public void ShowPick(int active, string banner, Rect litWorld, bool bannerAbove, Rect[] poles = null, int[] poleSlots = null, int[] poleDepths = null)
        {


            EnsureBuilt();
            HidePick();
            // with the Money Sort HUD the hint, the ✕ and the banner are the BoosterHelperUI the controller opens (Figma
            // WF/Flow-Booster): no dim, the board stays as it is; only the pole tap areas below are drawn here
            var rect = _root.rect;
            float Top(float worldY) => rect.yMax - _root.InverseTransformPoint(new Vector3(0f, worldY, _root.position.z)).y;
            const float margin = 20f;
            float litTop = Mathf.Max(0f, Top(litWorld.yMax) - margin), litBottom = Mathf.Min(Height, Top(litWorld.yMin) + margin);
            if (_moneySortHud != null)
            {
                // the pressed tile gives its place to the helper's ✕ (Figma Booster Use, third frame): no badge beside it
                var tile = BoosterTile(active);
                // hidden in place (alpha, not SetActive): the bar's layout keeps the other two tiles where they are
                if (tile != null)
                {
                    _pickTile = tile.GetComponent<CanvasGroup>();
                    if (_pickTile == null) _pickTile = tile.gameObject.AddComponent<CanvasGroup>();
                    _pickTile.alpha = 0f;
                    _pickTile.blocksRaycasts = false;
                }
            }
            else
            {
                Dim("DimTop", 0f, litTop);
                Dim("DimBottom", litBottom, Height - litBottom);
            }
            if (poles != null)
                for (int k = 0; k < poles.Length; k++)
                {
                    var r = poles[k];
                    if (r.width <= 0f) continue;
                    var min = _root.InverseTransformPoint(new Vector3(r.xMin, r.yMin, _root.position.z));
                    var max = _root.InverseTransformPoint(new Vector3(r.xMax, r.yMax, _root.position.z));
                    var hit = UiKit.HitArea($"Pole{k}", _pickLayer, min.x - rect.xMin, rect.yMax - max.y, max.x - min.x, max.y - min.y);
                    int slot = poleSlots[k], depth = poleDepths[k];
                    UiKit.Button(hit, () => PoleTapped?.Invoke(slot, depth));
                }
            if (_moneySortHud == null && active >= 0 && active < _tiles.Length)
            {
                var lit = (BoosterTileVisual[])_tiles.Clone();
                lit[active].Active = true;
                BoosterBar.Draw(_pickLayer, lit, BarArt, _ => PickCancelled?.Invoke(), only: active);
            }
            if (_moneySortHud == null && !string.IsNullOrEmpty(banner))
            {
                const float w = 840f, h = 104f;
                float y = bannerAbove ? litTop - h - 16f : litBottom + 16f;
                var bar = UiKit.Image("Banner", _pickLayer, _priceTag, (1080f - w) / 2f, y, w, h, sliced: true);
                var t = UiKit.Text("Text", bar.transform, _font, banner, 44f, DesignTokens.OnColor, 0f, 0f, w, h - 4f);
                float tw = t.GetPreferredValues(banner).x, s = 56f, total = s + 16f + tw;
                var icon = _boosterIcons != null && active >= 0 && active < _boosterIcons.Length ? _boosterIcons[active] : null;
                UiKit.Image("Icon", bar.transform, icon, (w - total) / 2f, (h - 4f - s) / 2f, s, s).color = DesignTokens.OnColor;
                t.rectTransform.anchoredPosition = new Vector2((w - total) / 2f + s + 16f, 0f);
                t.rectTransform.sizeDelta = new Vector2(tw + 4f, h - 4f);
            }
        }

        public void HidePick()
        {
            if(_moneySortHud!=null)_moneySortHud.HideBoosterUsePrompt();
            if (_pickTile != null) { _pickTile.alpha = 1f; _pickTile.blocksRaycasts = true; _pickTile = null; }

            if (_pickLayer == null) return;
            for (int i = _pickLayer.childCount - 1; i >= 0; i--) Destroy(_pickLayer.GetChild(i).gameObject);
        }

        private void Dim(string name, float y, float h)
        {
            if (h <= 0f) return;
            var dim = UiKit.Image(name, _pickLayer, null, 0f, y, 1080f, h, raycast: true);
            dim.color = DesignTokens.Scrim;
            UiKit.Button(dim, () => PickCancelled?.Invoke());
            dim.color = DesignTokens.Scrim;                  // Button resets the canvas colour, not the Image colour
        }

        /// <summary>FTUE (features/ftue.md v2, mock-ups ftue-l1-tap*-v05): the whole screen dims except a hole around
        /// <paramref name="pointAt"/> (world rect; empty = no hole, no hand), a ring and the pointing hand on it, and a dark
        /// bubble with <paramref name="text"/> under the tray. Nothing here catches taps — the HUD stays usable and the
        /// controller decides which stack a tap may go to.</summary>
        public void ShowTutorial(Rect pointAt, string text)
        {
            if(_moneySortTutorial!=null){var canvas=GetComponentInParent<Canvas>();var cam=canvas!=null?canvas.worldCamera:null;var min=RectTransformUtility.WorldToScreenPoint(cam,new Vector3(pointAt.xMin,pointAt.yMin,transform.position.z));var max=RectTransformUtility.WorldToScreenPoint(cam,new Vector3(pointAt.xMax,pointAt.yMax,transform.position.z));_moneySortTutorial.gameObject.SetActive(true);_moneySortTutorial.ShowExternal(new Rect(min,max-min),text);return;}

            EnsureBuilt();
            HideTutorial();
            if (pointAt.width > 0f)
            {
                var rect = _root.rect;
                var min = _root.InverseTransformPoint(new Vector3(pointAt.xMin, pointAt.yMin, _root.position.z));
                var max = _root.InverseTransformPoint(new Vector3(pointAt.xMax, pointAt.yMax, _root.position.z));
                const float m = 18f;
                float x0 = min.x - rect.xMin - m, x1 = max.x - rect.xMin + m, y0 = rect.yMax - max.y - m, y1 = rect.yMax - min.y + m;
                TutorialDim("DimTop", 0f, 0f, 1080f, y0);
                TutorialDim("DimBottom", 0f, y1, 1080f, Height - y1);
                TutorialDim("DimLeft", 0f, y0, x0, y1 - y0);
                TutorialDim("DimRight", x1, y0, 1080f - x1, y1 - y0);
                var ring = UiKit.Image("Ring", _tutorialLayer, _ring, x0 - 24f, y0 - 24f, x1 - x0 + 48f, y1 - y0 + 48f);
                ring.color = DesignTokens.TutorialRing;
                var hand = UiKit.Image("Hand", _tutorialLayer, _hand, (x0 + x1) / 2f + 20f, (y0 + y1) / 2f + 10f, 170f, 195f);
                hand.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 18f);
            }
            if (!string.IsNullOrEmpty(text))
            {
                const float w = 760f, h = 112f;   // fits "Stack up all poles to win!" (GDD 02-11) with the pill's rounding to spare
                var bubble = UiKit.Image("Bubble", _tutorialLayer, _pill, (1080f - w) / 2f, DesignTokens.TutorialBubbleTop, w, h, sliced: true);
                bubble.color = DesignTokens.Ink;
                UiKit.Text("Text", bubble.transform, _font, text, DesignTokens.TypeHud + 6f, DesignTokens.OnColor, 0f, 0f, w, h - 6f);
            }
        }

        private void TutorialDim(string name, float x, float y, float w, float h)
        {
            if (w <= 0f || h <= 0f) return;
            UiKit.Image(name, _tutorialLayer, null, x, y, w, h).color = DesignTokens.TutorialScrim;
        }

        // D-136: the lose countdown bar (Money Sort HUD)
        public void ShowLoseCountdown(string message, string number) { if (_moneySortHud != null) _moneySortHud.ShowLoseCountdown(message, number); }
        public void SetLoseCountdown(string number) { if (_moneySortHud != null) _moneySortHud.SetLoseCountdown(number); }
        public void HideLoseCountdown() { if (_moneySortHud != null) _moneySortHud.HideLoseCountdown(); }
        public void PointAtBooster(int index) { if (_moneySortHud != null) _moneySortHud.PointAtBooster(index); }

        // D-138 (GDD 1.3 "Low slot warning"): a bubble over the next RV Slot badge, tail down to it; tapping it is tapping the
        // RV Slot. It fades out after its seconds, or at once when hidden.
        [SerializeField] private Image _nudge;
        [SerializeField] private TextMeshProUGUI _nudgeText;
        private bool _nudgeBound;
        private float _nudgeUntil;
        private Rect _nudgeAnchor;   // the first RV badge in this view's space (PlaceRvBadges)

        public void ShowRvNudge(string text, float seconds)
        {
            if (_rvBadge == null || _nudgeAnchor.width <= 0f) return;
            EnsureBuilt();
            if (_nudge == null || _nudgeText == null) return;
            if (!_nudgeBound)
            {
                _nudge.transform.SetParent(_markLayer, false);
                _nudge.GetComponent<Button>().onClick.AddListener(() => RvSlotPressed?.Invoke());
                _nudgeBound = true;
            }
            UiKit.SetText(_nudgeText, text);
            var a = _nudgeAnchor;
            var size = _nudge.rectTransform.sizeDelta;
            // The authored speech bubble points at 23% of its width, unlike the old centered diamond tail.
            float x = Mathf.Clamp(a.center.x - size.x * 0.23f, DesignTokens.ScreenMargin,
                _root.rect.width - DesignTokens.ScreenMargin - size.x);
            UiKit.Place(_nudge.rectTransform, x, a.yMin - size.y - 8f, size.x, size.y);
            _nudge.transform.SetAsLastSibling();
            _nudge.gameObject.SetActive(true);
            _nudge.canvasRenderer.SetAlpha(1f);
            foreach (var g in _nudge.GetComponentsInChildren<Graphic>()) g.canvasRenderer.SetAlpha(1f);
            _nudge.transform.localScale = Vector3.one * 0.6f;
            _nudge.transform.DOKill();
            _nudge.transform.DOScale(1f, 0.25f).SetEase(Ease.OutBack);
            _nudgeUntil = Time.time + seconds;
        }

        // D-138: one capsule per free slot (world rects from the 3D view), blinking while shown
        private readonly List<Image> _slotWarns = new List<Image>();

        public void SetSlotWarning(Rect[] freeSlots)
        {
            int n = freeSlots != null && _slotWarn != null ? freeSlots.Length : 0;
            if (n > 0) EnsureBuilt();
            for (int i = 0; i < Mathf.Max(n, _slotWarns.Count); i++)
            {
                if (i >= n) { _slotWarns[i].gameObject.SetActive(false); continue; }
                if (i >= _slotWarns.Count) _slotWarns.Add(UiKit.Image($"SlotWarn{i}", _markLayer, _slotWarn, 0f, 0f, 1f, 1f));
                var img = _slotWarns[i];
                var r = freeSlots[i];
                Vector2 a = Local(new Vector2(r.xMin, r.yMax)), b = Local(new Vector2(r.xMax, r.yMin));
                // the cell's bounds run from the groove's lip down the rail's front: the capsule covers the groove only
                float full = Mathf.Abs(b.y - a.y), w = Mathf.Abs(b.x - a.x) * 0.8f, h = full * 0.55f;
                Vector2 c = (a + b) / 2f + new Vector2(0f, -full * 0.08f);
                UiKit.Place(img.rectTransform, c.x - w / 2f, c.y - h / 2f, w, h);
                img.raycastTarget = false;
                img.gameObject.SetActive(true);
            }
        }

        private void BlinkSlotWarns()
        {
            if (_slotWarns.Count == 0) return;
            float k = 0.5f + 0.5f * Mathf.Sin(Time.time * DesignTokens.SlotWarnRate * 2f * Mathf.PI);
            float alpha = Mathf.Lerp(DesignTokens.SlotWarnAlphaLow, 1f, k);
            foreach (var img in _slotWarns) if (img.gameObject.activeSelf) img.canvasRenderer.SetAlpha(alpha);
        }

        public void HideRvNudge()
        {
            if (_nudge == null || !_nudge.gameObject.activeSelf) return;
            _nudgeUntil = 0f;
            _nudge.transform.DOKill();
            _nudge.gameObject.SetActive(false);
        }

        // the bubble fades out over its last half second
        private void LateUpdate()
        {
            BlinkSlotWarns();   // D-138
            if (_nudge == null || !_nudge.gameObject.activeSelf || _nudgeUntil <= 0f) return;
            float left = _nudgeUntil - Time.time;
            if (left <= 0f) { HideRvNudge(); return; }
            if (left < 0.5f) foreach (var g in _nudge.GetComponentsInChildren<Graphic>()) g.canvasRenderer.SetAlpha(left / 0.5f);
        }

        public void HideTutorial()
        {
            if(_moneySortTutorial!=null){_moneySortTutorial.CancelTutorial();_moneySortTutorial.gameObject.SetActive(false);return;}

            if (_tutorialLayer == null) return;
            for (int i = _tutorialLayer.childCount - 1; i >= 0; i--) Destroy(_tutorialLayer.GetChild(i).gameObject);
        }
    }
}
