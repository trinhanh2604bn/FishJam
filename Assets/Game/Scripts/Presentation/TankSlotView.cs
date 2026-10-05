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
    }
}
