using System.Collections.Generic;
using FishPuzzle.Core;
using FishPuzzle.Domain;
using FishPuzzle.Tanks;
using NUnit.Framework;
using UnityEditor;

namespace FishPuzzle.Tests.EditMode
{
    public sealed class M4FishRoutingTests
    {
        private const string LevelPath = "Assets/Game/Tests/Fixtures/LegacyLevel_036.asset";
        private const string ConfigPath = "Assets/Game/Data/Config/GameConfig.asset";

        private int _seedId = 8000;

        [Test]
        public void Level001_InitialTargets_AreOrangeAndGreenStriped()
        {
            var session = LevelSession.Start(Level(), Config(), true);

            Assert.That(session.State, Is.EqualTo(GameState.PlayerInput));
            Assert.That(session.Tanks.Count, Is.EqualTo(4));
            Assert.That(session.Tanks[0].State, Is.EqualTo(TankState.AcceptingFish));
            Assert.That(session.Tanks[0].CurrentTarget, Is.EqualTo(FishType.Orange));
            Assert.That(session.Tanks[0].FillCount, Is.EqualTo(0));
            Assert.That(session.Tanks[1].State, Is.EqualTo(TankState.AcceptingFish));
            Assert.That(session.Tanks[1].CurrentTarget, Is.EqualTo(FishType.GreenStriped));
            Assert.That(session.Tanks[1].FillCount, Is.EqualTo(0));
            Assert.That(session.Tanks[2].State, Is.EqualTo(TankState.Locked));
            Assert.That(session.Tanks[2].HasTarget, Is.False);
            Assert.That(session.Tanks[3].State, Is.EqualTo(TankState.Locked));
            Assert.That(session.Tanks[3].HasTarget, Is.False);
            Assert.That(session.Targets.NextUnassignedIndex, Is.EqualTo(2));
            Assert.That(session.Progress.CollectedFishCount, Is.EqualTo(0));
            Assert.That(session.Progress.TotalFishRequired, Is.EqualTo(Level().TotalFishRequired));
            Assert.That(session.Tray.Count, Is.EqualTo(0));
            Assert.That(session.Tray.SlotCount, Is.EqualTo(Config().WaitingTraySlotCount));
            Assert.That(session.Tray.FailCount, Is.EqualTo(Config().WaitingTrayFailCount));
        }

        [Test]
        public void MatchingFish_RoutesToTheOnlyAcceptingTank()
        {
            var session = LevelSession.Start(Level(), Config(), true);
            var fish = session.FindFirstIdleFish(FishType.Orange);
            var bubble = BubbleOf(session, fish);
            var before = bubble.RemainingFishCount;

            var result = session.TrySelectFish(fish.Id, null);

            Assert.That(result.Outcome, Is.EqualTo(FishSelectionOutcome.RoutedToTank));
            Assert.That(result.TankSlotIndex, Is.EqualTo(0));
            Assert.That(result.FillCountAfterAccept, Is.EqualTo(1));
            Assert.That(session.Tanks[0].FillCount, Is.EqualTo(1));
            Assert.That(session.Tanks[0].ContainedFish[0].Id, Is.EqualTo(fish.Id));
            Assert.That(session.Tanks[0].ContainedFish[0].Type, Is.EqualTo(FishType.Orange));
            Assert.That(session.Tanks[1].FillCount, Is.EqualTo(0));
            Assert.That(session.Tray.Count, Is.EqualTo(0));
            Assert.That(session.Progress.CollectedFishCount, Is.EqualTo(0));
            Assert.That(bubble.RemainingFishCount, Is.EqualTo(before - 1));
            Assert.That(session.CountPlacements(fish.Id), Is.EqualTo(1));
            Assert.That(session.State, Is.EqualTo(GameState.PlayerInput));
        }

