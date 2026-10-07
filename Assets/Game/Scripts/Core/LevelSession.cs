using System;
using System.Collections.Generic;
using FishPuzzle.Bubbles;
using FishPuzzle.Domain;
using FishPuzzle.Progression;
using FishPuzzle.Tanks;
using FishPuzzle.Tray;

namespace FishPuzzle.Core
{
    /// <summary>
    /// One level attempt. Owns tank, tray, target, progress, and bubble-pile state.
    /// Input is accepted only while <see cref="State"/> is <see cref="GameState.PlayerInput"/>.
    /// </summary>
    public sealed class LevelSession
    {
        private readonly FishRoutingService _routing = new FishRoutingService();
        private readonly List<TankRuntimeState> _tanks;
        private readonly List<BubbleRuntimeState> _bubbles;
        private readonly Dictionary<int, FishRuntimeState> _fishById;
        private readonly Dictionary<string, BubbleRuntimeState> _bubblesById;
        private readonly List<GameState> _stateTrace = new List<GameState>();
        private readonly List<TrayPromotionRecord> _promotions = new List<TrayPromotionRecord>();
        private readonly List<BubblePileMove> _pileMoves = new List<BubblePileMove>();
        private readonly List<BubbleSpawn> _spawns = new List<BubbleSpawn>();
        private readonly List<IceProgressRecord> _iceUpdates = new List<IceProgressRecord>();
        private readonly List<BubbleRuntimeState> _adjacent = new List<BubbleRuntimeState>();
        private BubblePileOccupancy _pile;
        private Action<GameState> _stateObserver;
        private Action<GameState> _outcomeHandler;
        private int _pendingUnlockSlot = -1;
        private bool _presentationHold;
        private bool _poppedBubble;
        private string _poppedBubbleId = string.Empty;
        private int _poppedSlotId = -1;
        private GameState _state;

        private LevelSession(
            GameConfig config,
            TargetGroupQueue targets,
            List<TankRuntimeState> tanks,
            WaitingTrayState tray,
            LevelRuntimeProgress progress,
            List<BubbleRuntimeState> bubbles,
            Dictionary<int, FishRuntimeState> fishById,
            Dictionary<string, BubbleRuntimeState> bubblesById)
        {
            Config = config;
            Targets = targets;
            _tanks = tanks;
            Tray = tray;
            Progress = progress;
            _bubbles = bubbles;
            _fishById = fishById;
            _bubblesById = bubblesById;
            _state = GameState.PlayerInput;
            LastTurn = TurnResolution.Empty;
        }

        public GameConfig Config { get; }

        public GameState State => _state;

        public TargetGroupQueue Targets { get; }

        public WaitingTrayState Tray { get; }

        public LevelRuntimeProgress Progress { get; }

        public IReadOnlyList<TankRuntimeState> Tanks => _tanks;

        public IReadOnlyList<BubbleRuntimeState> Bubbles => _bubbles;

        public IReadOnlyList<GameState> StateTrace => _stateTrace;

        public TurnResolution LastTurn { get; private set; }

        public int PendingUnlockSlot => _pendingUnlockSlot;

        public TankUnlockScope UnlockScope => TankUnlockScope.LevelAttempt;

        public int PendingBubbleCount => _pile != null ? _pile.PendingCount : 0;

        public int NextBubbleQueueIndex => _pile != null ? _pile.NextIndex : 0;

        public bool HasPile => _pile != null;

        public static LevelSession Start(LevelData level, GameConfig config, bool allBubblesInPlay)
        {
            if (level == null)
            {
                throw new ArgumentNullException(nameof(level));
            }

            var bubbles = new List<IReadOnlyList<FishType>>();
            var ids = new List<string>();
            var queue = level.BubbleQueue;
            if (queue != null)
            {
                for (var i = 0; i < queue.Count; i++)
                {
                    var definition = queue[i];
                    ids.Add(definition != null ? definition.BubbleId : string.Empty);
                    bubbles.Add(CopyFish(definition != null ? definition.Fishes : null));
                }
            }

            var session = Start(
                config,
                level.TargetGroupQueue,
                level.InitialUnlockedTankCount,
                level.TotalFishRequired,
                ids,
                bubbles,
                allBubblesInPlay);
            session.ApplyAuthoredIce(queue);
            if (level.PileLayout != null)
            {
                session.AttachPile(level.PileLayout);
            }

            return session;
        }

