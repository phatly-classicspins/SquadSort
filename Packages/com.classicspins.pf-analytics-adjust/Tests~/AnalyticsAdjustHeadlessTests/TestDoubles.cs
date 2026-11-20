using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using ClassicSpins.PrototypeFramework.Application;
using ClassicSpins.PrototypeFramework.Domain;
using ClassicSpins.PrototypeFramework.Analytics.Adjust.Core;

namespace ClassicSpins.PrototypeFramework.Analytics.Adjust.HeadlessTests
{
    internal sealed class TestLog : ILog
    {
        public readonly List<string> Warnings = new List<string>();
        public bool Throws;
        public void Info(string message) { }
        public void Error(string message) { }
        public void Warn(string message) { if (Throws) throw new Exception("log defect"); Warnings.Add(message); }
    }
    internal sealed class TestConfig : IGameConfig
    {
        public readonly Dictionary<string, object> Values = new Dictionary<string, object> { ["analytics.adjust.appToken"] = "abcdefghijkl" };
        public readonly List<(string Key, Type Type)> Reads = new List<(string, Type)>();
        public bool Throws;
        public Action<string> DuringGet;
        public T Get<T>(ConfigKey<T> key)
        {
            Reads.Add((key.Value, typeof(T)));
            DuringGet?.Invoke(key.Value);
            if (Throws) throw new Exception("config defect");
            return Values.TryGetValue(key.Value, out var v) && v is T value ? value : default(T);
        }
        public UniTask RefreshRemoteAsync(CancellationToken ct) => UniTask.CompletedTask;
        public IObservable<Unit> OnChanged => null;
    }
    internal sealed class TestConsent : IConsentService
    {
        public bool Resolved = true, Granted = true, Throws;
        public bool IsResolved { get { if (Throws) throw new Exception("consent defect"); return Resolved; } }
        public bool IsGranted(ConsentPurpose purpose) => Granted;
        public void ResolveInitial(IReadOnlyCollection<ConsentPurpose> grantedPurposes) { }
        public void ChangeConsent(IReadOnlyCollection<ConsentPurpose> grantedPurposes) { }
    }
    // Simulation only: a shared fake enabled bit models persistence between provider instances.
    // It makes no claim about Adjust native consent/forget state or acceptance across instances.
    internal sealed class TestNativeState { public bool Enabled = true; }
    internal sealed class TestBridge : IAdjustAnalyticsBridge
    {
        public readonly List<string> Calls = new List<string>();
        public readonly List<(string Token, Dictionary<string, string> Params)> Events = new List<(string, Dictionary<string, string>)>();
        public readonly List<(string Source, double Amount, string Currency, string Network, string Unit, string Placement)> Revenues
            = new List<(string, double, string, string, string, string)>();
        public bool Refuses;
        public string ThrowOn, RefuseOn;
        public TestNativeState State = new TestNativeState();
        public Action<bool> DuringEnabled;
        public Action DuringStart, DuringSignals;
        private bool Call(string name) { Calls.Add(name); if (name == ThrowOn) throw new Exception("bridge defect"); return !Refuses && name != RefuseOn; }
        public bool ConfigureAdSignals(bool granted) { var result = Call("signals:" + granted); DuringSignals?.Invoke(); return result; }
        public bool Start(string token, string environment) { var result = Call("start:" + token + ":" + environment); DuringStart?.Invoke(); return result; }
        public bool SetEnabled(bool enabled)
        {
            var result = Call("enabled:" + enabled);
            DuringEnabled?.Invoke(enabled);
            // Deliberately settle after reentry, like an outer SDK call finishing after a callback.
            if (result) State.Enabled = enabled;
            return result;
        }
        public bool TrackToken(string token, IReadOnlyList<KeyValuePair<string, string>> parameters)
        { var result = Call("event:" + token) && State.Enabled; if (result) Events.Add((token, new Dictionary<string, string>(parameters))); return result; }
        public bool TrackRevenue(string source, double amount, string currency, string network, string unit, string placement)
        { var result = Call("revenue") && State.Enabled; if (result) Revenues.Add((source, amount, currency, network, unit, placement)); return result; }
        public bool AddCallbackParameter(string key, string value) => Call("callback:" + key + ":" + value);
        public bool AddPartnerParameter(string key, string value) => Call("partner:" + key + ":" + value);
        public bool ForgetMe() => Call("forget");
    }
    internal static class TestPaths
    {
        internal static string Root => Attribute("PfModuleRoot");
        internal static string Framework => Attribute("PfFrameworkPackage");
        private static string Attribute(string key)
        {
            foreach (var attr in typeof(TestPaths).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>())
                if (attr.Key == key) return attr.Value;
            throw new InvalidOperationException("Missing assembly metadata " + key);
        }
    }
}
