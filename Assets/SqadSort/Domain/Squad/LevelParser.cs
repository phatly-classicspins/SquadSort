using System;
using System.Collections.Generic;
using System.Globalization;

namespace Game.Domain
{
    /// <summary>
    /// Parses the line-based level text. Example:
    /// <code>
    /// name: First Steps
    /// difficulty: normal          // normal | hard | superhard
    /// plaza:
    ///   ####                      // '#' plaza cell, '.' nothing, a colour letter = cell with a buddy on it
    ///   ##R#
    /// tube  1 -1 up 4 : R R ?G Y   // x y direction capacity : contents closed end → mouth, '?' = hidden
    /// house B 0  2 down 4 : P P G  // a tube locked in a house of colour B
    /// spare 3 -1 up                // where Add Container puts its tube (optional)
    /// </code>
    /// Coordinates share one grid; Y grows downward; plaza row 0 is the first line under <c>plaza:</c>.
    /// Tube x/y is its mouth cell; direction points from the mouth toward the closed end.
    /// Colour letters: R O Y G D(dark green) C(cyan) B P K(pink) N(brown) A(gray) X(black) W(white). <c>//</c> starts a comment.
    /// The shipped levels use the JSON level pack instead (<see cref="LevelPackParser"/>); this format stays for tests.
    /// </summary>
    public static class LevelParser
    {
        /// <param name="requireFullSquads">When true (shipping levels), every colour must appear in multiples
        /// of <see cref="Board.SquadSize"/>. Rule tests pass false to build tiny boards.</param>
        public static LevelDefinition Parse(string text, bool requireFullSquads = true)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));

            string name = string.Empty;
            var difficulty = LevelDifficulty.Normal;
            var plazaCells = new List<GridPos>();
            var plazaBuddies = new List<PlazaBuddySpec>();
            var tubes = new List<TubeSpec>();
            GridPos? spareMouth = null;
            var spareDir = TubeDirection.Up;

            bool inPlaza = false;
            int plazaRow = 0;
            string[] lines = text.Replace("\r", string.Empty).Split('\n');

            for (int lineNo = 0; lineNo < lines.Length; lineNo++)
            {
                string line = StripComment(lines[lineNo]).Trim();
                if (line.Length == 0) continue;

                string lower = line.ToLowerInvariant();
                if (lower.StartsWith("name:", StringComparison.Ordinal))
                {
                    inPlaza = false;
                    name = line.Substring(5).Trim();
                }
                else if (lower.StartsWith("difficulty:", StringComparison.Ordinal))
                {
                    inPlaza = false;
                    difficulty = ParseDifficulty(line.Substring(11).Trim(), lineNo);
                }
                else if (lower.StartsWith("plaza:", StringComparison.Ordinal))
                {
                    inPlaza = true;
                    plazaRow = 0;
                }
                else if (lower.StartsWith("tube ", StringComparison.Ordinal) || lower.StartsWith("house ", StringComparison.Ordinal))
                {
                    inPlaza = false;
                    tubes.Add(ParseTube(line, lineNo));
                }
                else if (lower.StartsWith("spare ", StringComparison.Ordinal))
                {
                    inPlaza = false;
                    string[] t = Tokens(line);
                    if (t.Length != 4) throw Error(lineNo, "spare needs: spare x y direction");
                    spareMouth = new GridPos(ParseInt(t[1], lineNo), ParseInt(t[2], lineNo));
                    spareDir = ParseDirection(t[3], lineNo);
                }
                else if (inPlaza)
                {
                    for (int x = 0; x < line.Length; x++)
                    {
                        char c = line[x];
                        if (c == '.' || c == ' ') continue;
                        var pos = new GridPos(x, plazaRow);
                        plazaCells.Add(pos);
                        if (c == '#') continue;
                        if (!SquadColorCodes.TryParse(c, out SquadColor color))
                            throw Error(lineNo, $"unknown plaza character '{c}'");
                        plazaBuddies.Add(new PlazaBuddySpec(pos, new BuddySpec(color, false)));
                    }

                    plazaRow++;
                }
                else
                {
                    throw Error(lineNo, $"unrecognised line '{line}'");
                }
            }

            if (plazaCells.Count == 0) throw new FormatException("Level has no plaza cells.");
            if (tubes.Count == 0) throw new FormatException("Level has no tubes.");
            if (requireFullSquads) ValidateColourCounts(tubes, plazaBuddies);

            return new LevelDefinition(name, difficulty, plazaCells, plazaBuddies, tubes, spareMouth, spareDir);
        }

        private static TubeSpec ParseTube(string line, int lineNo)
        {
            int colon = line.IndexOf(':');
            if (colon < 0) throw Error(lineNo, "tube line needs ':' before its contents");

            string[] head = Tokens(line.Substring(0, colon));
            bool house = head[0].Equals("house", StringComparison.OrdinalIgnoreCase);
            int o = house ? 2 : 1;
            if (head.Length != o + 4) throw Error(lineNo, house ? "house needs: house C x y direction capacity : contents" : "tube needs: tube x y direction capacity : contents");

            SquadColor? houseColor = null;
            if (house)
            {
                if (head[1].Length != 1 || !SquadColorCodes.TryParse(head[1][0], out SquadColor hc))
                    throw Error(lineNo, $"bad house colour '{head[1]}'");
                houseColor = hc;
            }

            var mouth = new GridPos(ParseInt(head[o], lineNo), ParseInt(head[o + 1], lineNo));
            TubeDirection dir = ParseDirection(head[o + 2], lineNo);
            int capacity = ParseInt(head[o + 3], lineNo);
            if (capacity < 1) throw Error(lineNo, "capacity must be at least 1");

            var contents = new List<BuddySpec>();
            foreach (string token in Tokens(line.Substring(colon + 1)))
            {
                bool hidden = token.Length == 2 && token[0] == '?';
                char letter = hidden ? token[1] : token[0];
                if ((!hidden && token.Length != 1) || !SquadColorCodes.TryParse(letter, out SquadColor color))
                    throw Error(lineNo, $"bad buddy '{token}'");
                contents.Add(new BuddySpec(color, hidden));
            }

            if (contents.Count > capacity) throw Error(lineNo, $"{contents.Count} buddies do not fit capacity {capacity}");
            return new TubeSpec(mouth, dir, capacity, contents, houseColor);
        }

        /// <summary>Every colour must appear in multiples of 4 — otherwise the level cannot be won.</summary>
        internal static void ValidateColourCounts(List<TubeSpec> tubes, List<PlazaBuddySpec> plaza)
        {
            var counts = new Dictionary<SquadColor, int>();
            foreach (TubeSpec t in tubes)
            foreach (BuddySpec b in t.Contents)
                counts[b.Color] = counts.TryGetValue(b.Color, out int n) ? n + 1 : 1;
            foreach (PlazaBuddySpec p in plaza)
                counts[p.Buddy.Color] = counts.TryGetValue(p.Buddy.Color, out int n) ? n + 1 : 1;

            foreach (KeyValuePair<SquadColor, int> kv in counts)
            {
                if (kv.Value % Board.SquadSize != 0)
                    throw new FormatException($"Colour {kv.Key} appears {kv.Value} times; it must be a multiple of {Board.SquadSize}.");
            }
        }

        private static string StripComment(string line)
        {
            int i = line.IndexOf("//", StringComparison.Ordinal);
            return i < 0 ? line : line.Substring(0, i);
        }

        private static string[] Tokens(string s) => s.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

        private static int ParseInt(string s, int lineNo)
        {
            if (!int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v)) throw Error(lineNo, $"'{s}' is not a number");
            return v;
        }

        private static TubeDirection ParseDirection(string s, int lineNo)
        {
            switch (s.ToLowerInvariant())
            {
                case "up": return TubeDirection.Up;
                case "down": return TubeDirection.Down;
                case "left": return TubeDirection.Left;
                case "right": return TubeDirection.Right;
                default: throw Error(lineNo, $"bad direction '{s}'");
            }
        }

        private static LevelDifficulty ParseDifficulty(string s, int lineNo)
        {
            switch (s.ToLowerInvariant())
            {
                case "normal": return LevelDifficulty.Normal;
                case "hard": return LevelDifficulty.Hard;
                case "superhard": return LevelDifficulty.SuperHard;
                default: throw Error(lineNo, $"bad difficulty '{s}'");
            }
        }

        private static FormatException Error(int lineNo, string message) => new FormatException($"Level line {lineNo + 1}: {message}");
    }
}
