using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FishPuzzle.Presentation
{
    /// <summary>
    /// Pooled small bubbles left behind a flying fish, plus short landing bursts.
    /// Particles stay where they spawn (they do not follow the fish), drift up and fade.
    /// Presentation only: it holds no gameplay references and never blocks raycasts.
    /// </summary>
    public sealed class FishTrailEmitter : MonoBehaviour
    {
        public const int DefaultCap = 40;
        public const int HardCap = 64;
        public const float MinInterval = 0.04f;
        public const float MaxInterval = 0.07f;
        public const float MinLifetime = 0.30f;
        public const float MaxLifetime = 0.50f;
        public const float MinScale = 0.5f;
        public const float MaxScale = 1.0f;

        /// <summary>Bubble → Waiting Tray uses fewer particles: its spawn interval is multiplied by this.</summary>
        public const float TrayIntervalFactor = 2.2f;

        private const float BaseSize = 26f;

        private readonly List<Particle> _particles = new List<Particle>();
        private RectTransform _layer;
        private Sprite _sprite;
        private int _cap = DefaultCap;
        private int _emitCount;
        private int _burstCount;
        private uint _seed = 0x9E3779B9u;

        public int ActiveCount
        {
            get
            {
                var count = 0;
                for (var i = 0; i < _particles.Count; i++)
                {
                    if (_particles[i].Active)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public int PoolSize => _particles.Count;

        public int Cap => _cap;

        /// <summary>Total trail particles requested since the last <see cref="ResetCounters"/>.</summary>
        public int EmitCount => _emitCount;

        public int BurstCount => _burstCount;

        public bool HasSprite => _sprite != null;

        public static FishTrailEmitter Create(RectTransform parent, Sprite sprite, int cap = DefaultCap)
        {
            if (parent == null)
            {
                return null;
            }

            var host = new GameObject("FishTrailLayer", typeof(RectTransform), typeof(CanvasGroup), typeof(FishTrailEmitter));
            var rect = host.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            var group = host.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            var emitter = host.GetComponent<FishTrailEmitter>();
            emitter._layer = rect;
            emitter._sprite = sprite;
            emitter._cap = Mathf.Clamp(cap, 8, HardCap);
            return emitter;
        }

        public void SetSprite(Sprite sprite)
        {
            _sprite = sprite;
        }

        /// <summary>Random spawn interval inside [0.04, 0.07] s, stretched for the tray route.</summary>
        public float NextInterval(bool toTray)
        {
            var interval = Mathf.Lerp(MinInterval, MaxInterval, Random01());
            return toTray ? interval * TrayIntervalFactor : interval;
        }

        /// <summary>One trail bubble near <paramref name="worldPosition"/>. Returns false when nothing spawned.</summary>
        public bool Emit(Vector3 worldPosition)
        {
            _emitCount++;
            if (_sprite == null || _layer == null)
            {
                return false;
            }

            var particle = Rent();
            if (particle == null)
            {
                return false;
            }

            var local = _layer.InverseTransformPoint(worldPosition);
            var offset = new Vector2((Random01() - 0.5f) * 36f, (Random01() - 0.5f) * 28f);
            var scale = Mathf.Lerp(MinScale, MaxScale, Random01());
            var life = Mathf.Lerp(MinLifetime, MaxLifetime, Random01());
            var drift = new Vector2((Random01() - 0.5f) * 14f, 26f + (Random01() * 22f));
            var alpha = 0.45f + (Random01() * 0.25f);
            particle.Launch(_sprite, new Vector2(local.x, local.y) + offset, drift, BaseSize * scale, life, alpha);
            return true;
        }

        /// <summary>Short ring of bubbles around a landed fish.</summary>
        public int Burst(Vector3 worldPosition, int count)
        {
            if (_sprite == null || _layer == null || count <= 0)
            {
                return 0;
            }

            _burstCount++;
            var local = _layer.InverseTransformPoint(worldPosition);
            var center = new Vector2(local.x, local.y);
            var emitted = 0;
            for (var i = 0; i < count; i++)
            {
                var particle = Rent();
                if (particle == null)
                {
                    break;
                }

                var angle = ((i / (float)count) * Mathf.PI * 2f) + (Random01() * 0.5f);
                var radius = 26f + (Random01() * 18f);
                var start = center + new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius * 0.6f);
                var drift = new Vector2(Mathf.Cos(angle) * 22f, 34f + (Random01() * 30f));
                var size = BaseSize * Mathf.Lerp(MinScale, MaxScale, Random01());
                var life = Mathf.Lerp(0.35f, 0.55f, Random01());
                particle.Launch(_sprite, start, drift, size, life, 0.55f + (Random01() * 0.2f));
                emitted++;
            }

            return emitted;
        }

        public void ReleaseAll()
        {
            for (var i = 0; i < _particles.Count; i++)
            {
                _particles[i].Stop();
            }
        }

        public void ResetCounters()
        {
            _emitCount = 0;
            _burstCount = 0;
        }

        public void Tick(float delta)
        {
            var step = Mathf.Clamp(delta, 0f, 0.1f);
            for (var i = 0; i < _particles.Count; i++)
            {
                _particles[i].Advance(step);
            }
        }

        public bool AllRaycastsDisabled()
        {
            for (var i = 0; i < _particles.Count; i++)
            {
                if (_particles[i].RaycastTarget)
                {
                    return false;
                }
            }

            var group = GetComponent<CanvasGroup>();
            return group == null || !group.blocksRaycasts;
        }

        private void Update()
        {
            Tick(Time.unscaledDeltaTime);
        }

        private Particle Rent()
        {
            for (var i = 0; i < _particles.Count; i++)
            {
                if (!_particles[i].Active)
                {
                    return _particles[i];
                }
            }

            if (_particles.Count < _cap)
            {
                var created = Particle.Create(_layer);
                if (created != null)
                {
                    _particles.Add(created);
                }

                return created;
            }

            // At the cap: recycle the oldest particle instead of allocating.
            Particle oldest = null;
            for (var i = 0; i < _particles.Count; i++)
            {
                if (oldest == null || _particles[i].Progress > oldest.Progress)
                {
                    oldest = _particles[i];
                }
            }

            return oldest;
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
            private RectTransform _rect;
            private Image _image;
            private Vector2 _origin;
            private Vector2 _drift;
            private float _life;
            private float _elapsed;
            private float _alpha;
            private bool _active;

            public bool Active => _active;

            public float Progress => _life <= 0f ? 1f : _elapsed / _life;

            public bool RaycastTarget => _image != null && _image.raycastTarget;

            public static Particle Create(RectTransform parent)
            {
                if (parent == null)
                {
                    return null;
                }

                var host = new GameObject("TrailBubble", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                host.SetActive(false);
                var rect = host.GetComponent<RectTransform>();
                rect.SetParent(parent, false);
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                var image = host.GetComponent<Image>();
                image.raycastTarget = false;
                return new Particle { _rect = rect, _image = image };
            }

            public void Launch(Sprite sprite, Vector2 anchored, Vector2 drift, float size, float life, float alpha)
            {
                if (_rect == null || _image == null)
                {
                    return;
                }

                _origin = anchored;
                _drift = drift;
                _life = Mathf.Max(0.05f, life);
                _elapsed = 0f;
                _alpha = alpha;
                _active = true;
                _image.sprite = sprite;
                _image.color = new Color(1f, 1f, 1f, alpha);
                _rect.sizeDelta = new Vector2(size, size);
                _rect.anchoredPosition = anchored;
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
                var t = Mathf.Clamp01(_elapsed / _life);
                var eased = PresentationMotion.EaseOutQuad(t);
                _rect.anchoredPosition = _origin + (_drift * eased);
                var scale = Mathf.Lerp(0.85f, 1.1f, eased);
                _rect.localScale = new Vector3(scale, scale, 1f);
                var color = _image.color;
                color.a = _alpha * (1f - (t * t));
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
}
