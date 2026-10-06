using System.Collections;
using FishPuzzle.Core;
using FishPuzzle.Domain;
using FishPuzzle.Presentation;
using FishPuzzle.Tanks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FishPuzzle.Tests.PlayMode
{
    public sealed class M6ProgressionPlayTests
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
        [Timeout(180000)]
        public IEnumerator PlayMode_ProgressionUnlockAndRetry()
        {
            var flow = Flow();
            var bootstrap = Bootstrap();
            var references = Object.FindAnyObjectByType<GameplaySceneReferences>();
            Assert.That(bootstrap.Progression, Is.Not.Null);
            Assert.That(bootstrap.Progression.Progress.Lives, Is.EqualTo(5));
            Assert.That(bootstrap.Progression.Progress.Gold, Is.EqualTo(1000));
            Assert.That(bootstrap.Progression.Progress.Score, Is.EqualTo(0));
            Assert.That(references.LivesDisplay.text, Is.EqualTo("5"));
            Assert.That(references.GoldDisplay.text, Is.EqualTo("1000"));
            Assert.That(GameObject.Find("ScoreLabel"), Is.Null, "M12.1: no standalone Score HUD.");

            for (var i = 0; i < 5; i++)
            {
                Click(ViewFor(flow.Session.FindFirstIdleFish(FishType.PinkStriped).Id));
                yield return WaitUntilReady(flow);
            }

            Assert.That(flow.State, Is.EqualTo(GameState.Lose));
            Assert.That(bootstrap.Progression.Progress.Lives, Is.EqualTo(4));
            Assert.That(references.LivesDisplay.text, Is.EqualTo("4"));
            Assert.That(flow.IsLosePanelVisible, Is.True);
            flow.PresentCurrentOutcome();
            Assert.That(bootstrap.Progression.Progress.Lives, Is.EqualTo(4));
            Assert.That(references.LivesDisplay.text, Is.EqualTo("4"));

            var panel = Object.FindAnyObjectByType<LosePanelView>();
            Assert.That(panel.RetryButton, Is.Not.Null);
            panel.RetryButton.onClick.Invoke();
            yield return null;

            Assert.That(flow.State, Is.EqualTo(GameState.PlayerInput));
            Assert.That(flow.IsLosePanelVisible, Is.False);
            Assert.That(flow.Session.Progress.CollectedFishCount, Is.EqualTo(0));
            Assert.That(flow.Session.Progress.TotalFishRequired, Is.EqualTo(36));
            Assert.That(bootstrap.GlobalProgressLabel, Is.EqualTo("0 / 36"));
            Assert.That(flow.Session.Tray.Count, Is.EqualTo(0));
            Assert.That(bootstrap.WaitingTray.PresentedOccupantCount, Is.EqualTo(0));
            Assert.That(flow.Session.Tanks[0].IsUnlocked, Is.True);
            Assert.That(flow.Session.Tanks[1].IsUnlocked, Is.True);
            Assert.That(flow.Session.Tanks[2].State, Is.EqualTo(TankState.Locked));
            Assert.That(flow.Session.Tanks[3].State, Is.EqualTo(TankState.Locked));
            Assert.That(flow.Session.Targets.NextUnassignedIndex, Is.EqualTo(2));
            Assert.That(Count<BubbleView>(), Is.EqualTo(10));
            Assert.That(Count<TankSlotView>(), Is.EqualTo(4));
            Assert.That(Count<FishView>(), Is.EqualTo(30));
            Assert.That(Count<LosePanelView>(), Is.EqualTo(1));
            Assert.That(bootstrap.Progression.Progress.Lives, Is.EqualTo(4));
            Assert.That(bootstrap.Progression.Progress.Gold, Is.EqualTo(1000));
            Assert.That(bootstrap.Progression.Progress.Score, Is.EqualTo(0));
            Assert.That(references.LivesDisplay.text, Is.EqualTo("4"));
            Assert.That(references.GoldDisplay.text, Is.EqualTo("1000"));

            var red = flow.Session.FindFirstIdleFish(FishType.RedClown);
            Click(ViewFor(red.Id));
            yield return WaitUntilReady(flow);
            Assert.That(flow.Session.Tray.Count, Is.EqualTo(1));

            ClickLockedTank(2);
            Assert.That(flow.IsUnlockModalVisible, Is.True);
            Assert.That(flow.State, Is.EqualTo(GameState.TankUnlockModal));
            Assert.That(flow.UnlockModal.GoldButton, Is.Not.Null);
            flow.UnlockModal.GoldButton.onClick.Invoke();
            yield return WaitUntilReady(flow);

            Assert.That(flow.State, Is.EqualTo(GameState.PlayerInput));
            Assert.That(flow.IsUnlockModalVisible, Is.False);
            Assert.That(bootstrap.Progression.Progress.Gold, Is.EqualTo(400));
            Assert.That(references.GoldDisplay.text, Is.EqualTo("400"));
            Assert.That(flow.Session.Tanks[2].IsUnlocked, Is.True);
            Assert.That(flow.Session.Tanks[2].CurrentTarget, Is.EqualTo(FishType.RedClown));
            Assert.That(flow.Session.Tanks[2].FillCount, Is.EqualTo(1));
            Assert.That(flow.Session.Tanks[2].ContainedFish[0].Id, Is.EqualTo(red.Id));
            Assert.That(flow.Session.Tray.Count, Is.EqualTo(0));
            Assert.That(bootstrap.TankBoard.Slots[2].IsPresentedAsUnlocked, Is.True);
            Assert.That(bootstrap.TankBoard.Slots[2].transform.Find("LockedOverlay").gameObject.activeSelf, Is.False);
            Assert.That(bootstrap.TankBoard.Slots[2].PreviewBadge.PreviewFishType, Is.EqualTo(FishType.RedClown));
            Assert.That(bootstrap.TankBoard.Slots[2].PreviewBadge.PreviewProgressText, Is.EqualTo("1 / 3"));
            Assert.That(ViewFor(red.Id).transform.parent.name, Is.EqualTo("FishSlot_0"));

            ClickLockedTank(3);
            Assert.That(flow.IsUnlockModalVisible, Is.True);
            flow.UnlockModal.RewardButton.onClick.Invoke();
            yield return WaitUntilReady(flow);

            Assert.That(flow.State, Is.EqualTo(GameState.PlayerInput));
            Assert.That(flow.IsUnlockModalVisible, Is.False);
            Assert.That(bootstrap.Progression.Progress.Gold, Is.EqualTo(400));
            Assert.That(references.GoldDisplay.text, Is.EqualTo("400"));
            Assert.That(flow.Session.Tanks[3].IsUnlocked, Is.True);
            Assert.That(flow.Session.Tanks[3].HasTarget, Is.True);
            Assert.That(flow.Session.Tanks[3].CurrentTarget, Is.EqualTo(FishType.PinkStriped));
            Assert.That(bootstrap.TankBoard.Slots[3].IsPresentedAsUnlocked, Is.True);
            Assert.That(bootstrap.TankBoard.Slots[3].PreviewBadge, Is.Not.Null);
            Assert.That(flow.Session.Targets.NextUnassignedIndex, Is.EqualTo(4));

            var routing = new FishRoutingService();
            var guard = 0;
            while (flow.State != GameState.Win && guard < 80)
            {
                guard++;
                Assert.That(flow.State, Is.EqualTo(GameState.PlayerInput));
                var fish = FirstMatchingFish(flow.Session, routing);
                Assert.That(fish, Is.Not.Null);
                Click(ViewFor(fish.Id));
                yield return WaitUntilReady(flow);
                if (flow.State == GameState.Lose)
                {
                    Assert.Fail("Level_001 lost during the win check.");
                }
            }

            Assert.That(flow.State, Is.EqualTo(GameState.Win));
            Assert.That(flow.IsWinPanelVisible, Is.True);
            // M12.1: win grants +20 Gold on top of the post-unlock balance (1000 - 600 + 20).
            Assert.That(bootstrap.Progression.Progress.Score, Is.EqualTo(0));
            Assert.That(bootstrap.Progression.Progress.Lives, Is.EqualTo(4), "Win does not restore hearts.");
            Assert.That(bootstrap.Progression.Progress.Gold, Is.EqualTo(420));
            Assert.That(references.LivesDisplay.text, Is.EqualTo("4"));
            Assert.That(references.GoldDisplay.text, Is.EqualTo("420"));
            Assert.That(Flow().WinPanel.GoldText, Is.EqualTo("420"));
            flow.PresentCurrentOutcome();
            flow.PresentCurrentOutcome();
            Assert.That(bootstrap.Progression.Progress.Gold, Is.EqualTo(420));
            Assert.That(references.GoldDisplay.text, Is.EqualTo("420"));
            Assert.That(bootstrap.Progression.Progress.Lives, Is.EqualTo(4));
            yield return null;
            Assert.That(Count<WinPanelView>(), Is.EqualTo(1));
            Assert.That(Count<BubbleView>(), Is.EqualTo(0));
            Assert.That(Count<FishView>(), Is.EqualTo(0));
            Assert.That(Count<TankSlotView>(), Is.EqualTo(4));
        }

        private static void ClickLockedTank(int slotIndex)
        {
            var slot = Bootstrap().TankBoard.Slots[slotIndex];
            var plus = slot.transform.Find("LockedOverlay/Plus");
            Assert.That(plus, Is.Not.Null);
            var eventData = new PointerEventData(EventSystem.current);
            ExecuteEvents.Execute(plus.gameObject, eventData, ExecuteEvents.pointerClickHandler);
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

        private static IEnumerator WaitUntilReady(GameFlowController flow)
        {
            var elapsed = 0f;
            while (flow.IsPresentationBusy
                || (flow.State != GameState.PlayerInput && flow.State != GameState.Win && flow.State != GameState.Lose))
            {
                elapsed += Time.unscaledDeltaTime;
                if (elapsed > 8f)
                {
                    Assert.Fail("Resolution did not finish. State " + flow.State);
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

        private static void Click(FishView fish)
        {
            PointerGesture.Click(fish);
        }

        private static int Count<T>() where T : Object
        {
            return Object.FindObjectsByType<T>(FindObjectsInactive.Include).Length;
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
