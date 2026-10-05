using System;
using System.Collections.Generic;
using FishPuzzle.Domain;

namespace FishPuzzle.Tanks
{
    /// <summary>
    /// Ordered target groups for one level attempt. Each take hands the next group to one tank.
    /// </summary>
    public sealed class TargetGroupQueue
    {
        private readonly List<FishType> _groups;

        public TargetGroupQueue(IReadOnlyList<FishType> groups)
        {
            if (groups == null)
            {
                throw new ArgumentNullException(nameof(groups));
            }

            _groups = new List<FishType>(groups.Count);
            for (var i = 0; i < groups.Count; i++)
            {
                _groups.Add(groups[i]);
            }
        }

        public int Count => _groups.Count;

        public int NextUnassignedIndex { get; private set; }

        public bool TryTakeNext(out FishType fishType)
        {
            if (NextUnassignedIndex >= _groups.Count)
            {
                fishType = default;
                return false;
            }

            fishType = _groups[NextUnassignedIndex];
            NextUnassignedIndex++;
            return true;
        }
    }
}
