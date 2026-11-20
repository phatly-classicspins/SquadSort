using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClassicSpins.PrototypeFramework.Analytics.Firebase.Core;
using ClassicSpins.PrototypeFramework.Application;
using ClassicSpins.PrototypeFramework.Domain;
using NUnit.Framework;

namespace ClassicSpins.PrototypeFramework.Analytics.Firebase.HeadlessTests
{
    [TestFixture]
    public sealed class ReviewRegressionTests
    {
        private TestBridge _bridge;
        private TestConsent _consent;
        private TestLog _log;
        private FirebaseAnalyticsProvider _provider;
        [SetUp] public void Setup()
        {
            _bridge = new TestBridge(); _consent = new TestConsent(); _log = new TestLog();
            _provider = new FirebaseAnalyticsProvider(_bridge, new TestConfig(), _consent, _log, a => a());
        }
        private void Begin() => _provider.Send(new AdShown("valid"));
        private sealed record BrokenFact(bool ThrowName) : GameEvent
        {
            public override string WireName() => ThrowName ? throw new InvalidOperationException("name defect") : "broken_fact";
            public override void WriteParams(IEventParamWriter w)
            {
                w.Write("partial", "must never send");
                throw new InvalidOperationException("params defect");
            }
        }
        [TestCase(true)] [TestCase(false)]
        public void Custom_serialization_failure_drops_only_that_event_and_keeps_pending_start_alive(bool throwName)
        {
            Begin();
            Assert.DoesNotThrow(() => _provider.Send(new BrokenFact(throwName)));
            _provider.Send(new AdShown("also-valid"));
            _bridge.Complete();
            Assert.That(_bridge.Events.Select(e => (string)e.Params["placement"]), Is.EqualTo(new[] { "valid", "also-valid" }));
            Assert.That(_bridge.Starts, Is.EqualTo(1));
            Assert.That(_log.Warnings.Single(), Does.Contain("serialization failed"));
        }
        [TestCase(false)] [TestCase(true)]
        public void Late_completion_diagnoses_refused_and_throwing_collection_off_without_leaking(bool throws)
        {
            Begin(); _consent.Granted = false; _provider.OnAnalyticsConsent(false);
            if (throws) _bridge.ThrowOn = "collection:False";
            else _bridge.Refuses = true;
            Assert.DoesNotThrow(() => _bridge.Complete());
            Assert.That(_bridge.Events, Is.Empty);
            Assert.That(_bridge.Calls.Last(), Is.EqualTo("collection:False"));
            Assert.That(_log.Warnings.Any(w => w.Contains("withdrawal collection-off") && w.Contains(throws ? "failed" : "refused")), Is.True);
        }
        [TestCase("user:")] [TestCase("reset")]
        public void Privacy_attempts_both_independent_operations_even_when_either_one_throws(string throwsOn)
        {
            Begin(); _bridge.Complete(); _bridge.ThrowOn = throwsOn;
            Assert.That(_provider.Execute("privacy.forget_me", null), Is.False);
            Assert.That(_bridge.Calls.TakeLast(2), Is.EqualTo(new[] { "user:", "reset" }));
            Assert.That(_log.Warnings.Any(w => w.Contains("SDK work failed")), Is.True);
        }
        [TestCase(null, true)] [TestCase("0", false)] [TestCase(0, false)] [TestCase(0L, false)]
        [TestCase(0d, false)] [TestCase(0f, false)] [TestCase("1", true)] [TestCase(1, true)]
        public void Actual_GameConfigCore_preserves_absence_and_denies_numeric_or_text_zero(object setting, bool grantsAdSignals)
        {
            var defaults = new Dictionary<string, object>();
            if (setting != null) defaults["analytics.firebase.adSignals"] = setting;
            var config = InMemoryConfig.Create(defaults);
            _provider = new FirebaseAnalyticsProvider(_bridge, config, _consent, _log, a => a());
            Begin(); _bridge.Complete();
            Assert.That(_bridge.Calls[1], Is.EqualTo("consent:True:" + grantsAdSignals));
        }
        [Test] public void Config_double_mirrors_exact_typed_Get_and_provider_requests_object()
        {
            var config = new TestConfig { AdSignals = 0 };
            Assert.That(config.Get(new ConfigKey<string>("analytics.firebase.adSignals")), Is.Null);
            Assert.That(config.Get(new ConfigKey<object>("analytics.firebase.adSignals")), Is.EqualTo(0));
            config.ReadTypes.Clear();
            _provider = new FirebaseAnalyticsProvider(_bridge, config, _consent, _log, a => a());
            Begin(); _bridge.Complete();
            Assert.That(config.ReadTypes, Is.EqualTo(new[] { typeof(object) }));
            Assert.That(_bridge.Calls[1], Is.EqualTo("consent:True:False"));
        }

