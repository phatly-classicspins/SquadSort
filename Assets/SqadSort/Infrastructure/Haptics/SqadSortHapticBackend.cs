#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif
using ClassicSpins.PrototypeFramework.Domain;
using ClassicSpins.PrototypeFramework.Infrastructure;
using UnityEngine;

namespace Game.Infrastructure
{
    /// <summary>
    /// The device call behind the framework's <see cref="HapticService"/>, tuned like Money Sort (GDD §4 SFX &amp; haptics:
    /// weak / medium / strong). The framework's own backend is a placeholder (one long <c>Handheld.Vibrate</c> on Android,
    /// nothing on iOS), so the SKU swaps this one in: Android plays a short one-shot at the strength's amplitude, iOS calls
    /// the UIKit feedback generators in <c>Plugins/iOS/SqadSortHaptics.mm</c>. Editor and desktop stay a no-op.
    /// </summary>
    public sealed class SqadSortHapticBackend : IHapticBackend
    {
        private enum Strength { Weak, Medium, Strong }

        // Money Sort: two haptics closer than this collapse into one, so a burst of steps does not buzz.
        private const float MinInterval = 0.035f;
        private float _next;

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void SqadSort_PlayImpactHaptic(int strength);
#endif

        public void Emit(HapticType type)
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + MinInterval;
            Play(StrengthOf(type));
        }

        // GameFeedback plays Light for weak, Medium for medium and Heavy for strong; the rest fall in with their nearest.
        private static Strength StrengthOf(HapticType type) => type switch
        {
            HapticType.Selection or HapticType.Light => Strength.Weak,
            HapticType.Medium => Strength.Medium,
            _ => Strength.Strong,
        };

        private static void Play(Strength strength)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            using var vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
            if (vibrator == null || !vibrator.Call<bool>("hasVibrator")) return;

            var duration = strength == Strength.Weak ? 20L : strength == Strength.Medium ? 35L : 60L;
            var amplitude = strength == Strength.Weak ? 60 : strength == Strength.Medium ? 140 : 255;
            using var version = new AndroidJavaClass("android.os.Build$VERSION");
            if (version.GetStatic<int>("SDK_INT") >= 26)
            {
                using var effectClass = new AndroidJavaClass("android.os.VibrationEffect");
                using var effect = effectClass.CallStatic<AndroidJavaObject>("createOneShot", duration, amplitude);
                vibrator.Call("vibrate", effect);
            }
            else
            {
                vibrator.Call("vibrate", duration);
            }
#elif UNITY_IOS && !UNITY_EDITOR
            SqadSort_PlayImpactHaptic((int)strength);
#endif
        }
    }
}
