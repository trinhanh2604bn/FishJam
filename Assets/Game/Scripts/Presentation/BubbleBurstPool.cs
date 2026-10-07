using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FishPuzzle.Presentation
{
    /// <summary>
    /// Reused small-bubble burst. Particles keep floating after the bubble shell is gone.
    /// A pop throws a ring of small bubbles, a few medium ones, a soft glow and light sparkles.
    /// A missing sprite emits nothing and never blocks the caller. A full pool skips particles instead of recycling.
    /// </summary>
    public sealed class BubbleBurstPool : MonoBehaviour
    {
        /// <summary>Small bubbles per pop (requested count is clamped into this band).</summary>
        public const int MinCount = 26;
        public const int MaxCount = 40;
        public const int MinMediumCount = 3;
        public const int MaxMediumCount = 6;
        public const int MinSparkleCount = 3;
        public const int MaxSparkleCount = 5;
        public const float MinLifetime = 0.6f;
        public const float MaxLifetime = 1.1f;
        public const float MinAlpha = 0.35f;
        public const float MaxAlpha = 0.75f;
        public const int TrailMinCount = 8;
        public const int TrailMaxCount = 14;
        public const float TrailMinLifetime = 0.80f;
        public const float TrailMaxLifetime = 1.80f;
        public const int DefaultPoolCap = 200;
        public const int HardPoolCap = 240;

        private static readonly Color BubbleTint = new Color(0.92f, 0.98f, 1f, 1f);
        private static readonly Color GlowTint = new Color(0.62f, 0.9f, 1f, 1f);
        private static readonly Color SparkleTint = new Color(1f, 0.97f, 0.82f, 1f);

        private readonly List<Particle> _particles = new List<Particle>();
        private int _cap = DefaultPoolCap;
        private int _active;
        private int _lastEmitCount;
        private uint _seed = 0x2545F491u;

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
            _lastEmitCount = 0;
            if (!BubblePopVfx.CanPlay(pop, small))
            {
                return 0;
            }

            var bubble = small != null ? small : pop;
            var maxLife = Mathf.Clamp(lifetime, MinLifetime + 0.05f, MaxLifetime);
            var emitted = 0;

            if (Launch(Particle.Kind.Glow, ProceduralVfxSprite.Glow, anchoredPosition, Vector2.zero, 0f, 96f, 0.30f, 0.32f, GlowTint))
            {
                emitted++;
            }

            if (pop != null && Launch(Particle.Kind.Ring, pop, anchoredPosition, Vector2.zero, 0f, 46f, 0.24f, 0.6f, Color.white))
            {
                emitted++;
            }

            var smallCount = Mathf.Clamp(count, MinCount, MaxCount);
            var phase = Random01() * Mathf.PI * 2f;
            for (var i = 0; i < smallCount; i++)
            {
                var angle = phase + ((i / (float)smallCount) * Mathf.PI * 2f) + ((Random01() - 0.5f) * 0.45f);
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * 0.85f);
                var distance = Mathf.Lerp(34f, 92f, Random01());
                var size = Mathf.Lerp(8f, 20f, Random01() * Random01());
                var life = Mathf.Lerp(MinLifetime, maxLife, Random01());
                var alpha = Mathf.Lerp(MinAlpha, MaxAlpha, Random01());
                var start = anchoredPosition + (direction * Mathf.Lerp(6f, 16f, Random01()));
                if (!Launch(Particle.Kind.Bubble, bubble, start, direction * distance, Mathf.Lerp(40f, 90f, Random01()), size, life, alpha, BubbleTint))
                {
                    break;
                }

                emitted++;
            }

            var mediumCount = MinMediumCount + Mathf.FloorToInt(Random01() * (MaxMediumCount - MinMediumCount + 0.999f));
            for (var i = 0; i < mediumCount; i++)
            {
                var angle = phase + ((i + 0.5f) / mediumCount * Mathf.PI * 2f) + ((Random01() - 0.5f) * 0.8f);
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * 0.7f + 0.25f);
                var size = Mathf.Lerp(24f, 32f, Random01());
                var life = Mathf.Lerp(MinLifetime, maxLife, Random01());
                var alpha = Mathf.Lerp(0.40f, 0.62f, Random01());
                if (!Launch(Particle.Kind.Bubble, bubble, anchoredPosition, direction * Mathf.Lerp(26f, 48f, Random01()), Mathf.Lerp(60f, 100f, Random01()), size, life, alpha, BubbleTint))
                {
                    break;
                }

                emitted++;
            }

            var sparkleCount = MinSparkleCount + Mathf.FloorToInt(Random01() * (MaxSparkleCount - MinSparkleCount + 0.999f));
            for (var i = 0; i < sparkleCount; i++)
            {
                var angle = Random01() * Mathf.PI * 2f;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var size = Mathf.Lerp(12f, 20f, Random01());
                var life = Mathf.Lerp(0.30f, 0.50f, Random01());
                if (!Launch(Particle.Kind.Sparkle, ProceduralVfxSprite.Sparkle, anchoredPosition + (direction * Mathf.Lerp(10f, 30f, Random01())), direction * Mathf.Lerp(20f, 50f, Random01()), 18f, size, life, Mathf.Lerp(0.55f, 0.8f, Random01()), SparkleTint))
                {
                    break;
                }

                emitted++;
            }

            Recount();
            _lastEmitCount = emitted;
            transform.SetAsLastSibling();
            return emitted;
        }

        public int EmitTrail(Vector3 worldPosition, Sprite small, float lifetime, int count)
        {
            var rect = transform as RectTransform;
            _lastEmitCount = 0;
            if (rect == null || small == null)
            {
                return 0;
            }

            var local = rect.InverseTransformPoint(worldPosition);
            var origin = new Vector2(local.x, local.y);
            var emitCount = Mathf.Clamp(count, TrailMinCount, TrailMaxCount);
            var seconds = Mathf.Clamp(lifetime, TrailMinLifetime, TrailMaxLifetime);
            var emitted = 0;
            for (var i = 0; i < emitCount; i++)
            {
                var side = i % 2 == 0 ? -1f : 1f;
                var spread = new Vector2(side * Mathf.Lerp(14f, 38f, Random01()), 0f);
                var size = Mathf.Lerp(12f, 26f, Random01());
                var alpha = Mathf.Lerp(0.40f, 0.60f, Random01());
                if (!Launch(Particle.Kind.Bubble, small, origin + new Vector2(side * 6f, 4f), spread, Mathf.Lerp(110f, 200f, Random01()), size, seconds, alpha, BubbleTint))
                {
                    break;
                }

                emitted++;
            }

            Recount();
            _lastEmitCount = emitted;
            transform.SetAsLastSibling();
            return emitted;
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

        private bool Launch(Particle.Kind kind, Sprite sprite, Vector2 origin, Vector2 burst, float rise, float size, float life, float alpha, Color tint)
        {
            if (sprite == null)
            {
                return true;
            }

            var particle = Rent();
            if (particle == null)
            {
                return false;
            }

            particle.Launch(kind, sprite, origin, burst, rise, size, life, alpha, tint, Random01());
            return true;
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

            if (_particles.Count >= _cap)
            {
                return null;
            }

            var created = Particle.Create(transform as RectTransform);
            if (created != null)
            {
                _particles.Add(created);
            }

            return created;
        }

        private float Random01()
        {
            _seed ^= _seed << 13;
            _seed ^= _seed >> 17;
            _seed ^= _seed << 5;
            return (_seed & 0xFFFFFF) / 16777215f;
        }

        private sealed class Particle
        {
            public enum Kind
            {
                Bubble,
                Glow,
                Ring,
                Sparkle,
            }

            private RectTransform _rect;
            private Image _image;
            private Kind _kind;
            private Vector2 _origin;
            private Vector2 _burst;
            private float _rise;
            private float _life;
            private float _elapsed;
            private float _alpha;
            private float _wobblePhase;
            private float _spin;
            private bool _active;

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

            public void Launch(Kind kind, Sprite sprite, Vector2 origin, Vector2 burst, float rise, float size, float life, float alpha, Color tint, float random)
            {
                if (_rect == null || _image == null || sprite == null)
                {
                    Stop();
                    return;
                }

                _kind = kind;
                _origin = origin;
                _burst = burst;
                _rise = rise;
                _life = Mathf.Max(0.05f, life);
                _elapsed = 0f;
                _alpha = alpha;
                _wobblePhase = random * Mathf.PI * 2f;
                _spin = (random - 0.5f) * 240f;
                _active = true;
                _image.sprite = sprite;
                _image.color = new Color(tint.r, tint.g, tint.b, alpha);
                _rect.sizeDelta = new Vector2(size, size);
                _rect.anchoredPosition = origin;
                _rect.localRotation = Quaternion.identity;
                _rect.localScale = Vector3.one * (kind == Kind.Bubble ? 0.55f : 0.4f);
                _rect.SetAsLastSibling();
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
                float scale;
                float fade;
                switch (_kind)
                {
                    case Kind.Glow:
                        scale = Mathf.Lerp(0.6f, 1.5f, PresentationMotion.EaseOutQuad(t));
                        fade = 1f - t;
                        break;
                    case Kind.Ring:
                        scale = Mathf.Lerp(0.7f, 1.9f, PresentationMotion.EaseOutQuad(t));
                        fade = 1f - (t * t);
                        break;
                    case Kind.Sparkle:
                        scale = Mathf.Sin(t * Mathf.PI) * 1.1f;
                        fade = 1f - (t * t * t);
                        _rect.localRotation = Quaternion.Euler(0f, 0f, _spin * t);
                        break;
                    default:
                        scale = Mathf.Lerp(0.55f, 1f, Mathf.Clamp01(t / 0.18f)) * Mathf.Lerp(1f, 0.85f, t);
                        fade = t < 0.45f ? 0f : (t - 0.45f) / 0.55f;
                        fade *= fade;
                        break;
                }

                var outward = 1f - ((1f - t) * (1f - t) * (1f - t));
                var buoyancy = t * t;
                var wobble = _kind == Kind.Bubble ? Mathf.Sin((t * 9f) + _wobblePhase) * 3f * t : 0f;
                _rect.anchoredPosition = _origin + (_burst * outward) + new Vector2(wobble, _rise * buoyancy);
                _rect.localScale = new Vector3(scale, scale, 1f);
                if (_image != null)
                {
                    var color = _image.color;
                    color.a = _alpha * Mathf.Clamp01(1f - fade);
                    _image.color = color;
                }

                if (t >= 1f)
                {
                    Stop();
                }
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
