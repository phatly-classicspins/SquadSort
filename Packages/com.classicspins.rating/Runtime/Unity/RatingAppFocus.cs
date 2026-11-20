using System;
using UnityEngine;

namespace ClassicSpins.Rating
{
    /// <summary>
    /// Turns Unity's pause / focus messages into one "focus changed" event, for a game that has no lifecycle hook of its
    /// own. A game that already routes these messages (PrototypeFramework owns them in <c>AppLifecycle</c> and bans a second
    /// hook) must not create this — forward its own hook to <see cref="RatingPrompter.OnAppFocusChanged"/> instead.
    /// </summary>
    [AddComponentMenu("")]
    public sealed class RatingAppFocus : MonoBehaviour
    {
        private static RatingAppFocus _instance;
        private bool _focused = true;

        /// <summary>False when the game went to the background or lost focus, true when it is back.</summary>
        public event Action<bool> Changed;

        /// <summary>The single relay, created (and kept across scene loads) on first use.</summary>
        public static RatingAppFocus Instance
        {
            get
            {
                if (_instance != null) return _instance;
                var go = new GameObject("[RatingAppFocus]") { hideFlags = HideFlags.HideInHierarchy };
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<RatingAppFocus>();
                return _instance;
            }
        }

        private void OnApplicationPause(bool paused) => Set(!paused);
        private void OnApplicationFocus(bool focused) => Set(focused);

        // pause and focus both fire on a device; report each real change once
        private void Set(bool focused)
        {
            if (focused == _focused) return;
            _focused = focused;
            Changed?.Invoke(focused);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}
