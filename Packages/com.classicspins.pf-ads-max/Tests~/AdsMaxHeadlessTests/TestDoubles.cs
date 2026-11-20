using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using ClassicSpins.PrototypeFramework.Application;
using ClassicSpins.PrototypeFramework.Ads.Max.Core;
using ClassicSpins.PrototypeFramework.Domain;

namespace ClassicSpins.PrototypeFramework.Ads.Max.HeadlessTests
{
    /// <summary>A scriptable <see cref="IMaxBridge"/>: counts every call and every subscription, and lets a
    /// test raise each SDK callback by hand. <see cref="Subscriptions"/> / <see cref="LiveSubscribers"/>
    /// count the show-path events only; the load events <see cref="MaxAdUnits"/> listens to for its
    /// lifetime are counted by <see cref="LoadSubscribers"/>.</summary>
    internal sealed class FakeMaxBridge : IMaxBridge
    {
        public bool Initialized;
        public bool Ready = true;
        public bool ThrowOnIsReady;
        public bool ThrowOnShow;
        /// <summary>Both debugger calls throw (after being counted).</summary>
        public bool ThrowOnDebugger;
        /// <summary>Reading <see cref="IsInitialized"/> throws (after being counted).</summary>
        public bool ThrowOnIsInitialized;
        public int MediationDebuggerCalls;
        public int CreativeDebuggerCalls;

        /// <summary>What <see cref="HasSupportedCmp"/> answers.</summary>
        public bool SupportedCmp;
        /// <summary>Reading <see cref="HasSupportedCmp"/> throws.</summary>
        public bool ThrowOnHasSupportedCmp;
        /// <summary><see cref="ShowCmpForExistingUser"/> throws (after being counted, before capturing).</summary>
        public bool ThrowOnShowCmp;
        public int CmpCalls;
        /// <summary>The completion callback the last <see cref="ShowCmpForExistingUser"/> captured; the test
        /// invokes it (null ⇒ success, a message ⇒ an error), as often as it likes.</summary>
        public Action<string> PendingCmp;
        /// <summary>Complete inside the call with <see cref="SyncCmpResult"/>, as the in-editor stub may.</summary>
        public bool CompleteCmpSynchronously;
        public string SyncCmpResult;

        /// <summary>What <see cref="InitializeAsync"/> returns. Null ⇒ a pending source the test completes.</summary>
        public Func<UniTask<bool>> InitBehaviour;
        public readonly UniTaskCompletionSource<bool> PendingInit = new UniTaskCompletionSource<bool>();

        public int InitCount;
        public int IsInitializedReads;
        /// <summary>Every bridge call by name, in order (reads of <see cref="IsInitialized"/> excluded).</summary>
        public readonly List<string> Calls = new List<string>();
        public readonly List<(string unit, bool rewarded)> IsReadyCalls = new List<(string, bool)>();
        public readonly List<(string unit, bool rewarded)> LoadCalls = new List<(string, bool)>();
        public readonly List<(string unit, bool rewarded)> ShowCalls = new List<(string, bool)>();
        public readonly List<string> ShowPlacements = new List<string>();
        public readonly List<bool> VerboseLoggingCalls = new List<bool>();
        public readonly List<string[]> TestDeviceIdCalls = new List<string[]>();

        private Action<string> _loaded;
        private Action<string, string> _loadFailed;
        private Action<string> _displayed;
        private Action<string> _hidden;
        private Action<string> _rewarded;
        private Action<string, string> _displayFailed;
        private Action<MaxRevenue> _revenuePaid;

        public int Subscriptions;
        public int Unsubscriptions;
        public int LiveSubscribers =>
            (_displayed?.GetInvocationList().Length ?? 0) + (_hidden?.GetInvocationList().Length ?? 0)
            + (_rewarded?.GetInvocationList().Length ?? 0) + (_displayFailed?.GetInvocationList().Length ?? 0);
        public int LoadSubscribers =>
            (_loaded?.GetInvocationList().Length ?? 0) + (_loadFailed?.GetInvocationList().Length ?? 0);
        /// <summary>Live subscribers of <see cref="OnRevenuePaid"/> — counted apart from the show path.</summary>
        public int RevenueSubscribers => _revenuePaid?.GetInvocationList().Length ?? 0;

