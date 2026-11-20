using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using ClassicSpins.PrototypeFramework.Analytics.Firebase.Core;
using NUnit.Framework;

namespace ClassicSpins.PrototypeFramework.Analytics.Firebase.HeadlessTests
{
    [TestFixture]
    public sealed class FirebaseAnalyticsBuildRulesTests
    {
        private const string Manifest = "<manifest xmlns:android='http://schemas.android.com/apk/res/android'><application /></manifest>";
        private const string Plist = "<?xml version='1.0'?><!DOCTYPE plist PUBLIC '-//Apple//DTD PLIST 1.0//EN' 'http://www.apple.com/DTDs/PropertyList-1.0.dtd'><plist version='1.0'><dict><key>CFBundleName</key><string>Game</string></dict></plist>";
        private static string AndroidFlag(string value, string key = FirebaseAnalyticsBuildRules.AndroidCollectionKey)
            => "<manifest xmlns:android='http://schemas.android.com/apk/res/android'><application><meta-data android:name='" + key + "' android:value='" + value + "'/></application></manifest>";
        private static string IosFlag(string value, string key = FirebaseAnalyticsBuildRules.IosCollectionKey)
            => "<plist><dict><key>" + key + "</key>" + value + "</dict></plist>";
        private static FirebasePreflightFacts Safe(FirebaseBuildTarget target) => new FirebasePreflightFacts { InPlay = true, DefineArmed = true, Target = target, ServiceFilePresent = true, AndroidMinSdk = 24 };

