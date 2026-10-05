using UnityEngine;

namespace FishPuzzle.Presentation
{
    /// <summary>
    /// Fits a RectTransform to Screen.safeArea. Presentation only.
    /// In the Editor the safe area is usually the full screen, which resolves to a full stretch.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        private RectTransform _rect;
        private Rect _appliedSafeArea;
        private int _appliedScreenWidth;
        private int _appliedScreenHeight;

        private void Awake()
        {
            _rect = GetComponent<RectTransform>();
            Apply();
        }

        private void Update()
        {
            if (_appliedSafeArea == Screen.safeArea
                && _appliedScreenWidth == Screen.width
                && _appliedScreenHeight == Screen.height)
            {
                return;
            }

            Apply();
        }

        public void Apply()
        {
            if (_rect == null)
            {
                _rect = GetComponent<RectTransform>();
            }

            var width = Screen.width;
            var height = Screen.height;
            if (width <= 0 || height <= 0)
            {
                return;
            }

            var safe = Screen.safeArea;
            var min = safe.position;
            var max = safe.position + safe.size;
            min.x /= width;
            min.y /= height;
            max.x /= width;
            max.y /= height;
            _rect.anchorMin = min;
            _rect.anchorMax = max;
            _rect.offsetMin = Vector2.zero;
            _rect.offsetMax = Vector2.zero;
            _appliedSafeArea = safe;
            _appliedScreenWidth = width;
            _appliedScreenHeight = height;
        }
    }
}
