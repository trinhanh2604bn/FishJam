using System.Collections.Generic;
using FishPuzzle.Bubbles;
using FishPuzzle.Core;
using FishPuzzle.Domain;
using FishPuzzle.Tanks;
using NUnit.Framework;
using UnityEditor;

namespace FishPuzzle.Tests.EditMode
{
    public sealed class M5CoreLoopTests
    {
        private const string LevelPath = "Assets/Game/Data/Levels/Level_001.asset";
        private const string ConfigPath = "Assets/Game/Data/Config/GameConfig.asset";
        private const string LayoutPath = "Assets/Game/Data/BubblePileLayouts/BPL_Standard_10.asset";

        [Test]
        public void NewTarget_AutoPromotesMatchingTrayFish()
        {
            var session = StartCustom(
                new[] { FishType.Orange, FishType.GreenStriped, FishType.RedClown },
                2,
                new[]
                {
                    Fish(FishType.RedClown, FishType.PinkStriped, FishType.Yellow),
                    Fish(FishType.Orange, FishType.Orange, FishType.Orange)
                });
            Select(session, FishType.RedClown);

            SelectCount(session, FishType.Orange, 3);

            Assert.That(session.Tanks[0].CurrentTarget, Is.EqualTo(FishType.RedClown));
            Assert.That(session.Tanks[0].FillCount, Is.EqualTo(1));
            Assert.That(session.Tanks[0].ContainedFish[0].Type, Is.EqualTo(FishType.RedClown));
            Assert.That(session.Tanks[0].ContainedFish[0].State, Is.EqualTo(FishState.Tank));
            Assert.That(session.Tray.Count, Is.EqualTo(0));
            Assert.That(session.Progress.CollectedFishCount, Is.EqualTo(3));
            Assert.That(session.State, Is.EqualTo(GameState.PlayerInput));
            Assert.That(session.LastTurn.Promotions.Count, Is.EqualTo(1));
        }

        [Test]
        public void TrayScan_IsLeftToRight()
        {
            var session = StartCustom(
                new[] { FishType.Orange, FishType.GreenStriped, FishType.RedClown },
                2,
                new[]
                {
                    Fish(FishType.PinkStriped, FishType.RedClown, FishType.Orange),
                    Fish(FishType.RedClown, FishType.Orange, FishType.GreenStriped),
                    Fish(FishType.Orange, FishType.Yellow, FishType.PinkStriped)
                });
            var firstRed = session.FindFirstIdleFish(FishType.RedClown);
            Select(session, FishType.PinkStriped);
            Select(session, FishType.RedClown);
            var secondRed = session.FindFirstIdleFish(FishType.RedClown);
            Select(session, FishType.RedClown);

            SelectCount(session, FishType.Orange, 3);

            Assert.That(session.LastTurn.Promotions.Count, Is.EqualTo(2));
            Assert.That(session.LastTurn.Promotions[0].FishId, Is.EqualTo(firstRed.Id));
            Assert.That(session.LastTurn.Promotions[1].FishId, Is.EqualTo(secondRed.Id));
            Assert.That(session.Tanks[0].FillCount, Is.EqualTo(2));
            Assert.That(session.Tanks[0].ContainedFish[0].Id, Is.EqualTo(firstRed.Id));
            Assert.That(session.Tanks[0].ContainedFish[1].Id, Is.EqualTo(secondRed.Id));
            Assert.That(session.Tray.Count, Is.EqualTo(1));
            Assert.That(session.Tray.GetFishAt(0).Type, Is.EqualTo(FishType.PinkStriped));
        }

        [Test]
        public void MultipleMatchingTanks_AutoPromoteUsesHighestFillCount()
        {
            var config = Config();
            var session = LevelSession.Start(
                config,
                new[] { FishType.GreenStriped, FishType.Orange, FishType.Orange, FishType.PinkStriped },
                3,
                30,
                new List<IReadOnlyList<FishType>>
                {
                    Fish(FishType.GreenStriped, FishType.GreenStriped, FishType.GreenStriped)
                },
                true);
            SeedTank(session.Tanks[1], FishType.Orange, 1);
            SeedTank(session.Tanks[2], FishType.Orange, 2);
            var waiting = TransitFish(FishType.Orange);
            Assert.That(session.Tray.TryInsert(waiting, out _), Is.True);

            SelectCount(session, FishType.GreenStriped, 3);

            Assert.That(session.LastTurn.Promotions.Count, Is.EqualTo(1));
            Assert.That(session.LastTurn.Promotions[0].TankSlotIndex, Is.EqualTo(2));
            Assert.That(session.LastTurn.Promotions[0].CompletedTank, Is.True);
            Assert.That(session.LastTurn.Promotions[0].FishId, Is.EqualTo(waiting.Id));
            Assert.That(session.Tanks[1].FillCount, Is.EqualTo(1));
            Assert.That(session.Tanks[2].FillCount, Is.EqualTo(0));
            Assert.That(session.Tanks[2].State, Is.EqualTo(TankState.CompletedNoMoreTargets));
            Assert.That(session.Tanks[0].CurrentTarget, Is.EqualTo(FishType.PinkStriped));
            Assert.That(session.Tray.Count, Is.EqualTo(0));
            Assert.That(session.Progress.CollectedFishCount, Is.EqualTo(6));
        }

