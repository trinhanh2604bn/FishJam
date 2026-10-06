using System;
using System.Collections.Generic;
using System.Text;
using FishPuzzle.Bubbles;
using FishPuzzle.Core;

namespace FishPuzzle.Domain
{
    /// <summary>
    /// Pure level-authoring checks. Does not mutate data, log, or read global state.
    /// </summary>
    public sealed class LevelValidator
    {
        public LevelValidationResult Validate(LevelData level, GameConfig config)
        {
            var issues = new List<LevelValidationIssue>();
            if (level == null || config == null)
            {
                if (level == null)
                {
                    issues.Add(Error(
                        LevelValidationCodes.LevelNull,
                        "LevelData is null. Assign a level asset before validation.",
                        string.Empty));
                }

                if (config == null)
                {
                    issues.Add(Error(
                        LevelValidationCodes.ConfigNull,
                        "GameConfig is null. Pass the authoritative GameConfig asset into LevelValidator.",
                        level == null ? string.Empty : SafeLevelId(level)));
                }

                return new LevelValidationResult(issues);
            }

            var levelId = SafeLevelId(level);
            var tankCapacityValid = config.TankCapacity > 0;
            var distinctValid = config.DistinctFishTypesPerBubble > 0;
            var maxTanksValid = config.MaxTankSlots >= 1;

            if (string.IsNullOrEmpty(levelId))
            {
                issues.Add(Error(
                    LevelValidationCodes.LevelIdEmpty,
                    "Level id is empty. Use a stable technical id such as level_001.",
                    levelId));
            }

            if (!maxTanksValid)
            {
                issues.Add(Error(
                    LevelValidationCodes.ConfigMaxTankSlotsInvalid,
                    "GameConfig.MaxTankSlots is " + config.MaxTankSlots + ". It must be at least 1 before unlocked tank counts can be validated.",
                    levelId));
            }

            if (level.InitialUnlockedTankCount < 1)
            {
                issues.Add(Error(
                    LevelValidationCodes.UnlockedTankCountBelowMinimum,
                    Label(levelId) + " initialUnlockedTankCount is " + level.InitialUnlockedTankCount + ". It must be at least 1.",
                    levelId));
            }
            else if (maxTanksValid && level.InitialUnlockedTankCount > config.MaxTankSlots)
            {
                issues.Add(Error(
                    LevelValidationCodes.UnlockedTankCountAboveMax,
                    Label(levelId) + " initialUnlockedTankCount is " + level.InitialUnlockedTankCount
                    + ", which is above GameConfig.MaxTankSlots (" + config.MaxTankSlots + ").",
                    levelId));
            }

            if (!tankCapacityValid)
            {
                issues.Add(Error(
                    LevelValidationCodes.ConfigTankCapacityInvalid,
                    "GameConfig.TankCapacity is " + config.TankCapacity + ". It must be greater than 0 before fish totals can be validated.",
                    levelId));
            }

            if (!distinctValid)
            {
                issues.Add(Error(
                    LevelValidationCodes.ConfigDistinctFishTypesInvalid,
                    "GameConfig.DistinctFishTypesPerBubble is " + config.DistinctFishTypesPerBubble
                    + ". It must be greater than 0 before standard bubbles can be validated.",
                    levelId));
            }

            var targets = level.TargetGroupQueue;
            if (targets == null)
            {
                issues.Add(Error(
                    LevelValidationCodes.TargetQueueNull,
                    Label(levelId) + " targetGroupQueue is null. Assign the ordered target groups for this level.",
                    levelId));
            }
            else if (targets.Count == 0)
            {
                issues.Add(Error(
                    LevelValidationCodes.TargetQueueEmpty,
                    Label(levelId) + " targetGroupQueue is empty. Add at least one target group. Each entry is one group of tank-capacity fish.",
                    levelId));
            }
            else if (tankCapacityValid)
            {
                var expectedTotal = targets.Count * config.TankCapacity;
                if (level.TotalFishRequired != expectedTotal)
                {
                    issues.Add(Error(
                        LevelValidationCodes.TotalFishRequiredMismatch,
                        Label(levelId) + " totalFishRequired is " + level.TotalFishRequired
                        + ", but targetGroupQueue has " + targets.Count + " groups and tank capacity is " + config.TankCapacity
                        + ", so the required total is " + expectedTotal + ".",
                        levelId));
                }
            }

            var bubbles = level.BubbleQueue;
            if (bubbles == null)
            {
                issues.Add(Error(
                    LevelValidationCodes.BubbleQueueNull,
                    Label(levelId) + " bubbleQueue is null. Assign the ordered bubble definitions for this level.",
                    levelId));
            }
            else if (bubbles.Count == 0)
            {
                issues.Add(Error(
                    LevelValidationCodes.BubbleQueueEmpty,
                    Label(levelId) + " bubbleQueue is empty. Add at least one bubble definition.",
                    levelId));
            }

            if (level.PileLayout == null)
            {
                issues.Add(Error(
                    LevelValidationCodes.PileLayoutMissing,
                    Label(levelId) + " has no pile layout. Assign a BubblePileLayout such as BPL_Standard_10.",
                    levelId));
            }

            var actual = CreateFishCounts();
            var totalFish = 0;
            if (bubbles != null)
            {
                ValidateBubbles(bubbles, config, levelId, distinctValid, actual, ref totalFish, issues);
            }

            if (targets != null && bubbles != null && tankCapacityValid)
            {
                ValidatePerTypePopulation(targets, config.TankCapacity, actual, levelId, issues);
            }

            if (bubbles != null && level.TotalFishRequired != totalFish)
            {
                issues.Add(Error(
                    LevelValidationCodes.GlobalFishCountMismatch,
                    Label(levelId) + " totalFishRequired is " + level.TotalFishRequired
                    + ", but the bubble population contains " + totalFish + " fish.",
                    levelId));
            }

            if (level.PileLayout != null)
            {
                ValidatePile(level.PileLayout, levelId, issues);
            }

            return new LevelValidationResult(issues);
        }

