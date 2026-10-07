using FishPuzzle.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace FishPuzzle.Tests.EditMode
{
    /// <summary>M14.2: denser trail with a route density profile, and launch / glide / landing route pacing.</summary>
    public sealed class M142TrailPacingTests
    {
        [Test]
        public void Trail_DenserIntervals_AndBiggerPool()
        {
            Assert.That(FishTrailEmitter.MinInterval, Is.EqualTo(0.012f));
            Assert.That(FishTrailEmitter.MaxInterval, Is.EqualTo(0.022f));
            Assert.That(FishTrailEmitter.DefaultCap, Is.EqualTo(200));
            Assert.That(FishTrailEmitter.HardCap, Is.EqualTo(240));
            Assert.That(FishTrailEmitter.MinLifetime, Is.EqualTo(0.7f));
            Assert.That(FishTrailEmitter.MaxLifetime, Is.EqualTo(1.1f));
        }

        [Test]
        public void Trail_DensityProfile_LightStart_FullMiddle_TaperedEnd()
        {
            var start = FishTrailEmitter.RouteDensityFactor(0.05f);
            var middle = FishTrailEmitter.RouteDensityFactor(0.5f);
            var nearEnd = FishTrailEmitter.RouteDensityFactor(0.9f);
            var end = FishTrailEmitter.RouteDensityFactor(1f);
            Assert.That(middle, Is.EqualTo(1f));
            Assert.That(FishTrailEmitter.RouteDensityFactor(0.1f), Is.EqualTo(1f));
            Assert.That(FishTrailEmitter.RouteDensityFactor(0.8f), Is.EqualTo(1f));
            Assert.That(start, Is.GreaterThan(middle), "Light start.");
            Assert.That(nearEnd, Is.GreaterThan(middle).And.LessThan(end), "Slight taper toward the tank.");
            Assert.That(end, Is.LessThanOrEqualTo(1.6f), "Taper, not a gap.");
        }

        [Test]
        public void Trail_ExpectedVisibleCounts_MatchTargets()
        {
            var meanInterval = (FishTrailEmitter.MinInterval + FishTrailEmitter.MaxInterval) * 0.5f;
            var meanLife = (FishTrailEmitter.MinLifetime + FishTrailEmitter.MaxLifetime) * 0.5f;
            var tankVisible = meanLife / meanInterval;
            var trayVisible = meanLife / (meanInterval * FishTrailEmitter.TrayIntervalFactor);
            Assert.That(tankVisible, Is.InRange(40f, 65f), "Bubble → Tank steady-state visible bubbles.");
            Assert.That(trayVisible, Is.InRange(22f, 36f), "Bubble → Tray steady-state visible bubbles.");
            Assert.That(tankVisible, Is.LessThan(FishTrailEmitter.DefaultCap / 2f), "Two fish in flight still fit the pool.");
        }

        [Test]
        public void Trail_MixesSmallAndOccasionalMediumBubbles()
        {
            var parent = new GameObject("trail-parent", typeof(RectTransform));
            try
            {
                var trail = FishTrailEmitter.Create(parent.GetComponent<RectTransform>(), ProceduralVfxSprite.Dot);
                for (var i = 0; i < 50; i++)
                {
                    Assert.That(trail.Emit(new Vector3(i * 3f, 0f, 0f)), Is.True);
                }

                var small = 0;
                var medium = 0;
                foreach (var image in parent.GetComponentsInChildren<Image>(true))
                {
                    var scale = image.rectTransform.sizeDelta.x / 28f;
                    Assert.That(scale, Is.InRange(FishTrailEmitter.MinScale - 0.001f, FishTrailEmitter.MaxScale + 0.001f));
                    Assert.That(image.color.a, Is.InRange(FishTrailEmitter.MinAlpha - 0.001f, FishTrailEmitter.MaxAlpha + 0.001f));
                    Assert.That(image.raycastTarget, Is.False);
                    if (scale >= FishTrailEmitter.MediumMinScale)
                    {
                        medium++;
                    }
                    else
                    {
                        small++;
                    }
                }

                Assert.That(small, Is.GreaterThan(medium), "Mostly small bubbles.");
                Assert.That(medium, Is.GreaterThan(0), "Occasional medium bubble.");
            }
            finally
            {
                Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void RoutePace_LaunchesAtOnce_Glides_ThenLandsSnappily()
        {
            Assert.That(PresentationMotion.RoutePace(0f), Is.EqualTo(0f).Within(0.0001f));
            Assert.That(PresentationMotion.RoutePace(1f), Is.EqualTo(1f).Within(0.0001f));
            var previous = 0f;
            for (var i = 1; i <= 100; i++)
            {
                var value = PresentationMotion.RoutePace(i / 100f);
                Assert.That(value, Is.GreaterThan(previous), "Monotonic, never pauses.");
                previous = value;
            }

            Assert.That(Speed(0.005f), Is.GreaterThan(0.4f), "Fish moves on the first frame (no start delay).");
            Assert.That(Speed(0.14f), Is.GreaterThan(Speed(0.01f) * 1.6f), "Quick acceleration during launch.");
            Assert.That(Speed(0.95f), Is.GreaterThan(Speed(0.7f)), "Snappier landing than the glide.");
            Assert.That(Speed(0.995f), Is.GreaterThan(1f), "No ease-out float before the splash.");
        }

        private static float Speed(float t)
        {
            const float h = 0.005f;
            var a = Mathf.Clamp01(t - h);
            var b = Mathf.Clamp01(t + h);
            return (PresentationMotion.RoutePace(b) - PresentationMotion.RoutePace(a)) / (b - a);
        }
    }
}
