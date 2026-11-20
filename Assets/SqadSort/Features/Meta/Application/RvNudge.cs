using System;

namespace Game.Application
{
    /// <summary>
    /// D-138, GDD 1.3 RV Slot ▸ "Low slot warning" (Asana "Add prompt to use RV slots"): when <see cref="EconomyTuning.RvNudgeFreeSlots"/>
    /// or fewer waiting slots are free, the free slots blink red (<see cref="Warn"/>, always) and a bubble points at the next
    /// unused RV Slot for <see cref="EconomyTuning.RvNudgeBubbleSeconds"/> (<see cref="Poll"/>), by these rules:
    /// <list type="bullet">
    /// <item>once per drop: it shows when the free slots drop to the threshold, not again while they stay there;</item>
    /// <item>to show again they must rise above it, drop again, and <see cref="EconomyTuning.RvNudgeBubbleCooldownSeconds"/> must
    /// have passed since the last bubble — a drop inside the cooldown passes without one;</item>
    /// <item>at most <see cref="EconomyTuning.RvNudgeBubbleMaxPerAttempt"/> per level play attempt (a revive carries on the same
    /// attempt, a restart is a new one), 1 once the player ignored it in <see cref="EconomyTuning.RvNudgeIgnoreStreak"/> attempts in a
    /// row that showed it (the streak lives in the save and resets when an RV Slot is opened);</item>
    /// <item>never in the way: a drop waits while <c>blocked</c> (a tutorial, a booster, chips flying) and shows when that ends if the
    /// slots are still low; none without an RV Slot left, in an FTUE level, or once the board is lost (the caller passes none).</item>
    /// </list>
    /// Engine-free; one per Gameplay scope.
    /// </summary>
    public sealed class RvNudge
    {
        private readonly IProgressStore _store;
        private readonly EconomyTuning _tuning;
        private bool _armed = true, _pending, _showedThisAttempt, _openedThisAttempt, _attemptStarted;
        private int _shownThisAttempt;
        private DateTimeOffset? _lastBubble;

        public RvNudge(IProgressStore store, EconomyTuning tuning) { _store = store; _tuning = tuning; }

        /// <summary>A level play attempt starts (level start or restart; not a revive). The one before it counts toward the ignore
        /// streak if it showed a bubble and no RV Slot was opened.</summary>
        public void StartAttempt()
        {
            if (_attemptStarted && _showedThisAttempt && !_openedThisAttempt)
                Save(m => m.RvNudgeIgnoreStreak++);
            _attemptStarted = true;
            _showedThisAttempt = _openedThisAttempt = false;
            _shownThisAttempt = 0;
            _armed = true;
            _pending = false;
        }

        /// <summary>The player opened an RV Slot (bubble or slot): the ignore streak resets.</summary>
        public void RvOpened()
        {
            _openedThisAttempt = true;
            _pending = false;
            if (_store.Progress.RvNudgeIgnoreStreak != 0) Save(m => m.RvNudgeIgnoreStreak = 0);
        }

        /// <summary>The red blink: on while <paramref name="free"/> slots are at or under the threshold.</summary>
        public bool Warn(int free) => free <= _tuning.RvNudgeFreeSlots;

        /// <summary>Bubbles allowed in this attempt (fewer once the player keeps ignoring them).</summary>
        public int MaxThisAttempt => _store.Progress.RvNudgeIgnoreStreak >= _tuning.RvNudgeIgnoreStreak
            ? Math.Min(1, _tuning.RvNudgeBubbleMaxPerAttempt) : _tuning.RvNudgeBubbleMaxPerAttempt;

        /// <summary>Call after every board change and when a blocker ends. True = show the bubble now (recorded as shown).</summary>
        /// <param name="free">Free waiting slots.</param>
        /// <param name="rvLeft">An RV Slot is still on offer.</param>
        /// <param name="blocked">A tutorial popup, a booster or chips in flight: the bubble waits.</param>
        public bool Poll(int free, bool rvLeft, bool blocked, DateTimeOffset now)
        {
            if (free > _tuning.RvNudgeFreeSlots) { _armed = true; _pending = false; return false; }
            if (_armed) { _armed = false; _pending = true; }   // the drop
            if (!_pending || blocked) return false;
            _pending = false;                                   // this drop is decided now: shown or passed
            if (!rvLeft || _shownThisAttempt >= MaxThisAttempt) return false;
            if (_lastBubble is { } last && (now - last).TotalSeconds < _tuning.RvNudgeBubbleCooldownSeconds) return false;
            _lastBubble = now;
            _shownThisAttempt++;
            _showedThisAttempt = true;
            return true;
        }

        private void Save(Action<ProgressModel> change)
        {
            var before = _store.Progress.Clone();
            try { change(_store.Progress); _store.Save(); }
            catch (Exception) { _store.Progress.CopyFrom(before); }
        }
    }
}
