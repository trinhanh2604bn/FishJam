using UnityEngine;
using UnityEngine.UI;

namespace FishPuzzle.Presentation
{
    /// <summary>
    /// Short water splash where a fish enters a tank. It does not decide the landing.
    /// Hosts are reused. Missing sprites create nothing.
    /// </summary>
    public sealed class TankSplashView : MonoBehaviour
    {
        public const int MinDroplets = 4;
        public const int MaxDroplets = 10;
        public const int DefaultPoolCap = 4;
        public const int HardPoolCap = 8;

        private const float Duration = 0.62f;
        private const float DropletGravity = 520f;
        private const float SecondRingDelay = 0.18f;

        private static readonly Color GlowTint = new Color(0.62f, 0.92f, 1f, 1f);

        private RectTransform _burst;
        private Image _burstImage;
        private RectTransform _ring;
        private Image _ringImage;
        private RectTransform _ring2;
        private Image _ring2Image;
        private RectTransform _glow;
        private Image _glowImage;
        private readonly RectTransform[] _drops = new RectTransform[MaxDroplets];
        private readonly Image[] _dropImages = new Image[MaxDroplets];
        private readonly float[] _dropSpread = new float[MaxDroplets];
        private readonly float[] _dropSize = new float[MaxDroplets];
        private readonly float[] _dropHeight = new float[MaxDroplets];
        private int _dropletCount;
        private float _elapsed;
        private bool _playing;
        private bool _built;

        public bool IsPlaying => _playing;

        public int DropletCount => _dropletCount;

        public float Elapsed => _elapsed;

        public static TankSplashView Play(
            RectTransform parent,
            Vector3 worldPosition,
            Sprite splash,
            Sprite droplet,
            Sprite ring,
            bool allowProcedural,
            int dropletCount = 6,
            int poolCap = DefaultPoolCap)
        {
            if (parent == null)
            {
                return null;
            }

            var splashSprite = splash;
            var dropSprite = droplet;
            var ringSprite = ring;
            if (splashSprite == null && allowProcedural)
            {
                splashSprite = ProceduralVfxSprite.Ring;
            }

            if (dropSprite == null && allowProcedural)
            {
                dropSprite = ProceduralVfxSprite.Dot;
            }

            if (ringSprite == null && allowProcedural)
            {
                ringSprite = ProceduralVfxSprite.Ring;
            }

            if (splashSprite == null && dropSprite == null && ringSprite == null)
            {
                return null;
            }

            var view = Rent(parent, Mathf.Clamp(poolCap, 1, HardPoolCap));
            if (view == null)
            {
                return null;
            }

            view.Begin(
                worldPosition,
                splashSprite,
                dropSprite,
                ringSprite,
                Mathf.Clamp(dropletCount, MinDroplets, MaxDroplets));
            return view;
        }

