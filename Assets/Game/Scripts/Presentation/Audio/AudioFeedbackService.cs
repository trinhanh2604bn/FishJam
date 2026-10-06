using System;
using System.Collections.Generic;
using UnityEngine;

namespace FishPuzzle.Presentation
{
    /// <summary>
    /// Small pooled SFX player owned by the scene bootstrapper and handed to the flow.
    /// Presentation only: every call is fire-and-forget, never throws, and never blocks gameplay.
    /// </summary>
    public sealed class AudioFeedbackService : MonoBehaviour
    {
        public const int DefaultSourceCount = 8;
        private const float RepeatGuardSeconds = 0.035f;

        private static readonly int SlotCount = Enum.GetValues(typeof(SfxId)).Length;

        private readonly List<AudioSource> _sources = new List<AudioSource>();
        private readonly int[] _playCounts = new int[SlotCount];
        private readonly float[] _lastPlayed = new float[SlotCount];
        private GameAudioCatalog _catalog;
        private bool _proceduralFallback = true;
        private bool _muted;
        private int _next;
        private int _requestCount;
        private SfxId _lastId;
        private float _lastPitch = 1f;

        public int SourceCount => _sources.Count;

        public int RequestCount => _requestCount;

        public SfxId LastId => _lastId;

        public float LastPitch => _lastPitch;

        public bool Muted
        {
            get => _muted;
            set => _muted = value;
        }

        public static AudioFeedbackService Create(GameObject host, GameAudioCatalog catalog, int sourceCount = DefaultSourceCount)
        {
            if (host == null)
            {
                return null;
            }

            var service = host.GetComponent<AudioFeedbackService>();
            if (service == null)
            {
                service = host.AddComponent<AudioFeedbackService>();
            }

            service.Configure(catalog, catalog == null || catalog.ProceduralFallback);
            service.EnsureSources(Mathf.Clamp(sourceCount, 1, 16));
            return service;
        }

        public void Configure(GameAudioCatalog catalog, bool proceduralFallback)
        {
            _catalog = catalog;
            _proceduralFallback = proceduralFallback;
        }

        /// <summary>Number of accepted requests for one slot (includes muted or silent ones). Test and debug aid.</summary>
        public int PlayCount(SfxId id)
        {
            var index = (int)id;
            return index >= 0 && index < _playCounts.Length ? _playCounts[index] : 0;
        }

        public bool Play(SfxId id)
        {
            return Play(id, 1f, 1f);
        }

        /// <summary>
        /// Requests one sound. Returns true when a clip actually started.
        /// A missing clip, missing source or audio failure returns false and changes nothing else.
        /// </summary>
        public bool Play(SfxId id, float pitch, float volumeScale)
        {
            var index = (int)id;
            if (index < 0 || index >= _playCounts.Length)
            {
                return false;
            }

            var now = Time.unscaledTime;
            if (_playCounts[index] > 0 && now - _lastPlayed[index] < RepeatGuardSeconds)
            {
                return false;
            }

            _playCounts[index]++;
            _lastPlayed[index] = now;
            _requestCount++;
            _lastId = id;
            _lastPitch = pitch;
            if (_muted)
            {
                return false;
            }

            try
            {
                var clip = ResolveClip(id, out var volume);
                if (clip == null)
                {
                    return false;
                }

                var source = NextSource();
                if (source == null)
                {
                    return false;
                }

                var master = _catalog != null ? _catalog.MasterVolume : 0.7f;
                source.pitch = Mathf.Clamp(pitch, 0.5f, 2f);
                source.PlayOneShot(clip, Mathf.Clamp01(volume * master * volumeScale));
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public void StopAll()
        {
            for (var i = 0; i < _sources.Count; i++)
            {
                if (_sources[i] != null)
                {
                    _sources[i].Stop();
                }
            }
        }

        private AudioClip ResolveClip(SfxId id, out float volume)
        {
            volume = 1f;
            if (_catalog != null && _catalog.TryGet(id, out var clip, out volume))
            {
                return clip;
            }

            volume = 1f;
            return _proceduralFallback ? ProceduralSfx.Get(id) : null;
        }

        private AudioSource NextSource()
        {
            if (_sources.Count == 0)
            {
                EnsureSources(DefaultSourceCount);
            }

            for (var attempt = 0; attempt < _sources.Count; attempt++)
            {
                var source = _sources[_next];
                _next = (_next + 1) % _sources.Count;
                if (source != null && !source.isPlaying)
                {
                    return source;
                }
            }

            // All busy: steal the next one in rotation.
            var stolen = _sources[_next];
            _next = (_next + 1) % _sources.Count;
            return stolen;
        }

        private void EnsureSources(int count)
        {
            for (var i = _sources.Count - 1; i >= 0; i--)
            {
                if (_sources[i] == null)
                {
                    _sources.RemoveAt(i);
                }
            }

            while (_sources.Count < count)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = 0f;
                source.priority = 128;
                _sources.Add(source);
            }

            _next = 0;
        }
    }
}
