using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using ClassicSpins.PrototypeFramework.Application;
using ClassicSpins.PrototypeFramework.Domain;
using ClassicSpins.PrototypeFramework.Analytics.Firebase.Core;

namespace ClassicSpins.PrototypeFramework.Analytics.Firebase.HeadlessTests
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
        public object AdSignals;
        public bool Throws;
        public readonly List<Type> ReadTypes = new List<Type>();
        public readonly List<string> Keys = new List<string>();
        public T Get<T>(ConfigKey<T> key)
        {
            Keys.Add(key.Value); ReadTypes.Add(typeof(T));
            if (Throws) throw new Exception("config defect");
            return AdSignals is T value ? value : default(T);
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
    internal sealed class TestBridge : IFirebaseAnalyticsBridge
    {
        public readonly List<string> Calls = new List<string>();
        public readonly List<(string Name, Dictionary<string, object> Params)> Events = new List<(string, Dictionary<string, object>)>();
        public readonly List<(string Name, string Value)> Properties = new List<(string, string)>();
        public readonly List<string> UserIds = new List<string>();
        public Action<bool, string> Completion;
        public bool Throws, Refuses, CompleteImmediately;
        public string ThrowOn;
        public int Starts;
        private bool Call(string name) { Calls.Add(name); if (Throws || name == ThrowOn) throw new Exception("bridge defect: " + name); return !Refuses; }
        public void Start(Action<bool, string> completed)
        {
            Starts++;
            Call("start");
            Completion = completed;
            if (CompleteImmediately) Complete();
        }
        public void Complete(bool ok = true) => Completion(ok, ok ? null : "dependency failure");
        public bool SetConsent(bool analyticsStorage, bool adSignals) => Call("consent:" + analyticsStorage + ":" + adSignals);
        public bool SetCollectionEnabled(bool enabled) => Call("collection:" + enabled);
        public bool LogEvent(string name, IReadOnlyList<KeyValuePair<string, object>> parameters)
        {
            var accepted = Call("event:" + name);
            Events.Add((name, new Dictionary<string, object>(parameters)));
            return accepted;
        }
        public bool SetUserProperty(string name, string value) { var accepted = Call("property:" + name); Properties.Add((name, value)); return accepted; }
        public bool SetUserId(string id) { var accepted = Call("user:" + id); UserIds.Add(id); return accepted; }
        public bool ResetAnalyticsData() => Call("reset");
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