        public bool IsInitialized
        {
            get
            {
                IsInitializedReads++;
                if (ThrowOnIsInitialized) throw new InvalidOperationException("bridge IsInitialized exploded");
                return Initialized;
            }
        }

        public UniTask<bool> InitializeAsync()
        {
            InitCount++;
            Calls.Add("InitializeAsync");
            return InitBehaviour != null ? InitBehaviour() : PendingInit.Task;
        }

        public void SetVerboseLogging(bool enabled)
        {
            Calls.Add("SetVerboseLogging");
            VerboseLoggingCalls.Add(enabled);
        }

        public void SetTestDeviceAdvertisingIds(string[] advertisingIds)
        {
            Calls.Add("SetTestDeviceAdvertisingIds");
            TestDeviceIdCalls.Add(advertisingIds);
        }

        /// <summary>Run inside <see cref="IsReady"/> — lets a test open another surface mid-check.</summary>
        public Action DuringIsReady;

        public bool IsReady(string adUnitId, bool rewarded)
        {
            Calls.Add("IsReady");
            IsReadyCalls.Add((adUnitId, rewarded));
            DuringIsReady?.Invoke();
            if (ThrowOnIsReady) throw new InvalidOperationException("bridge IsReady exploded");
            return Ready;
        }

        public void Load(string adUnitId, bool rewarded)
        {
            Calls.Add("Load");
            LoadCalls.Add((adUnitId, rewarded));
        }

        /// <summary>Run inside <see cref="Show"/>, before <see cref="ThrowOnShow"/> throws — lets a test start
        /// another surface while a show is mid-call.</summary>
        public Action BeforeShowReturns;

        public void Show(string adUnitId, bool rewarded, string placement)
        {
            Calls.Add("Show");
            ShowCalls.Add((adUnitId, rewarded));
            ShowPlacements.Add(placement);
            BeforeShowReturns?.Invoke();
            if (ThrowOnShow) throw new InvalidOperationException("bridge Show exploded");
        }

        public void ShowMediationDebugger()
        {
            Calls.Add("ShowMediationDebugger");
            MediationDebuggerCalls++;
            if (ThrowOnDebugger) throw new InvalidOperationException("bridge ShowMediationDebugger exploded");
        }

        public void ShowCreativeDebugger()
        {
            Calls.Add("ShowCreativeDebugger");
            CreativeDebuggerCalls++;
            if (ThrowOnDebugger) throw new InvalidOperationException("bridge ShowCreativeDebugger exploded");
        }

        /// <summary>Run inside <see cref="HasSupportedCmp"/> — lets a test open another surface mid-availability-read.</summary>
        public Action DuringHasSupportedCmp;

        public bool HasSupportedCmp
        {
            get
            {
                DuringHasSupportedCmp?.Invoke();
                if (ThrowOnHasSupportedCmp) throw new InvalidOperationException("bridge HasSupportedCmp exploded");
                return SupportedCmp;
            }
        }

        public void ShowCmpForExistingUser(Action<string> onCompleted)
        {
            Calls.Add("ShowCmpForExistingUser");
            CmpCalls++;
            if (ThrowOnShowCmp) throw new InvalidOperationException("bridge ShowCmpForExistingUser exploded");
            PendingCmp = onCompleted;
            if (CompleteCmpSynchronously) onCompleted(SyncCmpResult);
        }

        public event Action<string> OnLoaded { add { _loaded += value; } remove { _loaded -= value; } }
        public event Action<string, string> OnLoadFailed { add { _loadFailed += value; } remove { _loadFailed -= value; } }
        public event Action<string> OnDisplayed { add { Subscriptions++; _displayed += value; } remove { Unsubscriptions++; _displayed -= value; } }
        public event Action<string> OnHidden { add { Subscriptions++; _hidden += value; } remove { Unsubscriptions++; _hidden -= value; } }
        public event Action<string> OnRewarded { add { Subscriptions++; _rewarded += value; } remove { Unsubscriptions++; _rewarded -= value; } }
        public event Action<string, string> OnDisplayFailed { add { Subscriptions++; _displayFailed += value; } remove { Unsubscriptions++; _displayFailed -= value; } }