        private static void ValidateBubbles(
            IReadOnlyList<BubbleDefinition> bubbles,
            GameConfig config,
            string levelId,
            bool distinctValid,
            Dictionary<FishType, int> actual,
            ref int totalFish,
            List<LevelValidationIssue> issues)
        {
            var idCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < bubbles.Count; i++)
            {
                var bubble = bubbles[i];
                if (bubble == null)
                {
                    issues.Add(Error(
                        LevelValidationCodes.BubbleNull,
                        Label(levelId) + " bubbleQueue contains a null entry at index " + i + ".",
                        levelId));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(bubble.BubbleId))
                {
                    issues.Add(Error(
                        LevelValidationCodes.BubbleIdEmpty,
                        Label(levelId) + " bubbleQueue index " + i + " has an empty bubbleId. Use a stable technical id such as L001_B001.",
                        levelId));
                }
                else
                {
                    idCounts.TryGetValue(bubble.BubbleId, out var seen);
                    idCounts[bubble.BubbleId] = seen + 1;
                }

                var fishes = bubble.Fishes;
                if (fishes == null)
                {
                    issues.Add(Error(
                        LevelValidationCodes.BubbleFishesNull,
                        DescribeBubble(bubble, i) + " fish list is null. Assign the fish for this bubble.",
                        levelId,
                        bubble.BubbleId));
                    continue;
                }

                if (fishes.Count == 0)
                {
                    issues.Add(Error(
                        LevelValidationCodes.BubbleFishesEmpty,
                        DescribeBubble(bubble, i) + " has no fish. Add the fish that belong in this bubble.",
                        levelId,
                        bubble.BubbleId));
                    continue;
                }

                if (config.MaxFishPerBubble > 0 && fishes.Count > config.MaxFishPerBubble)
                {
                    issues.Add(Error(
                        LevelValidationCodes.BubbleFishCountAboveMax,
                        DescribeBubble(bubble, i) + " contains " + fishes.Count
                        + " fish; the maximum is " + config.MaxFishPerBubble + " (GameConfig.MaxFishPerBubble). Add another bubble instead.",
                        levelId,
                        bubble.BubbleId));
                }

                var distinct = new HashSet<FishType>();
                for (var fishIndex = 0; fishIndex < fishes.Count; fishIndex++)
                {
                    var fish = fishes[fishIndex];
                    distinct.Add(fish);
                    actual.TryGetValue(fish, out var count);
                    actual[fish] = count + 1;
                    totalFish++;
                }

                if (distinctValid && bubble.Modifier == BubbleModifier.None && distinct.Count != config.DistinctFishTypesPerBubble)
                {
                    issues.Add(Error(
                        LevelValidationCodes.BubbleDistinctTypeCount,
                        DescribeBubble(bubble, i) + " contains " + distinct.Count
                        + " distinct FishTypes; expected exactly " + config.DistinctFishTypesPerBubble + ".",
                        levelId,
                        bubble.BubbleId));
                }
            }

