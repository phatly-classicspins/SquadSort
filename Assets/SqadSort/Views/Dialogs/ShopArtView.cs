using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Views
{
    /// <summary>
    /// The TMP material a Money Sort shop text uses (all Lilita One): <see cref="Plain"/> is the font's own — the brown
    /// <see cref="DesignTokens.MsText"/> face, no outline — the others a white face with a coloured outline and drop shadow
    /// (Money Sort "Atlas Material A13C04 / 03841E / BD0212 / 050D47", "SDF outline black"); D-076 adds <see cref="Leaf"/> ("Atlas Material
    /// 085E00", the level complete buttons) and <see cref="Feature"/> ("Lilita_Figma_NewFeature", the New Feature tag); <see cref="Life"/> is
    /// "Lilita_LifePopupTitle" (crimson outline: the lives popups' titles and counts).
    /// </summary>
    public enum MsInk { Plain, Brown, Green, Red, Navy, Black, Leaf, Feature, Life }

    /// <summary>
    /// D-067: the Shop dialogs drawn with Money Sort's art (art/vendor/moneysort/shop, copied as-is — Phat, 01-11-2026) and its font
    /// (Lilita One with its outline materials, Content/Fonts/LilitaOne) at Money Sort's own measurements. Money Sort lays its UI out on
    /// a 720 × 1280 canvas, CardSlot on 1080 × 1920, so every Money Sort number is multiplied by <see cref="Scale"/>. Positions are
    /// given the way the Money Sort prefabs store them: the centre of the element, measured from the top-left of its parent, y
    /// negative downwards.
    /// </summary>
    public abstract class ShopArtView : CardDialogView
    {
        /// <summary>Money Sort canvas px → CardSlot canvas px (720 → 1080).</summary>
        public const float Scale = 1.5f;

        // every sprite of the "shop" atlas, looked up by its name (CardSlot/Content/Build fills it)
        [SerializeField] private Sprite[] _shopArt = new Sprite[0];
        [SerializeField] protected TMP_FontAsset _msFont;
        // in MsInk order from Brown: A13C04, 03841E, BD0212, 050D47, outline black, 085E00, Lilita_Figma_NewFeature, Lilita_LifePopupTitle
        [SerializeField] private Material[] _msMaterials = new Material[5];

        protected Sprite Art(string id)
        {
            string icon = id switch
            {
                "shop_booster_hand" or "unlock_booster_hand" or "restart_booster_icon" => "Hand 1",
                "shop_booster_hammer" or "unlock_booster_hammer" => "Magnet 1",
                "shop_booster_swap" or "unlock_booster_swap" => "Shuffle 1",
                _ => id
            };
            foreach (var s in _shopArt) if (s != null && s.name == icon) return s;
            return null;
        }

        protected Material InkMaterial(MsInk ink) =>
            ink == MsInk.Plain || _msMaterials == null || (int)ink - 1 >= _msMaterials.Length ? null : _msMaterials[(int)ink - 1];

        /// <summary>A node of Money Sort size <paramref name="w"/> × <paramref name="h"/>, its top-left at (x, y) CardSlot px.</summary>
        protected static RectTransform Card(Transform parent, string name, float x, float y, float w, float h) =>
            UiKit.Node(name, parent, x, y, w * Scale, h * Scale);

        /// <summary>A sprite centred at Money Sort (cx, cy) of its parent, sized w × h Money Sort px.</summary>
        protected Image Put(Transform card, string art, float cx, float cy, float w, float h, bool raycast = false, bool sliced = false) =>
            UiKit.Image(art, card, Art(art), (cx - w / 2f) * Scale, (-cy - h / 2f) * Scale, w * Scale, h * Scale, sliced: sliced, raycast: raycast);

        /// <summary>A Money Sort text: Lilita One at <paramref name="size"/> Money Sort pt in a w × h box centred at (cx, cy), with
        /// its material, alignment and character spacing.</summary>
        protected TextMeshProUGUI Say(Transform card, string text, float cx, float cy, float w, float h, float size, MsInk ink,
            TextAlignmentOptions align = TextAlignmentOptions.Center, float spacing = 0f)
        {
            var t = UiKit.Text("Text", card, _msFont, text, size * Scale, ink == MsInk.Plain ? DesignTokens.MsText : DesignTokens.OnColor,
                (cx - w / 2f) * Scale, (-cy - h / 2f) * Scale, w * Scale, h * Scale, align);
            Style(t, ink, spacing);
            return t;
        }

        // Money Sort's TMP defaults: regular weight, wrapping on, overflow allowed
        protected void Style(TextMeshProUGUI t, MsInk ink, float spacing = 0f)
        {
            t.fontStyle = FontStyles.Normal;
            t.enableWordWrapping = true;
            t.characterSpacing = spacing;
            var m = InkMaterial(ink);
            if (m != null) t.fontSharedMaterial = m;
        }

        /// <summary>Money Sort's TMP colour gradient presets, top to bottom (the vertex colour stays white).</summary>
        protected static TextMeshProUGUI Gradient(TextMeshProUGUI t, Color top, Color bottom)
        {
            t.color = Color.white;
            t.enableVertexGradient = true;
            t.colorGradient = new VertexGradient(top, top, bottom, bottom);
            return t;
        }

        /// <summary>A sprite button with its label stretched over it like Money Sort's (inset 8 a side, raised by <paramref name="lift"/>).</summary>
        protected Button ArtButton(Transform card, string art, string label, float cx, float cy, float w, float h, float size, float lift,
            MsInk ink, Action onClick, float insetH = 8f)
        {
            var img = Put(card, art, cx, cy, w, h);
            var t = UiKit.Text("Label", img.transform, _msFont, label, size * Scale, DesignTokens.OnColor, 8f * Scale, -(lift - insetH / 2f) * Scale,
                (w - 16f) * Scale, (h - insetH) * Scale);
            Style(t, ink);
            return UiKit.Button(img, onClick);
        }

        /// <summary>✕ — Money Sort's round red close, centred at (cx, cy).</summary>
        protected void Close(Transform card, float cx, float cy) => UiKit.Button(Put(card, "starter_popup_close", cx, cy, 74f, 76f), RaiseCloseRequested);

        /// <summary>Money Sort's booster pictures for CardSlot's boosters (BoosterId order Hand, Shuffle, Remove): the hand, the swap
        /// arrows, the hammer.</summary>
        protected static readonly string[] BoosterArt = { "shop_booster_hand", "shop_booster_swap", "shop_booster_hammer" };

        /// <summary>A Money Sort popup card (StarterBundleUI, PurchaseRewardUI …): <paramref name="w"/> × <paramref name="h"/> Money Sort px,
        /// its centre offset (dx, dy) from the centre of the popup root, which Money Sort raises by 29.</summary>
        protected RectTransform PopupCard(float w, float h, float dx, float dy)
        {
            float cw = w * Scale, ch = h * Scale;
            return Card(Root, "OfferCard", (1080f - cw) / 2f + dx * Scale, (Height - ch) / 2f - (dy + 29f) * Scale, w, h);
        }

        /// <summary>The bundle block of StarterBundleUI "Rewards" / PurchaseRewardUI "BundleContent": coins, ∞ lives and the three
        /// boosters, each at its Money Sort place. A null part is not drawn.</summary>
        protected void Bundle(Transform card, in BundleLayout l, string coins, string lives, string booster)
        {
            if (coins != null)
            {
                Put(card, "starter_popup_coins", l.CoinX, l.TopY, l.CoinW, l.CoinH);
                Say(card, coins, l.CoinX, l.LabelY, l.CoinW, l.LabelH, l.Size, MsInk.Plain);
            }
            if (lives != null)
            {
                Put(card, "shop_heart_infinite", l.HeartX, l.TopY, l.HeartW, l.HeartH);
                Say(card, "∞", l.HeartX, l.TopY + l.InfinityLift, l.HeartW * 0.85f, l.HeartH * 0.72f, l.InfinitySize, MsInk.Red);
                var t = Say(card, lives, l.HeartX, l.LabelY, l.HeartW + 2f, l.LabelH, l.Size, MsInk.Plain);
                t.fontStyle = FontStyles.UpperCase;
            }
            if (booster == null) return;
            for (int i = 0; i < 3; i++)
            {
                var b = l.Boosters[i];
                Put(card, BoosterArt[b.Booster], b.X, b.Y, b.W, b.H);
                Say(card, booster, b.CountX, l.CountY, l.CountW, l.CountH, l.CountSize, MsInk.Plain);
            }
        }

        /// <summary>One booster picture of a bundle: which (BoosterId order), its centre and size, and its count's x.</summary>
        protected readonly struct BundleBooster
        {
            public readonly int Booster;
            public readonly float X, Y, W, H, CountX;
            public BundleBooster(int booster, float x, float y, float w, float h, float countX) { Booster = booster; X = x; Y = y; W = w; H = h; CountX = countX; }
        }

        /// <summary>Money Sort coordinates of one bundle block.</summary>
        protected readonly struct BundleLayout
        {
            public readonly float CoinX, CoinW, CoinH, HeartX, HeartW, HeartH, TopY, LabelY, LabelH, Size, InfinityLift, InfinitySize, CountY, CountW, CountH, CountSize;
            public readonly BundleBooster[] Boosters;
            public BundleLayout(float coinX, float coinW, float coinH, float heartX, float heartW, float heartH, float topY, float labelY, float labelH,
                float size, float infinityLift, float infinitySize, BundleBooster[] boosters, float countY, float countW, float countH, float countSize)
            {
                CoinX = coinX; CoinW = coinW; CoinH = coinH; HeartX = heartX; HeartW = heartW; HeartH = heartH; TopY = topY; LabelY = labelY; LabelH = labelH;
                Size = size; InfinityLift = infinityLift; InfinitySize = infinitySize; Boosters = boosters; CountY = countY; CountW = countW; CountH = countH;
                CountSize = countSize;
            }
        }
    }
}
