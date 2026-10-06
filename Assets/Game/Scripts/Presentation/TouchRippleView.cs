using UnityEngine;
using UnityEngine.UI;

namespace FishPuzzle.Presentation
{
    /// <summary>
    /// One expanding water ring. Missing art still plays from a procedural sprite.
    /// </summary>
    public sealed class TouchRippleView : MonoBehaviour
    {
        private RectTransform _rect;
        private Image _image;
        private float _duration = 0.35f;
        private float _elapsed;
        private Vector2 _origin;

        public bool IsActive { get; private set; }

        public static TouchRippleView Create(RectTransform parent)
        {
            var host = new GameObject("TouchRipple", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(TouchRippleView));
            var rect = host.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(64f, 64f);
            var image = host.GetComponent<Image>();
            image.raycastTarget = false;
            image.color = new Color(1f, 1f, 1f, 0f);
            var view = host.GetComponent<TouchRippleView>();
            view._rect = rect;
            view._image = image;
            view.IsActive = false;
            host.SetActive(false);
            return view;
        }

        public void Play(Vector2 anchoredPosition, Sprite sprite, float duration)
        {
            _origin = anchoredPosition;
            _duration = duration > 0f ? duration : 0.35f;
            _elapsed = 0f;
            IsActive = true;
            if (_rect == null)
            {
                _rect = transform as RectTransform;
            }

            if (_image == null)
            {
                _image = GetComponent<Image>();
            }

            if (_image != null)
            {
                _image.sprite = sprite != null ? sprite : ProceduralVfxSprite.Ring;
                _image.raycastTarget = false;
            }

            gameObject.SetActive(true);
            Apply(0f);
        }

        public void Stop()
        {
            IsActive = false;
            if (_image != null)
            {
                var color = _image.color;
                color.a = 0f;
                _image.color = color;
            }

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
                _rect.anchoredPosition = _origin;
                _rect.localScale = Vector3.one * Mathf.Lerp(0.22f, 1.7f, eased);
            }

            if (_image != null)
            {
                var color = _image.color;
                color.a = 0.42f * (1f - eased);
                _image.color = color;
            }
        }
    }
}
