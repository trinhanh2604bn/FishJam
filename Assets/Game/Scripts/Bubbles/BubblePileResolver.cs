using System.Collections.Generic;
using FishPuzzle.Core;
using UnityEngine;

namespace FishPuzzle.Bubbles
{
    /// <summary>
    /// Deterministic pile gravity. Lower rows move before upper rows.
    /// Destination choice is lowest Y, then shortest horizontal move, then lowest slot id.
    /// </summary>
    public static class BubblePileResolver
    {
        public static List<BubblePileMove> ResolveUntilStable(BubblePileOccupancy pile)
        {
            var moves = new List<BubblePileMove>();
            if (pile == null)
            {
                return moves;
            }

            var occupied = new List<BubblePileSlotDefinition>(pile.SlotCount);
            var limit = (pile.SlotCount * pile.SlotCount) + 1;
            var guard = 0;
            while (guard < limit)
            {
                guard++;
                pile.CopyOccupiedSlots(occupied);
                occupied.Sort(CompareLowerFirst);
                var moved = false;
                for (var i = 0; i < occupied.Count; i++)
                {
                    var from = occupied[i];
                    if (from == null || !pile.IsOccupied(from.SlotId))
                    {
                        continue;
                    }

                    var bubble = pile.GetAtSlot(from.SlotId);
                    if (bubble == null)
                    {
                        continue;
                    }

                    if (!TryChooseDestination(pile, from, out var destination))
                    {
                        continue;
                    }

                    pile.Move(bubble.BubbleId, destination.SlotId);
                    moves.Add(new BubblePileMove(bubble.BubbleId, from.SlotId, destination.SlotId));
                    moved = true;
                }

                if (!moved)
                {
                    return moves;
                }
            }

            GameLog.Error(nameof(BubblePileResolver), "Bubble pile did not settle within " + limit + " passes.");
            return moves;
        }

        private static int CompareLowerFirst(BubblePileSlotDefinition left, BubblePileSlotDefinition right)
        {
            var row = left.Row.CompareTo(right.Row);
            if (row != 0)
            {
                return row;
            }

            var column = left.Column.CompareTo(right.Column);
            if (column != 0)
            {
                return column;
            }

            return left.SlotId.CompareTo(right.SlotId);
        }

        private static bool TryChooseDestination(
            BubblePileOccupancy pile,
            BubblePileSlotDefinition origin,
            out BubblePileSlotDefinition destination)
        {
            destination = null;
            var candidates = origin.DownCandidateSlotIds;
            if (candidates == null)
            {
                return false;
            }

            for (var i = 0; i < candidates.Count; i++)
            {
                var slotId = candidates[i];
                if (pile.IsOccupied(slotId) || !pile.TryGetSlotDefinition(slotId, out var candidate) || candidate == null)
                {
                    continue;
                }

                if (destination == null || IsBetterDestination(origin, candidate, destination))
                {
                    destination = candidate;
                }
            }

            return destination != null;
        }

        private static bool IsBetterDestination(
            BubblePileSlotDefinition origin,
            BubblePileSlotDefinition candidate,
            BubblePileSlotDefinition current)
        {
            var candidateY = candidate.AnchoredPosition.y;
            var currentY = current.AnchoredPosition.y;
            if (candidateY < currentY - 0.01f)
            {
                return true;
            }

            if (candidateY > currentY + 0.01f)
            {
                return false;
            }

            var candidateDx = Mathf.Abs(candidate.AnchoredPosition.x - origin.AnchoredPosition.x);
            var currentDx = Mathf.Abs(current.AnchoredPosition.x - origin.AnchoredPosition.x);
            if (candidateDx < currentDx - 0.01f)
            {
                return true;
            }

            if (candidateDx > currentDx + 0.01f)
            {
                return false;
            }

            return candidate.SlotId < current.SlotId;
        }
    }
}
