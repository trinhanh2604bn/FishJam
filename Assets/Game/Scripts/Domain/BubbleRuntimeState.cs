using System.Collections.Generic;

namespace FishPuzzle.Domain
{
    /// <summary>
    /// Fish currently inside one bubble. Removing a fish does not pop the bubble.
    /// A frozen bubble keeps its pile slot but its fish cannot be selected until the ice counter reaches 0.
    /// </summary>
    public sealed class BubbleRuntimeState
    {
        private readonly List<FishRuntimeState> _fish;

        public BubbleRuntimeState(string bubbleId, IReadOnlyList<FishRuntimeState> fish, bool inPlay)
        {
            BubbleId = bubbleId ?? string.Empty;
            _fish = new List<FishRuntimeState>(fish != null ? fish.Count : 0);
            if (fish != null)
            {
                for (var i = 0; i < fish.Count; i++)
                {
                    if (fish[i] != null)
                    {
                        _fish.Add(fish[i]);
                    }
                }
            }

            IsInPlay = inPlay;
        }

        public string BubbleId { get; }

        public bool IsInPlay { get; private set; }

        /// <summary>False once popped, or while frozen.</summary>
        public bool IsInteractable => !HasPopped && !IsFrozen;

        public bool HasPopped { get; private set; }

        /// <summary>Authored ice requirement for this attempt. 0 for a normal bubble.</summary>
        public int IceBreakRequiredSelections { get; private set; }

        /// <summary>Adjacent selections still needed. Counts down to 0; 0 means the bubble is normal.</summary>
        public int IceSelectionsRemaining { get; private set; }

        public bool IsFrozen => IceSelectionsRemaining > 0;

        public int RemainingFishCount => _fish.Count;

        public IReadOnlyList<FishRuntimeState> Fish => _fish;

        public void SetInPlay(bool inPlay)
        {
            IsInPlay = inPlay;
        }

        /// <summary>Starts this attempt frozen with the authored requirement. Values below 1 leave the bubble normal.</summary>
        public void Freeze(int requiredSelections)
        {
            IceBreakRequiredSelections = requiredSelections > 0 ? requiredSelections : 0;
            IceSelectionsRemaining = IceBreakRequiredSelections;
        }

        /// <summary>One adjacent fish selection. Returns true when the counter changed.</summary>
        public bool TryChipIce()
        {
            if (!IsFrozen || HasPopped)
            {
                return false;
            }

            IceSelectionsRemaining--;
            return true;
        }

        public void MarkPopped()
        {
            HasPopped = true;
            IsInPlay = false;
        }

        public bool Contains(FishRuntimeState fish)
        {
            return fish != null && _fish.Contains(fish);
        }

        public bool Remove(FishRuntimeState fish)
        {
            if (fish == null)
            {
                return false;
            }

            return _fish.Remove(fish);
        }
    }
}
