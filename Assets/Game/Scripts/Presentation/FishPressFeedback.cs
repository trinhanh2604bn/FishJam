using UnityEngine;

namespace FishPuzzle.Presentation
{
    /// <summary>
    /// Cute hold pose for one fish. It only moves the fish graphic.
    /// </summary>
    public sealed class FishPressFeedback : MonoBehaviour
    {
        private const float WiggleCycle = 0.42f;

        private RectTransform _visual;
        private Vector2 _origin;
        private Vector3 _originScale;
        private Quaternion _originRotation;
        private float _lift = 8f;
        private float _scale = 1.05f;
        private float _wiggle = 2f;
        private float _bob = 1.5f;
        private float _time;
        private float _angle;

        public bool IsPressed { get; private set; }

        public float Lift => IsPressed && _visual != null ? _visual.anchoredPosition.y - _origin.y : 0f;

        public float Scale => IsPressed && _visual != null ? _visual.localScale.x : 1f;

        public float Angle => IsPressed ? _angle : 0f;

        public void Begin(RectTransform visual, AnimationTuning tuning)
        {
            _visual = visual;
            _time = 0f;
            _angle = 0f;
            if (tuning != null)
            {
                _lift = Mathf.Clamp(tuning.FishPressLift, 6f, 10f);
                _scale = Mathf.Clamp(tuning.FishPressScale, 1.04f, 1.07f);
                _wiggle = Mathf.Clamp(tuning.FishPressWiggleDegrees, 0f, 2f);
                _bob = Mathf.Clamp(tuning.FishPressBob, 0f, 2.5f);
            }

            if (_visual != null)
            {
                _origin = _visual.anchoredPosition;
                _originScale = _visual.localScale;
                _originRotation = _visual.localRotation;
            }

            IsPressed = true;
            Apply();
        }

        public void Tick(float delta)
        {
            if (!IsPressed)
            {
                return;
            }

            _time += Mathf.Max(0f, delta);
            Apply();
        }

        public void End()
        {
            if (!IsPressed && _visual == null)
            {
                return;
            }

            IsPressed = false;
            _angle = 0f;
            if (_visual == null)
            {
                return;
            }

            _visual.anchoredPosition = _origin;
            _visual.localScale = _originScale;
            _visual.localRotation = _originRotation;
        }

        private void Update()
        {
            Tick(Time.unscaledDeltaTime);
        }

        private void Apply()
        {
            if (_visual == null)
            {
                return;
            }

            var wave = Mathf.Sin((_time / WiggleCycle) * Mathf.PI * 2f);
            _angle = _wiggle * wave;
            var squash = 1f + (0.018f * wave);
            var stretch = 1f - (0.012f * wave);
            _visual.anchoredPosition = _origin + new Vector2(0f, _lift + (_bob * wave));
            _visual.localScale = new Vector3(
                _originScale.x * _scale * squash,
                _originScale.y * _scale * stretch,
                1f);
            _visual.localRotation = Quaternion.Euler(0f, 0f, _angle);
        }
    }
}
