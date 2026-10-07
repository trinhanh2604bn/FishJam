using System.Collections;
using System.Collections.Generic;
using System.IO;
using FishPuzzle.Core;
using FishPuzzle.Domain;
using FishPuzzle.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace FishPuzzle.Tests.PlayMode
{
    /// <summary>M13 juice: press highlight, fish trail, landing FX, audio and the presentation-only combo streak.</summary>
    public sealed class M13JuicePlayTests
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
        public IEnumerator HoldFish_ShowsHighlight_ReleaseRemovesIt_AndRoutes()
        {
            var flow = Flow();
            var audio = Bootstrap().Audio;
            Assert.That(audio, Is.Not.Null, "Bootstrapper injects the audio service.");
            Assert.That(flow.Audio, Is.SameAs(audio));
            var view = ViewFor(flow.Session.FindFirstIdleFish(FishType.Orange).Id);

            PointerGesture.Press(view);
            yield return null;
            Assert.That(view.IsHighlighted, Is.True);
            Assert.That(view.Highlight.HasOutline, Is.True);
            Assert.That(view.PressScale, Is.InRange(1.03f, 1.09f));
            Assert.That(audio.PlayCount(SfxId.FishPress), Is.EqualTo(1));
            AssertNoPressGraphicRaycasts(view);

            PointerGesture.ReleaseOver(view);
            Assert.That(view.IsHighlighted, Is.False);
            yield return WaitUntilReady(flow);
            Assert.That(flow.AcceptedRouteCount, Is.EqualTo(1));
            Assert.That(audio.PlayCount(SfxId.FishLaunch), Is.EqualTo(1));
            Assert.That(flow.Session.Tanks[0].FillCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator CancelledHold_RemovesHighlight_AndDoesNotRoute()
        {
            var flow = Flow();
            var view = ViewFor(flow.Session.FindFirstIdleFish(FishType.Orange).Id);
            PointerGesture.Press(view);
            yield return null;
            Assert.That(view.IsHighlighted, Is.True);
            UpAt(view, new Vector2(4000f, 4000f));
            yield return null;

            Assert.That(view.IsHighlighted, Is.False);
            Assert.That(view.IsPressed, Is.False);
            Assert.That(view.transform.Find("Visual").localScale.x, Is.LessThan(1.03f), "Hold scale removed (idle sway may breathe slightly).");
            Assert.That(flow.AcceptedRouteCount, Is.EqualTo(0));
            Assert.That(flow.State, Is.EqualTo(GameState.PlayerInput));
        }

        [UnityTest]
        public IEnumerator FishRoute_EmitsTrail_WithoutTouchingGameplay()
        {
            var flow = Flow();
            Assert.That(flow.Trail, Is.Not.Null);
            var fish = flow.Session.FindFirstIdleFish(FishType.Orange);
            var peak = 0;
            PointerGesture.Click(ViewFor(fish.Id));
            var elapsed = 0f;
            while ((flow.IsPresentationBusy || flow.State != GameState.PlayerInput) && elapsed < 8f)
            {
                peak = Mathf.Max(peak, flow.Trail.ActiveCount);
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.That(flow.TrailEmitCount, Is.GreaterThanOrEqualTo(4), "Continuous bubbles along the Bubble → Tank path.");
            Assert.That(peak, Is.GreaterThan(0));
            Assert.That(peak, Is.LessThanOrEqualTo(flow.Trail.Cap));
            Assert.That(flow.Trail.AllRaycastsDisabled(), Is.True);
            Assert.That(flow.Session.Tanks[0].FillCount, Is.EqualTo(1));
            Assert.That(flow.Session.CountPlacements(fish.Id), Is.EqualTo(1));

            var tankTrail = flow.TrailEmitCount;
            var pink = flow.Session.FindFirstIdleFish(FishType.PinkStriped);
            PointerGesture.Click(ViewFor(pink.Id));
            yield return WaitUntilReady(flow);
            var trayTrail = flow.TrailEmitCount - tankTrail;
            Assert.That(trayTrail, Is.GreaterThanOrEqualTo(1), "Bubble → Tray also trails.");
            Assert.That(trayTrail, Is.LessThan(tankTrail), "Bubble → Tray uses fewer particles.");
            Assert.That(flow.Session.Tray.Count, Is.EqualTo(1));
            Assert.That(flow.State, Is.EqualTo(GameState.PlayerInput));
        }

        [UnityTest]
        public IEnumerator BubbleToTank_TrailSamplesDistinctPositionsAlongTheRoute()
        {
            var flow = Flow();
            flow.Trail.ResetCounters();
            var fish = flow.Session.FindFirstIdleFish(FishType.Orange);
            PointerGesture.Click(ViewFor(fish.Id));

            var savedFrames = 0;
            var bestActive = 0;
            var elapsed = 0f;
            while ((flow.IsPresentationBusy || flow.State != GameState.PlayerInput) && elapsed < 8f)
            {
                if (flow.Trail.ActiveCount > bestActive)
                {
                    bestActive = flow.Trail.ActiveCount;
                }

                if (savedFrames < 3 && flow.Trail.SpawnCount >= 3 && flow.Trail.ActiveCount >= 2)
                {
                    var path = savedFrames == 1 ? "Logs/M131_FishTrail.png" : "Logs/M131_FishTrail_" + savedFrames + ".png";
                    if (SaveTrailFrame(path))
                    {
                        savedFrames++;
                    }
                }

                yield return null;

                elapsed += Time.unscaledDeltaTime;
            }

            var samples = new List<Vector2>();
            flow.Trail.CopySpawnPositions(samples);
            Assert.That(samples.Count, Is.GreaterThanOrEqualTo(4), "Bubble → Tank emits several bubbles, not one launch burst.");
            Assert.That(FishTrailEmitter.SamplesSpanRoute(samples, 20f), Is.True, DescribeSpawns(samples));
            Assert.That(flow.Trail.AllParentedToFxRoot(), Is.True);
            Assert.That(flow.Trail.name, Is.EqualTo(FishTrailEmitter.RootName));
            Assert.That(flow.TankSplashCount, Is.EqualTo(1));
            Assert.That(flow.Session.Tanks[0].FillCount, Is.EqualTo(1));
            Assert.That(flow.State, Is.EqualTo(GameState.PlayerInput));
            Assert.That(bestActive, Is.GreaterThanOrEqualTo(2));
        }

        [UnityTest]
        public IEnumerator TrayToTank_PromotionTrailSamplesDistinctPositions()
        {
            var flow = Flow();
            for (var i = 0; i < 3; i++)
            {
                PointerGesture.Click(ViewFor(flow.Session.FindFirstIdleFish(FishType.RedClown).Id));
                yield return WaitUntilReady(flow);
            }

            Assert.That(flow.Session.Tray.Count, Is.EqualTo(3));
            PointerGesture.Click(ViewFor(flow.Session.FindFirstIdleFish(FishType.Orange).Id));
            yield return WaitUntilReady(flow);
            PointerGesture.Click(ViewFor(flow.Session.FindFirstIdleFish(FishType.Orange).Id));
            yield return WaitUntilReady(flow);
            Assert.That(flow.Session.Tanks[0].FillCount, Is.EqualTo(2));

            flow.Trail.ResetCounters();
            var splashBefore = flow.TankSplashCount;
            PointerGesture.Click(ViewFor(flow.Session.FindFirstIdleFish(FishType.Orange).Id));
            var sawOrangeLanding = false;
            var mark = -1;
            var elapsed = 0f;
            while ((flow.IsPresentationBusy || flow.State != GameState.PlayerInput) && elapsed < 8f)
            {
                if (!sawOrangeLanding && flow.TankSplashCount > splashBefore && flow.Trail.SpawnCount >= 3)
                {
                    sawOrangeLanding = true;
                    mark = flow.Trail.SpawnCount;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.That(flow.State, Is.EqualTo(GameState.PlayerInput));
            Assert.That(flow.Session.Tray.Count, Is.EqualTo(0), "Waiting fish auto-promote into the tank.");
            var samples = new List<Vector2>();
            flow.Trail.CopySpawnPositions(samples);
            Assert.That(samples.Count, Is.GreaterThanOrEqualTo(4), DescribeSpawns(samples));
            Assert.That(mark, Is.GreaterThanOrEqualTo(3), "Bubble → Tank portion of the completing flight sampled the path.");
            var orange = new List<Vector2>();
            for (var i = 0; i < mark && i < samples.Count; i++)
            {
                orange.Add(samples[i]);
            }

            Assert.That(FishTrailEmitter.SamplesSpanRoute(orange, 20f), Is.True, "Bubble → Tank " + DescribeSpawns(orange));
            var promotion = new List<Vector2>();
            for (var i = mark; i < samples.Count; i++)
            {
                promotion.Add(samples[i]);
            }

            Assert.That(promotion.Count, Is.GreaterThanOrEqualTo(3), "Tray → Tank " + DescribeSpawns(promotion));
            var farthest = 0f;
            for (var i = 0; i < promotion.Count; i++)
            {
                for (var j = i + 1; j < promotion.Count; j++)
                {
                    farthest = Mathf.Max(farthest, Vector2.Distance(promotion[i], promotion[j]));
                }
            }

            Assert.That(farthest, Is.GreaterThan(20f), "Tray → Tank bubbles are spread along the route. " + DescribeSpawns(promotion));
        }

        [UnityTest]
        public IEnumerator CorrectTankLanding_PlaysSplashAndSound_TrayLandingDoesNot()
        {
            var flow = Flow();
            var audio = Bootstrap().Audio;
            PointerGesture.Click(ViewFor(flow.Session.FindFirstIdleFish(FishType.Orange).Id));
            yield return WaitUntilReady(flow);

            Assert.That(flow.LandingFxCount, Is.EqualTo(1));
            Assert.That(flow.TankSplashCount, Is.EqualTo(1));
            Assert.That(audio.PlayCount(SfxId.TankLand), Is.EqualTo(1));
            Assert.That(audio.PlayCount(SfxId.TankFill), Is.EqualTo(1));
            Assert.That(flow.Trail.BurstCount, Is.EqualTo(1), "Short bubble burst around the landed fish.");

            PointerGesture.Click(ViewFor(flow.Session.FindFirstIdleFish(FishType.PinkStriped).Id));
            yield return WaitUntilReady(flow);
            Assert.That(flow.LandingFxCount, Is.EqualTo(1));
            Assert.That(audio.PlayCount(SfxId.TrayLand), Is.EqualTo(1));
            Assert.That(flow.ComboStreak, Is.EqualTo(0), "No combo for ordinary fish insertion.");
            Assert.That(flow.ComboView == null || flow.ComboView.ShownCount == 0, Is.True);
        }

        [UnityTest]
        public IEnumerator ThirdFish_StillPlaysLandingFx_ThenCompletes_WithGoodCombo()
        {
            var flow = Flow();
            var audio = Bootstrap().Audio;
            flow.SetPresentationClock(() => 100f);
            for (var i = 0; i < 3; i++)
            {
                PointerGesture.Click(ViewFor(flow.Session.FindFirstIdleFish(FishType.Orange).Id));
                yield return WaitUntilReady(flow);
            }

            Assert.That(flow.LandingFxCount, Is.EqualTo(3), "The third fish keeps its landing FX.");
            Assert.That(flow.TankSplashCount, Is.EqualTo(3));
            Assert.That(audio.PlayCount(SfxId.TankLand), Is.EqualTo(3));
            Assert.That(audio.PlayCount(SfxId.TankComplete), Is.EqualTo(1));
            Assert.That(audio.PlayCount(SfxId.Combo), Is.EqualTo(1));
            Assert.That(flow.Session.Progress.CollectedFishCount, Is.EqualTo(3));
            Assert.That(flow.ComboStreak, Is.EqualTo(1));
            Assert.That(flow.LastComboTier, Is.EqualTo(ComboTier.Good));
            Assert.That(flow.ComboView, Is.Not.Null);
            Assert.That(flow.ComboView.LastText, Is.EqualTo("GOOD!"));
            AssertComboBelowModals(flow);
            foreach (var graphic in flow.ComboView.GetComponentsInChildren<Graphic>(true))
            {
                Assert.That(graphic.raycastTarget, Is.False, graphic.name);
            }
        }

        [UnityTest]
        public IEnumerator AutoPromotionCompletion_IncrementsStreak()
        {
            var flow = Flow();
            flow.SetPresentationClock(() => 100f);
            for (var i = 0; i < 3; i++)
            {
                PointerGesture.Click(ViewFor(flow.Session.FindFirstIdleFish(FishType.RedClown).Id));
                yield return WaitUntilReady(flow);
            }

            Assert.That(flow.Session.Tray.Count, Is.EqualTo(3));
            for (var i = 0; i < 3; i++)
            {
                PointerGesture.Click(ViewFor(flow.Session.FindFirstIdleFish(FishType.Orange).Id));
                yield return WaitUntilReady(flow);
            }

            Assert.That(flow.State, Is.EqualTo(GameState.PlayerInput));
            Assert.That(flow.Session.Tray.Count, Is.EqualTo(0), "Red clowns promoted into the new target.");
            Assert.That(flow.PromotionCompletionCount, Is.EqualTo(1));
            Assert.That(flow.ComboStreak, Is.EqualTo(2), "Cascade completion chains the streak.");
            Assert.That(flow.LastComboTier, Is.EqualTo(ComboTier.Great));
            Assert.That(flow.ComboView.LastText, Is.EqualTo("GREAT!"));
            Assert.That(Bootstrap().Audio.LastPitch, Is.EqualTo(1f).Or.GreaterThan(1f));
            Assert.That(flow.Session.Progress.CollectedFishCount, Is.EqualTo(6), "Combo never changes the score.");
        }

        [UnityTest]
        public IEnumerator Combo_ExpiresAfterTimeout()
        {
            var flow = Flow();
            var now = 100f;
            flow.SetPresentationClock(() => now);
            for (var i = 0; i < 3; i++)
            {
                PointerGesture.Click(ViewFor(flow.Session.FindFirstIdleFish(FishType.Orange).Id));
                yield return WaitUntilReady(flow);
            }

            Assert.That(flow.ComboStreak, Is.EqualTo(1));
            now = 102.9f;
            yield return null;
            Assert.That(flow.ComboStreak, Is.EqualTo(1));
            now = 103.2f;
            yield return null;
            Assert.That(flow.ComboStreak, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator Lose_ThenRetry_ResetCombo()
        {
            var bootstrap = Bootstrap();
            var flow = Flow();
            var audio = bootstrap.Audio;
            flow.SetPresentationClock(() => 100f);
            for (var i = 0; i < 3; i++)
            {
                PointerGesture.Click(ViewFor(flow.Session.FindFirstIdleFish(FishType.Orange).Id));
                yield return WaitUntilReady(flow);
            }

            Assert.That(flow.ComboStreak, Is.EqualTo(1));
            for (var i = 0; i < 5; i++)
            {
                PointerGesture.Click(ViewFor(flow.Session.FindFirstIdleFish(FishType.PinkStriped).Id));
                yield return WaitUntilReady(flow);
            }

            Assert.That(flow.State, Is.EqualTo(GameState.Lose));
            Assert.That(flow.ComboStreak, Is.EqualTo(0), "Lose resets the combo.");
            Assert.That(audio.PlayCount(SfxId.Lose), Is.EqualTo(1));
            Assert.That(audio.PlayCount(SfxId.Win), Is.EqualTo(0));
            flow.PresentCurrentOutcome();
            Assert.That(audio.PlayCount(SfxId.Lose), Is.EqualTo(1), "Lose sound plays once per attempt.");

            var resets = flow.ComboResetCount;
            bootstrap.RetryAttempt();
            yield return null;
            var retried = Flow();
            Assert.That(retried.State, Is.EqualTo(GameState.PlayerInput));
            Assert.That(retried.ComboStreak, Is.EqualTo(0));
            Assert.That(retried.ComboResetCount, Is.GreaterThan(resets), "Retry resets the combo.");
            Assert.That(retried.LastComboTier, Is.EqualTo(ComboTier.None));
            Assert.That(retried.LandingFxCount, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator MissingAudioAndVfx_CannotSoftLock()
        {
            var flow = Flow();
            flow.ConfigureFeedback(null);
            flow.SuppressJuiceVfx();
            flow.SuppressBubblePopVfx();
            flow.SuppressLandingAndTouchVfx();
            var pile = Bootstrap().BubblePile;
            Assert.That(pile.TryGetBubble(0, out var source), Is.True);
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

            Assert.That(flow.State, Is.EqualTo(GameState.PlayerInput));
            Assert.That(flow.IsPresentationBusy, Is.False);
            Assert.That(flow.TrailEmitCount, Is.EqualTo(0));
            Assert.That(flow.Session.Progress.CollectedFishCount, Is.GreaterThanOrEqualTo(3));
            Assert.That(flow.ComboView == null || flow.ComboView.ActiveLabelCount == 0, Is.True);

            var muted = Bootstrap().Audio;
            muted.Configure(null, false);
            flow.ConfigureFeedback(muted);
            var next = flow.Session.FindFirstIdleFish(FishType.GreenStriped);
            PointerGesture.Click(ViewFor(next.Id));
            yield return WaitUntilReady(flow);
            Assert.That(flow.State, Is.EqualTo(GameState.PlayerInput));
            Assert.That(flow.Session.CountPlacements(next.Id), Is.EqualTo(1));
        }

        // ---------- helpers ----------

        private static void AssertComboBelowModals(GameFlowController flow)
        {
            var layer = flow.ComboView.transform;
            var overlay = layer.parent != null ? layer.parent.Find("OverlayRoot") : null;
            Assert.That(overlay, Is.Not.Null);
            Assert.That(layer.GetSiblingIndex(), Is.LessThan(overlay.GetSiblingIndex()), "Modals stay above combo text.");
        }

        private static bool SaveTrailFrame(string relativePath)
        {
            var trail = Object.FindAnyObjectByType<FishTrailEmitter>();
            var canvas = trail != null ? trail.GetComponentInParent<Canvas>() : null;
            if (canvas == null)
            {
                return false;
            }

            var previousMode = canvas.renderMode;
            var previousCamera = canvas.worldCamera;
            var previousPlane = canvas.planeDistance;
            GameObject cameraObject = null;
            RenderTexture target = null;
            Texture2D texture = null;
            var previousActive = RenderTexture.active;
            try
            {
                cameraObject = new GameObject("TrailCaptureCamera");
                var camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.53f, 0.78f, 0.93f, 1f);
                camera.orthographic = true;
                camera.orthographicSize = 9.6f;
                camera.nearClipPlane = 0.3f;
                camera.farClipPlane = 1000f;
                camera.transform.position = new Vector3(0f, 0f, -100f);
                target = new RenderTexture(540, 960, 24);
                camera.targetTexture = target;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 100f;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                texture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                texture.Apply();
                var path = Path.Combine(Directory.GetCurrentDirectory(), relativePath);
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllBytes(path, texture.EncodeToPNG());
                return true;
            }
            catch (System.Exception)
            {
                return false;
            }
            finally
            {
                RenderTexture.active = previousActive;
                canvas.renderMode = previousMode;
                canvas.worldCamera = previousCamera;
                canvas.planeDistance = previousPlane;
                if (cameraObject != null)
                {
                    Object.Destroy(cameraObject);
                }

                if (target != null)
                {
                    target.Release();
                    Object.Destroy(target);
                }

                if (texture != null)
                {
                    Object.Destroy(texture);
                }
            }
        }

        private static string DescribeSpawns(List<Vector2> samples)
        {
            var text = "spawns " + samples.Count;
            var limit = samples.Count < 8 ? samples.Count : 8;
            for (var i = 0; i < limit; i++)
            {
                text += " (" + samples[i].x.ToString("0") + "," + samples[i].y.ToString("0") + ")";
            }

            return text;
        }

        private static void AssertNoPressGraphicRaycasts(FishView view)
        {
            foreach (var image in view.GetComponentsInChildren<Image>(true))
            {
                if (image.name.StartsWith("Press"))
                {
                    Assert.That(image.raycastTarget, Is.False, image.name);
                }
            }
        }

        private static void UpAt(FishView fish, Vector2 screen)
        {
            var data = new PointerEventData(EventSystem.current) { position = screen };
            ExecuteEvents.Execute(fish.gameObject, data, ExecuteEvents.pointerUpHandler);
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
