using System.Collections.Generic;
using UnityEngine;

namespace FishPuzzle.Presentation
{
    /// <summary>
    /// Soft generated placeholder tones for empty SFX slots. Original and development only.
    /// Each clip is synthesized once and cached. Replace with authored clips in <see cref="GameAudioCatalog"/>.
    /// </summary>
    public static class ProceduralSfx
    {
        private const int SampleRate = 44100;

        private static readonly Dictionary<SfxId, AudioClip> Cache = new Dictionary<SfxId, AudioClip>();

        private struct Note
        {
            public float Start;
            public float Duration;
            public float FromHz;
            public float ToHz;
            public float Gain;
            public float Noise;
            public float Harmonic;

            public Note(float start, float duration, float fromHz, float toHz, float gain, float noise = 0f, float harmonic = 0.18f)
            {
                Start = start;
                Duration = duration;
                FromHz = fromHz;
                ToHz = toHz;
                Gain = gain;
                Noise = noise;
                Harmonic = harmonic;
            }
        }

        public static AudioClip Get(SfxId id)
        {
            if (Cache.TryGetValue(id, out var cached) && cached != null)
            {
                return cached;
            }

            var notes = Recipe(id);
            if (notes == null || notes.Length == 0)
            {
                return null;
            }

            var clip = Synthesize("sfx_dev_" + id, notes);
            Cache[id] = clip;
            return clip;
        }

        private static Note[] Recipe(SfxId id)
        {
            switch (id)
            {
                case SfxId.FishPress:
                    // small "bloop"
                    return new[] { new Note(0f, 0.10f, 520f, 330f, 0.55f) };
                case SfxId.FishLaunch:
                    // short rising "whoop"
                    return new[] { new Note(0f, 0.15f, 360f, 860f, 0.45f, 0.02f) };
                case SfxId.FishFly:
                    return new[] { new Note(0f, 0.06f, 900f, 1150f, 0.18f), new Note(0.07f, 0.05f, 1000f, 1300f, 0.14f) };
                case SfxId.TankLand:
                    // water "plop/splash"
                    return new[] { new Note(0f, 0.14f, 420f, 160f, 0.6f, 0.10f), new Note(0.03f, 0.18f, 1600f, 900f, 0.10f, 0.25f, 0f) };
                case SfxId.TrayLand:
                    return new[] { new Note(0f, 0.10f, 300f, 210f, 0.45f, 0.03f) };
                case SfxId.TankFill:
                    return new[] { new Note(0f, 0.07f, 880f, 880f, 0.28f), new Note(0.06f, 0.10f, 1175f, 1175f, 0.24f) };
                case SfxId.TankComplete:
                    // bright chime
                    return new[]
                    {
                        new Note(0f, 0.30f, 1047f, 1047f, 0.34f, 0f, 0.3f),
                        new Note(0.07f, 0.30f, 1319f, 1319f, 0.30f, 0f, 0.3f),
                        new Note(0.14f, 0.42f, 1568f, 1568f, 0.30f, 0f, 0.3f),
                    };
                case SfxId.BubblePop:
                    // soft wet "pop"
                    return new[] { new Note(0f, 0.06f, 700f, 1500f, 0.5f, 0.06f), new Note(0.01f, 0.08f, 2200f, 1400f, 0.08f, 0.3f, 0f) };
                case SfxId.BubbleSettle:
                    return new[] { new Note(0f, 0.08f, 210f, 150f, 0.30f, 0.02f) };
                case SfxId.TopSpawn:
                    return new[] { new Note(0f, 0.12f, 480f, 720f, 0.22f) };
                case SfxId.TouchRipple:
                    return new[] { new Note(0f, 0.07f, 820f, 600f, 0.14f, 0.05f) };
                case SfxId.ButtonTap:
                    return new[] { new Note(0f, 0.05f, 1100f, 900f, 0.32f) };
                case SfxId.UnlockTank:
                    return new[]
                    {
                        new Note(0f, 0.14f, 784f, 784f, 0.30f),
                        new Note(0.09f, 0.14f, 988f, 988f, 0.30f),
                        new Note(0.18f, 0.30f, 1319f, 1319f, 0.30f, 0f, 0.3f),
                    };
                case SfxId.InsufficientGold:
                    return new[] { new Note(0f, 0.12f, 262f, 250f, 0.32f, 0f, 0.35f), new Note(0.14f, 0.18f, 220f, 200f, 0.32f, 0f, 0.35f) };
                case SfxId.Lose:
                    // short descending tone
                    return new[]
                    {
                        new Note(0f, 0.22f, 523f, 500f, 0.34f),
                        new Note(0.20f, 0.22f, 440f, 420f, 0.32f),
                        new Note(0.40f, 0.42f, 349f, 300f, 0.30f),
                    };
                case SfxId.Win:
                    // bright celebratory jingle
                    return new[]
                    {
                        new Note(0f, 0.14f, 784f, 784f, 0.30f, 0f, 0.3f),
                        new Note(0.12f, 0.14f, 988f, 988f, 0.30f, 0f, 0.3f),
                        new Note(0.24f, 0.14f, 1175f, 1175f, 0.30f, 0f, 0.3f),
                        new Note(0.36f, 0.50f, 1568f, 1568f, 0.32f, 0f, 0.35f),
                        new Note(0.36f, 0.50f, 1175f, 1175f, 0.16f, 0f, 0.2f),
                    };
                case SfxId.Combo:
                    // pitch is raised per tier by the player
                    return new[] { new Note(0f, 0.22f, 1319f, 1319f, 0.30f, 0f, 0.3f), new Note(0.06f, 0.30f, 1976f, 1976f, 0.20f, 0f, 0.2f) };
                default:
                    return null;
            }
        }