        [Test]
        public void AutoPromotion_CompactsTray()
        {
            var session = StartCustom(
                new[] { FishType.Orange, FishType.GreenStriped, FishType.RedClown },
                2,
                new[]
                {
                    Fish(FishType.PinkStriped, FishType.RedClown, FishType.Orange),
                    Fish(FishType.Yellow, FishType.Orange, FishType.GreenStriped),
                    Fish(FishType.Orange, FishType.PinkStriped, FishType.Yellow)
                });
            Select(session, FishType.PinkStriped);
            Select(session, FishType.RedClown);
            Select(session, FishType.Yellow);
            var yellow = session.Tray.GetFishAt(2);

            SelectCount(session, FishType.Orange, 3);

            Assert.That(session.Tray.Count, Is.EqualTo(2));
            Assert.That(session.Tray.GetFishAt(0).Type, Is.EqualTo(FishType.PinkStriped));
            Assert.That(session.Tray.GetFishAt(1).Id, Is.EqualTo(yellow.Id));
            Assert.That(session.Tray.GetFishAt(1).Type, Is.EqualTo(FishType.Yellow));
            Assert.That(session.Tanks[0].FillCount, Is.EqualTo(1));
            Assert.That(session.Tanks[0].ContainedFish[0].Type, Is.EqualTo(FishType.RedClown));
        }

        [Test]
        public void AutoPromotion_CanCompleteTank()
        {
            var session = StartCustom(
                new[] { FishType.Orange, FishType.GreenStriped, FishType.RedClown, FishType.PinkStriped },
                2,
                new[]
                {
                    Fish(FishType.RedClown, FishType.RedClown, FishType.RedClown),
                    Fish(FishType.Orange, FishType.Orange, FishType.Orange)
                });
            SelectCount(session, FishType.RedClown, 3);

            SelectCount(session, FishType.Orange, 3);

            Assert.That(session.Progress.CollectedFishCount, Is.EqualTo(6));
            Assert.That(session.Tanks[0].CurrentTarget, Is.EqualTo(FishType.PinkStriped));
            Assert.That(session.Tanks[0].FillCount, Is.EqualTo(0));
            Assert.That(session.Tanks[0].ContainedFish.Count, Is.EqualTo(0));
            Assert.That(session.Tray.Count, Is.EqualTo(0));
            Assert.That(session.Targets.NextUnassignedIndex, Is.EqualTo(4));
            Assert.That(session.State, Is.EqualTo(GameState.PlayerInput));
        }