            foreach (var pair in idCounts)
            {
                if (pair.Value < 2)
                {
                    continue;
                }

                issues.Add(Error(
                    LevelValidationCodes.BubbleIdDuplicate,
                    "Bubble id " + pair.Key + " is used " + pair.Value + " times. Bubble ids must be unique within the level.",
                    levelId,
                    pair.Key));
            }
        }

        private static void ValidatePerTypePopulation(
            IReadOnlyList<FishType> targets,
            int tankCapacity,
            Dictionary<FishType, int> actual,
            string levelId,
            List<LevelValidationIssue> issues)
        {
            var required = CreateFishCounts();
            for (var i = 0; i < targets.Count; i++)
            {
                var fish = targets[i];
                required.TryGetValue(fish, out var count);
                required[fish] = count + tankCapacity;
            }

            var fishTypes = (FishType[])Enum.GetValues(typeof(FishType));
            for (var i = 0; i < fishTypes.Length; i++)
            {
                var fish = fishTypes[i];
                required.TryGetValue(fish, out var requiredCount);
                actual.TryGetValue(fish, out var actualCount);
                if (requiredCount == actualCount)
                {
                    continue;
                }

                issues.Add(Error(
                    LevelValidationCodes.FishCountMismatch,
                    Label(levelId) + " requires " + requiredCount + " " + fish
                    + " fish from target groups but bubble population contains " + actualCount + ".",
                    levelId,
                    null,
                    null,
                    fish));
            }
        }

        private static void ValidatePile(BubblePileLayout layout, string levelId, List<LevelValidationIssue> issues)
        {
            var slots = layout.Slots;
            if (slots == null)
            {
                issues.Add(Error(
                    LevelValidationCodes.PileSlotsNull,
                    "Pile layout slot list is null. Assign a slot list with at least one slot.",
                    levelId));
                return;
            }

            if (slots.Count == 0)
            {
                issues.Add(Error(
                    LevelValidationCodes.PileNoSlots,
                    "Pile layout has no slots. Add at least one pile slot.",
                    levelId));
                return;
            }

            var byId = new Dictionary<int, List<BubblePileSlotDefinition>>();
            var usableSlotCount = 0;
            var hasTopSpawn = false;
            for (var i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot == null)
                {
                    issues.Add(Error(
                        LevelValidationCodes.PileSlotNull,
                        "Pile layout contains a null slot at index " + i + ".",
                        levelId));
                    continue;
                }

                usableSlotCount++;
                if (!byId.TryGetValue(slot.SlotId, out var group))
                {
                    group = new List<BubblePileSlotDefinition>();
                    byId.Add(slot.SlotId, group);
                }

                group.Add(slot);
                if (slot.CanReceiveSpawnFromTop)
                {
                    hasTopSpawn = true;
                }
            }

            foreach (var pair in byId)
            {
                if (pair.Value.Count < 2)
                {
                    continue;
                }

                issues.Add(Error(
                    LevelValidationCodes.PileDuplicateSlotId,
                    "Pile layout contains " + pair.Value.Count + " slots with id " + pair.Key + ". Slot ids must be unique.",
                    levelId,
                    null,
                    pair.Key));
            }

            if (usableSlotCount == 0)
            {
                issues.Add(Error(
                    LevelValidationCodes.PileNoSlots,
                    "Pile layout has no slots. Add at least one pile slot.",
                    levelId));
                return;
            }

            if (!hasTopSpawn)
            {
                issues.Add(Error(
                    LevelValidationCodes.PileNoTopSpawn,
                    "Pile layout has no slot with canReceiveSpawnFromTop enabled. Mark at least one top row slot so new bubbles can enter from above.",
                    levelId));
            }

