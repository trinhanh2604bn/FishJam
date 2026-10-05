using System.Collections.Generic;
using FishPuzzle.Bubbles;
using FishPuzzle.Domain;

namespace FishPuzzle.Core
{
    /// <summary>
    /// What one accepted tap did after the fish reached its first destination.
    /// </summary>
    public sealed class TrayPromotionRecord
    {
        public TrayPromotionRecord(
            int fishId,
            int tankSlotIndex,
            int landedOrdinal,
            bool completedTank,
            int[] consumedFishIds,
            int sourceTrayIndex,
            bool hasNextTarget,
            FishType nextTarget)
        {
            FishId = fishId;
            TankSlotIndex = tankSlotIndex;
            LandedOrdinal = landedOrdinal;
            CompletedTank = completedTank;
            ConsumedFishIds = consumedFishIds ?? System.Array.Empty<int>();
            SourceTrayIndex = sourceTrayIndex;
            HasNextTarget = hasNextTarget;
            NextTarget = nextTarget;
        }

        public int FishId { get; }

        public int TankSlotIndex { get; }

        public int LandedOrdinal { get; }

        public bool CompletedTank { get; }

        public int[] ConsumedFishIds { get; }

        public int SourceTrayIndex { get; }

        public bool HasNextTarget { get; }

        public FishType NextTarget { get; }
    }

    public sealed class TurnResolution
    {
        public TurnResolution(
            bool poppedBubble,
            string poppedBubbleId,
            int poppedSlotId,
            IReadOnlyList<TrayPromotionRecord> promotions,
            IReadOnlyList<BubblePileMove> pileMoves,
            IReadOnlyList<BubbleSpawn> spawns,
            bool won)
        {
            PoppedBubble = poppedBubble;
            PoppedBubbleId = poppedBubbleId ?? string.Empty;
            PoppedSlotId = poppedSlotId;
            Promotions = promotions ?? System.Array.Empty<TrayPromotionRecord>();
            PileMoves = pileMoves ?? System.Array.Empty<BubblePileMove>();
            Spawns = spawns ?? System.Array.Empty<BubbleSpawn>();
            Won = won;
        }

        public bool PoppedBubble { get; }

        public string PoppedBubbleId { get; }

        public int PoppedSlotId { get; }

        public IReadOnlyList<TrayPromotionRecord> Promotions { get; }

        public IReadOnlyList<BubblePileMove> PileMoves { get; }

        public IReadOnlyList<BubbleSpawn> Spawns { get; }

        public bool Won { get; }

        public static TurnResolution Empty { get; } = new TurnResolution(
            false,
            string.Empty,
            -1,
            System.Array.Empty<TrayPromotionRecord>(),
            System.Array.Empty<BubblePileMove>(),
            System.Array.Empty<BubbleSpawn>(),
            false);
    }
}
