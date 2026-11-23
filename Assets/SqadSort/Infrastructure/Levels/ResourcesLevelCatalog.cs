using System;
using ClassicSpins.PrototypeFramework.Application;
using Game.Application;
using Game.Domain;
using UnityEngine;

namespace Game.Infrastructure
{
    /// <summary>
    /// Reads the shipped levels from the JSON level pack <c>Assets/SqadSort/Resources/Levels/levels_all.json</c>
    /// (format: <see cref="LevelPackParser"/>), in pack order. Parsed once, on first use.
    /// </summary>
    public sealed class ResourcesLevelCatalog : ILevelCatalog
    {
        private const string PackPath = "Levels/levels_all";
        private readonly ILog _log;
        private LevelPack _pack;

        public ResourcesLevelCatalog(ILog log = null) => _log = log ?? new NullLog();

        public int Count => Pack.Levels.Count;

        public LevelDefinition GetLevel(int number)
        {
            if (number < 1 || number > Pack.Levels.Count)
                throw new ArgumentOutOfRangeException(nameof(number), number, $"Levels ship 1..{Pack.Levels.Count}.");
            return Pack.Levels[number - 1];
        }

        private LevelPack Pack
        {
            get
            {
                if (_pack != null) return _pack;
                var asset = Resources.Load<TextAsset>(PackPath);
                if (asset == null) throw new InvalidOperationException($"No level pack at Resources/{PackPath}.json.");
                _pack = LevelPackParser.Parse(asset.text);
                if (_pack.IgnoredBarriers > 0)
                    _log.Warn($"[Levels] {_pack.IgnoredBarriers} barrier(s) in the level pack have no rule yet; those tubes play as plain tubes.");
                return _pack;
            }
        }
    }
}
