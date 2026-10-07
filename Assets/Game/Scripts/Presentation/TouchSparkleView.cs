using UnityEngine;
using UnityEngine.UI;

namespace FishPuzzle.Presentation
{
    /// <summary>
    /// One twinkling star from a touch. It flies out a little, spins, pulses, and fades. Never blocks raycasts.
    /// </summary>
    public sealed class TouchSparkleView : MonoBehaviour
    {
        private static readonly Color[] Tints =
        {
            new Color(1f, 0.97f, 0.78f, 1f),
            new Color(0.82f, 0.96f, 1f, 1f),
            new Color(1f, 0.88f, 0.98f, 1f),
            Color.white
        };

        private RectTransform _rect;
        private Image _image;
        private Vector2 _start;
        private Vector2 _drift;
        private Color _tint = Color.white;
        private float _duration = 0.6f;
        private float _elapsed;
        private float _size = 26f;
        private float _spin;
        private float _twinkle;

        public bool IsActive { get; private set; }

        public static TouchSparkleView Create(RectTransform parent)
        {
            var host = new GameObject("TouchSparkle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(TouchSparkleView));
            var rect = host.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            var image = host.GetComponent<Image>();
            image.raycastTarget = false;
            var view = host.GetComponent<TouchSparkleView>();
            view._rect = rect;
            view._image = image;
            host.SetActive(false);
            return view;
        }

        /// <summary>
        /// <paramref name="direction"/> is the outward travel (anchored units); random01 values vary size, tint and spin.
        /// </summary>
        public void Play(Vector2 anchoredPosition, Vector2 direction, float lifetime, float randomA, float randomB)
        {
            _start = anchoredPosition;
            _drift = direction;
            _duration = lifetime > 0f ? lifetime : 0.6f;
            _size = Mathf.Lerp(18f, 38f, randomA * randomA);
            _spin = (randomB - 0.5f) * 360f;
            _twinkle = randomB * Mathf.PI * 2f;
            _tint = Tints[Mathf.Clamp((int)(randomA * Tints.Length), 0, Tints.Length - 1)];
            _elapsed = 0f;
            IsActive = true;
            if (_image != null)
            {
                _image.sprite = ProceduralVfxSprite.Sparkle;
                _image.raycastTarget = false;
            }

            if (_rect != null)
            {
                _rect.sizeDelta = new Vector2(_size, _size);
                _rect.SetAsLastSibling();
            }

            gameObject.SetActive(true);
            Apply(0f);
        }

        public void Stop()
        {
            IsActive = false;
            gameObject.SetActive(false);
        }

        public void Tick(float delta)
        {
            if (!IsActive)
            {
                return;
            }

            _elapsed += Mathf.Max(0f, delta);
            var t = _duration <= 0f ? 1f : Mathf.Clamp01(_elapsed / _duration);
            Apply(t);
            if (t >= 1f)
            {
                Stop();
            }
        }

        private void Apply(float t)
        {
            var eased = PresentationMotion.EaseOutQuad(t);
            if (_rect != null)
            {
                // Pop in fast, twinkle while drifting, shrink away at the end.
                var grow = Mathf.Clamp01(t / 0.15f);
                var twinkle = 0.8f + (0.25f * Mathf.Sin((t * 22f) + _twinkle));
                var scale = grow * twinkle * Mathf.Lerp(1f, 0.35f, t * t);
                _rect.anchoredPosition = _start + (_drift * eased) + new Vector2(0f, 14f * t);
                _rect.localScale = new Vector3(scale, scale, 1f);
                _rect.localRotation = Quaternion.Euler(0f, 0f, _spin * t);
            }

            if (_image != null)
            {
                var color = _tint;
                color.a = t < 0.6f ? 1f : 1f - ((t - 0.6f) / 0.4f);
                _image.color = color;
            }
        }
    }
}