        public static LevelSession Start(
            GameConfig config,
            IReadOnlyList<FishType> targetGroupQueue,
            int unlockedTankCount,
            int totalFishRequired,
            IReadOnlyList<IReadOnlyList<FishType>> bubbleFish,
            bool allBubblesInPlay)
        {
            var ids = new List<string>();
            var bubbles = bubbleFish ?? Array.Empty<IReadOnlyList<FishType>>();
            for (var i = 0; i < bubbles.Count; i++)
            {
                ids.Add("bubble_" + i);
            }

            return Start(
                config,
                targetGroupQueue,
                unlockedTankCount,
                totalFishRequired,
                ids,
                bubbles,
                allBubblesInPlay);
        }

        public void AttachPile(BubblePileLayout layout)
        {
            _pile = BubblePileOccupancy.Create(layout, _bubbles);
        }

        public void SetStateObserver(Action<GameState> observer)
        {
            _stateObserver = observer;
        }

        public void SetOutcomeHandler(Action<GameState> handler)
        {
            _outcomeHandler = handler;
        }

        public bool TryBeginUnlockModal(int slotIndex)
        {
            if (_state != GameState.PlayerInput)
            {
                return false;
            }

            var tank = FindTank(slotIndex);
            if (tank == null || tank.IsUnlocked || tank.State != TankState.Locked)
            {
                return false;
            }

            _pendingUnlockSlot = slotIndex;
            SetState(GameState.TankUnlockModal);
            return true;
        }

        public void CancelUnlockModal()
        {
            if (_state != GameState.TankUnlockModal)
            {
                return;
            }

            _pendingUnlockSlot = -1;
            SetState(GameState.PlayerInput);
        }

        public TankUnlockResult TryUnlockWithGold(int slotIndex, PlayerProgress progress, WalletService wallet)
        {
            if (!TryPrepareUnlock(slotIndex, out var tank, out var rejected))
            {
                return rejected;
            }

            var cost = Config.TankUnlockGoldCost;
            if (progress == null || wallet == null || cost < 0 || progress.Gold < cost)
            {
                return TankUnlockResult.NotEnoughGold(slotIndex);
            }

            if (!wallet.TrySpend(progress, cost))
            {
                return TankUnlockResult.NotEnoughGold(slotIndex);
            }

            var result = CommitUnlock(tank);
            if (!result.Succeeded)
            {
                tank.RevertUnlock();
                wallet.Refund(progress, cost);
            }

            return result;
        }

        public TankUnlockResult TryUnlockWithReward(int slotIndex)
        {
            if (!Config.ExtraTankAdUnlockAllowed)
            {
                return TankUnlockResult.Reject(slotIndex);
            }

            if (!TryPrepareUnlock(slotIndex, out var tank, out var rejected))
            {
                return rejected;
            }

            return CommitUnlock(tank);
        }

        public void HoldForPresentation(GameState visualState)
        {
            if (_state == GameState.Win || _state == GameState.Lose)
            {
                return;
            }

            if (visualState != GameState.RoutingFish
                && visualState != GameState.ResolvingTank
                && visualState != GameState.AssigningTarget
                && visualState != GameState.AutoPromotingTray
                && visualState != GameState.PoppingBubble
                && visualState != GameState.SettlingBubblePile
                && visualState != GameState.SpawningTopBubble)
            {
                return;
            }

            _presentationHold = true;
            SetState(visualState);
        }

        public void ReleasePresentationHold()
        {
            if (!_presentationHold)
            {
                return;
            }

            _presentationHold = false;
            if (_state != GameState.Win && _state != GameState.Lose)
            {
                SetState(GameState.PlayerInput);
            }
        }

        public void SetBubbleInPlay(string bubbleId, bool inPlay)
        {
            if (string.IsNullOrEmpty(bubbleId) || !_bubblesById.TryGetValue(bubbleId, out var bubble) || bubble == null || bubble.HasPopped)
            {
                return;
            }

            bubble.SetInPlay(inPlay);
        }

