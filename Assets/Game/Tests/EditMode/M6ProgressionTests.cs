using System.Collections.Generic;
using FishPuzzle.Ads;
using FishPuzzle.Core;
using FishPuzzle.Domain;
using FishPuzzle.Progression;
using FishPuzzle.Tanks;
using NUnit.Framework;
using UnityEditor;

namespace FishPuzzle.Tests.EditMode
{
    public sealed class M6ProgressionTests
    {
        private const string LevelPath = "Assets/Game/Tests/Fixtures/LegacyLevel_036.asset";
        private const string ConfigPath = "Assets/Game/Data/Config/GameConfig.asset";

        [Test]
        public void Win_AwardsTwentyScoreOnce()
        {
            var config = Config();
            var progression = new ProgressionRuntime(PlayerProgress.CreateDevelopmentDefaults());
            progression.BeginAttempt();
            var session = WinSession(config);
            session.SetOutcomeHandler(state => progression.Settle(state, config));

            SelectCount(session, FishType.Orange, config.TankCapacity);

            Assert.That(session.State, Is.EqualTo(GameState.Win));
            Assert.That(config.LevelCompleteScoreReward, Is.EqualTo(20));
            Assert.That(progression.Progress.Score, Is.EqualTo(20));
        }

        [Test]
        public void DuplicateWin_DoesNotAwardScoreAgain()
        {
            var config = Config();
            var progression = new ProgressionRuntime(PlayerProgress.CreateDevelopmentDefaults());
            progression.BeginAttempt();
            var session = WinSession(config);
            session.SetOutcomeHandler(state => progression.Settle(state, config));
            SelectCount(session, FishType.Orange, config.TankCapacity);

            var second = progression.Settle(GameState.Win, config);
            var third = progression.Settle(GameState.Win, config);

            Assert.That(second, Is.False);
            Assert.That(third, Is.False);
            Assert.That(progression.Progress.Score, Is.EqualTo(20));
        }

        [Test]
        public void Lose_RemovesOneHeartOnce()
        {
            var config = Config();
            var progression = new ProgressionRuntime(PlayerProgress.CreateDevelopmentDefaults());
            progression.BeginAttempt();
            var session = LoseSession(config);
            session.SetOutcomeHandler(state => progression.Settle(state, config));

            SelectCount(session, FishType.PinkStriped, config.WaitingTrayFailCount);

            Assert.That(session.State, Is.EqualTo(GameState.Lose));
            Assert.That(config.LoseLifeCost, Is.EqualTo(1));
            Assert.That(progression.Progress.Lives, Is.EqualTo(4));
        }

        [Test]
        public void DuplicateLose_DoesNotRemoveAnotherHeart()
        {
            var config = Config();
            var progression = new ProgressionRuntime(PlayerProgress.CreateDevelopmentDefaults());
            progression.BeginAttempt();
            var session = LoseSession(config);
            session.SetOutcomeHandler(state => progression.Settle(state, config));
            SelectCount(session, FishType.PinkStriped, config.WaitingTrayFailCount);

            var second = progression.Settle(GameState.Lose, config);
            var third = progression.Settle(GameState.Lose, config);

            Assert.That(second, Is.False);
            Assert.That(third, Is.False);
            Assert.That(progression.Progress.Lives, Is.EqualTo(4));
            Assert.That(progression.Progress.Score, Is.EqualTo(0));
            Assert.That(progression.Progress.Gold, Is.EqualTo(1000));
        }

        [Test]
        public void GoldUnlock_SpendsSixHundredAndUnlocksTank()
        {
            var config = Config();
            var progress = PlayerProgress.CreateDevelopmentDefaults();
            var wallet = new WalletService();
            var session = UnlockSession(config);
            Assert.That(session.TryBeginUnlockModal(2), Is.True);

            var result = session.TryUnlockWithGold(2, progress, wallet);

            Assert.That(config.TankUnlockGoldCost, Is.EqualTo(600));
            Assert.That(result.Succeeded, Is.True);
            Assert.That(progress.Gold, Is.EqualTo(400));
            Assert.That(session.Tanks[2].IsUnlocked, Is.True);
            Assert.That(session.Tanks[2].State, Is.EqualTo(TankState.AcceptingFish));
            Assert.That(session.State, Is.EqualTo(GameState.PlayerInput));
            Assert.That(session.UnlockScope, Is.EqualTo(TankUnlockScope.LevelAttempt));
        }

