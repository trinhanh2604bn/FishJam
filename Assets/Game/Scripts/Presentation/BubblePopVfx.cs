using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FishPuzzle.Presentation
{
    /// <summary>
    /// Short bubble-pop fragments. Missing sprites produce no objects and never block the caller.
    /// </summary>
    public static class BubblePopVfx
    {
        private const int FragmentCount = 6;
        private const float FragmentTravel = 72f;

        public static bool CanPlay(Sprite pop, Sprite small)
        {
            return pop != null || small != null;
        }

        public static int Spawn(RectTransform parent, Vector2 anchoredPosition, Sprite pop, Sprite small, List<GameObject> into)
        {
            if (parent == null || into == null || !CanPlay(pop, small))
            {
                return 0;
            }

            var created = 0;
            for (var i = 0; i < FragmentCount; i++)
            {
                var fragment = new GameObject("BubblePopVfx", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                var rect = fragment.GetComponent<RectTransform>();
                rect.SetParent(parent, false);
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = anchoredPosition;
                rect.sizeDelta = new Vector2(36f, 36f);
                var image = fragment.GetComponent<Image>();
                var sprite = i % 2 == 0 ? pop : small;
                if (sprite == null)
                {
                    sprite = pop != null ? pop : small;
                }

                image.sprite = sprite;
                image.raycastTarget = false;
                into.Add(fragment);
                created++;
            }

            return created;
        }

        public static void Animate(List<GameObject> fragments, Vector2 origin, float t)
        {
            if (fragments == null || fragments.Count == 0)
            {
                return;
            }

            var clamped = Mathf.Clamp01(t);
            for (var i = 0; i < fragments.Count; i++)
            {
                var fragment = fragments[i];
                if (fragment == null)
                {
                    continue;
                }

                var rect = fragment.transform as RectTransform;
                var angle = (Mathf.PI * 2f * i) / fragments.Count;
                var distance = FragmentTravel * clamped;
                if (rect != null)
                {
                    rect.anchoredPosition = origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
                    rect.localScale = Vector3.one * Mathf.Lerp(0.7f, 1.15f, clamped);
                }

                var image = fragment.GetComponent<Image>();
                if (image != null)
                {
                    var color = image.color;
                    color.a = 1f - clamped;
                    image.color = color;
                }
            }
        }
    }
}
