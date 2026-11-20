using System;
using System.Collections.Generic;
using System.IO;

namespace ClassicSpins.PrototypeFramework.Analytics.Firebase.Core
{
    /// <summary>Engine-free export orchestration shared by the real Editor hooks and file-based tests.</summary>
    public static class FirebaseAnalyticsExport
    {
        public static IReadOnlyList<string> Android(string unityLibraryPath)
        {
            var paths = new List<string> { Path.Combine(unityLibraryPath, "src/main/AndroidManifest.xml") };
            // Launcher is higher priority than unityLibrary in the final Android manifest merge.
            var launcher = Path.GetFullPath(Path.Combine(unityLibraryPath, "../launcher/src/main/AndroidManifest.xml"));
            if (File.Exists(launcher)) paths.Add(launcher);
            return Transform(paths, true);
        }

        public static IReadOnlyList<string> Ios(string exportPath)
            => Transform(new[] { Path.Combine(exportPath, "Info.plist") }, false);

        private static IReadOnlyList<string> Transform(IEnumerable<string> paths, bool android)
        {
            var problems = new List<string>();
            var pending = new List<KeyValuePair<string, string>>();
            foreach (var path in paths)
            {
                try
                {
                    var source = File.ReadAllText(path);
                    var result = android ? FirebaseAnalyticsBuildRules.AndroidManifest(source) : FirebaseAnalyticsBuildRules.IosPlist(source);
                    foreach (var problem in result.Problems) problems.Add(path + ": " + problem);
                    if (result.Changed) pending.Add(new KeyValuePair<string, string>(path, result.Content));
                }
                catch (Exception ex) { problems.Add("[" + FirebaseAnalyticsBuildRules.ModuleId + "] " + path + ": " + ex.Message); }
            }
            // Validate the whole export before touching any file; conflicts preserve all original bytes.
            if (problems.Count != 0) return problems;
            foreach (var write in pending)
            {
                try { File.WriteAllText(write.Key, write.Value); }
                catch (Exception ex) { problems.Add("[" + FirebaseAnalyticsBuildRules.ModuleId + "] " + write.Key + ": " + ex.Message); }
            }
            return problems;
        }
    }
}
