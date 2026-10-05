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
    public sealed class M8AnimationPlayTests
    {
        [UnitySetUp]
        public IEnumerator LoadGameplayScene()
        {
            yield return SceneManager.LoadSceneAsync("Gameplay", LoadSceneMode.Single);
            yield return null;
            Canvas.ForceUpdateCanvases();
        }

        [UnityTest]
        public IEnumerator FishToTank_MovesThenLands_AndBlocksInputWhileTraveling()
        {
            var flow = Flow();
            var fish = ViewFor(flow.Session.FindFirstIdleFish(FishType.Orange).Id);
            var bubble = fish.GetComponentInParent<BubbleView>();
            var origin = fish.transform.position;
            var other = flow.Session.FindFirstIdleFish(FishType.PinkStriped);

            Click(fish);

            Assert.That(flow.IsPresentationBusy, Is.True);
            Assert.That(flow.State, Is.EqualTo(GameState.RoutingFish));
            var ignored = flow.Session.TrySelectFish(other.Id, null);
            Assert.That(ignored.Outcome, Is.EqualTo(FishSelectionOutcome.Ignored));
            Assert.That(flow.Session.Tray.Count, Is.EqualTo(0));

            var moved = false;
            for (var i = 0; i < 40 && flow.IsPresentationBusy; i++)
            {
                yield return null;
                var traveled = Vector3.Distance(fish.transform.position, origin) > 0.01f;
                var onFlightLayer = fish.transform.parent != null && fish.transform.parent.name == "FishFlightLayer";
                if (traveled || onFlightLayer)
                {
                    moved = true;
                    break;
                }
            }

            yield return WaitUntilReady(flow);

            Assert.That(moved, Is.True);
            Assert.That(flow.State, Is.EqualTo(GameState.PlayerInput));
            Assert.That(flow.IsPresentationBusy, Is.False);
            Assert.That(fish.transform.parent.name, Is.EqualTo("FishSlot_0"));
            Assert.That(flow.Session.Tanks[0].FillCount, Is.EqualTo(1));
            Assert.That(flow.Session.CountPlacements(fish.FishId), Is.EqualTo(1));
            Assert.That(bubble.FishViews.Count, Is.EqualTo(2));
            var left = bubble.FishViews[0].GetComponent<RectTransform>().anchoredPosition.x;
            var right = bubble.FishViews[1].GetComponent<RectTransform>().anchoredPosition.x;
            Assert.That(left * right, Is.LessThan(0f));
        }

        [UnityTest]
        public IEnumerator FishToTray_LandsInTheNextSlot()
        {
            var flow = Flow();
            var fish = ViewFor(flow.Session.FindFirstIdleFish(FishType.PinkStriped).Id);

            Click(fish);
            yield return WaitUntilReady(flow);

            Assert.That(flow.State, Is.EqualTo(GameState.PlayerInput));
            Assert.That(flow.Session.Tray.Count, Is.EqualTo(1));
            Assert.That(fish.transform.parent.name, Is.EqualTo("Slot_0"));
            Assert.That(Bootstrap().WaitingTray.PresentedOccupantCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator InterruptedRoute_SnapsToDestination_AndCanContinue()
        {
            var flow = Flow();
            var fish = ViewFor(flow.Session.FindFirstIdleFish(FishType.Orange).Id);

            Click(fish);
            yield return null;
            Assert.That(flow.IsPresentationBusy, Is.True);
            flow.CompletePresentationNow();
            yield return null;

            Assert.That(flow.IsPresentationBusy, Is.False);
            Assert.That(flow.State, Is.EqualTo(GameState.PlayerInput));
            Assert.That(fish.transform.parent.name, Is.EqualTo("FishSlot_0"));
            Assert.That(flow.Session.Tanks[0].FillCount, Is.EqualTo(1));
            Assert.That(flow.Session.CountPlacements(fish.FishId), Is.EqualTo(1));

            var second = ViewFor(flow.Session.FindFirstIdleFish(FishType.Orange).Id);
            Click(second);
            yield return WaitUntilReady(flow);

            Assert.That(flow.State, Is.EqualTo(GameState.PlayerInput));
            Assert.That(flow.Session.Tanks[0].FillCount, Is.EqualTo(2));
            Assert.That(second.transform.parent.name, Is.EqualTo("FishSlot_1"));
        }

        [UnityTest]
        public IEnumerator TankComplete_ShowsThreeOfThree_ThenTheNextTarget()
        {
            var flow = Flow();
            for (var i = 0; i < 2; i++)
            {
                Click(ViewFor(flow.Session.FindFirstIdleFish(FishType.Orange).Id));
                yield return WaitUntilReady(flow);
            }

            var sawFull = false;
            Click(ViewFor(flow.Session.FindFirstIdleFish(FishType.Orange).Id));
            var elapsed = 0f;
            while (elapsed < 8f)
            {
                var badge = Bootstrap().TankBoard.Slots[0].PreviewBadge;
                if (badge.PreviewFishType == FishType.Orange && badge.PreviewProgressText == "3 / 3")
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

            var finalBadge = Bootstrap().TankBoard.Slots[0].PreviewBadge;
            Assert.That(sawFull, Is.True);
            Assert.That(flow.State, Is.EqualTo(GameState.PlayerInput));
            Assert.That(flow.IsPresentationBusy, Is.False);
            Assert.That(flow.Session.Progress.CollectedFishCount, Is.EqualTo(3));
            Assert.That(flow.Session.Tanks[0].CurrentTarget, Is.EqualTo(FishType.RedClown));
            Assert.That(flow.Session.Tanks[0].FillCount, Is.EqualTo(0));
            Assert.That(finalBadge.PreviewFishType, Is.EqualTo(FishType.RedClown));
            Assert.That(finalBadge.PreviewProgressText, Is.EqualTo("0 / 3"));
            Assert.That(Bootstrap().GlobalProgressLabel, Is.EqualTo("3 / 36"));
        }

        [UnityTest]
        public IEnumerator TrayCascade_CompactsAndReturnsToPlayerInput()
        {
            var flow = Flow();
            var red = flow.Session.FindFirstIdleFish(FishType.RedClown);
            Click(ViewFor(red.Id));
            yield return WaitUntilReady(flow);

            for (var i = 0; i < 3; i++)
            {
                Click(ViewFor(flow.Session.FindFirstIdleFish(FishType.Orange).Id));
                yield return WaitUntilReady(flow);
            }

            Assert.That(flow.State, Is.EqualTo(GameState.PlayerInput));
            Assert.That(flow.IsPresentationBusy, Is.False);
            Assert.That(flow.Session.Tray.Count, Is.EqualTo(0));
            Assert.That(Bootstrap().WaitingTray.PresentedOccupantCount, Is.EqualTo(0));
            Assert.That(flow.Session.Tanks[0].FillCount, Is.EqualTo(1));
            Assert.That(flow.Session.Tanks[0].ContainedFish[0].Id, Is.EqualTo(red.Id));
            Assert.That(ViewFor(red.Id).transform.parent.name, Is.EqualTo("FishSlot_0"));
            Assert.That(Bootstrap().TankBoard.Slots[0].PreviewBadge.PreviewProgressText, Is.EqualTo("1 / 3"));
        }

        [UnityTest]
        public IEnumerator BubblePop_ContinuesWithoutVfx_AndDoesNotSoftLock()
        {
            var flow = Flow();
            flow.SuppressBubblePopVfx();
            var pile = Bootstrap().BubblePile;
            Assert.That(pile.TryGetBubble(0, out var source), Is.True);
            var sourceId = source.BubbleId;
            Assert.That(pile.TryGetBubble(2, out var falling), Is.True);
            var fallingId = falling.BubbleId;
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
            Assert.That(flow.IsPresentationBusy, Is.False);
            Assert.That(FindBubble(sourceId), Is.Null);
            Assert.That(falling.BubbleId, Is.EqualTo(fallingId));
            Assert.That(falling.SlotId, Is.EqualTo(0));
            Assert.That(falling.GetComponent<Rigidbody2D>(), Is.Null);
            Assert.That(pile.TryGetSlotPosition(0, out var slotPosition), Is.True);
            var rect = falling.transform as RectTransform;
            Assert.That(Vector2.Distance(rect.anchoredPosition, slotPosition), Is.LessThan(1f));

            var followUp = flow.Session.FindFirstIdleFish(FishType.GreenStriped);
            Assert.That(followUp, Is.Not.Null);
            var greenFill = flow.Session.Tanks[1].FillCount;
            Click(ViewFor(followUp.Id));
            yield return WaitUntilReady(flow);

            Assert.That(flow.State, Is.EqualTo(GameState.PlayerInput));
            Assert.That(flow.IsPresentationBusy, Is.False);
            Assert.That(flow.Session.Tanks[1].FillCount, Is.EqualTo(greenFill + 1));
            Assert.That(flow.Session.CountPlacements(followUp.Id), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator TopSpawn_StaysNonInteractableUntilSettle()
        {
            var flow = Flow();
            var pile = Bootstrap().BubblePile;
            Assert.That(pile.TryGetBubble(0, out var source), Is.True);
            var fishIds = new int[source.FishViews.Count];
            for (var i = 0; i < fishIds.Length; i++)
            {
                fishIds[i] = source.FishViews[i].FishId;
            }

            for (var i = 0; i < fishIds.Length - 1; i++)
            {
                Click(ViewFor(fishIds[i]));
                yield return WaitUntilReady(flow);
            }

            Click(ViewFor(fishIds[fishIds.Length - 1]));
            var sawBlockedSpawn = false;
            var elapsed = 0f;
            while (flow.IsPresentationBusy && elapsed < 8f)
            {
                if (flow.State == GameState.SpawningTopBubble)
                {
                    var spawned = FindBubble("L001_B011");
                    if (spawned != null && spawned.FishViews.Count > 0)
                    {
                        var fishView = spawned.FishViews[0];
                        var image = fishView.GetComponentInChildren<Image>();
                        var before = spawned.FishViews.Count;
                        Assert.That(image, Is.Not.Null);
                        Assert.That(image.raycastTarget, Is.False);
                        Assert.That(RuntimeBubble(flow, spawned.BubbleId).IsInPlay, Is.False);
                        Click(fishView);
                        Assert.That(spawned.FishViews.Count, Is.EqualTo(before));
                        Assert.That(flow.Session.CountPlacements(fishView.FishId), Is.EqualTo(1));
                        sawBlockedSpawn = true;
                    }
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            yield return WaitUntilReady(flow);

            Assert.That(sawBlockedSpawn, Is.True);
            Assert.That(flow.State, Is.EqualTo(GameState.PlayerInput));
            var readySpawn = FindBubble("L001_B011");
            Assert.That(readySpawn, Is.Not.Null);
            Assert.That(readySpawn.SlotId, Is.Not.EqualTo(0));
            Assert.That(RuntimeBubble(flow, readySpawn.BubbleId).IsInPlay, Is.True);
            var readyFish = readySpawn.FishViews[0];
            Assert.That(readyFish.GetComponentInChildren<Image>().raycastTarget, Is.True);
            var readyCount = readySpawn.FishViews.Count;
            Click(readyFish);
            yield return WaitUntilReady(flow);

            Assert.That(flow.State, Is.EqualTo(GameState.PlayerInput));
            Assert.That(readySpawn.FishViews.Count, Is.EqualTo(readyCount - 1));
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

        private static void Click(FishView fish)
        {
            PointerGesture.Click(fish);
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