        [TestCase("Assets/Backup~/google-services.json")] [TestCase("Assets/.backup/google-services.json")]
        [TestCase("Assets/CVS/google-services.json")] [TestCase("Assets/archive.tmp/google-services.json")]
        [TestCase("Assets/../google-services.json")] [TestCase("backup/Assets/google-services.json")]
        public void Ignored_backups_cannot_count_as_service_or_native_inputs(string path)
        {
            Assert.That(FirebaseAnalyticsBuildRules.IsServiceFile(path, FirebaseBuildTarget.Android), Is.False);
            Assert.That(FirebaseAnalyticsBuildRules.IsNativeFileCandidate(path.Replace("google-services.json", "AndroidManifest.xml"), FirebaseBuildTarget.Android), Is.False);
            Assert.That(FirebaseAnalyticsBuildRules.IsServiceFile(path.Replace("google-services.json", "GoogleService-Info.plist"), FirebaseBuildTarget.Ios), Is.False);
        }
        [Test] public void Imported_service_files_are_selected_only_for_the_owning_platform()
        {
            Assert.That(FirebaseAnalyticsBuildRules.IsServiceFile("Assets/Config/google-services.json", FirebaseBuildTarget.Android), Is.True);
            Assert.That(FirebaseAnalyticsBuildRules.IsServiceFile("Assets/Config/GoogleService-Info.plist", FirebaseBuildTarget.Ios), Is.True);
            Assert.That(FirebaseAnalyticsBuildRules.IsServiceFile("Assets/Config/google-services.json", FirebaseBuildTarget.Ios), Is.False);
            Assert.That(FirebaseAnalyticsBuildRules.IsCollectionConfiguration("Assets/.backup/Info.plist", "bad", FirebaseBuildTarget.Ios), Is.False);
        }
        [TestCase("<plist><array><string>resource</string></array></plist>")] [TestCase("bplist00binary-resource")]
        public void Unrelated_array_and_binary_plist_resources_are_not_application_configuration(string source)
        {
            Assert.That(FirebaseAnalyticsBuildRules.IsCollectionConfiguration("Assets/Resources/Resource.plist", source, FirebaseBuildTarget.Ios), Is.False);
            Assert.That(FirebaseAnalyticsBuildRules.IsCollectionConfiguration("Assets/Plugins/iOS/Info.plist", source, FirebaseBuildTarget.Ios), Is.True);
        }
        [Test] public void A_custom_plist_containing_collection_keys_is_relevant_even_without_Info_filename()
        {
            Assert.That(FirebaseAnalyticsBuildRules.IsCollectionConfiguration("Assets/Plugins/iOS/Custom.plist",
                "<plist><dict><key>FIREBASE_ANALYTICS_COLLECTION_ENABLED</key><true/></dict></plist>", FirebaseBuildTarget.Ios), Is.True);
        }
        [TestCase("<notplist><dict/></notplist>")]
        [TestCase("<plist><array/></plist>")]
        [TestCase("<plist><dict/><dict/></plist>")]
        [TestCase("<plist><dict><key>FIREBASE_ANALYTICS_COLLECTION_ENABLED</key><false>text</false></dict></plist>")]
        [TestCase("<plist><dict><key>FIREBASE_ANALYTICS_COLLECTION_ENABLED</key><false><child/></false></dict></plist>")]
        [TestCase("<plist><dict><key>FIREBASE_ANALYTICS_COLLECTION_ENABLED</key><false invalid='attribute'/></dict></plist>")]
        public void Malformed_application_plists_are_refused_and_preserved(string source)
        {
            var result = FirebaseAnalyticsBuildRules.IosPlist(source);
            Assert.That(result.Problems, Is.Not.Empty); Assert.That(result.Changed, Is.False); Assert.That(result.Content, Is.EqualTo(source));
        }
        [TestCase("", "tools:node='remove'", "android:name='firebase_analytics_collection_enabled' android:value='false'")]
        [TestCase("", "tools:node='removeAll'", "")]
        [TestCase("tools:node='remove'", "", "android:name='firebase_analytics_collection_enabled' android:value='false'")]
        [TestCase("tools:node='removeAll'", "", "android:name='firebase_analytics_collection_enabled' android:value='false'")]
        [TestCase("", "tools:remove='android:value'", "android:name='firebase_analytics_collection_enabled' android:value='false'")]
        [TestCase("", "tools:remove='other:name'", "android:name='firebase_analytics_collection_enabled' android:value='false'")]
        [TestCase("tools:remove='*'", "", "android:name='firebase_analytics_collection_enabled' android:value='false'")]
        public void Android_merge_removal_directives_cannot_remove_collection_off_defaults(string applicationDirective, string metadataDirective, string metadata)
        {
            var source = "<manifest xmlns:android='http://schemas.android.com/apk/res/android' xmlns:other='http://schemas.android.com/apk/res/android' xmlns:tools='http://schemas.android.com/tools'><application "
                + applicationDirective + "><meta-data " + metadata + " " + metadataDirective + "/></application></manifest>";
            var result = FirebaseAnalyticsBuildRules.AndroidManifest(source);
            Assert.That(result.Problems.Any(p => p.Contains("tools:") && p.Contains("preserved")), Is.True);
            Assert.That(result.Content, Is.EqualTo(source)); Assert.That(result.Changed, Is.False);
        }
    }