        [Test]
        public void Cascade_AssignsNextTargetAndContinues()
        {
            var session = StartCustom(
                new[] { FishType.Orange, FishType.GreenStriped, FishType.RedClown, FishType.PinkStriped },
                2,
                new[]
                {
                    Fish(FishType.RedClown, FishType.RedClown, FishType.RedClown),
                    Fish(FishType.PinkStriped, FishType.Orange, FishType.Orange),
                    Fish(FishType.Orange, FishType.Yellow, FishType.GreenStriped)
                });
            SelectCount(session, FishType.RedClown, 3);
            var pink = session.FindFirstIdleFish(FishType.PinkStriped);
            Select(session, FishType.PinkStriped);

            SelectCount(session, FishType.Orange, 3);

            Assert.That(session.Progress.CollectedFishCount, Is.EqualTo(6));
            Assert.That(session.Tanks[0].CurrentTarget, Is.EqualTo(FishType.PinkStriped));
            Assert.That(session.Tanks[0].FillCount, Is.EqualTo(1));
            Assert.That(session.Tanks[0].ContainedFish[0].Id, Is.EqualTo(pink.Id));
            Assert.That(session.Tanks[1].CurrentTarget, Is.EqualTo(FishType.GreenStriped));
            Assert.That(session.Tanks[1].FillCount, Is.EqualTo(0));
            Assert.That(session.Tray.Count, Is.EqualTo(0));
            Assert.That(session.Targets.NextUnassignedIndex, Is.EqualTo(4));
            Assert.That(CountState(session, GameState.AssigningTarget), Is.GreaterThanOrEqualTo(2));
            Assert.That(CountState(session, GameState.AutoPromotingTray), Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public void PlayerInput_IsLockedDuringCascade()
        {
            var session = StartCustom(
                new[] { FishType.Orange, FishType.GreenStriped, FishType.RedClown, FishType.PinkStriped },
                2,
                new[]
                {
                    Fish(FishType.RedClown, FishType.RedClown, FishType.RedClown),
                    Fish(FishType.Orange, FishType.Orange, FishType.Orange),
                    Fish(FishType.Yellow, FishType.PinkStriped, FishType.GreenStriped)
                });
            SelectCount(session, FishType.RedClown, 3);
            var blocked = session.FindFirstIdleFish(FishType.Yellow);
            var sawPromotion = false;
            var acceptedDuringPromotion = false;
            session.SetStateObserver(state =>
            {
                if (state != GameState.AutoPromotingTray)
                {
                    return;
                }

                sawPromotion = true;
                var nested = session.TrySelectFish(blocked.Id, null);
                if (nested.Accepted)
                {
                    acceptedDuringPromotion = true;
                }
            });

            SelectCount(session, FishType.Orange, 3);

            Assert.That(sawPromotion, Is.True);
            Assert.That(acceptedDuringPromotion, Is.False);
            Assert.That(blocked.State, Is.EqualTo(FishState.Idle));
            Assert.That(session.StateTrace[session.StateTrace.Count - 1], Is.EqualTo(GameState.PlayerInput));
            for (var i = 0; i < session.StateTrace.Count - 1; i++)
            {
                Assert.That(session.StateTrace[i], Is.Not.EqualTo(GameState.PlayerInput));
            }
        }

        [Test]
        public void NonEmptyBubble_DoesNotPop()
        {
            var session = LevelSession.Start(Level(), Config(), false);
            var fish = session.FindFirstIdleFish(FishType.Orange);
            var bubble = BubbleOf(session, fish);
            var before = bubble.RemainingFishCount;

            session.TrySelectFish(fish.Id, null);

            Assert.That(bubble.RemainingFishCount, Is.EqualTo(before - 1));
            Assert.That(bubble.HasPopped, Is.False);
            Assert.That(bubble.IsInteractable, Is.True);
            Assert.That(session.TryGetOccupiedSlot(bubble.BubbleId, out var slotId), Is.True);
            Assert.That(slotId, Is.EqualTo(1));
            Assert.That(session.GetBubbleAtSlot(slotId), Is.SameAs(bubble));
            Assert.That(session.LastTurn.PoppedBubble, Is.False);
            Assert.That(session.LastTurn.PileMoves.Count, Is.EqualTo(0));
            Assert.That(session.LastTurn.Spawns.Count, Is.EqualTo(0));
            Assert.That(CountState(session, GameState.PoppingBubble), Is.EqualTo(0));
            Assert.That(session.PendingBubbleCount, Is.EqualTo(2));
            Assert.That(session.State, Is.EqualTo(GameState.PlayerInput));
        }

        [Test]
        public void EmptyBubble_FreesItsPileSlot()
        {
            var session = LevelSession.Start(Level(), Config(), false);
            var falling = session.GetBubbleAtSlot(2);
            Select(session, FishType.GreenStriped);
            Select(session, FishType.RedClown);
            Select(session, FishType.PinkStriped);

            var popped = session.Bubbles[0];
            Assert.That(popped.BubbleId, Is.EqualTo("L001_B001"));
            Assert.That(popped.RemainingFishCount, Is.EqualTo(0));
            Assert.That(popped.HasPopped, Is.True);
            Assert.That(popped.IsInteractable, Is.False);
            Assert.That(session.TryGetOccupiedSlot(popped.BubbleId, out _), Is.False);
            Assert.That(session.GetBubbleAtSlot(0), Is.SameAs(falling));
            Assert.That(falling.BubbleId, Is.EqualTo("L001_B003"));
            Assert.That(session.LastTurn.PoppedBubble, Is.True);
            Assert.That(session.LastTurn.PoppedSlotId, Is.EqualTo(0));
            Assert.That(CountState(session, GameState.PoppingBubble), Is.EqualTo(1));
            Assert.That(CountState(session, GameState.SettlingBubblePile), Is.EqualTo(1));
        }

        [Test]
        public void BubbleAbove_FallsIntoValidLowerGap()
        {
            var pile = OccupiedStandardPile();
            var above = pile.GetAtSlot(2);
            pile.Vacate("B00");

            var moves = BubblePileResolver.ResolveUntilStable(pile);

            Assert.That(moves.Count, Is.GreaterThan(0));
            Assert.That(moves[0].FromSlotId, Is.EqualTo(2));
            Assert.That(moves[0].ToSlotId, Is.EqualTo(0));
            Assert.That(moves[0].BubbleId, Is.EqualTo("B02"));
            Assert.That(pile.GetAtSlot(0), Is.SameAs(above));
        }

        [Test]
        public void MultiStepPile_SettlesDeterministically()
        {
            var pile = OccupiedStandardPile();
            pile.Vacate("B00");

            var moves = BubblePileResolver.ResolveUntilStable(pile);

            Assert.That(moves.Count, Is.EqualTo(3));
            Assert.That(moves[0].BubbleId, Is.EqualTo("B02"));
            Assert.That(moves[0].FromSlotId, Is.EqualTo(2));
            Assert.That(moves[0].ToSlotId, Is.EqualTo(0));
            Assert.That(moves[1].BubbleId, Is.EqualTo("B05"));
            Assert.That(moves[1].FromSlotId, Is.EqualTo(5));
            Assert.That(moves[1].ToSlotId, Is.EqualTo(2));
            Assert.That(moves[2].BubbleId, Is.EqualTo("B07"));
            Assert.That(moves[2].FromSlotId, Is.EqualTo(7));
            Assert.That(moves[2].ToSlotId, Is.EqualTo(5));
            Assert.That(pile.GetAtSlot(0).BubbleId, Is.EqualTo("B02"));
            Assert.That(pile.GetAtSlot(2).BubbleId, Is.EqualTo("B05"));
            Assert.That(pile.GetAtSlot(5).BubbleId, Is.EqualTo("B07"));
            Assert.That(pile.IsOccupied(7), Is.False);
        }

        [Test]
        public void Resolver_ProducesStablePile()
        {
            var pile = OccupiedStandardPile();
            pile.Vacate("B00");
            BubblePileResolver.ResolveUntilStable(pile);

            var second = BubblePileResolver.ResolveUntilStable(pile);

            Assert.That(second.Count, Is.EqualTo(0));
            Assert.That(pile.GetAtSlot(0).BubbleId, Is.EqualTo("B02"));
            Assert.That(pile.IsOccupied(7), Is.False);
        }

        [Test]
        public void PendingBubble_EntersTopSpawnSlot()
        {
            var pile = OccupiedStandardPile();
            pile.Vacate("B00");
            BubblePileResolver.ResolveUntilStable(pile);

            var spawned = pile.TrySpawnNextTop(out var spawn);

            Assert.That(spawned, Is.True);
            Assert.That(spawn.BubbleId, Is.EqualTo("B10"));
            Assert.That(spawn.SlotId, Is.EqualTo(7));
            Assert.That(LayoutSlot(spawn.SlotId).CanReceiveSpawnFromTop, Is.True);
            Assert.That(pile.GetAtSlot(7).BubbleId, Is.EqualTo("B10"));
            Assert.That(pile.GetAtSlot(7), Is.SameAs(BubbleNamed(pile, "B10")));
            Assert.That(pile.NextIndex, Is.EqualTo(11));
        }

        [Test]
        public void Replenish_PreservesQueueOrder()
        {
            var pile = OccupiedStandardPile();
            pile.Vacate("B00");
            BubblePileResolver.ResolveUntilStable(pile);
            Assert.That(pile.TrySpawnNextTop(out var first), Is.True);
            pile.Vacate("B08");

            Assert.That(pile.TrySpawnNextTop(out var second), Is.True);

            Assert.That(first.BubbleId, Is.EqualTo("B10"));
            Assert.That(second.BubbleId, Is.EqualTo("B11"));
            Assert.That(pile.NextIndex, Is.EqualTo(12));
            Assert.That(pile.PendingCount, Is.EqualTo(0));
            Assert.That(pile.GetAtSlot(second.SlotId).BubbleId, Is.EqualTo("B11"));
        }

        [Test]
        public void NewBubble_DoesNotSpawnIntoPoppedLowerSlot()
        {
            var session = LevelSession.Start(Level(), Config(), false);
            Select(session, FishType.GreenStriped);
            Select(session, FishType.RedClown);
            Select(session, FishType.PinkStriped);

            Assert.That(session.LastTurn.Spawns.Count, Is.EqualTo(1));
            Assert.That(session.LastTurn.Spawns[0].BubbleId, Is.EqualTo("L001_B011"));
            Assert.That(session.LastTurn.Spawns[0].SlotId, Is.Not.EqualTo(session.LastTurn.PoppedSlotId));
            Assert.That(session.LastTurn.Spawns[0].SlotId, Is.EqualTo(7));
            Assert.That(session.GetBubbleAtSlot(0).BubbleId, Is.EqualTo("L001_B003"));
            Assert.That(session.GetBubbleAtSlot(7).BubbleId, Is.EqualTo("L001_B011"));
            Assert.That(session.NextBubbleQueueIndex, Is.EqualTo(11));
            Assert.That(session.PendingBubbleCount, Is.EqualTo(1));
            Assert.That(CountState(session, GameState.SpawningTopBubble), Is.EqualTo(1));
        }

        [Test]
        public void Level001_CanReachThirtySixOfThirtySix()
        {
            var session = LevelSession.Start(Level(), Config(), false);

            PlayMatchingFish(session);

            Assert.That(session.State, Is.EqualTo(GameState.Win), Describe(session));
            Assert.That(session.Progress.CollectedFishCount, Is.EqualTo(36));
            Assert.That(session.Progress.TotalFishRequired, Is.EqualTo(36));
            Assert.That(session.Tray.Count, Is.EqualTo(0));
            Assert.That(session.PendingBubbleCount, Is.EqualTo(0));
            Assert.That(session.NextBubbleQueueIndex, Is.EqualTo(12));
            Assert.That(session.Targets.NextUnassignedIndex, Is.EqualTo(session.Targets.Count));
            for (var i = 0; i < session.Bubbles.Count; i++)
            {
                Assert.That(session.Bubbles[i].RemainingFishCount, Is.EqualTo(0), session.Bubbles[i].BubbleId);
            }

            for (var i = 0; i < session.Tanks.Count; i++)
            {
                Assert.That(session.Tanks[i].FillCount, Is.EqualTo(0));
                Assert.That(session.Tanks[i].HasTarget, Is.False);
            }
        }

        [Test]
        public void Win_LocksInput()
        {
            var config = Config();
            var session = LevelSession.Start(
                config,
                new[] { FishType.Orange },
                1,
                config.TankCapacity,
                new List<IReadOnlyList<FishType>>
                {
                    Fish(FishType.Orange, FishType.Orange, FishType.Orange)
                },
                true);

            SelectCount(session, FishType.Orange, config.TankCapacity);
            var ignored = session.TrySelectFish(1, null);

            Assert.That(session.State, Is.EqualTo(GameState.Win));
            Assert.That(session.Progress.CollectedFishCount, Is.EqualTo(config.TankCapacity));
            Assert.That(session.Tray.Count, Is.EqualTo(0));
            Assert.That(session.Tanks[0].FillCount, Is.EqualTo(0));
            Assert.That(session.Tanks[0].HasTarget, Is.False);
            Assert.That(ignored.Outcome, Is.EqualTo(FishSelectionOutcome.Ignored));
            Assert.That(session.State, Is.EqualTo(GameState.Win));
            Assert.That(session.LastTurn.Won, Is.True);
        }

        [Test]
        public void Lose_OverridesFurtherResolution()
        {
            var config = Config();
            var session = LevelSession.Start(
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
                    },
                    Fish(FishType.Orange, FishType.GreenStriped, FishType.RedClown)
                },
                true);
            session.AttachPile(Layout());
            for (var i = 0; i < config.WaitingTrayFailCount - 1; i++)
            {
                Select(session, FishType.PinkStriped);
                Assert.That(session.State, Is.EqualTo(GameState.PlayerInput));
            }

            var source = session.Bubbles[0];
            Select(session, FishType.PinkStriped);
            var orange = session.FindFirstIdleFish(FishType.Orange);
            var ignored = session.TrySelectFish(orange.Id, null);

            Assert.That(session.State, Is.EqualTo(GameState.Lose));
            Assert.That(session.Tray.Count, Is.EqualTo(config.WaitingTrayFailCount));
            Assert.That(source.RemainingFishCount, Is.EqualTo(0));
            Assert.That(source.HasPopped, Is.False);
            Assert.That(session.TryGetOccupiedSlot(source.BubbleId, out var slotId), Is.True);
            Assert.That(slotId, Is.EqualTo(0));
            Assert.That(session.LastTurn.PoppedBubble, Is.False);
            Assert.That(session.LastTurn.Promotions.Count, Is.EqualTo(0));
            Assert.That(session.LastTurn.Won, Is.False);
            Assert.That(CountState(session, GameState.PoppingBubble), Is.EqualTo(0));
            Assert.That(CountState(session, GameState.AutoPromotingTray), Is.EqualTo(0));
            Assert.That(CountState(session, GameState.Win), Is.EqualTo(0));
            Assert.That(ignored.Outcome, Is.EqualTo(FishSelectionOutcome.Ignored));
            Assert.That(orange.State, Is.EqualTo(FishState.Idle));
            Assert.That(session.Progress.CollectedFishCount, Is.EqualTo(0));
            Assert.That(session.Tanks[0].CurrentTarget, Is.EqualTo(FishType.Orange));
            Assert.That(session.Targets.NextUnassignedIndex, Is.EqualTo(1));
        }