        [TestCase(FirebaseBuildTarget.Android)] [TestCase(FirebaseBuildTarget.Ios)]
        public void Safe_mobile_facts_pass_and_missing_service_file_fails_with_platform_name(FirebaseBuildTarget target)
        {
            var facts = Safe(target); Assert.That(FirebaseAnalyticsBuildRules.Evaluate(facts), Is.Empty);
            facts.ServiceFilePresent = false;
            var problems = FirebaseAnalyticsBuildRules.Evaluate(facts);
            Assert.That(problems.Single(), Does.Contain(target == FirebaseBuildTarget.Android ? "google-services.json" : "GoogleService-Info.plist"));
        }
        [TestCase(0)] [TestCase(21)] [TestCase(23)]
        public void Android_minSdk_below_24_fails(int minimum)
        {
            var facts = Safe(FirebaseBuildTarget.Android); facts.AndroidMinSdk = minimum;
            Assert.That(FirebaseAnalyticsBuildRules.Evaluate(facts).Single(), Does.Contain("minSdk").And.Contain("24"));
        }
        [TestCase(24)] [TestCase(35)]
        public void Android_minSdk_floor_or_above_passes(int minimum)
        {
            var facts = Safe(FirebaseBuildTarget.Android); facts.AndroidMinSdk = minimum;
            Assert.That(FirebaseAnalyticsBuildRules.Evaluate(facts), Is.Empty);
        }
        [Test] public void Package_is_silent_when_unused_and_nonmobile_needs_no_service_file()
        {
            Assert.That(FirebaseAnalyticsBuildRules.Evaluate(new FirebasePreflightFacts()), Is.Empty);
            var facts = Safe(FirebaseBuildTarget.Other); facts.ServiceFilePresent = false; facts.AndroidMinSdk = 1;
            Assert.That(FirebaseAnalyticsBuildRules.Evaluate(facts), Is.Empty);
            facts.DefineArmed = false; Assert.That(FirebaseAnalyticsBuildRules.Evaluate(facts).Single(), Does.Contain("PF_FIREBASE"));
        }
        [Test] public void Preflight_aggregates_all_unsafe_facts_with_paths_and_never_mutates_sources()
        {
            var facts = Safe(FirebaseBuildTarget.Android); facts.ServiceFilePresent = false; facts.AndroidMinSdk = 23;
            var source = AndroidFlag("true"); facts.NativeFiles.Add(new KeyValuePair<string, string>("Assets/Plugins/Android/AndroidManifest.xml", source));
            var problems = FirebaseAnalyticsBuildRules.Evaluate(facts);
            Assert.That(problems, Has.Count.EqualTo(3));
            Assert.That(problems.Last(), Does.Contain("Assets/Plugins/Android/AndroidManifest.xml").And.Contain("existing='true'").And.Contain("required='false'"));
            Assert.That(facts.NativeFiles[0].Value, Is.EqualTo(source));
        }
        [Test] public void Android_missing_default_is_added_under_application_and_repeat_is_byte_stable()
        {
            var once = FirebaseAnalyticsBuildRules.AndroidManifest(Manifest);
            Assert.That(once.Problems, Is.Empty); Assert.That(once.Changed, Is.True);
            var flag = XDocument.Parse(once.Content).Root.Element("application").Element("meta-data");
            XNamespace android = "http://schemas.android.com/apk/res/android";
            Assert.That((string)flag.Attribute(android + "name"), Is.EqualTo(FirebaseAnalyticsBuildRules.AndroidCollectionKey));
            Assert.That((string)flag.Attribute(android + "value"), Is.EqualTo("false"));
            var twice = FirebaseAnalyticsBuildRules.AndroidManifest(once.Content);
            Assert.That(twice.Changed, Is.False); Assert.That(twice.Content, Is.EqualTo(once.Content));
        }
        [Test] public void Permission_only_library_manifest_can_receive_a_default_application_section()
        {
            var once = FirebaseAnalyticsBuildRules.AndroidManifest("<manifest><uses-permission /></manifest>");
            Assert.That(once.Problems, Is.Empty); Assert.That(once.Changed, Is.True);
            Assert.That(XDocument.Parse(once.Content).Root.Element("uses-permission"), Is.Not.Null);
            Assert.That(XDocument.Parse(once.Content).Root.Element("application").Element("meta-data"), Is.Not.Null);
        }
        [Test] public void Ios_missing_default_is_added_as_boolean_NO_and_unrelated_keys_and_doctype_survive()
        {
            var once = FirebaseAnalyticsBuildRules.IosPlist(Plist);
            Assert.That(once.Problems, Is.Empty); Assert.That(once.Changed, Is.True);
            var doc = XDocument.Parse(once.Content);
            Assert.That(doc.DocumentType, Is.Not.Null);
            var keys = doc.Root.Element("dict").Elements("key").ToArray();
            Assert.That(keys[0].ElementsAfterSelf().First().Value, Is.EqualTo("Game"));
            Assert.That(keys.Last().Value, Is.EqualTo(FirebaseAnalyticsBuildRules.IosCollectionKey));
            Assert.That(keys.Last().ElementsAfterSelf().First().Name.LocalName, Is.EqualTo("false"));
            var twice = FirebaseAnalyticsBuildRules.IosPlist(once.Content);
            Assert.That(twice.Changed, Is.False); Assert.That(twice.Content, Is.EqualTo(once.Content));
        }
        [TestCase("true")] [TestCase("TRUE")] [TestCase("@bool/collection")]
        public void Android_conflicts_are_preserved_diagnosed_with_both_values_and_not_partially_written(string value)
        {
            var source = AndroidFlag(value); var result = FirebaseAnalyticsBuildRules.AndroidManifest(source);
            Assert.That(result.Content, Is.EqualTo(source)); Assert.That(result.Changed, Is.False);
            Assert.That(result.Problems.Single(), Does.Contain(FirebaseAnalyticsBuildRules.ModuleId).And.Contain(value).And.Contain("false").And.Contain("preserved"));
        }
        [TestCase("<true/>")] [TestCase("<string>YES</string>")] [TestCase("<integer>1</integer>")]
        public void Ios_conflicts_are_preserved_and_diagnosed(string value)
        {
            var source = IosFlag(value); var result = FirebaseAnalyticsBuildRules.IosPlist(source);
            Assert.That(result.Content, Is.EqualTo(source)); Assert.That(result.Changed, Is.False);
            Assert.That(result.Problems.Single(), Does.Contain(FirebaseAnalyticsBuildRules.ModuleId).And.Contain(XElement.Parse(value).ToString(SaveOptions.DisableFormatting)).And.Contain("NO"));
            var facts = Safe(FirebaseBuildTarget.Ios); facts.NativeFiles.Add(new KeyValuePair<string, string>("Info.plist", source));
            Assert.That(FirebaseAnalyticsBuildRules.Evaluate(facts).Single(), Does.Contain("Info.plist"));
        }
        [Test] public void Existing_safe_flags_are_preserved_byte_for_byte()
        {
            var android = AndroidFlag("false"); var ios = IosFlag("<false/>");
            Assert.That(FirebaseAnalyticsBuildRules.AndroidManifest(android).Content, Is.EqualTo(android));
            Assert.That(FirebaseAnalyticsBuildRules.IosPlist(ios).Content, Is.EqualTo(ios));
        }
        [Test] public void Permanent_deactivation_is_refused_to_avoid_sdk_that_can_never_enable_collection()
        {
            var android = AndroidFlag("true", "firebase_analytics_collection_deactivated");
            var ios = IosFlag("<true/>", "FIREBASE_ANALYTICS_COLLECTION_DEACTIVATED");
            Assert.That(FirebaseAnalyticsBuildRules.AndroidManifest(android).Problems.Single(), Does.Contain("deactivated"));
            Assert.That(FirebaseAnalyticsBuildRules.IosPlist(ios).Problems.Single(), Does.Contain("DEACTIVATED"));
            Assert.That(FirebaseAnalyticsBuildRules.AndroidManifest(android).Content, Is.EqualTo(android));
            Assert.That(FirebaseAnalyticsBuildRules.IosPlist(ios).Content, Is.EqualTo(ios));
        }
        [Test] public void Resource_backed_and_duplicate_collection_flags_are_conservatively_refused()
        {
            var resource = "<manifest xmlns:android='http://schemas.android.com/apk/res/android'><application><meta-data android:name='firebase_analytics_collection_enabled' android:resource='@bool/on'/></application></manifest>";
            Assert.That(FirebaseAnalyticsBuildRules.AndroidManifest(resource).Problems, Is.Not.Empty);
            var duplicate = AndroidFlag("false").Replace("</application>", "<meta-data android:name='firebase_analytics_collection_enabled' android:value='false'/></application>");
            Assert.That(FirebaseAnalyticsBuildRules.AndroidManifest(duplicate).Problems.Single(), Does.Contain("duplicate"));
            var ios = IosFlag("<false/>").Replace("</dict>", "<key>FIREBASE_ANALYTICS_COLLECTION_ENABLED</key><false/></dict>");
            Assert.That(FirebaseAnalyticsBuildRules.IosPlist(ios).Problems.Single(), Does.Contain("duplicate"));
        }
        [TestCase("<invalid/>")] [TestCase("bad xml")]
        public void Unreadable_or_incomplete_native_sources_fail_preflight_by_name(string source)
        {
            var facts = Safe(FirebaseBuildTarget.Android); facts.NativeFiles.Add(new KeyValuePair<string, string>("bad.xml", source));
            Assert.That(FirebaseAnalyticsBuildRules.Evaluate(facts).Single(), Does.Contain("bad.xml").And.Contain("cannot inspect"));
        }
    }
}
