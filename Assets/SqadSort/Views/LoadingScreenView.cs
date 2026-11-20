using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Game.Views
{
    /// <summary>
    /// The boot Loading scene's cover — Money Sort's <c>LoadingUI</c> (logo, progress bar, percentage), driven as CardSlot
    /// drives it (D-083): on enable the bar eases 0 → 20 % → 60 % and then holds; <see cref="CompleteAsync"/> releases it to
    /// ease on to 100 % (0.25 s) and returns once it is full, just before the scene is unloaded.
    /// </summary>
    /// <remarks>Why it exists: the bar has to move to read as loading, and it must reach 100 % before the cover goes.
    /// Gallery: the scene itself, at any fill. It knows nothing about boot — <c>LoadingSceneHost</c> calls
    /// <see cref="CompleteAsync"/> on teardown.</remarks>
    public sealed class LoadingScreenView : MonoBehaviour
    {
        // Every wait here sits on the boot teardown path, which must never hang: an inactive LoadingUI runs no coroutine.
        private const float MaxWaitSeconds = 2f;

        [SerializeField] private LoadingUI _moneySortLoading;

        private Action _continueSourceLoad;
        private bool _sourceLoadFinished;

        private void OnEnable()
        {
            _sourceLoadFinished = false;
            _continueSourceLoad = null;
            _moneySortLoading.SetData(
                (Action<Action>)(done => done()),                          // nothing to wait for before 60 %
                (Action<Action>)(done => _continueSourceLoad = done),      // hold at 60 % until CompleteAsync
                (Action)(() => _sourceLoadFinished = true));
            _moneySortLoading.Show();
        }

        /// <summary>Ease the bar to full and return once it is there (bounded by <see cref="MaxWaitSeconds"/>).</summary>
        public async UniTask CompleteAsync(CancellationToken ct = default)
        {
            if (this == null || !isActiveAndEnabled) return;
            var deadline = Time.realtimeSinceStartup + MaxWaitSeconds;
            while (this != null && _continueSourceLoad == null && !ct.IsCancellationRequested && Time.realtimeSinceStartup < deadline)
                await UniTask.Yield(PlayerLoopTiming.Update, ct).SuppressCancellationThrow();
            _continueSourceLoad?.Invoke();
            _continueSourceLoad = null;
            while (this != null && !_sourceLoadFinished && !ct.IsCancellationRequested && Time.realtimeSinceStartup < deadline)
                await UniTask.Yield(PlayerLoopTiming.Update, ct).SuppressCancellationThrow();
        }
    }
}
