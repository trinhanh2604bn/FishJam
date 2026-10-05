using UnityEngine;

namespace FishPuzzle.Presentation
{
    /// <summary>
    /// Tiny generated ring and dot used when a painted VFX sprite is missing.
    /// </summary>
    public static class ProceduralVfxSprite
    {
        private static Sprite _ring;
        private static Sprite _dot;

        public static Sprite Ring => _ring != null ? _ring : (_ring = Create(false));

        public static Sprite Dot => _dot != null ? _dot : (_dot = Create(true));

        private static Sprite Create(bool filled)
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color[size * size];
            var center = (size - 1) * 0.5f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = (x - center) / center;
                    var dy = (y - center) / center;
                    var distance = Mathf.Sqrt((dx * dx) + (dy * dy));
                    var alpha = filled
                        ? Mathf.Clamp01(1f - Mathf.InverseLerp(0.72f, 1f, distance))
                        : Mathf.Clamp01(1f - (Mathf.Abs(distance - 0.72f) / 0.16f));
                    pixels[(y * size) + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, false);
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }
    }
}
