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
        public const float FishSize = 72f;

        public void Apply(IReadOnlyList<RectTransform> fishRects)
        {
            if (fishRects == null)
            {
                return;
            }

            if (fishRects.Count > 3)
            {
                GameLog.Warning(
                    nameof(BubbleFishLayoutController),
                    "No predefined fish layout exists for count " + fishRects.Count + ". Fish were stacked at the bubble center.");
            }

            for (var i = 0; i < fishRects.Count; i++)
            {
                if (!TryGetPlacement(i, fishRects.Count, out var position, out var size))
                {
                    position = Vector2.zero;
                    size = FishSize;
                }

                Place(fishRects[i], position, size);
            }
        }

        public static bool TryGetPlacement(int index, int count, out Vector2 anchoredPosition, out float size)
        {
            size = FishSize;
            switch (count)
            {
                case 1 when index == 0:
                    anchoredPosition = Vector2.zero;
                    return true;
                case 2 when index == 0:
                    anchoredPosition = new Vector2(-34f, 0f);
                    return true;
                case 2 when index == 1:
                    anchoredPosition = new Vector2(34f, 0f);
                    return true;
                case 3 when index == 0:
                    anchoredPosition = new Vector2(0f, 26f);
                    return true;
                case 3 when index == 1:
                    anchoredPosition = new Vector2(-34f, -22f);
                    return true;
                case 3 when index == 2:
                    anchoredPosition = new Vector2(34f, -22f);
                    return true;
                default:
                    anchoredPosition = Vector2.zero;
                    return false;
            }
        }

        public static void Prepare(RectTransform rect)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(FishSize, FishSize);
        }

        private static void Place(RectTransform rect, Vector2 anchoredPosition, float size)
        {
            if (rect == null)
            {
                return;
            }

            Prepare(rect);
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = anchoredPosition;
        }
    }
}
