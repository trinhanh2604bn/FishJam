using System.Collections.Generic;
using UnityEngine;

namespace FishPuzzle.Bubbles
{
    /// <summary>
    /// Deterministic slot graph for one bubble pile. This asset does not move bubbles.
    /// </summary>
    [CreateAssetMenu(fileName = "BubblePileLayout", menuName = "Fish Puzzle/Bubble Pile Layout")]
    public sealed class BubblePileLayout : ScriptableObject
    {
        [SerializeField] private List<BubblePileSlotDefinition> _slots = new List<BubblePileSlotDefinition>();

        public IReadOnlyList<BubblePileSlotDefinition> Slots => _slots;
    }
}
