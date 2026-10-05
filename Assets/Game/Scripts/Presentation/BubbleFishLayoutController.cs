using System.Collections.Generic;
using FishPuzzle.Core;
using UnityEngine;

namespace FishPuzzle.Presentation
{
    /// <summary>
    /// Deterministic local positions for fish inside one bubble.
    /// Counts other than the layouts defined here are reserved for later milestones.
    /// </summary>
    public sealed class BubbleFishLayoutController : MonoBehaviour
    {
        public void Apply(IReadOnlyList<RectTransform> fishRects)
        {
            if (fishRects == null)
            {
                return;
            }

            switch (fishRects.Count)
            {
                case 0:
                    break;
                case 1:
                    Place(fishRects[0], Vector2.zero, 72f);
                    break;
                case 2:
                    Place(fishRects[0], new Vector2(-34f, 0f), 72f);
                    Place(fishRects[1], new Vector2(34f, 0f), 72f);
                    break;
                case 3:
                    Place(fishRects[0], new Vector2(0f, 26f), 72f);
                    Place(fishRects[1], new Vector2(-34f, -22f), 72f);
                    Place(fishRects[2], new Vector2(34f, -22f), 72f);
                    break;
                default:
                    GameLog.Warning(
                        nameof(BubbleFishLayoutController),
                        "No predefined fish layout exists for count " + fishRects.Count + ". Fish were stacked at the bubble center.");
                    for (var i = 0; i < fishRects.Count; i++)
                    {
                        Place(fishRects[i], Vector2.zero, 72f);
                    }

                    break;
            }
        }

        private static void Place(RectTransform rect, Vector2 anchoredPosition, float size)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(size, size);
        }
    }
}
