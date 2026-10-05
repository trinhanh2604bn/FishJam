using UnityEngine;

namespace FishPuzzle.Presentation
{
    /// <summary>
    /// Visual waiting-tray slots. Gameplay insertion is owned by the level attempt, not this view.
    /// </summary>
    public sealed class WaitingTrayView : MonoBehaviour
    {
        [SerializeField] private RectTransform _slotsRoot;

        public int VisualSlotCount => _slotsRoot != null ? _slotsRoot.childCount : 0;

        public int PresentedOccupantCount
        {
            get
            {
                if (_slotsRoot == null)
                {
                    return 0;
                }

                return _slotsRoot.GetComponentsInChildren<FishView>(true).Length;
            }
        }

        public RectTransform GetSlotAnchor(int slotIndex)
        {
            if (_slotsRoot == null || slotIndex < 0)
            {
                return null;
            }

            return _slotsRoot.Find("Slot_" + slotIndex) as RectTransform;
        }

        public void PresentEmpty()
        {
            if (_slotsRoot == null)
            {
                return;
            }

            var fish = _slotsRoot.GetComponentsInChildren<FishView>(true);
            for (var i = 0; i < fish.Length; i++)
            {
                if (fish[i] != null)
                {
                    DestroyFish(fish[i].gameObject);
                }
            }
        }

        private static void DestroyFish(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            DestroyImmediate(target);
        }
    }
}
