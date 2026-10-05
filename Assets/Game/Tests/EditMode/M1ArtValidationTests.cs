using System;
using System.Text.RegularExpressions;
using FishPuzzle.Domain;
using FishPuzzle.Presentation;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine.TestTools;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace FishPuzzle.Tests.EditMode
{
    public sealed class M1ArtValidationTests
    {
        private const string FishCatalogPath = "Assets/Game/Data/Config/FishVisualCatalog.asset";
        private const string ArtCatalogPath = "Assets/Game/Data/Config/GameplayArtCatalog.asset";
        private const string ScenePath = "Assets/Game/Scenes/Dev/M1_AssetPreview.unity";

        [Test]
        public void FishVisualCatalog_HasNoDuplicateFishTypes()
        {
            var catalog = LoadFishCatalog();
            Assert.That(catalog.HasDuplicateTypes, Is.False, "Duplicates: " + string.Join(", ", catalog.GetDuplicateTypes()));
        }

        [Test]
        public void FishVisualCatalog_MapsEveryRequiredFishType()
        {
            var catalog = LoadFishCatalog();
            var missing = catalog.GetMissingRequiredTypes();
            Assert.That(missing, Is.Empty, "Missing FishType sprites: " + string.Join(", ", missing));
            Assert.That(catalog.Entries.Count, Is.EqualTo(Enum.GetValues(typeof(FishType)).Length));
            AssertSprite(catalog, FishType.Orange, "Assets/Game/Art/Fish/fish_orange.png");
            AssertSprite(catalog, FishType.GreenStriped, "Assets/Game/Art/Fish/fish_green_striped.png");
            AssertSprite(catalog, FishType.RedClown, "Assets/Game/Art/Fish/fish_red_clown.png");
            AssertSprite(catalog, FishType.PinkStriped, "Assets/Game/Art/Fish/fish_pink_striped.png");
            AssertSprite(catalog, FishType.BlackStriped, "Assets/Game/Art/Fish/fish_black_striped.png");
            AssertSprite(catalog, FishType.Yellow, "Assets/Game/Art/Fish/fish_yellow.png");
            AssertSprite(catalog, FishType.GreySpotted, "Assets/Game/Art/Fish/fish_grey_spotted.png");
            AssertSprite(catalog, FishType.Pink, "Assets/Game/Art/Fish/fish_pink.png");
            AssertSprite(catalog, FishType.Koi, "Assets/Game/Art/Fish/fish_koi_orange_white.png");
            AssertSprite(catalog, FishType.Blue, "Assets/Game/Art/Fish/fish_blue.png");
            AssertSprite(catalog, FishType.YellowBlack, "Assets/Game/Art/Fish/fish_yellow_black.png");
            AssertSprite(catalog, FishType.Crab, "Assets/Game/Art/Creatures/creature_crab_purple.png");
            AssertSprite(catalog, FishType.Snail, "Assets/Game/Art/Creatures/creature_snail_red.png");
        }

        [Test]
        public void FishVisualCatalog_RejectsDuplicateEntries()
        {
            var catalog = ScriptableObject.CreateInstance<FishVisualCatalog>();
            try
            {
                var serialized = new SerializedObject(catalog);
                var entries = serialized.FindProperty("_entries");
                AddEntry(entries, 0, FishType.Orange);
                AddEntry(entries, 1, FishType.Orange);
                LogAssert.Expect(LogType.Error, new Regex("Duplicate FishType mappings"));
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(catalog.HasDuplicateTypes, Is.True);
                Assert.That(catalog.TryGetSprite(FishType.Orange, out _), Is.False);
                Assert.That(catalog.GetMissingRequiredTypes(), Does.Contain(FishType.Orange));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void GameplayArtCatalog_RequiredReferencesAreAssigned()
        {
            var catalog = LoadArtCatalog();
            var missing = catalog.GetMissingRequiredReferences();
            Assert.That(missing, Is.Empty, "Missing required art: " + string.Join(", ", missing));
            Assert.That(catalog.BubbleBack, Is.Not.Null);
            Assert.That(catalog.BubbleFront, Is.Not.Null);
            Assert.That(catalog.TankBack, Is.Not.Null);
            Assert.That(catalog.TankFrontGlass, Is.Not.Null);
            Assert.That(catalog.TargetBadgeFrame, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(catalog.BubbleBack), Is.EqualTo("Assets/Game/Art/Bubbles/bubble_back.png"));
            Assert.That(AssetDatabase.GetAssetPath(catalog.BubbleFront), Is.EqualTo("Assets/Game/Art/Bubbles/bubble_front.png"));
            Assert.That(AssetDatabase.GetAssetPath(catalog.TankBack), Is.EqualTo("Assets/Game/Art/Tanks/tank_back.png"));
            Assert.That(AssetDatabase.GetAssetPath(catalog.TankFrontGlass), Is.EqualTo("Assets/Game/Art/Tanks/tank_front_glass.png"));
            Assert.That(AssetDatabase.GetAssetPath(catalog.TargetBadgeFrame), Is.EqualTo("Assets/Game/Art/Tanks/target_badge_frame.png"));
        }

        [Test]
        public void GameplayArtCatalog_OptionalTrayAndWaterHighlightStayUnassigned()
        {
            var catalog = LoadArtCatalog();
            Assert.That(catalog.TankWaterHighlight, Is.Null);
            Assert.That(catalog.WaitingTraySlot, Is.Null);
            Assert.That(catalog.WaitingTrayShelf, Is.Null);
            Assert.That(catalog.WinBackground, Is.Not.Null);
            Assert.That(catalog.SnowflakeIcon, Is.Not.Null);
        }

        [Test]
        public void FishAndCreatureTextures_AreSpritesWithoutMipMaps()
        {
            AssertSpriteImportSettings("Assets/Game/Art/Fish");
            AssertSpriteImportSettings("Assets/Game/Art/Creatures");
        }

        [Test]
        public void BubbleTextures_HaveMipMapsDisabled()
        {
            AssertSpriteImportSettings("Assets/Game/Art/Bubbles");
        }

        [Test]
        public void BubblePrefab_SandwichesFishBetweenBackAndFront()
        {
            var prefab = LoadPrefab("Assets/Game/Prefabs/Bubbles/PF_Bubble.prefab");
            var back = prefab.transform.Find("BubbleBack");
            var fish = prefab.transform.Find("FishContainer");
            var front = prefab.transform.Find("BubbleFront");
            Assert.That(back, Is.Not.Null);
            Assert.That(fish, Is.Not.Null);
            Assert.That(front, Is.Not.Null);
            Assert.That(prefab.transform.Find("ModifierContainer"), Is.Not.Null);
            Assert.That(prefab.transform.Find("InteractionArea"), Is.Not.Null);
            Assert.That(back.GetSiblingIndex(), Is.LessThan(fish.GetSiblingIndex()));
            Assert.That(fish.GetSiblingIndex(), Is.LessThan(front.GetSiblingIndex()));
            Assert.That(AssetDatabase.GetAssetPath(back.GetComponent<Image>().sprite), Is.EqualTo("Assets/Game/Art/Bubbles/bubble_back.png"));
            Assert.That(AssetDatabase.GetAssetPath(front.GetComponent<Image>().sprite), Is.EqualTo("Assets/Game/Art/Bubbles/bubble_front.png"));
            AssertNoMissingScripts(prefab);
        }

        [Test]
        public void TankSlotPrefab_SandwichesFishBetweenBackAndGlass()
        {
            var prefab = LoadPrefab("Assets/Game/Prefabs/Tanks/PF_TankSlot.prefab");
            var back = prefab.transform.Find("TankBack");
            var fish = prefab.transform.Find("FishContainer");
            var glass = prefab.transform.Find("TankFrontGlass");
            var locked = prefab.transform.Find("LockedOverlay");
            Assert.That(back, Is.Not.Null);
            Assert.That(fish, Is.Not.Null);
            Assert.That(glass, Is.Not.Null);
            Assert.That(fish.Find("FishSlot_0"), Is.Not.Null);
            Assert.That(fish.Find("FishSlot_1"), Is.Not.Null);
            Assert.That(fish.Find("FishSlot_2"), Is.Not.Null);
            Assert.That(locked.Find("Plus"), Is.Not.Null);
            Assert.That(prefab.transform.Find("TargetBadgeAnchor"), Is.Not.Null);
            Assert.That(back.GetSiblingIndex(), Is.LessThan(fish.GetSiblingIndex()));
            Assert.That(fish.GetSiblingIndex(), Is.LessThan(glass.GetSiblingIndex()));
            Assert.That(AssetDatabase.GetAssetPath(back.GetComponent<Image>().sprite), Is.EqualTo("Assets/Game/Art/Tanks/tank_back.png"));
            Assert.That(AssetDatabase.GetAssetPath(glass.GetComponent<Image>().sprite), Is.EqualTo("Assets/Game/Art/Tanks/tank_front_glass.png"));
            Assert.That(AssetDatabase.GetAssetPath(locked.Find("Plus").GetComponent<Image>().sprite), Is.EqualTo("Assets/Game/Art/Tanks/tank_locked_plus.png"));
            AssertNoMissingScripts(prefab);
        }

        [Test]
        public void TargetBadgePrefab_UsesDynamicProgressText()
        {
            var prefab = LoadPrefab("Assets/Game/Prefabs/Tanks/PF_TargetBadge.prefab");
            var frame = prefab.transform.Find("Frame").GetComponent<Image>();
            var text = prefab.GetComponentInChildren<TextMeshProUGUI>(true);
            Assert.That(AssetDatabase.GetAssetPath(frame.sprite), Is.EqualTo("Assets/Game/Art/Tanks/target_badge_frame.png"));
            Assert.That(prefab.transform.Find("FishIcon"), Is.Not.Null);
            Assert.That(text, Is.Not.Null);
            Assert.That(text.text, Is.EqualTo("0/3"));
            AssertNoMissingScripts(prefab);
        }

        [Test]
        public void WaitingTrayPrefab_HasExactlyFiveSlots()
        {
            var prefab = LoadPrefab("Assets/Game/Prefabs/UI/PF_WaitingTray.prefab");
            var slots = prefab.transform.Find("Slots");
            Assert.That(prefab.transform.Find("Shelf"), Is.Not.Null);
            Assert.That(slots.childCount, Is.EqualTo(5));
            for (var i = 0; i < 5; i++)
            {
                Assert.That(slots.Find("Slot_" + i), Is.Not.Null);
            }

            AssertNoMissingScripts(prefab);
        }

        [Test]
        public void FishPrefab_HasVisualAndFishView()
        {
            var prefab = LoadPrefab("Assets/Game/Prefabs/Fish/PF_Fish.prefab");
            Assert.That(prefab.transform.Find("Visual").GetComponent<Image>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<FishView>(), Is.Not.Null);
            AssertNoMissingScripts(prefab);
        }

        [Test]
        public void PreviewScene_OpensWithRealSpritesAndNoMissingScripts()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Assert.That(scene.IsValid(), Is.True);
            var missing = 0;
            var fishSprites = 0;
            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                var transforms = roots[i].GetComponentsInChildren<Transform>(true);
                for (var t = 0; t < transforms.Length; t++)
                {
                    var components = transforms[t].GetComponents<Component>();
                    for (var c = 0; c < components.Length; c++)
                    {
                        if (components[c] == null)
                        {
                            missing++;
                        }
                    }
                }
            }

            var background = GameObject.Find("GameplayBackground").GetComponent<Image>();
            Assert.That(AssetDatabase.GetAssetPath(background.sprite), Is.EqualTo("Assets/Game/Art/Backgrounds/bg_gameplay_underwater.png"));
            var fishes = UnityEngine.Object.FindObjectsByType<FishView>(FindObjectsInactive.Include);
            Assert.That(fishes.Length, Is.GreaterThanOrEqualTo(4));
            for (var i = 0; i < fishes.Length; i++)
            {
                var image = fishes[i].GetComponentInChildren<Image>(true);
                Assert.That(image.sprite, Is.Not.Null, fishes[i].name);
                fishSprites++;
            }

            Assert.That(GameObject.Find("WaitingTray").transform.Find("Slots").childCount, Is.EqualTo(5));
            Assert.That(GameObject.Find("TankSlot_2").transform.Find("LockedOverlay").gameObject.activeSelf, Is.True);
            Assert.That(GameObject.Find("TankSlot_0").transform.Find("LockedOverlay").gameObject.activeSelf, Is.False);
            Assert.That(missing, Is.EqualTo(0));
            Assert.That(fishSprites, Is.GreaterThanOrEqualTo(4));
        }

        private static void AssertSprite(FishVisualCatalog catalog, FishType fishType, string assetPath)
        {
            Assert.That(catalog.TryGetSprite(fishType, out var sprite), Is.True, fishType.ToString());
            Assert.That(sprite, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(sprite), Is.EqualTo(assetPath));
        }

        private static void AssertSpriteImportSettings(string folder)
        {
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
            Assert.That(guids.Length, Is.GreaterThan(0), folder);
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                Assert.That(importer, Is.Not.Null, path);
                Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite), path);
                Assert.That(importer.mipmapEnabled, Is.False, path);
                Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single), path);
                Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp), path);
            }
        }

        private static void AssertNoMissingScripts(GameObject prefab)
        {
            var transforms = prefab.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < transforms.Length; i++)
            {
                var components = transforms[i].GetComponents<Component>();
                for (var c = 0; c < components.Length; c++)
                {
                    Assert.That(components[c], Is.Not.Null, prefab.name + "/" + transforms[i].name);
                }
            }
        }

        private static void AddEntry(SerializedProperty entries, int index, FishType fishType)
        {
            entries.InsertArrayElementAtIndex(index);
            var element = entries.GetArrayElementAtIndex(index);
            element.FindPropertyRelative("_fishType").intValue = (int)fishType;
            element.FindPropertyRelative("_sprite").objectReferenceValue = null;
        }

        private static FishVisualCatalog LoadFishCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<FishVisualCatalog>(FishCatalogPath);
            Assert.That(catalog, Is.Not.Null, FishCatalogPath);
            return catalog;
        }

        private static GameplayArtCatalog LoadArtCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<GameplayArtCatalog>(ArtCatalogPath);
            Assert.That(catalog, Is.Not.Null, ArtCatalogPath);
            return catalog;
        }

        private static GameObject LoadPrefab(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null, path);
            return prefab;
        }
    }
}
