namespace FishPuzzle.Bubbles
{
    /// <summary>
    /// One deterministic occupancy change. The same bubble keeps its identity.
    /// </summary>
    public readonly struct BubblePileMove
    {
        public BubblePileMove(string bubbleId, int fromSlotId, int toSlotId)
        {
            BubbleId = bubbleId ?? string.Empty;
            FromSlotId = fromSlotId;
            ToSlotId = toSlotId;
        }

        public string BubbleId { get; }

        public int FromSlotId { get; }

        public int ToSlotId { get; }
    }

    /// <summary>
    /// One pending bubble placed into an empty top spawn slot.
    /// </summary>
    public readonly struct BubbleSpawn
    {
        public BubbleSpawn(string bubbleId, int slotId)
        {
            BubbleId = bubbleId ?? string.Empty;
            SlotId = slotId;
        }

        public string BubbleId { get; }

        public int SlotId { get; }
    }
}
