using System;
using UnityEngine;

namespace FishPuzzle.Presentation
{
    /// <summary>
    /// Optional SFX clips. Every slot may stay empty: the audio service then uses a soft generated
    /// development tone, or stays silent. A missing clip never blocks gameplay.
    /// </summary>
    [CreateAssetMenu(fileName = "GameAudioCatalog", menuName = "Fish Puzzle/Game Audio Catalog")]
    public sealed class GameAudioCatalog : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public SfxId Id;
            public AudioClip Clip;
            [Range(0f, 1f)] public float Volume;
        }

        [SerializeField, Range(0f, 1f)] private float _masterVolume = 0.7f;
        [Tooltip("Generate soft placeholder tones for empty slots. Development only; replace with original clips.")]
        [SerializeField] private bool _proceduralFallback = true;
        [SerializeField] private Entry[] _entries = Array.Empty<Entry>();

        public float MasterVolume => Mathf.Clamp01(_masterVolume);

        public bool ProceduralFallback => _proceduralFallback;

        public bool TryGet(SfxId id, out AudioClip clip, out float volume)
        {
            clip = null;
            volume = 1f;
            if (_entries == null)
            {
                return false;
            }

            for (var i = 0; i < _entries.Length; i++)
            {
                if (_entries[i].Id != id)
                {
                    continue;
                }

                clip = _entries[i].Clip;
                volume = _entries[i].Volume > 0f ? _entries[i].Volume : 1f;
                return clip != null;
            }

            return false;
        }
    }
}
