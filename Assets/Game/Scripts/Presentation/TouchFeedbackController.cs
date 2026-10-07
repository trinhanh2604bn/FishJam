using System.Collections.Generic;
using UnityEngine;

namespace FishPuzzle.Presentation
{
    /// <summary>
    /// Screen-space water touch. It never changes level, tank, or tray state.
    /// </summary>
    public sealed class TouchFeedbackController : MonoBehaviour
    {
        private const int RipplePool = 4;
        public const int SparklePool = 60;
        public const int TapSparkleCount = 9;
        public const int HoldSparkleCount = 2;
        private const float SparkleLifetime = 0.62f;

        private readonly List<TouchRippleView> _ripples = new List<TouchRippleView>();
        private readonly List<TouchBubbleParticleView> _bubbles = new List<TouchBubbleParticleView>();
        private readonly List<TouchSparkleView> _sparkles = new List<TouchSparkleView>();
        private uint _sparkleSeed = 0x9E3779B9u;
        private int _sparkleEmitCount;

        private RectTransform _layer;
        private GameplayArtCatalog _art;
        private AnimationTuning _tuning;
        private Vector2 _holdLocal;
        private float _emitElapsed;
        private float _seed;
        private int _bubbleLimit = 6;
        private bool _deviceHolding;
        private bool _syntheticHold;
        private bool _suppressed;

        public int ActiveRippleCount
        {
            get { return CountActive(_ripples); }
        }

        public int ActiveBubbleCount
        {
            get { return CountActiveBubbles(); }
        }

        public int ActiveSparkleCount
        {
            get { return CountActiveSparkles(); }
        }

        /// <summary>Sparkles started this session (taps, holds and drags).</summary>
        public int SparkleEmitCount => _sparkleEmitCount;

        public bool IsHolding => !_suppressed && (_deviceHolding || _syntheticHold);

        /// <summary>Raised when a touch ripple starts. Presentation listeners only (for example the touch sound).</summary>
        public event System.Action Rippled;

        public static TouchFeedbackController Create(Transform parent, GameplayArtCatalog art, AnimationTuning tuning)
        {
            var host = new GameObject("TouchFeedback", typeof(RectTransform), typeof(TouchFeedbackController));
            var rect = host.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            var controller = host.GetComponent<TouchFeedbackController>();
            controller._layer = rect;
            controller.Configure(art, tuning);
            rect.SetAsLastSibling();
            return controller;
        }

        public void Configure(GameplayArtCatalog art, AnimationTuning tuning)
        {
            _art = art;
            _tuning = tuning;
            var limit = tuning != null ? tuning.TouchBubbleLimit : 6;
            _bubbleLimit = Mathf.Clamp(limit, 1, 12);
        }

        public void Resume()
        {
            _suppressed = false;
        }

        public void Suppress()
        {
            _suppressed = true;
            _deviceHolding = false;
            _syntheticHold = false;
            StopAll();
        }

        public void Clear()
        {
            _deviceHolding = false;
            _syntheticHold = false;
            StopAll();
        }

        public void PresentPointerDown(Vector2 screenPosition)
        {
            BeginAt(ScreenToLocal(screenPosition), true);
        }

        public void PresentAtLocal(Vector2 anchoredPosition)
        {
            BeginAt(anchoredPosition, true);
        }

        public void PresentPointerUp()
        {
            _syntheticHold = false;
        }

        public void Tick(float delta)
        {
            var step = Mathf.Max(0f, delta);
            if (IsHolding)
            {
                _emitElapsed += step;
                var interval = _tuning != null ? _tuning.TouchBubbleInterval : 0.11f;
                if (interval <= 0f)
                {
                    interval = 0.11f;
                }

                if (_emitElapsed >= interval)
                {
                    _emitElapsed = 0f;
                    EmitBubble(_holdLocal);
                    EmitSparkles(_holdLocal, HoldSparkleCount, 46f);
                }
            }

            for (var i = 0; i < _ripples.Count; i++)
            {
                if (_ripples[i] != null)
                {
                    _ripples[i].Tick(step);
                }
            }

            for (var i = 0; i < _bubbles.Count; i++)
            {
                if (_bubbles[i] != null)
                {
                    _bubbles[i].Tick(step);
                }
            }

            for (var i = 0; i < _sparkles.Count; i++)
            {
                if (_sparkles[i] != null)
                {
                    _sparkles[i].Tick(step);
                }
            }
        }

        private void Update()
        {
            PollDevice();
            Tick(Time.unscaledDeltaTime);
        }

        private void PollDevice()
        {
            var position = Vector2.zero;
            var began = false;
            var held = false;
            var ended = false;
            if (Input.touchCount > 0)
            {
                var touch = Input.GetTouch(0);
                position = touch.position;
                began = touch.phase == TouchPhase.Began;
                held = touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary;
                ended = touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled;
            }
            else
            {
                position = Input.mousePosition;
                began = Input.GetMouseButtonDown(0);
                held = Input.GetMouseButton(0);
                ended = Input.GetMouseButtonUp(0);
            }

            if (began)
            {
                _deviceHolding = true;
                BeginAt(ScreenToLocal(position), false);
                return;
            }

            if (held)
            {
                _deviceHolding = true;
                _holdLocal = ScreenToLocal(position);
                return;
            }

            if (ended)
            {
                _deviceHolding = false;
            }
        }

