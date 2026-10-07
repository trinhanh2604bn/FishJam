using FishPuzzle.Domain;
using FishPuzzle.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FishPuzzle.Tests.EditMode
{
    public sealed class M81TouchFeelTests
    {
        private const string TuningPath = "Assets/Game/Data/Config/AnimationTuning.asset";
        private const string ArtPath = "Assets/Game/Data/Config/GameplayArtCatalog.asset";

        [Test]
        public void TuningAsset_UsesTheSlowerFeelRanges()
        {
            var tuning = AssetDatabase.LoadAssetAtPath<AnimationTuning>(TuningPath);

            Assert.That(tuning.FishRouteBaseDuration, Is.InRange(0.38f, 0.46f));
            Assert.That(tuning.FishRouteDuration, Is.EqualTo(tuning.FishRouteBaseDuration + 0.2f).Within(0.0001f));
            Assert.That(tuning.FishLandingBounceDuration, Is.InRange(0.08f, 0.10f));
            Assert.That(tuning.TankResolveDuration, Is.InRange(0.16f, 0.20f));
            Assert.That(tuning.TankTargetSwapDuration, Is.InRange(0.10f, 0.14f));
            Assert.That(tuning.TrayAutoMoveDuration, Is.InRange(0.18f, 0.22f));
            Assert.That(tuning.BubbleFishReflowDuration, Is.InRange(0.13f, 0.16f));
            Assert.That(tuning.BubblePopDuration, Is.InRange(0.10f, 0.16f));
            Assert.That(tuning.BubbleFallDuration, Is.InRange(0.28f, 0.36f));
            Assert.That(tuning.BubbleSlideDuration, Is.InRange(0.26f, 0.34f));
            Assert.That(tuning.BubbleTopSpawnDuration, Is.InRange(0.30f, 0.38f));
            Assert.That(tuning.TouchRippleDuration, Is.InRange(0.30f, 0.45f));
            Assert.That(tuning.FishPressLift, Is.InRange(6f, 10f));
            Assert.That(tuning.FishPressScale, Is.InRange(1.04f, 1.07f));
            Assert.That(tuning.FishPressWiggleDegrees, Is.InRange(0f, 2f));
        }

        [Test]
        public void ArtCatalog_AssignsRippleSplashAndDroplet()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<GameplayArtCatalog>(ArtPath);

            Assert.That(catalog.TouchRipple, Is.Not.Null);
            Assert.That(catalog.TankSplash, Is.Not.Null);
            Assert.That(catalog.SplashDroplet, Is.Not.Null);
            Assert.That(catalog.SmallBubbleParticle, Is.Not.Null);
        }

        [Test]
        public void FishPress_LiftsAndWiggles_ThenRestores()
        {
            var host = new GameObject("fish", typeof(RectTransform), typeof(FishPressFeedback));
            var visualObject = new GameObject("visual", typeof(RectTransform));
            var visual = visualObject.GetComponent<RectTransform>();
            visual.SetParent(host.transform, false);
            var feedback = host.GetComponent<FishPressFeedback>();
            try
            {
                feedback.Begin(visual, AnimationTuning.RuntimeDefault());

                Assert.That(feedback.IsPressed, Is.True);
                Assert.That(feedback.Lift, Is.InRange(6f, 10f));
                Assert.That(feedback.Scale, Is.InRange(1.04f, 1.07f));

                feedback.Tick(0.1f);

                Assert.That(Mathf.Abs(feedback.Angle), Is.GreaterThan(0.2f).And.LessThanOrEqualTo(2.05f));
                Assert.That(feedback.Lift, Is.InRange(6f, 11f));

                feedback.End();

                Assert.That(feedback.IsPressed, Is.False);
                Assert.That(feedback.Lift, Is.EqualTo(0f));
                Assert.That(visual.anchoredPosition.y, Is.EqualTo(0f).Within(0.01f));
                Assert.That(visual.localScale.x, Is.EqualTo(1f).Within(0.01f));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void FishIdle_SwaysLikeAPress_WithoutMovingTheRoot()
        {
            var host = new GameObject("fish", typeof(RectTransform), typeof(FishIdleSway));
            var visualObject = new GameObject("visual", typeof(RectTransform));
            var visual = visualObject.GetComponent<RectTransform>();
            visual.SetParent(host.transform, false);
            var idle = host.GetComponent<FishIdleSway>();
            var root = host.GetComponent<RectTransform>();
            try
            {
                var rootPosition = root.anchoredPosition;
                idle.Play(visual, AnimationTuning.RuntimeDefault());

                var maxAngle = 0f;
                for (var i = 0; i < 12; i++)
                {
                    idle.Tick(0.05f);
                    maxAngle = Mathf.Max(maxAngle, Mathf.Abs(idle.Angle));
                }

                Assert.That(idle.IsPlaying, Is.True);
                Assert.That(maxAngle, Is.GreaterThan(1.4f).And.LessThanOrEqualTo(2.05f));
                Assert.That(Mathf.Abs(visual.anchoredPosition.y), Is.LessThanOrEqualTo(1.6f));
                Assert.That(root.anchoredPosition, Is.EqualTo(rootPosition));

                idle.Pause();

                Assert.That(idle.IsPlaying, Is.False);
                Assert.That(idle.Angle, Is.EqualTo(0f));
                Assert.That(visual.anchoredPosition.y, Is.EqualTo(0f).Within(0.01f));
                Assert.That(visual.localEulerAngles.z, Is.EqualTo(0f).Within(0.01f));
                Assert.That(root.anchoredPosition, Is.EqualTo(rootPosition));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void TouchFeedback_RipplesAndBubbles_ThenSuppressCreatesNothing()
        {
            var host = new GameObject("touch-layer", typeof(RectTransform));
            var touch = TouchFeedbackController.Create(host.transform, null, AnimationTuning.RuntimeDefault());
            try
            {
                touch.PresentAtLocal(new Vector2(12f, 24f));

                Assert.That(touch.IsHolding, Is.True);
                Assert.That(touch.ActiveRippleCount, Is.EqualTo(1));
                Assert.That(touch.ActiveBubbleCount, Is.GreaterThan(0).And.LessThanOrEqualTo(6));

                var before = touch.ActiveBubbleCount;
                touch.Tick(0.2f);
                Assert.That(touch.ActiveBubbleCount, Is.GreaterThanOrEqualTo(before).And.LessThanOrEqualTo(6));

                touch.PresentPointerUp();
                var held = touch.ActiveBubbleCount;
                touch.Tick(0.05f);

                Assert.That(touch.IsHolding, Is.False);
                Assert.That(touch.ActiveBubbleCount, Is.LessThanOrEqualTo(held));

                touch.Suppress();
                touch.PresentAtLocal(Vector2.zero);

                Assert.That(touch.ActiveRippleCount, Is.EqualTo(0));
                Assert.That(touch.ActiveBubbleCount, Is.EqualTo(0));
                Assert.That(touch.IsHolding, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void TankSplash_MissingArtDoesNotCreateObjects()
        {
            Assert.That(TankSplashView.Play(null, Vector3.zero, null, null, null, false), Is.Null);

            var parent = new GameObject("splash-parent", typeof(RectTransform));
            try
            {
                var missing = TankSplashView.Play(parent.GetComponent<RectTransform>(), Vector3.zero, null, null, null, false);

                Assert.That(missing, Is.Null);
                Assert.That(parent.transform.childCount, Is.EqualTo(0));

                var procedural = TankSplashView.Play(parent.GetComponent<RectTransform>(), Vector3.zero, null, null, null, true);
                Assert.That(procedural, Is.Not.Null);
                Assert.That(parent.transform.childCount, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(parent);
            }
        }
    }
}
