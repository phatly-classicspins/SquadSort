using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace ClassicSpins.Rating
{
    /// <summary>
    /// The engine side of the flow. The store follows the build target, also in the Editor (an iOS-target Editor opens
    /// the App Store's https page), so the whole flow can be tried without a device.
    /// </summary>
    public sealed class UnityRatingHost : IRatingHost
    {
        public RatingPlatform Platform
        {
            get
            {
#if UNITY_IOS
                return RatingPlatform.Ios;
#elif UNITY_ANDROID
                return RatingPlatform.Android;
#else
                return RatingPlatform.Other;
#endif
            }
        }

        public bool NativeStoreLinks => !UnityEngine.Application.isEditor;

        public string AppIdentifier => UnityEngine.Application.identifier;

        public void OpenUrl(string url) => UnityEngine.Application.OpenURL(url);

        // Task.Delay resumes on the caller's context (Unity's main thread when awaited from it); it ignores timeScale,
        // which is right: a paused game still waits real time for the player to leave.
        public Task Delay(TimeSpan time, CancellationToken ct) => Task.Delay(time, ct);
    }

    /// <summary>The one-shot "offered" flag in PlayerPrefs, for a game that has no save system of its own.</summary>
    public sealed class PlayerPrefsRatingState : IRatingState
    {
        public const string DefaultKey = "ClassicSpins.Rating.PromptShown";
        private readonly string _key;

        public PlayerPrefsRatingState(string key) { _key = string.IsNullOrEmpty(key) ? DefaultKey : key; }

        public bool PromptShown => PlayerPrefs.GetInt(_key, 0) != 0;

        public bool MarkPromptShown()
        {
            try
            {
                PlayerPrefs.SetInt(_key, 1);
                PlayerPrefs.Save();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Rating] could not save {_key}: {e.Message}");
                return false;
            }
        }

        /// <summary>Forget the offer (a tester tool: the prompt can come again).</summary>
        public void Reset()
        {
            PlayerPrefs.DeleteKey(_key);
            PlayerPrefs.Save();
        }
    }
}
