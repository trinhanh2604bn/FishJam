namespace FishPuzzle.Bubbles
{
    /// <summary>
    /// Slot neighbours in the authored pile graph. Two slots touch when either lists the other as a gravity
    /// down candidate, or when they share a row in neighbouring columns. Screen distance is never used.
    /// </summary>
    public static class BubblePileAdjacency
    {
        public static bool AreAdjacent(BubblePileSlotDefinition a, BubblePileSlotDefinition b)
        {
            if (a == null || b == null || a.SlotId == b.SlotId)
            {
                return false;
            }

            if (ListsDown(a, b.SlotId) || ListsDown(b, a.SlotId))
            {
                return true;
            }

            return a.Row == b.Row && (a.Column - b.Column == 1 || b.Column - a.Column == 1);
        }

        private static bool ListsDown(BubblePileSlotDefinition slot, int slotId)
        {
            var down = slot.DownCandidateSlotIds;
            if (down == null)
            {
                return false;
            }

            for (var i = 0; i < down.Count; i++)
            {
                if (down[i] == slotId)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
