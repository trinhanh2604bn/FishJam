using UnityEngine;

namespace FishPuzzle.Presentation
{
    /// <summary>
    /// Lightweight entry animation for result screens: background fade plus a small scale pop.
    /// Uses unscaled time, never blocks input, and snaps to the final pose when disabled.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class ModalPopIn : MonoBehaviour
    {
        public const float Duration = 0.24f;
        private const float StartScale = 0.82f;

        private CanvasGroup _group;
        private RectTransform[] _targets = new RectTransform[0];
        private Vector3[] _finalScales = new Vector3[0];
        private float _elapsed = Duration;

        public bool IsPlaying => _elapsed < Duration;

        public static ModalPopIn Attach(GameObject root, params RectTransform[] targets)
        {
            var pop = root.GetComponent<ModalPopIn>();
            if (pop == null)
            {
                pop = root.AddComponent<ModalPopIn>();
            }

            pop._group = root.GetComponent<CanvasGroup>();
            pop._targets = targets ?? new RectTransform[0];
            pop._finalScales = new Vector3[pop._targets.Length];
            return pop;
        }

        /// <summary>Call after layout/fit so the current scales are the final ones.</summary>
        public void Play()
        {
            for (var i = 0; i < _targets.Length; i++)
            {
                _finalScales[i] = _targets[i] != null ? _targets[i].localScale : Vector3.one;
            }

            _elapsed = 0f;
            Apply(0f);
        }

        public void Complete()
        {
            _elapsed = Duration;
            Apply(1f);
        }

        private void Update()
        {
            if (_elapsed >= Duration)
            {
                return;
            }

            _elapsed += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            Apply(Mathf.Clamp01(_elapsed / Duration));
        }

        private void OnDisable()
        {
            if (_elapsed < Duration)
            {
                Complete();
            }
        }

        private void Apply(float t)
        {
            if (_group != null)
            {
                _group.alpha = Mathf.Clamp01(t * 1.6f);
                _group.interactable = true;
                _group.blocksRaycasts = true;
            }

            var eased = EaseOutBack(t);
            for (var i = 0; i < _targets.Length; i++)
            {
                if (_targets[i] != null)
                {
                    _targets[i].localScale = _finalScales[i] * Mathf.LerpUnclamped(StartScale, 1f, eased);
                }
            }
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            var x = t - 1f;
            return 1f + (c3 * x * x * x) + (c1 * x * x);
        }
    }
}
