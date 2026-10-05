using System;
using System.Collections.Generic;
using FishPuzzle.Bubbles;
using UnityEngine;

namespace FishPuzzle.Domain
{
    /// <summary>
    /// Authored contents of one bubble. This type stores data only.
    /// A standard bubble uses <see cref="BubbleModifier.None"/> and is checked by <see cref="LevelValidator"/>.
    /// </summary>
    [Serializable]
    public sealed class BubbleDefinition
    {
        [Tooltip("Stable technical id, such as L001_B001. Do not use a visual GameObject name.")]
        [SerializeField] private string _bubbleId;

        [Tooltip("Fish contained in this bubble, in authored order. The total count is not fixed at 3.")]
        [SerializeField] private List<FishType> _fishes = new List<FishType>();

        [Tooltip("Standard bubbles use None. Frozen and Locked are reserved and are not played in this milestone.")]
        [SerializeField] private BubbleModifier _modifier = BubbleModifier.None;

        public string BubbleId => _bubbleId;

        public IReadOnlyList<FishType> Fishes => _fishes;

        public BubbleModifier Modifier => _modifier;
    }
}