        public bool TryGetOccupiedSlot(string bubbleId, out int slotId)
        {
            slotId = -1;
            return _pile != null && _pile.TryGetSlotOfBubble(bubbleId, out slotId);
        }

        public BubbleRuntimeState GetBubbleAtSlot(int slotId)
        {
            return _pile != null ? _pile.GetAtSlot(slotId) : null;
        }

        public BubbleRuntimeState FindBubble(string bubbleId)
        {
            if (string.IsNullOrEmpty(bubbleId) || !_bubblesById.TryGetValue(bubbleId, out var bubble))
            {
                return null;
            }

            return bubble;
        }

        /// <summary>True while the fish sits inside a Frozen Bubble. Such fish reject press/select.</summary>
        public bool IsFishFrozen(int fishId)
        {
            if (!_fishById.TryGetValue(fishId, out var fish) || fish == null || fish.State != FishState.Idle)
            {
                return false;
            }

            var bubble = FindBubble(fish.SourceBubbleId);
            return bubble != null && bubble.IsFrozen && bubble.Contains(fish);
        }

        /// <summary>
        /// Starts Frozen Bubbles from the authored definitions. Called once per attempt, so Retry and Next Level
        /// always restore <see cref="BubbleDefinition.IceBreakRequiredSelections"/>.
        /// </summary>
        public void ApplyAuthoredIce(IReadOnlyList<BubbleDefinition> definitions)
        {
            if (definitions == null)
            {
                return;
            }

            for (var i = 0; i < definitions.Count && i < _bubbles.Count; i++)
            {
                var definition = definitions[i];
                if (definition != null && definition.IsFrozen && _bubbles[i] != null)
                {
                    _bubbles[i].Freeze(definition.IceBreakRequiredSelections);
                }
            }
        }

        public FishRuntimeState FindFirstIdleFish(FishType fishType)
        {
            for (var i = 0; i < _bubbles.Count; i++)
            {
                var bubble = _bubbles[i];
                if (bubble == null || !bubble.IsInPlay || !bubble.IsInteractable)
                {
                    continue;
                }

                var fish = bubble.Fish;
                for (var f = 0; f < fish.Count; f++)
                {
                    var candidate = fish[f];
                    if (candidate != null && candidate.State == FishState.Idle && candidate.Type == fishType)
                    {
                        return candidate;
                    }
                }
            }

            return null;
        }

        public int CountPlacements(int fishId)
        {
            var count = 0;
            for (var i = 0; i < _bubbles.Count; i++)
            {
                count += CountMatching(_bubbles[i] != null ? _bubbles[i].Fish : null, fishId);
            }

            for (var i = 0; i < _tanks.Count; i++)
            {
                count += CountMatching(_tanks[i] != null ? _tanks[i].ContainedFish : null, fishId);
            }

            for (var i = 0; i < Tray.Count; i++)
            {
                var fish = Tray.GetFishAt(i);
                if (fish != null && fish.Id == fishId)
                {
                    count++;
                }
            }

            return count;
        }

        public FishSelectionResult TrySelectFish(int fishId, Action<FishSelectionResult> onCommitted)
        {
            if (_state != GameState.PlayerInput)
            {
                return FishSelectionResult.Ignored(fishId);
            }

            if (!_fishById.TryGetValue(fishId, out var fish) || fish == null || fish.State != FishState.Idle)
            {
                return FishSelectionResult.Ignored(fishId);
            }

            if (!_bubblesById.TryGetValue(fish.SourceBubbleId, out var bubble)
                || bubble == null
                || !bubble.IsInPlay
                || !bubble.IsInteractable
                || bubble.HasPopped
                || !bubble.Contains(fish))
            {
                return FishSelectionResult.Ignored(fishId);
            }

            BeginTurn();
            SetState(GameState.RoutingFish);
            FishSelectionResult result = null;
            try
            {
                fish.Reserve();
                bubble.Remove(fish);
                fish.BeginTransit();
                result = CommitDestination(fish);
                if (result.Outcome == FishSelectionOutcome.RoutedToTank)
                {
                    ChipAdjacentIce(bubble.BubbleId);
                }

                if (result.Accepted)
                {
                    onCommitted?.Invoke(result);
                }

                if (_state == GameState.Lose)
                {
                    return result;
                }

                if (result.TankCompleted)
                {
                    RunAutoPromotion();
                }

                if (_state == GameState.Lose)
                {
                    return result;
                }

                ResolveEmptyBubble(fish.SourceBubbleId);
                TryCommitWin();
                return result;
            }
            finally
            {
                if (_state != GameState.Lose && _state != GameState.Win)
                {
                    SetState(GameState.PlayerInput);
                }

                LastTurn = new TurnResolution(
                    _poppedBubble,
                    _poppedBubbleId,
                    _poppedSlotId,
                    _promotions.ToArray(),
                    _pileMoves.ToArray(),
                    _spawns.ToArray(),
                    _state == GameState.Win,
                    _iceUpdates.ToArray());
            }
        }

