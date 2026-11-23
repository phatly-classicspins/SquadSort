using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Game.Domain
{
    /// <summary>
    /// Reads the JSON level pack (<c>Resources/Levels/levels_all.json</c>) into <see cref="LevelDefinition"/>s.
    /// <code>
    /// { "colors": [ { "index": 0, "name": "Red …", "hex": "#e53836" }, … ],
    ///   "levels": [ { "name": "Level001", "levelId": "c0001", "difficulty": 0,
    ///                 "waitingCells": [ [x, y], … ],
    ///                 "containers": [ { "side": 0, "x": -1, "y": -1, "pets": [3, 3, 3], "hidden": [0, 0, 0],
    ///                                   "features": [ { "type": "CurtainFeatureData", "UnlockColorIndex": 8 } ] }, … ] }, … ] }
    /// </code>
    /// <list type="bullet">
    /// <item><c>waitingCells</c> are the plaza. The grid is the level text's (Y grows downward, so a container at
    /// <c>y - 1</c> of the plaza sits above it), shifted so the plaza starts at (0, 0).</item>
    /// <item>A container is a tube of <see cref="Board.SquadSize"/> whose mouth is its cell: <c>side</c> 0 opens down onto
    /// the plaza (the tube runs <see cref="TubeDirection.Up"/>), <c>side</c> 1 opens up onto it (runs
    /// <see cref="TubeDirection.Down"/>); any other side is read from the plaza cell beside it.</item>
    /// <item><c>pets</c> run closed end → mouth (the last one is the front), each a colour index; <c>hidden</c> non-zero ⇒ "?".</item>
    /// <item>A <c>CurtainFeatureData</c> locks the tube in a house of <c>UnlockColorIndex</c>'s colour (GDD §2.2b-12).</item>
    /// <item><c>BarrierFeatureData</c>, <c>keyColors</c>, <c>offset</c>, <c>holes</c> and <c>levelDuration</c> have no rule
    /// in this game yet and are ignored — <see cref="LevelPack.IgnoredBarriers"/> counts the barriers.</item>
    /// <item>The pack names no Add Container position, so the spare tube goes to the free slot nearest the plaza's middle.</item>
    /// </list>
    /// </summary>
    public static class LevelPackParser
    {
        /// <summary>The pack's colour index → this game's colour (Red, Blue, Green, Purple, Orange, Pink, Gray, Brown,
        /// Yellow, Cyan, Black, White, DarkGreen).</summary>
        private static readonly SquadColor[] PackColors =
        {
            SquadColor.Red, SquadColor.Blue, SquadColor.Green, SquadColor.Purple, SquadColor.Orange, SquadColor.Pink,
            SquadColor.Gray, SquadColor.Brown, SquadColor.Yellow, SquadColor.Cyan, SquadColor.Black, SquadColor.White,
            SquadColor.DarkGreen,
        };

        public static LevelPack Parse(string json)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));
            var root = Obj(new JsonReader(json).ReadDocument(), "the pack");
            var levels = new List<LevelDefinition>();
            int barriers = 0;
            int n = 0;
            foreach (object level in Arr(Field(root, "levels"), "levels"))
            {
                n++;
                try { levels.Add(ParseLevel(Obj(level, "a level"), ref barriers)); }
                catch (FormatException e) { throw new FormatException($"Level pack entry {n}: {e.Message}", e); }
            }

            if (levels.Count == 0) throw new FormatException("The level pack has no levels.");
            return new LevelPack(levels, barriers);
        }

        /// <summary>The pack's colour index as a game colour.</summary>
        public static SquadColor ColorOf(int packIndex)
        {
            if (packIndex < 0 || packIndex >= PackColors.Length) throw new FormatException($"unknown colour index {packIndex}");
            return PackColors[packIndex];
        }

        private static LevelDefinition ParseLevel(Dictionary<string, object> level, ref int barriers)
        {
            string name = level.TryGetValue("name", out object nm) && nm is string s ? s : string.Empty;
            var difficulty = (LevelDifficulty)Math.Max(0, Math.Min(2, OptInt(level, "difficulty", 0)));

            var raw = new List<GridPos>();
            foreach (object cell in Arr(Field(level, "waitingCells"), "waitingCells")) raw.Add(Pos(cell));
            if (raw.Count == 0) throw new FormatException($"{name}: no waiting cells");
            int minX = int.MaxValue, minY = int.MaxValue;
            foreach (GridPos p in raw) { minX = Math.Min(minX, p.X); minY = Math.Min(minY, p.Y); }
            var plaza = new List<GridPos>();
            var plazaSet = new HashSet<GridPos>();
            foreach (GridPos p in raw)
            {
                var q = new GridPos(p.X - minX, p.Y - minY);
                if (plazaSet.Add(q)) plaza.Add(q);
            }

            var tubes = new List<TubeSpec>();
            foreach (object c in Arr(Field(level, "containers"), "containers"))
            {
                var box = Obj(c, "a container");
                var mouth = new GridPos(Int(Field(box, "x")) - minX, Int(Field(box, "y")) - minY);
                TubeDirection dir = Direction(OptInt(box, "side", -1), mouth, plazaSet, name);

                var pets = Arr(Field(box, "pets"), "pets");
                var hidden = box.TryGetValue("hidden", out object h) && h is List<object> hl ? hl : new List<object>();
                if (pets.Count > Board.SquadSize) throw new FormatException($"{name}: a container at {mouth} holds {pets.Count} pets");
                var contents = new List<BuddySpec>(pets.Count);
                for (int i = 0; i < pets.Count; i++)
                    contents.Add(new BuddySpec(ColorOf(Int(pets[i])), i < hidden.Count && Int(hidden[i]) != 0));

                SquadColor? house = null;
                if (box.TryGetValue("features", out object f) && f is List<object> features)
                    foreach (object feature in features)
                    {
                        var fd = Obj(feature, "a feature");
                        string type = fd.TryGetValue("type", out object t) && t is string ts ? ts : string.Empty;
                        if (type == "CurtainFeatureData") house = ColorOf(Int(Field(fd, "UnlockColorIndex")));
                        else if (type == "BarrierFeatureData") barriers++;
                    }

                tubes.Add(new TubeSpec(mouth, dir, Board.SquadSize, contents, house));
            }

            if (tubes.Count == 0) throw new FormatException($"{name}: no containers");
            LevelParser.ValidateColourCounts(tubes, new List<PlazaBuddySpec>());

            GridPos? spare = SpareSlot(plaza, tubes, out TubeDirection spareDir);
            return new LevelDefinition(name, difficulty, plaza, Array.Empty<PlazaBuddySpec>(), tubes, spare, spareDir);
        }

        private static TubeDirection Direction(int side, GridPos mouth, HashSet<GridPos> plaza, string name)
        {
            if (side == 0) return TubeDirection.Up;
            if (side == 1) return TubeDirection.Down;
            if (plaza.Contains(mouth.Step(TubeDirection.Down, 1))) return TubeDirection.Up;
            if (plaza.Contains(mouth.Step(TubeDirection.Up, 1))) return TubeDirection.Down;
            if (plaza.Contains(mouth.Step(TubeDirection.Right, 1))) return TubeDirection.Left;
            if (plaza.Contains(mouth.Step(TubeDirection.Left, 1))) return TubeDirection.Right;
            throw new FormatException($"{name}: the container at {mouth} touches no waiting cell");
        }

        /// <summary>The Add Container slot: a mouth beside the plaza whose whole tube is free, vertical before sideways,
        /// then nearest the plaza's middle column; null when no slot is free.</summary>
        private static GridPos? SpareSlot(List<GridPos> plaza, List<TubeSpec> tubes, out TubeDirection direction)
        {
            var used = new HashSet<GridPos>(plaza);
            foreach (TubeSpec t in tubes)
                for (int s = 0; s < t.Capacity; s++) used.Add(t.Mouth.Step(t.Direction, s));

            double midX = 0;
            foreach (GridPos p in plaza) midX += p.X;
            midX /= plaza.Count;

            GridPos? best = null;
            direction = TubeDirection.Up;
            double bestScore = double.MaxValue;
            var directions = new[] { TubeDirection.Up, TubeDirection.Down, TubeDirection.Left, TubeDirection.Right };
            foreach (GridPos p in plaza)
            foreach (TubeDirection d in directions)
            {
                GridPos mouth = p.Step(d, 1);
                bool free = true;
                for (int s = 0; s < Board.SquadSize && free; s++) free = !used.Contains(mouth.Step(d, s));
                if (!free) continue;
                bool sideways = d == TubeDirection.Left || d == TubeDirection.Right;
                double score = (sideways ? 1000 : 0) + Math.Abs(mouth.X - midX) * 10 + (d == TubeDirection.Up ? 0 : 1);
                if (score < bestScore) { bestScore = score; best = mouth; direction = d; }
            }

            return best;
        }

        // ------------------------------------------------------------------ JSON helpers

        private static object Field(Dictionary<string, object> o, string key)
            => o.TryGetValue(key, out object v) ? v : throw new FormatException($"missing \"{key}\"");

        private static Dictionary<string, object> Obj(object v, string what)
            => v as Dictionary<string, object> ?? throw new FormatException($"{what} is not an object");

        private static List<object> Arr(object v, string what)
            => v as List<object> ?? throw new FormatException($"\"{what}\" is not an array");

        private static int Int(object v)
            => v is double d && d == Math.Floor(d) ? (int)d : throw new FormatException($"'{v}' is not a whole number");

        private static int OptInt(Dictionary<string, object> o, string key, int fallback)
            => o.TryGetValue(key, out object v) && v is double ? Int(v) : fallback;

        private static GridPos Pos(object v)
        {
            var a = Arr(v, "a cell");
            if (a.Count != 2) throw new FormatException("a cell is not [x, y]");
            return new GridPos(Int(a[0]), Int(a[1]));
        }

        /// <summary>A minimal JSON reader (objects → Dictionary, arrays → List, numbers → double). Engine-free on purpose:
        /// Game.Domain references no JSON library.</summary>
        private sealed class JsonReader
        {
            private readonly string _s;
            private int _i;

            public JsonReader(string s) { _s = s; }

            public object ReadDocument()
            {
                if (_s.Length > 0 && _s[0] == '﻿') _i = 1;
                object v = Value();
                Skip();
                if (_i != _s.Length) throw Error("trailing text");
                return v;
            }

            private object Value()
            {
                Skip();
                if (_i >= _s.Length) throw Error("unexpected end");
                char c = _s[_i];
                switch (c)
                {
                    case '{': return Object();
                    case '[': return Array();
                    case '"': return String();
                    case 't': Word("true"); return true;
                    case 'f': Word("false"); return false;
                    case 'n': Word("null"); return null;
                    default:
                        if (c == '-' || (c >= '0' && c <= '9')) return Number();
                        throw Error($"unexpected '{c}'");
                }
            }

            private Dictionary<string, object> Object()
            {
                var o = new Dictionary<string, object>(StringComparer.Ordinal);
                _i++;
                Skip();
                if (Peek('}')) { _i++; return o; }
                while (true)
                {
                    Skip();
                    if (!Peek('"')) throw Error("expected a key");
                    string key = String();
                    Skip();
                    Expect(':');
                    o[key] = Value();
                    Skip();
                    if (Peek(',')) { _i++; continue; }
                    Expect('}');
                    return o;
                }
            }

            private List<object> Array()
            {
                var a = new List<object>();
                _i++;
                Skip();
                if (Peek(']')) { _i++; return a; }
                while (true)
                {
                    a.Add(Value());
                    Skip();
                    if (Peek(',')) { _i++; continue; }
                    Expect(']');
                    return a;
                }
            }

            private string String()
            {
                _i++;
                var sb = new StringBuilder();
                while (true)
                {
                    if (_i >= _s.Length) throw Error("unterminated string");
                    char c = _s[_i++];
                    if (c == '"') return sb.ToString();
                    if (c != '\\') { sb.Append(c); continue; }
                    if (_i >= _s.Length) throw Error("unterminated escape");
                    char e = _s[_i++];
                    switch (e)
                    {
                        case 'n': sb.Append('\n'); break;
                        case 't': sb.Append('\t'); break;
                        case 'r': sb.Append('\r'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'u':
                            if (_i + 4 > _s.Length) throw Error("bad \\u escape");
                            sb.Append((char)int.Parse(_s.Substring(_i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            _i += 4;
                            break;
                        default: sb.Append(e); break;
                    }
                }
            }

            private double Number()
            {
                int start = _i;
                while (_i < _s.Length && "+-0123456789.eE".IndexOf(_s[_i]) >= 0) _i++;
                string t = _s.Substring(start, _i - start);
                if (!double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out double d)) throw Error($"bad number '{t}'");
                return d;
            }

            private void Word(string w)
            {
                if (string.CompareOrdinal(_s, _i, w, 0, w.Length) != 0) throw Error($"expected {w}");
                _i += w.Length;
            }

            private void Skip()
            {
                while (_i < _s.Length && char.IsWhiteSpace(_s[_i])) _i++;
            }

            private bool Peek(char c) => _i < _s.Length && _s[_i] == c;

            private void Expect(char c)
            {
                if (!Peek(c)) throw Error($"expected '{c}'");
                _i++;
            }

            private FormatException Error(string message) => new FormatException($"JSON at {_i}: {message}");
        }
    }

    /// <summary>The parsed level pack, in pack order (level 1 first).</summary>
    public sealed class LevelPack
    {
        public LevelPack(IReadOnlyList<LevelDefinition> levels, int ignoredBarriers)
        {
            Levels = levels;
            IgnoredBarriers = ignoredBarriers;
        }

        public IReadOnlyList<LevelDefinition> Levels { get; }

        /// <summary>How many <c>BarrierFeatureData</c> the pack carries that this game has no rule for yet (played as plain tubes).</summary>
        public int IgnoredBarriers { get; }
    }
}
