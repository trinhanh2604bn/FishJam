using System.Collections;
using FishPuzzle.Core;
using FishPuzzle.Domain;
using FishPuzzle.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FishPuzzle.Tests.PlayMode
{
    public sealed class M81TouchFeelPlayTests
    {
        [UnitySetUp]
        public IEnumerator LoadGameplayScene()
        {
            yield return SceneManager.LoadSceneAsync("Gameplay", LoadSceneMode.Single);
            yield return null;
            Canvas.ForceUpdateCanvases();
        }

        [UnityTest]
        public IEnumerator PointerDown_DoesNotMutateSession_AndShowsPressedFish()
        {
            var flow = Flow();
            var fish = ViewFor(flow.Session.FindFirstIdleFish(FishType.Orange).Id);
            var bubble = fish.GetComponentInParent<BubbleView>();
            var fishBefore = bubble.FishViews.Count;
            var viewsBefore = CountFishViews();

            PointerGesture.Press(fish);

            Assert.That(flow.IsFishPressCaptured, Is.True);
            Assert.That(flow.IsPresentationBusy, Is.False);
            Assert.That(flow.State, Is.EqualTo(GameState.PlayerInput));
            Assert.That(flow.AcceptedRouteCount, Is.EqualTo(0));
            Assert.That(flow.Session.Tanks[0].FillCount, Is.EqualTo(0));
            Assert.That(flow.Session.Tray.Count, Is.EqualTo(0));
            Assert.That(StillIdleInBubble(flow.Session, fish.FishId), Is.True);
            Assert.That(bubble.FishViews.Count, Is.EqualTo(fishBefore));
            Assert.That(CountFishViews(), Is.EqualTo(viewsBefore));
            Assert.That(fish.IsPressed, Is.True);
            Assert.That(fish.PressLift, Is.InRange(6f, 10f));
            Assert.That(fish.PressScale, Is.InRange(1.04f, 1.07f));
            Assert.That(fish.GetComponentInParent<BubbleView>(), Is.SameAs(bubble));

            fish.AdvancePress(0.1f);

            Assert.That(Mathf.Abs(fish.PressAngle), Is.GreaterThan(0.2f).And.LessThanOrEqualTo(2.05f));
            Assert.That(flow.Session.Tanks[0].FillCount, Is.EqualTo(0));
            yield return null;
        }

        [UnityTest]
        public IEnumerator PointerUp_RoutesOnce_ToTheMatchingTank()
        {
            var flow = Flow();
            var fish = ViewFor(flow.Session.FindFirstIdleFish(FishType.Orange).Id);
            var fishId = fish.FishId;

            PointerGesture.Press(fish);
            Assert.That(flow.AcceptedRouteCount, Is.EqualTo(0));
            PointerGesture.ReleaseOver(fish);
            Assert.That(flow.IsPresentationBusy, Is.True);
            Assert.That(flow.State, Is.EqualTo(GameState.RoutingFish));
            Assert.That(flow.AcceptedRouteCount, Is.EqualTo(1));

            PointerGesture.ReleaseOver(fish);
            yield return WaitUntilReady(flow);

            Assert.That(flow.AcceptedRouteCount, Is.EqualTo(1));
            Assert.That(flow.State, Is.EqualTo(GameState.PlayerInput));
            Assert.That(flow.IsPresentationBusy, Is.False);
            Assert.That(flow.IsFishPressCaptured, Is.False);
            Assert.That(flow.Session.Tanks[0].FillCount, Is.EqualTo(1));
            Assert.That(flow.Session.Tanks[0].ContainedFish[0].Id, Is.EqualTo(fishId));
            Assert.That(flow.Session.CountPlacements(fishId), Is.EqualTo(1));
            Assert.That(flow.Session.Tray.Count, Is.EqualTo(0));
            Assert.That(flow.State, Is.Not.EqualTo(GameState.Win));
            Assert.That(flow.TankSplashCount, Is.EqualTo(1));
            Assert.That(fish.transform.parent.name, Is.EqualTo("FishSlot_0"));
        }

        [UnityTest]
        public IEnumerator PointerCancel_RoutesZeroTimes_AndRestoresTheFish()
        {
            var flow = Flow();
            var fish = ViewFor(flow.Session.FindFirstIdleFish(FishType.Orange).Id);
            var bubble = fish.GetComponentInParent<BubbleView>();
            var before = bubble.FishViews.Count;

            PointerGesture.Cancel(fish);

            Assert.That(flow.AcceptedRouteCount, Is.EqualTo(0));
            Assert.That(flow.IsFishPressCaptured, Is.False);
            Assert.That(flow.IsPresentationBusy, Is.False);
            Assert.That(flow.State, Is.EqualTo(GameState.PlayerInput));
            Assert.That(fish.IsPressed, Is.False);
            Assert.That(fish.PressLift, Is.EqualTo(0f));
            Assert.That(StillIdleInBubble(flow.Session, fish.FishId), Is.True);
            Assert.That(bubble.FishViews.Count, Is.EqualTo(before));
            Assert.That(flow.Session.Tanks[0].FillCount, Is.EqualTo(0));
            Assert.That(flow.TankSplashCount, Is.EqualTo(0));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Holding_DoesNotSelectAnotherFish_OrDuplicateViews()
        {
            var flow = Flow();
            var first = ViewFor(flow.Session.FindFirstIdleFish(FishType.Orange).Id);
            var second = ViewFor(flow.Session.FindFirstIdleFish(FishType.PinkStriped).Id);
            var viewsBefore = CountFishViews();

            PointerGesture.Press(first);
            PointerGesture.Press(second);
            PointerGesture.Press(first);

            Assert.That(first.IsPressed, Is.True);
            Assert.That(second.IsPressed, Is.False);
            Assert.That(flow.AcceptedRouteCount, Is.EqualTo(0));
            Assert.That(CountFishViews(), Is.EqualTo(viewsBefore));
            Assert.That(StillIdleInBubble(flow.Session, first.FishId), Is.True);
            Assert.That(StillIdleInBubble(flow.Session, second.FishId), Is.True);
            Assert.That(flow.State, Is.EqualTo(GameState.PlayerInput));

            PointerGesture.ReleaseOver(first);
            yield return WaitUntilReady(flow);

            Assert.That(flow.AcceptedRouteCount, Is.EqualTo(1));
            Assert.That(flow.Session.CountPlacements(first.FishId), Is.EqualTo(1));
            Assert.That(flow.Session.CountPlacements(second.FishId), Is.EqualTo(1));
            Assert.That(StillIdleInBubble(flow.Session, second.FishId), Is.True);
            Assert.That(CountFishViews(), Is.EqualTo(viewsBefore));
            Assert.That(flow.State, Is.EqualTo(GameState.PlayerInput));
            Assert.That(flow.IsPresentationBusy, Is.False);
        }

        [UnityTest]
        public IEnumerator EmptyTouch_ShowsRippleAndBubbles_WithoutGameplayMutation()
        {
            var flow = Flow();
            var touch = flow.TouchFeedback;
            Assert.That(touch, Is.Not.Null);
            var viewsBefore = CountFishViews();

            touch.PresentPointerDown(new Vector2(40f, 80f));

            Assert.That(touch.ActiveRippleCount, Is.GreaterThan(0));
            Assert.That(touch.ActiveBubbleCount, Is.GreaterThan(0).And.LessThanOrEqualTo(6));
            Assert.That(touch.IsHolding, Is.True);
            Assert.That(flow.State, Is.EqualTo(GameState.PlayerInput));
            Assert.That(flow.IsPresentationBusy, Is.False);
            Assert.That(flow.AcceptedRouteCount, Is.EqualTo(0));
            Assert.That(flow.Session.Tray.Count, Is.EqualTo(0));
            Assert.That(flow.Session.Tanks[0].FillCount, Is.EqualTo(0));
            Assert.That(CountFishViews(), Is.EqualTo(viewsBefore));

            touch.PresentPointerUp();

            Assert.That(touch.IsHolding, Is.False);
            Assert.That(flow.State, Is.EqualTo(GameState.PlayerInput));
            yield return null;
        }

        [UnityTest]
        public IEnumerator TrayRoute_DoesNotPlayTankSplash()
        {
            var flow = Flow();
            var fish = ViewFor(flow.Session.FindFirstIdleFish(FishType.PinkStriped).Id);

            PointerGesture.Click(fish);
            yield return WaitUntilReady(flow);

            Assert.That(flow.State, Is.EqualTo(GameState.PlayerInput));
            Assert.That(flow.State, Is.Not.EqualTo(GameState.Lose));
            Assert.That(flow.State, Is.Not.EqualTo(GameState.Win));
            Assert.That(flow.Session.Tray.Count, Is.EqualTo(1));
            Assert.That(flow.Session.Tanks[0].FillCount, Is.EqualTo(0));
            Assert.That(flow.TankSplashCount, Is.EqualTo(0));
            Assert.That(fish.transform.parent.name, Is.EqualTo("Slot_0"));
        }

        [UnityTest]
        public IEnumerator MissingSplashAndRipple_CannotSoftLock()
        {
            var flow = Flow();
            flow.SuppressLandingAndTouchVfx();
            var touch = flow.TouchFeedback;
            touch.PresentPointerDown(new Vector2(30f, 50f));

            Assert.That(touch.ActiveRippleCount, Is.EqualTo(0));
            Assert.That(touch.ActiveBubbleCount, Is.EqualTo(0));

            var fish = ViewFor(flow.Session.FindFirstIdleFish(FishType.Orange).Id);
            PointerGesture.Click(fish);
            yield return WaitUntilReady(flow);

            Assert.That(flow.IsPresentationBusy, Is.False);
            Assert.That(flow.State, Is.EqualTo(GameState.PlayerInput));
            Assert.That(flow.TankSplashCount, Is.EqualTo(0));
            Assert.That(flow.Session.Tanks[0].FillCount, Is.EqualTo(1));
            Assert.That(flow.Session.CountPlacements(fish.FishId), Is.EqualTo(1));
            Assert.That(flow.AcceptedRouteCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator FifthTrayRelease_StillLoses_AndBlocksFurtherInput()
        {
            var flow = Flow();
            for (var i = 0; i < 5; i++)
            {
                PointerGesture.Click(ViewFor(flow.Session.FindFirstIdleFish(FishType.PinkStriped).Id));
                yield return WaitUntilReady(flow);
            }

            var blocked = flow.Session.FindFirstIdleFish(FishType.Orange);
            PointerGesture.Click(ViewFor(blocked.Id));
            yield return null;

            Assert.That(flow.State, Is.EqualTo(GameState.Lose));
            Assert.That(flow.State, Is.Not.EqualTo(GameState.Win));
            Assert.That(flow.Session.Tray.Count, Is.EqualTo(5));
            Assert.That(flow.Session.Progress.CollectedFishCount, Is.EqualTo(0));
            Assert.That(blocked.State, Is.EqualTo(FishState.Idle));
            Assert.That(flow.IsLosePanelVisible, Is.True);
        }

        private static bool StillIdleInBubble(LevelSession session, int fishId)
        {
            var bubbles = session.Bubbles;
            for (var i = 0; i < bubbles.Count; i++)
            {
                var bubble = bubbles[i];
                if (bubble == null)
                {
                    continue;
                }

                var fish = bubble.Fish;
                for (var f = 0; f < fish.Count; f++)
                {
                    if (fish[f] != null && fish[f].Id == fishId)
                    {
                        return fish[f].State == FishState.Idle;
                    }
                }
            }

            return false;
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

        private static int CountFishViews()
        {
            return Object.FindObjectsByType<FishView>(FindObjectsInactive.Exclude).Length;
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
            var bootstrap = Object.FindAnyObjectByType<LevelSceneBootstrapper>();
            Assert.That(bootstrap, Is.Not.Null);
            Assert.That(bootstrap.IsInitialized, Is.True);
            var flow = bootstrap.Flow;
            Assert.That(flow, Is.Not.Null);
            Assert.That(flow.Session, Is.Not.Null);
            return flow;
        }
    }
}