        [Test]
        public void NonTargetFish_RoutesToWaitingTray()
        {
            var session = LevelSession.Start(Level(), Config(), true);
            var fish = session.FindFirstIdleFish(FishType.PinkStriped);

            var result = session.TrySelectFish(fish.Id, null);

            Assert.That(result.Outcome, Is.EqualTo(FishSelectionOutcome.RoutedToTray));
            Assert.That(result.TraySlotIndex, Is.EqualTo(0));
            Assert.That(session.Tray.Count, Is.EqualTo(1));
            Assert.That(session.Tray.GetFishAt(0).Id, Is.EqualTo(fish.Id));
            Assert.That(session.Tray.GetFishAt(0).Type, Is.EqualTo(FishType.PinkStriped));
            Assert.That(session.Tray.GetFishAt(0).State, Is.EqualTo(FishState.WaitingTray));
            Assert.That(session.Tanks[0].FillCount, Is.EqualTo(0));
            Assert.That(session.Tanks[1].FillCount, Is.EqualTo(0));
            Assert.That(session.Progress.CollectedFishCount, Is.EqualTo(0));
            Assert.That(session.State, Is.EqualTo(GameState.PlayerInput));
        }

        [Test]
        public void MultipleMatchingTanks_PreferHigherFillCount()
        {
            var config = Config();
            var session = TwoOrangeTanks(config);
            Seed(session.Tanks[0], FishType.Orange, 1);
            Seed(session.Tanks[1], FishType.Orange, 2);
            var routing = new FishRoutingService();
            var forward = routing.SelectTank(FishType.Orange, session.Tanks);
            var reversed = routing.SelectTank(
                FishType.Orange,
                new List<TankRuntimeState> { session.Tanks[1], session.Tanks[0] });

            var fish = session.FindFirstIdleFish(FishType.Orange);
            var result = session.TrySelectFish(fish.Id, null);

            Assert.That(forward.SlotIndex, Is.EqualTo(1));
            Assert.That(reversed.SlotIndex, Is.EqualTo(1));
            Assert.That(result.TankSlotIndex, Is.EqualTo(1));
            Assert.That(result.FillCountAfterAccept, Is.EqualTo(config.TankCapacity));
            Assert.That(result.TankCompleted, Is.True);
            Assert.That(session.Tanks[0].FillCount, Is.EqualTo(1));
            Assert.That(session.Tanks[1].FillCount, Is.EqualTo(0));
            Assert.That(session.CountPlacements(fish.Id), Is.EqualTo(0));
            Assert.That(fish.State, Is.EqualTo(FishState.Consumed));
        }

        [Test]
        public void TiedFillCount_UsesLowerTankSlotIndex()
        {
            var session = TwoOrangeTanks(Config());
            Seed(session.Tanks[0], FishType.Orange, 1);
            Seed(session.Tanks[1], FishType.Orange, 1);
            var reversed = new FishRoutingService().SelectTank(
                FishType.Orange,
                new List<TankRuntimeState> { session.Tanks[1], session.Tanks[0] });

            var fish = session.FindFirstIdleFish(FishType.Orange);
            var result = session.TrySelectFish(fish.Id, null);

            Assert.That(reversed.SlotIndex, Is.EqualTo(0));
            Assert.That(result.TankSlotIndex, Is.EqualTo(0));
            Assert.That(result.TankCompleted, Is.False);
            Assert.That(session.Tanks[0].FillCount, Is.EqualTo(2));
            Assert.That(session.Tanks[1].FillCount, Is.EqualTo(1));
            Assert.That(session.Progress.CollectedFishCount, Is.EqualTo(0));
            Assert.That(session.CountPlacements(fish.Id), Is.EqualTo(1));
        }

