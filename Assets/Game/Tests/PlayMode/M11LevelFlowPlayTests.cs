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
    public sealed class M11LevelFlowPlayTests
    {
        private static readonly int[] Totals = { 9, 15, 21, 36, 60, 51, 57, 60, 66, 75 };
        private static readonly int[] BubbleCounts = { 3, 5, 6, 9, 12, 12, 13, 14, 15, 17 };
        private const int LevelCount = 10;
        private const int PileSlots = 10;

        [UnitySetUp]
        public IEnumerator LoadGameplayScene()
        {
            yield return SceneManager.LoadSceneAsync("Gameplay", LoadSceneMode.Single);
            yield return null;
            Canvas.ForceUpdateCanvases();
        }

        [UnityTest]
        public IEnumerator SceneStart_UsesCatalogLevel001_AndShowsLevelBanner()
        {
            var bootstrap = Bootstrap();
            Assert.That(bootstrap.Catalog, Is.Not.Null, "Gameplay scene must reference LevelCatalog.");
            Assert.That(bootstrap.Catalog.Count, Is.EqualTo(LevelCount));
            Assert.That(bootstrap.CurrentLevelIndex, Is.EqualTo(0));
            Assert.That(bootstrap.LevelId, Is.EqualTo("level_001"));
            Assert.That(bootstrap.LevelLabel, Is.EqualTo("Màn 1"));
            Assert.That(bootstrap.LevelBadge, Is.Not.Null, "HUD level badge is applied.");
            Assert.That(bootstrap.LevelBadge.Icon, Is.Not.Null);
            Assert.That(bootstrap.GlobalProgressLabel, Is.EqualTo("0 / 9"));
            Assert.That(bootstrap.VisibleBubbleCount, Is.EqualTo(3), "Level 1 starts with only 3 bubbles.");
            Assert.That(bootstrap.PendingBubbleCount, Is.EqualTo(0));
            Assert.That(bootstrap.HasNextLevel, Is.True);
            Assert.That(bootstrap.Transition, Is.Not.Null);
            Assert.That(bootstrap.Transition.BannerText, Is.EqualTo("Màn 1"));
            Assert.That(bootstrap.Transition.IsFadeBlocking, Is.False);
            Assert.That(Flow().State, Is.EqualTo(GameState.PlayerInput));
            yield return null;
        }

        [UnityTest]
        public IEnumerator DebugLoad_EachLevel_ResetsAttemptState_AndUsesLevelTotals()
        {
            var bootstrap = Bootstrap();
            for (var index = 1; index < LevelCount; index++)
            {
                Assert.That(bootstrap.DebugLoadLevel(index), Is.True);
                yield return null;
                var flow = Flow();
                AssertFreshLevel(bootstrap, flow, index);
                AssertFishCentersInsideBubbles(bootstrap);
                Assert.That(bootstrap.Transition.BannerText, Is.EqualTo("Màn " + (index + 1)));
            }
        }

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator Level001_Win_Replay_ThenNextLoadsLevel002()
        {
            var bootstrap = Bootstrap();
            yield return PlayToWin(bootstrap);
            Assert.That(bootstrap.Progression.Progress.Gold, Is.EqualTo(1020));
            Assert.That(bootstrap.GoldHudText, Is.EqualTo("1020"), "Gold HUD updates when the reward commits.");
            var panel = Flow().WinPanel;
            Assert.That(panel, Is.Not.Null);
            Assert.That(panel.Message, Is.EqualTo(WinPanelView.TitleText));
            Assert.That(panel.Subtitle, Is.Empty, "All-levels caption only on the last level.");
            Assert.That(panel.RewardText, Is.EqualTo("+20"));
            Assert.That(panel.GoldText, Is.EqualTo("1020"), "Win screen shows the already-updated gold.");
            Assert.That(panel.ProgressCaption, Is.EqualTo(WinPanelView.ProgressCaptionText));
            Assert.That(panel.ProgressText, Is.EqualTo("1/" + LevelCount));
            Assert.That(panel.PrimaryButtonText, Is.EqualTo(WinPanelView.ClaimText));
            Assert.That(panel.DoubleButton, Is.Not.Null);
            Assert.That(panel.ReplayButton, Is.Not.Null);

            panel.ReplayButton.onClick.Invoke();
            panel.ReplayButton.onClick.Invoke();
            yield return WaitForTransition(bootstrap);
            AssertFreshLevel(bootstrap, Flow(), 0);
            Assert.That(bootstrap.LevelLoadCount, Is.EqualTo(1), "Double tap on Replay must rebuild once.");
            Assert.That(bootstrap.Progression.Progress.Gold, Is.EqualTo(1020), "Replay keeps gold.");
            Assert.That(bootstrap.GoldHudText, Is.EqualTo("1020"));

            yield return PlayToWin(bootstrap);
            Assert.That(bootstrap.Progression.Progress.Gold, Is.EqualTo(1040), "+20 Gold once per won attempt.");
            Flow().PresentCurrentOutcome();
            Assert.That(bootstrap.Progression.Progress.Gold, Is.EqualTo(1040));

            Flow().WinPanel.PrimaryButton.onClick.Invoke();
            Flow().WinPanel.PrimaryButton.onClick.Invoke();
            yield return WaitForTransition(bootstrap);
            AssertFreshLevel(bootstrap, Flow(), 1);
            Assert.That(bootstrap.LevelLoadCount, Is.EqualTo(2));
            Assert.That(bootstrap.Progression.Progress.Gold, Is.EqualTo(1040), "Next level keeps gold.");
            Assert.That(bootstrap.GoldHudText, Is.EqualTo("1040"));
            Assert.That(bootstrap.Progression.Progress.Lives, Is.EqualTo(5));
            Assert.That(bootstrap.Progression.Progress.Score, Is.EqualTo(0), "Score is no longer awarded.");
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator Level002_LoseRetry_ReloadsSameLevel_WithoutSecondHeart()
        {
            var bootstrap = Bootstrap();
            Assert.That(bootstrap.DebugLoadLevel(1), Is.True);
            yield return null;
            var flow = Flow();
            var gold = bootstrap.Progression.Progress.Gold;

            // Level_002 starts with Orange and GreenStriped targets. Five non-matching taps fill the tray.
            yield return TapNonMatching(flow, 5);

            Assert.That(flow.State, Is.EqualTo(GameState.Lose));
            Assert.That(flow.IsLosePanelVisible, Is.True);
            Assert.That(bootstrap.Progression.Progress.Lives, Is.EqualTo(4));
            var lose = flow.LosePanel;
            Assert.That(lose.Message, Is.EqualTo(LosePanelView.TitleText));
            Assert.That(lose.HeaderText, Is.EqualTo("Màn 2"));
            Assert.That(lose.LifeCostText, Is.EqualTo("-1"));
            Assert.That(Object.FindAnyObjectByType<GameplaySceneReferences>().LivesDisplay.text, Is.EqualTo("4"), "HUD hearts already updated.");
            flow.PresentCurrentOutcome();
            Assert.That(bootstrap.Progression.Progress.Lives, Is.EqualTo(4), "Re-presenting the outcome does not deduct again.");
            Assert.That(lose.RetryButton, Is.Not.Null);
            Assert.That(lose.CloseButton, Is.Not.Null);

            lose.RetryButton.onClick.Invoke();
            lose.RetryButton.onClick.Invoke();
            yield return null;

            AssertFreshLevel(bootstrap, Flow(), 1);
            Assert.That(flow.IsLosePanelVisible, Is.False);
            Assert.That(bootstrap.Progression.Progress.Lives, Is.EqualTo(4), "Retry must not deduct another heart.");
            Assert.That(bootstrap.Progression.Progress.Gold, Is.EqualTo(gold));
            Assert.That(bootstrap.LivesHudText, Is.EqualTo("4"));
            Assert.That(Count<LosePanelView>(), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator LoseClose_CollapsesToRetry_WithoutSoftLock()
        {
            // Level 1 cannot be lost (only three fish of the non-target type exist), so this uses Level 2.
            var bootstrap = Bootstrap();
            Assert.That(bootstrap.DebugLoadLevel(1), Is.True);
            yield return null;
            var flow = Flow();
            yield return TapNonMatching(flow, 5);

            Assert.That(flow.State, Is.EqualTo(GameState.Lose));
            var lose = flow.LosePanel;
            lose.CloseButton.onClick.Invoke();
            yield return null;
            Assert.That(flow.IsLosePanelVisible, Is.False);
            Assert.That(lose.IsCollapsed, Is.True);
            Assert.That(flow.State, Is.EqualTo(GameState.Lose));
            Assert.That(bootstrap.Progression.Progress.Lives, Is.EqualTo(4));

            lose.CollapsedRetryButton.onClick.Invoke();
            yield return null;
            AssertFreshLevel(bootstrap, Flow(), 1);
            Assert.That(lose.IsCollapsed, Is.False);
            Assert.That(bootstrap.Progression.Progress.Lives, Is.EqualTo(4));
        }

        [UnityTest]
        [Timeout(900000)]
        public IEnumerator FullSequence_Level001ToLevel010_ThenPlayAgain()
        {
            var bootstrap = Bootstrap();
            var started = Time.realtimeSinceStartup;
            for (var index = 0; index < LevelCount; index++)
            {
                AssertFreshLevel(bootstrap, Flow(), index);
                AssertFishCentersInsideBubbles(bootstrap);
                var levelStarted = Time.realtimeSinceStartup;
                yield return PlayToWin(bootstrap);
                Debug.Log("[M11] " + bootstrap.LevelId + " automated play seconds " + (Time.realtimeSinceStartup - levelStarted).ToString("0.0"));
                Assert.That(bootstrap.Progression.Progress.Gold, Is.EqualTo(1000 + (20 * (index + 1))));
                Assert.That(bootstrap.GoldHudText, Is.EqualTo((1000 + (20 * (index + 1))).ToString()));
                Assert.That(bootstrap.Progression.Progress.Lives, Is.EqualTo(5));

                var panel = Flow().WinPanel;
                if (index < LevelCount - 1)
                {
                    Assert.That(bootstrap.HasNextLevel, Is.True);
                    Assert.That(panel.PrimaryButtonText, Is.EqualTo(WinPanelView.ClaimText));
                    Assert.That(panel.ProgressText, Is.EqualTo((index + 1) + "/" + LevelCount));
                    panel.PrimaryButton.onClick.Invoke();
                    yield return WaitForTransition(bootstrap);
                }
            }

            Debug.Log("[M11] Level_001..Level_010 automated play seconds " + (Time.realtimeSinceStartup - started).ToString("0.0"));
            Assert.That(bootstrap.LevelId, Is.EqualTo("level_010"));
            Assert.That(bootstrap.HasNextLevel, Is.False);
            var last = Flow().WinPanel;
            Assert.That(last.HasNextLevel, Is.False);
            Assert.That(last.Subtitle, Is.EqualTo(WinPanelView.AllLevelsCompletedText));
            Assert.That(last.PrimaryButtonText, Is.EqualTo(WinPanelView.ClaimText));
            Assert.That(last.ProgressText, Is.EqualTo(LevelCount + "/" + LevelCount));
            Assert.That(last.ProgressFraction, Is.EqualTo(1f));
            Assert.That(bootstrap.RequestNextLevel(), Is.False, "Level_010 has no next level.");
            Assert.That(bootstrap.CurrentLevelIndex, Is.EqualTo(LevelCount - 1));
            Assert.That(Count<WinPanelView>(), Is.EqualTo(1));

            last.PrimaryButton.onClick.Invoke();
            yield return WaitForTransition(bootstrap);
            AssertFreshLevel(bootstrap, Flow(), 0);
            Assert.That(bootstrap.Progression.Progress.Gold, Is.EqualTo(1000 + (20 * LevelCount)), "Play again keeps gold.");
        }

        [UnityTest]
        public IEnumerator RemovingFish_DoesNotResizeRemainingFish()
        {
            var bootstrap = Bootstrap();
            Assert.That(bootstrap.DebugLoadLevel(4), Is.True);
            yield return null;
            var flow = Flow();
            var routing = new FishRoutingService();
            BubbleView source = null;
            FishRuntimeState tapped = null;
            var bubbles = bootstrap.BubblePile.VisibleBubbles;
            for (var i = 0; i < bubbles.Count && tapped == null; i++)
            {
                var state = FindBubbleState(flow.Session, bubbles[i].BubbleId);
                for (var f = 0; f < state.Fish.Count; f++)
                {
                    if (routing.SelectTank(state.Fish[f].Type, flow.Session.Tanks).Found)
                    {
                        source = bubbles[i];
                        tapped = state.Fish[f];
                        break;
                    }
                }
            }

            Assert.That(source, Is.Not.Null);
            Assert.That(source.FishViews.Count, Is.EqualTo(5));
            var tappedView = ViewFor(tapped.Id);
            PointerGesture.Click(tappedView);
            for (var frame = 0; frame < 6; frame++)
            {
                yield return null;
                AssertFixedFishSize(bootstrap);
            }

            yield return WaitUntilReady(flow);
            Assert.That(source.FishViews.Count, Is.EqualTo(4));
            AssertFixedFishSize(bootstrap);
            for (var f = 0; f < source.FishViews.Count; f++)
            {
                var scale = source.FishViews[f].transform.localScale;
                Assert.That(scale.x, Is.EqualTo(1f).Within(0.001f));
                Assert.That(scale.y, Is.EqualTo(1f).Within(0.001f));
            }

            // The routed fish keeps the 124 base size inside the tank (it overlaps instead of shrinking).
            Assert.That(tappedView.transform.parent.name, Does.StartWith("FishSlot_"));
            var landed = tappedView.transform as RectTransform;
            Assert.That(landed.rect.width, Is.EqualTo(BubbleFishLayoutController.FishSize).Within(0.5f));
            Assert.That(landed.rect.height, Is.EqualTo(BubbleFishLayoutController.FishSize).Within(0.5f));
            Assert.That(landed.localScale.x, Is.EqualTo(1f).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator ThreeFishInTank_OverlapAtBaseSize_ThenResolve()
        {
            var bootstrap = Bootstrap();
            var flow = Flow();
            var routing = new FishRoutingService();
            var target = flow.Session.Tanks[0].CurrentTarget;
            var widths = new List<float>();
            var positions = new List<Vector2>();
            for (var n = 0; n < 2; n++)
            {
                var fish = flow.Session.FindFirstIdleFish(target);
                var view = ViewFor(fish.Id);
                PointerGesture.Click(view);
                yield return WaitUntilReady(flow);
                var rect = view.transform as RectTransform;
                widths.Add(rect.rect.width);
                Assert.That(rect.parent.name, Is.EqualTo("FishSlot_" + n));
                Assert.That(flow.Session.Tanks[0].FillCount, Is.EqualTo(n + 1));
            }

            var slot0 = bootstrap.TankBoard.Slots[0].GetFishAnchor(0);
            var slot1 = bootstrap.TankBoard.Slots[0].GetFishAnchor(1);
            Assert.That(slot0.anchoredPosition, Is.EqualTo(TankSlotView.TankFishPosition(0, 2)));
            Assert.That(slot1.anchoredPosition, Is.EqualTo(TankSlotView.TankFishPosition(1, 2)));
            Assert.That(slot1.GetSiblingIndex(), Is.GreaterThan(slot0.GetSiblingIndex()), "Later fish draw in front.");
            for (var i = 0; i < widths.Count; i++)
            {
                Assert.That(widths[i], Is.EqualTo(BubbleFishLayoutController.FishSize).Within(0.5f), "Tank fish keep the base size.");
            }

            var third = flow.Session.FindFirstIdleFish(target);
            PointerGesture.Click(ViewFor(third.Id));
            yield return WaitUntilReady(flow);
            Assert.That(flow.Session.Progress.CollectedFishCount, Is.EqualTo(3));
            Assert.That(flow.State, Is.EqualTo(GameState.PlayerInput));
        }

        private static BubbleRuntimeState FindBubbleState(LevelSession session, string bubbleId)
        {
            for (var i = 0; i < session.Bubbles.Count; i++)
            {
                if (session.Bubbles[i].BubbleId == bubbleId)
                {
                    return session.Bubbles[i];
                }
            }

            return null;
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator WinClaimDouble_ContinuesSafely_WithoutExtraGold()
        {
            var bootstrap = Bootstrap();
            yield return PlayToWin(bootstrap);
            Assert.That(bootstrap.Progression.Progress.Gold, Is.EqualTo(1020));
            Assert.That(bootstrap.GoldHudText, Is.EqualTo("1020"));
            var panel = Flow().WinPanel;
            panel.DoubleButton.onClick.Invoke();
            panel.DoubleButton.onClick.Invoke();
            panel.PrimaryButton.onClick.Invoke();
            yield return WaitForTransition(bootstrap);
            AssertFreshLevel(bootstrap, Flow(), 1);
            Assert.That(bootstrap.LevelLoadCount, Is.EqualTo(1), "One continue request per win screen.");
            Assert.That(bootstrap.Progression.Progress.Gold, Is.EqualTo(1020), "Nhận x2 grants nothing extra until rewarded doubling exists.");
            Assert.That(bootstrap.LevelLabel, Is.EqualTo("Màn 2"));
        }

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator M121_HeartsPersist_LoseRetryLoseNext_AndHudShowsOnlyLevelGoldHearts()
        {
            var bootstrap = Bootstrap();
            Assert.That(GameObject.Find("ScoreLabel"), Is.Null, "Standalone Score HUD removed.");
            Assert.That(bootstrap.GoldHudText, Is.EqualTo("1000"));
            Assert.That(bootstrap.LivesHudText, Is.EqualTo("5"));

            Assert.That(bootstrap.DebugLoadLevel(1), Is.True);
            yield return null;
            yield return TapNonMatching(Flow(), 5);
            Assert.That(Flow().State, Is.EqualTo(GameState.Lose));
            Assert.That(bootstrap.Progression.Progress.Lives, Is.EqualTo(4));
            Assert.That(bootstrap.LivesHudText, Is.EqualTo("4"));
            Flow().PresentCurrentOutcome();
            Assert.That(bootstrap.Progression.Progress.Lives, Is.EqualTo(4), "Duplicate Lose does not deduct twice.");

            Flow().LosePanel.RetryButton.onClick.Invoke();
            yield return null;
            AssertFreshLevel(bootstrap, Flow(), 1);
            Assert.That(bootstrap.LivesHudText, Is.EqualTo("4"), "Retry keeps hearts.");

            yield return TapNonMatching(Flow(), 5);
            Assert.That(bootstrap.Progression.Progress.Lives, Is.EqualTo(3));
            Assert.That(bootstrap.LivesHudText, Is.EqualTo("3"));

            Assert.That(bootstrap.DebugLoadLevel(2), Is.True);
            yield return null;
            AssertFreshLevel(bootstrap, Flow(), 2);
            Assert.That(bootstrap.Progression.Progress.Lives, Is.EqualTo(3), "Next level keeps current hearts.");
            Assert.That(bootstrap.LivesHudText, Is.EqualTo("3"));
            Assert.That(bootstrap.GoldHudText, Is.EqualTo("1000"));
            Assert.That(GameObject.Find("ScoreLabel"), Is.Null);

            yield return PlayToWin(bootstrap);
            Assert.That(bootstrap.Progression.Progress.Lives, Is.EqualTo(3), "Win does not restore hearts.");
            Assert.That(bootstrap.Progression.Progress.Gold, Is.EqualTo(1020));
            Assert.That(bootstrap.GoldHudText, Is.EqualTo("1020"));
            Assert.That(Flow().WinPanel.RewardText, Is.EqualTo("+20"));
            Assert.That(Flow().WinPanel.GoldText, Is.EqualTo("1020"));
        }

        [UnityTest]
        public IEnumerator LosePanel_ClosePopsIn_AndRetryRestoresBadge()
        {
            var bootstrap = Bootstrap();
            Assert.That(bootstrap.DebugLoadLevel(2), Is.True);
            yield return null;
            var flow = Flow();
            yield return TapNonMatching(flow, 5);
            Assert.That(flow.State, Is.EqualTo(GameState.Lose));
            var lose = flow.LosePanel;
            Assert.That(lose.HeaderText, Is.EqualTo("Màn 3"));
            Assert.That(lose.RetryButton.GetComponentInChildren<TMPro.TextMeshProUGUI>().text, Is.EqualTo(LosePanelView.RetryText));
            yield return new WaitForSecondsRealtime(ModalPopIn.Duration + 0.1f);
            Assert.That(lose.Card.localScale.x, Is.GreaterThan(0.5f));
            lose.RetryButton.onClick.Invoke();
            yield return null;
            Assert.That(bootstrap.LevelLabel, Is.EqualTo("Màn 3"));
            Assert.That(bootstrap.Progression.Progress.Lives, Is.EqualTo(4));
        }

        private static IEnumerator PlayToWin(LevelSceneBootstrapper bootstrap)
        {
            var flow = Flow();
            var routing = new FishRoutingService();
            var guard = 0;
            while (flow.State != GameState.Win && guard < 160)
            {
                guard++;
                Assert.That(flow.State, Is.EqualTo(GameState.PlayerInput), Describe(bootstrap));
                var fish = FirstMatchingFish(flow.Session, routing) ?? FallbackFish(flow.Session, bootstrap.Level);
                Assert.That(fish, Is.Not.Null, Describe(bootstrap));
                PointerGesture.Click(ViewFor(fish.Id));
                yield return WaitUntilReady(flow);
                Assert.That(flow.State, Is.Not.EqualTo(GameState.Lose), Describe(bootstrap));
            }

            Assert.That(flow.State, Is.EqualTo(GameState.Win), Describe(bootstrap));
            Assert.That(flow.IsWinPanelVisible, Is.True);
            Assert.That(flow.Session.Progress.CollectedFishCount, Is.EqualTo(bootstrap.TotalFishRequired));
            Assert.That(flow.Session.Tray.Count, Is.EqualTo(0));
            yield return null;
            Assert.That(Count<BubbleView>(), Is.EqualTo(0));
            Assert.That(Count<FishView>(), Is.EqualTo(0));
        }

        private static void AssertFreshLevel(LevelSceneBootstrapper bootstrap, GameFlowController flow, int index)
        {
            var number = index + 1;
            Assert.That(bootstrap.CurrentLevelIndex, Is.EqualTo(index));
            Assert.That(bootstrap.LevelId, Is.EqualTo("level_" + number.ToString("000")));
            Assert.That(bootstrap.LevelLabel, Is.EqualTo("Màn " + number));
            Assert.That(flow.State, Is.EqualTo(GameState.PlayerInput));
            Assert.That(flow.IsWinPanelVisible, Is.False);
            Assert.That(flow.IsLosePanelVisible, Is.False);
            Assert.That(flow.Session.Progress.CollectedFishCount, Is.EqualTo(0));
            Assert.That(flow.Session.Progress.TotalFishRequired, Is.EqualTo(Totals[index]));
            Assert.That(bootstrap.GlobalProgressLabel, Is.EqualTo("0 / " + Totals[index]));
            Assert.That(flow.Session.Tray.Count, Is.EqualTo(0));
            Assert.That(bootstrap.WaitingTray.PresentedOccupantCount, Is.EqualTo(0));
            Assert.That(flow.Session.Targets.NextUnassignedIndex, Is.EqualTo(2));
            Assert.That(flow.Session.Tanks[2].State, Is.EqualTo(TankState.Locked));
            Assert.That(flow.Session.Tanks[3].State, Is.EqualTo(TankState.Locked));
            var level = bootstrap.Level;
            Assert.That(level.BubbleQueue.Count, Is.EqualTo(BubbleCounts[index]));
            var visible = Mathf.Min(BubbleCounts[index], PileSlots);
            var expectedFish = 0;
            for (var i = 0; i < visible; i++)
            {
                expectedFish += level.BubbleQueue[i].Fishes.Count;
            }

            Assert.That(flow.Session.PendingBubbleCount, Is.EqualTo(BubbleCounts[index] - visible));
            Assert.That(bootstrap.VisibleBubbleCount, Is.EqualTo(visible));
            Assert.That(Count<BubbleView>(), Is.EqualTo(visible), "No duplicated bubbles.");
            Assert.That(Count<TankSlotView>(), Is.EqualTo(4), "No duplicated tanks.");
            Assert.That(Count<FishView>(), Is.EqualTo(expectedFish));
            Assert.That(Count<LevelTransitionView>(), Is.EqualTo(1));
            Assert.That(Count<GameFlowController>(), Is.EqualTo(1));
            var bubbles = bootstrap.BubblePile.VisibleBubbles;
            for (var i = 0; i < bubbles.Count; i++)
            {
                var definition = FindDefinition(level, bubbles[i].BubbleId);
                Assert.That(definition, Is.Not.Null, bubbles[i].BubbleId);
                Assert.That(bubbles[i].FishViews.Count, Is.EqualTo(definition.Fishes.Count), bubbles[i].BubbleId);
            }

            AssertFixedFishSize(bootstrap);
        }

        private static BubbleDefinition FindDefinition(LevelData level, string bubbleId)
        {
            for (var i = 0; i < level.BubbleQueue.Count; i++)
            {
                if (level.BubbleQueue[i].BubbleId == bubbleId)
                {
                    return level.BubbleQueue[i];
                }
            }

            return null;
        }

        private static void AssertFixedFishSize(LevelSceneBootstrapper bootstrap)
        {
            var bubbles = bootstrap.BubblePile.VisibleBubbles;
            for (var i = 0; i < bubbles.Count; i++)
            {
                var fish = bubbles[i].FishViews;
                for (var f = 0; f < fish.Count; f++)
                {
                    var rect = fish[f].transform as RectTransform;
                    if (rect.parent == null || rect.parent.name != "FishContainer")
                    {
                        continue;
                    }

                    Assert.That(rect.sizeDelta.x, Is.EqualTo(BubbleFishLayoutController.FishSize).Within(0.01f), bubbles[i].BubbleId + " fish " + f);
                    Assert.That(rect.sizeDelta.y, Is.EqualTo(BubbleFishLayoutController.FishSize).Within(0.01f), bubbles[i].BubbleId + " fish " + f);
                }
            }
        }

        /// <summary>Overflow past the rim is allowed (M11.2). Only fish centers must lie inside the bubble.</summary>
        private static void AssertFishCentersInsideBubbles(LevelSceneBootstrapper bootstrap)
        {
            Canvas.ForceUpdateCanvases();
            var bubbles = bootstrap.BubblePile.VisibleBubbles;
            for (var i = 0; i < bubbles.Count; i++)
            {
                var bubbleRect = bubbles[i].transform as RectTransform;
                var radius = bubbleRect.rect.width * 0.5f;
                var fish = bubbles[i].FishViews;
                for (var f = 0; f < fish.Count; f++)
                {
                    Vector2 local = bubbleRect.InverseTransformPoint(fish[f].transform.position);
                    Assert.That(local.magnitude, Is.LessThan(radius * 0.8f), bubbles[i].BubbleId + " fish " + f + " center " + local);
                }
            }
        }

        private static IEnumerator TapNonMatching(GameFlowController flow, int count)
        {
            var routing = new FishRoutingService();
            for (var n = 0; n < count; n++)
            {
                FishRuntimeState pick = null;
                var bubbles = flow.Session.Bubbles;
                for (var i = 0; i < bubbles.Count && pick == null; i++)
                {
                    var bubble = bubbles[i];
                    if (bubble == null || !bubble.IsInPlay || !bubble.IsInteractable)
                    {
                        continue;
                    }

                    for (var f = 0; f < bubble.Fish.Count; f++)
                    {
                        if (bubble.Fish[f].State == FishState.Idle && !routing.SelectTank(bubble.Fish[f].Type, flow.Session.Tanks).Found)
                        {
                            pick = bubble.Fish[f];
                            break;
                        }
                    }
                }

                Assert.That(pick, Is.Not.Null, "No non-matching fish to tap.");
                PointerGesture.Click(ViewFor(pick.Id));
                yield return WaitUntilReady(flow);
            }
        }

        private static IEnumerator WaitForTransition(LevelSceneBootstrapper bootstrap)
        {
            yield return null;
            var elapsed = 0f;
            while (bootstrap.IsTransitioning)
            {
                elapsed += Time.unscaledDeltaTime;
                if (elapsed > 5f)
                {
                    Assert.Fail("Level transition did not finish.");
                }

                yield return null;
            }

            yield return null;
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

        private static FishRuntimeState FallbackFish(LevelSession session, LevelData level)
        {
            FishRuntimeState best = null;
            var bestSoon = int.MaxValue;
            var bestRemaining = int.MaxValue;
            for (var i = 0; i < session.Bubbles.Count; i++)
            {
                var bubble = session.Bubbles[i];
                if (bubble == null || !bubble.IsInPlay || !bubble.IsInteractable)
                {
                    continue;
                }

                for (var f = 0; f < bubble.Fish.Count; f++)
                {
                    var candidate = bubble.Fish[f];
                    if (candidate.State != FishState.Idle)
                    {
                        continue;
                    }

                    var soon = int.MaxValue / 2;
                    for (var q = session.Targets.NextUnassignedIndex; q < level.TargetGroupQueue.Count; q++)
                    {
                        if (level.TargetGroupQueue[q] == candidate.Type)
                        {
                            soon = q;
                            break;
                        }
                    }

                    if (best == null || soon < bestSoon || (soon == bestSoon && bubble.RemainingFishCount < bestRemaining))
                    {
                        best = candidate;
                        bestSoon = soon;
                        bestRemaining = bubble.RemainingFishCount;
                    }
                }
            }

            return best;
        }

        private static string Describe(LevelSceneBootstrapper bootstrap)
        {
            var flow = bootstrap.Flow;
            var session = flow != null ? flow.Session : null;
            if (session == null)
            {
                return "no session";
            }

            return bootstrap.LevelId
                + " state " + flow.State
                + " busy " + flow.IsPresentationBusy
                + " progress " + session.Progress.CollectedFishCount + "/" + session.Progress.TotalFishRequired
                + " tray " + session.Tray.Count
                + " pending " + session.PendingBubbleCount;
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
