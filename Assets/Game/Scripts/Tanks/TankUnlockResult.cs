using FishPuzzle.Domain;

namespace FishPuzzle.Tanks
{
    public enum TankUnlockStatus
    {
        Succeeded = 0,
        InsufficientGold = 1,
        Rejected = 2
    }

    /// <summary>
    /// Result of one extra-tank unlock attempt. Gold changes only on a successful gold unlock.
    /// </summary>
    public sealed class TankUnlockResult
    {
        private TankUnlockResult(TankUnlockStatus status, int slotIndex, bool assignedTarget, FishType target)
        {
            Status = status;
            SlotIndex = slotIndex;
            AssignedTarget = assignedTarget;
            Target = target;
        }

        public TankUnlockStatus Status { get; }

        public int SlotIndex { get; }

        public bool AssignedTarget { get; }

        public FishType Target { get; }

        public bool Succeeded => Status == TankUnlockStatus.Succeeded;

        public bool InsufficientGold => Status == TankUnlockStatus.InsufficientGold;

        public static TankUnlockResult Success(int slotIndex, bool assignedTarget, FishType target)
        {
            return new TankUnlockResult(TankUnlockStatus.Succeeded, slotIndex, assignedTarget, target);
        }

        public static TankUnlockResult NotEnoughGold(int slotIndex)
        {
            return new TankUnlockResult(TankUnlockStatus.InsufficientGold, slotIndex, false, default);
        }

        public static TankUnlockResult Reject(int slotIndex)
        {
            return new TankUnlockResult(TankUnlockStatus.Rejected, slotIndex, false, default);
        }
    }
}
