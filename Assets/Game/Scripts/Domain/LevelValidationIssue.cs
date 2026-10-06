using System;
using System.Globalization;

namespace FishPuzzle.Domain
{
    public enum LevelValidationSeverity
    {
        Error = 0,
        Warning = 1
    }

    /// <summary>
    /// Stable issue codes returned by <see cref="LevelValidator"/>.
    /// </summary>
    public static class LevelValidationCodes
    {
        public const string LevelNull = "LEVEL_NULL";
        public const string ConfigNull = "CONFIG_NULL";
        public const string LevelIdEmpty = "LEVEL_ID_EMPTY";
        public const string UnlockedTankCountBelowMinimum = "LEVEL_UNLOCKED_TANK_COUNT_BELOW_MINIMUM";
        public const string UnlockedTankCountAboveMax = "LEVEL_UNLOCKED_TANK_COUNT_ABOVE_MAX";
        public const string TargetQueueNull = "LEVEL_TARGET_QUEUE_NULL";
        public const string TargetQueueEmpty = "LEVEL_TARGET_QUEUE_EMPTY";
        public const string BubbleQueueNull = "LEVEL_BUBBLE_QUEUE_NULL";
        public const string BubbleQueueEmpty = "LEVEL_BUBBLE_QUEUE_EMPTY";
        public const string PileLayoutMissing = "LEVEL_PILE_LAYOUT_MISSING";
        public const string TotalFishRequiredMismatch = "LEVEL_TOTAL_FISH_REQUIRED_MISMATCH";
        public const string FishCountMismatch = "LEVEL_FISH_COUNT_MISMATCH";
        public const string GlobalFishCountMismatch = "LEVEL_GLOBAL_FISH_COUNT_MISMATCH";
        public const string BubbleNull = "BUBBLE_NULL";
        public const string BubbleIdEmpty = "BUBBLE_ID_EMPTY";
        public const string BubbleIdDuplicate = "BUBBLE_ID_DUPLICATE";
        public const string BubbleFishesNull = "BUBBLE_FISHES_NULL";
        public const string BubbleFishesEmpty = "BUBBLE_FISHES_EMPTY";
        public const string BubbleDistinctTypeCount = "BUBBLE_DISTINCT_TYPE_COUNT";
        public const string BubbleFishCountAboveMax = "BUBBLE_FISH_COUNT_ABOVE_MAX";
        public const string PileSlotsNull = "PILE_SLOTS_NULL";
        public const string PileNoSlots = "PILE_NO_SLOTS";
        public const string PileSlotNull = "PILE_SLOT_NULL";
        public const string PileDuplicateSlotId = "PILE_DUPLICATE_SLOT_ID";
        public const string PileDownCandidatesNull = "PILE_DOWN_CANDIDATES_NULL";
        public const string PileDownCandidateMissing = "PILE_DOWN_CANDIDATE_MISSING";
        public const string PileSelfReference = "PILE_SELF_REFERENCE";
        public const string PileUpwardGravity = "PILE_UPWARD_GRAVITY";
        public const string PileDuplicateDownCandidate = "PILE_DUPLICATE_DOWN_CANDIDATE";
        public const string PileNoTopSpawn = "PILE_NO_TOP_SPAWN";
        public const string PileGravityCycle = "PILE_GRAVITY_CYCLE";
        public const string ConfigTankCapacityInvalid = "CONFIG_TANK_CAPACITY_INVALID";
        public const string ConfigDistinctFishTypesInvalid = "CONFIG_DISTINCT_FISH_TYPES_INVALID";
        public const string ConfigMaxTankSlotsInvalid = "CONFIG_MAX_TANK_SLOTS_INVALID";
    }

    /// <summary>
    /// One actionable level-authoring problem. Designer data is never repaired.
    /// </summary>
    public sealed class LevelValidationIssue
    {
        public LevelValidationIssue(
            string code,
            LevelValidationSeverity severity,
            string message,
            string levelId,
            string bubbleId,
            int? slotId,
            FishType? fishType)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                throw new ArgumentException("Issue code is required.", nameof(code));
            }

            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            Code = code;
            Severity = severity;
            Message = message;
            LevelId = levelId ?? string.Empty;
            BubbleId = bubbleId ?? string.Empty;
            SlotId = slotId;
            FishType = fishType;
        }

        public string Code { get; }

        public LevelValidationSeverity Severity { get; }

        public string Message { get; }

        public string LevelId { get; }

        public string BubbleId { get; }

        public int? SlotId { get; }

        public FishType? FishType { get; }

        public override string ToString()
        {
            var bubble = string.IsNullOrEmpty(BubbleId) ? "-" : BubbleId;
            var slot = SlotId.HasValue ? SlotId.Value.ToString(CultureInfo.InvariantCulture) : "-";
            var fish = FishType.HasValue ? FishType.Value.ToString() : "-";
            var level = string.IsNullOrEmpty(LevelId) ? "-" : LevelId;
            return Code
                + " | severity=" + Severity
                + " | level=" + level
                + " | bubble=" + bubble
                + " | slot=" + slot
                + " | fish=" + fish
                + " | " + Message;
        }
    }
}
