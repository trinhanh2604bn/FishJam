using System;
using System.Collections.Generic;
using FishPuzzle.Bubbles;
using UnityEngine;

namespace FishPuzzle.Domain
{
    /// <summary>
    /// Authored contents of one bubble. This type stores data only.
    /// A standard bubble uses <see cref="BubbleModifier.None"/>; a Frozen Bubble uses <see cref="BubbleModifier.Frozen"/>
    /// with a positive <see cref="IceBreakRequiredSelections"/>. Both are checked by <see cref="LevelValidator"/>.
    /// </summary>
    [Serializable]
    public sealed class BubbleDefinition
    {
        [Tooltip("Stable technical id, such as L001_B001. Do not use a visual GameObject name.")]
        [SerializeField] private string _bubbleId;

        [Tooltip("Fish contained in this bubble, in authored order. 2 to GameConfig.MaxFishPerBubble (5) fish.")]
        [SerializeField] private List<FishType> _fishes = new List<FishType>();

        [Tooltip("None = normal bubble. Frozen = fish are locked until IceBreakRequiredSelections adjacent fish are selected. Locked is reserved.")]
        [SerializeField] private BubbleModifier _modifier = BubbleModifier.None;

        [Tooltip("Frozen only: fish selections from adjacent pile slots needed to break the ice. Normal bubbles use 0.")]
        [SerializeField] private int _iceBreakRequiredSelections;

        public string BubbleId => _bubbleId;

        public IReadOnlyList<FishType> Fishes => _fishes;

        public BubbleModifier Modifier => _modifier;

        public bool IsFrozen => _modifier == BubbleModifier.Frozen;

        /// <summary>Authored requirement. Runtime progress lives in <see cref="BubbleRuntimeState.IceSelectionsRemaining"/>.</summary>
        public int IceBreakRequiredSelections => _iceBreakRequiredSelections;
    }
}
