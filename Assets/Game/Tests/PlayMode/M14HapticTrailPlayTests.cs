using System.Collections;
using System.Collections.Generic;
using FishPuzzle.Core;
using FishPuzzle.Domain;
using FishPuzzle.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FishPuzzle.Tests.PlayMode
{
    /// <summary>M14: haptic requests on gameplay moments, a trail that stays behind the fish, and the slower route.</summary>
    public sealed class M14HapticTrailPlayTests
    {
        [UnitySetUp]
        public IEnumerator LoadGameplayScene()
        {
            LegacyLevelFixture.UseForNextSceneLoad();
            yield return SceneManager.LoadSceneAsync("Gameplay", LoadSceneMode.Single);
            yield return null;
            Canvas.ForceUpdateCanvases();
        }

        [UnityTest]
        public IEnumerator ScreenPointerDown_RequestsTouchHaptic()
        {
            var flow = Flow();
            var haptics = Bootstrap().Haptics;
            Assert.That(haptics, Is.Not.Null, "Bootstrapper injects the haptic service.");
            Assert.That(flow.Haptics, Is.SameAs(haptics));
            Assert.That(haptics.HasDeviceOutput, Is.False, "Editor never vibrates.");

            var before = haptics.RequestCount(HapticKind.Touch);
            flow.TouchFeedback.PresentPointerDown(new Vector2(200f, 200f));
            flow.TouchFeedback.PresentPointerUp();
            yield return null;

            Assert.That(haptics.RequestCount(HapticKind.Touch), Is.EqualTo(before + 1));
            Assert.That(haptics.DeviceVibrationCount, Is.EqualTo(0));
            Assert.That(flow.State, Is.EqualTo(GameState.PlayerInput), "Touch haptic never touches gameplay.");
            Assert.That(flow.AcceptedRouteCount, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator FishPress_ThenTankLanding_RequestHaptics_AndSlowerRouteReturnsToPlayerInput()
        {
            var flow = Flow();
            var haptics = Bootstrap().Haptics;
            var view = ViewFor(flow.Session.FindFirstIdleFish(FishType.Orange).Id);

            PointerGesture.Press(view);
            yield return null;
            Assert.That(haptics.RequestCount(HapticKind.FishPress), Is.EqualTo(1));
            Assert.That(haptics.RequestCount(HapticKind.TankLanding), Is.EqualTo(0));

            PointerGesture.ReleaseOver(view);
            var elapsed = 0f;
            var landedAt = -1f;
            while ((flow.IsPresentationBusy || flow.State != GameState.PlayerInput) && elapsed < 8f)
            {
                yield return null;
                elapsed += Time.unscaledDeltaTime;
                if (landedAt < 0f && flow.TankSplashCount > 0)
                {
                    landedAt = elapsed;
                }
            }

            Assert.That(flow.State, Is.EqualTo(GameState.PlayerInput), "Full turn returns to PlayerInput.");
            Assert.That(flow.IsPresentationBusy, Is.False);
            Assert.That(flow.Session.Tanks[0].FillCount, Is.EqualTo(1));
            Assert.That(flow.TankSplashCount, Is.EqualTo(1), "Tank splash still plays.");
            Assert.That(haptics.RequestCount(HapticKind.TankLanding), Is.EqualTo(1));
            Assert.That(haptics.PlayCount(HapticKind.TankLanding), Is.EqualTo(1));

            var tuning = flow.Tuning;
            Assert.That(tuning.FishRouteDuration, Is.EqualTo(tuning.FishRouteBaseDuration + 0.2f).Within(0.0001f));
            Assert.That(landedAt, Is.GreaterThanOrEqualTo(tuning.FishRouteDuration - 0.02f), "Flight lasts the slower route.");
            Assert.That(elapsed - landedAt, Is.LessThan(1.5f), "No dead wait after landing.");
        }

        [UnityTest]
        public IEnumerator BubblePop_AndTankComplete_RequestHaptics()
        {
            var flow = Flow();
            var bootstrap = Bootstrap();
            var haptics = bootstrap.Haptics;
            var audio = bootstrap.Audio;
            Assert.That(bootstrap.BubblePile.TryGetBubble(0, out var source), Is.True);
            var ids = new int[source.FishViews.Count];
            for (var i = 0; i < ids.Length; i++)
            {
                ids[i] = source.FishViews[i].FishId;
            }

            for (var i = 0; i < ids.Length; i++)
            {
                PointerGesture.Click(ViewFor(ids[i]));
                yield return WaitUntilReady(flow);
            }

            Assert.That(audio.PlayCount(SfxId.BubblePop), Is.GreaterThan(0), "Emptying a bubble pops it.");
            Assert.That(haptics.RequestCount(HapticKind.BubblePop), Is.EqualTo(audio.PlayCount(SfxId.BubblePop)));
            Assert.That(haptics.PlayCount(HapticKind.BubblePop), Is.GreaterThan(0));

            for (var i = 0; i < 3; i++)
            {
                var orange = flow.Session.FindFirstIdleFish(FishType.Orange);
                if (orange == null || flow.Session.Tanks[0].CurrentTarget != FishType.Orange)
                {
                    break;
                }

                PointerGesture.Click(ViewFor(orange.Id));
                yield return WaitUntilReady(flow);
            }

            Assert.That(audio.PlayCount(SfxId.TankComplete), Is.GreaterThan(0), "Orange tank reached 3/3.");
            Assert.That(haptics.RequestCount(HapticKind.TankComplete), Is.EqualTo(audio.PlayCount(SfxId.TankComplete)));
            Assert.That(haptics.PlayCount(HapticKind.TankComplete), Is.GreaterThan(0), "Tank 3/3 plays the strong pulse.");
            Assert.That(haptics.RequestCount(HapticKind.TankLanding), Is.EqualTo(audio.PlayCount(SfxId.TankLand)));
            Assert.That(flow.State, Is.EqualTo(GameState.PlayerInput));
        }

        [UnityTest]
        public IEnumerator TrailParticles_StayBehindTheMovingFish_AtDistinctPositions()
        {
            var flow = Flow();
            var trail = flow.Trail;
            trail.ResetCounters();
            var fish = ViewFor(flow.Session.FindFirstIdleFish(FishType.Orange).Id).transform;
            var root = trail.FxRoot;
            PointerGesture.Click(fish.GetComponent<FishView>());

            var hasFirst = false;
            var firstSpawn = Vector3.zero;
            var checkedBehind = false;
            var peak = 0;
            var elapsed = 0f;
            while ((flow.IsPresentationBusy || flow.State != GameState.PlayerInput) && elapsed < 8f)
            {
                yield return null;
                elapsed += Time.unscaledDeltaTime;
                if (flow.TankSplashCount == 0)
                {
                    peak = Mathf.Max(peak, trail.ActiveCount);
                }

                if (!hasFirst && trail.SpawnCount > 0 && trail.PoolSize > 0)
                {
                    hasFirst = true;
                    firstSpawn = root.InverseTransformPoint(trail.GetParticleWorldPosition(0));
                    continue;
                }

                if (!hasFirst || checkedBehind || flow.TankSplashCount > 0 || !trail.IsParticleActive(0))
                {
                    continue;
                }

                var fishLocal = root.InverseTransformPoint(fish.position);
                if (Vector2.Distance(fishLocal, firstSpawn) < 60f)
                {
                    continue;
                }

                checkedBehind = true;
                var particle = root.InverseTransformPoint(trail.GetParticleWorldPosition(0));
                Assert.That(trail.GetParticleParent(0), Is.SameAs(root), "Trail bubble lives on the FX root.");
                Assert.That(trail.GetParticleParent(0), Is.Not.SameAs(fish));
                Assert.That(Vector2.Distance(particle, fishLocal), Is.GreaterThan(40f), "Old bubble stays behind the fish.");
                Assert.That(Mathf.Abs(particle.x - firstSpawn.x), Is.LessThan(12f), "Bubble does not follow the fish sideways.");
                Assert.That(particle.y, Is.GreaterThanOrEqualTo(firstSpawn.y - 0.5f), "Bubble floats upward.");
            }

            Assert.That(checkedBehind, Is.True, "The fish moved away while its first trail bubble was alive.");
            var samples = new List<Vector2>();
            trail.CopySpawnPositions(samples);
            Assert.That(FishTrailEmitter.SamplesSpanRoute(samples, 20f), Is.True, "spawns " + samples.Count);
            Assert.That(peak, Is.InRange(4, 10), "About 5–9 trail bubbles visible during Bubble → Tank flight (landing burst excluded).");
            Assert.That(trail.AllRaycastsDisabled(), Is.True);
            Assert.That(trail.AllParentedToFxRoot(), Is.True);
            Assert.That(flow.State, Is.EqualTo(GameState.PlayerInput));
        }

        private static IEnumerator WaitUntilReady(GameFlowController flow)
        {
            var elapsed = 0f;
            while (flow.IsPresentationBusy
                || (flow.State != GameState.PlayerInput && flow.State != GameState.Win && flow.State != GameState.Lose))
            {
                elapsed += Time.unscaledDeltaTime;
                if (elapsed > 8f)
                {
                    Assert.Fail("Resolution did not finish. State " + flow.State + " busy " + flow.IsPresentationBusy);
                }

                yield return null;
            }
        }

        private static FishView ViewFor(int fishId)
        {
            var views = Object.FindObjectsByType<FishView>(FindObjectsInactive.Exclude);
            for (var i = 0; i < views.Length; i++)
            {
                if (views[i] != null && views[i].FishId == fishId)
                {
                    return views[i];
                }
            }

            Assert.Fail("Missing FishView " + fishId);
            return null;
        }

        private static GameFlowController Flow()
        {
            var flow = Bootstrap().Flow;
            Assert.That(flow, Is.Not.Null);
            Assert.That(flow.Session, Is.Not.Null);
            return flow;
        }

        private static LevelSceneBootstrapper Bootstrap()
        {
            var bootstrap = Object.FindAnyObjectByType<LevelSceneBootstrapper>();
            Assert.That(bootstrap, Is.Not.Null);
            Assert.That(bootstrap.IsInitialized, Is.True);
            return bootstrap;
        }
    }
}