        public event Action<MaxRevenue> OnRevenuePaid { add { _revenuePaid += value; } remove { _revenuePaid -= value; } }

        public readonly List<string> BannerCalls = new List<string>();
        public bool ThrowOnBanner;
        private Action _initialized;
        public void ShowBanner(string adUnitId)
        {
            if (ThrowOnBanner) throw new InvalidOperationException("bridge ShowBanner exploded");
            BannerCalls.Add("show:" + adUnitId);
        }
        public void HideBanner(string adUnitId) => BannerCalls.Add("hide:" + adUnitId);
        public event Action OnInitialized { add { _initialized += value; } remove { _initialized -= value; } }
        public void RaiseInitialized() { Initialized = true; _initialized?.Invoke(); }

        public void RaiseLoaded(string unit) => _loaded?.Invoke(unit);
        public void RaiseRevenuePaid(MaxRevenue revenue) => _revenuePaid?.Invoke(revenue);
        public void RaiseLoadFailed(string unit, string message) => _loadFailed?.Invoke(unit, message);
        public void RaiseDisplayed(string unit) => _displayed?.Invoke(unit);
        public void RaiseHidden(string unit) => _hidden?.Invoke(unit);
        public void RaiseRewarded(string unit) => _rewarded?.Invoke(unit);
        public void RaiseDisplayFailed(string unit, string message) => _displayFailed?.Invoke(unit, message);
    }

    /// <summary>
    /// The injected delay, by hand: every call is recorded with its duration and token and stays pending
    /// until the test elapses it; cancelling the token cancels it, as a real delay does.
    /// </summary>
    internal sealed class FakeDelay
    {
        public sealed class Call
        {
            public TimeSpan Delay;
            public CancellationToken Token;
            public UniTaskCompletionSource Source;
            public bool IsPending => Source.Task.Status == UniTaskStatus.Pending;
            public bool IsCancelled => Source.Task.Status == UniTaskStatus.Canceled;
        }

        public readonly List<Call> Calls = new List<Call>();

        public Func<TimeSpan, CancellationToken, UniTask> Func => Invoke;

        public UniTask Invoke(TimeSpan delay, CancellationToken ct)
        {
            var call = new Call { Delay = delay, Token = ct, Source = new UniTaskCompletionSource() };
            Calls.Add(call);
            if (ct.IsCancellationRequested) call.Source.TrySetCanceled(ct);
            else ct.Register(() => call.Source.TrySetCanceled(ct));
            return call.Source.Task;
        }

        public double[] Seconds => Calls.Select(c => c.Delay.TotalSeconds).ToArray();

        public int Pending => Calls.Count(c => c.IsPending);

        public void ElapseLast() => Calls[Calls.Count - 1].Source.TrySetResult();
    }

    internal sealed class RecordingPublisher : IAdVisibilityChangedPublisher
    {
        public readonly List<AdVisibilityChanged> Published = new List<AdVisibilityChanged>();
        public void Publish(AdVisibilityChanged fact) => Published.Add(fact);
        public bool[] Fullscreen => Published.Select(p => p.FullscreenAdVisible).ToArray();
    }

    /// <summary>A publisher that throws on the values it is told to, and records the ones it accepts.</summary>
    internal sealed class ThrowingPublisher : IAdVisibilityChangedPublisher
    {
        public bool ThrowOnVisible = true;
        public bool ThrowOnHidden;
        public int Attempts;
        public readonly List<AdVisibilityChanged> Published = new List<AdVisibilityChanged>();

        public void Publish(AdVisibilityChanged fact)
        {
            Attempts++;
            if (fact.FullscreenAdVisible ? ThrowOnVisible : ThrowOnHidden)
                throw new InvalidOperationException("publisher exploded");
            Published.Add(fact);
        }

