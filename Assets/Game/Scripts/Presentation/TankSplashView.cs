using UnityEngine;
using UnityEngine.UI;

namespace FishPuzzle.Presentation
{
    /// <summary>
    /// Short water splash where a fish enters a tank. It does not decide the landing.
    /// </summary>
    public sealed class TankSplashView : MonoBehaviour
    {
        private const int DropletCount = 4;
        private const float Duration = 0.34f;

        private RectTransform _burst;
        private Image _burstImage;
        private RectTransform _ring;
        private Image _ringImage;
        private readonly RectTransform[] _drops = new RectTransform[DropletCount];
        private readonly Image[] _dropImages = new Image[DropletCount];
        private readonly float[] _dropSpread = new float[DropletCount];
        private float _elapsed;
        private bool _playing;

        public static TankSplashView Play(
            RectTransform parent,
            Vector3 worldPosition,
            Sprite splash,
            Sprite droplet,
            Sprite ring,
            bool allowProcedural)
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

            var host = new GameObject("TankSplash", typeof(RectTransform), typeof(TankSplashView));
            var rect = host.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.position = worldPosition;
            rect.sizeDelta = new Vector2(120f, 120f);
            var view = host.GetComponent<TankSplashView>();
            if (ringSprite != null)
            {
                view._ringImage = CreateImage(rect, "SplashRing", ringSprite, new Vector2(72f, 72f), out view._ring);
            }

            if (splashSprite != null)
            {
                view._burstImage = CreateImage(rect, "SplashBurst", splashSprite, new Vector2(88f, 88f), out view._burst);
            }

            if (dropSprite != null)
            {
                for (var i = 0; i < DropletCount; i++)
                {
                    view._dropImages[i] = CreateImage(rect, "SplashDrop", dropSprite, new Vector2(18f, 18f), out view._drops[i]);
                    view._dropSpread[i] = -18f + (i * 12f);
                }
            }

            view._playing = true;
            view.Apply(0f);
            rect.SetAsLastSibling();
            return view;
        }

        private void Update()
        {
            if (!_playing)
            {
                return;
            }

            _elapsed += Time.unscaledDeltaTime;
            var t = Mathf.Clamp01(_elapsed / Duration);
            Apply(t);
            if (t < 1f)
            {
                return;
            }

            _playing = false;
            SceneObjectCleanup.DestroyObject(gameObject);
        }

        private void Apply(float t)
        {
            var eased = PresentationMotion.EaseOutQuad(t);
            if (_burst != null)
            {
                _burst.localScale = Vector3.one * Mathf.Lerp(0.35f, 1.12f, eased);
                _burst.anchoredPosition = new Vector2(0f, Mathf.Lerp(0f, 10f, eased));
            }

            SetAlpha(_burstImage, 0.75f * (1f - eased));
            if (_ring != null)
            {
                _ring.localScale = Vector3.one * Mathf.Lerp(0.3f, 1.45f, eased);
            }

            SetAlpha(_ringImage, 0.4f * (1f - eased));
            var rise = Mathf.Sin(t * Mathf.PI) * 36f;
            for (var i = 0; i < DropletCount; i++)
            {
                if (_drops[i] == null)
                {
                    continue;
                }

                _drops[i].anchoredPosition = new Vector2(_dropSpread[i] * eased, rise);
                _drops[i].localScale = Vector3.one * Mathf.Lerp(0.7f, 1f, 1f - t);
                SetAlpha(_dropImages[i], 0.55f * (1f - t));
            }
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
