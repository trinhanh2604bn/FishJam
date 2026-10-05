using System;
using System.Collections.Generic;
using UnityEngine;

namespace FishPuzzle.Bubbles
{
    /// <summary>
    /// One authored pile slot. Row 0 is the bottom-most row.
    /// Down candidates are gravity edges only. No bubble movement is performed here.
    /// </summary>
    [Serializable]
    public sealed class BubblePileSlotDefinition
    {
        [SerializeField] private int _slotId;

        [Tooltip("RectTransform anchored position in the portrait gameplay field.")]
        [SerializeField] private Vector2 _anchoredPosition;

        [Tooltip("0 is the bottom-most row.")]
        [SerializeField] private int _row;

        [SerializeField] private int _column;

        [Tooltip("Higher values draw in front. Lower rows use higher sorting orders.")]
        [SerializeField] private int _sortingOrder;

        [Tooltip("Slots this bubble may fall toward. Each destination must be on a lower row.")]
        [SerializeField] private List<int> _downCandidateSlotIds = new List<int>();

        [Tooltip("New bubbles may enter this slot from above the pile.")]
        [SerializeField] private bool _canReceiveSpawnFromTop;

        public int SlotId => _slotId;

        public Vector2 AnchoredPosition => _anchoredPosition;

        public int Row => _row;

        public int Column => _column;

        public int SortingOrder => _sortingOrder;

        public IReadOnlyList<int> DownCandidateSlotIds => _downCandidateSlotIds;

        public bool CanReceiveSpawnFromTop => _canReceiveSpawnFromTop;
    }
}
