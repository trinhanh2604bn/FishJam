using FishPuzzle.Core;
using FishPuzzle.Domain;
using FishPuzzle.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FishPuzzle.Tests.EditMode
{
    public sealed class M10ImpactFxTests
    {
        private const string TuningPath = "Assets/Game/Data/Config/AnimationTuning.asset";
        private const string LevelPath = "Assets/Game/Tests/Fixtures/LegacyLevel_036.asset";
        private const string ConfigPath = "Assets/Game/Data/Config/GameConfig.asset";

        [Test]
        public void Tuning_PopsImmediately_AndCapsBurstSplashAndTouch()
        {
            var tuning = AssetDatabase.LoadAssetAtPath<AnimationTuning>(TuningPath);

            Assert.That(tuning, Is.Not.Null);
            Assert.That(tuning.BubblePopDuration, Is.InRange(0.05f, 0.10f));
            Assert.That(tuning.BubblePopDuration, Is.LessThan(0.20f));
            Assert.That(tuning.BubbleBurstLifetime, Is.InRange(BubbleBurstPool.MinLifetime, BubbleBurstPool.MaxLifetime));
            Assert.That(tuning.BubbleBurstCount, Is.InRange(BubbleBurstPool.MinCount, BubbleBurstPool.MaxCount));
            Assert.That(tuning.BubbleLaunchLifetime, Is.InRange(BubbleBurstPool.TrailMinLifetime, BubbleBurstPool.TrailMaxLifetime));
            Assert.That(tuning.BubbleLaunchLifetime, Is.GreaterThan(tuning.BubbleBurstLifetime));
            Assert.That(tuning.BubbleLaunchCount, Is.InRange(BubbleBurstPool.TrailMinCount, BubbleBurstPool.TrailMaxCount));
            Assert.That(tuning.BubbleBurstPoolCap, Is.GreaterThanOrEqualTo(tuning.BubbleBurstCount));
            Assert.That(tuning.BubbleBurstPoolCap, Is.LessThanOrEqualTo(BubbleBurstPool.HardPoolCap));
            Assert.That(tuning.TankSplashDropletCount, Is.InRange(TankSplashView.MinDroplets, TankSplashView.MaxDroplets));
            Assert.That(tuning.TankSplashPoolCap, Is.InRange(1, TankSplashView.HardPoolCap));
            Assert.That(tuning.TouchBubbleLimit, Is.InRange(1, 8));
        }

        [Test]
        public void LastFish_MakesTheBubbleNonInteractableImmediately()
        {
            var session = LevelSession.Start(Level(), Config(), true);
            var bubble = session.Bubbles[0];
            var ids = FishIds(bubble);

            for (var i = 0; i < ids.Length; i++)
            {
                var result = session.TrySelectFish(ids[i], null);
                Assert.That(result.Accepted, Is.True);
                if (i < ids.Length - 1)
                {
                    Assert.That(bubble.RemainingFishCount, Is.GreaterThan(0));
                    Assert.That(bubble.IsInteractable, Is.True);
                    Assert.That(bubble.HasPopped, Is.False);
                }
            }

            Assert.That(bubble.RemainingFishCount, Is.EqualTo(0));
            Assert.That(bubble.IsInteractable, Is.False);
            Assert.That(bubble.HasPopped, Is.True);
            Assert.That(session.State, Is.EqualTo(GameState.PlayerInput));
        }

        [Test]
        public void BubbleBurst_EmitsSmallParticles_ThenReusesThePool()
        {
            var parent = new GameObject("burst-parent", typeof(RectTransform));
            var pool = BubbleBurstPool.Create(parent.GetComponent<RectTransform>(), BubbleBurstPool.DefaultPoolCap);
            try
            {
                var emitted = pool.Emit(new Vector2(12f, -8f), ProceduralVfxSprite.Dot, ProceduralVfxSprite.Ring, 0.40f, 11);

                Assert.That(emitted, Is.GreaterThanOrEqualTo(BubbleBurstPool.MinCount + BubbleBurstPool.MinMediumCount));
                Assert.That(pool.LastEmitCount, Is.EqualTo(emitted));
                Assert.That(pool.ActiveCount, Is.EqualTo(emitted));
                Assert.That(pool.PoolSize, Is.EqualTo(emitted));
                Assert.That(pool.PoolSize, Is.LessThanOrEqualTo(BubbleBurstPool.DefaultPoolCap));
                Assert.That(pool.HasSizeVariation(), Is.True);
                Assert.That(pool.IsSemiTransparent(), Is.True);

                pool.Tick(0.16f);

                Assert.That(pool.ActiveCount, Is.EqualTo(emitted));
                Assert.That(pool.MovedUpward(8f), Is.True);
                Assert.That(pool.IsSemiTransparent(), Is.True);

                pool.Tick(0.60f);

                Assert.That(pool.ActiveCount, Is.EqualTo(0));
                var pooled = pool.PoolSize;
                var again = pool.Emit(Vector2.zero, ProceduralVfxSprite.Dot, null, 0.40f, 11);

                Assert.That(again, Is.GreaterThanOrEqualTo(BubbleBurstPool.MinCount + BubbleBurstPool.MinMediumCount));
                Assert.That(pool.PoolSize, Is.LessThanOrEqualTo(Mathf.Max(pooled, again)));
                Assert.That(pool.ActiveCount, Is.EqualTo(again));
            }
            finally
            {
                Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void LaunchTrail_LingersAfterThePopBurstWouldHaveFaded()
        {
            var parent = new GameObject("trail-parent", typeof(RectTransform));
            var rect = parent.GetComponent<RectTransform>();
            var pool = BubbleBurstPool.Create(rect, BubbleBurstPool.DefaultPoolCap);
            try
            {
                var emitted = pool.EmitTrail(rect.position, ProceduralVfxSprite.Dot, 0.95f, 12);

                Assert.That(emitted, Is.InRange(BubbleBurstPool.TrailMinCount, BubbleBurstPool.TrailMaxCount));
                Assert.That(pool.ActiveCount, Is.EqualTo(emitted));
                Assert.That(pool.IsSemiTransparent(), Is.True);

                pool.Tick(0.55f);

                Assert.That(pool.ActiveCount, Is.EqualTo(emitted));
                Assert.That(pool.MovedUpward(8f), Is.True);

                pool.Tick(0.70f);

                Assert.That(pool.ActiveCount, Is.EqualTo(0));
            }
            finally
            {
                Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void BubbleBurst_StaysInsideThePoolCap()
        {
            var parent = new GameObject("burst-cap", typeof(RectTransform));
            var pool = BubbleBurstPool.Create(parent.GetComponent<RectTransform>(), BubbleBurstPool.DefaultPoolCap);
            try
            {
                for (var i = 0; i < 6; i++)
                {
                    pool.Emit(Vector2.zero, ProceduralVfxSprite.Dot, null, 0.40f, BubbleBurstPool.MaxCount);
                }

                var skipped = pool.Emit(Vector2.zero, ProceduralVfxSprite.Dot, null, 0.40f, BubbleBurstPool.MaxCount);

                Assert.That(skipped, Is.EqualTo(0), "A full pool skips particles instead of recycling live ones.");
                Assert.That(pool.PoolSize, Is.EqualTo(BubbleBurstPool.DefaultPoolCap));
                Assert.That(pool.ActiveCount, Is.LessThanOrEqualTo(BubbleBurstPool.DefaultPoolCap));
            }
            finally
            {
                Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void MissingBurst_CreatesNothing_AndDoesNotMutateTheSession()
        {
            var session = LevelSession.Start(Level(), Config(), true);
            var fill = session.Tanks[0].FillCount;
            var tray = session.Tray.Count;
            var progress = session.Progress.CollectedFishCount;
            var parent = new GameObject("missing-burst", typeof(RectTransform));
            var pool = BubbleBurstPool.Create(parent.GetComponent<RectTransform>(), BubbleBurstPool.DefaultPoolCap);
            try
            {
                var emitted = pool.Emit(Vector2.zero, null, null, 0.40f, 11);
                pool.Tick(0.40f);
                var splash = TankSplashView.Play(parent.GetComponent<RectTransform>(), Vector3.zero, null, null, null, false);

                Assert.That(emitted, Is.EqualTo(0));
                Assert.That(pool.ActiveCount, Is.EqualTo(0));
                Assert.That(pool.PoolSize, Is.EqualTo(0));
                Assert.That(splash, Is.Null);
                Assert.That(parent.GetComponentsInChildren<TankSplashView>(true).Length, Is.EqualTo(0));
                Assert.That(session.Tanks[0].FillCount, Is.EqualTo(fill));
                Assert.That(session.Tray.Count, Is.EqualTo(tray));
                Assert.That(session.Progress.CollectedFishCount, Is.EqualTo(progress));
                Assert.That(session.State, Is.EqualTo(GameState.PlayerInput));
            }
            finally
            {
                Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void TankSplash_UsesDropletsWithinRange_AndReusesHosts()
        {
            var parent = new GameObject("splash-pool", typeof(RectTransform));
            var rect = parent.GetComponent<RectTransform>();
            try
            {
                var first = TankSplashView.Play(rect, Vector3.zero, null, null, null, true);
                Assert.That(first, Is.Not.Null);
                Assert.That(first.DropletCount, Is.InRange(TankSplashView.MinDroplets, TankSplashView.MaxDroplets));
                Assert.That(first.IsPlaying, Is.True);
                Assert.That(parent.transform.childCount, Is.EqualTo(1));

                first.Tick(1f);

                Assert.That(first.IsPlaying, Is.False);
                var reused = TankSplashView.Play(rect, Vector3.zero, null, null, null, true, 8);
                Assert.That(reused, Is.SameAs(first));
                Assert.That(reused.DropletCount, Is.EqualTo(8));
                Assert.That(reused.IsPlaying, Is.True);
                Assert.That(parent.transform.childCount, Is.EqualTo(1));

                for (var i = 0; i < 4; i++)
                {
                    TankSplashView.Play(rect, Vector3.zero, null, null, null, true);
                }

                Assert.That(parent.transform.childCount, Is.LessThanOrEqualTo(TankSplashView.DefaultPoolCap));
            }
            finally
            {
                Object.DestroyImmediate(parent);
            }
        }

        private static int[] FishIds(BubbleRuntimeState bubble)
        {
            var fish = bubble.Fish;
            var ids = new int[fish.Count];
            for (var i = 0; i < fish.Count; i++)
            {
                ids[i] = fish[i].Id;
            }

            return ids;
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