        public bool[] Fullscreen => Published.Select(p => p.FullscreenAdVisible).ToArray();
    }

    /// <summary>Records every <see cref="AdReadinessChanged"/> in order.</summary>
    internal sealed class RecordingReadinessPublisher : IAdReadinessChangedPublisher
    {
        public readonly List<AdReadinessChanged> Published = new List<AdReadinessChanged>();
        public void Publish(AdReadinessChanged fact) => Published.Add(fact);

        /// <summary>The facts as <c>(placement, ready)</c> pairs, for compact assertions.</summary>
        public (string, bool)[] Pairs => Published.Select(f => (f.Placement.Value, f.Ready)).ToArray();
    }

    /// <summary>An <see cref="IAdReadinessChangedPublisher"/> that throws on every publish and counts the attempts.</summary>
    internal sealed class ThrowingReadinessPublisher : IAdReadinessChangedPublisher
    {
        public int Attempts;
        public void Publish(AdReadinessChanged fact)
        {
            Attempts++;
            throw new InvalidOperationException("readiness publisher exploded");
        }
    }

    internal sealed class RecordingAnalytics : IAnalytics
    {
        public readonly List<GameEvent> Tracked = new List<GameEvent>();
        public void Track(GameEvent evt) => Tracked.Add(evt);
        public void SetUserProperty(UserPropKey prop, string value) { }
    }

    /// <summary>An <see cref="IAnalytics"/> whose <see cref="Track"/> always throws.</summary>
    internal sealed class ThrowingAnalytics : IAnalytics
    {
        public int TrackCalls;
        public void Track(GameEvent evt) { TrackCalls++; throw new InvalidOperationException("analytics exploded"); }
        public void SetUserProperty(UserPropKey prop, string value) { }
    }

    /// <summary>A settable <see cref="IConsentService"/> for driving a real <see cref="FanOutAnalytics"/>.</summary>
    internal sealed class FakeConsent : IConsentService
    {
        public bool Resolved;
        public readonly HashSet<ConsentPurpose> Granted = new HashSet<ConsentPurpose>();
        public bool IsResolved => Resolved;
        public bool IsGranted(ConsentPurpose purpose) => Resolved && Granted.Contains(purpose);
        public void ResolveInitial(IReadOnlyCollection<ConsentPurpose> grantedPurposes) => Set(grantedPurposes);
        public void ChangeConsent(IReadOnlyCollection<ConsentPurpose> grantedPurposes) => Set(grantedPurposes);

        private void Set(IReadOnlyCollection<ConsentPurpose> grantedPurposes)
        {
            Resolved = true;
            Granted.Clear();
            if (grantedPurposes != null) Granted.UnionWith(grantedPurposes);
        }
    }

    /// <summary>A provider behind <see cref="FanOutAnalytics"/> that counts what reaches it.</summary>
    internal sealed class CountingProvider : IAnalyticsProvider
    {
        public readonly List<GameEvent> Sent = new List<GameEvent>();
        public void OnAnalyticsConsent(bool granted) { }
        public void Send(GameEvent evt) => Sent.Add(evt);
        public void SetUserProperty(UserPropKey prop, string value) { }
    }

    /// <summary>An <see cref="IAnalytics"/> that counts calls and forwards them to an inner sink.</summary>
    internal sealed class CountingAnalytics : IAnalytics
    {
        private readonly IAnalytics _inner;
        public int TrackCalls;
        public CountingAnalytics(IAnalytics inner) { _inner = inner; }
        public void Track(GameEvent evt) { TrackCalls++; _inner.Track(evt); }
        public void SetUserProperty(UserPropKey prop, string value) => _inner.SetUserProperty(prop, value);
    }

    /// <summary>An <see cref="IGameConfig"/> whose every read throws.</summary>
    internal sealed class ThrowingConfig : IGameConfig
    {
        public T Get<T>(ConfigKey<T> key) => throw new InvalidOperationException("config exploded");
        public UniTask RefreshRemoteAsync(CancellationToken ct) => UniTask.CompletedTask;
        public IObservable<Unit> OnChanged => throw new InvalidOperationException("config exploded");
    }

