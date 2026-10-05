using System;
using System.Collections.Generic;
using FishPuzzle.Core;
using FishPuzzle.Domain;
using UnityEngine;

namespace FishPuzzle.Presentation
{
    /// <summary>
    /// Serialized FishType to sprite map. Filename lookups are not the source of truth.
    /// </summary>
    [CreateAssetMenu(fileName = "FishVisualCatalog", menuName = "Fish Puzzle/Fish Visual Catalog")]
    public sealed class FishVisualCatalog : ScriptableObject
    {
        [SerializeField] private List<FishSpriteMapping> _entries = new List<FishSpriteMapping>();

        [NonSerialized] private Dictionary<FishType, Sprite> _sprites;
        [NonSerialized] private HashSet<FishType> _duplicateTypes;
        [NonSerialized] private bool _cacheReady;

        public IReadOnlyList<FishSpriteMapping> Entries => _entries;

        public bool HasDuplicateTypes
        {
            get
            {
                EnsureCache();
                return _duplicateTypes.Count > 0;
            }
        }

        public bool TryGetSprite(FishType fishType, out Sprite sprite)
        {
            EnsureCache();
            sprite = null;
            if (_duplicateTypes.Contains(fishType))
            {
                return false;
            }

            return _sprites.TryGetValue(fishType, out sprite) && sprite != null;
        }

        public List<FishType> GetMissingRequiredTypes()
        {
            var missing = new List<FishType>();
            var values = (FishType[])Enum.GetValues(typeof(FishType));
            for (var i = 0; i < values.Length; i++)
            {
                if (!TryGetSprite(values[i], out _))
                {
                    missing.Add(values[i]);
                }
            }

            return missing;
        }

        public List<FishType> GetDuplicateTypes()
        {
            EnsureCache();
            return new List<FishType>(_duplicateTypes);
        }

        private void OnEnable()
        {
            _cacheReady = false;
        }

        private void OnValidate()
        {
            _cacheReady = false;
            EnsureCache();
            if (_duplicateTypes.Count == 0)
            {
                return;
            }

            GameLog.Error(
                nameof(FishVisualCatalog),
                "Duplicate FishType mappings: " + string.Join(", ", _duplicateTypes) + ". Each FishType must appear once.");
        }

        private void EnsureCache()
        {
            if (_cacheReady && _sprites != null && _duplicateTypes != null)
            {
                return;
            }

            _sprites = new Dictionary<FishType, Sprite>();
            _duplicateTypes = new HashSet<FishType>();
            var seen = new HashSet<FishType>();
            for (var i = 0; i < _entries.Count; i++)
            {
                var fishType = _entries[i].FishType;
                if (!seen.Add(fishType))
                {
                    _duplicateTypes.Add(fishType);
                    _sprites.Remove(fishType);
                    continue;
                }

                _sprites.Add(fishType, _entries[i].Sprite);
            }

            _cacheReady = true;
        }
    }

    [Serializable]
    public struct FishSpriteMapping
    {
        [SerializeField] private FishType _fishType;
        [SerializeField] private Sprite _sprite;

        public FishType FishType => _fishType;

        public Sprite Sprite => _sprite;
    }
}
