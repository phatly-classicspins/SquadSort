using System;
using System.Collections.Generic;
using System.Threading;
using ClassicSpins.PrototypeFramework.Application;
using ClassicSpins.PrototypeFramework.Domain;
using ClassicSpins.PrototypeFramework.Presentation;
using Cysharp.Threading.Tasks;
using Game.Views;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Presentation
{
    /// <summary>One open authored popup: <see cref="Close"/> ends it with a result; <see cref="Token"/> is cancelled once it closes
    /// (a countdown stops there); while <see cref="Busy"/> (a purchase in flight) Back does nothing.</summary>
    public sealed class PopupHandle<T>
    {
        internal readonly UniTaskCompletionSource<T> Choice = new UniTaskCompletionSource<T>();
        internal Func<T> BackResult;

        public PopupHandle(CancellationToken token) { Token = token; }

        public CancellationToken Token { get; }
        public bool Busy { get; set; }
        public void Close(T result) => Choice.TrySetResult(result);

        internal void Back()
        {
            if (!Busy && BackResult != null) Close(BackResult());
        }
    }

    /// <summary>
    /// Opens an authored Money Sort UI prefab directly on the Popup layer — no framework dialog (Phat, 07-11-2026: "không xài dialog
    /// nữa, gọi UI luôn, giống con Money Sort"). The prefab brings its own layout, animation, scrim and input surface; this loads,
    /// stamps, shows, waits for the result, plays its hide and releases it. Back goes to the topmost popup opened here
    /// (<see cref="CloseTop"/>, called first by each screen's <c>OnBackRequested</c>). Root: popups open over Home and Gameplay.
    /// </summary>
    public sealed class PopupUi
    {
        private readonly IAssetService _assets;
        private readonly IRenderLayerRegistry _layers;
        private readonly List<Action> _back = new List<Action>();

        public PopupUi(IAssetService assets, IRenderLayerRegistry layers) { _assets = assets; _layers = layers; }

        /// <summary>Back: closes the topmost popup opened here with its Back result; false when none is open.</summary>
        public bool CloseTop()
        {
            if (_back.Count == 0) return false;
            _back[_back.Count - 1]();
            return true;
        }

        /// <param name="back">The result Back closes with (null: Back does nothing on this popup).</param>
        /// <param name="bind">Fills the UI and wires its buttons to <see cref="PopupHandle{T}.Close"/>; runs after <c>Init</c>, before <c>Show</c>.</param>
        public async UniTask<T> ShowAsync<TUi, T>(AssetKey<GameObject> key, Func<T> back, Action<TUi, PopupHandle<T>> bind, CancellationToken ct)
            where TUi : UIBase
        {
            var prefab = await _assets.LoadAsync(key, ct);
            GameObject instance = null;
            TUi ui = null;
            using var open = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var handle = new PopupHandle<T>(open.Token) { BackResult = back };
            Action onBack = handle.Back;
            _back.Add(onBack);
            try
            {
                instance = Object.Instantiate(prefab, _layers.GetHost(RenderLayers.Popup), false);
                _layers.Stamp(instance, RenderLayers.Popup);
                ui = instance.GetComponent<TUi>();
                ui.Init(null);
                bind(ui, handle);
                ui.Show();
                var result = await handle.Choice.Task.AttachExternalCancellation(ct);
                open.Cancel();
                _back.Remove(onBack);
                var hidden = new UniTaskCompletionSource();
                ui.HideWithCompletion(() => hidden.TrySetResult());
                await hidden.Task.AttachExternalCancellation(ct);
                return result;
            }
            finally
            {
                open.Cancel();
                _back.Remove(onBack);
                if (ui != null) ui.Animation?.Stop();
                if (instance != null) Object.Destroy(instance);
                _assets.Release(prefab);
            }
        }

        /// <summary>Re-runs <paramref name="tick"/> once a second (unscaled) until the popup closes — a countdown, a refill check.</summary>
        public static async UniTaskVoid EverySecond(CancellationToken token, Action tick)
        {
            while (!token.IsCancellationRequested)
            {
                if (await UniTask.Delay(TimeSpan.FromSeconds(1), ignoreTimeScale: true, cancellationToken: token).SuppressCancellationThrow()) return;
                tick();
            }
        }
    }
}
