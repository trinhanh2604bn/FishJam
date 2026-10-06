using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FishPuzzle.Presentation
{
    /// <summary>
    /// Reused small-bubble burst. Particles keep floating after the bubble shell is gone.
    /// A missing sprite emits nothing and never blocks the caller.
    /// </summary>
    public sealed class BubbleBurstPool : MonoBehaviour
    {
        public const int MinCount = 18;
        public const int MaxCount = 26;
        public const float MinLifetime = 0.30f;
        public const float MaxLifetime = 1.20f;
        public const int TrailMinCount = 8;
        public const int TrailMaxCount = 14;
        public const float TrailMinLifetime = 0.80f;
        public const float TrailMaxLifetime = 1.80f;
        public const int DefaultPoolCap = 48;
        public const int HardPoolCap = 72;

        private readonly List<Particle> _particles = new List<Particle>();
        private int _cap = DefaultPoolCap;
        private int _active;
        private int _lastEmitCount;

        public int ActiveCount => _active;

        public int PoolSize => _particles.Count;

        public int LastEmitCount => _lastEmitCount;

        public static BubbleBurstPool Create(RectTransform parent, int cap)
        {
            if (parent == null)
            {
                return null;
            }

            var host = new GameObject("BubbleBurstPool", typeof(RectTransform), typeof(BubbleBurstPool));
            var rect = host.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            rect.localScale = Vector3.one;
            var pool = host.GetComponent<BubbleBurstPool>();
            pool._cap = Mathf.Clamp(cap, MinCount, HardPoolCap);
            return pool;
        }

        public int Emit(Vector2 anchoredPosition, Sprite small, Sprite pop, float lifetime, int count)
        {
            return Emit(anchoredPosition, small, pop, lifetime, count, false);
        }

        public int EmitTrail(Vector3 worldPosition, Sprite small, float lifetime, int count)
        {
            var rect = transform as RectTransform;
            if (rect == null || small == null)
            {
                _lastEmitCount = 0;
                return 0;
            }

            var local = rect.InverseTransformPoint(worldPosition);
            return Emit(new Vector2(local.x, local.y), small, null, lifetime, count, true);
        }

        private int Emit(Vector2 anchoredPosition, Sprite small, Sprite pop, float lifetime, int count, bool trail)
        {
            _lastEmitCount = 0;
            if (!BubblePopVfx.CanPlay(pop, small))
            {
                return 0;
            }

            var minCount = trail ? TrailMinCount : MinCount;
            var maxCount = trail ? TrailMaxCount : MaxCount;
            var emitCount = Mathf.Clamp(count, minCount, maxCount);
            if (emitCount > _cap)
            {
                emitCount = _cap;
            }

            var minLife = trail ? TrailMinLifetime : MinLifetime;
            var maxLife = trail ? TrailMaxLifetime : MaxLifetime;
            var seconds = Mathf.Clamp(lifetime, minLife, maxLife);
            for (var i = 0; i < emitCount; i++)
            {
                var particle = Rent();
                if (particle == null)
                {
                    break;
                }

                var usePop = !trail && pop != null && (small == null || i == 0);
                var sprite = usePop ? pop : small;
                if (sprite == null)
                {
                    sprite = pop != null ? pop : small;
                }

                particle.Launch(anchoredPosition, sprite, usePop, seconds, i, trail);
            }

            Recount();
            _lastEmitCount = emitCount;
            transform.SetAsLastSibling();
            return _lastEmitCount;
        }

        public void Tick(float delta)
        {
            if (_particles.Count == 0)
            {
                _active = 0;
                return;
            }

            var remaining = Mathf.Clamp(delta, 0f, 1f);
            while (remaining > 0f)
            {
                var step = Mathf.Min(0.05f, remaining);
                remaining -= step;
                for (var i = 0; i < _particles.Count; i++)
                {
                    var particle = _particles[i];
                    if (particle == null || !particle.Active)
                    {
                        continue;
                    }

                    particle.Advance(step);
                }
            }

            Recount();
        }

        public void ReleaseAll()
        {
            for (var i = 0; i < _particles.Count; i++)
            {
                if (_particles[i] != null)
                {
                    _particles[i].Stop();
                }
            }

            _active = 0;
        }

        public bool MovedUpward(float minimumRise)
        {
            for (var i = 0; i < _particles.Count; i++)
            {
                var particle = _particles[i];
                if (particle != null && particle.Active && particle.Rise >= minimumRise)
                {
                    return true;
                }
            }

            return false;
        }

        public bool HasSizeVariation()
        {
            var seen = -1f;
            for (var i = 0; i < _particles.Count; i++)
            {
                var particle = _particles[i];
                if (particle == null || !particle.Active)
                {
                    continue;
                }

                if (seen < 0f)
                {
                    seen = particle.Size;
                    continue;
                }

                if (Mathf.Abs(particle.Size - seen) > 0.5f)
                {
                    return true;
                }
            }

            return false;
        }

        public bool IsSemiTransparent()
        {
            for (var i = 0; i < _particles.Count; i++)
            {
                var particle = _particles[i];
                if (particle == null || !particle.Active)
                {
                    continue;
                }

                var alpha = particle.Alpha;
                if (alpha <= 0.05f || alpha >= 0.95f)
                {
                    return false;
                }
            }

            return _active > 0;
        }

        private void Update()
        {
            Tick(Mathf.Min(Time.unscaledDeltaTime, 0.05f));
        }

        private void Recount()
        {
            var active = 0;
            for (var i = 0; i < _particles.Count; i++)
            {
                if (_particles[i] != null && _particles[i].Active)
                {
                    active++;
                }
            }

            _active = active;
        }

        private Particle Rent()
        {
            for (var i = 0; i < _particles.Count; i++)
            {
                var particle = _particles[i];
                if (particle != null && !particle.Active)
                {
                    return particle;
                }
            }

            if (_particles.Count < _cap)
            {
                var created = Particle.Create(transform as RectTransform);
                if (created == null)
                {
                    return null;
                }

                _particles.Add(created);
                return created;
            }

            Particle oldest = null;
            for (var i = 0; i < _particles.Count; i++)
            {
                var particle = _particles[i];
                if (particle == null || !particle.Active)
                {
                    continue;
                }

                if (oldest == null || particle.Elapsed > oldest.Elapsed)
                {
                    oldest = particle;
                }
            }

            return oldest;
        }

        private sealed class Particle
        {
            private RectTransform _rect;
            private Image _image;
            private Vector2 _origin;
            private Vector2 _travel;
            private float _life;
            private float _elapsed;
            private float _alpha;
            private bool _active;
            private bool _linger;

            public bool Active => _active;

            public float Elapsed => _elapsed;

            public float Size => _rect != null ? _rect.sizeDelta.x : 0f;

            public float Alpha => _image != null ? _image.color.a : 0f;

            public float Rise => _rect != null ? _rect.anchoredPosition.y - _origin.y : 0f;

            public static Particle Create(RectTransform parent)
            {
                if (parent == null)
                {
                    return null;
                }

                var host = new GameObject("BubbleBurstParticle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                var rect = host.GetComponent<RectTransform>();
                rect.SetParent(parent, false);
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.localScale = Vector3.one;
                var image = host.GetComponent<Image>();
                image.raycastTarget = false;
                var particle = new Particle();
                particle._rect = rect;
                particle._image = image;
                host.SetActive(false);
                return particle;
            }

            public void Launch(Vector2 anchoredPosition, Sprite sprite, bool popFragment, float lifetime, int index, bool linger)
            {
                if (_rect == null || _image == null || sprite == null)
                {
                    Stop();
                    return;
                }

                var side = index % 2 == 0 ? -1f : 1f;
                var spread = linger ? 18f + ((index % 4) * 7f) : 18f + ((index % 6) * 9f);
                var rise = linger ? 110f + ((index % 5) * 22f) : 70f + ((index % 5) * 16f);
                var size = linger ? 14f + ((index % 5) * 4f) : (popFragment ? 40f : 18f) + ((index % 6) * 4f);
                _origin = anchoredPosition + new Vector2(side * (linger ? 6f : 10f), linger ? 4f : 8f);
                _travel = new Vector2(side * spread, rise);
                _life = lifetime;
                _elapsed = 0f;
                _linger = linger;
                _alpha = linger ? 0.40f + ((index % 4) * 0.06f) : 0.48f + ((index % 4) * 0.07f);
                _active = true;
                _image.sprite = sprite;
                _image.color = new Color(1f, 1f, 1f, _alpha);
                _rect.sizeDelta = new Vector2(size, size);
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

                _elapsed += Mathf.Max(0f, step);
                var t = _life <= 0f ? 1f : Mathf.Clamp01(_elapsed / _life);
                var eased = PresentationMotion.EaseOutQuad(t);
                var rise = _linger ? Mathf.Lerp(0.04f, 1f, eased) : Mathf.Lerp(0.12f, 1f, t);
                _rect.anchoredPosition = _origin + new Vector2(_travel.x * eased, _travel.y * rise);
                var scale = Mathf.Lerp(1f, _linger ? 0.82f : 0.7f, t);
                _rect.localScale = new Vector3(scale, scale, 1f);
                if (_image != null)
                {
                    var fade = _linger ? t * t : t * t * t;
                    var color = _image.color;
                    color.a = _alpha * (1f - fade);
                    _image.color = color;
                }

                if (t < 1f)
                {
                    return;
                }

                Stop();
            }

            public void Stop()
            {
                _active = false;
                _elapsed = 0f;
                if (_rect != null)
                {
                    _rect.gameObject.SetActive(false);
                }
            }
        }
    }
}
