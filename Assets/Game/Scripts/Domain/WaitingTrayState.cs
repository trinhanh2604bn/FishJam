using System;

namespace FishPuzzle.Domain
{
    /// <summary>
    /// Ordered waiting-tray slots. Insertion is left to right. This type does not deduct lives.
    /// </summary>
    public sealed class WaitingTrayState
    {
        private readonly FishRuntimeState[] _slots;

        public WaitingTrayState(int slotCount, int failCount)
        {
            if (slotCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(slotCount));
            }

            if (failCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(failCount));
            }

            SlotCount = slotCount;
            FailCount = failCount;
            _slots = new FishRuntimeState[slotCount];
        }

        public int SlotCount { get; }

        public int FailCount { get; }

        public int Count { get; private set; }

        public bool ReachedFailCount => Count >= FailCount;

        public bool TryInsert(FishRuntimeState fish, out int slotIndex)
        {
            slotIndex = -1;
            if (fish == null || Count >= _slots.Length)
            {
                return false;
            }

            slotIndex = Count;
            _slots[slotIndex] = fish;
            Count++;
            fish.CommitToWaitingTray();
            return true;
        }

        public FishRuntimeState GetFishAt(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= Count)
            {
                return null;
            }

            return _slots[slotIndex];
        }

        public FishRuntimeState RemoveAt(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= Count)
            {
                return null;
            }

            var removed = _slots[slotIndex];
            for (var i = slotIndex; i < Count - 1; i++)
            {
                _slots[i] = _slots[i + 1];
            }

            Count--;
            _slots[Count] = null;
            return removed;
        }
    }
}