    [TestFixture]
    public sealed class FirebaseExportFileTests
    {
        private string _root;
        private const string Manifest = "<manifest><application/></manifest>";
        private const string Plist = "<plist><dict/></plist>";
        private string Library => Path.Combine(_root, "unityLibrary");
        private string LibraryManifest => Path.Combine(Library, "src/main/AndroidManifest.xml");
        private string LauncherManifest => Path.Combine(_root, "launcher/src/main/AndroidManifest.xml");
        [SetUp] public void Setup() { _root = Path.Combine(Path.GetTempPath(), "pf-firebase-export-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(_root); }
        [TearDown] public void Cleanup() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
        private static void Write(string path, string content) { Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path, content); }
        [Test] public void Real_file_orchestration_updates_unityLibrary_and_launcher_and_repeats_without_changing_bytes()
        {
            Write(LibraryManifest, Manifest); Write(LauncherManifest, Manifest);
            Assert.That(FirebaseAnalyticsExport.Android(Library), Is.Empty);
            var library = File.ReadAllBytes(LibraryManifest); var launcher = File.ReadAllBytes(LauncherManifest);
            Assert.That(File.ReadAllText(LibraryManifest), Does.Contain(FirebaseAnalyticsBuildRules.AndroidCollectionKey));
            Assert.That(File.ReadAllText(LauncherManifest), Does.Contain(FirebaseAnalyticsBuildRules.AndroidCollectionKey));
            Assert.That(FirebaseAnalyticsExport.Android(Library), Is.Empty);
            Assert.That(File.ReadAllBytes(LibraryManifest), Is.EqualTo(library)); Assert.That(File.ReadAllBytes(LauncherManifest), Is.EqualTo(launcher));
        }
        [Test] public void Android_export_supports_absent_launcher_but_requires_library_manifest()
        {
            Assert.That(FirebaseAnalyticsExport.Android(Library).Single(), Does.Contain("AndroidManifest.xml"));
            Write(LibraryManifest, Manifest); Assert.That(FirebaseAnalyticsExport.Android(Library), Is.Empty);
            Assert.That(File.Exists(LauncherManifest), Is.False);
        }
        [Test] public void Launcher_conflict_refuses_entire_export_and_preserves_original_conflicting_bytes()
        {
            Write(LibraryManifest, Manifest);
            Write(LauncherManifest, "<manifest xmlns:android='http://schemas.android.com/apk/res/android'><application><meta-data android:name='firebase_analytics_collection_enabled' android:value='true'/></application></manifest>");
            var library = File.ReadAllBytes(LibraryManifest); var launcher = File.ReadAllBytes(LauncherManifest);
            var problems = FirebaseAnalyticsExport.Android(Library);
            Assert.That(problems.Single(), Does.Contain(LauncherManifest).And.Contain("existing='true'").And.Contain("required='false'"));
            Assert.That(File.ReadAllBytes(LibraryManifest), Is.EqualTo(library)); Assert.That(File.ReadAllBytes(LauncherManifest), Is.EqualTo(launcher));
        }
        [Test] public void Ios_file_orchestration_is_idempotent_and_missing_or_conflicting_Info_plist_fails_without_rewrite()
        {
            var path = Path.Combine(_root, "Info.plist");
            Assert.That(FirebaseAnalyticsExport.Ios(_root).Single(), Does.Contain("Info.plist"));
            Write(path, Plist); Assert.That(FirebaseAnalyticsExport.Ios(_root), Is.Empty);
            var safe = File.ReadAllBytes(path); Assert.That(FirebaseAnalyticsExport.Ios(_root), Is.Empty);
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(safe));
            Write(path, "<plist><dict><key>FIREBASE_ANALYTICS_COLLECTION_ENABLED</key><true/></dict></plist>");
            var conflict = File.ReadAllBytes(path);
            Assert.That(FirebaseAnalyticsExport.Ios(_root).Single(), Does.Contain("Info.plist").And.Contain("NO"));
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(conflict));
        }
    }
}
