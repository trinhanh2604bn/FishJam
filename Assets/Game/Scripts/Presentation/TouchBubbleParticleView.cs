using UnityEngine;
using UnityEngine.UI;

namespace FishPuzzle.Presentation
{
    /// <summary>
    /// One small translucent bubble that drifts up from a touch and fades.
    /// </summary>
    public sealed class TouchBubbleParticleView : MonoBehaviour
    {
        private RectTransform _rect;
        private Image _image;
        private Vector2 _start;
        private Vector2 _drift;
        private float _duration = 0.42f;
        private float _elapsed;
        private float _scale = 0.7f;
        private float _alpha = 0.34f;

        public bool IsActive { get; private set; }

        public static TouchBubbleParticleView Create(RectTransform parent)
        {
            var host = new GameObject("TouchBubble", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(TouchBubbleParticleView));
            var rect = host.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(28f, 28f);
            var image = host.GetComponent<Image>();
            image.raycastTarget = false;
            var view = host.GetComponent<TouchBubbleParticleView>();
            view._rect = rect;
            view._image = image;
            view.IsActive = false;
            host.SetActive(false);
            return view;
        }

        public void Play(Vector2 anchoredPosition, Sprite sprite, float lifetime, float seed)
        {
            _start = anchoredPosition + new Vector2(Mathf.Lerp(-18f, 18f, Repeat(seed)), Mathf.Lerp(-8f, 10f, Repeat(seed + 0.37f)));
            _drift = new Vector2(Mathf.Lerp(-8f, 8f, Repeat(seed + 0.11f)), Mathf.Lerp(22f, 40f, Repeat(seed + 0.53f)));
            _scale = Mathf.Lerp(0.5f, 0.82f, Repeat(seed + 0.71f));
            _alpha = Mathf.Lerp(0.22f, 0.38f, Repeat(seed + 0.19f));
            _duration = lifetime > 0f ? lifetime : 0.42f;
            _elapsed = 0f;
            IsActive = true;
            if (_image != null)
            {
                _image.sprite = sprite != null ? sprite : ProceduralVfxSprite.Dot;
                _image.raycastTarget = false;
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
                _rect.anchoredPosition = _start + (_drift * eased);
                _rect.localScale = Vector3.one * _scale * Mathf.Lerp(0.85f, 1.05f, eased);
            }

            if (_image != null)
            {
                var color = Color.white;
                color.a = _alpha * (1f - eased);
                _image.color = color;
            }
        }

        private static float Repeat(float value)
        {
            var wrapped = value - Mathf.Floor(value);
            return wrapped < 0f ? wrapped + 1f : wrapped;
        }
    }
}