        [Test]
        public void TankFill_UpdatesUntilCapacity()
        {
            var config = Config();
            var session = SingleOrangeTank(config, config.TankCapacity);
            var first = session.FindFirstIdleFish(FishType.Orange);
            var firstResult = session.TrySelectFish(first.Id, null);
            var second = session.FindFirstIdleFish(FishType.Orange);
            var secondResult = session.TrySelectFish(second.Id, null);

            Assert.That(firstResult.FillCountAfterAccept, Is.EqualTo(1));
            Assert.That(secondResult.FillCountAfterAccept, Is.EqualTo(2));
            Assert.That(secondResult.TankCompleted, Is.False);
            Assert.That(session.Tanks[0].FillCount, Is.EqualTo(2));
            Assert.That(session.Tanks[0].FillCount, Is.LessThanOrEqualTo(config.TankCapacity));
            Assert.That(session.Progress.CollectedFishCount, Is.EqualTo(0));

            var tank = TankRuntimeState.CreateUnlocked(0, config.TankCapacity);
            tank.AssignTarget(FishType.Orange);
            for (var i = 0; i < config.TankCapacity; i++)
            {
                Assert.That(SeedOne(tank, FishType.Orange), Is.True);
            }

            Assert.That(SeedOne(tank, FishType.Orange), Is.False);
            Assert.That(tank.FillCount, Is.EqualTo(config.TankCapacity));
        }

        [Test]
        public void ThirdFish_CompletesTank()
        {
            var config = Config();
            var session = SingleOrangeTank(config, config.TankCapacity + 1);
            FishSelectionResult completing = null;
            var stateDuringCompletion = GameState.PlayerInput;
            for (var i = 0; i < config.TankCapacity; i++)
            {
                var fish = session.FindFirstIdleFish(FishType.Orange);
                var isLast = i == config.TankCapacity - 1;
                if (isLast)
                {
                    completing = session.TrySelectFish(fish.Id, _ => { stateDuringCompletion = session.State; });
                }
                else
                {
                    completing = session.TrySelectFish(fish.Id, null);
                }
            }

            Assert.That(stateDuringCompletion, Is.EqualTo(GameState.ResolvingTank));
            Assert.That(completing.TankCompleted, Is.True);
            Assert.That(completing.FillCountAfterAccept, Is.EqualTo(config.TankCapacity));
            Assert.That(session.Tanks[0].FillCount, Is.EqualTo(0));
            Assert.That(session.Tanks[0].ContainedFish.Count, Is.EqualTo(0));
            Assert.That(session.State, Is.EqualTo(GameState.PlayerInput));
            Assert.That(session.Bubbles.Count, Is.EqualTo(1));
            Assert.That(session.Bubbles[0].RemainingFishCount, Is.EqualTo(1));
        }

        [Test]
        public void TankCompletion_AddsExactlyTankCapacityToProgressOnce()
        {
            var config = Config();
            var level = Level();
            var session = LevelSession.Start(level, config, true);
            SelectCount(session, FishType.Orange, config.TankCapacity);

            Assert.That(session.Progress.CollectedFishCount, Is.EqualTo(config.TankCapacity));
            Assert.That(session.Progress.CollectedFishCount, Is.EqualTo(3));
            Assert.That(session.Progress.TotalFishRequired, Is.EqualTo(level.TotalFishRequired));

            var extra = session.FindFirstIdleFish(FishType.Orange);
            var extraResult = session.TrySelectFish(extra.Id, null);
            Assert.That(extraResult.Outcome, Is.EqualTo(FishSelectionOutcome.RoutedToTray));
            Assert.That(session.Progress.CollectedFishCount, Is.EqualTo(config.TankCapacity));

            var small = SingleOrangeTank(config, config.TankCapacity, 12);
            SelectCount(small, FishType.Orange, config.TankCapacity);
            Assert.That(small.Progress.CollectedFishCount, Is.EqualTo(config.TankCapacity));
            Assert.That(small.Progress.TotalFishRequired, Is.EqualTo(12));
            Assert.That(small.State, Is.EqualTo(GameState.PlayerInput));
        }

