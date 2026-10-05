using System.Collections.Generic;

namespace FishPuzzle.Domain
{
    /// <summary>
    /// Fish currently inside one bubble. Removing a fish does not pop the bubble.
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
            IsInteractable = true;
        }

        public string BubbleId { get; }

        public bool IsInPlay { get; private set; }

        public bool IsInteractable { get; private set; }

        public bool HasPopped { get; private set; }

        public int RemainingFishCount => _fish.Count;

        public IReadOnlyList<FishRuntimeState> Fish => _fish;

        public void SetInPlay(bool inPlay)
        {
            IsInPlay = inPlay;
        }

        public void MarkPopped()
        {
            HasPopped = true;
            IsInteractable = false;
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
