using System;
using System.Collections.Generic;

namespace ClassicSpins.Cheats
{
    public enum CheatKind { Button, Number, Toggle, Text }

    /// <summary>One entry of the menu. Built through <see cref="CheatGroup"/>; read by the panel.</summary>
    public sealed class CheatItem
    {
        public CheatKind Kind { get; }
        public string Section { get; }
        public string Label { get; }
        /// <summary>Button: what a tap does.</summary>
        public Action Click { get; internal set; }
        /// <summary>Number: what the submit button does with the typed value.</summary>
        public Action<int> Submit { get; internal set; }
        /// <summary>Number: the value the field starts with.</summary>
        public int DefaultNumber { get; internal set; }
        /// <summary>Number: when set, read each time the panel opens instead of <see cref="DefaultNumber"/> (e.g. the current level).</summary>
        public Func<int> ReadDefault { get; internal set; }
        /// <summary>Number: the submit button's caption.</summary>
        public string SubmitLabel { get; internal set; }
        /// <summary>Toggle: the current state, read every time the panel draws it.</summary>
        public Func<bool> Get { get; internal set; }
        /// <summary>Toggle: apply a new state.</summary>
        public Action<bool> Set { get; internal set; }
        /// <summary>Text: a read-out, polled while the panel is open.</summary>
        public Func<string> Read { get; internal set; }

        internal CheatItem(CheatKind kind, string section, string label)
        {
            Kind = kind; Section = section; Label = label ?? string.Empty;
        }
    }

    /// <summary>
    /// The list of cheats a game offers. Engine-free: a game fills it from its composition root, the panel
    /// (<c>ClassicSpins.Cheats.UI</c>) draws it and redraws on <see cref="Changed"/>. Sections keep the order they
    /// were first used in; items keep the order they were added. Main thread only.
    /// </summary>
    public sealed class CheatRegistry
    {
        private readonly List<CheatItem> _items = new List<CheatItem>();
        private readonly List<string> _sections = new List<string>();

        /// <summary>An item was added or removed.</summary>
        public event Action Changed;
        /// <summary>A cheat reported something (its own message, or the error it threw).</summary>
        public event Action<string> StatusChanged;

        public IReadOnlyList<CheatItem> Items => _items;
        public IReadOnlyList<string> Sections => _sections;
        public string LastStatus { get; private set; } = string.Empty;

        /// <summary>A builder for one section. Disposing it removes everything it added (a scope that dies takes its cheats along).</summary>
        public CheatGroup Group(string section) => new CheatGroup(this, string.IsNullOrEmpty(section) ? "General" : section);

        /// <summary>Show a one-line message under the panel header.</summary>
        public void Status(string message)
        {
            LastStatus = message ?? string.Empty;
            StatusChanged?.Invoke(LastStatus);
        }

        /// <summary>Run a cheat's action; an exception becomes the status line instead of breaking the panel. Returns the exception, or null.</summary>
        public Exception Run(CheatItem item, Action action)
        {
            try { action?.Invoke(); return null; }
            catch (Exception e)
            {
                Status($"{item?.Label}: {e.GetType().Name}: {e.Message}");
                return e;
            }
        }

        /// <summary>The items of one section, in order.</summary>
        public List<CheatItem> ItemsIn(string section)
        {
            var list = new List<CheatItem>();
            foreach (var item in _items) if (item.Section == section) list.Add(item);
            return list;
        }

        internal void Add(CheatItem item)
        {
            if (!_sections.Contains(item.Section)) _sections.Add(item.Section);
            _items.Add(item);
            Changed?.Invoke();
        }

        internal void Remove(IReadOnlyCollection<CheatItem> items)
        {
            if (items.Count == 0) return;
            foreach (var item in items) _items.Remove(item);
            _sections.RemoveAll(s => !_items.Exists(i => i.Section == s));
            Changed?.Invoke();
        }
    }

    /// <summary>Adds items to one section of a <see cref="CheatRegistry"/>; chainable. Dispose removes them again.</summary>
    public sealed class CheatGroup : IDisposable
    {
        private readonly CheatRegistry _registry;
        private readonly List<CheatItem> _added = new List<CheatItem>();
        private bool _disposed;

        public string Section { get; }

        internal CheatGroup(CheatRegistry registry, string section)
        {
            _registry = registry; Section = section;
        }

        public CheatGroup Button(string label, Action click)
        {
            var item = new CheatItem(CheatKind.Button, Section, label) { Click = click };
            return Add(item);
        }

        /// <summary>A whole-number field and a submit button.</summary>
        public CheatGroup Number(string label, int defaultValue, Action<int> submit, string submitLabel = "Set")
        {
            var item = new CheatItem(CheatKind.Number, Section, label) { DefaultNumber = defaultValue, Submit = submit, SubmitLabel = submitLabel };
            return Add(item);
        }

        /// <summary>A whole-number field whose starting value is read each time the panel opens.</summary>
        public CheatGroup Number(string label, Func<int> defaultValue, Action<int> submit, string submitLabel = "Set")
        {
            var item = new CheatItem(CheatKind.Number, Section, label) { ReadDefault = defaultValue, Submit = submit, SubmitLabel = submitLabel };
            return Add(item);
        }

        public CheatGroup Toggle(string label, Func<bool> get, Action<bool> set)
        {
            var item = new CheatItem(CheatKind.Toggle, Section, label) { Get = get, Set = set };
            return Add(item);
        }

        /// <summary>A live read-out (state, counters); polled while the panel is open.</summary>
        public CheatGroup Text(Func<string> read)
        {
            var item = new CheatItem(CheatKind.Text, Section, string.Empty) { Read = read };
            return Add(item);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _registry.Remove(_added);
            _added.Clear();
        }

        private CheatGroup Add(CheatItem item)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(CheatGroup));
            _added.Add(item);
            _registry.Add(item);
            return this;
        }
    }
}
