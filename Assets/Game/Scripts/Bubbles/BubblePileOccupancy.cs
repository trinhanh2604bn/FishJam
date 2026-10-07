using System.Collections.Generic;
using FishPuzzle.Core;
using FishPuzzle.Domain;

namespace FishPuzzle.Bubbles
{
    /// <summary>
    /// Logical slot occupancy for one pile. Queue index i starts in the slot whose id is i.
    /// Later queue entries stay pending until a top slot is free.
    /// </summary>
    public sealed class BubblePileOccupancy
    {
        private readonly List<BubblePileSlotDefinition> _slots;
        private readonly Dictionary<int, BubblePileSlotDefinition> _slotsById;
        private readonly List<BubbleRuntimeState> _queue;
        private readonly Dictionary<int, BubbleRuntimeState> _bySlot;
        private readonly Dictionary<string, int> _slotByBubbleId;

        private BubblePileOccupancy(
            List<BubblePileSlotDefinition> slots,
            Dictionary<int, BubblePileSlotDefinition> slotsById,
            List<BubbleRuntimeState> queue,
            Dictionary<int, BubbleRuntimeState> bySlot,
            Dictionary<string, int> slotByBubbleId,
            int nextIndex)
        {
            _slots = slots;
            _slotsById = slotsById;
            _queue = queue;
            _bySlot = bySlot;
            _slotByBubbleId = slotByBubbleId;
            NextIndex = nextIndex;
        }

        public int NextIndex { get; private set; }

        public int PendingCount => _queue.Count - NextIndex;

        public int SlotCount => _slots.Count;

        public IReadOnlyList<BubblePileSlotDefinition> Slots => _slots;

        public static BubblePileOccupancy Create(BubblePileLayout layout, IReadOnlyList<BubbleRuntimeState> queueInOrder)
        {
            var slots = new List<BubblePileSlotDefinition>();
            var slotsById = new Dictionary<int, BubblePileSlotDefinition>();
            if (layout != null && layout.Slots != null)
            {
                for (var i = 0; i < layout.Slots.Count; i++)
                {
                    var slot = layout.Slots[i];
                    if (slot == null || slotsById.ContainsKey(slot.SlotId))
                    {
                        continue;
                    }

                    slots.Add(slot);
                    slotsById.Add(slot.SlotId, slot);
                }
            }

            var queue = new List<BubbleRuntimeState>();
            if (queueInOrder != null)
            {
                for (var i = 0; i < queueInOrder.Count; i++)
                {
                    queue.Add(queueInOrder[i]);
                }
            }

            var bySlot = new Dictionary<int, BubbleRuntimeState>();
            var slotByBubble = new Dictionary<string, int>();
            var next = 0;
            for (var index = 0; index < queue.Count; index++)
            {
                if (!slotsById.TryGetValue(index, out var slot))
                {
                    break;
                }

                next = index + 1;
                var bubble = queue[index];
                if (bubble == null || slotByBubble.ContainsKey(bubble.BubbleId))
                {
                    continue;
                }

                bySlot.Add(slot.SlotId, bubble);
                slotByBubble.Add(bubble.BubbleId, slot.SlotId);
                bubble.SetInPlay(true);
            }

            return new BubblePileOccupancy(slots, slotsById, queue, bySlot, slotByBubble, next);
        }

        public bool TryGetSlotDefinition(int slotId, out BubblePileSlotDefinition slot)
        {
            return _slotsById.TryGetValue(slotId, out slot) && slot != null;
        }

        public bool IsOccupied(int slotId)
        {
            return _bySlot.ContainsKey(slotId);
        }

        public BubbleRuntimeState GetAtSlot(int slotId)
        {
            _bySlot.TryGetValue(slotId, out var bubble);
            return bubble;
        }