        private void BeginTurn()
        {
            _stateTrace.Clear();
            _promotions.Clear();
            _pileMoves.Clear();
            _spawns.Clear();
            _iceUpdates.Clear();
            _poppedBubble = false;
            _poppedBubbleId = string.Empty;
            _poppedSlotId = -1;
        }

        /// <summary>
        /// A correct fish (routed to a tank) selected from <paramref name="sourceBubbleId"/> chips every Frozen Bubble in a
        /// slot adjacent to the source slot right now. Fish sent to the Waiting Tray never chip ice. Runs before the source
        /// bubble pops and the pile settles, so adjacency is the pile the player tapped. Routing itself is unchanged.
        /// </summary>
        private void ChipAdjacentIce(string sourceBubbleId)
        {
            if (_pile == null || !_pile.TryGetSlotOfBubble(sourceBubbleId, out var sourceSlot))
            {
                return;
            }

            _pile.CollectAdjacentOccupants(sourceSlot, _adjacent);
            for (var i = 0; i < _adjacent.Count; i++)
            {
                var frozen = _adjacent[i];
                if (frozen == null || frozen.BubbleId == sourceBubbleId || !frozen.TryChipIce())
                {
                    continue;
                }

                _pile.TryGetSlotOfBubble(frozen.BubbleId, out var frozenSlot);
                _iceUpdates.Add(new IceProgressRecord(
                    frozen.BubbleId,
                    frozenSlot,
                    frozen.IceSelectionsRemaining,
                    frozen.IceBreakRequiredSelections));
            }

            _adjacent.Clear();
        }

        private FishSelectionResult CommitDestination(FishRuntimeState fish)
        {
            var choice = _routing.SelectTank(fish.Type, _tanks);
            if (choice.Found)
            {
                return CommitToTank(fish, choice.SlotIndex);
            }

            return CommitToTray(fish);
        }

        private FishSelectionResult CommitToTank(FishRuntimeState fish, int slotIndex)
        {
            var tank = FindTank(slotIndex);
            if (tank == null || !tank.TryAccept(fish))
            {
                GameLog.Error(
                    nameof(LevelSession),
                    "Tank " + slotIndex + " did not accept reserved fish " + fish.Id + ".");
                return FishSelectionResult.Ignored(fish.Id);
            }

            var landedOrdinal = tank.FillCount - 1;
            var fillAfterAccept = tank.FillCount;
            if (fillAfterAccept < tank.Capacity)
            {
                return FishSelectionResult.ToTank(
                    fish.Id,
                    fish.SourceBubbleId,
                    slotIndex,
                    landedOrdinal,
                    fillAfterAccept,
                    false,
                    false,
                    default,
                    Array.Empty<int>(),
                    Progress.CollectedFishCount);
            }

            if (!TryResolveFullTank(tank, out var hasNext, out var nextTarget, out var consumed))
            {
                return FishSelectionResult.ToTank(
                    fish.Id,
                    fish.SourceBubbleId,
                    slotIndex,
                    landedOrdinal,
                    fillAfterAccept,
                    false,
                    false,
                    default,
                    Array.Empty<int>(),
                    Progress.CollectedFishCount);
            }

            SetState(GameState.ResolvingTank);
            return FishSelectionResult.ToTank(
                fish.Id,
                fish.SourceBubbleId,
                slotIndex,
                landedOrdinal,
                fillAfterAccept,
                true,
                hasNext,
                nextTarget,
                consumed,
                Progress.CollectedFishCount);
        }

