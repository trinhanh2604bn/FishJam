using System.Collections;
using System.Collections.Generic;
using FishPuzzle.Core;
using FishPuzzle.Domain;
using FishPuzzle.Presentation;
using FishPuzzle.Tanks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FishPuzzle.Tests.PlayMode
{
    public sealed class M5CoreLoopPlayTests
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
        public IEnumerator EmptyBubble_Pops_AndUpperBubbleKeepsItsIdentity()
        {
            var flow = Flow();
            var pile = Bootstrap().BubblePile;
            Assert.That(pile.TryGetBubble(2, out var falling), Is.True);
            var fallingId = falling.BubbleId;
            Assert.That(pile.TryGetBubble(0, out var source), Is.True);
            Assert.That(source.BubbleId, Is.EqualTo("L001_B001"));
            var fishIds = new int[source.FishViews.Count];
            for (var i = 0; i < fishIds.Length; i++)
            {
                fishIds[i] = source.FishViews[i].FishId;
            }

            for (var i = 0; i < fishIds.Length; i++)
            {
                Click(ViewFor(fishIds[i]));
                yield return WaitUntilReady(flow);
            }

            Assert.That(flow.State, Is.EqualTo(GameState.PlayerInput));
            Assert.That(falling, Is.Not.Null);
            Assert.That(falling.BubbleId, Is.EqualTo(fallingId));
            Assert.That(falling.SlotId, Is.EqualTo(0));
            Assert.That(falling.GetComponent<Rigidbody2D>(), Is.Null);
            Assert.That(pile.TryGetSlotPosition(0, out var slotPosition), Is.True);
            var rect = falling.transform as RectTransform;
            Assert.That(Vector2.Distance(rect.anchoredPosition, slotPosition), Is.LessThan(1f));
            Assert.That(FindBubble("L001_B001"), Is.Null);
            var spawned = FindBubble("L001_B011");
            Assert.That(spawned, Is.Not.Null);
            Assert.That(spawned.SlotId, Is.EqualTo(7));
            Assert.That(spawned.SlotId, Is.Not.EqualTo(0));
            Assert.That(FindBubble("L001_B012"), Is.Null);
            Assert.That(flow.Session.PendingBubbleCount, Is.EqualTo(1));
            Assert.That(flow.Session.NextBubbleQueueIndex, Is.EqualTo(11));
            Assert.That(Bootstrap().VisibleBubbleCount, Is.EqualTo(10));
        }

        [UnityTest]
        public IEnumerator TargetChange_AutoPromotesMatchingTrayFish()
        {
            var flow = Flow();
            var red = flow.Session.FindFirstIdleFish(FishType.RedClown);
            Click(ViewFor(red.Id));
            yield return WaitUntilReady(flow);

            for (var i = 0; i < 3; i++)
            {
                var orange = flow.Session.FindFirstIdleFish(FishType.Orange);
                Click(ViewFor(orange.Id));
                yield return WaitUntilReady(flow);
            }

            Assert.That(flow.State, Is.EqualTo(GameState.PlayerInput));
            Assert.That(flow.Session.Tray.Count, Is.EqualTo(0));
            Assert.That(flow.Session.Tanks[0].CurrentTarget, Is.EqualTo(FishType.RedClown));
            Assert.That(flow.Session.Tanks[0].FillCount, Is.EqualTo(1));
            Assert.That(flow.Session.Tanks[0].ContainedFish[0].Id, Is.EqualTo(red.Id));
            Assert.That(flow.Session.Progress.CollectedFishCount, Is.EqualTo(3));
            Assert.That(Bootstrap().GlobalProgressLabel, Is.EqualTo("3 / 36"));
            Assert.That(Bootstrap().TankBoard.Slots[0].PreviewBadge.PreviewFishType, Is.EqualTo(FishType.RedClown));
            Assert.That(Bootstrap().TankBoard.Slots[0].PreviewBadge.PreviewProgressText, Is.EqualTo("1 / 3"));
            Assert.That(Bootstrap().WaitingTray.PresentedOccupantCount, Is.EqualTo(0));
            Assert.That(ViewFor(red.Id).transform.parent.name, Is.EqualTo("FishSlot_0"));
            Assert.That(flow.IsWinPanelVisible, Is.False);
            Assert.That(flow.IsLosePanelVisible, Is.False);
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator Level001_PlayToCompletion_ShowsWinAndLocksInput()
        {
            var flow = Flow();
            var routing = new FishRoutingService();
            var guard = 0;
            while (flow.State != GameState.Win && guard < 80)
            {
                guard++;
                Assert.That(flow.State, Is.EqualTo(GameState.PlayerInput), Describe(flow));
                var fish = FirstMatchingFish(flow.Session, routing);
                Assert.That(fish, Is.Not.Null, Describe(flow));
                Click(ViewFor(fish.Id));
                yield return WaitUntilReady(flow);
                if (flow.State == GameState.Lose)
                {
                    Assert.Fail("Level_001 lost. " + Describe(flow));
                }
            }

            Assert.That(flow.State, Is.EqualTo(GameState.Win), Describe(flow));
            Assert.That(flow.IsPresentationBusy, Is.False);
            Assert.That(flow.Session.Progress.CollectedFishCount, Is.EqualTo(36));
            Assert.That(flow.Session.Progress.TotalFishRequired, Is.EqualTo(36));
            Assert.That(Bootstrap().GlobalProgressLabel, Is.EqualTo("36 / 36"));
            Assert.That(flow.Session.Tray.Count, Is.EqualTo(0));
            Assert.That(flow.Session.PendingBubbleCount, Is.EqualTo(0));
            Assert.That(flow.IsWinPanelVisible, Is.True);
            Assert.That(flow.IsLosePanelVisible, Is.False);
            var panel = Object.FindAnyObjectByType<WinPanelView>();
            Assert.That(panel, Is.Not.Null);
            Assert.That(panel.Message, Is.EqualTo("Win"));
            Assert.That(Bootstrap().VisibleBubbleCount, Is.EqualTo(0));
            Assert.That(CountFishViews(), Is.EqualTo(0));

            var ignored = flow.Session.TrySelectFish(1, null);
            Assert.That(ignored.Outcome, Is.EqualTo(FishSelectionOutcome.Ignored));
            Assert.That(flow.State, Is.EqualTo(GameState.Win));
            Assert.That(flow.Session.Progress.CollectedFishCount, Is.EqualTo(36));
        }

        private static FishRuntimeState FirstMatchingFish(LevelSession session, FishRoutingService routing)
        {
            var bubbles = session.Bubbles;
            for (var i = 0; i < bubbles.Count; i++)
            {
                var bubble = bubbles[i];
                if (bubble == null || !bubble.IsInPlay || !bubble.IsInteractable)
                {
                    continue;
                }

                var fish = bubble.Fish;
                for (var f = 0; f < fish.Count; f++)
                {
                    var candidate = fish[f];
                    if (candidate != null
                        && candidate.State == FishState.Idle
                        && routing.SelectTank(candidate.Type, session.Tanks).Found)
                    {
                        return candidate;
                    }
                }
            }

            return null;
        }

        private static string Describe(GameFlowController flow)
        {
            var session = flow.Session;
            return "state " + flow.State
                + " busy " + flow.IsPresentationBusy
                + " progress " + session.Progress.CollectedFishCount
                + " tray " + session.Tray.Count
                + " pending " + session.PendingBubbleCount;
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
                    Assert.Fail("Resolution did not finish. " + Describe(flow));
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

        private static void Click(FishView fish)
        {
            PointerGesture.Click(fish);
        }

        private static int CountFishViews()
        {
            return Object.FindObjectsByType<FishView>(FindObjectsInactive.Exclude).Length;
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
