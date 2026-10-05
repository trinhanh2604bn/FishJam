using System.Collections.Generic;
using FishPuzzle.Core;
using FishPuzzle.Domain;
using UnityEngine;
using UnityEngine.UI;

namespace FishPuzzle.Presentation
{
    /// <summary>
    /// Presents one bubble definition. It does not pop, route, or move the bubble.
    /// </summary>
    public sealed class BubbleView : MonoBehaviour
    {
        [SerializeField] private RectTransform _fishContainer;
        [SerializeField] private BubbleFishLayoutController _fishLayout;

        private readonly List<FishView> _fishViews = new List<FishView>();

        public string BubbleId { get; private set; }

        public int SlotId { get; private set; }

        public void SetSlot(int slotId)
        {
            SlotId = slotId;
        }

        public BubbleDefinition SourceDefinition { get; private set; }

        public IReadOnlyList<FishView> FishViews => _fishViews;

        public void Bind(BubbleDefinition definition, int slotId, FishVisualCatalog catalog, GameObject fishPrefab)
        {
            AllowFishRaycasts();
            ClearFish();
            SourceDefinition = definition;
            SlotId = slotId;
            BubbleId = definition != null ? definition.BubbleId : string.Empty;
            if (definition == null || definition.Fishes == null || _fishContainer == null || fishPrefab == null)
            {
                GameLog.Error(nameof(BubbleView), "Bubble " + BubbleId + " could not bind fish. Definition, container, or fish prefab is missing.");
                return;
            }

            if (catalog == null)
            {
                GameLog.Error(nameof(BubbleView), "Bubble " + BubbleId + " has no FishVisualCatalog.");
                return;
            }

            var rects = new List<RectTransform>(definition.Fishes.Count);
            for (var i = 0; i < definition.Fishes.Count; i++)
            {
                var fishObject = Instantiate(fishPrefab, _fishContainer);
                fishObject.name = "Fish_" + i;
                var rect = fishObject.GetComponent<RectTransform>();
                rects.Add(rect);
                var view = fishObject.GetComponent<FishView>();
                if (view == null)
                {
                    GameLog.Error(nameof(BubbleView), "PF_Fish is missing FishView.");
                    continue;
                }

                view.BindCatalog(catalog);
                view.Show(definition.Fishes[i]);
                _fishViews.Add(view);
            }

            if (_fishLayout != null)
            {
                _fishLayout.Apply(rects);
            }
        }

        public void ReleaseFish(FishView fish)
        {
            if (fish == null)
            {
                return;
            }

            _fishViews.Remove(fish);
            ApplyLayout();
        }

        public void ClearFish()
        {
            for (var i = _fishViews.Count - 1; i >= 0; i--)
            {
                if (_fishViews[i] != null)
                {
                    DestroyObject(_fishViews[i].gameObject);
                }
            }

            _fishViews.Clear();
            if (_fishContainer == null)
            {
                return;
            }

            for (var i = _fishContainer.childCount - 1; i >= 0; i--)
            {
                DestroyObject(_fishContainer.GetChild(i).gameObject);
            }
        }

        private void ApplyLayout()
        {
            if (_fishLayout == null)
            {
                return;
            }

            var rects = new List<RectTransform>(_fishViews.Count);
            for (var i = 0; i < _fishViews.Count; i++)
            {
                if (_fishViews[i] == null)
                {
                    continue;
                }

                var rect = _fishViews[i].transform as RectTransform;
                if (rect != null)
                {
                    rects.Add(rect);
                }
            }

            _fishLayout.Apply(rects);
        }

        private void AllowFishRaycasts()
        {
            var area = transform.Find("InteractionArea");
            if (area == null)
            {
                return;
            }

            var image = area.GetComponent<Image>();
            if (image != null)
            {
                image.raycastTarget = false;
            }
        }

        private static void DestroyObject(GameObject target)
        {
            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}