        public bool TryGetSlotOfBubble(string bubbleId, out int slotId)
        {
            slotId = -1;
            if (string.IsNullOrEmpty(bubbleId))
            {
                return false;
            }

            return _slotByBubbleId.TryGetValue(bubbleId, out slotId);
        }

        /// <summary>Bubbles currently occupying slots adjacent to <paramref name="slotId"/>, in layout order.</summary>
        public void CollectAdjacentOccupants(int slotId, List<BubbleRuntimeState> destination)
        {
            if (destination == null)
            {
                return;
            }

            destination.Clear();
            if (!_slotsById.TryGetValue(slotId, out var origin) || origin == null)
            {
                return;
            }

            for (var i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (slot == null
                    || !_bySlot.TryGetValue(slot.SlotId, out var bubble)
                    || bubble == null
                    || !BubblePileAdjacency.AreAdjacent(origin, slot))
                {
                    continue;
                }

                destination.Add(bubble);
            }
        }

        public void Vacate(string bubbleId)
        {
            if (string.IsNullOrEmpty(bubbleId) || !_slotByBubbleId.TryGetValue(bubbleId, out var slotId))
            {
                return;
            }

            _slotByBubbleId.Remove(bubbleId);
            _bySlot.Remove(slotId);
        }

        public void Move(string bubbleId, int toSlotId)
        {
            if (string.IsNullOrEmpty(bubbleId) || !_slotByBubbleId.TryGetValue(bubbleId, out var fromSlotId))
            {
                GameLog.Error(nameof(BubblePileOccupancy), "Bubble " + bubbleId + " is not in a pile slot.");
                return;
            }

            if (fromSlotId == toSlotId)
            {
                return;
            }

            if (!_bySlot.TryGetValue(fromSlotId, out var bubble) || bubble == null)
            {
                GameLog.Error(nameof(BubblePileOccupancy), "Pile slot " + fromSlotId + " lost its bubble.");
                return;
            }

            if (_bySlot.ContainsKey(toSlotId))
            {
                GameLog.Error(nameof(BubblePileOccupancy), "Pile slot " + toSlotId + " is already occupied.");
                return;
            }

            _bySlot.Remove(fromSlotId);
            _bySlot.Add(toSlotId, bubble);
            _slotByBubbleId[bubbleId] = toSlotId;
        }

        public bool TrySpawnNextTop(out BubbleSpawn spawn)
        {
            spawn = default;
            var destination = FindLowestEmptyTopSlot();
            if (destination < 0 || NextIndex >= _queue.Count)
            {
                return false;
            }

            var bubble = _queue[NextIndex];
            if (bubble == null)
            {
                GameLog.Error(nameof(BubblePileOccupancy), "Pending bubble " + NextIndex + " is missing.");
                return false;
            }

            if (_slotByBubbleId.ContainsKey(bubble.BubbleId))
            {
                GameLog.Error(nameof(BubblePileOccupancy), "Bubble " + bubble.BubbleId + " is already in the pile.");
                return false;
            }

            NextIndex++;
            _bySlot.Add(destination, bubble);
            _slotByBubbleId.Add(bubble.BubbleId, destination);
            bubble.SetInPlay(true);
            spawn = new BubbleSpawn(bubble.BubbleId, destination);
            return true;
        }

        public void CopyOccupiedSlots(List<BubblePileSlotDefinition> destination)
        {
            if (destination == null)
            {
                return;
            }

            destination.Clear();
            for (var i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (slot != null && _bySlot.ContainsKey(slot.SlotId))
                {
                    destination.Add(slot);
                }
            }
        }

        private int FindLowestEmptyTopSlot()
        {
            var found = false;
            var best = int.MaxValue;
            for (var i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (slot == null || !slot.CanReceiveSpawnFromTop || _bySlot.ContainsKey(slot.SlotId))
                {
                    continue;
                }

                if (!found || slot.SlotId < best)
                {
                    found = true;
                    best = slot.SlotId;
                }
            }

            return found ? best : -1;
        }
    }
}
