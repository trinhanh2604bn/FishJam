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

        /// <summary>Vertical distance between rows as a fraction of the pitch: sqrt(3)/2 for touching marbles.</summary>
        public const float RowPitchRatio = 0.8660254f;

        /// <summary>Visible bubble diameter over the pitch. Slightly above 1 so packed bubbles squeeze together.</summary>
        public const float MarbleSqueeze = 1.04f;

        private const float SideMargin = 6f;
        private const float BottomMargin = 4f;
        private const float TopMargin = 0f;
        private const float MinContentScale = 0.6f;
        private const float MaxContentScale = 1.6f;

        private BubblePileLayout _layout;
        private bool _packed;
        private float _pitch;
        private readonly Dictionary<int, int> _rowCounts = new Dictionary<int, int>();
        private int _minRow;
        private Vector2 _origin;
        private Vector2 _appliedFieldSize;
        private GameObject _bubblePrefab;
        private GameObject _fishPrefab;
        private FishVisualCatalog _fishCatalog;

        private void LateUpdate()
        {
            SyncFieldPositions();
        }

        private void SyncFieldPositions()
        {
            if (_slotRoot == null || _layout == null || _viewsBySlot.Count == 0)
            {
                return;
            }

            var size = _slotRoot.rect.size;
            if (size.x < 400f || size.y < 400f)
            {
                return;
            }

            if ((size - _appliedFieldSize).sqrMagnitude < 4f)
            {
                return;
            }

            _appliedFieldSize = size;
            ComputePacking();
            foreach (var pair in _viewsBySlot)
            {
                if (pair.Value == null || !TryFindSlot(_layout, pair.Key, out var slot) || slot == null)
                {
                    continue;
                }

                var rect = pair.Value.GetComponent<RectTransform>();
                if (rect != null)
                {
                    rect.anchoredPosition = MapToField(slot);
                }

                pair.Value.SetContentScale(ContentScale);
            }
        }

        /// <summary>Whole-bubble scale from marble packing (1 before the field has a usable size).</summary>
        public float ContentScale { get; private set; } = 1f;

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
            _appliedFieldSize = Vector2.zero;
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

            Canvas.ForceUpdateCanvases();
            ComputePacking();

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
                PrepareRect(instance.GetComponent<RectTransform>(), MapToField(slot));

                var view = instance.GetComponent<BubbleView>();
                if (view == null)
                {
                    GameLog.Error(nameof(BubblePileView), "PF_Bubble is missing BubbleView.");
                    continue;
                }

                view.Bind(definition, slot.SlotId, fishCatalog, fishPrefab);
                view.SetContentScale(ContentScale);
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

        public bool TryGetSlotRow(int slotId, out int row)
        {
            row = 0;
            if (!TryFindSlot(_layout, slotId, out var slot) || slot == null)
            {
                return false;
            }

            row = slot.Row;
            return true;
        }

        public bool TryGetSlotPosition(int slotId, out Vector2 anchoredPosition)
        {
            anchoredPosition = Vector2.zero;
            SyncFieldPositions();
            if (!TryFindSlot(_layout, slotId, out var slot) || slot == null)
            {
                return false;
            }

            anchoredPosition = MapToField(slot);
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
            view.SetContentScale(ContentScale);
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

        /// <summary>
        /// Marble packing. Slots keep their authored rows and columns, but are placed as a hexagonal stack:
        /// same-row neighbours are one bubble apart and each row sits in the hollow of the row below, so
        /// diagonal neighbours touch too. The stack rests on the bottom of the field (gravity) and is
        /// sized to the largest pitch that fits; bubbles and fish grow with it.
        /// </summary>
        private void ComputePacking()
        {
            _packed = false;
            ContentScale = 1f;
            var area = _slotRoot != null ? _slotRoot.rect : new Rect(0f, 0f, 0f, 0f);
            if (area.width < 64f || area.height < 64f || _layout == null || _layout.Slots == null || _layout.Slots.Count == 0)
            {
                return;
            }

            var minRow = int.MaxValue;
            var maxRow = int.MinValue;
            _rowCounts.Clear();
            var slots = _layout.Slots;
            for (var i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot == null)
                {
                    continue;
                }

                minRow = Mathf.Min(minRow, slot.Row);
                maxRow = Mathf.Max(maxRow, slot.Row);
                _rowCounts.TryGetValue(slot.Row, out var inRow);
                _rowCounts[slot.Row] = inRow + 1;
            }

            if (minRow > maxRow)
            {
                return;
            }

            var widestRow = 1;
            foreach (var pair in _rowCounts)
            {
                widestRow = Mathf.Max(widestRow, pair.Value);
            }

            // Visible diameter = pitch * MarbleSqueeze, so neighbours press slightly into each other.
            var columnsSpan = widestRow - 1;
            var rowsSpan = (maxRow - minRow) * RowPitchRatio;
            var byWidth = (area.width - (2f * SideMargin)) / (columnsSpan + MarbleSqueeze);
            var byHeight = (area.height - BottomMargin - TopMargin) / (rowsSpan + MarbleSqueeze);
            var pitch = Mathf.Max(40f, Mathf.Min(byWidth, byHeight));
            ContentScale = Mathf.Clamp(
                pitch * MarbleSqueeze / (BubbleView.BaseSize * BubbleView.VisibleDiameterFraction),
                MinContentScale,
                MaxContentScale);
            // Keep the pitch consistent with a clamped scale so bubbles always touch.
            pitch = ContentScale * BubbleView.BaseSize * BubbleView.VisibleDiameterFraction / MarbleSqueeze;
            _pitch = pitch;
            _minRow = minRow;
            // Anchored positions are measured from the field centre (bubbles use centre anchors).
            _origin = new Vector2(0f, (-area.height * 0.5f) + BottomMargin + (pitch * MarbleSqueeze * 0.5f));
            _packed = true;
        }

        /// <summary>
        /// Row and column decide the packed position; authored x/y jitter is ignored. Each row is centred, so a
        /// 2-slot row sits in the hollows of a 3-slot row like stacked marbles.
        /// </summary>
        private Vector2 MapToField(BubblePileSlotDefinition slot)
        {
            if (slot == null)
            {
                return Vector2.zero;
            }

            if (!_packed)
            {
                ComputePacking();
            }

            if (!_packed)
            {
                return slot.AnchoredPosition;
            }

            _rowCounts.TryGetValue(slot.Row, out var inRow);
            var x = (slot.Column - ((Mathf.Max(1, inRow) - 1) * 0.5f)) * _pitch;
            var y = (slot.Row - _minRow) * RowPitchRatio * _pitch;
            return _origin + new Vector2(x, y);
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
            SceneObjectCleanup.DestroyObject(target);
        }
    }
}
