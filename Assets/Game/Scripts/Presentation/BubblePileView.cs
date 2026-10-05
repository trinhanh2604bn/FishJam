using System.Collections.Generic;
using FishPuzzle.Bubbles;
using FishPuzzle.Core;
using FishPuzzle.Domain;
using UnityEngine;

namespace FishPuzzle.Presentation
{
    /// <summary>
    /// Places the initial visible bubbles on authored slots. It does not resolve gravity or spawn replacements.
    /// Queue index i is shown in the slot whose slotId is i. Later queue entries stay pending.
    /// </summary>
    public sealed class BubblePileView : MonoBehaviour
    {
        [SerializeField] private RectTransform _slotRoot;

        private readonly Dictionary<int, BubbleView> _viewsBySlot = new Dictionary<int, BubbleView>();
        private readonly List<BubbleView> _visible = new List<BubbleView>();
        private readonly List<BubbleDefinition> _queue = new List<BubbleDefinition>();

        private BubblePileLayout _layout;
        private GameObject _bubblePrefab;
        private GameObject _fishPrefab;
        private FishVisualCatalog _fishCatalog;

        public int VisibleBubbleCount => _visible.Count;

        public int PendingBubbleCount { get; private set; }

        public int NextQueueIndex { get; private set; }

        public IReadOnlyList<BubbleView> VisibleBubbles => _visible;

        public bool TryGetBubble(int slotId, out BubbleView bubble)
        {
            return _viewsBySlot.TryGetValue(slotId, out bubble) && bubble != null;
        }

        public void PresentInitialQueue(
            IReadOnlyList<BubbleDefinition> queue,
            BubblePileLayout layout,
            GameObject bubblePrefab,
            GameObject fishPrefab,
            FishVisualCatalog fishCatalog)
        {
            ClearOwnedBubbles();
            _queue.Clear();
            _layout = layout;
            _bubblePrefab = bubblePrefab;
            _fishPrefab = fishPrefab;
            _fishCatalog = fishCatalog;
            PendingBubbleCount = 0;
            NextQueueIndex = 0;
            if (queue != null)
            {
                for (var i = 0; i < queue.Count; i++)
                {
                    _queue.Add(queue[i]);
                }
            }

            if (queue == null || layout == null || layout.Slots == null || bubblePrefab == null || _slotRoot == null)
            {
                GameLog.Error(nameof(BubblePileView), "Initial bubble pile could not be presented. Queue, layout, or bubble prefab is missing.");
                return;
            }

            var placed = 0;
            for (var index = 0; index < queue.Count; index++)
            {
                if (!TryFindSlot(layout, index, out var slot))
                {
                    break;
                }

                var definition = queue[index];
                var instance = Instantiate(bubblePrefab, _slotRoot);
                instance.name = "Bubble_" + (definition != null ? definition.BubbleId : index.ToString());
                PrepareRect(instance.GetComponent<RectTransform>(), slot.AnchoredPosition);

                var view = instance.GetComponent<BubbleView>();
                if (view == null)
                {
                    GameLog.Error(nameof(BubblePileView), "PF_Bubble is missing BubbleView.");
                    continue;
                }

                view.Bind(definition, slot.SlotId, fishCatalog, fishPrefab);
                _viewsBySlot[slot.SlotId] = view;
                _visible.Add(view);
                placed++;
            }

            ApplySortingOrder(layout);
            NextQueueIndex = placed;
            PendingBubbleCount = queue.Count - placed;
        }

        private void ApplySortingOrder(BubblePileLayout layout)
        {
            var ordered = new List<BubbleView>(_visible);
            ordered.Sort(CompareSorting);
            for (var i = 0; i < ordered.Count; i++)
            {
                if (ordered[i] != null)
                {
                    ordered[i].transform.SetSiblingIndex(i);
                }
            }

            int CompareSorting(BubbleView left, BubbleView right)
            {
                var leftOrder = FindSortingOrder(layout, left != null ? left.SlotId : 0);
                var rightOrder = FindSortingOrder(layout, right != null ? right.SlotId : 0);
                var order = leftOrder.CompareTo(rightOrder);
                if (order != 0)
                {
                    return order;
                }

                var leftSlot = left != null ? left.SlotId : 0;
                var rightSlot = right != null ? right.SlotId : 0;
                return leftSlot.CompareTo(rightSlot);
            }
        }

        private static int FindSortingOrder(BubblePileLayout layout, int slotId)
        {
            if (layout == null || layout.Slots == null)
            {
                return 0;
            }

            for (var i = 0; i < layout.Slots.Count; i++)
            {
                var slot = layout.Slots[i];
                if (slot != null && slot.SlotId == slotId)
                {
                    return slot.SortingOrder;
                }
            }

            return 0;
        }

