using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace FishPuzzle.Presentation
{
    /// <summary>
    /// Forwards a tap on a locked tank's plus icon. It does not spend gold or unlock the tank.
    /// </summary>
    public sealed class LockedTankClick : MonoBehaviour, IPointerClickHandler
    {
        private int _slotIndex;
        private Action<int> _onClick;

        public void Bind(int slotIndex, Action<int> onClick)
        {
            _slotIndex = slotIndex;
            _onClick = onClick;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData == null || _onClick == null)
            {
                return;
            }

            _onClick(_slotIndex);
        }
    }
}