    internal sealed class RecordingLog : ILog
    {
        public readonly List<string> Infos = new List<string>();
        public readonly List<string> Warns = new List<string>();
        public readonly List<string> Errors = new List<string>();
        public void Info(string message) => Infos.Add(message);
        public void Warn(string message) => Warns.Add(message);
        public void Error(string message) => Errors.Add(message);
    }

    /// <summary>A dictionary-backed <see cref="IGameConfig"/> honouring the port's "absent ⇒ default" contract.
    /// <see cref="OnChanged"/> fires only when a test calls <see cref="RaiseChanged"/> (a <see cref="Set"/>
    /// alone raises nothing, as a layered config applies before it signals).</summary>
    internal sealed class DictConfig : IGameConfig
    {
        private readonly Dictionary<string, object> _values = new Dictionary<string, object>();
        private readonly ObservableSource<Unit> _changed = new ObservableSource<Unit>();

        public DictConfig Set(string key, object value)
        {
            _values[key] = value;
            return this;
        }

        /// <summary>Make <paramref name="key"/> absent again.</summary>
        public DictConfig Remove(string key)
        {
            _values.Remove(key);
            return this;
        }

        public T Get<T>(ConfigKey<T> key) => _values.TryGetValue(key.Value, out var v) ? (T)v : default;
        public UniTask RefreshRemoteAsync(CancellationToken ct) => UniTask.CompletedTask;
        public IObservable<Unit> OnChanged => _changed;

        /// <summary>Signal a config change to every subscriber.</summary>
        public void RaiseChanged() => _changed.Publish(Unit.Default);
    }

    /// <summary>An <see cref="IGameConfig"/> that counts every read and forwards it to an inner config.</summary>
    internal sealed class CountingConfig : IGameConfig
    {
        private readonly IGameConfig _inner;
        public int Reads;
        public CountingConfig(IGameConfig inner) { _inner = inner; }
        public T Get<T>(ConfigKey<T> key) { Reads++; return _inner.Get(key); }
        public UniTask RefreshRemoteAsync(CancellationToken ct) => _inner.RefreshRemoteAsync(ct);
        public IObservable<Unit> OnChanged => _inner.OnChanged;
    }

    /// <summary>A minimal boot node for real-<see cref="BootFlow"/> graphs around the ads node.</summary>
    internal sealed class StubBootNode : IBootNode
    {
        public string Id { get; set; } = "Stub";
        public IReadOnlyList<BootCap> Requires { get; set; } = Array.Empty<BootCap>();
        public IReadOnlyList<BootCap> Provides { get; set; } = Array.Empty<BootCap>();
        public bool Required { get; set; }
        public TimeSpan SoftTimeout => TimeSpan.Zero;
        public TimeSpan HardTimeout => TimeSpan.Zero;
        public Func<BootContext, bool> Predicate => null;
        public string ExclusiveGroup => "";
        public float Weight => 1f;
        public int RunCount;
        public bool Completed;

        public UniTask<BootNodeResult> RunAsync(BootContext ctx, CancellationToken ct)
        {
            RunCount++;
            Completed = true;
            return UniTask.FromResult(BootNodeResult.Succeeded);
        }
    }

    internal static class TestSupport
    {
        public static async Task SpinUntil(Func<bool> cond, int timeoutMs = 3000)
        {
            var waited = 0;
            while (!cond() && waited < timeoutMs) { await Task.Delay(15); waited += 15; }
        }

        /// <summary>The module package root (baked in by the csproj).</summary>
        public static string ModuleRoot => Metadata("PfModuleRoot");

        /// <summary>The framework package root the Domain/Application sources were linked from.</summary>
        public static string FrameworkRoot => Metadata("PfFrameworkPackage");

        private static string Metadata(string key)
        {
            var value = typeof(TestSupport).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                .Single(a => a.Key == key).Value;
            return System.IO.Path.GetFullPath(value);
        }
    }
}
