using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Views
{
    /// <summary>
    /// Small builders for runtime uGUI in mock-up coordinates: (x, y) is the top-left corner measured from
    /// the parent's top-left, in reference px (1 px = 1 world unit). Pure presentation — no game state.
    /// </summary>
    public static class UiKit
    {
        public static RectTransform Node(string name, Transform parent, float x, float y, float w, float h, bool fromBottom = false)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            Place(rt, x, y, w, h, fromBottom);
            return rt;
        }

        /// <summary>Move an existing node to (x, y, w, h) in the same coordinates as <see cref="Node"/>.</summary>
        public static void Place(RectTransform rt, float x, float y, float w, float h, bool fromBottom = false)
        {
            var a = fromBottom ? new Vector2(0f, 0f) : new Vector2(0f, 1f);
            rt.anchorMin = rt.anchorMax = rt.pivot = a;
            rt.anchoredPosition = fromBottom ? new Vector2(x, y) : new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
        }

        /// <summary>An invisible tap area (<see cref="HitArea"/>): catches raycasts, draws nothing.</summary>
        public static HitArea HitArea(string name, Transform parent, float x, float y, float w, float h)
        {
            var go = Node(name, parent, x, y, w, h).gameObject;
            go.AddComponent<CanvasRenderer>();   // the raycaster reads it; AddComponent of a Graphic subclass does not add it
            var hit = go.AddComponent<HitArea>();
            hit.raycastTarget = true;
            return hit;
        }

        /// <summary>Set <paramref name="t"/>'s text only when it changed (TMP regenerates its mesh on every set).</summary>
        public static void SetText(TMP_Text t, string value)
        {
            value ??= string.Empty;
            if (t.text != value) t.SetText(value);
        }

        /// <summary>Temporary bridge for screens still authored at 1080x1920. New Figma UI uses Stretch directly.</summary>
        public static RectTransform LegacyRoot(string name, Transform parent)
        {
            var rt = Stretch(name, parent);
            rt.gameObject.AddComponent<LegacyCanvasLayout>();
            return rt;
        }

        public static RectTransform Stretch(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        public static Image Image(string name, Transform parent, Sprite sprite, float x, float y, float w, float h, bool sliced = false, bool raycast = false, bool fromBottom = false)
        {
            var rt = Node(name, parent, x, y, w, h, fromBottom);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.type = sliced ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            img.raycastTarget = raycast;
            return img;
        }

        public static Image Fill(string name, Transform parent, Color color, bool raycast)
        {
            var rt = Stretch(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        public static TextMeshProUGUI Text(string name, Transform parent, TMP_FontAsset font, string value, float size, Color color,
            float x, float y, float w, float h, TextAlignmentOptions align = TextAlignmentOptions.Center, bool fromBottom = false)
        {
            var rt = Node(name, parent, x, y, w, h, fromBottom);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = size;
            t.fontStyle = FontStyles.Bold;
            t.color = color;
            t.alignment = align;
            t.enableWordWrapping = false;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            t.SetText(value ?? string.Empty);
            return t;
        }

        /// <summary>A UiKit button was pressed — a fact for the click sound and haptic (GDD v2.0 §9); the View knows no audio.</summary>
        public static event Action Clicked;

        /// <summary>Raise <see cref="Clicked"/> for a button built outside <see cref="Button"/>.</summary>
        public static void NotifyClicked() => Clicked?.Invoke();

        public static Button Button(Graphic image, Action onClick)
        {
            image.raycastTarget = true;
            var b = image.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.targetGraphic = image;
            // AddComponent may already have run a default ColorTint transition (e.g. the "disabled" tint while a
            // dialog's CanvasGroup is non-interactable during its show leg); None never undoes it, so reset it here
            image.canvasRenderer.SetColor(Color.white);
            b.onClick.AddListener(() => { Clicked?.Invoke(); onClick?.Invoke(); });
            return b;
        }

        /// <summary>The coin counter (mock-ups *-coins-v02, CR-012 C1): a surface pill at the top-left screen corner with
        /// the coin overlapping its left end; returns the number text so the caller can update it.</summary>
        public static TextMeshProUGUI CoinPill(Transform parent, Sprite pill, Sprite coin, TMP_FontAsset font, string label)
        {
            const float x = 40f, y = 66f, w = 270f, h = 104f;
            Image("CoinPill", parent, pill, x, y, w, h, sliced: true);
            Image("Coin", parent, coin, x - 6f, y + 4f, 96f, 96f);
            return Text("Coins", parent, font, label ?? string.Empty, DesignTokens.TypeHud, DesignTokens.Ink, x + 84f, y, w - 100f, h - 8f);
        }

        /// <summary>CR-012 E (mock-up home-lives-v04): the lives pill top-right, mirroring the coin pill — the heart with the
        /// count on it, then "Full" or the time to the next life.</summary>
        public static (TextMeshProUGUI count, TextMeshProUGUI right) LivesPill(Transform parent, Sprite pill, Sprite heart, TMP_FontAsset font)
        {
            const float w = 270f, h = 104f, x = 1080f - 40f - w, y = 66f;
            Image("LivesPill", parent, pill, x + 40f, y, w - 40f, h, sliced: true);
            Image("Heart", parent, heart, x - 4f, y - 2f, 108f, 108f);
            var count = Text("Lives", parent, font, string.Empty, DesignTokens.TypeHud, DesignTokens.OnColor, x - 4f, y, 108f, h - 8f);
            var right = Text("LivesNext", parent, font, string.Empty, DesignTokens.TypeCount, DesignTokens.Ink, x + 104f, y, w - 120f, h - 8f);
            return (count, right);
        }
    }

    /// <summary>One booster tile as the controller decided it (features/boosters.md v2). Exactly one of
    /// <see cref="Locked"/>, <see cref="Badge"/>, <see cref="Price"/> is set; all text arrives localized.</summary>
    public struct BoosterTileVisual
    {
        public string Name;
        /// <summary>"Lv 8" while the booster is locked, else null.</summary>
        public string Locked;
        /// <summary>How many are owned, or null when none.</summary>
        public string Badge;
        /// <summary>The coin price shown when none is owned, or null.</summary>
        public string Price;
        /// <summary>Highlighted: the tile being used (pick mode) or just unlocked.</summary>
        public bool Active;
    }

    /// <summary>The sprites the booster bar draws with (wired by <c>CardSlot/Content/Build</c>).</summary>
    public readonly struct BoosterBarArt
    {
        public readonly Sprite Tile, Badge, PriceTag, Lock, Coin, Glow;
        public readonly Sprite[] Icons;
        /// <summary>Abhi's booster tiles (D-073): each icon with its own gold frame, drawn whole in place of Tile + Icon; null ⇒
        /// the tile and tinted icon.</summary>
        public readonly Sprite[] Framed;
        public readonly TMP_FontAsset Font;
        /// <param name="glow">The white ring with its soft halo around the active tile (<c>ftue_ring</c>).</param>
        public BoosterBarArt(Sprite tile, Sprite[] icons, Sprite badge, Sprite priceTag, Sprite lockIcon, Sprite coin, TMP_FontAsset font, Sprite glow,
            Sprite[] framed = null)
        {
            Tile = tile; Icons = icons; Badge = badge; PriceTag = priceTag; Lock = lockIcon; Coin = coin; Font = font; Glow = glow; Framed = framed;
        }
    }

    /// <summary>The booster bar (mock-ups gameplay-boosters-v02, CR-012 C2): three tiles centred along the screen bottom. The board
    /// draws it, and the lose / unlock dialogs draw the same bar above the dim, at the same place.</summary>
    public static class BoosterBar
    {
        public static float TileX(int i, int n) =>
            (1080f - (n * DesignTokens.BoosterTileWidth + (n - 1) * DesignTokens.BoosterTileGap)) / 2f + i * (DesignTokens.BoosterTileWidth + DesignTokens.BoosterTileGap);

        /// <summary>Draw <paramref name="tiles"/> under <paramref name="parent"/> (a full-screen node); a press relays the
        /// tile index. <paramref name="only"/> ≥ 0 draws just that tile (pick mode: the active tile above the dim).</summary>
        // Abhi's gameplay demo, measured at 1080×1920: three framed tiles 160 across (art 512), 235 apart centre to centre,
        // 60 above the screen bottom, no name under them
        private const float FramedTile = 160f, FramedArt = 512f, FramedStep = 235f, FramedBottom = 60f;

        private static void DrawFramed(Transform parent, BoosterTileVisual[] tiles, in BoosterBarArt art, Action<int> pressed, int only)
        {
            float w = FramedTile, y = FramedBottom;
            for (int i = 0; i < tiles.Length; i++)
            {
                if (only >= 0 && i != only) continue;
                var t = tiles[i];
                float cx = 540f + (i - (tiles.Length - 1) / 2f) * FramedStep, x = cx - w / 2f;
                if (t.Active)
                {
                    float m = DesignTokens.GlowRingHalo;
                    var glow = UiKit.Image($"Glow{i}", parent, art.Glow, x - 10f - m, y - 10f - m, w + 20f + 2f * m, w + 20f + 2f * m, sliced: true, fromBottom: true);
                    glow.color = DesignTokens.OnColor;
                }
                // the tile node is the frame's 512 box; a sprite with art past it (the magnet's sparks) keeps its scale
                var sprite = art.Framed[i];
                var tile = UiKit.Image($"Booster{i}", parent, null, x, y, w, w, fromBottom: true);
                tile.color = new Color(1f, 1f, 1f, 0f);
                float sw = sprite.rect.width * w / FramedArt, sh = sprite.rect.height * w / FramedArt;
                var face = UiKit.Image("Icon", tile.transform, sprite, 0f, w - sh, sw, sh);
                face.raycastTarget = false;
                if (t.Locked != null)
                {
                    face.color = DesignTokens.CoveredTint;
                    UiKit.Image("Lock", tile.transform, art.Lock, (w - 70f) / 2f, 28f, 70f, 70f).color = DesignTokens.OnColor;
                    UiKit.Text("Level", tile.transform, art.Font, t.Locked, 34f, DesignTokens.OnColor, 0f, 100f, w, 46f);
                }
                if (t.Badge != null)
                {
                    float b = DesignTokens.BoosterBadge;
                    var badge = UiKit.Image("Badge", tile.transform, art.Badge, w - 44f, -20f, b, b - 2f, sliced: true);
                    UiKit.Text("Count", badge.transform, art.Font, t.Badge, 40f, DesignTokens.OnColor, 0f, 0f, b, b - 10f);
                }
                if (t.Price != null)
                {
                    var tag = UiKit.Image("Price", tile.transform, art.PriceTag, 12f, w - 34f, w - 24f, 60f, sliced: true);
                    var label = UiKit.Text("Amount", tag.transform, art.Font, t.Price, 34f, DesignTokens.OnColor, 0f, 0f, w - 24f, 56f);
                    float tw = label.GetPreferredValues(t.Price).x, coin = 38f, total = coin + 6f + tw;
                    UiKit.Image("Coin", tag.transform, art.Coin, (w - 24f - total) / 2f, 9f, coin, coin);
                    label.rectTransform.anchoredPosition = new Vector2((w - 24f - total) / 2f + coin + 6f, 0f);
                    label.rectTransform.sizeDelta = new Vector2(tw + 4f, 56f);
                }
                int index = i;
                UiKit.Button(tile, () => pressed?.Invoke(index));
            }
        }

        public static void Draw(Transform parent, BoosterTileVisual[] tiles, in BoosterBarArt art, Action<int> pressed, int only = -1)
        {
            if (art.Framed != null && art.Framed.Length >= tiles.Length) { DrawFramed(parent, tiles, art, pressed, only); return; }
            float w = DesignTokens.BoosterTileWidth, h = DesignTokens.BoosterTileHeight, y = DesignTokens.BoosterBarBottom;
            for (int i = 0; i < tiles.Length; i++)
            {
                if (only >= 0 && i != only) continue;
                var t = tiles[i];
                float x = TileX(i, tiles.Length);
                if (t.Active)
                {
                    // the mock-up's ring box (tile + 14 / 8) drawn with glow_ring, whose halo sits GlowRingHalo outside the box (B-004)
                    float m = DesignTokens.GlowRingHalo;
                    var glow = UiKit.Image($"Glow{i}", parent, art.Glow, x - 14f - m, y - 8f - m, w + 28f + 2f * m, h + 22f + 2f * m, sliced: true, fromBottom: true);
                    glow.color = DesignTokens.OnColor;
                }
                var tile = UiKit.Image($"Booster{i}", parent, art.Tile, x, y, w, h, fromBottom: true);
                if (t.Locked != null)
                {
                    tile.color = DesignTokens.CoveredTint;
                    UiKit.Image("Lock", tile.transform, art.Lock, (w - 80f) / 2f, 30f, 80f, 80f).color = DesignTokens.InkSoft;
                    UiKit.Text("Level", tile.transform, art.Font, t.Locked, 36f, DesignTokens.InkSoft, 0f, 112f, w, 50f);
                }
                else
                {
                    var icon = art.Icons != null && i < art.Icons.Length ? art.Icons[i] : null;
                    UiKit.Image("Icon", tile.transform, icon, (w - 90f) / 2f, 34f, 90f, 90f).color = DesignTokens.Secondary;
                    UiKit.Text("Name", parent, art.Font, t.Name, DesignTokens.TypeBoosterLabel, DesignTokens.Ink,
                        x - 20f, y - DesignTokens.BoosterLabelGap - DesignTokens.BoosterLabelHeight, w + 40f, DesignTokens.BoosterLabelHeight, fromBottom: true);
                }
                if (t.Badge != null)
                {
                    float b = DesignTokens.BoosterBadge;
                    var badge = UiKit.Image("Badge", tile.transform, art.Badge, w - 50f, -22f, b, b - 2f, sliced: true);
                    UiKit.Text("Count", badge.transform, art.Font, t.Badge, 40f, DesignTokens.OnColor, 0f, 0f, b, b - 10f);
                }
                if (t.Price != null)
                {
                    var tag = UiKit.Image("Price", tile.transform, art.PriceTag, 18f, h - 50f, w - 36f, 64f, sliced: true);
                    var label = UiKit.Text("Amount", tag.transform, art.Font, t.Price, 34f, DesignTokens.OnColor, 0f, 0f, w - 36f, 60f);
                    float tw = label.GetPreferredValues(t.Price).x, coin = 40f, total = coin + 6f + tw;
                    UiKit.Image("Coin", tag.transform, art.Coin, (w - 36f - total) / 2f, 10f, coin, coin);
                    label.rectTransform.anchoredPosition = new Vector2((w - 36f - total) / 2f + coin + 6f, 0f);
                    label.rectTransform.sizeDelta = new Vector2(tw + 4f, 60f);
                }
                int index = i;
                UiKit.Button(tile, () => pressed?.Invoke(index));
            }
        }
    }

    /// <summary>Relays a tap on a board element as its index (rule #9: EventSystem only, no polling;
    /// rule #10: the controller gets a game-unit index, never a screen position).</summary>
    public sealed class TapRelay : MonoBehaviour, IPointerClickHandler
    {
        public int Index;
        public Action<int> Tapped;

        public void OnPointerClick(PointerEventData eventData) => Tapped?.Invoke(Index);
    }

    /// <summary>An invisible tap area: takes EventSystem raycasts but builds no mesh (a clear Image still sends a quad to
    /// the canvas — overdraw on mobile).</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class HitArea : Graphic
    {
        protected override void OnPopulateMesh(VertexHelper vh) => vh.Clear();
    }

    /// <summary>
    /// Indexed runtime uGUI kept across redraws instead of destroyed and rebuilt each time. Per redraw: <see cref="Begin"/>,
    /// then <see cref="Fetch{T}"/> each element by (kind, index) — false means build it and <see cref="Keep{T}"/> it — and
    /// <see cref="End"/> hides whatever this redraw did not ask for.
    /// </summary>
    public sealed class UiSlots
    {
        private struct Entry { public Component Component; public int Gen; }
        private readonly Dictionary<(string, int), Entry> _entries = new Dictionary<(string, int), Entry>();
        private int _gen;

        public void Begin() => _gen++;

        public bool Fetch<T>(string kind, int index, out T component) where T : Component
        {
            if (_entries.TryGetValue((kind, index), out var e) && e.Component != null)
            {
                _entries[(kind, index)] = new Entry { Component = e.Component, Gen = _gen };
                if (!e.Component.gameObject.activeSelf) e.Component.gameObject.SetActive(true);
                component = (T)e.Component;
                return true;
            }
            component = null;
            return false;
        }

        public T Keep<T>(string kind, int index, T component) where T : Component
        {
            _entries[(kind, index)] = new Entry { Component = component, Gen = _gen };
            return component;
        }

        public void End()
        {
            foreach (var e in _entries.Values)
                if (e.Gen != _gen && e.Component != null && e.Component.gameObject.activeSelf) e.Component.gameObject.SetActive(false);
        }
    }

    /// <summary>Keeps legacy content proportional while the owning canvas uses native Figma pixels.</summary>
    [DisallowMultipleComponent]
    public sealed class LegacyCanvasLayout : MonoBehaviour
    {
        private bool _applying;

        private void OnEnable() => Apply();
        private void OnRectTransformDimensionsChange() => Apply();

        private void Apply()
        {
            if (_applying || !(transform is RectTransform rt) || !(rt.parent is RectTransform parent)) return;
            _applying = true;
            try
            {
                var canvas = GetComponentInParent<Canvas>();
                var scaler = canvas != null ? canvas.GetComponent<ClassicSpins.PrototypeFramework.Views.WorldSpaceCanvasScaler>() : null;
                // Capture fixtures retain their historical 1080-unit rig; the live game reads its own scaler.
                float scale = scaler != null ? scaler.ReferenceResolution.x / 1080f : 1f;
                if (scale <= 0f) scale = 1f;
                rt.localScale = new Vector3(scale, scale, 1f);
                rt.sizeDelta = parent.rect.size * (1f / scale - 1f);
            }
            finally { _applying = false; }
        }
    }
}