        public void Tick(float delta)
        {
            if (!_playing)
            {
                return;
            }

            var remaining = Mathf.Clamp(delta, 0f, 1f);
            while (_playing && remaining > 0f)
            {
                var step = Mathf.Min(0.05f, remaining);
                remaining -= step;
                _elapsed += step;
                var t = Mathf.Clamp01(_elapsed / Duration);
                Apply(t);
                if (t < 1f)
                {
                    continue;
                }

                _playing = false;
                gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            Tick(Mathf.Min(Time.unscaledDeltaTime, 0.05f));
        }

        private static TankSplashView Rent(RectTransform parent, int poolCap)
        {
            var existing = parent.GetComponentsInChildren<TankSplashView>(true);
            TankSplashView free = null;
            TankSplashView oldest = null;
            var active = 0;
            for (var i = 0; i < existing.Length; i++)
            {
                var view = existing[i];
                if (view == null)
                {
                    continue;
                }

                if (!view._playing)
                {
                    if (free == null)
                    {
                        free = view;
                    }

                    continue;
                }

                active++;
                if (oldest == null || view._elapsed >= oldest._elapsed)
                {
                    oldest = view;
                }
            }

            if (free != null)
            {
                free.gameObject.SetActive(true);
                return free;
            }

            if (active >= poolCap && oldest != null)
            {
                return oldest;
            }

            var host = new GameObject("TankSplash", typeof(RectTransform), typeof(TankSplashView));
            var rect = host.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(120f, 120f);
            return host.GetComponent<TankSplashView>();
        }

        private void Begin(Vector3 worldPosition, Sprite splash, Sprite droplet, Sprite ring, int dropletCount)
        {
            var rect = transform as RectTransform;
            if (rect != null)
            {
                rect.position = worldPosition;
                rect.localScale = Vector3.one;
                rect.SetAsLastSibling();
            }

            if (!_built)
            {
                Build(splash, droplet, ring);
            }
            else
            {
                Assign(_burstImage, splash);
                Assign(_ringImage, ring);
                Assign(_ring2Image, ring);
                for (var i = 0; i < MaxDroplets; i++)
                {
                    Assign(_dropImages[i], droplet);
                }
            }

            _dropletCount = dropletCount;
            _elapsed = 0f;
            _playing = true;
            for (var i = 0; i < MaxDroplets; i++)
            {
                var visible = i < _dropletCount && _drops[i] != null;
                if (_drops[i] != null)
                {
                    _drops[i].gameObject.SetActive(visible);
                    var angle = _dropletCount <= 1
                        ? 0f
                        : Mathf.Lerp(-130f, 130f, i / (float)(_dropletCount - 1));
                    var rad = angle * Mathf.Deg2Rad;
                    var speed = 160f + ((i % 3) * 55f) + ((i % 2) * 20f);
                    _dropSpread[i] = Mathf.Sin(rad) * speed;
                    _dropHeight[i] = Mathf.Cos(rad) * speed;
                    _dropSize[i] = 14f + ((i % 4) * 5f);
                    _drops[i].sizeDelta = new Vector2(_dropSize[i] * 0.62f, _dropSize[i] * 1.45f);
                    _drops[i].localRotation = Quaternion.identity;
                }
            }

            if (_burst != null)
            {
                _burst.gameObject.SetActive(splash != null);
            }

            if (_ring != null)
            {
                _ring.gameObject.SetActive(ring != null);
            }

            if (_ring2 != null)
            {
                _ring2.gameObject.SetActive(ring != null);
            }

            Apply(0f);
        }

        private void Build(Sprite splash, Sprite droplet, Sprite ring)
        {
            var rect = transform as RectTransform;
            if (rect == null)
            {
                return;
            }

            _glowImage = CreateImage(rect, "SplashGlow", ProceduralVfxSprite.Glow, new Vector2(120f, 120f), out _glow);
            _glowImage.color = GlowTint;

            if (ring != null)
            {
                _ringImage = CreateImage(rect, "SplashRing", ring, new Vector2(72f, 72f), out _ring);
                _ring2Image = CreateImage(rect, "SplashRing2", ring, new Vector2(56f, 56f), out _ring2);
            }

            if (splash != null)
            {
                _burstImage = CreateImage(rect, "SplashBurst", splash, new Vector2(88f, 88f), out _burst);
            }

            if (droplet != null)
            {
                for (var i = 0; i < MaxDroplets; i++)
                {
                    _dropImages[i] = CreateImage(rect, "SplashDrop", droplet, new Vector2(16f, 16f), out _drops[i]);
                }
            }

            _built = true;
        }

        private void Apply(float t)
        {
            var eased = PresentationMotion.EaseOutQuad(t);
            if (_burst != null)
            {
                _burst.localScale = Vector3.one * Mathf.Lerp(0.5f, 1.7f, eased);
                _burst.anchoredPosition = new Vector2(0f, Mathf.Lerp(0f, 28f, eased));
            }

            SetAlpha(_burstImage, 0.82f * (1f - eased));
            if (_ring != null)
            {
                _ring.localScale = Vector3.one * Mathf.Lerp(0.35f, 1.95f, eased);
            }

            SetAlpha(_ringImage, 0.42f * (1f - eased));
            var late = Mathf.Clamp01(((t * Duration) - SecondRingDelay) / (Duration - SecondRingDelay));
            if (_ring2 != null)
            {
                _ring2.localScale = Vector3.one * Mathf.Lerp(0.3f, 1.6f, PresentationMotion.EaseOutQuad(late));
            }

            SetAlpha(_ring2Image, late <= 0f ? 0f : 0.32f * (1f - late));
            if (_glow != null)
            {
                _glow.localScale = Vector3.one * Mathf.Lerp(0.5f, 1.3f, eased);
            }

            var flash = Mathf.Clamp01(t / 0.5f);
            SetAlpha(_glowImage, 0.3f * (1f - (flash * flash)));
            var time = t * Duration;
            for (var i = 0; i < _dropletCount; i++)
            {
                if (_drops[i] == null)
                {
                    continue;
                }

                var x = _dropSpread[i] * time;
                var y = (_dropHeight[i] * time) - (0.5f * DropletGravity * time * time);
                _drops[i].anchoredPosition = new Vector2(x, y);
                var fall = _dropHeight[i] - (DropletGravity * time);
                _drops[i].localRotation = Quaternion.Euler(0f, 0f, -Mathf.Atan2(_dropSpread[i], fall) * Mathf.Rad2Deg);
                _drops[i].localScale = Vector3.one * Mathf.Lerp(1f, 0.72f, t);
                SetAlpha(_dropImages[i], 0.9f * (1f - (t * t)));
            }
        }

        private static void Assign(Image image, Sprite sprite)
        {
            if (image == null || sprite == null)
            {
                return;
            }

            image.sprite = sprite;
        }

        private static void SetAlpha(Image image, float alpha)
        {
            if (image == null)
            {
                return;
            }

            var color = image.color;
            color.a = alpha;
            image.color = color;
        }

        private static Image CreateImage(RectTransform parent, string name, Sprite sprite, Vector2 size, out RectTransform rect)
        {
            var host = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            rect = host.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            var image = host.GetComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            image.color = Color.white;
            return image;
        }
    }
}
