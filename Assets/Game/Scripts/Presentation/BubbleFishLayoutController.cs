using System.Collections.Generic;
using FishPuzzle.Core;
using UnityEngine;

namespace FishPuzzle.Presentation
{
    /// <summary>
    /// Deterministic local positions for fish inside one bubble.
    /// Controls POSITION only. Every fish uses the fixed base size FishSize (124). Formations exist for 1 to 5 fish (GameConfig.MaxFishPerBubble).
    /// </summary>
    public sealed class BubbleFishLayoutController : MonoBehaviour
    {
        public const float FishSize = 124f;

        public void Apply(IReadOnlyList<RectTransform> fishRects)
        {
            if (fishRects == null)
            {
                return;
            }

            if (fishRects.Count > MaxLayoutCount)
            {
                GameLog.Warning(
                    nameof(BubbleFishLayoutController),
                    "No predefined fish layout exists for count " + fishRects.Count + ". Fish were stacked at the bubble center.");
            }

            for (var i = 0; i < fishRects.Count; i++)
            {
                if (!TryGetPosition(i, fishRects.Count, out var position))
                {
                    position = Vector2.zero;
                }

                Place(fishRects[i], position);
            }
        }

        public const int MaxLayoutCount = 5;

        // Local positions inside a 300 px bubble. Every fish keeps the same base size (FishSize).
        // 4-5 fish spread their positions instead of shrinking. Small overlap with neighbours
        // or the BubbleFront rim is accepted; fish are never scaled to fit.
        private static readonly Vector2[][] Formations =
        {
            new Vector2[0],
            new[] { Vector2.zero },
            new[] { new Vector2(-44f, 0f), new Vector2(44f, 0f) },
            new[] { new Vector2(0f, 38f), new Vector2(-46f, -30f), new Vector2(46f, -30f) },
            new[] { new Vector2(-48f, 40f), new Vector2(48f, 40f), new Vector2(-48f, -40f), new Vector2(48f, -40f) },
            new[]
            {
                new Vector2(0f, 58f),
                new Vector2(-68f, 14f),
                new Vector2(68f, 14f),
                new Vector2(-42f, -50f),
                new Vector2(42f, -50f)
            }
        };

        /// <summary>
        /// Position for fish <paramref name="index"/> of <paramref name="count"/>.
        /// <paramref name="size"/> is always <see cref="FishSize"/>; fish size never depends on occupancy.
        /// </summary>
        public static bool TryGetPlacement(int index, int count, out Vector2 anchoredPosition, out float size)
        {
            size = FishSize;
            if (count < 1 || count > MaxLayoutCount || index < 0 || index >= count)
            {
                anchoredPosition = Vector2.zero;
                return false;
            }

            anchoredPosition = Formations[count][index];
            return true;
        }

        public static bool TryGetPosition(int index, int count, out Vector2 anchoredPosition)
        {
            return TryGetPlacement(index, count, out anchoredPosition, out _);
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

        private static void Place(RectTransform rect, Vector2 anchoredPosition)
        {
            if (rect == null)
            {
                return;
            }

            Prepare(rect);
            rect.anchoredPosition = anchoredPosition;
        }
    }
}
