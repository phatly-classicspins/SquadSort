using UnityEngine;

namespace ClassicSpins.Rating
{
    /// <summary>
    /// Text and look of the default popup. The defaults are Money Sort's English texts on a warm "toy box" palette;
    /// set any field before <see cref="RatingKit.Create"/> to match the game. Sprites are optional: when one is set it is
    /// drawn untinted in place of the built-in shape.
    /// </summary>
    public sealed class RatingPopupStyle
    {
        // ── text ─────────────────────────────────────────────────────────────────────────────────
        public string Title = "We'd like to hear from you";
        public string Question = "How was your experience\nwith the game so far?";
        public string Submit = "Submit";
        public string ThanksTitle = "Rating Submitted!";
        public string ThanksPositive = "Thanks for the love! Your support helps us bring you more great updates!";
        public string ThanksNegative = "Thank you for helping us improve! We're working hard to make the game even better!";
        public string Continue = "Continue";
        /// <summary>A legacy uGUI font; null uses Unity's built-in one.</summary>
        public Font Font;

        // ── colours ──────────────────────────────────────────────────────────────────────────────
        public Color Scrim = new Color(0.10f, 0.06f, 0.03f, 0.72f);
        public Color Panel = Hex(0xFFF8EC);
        public Color PanelEdge = Hex(0xD9B07A);
        public Color Header = Hex(0xFF8A3D);
        public Color HeaderEdge = Hex(0xD35F14);
        public Color Ink = Hex(0x5A3415);
        public Color OnColor = Color.white;
        public Color StarOn = Hex(0xFFC93C);
        public Color StarOnEdge = Hex(0xD99A0B);
        public Color StarOff = Hex(0xEBDDC8);
        public Color StarOffEdge = Hex(0xCDBFAE);
        public Color Button = Hex(0x3CC26B);
        public Color ButtonEdge = Hex(0x22924A);
        public Color ButtonDisabled = Hex(0xCDBFAE);
        public Color ButtonDisabledEdge = Hex(0xA8977F);
        public Color Close = Hex(0xE2604A);
        public Color CloseEdge = Hex(0xB7411F);

        // ── optional art (untinted when set) ─────────────────────────────────────────────────────
        public Sprite PanelSprite, HeaderSprite, ButtonSprite, CloseSprite, StarOnSprite, StarOffSprite;

        // ── canvas ───────────────────────────────────────────────────────────────────────────────
        /// <summary>The popup draws on its own screen-space overlay canvas; this is its sorting order.</summary>
        public int SortingOrder = 30000;
        /// <summary>CanvasScaler reference resolution; every size in the popup is in these pixels (the panel is 880 wide).</summary>
        public Vector2 ReferenceResolution = new Vector2(1080, 1920);
        /// <summary>CanvasScaler width/height match (0 = width, right for a portrait game; 1 = height).</summary>
        public float MatchWidthOrHeight = 0f;
        /// <summary>Unity layer of the popup's objects.</summary>
        public int Layer = 5;   // "UI"

        public static Color Hex(int rgb) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
    }
}