            var adjacency = new Dictionary<int, List<int>>();
            var nodeOrder = new List<int>();
            for (var i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot == null)
                {
                    continue;
                }

                if (!adjacency.ContainsKey(slot.SlotId))
                {
                    adjacency.Add(slot.SlotId, new List<int>());
                    nodeOrder.Add(slot.SlotId);
                }

                ValidateDownCandidates(slot, byId, adjacency[slot.SlotId], levelId, issues);
            }

            ValidateGravityCycles(adjacency, nodeOrder, levelId, issues);
        }

        private static void ValidateDownCandidates(
            BubblePileSlotDefinition slot,
            Dictionary<int, List<BubblePileSlotDefinition>> byId,
            List<int> adjacency,
            string levelId,
            List<LevelValidationIssue> issues)
        {
            var candidates = slot.DownCandidateSlotIds;
            if (candidates == null)
            {
                issues.Add(Error(
                    LevelValidationCodes.PileDownCandidatesNull,
                    "Slot " + slot.SlotId + " downCandidateSlotIds is null. Use an empty list when the slot has no lower destinations.",
                    levelId,
                    null,
                    slot.SlotId));
                return;
            }

            var seen = new HashSet<int>();
            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (!seen.Add(candidate))
                {
                    issues.Add(Error(
                        LevelValidationCodes.PileDuplicateDownCandidate,
                        "Slot " + slot.SlotId + " lists down candidate " + candidate + " more than once. Remove the duplicate.",
                        levelId,
                        null,
                        slot.SlotId));
                    continue;
                }

                if (candidate == slot.SlotId)
                {
                    issues.Add(Error(
                        LevelValidationCodes.PileSelfReference,
                        "Slot " + slot.SlotId + " lists itself as a down candidate. A slot cannot fall into itself.",
                        levelId,
                        null,
                        slot.SlotId));
                    continue;
                }

                if (!byId.TryGetValue(candidate, out var destinations) || destinations.Count == 0)
                {
                    issues.Add(Error(
                        LevelValidationCodes.PileDownCandidateMissing,
                        "Slot " + slot.SlotId + " lists down candidate " + candidate + ", but no slot with that id exists in the pile layout.",
                        levelId,
                        null,
                        slot.SlotId));
                    continue;
                }

                var destinationExists = false;
                for (var destinationIndex = 0; destinationIndex < destinations.Count; destinationIndex++)
                {
                    var destination = destinations[destinationIndex];
                    if (destination == null)
                    {
                        continue;
                    }

                    destinationExists = true;
                    if (destination.Row >= slot.Row)
                    {
                        issues.Add(Error(
                            LevelValidationCodes.PileUpwardGravity,
                            "Slot " + slot.SlotId + " lists down candidate " + candidate
                            + ", but destination row " + destination.Row + " is not strictly below source row " + slot.Row
                            + ". Down candidates must move to a lower row.",
                            levelId,
                            null,
                            slot.SlotId));
                    }
                }

                if (destinationExists && !ContainsId(adjacency, candidate))
                {
                    adjacency.Add(candidate);
                }
            }
        }

        private static void ValidateGravityCycles(
            Dictionary<int, List<int>> adjacency,
            List<int> nodeOrder,
            string levelId,
            List<LevelValidationIssue> issues)
        {
            var color = new Dictionary<int, int>(adjacency.Count);
            for (var i = 0; i < nodeOrder.Count; i++)
            {
                color[nodeOrder[i]] = 0;
            }

            var reported = new HashSet<string>(StringComparer.Ordinal);
            var path = new List<int>();
            var stack = new List<Frame>();
            for (var startIndex = 0; startIndex < nodeOrder.Count; startIndex++)
            {
                var start = nodeOrder[startIndex];
                if (color[start] != 0)
                {
                    continue;
                }

                color[start] = 1;
                path.Add(start);
                stack.Add(new Frame(start, 0));
                while (stack.Count > 0)
                {
                    var frameIndex = stack.Count - 1;
                    var frame = stack[frameIndex];
                    var neighbors = adjacency[frame.SlotId];
                    if (frame.NextIndex < neighbors.Count)
                    {
                        var next = neighbors[frame.NextIndex];
                        stack[frameIndex] = new Frame(frame.SlotId, frame.NextIndex + 1);
                        if (!color.TryGetValue(next, out var nextColor))
                        {
                            continue;
                        }

                        if (nextColor == 1)
                        {
                            ReportCycle(path, next, reported, levelId, issues);
                        }
                        else if (nextColor == 0)
                        {
                            color[next] = 1;
                            path.Add(next);
                            stack.Add(new Frame(next, 0));
                        }
                    }
                    else
                    {
                        color[frame.SlotId] = 2;
                        stack.RemoveAt(frameIndex);
                        if (path.Count > 0)
                        {
                            path.RemoveAt(path.Count - 1);
                        }
                    }
                }
            }
        }

        private static void ReportCycle(
            List<int> path,
            int backTo,
            HashSet<string> reported,
            string levelId,
            List<LevelValidationIssue> issues)
        {
            var start = -1;
            for (var i = 0; i < path.Count; i++)
            {
                if (path[i] == backTo)
                {
                    start = i;
                    break;
                }
            }

            if (start < 0 || start >= path.Count)
            {
                return;
            }

            var cycleLength = path.Count - start;
            if (cycleLength <= 0)
            {
                return;
            }

            var rotated = new int[cycleLength];
            var minOffset = 0;
            for (var i = 0; i < cycleLength; i++)
            {
                rotated[i] = path[start + i];
                if (rotated[i] < rotated[minOffset])
                {
                    minOffset = i;
                }
            }

            var canonical = new int[cycleLength];
            for (var i = 0; i < cycleLength; i++)
            {
                canonical[i] = rotated[(minOffset + i) % cycleLength];
            }

            var keyBuilder = new StringBuilder();
            var descriptionBuilder = new StringBuilder();
            for (var i = 0; i < canonical.Length; i++)
            {
                if (i > 0)
                {
                    keyBuilder.Append('>');
                    descriptionBuilder.Append(" -> ");
                }

                keyBuilder.Append(canonical[i]);
                descriptionBuilder.Append(canonical[i]);
            }

            descriptionBuilder.Append(" -> ");
            descriptionBuilder.Append(canonical[0]);
            var key = keyBuilder.ToString();
            if (!reported.Add(key))
            {
                return;
            }

            var description = descriptionBuilder.ToString();
            issues.Add(Error(
                LevelValidationCodes.PileGravityCycle,
                "Pile gravity graph contains a cycle: " + description + ". Downward gravity must be acyclic.",
                levelId,
                null,
                canonical[0]));
        }

        private static Dictionary<FishType, int> CreateFishCounts()
        {
            var counts = new Dictionary<FishType, int>();
            var fishTypes = (FishType[])Enum.GetValues(typeof(FishType));
            for (var i = 0; i < fishTypes.Length; i++)
            {
                counts[fishTypes[i]] = 0;
            }

            return counts;
        }

        private static bool ContainsId(List<int> ids, int id)
        {
            for (var i = 0; i < ids.Count; i++)
            {
                if (ids[i] == id)
                {
                    return true;
                }
            }

            return false;
        }

        private static string SafeLevelId(LevelData level)
        {
            return string.IsNullOrWhiteSpace(level.LevelId) ? string.Empty : level.LevelId.Trim();
        }

        private static string Label(string levelId)
        {
            return string.IsNullOrEmpty(levelId) ? "This level" : "Level " + levelId;
        }

        private static string DescribeBubble(BubbleDefinition bubble, int index)
        {
            if (bubble == null || string.IsNullOrWhiteSpace(bubble.BubbleId))
            {
                return "Bubble at index " + index;
            }

            return "Bubble " + bubble.BubbleId;
        }

        private static LevelValidationIssue Error(
            string code,
            string message,
            string levelId,
            string bubbleId = null,
            int? slotId = null,
            FishType? fishType = null)
        {
            return new LevelValidationIssue(
                code,
                LevelValidationSeverity.Error,
                message,
                levelId,
                bubbleId,
                slotId,
                fishType);
        }

        private readonly struct Frame
        {
            public Frame(int slotId, int nextIndex)
            {
                SlotId = slotId;
                NextIndex = nextIndex;
            }

            public int SlotId { get; }

            public int NextIndex { get; }
        }
    }
}
