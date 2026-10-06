using System;
using UnityEngine;
using UnityEngine.UI;

namespace FishPuzzle.Presentation
{
    /// <summary>
    /// Visual state of one tank slot. Lock presentation is not tank-domain behavior.
    /// </summary>
    public sealed class TankSlotView : MonoBehaviour
    {
        [SerializeField] private GameObject _lockedOverlay;
        [SerializeField] private RectTransform _badgeAnchor;

        public int SlotIndex { get; private set; }

        public bool IsPresentedAsUnlocked { get; private set; }

        public TargetBadgeView PreviewBadge { get; private set; }

        public RectTransform BadgeAnchor => _badgeAnchor;

        public void PresentInitialLockState(int slotIndex, bool unlocked)
        {
            SlotIndex = slotIndex;
            IsPresentedAsUnlocked = unlocked;
            if (_lockedOverlay != null)
            {
                _lockedOverlay.SetActive(!unlocked);
            }
        }

        public void PresentUnlocked()
        {
            IsPresentedAsUnlocked = true;
            if (_lockedOverlay != null)
            {
                _lockedOverlay.SetActive(false);
            }
        }

        public void BindLockedClick(Action<int> onClick)
        {
            if (IsPresentedAsUnlocked || _lockedOverlay == null || onClick == null)
            {
                return;
            }

            var plus = _lockedOverlay.GetComponentInChildren<Image>(true);
            if (plus == null)
            {
                return;
            }

            plus.raycastTarget = true;
            var click = plus.GetComponent<LockedTankClick>();
            if (click == null)
            {
                click = plus.gameObject.AddComponent<LockedTankClick>();
            }

            click.Bind(SlotIndex, onClick);
        }

        public void AttachPreviewBadge(TargetBadgeView badge)
        {
            PreviewBadge = badge;
        }

        public RectTransform GetFishAnchor(int ordinal)
        {
            if (ordinal < 0)
            {
                return null;
            }

            var container = transform.Find("FishContainer");
            if (container == null)
            {
                return null;
            }

            return container.Find("FishSlot_" + ordinal) as RectTransform;
        }

        /// <summary>Fish inside a tank keep the gameplay base size. They overlap instead of shrinking.</summary>
        public const float TankFishSize = BubbleFishLayoutController.FishSize;

        private static readonly Vector2[][] TankFormations =
        {
            new[] { new Vector2(0f, 0f) },
            new[] { new Vector2(-28f, 4f), new Vector2(28f, -4f) },
            new[] { new Vector2(-40f, 7f), new Vector2(0f, -6f), new Vector2(40f, 7f) }
        };

        public static Vector2 TankFishPosition(int ordinal, int occupiedCount)
        {
            var count = Mathf.Clamp(occupiedCount, 1, TankFormations.Length);
            var formation = TankFormations[count - 1];
            return ordinal >= 0 && ordinal < formation.Length ? formation[ordinal] : Vector2.zero;
        }

        /// <summary>
        /// Places occupied fish close together in the water at the fixed base size.
        /// Later fish draw in front (sibling order) so 1/3, 2/3 and 3/3 stay countable.
        /// </summary>
        public void LayoutContainedFish(int occupiedCount)
        {
            var count = Mathf.Clamp(occupiedCount, 1, TankFormations.Length);
            for (var ordinal = 0; ordinal < TankFormations.Length; ordinal++)
            {
                var position = ordinal < count ? TankFishPosition(ordinal, count) : TankFishPosition(ordinal, ordinal + 1);
                PlaceFishSlot(ordinal, position, TankFishSize);
                var slot = GetFishAnchor(ordinal);
                if (slot != null)
                {
                    slot.SetSiblingIndex(ordinal);
                }
            }
        }

        private void PlaceFishSlot(int ordinal, Vector2 anchoredPosition, float size)
        {
            var slot = GetFishAnchor(ordinal);
            if (slot == null)
            {
                return;
            }

            slot.anchorMin = new Vector2(0.5f, 0.5f);
            slot.anchorMax = new Vector2(0.5f, 0.5f);
            slot.pivot = new Vector2(0.5f, 0.5f);
            slot.anchoredPosition = anchoredPosition;
            slot.sizeDelta = new Vector2(size, size);
        }
    }
}
