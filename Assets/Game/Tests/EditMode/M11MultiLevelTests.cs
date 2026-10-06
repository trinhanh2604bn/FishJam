using System.Collections.Generic;
using FishPuzzle.Bubbles;
using FishPuzzle.Core;
using FishPuzzle.Domain;
using FishPuzzle.Presentation;
using FishPuzzle.Progression;
using FishPuzzle.Tanks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FishPuzzle.Tests.EditMode
{
    /// <summary>
    /// M11 / M11.3: six onboarding levels (max 5 fish per bubble) in one Gameplay scene, Win/Lose flow, fixed 124 fish size.
    /// </summary>
    public sealed class M11MultiLevelTests
    {
        private const string ConfigPath = "Assets/Game/Data/Config/GameConfig.asset";
        private const string CatalogPath = "Assets/Game/Data/Config/LevelCatalog.asset";
        private const string FishCatalogPath = "Assets/Game/Data/Config/FishVisualCatalog.asset";
        private const string LayoutPath = "Assets/Game/Data/BubblePileLayouts/BPL_Standard_10.asset";

        private static readonly int[] Totals = { 9, 15, 21, 36, 60, 75 };
        private static readonly int[] BubbleCounts = { 3, 5, 6, 9, 12, 15 };
        private static readonly int[] TypeCounts = { 3, 5, 7, 9, 11, 11 };

        [TestCase(1, 9, 3)]
        [TestCase(2, 15, 5)]
        [TestCase(3, 21, 6)]
        [TestCase(4, 36, 9)]
        [TestCase(5, 60, 12)]
        [TestCase(6, 75, 15)]
        public void Level_IsValid_WithExactTotalAndBubbleCount(int number, int expectedTotal, int expectedBubbles)
        {
            var level = Level(number);
            var result = new LevelValidator().Validate(level, Config());

            Assert.That(result.IsValid, Is.True, Describe(result));
            Assert.That(result.Issues, Is.Empty);
            Assert.That(level.LevelId, Is.EqualTo("level_00" + number));
            Assert.That(level.InitialUnlockedTankCount, Is.EqualTo(2));
            Assert.That(level.TotalFishRequired, Is.EqualTo(expectedTotal));
            Assert.That(level.TargetGroupQueue.Count * Config().TankCapacity, Is.EqualTo(expectedTotal));
            Assert.That(CountFish(level), Is.EqualTo(expectedTotal));
            Assert.That(level.BubbleQueue.Count, Is.EqualTo(expectedBubbles));
            Assert.That(level.PileLayout, Is.SameAs(AssetDatabase.LoadAssetAtPath<BubblePileLayout>(LayoutPath)));
        }

        [Test]
        public void Progression_GetsDenserLevelByLevel()
        {
            var total = 0;
            for (var number = 1; number <= 6; number++)
            {
                var level = Level(number);
                total += level.TotalFishRequired;
                Assert.That(level.BubbleQueue.Count, Is.EqualTo(BubbleCounts[number - 1]));
                Assert.That(level.TotalFishRequired, Is.EqualTo(Totals[number - 1]));
                Assert.That(DistinctTypes(level), Is.EqualTo(TypeCounts[number - 1]));
                if (number > 1)
                {
                    var previous = Level(number - 1);
                    Assert.That(level.BubbleQueue.Count, Is.GreaterThan(previous.BubbleQueue.Count));
                    Assert.That(level.TotalFishRequired, Is.GreaterThan(previous.TotalFishRequired));
                    Assert.That(DistinctTypes(level), Is.GreaterThanOrEqualTo(DistinctTypes(previous)));
                }
            }

            Assert.That(total, Is.EqualTo(216));
        }

        [TestCase(1, 3, 3)]
        [TestCase(2, 3, 3)]
        [TestCase(3, 3, 4)]
        [TestCase(4, 4, 4)]
        [TestCase(5, 5, 5)]
        [TestCase(6, 5, 5)]
        public void Level_BubbleSizes_AndExactlyThreeDistinctTypes(int number, int minFish, int maxFish)
        {
            var level = Level(number);
            var ids = new HashSet<string>();
            for (var i = 0; i < level.BubbleQueue.Count; i++)
            {
                var bubble = level.BubbleQueue[i];
                Assert.That(bubble.Fishes.Count, Is.InRange(minFish, maxFish), bubble.BubbleId);
                Assert.That(bubble.Fishes.Count, Is.LessThanOrEqualTo(Config().MaxFishPerBubble), bubble.BubbleId);
                Assert.That(new HashSet<FishType>(bubble.Fishes).Count, Is.EqualTo(3), bubble.BubbleId);
                Assert.That(bubble.Modifier, Is.EqualTo(BubbleModifier.None), bubble.BubbleId);
                Assert.That(bubble.BubbleId, Is.EqualTo("L00" + number + "_B" + (i + 1).ToString("000")));
                Assert.That(ids.Add(bubble.BubbleId), Is.True);
                Assert.That(bubble.Fishes, Has.No.Member(FishType.Crab));
                Assert.That(bubble.Fishes, Has.No.Member(FishType.Snail));
            }
        }

        [Test]
        public void Level003_MixesThreeAndFourFishBubbles()
        {
            var level = Level(3);
            var threes = 0;
            var fours = 0;
            for (var i = 0; i < level.BubbleQueue.Count; i++)
            {
                threes += level.BubbleQueue[i].Fishes.Count == 3 ? 1 : 0;
                fours += level.BubbleQueue[i].Fishes.Count == 4 ? 1 : 0;
            }

            Assert.That(fours, Is.EqualTo(3));
            Assert.That(threes, Is.EqualTo(3));
        }

        [TestCase(5)]
        [TestCase(6)]
        public void FiveFishLevels_UseVariedCompositions(int number)
        {
            var level = Level(number);
            var twoTwoOne = 0;
            var threeOneOne = 0;
            var distinctBubbles = new HashSet<string>();
            for (var i = 0; i < level.BubbleQueue.Count; i++)
            {
                var counts = new Dictionary<FishType, int>();
                var key = string.Empty;
                for (var f = 0; f < level.BubbleQueue[i].Fishes.Count; f++)
                {
                    var type = level.BubbleQueue[i].Fishes[f];
                    counts.TryGetValue(type, out var c);
                    counts[type] = c + 1;
                    key += (int)type + ",";
                }

                var values = new List<int>(counts.Values);
                values.Sort();
                twoTwoOne += values.Count == 3 && values[0] == 1 && values[1] == 2 && values[2] == 2 ? 1 : 0;
                threeOneOne += values.Count == 3 && values[0] == 1 && values[1] == 1 && values[2] == 3 ? 1 : 0;
                distinctBubbles.Add(key);
            }

            Assert.That(twoTwoOne, Is.GreaterThanOrEqualTo(3));
            Assert.That(threeOneOne, Is.GreaterThanOrEqualTo(3));
            Assert.That(twoTwoOne + threeOneOne, Is.EqualTo(level.BubbleQueue.Count));
            Assert.That(distinctBubbles.Count, Is.EqualTo(level.BubbleQueue.Count), "No two bubbles are identical.");
        }

        [Test]
        public void LaterLevels_QueueBubblesBeyondTheVisiblePile()
        {
            var slots = Level(1).PileLayout.Slots.Count;
            Assert.That(slots, Is.EqualTo(10));
            Assert.That(Level(5).BubbleQueue.Count - slots, Is.EqualTo(2));
            Assert.That(Level(6).BubbleQueue.Count - slots, Is.EqualTo(5));
        }

        [Test]
        public void Validator_RejectsBubbleWithMoreThanFiveFish()
        {
            var config = Config();
            Assert.That(config.MaxFishPerBubble, Is.EqualTo(5));
            var level = ScriptableObject.CreateInstance<LevelData>();
            try
            {
                var serialized = new SerializedObject(level);
                serialized.FindProperty("_levelId").stringValue = "level_test_max";
                serialized.FindProperty("_initialUnlockedTankCount").intValue = 2;
                serialized.FindProperty("_totalFishRequired").intValue = 6;
                var queue = serialized.FindProperty("_targetGroupQueue");
                queue.arraySize = 2;
                queue.GetArrayElementAtIndex(0).intValue = (int)FishType.Orange;
                queue.GetArrayElementAtIndex(1).intValue = (int)FishType.Orange;
                var bubbles = serialized.FindProperty("_bubbleQueue");
                bubbles.arraySize = 1;
                var bubble = bubbles.GetArrayElementAtIndex(0);
                bubble.FindPropertyRelative("_bubbleId").stringValue = "LTEST_B001";
                var fishes = bubble.FindPropertyRelative("_fishes");
                var six = new[] { FishType.Orange, FishType.Orange, FishType.Orange, FishType.Orange, FishType.GreenStriped, FishType.RedClown };
                fishes.arraySize = six.Length;
                for (var i = 0; i < six.Length; i++)
                {
                    fishes.GetArrayElementAtIndex(i).intValue = (int)six[i];
                }

                serialized.FindProperty("_pileLayout").objectReferenceValue = AssetDatabase.LoadAssetAtPath<BubblePileLayout>(LayoutPath);
                serialized.ApplyModifiedPropertiesWithoutUndo();

                var result = new LevelValidator().Validate(level, config);
                Assert.That(result.IsValid, Is.False);
                var found = false;
                for (var i = 0; i < result.Issues.Count; i++)
                {
                    found |= result.Issues[i].Code == LevelValidationCodes.BubbleFishCountAboveMax;
                }

                Assert.That(found, Is.True, Describe(result));
            }
            finally
            {
                Object.DestroyImmediate(level);
            }
        }

        [Test]
        public void Validator_AcceptsExactlyFiveFish()
        {
            for (var number = 5; number <= 6; number++)
            {
                var result = new LevelValidator().Validate(Level(number), Config());
                Assert.That(result.IsValid, Is.True, Describe(result));
            }
        }

        [Test]
        public void Level001_MatchesAuthoredData()
        {
            var level = Level(1);
            AssertSequence(
                level.TargetGroupQueue,
                FishType.Orange, FishType.GreenStriped, FishType.RedClown);
            Assert.That(level.BubbleQueue.Count, Is.EqualTo(3));
            AssertSequence(level.BubbleQueue[0].Fishes, FishType.Orange, FishType.GreenStriped, FishType.RedClown);
            AssertSequence(level.BubbleQueue[1].Fishes, FishType.GreenStriped, FishType.RedClown, FishType.Orange);
            AssertSequence(level.BubbleQueue[2].Fishes, FishType.RedClown, FishType.Orange, FishType.GreenStriped);
        }

        [Test]
        public void Level001_Population_3Types()
        {
            AssertPopulation(Level(1), (FishType.Orange, 3), (FishType.GreenStriped, 3), (FishType.RedClown, 3));
        }

        [Test]
        public void Level002_MatchesAuthoredData()
        {
            var level = Level(2);
            AssertSequence(
                level.TargetGroupQueue,
                FishType.Orange, FishType.GreenStriped, FishType.RedClown, FishType.PinkStriped, FishType.BlackStriped);
            Assert.That(level.BubbleQueue.Count, Is.EqualTo(5));
            AssertSequence(level.BubbleQueue[0].Fishes, FishType.Orange, FishType.GreenStriped, FishType.RedClown);
            AssertSequence(level.BubbleQueue[1].Fishes, FishType.GreenStriped, FishType.RedClown, FishType.PinkStriped);
            AssertSequence(level.BubbleQueue[2].Fishes, FishType.RedClown, FishType.PinkStriped, FishType.BlackStriped);
            AssertSequence(level.BubbleQueue[3].Fishes, FishType.PinkStriped, FishType.BlackStriped, FishType.Orange);
            AssertSequence(level.BubbleQueue[4].Fishes, FishType.BlackStriped, FishType.Orange, FishType.GreenStriped);
        }

        [Test]
        public void Level002_Population_5Types()
        {
            AssertPopulation(Level(2), (FishType.Orange, 3), (FishType.GreenStriped, 3), (FishType.RedClown, 3), (FishType.PinkStriped, 3), (FishType.BlackStriped, 3));
        }

        [Test]
        public void Level003_MatchesAuthoredData()
        {
            var level = Level(3);
            AssertSequence(
                level.TargetGroupQueue,
                FishType.Orange, FishType.GreenStriped, FishType.RedClown, FishType.PinkStriped, FishType.BlackStriped, FishType.Yellow, FishType.GreySpotted);
            Assert.That(level.BubbleQueue.Count, Is.EqualTo(6));
            AssertSequence(level.BubbleQueue[0].Fishes, FishType.BlackStriped, FishType.BlackStriped, FishType.GreenStriped, FishType.RedClown);
            AssertSequence(level.BubbleQueue[1].Fishes, FishType.GreenStriped, FishType.GreenStriped, FishType.Orange, FishType.BlackStriped);
            AssertSequence(level.BubbleQueue[2].Fishes, FishType.PinkStriped, FishType.PinkStriped, FishType.RedClown, FishType.GreySpotted);
            AssertSequence(level.BubbleQueue[3].Fishes, FishType.PinkStriped, FishType.Yellow, FishType.GreySpotted);
            AssertSequence(level.BubbleQueue[4].Fishes, FishType.Orange, FishType.Yellow, FishType.GreySpotted);
            AssertSequence(level.BubbleQueue[5].Fishes, FishType.Orange, FishType.RedClown, FishType.Yellow);
        }

        [Test]
        public void Level003_Population_7Types()
        {
            AssertPopulation(Level(3), (FishType.Orange, 3), (FishType.GreenStriped, 3), (FishType.RedClown, 3), (FishType.PinkStriped, 3), (FishType.BlackStriped, 3), (FishType.Yellow, 3), (FishType.GreySpotted, 3));
        }

        [Test]
        public void Level004_MatchesAuthoredData()
        {
            var level = Level(4);
            AssertSequence(
                level.TargetGroupQueue,
                FishType.Orange, FishType.GreenStriped, FishType.RedClown, FishType.PinkStriped, FishType.BlackStriped, FishType.Yellow, FishType.GreySpotted, FishType.Pink, FishType.Blue, FishType.Orange, FishType.GreenStriped, FishType.RedClown);
            Assert.That(level.BubbleQueue.Count, Is.EqualTo(9));
            AssertSequence(level.BubbleQueue[0].Fishes, FishType.BlackStriped, FishType.BlackStriped, FishType.GreenStriped, FishType.Blue);
            AssertSequence(level.BubbleQueue[1].Fishes, FishType.RedClown, FishType.RedClown, FishType.GreenStriped, FishType.Pink);
            AssertSequence(level.BubbleQueue[2].Fishes, FishType.PinkStriped, FishType.PinkStriped, FishType.Orange, FishType.RedClown);
            AssertSequence(level.BubbleQueue[3].Fishes, FishType.Orange, FishType.Orange, FishType.GreenStriped, FishType.GreySpotted);
            AssertSequence(level.BubbleQueue[4].Fishes, FishType.GreenStriped, FishType.GreenStriped, FishType.RedClown, FishType.BlackStriped);
            AssertSequence(level.BubbleQueue[5].Fishes, FishType.Orange, FishType.Orange, FishType.GreenStriped, FishType.Yellow);
            AssertSequence(level.BubbleQueue[6].Fishes, FishType.Yellow, FishType.Yellow, FishType.PinkStriped, FishType.Pink);
            AssertSequence(level.BubbleQueue[7].Fishes, FishType.GreySpotted, FishType.GreySpotted, FishType.Orange, FishType.Blue);
            AssertSequence(level.BubbleQueue[8].Fishes, FishType.RedClown, FishType.RedClown, FishType.Pink, FishType.Blue);
        }

        [Test]
        public void Level004_Population_9Types()
        {
            AssertPopulation(Level(4), (FishType.Orange, 6), (FishType.GreenStriped, 6), (FishType.RedClown, 6), (FishType.PinkStriped, 3), (FishType.BlackStriped, 3), (FishType.Yellow, 3), (FishType.GreySpotted, 3), (FishType.Pink, 3), (FishType.Blue, 3));
        }

        [Test]
        public void Level005_MatchesAuthoredData()
        {
            var level = Level(5);
            AssertSequence(
                level.TargetGroupQueue,
                FishType.Orange, FishType.GreenStriped, FishType.RedClown, FishType.PinkStriped, FishType.BlackStriped, FishType.Yellow, FishType.GreySpotted, FishType.Pink, FishType.Blue, FishType.Koi, FishType.YellowBlack, FishType.Orange, FishType.GreenStriped, FishType.RedClown, FishType.PinkStriped, FishType.BlackStriped, FishType.Yellow, FishType.GreySpotted, FishType.Pink, FishType.Blue);
            Assert.That(level.BubbleQueue.Count, Is.EqualTo(12));
            AssertSequence(level.BubbleQueue[0].Fishes, FishType.GreenStriped, FishType.GreenStriped, FishType.Blue, FishType.Blue, FishType.YellowBlack);
            AssertSequence(level.BubbleQueue[1].Fishes, FishType.RedClown, FishType.RedClown, FishType.PinkStriped, FishType.PinkStriped, FishType.Orange);
            AssertSequence(level.BubbleQueue[2].Fishes, FishType.GreySpotted, FishType.GreySpotted, FishType.GreySpotted, FishType.PinkStriped, FishType.Blue);
            AssertSequence(level.BubbleQueue[3].Fishes, FishType.Orange, FishType.Orange, FishType.Pink, FishType.Pink, FishType.Koi);
            AssertSequence(level.BubbleQueue[4].Fishes, FishType.BlackStriped, FishType.BlackStriped, FishType.BlackStriped, FishType.Yellow, FishType.YellowBlack);
            AssertSequence(level.BubbleQueue[5].Fishes, FishType.Pink, FishType.Pink, FishType.Pink, FishType.Orange, FishType.GreySpotted);
            AssertSequence(level.BubbleQueue[6].Fishes, FishType.Blue, FishType.Blue, FishType.Koi, FishType.Koi, FishType.YellowBlack);
            AssertSequence(level.BubbleQueue[7].Fishes, FishType.PinkStriped, FishType.PinkStriped, FishType.PinkStriped, FishType.Yellow, FishType.Blue);
            AssertSequence(level.BubbleQueue[8].Fishes, FishType.GreenStriped, FishType.GreenStriped, FishType.Yellow, FishType.Yellow, FishType.BlackStriped);
            AssertSequence(level.BubbleQueue[9].Fishes, FishType.RedClown, FishType.RedClown, FishType.BlackStriped, FishType.BlackStriped, FishType.Pink);
            AssertSequence(level.BubbleQueue[10].Fishes, FishType.GreenStriped, FishType.GreenStriped, FishType.RedClown, FishType.RedClown, FishType.Yellow);
            AssertSequence(level.BubbleQueue[11].Fishes, FishType.Orange, FishType.Orange, FishType.GreySpotted, FishType.GreySpotted, FishType.Yellow);
        }

        [Test]
        public void Level005_Population_11Types()
        {
            AssertPopulation(Level(5), (FishType.Orange, 6), (FishType.GreenStriped, 6), (FishType.RedClown, 6), (FishType.PinkStriped, 6), (FishType.BlackStriped, 6), (FishType.Yellow, 6), (FishType.GreySpotted, 6), (FishType.Pink, 6), (FishType.Blue, 6), (FishType.Koi, 3), (FishType.YellowBlack, 3));
        }

        [Test]
        public void Level006_MatchesAuthoredData()
        {
            var level = Level(6);
            AssertSequence(
                level.TargetGroupQueue,
                FishType.Orange, FishType.GreenStriped, FishType.RedClown, FishType.PinkStriped, FishType.BlackStriped, FishType.Yellow, FishType.GreySpotted, FishType.Pink, FishType.Blue, FishType.Koi, FishType.YellowBlack, FishType.Orange, FishType.GreenStriped, FishType.RedClown, FishType.PinkStriped, FishType.BlackStriped, FishType.Yellow, FishType.GreySpotted, FishType.Pink, FishType.Blue, FishType.Koi, FishType.YellowBlack, FishType.Orange, FishType.GreenStriped, FishType.RedClown);
            Assert.That(level.BubbleQueue.Count, Is.EqualTo(15));
            AssertSequence(level.BubbleQueue[0].Fishes, FishType.GreySpotted, FishType.GreySpotted, FishType.YellowBlack, FishType.YellowBlack, FishType.BlackStriped);
            AssertSequence(level.BubbleQueue[1].Fishes, FishType.Orange, FishType.Orange, FishType.Yellow, FishType.Yellow, FishType.PinkStriped);
            AssertSequence(level.BubbleQueue[2].Fishes, FishType.RedClown, FishType.RedClown, FishType.Blue, FishType.Blue, FishType.Koi);
            AssertSequence(level.BubbleQueue[3].Fishes, FishType.Orange, FishType.Orange, FishType.BlackStriped, FishType.BlackStriped, FishType.RedClown);
            AssertSequence(level.BubbleQueue[4].Fishes, FishType.Orange, FishType.Orange, FishType.Pink, FishType.Pink, FishType.PinkStriped);
            AssertSequence(level.BubbleQueue[5].Fishes, FishType.Orange, FishType.Orange, FishType.Koi, FishType.Koi, FishType.RedClown);
            AssertSequence(level.BubbleQueue[6].Fishes, FishType.GreenStriped, FishType.GreenStriped, FishType.GreenStriped, FishType.BlackStriped, FishType.Yellow);
            AssertSequence(level.BubbleQueue[7].Fishes, FishType.GreenStriped, FishType.GreenStriped, FishType.PinkStriped, FishType.PinkStriped, FishType.YellowBlack);
            AssertSequence(level.BubbleQueue[8].Fishes, FishType.Pink, FishType.Pink, FishType.Blue, FishType.Blue, FishType.RedClown);
            AssertSequence(level.BubbleQueue[9].Fishes, FishType.PinkStriped, FishType.PinkStriped, FishType.Blue, FishType.Blue, FishType.Orange);
            AssertSequence(level.BubbleQueue[10].Fishes, FishType.GreenStriped, FishType.GreenStriped, FishType.Pink, FishType.Pink, FishType.BlackStriped);
            AssertSequence(level.BubbleQueue[11].Fishes, FishType.GreySpotted, FishType.GreySpotted, FishType.Koi, FishType.Koi, FishType.GreenStriped);
            AssertSequence(level.BubbleQueue[12].Fishes, FishType.Yellow, FishType.Yellow, FishType.Yellow, FishType.RedClown, FishType.GreySpotted);
            AssertSequence(level.BubbleQueue[13].Fishes, FishType.YellowBlack, FishType.YellowBlack, FishType.YellowBlack, FishType.GreenStriped, FishType.Koi);
            AssertSequence(level.BubbleQueue[14].Fishes, FishType.RedClown, FishType.RedClown, FishType.RedClown, FishType.BlackStriped, FishType.GreySpotted);
        }

        [Test]
        public void Level006_Population_11Types()
        {
            AssertPopulation(Level(6), (FishType.Orange, 9), (FishType.GreenStriped, 9), (FishType.RedClown, 9), (FishType.PinkStriped, 6), (FishType.BlackStriped, 6), (FishType.Yellow, 6), (FishType.GreySpotted, 6), (FishType.Pink, 6), (FishType.Blue, 6), (FishType.Koi, 6), (FishType.YellowBlack, 6));
        }

        [Test]
        public void FishVisualCatalog_HasSpriteForEveryTypeUsedByLevels()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<FishVisualCatalog>(FishCatalogPath);
            Assert.That(catalog, Is.Not.Null, FishCatalogPath);
            for (var number = 1; number <= 6; number++)
            {
                var level = Level(number);
                for (var i = 0; i < level.BubbleQueue.Count; i++)
                {
                    var fishes = level.BubbleQueue[i].Fishes;
                    for (var f = 0; f < fishes.Count; f++)
                    {
                        Assert.That(catalog.TryGetSprite(fishes[f], out _), Is.True, level.LevelId + " " + fishes[f]);
                    }
                }
            }
        }

        [Test]
        public void LevelCatalog_OrderIsLevel001To006()
        {
            var catalog = Catalog();
            Assert.That(catalog.Count, Is.EqualTo(6));
            for (var i = 0; i < 6; i++)
            {
                Assert.That(catalog.Get(i), Is.SameAs(Level(i + 1)), "Catalog index " + i);
                Assert.That(catalog.IndexOf(Level(i + 1)), Is.EqualTo(i));
            }

            Assert.That(catalog.Get(6), Is.Null);
            Assert.That(catalog.Get(-1), Is.Null);
        }

        [Test]
        public void LevelSequence_AdvancesInOrder_AndStopsAtLevel006()
        {
            var sequence = new LevelSequence(Catalog().Levels);
            Assert.That(sequence.Count, Is.EqualTo(6));
            Assert.That(sequence.Current.LevelId, Is.EqualTo("level_001"));
            for (var expected = 2; expected <= 6; expected++)
            {
                Assert.That(sequence.HasNext, Is.True);
                Assert.That(sequence.PeekNext().LevelId, Is.EqualTo("level_00" + expected));
                Assert.That(sequence.TryAdvance(), Is.True);
                Assert.That(sequence.CurrentNumber, Is.EqualTo(expected));
            }

            Assert.That(sequence.IsLast, Is.True);
            Assert.That(sequence.PeekNext(), Is.Null);
            Assert.That(sequence.TryAdvance(), Is.False, "Level_006 has no next level.");
            Assert.That(sequence.Current.LevelId, Is.EqualTo("level_006"));
            sequence.Restart();
            Assert.That(sequence.Current.LevelId, Is.EqualTo("level_001"));
        }

        [Test]
        public void WinThenNext_StartsNextLevelWithFreshAttempt()
        {
            var config = Config();
            var sequence = new LevelSequence(Catalog().Levels);
            var progression = new ProgressionRuntime(PlayerProgress.CreateDevelopmentDefaults());
            progression.BeginAttempt();
            var first = LevelSession.Start(sequence.Current, config, false);
            first.SetOutcomeHandler(state => progression.Settle(state, config));
            PlayToWin(first);
            Assert.That(first.State, Is.EqualTo(GameState.Win), Describe(first));

            Assert.That(sequence.TryAdvance(), Is.True);
            progression.BeginAttempt();
            var next = LevelSession.Start(sequence.Current, config, false);

            AssertFreshAttempt(next, sequence.Current);
            Assert.That(next.Progress.TotalFishRequired, Is.EqualTo(15));
            Assert.That(progression.Progress.Gold, Is.EqualTo(1020), "Next level keeps the won gold.");
        }

        [Test]
        public void Retry_ReloadsSameLevel_AndDeductsOneHeartOnly()
        {
            var config = Config();
            var level = Level(3);
            var progress = PlayerProgress.CreateDevelopmentDefaults();
            var progression = new ProgressionRuntime(progress);
            progression.BeginAttempt();
            var first = LevelSession.Start(level, config, false);
            first.SetOutcomeHandler(state => progression.Settle(state, config));

            SelectNonMatching(first, config.WaitingTrayFailCount);
            Assert.That(first.State, Is.EqualTo(GameState.Lose));
            Assert.That(progress.Lives, Is.EqualTo(4));
            Assert.That(progression.Settle(GameState.Lose, config), Is.False);

            progression.BeginAttempt();
            var retry = LevelSession.Start(level, config, false);
            retry.SetOutcomeHandler(state => progression.Settle(state, config));

            AssertFreshAttempt(retry, level);
            Assert.That(progress.Lives, Is.EqualTo(4), "Retry must not deduct another heart.");
            Assert.That(progress.Gold, Is.EqualTo(1000));
            Assert.That(progress.Score, Is.EqualTo(0));
        }

        [Test]
        public void LevelTransition_ResetsAttemptState()
        {
            var config = Config();
            var sequence = new LevelSequence(Catalog().Levels);
            var progress = PlayerProgress.CreateDevelopmentDefaults();
            var progression = new ProgressionRuntime(progress);
            progression.BeginAttempt();
            var first = LevelSession.Start(sequence.Current, config, false);
            first.SetOutcomeHandler(state => progression.Settle(state, config));
            Assert.That(first.TryUnlockWithGold(2, progress, progression.Wallet).Succeeded, Is.True);
            PlayToWin(first);
            Assert.That(first.State, Is.EqualTo(GameState.Win), Describe(first));

            sequence.TryAdvance();
            progression.BeginAttempt();
            var second = LevelSession.Start(sequence.Current, config, false);

            AssertFreshAttempt(second, sequence.Current);
            Assert.That(second.Tanks[2].State, Is.EqualTo(TankState.Locked), "Tank unlock scope is one level attempt.");
            Assert.That(progress.Gold, Is.EqualTo(420), "1000 - 600 unlock + 20 win, kept into the next level.");
        }

        [Test]
        public void GoldReward_IsAwardedExactlyOncePerWonLevel()
        {
            var config = Config();
            var progression = new ProgressionRuntime(PlayerProgress.CreateDevelopmentDefaults());
            progression.BeginAttempt();
            var session = LevelSession.Start(Level(2), config, false);
            session.SetOutcomeHandler(state => progression.Settle(state, config));
            PlayToWin(session);

            Assert.That(session.State, Is.EqualTo(GameState.Win), Describe(session));
            Assert.That(progression.Progress.Gold, Is.EqualTo(1020));
            Assert.That(progression.Settle(GameState.Win, config), Is.False);
            Assert.That(progression.Progress.Gold, Is.EqualTo(1020));
        }

        [Test]
        public void HeartDeduction_IsExactlyOncePerLostAttempt()
        {
            var config = Config();
            var progression = new ProgressionRuntime(PlayerProgress.CreateDevelopmentDefaults());
            progression.BeginAttempt();
            var session = LevelSession.Start(Level(6), config, false);
            session.SetOutcomeHandler(state => progression.Settle(state, config));
            SelectNonMatching(session, config.WaitingTrayFailCount);

            Assert.That(session.State, Is.EqualTo(GameState.Lose));
            Assert.That(progression.Progress.Lives, Is.EqualTo(4));
            Assert.That(progression.Settle(GameState.Lose, config), Is.False);
            Assert.That(progression.Progress.Lives, Is.EqualTo(4));
        }

        [Test]
        public void FullSequence_Level001ToLevel006_CanComplete()
        {
            var config = Config();
            var sequence = new LevelSequence(Catalog().Levels);
            var progression = new ProgressionRuntime(PlayerProgress.CreateDevelopmentDefaults());
            var taps = 0;
            for (var i = 0; i < 6; i++)
            {
                Assert.That(sequence.CurrentIndex, Is.EqualTo(i));
                progression.BeginAttempt();
                var session = LevelSession.Start(sequence.Current, config, false);
                session.SetOutcomeHandler(state => progression.Settle(state, config));
                AssertFreshAttempt(session, sequence.Current);

                taps += PlayToWin(session);

                Assert.That(session.State, Is.EqualTo(GameState.Win), sequence.Current.LevelId + " " + Describe(session));
                Assert.That(session.Progress.CollectedFishCount, Is.EqualTo(Totals[i]));
                Assert.That(session.Tray.Count, Is.EqualTo(0));
                Assert.That(session.PendingBubbleCount, Is.EqualTo(0));
                Assert.That(session.Targets.NextUnassignedIndex, Is.EqualTo(session.Targets.Count));
                Assert.That(progression.Progress.Gold, Is.EqualTo(1000 + (20 * (i + 1))));
                Assert.That(progression.Progress.Lives, Is.EqualTo(5));
                Assert.That(sequence.TryAdvance(), Is.EqualTo(i < 5));
            }

            Assert.That(sequence.Current.LevelId, Is.EqualTo("level_006"));
            Assert.That(taps, Is.EqualTo(216));
        }

        [Test]
        public void EveryLevel_IsWinnable_UnderSeveralDeterministicPlayOrders()
        {
            // Rules-only playability check through the real LevelSession and pile resolver.
            // Matching fish first (4 tie-break orders); when nothing matches, the fallback picks the
            // fish whose type is needed soonest. Every run must win without overflowing the tray.
            var config = Config();
            for (var number = 1; number <= 6; number++)
            {
                for (var variant = 0; variant < 4; variant++)
                {
                    var level = Level(number);
                    var session = LevelSession.Start(level, config, false);
                    var routing = new FishRoutingService();
                    var guard = 0;
                    while (session.State == GameState.PlayerInput && guard++ < 300)
                    {
                        var fish = PickMatching(session, routing, variant) ?? PickFallback(session, level);
                        Assert.That(fish, Is.Not.Null, level.LevelId + " variant " + variant + " " + Describe(session));
                        Assert.That(session.TrySelectFish(fish.Id, null).Accepted, Is.True);
                    }

                    Assert.That(session.State, Is.EqualTo(GameState.Win), level.LevelId + " variant " + variant + " " + Describe(session));
                }
            }
        }

        [Test]
        public void FishSize_IsFixed124_ForEveryOccupancy()
        {
            Assert.That(BubbleFishLayoutController.FishSize, Is.EqualTo(124f));
            Assert.That(BubbleFishLayoutController.MaxLayoutCount, Is.EqualTo(5));
            for (var count = 1; count <= 5; count++)
            {
                var positions = new HashSet<Vector2>();
                for (var i = 0; i < count; i++)
                {
                    Assert.That(BubbleFishLayoutController.TryGetPlacement(i, count, out var position, out var size), Is.True, count + "/" + i);
                    Assert.That(size, Is.EqualTo(124f), "Fish size must not depend on occupancy.");
                    Assert.That(positions.Add(position), Is.True, "Duplicate position for count " + count);
                    Assert.That(position.magnitude, Is.LessThanOrEqualTo(110f), "Fish center stays inside the bubble " + count + "/" + i);
                }

                Assert.That(BubbleFishLayoutController.TryGetPlacement(count, count, out _, out var outside), Is.False);
                Assert.That(outside, Is.EqualTo(124f));
            }

            Assert.That(BubbleFishLayoutController.TryGetPlacement(0, 6, out _, out var sixSize), Is.False, "No 6-fish formation exists.");
            Assert.That(sixSize, Is.EqualTo(124f));
        }

        [Test]
        public void FishLayout_SpreadsPositionsForDenseBubbles()
        {
            Assert.That(MinDistance(4), Is.GreaterThanOrEqualTo(80f));
            Assert.That(MinDistance(5), Is.GreaterThanOrEqualTo(68f));
        }

        [Test]
        public void TankFish_KeepBaseSize_AndOverlapWithOffsets()
        {
            Assert.That(TankSlotView.TankFishSize, Is.EqualTo(124f));
            Assert.That(TankSlotView.TankFishPosition(0, 1), Is.EqualTo(new Vector2(0f, 0f)));
            Assert.That(TankSlotView.TankFishPosition(0, 2), Is.EqualTo(new Vector2(-28f, 4f)));
            Assert.That(TankSlotView.TankFishPosition(1, 2), Is.EqualTo(new Vector2(28f, -4f)));
            Assert.That(TankSlotView.TankFishPosition(0, 3), Is.EqualTo(new Vector2(-40f, 7f)));
            Assert.That(TankSlotView.TankFishPosition(1, 3), Is.EqualTo(new Vector2(0f, -6f)));
            Assert.That(TankSlotView.TankFishPosition(2, 3), Is.EqualTo(new Vector2(40f, 7f)));
            Assert.That(
                Vector2.Distance(TankSlotView.TankFishPosition(0, 3), TankSlotView.TankFishPosition(2, 3)),
                Is.LessThan(TankSlotView.TankFishSize),
                "Tank fish overlap instead of shrinking into separate slots.");
        }

        [Test]
        public void BubbleLayout_IsDeterministic()
        {
            for (var count = 1; count <= 6; count++)
            {
                for (var i = 0; i < count; i++)
                {
                    BubbleFishLayoutController.TryGetPlacement(i, count, out var first, out var firstSize);
                    BubbleFishLayoutController.TryGetPlacement(i, count, out var second, out var secondSize);
                    Assert.That(second, Is.EqualTo(first));
                    Assert.That(secondSize, Is.EqualTo(firstSize));
                }
            }
        }

        private static float MinDistance(int count)
        {
            var min = float.MaxValue;
            for (var i = 0; i < count; i++)
            {
                for (var j = i + 1; j < count; j++)
                {
                    BubbleFishLayoutController.TryGetPosition(i, count, out var a);
                    BubbleFishLayoutController.TryGetPosition(j, count, out var b);
                    min = Mathf.Min(min, Vector2.Distance(a, b));
                }
            }

            return min;
        }

        private static void AssertFreshAttempt(LevelSession session, LevelData level)
        {
            Assert.That(session.State, Is.EqualTo(GameState.PlayerInput));
            Assert.That(session.Progress.CollectedFishCount, Is.EqualTo(0));
            Assert.That(session.Progress.TotalFishRequired, Is.EqualTo(level.TotalFishRequired));
            Assert.That(session.Tray.Count, Is.EqualTo(0));
            Assert.That(session.Targets.NextUnassignedIndex, Is.EqualTo(2));
            Assert.That(session.Tanks[0].CurrentTarget, Is.EqualTo(level.TargetGroupQueue[0]));
            Assert.That(session.Tanks[1].CurrentTarget, Is.EqualTo(level.TargetGroupQueue[1]));
            Assert.That(session.Tanks[0].FillCount, Is.EqualTo(0));
            Assert.That(session.Tanks[1].FillCount, Is.EqualTo(0));
            Assert.That(session.Tanks[2].IsUnlocked, Is.False);
            Assert.That(session.Tanks[3].IsUnlocked, Is.False);
            Assert.That(session.Bubbles.Count, Is.EqualTo(level.BubbleQueue.Count));
            var slots = level.PileLayout.Slots.Count;
            var pending = level.BubbleQueue.Count > slots ? level.BubbleQueue.Count - slots : 0;
            Assert.That(session.PendingBubbleCount, Is.EqualTo(pending));
        }

        private static int PlayToWin(LevelSession session)
        {
            var routing = new FishRoutingService();
            var taps = 0;
            var guard = 0;
            while (session.State == GameState.PlayerInput && guard < 300)
            {
                guard++;
                var fish = PickMatching(session, routing, 0) ?? PickFallback(session, FindLevel(session));
                Assert.That(fish, Is.Not.Null, "No selectable fish. " + Describe(session));
                var result = session.TrySelectFish(fish.Id, null);
                Assert.That(result.Accepted, Is.True, Describe(session));
                taps++;
            }

            return taps;
        }

        /// <summary>Matching fish chosen by a deterministic tie-break: 0 first, 1 last, 2 every other, 3 by type order.</summary>
        private static FishRuntimeState PickMatching(LevelSession session, FishRoutingService routing, int variant)
        {
            var candidates = new List<FishRuntimeState>();
            for (var i = 0; i < session.Bubbles.Count; i++)
            {
                var bubble = session.Bubbles[i];
                if (bubble == null || !bubble.IsInPlay || !bubble.IsInteractable)
                {
                    continue;
                }

                for (var f = 0; f < bubble.Fish.Count; f++)
                {
                    var candidate = bubble.Fish[f];
                    if (candidate != null
                        && candidate.State == FishState.Idle
                        && routing.SelectTank(candidate.Type, session.Tanks).Found)
                    {
                        candidates.Add(candidate);
                    }
                }
            }

            if (candidates.Count == 0)
            {
                return null;
            }

            switch (variant)
            {
                case 1:
                    return candidates[candidates.Count - 1];
                case 2:
                    return candidates[(session.Progress.CollectedFishCount / 3) % candidates.Count];
                case 3:
                    candidates.Sort((a, b) => ((int)b.Type).CompareTo((int)a.Type));
                    return candidates[0];
                default:
                    return candidates[0];
            }
        }

        /// <summary>No matching fish: pick the fish whose type is needed soonest, from the emptiest bubble.</summary>
        private static FishRuntimeState PickFallback(LevelSession session, LevelData level)
        {
            FishRuntimeState best = null;
            var bestSoon = int.MaxValue;
            var bestRemaining = int.MaxValue;
            for (var i = 0; i < session.Bubbles.Count; i++)
            {
                var bubble = session.Bubbles[i];
                if (bubble == null || !bubble.IsInPlay || !bubble.IsInteractable)
                {
                    continue;
                }

                for (var f = 0; f < bubble.Fish.Count; f++)
                {
                    var candidate = bubble.Fish[f];
                    if (candidate.State != FishState.Idle)
                    {
                        continue;
                    }

                    var soon = int.MaxValue / 2;
                    for (var q = session.Targets.NextUnassignedIndex; level != null && q < level.TargetGroupQueue.Count; q++)
                    {
                        if (level.TargetGroupQueue[q] == candidate.Type)
                        {
                            soon = q;
                            break;
                        }
                    }

                    if (best == null || soon < bestSoon || (soon == bestSoon && bubble.RemainingFishCount < bestRemaining))
                    {
                        best = candidate;
                        bestSoon = soon;
                        bestRemaining = bubble.RemainingFishCount;
                    }
                }
            }

            return best;
        }

        private static LevelData FindLevel(LevelSession session)
        {
            for (var number = 1; number <= 6; number++)
            {
                var level = Level(number);
                if (level.TotalFishRequired == session.Progress.TotalFishRequired && level.BubbleQueue.Count == session.Bubbles.Count)
                {
                    return level;
                }
            }

            return null;
        }

        private static void SelectNonMatching(LevelSession session, int count)
        {
            var routing = new FishRoutingService();
            for (var n = 0; n < count; n++)
            {
                FishRuntimeState pick = null;
                for (var i = 0; i < session.Bubbles.Count && pick == null; i++)
                {
                    var bubble = session.Bubbles[i];
                    if (bubble == null || !bubble.IsInPlay || !bubble.IsInteractable)
                    {
                        continue;
                    }

                    for (var f = 0; f < bubble.Fish.Count; f++)
                    {
                        var candidate = bubble.Fish[f];
                        if (candidate.State == FishState.Idle && !routing.SelectTank(candidate.Type, session.Tanks).Found)
                        {
                            pick = candidate;
                            break;
                        }
                    }
                }

                Assert.That(pick, Is.Not.Null, "No non-matching fish available. " + Describe(session));
                Assert.That(session.TrySelectFish(pick.Id, null).Accepted, Is.True);
            }
        }

        private static void AssertPopulation(LevelData level, params (FishType type, int count)[] expected)
        {
            var fishCounts = new Dictionary<FishType, int>();
            for (var i = 0; i < level.BubbleQueue.Count; i++)
            {
                var fishes = level.BubbleQueue[i].Fishes;
                for (var f = 0; f < fishes.Count; f++)
                {
                    fishCounts.TryGetValue(fishes[f], out var count);
                    fishCounts[fishes[f]] = count + 1;
                }
            }

            var groupCounts = new Dictionary<FishType, int>();
            for (var i = 0; i < level.TargetGroupQueue.Count; i++)
            {
                groupCounts.TryGetValue(level.TargetGroupQueue[i], out var count);
                groupCounts[level.TargetGroupQueue[i]] = count + 1;
            }

            Assert.That(fishCounts.Count, Is.EqualTo(expected.Length), level.LevelId + " FishType count");
            Assert.That(groupCounts.Count, Is.EqualTo(expected.Length), level.LevelId + " target FishType count");
            var tankCapacity = Config().TankCapacity;
            for (var i = 0; i < expected.Length; i++)
            {
                var type = expected[i].type;
                Assert.That(fishCounts.ContainsKey(type), Is.True, level.LevelId + " missing " + type);
                Assert.That(fishCounts[type], Is.EqualTo(expected[i].count), level.LevelId + " " + type);
                Assert.That(groupCounts[type] * tankCapacity, Is.EqualTo(fishCounts[type]), level.LevelId + " " + type + " = target occurrences x TankCapacity");
            }
        }

        private static void AssertSequence(IReadOnlyList<FishType> actual, params FishType[] expected)
        {
            Assert.That(actual.Count, Is.EqualTo(expected.Length));
            for (var i = 0; i < expected.Length; i++)
            {
                Assert.That(actual[i], Is.EqualTo(expected[i]), "Index " + i);
            }
        }

        private static int CountFish(LevelData level)
        {
            var total = 0;
            for (var i = 0; i < level.BubbleQueue.Count; i++)
            {
                total += level.BubbleQueue[i].Fishes.Count;
            }

            return total;
        }

        private static int DistinctTypes(LevelData level)
        {
            var types = new HashSet<FishType>();
            for (var i = 0; i < level.BubbleQueue.Count; i++)
            {
                types.UnionWith(level.BubbleQueue[i].Fishes);
            }

            return types.Count;
        }

        private static string Describe(LevelValidationResult result)
        {
            var text = string.Empty;
            for (var i = 0; i < result.Issues.Count; i++)
            {
                text += result.Issues[i] + "\n";
            }

            return text;
        }

        private static string Describe(LevelSession session)
        {
            return "state " + session.State
                + " progress " + session.Progress.CollectedFishCount + "/" + session.Progress.TotalFishRequired
                + " tray " + session.Tray.Count
                + " pending " + session.PendingBubbleCount;
        }

        private static LevelData Level(int number)
        {
            var path = "Assets/Game/Data/Levels/Level_00" + number + ".asset";
            var level = AssetDatabase.LoadAssetAtPath<LevelData>(path);
            Assert.That(level, Is.Not.Null, path);
            return level;
        }

        private static LevelCatalog Catalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null, CatalogPath);
            return catalog;
        }

        private static GameConfig Config()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            Assert.That(config, Is.Not.Null, ConfigPath);
            return config;
        }
    }
}
