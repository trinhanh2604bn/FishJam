using System.Collections;
using FishPuzzle.Core;
using FishPuzzle.Domain;
using FishPuzzle.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace FishPuzzle.Tests.PlayMode
{
    public sealed class M10ImpactFxPlayTests
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
        public IEnumerator LastFish_PopsImmediately_AndSettlesWhileBurstContinues()
        {
            var flow = Flow();
            var pile = Bootstrap().BubblePile;
            Assert.That(pile.TryGetBubble(0, out var source), Is.True);
            var bubbleId = source.BubbleId;
            var fishIds = CopyFishIds(source);
            for (var i = 0; i < fishIds.Length - 1; i++)
            {
                PointerGesture.Click(ViewFor(fishIds[i]));
                yield return WaitUntilReady(flow);
            }

            var runtime = RuntimeBubble(flow, bubbleId);
            var routesBefore = flow.AcceptedRouteCount;
            var progressBefore = flow.Session.Progress.CollectedFishCount;
            PointerGesture.Click(ViewFor(fishIds[fishIds.Length - 1]));

            Assert.That(runtime.RemainingFishCount, Is.EqualTo(0));
            Assert.That(runtime.IsInteractable, Is.False);
            Assert.That(runtime.HasPopped, Is.True);
            Assert.That(flow.ActiveBubbleBurstCount, Is.GreaterThanOrEqualTo(BubbleBurstPool.MinCount));
            Assert.That(flow.ActiveBubbleBurstCount, Is.LessThanOrEqualTo(BubbleBurstPool.HardPoolCap));
            Assert.That(flow.LastBubblePopSeconds, Is.InRange(0.10f, 0.16f));
            AssertShellIgnored(bubbleId);

            var sawSettleWithBurst = false;
            var bubbleGone = FindBubble(bubbleId) == null;
            var elapsed = 0f;
            while (flow.IsPresentationBusy && elapsed < 8f)
            {
                if (FindBubble(bubbleId) == null)
                {
                    bubbleGone = true;
                }

                if ((flow.State == GameState.SettlingBubblePile || flow.State == GameState.SpawningTopBubble)
                    && flow.ActiveBubbleBurstCount >= BubbleBurstPool.MinCount)
                {
                    sawSettleWithBurst = true;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            yield return WaitUntilReady(flow);

            Assert.That(bubbleGone, Is.True);
            Assert.That(FindBubble(bubbleId), Is.Null);
            Assert.That(sawSettleWithBurst, Is.True);
            Assert.That(flow.LastBubblePopSeconds, Is.LessThan(0.20f));
            Assert.That(flow.State, Is.EqualTo(GameState.PlayerInput));
            Assert.That(flow.IsPresentationBusy, Is.False);
            Assert.That(flow.AcceptedRouteCount, Is.EqualTo(routesBefore + 1));
            Assert.That(flow.Session.CountPlacements(fishIds[fishIds.Length - 1]), Is.EqualTo(1));
            Assert.That(flow.Session.Progress.CollectedFishCount, Is.EqualTo(progressBefore).Or.EqualTo(progressBefore + 3));

            var followUp = flow.Session.FindFirstIdleFish(FishType.Orange);
            if (followUp == null)
            {
                followUp = flow.Session.FindFirstIdleFish(FishType.GreenStriped);
            }

            Assert.That(followUp, Is.Not.Null);
            PointerGesture.Click(ViewFor(followUp.Id));
            yield return WaitUntilReady(flow);

            Assert.That(flow.State, Is.EqualTo(GameState.PlayerInput));
            Assert.That(flow.IsPresentationBusy, Is.False);
            Assert.That(flow.Session.CountPlacements(followUp.Id), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator MissingBurst_StillSettles_AndReturnsToPlayerInput()
        {
            var flow = Flow();
            flow.SuppressBubblePopVfx();
            var pile = Bootstrap().BubblePile;
            Assert.That(pile.TryGetBubble(0, out var source), Is.True);
            var bubbleId = source.BubbleId;
            var fishIds = CopyFishIds(source);
            for (var i = 0; i < fishIds.Length; i++)
            {
                PointerGesture.Click(ViewFor(fishIds[i]));
                yield return WaitUntilReady(flow);
            }

            Assert.That(flow.BubbleBurstEmissionCount, Is.EqualTo(0));
            Assert.That(flow.ActiveBubbleBurstCount, Is.EqualTo(0));
            Assert.That(FindBubble(bubbleId), Is.Null);
            Assert.That(flow.State, Is.EqualTo(GameState.PlayerInput));
            Assert.That(flow.IsPresentationBusy, Is.False);

            var followUp = flow.Session.FindFirstIdleFish(FishType.GreenStriped);
            Assert.That(followUp, Is.Not.Null);
            var greenFill = flow.Session.Tanks[1].FillCount;
            PointerGesture.Click(ViewFor(followUp.Id));
            yield return WaitUntilReady(flow);

            Assert.That(flow.State, Is.EqualTo(GameState.PlayerInput));
            Assert.That(flow.Session.Tanks[1].FillCount, Is.EqualTo(greenFill + 1));
            Assert.That(flow.Session.CountPlacements(followUp.Id), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ThirdFish_SplashesBeforeTankResolve()
        {
            var flow = Flow();
            for (var i = 0; i < 2; i++)
            {
                PointerGesture.Click(ViewFor(flow.Session.FindFirstIdleFish(FishType.Orange).Id));
                yield return WaitUntilReady(flow);
            }

            Assert.That(flow.TankSplashCount, Is.EqualTo(2));
            Assert.That(flow.Session.Tanks[0].FillCount, Is.EqualTo(2));
            var badge = Bootstrap().TankBoard.Slots[0].PreviewBadge;
            Assert.That(badge.PreviewProgressText, Is.EqualTo("2 / 3"));

            var sawSplashBeforeFull = false;
            var sawFull = false;
            PointerGesture.Click(ViewFor(flow.Session.FindFirstIdleFish(FishType.Orange).Id));
            var elapsed = 0f;
            while (elapsed < 8f)
            {
                var text = badge.PreviewProgressText;
                if (flow.TankSplashCount >= 3 && text == "2 / 3")
                {
                    sawSplashBeforeFull = true;
                }

                if (text == "3 / 3")
                {
                    sawFull = true;
                }

                if (!flow.IsPresentationBusy && flow.State == GameState.PlayerInput && sawFull)
                {
                    break;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.That(sawSplashBeforeFull, Is.True);
            Assert.That(sawFull, Is.True);
            Assert.That(flow.TankSplashCount, Is.EqualTo(3));
            Assert.That(flow.State, Is.EqualTo(GameState.PlayerInput));
            Assert.That(flow.IsPresentationBusy, Is.False);
            Assert.That(flow.Session.Progress.CollectedFishCount, Is.EqualTo(3));
            Assert.That(flow.Session.Tanks[0].FillCount, Is.EqualTo(0));
            Assert.That(badge.PreviewFishType, Is.EqualTo(FishType.RedClown));
            Assert.That(badge.PreviewProgressText, Is.EqualTo("0 / 3"));
        }

        [UnityTest]
        public IEnumerator TrayLanding_DoesNotSplash_AndReturnsToPlayerInput()
        {
            var flow = Flow();
            var fish = ViewFor(flow.Session.FindFirstIdleFish(FishType.PinkStriped).Id);
            PointerGesture.Click(fish);
            yield return WaitUntilReady(flow);

            Assert.That(flow.State, Is.EqualTo(GameState.PlayerInput));
            Assert.That(flow.IsPresentationBusy, Is.False);
            Assert.That(flow.Session.Tray.Count, Is.EqualTo(1));
            Assert.That(flow.TankSplashCount, Is.EqualTo(0));
            Assert.That(flow.Session.Tanks[0].FillCount, Is.EqualTo(0));
            Assert.That(flow.Session.CountPlacements(fish.FishId), Is.EqualTo(1));
        }

        private static void AssertShellIgnored(string bubbleId)
        {
            var shell = FindBubble(bubbleId);
            if (shell == null)
            {
                return;
            }

            var images = shell.GetComponentsInChildren<Image>(true);
            for (var i = 0; i < images.Length; i++)
            {
                Assert.That(images[i].raycastTarget, Is.False);
            }
        }

        private static int[] CopyFishIds(BubbleView source)
        {
            var ids = new int[source.FishViews.Count];
            for (var i = 0; i < ids.Length; i++)
            {
                ids[i] = source.FishViews[i].FishId;
            }

            return ids;
        }

        private static BubbleRuntimeState RuntimeBubble(GameFlowController flow, string bubbleId)
        {
            var bubbles = flow.Session.Bubbles;
            for (var i = 0; i < bubbles.Count; i++)
            {
                if (bubbles[i] != null && bubbles[i].BubbleId == bubbleId)
                {
                    return bubbles[i];
                }
            }

            Assert.Fail("Missing runtime bubble " + bubbleId);
            return null;
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

        private static BubbleView FindBubble(string bubbleId)
        {
            var views = Object.FindObjectsByType<BubbleView>(FindObjectsInactive.Exclude);
            for (var i = 0; i < views.Length; i++)
            {
                if (views[i] != null && views[i].BubbleId == bubbleId)
                {
                    return views[i];
                }
            }

            return null;
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
