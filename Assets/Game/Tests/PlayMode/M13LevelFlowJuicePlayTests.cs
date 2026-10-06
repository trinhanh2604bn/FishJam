using System.Collections;
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
    /// <summary>M13 combo/audio across the level flow on the shipped catalog (Level_001 onward).</summary>
    public sealed class M13LevelFlowJuicePlayTests
    {
        [UnitySetUp]
        public IEnumerator LoadGameplayScene()
        {
            yield return SceneManager.LoadSceneAsync("Gameplay", LoadSceneMode.Single);
            yield return null;
            Canvas.ForceUpdateCanvases();
        }

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator FullLevel_ReachesWin_WithJuice_ThenReplayAndNextResetCombo()
        {
            var bootstrap = Bootstrap();
            var flow = Flow();
            var audio = bootstrap.Audio;
            flow.SetPresentationClock(() => 100f);

            yield return PlayToWin(bootstrap);
            Assert.That(flow.ComboStreak, Is.GreaterThanOrEqualTo(1), "Frozen clock keeps the streak alive until Win.");
            Assert.That(flow.LandingFxCount, Is.EqualTo(bootstrap.TotalFishRequired), "Every fish lands in a tank with the landing FX.");
            Assert.That(flow.TrailEmitCount, Is.GreaterThan(0));
            Assert.That(audio.PlayCount(SfxId.Win), Is.EqualTo(1));
            Assert.That(audio.PlayCount(SfxId.Lose), Is.EqualTo(0));
            Assert.That(audio.PlayCount(SfxId.TankComplete), Is.EqualTo(bootstrap.TotalFishRequired / 3));
            Assert.That(audio.PlayCount(SfxId.BubblePop), Is.GreaterThan(0));
            Assert.That(bootstrap.Progression.Progress.Gold, Is.EqualTo(1020), "Combo never adds gold.");

            var taps = audio.PlayCount(SfxId.ButtonTap);
            var resets = flow.ComboResetCount;
            flow.WinPanel.ReplayButton.onClick.Invoke();
            yield return WaitForTransition(bootstrap);
            Assert.That(audio.PlayCount(SfxId.ButtonTap), Is.EqualTo(taps + 1));
            Assert.That(Flow().ComboStreak, Is.EqualTo(0), "Replay resets the combo.");
            Assert.That(Flow().ComboResetCount, Is.GreaterThan(resets));
            Assert.That(Flow().LastComboTier, Is.EqualTo(ComboTier.None));

            Flow().SetPresentationClock(() => 200f);
            yield return PlayToWin(bootstrap);
            Assert.That(Flow().ComboStreak, Is.GreaterThanOrEqualTo(1));
            Assert.That(audio.PlayCount(SfxId.Win), Is.EqualTo(2));
            Flow().WinPanel.PrimaryButton.onClick.Invoke();
            yield return WaitForTransition(bootstrap);
            Assert.That(bootstrap.CurrentLevelIndex, Is.EqualTo(1));
            Assert.That(Flow().ComboStreak, Is.EqualTo(0), "Next Level resets the combo.");
            Assert.That(Flow().LastComboTier, Is.EqualTo(ComboTier.None));
            Assert.That(Flow().LandingFxCount, Is.EqualTo(0));
            Assert.That(Flow().ComboView == null || Flow().ComboView.ActiveLabelCount == 0, Is.True);
            Assert.That(Flow().State, Is.EqualTo(GameState.PlayerInput));
        }

        private static IEnumerator PlayToWin(LevelSceneBootstrapper bootstrap)
        {
            var flow = Flow();
            var routing = new FishRoutingService();
            var guard = 0;
            while (flow.State != GameState.Win && guard < 160)
            {
                guard++;
                Assert.That(flow.State, Is.EqualTo(GameState.PlayerInput));
                var fish = FirstMatchingFish(flow.Session, routing) ?? FallbackFish(flow.Session, bootstrap.Level);
                Assert.That(fish, Is.Not.Null);
                PointerGesture.Click(ViewFor(fish.Id));
                yield return WaitUntilReady(flow);
                Assert.That(flow.State, Is.Not.EqualTo(GameState.Lose));
            }

            Assert.That(flow.State, Is.EqualTo(GameState.Win));
            Assert.That(flow.IsWinPanelVisible, Is.True);
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

                for (var f = 0; f < bubble.Fish.Count; f++)
                {
                    var candidate = bubble.Fish[f];
                    if (candidate != null && candidate.State == FishState.Idle && routing.SelectTank(candidate.Type, session.Tanks).Found)
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

                    if (best == null || soon < bestSoon)
                    {
                        best = candidate;
                        bestSoon = soon;
                    }
                }
            }

            return best;
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