        private FishSelectionResult CommitToTray(FishRuntimeState fish)
        {
            if (!Tray.TryInsert(fish, out var slotIndex))
            {
                GameLog.Error(nameof(LevelSession), "Waiting tray rejected reserved fish " + fish.Id + ".");
                SetState(GameState.Lose);
                return FishSelectionResult.ToTray(fish.Id, fish.SourceBubbleId, -1, true, Progress.CollectedFishCount);
            }

            var lost = Tray.ReachedFailCount;
            if (lost)
            {
                SetState(GameState.Lose);
            }

            return FishSelectionResult.ToTray(fish.Id, fish.SourceBubbleId, slotIndex, lost, Progress.CollectedFishCount);
        }

        private void RunAutoPromotion()
        {
            SetState(GameState.AutoPromotingTray);
            var guard = 0;
            var limit = (Tray.SlotCount * _tanks.Count) + Tray.SlotCount + 4;
            while (guard < limit && _state != GameState.Lose && _state != GameState.Win)
            {
                guard++;
                if (!TrayAutoPromotionService.TryFindFirstMove(Tray, _tanks, _routing, out var trayIndex, out var tankSlot))
                {
                    return;
                }

                var fish = Tray.RemoveAt(trayIndex);
                if (fish == null)
                {
                    GameLog.Error(nameof(LevelSession), "Waiting tray slot " + trayIndex + " was empty during auto-promotion.");
                    return;
                }

                fish.BeginTransitFromWaitingTray();
                var tank = FindTank(tankSlot);
                if (tank == null || !tank.TryAccept(fish))
                {
                    GameLog.Error(
                        nameof(LevelSession),
                        "Tank " + tankSlot + " did not accept auto-promoted fish " + fish.Id + ".");
                    return;
                }

                var landedOrdinal = tank.FillCount - 1;
                var completed = false;
                var hasNext = false;
                var nextTarget = default(FishType);
                var consumed = Array.Empty<int>();
                if (tank.FillCount >= tank.Capacity)
                {
                    completed = TryResolveFullTank(tank, out hasNext, out nextTarget, out consumed);
                    SetState(GameState.AutoPromotingTray);
                }

                _promotions.Add(new TrayPromotionRecord(
                    fish.Id,
                    tankSlot,
                    landedOrdinal,
                    completed,
                    consumed,
                    trayIndex,
                    hasNext,
                    nextTarget));
            }

            if (guard >= limit)
            {
                GameLog.Error(nameof(LevelSession), "Waiting-tray auto-promotion did not finish.");
            }
        }

        private bool TryResolveFullTank(
            TankRuntimeState tank,
            out bool hasNext,
            out FishType nextTarget,
            out int[] consumed)
        {
            hasNext = false;
            nextTarget = default;
            consumed = Array.Empty<int>();
            SetState(GameState.ResolvingTank);
            if (tank == null || !tank.TryBeginResolution())
            {
                GameLog.Error(nameof(LevelSession), "A full tank did not resolve.");
                return false;
            }

            Progress.CommitCompletedGroup(tank.Capacity);
            consumed = CopyContainedIds(tank);
            hasNext = Targets.TryTakeNext(out nextTarget);
            SetState(GameState.AssigningTarget);
            tank.CompleteResolution(hasNext, nextTarget);
            return true;
        }

        private void ResolveEmptyBubble(string bubbleId)
        {
            if (_state == GameState.Lose || string.IsNullOrEmpty(bubbleId))
            {
                return;
            }

            if (!_bubblesById.TryGetValue(bubbleId, out var bubble) || bubble == null || bubble.RemainingFishCount > 0)
            {
                return;
            }

            if (_pile == null || !_pile.TryGetSlotOfBubble(bubbleId, out var slotId))
            {
                return;
            }

            SetState(GameState.PoppingBubble);
            bubble.MarkPopped();
            _pile.Vacate(bubbleId);
            _poppedBubble = true;
            _poppedBubbleId = bubbleId;
            _poppedSlotId = slotId;

            SetState(GameState.SettlingBubblePile);
            var moves = BubblePileResolver.ResolveUntilStable(_pile);
            for (var i = 0; i < moves.Count; i++)
            {
                _pileMoves.Add(moves[i]);
            }

            var spawnGuard = 0;
            while (spawnGuard < _pile.SlotCount && _pile.TrySpawnNextTop(out var spawn))
            {
                spawnGuard++;
                if (spawnGuard == 1)
                {
                    SetState(GameState.SpawningTopBubble);
                }

                _spawns.Add(spawn);
            }
        }

