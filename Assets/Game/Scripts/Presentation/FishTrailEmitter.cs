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
        public const int DefaultCap = 200;
        public const int HardCap = 240;
        public const float MinInterval = 0.012f;
        public const float MaxInterval = 0.022f;
        public const float MinLifetime = 0.7f;
        public const float MaxLifetime = 1.1f;
        public const float MinScale = 0.32f;
        public const float MaxScale = 1.0f;

        /// <summary>Most trail bubbles are small; this share is drawn from the medium band instead.</summary>
        public const float MediumChance = 0.2f;
        public const float SmallMaxScale = 0.62f;
        public const float MediumMinScale = 0.75f;
        public const float MinAlpha = 0.35f;
        public const float MaxAlpha = 0.75f;
        public const float MinRise = 24f;
        public const float MaxRise = 60f;

        /// <summary>Full width of the small sideways jitter, in FX-root pixels. Half of this is the maximum offset.</summary>
        public const float HorizontalJitter = 18f;

        /// <summary>Bubble → Waiting Tray uses a longer interval so the trail is lighter but still follows the route.</summary>
        public const float TrayIntervalFactor = 1.8f;

        /// <summary>Interval multiplier for the first 10% of the route (light start).</summary>
        public const float StartIntervalFactor = 1.4f;

        /// <summary>Interval multiplier reached at the very end of the route (taper from 80%).</summary>
        public const float EndIntervalFactor = 1.5f;

        public const string RootName = "FishTrailFxRoot";

        private const float BaseSize = 28f;

        private static readonly Color SparkleTint = new Color(1f, 0.96f, 0.8f, 1f);
        private static readonly Color BubbleTint = Color.white;

        private readonly List<Particle> _particles = new List<Particle>();
        private readonly List<Vector2> _spawnPositions = new List<Vector2>();
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

        public int SpawnCount => _spawnPositions.Count;

        public bool HasSprite => _sprite != null;

        public RectTransform FxRoot => _layer;

        public static FishTrailEmitter Create(RectTransform parent, Sprite sprite, int cap = DefaultCap)
        {
            if (parent == null)
            {
                return null;
            }

            var host = new GameObject(RootName, typeof(RectTransform), typeof(CanvasGroup), typeof(FishTrailEmitter));
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

        /// <summary>Random spawn interval inside [0.012, 0.022] s, stretched for the tray route.</summary>
        public float NextInterval(bool toTray)
        {
            var interval = Mathf.Lerp(MinInterval, MaxInterval, Random01());
            return toTray ? interval * TrayIntervalFactor : interval;
        }

        /// <summary>Spawn interval at <paramref name="routeProgress"/> (0–1): light start, full middle, slight taper.</summary>
        public float NextInterval(bool toTray, float routeProgress)
        {
            return NextInterval(toTray) * RouteDensityFactor(routeProgress);
        }

        /// <summary>Interval multiplier along the route. 1 means full density; larger means sparser.</summary>
        public static float RouteDensityFactor(float routeProgress)
        {
            var t = Mathf.Clamp01(routeProgress);
            if (t < 0.1f)
            {
                return StartIntervalFactor;
            }

            if (t <= 0.8f)
            {
                return 1f;
            }

            return Mathf.Lerp(1f, EndIntervalFactor, (t - 0.8f) / 0.2f);
        }

        /// <summary>
        /// One trail bubble at the fish's position right now. The particle is parented to this FX root, not the fish.
        /// </summary>
        public bool EmitCurrent(Transform fish)
        {
            if (fish == null)
            {
                _emitCount++;
                return false;
            }

            return Emit(fish.position);
        }

        /// <summary>One trail bubble at <paramref name="worldPosition"/>. Returns false when the pool is exhausted or nothing spawned.</summary>
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

            var jitter = new Vector2((Random01() - 0.5f) * HorizontalJitter, (Random01() - 0.5f) * 10f);
            var scale = Random01() < MediumChance
                ? Mathf.Lerp(MediumMinScale, MaxScale, Random01())
                : Mathf.Lerp(MinScale, SmallMaxScale, Random01());
            var life = Mathf.Lerp(MinLifetime, MaxLifetime, Random01());
            var drift = new Vector2((Random01() - 0.5f) * 8f, Mathf.Lerp(MinRise, MaxRise, Random01()));
            var alpha = Mathf.Lerp(MinAlpha, MaxAlpha, Random01());
            var anchored = WorldToAnchored(_layer, worldPosition) + jitter;
            particle.Launch(_layer, _sprite, anchored, drift, BaseSize * scale, life, alpha, BubbleTint, false, Random01());
            if (!PlacedNear(particle.WorldPosition, worldPosition, jitter))
            {
                particle.MoveToWorld(_layer, worldPosition, jitter);
            }

            _spawnPositions.Add(new Vector2(particle.WorldPosition.x, particle.WorldPosition.y));
            return true;
        }

        /// <summary>One small glint near <paramref name="worldPosition"/>. Skipped silently when the pool is full.</summary>
        public bool EmitSparkle(Vector3 worldPosition)
        {
            if (_layer == null)
            {
                return false;
            }

            var particle = Rent();
            if (particle == null)
            {
                return false;
            }

            var offset = new Vector2((Random01() - 0.5f) * 52f, (Random01() - 0.5f) * 40f);
            var drift = new Vector2((Random01() - 0.5f) * 30f, Mathf.Lerp(10f, 32f, Random01()));
            particle.Launch(
                _layer,
                ProceduralVfxSprite.Sparkle,
                WorldToAnchored(_layer, worldPosition) + offset,
                drift,
                Mathf.Lerp(16f, 30f, Random01() * Random01()),
                Mathf.Lerp(0.4f, 0.65f, Random01()),
                Mathf.Lerp(0.7f, 1f, Random01()),
                SparkleTint,
                true,
                Random01());
            return true;
        }

        /// <summary>Short ring of small and medium bubbles around a landed fish, plus a few light glints.</summary>
        public int Burst(Vector3 worldPosition, int count, int sparkles = 0)
        {
            if (_sprite == null || _layer == null || count <= 0)
            {
                return 0;
            }

            _burstCount++;
            var center = WorldToAnchored(_layer, worldPosition);
            var emitted = 0;
            var phase = Random01() * Mathf.PI * 2f;
            for (var i = 0; i < count; i++)
            {
                var particle = Rent();
                if (particle == null)
                {
                    break;
                }

                var medium = i < 2;
                var angle = phase + ((i / (float)count) * Mathf.PI * 2f) + ((Random01() - 0.5f) * 0.5f);
                var radius = 18f + (Random01() * 20f);
                var start = center + new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius * 0.6f);
                var drift = new Vector2(Mathf.Cos(angle) * Mathf.Lerp(18f, 40f, Random01()), Mathf.Lerp(30f, 70f, Random01()));
                var size = BaseSize * (medium ? Mathf.Lerp(MediumMinScale, MaxScale, Random01()) : Mathf.Lerp(MinScale, SmallMaxScale, Random01()));
                var life = Mathf.Lerp(0.32f, 0.5f, Random01());
                var alpha = Mathf.Lerp(0.45f, 0.75f, Random01());
                particle.Launch(_layer, _sprite, start, drift, size, life, alpha, BubbleTint, false, Random01());
                emitted++;
            }

            for (var i = 0; i < sparkles; i++)
            {
                if (!EmitSparkle(worldPosition))
                {
                    break;
                }

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
            _spawnPositions.Clear();
        }

        public void CopySpawnPositions(List<Vector2> destination)
        {
            if (destination == null)
            {
                return;
            }

            destination.Clear();
            for (var i = 0; i < _spawnPositions.Count; i++)
            {
                destination.Add(_spawnPositions[i]);
            }
        }

        public Vector2 SpawnPosition(int index)
        {
            return _spawnPositions[index];
        }

        /// <summary>
        /// True when <paramref name="positions"/> contains at least three separated samples,
        /// the first and last are at least <paramref name="minSpan"/> apart, and later samples
        /// progress along that span. This is the route proof — not merely "emit was called".
        /// </summary>
        public static bool SamplesSpanRoute(IReadOnlyList<Vector2> positions, float minSpan)
        {
            if (positions == null || positions.Count < 3 || minSpan <= 0f)
            {
                return false;
            }

            var first = positions[0];
            var last = positions[positions.Count - 1];
            var span = Vector2.Distance(first, last);
            if (span < minSpan)
            {
                return false;
            }

            var distinct = 1;
            var distinctGap = minSpan * 0.12f;
            for (var i = 1; i < positions.Count; i++)
            {
                var separated = true;
                for (var j = 0; j < i; j++)
                {
                    if (Vector2.Distance(positions[i], positions[j]) < distinctGap)
                    {
                        separated = false;
                        break;
                    }
                }

                if (separated)
                {
                    distinct++;
                }
            }

            if (distinct < 3)
            {
                return false;
            }

            var axis = (last - first) / span;
            var previous = 0f;
            var forward = 0;
            var step = minSpan * 0.08f;
            for (var i = 1; i < positions.Count; i++)
            {
                var projection = Vector2.Dot(positions[i] - first, axis);
                if (projection > previous + step)
                {
                    forward++;
                }

                if (projection > previous)
                {
                    previous = projection;
                }
            }

            return forward >= 2;
        }

        public bool SamplesSinceFollowRoute(int startIndex, float minSpan = 20f)
        {
            if (startIndex < 0)
            {
                startIndex = 0;
            }

            if (startIndex > _spawnPositions.Count)
            {
                startIndex = _spawnPositions.Count;
            }

            var slice = new List<Vector2>(_spawnPositions.Count - startIndex);
            for (var i = startIndex; i < _spawnPositions.Count; i++)
            {
                slice.Add(_spawnPositions[i]);
            }

            return SamplesSpanRoute(slice, minSpan);
        }

        public Transform GetParticleParent(int index)
        {
            return _particles[index].Parent;
        }

        public Vector3 GetParticleWorldPosition(int index)
        {
            return _particles[index].WorldPosition;
        }

        public bool IsParticleActive(int index)
        {
            return index >= 0 && index < _particles.Count && _particles[index].Active;
        }

        public bool AllParentedToFxRoot()
        {
            for (var i = 0; i < _particles.Count; i++)
            {
                if (_particles[i].Parent != _layer)
                {
                    return false;
                }
            }

            return _layer != null;
        }

        /// <summary>
        /// Converts a world point into the center-anchored coordinate used by trail particles.
        /// Screen-point conversion is preferred when it agrees with the FX root's transform;
        /// otherwise the transform inverse is used so a fish local position is never copied across parents.
        /// </summary>
        public static Vector2 WorldToAnchored(RectTransform layer, Vector3 worldPosition)
        {
            if (layer == null)
            {
                return Vector2.zero;
            }

            var inverse = layer.InverseTransformPoint(worldPosition);
            var fromInverse = new Vector2(inverse.x, inverse.y) - layer.rect.center;
            var canvas = layer.GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                return fromInverse;
            }

            Camera camera = null;
            if (canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                camera = canvas.worldCamera;
            }

            var screen = RectTransformUtility.WorldToScreenPoint(camera, worldPosition);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, screen, camera, out var pivotLocal))
            {
                return fromInverse;
            }

            var fromUtility = pivotLocal - layer.rect.center;
            if ((fromUtility - fromInverse).sqrMagnitude <= 4f)
            {
                return fromUtility;
            }

            return fromInverse;
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

            if (_particles.Count >= _cap)
            {
                return null;
            }

            var created = Particle.Create(_layer);
            if (created != null)
            {
                _particles.Add(created);
            }

            return created;
        }

        private static bool PlacedNear(Vector3 particleWorld, Vector3 fishWorld, Vector2 jitter)
        {
            var miss = particleWorld - fishWorld;
            miss.z = 0f;
            var allowance = jitter.magnitude + 2f;
            return miss.sqrMagnitude <= allowance * allowance;
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
            private float _wobblePhase;
            private float _spin;
            private bool _sparkle;
            private bool _active;

            public bool Active => _active;

            public float Progress => _life <= 0f ? 1f : _elapsed / _life;

            public bool RaycastTarget => _image != null && _image.raycastTarget;

            public Transform Parent => _rect != null ? _rect.parent : null;

            public Vector3 WorldPosition => _rect != null ? _rect.position : Vector3.zero;

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

            public void Launch(RectTransform parent, Sprite sprite, Vector2 anchored, Vector2 drift, float size, float life, float alpha, Color tint, bool sparkle, float random)
            {
                if (_rect == null || _image == null || parent == null)
                {
                    return;
                }

                if (_rect.parent != parent)
                {
                    _rect.SetParent(parent, false);
                }

                _drift = drift;
                _life = Mathf.Max(0.05f, life);
                _elapsed = 0f;
                _alpha = alpha;
                _sparkle = sparkle;
                _wobblePhase = random * Mathf.PI * 2f;
                _spin = (random - 0.5f) * 200f;
                _active = true;
                _image.sprite = sprite;
                _image.color = new Color(tint.r, tint.g, tint.b, alpha);
                _rect.sizeDelta = new Vector2(size, size);
                _rect.anchoredPosition = anchored;
                _origin = anchored;
                _rect.localRotation = Quaternion.identity;
                _rect.localScale = Vector3.one;
                _rect.SetAsLastSibling();
                _rect.gameObject.SetActive(true);
            }

            public void MoveToWorld(RectTransform parent, Vector3 worldPosition, Vector2 anchoredJitter)
            {
                if (_rect == null || parent == null)
                {
                    return;
                }

                if (_rect.parent != parent)
                {
                    _rect.SetParent(parent, false);
                }

                _rect.position = worldPosition;
                _rect.anchoredPosition += anchoredJitter;
                _origin = _rect.anchoredPosition;
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
                float scale;
                if (_sparkle)
                {
                    scale = Mathf.Sin(t * Mathf.PI) * 1.1f;
                    _rect.localRotation = Quaternion.Euler(0f, 0f, _spin * t);
                    _rect.anchoredPosition = _origin + (_drift * eased);
                }
                else
                {
                    scale = Mathf.Lerp(0.6f, 1f, Mathf.Clamp01(t / 0.15f)) * Mathf.Lerp(1f, 1.12f, eased);
                    var wobble = Mathf.Sin((t * 8f) + _wobblePhase) * 2.5f * t;
                    _rect.anchoredPosition = _origin + (_drift * eased) + new Vector2(wobble, 0f);
                }

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
