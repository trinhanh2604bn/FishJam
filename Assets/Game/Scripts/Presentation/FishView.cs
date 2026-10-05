using System;
using FishPuzzle.Core;
using FishPuzzle.Domain;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FishPuzzle.Presentation
{
    /// <summary>
    /// Displays one fish sprite and reports a tap. It does not choose a tank or a tray slot.
    /// </summary>
    public sealed class FishView : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private Image _visual;
        [SerializeField] private FishVisualCatalog _catalog;
        [SerializeField] private FishType _displayedType;

        private Action<FishView> _onSelected;

        public int FishId { get; private set; }

        public FishType DisplayedType => _displayedType;

        public Sprite CurrentSprite => _visual != null ? _visual.sprite : null;

        public void BindInteraction(int fishId, Action<FishView> onSelected)
        {
            FishId = fishId;
            _onSelected = onSelected;
            SetRaycastTarget(true);
        }

        public void ReleaseInteraction()
        {
            _onSelected = null;
            SetRaycastTarget(false);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData == null || _onSelected == null)
            {
                return;
            }

            _onSelected(this);
        }

        private void SetRaycastTarget(bool raycastOn)
        {
            if (_visual != null)
            {
                _visual.raycastTarget = raycastOn;
            }
        }

        public void BindCatalog(FishVisualCatalog catalog)
        {
            _catalog = catalog;
        }

        public void Show(FishType fishType)
        {
            _displayedType = fishType;
            if (_visual == null)
            {
                GameLog.Error(nameof(FishView), "Fish visual Image is not assigned.");
                return;
            }

            if (_catalog == null)
            {
                GameLog.Error(nameof(FishView), "FishVisualCatalog is not assigned.");
                _visual.sprite = null;
                return;
            }

            if (!_catalog.TryGetSprite(fishType, out var sprite))
            {
                GameLog.Error(nameof(FishView), "No sprite is mapped for FishType." + fishType + ".");
                _visual.sprite = null;
                return;
            }

            _visual.sprite = sprite;
            _visual.enabled = true;
        }
    }
}
