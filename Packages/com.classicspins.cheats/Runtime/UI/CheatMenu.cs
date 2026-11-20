using UnityEngine;

namespace ClassicSpins.Cheats
{
    /// <summary>How the panel is placed and sized. Every field has a sensible default for a portrait 1080×1920 game.</summary>
    public sealed class CheatMenuOptions
    {
        /// <summary>The panel draws on its own screen-space overlay canvas, above every camera; this is its sorting order.</summary>
        public int SortingOrder = 32000;
        /// <summary>CanvasScaler reference resolution; sizes below are in these pixels.</summary>
        public Vector2 ReferenceResolution = new Vector2(1080, 1920);
        /// <summary>CanvasScaler width/height match (0 = width, 1 = height).</summary>
        public float MatchWidthOrHeight = 0.5f;
        /// <summary>Width of the Cheats card (reference pixels); it sits at the top-left and is as tall as its items. Logs use the whole safe area.</summary>
        public float PanelWidth = 300f;
        /// <summary>Gap between the panel and the safe-area edges.</summary>
        public float PanelMargin = 12f;
        /// <summary>Caption of the floating button that opens the panel. It can be dragged; its position is remembered.</summary>
        public string ToggleLabel = "CHEAT";
        /// <summary>Where the floating button starts, as a fraction of the safe area (0,0 = bottom-left).</summary>
        public Vector2 ToggleStart = new Vector2(0f, 0.62f);
        /// <summary>Capture Unity's log into the Logs tab.</summary>
        public bool CaptureLogs = true;
        /// <summary>Log lines kept for the Logs tab.</summary>
        public int LogCapacity = 300;
        /// <summary>Unity layer the panel's objects use (the canvas is screen-space overlay, so no camera has to see it).</summary>
        public int Layer = 5;   // "UI"
    }

    /// <summary>
    /// Entry point. A game builds a <see cref="CheatRegistry"/>, then calls <see cref="Create"/> once (it survives scene
    /// loads). Nothing is created in a build where <see cref="IsAvailable"/> is false, so a release build carries the code
    /// but never shows the menu.
    /// </summary>
    public static class CheatMenu
    {
        /// <summary>
        /// True in the Editor, in development builds, and in any build with the <c>CS_CHEATS</c> scripting define;
        /// <c>CS_CHEATS_DISABLED</c> turns it off everywhere (e.g. to capture clean screenshots in the Editor).
        /// </summary>
        public static bool IsAvailable
        {
            get
            {
#if CS_CHEATS_DISABLED
                return false;
#elif UNITY_EDITOR || DEVELOPMENT_BUILD || CS_CHEATS
                return true;
#else
                return false;
#endif
            }
        }

        /// <summary>Create the floating button and its panel. Returns null — and creates nothing — when <see cref="IsAvailable"/> is false.</summary>
        public static CheatMenuView Create(CheatRegistry registry, CheatMenuOptions options = null)
        {
            if (!IsAvailable || registry == null) return null;
            var go = new GameObject("[CheatMenu]");
            Object.DontDestroyOnLoad(go);
            var view = go.AddComponent<CheatMenuView>();
            view.Build(registry, options ?? new CheatMenuOptions());
            return view;
        }
    }
}