        [Test]
        public void CompletedTank_ReceivesNextTarget()
        {
            var config = Config();
            var session = LevelSession.Start(Level(), config, true);
            var waiting = session.FindFirstIdleFish(FishType.RedClown);
            session.TrySelectFish(waiting.Id, null);
            SelectCount(session, FishType.Orange, config.TankCapacity);

            Assert.That(session.Tanks[0].State, Is.EqualTo(TankState.AcceptingFish));
            Assert.That(session.Tanks[0].HasTarget, Is.True);
            Assert.That(session.Tanks[0].CurrentTarget, Is.EqualTo(FishType.RedClown));
            Assert.That(session.Tanks[0].FillCount, Is.EqualTo(1));
            Assert.That(session.Tanks[0].ContainedFish[0].Id, Is.EqualTo(waiting.Id));
            Assert.That(session.Tanks[0].ContainedFish[0].State, Is.EqualTo(FishState.Tank));
            Assert.That(session.Targets.NextUnassignedIndex, Is.EqualTo(3));
            Assert.That(session.Tanks[1].CurrentTarget, Is.EqualTo(FishType.GreenStriped));
            Assert.That(session.Tray.Count, Is.EqualTo(0));
            Assert.That(session.Progress.CollectedFishCount, Is.EqualTo(config.TankCapacity));
            Assert.That(session.State, Is.EqualTo(GameState.PlayerInput));
        }

        [Test]
        public void FifthWaitingFish_CommitsLoseImmediately()
        {
            var config = Config();
            var session = LevelSession.Start(Level(), config, true);
            FishSelectionResult last = null;
            for (var i = 0; i < config.WaitingTrayFailCount; i++)
            {
                var fish = session.FindFirstIdleFish(FishType.PinkStriped);
                last = session.TrySelectFish(fish.Id, null);
                if (i < config.WaitingTrayFailCount - 1)
                {
                    Assert.That(last.Outcome, Is.EqualTo(FishSelectionOutcome.RoutedToTray));
                    Assert.That(session.State, Is.EqualTo(GameState.PlayerInput));
                }
            }

            Assert.That(config.WaitingTrayFailCount, Is.EqualTo(5));
            Assert.That(last.Outcome, Is.EqualTo(FishSelectionOutcome.Lost));
            Assert.That(last.TraySlotIndex, Is.EqualTo(config.WaitingTrayFailCount - 1));
            Assert.That(session.State, Is.EqualTo(GameState.Lose));
            Assert.That(session.Tray.Count, Is.EqualTo(config.WaitingTrayFailCount));
            Assert.That(session.Progress.CollectedFishCount, Is.EqualTo(0));
            Assert.That(session.Tanks[0].CurrentTarget, Is.EqualTo(FishType.Orange));
            Assert.That(session.Tanks[0].FillCount, Is.EqualTo(0));
            Assert.That(session.Tanks[1].CurrentTarget, Is.EqualTo(FishType.GreenStriped));
            Assert.That(session.Tanks[2].State, Is.EqualTo(TankState.Locked));
            Assert.That(session.Targets.NextUnassignedIndex, Is.EqualTo(2));
        }

        [Test]
        public void DoubleTap_CannotDuplicateFish()
        {
            var session = LevelSession.Start(Level(), Config(), true);
            var fish = session.FindFirstIdleFish(FishType.Orange);
            var bubble = BubbleOf(session, fish);
            var before = bubble.RemainingFishCount;
            var duringResolution = GameState.PlayerInput;

            var first = session.TrySelectFish(fish.Id, _ =>
            {
                duringResolution = session.State;
                var other = session.FindFirstIdleFish(FishType.PinkStriped);
                var nested = session.TrySelectFish(other.Id, null);
                Assert.That(nested.Outcome, Is.EqualTo(FishSelectionOutcome.Ignored));
                Assert.That(other.State, Is.EqualTo(FishState.Idle));
            });
            var second = session.TrySelectFish(fish.Id, null);

            Assert.That(duringResolution, Is.EqualTo(GameState.RoutingFish));
            Assert.That(first.Outcome, Is.EqualTo(FishSelectionOutcome.RoutedToTank));
            Assert.That(second.Outcome, Is.EqualTo(FishSelectionOutcome.Ignored));
            Assert.That(fish.State, Is.EqualTo(FishState.Tank));
            Assert.That(session.Tanks[0].FillCount, Is.EqualTo(1));
            Assert.That(session.Tanks[0].ContainedFish.Count, Is.EqualTo(1));
            Assert.That(session.CountPlacements(fish.Id), Is.EqualTo(1));
            Assert.That(bubble.RemainingFishCount, Is.EqualTo(before - 1));
            Assert.That(session.Tray.Count, Is.EqualTo(0));
            Assert.That(session.Progress.CollectedFishCount, Is.EqualTo(0));
            Assert.That(session.State, Is.EqualTo(GameState.PlayerInput));
        }

