using UnityEngine;

namespace FishPuzzle.Presentation
{
    /// <summary>
    /// Gentle swim sway for the fish graphic. The root stays put, so layout and taps stay stable.
    /// </summary>
    public sealed class FishIdleSway : MonoBehaviour
    {
        private RectTransform _visual;
        private Vector2 _origin;
        private Vector3 _originScale;
        private Quaternion _originRotation;
        private float _wiggle = 2f;
        private float _bob = 1.5f;
        private float _cycle = 0.48f;
        private float _phase;
        private float _time;
        private float _angle;
        private bool _captured;
        private bool _playing;

        public bool IsPlaying => _playing;

        public float Angle => _playing ? _angle : 0f;

        public void Play(RectTransform visual, AnimationTuning tuning)
        {
            if (visual == null || visual == transform)
            {
                return;
            }

            if (_visual != visual)
            {
                _captured = false;
            }

            _visual = visual;
            if (tuning != null)
            {
                _wiggle = Mathf.Clamp(tuning.FishIdleWiggleDegrees, 0f, tuning.FishPressWiggleDegrees > 0f ? tuning.FishPressWiggleDegrees : 2f);
                _bob = Mathf.Clamp(tuning.FishIdleBob, 0f, tuning.FishPressBob > 0f ? tuning.FishPressBob : 1.5f);
                _cycle = tuning.FishIdleCycle > 0.2f ? tuning.FishIdleCycle : 0.48f;
            }

            if (!_captured)
            {
                _origin = _visual.anchoredPosition;
                _originScale = _visual.localScale;
                _originRotation = _visual.localRotation;
                _phase = (Mathf.Abs(GetEntityId().GetHashCode()) % 13) * 0.41f;
                _captured = true;
            }

            _playing = true;
            Apply();
        }

        public void Pause()
        {
            _playing = false;
            _angle = 0f;
            Restore();
        }

        public void Tick(float delta)
        {
            if (!_playing || _visual == null)
            {
                return;
            }

            _time += Mathf.Max(0f, delta);
            Apply();
        }

        private void Update()
        {
            Tick(Mathf.Min(Time.unscaledDeltaTime, 0.05f));
        }

        private void Apply()
        {
            if (_visual == null)
            {
                return;
            }

            var wave = Mathf.Sin(((_time + _phase) / _cycle) * Mathf.PI * 2f);
            _angle = _wiggle * wave;
            var squash = 1f + (0.018f * wave);
            var stretch = 1f - (0.012f * wave);
            _visual.anchoredPosition = _origin + new Vector2(0f, _bob * wave);
            _visual.localScale = new Vector3(_originScale.x * squash, _originScale.y * stretch, 1f);
            _visual.localRotation = _originRotation * Quaternion.Euler(0f, 0f, _angle);
        }

        private void Restore()
        {
            if (_visual == null || !_captured)
            {
                return;
            }

            _visual.anchoredPosition = _origin;
            _visual.localScale = _originScale;
            _visual.localRotation = _originRotation;
        }
    }
}