        private void TryCommitWin()
        {
            if (_state == GameState.Lose || Progress.CollectedFishCount != Progress.TotalFishRequired)
            {
                return;
            }

            if (!WinInvariantsHold(out var reason))
            {
                GameLog.Error(
                    nameof(LevelSession),
                    "Collected fish reached the level total, but win invariants failed. " + reason);
                return;
            }

            SetState(GameState.Win);
        }

        private bool WinInvariantsHold(out string reason)
        {
            for (var i = 0; i < _bubbles.Count; i++)
            {
                var bubble = _bubbles[i];
                if (bubble != null && bubble.RemainingFishCount > 0)
                {
                    reason = "Bubble " + bubble.BubbleId + " still has " + bubble.RemainingFishCount + " fish.";
                    return false;
                }
            }

            if (Tray.Count != 0)
            {
                reason = "Waiting tray still has " + Tray.Count + " fish.";
                return false;
            }

            for (var i = 0; i < _tanks.Count; i++)
            {
                var tank = _tanks[i];
                if (tank == null)
                {
                    continue;
                }

                if (tank.FillCount != 0)
                {
                    reason = "Tank " + tank.SlotIndex + " still has " + tank.FillCount + " fish.";
                    return false;
                }

                if (tank.HasTarget)
                {
                    reason = "Tank " + tank.SlotIndex + " still has an active target.";
                    return false;
                }
            }

            if (Targets.NextUnassignedIndex != Targets.Count)
            {
                reason = "Target queue still has unassigned groups.";
                return false;
            }

            if (_pile != null && _pile.PendingCount != 0)
            {
                reason = "Bubble queue still has " + _pile.PendingCount + " pending bubbles.";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private bool TryPrepareUnlock(int slotIndex, out TankRuntimeState tank, out TankUnlockResult rejected)
        {
            tank = null;
            rejected = TankUnlockResult.Reject(slotIndex);
            if (_state != GameState.PlayerInput && _state != GameState.TankUnlockModal)
            {
                return false;
            }

            if (_state == GameState.TankUnlockModal && _pendingUnlockSlot != slotIndex)
            {
                return false;
            }

            tank = FindTank(slotIndex);
            if (tank == null || tank.IsUnlocked || tank.State != TankState.Locked)
            {
                return false;
            }

            return true;
        }

        private TankUnlockResult CommitUnlock(TankRuntimeState tank)
        {
            if (tank == null || !tank.TryUnlock())
            {
                return TankUnlockResult.Reject(tank != null ? tank.SlotIndex : -1);
            }

            BeginTurn();
            var assigned = false;
            var target = default(FishType);
            try
            {
                SetState(GameState.AssigningTarget);
                if (Targets.TryTakeNext(out target))
                {
                    tank.AssignTarget(target);
                    assigned = tank.HasTarget;
                }

                RunAutoPromotion();
                TryCommitWin();
                _pendingUnlockSlot = -1;
                return TankUnlockResult.Success(tank.SlotIndex, assigned, assigned ? target : default);
            }
            finally
            {
                if (_state != GameState.Win && _state != GameState.Lose)
                {
                    SetState(GameState.PlayerInput);
                }

                LastTurn = new TurnResolution(
                    _poppedBubble,
                    _poppedBubbleId,
                    _poppedSlotId,
                    _promotions.ToArray(),
                    _pileMoves.ToArray(),
                    _spawns.ToArray(),
                    _state == GameState.Win);
            }
        }

        private TankRuntimeState FindTank(int slotIndex)
        {
            for (var i = 0; i < _tanks.Count; i++)
            {
                if (_tanks[i] != null && _tanks[i].SlotIndex == slotIndex)
                {
                    return _tanks[i];
                }
            }

            return null;
        }

        private void SetState(GameState state)
        {
            var becameOutcome = (state == GameState.Win || state == GameState.Lose) && _state != state;
            _state = state;
            _stateTrace.Add(state);
            _stateObserver?.Invoke(state);
            if (becameOutcome)
            {
                _outcomeHandler?.Invoke(state);
            }
        }

        private static LevelSession Start(
            GameConfig config,
            IReadOnlyList<FishType> targetGroupQueue,
            int unlockedTankCount,
            int totalFishRequired,
            IReadOnlyList<string> bubbleIds,
            IReadOnlyList<IReadOnlyList<FishType>> bubbleFish,
            bool allBubblesInPlay)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            if (targetGroupQueue == null)
            {
                throw new ArgumentNullException(nameof(targetGroupQueue));
            }

            if (config.MaxTankSlots <= 0 || config.TankCapacity <= 0 || config.WaitingTraySlotCount <= 0 || config.WaitingTrayFailCount <= 0)
            {
                throw new ArgumentException("GameConfig tank and waiting-tray values must be positive.", nameof(config));
            }

            var targets = new TargetGroupQueue(targetGroupQueue);
            var tanks = new List<TankRuntimeState>(config.MaxTankSlots);
            for (var i = 0; i < config.MaxTankSlots; i++)
            {
                if (i < unlockedTankCount)
                {
                    var tank = TankRuntimeState.CreateUnlocked(i, config.TankCapacity);
                    if (targets.TryTakeNext(out var target))
                    {
                        tank.AssignTarget(target);
                    }

                    tanks.Add(tank);
                }
                else
                {
                    tanks.Add(TankRuntimeState.CreateLocked(i, config.TankCapacity));
                }
            }

            var fishById = new Dictionary<int, FishRuntimeState>();
            var bubbles = new List<BubbleRuntimeState>();
            var bubblesById = new Dictionary<string, BubbleRuntimeState>();
            var nextId = 1;
            var bubbleCount = bubbleFish != null ? bubbleFish.Count : 0;
            for (var i = 0; i < bubbleCount; i++)
            {
                var id = bubbleIds != null && i < bubbleIds.Count && !string.IsNullOrEmpty(bubbleIds[i])
                    ? bubbleIds[i]
                    : "bubble_" + i;
                var source = bubbleFish[i];
                var fish = new List<FishRuntimeState>(source != null ? source.Count : 0);
                if (source != null)
                {
                    for (var f = 0; f < source.Count; f++)
                    {
                        var runtimeFish = new FishRuntimeState(nextId, source[f], id);
                        nextId++;
                        fish.Add(runtimeFish);
                        fishById.Add(runtimeFish.Id, runtimeFish);
                    }
                }

                var bubble = new BubbleRuntimeState(id, fish, allBubblesInPlay);
                bubbles.Add(bubble);
                if (!bubblesById.ContainsKey(id))
                {
                    bubblesById.Add(id, bubble);
                }
            }

            var tray = new WaitingTrayState(config.WaitingTraySlotCount, config.WaitingTrayFailCount);
            var progress = new LevelRuntimeProgress(totalFishRequired);
            return new LevelSession(config, targets, tanks, tray, progress, bubbles, fishById, bubblesById);
        }

        private static IReadOnlyList<FishType> CopyFish(IReadOnlyList<FishType> source)
        {
            if (source == null)
            {
                return Array.Empty<FishType>();
            }

            var copy = new FishType[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                copy[i] = source[i];
            }

            return copy;
        }

        private static int CountMatching(IReadOnlyList<FishRuntimeState> fish, int fishId)
        {
            if (fish == null)
            {
                return 0;
            }

            var count = 0;
            for (var i = 0; i < fish.Count; i++)
            {
                if (fish[i] != null && fish[i].Id == fishId)
                {
                    count++;
                }
            }

            return count;
        }

        private static int[] CopyContainedIds(TankRuntimeState tank)
        {
            var contained = tank.ContainedFish;
            var ids = new int[contained.Count];
            for (var i = 0; i < contained.Count; i++)
            {
                ids[i] = contained[i].Id;
            }

            return ids;
        }
    }
}
