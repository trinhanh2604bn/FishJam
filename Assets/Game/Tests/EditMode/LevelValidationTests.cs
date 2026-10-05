using System;
using System.Collections.Generic;
using FishPuzzle.Bubbles;
using FishPuzzle.Core;
using FishPuzzle.Domain;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FishPuzzle.Tests.EditMode
{
    public sealed class LevelValidationTests
    {
        private const string LevelPath = "Assets/Game/Data/Levels/Level_001.asset";
        private const string LayoutPath = "Assets/Game/Data/BubblePileLayouts/BPL_Standard_10.asset";
        private const string ConfigPath = "Assets/Game/Data/Config/GameConfig.asset";

        [Test]
        public void Level_001_IsValid()
        {
            var level = AssetDatabase.LoadAssetAtPath<LevelData>(LevelPath);
            var layout = AssetDatabase.LoadAssetAtPath<BubblePileLayout>(LayoutPath);
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);

            Assert.That(level, Is.Not.Null, "Missing " + LevelPath);
            Assert.That(layout, Is.Not.Null, "Missing " + LayoutPath);
            Assert.That(config, Is.Not.Null, "Missing " + ConfigPath);
            Assert.That(level.LevelId, Is.EqualTo("level_001"));
            Assert.That(level.InitialUnlockedTankCount, Is.EqualTo(2));
            Assert.That(level.TotalFishRequired, Is.EqualTo(36));
            Assert.That(level.PileLayout, Is.SameAs(layout));
            AssertSequence(
                level.TargetGroupQueue,
                FishType.Orange,
                FishType.GreenStriped,
                FishType.RedClown,
                FishType.PinkStriped,
                FishType.Orange,
                FishType.RedClown,
                FishType.GreenStriped,
                FishType.PinkStriped,
                FishType.RedClown,
                FishType.Orange,
                FishType.PinkStriped,
                FishType.GreenStriped);
            AssertBubble(level, 0, "L001_B001", FishType.GreenStriped, FishType.RedClown, FishType.PinkStriped);
            AssertBubble(level, 1, "L001_B002", FishType.Orange, FishType.RedClown, FishType.PinkStriped);
            AssertBubble(level, 2, "L001_B003", FishType.Orange, FishType.GreenStriped, FishType.PinkStriped);
            AssertBubble(level, 3, "L001_B004", FishType.Orange, FishType.GreenStriped, FishType.RedClown);
            AssertBubble(level, 4, "L001_B005", FishType.GreenStriped, FishType.RedClown, FishType.PinkStriped);
            AssertBubble(level, 5, "L001_B006", FishType.Orange, FishType.RedClown, FishType.PinkStriped);
            AssertBubble(level, 6, "L001_B007", FishType.Orange, FishType.GreenStriped, FishType.PinkStriped);
            AssertBubble(level, 7, "L001_B008", FishType.Orange, FishType.GreenStriped, FishType.RedClown);
            AssertBubble(level, 8, "L001_B009", FishType.GreenStriped, FishType.RedClown, FishType.PinkStriped);
            AssertBubble(level, 9, "L001_B010", FishType.Orange, FishType.RedClown, FishType.PinkStriped);
            AssertBubble(level, 10, "L001_B011", FishType.Orange, FishType.GreenStriped, FishType.PinkStriped);
            AssertBubble(level, 11, "L001_B012", FishType.Orange, FishType.GreenStriped, FishType.RedClown);
            AssertStandardPile(layout);

            var result = new LevelValidator().Validate(level, config);
            Assert.That(result.IsValid, Is.True, Describe(result));
            Assert.That(result.Issues, Is.Empty);
        }

        [Test]
        public void Level001_HasExpectedPopulation()
        {
            var level = AssetDatabase.LoadAssetAtPath<LevelData>(LevelPath);
            Assert.That(level, Is.Not.Null);
            Assert.That(level.TargetGroupQueue, Is.Not.Null);
            Assert.That(level.TargetGroupQueue.Count, Is.EqualTo(12));
            Assert.That(level.BubbleQueue, Is.Not.Null);

            var counts = new Dictionary<FishType, int>();
            var total = 0;
            for (var i = 0; i < level.BubbleQueue.Count; i++)
            {
                var bubble = level.BubbleQueue[i];
                Assert.That(bubble, Is.Not.Null);
                Assert.That(bubble.Fishes, Is.Not.Null);
                for (var fishIndex = 0; fishIndex < bubble.Fishes.Count; fishIndex++)
                {
                    var fish = bubble.Fishes[fishIndex];
                    counts.TryGetValue(fish, out var count);
                    counts[fish] = count + 1;
                    total++;
                }
            }

            Assert.That(counts[FishType.Orange], Is.EqualTo(9));
            Assert.That(counts[FishType.GreenStriped], Is.EqualTo(9));
            Assert.That(counts[FishType.RedClown], Is.EqualTo(9));
            Assert.That(counts[FishType.PinkStriped], Is.EqualTo(9));
            Assert.That(total, Is.EqualTo(36));
            Assert.That(level.TotalFishRequired, Is.EqualTo(36));

            var fishTypes = (FishType[])Enum.GetValues(typeof(FishType));
            for (var i = 0; i < fishTypes.Length; i++)
            {
                var fish = fishTypes[i];
                if (fish == FishType.Orange || fish == FishType.GreenStriped || fish == FishType.RedClown || fish == FishType.PinkStriped)
                {
                    continue;
                }

                counts.TryGetValue(fish, out var count);
                Assert.That(count, Is.EqualTo(0), fish + " should not appear in Level_001.");
            }
        }

        [Test]
        public void EmptyTargetQueue_IsRejected()
        {
            WithFixture("level_empty_targets", fixture => fixture.ClearTargets(), result =>
            {
                var issue = RequireIssue(result, LevelValidationCodes.TargetQueueEmpty);
                Assert.That(issue.Severity, Is.EqualTo(LevelValidationSeverity.Error));
                Assert.That(issue.Message, Does.Contain("targetGroupQueue is empty"));
            });
        }

        [Test]
        public void IncorrectTotalFishRequired_IsRejected()
        {
            WithFixture("level_bad_total", fixture => fixture.SetTotal(8), result =>
            {
                var issue = RequireIssue(result, LevelValidationCodes.TotalFishRequiredMismatch);
                Assert.That(
                    issue.Message,
                    Is.EqualTo("Level level_bad_total totalFishRequired is 8, but targetGroupQueue has 3 groups and tank capacity is 3, so the required total is 9."));
            });
        }

        [Test]
        public void MissingFishForTarget_IsRejected()
        {
            WithFixture("level_missing_fish", fixture =>
            {
                fixture.AddTarget(FishType.Orange);
                fixture.SetTotal(12);
            }, result =>
            {
                var issue = RequireIssue(result, LevelValidationCodes.FishCountMismatch, FishType.Orange);
                Assert.That(
                    issue.Message,
                    Is.EqualTo("Level level_missing_fish requires 6 Orange fish from target groups but bubble population contains 3."));
            });
        }

        [Test]
        public void ExtraFishNotRequiredByTarget_IsRejected()
        {
            WithFixture("level_extra_fish", fixture =>
            {
                fixture.SetBubbleFish(0, FishType.PinkStriped, FishType.GreenStriped, FishType.RedClown);
            }, result =>
            {
                var issue = RequireIssue(result, LevelValidationCodes.FishCountMismatch, FishType.PinkStriped);
                Assert.That(
                    issue.Message,
                    Is.EqualTo("Level level_extra_fish requires 0 PinkStriped fish from target groups but bubble population contains 1."));
            });
        }

        [Test]
        public void BubbleWithTwoDistinctTypes_IsRejected()
        {
            WithFixture("level_two_types", fixture =>
            {
                fixture.SetBubbleFish(0, FishType.Orange, FishType.Orange, FishType.GreenStriped);
            }, result =>
            {
                var issue = RequireIssue(result, LevelValidationCodes.BubbleDistinctTypeCount);
                Assert.That(issue.BubbleId, Is.EqualTo("LTEST_B001"));
                Assert.That(issue.Message, Is.EqualTo("Bubble LTEST_B001 contains 2 distinct FishTypes; expected exactly 3."));
            });
        }

        [Test]
        public void DuplicateBubbleId_IsRejected()
        {
            WithFixture("level_duplicate_bubble", fixture => fixture.SetBubbleId(1, "LTEST_B001"), result =>
            {
                var issue = RequireIssue(result, LevelValidationCodes.BubbleIdDuplicate);
                Assert.That(issue.BubbleId, Is.EqualTo("LTEST_B001"));
                Assert.That(issue.Message, Does.Contain("used 2 times"));
            });
        }

        [Test]
        public void DuplicatePileSlotId_IsRejected()
        {
            WithFixture("level_duplicate_slot", fixture =>
            {
                fixture.SetLayout(
                    new SlotSpec(0, 0, true),
                    new SlotSpec(0, 1, false));
            }, result =>
            {
                var issue = RequireIssue(result, LevelValidationCodes.PileDuplicateSlotId);
                Assert.That(issue.SlotId, Is.EqualTo(0));
            });
        }

        [Test]
        public void MissingDownCandidateSlot_IsRejected()
        {
            WithFixture("level_missing_candidate", fixture =>
            {
                fixture.SetLayout(
                    new SlotSpec(0, 0, true),
                    new SlotSpec(1, 1, false, 99));
            }, result =>
            {
                var issue = RequireIssue(result, LevelValidationCodes.PileDownCandidateMissing);
                Assert.That(issue.SlotId, Is.EqualTo(1));
                Assert.That(issue.Message, Does.Contain("down candidate 99"));
            });
        }

        [Test]
        public void SelfReferencingPileSlot_IsRejected()
        {
            WithFixture("level_self_slot", fixture =>
            {
                fixture.SetLayout(new SlotSpec(4, 1, true, 4));
            }, result =>
            {
                var issue = RequireIssue(result, LevelValidationCodes.PileSelfReference);
                Assert.That(issue.SlotId, Is.EqualTo(4));
                Assert.That(HasCode(result, LevelValidationCodes.PileGravityCycle), Is.False, Describe(result));
            });
        }

        [Test]
        public void UpwardGravityCandidate_IsRejected()
        {
            WithFixture("level_upward", fixture =>
            {
                fixture.SetLayout(
                    new SlotSpec(0, 1, true),
                    new SlotSpec(1, 0, false, 0));
            }, result =>
            {
                var issue = RequireIssue(result, LevelValidationCodes.PileUpwardGravity);
                Assert.That(issue.SlotId, Is.EqualTo(1));
                Assert.That(issue.Message, Does.Contain("not strictly below"));
                Assert.That(HasCode(result, LevelValidationCodes.PileGravityCycle), Is.False, Describe(result));
            });
        }

        [Test]
        public void NoTopSpawnSlot_IsRejected()
        {
            WithFixture("level_no_spawn", fixture =>
            {
                fixture.SetLayout(new SlotSpec(0, 0, false));
            }, result =>
            {
                RequireIssue(result, LevelValidationCodes.PileNoTopSpawn);
            });
        }

        [Test]
        public void DuplicateDownCandidate_IsRejected()
        {
            WithFixture("level_duplicate_candidate", fixture =>
            {
                fixture.SetLayout(
                    new SlotSpec(0, 0, true),
                    new SlotSpec(1, 1, false, 0, 0));
            }, result =>
            {
                var issue = RequireIssue(result, LevelValidationCodes.PileDuplicateDownCandidate);
                Assert.That(issue.SlotId, Is.EqualTo(1));
                Assert.That(issue.Message, Does.Contain("down candidate 0 more than once"));
            });
        }

        [Test]
        public void InitialUnlockedTankCountAboveMax_IsRejected()
        {
            WithFixture("level_too_many_tanks", fixture => fixture.SetUnlocked(5), result =>
            {
                var issue = RequireIssue(result, LevelValidationCodes.UnlockedTankCountAboveMax);
                Assert.That(
                    issue.Message,
                    Is.EqualTo("Level level_too_many_tanks initialUnlockedTankCount is 5, which is above GameConfig.MaxTankSlots (4)."));
            });
        }

        [Test]
        public void GravityCycle_IsRejected()
        {
            WithFixture("level_cycle", fixture =>
            {
                fixture.SetLayout(
                    new SlotSpec(0, 0, true, 1),
                    new SlotSpec(1, 0, false, 0));
            }, result =>
            {
                var issue = RequireIssue(result, LevelValidationCodes.PileGravityCycle);
                Assert.That(issue.Message, Does.Contain("0 -> 1 -> 0"));
            });
        }

        private static void WithFixture(string levelId, Action<Fixture> arrange, Action<LevelValidationResult> assert)
        {
            var fixture = Fixture.Create(levelId);
            try
            {
                arrange(fixture);
                var result = new LevelValidator().Validate(fixture.Level, fixture.Config);
                assert(result);
            }
            finally
            {
                fixture.Dispose();
            }
        }

        private static void AssertBubble(LevelData level, int index, string bubbleId, params FishType[] fish)
        {
            Assert.That(level.BubbleQueue, Is.Not.Null);
            Assert.That(index, Is.LessThan(level.BubbleQueue.Count));
            var bubble = level.BubbleQueue[index];
            Assert.That(bubble, Is.Not.Null);
            Assert.That(bubble.BubbleId, Is.EqualTo(bubbleId));
            Assert.That(bubble.Modifier, Is.EqualTo(BubbleModifier.None));
            AssertSequence(bubble.Fishes, fish);
        }

        private static void AssertStandardPile(BubblePileLayout layout)
        {
            Assert.That(layout.Slots, Is.Not.Null);
            Assert.That(layout.Slots.Count, Is.EqualTo(10));
            AssertDown(layout, 0);
            AssertDown(layout, 1);
            AssertDown(layout, 2, 0);
            AssertDown(layout, 3, 0, 1);
            AssertDown(layout, 4, 1);
            AssertDown(layout, 5, 2, 3);
            AssertDown(layout, 6, 3, 4);
            AssertDown(layout, 7, 5);
            AssertDown(layout, 8, 5, 6);
            AssertDown(layout, 9, 6);
            Assert.That(RequireSlot(layout, 0).Row, Is.EqualTo(0));
            Assert.That(RequireSlot(layout, 2).Row, Is.EqualTo(1));
            Assert.That(RequireSlot(layout, 5).Row, Is.EqualTo(2));
            Assert.That(RequireSlot(layout, 7).Row, Is.EqualTo(3));
            for (var slotId = 0; slotId <= 9; slotId++)
            {
                var slot = RequireSlot(layout, slotId);
                Assert.That(slot.CanReceiveSpawnFromTop, Is.EqualTo(slotId >= 7), "slot " + slotId);
            }
        }

        private static void AssertDown(BubblePileLayout layout, int slotId, params int[] expected)
        {
            AssertSequence(RequireSlot(layout, slotId).DownCandidateSlotIds, expected);
        }

        private static BubblePileSlotDefinition RequireSlot(BubblePileLayout layout, int slotId)
        {
            for (var i = 0; i < layout.Slots.Count; i++)
            {
                var slot = layout.Slots[i];
                if (slot != null && slot.SlotId == slotId)
                {
                    return slot;
                }
            }

            Assert.Fail("Missing pile slot " + slotId);
            return null;
        }

        private static void AssertSequence<T>(IReadOnlyList<T> actual, params T[] expected)
        {
            Assert.That(actual, Is.Not.Null);
            Assert.That(actual.Count, Is.EqualTo(expected.Length));
            for (var i = 0; i < expected.Length; i++)
            {
                Assert.That(actual[i], Is.EqualTo(expected[i]));
            }
        }

        private static LevelValidationIssue RequireIssue(LevelValidationResult result, string code)
        {
            Assert.That(result.IsValid, Is.False, Describe(result));
            for (var i = 0; i < result.Issues.Count; i++)
            {
                if (result.Issues[i].Code == code)
                {
                    return result.Issues[i];
                }
            }

            Assert.Fail("Missing issue " + code + "\n" + Describe(result));
            return null;
        }

        private static LevelValidationIssue RequireIssue(LevelValidationResult result, string code, FishType fishType)
        {
            Assert.That(result.IsValid, Is.False, Describe(result));
            for (var i = 0; i < result.Issues.Count; i++)
            {
                var issue = result.Issues[i];
                if (issue.Code == code && issue.FishType == fishType)
                {
                    return issue;
                }
            }

            Assert.Fail("Missing issue " + code + " for " + fishType + "\n" + Describe(result));
            return null;
        }

        private static bool HasCode(LevelValidationResult result, string code)
        {
            for (var i = 0; i < result.Issues.Count; i++)
            {
                if (result.Issues[i].Code == code)
                {
                    return true;
                }
            }

            return false;
        }

        private static string Describe(LevelValidationResult result)
        {
            if (result.Issues.Count == 0)
            {
                return "No validation issues.";
            }

            var lines = new string[result.Issues.Count];
            for (var i = 0; i < result.Issues.Count; i++)
            {
                lines[i] = result.Issues[i].ToString();
            }

            return string.Join("\n", lines);
        }

        private readonly struct SlotSpec
        {
            public SlotSpec(int id, int row, bool topSpawn, params int[] down)
            {
                Id = id;
                Row = row;
                TopSpawn = topSpawn;
                Down = down ?? Array.Empty<int>();
            }

            public int Id { get; }

            public int Row { get; }

            public bool TopSpawn { get; }

            public int[] Down { get; }
        }

        private sealed class Fixture : IDisposable
        {
            private Fixture(LevelData level, BubblePileLayout layout, GameConfig config)
            {
                Level = level;
                Layout = layout;
                Config = config;
            }

            public LevelData Level { get; }

            public BubblePileLayout Layout { get; private set; }

            public GameConfig Config { get; }

            public static Fixture Create(string levelId)
            {
                var config = ScriptableObject.CreateInstance<GameConfig>();
                var layout = ScriptableObject.CreateInstance<BubblePileLayout>();
                var level = ScriptableObject.CreateInstance<LevelData>();
                var fixture = new Fixture(level, layout, config);
                fixture.SetLayout(new SlotSpec(0, 0, true));

                var serialized = new SerializedObject(level);
                serialized.FindProperty("_levelId").stringValue = levelId;
                serialized.FindProperty("_initialUnlockedTankCount").intValue = 2;
                serialized.FindProperty("_totalFishRequired").intValue = 9;
                serialized.FindProperty("_pileLayout").objectReferenceValue = fixture.Layout;

                var targets = serialized.FindProperty("_targetGroupQueue");
                targets.arraySize = 3;
                targets.GetArrayElementAtIndex(0).intValue = (int)FishType.Orange;
                targets.GetArrayElementAtIndex(1).intValue = (int)FishType.GreenStriped;
                targets.GetArrayElementAtIndex(2).intValue = (int)FishType.RedClown;

                var bubbles = serialized.FindProperty("_bubbleQueue");
                bubbles.arraySize = 3;
                WriteBubble(bubbles.GetArrayElementAtIndex(0), "LTEST_B001");
                WriteBubble(bubbles.GetArrayElementAtIndex(1), "LTEST_B002");
                WriteBubble(bubbles.GetArrayElementAtIndex(2), "LTEST_B003");
                serialized.ApplyModifiedPropertiesWithoutUndo();
                return fixture;
            }

            public void ClearTargets()
            {
                var serialized = new SerializedObject(Level);
                serialized.FindProperty("_targetGroupQueue").arraySize = 0;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            public void AddTarget(FishType fishType)
            {
                var serialized = new SerializedObject(Level);
                var targets = serialized.FindProperty("_targetGroupQueue");
                var index = targets.arraySize;
                targets.arraySize = index + 1;
                targets.GetArrayElementAtIndex(index).intValue = (int)fishType;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            public void SetTotal(int totalFishRequired)
            {
                var serialized = new SerializedObject(Level);
                serialized.FindProperty("_totalFishRequired").intValue = totalFishRequired;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            public void SetUnlocked(int count)
            {
                var serialized = new SerializedObject(Level);
                serialized.FindProperty("_initialUnlockedTankCount").intValue = count;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            public void SetBubbleId(int index, string bubbleId)
            {
                var serialized = new SerializedObject(Level);
                var bubble = serialized.FindProperty("_bubbleQueue").GetArrayElementAtIndex(index);
                bubble.FindPropertyRelative("_bubbleId").stringValue = bubbleId;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            public void SetBubbleFish(int index, params FishType[] fish)
            {
                var serialized = new SerializedObject(Level);
                var fishes = serialized.FindProperty("_bubbleQueue").GetArrayElementAtIndex(index).FindPropertyRelative("_fishes");
                fishes.arraySize = fish.Length;
                for (var i = 0; i < fish.Length; i++)
                {
                    fishes.GetArrayElementAtIndex(i).intValue = (int)fish[i];
                }

                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            public void SetLayout(params SlotSpec[] slots)
            {
                var layout = ScriptableObject.CreateInstance<BubblePileLayout>();
                var layoutObject = new SerializedObject(layout);
                var array = layoutObject.FindProperty("_slots");
                array.arraySize = slots.Length;
                for (var i = 0; i < slots.Length; i++)
                {
                    var element = array.GetArrayElementAtIndex(i);
                    element.FindPropertyRelative("_slotId").intValue = slots[i].Id;
                    element.FindPropertyRelative("_anchoredPosition").vector2Value = Vector2.zero;
                    element.FindPropertyRelative("_row").intValue = slots[i].Row;
                    element.FindPropertyRelative("_column").intValue = 0;
                    element.FindPropertyRelative("_sortingOrder").intValue = 0;
                    element.FindPropertyRelative("_canReceiveSpawnFromTop").boolValue = slots[i].TopSpawn;
                    var downs = element.FindPropertyRelative("_downCandidateSlotIds");
                    var down = slots[i].Down ?? Array.Empty<int>();
                    downs.arraySize = down.Length;
                    for (var downIndex = 0; downIndex < down.Length; downIndex++)
                    {
                        downs.GetArrayElementAtIndex(downIndex).intValue = down[downIndex];
                    }
                }

                layoutObject.ApplyModifiedPropertiesWithoutUndo();

                var levelObject = new SerializedObject(Level);
                levelObject.FindProperty("_pileLayout").objectReferenceValue = layout;
                levelObject.ApplyModifiedPropertiesWithoutUndo();

                if (Layout != null && !ReferenceEquals(Layout, layout))
                {
                    UnityEngine.Object.DestroyImmediate(Layout);
                }

                Layout = layout;
            }

            public void Dispose()
            {
                if (Level != null)
                {
                    UnityEngine.Object.DestroyImmediate(Level);
                }

                if (Layout != null)
                {
                    UnityEngine.Object.DestroyImmediate(Layout);
                }

                if (Config != null)
                {
                    UnityEngine.Object.DestroyImmediate(Config);
                }
            }

            private static void WriteBubble(SerializedProperty bubble, string bubbleId)
            {
                bubble.FindPropertyRelative("_bubbleId").stringValue = bubbleId;
                bubble.FindPropertyRelative("_modifier").intValue = (int)BubbleModifier.None;
                var fishes = bubble.FindPropertyRelative("_fishes");
                fishes.arraySize = 3;
                fishes.GetArrayElementAtIndex(0).intValue = (int)FishType.Orange;
                fishes.GetArrayElementAtIndex(1).intValue = (int)FishType.GreenStriped;
                fishes.GetArrayElementAtIndex(2).intValue = (int)FishType.RedClown;
            }
        }
    }
}
