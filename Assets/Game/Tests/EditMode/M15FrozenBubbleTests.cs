using System.Collections.Generic;
using FishPuzzle.Bubbles;
using FishPuzzle.Core;
using FishPuzzle.Domain;
using FishPuzzle.Presentation;
using FishPuzzle.Tanks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FishPuzzle.Tests.EditMode
{
    /// <summary>
    /// M15: mixed 2-5 fish bubbles, bubble shell scale, Frozen Bubble rules, and Level 6-10 authoring.
    /// </summary>
    public sealed class M15FrozenBubbleTests
    {
        private const string ConfigPath = "Assets/Game/Data/Config/GameConfig.asset";
        private const string LayoutPath = "Assets/Game/Data/BubblePileLayouts/BPL_Standard_10.asset";
        private const string BubblePrefabPath = "Assets/Game/Prefabs/Bubbles/PF_Bubble.prefab";
        private const string FishPrefabPath = "Assets/Game/Prefabs/Fish/PF_Fish.prefab";
        private const string FishCatalogPath = "Assets/Game/Data/Config/FishVisualCatalog.asset";
        private const string ArtCatalogPath = "Assets/Game/Data/Config/GameplayArtCatalog.asset";

        private const FishType O = FishType.Orange;
        private const FishType G = FishType.GreenStriped;
        private const FishType R = FishType.RedClown;
        private const FishType P = FishType.PinkStriped;
        private const FishType K = FishType.BlackStriped;
        private const FishType Y = FishType.Yellow;

        private static readonly FishType[] NormalTypes =
        {
            FishType.Orange, FishType.GreenStriped, FishType.RedClown, FishType.PinkStriped, FishType.BlackStriped, FishType.Yellow,
            FishType.GreySpotted, FishType.Pink, FishType.Koi, FishType.Blue, FishType.YellowBlack
        };

        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (var i = 0; i < _created.Count; i++)
            {
                if (_created[i] != null)
                {
                    Object.DestroyImmediate(_created[i]);
                }
            }

            _created.Clear();
        }

        // ---------- Validation: fish counts ----------

        [Test]
        public void MaxFishPerBubble_IsFive_AndSixFishAreRejected()
        {
            Assert.That(Config().MaxFishPerBubble, Is.EqualTo(5));
            var level = Build("level_m15_six", new[] { O, O },
                Bubble(O, O, O, O, G, R).Raw(),
                Bubble(G, R).Raw());
            AssertHasIssue(level, LevelValidationCodes.BubbleFishCountAboveMax);
        }

        [Test]
        public void MixedTwoToFiveFishBubbles_AreValid()
        {
            var level = MixedLevel(0, -1);
            var result = Validate(level);
            Assert.That(result.IsValid, Is.True, Describe(result));
            var sizes = new HashSet<int>();
            for (var i = 0; i < level.BubbleQueue.Count; i++)
            {
                sizes.Add(level.BubbleQueue[i].Fishes.Count);
            }

            Assert.That(sizes, Is.EquivalentTo(new[] { 2, 3, 4, 5 }));
        }

        [Test]
        public void OneFishBubble_IsRejected()
        {
            var level = Build("level_m15_one", new[] { O },
                Bubble(O).Raw(),
                Bubble(O, O).Raw());
            AssertHasIssue(level, LevelValidationCodes.BubbleFishCountBelowMin);
        }

        [Test]
        public void TwoFishBubble_NeedsTwoDistinctTypes()
        {
            var level = Build("level_m15_pair", new[] { O, G },
                Bubble(O, O).Raw(),
                Bubble(O, G, G).Raw(),
                Bubble(G).Raw());
            var issue = AssertHasIssue(level, LevelValidationCodes.BubbleDistinctTypeCount);
            Assert.That(issue.Message, Does.Contain("expected exactly 2"));
        }

        // ---------- Validation: frozen data ----------

        [Test]
        public void FrozenBubble_WithZeroRequirement_IsRejected()
        {
            var level = MixedLevel(0, 1, BubbleModifier.Frozen);
            AssertHasIssue(level, LevelValidationCodes.BubbleFrozenRequirementInvalid);
        }

        [Test]
        public void NormalBubble_WithIceRequirement_IsRejected()
        {
            var level = MixedLevel(3, 1, BubbleModifier.None);
            AssertHasIssue(level, LevelValidationCodes.BubbleIceOnNormalBubble);
        }

        [Test]
        public void FrozenBubble_WithEnoughAdjacentFish_IsValid()
        {
            // Slot 1 touches slots 0, 3 and 4: 2 + 5 + 4 = 11 adjacent normal fish.
            var level = MixedLevel(3, 1);
            var result = Validate(level);
            Assert.That(result.IsValid, Is.True, Describe(result));
            Assert.That(level.BubbleQueue[1].IsFrozen, Is.True);
            Assert.That(level.BubbleQueue[1].IceBreakRequiredSelections, Is.EqualTo(3));
        }

        [Test]
        public void FrozenBubble_WithoutEnoughAdjacentFish_IsRejected()
        {
            var level = MixedLevel(12, 1);
            var issue = AssertHasIssue(level, LevelValidationCodes.FrozenBubbleUnreachable);
            Assert.That(issue.BubbleId, Is.EqualTo("LM15_B002"));
            Assert.That(issue.Message, Does.Contain("11 fish"));
        }

        [Test]
        public void Validator_RejectsImpossibleTargetPopulation()
        {
            var level = Build("level_m15_population", new[] { O, G },
                Bubble(O, G, R).Raw(),
                Bubble(O, G, R).Raw());
            AssertHasIssue(level, LevelValidationCodes.FishCountMismatch);
        }

        // ---------- Presentation: bubble shell scale ----------

        [Test]
        public void BubbleVisualScale_MapsFishCount()
        {
            Assert.That(BubbleView.VisualScaleForFishCount(2), Is.EqualTo(0.88f));
            Assert.That(BubbleView.VisualScaleForFishCount(3), Is.EqualTo(0.93f));
            Assert.That(BubbleView.VisualScaleForFishCount(4), Is.EqualTo(0.98f));
            Assert.That(BubbleView.VisualScaleForFishCount(5), Is.EqualTo(1.03f));
            Assert.That(BubbleView.VisualScaleForFishCount(1), Is.EqualTo(0.88f));
            Assert.That(BubbleView.VisualScaleForFishCount(6), Is.EqualTo(1.03f));
            for (var count = 3; count <= 5; count++)
            {
                Assert.That(BubbleView.VisualScaleForFishCount(count), Is.GreaterThan(BubbleView.VisualScaleForFishCount(count - 1)));
            }

            Assert.That(BubbleFishLayoutController.FishSize, Is.EqualTo(124f), "Fish size never follows bubble size.");
        }

        [TestCase(2, 0.88f)]
        [TestCase(3, 0.93f)]
        [TestCase(4, 0.98f)]
        [TestCase(5, 1.03f)]
        public void BoundBubble_ScalesShellButNotFish(int fishCount, float expectedScale)
        {
            var definition = FirstBubbleWithFishCount(Level(10), fishCount);
            var view = BindBubble(definition);

            Assert.That(view.VisualScale, Is.EqualTo(expectedScale));
            Assert.That(view.transform.Find("BubbleBack").localScale.x, Is.EqualTo(expectedScale).Within(0.0001f));
            Assert.That(view.transform.Find("BubbleFront").localScale.x, Is.EqualTo(expectedScale).Within(0.0001f));
            Assert.That(view.transform.Find("ModifierContainer").localScale.x, Is.EqualTo(expectedScale).Within(0.0001f));
            Assert.That(view.transform.Find("FishContainer").localScale, Is.EqualTo(Vector3.one));
            Assert.That(view.FishViews.Count, Is.EqualTo(definition.Fishes.Count));
            for (var i = 0; i < view.FishViews.Count; i++)
            {
                var rect = (RectTransform)view.FishViews[i].transform;
                Assert.That(rect.sizeDelta, Is.EqualTo(new Vector2(124f, 124f)));
                Assert.That(rect.localScale, Is.EqualTo(Vector3.one));
            }
        }

        [Test]
        public void Pile_PacksLikeMarbles_RestingOnTheFieldBottom()
        {
            var field = new GameObject("BubbleField", typeof(RectTransform));
            _created.Add(field);
            var rect = field.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(940f, 1100f);
            var pile = field.AddComponent<BubblePileView>();
            var serialized = new SerializedObject(pile);
            serialized.FindProperty("_slotRoot").objectReferenceValue = rect;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var level = Level(10);
            pile.PresentInitialQueue(
                level.BubbleQueue,
                level.PileLayout,
                AssetDatabase.LoadAssetAtPath<GameObject>(BubblePrefabPath),
                AssetDatabase.LoadAssetAtPath<GameObject>(FishPrefabPath),
                AssetDatabase.LoadAssetAtPath<FishVisualCatalog>(FishCatalogPath));

            Assert.That(pile.VisibleBubbleCount, Is.EqualTo(10));
            Assert.That(pile.ContentScale, Is.GreaterThan(1f), "Packed bubbles and fish are larger than the 300 px base.");
            var pitch = pile.ContentScale * BubbleView.BaseSize * BubbleView.VisibleDiameterFraction / BubblePileView.MarbleSqueeze;
            var layout = level.PileLayout;
            for (var a = 0; a < layout.Slots.Count; a++)
            {
                for (var b = a + 1; b < layout.Slots.Count; b++)
                {
                    if (!BubblePileAdjacency.AreAdjacent(layout.Slots[a], layout.Slots[b]))
                    {
                        continue;
                    }

                    pile.TryGetSlotPosition(layout.Slots[a].SlotId, out var pa);
                    pile.TryGetSlotPosition(layout.Slots[b].SlotId, out var pb);
                    Assert.That(Vector2.Distance(pa, pb), Is.EqualTo(pitch).Within(0.5f), "Neighbours " + a + "-" + b + " touch at one pitch. pa=" + pa + " pb=" + pb + " content=" + pile.ContentScale + " rect=" + rect.rect + " slotA=" + layout.Slots[a].AnchoredPosition + " slotB=" + layout.Slots[b].AnchoredPosition);
                }
            }

            pile.TryGetSlotPosition(0, out var bottom);
            var visibleRadius = pitch * BubblePileView.MarbleSqueeze * 0.5f;
            Assert.That(bottom.y - visibleRadius, Is.EqualTo(-550f).Within(6f), "The stack rests on the bottom of the field.");
            Assert.That(pile.TryGetBubble(0, out var view), Is.True);
            Assert.That(view.ContentScale, Is.EqualTo(pile.ContentScale));
            Assert.That(view.transform.Find("FishContainer").localScale.x, Is.EqualTo(pile.ContentScale).Within(0.0001f), "Fish grow with the bubble.");
        }

        [Test]
        public void FrozenVisuals_SitAboveBubbleFront_AndNeverBlockRaycasts()
        {
            var view = BindBubble(Level(6).BubbleQueue[0]);
            var art = AssetDatabase.LoadAssetAtPath<GameplayArtCatalog>(ArtCatalogPath);
            Assert.That(art.BubbleFrostOverlay, Is.Not.Null);

            view.ShowIce(2, art.BubbleFrostOverlay, null);

            Assert.That(view.IsIceShown, Is.True);
            Assert.That(view.IceCounterValue, Is.EqualTo(2));
            Assert.That(view.IceCounter.text, Is.EqualTo("2"));
            Assert.That(view.FrostOverlay.sprite, Is.SameAs(art.BubbleFrostOverlay));
            Assert.That(view.FrostOverlay.raycastTarget, Is.False);
            Assert.That(view.IceCounter.raycastTarget, Is.False);
            var host = view.FrostOverlay.transform.parent;
            Assert.That(host.name, Is.EqualTo("ModifierContainer"));
            Assert.That(host.GetSiblingIndex(), Is.GreaterThan(view.transform.Find("BubbleFront").GetSiblingIndex()));
            Assert.That(view.transform.Find("BubbleFront").GetSiblingIndex(), Is.GreaterThan(view.transform.Find("FishContainer").GetSiblingIndex()));
            Assert.That(view.IceCounter.transform.GetSiblingIndex(), Is.GreaterThan(view.FrostOverlay.transform.GetSiblingIndex()));

            view.ShowIce(0, art.BubbleFrostOverlay, null);
            Assert.That(view.IsIceShown, Is.False);
            Assert.That(view.IceCounter.gameObject.activeSelf, Is.False);
            Assert.That(view.IceCounterValue, Is.EqualTo(0));
        }

        // ---------- Session: frozen rules ----------

        [Test]
        public void FrozenFish_CannotBeSelected_AndNothingMutates()
        {
            var session = Start(MixedLevel(2, 1));
            var frozen = session.Bubbles[1];
            Assert.That(frozen.IsFrozen, Is.True);
            Assert.That(frozen.IsInteractable, Is.False);
            Assert.That(frozen.IceSelectionsRemaining, Is.EqualTo(2));

            var fish = frozen.Fish[0];
            Assert.That(session.IsFishFrozen(fish.Id), Is.True);
            var result = session.TrySelectFish(fish.Id, null);

            Assert.That(result.Accepted, Is.False);
            Assert.That(fish.State, Is.EqualTo(FishState.Idle));
            Assert.That(frozen.RemainingFishCount, Is.EqualTo(3));
            Assert.That(session.Tray.Count, Is.EqualTo(0));
            Assert.That(session.Tanks[0].FillCount + session.Tanks[1].FillCount, Is.EqualTo(0));
            Assert.That(session.State, Is.EqualTo(GameState.PlayerInput));
            Assert.That(session.FindFirstIdleFish(K), Is.Not.SameAs(fish));
        }

        [Test]
        public void AdjacentCorrectFish_DecrementsIce_AndStillRoutesToTank()
        {
            var session = Start(MixedLevel(2, 1));
            var frozen = session.Bubbles[1];

            // Slot 0 touches slot 1. Orange matches the first tank target.
            var orange = Find(session.Bubbles[0], O);
            var toTank = session.TrySelectFish(orange.Id, null);
            Assert.That(toTank.Outcome, Is.EqualTo(FishSelectionOutcome.RoutedToTank));
            Assert.That(frozen.IceSelectionsRemaining, Is.EqualTo(1));
            Assert.That(session.LastTurn.IceUpdates.Count, Is.EqualTo(1));
            Assert.That(session.LastTurn.IceUpdates[0].BubbleId, Is.EqualTo(frozen.BubbleId));
            Assert.That(session.LastTurn.IceUpdates[0].Remaining, Is.EqualTo(1));
            Assert.That(session.LastTurn.IceUpdates[0].Broke, Is.False);

            // GreenStriped matches the second tank target.
            var green = Find(session.Bubbles[0], G);
            Assert.That(session.TrySelectFish(green.Id, null).Outcome, Is.EqualTo(FishSelectionOutcome.RoutedToTank));
            Assert.That(frozen.IceSelectionsRemaining, Is.EqualTo(0));
            Assert.That(session.LastTurn.IceUpdates[0].Broke, Is.True);
        }

        [Test]
        public void AdjacentWrongFish_GoesToTray_AndDoesNotDecrementIce()
        {
            var session = Start(MixedLevel(2, 1));
            var frozen = session.Bubbles[1];

            // Slot 4 touches slot 1, but Yellow is not an active target: it goes to the Waiting Tray.
            var yellow = Find(session.Bubbles[4], Y);
            var toTray = session.TrySelectFish(yellow.Id, null);
            Assert.That(toTray.Outcome, Is.EqualTo(FishSelectionOutcome.RoutedToTray));
            Assert.That(session.Tray.Count, Is.EqualTo(1));
            Assert.That(frozen.IceSelectionsRemaining, Is.EqualTo(2));
            Assert.That(session.LastTurn.IceUpdates, Is.Empty);
        }

        [Test]
        public void NonAdjacentSelection_DoesNotDecrementIce()
        {
            var session = Start(MixedLevel(2, 1));
            var frozen = session.Bubbles[1];
            var layout = AssetDatabase.LoadAssetAtPath<BubblePileLayout>(LayoutPath);
            Assert.That(BubblePileAdjacency.AreAdjacent(Slot(layout, 2), Slot(layout, 1)), Is.False);

            // Correct fish, but slot 2 does not touch slot 1.
            var fish = Find(session.Bubbles[2], O);
            Assert.That(session.TrySelectFish(fish.Id, null).Outcome, Is.EqualTo(FishSelectionOutcome.RoutedToTank));
            Assert.That(frozen.IceSelectionsRemaining, Is.EqualTo(2));
            Assert.That(session.LastTurn.IceUpdates, Is.Empty);
        }

        [Test]
        public void OneSelection_DecrementsEveryAdjacentFrozenBubble()
        {
            // Frozen in slots 2 and 4; slot 3 touches both. RedClown is the first target.
            var level = Build("level_m15_multi", new[] { R, O, G, P, K, Y },
                Bubble(O, G).Raw(),
                Bubble(R, P, K).Raw(),
                Bubble(K, K, O, G).Ice(2),
                Bubble(R, R, P, P, Y).Raw(),
                Bubble(Y, Y, O, G).Ice(2));
            var session = Start(level);
            var result = session.TrySelectFish(Find(session.Bubbles[3], R).Id, null);

            Assert.That(result.Outcome, Is.EqualTo(FishSelectionOutcome.RoutedToTank));
            Assert.That(session.Bubbles[2].IceSelectionsRemaining, Is.EqualTo(1));
            Assert.That(session.Bubbles[4].IceSelectionsRemaining, Is.EqualTo(1));
            Assert.That(session.LastTurn.IceUpdates.Count, Is.EqualTo(2));

            // Slot 0 touches slot 2 but not slot 4. Orange is the second target.
            session.TrySelectFish(Find(session.Bubbles[0], O).Id, null);
            Assert.That(session.Bubbles[2].IceSelectionsRemaining, Is.EqualTo(0));
            Assert.That(session.Bubbles[4].IceSelectionsRemaining, Is.EqualTo(1));
        }

        [Test]
        public void CounterZero_UnlocksBubble_AndItsFishBecomeSelectable()
        {
            var session = Start(MixedLevel(2, 1));
            var frozen = session.Bubbles[1];
            var frozenFish = Find(frozen, R);
            session.TrySelectFish(Find(session.Bubbles[0], O).Id, null);
            Assert.That(session.TrySelectFish(frozenFish.Id, null).Accepted, Is.False, "Still frozen at 1.");
            session.TrySelectFish(Find(session.Bubbles[0], G).Id, null);

            Assert.That(frozen.IceSelectionsRemaining, Is.EqualTo(0));
            Assert.That(frozen.IsFrozen, Is.False);
            Assert.That(frozen.IsInteractable, Is.True);
            Assert.That(frozen.HasPopped, Is.False, "Breaking ice never pops the bubble.");
            Assert.That(session.TryGetOccupiedSlot(frozen.BubbleId, out _), Is.True);
            Assert.That(session.IsFishFrozen(frozenFish.Id), Is.False);
            Assert.That(session.TrySelectFish(frozenFish.Id, null).Accepted, Is.True);
            Assert.That(frozen.RemainingFishCount, Is.EqualTo(2));
        }

        [Test]
        public void FrozenBubble_FollowsGravity_AndAdjacencyUsesCurrentSlots()
        {
            // Frozen bubble in slot 3 with a high counter. Emptying slot 1 drops it into slot 1.
            // Targets: RedClown and BlackStriped are active; PinkStriped goes to the tray and never chips.
            var level = Build("level_m15_gravity", new[] { R, K, P, O, G, Y },
                Bubble(R, G).Raw(),
                Bubble(R, P, K).Raw(),
                Bubble(K, K, O, G).Raw(),
                Bubble(R, R, P, P, Y).Ice(9),
                Bubble(Y, Y, O, G).Raw());
            var session = Start(level);
            var frozen = session.Bubbles[3];
            var source = session.Bubbles[1];
            while (source.RemainingFishCount > 0)
            {
                Assert.That(session.TrySelectFish(source.Fish[0].Id, null).Accepted, Is.True);
            }

            Assert.That(source.HasPopped, Is.True);
            Assert.That(session.Tray.Count, Is.EqualTo(1));
            Assert.That(frozen.IceSelectionsRemaining, Is.EqualTo(7), "Only the two tank-bound fish chipped the ice.");
            Assert.That(session.TryGetOccupiedSlot(frozen.BubbleId, out var frozenSlot), Is.True);
            Assert.That(frozenSlot, Is.EqualTo(1), "Frozen Bubbles still fall with deterministic pile gravity.");

            // Slot 2 touched slot 3, but does not touch slot 1.
            var left = session.GetBubbleAtSlot(2);
            Assert.That(left, Is.SameAs(session.Bubbles[2]));
            Assert.That(session.TrySelectFish(Find(left, K).Id, null).Outcome, Is.EqualTo(FishSelectionOutcome.RoutedToTank));
            Assert.That(frozen.IceSelectionsRemaining, Is.EqualTo(7));

            // Slot 0 touches slot 1.
            Assert.That(session.TrySelectFish(Find(session.GetBubbleAtSlot(0), R).Id, null).Outcome, Is.EqualTo(FishSelectionOutcome.RoutedToTank));
            Assert.That(frozen.IceSelectionsRemaining, Is.EqualTo(6));
        }

        [Test]
        public void Retry_RestoresAuthoredCounter()
        {
            var level = MixedLevel(2, 1);
            var first = Start(level);
            first.TrySelectFish(Find(first.Bubbles[0], O).Id, null);
            first.TrySelectFish(Find(first.Bubbles[0], G).Id, null);
            Assert.That(first.Bubbles[1].IsFrozen, Is.False);

            Assert.That(level.BubbleQueue[1].IceBreakRequiredSelections, Is.EqualTo(2), "Authored data is never mutated.");
            var retry = Start(level);
            Assert.That(retry.Bubbles[1].IsFrozen, Is.True);
            Assert.That(retry.Bubbles[1].IceSelectionsRemaining, Is.EqualTo(2));
            Assert.That(retry.Bubbles[1].IceBreakRequiredSelections, Is.EqualTo(2));
        }

        // ---------- Shipped levels ----------

        [Test]
        public void Levels001To010_Validate_WithExactTargetAndFishCounts()
        {
            var config = Config();
            for (var number = 1; number <= 10; number++)
            {
                var level = Level(number);
                var result = Validate(level);
                Assert.That(result.IsValid, Is.True, level.LevelId + "\n" + Describe(result));
                Assert.That(level.TotalFishRequired, Is.EqualTo(level.TargetGroupQueue.Count * config.TankCapacity), level.LevelId);
                var targets = new Dictionary<FishType, int>();
                var fish = new Dictionary<FishType, int>();
                for (var i = 0; i < level.TargetGroupQueue.Count; i++)
                {
                    targets.TryGetValue(level.TargetGroupQueue[i], out var count);
                    targets[level.TargetGroupQueue[i]] = count + 1;
                }

                var total = 0;
                for (var b = 0; b < level.BubbleQueue.Count; b++)
                {
                    var fishes = level.BubbleQueue[b].Fishes;
                    Assert.That(fishes.Count, Is.InRange(2, 5), level.BubbleQueue[b].BubbleId);
                    for (var f = 0; f < fishes.Count; f++)
                    {
                        fish.TryGetValue(fishes[f], out var count);
                        fish[fishes[f]] = count + 1;
                        total++;
                    }
                }

                Assert.That(total, Is.EqualTo(level.TotalFishRequired), level.LevelId);
                foreach (var pair in fish)
                {
                    Assert.That(pair.Key, Is.Not.EqualTo(FishType.Crab).And.Not.EqualTo(FishType.Snail));
                    targets.TryGetValue(pair.Key, out var groups);
                    Assert.That(pair.Value, Is.EqualTo(groups * config.TankCapacity), level.LevelId + " " + pair.Key);
                }
            }
        }

        [TestCase(6, 1, 2, 2)]
        [TestCase(7, 2, 2, 3)]
        [TestCase(8, 2, 2, 4)]
        [TestCase(9, 3, 3, 4)]
        [TestCase(10, 4, 3, 5)]
        public void FrozenLevels_UseAuthoredCountersInRange(int number, int frozenCount, int minCounter, int maxCounter)
        {
            var level = Level(number);
            var frozen = 0;
            for (var i = 0; i < level.BubbleQueue.Count; i++)
            {
                var bubble = level.BubbleQueue[i];
                if (!bubble.IsFrozen)
                {
                    Assert.That(bubble.Modifier, Is.EqualTo(BubbleModifier.None), bubble.BubbleId);
                    Assert.That(bubble.IceBreakRequiredSelections, Is.EqualTo(0), bubble.BubbleId);
                    continue;
                }

                frozen++;
                Assert.That(bubble.IceBreakRequiredSelections, Is.InRange(minCounter, maxCounter), bubble.BubbleId);
                Assert.That(i, Is.LessThan(level.PileLayout.Slots.Count), bubble.BubbleId + " starts in the opening pile.");
            }

            Assert.That(frozen, Is.EqualTo(frozenCount));
        }

        [Test]
        public void Levels001To005_HaveNoFrozenBubbles()
        {
            for (var number = 1; number <= 5; number++)
            {
                var level = Level(number);
                for (var i = 0; i < level.BubbleQueue.Count; i++)
                {
                    Assert.That(level.BubbleQueue[i].IsFrozen, Is.False, level.BubbleQueue[i].BubbleId);
                    Assert.That(level.BubbleQueue[i].IceBreakRequiredSelections, Is.EqualTo(0));
                }
            }
        }

        [Test]
        public void Level010_IsHardest_AndMixesEveryBubbleSizeAndNormalType()
        {
            var level = Level(10);
            var sizes = new HashSet<int>();
            var types = new HashSet<FishType>();
            for (var i = 0; i < level.BubbleQueue.Count; i++)
            {
                sizes.Add(level.BubbleQueue[i].Fishes.Count);
                types.UnionWith(level.BubbleQueue[i].Fishes);
            }

            Assert.That(sizes, Is.EquivalentTo(new[] { 2, 3, 4, 5 }));
            Assert.That(types, Is.EquivalentTo(NormalTypes));
            Assert.That(level.BubbleQueue.Count, Is.InRange(16, 18));
            for (var number = 1; number < 10; number++)
            {
                Assert.That(Level(number).TotalFishRequired, Is.LessThan(level.TotalFishRequired), "level " + number);
            }
        }

        [Test]
        public void FrozenLevels_NeverSoftLock_UnderSeededRandomPlay()
        {
            // A soft-lock is a live attempt with fish left in the pile but nothing selectable.
            // Random players (matching fish 70% of the time) may lose by tray overflow, but must never get stuck.
            var config = Config();
            var routing = new FishRoutingService();
            for (var number = 6; number <= 10; number++)
            {
                var level = Level(number);
                var random = new System.Random(1500 + number);
                var wins = 0;
                for (var run = 0; run < 250; run++)
                {
                    var session = LevelSession.Start(level, config, false);
                    var guard = 0;
                    while (session.State == GameState.PlayerInput && guard++ < 200)
                    {
                        var all = new List<FishRuntimeState>();
                        var matching = new List<FishRuntimeState>();
                        for (var b = 0; b < session.Bubbles.Count; b++)
                        {
                            var bubble = session.Bubbles[b];
                            if (bubble == null || !bubble.IsInPlay || !bubble.IsInteractable)
                            {
                                continue;
                            }

                            for (var f = 0; f < bubble.Fish.Count; f++)
                            {
                                all.Add(bubble.Fish[f]);
                                if (routing.SelectTank(bubble.Fish[f].Type, session.Tanks).Found)
                                {
                                    matching.Add(bubble.Fish[f]);
                                }
                            }
                        }

                        Assert.That(all.Count, Is.GreaterThan(0), level.LevelId + " run " + run + " soft-locked with frozen fish only.");
                        var pool = matching.Count > 0 && random.NextDouble() < 0.7 ? matching : all;
                        Assert.That(session.TrySelectFish(pool[random.Next(pool.Count)].Id, null).Accepted, Is.True);
                    }

                    Assert.That(session.State, Is.Not.EqualTo(GameState.PlayerInput), level.LevelId + " run " + run + " did not finish.");
                    wins += session.State == GameState.Win ? 1 : 0;
                }

                Assert.That(wins, Is.GreaterThan(0), level.LevelId + " must be winnable.");
            }
        }

        [Test]
        public void FrozenLevels_HaveIceThatBreaksDuringAWinningPlay()
        {
            // Matching-first player that otherwise prefers fish adjacent to Frozen Bubbles.
            var config = Config();
            var routing = new FishRoutingService();
            for (var number = 6; number <= 10; number++)
            {
                var level = Level(number);
                var session = LevelSession.Start(level, config, false);
                var breaks = 0;
                var guard = 0;
                while (session.State == GameState.PlayerInput && guard++ < 200)
                {
                    FishRuntimeState pick = null;
                    for (var b = 0; b < session.Bubbles.Count && pick == null; b++)
                    {
                        var bubble = session.Bubbles[b];
                        if (bubble == null || !bubble.IsInPlay || !bubble.IsInteractable)
                        {
                            continue;
                        }

                        for (var f = 0; f < bubble.Fish.Count; f++)
                        {
                            if (routing.SelectTank(bubble.Fish[f].Type, session.Tanks).Found)
                            {
                                pick = bubble.Fish[f];
                                break;
                            }
                        }
                    }

                    pick = pick ?? SoonestNeeded(session, level);
                    Assert.That(pick, Is.Not.Null, level.LevelId + " has nothing selectable.");
                    session.TrySelectFish(pick.Id, null);
                    for (var u = 0; u < session.LastTurn.IceUpdates.Count; u++)
                    {
                        breaks += session.LastTurn.IceUpdates[u].Broke ? 1 : 0;
                    }
                }

                Assert.That(session.State, Is.EqualTo(GameState.Win), level.LevelId);
                var frozen = 0;
                for (var i = 0; i < level.BubbleQueue.Count; i++)
                {
                    frozen += level.BubbleQueue[i].IsFrozen ? 1 : 0;
                }

                Assert.That(breaks, Is.EqualTo(frozen), level.LevelId + " every Frozen Bubble had to be opened to win.");
            }
        }

        // ---------- Helpers ----------

        private static BubbleDefinition FirstBubbleWithFishCount(LevelData level, int fishCount)
        {
            for (var i = 0; i < level.BubbleQueue.Count; i++)
            {
                if (level.BubbleQueue[i].Fishes.Count == fishCount)
                {
                    return level.BubbleQueue[i];
                }
            }

            Assert.Fail(level.LevelId + " has no " + fishCount + "-fish bubble.");
            return null;
        }

        private static FishRuntimeState SoonestNeeded(LevelSession session, LevelData level)
        {
            FishRuntimeState best = null;
            var bestSoon = int.MaxValue;
            for (var b = 0; b < session.Bubbles.Count; b++)
            {
                var bubble = session.Bubbles[b];
                if (bubble == null || !bubble.IsInPlay || !bubble.IsInteractable)
                {
                    continue;
                }

                for (var f = 0; f < bubble.Fish.Count; f++)
                {
                    var soon = int.MaxValue / 2;
                    for (var q = session.Targets.NextUnassignedIndex; q < level.TargetGroupQueue.Count; q++)
                    {
                        if (level.TargetGroupQueue[q] == bubble.Fish[f].Type)
                        {
                            soon = q;
                            break;
                        }
                    }

                    if (best == null || soon < bestSoon)
                    {
                        best = bubble.Fish[f];
                        bestSoon = soon;
                    }
                }
            }

            return best;
        }

        /// <summary>
        /// Valid 18-fish level with 2/3/4/5-fish bubbles. Queue index i starts in slot i.
        /// B001 O,G | B002 R,P,K | B003 K,K,O,G | B004 R,R,P,P,Y | B005 Y,Y,O,G.
        /// </summary>
        private LevelData MixedLevel(int ice, int iceIndex, BubbleModifier? modifier = null)
        {
            var specs = new[]
            {
                Bubble(O, G),
                Bubble(R, P, K),
                Bubble(K, K, O, G),
                Bubble(R, R, P, P, Y),
                Bubble(Y, Y, O, G)
            };

            for (var i = 0; i < specs.Length; i++)
            {
                if (i != iceIndex)
                {
                    specs[i] = specs[i].Raw();
                    continue;
                }

                specs[i] = modifier.HasValue ? specs[i].With(modifier.Value, ice) : specs[i].Ice(ice);
            }

            return Build("level_m15_mixed", new[] { O, G, R, P, K, Y }, specs);
        }

        private LevelData Build(string id, FishType[] targets, params BubbleSpec[] bubbles)
        {
            var level = ScriptableObject.CreateInstance<LevelData>();
            _created.Add(level);
            var serialized = new SerializedObject(level);
            serialized.FindProperty("_levelId").stringValue = id;
            serialized.FindProperty("_initialUnlockedTankCount").intValue = 2;
            serialized.FindProperty("_totalFishRequired").intValue = targets.Length * 3;
            var queue = serialized.FindProperty("_targetGroupQueue");
            queue.arraySize = targets.Length;
            for (var i = 0; i < targets.Length; i++)
            {
                queue.GetArrayElementAtIndex(i).intValue = (int)targets[i];
            }

            var list = serialized.FindProperty("_bubbleQueue");
            list.arraySize = bubbles.Length;
            for (var i = 0; i < bubbles.Length; i++)
            {
                var element = list.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("_bubbleId").stringValue = "LM15_B" + (i + 1).ToString("000");
                element.FindPropertyRelative("_modifier").intValue = (int)bubbles[i].Modifier;
                element.FindPropertyRelative("_iceBreakRequiredSelections").intValue = bubbles[i].IceCount;
                var fishes = element.FindPropertyRelative("_fishes");
                fishes.arraySize = bubbles[i].Fish.Length;
                for (var f = 0; f < bubbles[i].Fish.Length; f++)
                {
                    fishes.GetArrayElementAtIndex(f).intValue = (int)bubbles[i].Fish[f];
                }
            }

            serialized.FindProperty("_pileLayout").objectReferenceValue = AssetDatabase.LoadAssetAtPath<BubblePileLayout>(LayoutPath);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return level;
        }

        private BubbleView BindBubble(BubbleDefinition definition)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BubblePrefabPath);
            var fishPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FishPrefabPath);
            var catalog = AssetDatabase.LoadAssetAtPath<FishVisualCatalog>(FishCatalogPath);
            Assert.That(prefab, Is.Not.Null, BubblePrefabPath);
            Assert.That(fishPrefab, Is.Not.Null, FishPrefabPath);
            var instance = Object.Instantiate(prefab);
            _created.Add(instance);
            var view = instance.GetComponent<BubbleView>();
            view.Bind(definition, 0, catalog, fishPrefab);
            return view;
        }

        private static LevelSession Start(LevelData level)
        {
            return LevelSession.Start(level, Config(), false);
        }

        private static FishRuntimeState Find(BubbleRuntimeState bubble, FishType type)
        {
            for (var i = 0; i < bubble.Fish.Count; i++)
            {
                if (bubble.Fish[i].Type == type)
                {
                    return bubble.Fish[i];
                }
            }

            Assert.Fail("Bubble " + bubble.BubbleId + " has no " + type);
            return null;
        }

        private static BubblePileSlotDefinition Slot(BubblePileLayout layout, int slotId)
        {
            for (var i = 0; i < layout.Slots.Count; i++)
            {
                if (layout.Slots[i].SlotId == slotId)
                {
                    return layout.Slots[i];
                }
            }

            return null;
        }

        private static LevelValidationIssue AssertHasIssue(LevelData level, string code)
        {
            var result = Validate(level);
            Assert.That(result.IsValid, Is.False);
            for (var i = 0; i < result.Issues.Count; i++)
            {
                if (result.Issues[i].Code == code)
                {
                    return result.Issues[i];
                }
            }

            Assert.Fail("Missing " + code + "\n" + Describe(result));
            return null;
        }

        private static LevelValidationResult Validate(LevelData level)
        {
            return new LevelValidator().Validate(level, Config());
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

        private static GameConfig Config()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            Assert.That(config, Is.Not.Null, ConfigPath);
            return config;
        }

        private static LevelData Level(int number)
        {
            var path = "Assets/Game/Data/Levels/Level_" + number.ToString("000") + ".asset";
            var level = AssetDatabase.LoadAssetAtPath<LevelData>(path);
            Assert.That(level, Is.Not.Null, path);
            return level;
        }

        private static BubbleSpec Bubble(params FishType[] fish)
        {
            return new BubbleSpec(fish, BubbleModifier.None, 0);
        }

        private readonly struct BubbleSpec
        {
            public BubbleSpec(FishType[] fish, BubbleModifier modifier, int ice)
            {
                Fish = fish;
                Modifier = modifier;
                IceCount = ice;
            }

            public FishType[] Fish { get; }

            public BubbleModifier Modifier { get; }

            public int IceCount { get; }

            public BubbleSpec Raw()
            {
                return new BubbleSpec(Fish, BubbleModifier.None, 0);
            }

            public BubbleSpec Ice(int count)
            {
                return new BubbleSpec(Fish, BubbleModifier.Frozen, count);
            }

            public BubbleSpec With(BubbleModifier modifier, int count)
            {
                return new BubbleSpec(Fish, modifier, count);
            }
        }
    }
}
