using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ClassicSpins.Rating
{
    /// <summary>
    /// The default popup (Money Sort's RatingUI, built in code): a dimmed screen, a panel with a title band and a close
    /// button, the question, five stars and Submit (off until a star is picked); then the thank-you with Continue.
    /// It draws on its own overlay canvas, so it needs nothing from the scene except an <see cref="EventSystem"/>, and it
    /// survives scene loads. Text and colours come from <see cref="RatingPopupStyle"/>.
    /// </summary>
    public sealed class RatingPopup : IRatingPopup, IDisposable
    {
        private const float PanelW = 880f, PanelH = 720f, StarSize = 132f, StarStep = 152f;

        private readonly RatingPopupStyle _style;
        private GameObject _root;
        private RatingPopupMotion _motion;
        private RectTransform _panel;
        private Text _title;
        private GameObject _ask, _thanks;
        private Text _thanksText;
        private Image[] _starFace, _starEdge;
        private RectTransform[] _starNode;
        private Button _submit;
        private Image _submitFace, _submitEdge;
        private int _selected;
        private Action<int> _finish;

        public RatingPopup(RatingPopupStyle style) { _style = style ?? new RatingPopupStyle(); }

        public bool IsVisible => _root != null && _root.activeSelf;

        public Task<int> AskAsync(CancellationToken ct)
        {
            var tcs = Begin(ct);
            _title.text = _style.Title;
            _ask.SetActive(true);
            _thanks.SetActive(false);
            Select(0, punch: false);
            return tcs.Task;
        }

        public async Task ThanksAsync(bool positive, CancellationToken ct)
        {
            var tcs = Begin(ct);
            _title.text = _style.ThanksTitle;
            _thanksText.text = positive ? _style.ThanksPositive : _style.ThanksNegative;
            _ask.SetActive(false);
            _thanks.SetActive(true);
            await tcs.Task;
        }

        /// <summary>The Android back button: closes the popup (as the ✕ would). False when nothing is up.</summary>
        public bool HandleBack()
        {
            if (!IsVisible || _finish == null) return false;
            Finish(0);
            return true;
        }

        public void Dispose()
        {
            if (_root != null) UnityEngine.Object.Destroy(_root);
            _root = null;
        }

        // ── flow ─────────────────────────────────────────────────────────────────────────────────
        private TaskCompletionSource<int> Begin(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            Build();
            _finish?.Invoke(0);   // a popup still up from an earlier call ends as closed
            var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            CancellationTokenRegistration reg = default;
            _finish = stars =>
            {
                _finish = null;
                reg.Dispose();
                Hide();
                tcs.TrySetResult(stars);
            };
            reg = ct.Register(() =>
            {
                _finish = null;
                Hide();
                tcs.TrySetCanceled();
            });
            if (EventSystem.current == null)
                Debug.LogWarning("[Rating] no EventSystem in the scene: the popup cannot be tapped.");
            _root.SetActive(true);
            _motion.Show();
            return tcs;
        }

        private void Finish(int stars) => _finish?.Invoke(stars);

        private void Hide()
        {
            if (_root != null) _root.SetActive(false);
        }

        private void Select(int stars, bool punch)
        {
            _selected = Mathf.Clamp(stars, 0, _starFace.Length);
            for (int i = 0; i < _starFace.Length; i++)
            {
                bool on = i < _selected;
                Paint(_starFace[i], on ? _style.StarOnSprite : _style.StarOffSprite, on ? _style.StarOn : _style.StarOff, RatingArt.Star);
                _starEdge[i].enabled = (on ? _style.StarOnSprite : _style.StarOffSprite) == null;
                _starEdge[i].color = on ? _style.StarOnEdge : _style.StarOffEdge;
                if (punch && on) _motion.Punch(_starNode[i], i * 0.04f);
            }
            bool ready = _selected > 0;
            _submit.interactable = ready;
            _submitFace.color = _style.ButtonSprite != null ? (ready ? Color.white : new Color(1f, 1f, 1f, 0.5f)) : ready ? _style.Button : _style.ButtonDisabled;
            _submitEdge.color = ready ? _style.ButtonEdge : _style.ButtonDisabledEdge;
        }

        // ── building ─────────────────────────────────────────────────────────────────────────────
        private void Build()
        {
            if (_root != null) return;
            var s = _style;
            _root = new GameObject("[RatingPopup]", typeof(RectTransform)) { layer = s.Layer };
            UnityEngine.Object.DontDestroyOnLoad(_root);
            var canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = s.SortingOrder;
            var scaler = _root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = s.ReferenceResolution;
            scaler.matchWidthOrHeight = s.MatchWidthOrHeight;
            _root.AddComponent<GraphicRaycaster>();
            var group = _root.AddComponent<CanvasGroup>();
            _motion = _root.AddComponent<RatingPopupMotion>();

            var scrim = Img("Scrim", _root.transform, null, s.Scrim);
            scrim.raycastTarget = true;   // the game underneath takes no taps while the popup is up
            var srt = scrim.rectTransform;
            srt.anchorMin = Vector2.zero; srt.anchorMax = Vector2.one; srt.offsetMin = srt.offsetMax = Vector2.zero;

            _panel = Node("Panel", _root.transform, 0f, 0f, PanelW, PanelH);
            _motion.Bind(_panel, group);
            Block(_panel, "Body", 0f, 0f, PanelW, PanelH, 16f, s.PanelSprite, s.Panel, s.PanelEdge, RatingArt.Round);

            var header = Block(_panel, "Header", 0f, PanelH / 2f - 6f, 740f, 132f, 12f, s.HeaderSprite, s.Header, s.HeaderEdge, RatingArt.Round);
            _title = Label("Title", header, "", 54, s.OnColor, 680f, 112f);

            var close = Block(_panel, "Close", PanelW / 2f - 34f, PanelH / 2f - 30f, 108f, 108f, 9f, s.CloseSprite, s.Close, s.CloseEdge, RatingArt.Circle);
            if (s.CloseSprite == null) Cross(close, s.OnColor);
            Tap(close, () => Finish(0));

            // the question
            _ask = Node("Ask", _panel, 0f, 0f, PanelW, PanelH).gameObject;
            Label("Question", _ask.transform, s.Question, 50, s.Ink, 760f, 160f, 0f, 120f);
            _starFace = new Image[5]; _starEdge = new Image[5]; _starNode = new RectTransform[5];
            for (int i = 0; i < 5; i++)
            {
                int stars = i + 1;
                var node = Node($"Star{stars}", _ask.transform, (i - 2) * StarStep, -40f, StarSize, StarSize);
                _starEdge[i] = Img("Edge", node, RatingArt.Star, s.StarOffEdge);
                Fill(_starEdge[i].rectTransform, 0f, -8f);
                _starFace[i] = Img("Face", node, RatingArt.Star, s.StarOff);
                Fill(_starFace[i].rectTransform, 0f, 0f);
                _starNode[i] = node;
                Tap(node, () => Select(stars, punch: true), _starFace[i]);
            }
            var submit = Block(_ask.transform, "Submit", 0f, -232f, 440f, 136f, 12f, s.ButtonSprite, s.Button, s.ButtonEdge, RatingArt.Round);
            _submitFace = submit.GetComponent<Image>();
            _submitEdge = submit.parent.Find("SubmitEdge").GetComponent<Image>();
            Label("Label", submit, s.Submit, 62, s.OnColor, 400f, 120f);
            _submit = Tap(submit, () => { if (_selected > 0) Finish(_selected); });

            // the thank-you
            _thanks = Node("Thanks", _panel, 0f, 0f, PanelW, PanelH).gameObject;
            _thanksText = Label("Message", _thanks.transform, "", 50, s.Ink, 740f, 300f, 0f, 30f);
            var cont = Block(_thanks.transform, "Continue", 0f, -232f, 440f, 136f, 12f, s.ButtonSprite, s.Button, s.ButtonEdge, RatingArt.Round);
            Label("Label", cont, s.Continue, 62, s.OnColor, 400f, 120f);
            Tap(cont, () => Finish(0));

            _root.SetActive(false);
        }

        private Font Font
        {
            get
            {
                if (_style.Font != null) return _style.Font;
                Font f = null;
                try { f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch (Exception) { }
                if (f == null) { try { f = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch (Exception) { } }
                return _style.Font = f;
            }
        }

        private RectTransform Node(string name, Transform parent, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = _style.Layer };
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        private Image Img(string name, Transform parent, Sprite sprite, Color color)
        {
            var img = Node(name, parent, 0f, 0f, 0f, 0f).gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            if (sprite != null && sprite.border != Vector4.zero) img.type = Image.Type.Sliced;
            return img;
        }

        private static void Fill(RectTransform rt, float x, float y)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(x, y); rt.offsetMax = new Vector2(x, y);
        }

        private static void Paint(Image img, Sprite custom, Color tint, Sprite fallback)
        {
            img.sprite = custom != null ? custom : fallback;
            img.type = img.sprite != null && img.sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            img.color = custom != null ? Color.white : tint;
        }

        /// <summary>A chunky block — a darker edge slab peeking out <paramref name="depth"/> below the face — or the custom
        /// sprite alone. Returns the face (the edge is its sibling "&lt;name&gt;Edge").</summary>
        private RectTransform Block(Transform parent, string name, float x, float y, float w, float h, float depth,
            Sprite custom, Color face, Color edge, Sprite shape)
        {
            var e = Img(name + "Edge", parent, shape, edge);
            var ert = e.rectTransform;
            ert.anchoredPosition = new Vector2(x, y - depth); ert.sizeDelta = new Vector2(w, h);
            e.enabled = custom == null;
            var f = Img(name, parent, null, face);
            Paint(f, custom, face, shape);
            f.rectTransform.anchoredPosition = new Vector2(x, y); f.rectTransform.sizeDelta = new Vector2(w, h);
            return f.rectTransform;
        }

        private Text Label(string name, Transform parent, string text, int size, Color color, float w, float h, float x = 0f, float y = 0f)
        {
            var t = Node(name, parent, x, y, w, h).gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.fontStyle = FontStyle.Bold;
            t.color = color;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = Mathf.Max(18, size / 2);
            t.resizeTextMaxSize = size;
            t.raycastTarget = false;
            return t;
        }

        private void Cross(RectTransform parent, Color color)
        {
            for (int k = 0; k < 2; k++)
            {
                var bar = Img(k == 0 ? "CrossA" : "CrossB", parent, RatingArt.Round, color);
                bar.type = Image.Type.Sliced;
                bar.pixelsPerUnitMultiplier = 4f;   // the 40 px corner shrinks to fit a 14 px bar
                bar.rectTransform.sizeDelta = new Vector2(58f, 14f);
                bar.rectTransform.localRotation = Quaternion.Euler(0f, 0f, k == 0 ? 45f : -45f);
            }
        }

        private static Button Tap(RectTransform target, Action onClick, Graphic graphic = null)
        {
            var g = graphic != null ? graphic : target.GetComponent<Graphic>();
            if (g == null)
            {
                var hit = target.gameObject.AddComponent<Image>();
                hit.color = Color.clear;
                g = hit;
            }
            g.raycastTarget = true;
            var b = target.gameObject.AddComponent<Button>();
            b.targetGraphic = g;
            b.transition = Selectable.Transition.None;
            target.gameObject.AddComponent<RatingPress>();
            b.onClick.AddListener(() => onClick());
            return b;
        }
    }

    /// <summary>The popup's motion (unscaled time, so a paused game still animates it): pop-in, star punch.</summary>
    [AddComponentMenu("")]
    internal sealed class RatingPopupMotion : MonoBehaviour
    {
        private const float ShowTime = 0.22f, PunchTime = 0.28f;
        private RectTransform _panel;
        private CanvasGroup _group;
        private float _shown = -1f;
        private readonly System.Collections.Generic.List<(RectTransform rt, float at)> _punches = new System.Collections.Generic.List<(RectTransform, float)>();

        public void Bind(RectTransform panel, CanvasGroup group) { _panel = panel; _group = group; }

        public void Show()
        {
            _shown = Time.unscaledTime;
            Tick();
        }

        public void Punch(RectTransform rt, float delay) => _punches.Add((rt, Time.unscaledTime + delay));

        private void Update() => Tick();

        private void Tick()
        {
            if (_shown >= 0f && _panel != null)
            {
                float t = Mathf.Clamp01((Time.unscaledTime - _shown) / ShowTime);
                _panel.localScale = Vector3.one * Mathf.LerpUnclamped(0.86f, 1f, BackOut(t));
                if (_group != null) _group.alpha = Mathf.Clamp01(t * 2f);
                if (t >= 1f) _shown = -1f;
            }
            for (int i = _punches.Count - 1; i >= 0; i--)
            {
                var (rt, at) = _punches[i];
                float t = (Time.unscaledTime - at) / PunchTime;
                if (rt == null || t >= 1f) { if (rt != null) rt.localScale = Vector3.one; _punches.RemoveAt(i); continue; }
                rt.localScale = Vector3.one * (t < 0f ? 1f : 1f + 0.22f * Mathf.Sin(t * Mathf.PI));
            }
        }

        private void OnDisable()
        {
            foreach (var (rt, _) in _punches) if (rt != null) rt.localScale = Vector3.one;
            _punches.Clear();
        }

        private static float BackOut(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }
    }

    /// <summary>Press feedback for the popup's buttons: a quick squash while held.</summary>
    [AddComponentMenu("")]
    internal sealed class RatingPress : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public void OnPointerDown(PointerEventData e)
        {
            var b = GetComponent<Button>();
            if (b == null || b.interactable) transform.localScale = Vector3.one * 0.94f;
        }

        public void OnPointerUp(PointerEventData e) => transform.localScale = Vector3.one;
        public void OnPointerExit(PointerEventData e) => transform.localScale = Vector3.one;
    }
}
