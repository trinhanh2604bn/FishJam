using System;
using System.Collections.Generic;
using FishPuzzle.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.TestTools;

namespace FishPuzzle.Tests.EditMode
{
    /// <summary>M14: presentation haptics and the +0.20 s fish route.</summary>
    public sealed class M14HapticRouteTests
    {
        private const string TuningPath = "Assets/Game/Data/Config/AnimationTuning.asset";

        [Test]
        public void Durations_AreShort_AndOrderedLightToStrong()
        {
            Assert.That(HapticFeedbackService.DurationMs(HapticKind.Touch), Is.InRange(10, 15));
            Assert.That(HapticFeedbackService.DurationMs(HapticKind.FishPress), Is.InRange(15, 20));
            Assert.That(HapticFeedbackService.DurationMs(HapticKind.TankLanding), Is.InRange(25, 35));
            Assert.That(HapticFeedbackService.DurationMs(HapticKind.BubblePop), Is.InRange(30, 40));
            Assert.That(HapticFeedbackService.DurationMs(HapticKind.TankComplete), Is.InRange(50, 70));
        }

        [Test]
        public void EditorService_IsSilentNoOp_AndNeverThrows()
        {
            var haptics = HapticFeedbackService.Create();
            Assert.That(haptics.HasDeviceOutput, Is.False, "Editor never vibrates.");
            var now = 0f;
            var clocked = new HapticFeedbackService(null, () => now);
            foreach (HapticKind kind in Enum.GetValues(typeof(HapticKind)))
            {
                Assert.DoesNotThrow(() => haptics.Play(kind));
                now += 1f;
                Assert.That(clocked.Play(kind), Is.True, kind.ToString());
            }

            Assert.That(haptics.DeviceVibrationCount, Is.EqualTo(0));
            Assert.That(clocked.DeviceVibrationCount, Is.EqualTo(0));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void RepeatedRequests_AreThrottled_ThenAllowedAfterCooldown()
        {
            var now = 10f;
            var pulses = new List<int>();
            var haptics = new HapticFeedbackService((ms, amplitude) => { pulses.Add(ms); return true; }, () => now);

            Assert.That(haptics.Play(HapticKind.Touch), Is.True);
            for (var i = 0; i < 10; i++)
            {
                now += 0.01f;
                Assert.That(haptics.Play(HapticKind.Touch), Is.False, "Spam inside the cooldown is dropped.");
            }

            now += HapticFeedbackService.CooldownSeconds(HapticKind.Touch);
            Assert.That(haptics.Play(HapticKind.Touch), Is.True);
            Assert.That(haptics.RequestCount(HapticKind.Touch), Is.EqualTo(12));
            Assert.That(haptics.PlayCount(HapticKind.Touch), Is.EqualTo(2));
            Assert.That(pulses, Is.EqualTo(new[] { 12, 12 }));
        }

        [Test]
        public void StrongerPulse_PassesTheGlobalGap_WeakerDoesNot()
        {
            var now = 5f;
            var haptics = new HapticFeedbackService((ms, amplitude) => true, () => now);

            Assert.That(haptics.Play(HapticKind.TankLanding), Is.True);
            now += 0.01f;
            Assert.That(haptics.Play(HapticKind.TankComplete), Is.True, "Tank 3/3 is not swallowed by the landing pulse.");
            now += 0.01f;
            Assert.That(haptics.Play(HapticKind.FishPress), Is.False, "Lighter pulse never cuts a strong one short.");
            Assert.That(haptics.LastPlayed, Is.EqualTo(HapticKind.TankComplete));
            Assert.That(haptics.DeviceVibrationCount, Is.EqualTo(2));
        }

        [Test]
        public void FailingDeviceOutput_IsSwallowed_AndTurnedOff()
        {
            var now = 0f;
            var calls = 0;
            var haptics = new HapticFeedbackService(
                (ms, amplitude) =>
                {
                    calls++;
                    throw new InvalidOperationException("no vibrator");
                },
                () => now);

            Assert.DoesNotThrow(() => haptics.Play(HapticKind.BubblePop));
            now += 1f;
            Assert.DoesNotThrow(() => haptics.Play(HapticKind.BubblePop));
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(haptics.HasDeviceOutput, Is.False);
            Assert.That(haptics.PlayCount(HapticKind.BubblePop), Is.EqualTo(2));
        }

        [Test]
        public void RouteDurations_AreCurrentValuesPlusTwentyHundredths()
        {
            var tuning = AssetDatabase.LoadAssetAtPath<AnimationTuning>(TuningPath);
            Assert.That(tuning, Is.Not.Null);
            Assert.That(tuning.FishRouteExtraDuration, Is.EqualTo(0.2f).Within(0.0001f));
            Assert.That(tuning.FishRouteBaseDuration, Is.EqualTo(0.42f).Within(0.0001f), "Bubble → Tank / Tray → Tank base unchanged.");
            Assert.That(tuning.TrayAutoMoveDuration, Is.EqualTo(0.21f).Within(0.0001f), "Bubble → Tray base unchanged.");
            Assert.That(tuning.FishRouteDuration, Is.EqualTo(0.62f).Within(0.0001f));
            Assert.That(tuning.TrayRouteDuration, Is.EqualTo(0.41f).Within(0.0001f));
            Assert.That(tuning.PresentationSafetySeconds, Is.GreaterThan(tuning.FishRouteDuration * 4f), "Watchdog keeps ample headroom.");

            var defaults = AnimationTuning.RuntimeDefault();
            Assert.That(defaults.FishRouteDuration, Is.EqualTo(defaults.FishRouteBaseDuration + 0.2f).Within(0.0001f));
            Assert.That(defaults.TrayRouteDuration, Is.EqualTo(defaults.TrayAutoMoveDuration + 0.2f).Within(0.0001f));
            UnityEngine.Object.DestroyImmediate(defaults);
        }
    }
}
