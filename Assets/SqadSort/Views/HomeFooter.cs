using System;
using TMPro;
using UnityEngine;

namespace Game.Views
{
    /// <summary>The Home footer tabs (Money Sort <c>HomeFooterUI</c>): Shop, the main game, a locked tab reserved for a later feature.</summary>
    public enum HomeTab { Shop, Home, Locked }

    /// <summary>
    /// D-066 / D-067: Money Sort's Home footer — the orange bar across the bottom, split 35.1% / 31.0% / 33.9% by the bar art's
    /// dividers; the selected tab is the yellow tile rising above the bar with a larger icon and its label, the others the icon alone,
    /// the third tab only its lock. Drawn by Home and again by the Store over it (Money Sort's <c>FooterShopTabOverlay</c>).
    /// Money Sort numbers, scaled by <see cref="ShopArtView.Scale"/>.
    /// </summary>
    public sealed class HomeFooter
    {
        /// <summary>The sprites and the label font / material (Lilita One with the brown outline).</summary>
        public readonly struct Art
        {
            public readonly Sprite Bar, Selected, Shop, Home, Lock;
            public readonly TMP_FontAsset Font;
            public readonly Material Label;
            public Art(Sprite bar, Sprite selected, Sprite shop, Sprite home, Sprite lockIcon, TMP_FontAsset font, Material label)
            {
                Bar = bar; Selected = selected; Shop = shop; Home = home; Lock = lockIcon; Font = font; Label = label;
            }
        }

        private const float Ms = ShopArtView.Scale;
        public const float Height = 137f * Ms;
        private static readonly float[] TabEdge = { 0f, 0.3513889f, 0.6611111f, 1f };

        private readonly GameObject[] _on = new GameObject[2], _off = new GameObject[2];
        private readonly TextMeshProUGUI[] _label = new TextMeshProUGUI[2];

        public static HomeFooter Build(RectTransform parent, in Art art, Action<HomeTab> tapped)
        {
            var f = new HomeFooter();
            var bar = UiKit.Image("Footer", parent, art.Bar, 0f, 0f, 0f, Height, fromBottom: true);
            var rt = bar.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(1f, 0f); rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = Vector2.zero; rt.sizeDelta = new Vector2(0f, Height);
            bar.raycastTarget = true;   // taps on the bar never fall through to what is behind it
            f.Tab(rt, art, HomeTab.Shop, art.Shop, new Vector2(154f, 130f), new Vector2(138.6f, 117f), tapped);
            f.Tab(rt, art, HomeTab.Home, art.Home, new Vector2(145f, 118f), new Vector2(130.5f, 106.2f), tapped);
            Centered("Lock", Column(rt, (int)HomeTab.Locked), art.Lock, 62f, new Vector2(117.9f, 117f));
            return f;
        }

        /// <summary>Which tab is lit, and the two labels (localized).</summary>
        public void Set(HomeTab selected, string shopLabel, string homeLabel)
        {
            _label[(int)HomeTab.Shop].SetText(shopLabel ?? string.Empty);
            _label[(int)HomeTab.Home].SetText(homeLabel ?? string.Empty);
            for (int i = 0; i < _on.Length; i++)
            {
                _on[i].SetActive(i == (int)selected);
                _off[i].SetActive(i != (int)selected);
            }
        }

        private void Tab(RectTransform bar, in Art art, HomeTab tab, Sprite icon, Vector2 iconOn, Vector2 iconOff, Action<HomeTab> tapped)
        {
            int i = (int)tab;
            var col = Column(bar, i);
            var on = UiKit.Stretch("Selected", col);
            Centered("Tile", on, art.Selected, 81f, new Vector2(253f, 162f));
            Centered("Icon", on, icon, 118f, iconOn);
            var label = UiKit.Text("Label", on, art.Font, string.Empty, 38f * Ms, DesignTokens.OnColor, 0f, 0f, 0f, 60f * Ms);
            label.fontStyle = FontStyles.Normal;
            if (art.Label != null) label.fontSharedMaterial = art.Label;
            var lr = label.rectTransform;
            lr.anchorMin = new Vector2(0f, 0f); lr.anchorMax = new Vector2(1f, 0f); lr.pivot = new Vector2(0.5f, 0.5f);
            lr.anchoredPosition = new Vector2(0f, 35f * Ms); lr.sizeDelta = new Vector2(0f, 60f * Ms);
            var off = Centered("Icon", col, icon, 62f, iconOff).gameObject;
            _on[i] = on.gameObject; _off[i] = off; _label[i] = label;
            var hit = UiKit.Stretch("Hit", col).gameObject;
            hit.AddComponent<CanvasRenderer>();
            UiKit.Button(hit.AddComponent<HitArea>(), () => tapped?.Invoke(tab));
        }

        private static RectTransform Column(RectTransform bar, int i)
        {
            var col = UiKit.Stretch($"Tab{i}", bar);
            col.anchorMin = new Vector2(TabEdge[i], 0f); col.anchorMax = new Vector2(TabEdge[i + 1], 1f);
            return col;
        }

        // an image centred on its column, its centre y Money Sort px above the bar's bottom edge
        private static RectTransform Centered(string name, RectTransform parent, Sprite sprite, float y, Vector2 size)
        {
            var rt = UiKit.Image(name, parent, sprite, 0f, 0f, size.x * Ms, size.y * Ms).rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f); rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, y * Ms);
            return rt;
        }
    }
}
