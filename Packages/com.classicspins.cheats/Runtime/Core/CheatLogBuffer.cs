using System;
using System.Collections.Generic;
using System.Text;

namespace ClassicSpins.Cheats
{
    public enum CheatLogLevel { Info, Warning, Error }

    public readonly struct CheatLogEntry
    {
        public readonly CheatLogLevel Level;
        public readonly string Message, StackTrace;
        public readonly DateTime Time;

        public CheatLogEntry(CheatLogLevel level, string message, string stackTrace, DateTime time)
        {
            Level = level; Message = message ?? string.Empty; StackTrace = stackTrace ?? string.Empty; Time = time;
        }
    }

    /// <summary>
    /// The last N log lines, for the panel's Logs tab. Engine-free and thread-safe: the engine's log callback may
    /// arrive on any thread, the panel reads on the main thread. <see cref="Version"/> moves on every change so the
    /// panel redraws only when something happened.
    /// </summary>
    public sealed class CheatLogBuffer
    {
        private readonly object _gate = new object();
        private readonly CheatLogEntry[] _ring;
        private int _start, _count;

        public CheatLogBuffer(int capacity = 300)
        {
            _ring = new CheatLogEntry[Math.Max(1, capacity)];
        }

        public int Capacity => _ring.Length;
        public int Version { get; private set; }
        /// <summary>Errors added since <see cref="MarkErrorsSeen"/>.</summary>
        public int UnseenErrors { get; private set; }

        public int Count { get { lock (_gate) return _count; } }

        public void Add(CheatLogEntry entry)
        {
            lock (_gate)
            {
                int at = (_start + _count) % _ring.Length;
                _ring[at] = entry;
                if (_count < _ring.Length) _count++;
                else _start = (_start + 1) % _ring.Length;   // full: the oldest goes
                if (entry.Level == CheatLogLevel.Error) UnseenErrors++;
                Version++;
            }
        }

        public void Clear()
        {
            lock (_gate) { _start = 0; _count = 0; UnseenErrors = 0; Version++; }
        }

        public void MarkErrorsSeen()
        {
            lock (_gate) { if (UnseenErrors == 0) return; UnseenErrors = 0; Version++; }
        }

        /// <summary>Oldest first.</summary>
        public List<CheatLogEntry> Snapshot()
        {
            lock (_gate)
            {
                var list = new List<CheatLogEntry>(_count);
                for (int i = 0; i < _count; i++) list.Add(_ring[(_start + i) % _ring.Length]);
                return list;
            }
        }

        /// <summary>Every kept line as plain text, newest last, with stack traces for warnings and errors — what "Copy" puts on the clipboard.</summary>
        public string ToPlainText()
        {
            var sb = new StringBuilder();
            foreach (var e in Snapshot())
            {
                sb.Append(e.Time.ToString("HH:mm:ss.fff")).Append(' ').Append(Tag(e.Level)).Append(' ').AppendLine(e.Message);
                if (e.Level != CheatLogLevel.Info && e.StackTrace.Length > 0) sb.AppendLine(e.StackTrace.TrimEnd());
            }
            return sb.ToString();
        }

        public static string Tag(CheatLogLevel level) => level == CheatLogLevel.Error ? "[E]" : level == CheatLogLevel.Warning ? "[W]" : "[I]";
    }
}
