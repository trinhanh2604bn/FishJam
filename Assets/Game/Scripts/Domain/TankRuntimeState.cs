using System;
using System.Collections.Generic;
using FishPuzzle.Tanks;

namespace FishPuzzle.Domain
{
    /// <summary>
    /// One tank slot during a level attempt. Fill never exceeds the capacity taken from GameConfig.
    /// </summary>
    public sealed class TankRuntimeState
    {
        private readonly List<FishRuntimeState> _contained = new List<FishRuntimeState>();

        private TankRuntimeState(int slotIndex, bool unlocked, int capacity, TankState state)
        {
            if (slotIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(slotIndex));
            }

            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            SlotIndex = slotIndex;
            IsUnlocked = unlocked;
            Capacity = capacity;
            State = state;
        }

        public int SlotIndex { get; }

        public bool IsUnlocked { get; private set; }

        public int Capacity { get; }

        public TankState State { get; private set; }

        public bool HasTarget { get; private set; }

        public FishType CurrentTarget { get; private set; }

        public int FillCount => _contained.Count;

        public IReadOnlyList<FishRuntimeState> ContainedFish => _contained;

        public static TankRuntimeState CreateLocked(int slotIndex, int capacity)
        {
            return new TankRuntimeState(slotIndex, false, capacity, TankState.Locked);
        }

        public static TankRuntimeState CreateUnlocked(int slotIndex, int capacity)
        {
            return new TankRuntimeState(slotIndex, true, capacity, TankState.WaitingForTarget);
        }

        public bool TryUnlock()
        {
            if (IsUnlocked || State != TankState.Locked)
            {
                return false;
            }

            IsUnlocked = true;
            State = TankState.WaitingForTarget;
            return true;
        }

        public void RevertUnlock()
        {
            if (!IsUnlocked || FillCount != 0 || State == TankState.ResolvingTriple)
            {
                return;
            }

            IsUnlocked = false;
            HasTarget = false;
            CurrentTarget = default;
            State = TankState.Locked;
        }

        public void AssignTarget(FishType target)
        {
            if (!IsUnlocked || State == TankState.Locked || _contained.Count != 0)
            {
                return;
            }

            CurrentTarget = target;
            HasTarget = true;
            State = TankState.AcceptingFish;
        }

        public bool CanAccept(FishType fishType)
        {
            return State == TankState.AcceptingFish
                && HasTarget
                && CurrentTarget == fishType
                && FillCount < Capacity;
        }

        public bool TryAccept(FishRuntimeState fish)
        {
            if (fish == null || !CanAccept(fish.Type))
            {
                return false;
            }

            _contained.Add(fish);
            fish.CommitToTank();
            return true;
        }

        public bool TryBeginResolution()
        {
            if (State != TankState.AcceptingFish || FillCount != Capacity)
            {
                return false;
            }

            State = TankState.ResolvingTriple;
            return true;
        }

        public void CompleteResolution(bool hasNextTarget, FishType nextTarget)
        {
            if (State != TankState.ResolvingTriple)
            {
                return;
            }

            for (var i = 0; i < _contained.Count; i++)
            {
                _contained[i].Consume();
            }

            _contained.Clear();
            if (hasNextTarget)
            {
                AssignTarget(nextTarget);
                return;
            }

            HasTarget = false;
            CurrentTarget = default;
            State = TankState.CompletedNoMoreTargets;
        }
    }
}
