using UnityEngine;

namespace FishPuzzle.Presentation
{
    /// <summary>
    /// Shared motion math. A missing target or a finished clock always reports the authoritative end sample.
    /// </summary>
    public readonly struct MotionSample
    {
        public MotionSample(float t, bool completed)
        {
            T = t;
            Completed = completed;
        }

        public float T { get; }

        public bool Completed { get; }
    }

    public static class PresentationMotion
    {
        // Relative speed at each time knot, linear in between: quick launch (0–15%), readable glide (15–82%),
        // snappier entry (82–100%). Speed never reaches zero, so the fish moves on the first frame and lands without a float.
        private static readonly float[] PaceTimes = { 0f, 0.15f, 0.82f, 1f };
        private static readonly float[] PaceSpeeds = { 0.55f, 1.2f, 0.9f, 1.3f };
        public static MotionSample Sample(bool targetAlive, float elapsed, float duration)
        {
            if (!targetAlive || duration <= 0f || elapsed >= duration)
            {
                return new MotionSample(1f, true);
            }

            return new MotionSample(Mathf.Clamp01(elapsed / duration), false);
        }

        public static Vector3 QuadraticBezier(Vector3 start, Vector3 control, Vector3 end, float t)
        {
            var clamped = Mathf.Clamp01(t);
            var inverse = 1f - clamped;
            return (inverse * inverse * start) + (2f * inverse * clamped * control) + (clamped * clamped * end);
        }

        public static float EaseOutQuad(float t)
        {
            var clamped = Mathf.Clamp01(t);
            return 1f - ((1f - clamped) * (1f - clamped));
        }

        public static float EaseInQuad(float t)
        {
            var clamped = Mathf.Clamp01(t);
            return clamped * clamped;
        }

        public static float Hop(float t)
        {
            var clamped = Mathf.Clamp01(t);
            return clamped * clamped * (3f - (2f * clamped));
        }

        /// <summary>Route progress for a fish flying into a tank: launch, glide, snappy landing. 0 → 0 and 1 → 1.</summary>
        public static float RoutePace(float t)
        {
            var clamped = Mathf.Clamp01(t);
            var total = 0f;
            var reached = 0f;
            for (var i = 0; i < PaceTimes.Length - 1; i++)
            {
                var t0 = PaceTimes[i];
                var width = PaceTimes[i + 1] - t0;
                var v0 = PaceSpeeds[i];
                var v1 = PaceSpeeds[i + 1];
                total += width * (v0 + v1) * 0.5f;
                if (clamped <= t0)
                {
                    continue;
                }

                var span = Mathf.Min(clamped - t0, width);
                var speedAt = v0 + ((v1 - v0) * (span / width));
                reached += span * (v0 + speedAt) * 0.5f;
            }

            return total > 0f ? reached / total : clamped;
        }

        public static float PopScale(float t)
        {
            var clamped = Mathf.Clamp01(t);
            if (clamped < 0.34f)
            {
                return Mathf.Lerp(1f, 0.78f, clamped / 0.34f);
            }

            if (clamped < 0.68f)
            {
                return Mathf.Lerp(0.78f, 1.18f, (clamped - 0.34f) / 0.34f);
            }

            return Mathf.Lerp(1.18f, 0f, (clamped - 0.68f) / 0.32f);
        }

        public static Vector3 BounceScale(float t, float overshoot)
        {
            var wave = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);
            var stretch = 1f + (overshoot * wave);
            var squash = 1f - (overshoot * 0.65f * wave);
            return new Vector3(stretch, squash, 1f);
        }

        public static Vector2 FallSlide(Vector2 from, Vector2 to, float t, float arc)
        {
            if (t <= 0f)
            {
                return from;
            }

            if (t >= 1f)
            {
                return to;
            }

            var eased = EaseInQuad(t);
            var position = Vector2.Lerp(from, to, eased);
            if (Mathf.Abs(to.x - from.x) < 0.01f)
            {
                return position;
            }

            position.x += Mathf.Sin(eased * Mathf.PI) * arc * Mathf.Sign(to.x - from.x);
            return position;
        }
    }
}