        [Test]
        public void InsufficientGold_RejectsUnlock()
        {
            var config = Config();
            var progress = PlayerProgress.Create(0, 599, 5);
            var wallet = new WalletService();
            var session = UnlockSession(config);
            Assert.That(session.TryBeginUnlockModal(2), Is.True);
            var nextIndex = session.Targets.NextUnassignedIndex;

            var result = session.TryUnlockWithGold(2, progress, wallet);

            Assert.That(result.InsufficientGold, Is.True);
            Assert.That(progress.Gold, Is.EqualTo(599));
            Assert.That(session.Tanks[2].IsUnlocked, Is.False);
            Assert.That(session.Tanks[2].State, Is.EqualTo(TankState.Locked));
            Assert.That(session.Tanks[2].HasTarget, Is.False);
            Assert.That(session.Targets.NextUnassignedIndex, Is.EqualTo(nextIndex));
            Assert.That(session.State, Is.EqualTo(GameState.TankUnlockModal));
        }

        [Test]
        public void RewardedAd_UnlocksWithoutSpendingGold()
        {
            var config = Config();
            var progress = PlayerProgress.CreateDevelopmentDefaults();
            var session = UnlockSession(config);
            Assert.That(session.TryBeginUnlockModal(2), Is.True);
            var ads = new MockRewardedAdService();
            ads.SetNextOutcome(MockRewardedAdOutcome.RewardEarned);
            TankUnlockResult result = null;

            ads.Show(
                () => result = session.TryUnlockWithReward(2),
                () => result = TankUnlockResult.Reject(2),
                () => result = TankUnlockResult.Reject(2));

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Succeeded, Is.True);
            Assert.That(progress.Gold, Is.EqualTo(1000));
            Assert.That(session.Tanks[2].IsUnlocked, Is.True);
            Assert.That(session.Tanks[2].HasTarget, Is.True);
            Assert.That(session.State, Is.EqualTo(GameState.PlayerInput));
        }

        [Test]
        public void CanceledAd_DoesNotUnlock()
        {
            var config = Config();
            var progress = PlayerProgress.CreateDevelopmentDefaults();
            var session = UnlockSession(config);
            Assert.That(session.TryBeginUnlockModal(2), Is.True);
            var nextIndex = session.Targets.NextUnassignedIndex;
            var ads = new MockRewardedAdService();
            ads.SetNextOutcome(MockRewardedAdOutcome.ClosedWithoutReward);
            var unlocked = false;

            ads.Show(
                () => unlocked = session.TryUnlockWithReward(2).Succeeded,
                () => { },
                () => unlocked = true);

            Assert.That(unlocked, Is.False);
            Assert.That(progress.Gold, Is.EqualTo(1000));
            Assert.That(session.Tanks[2].IsUnlocked, Is.False);
            Assert.That(session.Targets.NextUnassignedIndex, Is.EqualTo(nextIndex));
            Assert.That(session.State, Is.EqualTo(GameState.TankUnlockModal));

            var failedSession = UnlockSession(config);
            var failedAds = new MockRewardedAdService();
            failedAds.SetNextOutcome(MockRewardedAdOutcome.Failed);
            var failedUnlocked = false;
            failedAds.Show(
                () => failedUnlocked = failedSession.TryUnlockWithReward(2).Succeeded,
                () => failedUnlocked = true,
                () => { });

            Assert.That(failedUnlocked, Is.False);
            Assert.That(failedSession.Tanks[2].IsUnlocked, Is.False);
            Assert.That(progress.Gold, Is.EqualTo(1000));
        }

