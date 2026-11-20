using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ClassicSpins.Cheats
{
    /// <summary>The panel's own palette and sizes (reference pixels). A tester tool, so it keeps one look in every game.</summary>
    internal static class CheatTheme
    {
        public static readonly Color Scrim = new Color(0.06f, 0.07f, 0.10f, 1f);
        public static readonly Color Bar = new Color(0.10f, 0.12f, 0.16f, 1f);
        public static readonly Color Button = new Color(0.20f, 0.24f, 0.32f, 1f);
        public static readonly Color Accent = new Color(0.16f, 0.52f, 0.86f, 1f);
        public static readonly Color On = new Color(0.20f, 0.62f, 0.36f, 1f);
        public static readonly Color Danger = new Color(0.78f, 0.24f, 0.24f, 1f);
        public static readonly Color Field = new Color(0.93f, 0.94f, 0.96f, 1f);
        public static readonly Color FieldText = new Color(0.08f, 0.09f, 0.12f, 1f);
        public static readonly Color Text = new Color(0.93f, 0.95f, 0.98f, 1f);
        public static readonly Color Muted = new Color(0.62f, 0.67f, 0.76f, 1f);
        public static readonly Color Section = new Color(1.00f, 0.80f, 0.36f, 1f);
        public static readonly Color ToggleIdle = new Color(0.10f, 0.12f, 0.16f, 0.72f);

        public const int Font = 20, SmallFont = 18, SectionFont = 18, LogFont = 16;
        public const float Row = 42f, Gap = 6f, Pad = 8f, Header = 48f, Status = 28f, SectionRow = 26f;
    }

    /// <summary>Builds uGUI objects in code, so the package ships no prefab and needs no TMP font asset.</summary>
    internal static class CheatUi
    {
        private static Font _font;

        public static Font Font
        {
            get
            {
                if (_font != null) return _font;
                try { _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch (Exception) { }
                if (_font == null) { try { _font = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch (Exception) { } }
                return _font;
            }
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static void Stretch(RectTransform rt, float left = 0, float bottom = 0, float right = 0, float top = 0)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom); rt.offsetMax = new Vector2(-right, -top);
        }

        /// <summary>A band glued to the top edge of its parent.</summary>
        public static void TopBand(RectTransform rt, float fromTop, float height, float side = 0)
        {
            rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(0.5f, 1);
            rt.offsetMin = new Vector2(side, -fromTop - height); rt.offsetMax = new Vector2(-side, -fromTop);
        }

        /// <summary>A band glued to the bottom edge of its parent.</summary>
        public static void BottomBand(RectTransform rt, float fromBottom, float height, float side = 0)
        {
            rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(1, 0); rt.pivot = new Vector2(0.5f, 0);
            rt.offsetMin = new Vector2(side, fromBottom); rt.offsetMax = new Vector2(-side, fromBottom + height);
        }

        public static Image Panel(string name, Transform parent, Color color)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            return img;
        }

        public static Text Label(string name, Transform parent, string text, int size, Color color, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font; t.fontSize = size; t.color = color; t.alignment = align; t.text = text;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Truncate;
            t.raycastTarget = false;
            return t;
        }

        public static Button Button(string name, Transform parent, string caption, Color color, Action click, out Text label)
        {
            var img = Panel(name, parent, color);
            var b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var colors = b.colors;
            colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
            colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            b.colors = colors;
            label = Label("Label", img.transform, caption, CheatTheme.Font, CheatTheme.Text, TextAnchor.MiddleCenter);
            label.resizeTextForBestFit = true; label.resizeTextMinSize = 12; label.resizeTextMaxSize = CheatTheme.Font;
            Stretch(label.rectTransform, 6, 2, 6, 2);
            if (click != null) b.onClick.AddListener(() => click());
            return b;
        }

        public static InputField NumberField(Transform parent, int value)
        {
            var img = Panel("Field", parent, CheatTheme.Field);
            img.gameObject.SetActive(false);   // InputField wants its text component before it enables
            var text = Label("Text", img.transform, string.Empty, CheatTheme.Font, CheatTheme.FieldText);
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            Stretch(text.rectTransform, 10, 2, 10, 2);
            var hint = Label("Placeholder", img.transform, "0", CheatTheme.Font, new Color(0.5f, 0.52f, 0.56f, 1f));
            hint.fontStyle = FontStyle.Italic;
            Stretch(hint.rectTransform, 10, 2, 10, 2);
            var field = img.gameObject.AddComponent<InputField>();
            field.targetGraphic = img;
            field.textComponent = text;
            field.placeholder = hint;
            field.contentType = InputField.ContentType.IntegerNumber;
            field.lineType = InputField.LineType.SingleLine;
            field.text = value.ToString();
            img.gameObject.SetActive(true);
            return field;
        }

        public static LayoutElement Size(Component c, float minHeight, float flexibleWidth = -1, float minWidth = -1)
        {
            var le = c.gameObject.GetComponent<LayoutElement>();   // not ??: a missing component is Unity-null, not C# null
            if (le == null) le = c.gameObject.AddComponent<LayoutElement>();
            le.layoutPriority = 2;   // above InputField's own (1), which would size the field by its text
            le.minHeight = minHeight; le.preferredHeight = minHeight;
            if (flexibleWidth >= 0) le.flexibleWidth = flexibleWidth;
            if (minWidth >= 0) { le.minWidth = minWidth; le.preferredWidth = minWidth; }
            return le;
        }

        public static HorizontalLayoutGroup Row(Transform parent, float height)
        {
            var rt = Rect("Row", parent);
            var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = CheatTheme.Gap;
            h.childControlWidth = true; h.childControlHeight = true;
            h.childForceExpandWidth = true; h.childForceExpandHeight = true;
            Size(h, height);
            return h;
        }

        /// <summary>A vertical scroll view filling <paramref name="parent"/>; items go into the returned content.</summary>
        public static ScrollRect Scroll(Transform parent, out RectTransform content)
        {
            var root = Rect("Scroll", parent);
            var scroll = root.gameObject.AddComponent<ScrollRect>();
            var viewport = Rect("Viewport", root);
            Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            var hit = viewport.gameObject.AddComponent<Image>();   // so a drag on empty space still scrolls
            hit.color = new Color(0, 0, 0, 0);
            content = Rect("Content", viewport);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(0.5f, 1);
            content.offsetMin = Vector2.zero; content.offsetMax = Vector2.zero;
            var v = content.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = CheatTheme.Gap;
            v.padding = new RectOffset((int)CheatTheme.Pad, (int)CheatTheme.Pad, (int)CheatTheme.Pad, (int)CheatTheme.Pad * 2);
            v.childControlWidth = true; v.childControlHeight = true;
            v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport; scroll.content = content;
            scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            return scroll;
        }
    }

    /// <summary>Lets the floating button be dragged out of the way; the tap that ends a drag does not open the panel.</summary>
    internal sealed class CheatDragHandle : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public RectTransform Bounds;
        public Action Moved;
        public bool JustDragged { get; private set; }

        private RectTransform _rt;
        private Canvas _canvas;

        private void Awake()
        {
            _rt = (RectTransform)transform;
            _canvas = GetComponentInParent<Canvas>();
        }

        public void OnPointerDown(PointerEventData e) => JustDragged = false;
        public void OnBeginDrag(PointerEventData e) => JustDragged = true;

        public void OnDrag(PointerEventData e)
        {
            float scale = _canvas != null && _canvas.scaleFactor > 0 ? _canvas.scaleFactor : 1f;
            _rt.anchoredPosition += e.delta / scale;
            Clamp();
        }

        public void OnEndDrag(PointerEventData e) => Moved?.Invoke();

        public void Clamp()
        {
            if (Bounds == null) return;
            var size = Bounds.rect.size;
            var half = _rt.rect.size * 0.5f;
            // the button is anchored at the bottom-left of the bounds with a centred pivot
            var p = _rt.anchoredPosition;
            p.x = Mathf.Clamp(p.x, half.x, Mathf.Max(half.x, size.x - half.x));
            p.y = Mathf.Clamp(p.y, half.y, Mathf.Max(half.y, size.y - half.y));
            _rt.anchoredPosition = p;
        }
    }
}
