using System.Collections.Generic;
using UnityEngine;

namespace FishPuzzle.Domain
{
    /// <summary>
    /// Ordered list of playable levels. One Gameplay scene plays every entry in this order.
    /// The catalog stores data only. <see cref="FishPuzzle.Progression.LevelSequence"/> owns the current position.
    /// </summary>
    [CreateAssetMenu(fileName = "LevelCatalog", menuName = "Fish Puzzle/Level Catalog")]
    public sealed class LevelCatalog : ScriptableObject
    {
        [Tooltip("Levels in play order. Index 0 is the first level of a fresh run.")]
        [SerializeField] private List<LevelData> _levels = new List<LevelData>();

        public IReadOnlyList<LevelData> Levels => _levels;

        public int Count => _levels != null ? _levels.Count : 0;

        public LevelData Get(int index)
        {
            if (_levels == null || index < 0 || index >= _levels.Count)
            {
                return null;
            }

            return _levels[index];
        }

        public int IndexOf(LevelData level)
        {
            if (_levels == null || level == null)
            {
                return -1;
            }

            return _levels.IndexOf(level);
        }
    }
}
