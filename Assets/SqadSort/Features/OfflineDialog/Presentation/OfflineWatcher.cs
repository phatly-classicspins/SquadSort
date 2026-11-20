using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using ClassicSpins.PrototypeFramework.Application;
using ClassicSpins.PrototypeFramework.Domain;
using ClassicSpins.PrototypeFramework.Presentation;
using Game.Application;
using Game.Gen;
using Game.Views;

namespace Game.Presentation
{
    /// <summary>
    /// D-063 (features/online.md): puts the No Internet popup up whenever <see cref="OnlineGate"/> says so — every
    /// <c>internet_check_seconds</c> while the app runs and at once when it comes back from the background. Session-long (Root,
    /// where the dialog service lives), so it covers Home, Gameplay and every dialog. A popup that closes while the gate still
    /// blocks (Back, a scene change aborting it) comes straight back.
    /// </summary>
    public sealed class OfflineWatcher : IAppPhaseHandler
    {
        private static readonly TimeSpan Step = TimeSpan.FromMilliseconds(250);

        private readonly OnlineGate _gate;
        private readonly IAssetService _assets;
        private readonly IInteractionLock _inputLock;
        private readonly IRenderLayerRegistry _layers;
        private readonly ILocalizationService _loc;
        private readonly ILog _log;
        private volatile bool _lookNow;

        public OfflineWatcher(OnlineGate gate, IAssetService assets, IRenderLayerRegistry layers, ILocalizationService loc, IInteractionLock inputLock, ILog log)
        {
            _gate = gate; _assets = assets; _layers = layers; _loc = loc; _inputLock = inputLock; _log = log;
        }

        /// <summary>True while the popup is up.</summary>
        public bool Showing { get; private set; }

        public async UniTask RunAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                if (await WaitAsync(ct)) return;
                while (_gate.ShouldBlock && !ct.IsCancellationRequested)
                {
                    Showing = true;
                    try { await ShowUIAsync(ct); }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
                    catch (Exception e)
                    {
                        // not up yet (boot, a scene swap): try again at the next look rather than spin
                        _log.Warn($"[OfflineWatcher] could not show the No Internet popup: {e.Message}");
                        break;
                    }
                    finally { Showing = false; }
                }
            }
        }

        private async UniTask ShowUIAsync(CancellationToken ct)
        {
            var prefab = await _assets.LoadAsync(AssetKeys.UI.NoInternetUI, ct);
            UnityEngine.GameObject instance = null;
            NoInternetUI ui = null;
            IDisposable hold = null;
            try
            {
                // Wait for an existing scene transition before taking ownership of the Overlay input/camera hold.
                while (_inputLock.IsLocked) await UniTask.Delay(Step, ignoreTimeScale: true, cancellationToken: ct);
                if (!_gate.ShouldBlock) return;
                hold = _inputLock.Acquire("no-internet-ui");
                instance = UnityEngine.Object.Instantiate(prefab, _layers.GetHost(RenderLayers.Overlay), false);
                _layers.Stamp(instance, RenderLayers.Overlay, sortingOrder: 100);
                ui = instance.GetComponent<NoInternetUI>();
                ui.Init(null);
                ui.SetMessages(_loc.Get(LocKeys.OfflineBody), _loc.Get(LocKeys.OfflineConnecting));
                bool retryRequested = false;
                ui.SetData((Action)(() => retryRequested = true));
                ui.SetLabel("@NoInternetCard/Header/Title", _loc.Get(LocKeys.OfflineTitle));
                ui.SetButtonLabel("_retryButton", _loc.Get(LocKeys.OfflineRetry));
                ui.Show();
                while (_gate.ShouldBlock)
                {
                    ct.ThrowIfCancellationRequested();
                    if (retryRequested)
                    {
                        retryRequested = false;
                        ui.SetReconnecting(true);
                        try
                        {
                            if (await _gate.RetryAsync((delay, token) => UniTask.Delay(delay,
                                ignoreTimeScale: true, cancellationToken: token).AsTask(), ct)) break;
                        }
                        finally { if (ui != null) ui.SetReconnecting(false); }
                    }
                    await UniTask.Delay(Step, ignoreTimeScale: true, cancellationToken: ct);
                }
                var hidden = new UniTaskCompletionSource();
                ui.HideWithCompletion(() => hidden.TrySetResult());
                await hidden.Task.AttachExternalCancellation(ct);
            }
            finally
            {
                if (ui != null) ui.Animation?.Stop();
                if (instance != null) UnityEngine.Object.Destroy(instance);
                _assets.Release(prefab);
                hold?.Dispose();
            }
        }
        // one check interval, cut short by a return from the background; true when cancelled
        private async UniTask<bool> WaitAsync(CancellationToken ct)
        {
            var waited = TimeSpan.Zero;
            while (waited < _gate.CheckInterval && !_lookNow)
            {
                if (await UniTask.Delay(Step, ignoreTimeScale: true, cancellationToken: ct).SuppressCancellationThrow()) return true;
                waited += Step;
            }
            _lookNow = false;
            return false;
        }

        public AppLifecyclePhase Phase => AppLifecyclePhase.PresentationFlush;
        public void OnPause(bool paused) { if (!paused) _lookNow = true; }
        public void OnFocus(bool focused) { }
        public void OnQuit() { }
    }
}