        public bool TryGetByBubbleId(string bubbleId, out BubbleView bubble)
        {
            bubble = null;
            if (string.IsNullOrEmpty(bubbleId))
            {
                return false;
            }

            for (var i = 0; i < _visible.Count; i++)
            {
                var candidate = _visible[i];
                if (candidate != null && candidate.BubbleId == bubbleId)
                {
                    bubble = candidate;
                    return true;
                }
            }

            return false;
        }

        public bool TryGetSlotPosition(int slotId, out Vector2 anchoredPosition)
        {
            anchoredPosition = Vector2.zero;
            if (!TryFindSlot(_layout, slotId, out var slot) || slot == null)
            {
                return false;
            }

            anchoredPosition = slot.AnchoredPosition;
            return true;
        }

        public bool TryDetach(string bubbleId, out BubbleView bubble)
        {
            if (!TryGetByBubbleId(bubbleId, out bubble) || bubble == null)
            {
                bubble = null;
                return false;
            }

            if (_viewsBySlot.TryGetValue(bubble.SlotId, out var occupying) && occupying == bubble)
            {
                _viewsBySlot.Remove(bubble.SlotId);
            }

            _visible.Remove(bubble);
            return true;
        }

        public void AdoptSlot(BubbleView bubble, int slotId)
        {
            if (bubble == null)
            {
                return;
            }

            if (_viewsBySlot.TryGetValue(bubble.SlotId, out var previous) && previous == bubble)
            {
                _viewsBySlot.Remove(bubble.SlotId);
            }

            bubble.SetSlot(slotId);
            _viewsBySlot[slotId] = bubble;
            if (!_visible.Contains(bubble))
            {
                _visible.Add(bubble);
            }

            RefreshDepth();
        }

        public void NoteSpawned()
        {
            if (PendingBubbleCount > 0)
            {
                PendingBubbleCount--;
            }

            NextQueueIndex++;
        }

        public BubbleView CreateIncoming(string bubbleId, int slotId, Vector2 anchoredPosition)
        {
            if (_bubblePrefab == null || _slotRoot == null)
            {
                GameLog.Error(nameof(BubblePileView), "Cannot spawn bubble " + bubbleId + ". The bubble prefab is missing.");
                return null;
            }

            var definition = FindDefinition(bubbleId);
            var instance = Instantiate(_bubblePrefab, _slotRoot);
            instance.name = "Bubble_" + bubbleId;
            PrepareRect(instance.GetComponent<RectTransform>(), anchoredPosition);
            var view = instance.GetComponent<BubbleView>();
            if (view == null)
            {
                GameLog.Error(nameof(BubblePileView), "PF_Bubble is missing BubbleView.");
                DestroyObject(instance);
                return null;
            }

            view.Bind(definition, slotId, _fishCatalog, _fishPrefab);
            _viewsBySlot[slotId] = view;
            _visible.Add(view);
            RefreshDepth();
            NoteSpawned();
            return view;
        }

        public void RefreshDepth()
        {
            ApplySortingOrder(_layout);
        }

        private BubbleDefinition FindDefinition(string bubbleId)
        {
            for (var i = 0; i < _queue.Count; i++)
            {
                var definition = _queue[i];
                if (definition != null && definition.BubbleId == bubbleId)
                {
                    return definition;
                }
            }

            GameLog.Error(nameof(BubblePileView), "Bubble definition " + bubbleId + " is not in the level queue.");
            return null;
        }

        private static void PrepareRect(RectTransform rect, Vector2 anchoredPosition)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(300f, 300f);
            rect.localScale = Vector3.one;
        }

        private static bool TryFindSlot(BubblePileLayout layout, int slotId, out BubblePileSlotDefinition slot)
        {
            slot = null;
            if (layout == null || layout.Slots == null)
            {
                return false;
            }

            for (var i = 0; i < layout.Slots.Count; i++)
            {
                var candidate = layout.Slots[i];
                if (candidate != null && candidate.SlotId == slotId)
                {
                    slot = candidate;
                    return true;
                }
            }

            return false;
        }

        private void ClearOwnedBubbles()
        {
            for (var i = _visible.Count - 1; i >= 0; i--)
            {
                if (_visible[i] != null)
                {
                    DestroyObject(_visible[i].gameObject);
                }
            }

            _visible.Clear();
            _viewsBySlot.Clear();
        }

        private static void DestroyObject(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            DestroyImmediate(target);
        }
    }
}
