using System;
using System.Collections.Generic;
using FishPuzzle.Domain;

namespace FishPuzzle.Progression
{
    /// <summary>
    /// Current position in the ordered level list for one play session.
    /// It never points past the last level. Advancing from the last level is refused.
    /// No save data is written in this milestone.
    /// </summary>
    public sealed class LevelSequence
    {
        private readonly List<LevelData> _levels;

        public LevelSequence(IReadOnlyList<LevelData> levels, int startIndex = 0)
        {
            if (levels == null)
            {
                throw new ArgumentNullException(nameof(levels));
            }

            _levels = new List<LevelData>(levels.Count);
            for (var i = 0; i < levels.Count; i++)
            {
                if (levels[i] == null)
                {
                    throw new ArgumentException("Level list entry " + i + " is null.", nameof(levels));
                }

                _levels.Add(levels[i]);
            }

            if (_levels.Count == 0)
            {
                throw new ArgumentException("Level list is empty.", nameof(levels));
            }

            if (startIndex < 0 || startIndex >= _levels.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(startIndex));
            }

            CurrentIndex = startIndex;
        }

        public int Count => _levels.Count;

        public int CurrentIndex { get; private set; }

        public LevelData Current => _levels[CurrentIndex];

        /// <summary>One-based number shown to the player.</summary>
        public int CurrentNumber => CurrentIndex + 1;

        public bool HasNext => CurrentIndex + 1 < _levels.Count;

        public bool IsLast => !HasNext;

        public LevelData PeekNext()
        {
            return HasNext ? _levels[CurrentIndex + 1] : null;
        }

        public bool TryAdvance()
        {
            if (!HasNext)
            {
                return false;
            }

            CurrentIndex++;
            return true;
        }

        public void Restart()
        {
            CurrentIndex = 0;
        }

        public LevelData Get(int index)
        {
            return index >= 0 && index < _levels.Count ? _levels[index] : null;
        }
    }
}