        [Test]
        public void Input_IsBlockedAfterLose()
        {
            var config = Config();
            var session = LevelSession.Start(Level(), config, true);
            for (var i = 0; i < config.WaitingTrayFailCount; i++)
            {
                var pink = session.FindFirstIdleFish(FishType.PinkStriped);
                session.TrySelectFish(pink.Id, null);
            }

            var blocked = session.FindFirstIdleFish(FishType.Orange);
            var bubble = BubbleOf(session, blocked);
            var before = bubble.RemainingFishCount;
            var ignored = session.TrySelectFish(blocked.Id, null);

            Assert.That(session.State, Is.EqualTo(GameState.Lose));
            Assert.That(ignored.Outcome, Is.EqualTo(FishSelectionOutcome.Ignored));
            Assert.That(blocked.State, Is.EqualTo(FishState.Idle));
            Assert.That(bubble.RemainingFishCount, Is.EqualTo(before));
            Assert.That(session.CountPlacements(blocked.Id), Is.EqualTo(1));
            Assert.That(session.Tray.Count, Is.EqualTo(config.WaitingTrayFailCount));
            Assert.That(session.Progress.CollectedFishCount, Is.EqualTo(0));
            Assert.That(session.Tanks[0].FillCount, Is.EqualTo(0));
        }

        private void Seed(TankRuntimeState tank, FishType type, int count)
        {
            for (var i = 0; i < count; i++)
            {
                Assert.That(SeedOne(tank, type), Is.True);
            }
        }

        private bool SeedOne(TankRuntimeState tank, FishType type)
        {
            var fish = new FishRuntimeState(_seedId, type, "seed");
            _seedId++;
            fish.Reserve();
            fish.BeginTransit();
            return tank.TryAccept(fish);
        }

        private static void SelectCount(LevelSession session, FishType type, int count)
        {
            for (var i = 0; i < count; i++)
            {
                var fish = session.FindFirstIdleFish(type);
                Assert.That(fish, Is.Not.Null, type + " " + i);
                var result = session.TrySelectFish(fish.Id, null);
                Assert.That(result.Accepted, Is.True, type + " " + i);
            }
        }

        private static LevelSession TwoOrangeTanks(GameConfig config)
        {
            return LevelSession.Start(
                config,
                new[] { FishType.Orange, FishType.Orange, FishType.GreenStriped },
                2,
                9,
                new List<IReadOnlyList<FishType>>
                {
                    new[] { FishType.Orange, FishType.PinkStriped, FishType.RedClown }
                },
                true);
        }

        private static LevelSession SingleOrangeTank(GameConfig config, int orangeCount)
        {
            return SingleOrangeTank(config, orangeCount, orangeCount);
        }

        private static LevelSession SingleOrangeTank(GameConfig config, int orangeCount, int totalFishRequired)
        {
            var fish = new FishType[orangeCount];
            for (var i = 0; i < orangeCount; i++)
            {
                fish[i] = FishType.Orange;
            }

            return LevelSession.Start(
                config,
                new[] { FishType.Orange, FishType.GreenStriped },
                1,
                totalFishRequired,
                new List<IReadOnlyList<FishType>> { fish },
                true);
        }

        private static BubbleRuntimeState BubbleOf(LevelSession session, FishRuntimeState fish)
        {
            for (var i = 0; i < session.Bubbles.Count; i++)
            {
                if (session.Bubbles[i].BubbleId == fish.SourceBubbleId)
                {
                    return session.Bubbles[i];
                }
            }

            return null;
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