        private static void PlayMatchingFish(LevelSession session)
        {
            var routing = new FishRoutingService();
            var guard = 0;
            while (session.State == GameState.PlayerInput && guard < 80)
            {
                guard++;
                var fish = FirstMatchingFish(session, routing);
                Assert.That(fish, Is.Not.Null, Describe(session));
                var result = session.TrySelectFish(fish.Id, null);
                Assert.That(result.Accepted, Is.True, Describe(session));
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

        private static string Describe(LevelSession session)
        {
            return "state " + session.State
                + " progress " + session.Progress.CollectedFishCount
                + " tray " + session.Tray.Count
                + " pending " + session.PendingBubbleCount;
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

        private static BubbleRuntimeState BubbleNamed(BubblePileOccupancy pile, string bubbleId)
        {
            for (var slot = 0; slot < 10; slot++)
            {
                var bubble = pile.GetAtSlot(slot);
                if (bubble != null && bubble.BubbleId == bubbleId)
                {
                    return bubble;
                }
            }

            return null;
        }

        private static BubblePileOccupancy OccupiedStandardPile()
        {
            var bubbles = new List<BubbleRuntimeState>(12);
            for (var i = 0; i < 12; i++)
            {
                bubbles.Add(new BubbleRuntimeState("B" + i.ToString("00"), new List<FishRuntimeState>(), false));
            }

            return BubblePileOccupancy.Create(Layout(), bubbles);
        }

        private static BubblePileSlotDefinition LayoutSlot(int slotId)
        {
            var slots = Layout().Slots;
            for (var i = 0; i < slots.Count; i++)
            {
                if (slots[i] != null && slots[i].SlotId == slotId)
                {
                    return slots[i];
                }
            }

            Assert.Fail("Missing slot " + slotId);
            return null;
        }

        private int _seedId = 9000;

        private void SeedTank(TankRuntimeState tank, FishType type, int count)
        {
            for (var i = 0; i < count; i++)
            {
                Assert.That(tank.TryAccept(TransitFish(type)), Is.True);
            }
        }

        private FishRuntimeState TransitFish(FishType type)
        {
            var fish = new FishRuntimeState(_seedId, type, "seed");
            _seedId++;
            fish.Reserve();
            fish.BeginTransit();
            return fish;
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

        private static LevelSession StartCustom(FishType[] targets, int unlockedTanks, FishType[][] bubbles)
        {
            var lists = new List<IReadOnlyList<FishType>>(bubbles.Length);
            for (var i = 0; i < bubbles.Length; i++)
            {
                lists.Add(bubbles[i]);
            }

            return LevelSession.Start(Config(), targets, unlockedTanks, 36, lists, true);
        }

        private static FishType[] Fish(params FishType[] fish)
        {
            return fish;
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

        private static BubblePileLayout Layout()
        {
            var layout = AssetDatabase.LoadAssetAtPath<BubblePileLayout>(LayoutPath);
            Assert.That(layout, Is.Not.Null, LayoutPath);
            return layout;
        }
    }
}
