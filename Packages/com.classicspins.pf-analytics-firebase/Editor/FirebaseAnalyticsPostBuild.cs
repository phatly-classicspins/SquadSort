using System.Collections.Generic;
using ClassicSpins.PrototypeFramework.Analytics.Firebase.Core;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
#if UNITY_ANDROID
using UnityEditor.Android;
#endif

namespace ClassicSpins.PrototypeFramework.Analytics.Firebase.Editor
{
    /// <summary>Permitted transform-only callbacks. Explicit late order; existing conflicts abort without rewriting them.</summary>
    internal sealed class FirebaseAnalyticsPostBuild : IPostprocessBuildWithReport
#if UNITY_ANDROID
        , IPostGenerateGradleAndroidProject
#endif
    {
        public int callbackOrder => 10000;
        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.iOS || !FirebaseAnalyticsPreflight.IsInPlay) return;
            RequireSafeExport(FirebaseAnalyticsExport.Ios(report.summary.outputPath));
        }
#if UNITY_ANDROID
        public void OnPostGenerateGradleAndroidProject(string path)
        {
            if (!FirebaseAnalyticsPreflight.IsInPlay) return;
            RequireSafeExport(FirebaseAnalyticsExport.Android(path));
        }
#endif
        private static void RequireSafeExport(IReadOnlyList<string> problems)
        {
            if (problems.Count > 0) throw new BuildFailedException(string.Join("\n", problems));
        }
    }
}
