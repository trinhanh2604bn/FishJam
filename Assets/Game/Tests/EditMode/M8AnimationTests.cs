using System.Collections.Generic;
using FishPuzzle.Core;
using FishPuzzle.Domain;
using FishPuzzle.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FishPuzzle.Tests.EditMode
{
    public sealed class M8AnimationTests
    {
        private const string TuningPath = "Assets/Game/Data/Config/AnimationTuning.asset";
        private const string LevelPath = "Assets/Game/Data/Levels/Level_001.asset";
        private const string ConfigPath = "Assets/Game/Data/Config/GameConfig.asset";

        [Test]
        public void TuningAsset_KeepsDurationsShort()
        {
            var tuning = AssetDatabase.LoadAssetAtPath<AnimationTuning>(TuningPath);

            Assert.That(tuning, Is.Not.Null);
            Assert.That(tuning.FishTapSquashDuration, Is.GreaterThan(0f).And.LessThanOrEqualTo(0.25f));
            Assert.That(tuning.FishRouteDuration, Is.GreaterThan(0f).And.LessThanOrEqualTo(0.25f));
            Assert.That(tuning.FishLandingBounceDuration, Is.GreaterThan(0f).And.LessThanOrEqualTo(0.25f));
            Assert.That(tuning.TankResolveDuration, Is.GreaterThan(0f).And.LessThanOrEqualTo(0.25f));
            Assert.That(tuning.TankTargetSwapDuration, Is.GreaterThan(0f).And.LessThanOrEqualTo(0.25f));
            Assert.That(tuning.TrayAutoMoveDuration, Is.GreaterThan(0f).And.LessThanOrEqualTo(0.25f));
            Assert.That(tuning.TrayAutoMoveStagger, Is.GreaterThanOrEqualTo(0f).And.LessThanOrEqualTo(0.25f));
            Assert.That(tuning.BubbleFishReflowDuration, Is.GreaterThan(0f).And.LessThanOrEqualTo(0.25f));
            Assert.That(tuning.BubblePopDuration, Is.GreaterThan(0f).And.LessThanOrEqualTo(0.25f));
            Assert.That(tuning.BubbleFallDuration, Is.GreaterThan(0f).And.LessThanOrEqualTo(0.25f));
            Assert.That(tuning.BubbleSlideDuration, Is.GreaterThan(0f).And.LessThanOrEqualTo(0.25f));
            Assert.That(tuning.BubbleLandingBounceDuration, Is.GreaterThan(0f).And.LessThanOrEqualTo(0.25f));
            Assert.That(tuning.BubbleTopSpawnDuration, Is.GreaterThan(0f).And.LessThanOrEqualTo(0.25f));
            Assert.That(tuning.PresentationSafetySeconds, Is.GreaterThan(tuning.FishRouteDuration));
        }

        [Test]
        public void Curve_StartsAndEndsOnTheAuthoritativePoints()
        {
            var start = new Vector3(0f, 0f, 0f);
            var end = new Vector3(100f, -40f, 0f);
            var control = new Vector3(40f, 80f, 0f);

            var atStart = PresentationMotion.QuadraticBezier(start, control, end, 0f);
            var atEnd = PresentationMotion.QuadraticBezier(start, control, end, 1f);
            var mid = PresentationMotion.QuadraticBezier(start, control, end, 0.5f);

            Assert.That(Vector3.Distance(atStart, start), Is.LessThan(0.001f));
            Assert.That(Vector3.Distance(atEnd, end), Is.LessThan(0.001f));
            Assert.That(mid.y, Is.GreaterThan(start.y));
            Assert.That(mid.y, Is.GreaterThan(end.y));
        }

        [Test]
        public void InterruptedOrMissingMotion_StillReportsTheEnd()
        {
            var missing = PresentationMotion.Sample(false, 0.01f, 0.2f);
            var finished = PresentationMotion.Sample(true, 0.2f, 0.2f);
            var instant = PresentationMotion.Sample(true, 0f, 0f);
            var running = PresentationMotion.Sample(true, 0.05f, 0.2f);

            Assert.That(missing.Completed, Is.True);
            Assert.That(missing.T, Is.EqualTo(1f));
            Assert.That(finished.Completed, Is.True);
            Assert.That(instant.Completed, Is.True);
            Assert.That(running.Completed, Is.False);
            Assert.That(running.T, Is.EqualTo(0.25f).Within(0.001f));
            Assert.That(PresentationMotion.FallSlide(Vector2.zero, new Vector2(10f, -30f), 1f, 20f), Is.EqualTo(new Vector2(10f, -30f)));
        }

        [Test]
        public void MissingPopVfx_CreatesNothing()
        {
            var host = new GameObject("pop-vfx-host", typeof(RectTransform));
            try
            {
                var created = new List<GameObject>();
                var count = BubblePopVfx.Spawn(host.GetComponent<RectTransform>(), Vector2.zero, null, null, created);

                Assert.That(BubblePopVfx.CanPlay(null, null), Is.False);
                Assert.That(count, Is.EqualTo(0));
                Assert.That(created, Is.Empty);
                Assert.That(host.transform.childCount, Is.EqualTo(0));
                BubblePopVfx.Animate(created, Vector2.zero, 1f);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void PresentationHold_BlocksInput_ThenReturnsToPlayerInput()
        {
            var session = LevelSession.Start(Level(), Config(), true);
            var fish = session.FindFirstIdleFish(FishType.Orange);

            session.HoldForPresentation(GameState.RoutingFish);

            Assert.That(session.State, Is.EqualTo(GameState.RoutingFish));
            var ignored = session.TrySelectFish(fish.Id, null);
            Assert.That(ignored.Outcome, Is.EqualTo(FishSelectionOutcome.Ignored));
            Assert.That(session.Tanks[0].FillCount, Is.EqualTo(0));

            session.ReleasePresentationHold();

            Assert.That(session.State, Is.EqualTo(GameState.PlayerInput));
            var accepted = session.TrySelectFish(fish.Id, null);
            Assert.That(accepted.Outcome, Is.EqualTo(FishSelectionOutcome.RoutedToTank));
            Assert.That(session.State, Is.EqualTo(GameState.PlayerInput));
        }

        [Test]
        public void TrayCascade_RecordsSourceSlot_AndReturnsToPlayerInput()
        {
            var session = LevelSession.Start(
                Config(),
                new[] { FishType.Orange, FishType.GreenStriped, FishType.RedClown },
                2,
                9,
                new List<IReadOnlyList<FishType>>
                {
                    new[] { FishType.RedClown, FishType.PinkStriped, FishType.Yellow },
                    new[] { FishType.Orange, FishType.Orange, FishType.Orange }
                },
                true);
            var red = session.FindFirstIdleFish(FishType.RedClown);
            session.TrySelectFish(red.Id, null);
            for (var i = 0; i < 3; i++)
            {
                var orange = session.FindFirstIdleFish(FishType.Orange);
                session.TrySelectFish(orange.Id, null);
            }

            Assert.That(session.State, Is.EqualTo(GameState.PlayerInput));
            Assert.That(session.LastTurn.Promotions.Count, Is.EqualTo(1));
            Assert.That(session.LastTurn.Promotions[0].FishId, Is.EqualTo(red.Id));
            Assert.That(session.LastTurn.Promotions[0].SourceTrayIndex, Is.EqualTo(0));
            Assert.That(session.LastTurn.Promotions[0].TankSlotIndex, Is.EqualTo(0));
            Assert.That(session.Tray.Count, Is.EqualTo(0));
            Assert.That(session.Tanks[0].CurrentTarget, Is.EqualTo(FishType.RedClown));
            Assert.That(session.Tanks[0].FillCount, Is.EqualTo(1));
        }

        private static LevelData Level()
        {
            return AssetDatabase.LoadAssetAtPath<LevelData>(LevelPath);
        }

        private static GameConfig Config()
        {
            return AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
        }
    }
}
