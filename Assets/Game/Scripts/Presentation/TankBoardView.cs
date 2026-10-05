using System.Collections.Generic;
using FishPuzzle.Core;
using FishPuzzle.Domain;
using UnityEngine;

namespace FishPuzzle.Presentation
{
    /// <summary>
    /// Places the shelf's tank visuals. It does not route fish or advance targets.
    /// </summary>
    public sealed class TankBoardView : MonoBehaviour
    {
        [SerializeField] private RectTransform _slotRoot;
        [SerializeField] private float _horizontalSpacing = 260f;

        private readonly List<TankSlotView> _slots = new List<TankSlotView>();
        private GameObject _badgePrefab;

        public IReadOnlyList<TankSlotView> Slots => _slots;

        public int UnlockedPresentationCount
        {
            get
            {
                var count = 0;
                for (var i = 0; i < _slots.Count; i++)
                {
                    if (_slots[i] != null && _slots[i].IsPresentedAsUnlocked)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public void PresentInitialBoard(
            int slotCount,
            int unlockedCount,
            GameObject tankPrefab,
            GameObject badgePrefab,
            LevelData level,
            GameConfig config,
            FishVisualCatalog fishCatalog)
        {
            ClearOwnedSlots();
            if (_slotRoot == null || tankPrefab == null || slotCount <= 0)
            {
                return;
            }

            _badgePrefab = badgePrefab;
            var origin = -((slotCount - 1) * _horizontalSpacing) * 0.5f;
            for (var i = 0; i < slotCount; i++)
            {
                var instance = Instantiate(tankPrefab, _slotRoot);
                instance.name = "TankSlot_" + i;
                var rect = instance.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(origin + (i * _horizontalSpacing), 24f);
                rect.sizeDelta = new Vector2(230f, 270f);

                var slot = instance.GetComponent<TankSlotView>();
                if (slot == null)
                {
                    GameLog.Error(nameof(TankBoardView), "PF_TankSlot is missing TankSlotView.");
                    continue;
                }

                var unlocked = i < unlockedCount;
                slot.PresentInitialLockState(i, unlocked);
                if (unlocked && level != null && level.TargetGroupQueue != null && i < level.TargetGroupQueue.Count)
                {
                    var badge = CreateBadge(slot);
                    if (badge != null)
                    {
                        var capacity = config != null ? config.TankCapacity : 3;
                        badge.ShowPreviewTarget(level.TargetGroupQueue[i], capacity, fishCatalog);
                    }
                }

                _slots.Add(slot);
            }
        }

        public TargetBadgeView EnsureBadge(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _slots.Count || _slots[slotIndex] == null)
            {
                return null;
            }

            var slot = _slots[slotIndex];
            slot.PresentUnlocked();
            if (slot.PreviewBadge != null)
            {
                return slot.PreviewBadge;
            }

            return CreateBadge(slot);
        }

        private TargetBadgeView CreateBadge(TankSlotView slot)
        {
            if (slot == null || slot.BadgeAnchor == null || _badgePrefab == null)
            {
                return null;
            }

            var badgeObject = Instantiate(_badgePrefab, slot.BadgeAnchor);
            badgeObject.name = "PreviewTargetBadge_" + slot.SlotIndex;
            var badgeRect = badgeObject.GetComponent<RectTransform>();
            if (badgeRect != null)
            {
                badgeRect.anchoredPosition = Vector2.zero;
            }

            var badge = badgeObject.GetComponent<TargetBadgeView>();
            if (badge != null)
            {
                slot.AttachPreviewBadge(badge);
            }

            return badge;
        }

        private void ClearOwnedSlots()
        {
            for (var i = _slots.Count - 1; i >= 0; i--)
            {
                if (_slots[i] != null)
                {
                    DestroyObject(_slots[i].gameObject);
                }
            }

            _slots.Clear();
            if (_slotRoot == null)
            {
                return;
            }

            for (var i = _slotRoot.childCount - 1; i >= 0; i--)
            {
                var child = _slotRoot.GetChild(i);
                if (child.GetComponent<TankSlotView>() != null)
                {
                    DestroyObject(child.gameObject);
                }
            }
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
