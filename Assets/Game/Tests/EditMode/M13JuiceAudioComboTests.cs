using System;
using System.Reflection;
using FishPuzzle.Core;
using FishPuzzle.Domain;
using FishPuzzle.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace FishPuzzle.Tests.EditMode
{
    public sealed class M13JuiceAudioComboTests
    {
        private const string LevelPath = "Assets/Game/Tests/Fixtures/LegacyLevel_036.asset";
        private const string ConfigPath = "Assets/Game/Data/Config/GameConfig.asset";
        private const string FishPrefabPath = "Assets/Game/Prefabs/Fish/PF_Fish.prefab";
        private const string FishCatalogPath = "Assets/Game/Data/Config/FishVisualCatalog.asset";
        private const string AudioCatalogPath = "Assets/Game/Data/Config/GameAudioCatalog.asset";
        private const string TuningPath = "Assets/Game/Data/Config/AnimationTuning.asset";

        // ---------- Combo ----------

        [Test]
        public void Combo_TierSequence_IsGoodGreatExcellentAmazingWellDone()
        {
            var combo = new ComboStreakTracker();
            var expected = new[]
            {
                ComboTier.Good, ComboTier.Great, ComboTier.Excellent, ComboTier.Amazing, ComboTier.WellDone, ComboTier.WellDone,
            };
            var labels = new[] { "GOOD!", "GREAT!", "EXCELLENT!", "AMAZING!", "WELL DONE!", "WELL DONE!" };

            for (var i = 0; i < expected.Length; i++)
            {
                var tier = combo.RegisterCompletion(10f + (i * 1.5f));
                Assert.That(tier, Is.EqualTo(expected[i]));
                Assert.That(ComboStreakTracker.Label(tier), Is.EqualTo(labels[i]));
                Assert.That(combo.Streak, Is.EqualTo(i + 1));
            }
        }

        [Test]
        public void Combo_DefaultWindowIsThreeSeconds_AndExpires()
        {
            var combo = new ComboStreakTracker();
            Assert.That(combo.Window, Is.EqualTo(3f));

            Assert.That(combo.RegisterCompletion(0f), Is.EqualTo(ComboTier.Good));
            Assert.That(combo.RegisterCompletion(3f), Is.EqualTo(ComboTier.Great), "Exactly at the window edge still chains.");
            Assert.That(combo.Expire(5.9f), Is.False);
            Assert.That(combo.Streak, Is.EqualTo(2));
            Assert.That(combo.Expire(6.01f), Is.True);
            Assert.That(combo.Streak, Is.EqualTo(0));
            Assert.That(combo.Tier, Is.EqualTo(ComboTier.None));

            Assert.That(combo.RegisterCompletion(10f), Is.EqualTo(ComboTier.Good));
            Assert.That(combo.RegisterCompletion(13.5f), Is.EqualTo(ComboTier.Good), "A late completion restarts the streak.");
            Assert.That(combo.Streak, Is.EqualTo(1));
        }

        [Test]
        public void Combo_Reset_ClearsStreak()
        {
            var combo = new ComboStreakTracker();
            combo.RegisterCompletion(1f);
            combo.RegisterCompletion(2f);
            combo.Reset();
            Assert.That(combo.Streak, Is.EqualTo(0));
            Assert.That(combo.RegisterCompletion(2.5f), Is.EqualTo(ComboTier.Good));
        }

        [Test]
        public void Combo_AudioPitchAndSize_RiseWithTier()
        {
            Assert.That(ComboStreakTracker.Pitch(ComboTier.Good), Is.EqualTo(1.00f).Within(0.001f));
            Assert.That(ComboStreakTracker.Pitch(ComboTier.Great), Is.EqualTo(1.08f).Within(0.001f));
            Assert.That(ComboStreakTracker.Pitch(ComboTier.Excellent), Is.EqualTo(1.16f).Within(0.001f));
            Assert.That(ComboStreakTracker.Pitch(ComboTier.Amazing), Is.EqualTo(1.24f).Within(0.001f));
            Assert.That(ComboStreakTracker.Pitch(ComboTier.WellDone), Is.EqualTo(1.32f).Within(0.001f));
            for (var t = 2; t <= 5; t++)
            {
                Assert.That(ComboStreakTracker.Scale((ComboTier)t), Is.GreaterThan(ComboStreakTracker.Scale((ComboTier)(t - 1))));
                Assert.That(ComboStreakTracker.BurstCount((ComboTier)t), Is.GreaterThan(ComboStreakTracker.BurstCount((ComboTier)(t - 1))));
            }
        }

        [Test]
        public void ComboView_PopsAndFades_WithoutBlockingRaycasts_BelowModals()
        {
            var canvas = new GameObject("combo-canvas", typeof(RectTransform), typeof(Canvas));
            var overlay = new GameObject("OverlayRoot", typeof(RectTransform));
            overlay.transform.SetParent(canvas.transform, false);
            try
            {
                var view = ComboFeedbackView.Create(canvas.transform, overlay.transform, null, null);
                Assert.That(view, Is.Not.Null);
                Assert.That(view.transform.GetSiblingIndex(), Is.LessThan(overlay.transform.GetSiblingIndex()));

                Assert.That(view.Show(ComboTier.Excellent, canvas.transform.position), Is.True);
                Assert.That(view.ShownCount, Is.EqualTo(1));
                Assert.That(view.LastText, Is.EqualTo("EXCELLENT!"));
                Assert.That(view.ActiveLabelCount, Is.EqualTo(1));
                Assert.That(view.ActiveSparkleCount, Is.GreaterThan(0));
                Assert.That(view.transform.GetSiblingIndex(), Is.LessThan(overlay.transform.GetSiblingIndex()));
                Assert.That(view.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
                foreach (var graphic in view.GetComponentsInChildren<Graphic>(true))
                {
                    Assert.That(graphic.raycastTarget, Is.False, graphic.name);
                }

                view.Tick(0.1f);
                view.Tick(0.1f);
                view.Tick(0.1f);
                view.Tick(0.1f);
                view.Tick(0.1f);
                view.Tick(0.1f);
                view.Tick(0.1f);
                view.Tick(0.1f);
                view.Tick(0.1f);
                view.Tick(0.1f);
                Assert.That(view.ActiveLabelCount, Is.EqualTo(0), "Text is gone in about a second.");
                Assert.That(view.ActiveSparkleCount, Is.EqualTo(0));
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }

        // ---------- Fish press highlight ----------

        [Test]
        public void HoldingFish_ActivatesHighlight_ReleaseRemovesIt()
        {
            var fish = CreateFish(out var root);
            try
            {
                var visual = VisualOf(fish);
                var sprite = visual.sprite;
                var scale = visual.rectTransform.localScale;
                var color = visual.color;
                var commits = 0;
                fish.BindInteraction(7, f => { f.BeginPress(Tuning()); return true; }, f => { commits++; f.EndPress(); }, f => f.EndPress());

                fish.OnPointerDown(Pointer(new Vector2(100f, 100f)));
                Assert.That(fish.IsPressed, Is.True);
                Assert.That(fish.IsHighlighted, Is.True);
                Assert.That(fish.Highlight.HasOutline, Is.True, "Silhouette shader should provide the white outline.");
                Assert.That(fish.Highlight.OutlineAlpha, Is.GreaterThan(0.5f));
                Assert.That(fish.Highlight.BrightenAlpha, Is.InRange(0.05f, 0.3f));
                Assert.That(fish.PressScale, Is.InRange(1.03f, 1.09f));
                AssertHighlightNeverRaycasts(fish);

                fish.OnPointerUp(Pointer(new Vector2(100f, 100f)));
                Assert.That(commits, Is.EqualTo(1));
                Assert.That(fish.IsHighlighted, Is.False);
                Assert.That(fish.IsPressed, Is.False);
                Assert.That(visual.sprite, Is.SameAs(sprite), "Fish sprite is never modified.");
                Assert.That(visual.rectTransform.localScale, Is.EqualTo(scale), "Base scale restored.");
                Assert.That(visual.color, Is.EqualTo(color), "Brightness restored (fish tint untouched).");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CancelledHold_RemovesHighlight()
        {
            var fish = CreateFish(out var root);
            try
            {
                var commits = 0;
                var cancels = 0;
                fish.BindInteraction(
                    3,
                    f => { f.BeginPress(Tuning()); return true; },
                    f => { commits++; f.EndPress(); },
                    f => { cancels++; f.EndPress(); });

                fish.OnPointerDown(Pointer(new Vector2(100f, 100f)));
                Assert.That(fish.IsHighlighted, Is.True);
                fish.OnPointerUp(Pointer(new Vector2(2000f, 2000f)));
                Assert.That(cancels, Is.EqualTo(1));
                Assert.That(commits, Is.EqualTo(0));
                Assert.That(fish.IsHighlighted, Is.False);

                fish.OnPointerDown(Pointer(new Vector2(100f, 100f)));
                Assert.That(fish.IsHighlighted, Is.True);
                fish.ReleaseInteraction();
                Assert.That(fish.IsHighlighted, Is.False, "Interaction release (input lock) also clears the highlight.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Highlight_UsesOneSharedMaterial()
        {
            var a = CreateFish(out var rootA);
            var b = CreateFish(out var rootB);
            try
            {
                a.BeginPress(Tuning());
                b.BeginPress(Tuning());
                var material = FishPressHighlight.SilhouetteMaterial;
                Assert.That(material, Is.Not.Null);
                foreach (var image in rootA.GetComponentsInChildren<Image>(true))
                {
                    if (image.name == "PressOutline" || image.name == "PressBrighten")
                    {
                        Assert.That(image.material, Is.SameAs(material));
                    }
                }

                foreach (var image in rootB.GetComponentsInChildren<Image>(true))
                {
                    if (image.name == "PressOutline" || image.name == "PressBrighten")
                    {
                        Assert.That(image.material, Is.SameAs(material));
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(rootA);
                Object.DestroyImmediate(rootB);
            }
        }

        // ---------- Trail ----------

        [Test]
        public void Trail_IntervalAndParticleRanges_MatchSpec()
        {
            var parent = new GameObject("trail-parent", typeof(RectTransform));
            try
            {
                var trail = FishTrailEmitter.Create(parent.GetComponent<RectTransform>(), ProceduralVfxSprite.Dot);
                for (var i = 0; i < 50; i++)
                {
                    Assert.That(trail.NextInterval(false), Is.InRange(0.04f, 0.07f));
                    Assert.That(trail.NextInterval(true), Is.GreaterThan(0.07f), "Bubble → Tray uses fewer particles.");
                }

                Assert.That(FishTrailEmitter.MinLifetime, Is.InRange(0.3f, 0.5f));
                Assert.That(FishTrailEmitter.MaxLifetime, Is.InRange(0.3f, 0.5f));
                Assert.That(FishTrailEmitter.MinScale, Is.EqualTo(0.5f));
                Assert.That(FishTrailEmitter.MaxScale, Is.EqualTo(1.0f));
            }
            finally
            {
                Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void Trail_IsPooledCapped_AndCannotMutateGameplay()
        {
            var session = LevelSession.Start(Load<LevelData>(LevelPath), Load<GameConfig>(ConfigPath), true);
            var before = Snapshot(session);
            var parent = new GameObject("trail-parent", typeof(RectTransform));
            try
            {
                var trail = FishTrailEmitter.Create(parent.GetComponent<RectTransform>(), ProceduralVfxSprite.Dot);
                for (var i = 0; i < 200; i++)
                {
                    trail.Emit(new Vector3(i, i * 0.5f, 0f));
                }

                Assert.That(trail.Burst(Vector3.zero, 7), Is.EqualTo(7));
                Assert.That(trail.EmitCount, Is.EqualTo(200));
                Assert.That(trail.PoolSize, Is.LessThanOrEqualTo(trail.Cap));
                Assert.That(trail.ActiveCount, Is.LessThanOrEqualTo(trail.Cap));
                Assert.That(trail.Cap, Is.LessThanOrEqualTo(FishTrailEmitter.HardCap));
                Assert.That(trail.AllRaycastsDisabled(), Is.True);
                var pooled = parent.GetComponentsInChildren<Image>(true).Length;

                for (var i = 0; i < 20; i++)
                {
                    trail.Tick(0.05f);
                }

                Assert.That(trail.ActiveCount, Is.EqualTo(0), "Particles fade out within ~0.5 s.");
                for (var i = 0; i < 30; i++)
                {
                    trail.Emit(Vector3.zero);
                }

                Assert.That(parent.GetComponentsInChildren<Image>(true).Length, Is.EqualTo(pooled), "Reuses pooled images, no new Instantiate.");
                Assert.That(Snapshot(session), Is.EqualTo(before), "Trail never touches session state.");

                var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
                foreach (var field in typeof(FishTrailEmitter).GetFields(flags))
                {
                    var ns = field.FieldType.Namespace ?? string.Empty;
                    Assert.That(ns.StartsWith("FishPuzzle.Core") || ns.StartsWith("FishPuzzle.Domain"), Is.False, field.Name);
                }
            }
            finally
            {
                Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void Trail_WithoutSprite_EmitsNothing_AndDoesNotThrow()
        {
            var parent = new GameObject("trail-parent", typeof(RectTransform));
            try
            {
                var trail = FishTrailEmitter.Create(parent.GetComponent<RectTransform>(), null);
                Assert.That(trail.Emit(Vector3.zero), Is.False);
                Assert.That(trail.Burst(Vector3.zero, 5), Is.EqualTo(0));
                Assert.That(trail.ActiveCount, Is.EqualTo(0));
            }
            finally
            {
                Object.DestroyImmediate(parent);
            }
        }

        // ---------- Audio ----------

        [Test]
        public void Audio_MissingClipsAndNoFallback_NeverThrows()
        {
            var host = new GameObject("audio-host");
            try
            {
                var audio = AudioFeedbackService.Create(host, null);
                audio.Configure(null, false);
                foreach (SfxId id in Enum.GetValues(typeof(SfxId)))
                {
                    Assert.DoesNotThrow(() => audio.Play(id));
                    Assert.That(audio.Play(id, 1.2f, 1f), Is.False);
                }

                Assert.That(audio.PlayCount(SfxId.Win), Is.GreaterThanOrEqualTo(1), "Requests are still counted.");
                Assert.That(audio.SourceCount, Is.EqualTo(AudioFeedbackService.DefaultSourceCount));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void Audio_ProceduralFallback_CoversEverySlot_SoftAndShort()
        {
            foreach (SfxId id in Enum.GetValues(typeof(SfxId)))
            {
                var clip = ProceduralSfx.Get(id);
                Assert.That(clip, Is.Not.Null, id.ToString());
                Assert.That(clip.length, Is.InRange(0.03f, 1.2f), id.ToString());
                var data = new float[clip.samples];
                clip.GetData(data, 0);
                var peak = 0f;
                for (var i = 0; i < data.Length; i++)
                {
                    peak = Mathf.Max(peak, Mathf.Abs(data[i]));
                }

                Assert.That(peak, Is.InRange(0.02f, 0.91f), id + " is audible but never clips.");
                Assert.That(ProceduralSfx.Get(id), Is.SameAs(clip), "Generated once and cached.");
            }
        }

        [Test]
        public void AudioCatalog_Asset_HasEverySlot_AndFallbackEnabled()
        {
            var catalog = Load<GameAudioCatalog>(AudioCatalogPath);
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.ProceduralFallback, Is.True);
            Assert.That(catalog.MasterVolume, Is.InRange(0.3f, 0.9f), "Avoid loud output.");
            var serialized = new SerializedObject(catalog);
            var entries = serialized.FindProperty("_entries");
            foreach (SfxId id in Enum.GetValues(typeof(SfxId)))
            {
                var found = false;
                for (var i = 0; i < entries.arraySize; i++)
                {
                    if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("Id").intValue == (int)id)
                    {
                        found = true;
                    }
                }

                Assert.That(found, Is.True, id.ToString());
            }
        }

        // ---------- helpers ----------

        private static string Snapshot(LevelSession session)
        {
            var text = session.State + "|" + session.Progress.CollectedFishCount + "|" + session.Tray.Count;
            for (var i = 0; i < session.Tanks.Count; i++)
            {
                var tank = session.Tanks[i];
                text += "|" + tank.IsUnlocked + ":" + tank.FillCount + ":" + (tank.HasTarget ? tank.CurrentTarget.ToString() : "-");
            }

            for (var i = 0; i < session.Bubbles.Count; i++)
            {
                text += "|" + session.Bubbles[i].RemainingFishCount;
            }

            return text;
        }

        private static void AssertHighlightNeverRaycasts(FishView fish)
        {
            foreach (var image in fish.GetComponentsInChildren<Image>(true))
            {
                if (image.name.StartsWith("Press"))
                {
                    Assert.That(image.raycastTarget, Is.False, image.name);
                }
            }
        }

        private static FishView CreateFish(out GameObject root)
        {
            root = new GameObject("fish-canvas", typeof(RectTransform), typeof(Canvas));
            var prefab = Load<GameObject>(FishPrefabPath);
            var instance = (GameObject)Object.Instantiate(prefab, root.transform);
            var fish = instance.GetComponent<FishView>();
            fish.BindCatalog(Load<FishVisualCatalog>(FishCatalogPath));
            fish.Show(FishType.Orange);
            return fish;
        }

        private static Image VisualOf(FishView fish)
        {
            return fish.transform.Find("Visual").GetComponent<Image>();
        }

        private static PointerEventData Pointer(Vector2 position)
        {
            return new PointerEventData(null) { position = position };
        }

        private static AnimationTuning Tuning()
        {
            return Load<AnimationTuning>(TuningPath);
        }

        private static T Load<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.That(asset, Is.Not.Null, "Missing " + path);
            return asset;
        }
    }
}
