using FishPuzzle.Domain;

namespace FishPuzzle.Core
{
    public enum FishSelectionOutcome
    {
        Ignored = 0,
        RoutedToTank = 1,
        RoutedToTray = 2,
        Lost = 3
    }

    /// <summary>
    /// What one accepted or ignored fish tap did to the level attempt.
    /// </summary>
    public sealed class FishSelectionResult
    {
        private FishSelectionResult(
            FishSelectionOutcome outcome,
            int fishId,
            string sourceBubbleId,
            int tankSlotIndex,
            int traySlotIndex,
            int landedOrdinal,
            int fillCountAfterAccept,
            bool tankCompleted,
            bool hasNextTarget,
            FishType nextTarget,
            int[] consumedFishIds,
            int collectedFishCount)
        {
            Outcome = outcome;
            FishId = fishId;
            SourceBubbleId = sourceBubbleId ?? string.Empty;
            TankSlotIndex = tankSlotIndex;
            TraySlotIndex = traySlotIndex;
            LandedOrdinal = landedOrdinal;
            FillCountAfterAccept = fillCountAfterAccept;
            TankCompleted = tankCompleted;
            HasNextTarget = hasNextTarget;
            NextTarget = nextTarget;
            ConsumedFishIds = consumedFishIds ?? System.Array.Empty<int>();
            CollectedFishCount = collectedFishCount;
        }

        public FishSelectionOutcome Outcome { get; }

        public bool Accepted => Outcome != FishSelectionOutcome.Ignored;

        public int FishId { get; }

        public string SourceBubbleId { get; }

        public int TankSlotIndex { get; }

        public int TraySlotIndex { get; }

        public int LandedOrdinal { get; }

        public int FillCountAfterAccept { get; }

        public bool TankCompleted { get; }

        public bool HasNextTarget { get; }

        public FishType NextTarget { get; }

        public int[] ConsumedFishIds { get; }

        public int CollectedFishCount { get; }

        public static FishSelectionResult Ignored(int fishId)
        {
            return new FishSelectionResult(
                FishSelectionOutcome.Ignored,
                fishId,
                string.Empty,
                -1,
                -1,
                -1,
                0,
                false,
                false,
                default,
                System.Array.Empty<int>(),
                0);
        }

        public static FishSelectionResult ToTank(
            int fishId,
            string sourceBubbleId,
            int tankSlotIndex,
            int landedOrdinal,
            int fillCountAfterAccept,
            bool tankCompleted,
            bool hasNextTarget,
            FishType nextTarget,
            int[] consumedFishIds,
            int collectedFishCount)
        {
            return new FishSelectionResult(
                FishSelectionOutcome.RoutedToTank,
                fishId,
                sourceBubbleId,
                tankSlotIndex,
                -1,
                landedOrdinal,
                fillCountAfterAccept,
                tankCompleted,
                hasNextTarget,
                nextTarget,
                consumedFishIds,
                collectedFishCount);
        }

        public static FishSelectionResult ToTray(
            int fishId,
            string sourceBubbleId,
            int traySlotIndex,
            bool lost,
            int collectedFishCount)
        {
            return new FishSelectionResult(
                lost ? FishSelectionOutcome.Lost : FishSelectionOutcome.RoutedToTray,
                fishId,
                sourceBubbleId,
                -1,
                traySlotIndex,
                traySlotIndex,
                0,
                false,
                false,
                default,
                System.Array.Empty<int>(),
                collectedFishCount);
        }
    }
}
