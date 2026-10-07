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
        private static Sprite _droplet;

        public static Sprite Ring => _ring != null ? _ring : (_ring = Create(false));

        public static Sprite Dot => _dot != null ? _dot : (_dot = Create(true));

        public static Sprite Droplet => _droplet != null ? _droplet : (_droplet = CreateDroplet());

        /// <summary>Four-point star with a soft core, for light magical glints.</summary>
        public static Sprite Sparkle => _sparkle != null ? _sparkle : (_sparkle = CreateSparkle());

        /// <summary>Radial soft glow that fades to nothing at the edge.</summary>
        public static Sprite Glow => _glow != null ? _glow : (_glow = CreateGlow());

        private static Sprite _sparkle;
        private static Sprite _glow;

        private static Sprite CreateSparkle()
        {
            const int size = 64;
            var texture = NewTexture(size, size);
            var pixels = new Color[size * size];
            var center = (size - 1) * 0.5f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = Mathf.Abs((x - center) / center);
                    var dy = Mathf.Abs((y - center) / center);
                    var distance = Mathf.Sqrt((dx * dx) + (dy * dy));
                    var horizontal = Mathf.Clamp01(1f - (dy / Mathf.Lerp(0.16f, 0.02f, dx))) * Mathf.Clamp01(1f - dx);
                    var vertical = Mathf.Clamp01(1f - (dx / Mathf.Lerp(0.16f, 0.02f, dy))) * Mathf.Clamp01(1f - dy);
                    var core = Mathf.Clamp01(1f - (distance / 0.32f));
                    var alpha = Mathf.Clamp01(Mathf.Max(horizontal, vertical) + (core * core));
                    pixels[(y * size) + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            return Finish(texture, pixels, size, size);
        }

        private static Sprite CreateGlow()
        {
            const int size = 64;
            var texture = NewTexture(size, size);
            var pixels = new Color[size * size];
            var center = (size - 1) * 0.5f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = (x - center) / center;
                    var dy = (y - center) / center;
                    var falloff = Mathf.Clamp01(1f - Mathf.Sqrt((dx * dx) + (dy * dy)));
                    pixels[(y * size) + x] = new Color(1f, 1f, 1f, falloff * falloff);
                }
            }

            return Finish(texture, pixels, size, size);
        }

        private static Texture2D NewTexture(int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.wrapMode = TextureWrapMode.Clamp;
            return texture;
        }

        private static Sprite Finish(Texture2D texture, Color[] pixels, int width, int height)
        {
            texture.SetPixels(pixels);
            texture.Apply(false, false);
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), width);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

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

        private static Sprite CreateDroplet()
        {
            const int width = 48;
            const int height = 72;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color[width * height];
            var center = (width - 1) * 0.5f;
            for (var y = 0; y < height; y++)
            {
                var ny = y / (float)(height - 1);
                const float bodyCenter = 0.34f;
                const float bodyRadius = 0.34f;
                var dy = (ny - bodyCenter) / bodyRadius;
                var half = 0f;
                if ((dy * dy) <= 1f)
                {
                    half = 0.48f * Mathf.Sqrt(1f - (dy * dy));
                }

                if (ny > bodyCenter)
                {
                    var taper = Mathf.Lerp(0.48f, 0f, Mathf.InverseLerp(bodyCenter, 1f, ny));
                    half = (dy * dy) <= 1f ? Mathf.Min(half, taper) : taper;
                }
                for (var x = 0; x < width; x++)
                {
                    var nx = (x - center) / center;
                    var edge = half <= 0.001f ? 1f : Mathf.Abs(nx) / half;
                    var alpha = Mathf.Clamp01(1f - Mathf.InverseLerp(0.72f, 1f, edge));
                    var highlight = Mathf.Clamp01(1f - (Mathf.Abs(nx + 0.12f) * 3.2f) - (Mathf.Abs(ny - 0.34f) * 4f));
                    var color = Color.Lerp(new Color(0.35f, 0.82f, 1f, 1f), Color.white, highlight * 0.85f);
                    color.a = alpha * Mathf.Lerp(0.92f, 0.55f, ny);
                    pixels[(y * width) + x] = color;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, false);
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.42f), width);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }
    }
}
