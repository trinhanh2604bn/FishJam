using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FishPuzzle.Presentation
{
    /// <summary>
    /// "GOOD! / GREAT! / ..." pop-up shown when a tank group completes.
    /// Lives above the HUD and below modals. Labels and sparkles are pooled and never block raycasts.
    /// </summary>
    public sealed class ComboFeedbackView : MonoBehaviour
    {
        public const int LabelPool = 3;
        public const int SparklePool = 24;
        public const float Duration = 0.85f;

        private const float BaseFontSize = 92f;
        private const float Rise = 46f;

        private readonly Label[] _labels = new Label[LabelPool];
        private readonly Sparkle[] _sparkles = new Sparkle[SparklePool];
        private RectTransform _layer;
        private Sprite _sparkleSprite;
        private int _nextLabel;
        private int _shownCount;
        private string _lastText = string.Empty;
        private ComboTier _lastTier;

        public int ShownCount => _shownCount;

        public string LastText => _lastText;

        public ComboTier LastTier => _lastTier;

        public int ActiveLabelCount
        {
            get
            {
                var count = 0;
                for (var i = 0; i < _labels.Length; i++)
                {
                    if (_labels[i] != null && _labels[i].Active)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public int ActiveSparkleCount
        {
            get
            {
                var count = 0;
                for (var i = 0; i < _sparkles.Length; i++)
                {
                    if (_sparkles[i] != null && _sparkles[i].Active)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        /// <summary>
        /// Creates the layer under <paramref name="canvas"/>, just below <paramref name="modalRoot"/> when given,
        /// so modals stay on top and the HUD stays underneath.
        /// </summary>
        public static ComboFeedbackView Create(Transform canvas, Transform modalRoot, TMP_FontAsset font, Sprite sparkle)
        {
            if (canvas == null)
            {
                return null;
            }

            var host = new GameObject("ComboFeedbackLayer", typeof(RectTransform), typeof(CanvasGroup));
            var rect = host.GetComponent<RectTransform>();
            rect.SetParent(canvas, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            var group = host.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            if (modalRoot != null && modalRoot.parent == canvas)
            {
                rect.SetSiblingIndex(modalRoot.GetSiblingIndex());
            }
            else
            {
                rect.SetAsLastSibling();
            }

            var view = host.AddComponent<ComboFeedbackView>();
            view._layer = rect;
            view._sparkleSprite = sparkle != null ? sparkle : ProceduralVfxSprite.Dot;
            for (var i = 0; i < LabelPool; i++)
            {
                view._labels[i] = Label.Create(rect, font);
            }

            for (var i = 0; i < SparklePool; i++)
            {
                view._sparkles[i] = Sparkle.Create(rect);
            }

            return view;
        }

        /// <summary>Shows one tier at a world position. Returns false when nothing could be shown.</summary>
        public bool Show(ComboTier tier, Vector3 worldPosition)
        {
            if (tier == ComboTier.None || _layer == null)
            {
                return false;
            }

            var local = _layer.InverseTransformPoint(worldPosition);
            var anchored = new Vector2(local.x, local.y);
            var label = _labels[_nextLabel];
            _nextLabel = (_nextLabel + 1) % _labels.Length;
            for (var i = 0; i < _labels.Length; i++)
            {
                // A newer pop replaces older ones quickly so text never stacks into a pile.
                if (_labels[i] != null && _labels[i] != label && _labels[i].Active)
                {
                    _labels[i].FadeQuickly();
                }
            }

            if (label != null)
            {
                label.Play(tier, anchored);
            }

            EmitSparkles(tier, anchored);
            _layer.SetAsLastSiblingBelowModals();
            _shownCount++;
            _lastTier = tier;
            _lastText = ComboStreakTracker.Label(tier);
            return true;
        }

        public void Clear()
        {
            for (var i = 0; i < _labels.Length; i++)
            {
                if (_labels[i] != null)
                {
                    _labels[i].Stop();
                }
            }

            for (var i = 0; i < _sparkles.Length; i++)
            {
                if (_sparkles[i] != null)
                {
                    _sparkles[i].Stop();
                }
            }
        }

        public void Tick(float delta)
        {
            var step = Mathf.Clamp(delta, 0f, 0.1f);
            for (var i = 0; i < _labels.Length; i++)
            {
                if (_labels[i] != null)
                {
                    _labels[i].Advance(step);
                }
            }

            for (var i = 0; i < _sparkles.Length; i++)
            {
                if (_sparkles[i] != null)
                {
                    _sparkles[i].Advance(step);
                }
            }
        }

        private void Update()
        {
            Tick(Time.unscaledDeltaTime);
        }

        private void EmitSparkles(ComboTier tier, Vector2 center)
        {
            var count = Mathf.Min(ComboStreakTracker.BurstCount(tier), SparklePool);
            var strength = ComboStreakTracker.Scale(tier);
            var color = ComboStreakTracker.Face(tier);
            var emitted = 0;
            for (var i = 0; i < _sparkles.Length && emitted < count; i++)
            {
                if (_sparkles[i] == null || _sparkles[i].Active)
                {
                    continue;
                }

                var angle = ((emitted / (float)count) * 360f) + (tier == ComboTier.None ? 0f : (int)tier * 11f);
                _sparkles[i].Play(_sparkleSprite, center, angle, strength, color, emitted);
                emitted++;
            }
        }

        private sealed class Label
        {
            private RectTransform _rect;
            private TextMeshProUGUI _text;
            private Vector2 _origin;
            private float _elapsed;
            private float _life;
            private float _scale;
            private bool _active;

            public bool Active => _active;

            public static Label Create(RectTransform parent, TMP_FontAsset font)
            {
                var host = new GameObject("ComboLabel", typeof(RectTransform));
                host.SetActive(false);
                var rect = host.GetComponent<RectTransform>();
                rect.SetParent(parent, false);
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(900f, 150f);
                var text = host.AddComponent<TextMeshProUGUI>();
                if (font != null)
                {
                    text.font = font;
                }

                text.alignment = TextAlignmentOptions.Center;
                text.textWrappingMode = TextWrappingModes.NoWrap;
                text.raycastTarget = false;
                ResultUiStyle.Chunky(text, BaseFontSize, Color.white, ComboStreakTracker.Outline(ComboTier.Good), 0.24f);
                return new Label { _rect = rect, _text = text };
            }

            public void Play(ComboTier tier, Vector2 anchored)
            {
                if (_rect == null || _text == null)
                {
                    return;
                }

                _origin = anchored;
                _elapsed = 0f;
                _life = Duration + (0.04f * ((int)tier - 1));
                _scale = ComboStreakTracker.Scale(tier);
                _active = true;
                _text.text = ComboStreakTracker.Label(tier);
                var face = ComboStreakTracker.Face(tier);
                var top = Color.Lerp(face, Color.white, 0.55f);
                ResultUiStyle.VerticalGradient(_text, top, face);
                if (Application.isPlaying && _text.font != null)
                {
                    // Same per-label material instance created at build time; only a property changes here.
                    _text.outlineColor = ComboStreakTracker.Outline(tier);
                }

                _text.alpha = 1f;
                _rect.anchoredPosition = anchored;
                _rect.localScale = Vector3.one * (0.7f * _scale);
                _rect.SetAsLastSibling();
                _rect.gameObject.SetActive(true);
            }

            public void FadeQuickly()
            {
                if (!_active)
                {
                    return;
                }

                _elapsed = Mathf.Max(_elapsed, _life * 0.8f);
            }

            public void Advance(float step)
            {
                if (!_active || _rect == null)
                {
                    return;
                }

                _elapsed += step;
                var t = _life <= 0f ? 1f : Mathf.Clamp01(_elapsed / _life);
                _rect.localScale = Vector3.one * (_scale * PopScale(t));
                _rect.anchoredPosition = _origin + new Vector2(0f, Rise * PresentationMotion.EaseOutQuad(t));
                if (_text != null)
                {
                    _text.alpha = t < 0.6f ? 1f : 1f - Mathf.InverseLerp(0.6f, 1f, t);
                }

                if (t >= 1f)
                {
                    Stop();
                }
            }

            public void Stop()
            {
                _active = false;
                if (_rect != null)
                {
                    _rect.gameObject.SetActive(false);
                }
            }

            /// <summary>0.7 → 1.15 → 1.0 in the first ~30% of the life, then holds at 1.0.</summary>
            public static float PopScale(float t)
            {
                if (t < 0.16f)
                {
                    return Mathf.Lerp(0.7f, 1.15f, PresentationMotion.EaseOutQuad(t / 0.16f));
                }

                if (t < 0.30f)
                {
                    return Mathf.Lerp(1.15f, 1f, (t - 0.16f) / 0.14f);
                }

                return 1f;
            }
        }

        private sealed class Sparkle
        {
            private RectTransform _rect;
            private Image _image;
            private Vector2 _origin;
            private Vector2 _travel;
            private float _elapsed;
            private float _life;
            private float _size;
            private bool _active;

            public bool Active => _active;

            public static Sparkle Create(RectTransform parent)
            {
                var host = new GameObject("ComboSparkle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                host.SetActive(false);
                var rect = host.GetComponent<RectTransform>();
                rect.SetParent(parent, false);
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                var image = host.GetComponent<Image>();
                image.raycastTarget = false;
                return new Sparkle { _rect = rect, _image = image };
            }

            public void Play(Sprite sprite, Vector2 center, float angleDegrees, float strength, Color tint, int index)
            {
                if (_rect == null || _image == null || sprite == null)
                {
                    return;
                }

                var rad = angleDegrees * Mathf.Deg2Rad;
                var reach = (150f + ((index % 3) * 40f)) * strength;
                _origin = center + new Vector2(Mathf.Cos(rad) * 60f, Mathf.Sin(rad) * 24f);
                _travel = new Vector2(Mathf.Cos(rad) * reach, Mathf.Sin(rad) * reach * 0.45f);
                _size = (26f + ((index % 4) * 8f)) * strength;
                _elapsed = 0f;
                _life = 0.55f + ((index % 3) * 0.08f);
                _active = true;
                _image.sprite = sprite;
                _image.color = Color.Lerp(tint, Color.white, 0.4f);
                _rect.sizeDelta = new Vector2(_size, _size);
                _rect.anchoredPosition = _origin;
                _rect.localScale = Vector3.one;
                _rect.gameObject.SetActive(true);
            }

            public void Advance(float step)
            {
                if (!_active || _rect == null)
                {
                    return;
                }

                _elapsed += step;
                var t = _life <= 0f ? 1f : Mathf.Clamp01(_elapsed / _life);
                var eased = PresentationMotion.EaseOutQuad(t);
                _rect.anchoredPosition = _origin + (_travel * eased);
                _rect.localScale = Vector3.one * Mathf.Lerp(1f, 0.4f, t);
                _rect.localRotation = Quaternion.Euler(0f, 0f, 120f * t);
                var color = _image.color;
                color.a = 1f - (t * t);
                _image.color = color;
                if (t >= 1f)
                {
                    Stop();
                }
            }

            public void Stop()
            {
                _active = false;
                if (_rect != null)
                {
                    _rect.gameObject.SetActive(false);
                }
            }
        }
    }

    internal static class ComboLayerOrdering
    {
        /// <summary>Keeps the combo layer directly below the modal root (if present) after other layers were appended.</summary>
        public static void SetAsLastSiblingBelowModals(this RectTransform layer)
        {
            if (layer == null || layer.parent == null)
            {
                return;
            }

            var parent = layer.parent;
            var overlay = parent.Find("OverlayRoot");
            if (overlay == null)
            {
                layer.SetAsLastSibling();
                return;
            }

            layer.SetAsLastSibling();
            overlay.SetAsLastSibling();
        }
    }
}
