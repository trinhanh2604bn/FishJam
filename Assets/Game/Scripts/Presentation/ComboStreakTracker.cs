using UnityEngine;

namespace FishPuzzle.Presentation
{
    public enum ComboTier
    {
        None = 0,
        Good = 1,
        Great = 2,
        Excellent = 3,
        Amazing = 4,
        WellDone = 5,
    }

    /// <summary>
    /// Presentation-only tank completion streak. It never touches score, gold, targets or difficulty.
    /// A completion within <see cref="Window"/> seconds of the previous one extends the streak; otherwise it restarts at 1.
    /// </summary>
    public sealed class ComboStreakTracker
    {
        public const float DefaultWindowSeconds = 3f;

        private readonly float _window;
        private float _lastCompletion;
        private int _streak;

        public ComboStreakTracker(float windowSeconds = DefaultWindowSeconds)
        {
            _window = windowSeconds > 0f ? windowSeconds : DefaultWindowSeconds;
        }

        public float Window => _window;

        public int Streak => _streak;

        public ComboTier Tier => TierFor(_streak);

        public ComboTier RegisterCompletion(float now)
        {
            if (_streak > 0 && now - _lastCompletion > _window)
            {
                _streak = 0;
            }

            _streak++;
            _lastCompletion = now;
            return TierFor(_streak);
        }

        /// <summary>Resets the streak when the window has passed. Returns true when it expired on this call.</summary>
        public bool Expire(float now)
        {
            if (_streak == 0 || now - _lastCompletion <= _window)
            {
                return false;
            }

            _streak = 0;
            return true;
        }

        public void Reset()
        {
            _streak = 0;
            _lastCompletion = 0f;
        }

        public static ComboTier TierFor(int streak)
        {
            if (streak <= 0)
            {
                return ComboTier.None;
            }

            return streak >= (int)ComboTier.WellDone ? ComboTier.WellDone : (ComboTier)streak;
        }

        public static string Label(ComboTier tier)
        {
            switch (tier)
            {
                case ComboTier.Good:
                    return "GOOD!";
                case ComboTier.Great:
                    return "GREAT!";
                case ComboTier.Excellent:
                    return "EXCELLENT!";
                case ComboTier.Amazing:
                    return "AMAZING!";
                case ComboTier.WellDone:
                    return "WELL DONE!";
                default:
                    return string.Empty;
            }
        }

        /// <summary>Combo cue pitch: 1.00, 1.08, 1.16, 1.24, 1.32.</summary>
        public static float Pitch(ComboTier tier)
        {
            var step = Mathf.Clamp((int)tier, 1, 5) - 1;
            return 1f + (0.08f * step);
        }

        /// <summary>Relative text size per tier. Higher tiers read slightly larger.</summary>
        public static float Scale(ComboTier tier)
        {
            var step = Mathf.Clamp((int)tier, 1, 5) - 1;
            return 1f + (0.08f * step);
        }

        public static Color Face(ComboTier tier)
        {
            switch (tier)
            {
                case ComboTier.Great:
                    return new Color(0.80f, 1f, 0.42f, 1f);
                case ComboTier.Excellent:
                    return new Color(1f, 0.86f, 0.25f, 1f);
                case ComboTier.Amazing:
                    return new Color(1f, 0.66f, 0.18f, 1f);
                case ComboTier.WellDone:
                    return new Color(1f, 0.93f, 0.55f, 1f);
                default:
                    return new Color(0.80f, 0.94f, 1f, 1f);
            }
        }

        public static Color Outline(ComboTier tier)
        {
            switch (tier)
            {
                case ComboTier.Great:
                    return new Color(0.10f, 0.45f, 0.12f, 1f);
                case ComboTier.Excellent:
                    return new Color(0.55f, 0.32f, 0.02f, 1f);
                case ComboTier.Amazing:
                    return new Color(0.62f, 0.20f, 0.02f, 1f);
                case ComboTier.WellDone:
                    return new Color(0.58f, 0.30f, 0.02f, 1f);
                default:
                    return new Color(0.06f, 0.26f, 0.58f, 1f);
            }
        }

        /// <summary>Burst sparkle count per tier (stronger burst for higher streaks).</summary>
        public static int BurstCount(ComboTier tier)
        {
            return 3 + (2 * Mathf.Clamp((int)tier, 1, 5));
        }
    }
}
