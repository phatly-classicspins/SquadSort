using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ClassicSpins.Cheats
{
    /// <summary>
    /// The floating button and the panel behind it: a Cheats tab drawn from a <see cref="CheatRegistry"/> and a Logs tab
    /// over a <see cref="CheatLogBuffer"/>. Created by <see cref="CheatMenu.Create"/>; destroy its GameObject to remove it.
    /// </summary>
    public sealed class CheatMenuView : MonoBehaviour
    {
        private const string TogglePosKey = "cs.cheats.toggle";
        private const float PollSeconds = 0.25f;
        private const int LogCharBudget = 12000;   // a legacy Text stays under the 65k-vertex mesh limit
        private const int LogLineMax = 400;

        private CheatRegistry _registry;
        private CheatMenuOptions _options;
        private CheatLogBuffer _logs;

        private RectTransform _safe, _panel, _cheatsPage, _logsPage, _cheatsContent;
        private RectTransform _toggleRect;
        private CheatDragHandle _drag;
        private Image _toggleImage, _tabCheats, _tabLogs, _errorsOnlyImage;
        private Text _toggleLabel, _status, _logText;
        private ScrollRect _logScroll;
        private Rect _lastSafeArea;
        private bool _logsTab, _errorsOnly, _rebuildCheats, _logsToBottom;
        private int _logVersionShown = -1, _badgeShown = -1;
        private float _nextPoll;

        private readonly List<(Text text, Func<string> read)> _readouts = new List<(Text, Func<string>)>();
        private readonly List<(Image image, Text text, CheatItem item)> _toggles = new List<(Image, Text, CheatItem)>();

        public CheatRegistry Registry => _registry;
        /// <summary>The captured log (null when <see cref="CheatMenuOptions.CaptureLogs"/> is off).</summary>
        public CheatLogBuffer Logs => _logs;
        public bool IsOpen => _panel != null && _panel.gameObject.activeSelf;

        /// <summary>Show or hide the floating button (and the panel with it) — e.g. while capturing screenshots.</summary>
        public void SetVisible(bool visible)
        {
            if (_safe != null) _safe.gameObject.SetActive(visible);
        }

        public void Open(bool logs = false)
        {
            _panel.gameObject.SetActive(true);
            _toggleRect.gameObject.SetActive(false);   // the panel's own X closes it
            _rebuildCheats = true;                     // number fields pick up their current defaults
            ShowTab(logs);
            if (!logs) RebuildCheats();
            LayoutPanel();
        }

        public void Close()
        {
            _panel.gameObject.SetActive(false);
            _toggleRect.gameObject.SetActive(true);
        }

        internal void Build(CheatRegistry registry, CheatMenuOptions options)
        {
            _registry = registry;
            _options = options;
            gameObject.layer = options.Layer;
            if (options.CaptureLogs) _logs = new CheatLogBuffer(options.LogCapacity);

            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = options.SortingOrder;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = options.ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = options.MatchWidthOrHeight;
            gameObject.AddComponent<GraphicRaycaster>();

            _safe = CheatUi.Rect("SafeArea", transform);
            ApplySafeArea();

            BuildPanel();
            BuildToggle();

            _registry.Changed += OnRegistryChanged;
            _registry.StatusChanged += OnStatus;
            _rebuildCheats = true;
            if (_logs != null) Application.logMessageReceivedThreaded += OnLog;
        }

        private void Start()
        {
            if (EventSystem.current == null)
                Debug.LogWarning("[CheatMenu] no EventSystem in the scene — the cheat button cannot be tapped.");
        }

        private void OnDestroy()
        {
            if (_registry != null)
            {
                _registry.Changed -= OnRegistryChanged;
                _registry.StatusChanged -= OnStatus;
            }
            if (_logs != null) Application.logMessageReceivedThreaded -= OnLog;
        }

        private void Update()
        {
            // a domain reload in play mode ("Recompile and Continue Playing") keeps this component but drops every reference
            if (_registry == null || _toggleLabel == null) { Destroy(gameObject); return; }
            if (Screen.safeArea != _lastSafeArea) { ApplySafeArea(); _drag.Clamp(); }
            UpdateBadge();
            if (!IsOpen) return;
            if (_rebuildCheats && !_logsTab) RebuildCheats();
            LayoutPanel();
            if (_logsTab) { RefreshLogs(); return; }
            if (Time.unscaledTime < _nextPoll) return;
            _nextPoll = Time.unscaledTime + PollSeconds;
            RefreshLiveItems();
        }

        // ── layout ────────────────────────────────────────────────────────────────────────────────────

        private void ApplySafeArea()
        {
            _lastSafeArea = Screen.safeArea;
            float w = Mathf.Max(1, Screen.width), h = Mathf.Max(1, Screen.height);
            _safe.anchorMin = new Vector2(_lastSafeArea.xMin / w, _lastSafeArea.yMin / h);
            _safe.anchorMax = new Vector2(_lastSafeArea.xMax / w, _lastSafeArea.yMax / h);
            _safe.offsetMin = Vector2.zero; _safe.offsetMax = Vector2.zero;
        }

        private void BuildToggle()
        {
            var b = CheatUi.Button("CheatToggle", _safe, _options.ToggleLabel, CheatTheme.ToggleIdle, OnTogglePressed, out _toggleLabel);
            _toggleImage = (Image)b.targetGraphic;
            _toggleRect = (RectTransform)b.transform;
            _toggleRect.anchorMin = _toggleRect.anchorMax = Vector2.zero;
            _toggleRect.pivot = new Vector2(0.5f, 0.5f);
            _toggleRect.sizeDelta = new Vector2(112, 44);
            _toggleLabel.fontSize = CheatTheme.SmallFont; _toggleLabel.resizeTextMaxSize = CheatTheme.SmallFont;
            _drag = b.gameObject.AddComponent<CheatDragHandle>();
            _drag.Bounds = _safe;
            _drag.Moved = SaveTogglePosition;
            _toggleRect.anchoredPosition = LoadTogglePosition();
            Canvas.ForceUpdateCanvases();
            _drag.Clamp();
        }

        private void BuildPanel()
        {
            var bg = CheatUi.Panel("CheatPanel", _safe, CheatTheme.Scrim);
            _panel = bg.rectTransform;   // sized by LayoutPanel: a small card top-left, full screen for Logs

            var header = CheatUi.Panel("Header", _panel, CheatTheme.Bar).rectTransform;
            CheatUi.TopBand(header, 0, CheatTheme.Header);
            var row = header.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.padding = new RectOffset(6, 6, 6, 6);
            row.spacing = CheatTheme.Gap;
            row.childControlWidth = true; row.childControlHeight = true;
            row.childForceExpandWidth = false; row.childForceExpandHeight = true;
            _tabCheats = (Image)CheatUi.Button("TabCheats", header, "Cheats", CheatTheme.Accent, () => ShowTab(false), out _).targetGraphic;
            CheatUi.Size(_tabCheats, 36, 1);
            _tabLogs = (Image)CheatUi.Button("TabLogs", header, "Logs", CheatTheme.Button, () => ShowTab(true), out _).targetGraphic;
            CheatUi.Size(_tabLogs, 36, 1);
            var close = CheatUi.Button("Close", header, "X", CheatTheme.Danger, Close, out _);
            CheatUi.Size(close, 36, 0, 48);

            _status = CheatUi.Label("Status", _panel, string.Empty, CheatTheme.SmallFont, CheatTheme.Muted);
            CheatUi.TopBand(_status.rectTransform, CheatTheme.Header, CheatTheme.Status, CheatTheme.Pad);

            float top = CheatTheme.Header + CheatTheme.Status;
            _cheatsPage = CheatUi.Rect("CheatsPage", _panel);
            CheatUi.Stretch(_cheatsPage, 0, 0, 0, top);
            CheatUi.Scroll(_cheatsPage, out _cheatsContent);
            CheatUi.Stretch((RectTransform)_cheatsPage.GetChild(0));

            _logsPage = CheatUi.Rect("LogsPage", _panel);
            CheatUi.Stretch(_logsPage, 0, 0, 0, top);
            var tools = CheatUi.Rect("LogTools", _logsPage);
            CheatUi.BottomBand(tools, CheatTheme.Pad * 0.5f, CheatTheme.Row, CheatTheme.Pad);
            var toolRow = tools.gameObject.AddComponent<HorizontalLayoutGroup>();
            toolRow.spacing = CheatTheme.Gap;
            toolRow.childControlWidth = true; toolRow.childControlHeight = true;
            toolRow.childForceExpandWidth = true; toolRow.childForceExpandHeight = true;
            CheatUi.Button("Clear", tools, "Clear", CheatTheme.Button, () => { _logs?.Clear(); }, out _);
            CheatUi.Button("Copy", tools, "Copy all", CheatTheme.Button, CopyLogs, out _);
            _errorsOnlyImage = (Image)CheatUi.Button("ErrorsOnly", tools, "Errors only", CheatTheme.Button, ToggleErrorsOnly, out _).targetGraphic;
            var logArea = CheatUi.Rect("LogArea", _logsPage);
            CheatUi.Stretch(logArea, 0, CheatTheme.Row + CheatTheme.Pad, 0, 0);
            _logScroll = CheatUi.Scroll(logArea, out var logContent);
            CheatUi.Stretch((RectTransform)_logScroll.transform);
            _logText = CheatUi.Label("Log", logContent, string.Empty, CheatTheme.LogFont, CheatTheme.Text, TextAnchor.UpperLeft);
            _logText.verticalOverflow = VerticalWrapMode.Overflow;
            _logText.supportRichText = true;

            _panel.gameObject.SetActive(false);
            ShowTab(false);
        }

        // Cheats: a narrow card at the top-left, as tall as its items (scrolls past the safe area). Logs: the whole safe area.
        private void LayoutPanel()
        {
            float margin = _options.PanelMargin;
            var safe = _safe.rect.size;
            _panel.anchorMin = _panel.anchorMax = new Vector2(0, 1);
            _panel.pivot = new Vector2(0, 1);
            _panel.anchoredPosition = new Vector2(margin, -margin);
            float maxH = Mathf.Max(CheatTheme.Header * 2, safe.y - margin * 2);
            if (_logsTab)
            {
                _panel.sizeDelta = new Vector2(Mathf.Max(0, safe.x - margin * 2), maxH);
                return;
            }
            float width = Mathf.Min(_options.PanelWidth, safe.x - margin * 2);
            float h = CheatTheme.Header + CheatTheme.Status + LayoutUtility.GetPreferredHeight(_cheatsContent);
            _panel.sizeDelta = new Vector2(width, Mathf.Min(h, maxH));
        }

        private void ShowTab(bool logs)
        {
            _logsTab = logs;
            _cheatsPage.gameObject.SetActive(!logs);
            _logsPage.gameObject.SetActive(logs);
            _tabCheats.color = logs ? CheatTheme.Button : CheatTheme.Accent;
            _tabLogs.color = logs ? CheatTheme.Accent : CheatTheme.Button;
            if (logs) { _logVersionShown = -1; _logsToBottom = true; _logs?.MarkErrorsSeen(); }
            else _nextPoll = 0;
        }

        private void OnTogglePressed()
        {
            if (_drag.JustDragged) return;
            if (IsOpen) Close(); else Open();
        }

        private void UpdateBadge()
        {
            int errors = _logs?.UnseenErrors ?? 0;
            if (errors == _badgeShown) return;
            _badgeShown = errors;
            _toggleLabel.text = errors > 0 ? $"{_options.ToggleLabel} !{errors}" : _options.ToggleLabel;
            _toggleImage.color = errors > 0 ? CheatTheme.Danger : CheatTheme.ToggleIdle;
        }

        private Vector2 LoadTogglePosition()
        {
            var start = new Vector2(60, 0);
            try
            {
                var saved = PlayerPrefs.GetString(TogglePosKey, string.Empty);
                var parts = saved.Split(';');
                if (parts.Length == 2 && float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x)
                    && float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var y))
                    return new Vector2(x, y);
            }
            catch (Exception) { }
            var size = _safe.rect.size;
            if (size.y <= 0) size = _options.ReferenceResolution;
            start.x = Mathf.Max(start.x, size.x * _options.ToggleStart.x);
            start.y = size.y * _options.ToggleStart.y;
            return start;
        }

        private void SaveTogglePosition()
        {
            var p = _toggleRect.anchoredPosition;
            try
            {
                PlayerPrefs.SetString(TogglePosKey, p.x.ToString(System.Globalization.CultureInfo.InvariantCulture) + ";" + p.y.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            catch (Exception) { }
        }

        // ── cheats tab ───────────────────────────────────────────────────────────────────────────────

        private void OnRegistryChanged() => _rebuildCheats = true;
        private void OnStatus(string message) { if (_status != null) _status.text = message; }

        private void RebuildCheats()
        {
            _rebuildCheats = false;
            _readouts.Clear();
            _toggles.Clear();
            for (int i = _cheatsContent.childCount - 1; i >= 0; i--) Destroy(_cheatsContent.GetChild(i).gameObject);

            foreach (var section in _registry.Sections)
            {
                var title = CheatUi.Label("Section", _cheatsContent, section.ToUpperInvariant(), CheatTheme.SectionFont, CheatTheme.Section);
                title.fontStyle = FontStyle.Bold;
                CheatUi.Size(title, CheatTheme.SectionRow);
                HorizontalLayoutGroup buttonRow = null;
                foreach (var item in _registry.ItemsIn(section))
                {
                    if (item.Kind != CheatKind.Button) buttonRow = null;
                    switch (item.Kind)
                    {
                        case CheatKind.Button:
                            // buttons pair up two to a row
                            if (buttonRow == null || buttonRow.transform.childCount >= 2) buttonRow = CheatUi.Row(_cheatsContent, CheatTheme.Row);
                            var captured = item;
                            CheatUi.Button("Button", buttonRow.transform, item.Label, CheatTheme.Button, () => Invoke(captured, captured.Click), out _);
                            break;
                        case CheatKind.Number:
                            AddNumber(item);
                            break;
                        case CheatKind.Toggle:
                            AddToggle(item);
                            break;
                        case CheatKind.Text:
                            var text = CheatUi.Label("Readout", _cheatsContent, string.Empty, CheatTheme.SmallFont, CheatTheme.Text);
                            text.verticalOverflow = VerticalWrapMode.Overflow;   // the content's layout group sizes it to its preferred height
                            _readouts.Add((text, item.Read));
                            break;
                    }
                }
            }
            RefreshLiveItems();
        }

        // like the old panel: [ value ][ Label ] — the button carries the label, so a row fits the narrow card
        private void AddNumber(CheatItem item)
        {
            var row = CheatUi.Row(_cheatsContent, CheatTheme.Row);
            int start = item.DefaultNumber;
            if (item.ReadDefault != null) { try { start = item.ReadDefault(); } catch (Exception) { } }
            var field = CheatUi.NumberField(row.transform, start);
            CheatUi.Size(field, CheatTheme.Row, 1, 0);      // widths split by flex only, so every row lines up
            string caption = string.IsNullOrEmpty(item.Label) ? item.SubmitLabel : item.Label;
            var b = CheatUi.Button("Submit", row.transform, caption, CheatTheme.Accent, null, out _);
            CheatUi.Size(b, CheatTheme.Row, 1.4f, 0);
            b.onClick.AddListener(() =>
            {
                if (!int.TryParse(field.text, out var value)) { _registry.Status($"{item.Label}: not a whole number"); return; }
                Invoke(item, () => item.Submit?.Invoke(value));
            });
        }

        private void AddToggle(CheatItem item)
        {
            var row = CheatUi.Row(_cheatsContent, CheatTheme.Row);
            Image image = null;
            Text text = null;
            var b = CheatUi.Button("Toggle", row.transform, item.Label, CheatTheme.Button, () =>
            {
                bool now = Read(item);
                Invoke(item, () => item.Set?.Invoke(!now));
                Paint(image, text, item);
            }, out text);
            image = (Image)b.targetGraphic;
            _toggles.Add((image, text, item));
            Paint(image, text, item);
        }

        private void Invoke(CheatItem item, Action action)
        {
            var error = _registry.Run(item, action);
            if (error != null) Debug.LogException(error);
            _nextPoll = 0;   // read-outs catch up on the next frame
        }

        private void RefreshLiveItems()
        {
            foreach (var (text, read) in _readouts)
            {
                string value;
                try { value = read?.Invoke() ?? string.Empty; }
                catch (Exception e) { value = $"<{e.GetType().Name}>"; }
                if (text.text != value) text.text = value;
            }
            foreach (var (image, text, item) in _toggles) Paint(image, text, item);
        }

        private static bool Read(CheatItem item)
        {
            try { return item.Get != null && item.Get(); }
            catch (Exception) { return false; }
        }

        private static void Paint(Image image, Text text, CheatItem item)
        {
            bool on = Read(item);
            image.color = on ? CheatTheme.On : CheatTheme.Button;
            string caption = $"{item.Label}: {(on ? "ON" : "OFF")}";
            if (text.text != caption) text.text = caption;
        }

        // ── logs tab ─────────────────────────────────────────────────────────────────────────────────

        private void OnLog(string message, string stackTrace, LogType type)
        {
            var level = type == LogType.Log ? CheatLogLevel.Info : type == LogType.Warning ? CheatLogLevel.Warning : CheatLogLevel.Error;
            _logs?.Add(new CheatLogEntry(level, message, stackTrace, DateTime.Now));
        }

        private void ToggleErrorsOnly()
        {
            _errorsOnly = !_errorsOnly;
            _errorsOnlyImage.color = _errorsOnly ? CheatTheme.Accent : CheatTheme.Button;
            _logVersionShown = -1;
            _logsToBottom = true;
        }

        private void CopyLogs()
        {
            if (_logs == null) return;
            GUIUtility.systemCopyBuffer = _logs.ToPlainText();
            _registry.Status($"Copied {_logs.Count} log lines");
        }

        private void RefreshLogs()
        {
            if (_logs == null) { _logText.text = "Log capture is off (CheatMenuOptions.CaptureLogs)."; return; }
            _logs.MarkErrorsSeen();
            if (_logs.Version == _logVersionShown) return;
            _logVersionShown = _logs.Version;
            bool atBottom = _logScroll.verticalNormalizedPosition <= 0.01f;

            // newest last; walk back from the newest until the character budget is spent
            var entries = _logs.Snapshot();
            var lines = new List<string>();
            int budget = LogCharBudget;
            for (int i = entries.Count - 1; i >= 0 && budget > 0; i--)
            {
                var e = entries[i];
                if (_errorsOnly && e.Level != CheatLogLevel.Error) continue;
                string msg = e.Message.Length > LogLineMax ? e.Message.Substring(0, LogLineMax) + "…" : e.Message;
                msg = msg.Replace("<", "<​");   // a log line must not open rich-text tags
                string colour = e.Level == CheatLogLevel.Error ? "#FF7A7A" : e.Level == CheatLogLevel.Warning ? "#FFD27A" : "#DDE3EE";
                string line = $"<color=#8A93A6>{e.Time:HH:mm:ss}</color> <color={colour}>{msg}</color>";
                lines.Add(line);
                budget -= msg.Length + 40;
            }
            lines.Reverse();
            var sb = new StringBuilder();
            foreach (var line in lines) sb.AppendLine(line);
            _logText.text = lines.Count == 0 ? "<color=#8A93A6>No log lines yet.</color>" : sb.ToString();
            if (atBottom || _logsToBottom)
            {
                _logsToBottom = false;
                Canvas.ForceUpdateCanvases();
                _logScroll.verticalNormalizedPosition = 0f;
            }
        }
    }
}