        [Test]
        public void UnlockedTank_ReceivesNextTarget()
        {
            var config = Config();
            var progress = PlayerProgress.CreateDevelopmentDefaults();
            var session = UnlockSession(config);
            var nextIndex = session.Targets.NextUnassignedIndex;

            var result = session.TryUnlockWithGold(2, progress, new WalletService());

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.AssignedTarget, Is.True);
            Assert.That(result.Target, Is.EqualTo(FishType.RedClown));
            Assert.That(session.Tanks[2].CurrentTarget, Is.EqualTo(FishType.RedClown));
            Assert.That(session.Tanks[2].HasTarget, Is.True);
            Assert.That(session.Tanks[2].FillCount, Is.EqualTo(0));
            Assert.That(session.Targets.NextUnassignedIndex, Is.EqualTo(nextIndex + 1));
            Assert.That(session.Tanks[0].CurrentTarget, Is.EqualTo(FishType.Orange));
            Assert.That(session.Tanks[1].CurrentTarget, Is.EqualTo(FishType.GreenStriped));
        }

        [Test]
        public void Unlock_AutoPromotesMatchingTrayFish()
        {
            var config = Config();
            var progress = PlayerProgress.CreateDevelopmentDefaults();
            var session = UnlockSession(config);
            var red = session.FindFirstIdleFish(FishType.RedClown);
            Select(session, FishType.RedClown);
            Assert.That(session.Tray.Count, Is.EqualTo(1));

            var result = session.TryUnlockWithGold(2, progress, new WalletService());

            Assert.That(result.Succeeded, Is.True);
            Assert.That(session.LastTurn.Promotions.Count, Is.EqualTo(1));
            Assert.That(session.LastTurn.Promotions[0].FishId, Is.EqualTo(red.Id));
            Assert.That(session.LastTurn.Promotions[0].TankSlotIndex, Is.EqualTo(2));
            Assert.That(session.Tanks[2].CurrentTarget, Is.EqualTo(FishType.RedClown));
            Assert.That(session.Tanks[2].FillCount, Is.EqualTo(1));
            Assert.That(session.Tanks[2].ContainedFish[0].Id, Is.EqualTo(red.Id));
            Assert.That(session.Tray.Count, Is.EqualTo(0));
            Assert.That(session.State, Is.EqualTo(GameState.PlayerInput));
            Assert.That(CountState(session, GameState.AutoPromotingTray), Is.GreaterThanOrEqualTo(1));
            Assert.That(progress.Gold, Is.EqualTo(400));
        }

        [Test]
        public void Retry_ResetsLevelAttempt()
        {
            var config = Config();
            var level = Level();
            var progress = PlayerProgress.CreateDevelopmentDefaults();
            var progression = new ProgressionRuntime(progress);
            progression.BeginAttempt();
            var first = LevelSession.Start(level, config, true);
            first.SetOutcomeHandler(state => progression.Settle(state, config));
            Assert.That(first.TryUnlockWithGold(2, progress, progression.Wallet).Succeeded, Is.True);
            SelectCount(first, FishType.PinkStriped, config.WaitingTrayFailCount);

            Assert.That(first.State, Is.EqualTo(GameState.Lose));
            Assert.That(first.Tanks[2].IsUnlocked, Is.True);
            Assert.That(first.Tray.Count, Is.EqualTo(5));
            Assert.That(progress.Gold, Is.EqualTo(400));
            Assert.That(progress.Lives, Is.EqualTo(4));

            progression.BeginAttempt();
            var second = LevelSession.Start(level, config, true);

            Assert.That(second.State, Is.EqualTo(GameState.PlayerInput));
            Assert.That(second.Progress.CollectedFishCount, Is.EqualTo(0));
            Assert.That(second.Progress.TotalFishRequired, Is.EqualTo(36));
            Assert.That(second.Tray.Count, Is.EqualTo(0));
            Assert.That(second.Tanks[0].IsUnlocked, Is.True);
            Assert.That(second.Tanks[1].IsUnlocked, Is.True);
            Assert.That(second.Tanks[2].IsUnlocked, Is.False);
            Assert.That(second.Tanks[2].State, Is.EqualTo(TankState.Locked));
            Assert.That(second.Tanks[3].IsUnlocked, Is.False);
            Assert.That(second.Targets.NextUnassignedIndex, Is.EqualTo(2));
            Assert.That(second.PendingBubbleCount, Is.EqualTo(2));
            Assert.That(second, Is.Not.SameAs(first));
        }

        [Test]
        public void Retry_PreservesProgressionValues()
        {
            var config = Config();
            var level = Level();
            var progress = PlayerProgress.CreateDevelopmentDefaults();
            var progression = new ProgressionRuntime(progress);
            progression.BeginAttempt();
            var first = LevelSession.Start(level, config, true);
            first.SetOutcomeHandler(state => progression.Settle(state, config));
            Assert.That(first.TryUnlockWithGold(2, progress, progression.Wallet).Succeeded, Is.True);
            SelectCount(first, FishType.PinkStriped, config.WaitingTrayFailCount);
            var score = progress.Score;
            var gold = progress.Gold;
            var lives = progress.Lives;

            progression.BeginAttempt();
            var second = LevelSession.Start(level, config, true);
            second.SetOutcomeHandler(state => progression.Settle(state, config));

            Assert.That(progress.Score, Is.EqualTo(score));
            Assert.That(progress.Score, Is.EqualTo(0));
            Assert.That(progress.Gold, Is.EqualTo(gold));
            Assert.That(progress.Gold, Is.EqualTo(400));
            Assert.That(progress.Lives, Is.EqualTo(lives));
            Assert.That(progress.Lives, Is.EqualTo(4));
            Assert.That(second.State, Is.EqualTo(GameState.PlayerInput));
            Assert.That(second.Tanks[2].IsUnlocked, Is.False);
        }

        private static LevelSession WinSession(GameConfig config)
        {
            return LevelSession.Start(
                config,
                new[] { FishType.Orange },
                1,
                config.TankCapacity,
                new List<IReadOnlyList<FishType>>
                {
                    new[] { FishType.Orange, FishType.Orange, FishType.Orange }
                },
                true);
        }

        private static LevelSession LoseSession(GameConfig config)
        {
            return LevelSession.Start(
                config,
                new[] { FishType.Orange },
                1,
                30,
                new List<IReadOnlyList<FishType>>
                {
                    new[]
                    {
                        FishType.PinkStriped,
                        FishType.PinkStriped,
                        FishType.PinkStriped,
                        FishType.PinkStriped,
                        FishType.PinkStriped
                    }
                },
                true);
        }

        private static LevelSession UnlockSession(GameConfig config)
        {
            return LevelSession.Start(
                config,
                new[] { FishType.Orange, FishType.GreenStriped, FishType.RedClown },
                2,
                36,
                new List<IReadOnlyList<FishType>>
                {
                    new[] { FishType.RedClown, FishType.PinkStriped, FishType.Yellow },
                    new[] { FishType.Orange, FishType.GreenStriped, FishType.Yellow }
                },
                true);
        }

        private static int CountState(LevelSession session, GameState state)
        {
            var count = 0;
            for (var i = 0; i < session.StateTrace.Count; i++)
            {
                if (session.StateTrace[i] == state)
                {
                    count++;
                }
            }

            return count;
        }

        private static void Select(LevelSession session, FishType type)
        {
            var fish = session.FindFirstIdleFish(type);
            Assert.That(fish, Is.Not.Null, type.ToString());
            var result = session.TrySelectFish(fish.Id, null);
            Assert.That(result.Accepted, Is.True, type.ToString());
        }

        private static void SelectCount(LevelSession session, FishType type, int count)
        {
            for (var i = 0; i < count; i++)
            {
                Select(session, type);
            }
        }

        private static LevelData Level()
        {
            var level = AssetDatabase.LoadAssetAtPath<LevelData>(LevelPath);
            Assert.That(level, Is.Not.Null, LevelPath);
            return level;
        }

        private static GameConfig Config()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            Assert.That(config, Is.Not.Null, ConfigPath);
            return config;
        }
    }
}