        private static AudioClip Synthesize(string name, Note[] notes)
        {
            var length = 0f;
            for (var i = 0; i < notes.Length; i++)
            {
                length = Mathf.Max(length, notes[i].Start + notes[i].Duration);
            }

            var count = Mathf.Max(1, Mathf.CeilToInt((length + 0.02f) * SampleRate));
            var data = new float[count];
            var seed = (uint)name.GetHashCode() | 1u;
            for (var n = 0; n < notes.Length; n++)
            {
                var note = notes[n];
                var first = Mathf.FloorToInt(note.Start * SampleRate);
                var samples = Mathf.CeilToInt(note.Duration * SampleRate);
                var phase = 0.0;
                for (var s = 0; s < samples && first + s < count; s++)
                {
                    var t = s / (float)SampleRate;
                    var u = t / note.Duration;
                    var hz = Mathf.Lerp(note.FromHz, note.ToHz, u * (2f - u));
                    phase += 2.0 * Mathf.PI * hz / SampleRate;
                    var attack = Mathf.Clamp01(t / 0.006f);
                    var decay = Mathf.Exp(-4.2f * u) * (1f - (u * u));
                    var tone = Mathf.Sin((float)phase) + (note.Harmonic * Mathf.Sin((float)(phase * 2.0)));
                    seed ^= seed << 13;
                    seed ^= seed >> 17;
                    seed ^= seed << 5;
                    var noise = ((seed & 0xFFFF) / 32767.5f) - 1f;
                    data[first + s] += note.Gain * attack * decay * ((tone * (1f - note.Noise)) + (noise * note.Noise));
                }
            }

            var peak = 0f;
            for (var i = 0; i < count; i++)
            {
                peak = Mathf.Max(peak, Mathf.Abs(data[i]));
            }

            if (peak > 0.9f)
            {
                var scale = 0.9f / peak;
                for (var i = 0; i < count; i++)
                {
                    data[i] *= scale;
                }
            }

            var clip = AudioClip.Create(name, count, 1, SampleRate, false);
            clip.SetData(data, 0);
            clip.hideFlags = HideFlags.HideAndDontSave;
            return clip;
        }
    }
}