        private void BeginAt(Vector2 local, bool synthetic)
        {
            if (_suppressed || _layer == null)
            {
                return;
            }

            if (synthetic)
            {
                _syntheticHold = true;
            }

            _holdLocal = local;
            _emitElapsed = 0f;
            _layer.SetAsLastSibling();
            var ripple = NextRipple();
            if (ripple != null)
            {
                var duration = _tuning != null ? _tuning.TouchRippleDuration : 0.35f;
                ripple.Play(local, RippleSprite(), duration);
                Rippled?.Invoke();
            }

            EmitSparkles(local, TapSparkleCount, 70f);
            var initial = _bubbleLimit < 2 ? _bubbleLimit : 2;
            for (var i = 0; i < initial; i++)
            {
                EmitBubble(local);
            }
        }

        private void EmitBubble(Vector2 local)
        {
            if (_suppressed || ActiveBubbleCount >= _bubbleLimit)
            {
                return;
            }

            var bubble = NextBubble();
            if (bubble == null)
            {
                return;
            }

            _seed += 0.173f;
            var lifetime = _tuning != null ? _tuning.TouchBubbleLifetime : 0.42f;
            bubble.Play(local, BubbleSprite(), lifetime, _seed);
        }

        /// <summary>A ring of twinkling stars around <paramref name="local"/>; <paramref name="reach"/> is the outward travel.</summary>
        private void EmitSparkles(Vector2 local, int count, float reach)
        {
            if (_suppressed || _layer == null)
            {
                return;
            }

            var phase = SparkleRandom() * Mathf.PI * 2f;
            for (var i = 0; i < count; i++)
            {
                var sparkle = NextSparkle();
                if (sparkle == null)
                {
                    return;
                }

                var angle = phase + ((i / (float)Mathf.Max(1, count)) * Mathf.PI * 2f) + ((SparkleRandom() - 0.5f) * 0.6f);
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var start = local + (direction * Mathf.Lerp(6f, 22f, SparkleRandom()));
                var travel = direction * reach * Mathf.Lerp(0.55f, 1f, SparkleRandom());
                var life = SparkleLifetime * Mathf.Lerp(0.75f, 1.2f, SparkleRandom());
                sparkle.Play(start, travel, life, SparkleRandom(), SparkleRandom());
                _sparkleEmitCount++;
            }
        }

        private TouchSparkleView NextSparkle()
        {
            for (var i = 0; i < _sparkles.Count; i++)
            {
                if (_sparkles[i] != null && !_sparkles[i].IsActive)
                {
                    return _sparkles[i];
                }
            }

            if (_sparkles.Count >= SparklePool)
            {
                return null;
            }

            var created = TouchSparkleView.Create(_layer);
            _sparkles.Add(created);
            return created;
        }

        private int CountActiveSparkles()
        {
            var count = 0;
            for (var i = 0; i < _sparkles.Count; i++)
            {
                if (_sparkles[i] != null && _sparkles[i].IsActive)
                {
                    count++;
                }
            }

            return count;
        }

        private float SparkleRandom()
        {
            _sparkleSeed ^= _sparkleSeed << 13;
            _sparkleSeed ^= _sparkleSeed >> 17;
            _sparkleSeed ^= _sparkleSeed << 5;
            return (_sparkleSeed & 0xFFFFFF) / 16777215f;
        }

        private TouchRippleView NextRipple()
        {
            for (var i = 0; i < _ripples.Count; i++)
            {
                if (_ripples[i] != null && !_ripples[i].IsActive)
                {
                    return _ripples[i];
                }
            }

            if (_ripples.Count >= RipplePool)
            {
                return _ripples[0];
            }

            var created = TouchRippleView.Create(_layer);
            _ripples.Add(created);
            return created;
        }

        private TouchBubbleParticleView NextBubble()
        {
            for (var i = 0; i < _bubbles.Count; i++)
            {
                if (_bubbles[i] != null && !_bubbles[i].IsActive)
                {
                    return _bubbles[i];
                }
            }

            if (_bubbles.Count >= _bubbleLimit)
            {
                return null;
            }

            var created = TouchBubbleParticleView.Create(_layer);
            _bubbles.Add(created);
            return created;
        }

        private void StopAll()
        {
            for (var i = 0; i < _ripples.Count; i++)
            {
                if (_ripples[i] != null)
                {
                    _ripples[i].Stop();
                }
            }

            for (var i = 0; i < _bubbles.Count; i++)
            {
                if (_bubbles[i] != null)
                {
                    _bubbles[i].Stop();
                }
            }

            for (var i = 0; i < _sparkles.Count; i++)
            {
                if (_sparkles[i] != null)
                {
                    _sparkles[i].Stop();
                }
            }
        }

        private Sprite RippleSprite()
        {
            if (_art != null && _art.TouchRipple != null)
            {
                return _art.TouchRipple;
            }

            return ProceduralVfxSprite.Ring;
        }

        private Sprite BubbleSprite()
        {
            if (_art != null && _art.SmallBubbleParticle != null)
            {
                return _art.SmallBubbleParticle;
            }

            return ProceduralVfxSprite.Dot;
        }

        private Vector2 ScreenToLocal(Vector2 screen)
        {
            if (_layer == null)
            {
                return screen;
            }

            var canvas = _layer.GetComponentInParent<Canvas>();
            Camera camera = null;
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                camera = canvas.worldCamera;
            }

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_layer, screen, camera, out var local))
            {
                return local;
            }

            return Vector2.zero;
        }

        private static int CountActive(List<TouchRippleView> views)
        {
            var count = 0;
            for (var i = 0; i < views.Count; i++)
            {
                if (views[i] != null && views[i].IsActive)
                {
                    count++;
                }
            }

            return count;
        }

        private int CountActiveBubbles()
        {
            var count = 0;
            for (var i = 0; i < _bubbles.Count; i++)
            {
                if (_bubbles[i] != null && _bubbles[i].IsActive)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
