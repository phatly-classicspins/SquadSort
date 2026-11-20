using System;
using System.Linq;
using Game.Application;
using UnityEngine;

namespace Game.Infrastructure
{
    /// <summary>
    /// Reads the shipped levels from <c>Assets/SqadSort/Resources/Levels/level_NNN.txt</c>, ordered by
    /// file name. Loaded once, on first use.
    /// </summary>
    public sealed class ResourcesLevelCatalog : ILevelCatalog
    {
        private const string Folder = "Levels";
        private TextAsset[] _levels;

        public int Count => Levels.Length;

        public string GetLevelText(int number)
        {
            if (number < 1 || number > Levels.Length)
                throw new ArgumentOutOfRangeException(nameof(number), number, $"Levels ship 1..{Levels.Length}.");
            return Levels[number - 1].text;
        }

        private TextAsset[] Levels
        {
            get
            {
                if (_levels != null) return _levels;
                _levels = Resources.LoadAll<TextAsset>(Folder)
                    .OrderBy(a => a.name, StringComparer.Ordinal)
                    .ToArray();
                if (_levels.Length == 0)
                    throw new InvalidOperationException($"No level text under Resources/{Folder}.");
                return _levels;
            }
        }
    }
}
