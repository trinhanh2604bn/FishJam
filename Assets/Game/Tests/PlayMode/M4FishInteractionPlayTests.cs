using System.Collections;
using System.Collections.Generic;
using System.Text;
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
    public sealed class M4FishInteractionPlayTests
    {
        [UnitySetUp]
        public IEnumerator LoadGameplayScene()
        {
            yield return SceneManager.LoadSceneAsync("Gameplay", LoadSceneMode.Single);
            yield return null;
            Canvas.ForceUpdateCanvases();
        }

        [UnityTest]
        public IEnumerator PlayableLevel_StartsWithOrangeAndGreenTargets()
        {
            var flow = Flow();
            var board = Bootstrap().TankBoard;

            Assert.That(flow.State, Is.EqualTo(GameState.PlayerInput));
            Assert.That(flow.Session.Tanks[0].CurrentTarget, Is.EqualTo(FishType.Orange));
            Assert.That(flow.Session.Tanks[1].CurrentTarget, Is.EqualTo(FishType.GreenStriped));
            Assert.That(flow.Session.Targets.NextUnassignedIndex, Is.EqualTo(2));
            Assert.That(flow.Session.Tanks[2].State, Is.EqualTo(TankState.Locked));
            Assert.That(flow.Session.Tanks[3].State, Is.EqualTo(TankState.Locked));
            Assert.That(flow.Session.Progress.CollectedFishCount, Is.EqualTo(0));
            Assert.That(flow.Session.Progress.TotalFishRequired, Is.EqualTo(36));
            Assert.That(flow.Session.Tray.Count, Is.EqualTo(0));
            Assert.That(Bootstrap().GlobalProgressLabel, Is.EqualTo("0 / 36"));
            Assert.That(board.Slots[0].PreviewBadge.PreviewFishType, Is.EqualTo(FishType.Orange));
            Assert.That(board.Slots[0].PreviewBadge.PreviewProgressText, Is.EqualTo("0 / 3"));
            Assert.That(board.Slots[1].PreviewBadge.PreviewFishType, Is.EqualTo(FishType.GreenStriped));
            Assert.That(board.Slots[1].PreviewBadge.PreviewProgressText, Is.EqualTo("0 / 3"));
            Assert.That(flow.IsLosePanelVisible, Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator RaycastClick_OrangeFish_EntersTankAndIgnoresDoubleTap()
        {
            var flow = Flow();
            var fish = FindTopmostFish(FishType.Orange);
            Assert.That(fish, Is.Not.Null, "No Orange fish was the topmost raycast target.\n" + DescribeFirstFish(FishType.Orange));
            var fishId = fish.FishId;
            var bubble = fish.GetComponentInParent<BubbleView>();
            var before = bubble.FishViews.Count;
            var fishBefore = CountFishViews();

            Click(fish);
            yield return null;

            Assert.That(flow.Session.Tanks[0].FillCount, Is.EqualTo(1));
            Assert.That(flow.Session.Tanks[0].ContainedFish[0].Id, Is.EqualTo(fishId));
            Assert.That(flow.Session.Tray.Count, Is.EqualTo(0));
            Assert.That(flow.Session.Progress.CollectedFishCount, Is.EqualTo(0));
            Assert.That(bubble.FishViews.Count, Is.EqualTo(before - 1));
            Assert.That(bubble.gameObject.activeInHierarchy, Is.True);
            Assert.That(fish.transform.parent.name, Is.EqualTo("FishSlot_0"));
            Assert.That(Bootstrap().TankBoard.Slots[0].PreviewBadge.PreviewProgressText, Is.EqualTo("1 / 3"));
            Assert.That(CountFishViews(), Is.EqualTo(fishBefore));
            AssertDistinctRemainingLayout(bubble);

            Click(fish);
            flow.Session.TrySelectFish(fishId, null);
            yield return null;

            Assert.That(flow.Session.Tanks[0].FillCount, Is.EqualTo(1));
            Assert.That(flow.Session.CountPlacements(fishId), Is.EqualTo(1));
            Assert.That(CountFishViews(), Is.EqualTo(fishBefore));
            Assert.That(flow.State, Is.EqualTo(GameState.PlayerInput));
        }

        [UnityTest]
        public IEnumerator Click_NonTargetFish_EntersWaitingTray()
        {
            var flow = Flow();
            var fish = ViewFor(flow.Session.FindFirstIdleFish(FishType.PinkStriped).Id);

            Click(fish);
            yield return null;

            Assert.That(flow.Session.Tray.Count, Is.EqualTo(1));
            Assert.That(flow.Session.Tray.GetFishAt(0).Type, Is.EqualTo(FishType.PinkStriped));
            Assert.That(flow.Session.Tanks[0].FillCount, Is.EqualTo(0));
            Assert.That(flow.Session.Tanks[1].FillCount, Is.EqualTo(0));
            Assert.That(fish.transform.parent.name, Is.EqualTo("Slot_0"));
            Assert.That(Bootstrap().WaitingTray.PresentedOccupantCount, Is.EqualTo(1));
            Assert.That(Bootstrap().VisibleBubbleCount, Is.EqualTo(10));
        }

        [UnityTest]
        public IEnumerator ThirdOrange_CompletesTank_AddsThreeProgress_AssignsNextTarget()
        {
            var flow = Flow();
            for (var i = 0; i < 3; i++)
            {
                var fish = ViewFor(flow.Session.FindFirstIdleFish(FishType.Orange).Id);
                Click(fish);
            }

            yield return null;

            Assert.That(flow.State, Is.EqualTo(GameState.PlayerInput));
            Assert.That(flow.Session.Progress.CollectedFishCount, Is.EqualTo(3));
            Assert.That(flow.Session.Progress.TotalFishRequired, Is.EqualTo(flow.Session.Config.TankCapacity * 12));
            Assert.That(Bootstrap().GlobalProgressLabel, Is.EqualTo("3 / 36"));
            Assert.That(Bootstrap().CollectedFishDisplay, Is.EqualTo(3));
            Assert.That(flow.Session.Tanks[0].FillCount, Is.EqualTo(0));
            Assert.That(flow.Session.Tanks[0].CurrentTarget, Is.EqualTo(FishType.RedClown));
            Assert.That(flow.Session.Targets.NextUnassignedIndex, Is.EqualTo(3));
            Assert.That(Bootstrap().TankBoard.Slots[0].PreviewBadge.PreviewFishType, Is.EqualTo(FishType.RedClown));
            Assert.That(Bootstrap().TankBoard.Slots[0].PreviewBadge.PreviewProgressText, Is.EqualTo("0 / 3"));
            Assert.That(flow.Session.Tanks[1].CurrentTarget, Is.EqualTo(FishType.GreenStriped));
            Assert.That(flow.Session.Tanks[1].FillCount, Is.EqualTo(0));
            Assert.That(Bootstrap().VisibleBubbleCount, Is.EqualTo(10));
            Assert.That(CountFishViews(), Is.EqualTo(27));
            Assert.That(flow.IsLosePanelVisible, Is.False);
        }

        [UnityTest]
        public IEnumerator FifthWaitingFish_ShowsLose_AndBlocksFurtherInput()
        {
            var flow = Flow();
            var references = Object.FindAnyObjectByType<GameplaySceneReferences>();
            Assert.That(references.LivesDisplay.text, Is.EqualTo("5"));

            for (var i = 0; i < 5; i++)
            {
                var fish = ViewFor(flow.Session.FindFirstIdleFish(FishType.PinkStriped).Id);
                Click(fish);
            }

            yield return null;

            var blocked = flow.Session.FindFirstIdleFish(FishType.Orange);
            var blockedView = ViewFor(blocked.Id);
            Click(blockedView);
            var ignored = flow.Session.TrySelectFish(blocked.Id, null);
            yield return null;

            Assert.That(flow.State, Is.EqualTo(GameState.Lose));
            Assert.That(flow.Session.Tray.Count, Is.EqualTo(5));
            Assert.That(Bootstrap().WaitingTray.PresentedOccupantCount, Is.EqualTo(5));
            Assert.That(flow.IsLosePanelVisible, Is.True);
            var panel = Object.FindAnyObjectByType<LosePanelView>();
            Assert.That(panel, Is.Not.Null);
            Assert.That(panel.Message, Is.EqualTo("Lose"));
            Assert.That(ignored.Outcome, Is.EqualTo(FishSelectionOutcome.Ignored));
            Assert.That(blocked.State, Is.EqualTo(FishState.Idle));
            Assert.That(flow.Session.Tray.Count, Is.EqualTo(5));
            Assert.That(flow.Session.Progress.CollectedFishCount, Is.EqualTo(0));
            Assert.That(flow.Session.Tanks[0].CurrentTarget, Is.EqualTo(FishType.Orange));
            Assert.That(references.LivesDisplay.text, Is.EqualTo("4"));
            Assert.That(blockedView.transform.parent.name, Is.EqualTo("FishContainer"));
        }

        private static void AssertDistinctRemainingLayout(BubbleView bubble)
        {
            Assert.That(bubble.FishViews.Count, Is.EqualTo(2));
            var first = bubble.FishViews[0].GetComponent<RectTransform>().anchoredPosition.x;
            var second = bubble.FishViews[1].GetComponent<RectTransform>().anchoredPosition.x;
            Assert.That(first, Is.Not.EqualTo(second));
        }

        private static FishView FindTopmostFish(FishType type)
        {
            var views = Object.FindObjectsByType<FishView>(FindObjectsInactive.Exclude);
            for (var i = 0; i < views.Length; i++)
            {
                if (views[i] != null && views[i].DisplayedType == type && IsTopmostClick(views[i]))
                {
                    return views[i];
                }
            }

            return null;
        }

        private static bool IsTopmostClick(FishView fish)
        {
            if (EventSystem.current == null || fish == null)
            {
                return false;
            }

            var rect = fish.transform as RectTransform;
            var canvas = fish.GetComponentInParent<Canvas>();
            var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            var screen = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
            var data = new PointerEventData(EventSystem.current);
            data.position = screen;
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(data, hits);
            if (hits.Count == 0)
            {
                return false;
            }

            var handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
            return handler == fish.gameObject;
        }

        private static string DescribeFirstFish(FishType type)
        {
            var views = Object.FindObjectsByType<FishView>(FindObjectsInactive.Exclude);
            var report = new StringBuilder();
            report.Append("Screen ");
            report.Append(Screen.width);
            report.Append('x');
            report.Append(Screen.height);
            report.Append(" EventSystem ");
            report.Append(EventSystem.current != null);
            for (var i = 0; i < views.Length; i++)
            {
                if (views[i] == null || views[i].DisplayedType != type)
                {
                    continue;
                }

                report.Append("\n");
                report.Append(views[i].name);
                report.Append(" parent ");
                report.Append(views[i].transform.parent != null ? views[i].transform.parent.name : "none");
                report.Append(" top ");
                report.Append(TopHitName(views[i]));
                break;
            }

            return report.ToString();
        }

        private static string TopHitName(FishView fish)
        {
            var rect = fish.transform as RectTransform;
            var canvas = fish.GetComponentInParent<Canvas>();
            var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            var screen = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
            var data = new PointerEventData(EventSystem.current);
            data.position = screen;
            var hits = new List<RaycastResult>();
            if (EventSystem.current != null)
            {
                EventSystem.current.RaycastAll(data, hits);
            }

            if (hits.Count == 0)
            {
                return "none @" + screen;
            }

            return hits[0].gameObject.name + " @" + screen;
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
            var eventData = new PointerEventData(EventSystem.current);
            ExecuteEvents.Execute(fish.gameObject, eventData, ExecuteEvents.pointerClickHandler);
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
