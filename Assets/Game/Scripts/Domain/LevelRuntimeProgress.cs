using System;

namespace FishPuzzle.Domain
{
    /// <summary>
    /// Collected fish versus the level's required total. The total is level data, not a fixed number.
    /// </summary>
    public sealed class LevelRuntimeProgress
    {
        public LevelRuntimeProgress(int totalFishRequired)
        {
            if (totalFishRequired < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(totalFishRequired));
            }

            TotalFishRequired = totalFishRequired;
        }

        public int CollectedFishCount { get; private set; }

        public int TotalFishRequired { get; }

        public void CommitCompletedGroup(int fishCount)
        {
            if (fishCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(fishCount));
            }

            CollectedFishCount += fishCount;
        }
    }
}
