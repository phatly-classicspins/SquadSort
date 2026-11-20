using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace ClassicSpins.PrototypeFramework.Analytics.Firebase.Core
{
    public enum FirebaseBuildTarget { Other, Android, Ios }

    public sealed class FirebasePreflightFacts
    {
        public FirebaseBuildTarget Target { get; set; }
        public bool InPlay { get; set; }
        public bool DefineArmed { get; set; }
        public bool ServiceFilePresent { get; set; }
        public int AndroidMinSdk { get; set; }
        public readonly List<KeyValuePair<string, string>> NativeFiles = new List<KeyValuePair<string, string>>();
    }

    public sealed class FirebaseTransformResult
    {
        public string Content { get; internal set; }
        public bool Changed { get; internal set; }
        public readonly List<string> Problems = new List<string>();
    }

    /// <summary>Read-only preflight and conflict-preserving XML transforms, with no editor or vendor dependency.</summary>
    public static class FirebaseAnalyticsBuildRules
    {
        public const string ModuleId = "com.classicspins.pf-analytics-firebase";
        public const string InstallerType = "ClassicSpins.PrototypeFramework.Analytics.Firebase.FirebaseAnalyticsInstaller";
        public const string AndroidCollectionKey = "firebase_analytics_collection_enabled";
        public const string IosCollectionKey = "FIREBASE_ANALYTICS_COLLECTION_ENABLED";
        private static readonly XNamespace Tools = "http://schemas.android.com/tools";
        private static readonly XNamespace Android = "http://schemas.android.com/apk/res/android";

        /// <summary>Paths Unity imports; ignored backups cannot satisfy SDK service-file requirements.</summary>
        public static bool IsImportedAssetPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            var parts = path.Replace('\\', '/').Split('/');
            return parts.Length > 1 && parts[0] == "Assets" && !parts.Skip(1).Any(p =>
                string.IsNullOrEmpty(p) || p.StartsWith(".", StringComparison.Ordinal)
                || p.EndsWith("~", StringComparison.Ordinal) || p.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)
                || string.Equals(p, "CVS", StringComparison.OrdinalIgnoreCase));
        }

        public static bool IsServiceFile(string path, FirebaseBuildTarget target)
        {
            if (!IsImportedAssetPath(path)) return false;
            var name = path.Replace('\\', '/').Split('/').Last();
            return target == FirebaseBuildTarget.Android ? name == "google-services.json"
                : target == FirebaseBuildTarget.Ios && name == "GoogleService-Info.plist";
        }

        public static bool IsNativeFileCandidate(string path, FirebaseBuildTarget target)
        {
            if (!IsImportedAssetPath(path)) return false;
            return target == FirebaseBuildTarget.Android ? path.Replace('\\', '/').Split('/').Last() == "AndroidManifest.xml"
                : target == FirebaseBuildTarget.Ios && path.EndsWith(".plist", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsCollectionConfiguration(string path, string content, FirebaseBuildTarget target)
        {
            if (!IsNativeFileCandidate(path, target)) return false;
            return target == FirebaseBuildTarget.Android || path.Replace('\\', '/').Split('/').Last() == "Info.plist"
                || (content != null && (content.Contains(IosCollectionKey) || content.Contains("FIREBASE_ANALYTICS_COLLECTION_DEACTIVATED")));
        }

        public static IReadOnlyList<string> Evaluate(FirebasePreflightFacts facts)
        {
            var problems = new List<string>();
            if (facts == null || !facts.InPlay) return problems;
            if (!facts.DefineArmed) problems.Add("PF_FIREBASE is unarmed; install Firebase app and analytics 14.1.0 manual packages before building.");
            if (facts.Target == FirebaseBuildTarget.Other) return problems;
            if (!facts.ServiceFilePresent) problems.Add("missing " + (facts.Target == FirebaseBuildTarget.Android ? "google-services.json" : "GoogleService-Info.plist") + " under Assets; download the matching platform service file from Firebase Console.");
            if (facts.Target == FirebaseBuildTarget.Android && facts.AndroidMinSdk < 24)
                problems.Add("Android minSdk " + facts.AndroidMinSdk + " is below Firebase 14's required 24.");
            foreach (var file in facts.NativeFiles)
            {
                try
                {
                    var result = facts.Target == FirebaseBuildTarget.Android ? AndroidManifest(file.Value) : IosPlist(file.Value);
                    foreach (var problem in result.Problems) problems.Add(file.Key + ": " + problem);
                }
                catch (Exception ex) { problems.Add(file.Key + ": cannot inspect collection flags: " + ex.Message); }
            }
            return problems;
        }

        public static FirebaseTransformResult AndroidManifest(string source)
        {
            var doc = XDocument.Parse(source, LoadOptions.PreserveWhitespace);
            var result = new FirebaseTransformResult { Content = source };
            if (doc.Root == null || doc.Root.Name.LocalName != "manifest") throw new FormatException("Expected Android manifest root.");
            var app = doc.Root.Element("application");
            if (app == null)
            {
                // Valid permission-only library manifests may omit application entirely.
                app = new XElement("application");
                doc.Root.Add(app);
            }
            var flags = app.Elements("meta-data").ToArray();
            CheckMergeRemoval(app, true, result.Problems);
            foreach (var flag in flags.Where(f => (string)f.Attribute(Android + "name") == AndroidCollectionKey
                || (string)f.Attribute(Android + "name") == "firebase_analytics_collection_deactivated"
                || (string)f.Attribute(Tools + "node") == "removeAll"))
                CheckMergeRemoval(flag, false, result.Problems);
            foreach (var flag in flags.Where(f => (string)f.Attribute(Android + "name") == "firebase_analytics_collection_deactivated"))
                if ((string)flag.Attribute(Android + "value") != "false" || flag.Attribute(Android + "resource") != null)
                    result.Problems.Add(Conflict("firebase_analytics_collection_deactivated", FlagValue(flag), "false (or absent); permanent deactivation prevents runtime consent"));
            var collection = flags.Where(f => (string)f.Attribute(Android + "name") == AndroidCollectionKey).ToArray();
            foreach (var flag in collection)
                if ((string)flag.Attribute(Android + "value") != "false" || flag.Attribute(Android + "resource") != null)
                    result.Problems.Add(Conflict(AndroidCollectionKey, FlagValue(flag), "false"));
            if (collection.Length > 1) result.Problems.Add("duplicate " + AndroidCollectionKey + "; remove ambiguous declarations.");
            // Preserve the complete input when any conflict exists; a failed transform never partially writes.
            if (result.Problems.Count != 0) return result;
            if (collection.Length == 0)
            {
                app.Add(new XElement("meta-data", new XAttribute(Android + "name", AndroidCollectionKey), new XAttribute(Android + "value", "false")));
                result.Changed = true;
                result.Content = doc.ToString(SaveOptions.DisableFormatting);
            }
            return result;
        }

        public static FirebaseTransformResult IosPlist(string source)
        {
            var doc = XDocument.Parse(source, LoadOptions.PreserveWhitespace);
            var result = new FirebaseTransformResult { Content = source };
            if (doc.Root == null || doc.Root.Name != "plist" || doc.Root.Elements().Count() != 1
                || doc.Root.Elements().First().Name != "dict")
            {
                result.Problems.Add("[" + ModuleId + "] expected plist root containing one application dictionary; input preserved.");
                return result;
            }
            var dict = doc.Root.Element("dict");
            var keys = dict.Elements("key").ToArray();
            foreach (var key in keys.Where(k => k.Value == "FIREBASE_ANALYTICS_COLLECTION_DEACTIVATED"))
                if (!PlistFalse(key.ElementsAfterSelf().FirstOrDefault()))
                    result.Problems.Add(Conflict(key.Value, PlistValue(key), "NO (or absent); permanent deactivation prevents runtime consent"));
            var collection = keys.Where(k => k.Value == IosCollectionKey).ToArray();
            foreach (var key in collection)
                if (!PlistFalse(key.ElementsAfterSelf().FirstOrDefault())) result.Problems.Add(Conflict(key.Value, PlistValue(key), "NO"));
            if (collection.Length > 1) result.Problems.Add("duplicate " + IosCollectionKey + "; remove ambiguous declarations.");
            if (result.Problems.Count != 0) return result;
            if (collection.Length == 0)
            {
                dict.Add(new XElement("key", IosCollectionKey), new XElement("false"));
                result.Changed = true;
                result.Content = doc.ToString(SaveOptions.DisableFormatting);
            }
            return result;
        }

        private static bool PlistFalse(XElement value)
            => value != null && value.Name == "false" && !value.HasAttributes && !value.Nodes().Any();

        private static void CheckMergeRemoval(XElement element, bool application, List<string> problems)
        {
            var node = (string)element.Attribute(Tools + "node");
            if (node == "remove" || node == "removeAll")
                problems.Add(Conflict(application ? "application tools:node" : "meta-data tools:node", node, "no removal directive for protected collection defaults"));
            var remove = (string)element.Attribute(Tools + "remove");
            if (string.IsNullOrWhiteSpace(remove)) return;
            // tools:remove is a comma-separated QName list (not CSV). Resolve aliases rather than assuming android:.
            foreach (var token in remove.Split(','))
            {
                var name = token.Trim();
                var parts = name.Split(':');
                var local = parts[parts.Length - 1];
                var isAndroid = parts.Length == 2 && element.GetNamespaceOfPrefix(parts[0]) == Android;
                if (name == "*" || (!application && (local == "name" || local == "value" || local == "resource")
                    && (isAndroid || parts.Length == 1)) || (application && local == "meta-data"))
                    problems.Add(Conflict((application ? "application" : "meta-data") + " tools:remove", remove,
                        "no removal of protected collection metadata/attributes"));
            }
        }
        private static string PlistValue(XElement key)
        {
            var v = key.ElementsAfterSelf().FirstOrDefault();
            return v == null ? "<missing>" : v.ToString(SaveOptions.DisableFormatting);
        }
        private static string FlagValue(XElement flag)
            => (string)flag.Attribute(Android + "value") ?? (string)flag.Attribute(Android + "resource") ?? "<missing>";
        private static string Conflict(string key, string existing, string desired)
            => "[" + ModuleId + "] " + key + " existing='" + existing + "', required='" + desired + "'; existing value preserved; fix the conflicting configuration.";
    }
}
