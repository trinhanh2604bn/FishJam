using System;
using System.IO;
using System.Text;
using FishPuzzle.Domain;
using FishPuzzle.Presentation;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FishPuzzle.AssetPipeline
{
    /// <summary>
    /// Creates M1 catalogs, visual prefabs, and the asset preview scene from imported sprites.
    /// </summary>
    public static class M1VisualFoundationBuilder
    {
        private const string FishCatalogPath = "Assets/Game/Data/Config/FishVisualCatalog.asset";
        private const string ArtCatalogPath = "Assets/Game/Data/Config/GameplayArtCatalog.asset";
        private const string FishPrefabPath = "Assets/Game/Prefabs/Fish/PF_Fish.prefab";
        private const string BubblePrefabPath = "Assets/Game/Prefabs/Bubbles/PF_Bubble.prefab";
        private const string BadgePrefabPath = "Assets/Game/Prefabs/Tanks/PF_TargetBadge.prefab";
        private const string TankPrefabPath = "Assets/Game/Prefabs/Tanks/PF_TankSlot.prefab";
        private const string TrayPrefabPath = "Assets/Game/Prefabs/UI/PF_WaitingTray.prefab";
        private const string ScenePath = "Assets/Game/Scenes/Dev/M1_AssetPreview.unity";

        public static void Build()
        {
            try
            {
                TmpEssentialResources.EnsureImported();
                AssetDatabase.ImportAsset("Assets/Game/Art", ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                var fishCatalog = CreateFishCatalog();
                var artCatalog = CreateArtCatalog();
                var font = LoadFont();
                var fishPrefab = CreateFishPrefab(fishCatalog);
                var bubblePrefab = CreateBubblePrefab(artCatalog);
                var badgePrefab = CreateBadgePrefab(artCatalog, font);
                var tankPrefab = CreateTankPrefab(artCatalog);
                var trayPrefab = CreateWaitingTrayPrefab(artCatalog);
                CreatePreviewScene(artCatalog, fishCatalog, font, fishPrefab, bubblePrefab, badgePrefab, tankPrefab, trayPrefab);
                AssetDatabase.SaveAssets();
                var report = VerifyScene();
                File.WriteAllText(Path.Combine(Directory.GetParent(Application.dataPath).FullName, "SourceArt", "M1UnityReport.txt"), report);
                UnityEngine.Debug.Log(report);
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogError("[M1VisualFoundationBuilder] " + exception);
                EditorApplication.Exit(1);
            }
        }

        private static FishVisualCatalog CreateFishCatalog()
        {
            var catalog = LoadOrCreate<FishVisualCatalog>(FishCatalogPath);
            var mappings = new (FishType type, string path)[]
            {
                (FishType.Orange, "Assets/Game/Art/Fish/fish_orange.png"),
                (FishType.GreenStriped, "Assets/Game/Art/Fish/fish_green_striped.png"),
                (FishType.RedClown, "Assets/Game/Art/Fish/fish_red_clown.png"),
                (FishType.PinkStriped, "Assets/Game/Art/Fish/fish_pink_striped.png"),
                (FishType.BlackStriped, "Assets/Game/Art/Fish/fish_black_striped.png"),
                (FishType.Yellow, "Assets/Game/Art/Fish/fish_yellow.png"),
                (FishType.GreySpotted, "Assets/Game/Art/Fish/fish_grey_spotted.png"),
                (FishType.Pink, "Assets/Game/Art/Fish/fish_pink.png"),
                (FishType.Koi, "Assets/Game/Art/Fish/fish_koi_orange_white.png"),
                (FishType.Crab, "Assets/Game/Art/Creatures/creature_crab_purple.png"),
                (FishType.Snail, "Assets/Game/Art/Creatures/creature_snail_red.png"),
                (FishType.Blue, "Assets/Game/Art/Fish/fish_blue.png"),
                (FishType.YellowBlack, "Assets/Game/Art/Fish/fish_yellow_black.png")
            };

            var serialized = new SerializedObject(catalog);
            var entries = serialized.FindProperty("_entries");
            entries.ClearArray();
            for (var i = 0; i < mappings.Length; i++)
            {
                var sprite = LoadSprite(mappings[i].path);
                entries.InsertArrayElementAtIndex(i);
                var element = entries.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("_fishType").intValue = (int)mappings[i].type;
                element.FindPropertyRelative("_sprite").objectReferenceValue = sprite;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        private static GameplayArtCatalog CreateArtCatalog()
        {
            var catalog = LoadOrCreate<GameplayArtCatalog>(ArtCatalogPath);
            var serialized = new SerializedObject(catalog);
            Assign(serialized, "_gameplayBackground", "Assets/Game/Art/Backgrounds/bg_gameplay_underwater.png");
            Assign(serialized, "_bubbleBack", "Assets/Game/Art/Bubbles/bubble_back.png");
            Assign(serialized, "_bubbleFront", "Assets/Game/Art/Bubbles/bubble_front.png");
            Assign(serialized, "_bubbleFrostOverlay", "Assets/Game/Art/Bubbles/bubble_frost_overlay.png");
            Assign(serialized, "_bubblePopParticle", "Assets/Game/Art/Bubbles/bubble_pop_particle.png");
            Assign(serialized, "_smallBubbleParticle", "Assets/Game/Art/Bubbles/bubble_small_particle.png");
            Assign(serialized, "_tankBack", "Assets/Game/Art/Tanks/tank_back.png");
            Assign(serialized, "_tankFrontGlass", "Assets/Game/Art/Tanks/tank_front_glass.png");
            Assign(serialized, "_tankLockedPlus", "Assets/Game/Art/Tanks/tank_locked_plus.png");
            Assign(serialized, "_tankShelf", "Assets/Game/Art/Tanks/tank_shelf.png");
            Assign(serialized, "_targetBadgeFrame", "Assets/Game/Art/Tanks/target_badge_frame.png");
            Assign(serialized, "_levelHudPanel", "Assets/Game/Art/HUD/hud_level_panel.png");
            Assign(serialized, "_currencyCounterFrame", "Assets/Game/Art/HUD/hud_counter_frame.png");
            Assign(serialized, "_coinIcon", "Assets/Game/Art/HUD/icon_coin.png");
            Assign(serialized, "_greenPlusIcon", "Assets/Game/Art/HUD/icon_plus_green_small.png");
            Assign(serialized, "_heartIcon", "Assets/Game/Art/HUD/icon_heart_red.png");
            Assign(serialized, "_brokenHeartIcon", "Assets/Game/Art/HUD/icon_heart_broken.png");
            Assign(serialized, "_globalProgressPanel", "Assets/Game/Art/HUD/ui_global_progress_panel.png");
            Assign(serialized, "_settingsButton", "Assets/Game/Art/HUD/button_square_blue.png");
            Assign(serialized, "_gearIcon", "Assets/Game/Art/HUD/icon_gear_white.png");
            Assign(serialized, "_lockedButton", "Assets/Game/Art/HUD/button_square_locked.png");
            Assign(serialized, "_lockIcon", "Assets/Game/Art/HUD/icon_lock.png");
            serialized.FindProperty("_tankWaterHighlight").objectReferenceValue = null;
            serialized.FindProperty("_waitingTraySlot").objectReferenceValue = null;
            serialized.FindProperty("_waitingTrayShelf").objectReferenceValue = null;
            Assign(serialized, "_winBackground", "Assets/Game/Art/Backgrounds/bg_win_underwater.png");
            Assign(serialized, "_snowflakeIcon", "Assets/Game/Art/Bubbles/icon_snowflake.png");
            Assign(serialized, "_modalPanel", "Assets/Game/Art/UI/Common/modal_main_blue.png");
            Assign(serialized, "_modalHeader", "Assets/Game/Art/UI/Common/modal_header_blue.png");
            Assign(serialized, "_modalInner", "Assets/Game/Art/UI/Common/modal_inner_cream.png");
            Assign(serialized, "_buttonGreenLarge", "Assets/Game/Art/UI/Common/button_green_large.png");
            Assign(serialized, "_buttonBlueLarge", "Assets/Game/Art/UI/Common/button_blue_large.png");
            Assign(serialized, "_buttonClose", "Assets/Game/Art/UI/Common/button_close_red.png");
            Assign(serialized, "_closeIcon", "Assets/Game/Art/UI/Common/icon_x_white.png");
            Assign(serialized, "_rewardedAdIcon", "Assets/Game/Art/UI/Unlock/icon_rewarded_ad.png");
            Assign(serialized, "_successBurst", "Assets/Game/Art/VFX/vfx_success_burst.png");
            Assign(serialized, "_coinSparkle", "Assets/Game/Art/VFX/vfx_coin_sparkle.png");
            Assign(serialized, "_failFlash", "Assets/Game/Art/VFX/vfx_fail_flash.png");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            var missing = catalog.GetMissingRequiredReferences();
            if (missing.Count > 0)
            {
                throw new InvalidOperationException("GameplayArtCatalog is missing: " + string.Join(", ", missing));
            }

            return catalog;
        }

        private static GameObject CreateFishPrefab(FishVisualCatalog catalog)
        {
            return BuildPrefab(FishPrefabPath, "PF_Fish", root =>
            {
                var visual = CreateImage("Visual", root.transform, null, true);
                Stretch(visual.rectTransform);
                visual.raycastTarget = true;
                visual.preserveAspect = true;
                var view = root.AddComponent<FishView>();
                var serialized = new SerializedObject(view);
                serialized.FindProperty("_visual").objectReferenceValue = visual;
                serialized.FindProperty("_catalog").objectReferenceValue = catalog;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            });
        }

        private static GameObject CreateBubblePrefab(GameplayArtCatalog catalog)
        {
            return BuildPrefab(BubblePrefabPath, "PF_Bubble", root =>
            {
                Place(root.GetComponent<RectTransform>(), Vector2.zero, new Vector2(320f, 320f));
                var back = CreateImage("BubbleBack", root.transform, catalog.BubbleBack, true);
                Stretch(back.rectTransform);
                var fishContainer = CreateRect("FishContainer", root.transform);
                Inset(fishContainer, 78f);
                var front = CreateImage("BubbleFront", root.transform, catalog.BubbleFront, true);
                Stretch(front.rectTransform);
                CreateRect("ModifierContainer", root.transform);
                Stretch(root.transform.Find("ModifierContainer").GetComponent<RectTransform>());
                var interaction = CreateImage("InteractionArea", root.transform, null, false);
                Stretch(interaction.rectTransform);
                interaction.color = new Color(1f, 1f, 1f, 0f);
                interaction.raycastTarget = true;
            });
        }

        private static GameObject CreateBadgePrefab(GameplayArtCatalog catalog, TMP_FontAsset font)
        {
            return BuildPrefab(BadgePrefabPath, "PF_TargetBadge", root =>
            {
                Place(root.GetComponent<RectTransform>(), Vector2.zero, new Vector2(170f, 190f));
                var frame = CreateImage("Frame", root.transform, catalog.TargetBadgeFrame, true);
                Stretch(frame.rectTransform);
                var icon = CreateImage("FishIcon", root.transform, null, true);
                Place(icon.rectTransform, new Vector2(0f, 10f), new Vector2(72f, 72f));
                var textObject = CreateRect("ProgressText", root.transform);
                Place(textObject, new Vector2(0f, -42f), new Vector2(120f, 42f));
                var text = textObject.gameObject.AddComponent<TextMeshProUGUI>();
                text.font = font;
                text.text = "0/3";
                text.fontSize = 32f;
                text.alignment = TextAlignmentOptions.Center;
                text.color = new Color(0.1f, 0.27f, 0.55f, 1f);
                text.raycastTarget = false;
            });
        }

        private static GameObject CreateTankPrefab(GameplayArtCatalog catalog)
        {
            return BuildPrefab(TankPrefabPath, "PF_TankSlot", root =>
            {
                Place(root.GetComponent<RectTransform>(), Vector2.zero, new Vector2(240f, 280f));
                var back = CreateImage("TankBack", root.transform, catalog.TankBack, true);
                Stretch(back.rectTransform);
                var fishContainer = CreateRect("FishContainer", root.transform);
                Inset(fishContainer, 34f);
                CreateAnchor("FishSlot_0", fishContainer, new Vector2(-46f, -24f), new Vector2(76f, 76f));
                CreateAnchor("FishSlot_1", fishContainer, new Vector2(46f, -24f), new Vector2(76f, 76f));
                CreateAnchor("FishSlot_2", fishContainer, new Vector2(0f, 46f), new Vector2(76f, 76f));
                var front = CreateImage("TankFrontGlass", root.transform, catalog.TankFrontGlass, true);
                Stretch(front.rectTransform);
                var locked = CreateRect("LockedOverlay", root.transform);
                Stretch(locked);
                var plus = CreateImage("Plus", locked, catalog.TankLockedPlus, true);
                Stretch(plus.rectTransform);
                locked.gameObject.SetActive(false);
                var badgeAnchor = CreateRect("TargetBadgeAnchor", root.transform);
                Place(badgeAnchor, new Vector2(0f, -168f), new Vector2(150f, 24f));
            });
        }

        private static GameObject CreateWaitingTrayPrefab(GameplayArtCatalog catalog)
        {
            return BuildPrefab(TrayPrefabPath, "PF_WaitingTray", root =>
            {
                Place(root.GetComponent<RectTransform>(), Vector2.zero, new Vector2(1000f, 168f));
                // Wooden bar along the bottom. The shelf sprite has transparent padding above and below
                // the plank, so the rect is taller/wider than the visible bar and offset downwards.
                var shelf = CreateImage("Shelf", root.transform, catalog.TankShelf, false);
                var shelfRect = shelf.rectTransform;
                shelfRect.anchorMin = Vector2.zero;
                shelfRect.anchorMax = new Vector2(1f, 0f);
                shelfRect.pivot = new Vector2(0.5f, 0f);
                shelfRect.anchoredPosition = new Vector2(0f, -37f);
                shelfRect.sizeDelta = new Vector2(45f, 105f);
                shelf.raycastTarget = false;
                // Glass slots rest on top of the bar.
                var slots = CreateRect("Slots", root.transform);
                Stretch(slots);
                slots.offsetMin = new Vector2(0f, 18f);
                for (var i = 0; i < 5; i++)
                {
                    var slot = CreateImage("Slot_" + i, slots, catalog.TankFrontGlass, false);
                    var rect = slot.rectTransform;
                    rect.anchorMin = new Vector2(i / 5f, 0f);
                    rect.anchorMax = new Vector2((i + 1) / 5f, 1f);
                    // Widen into the glass sprite's transparent margin so neighbouring slots sit flush.
                    rect.offsetMin = new Vector2(-4f, 0f);
                    rect.offsetMax = new Vector2(4f, 0f);
                    slot.raycastTarget = false;
                }
            });
        }

        private static void CreatePreviewScene(
            GameplayArtCatalog artCatalog,
            FishVisualCatalog fishCatalog,
            TMP_FontAsset font,
            GameObject fishPrefab,
            GameObject bubblePrefab,
            GameObject badgePrefab,
            GameObject tankPrefab,
            GameObject trayPrefab)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraObject = new GameObject("Main Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.05f, 0.2f, 0.35f, 1f);
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            cameraObject.tag = "MainCamera";
            cameraObject.AddComponent<AudioListener>();

            var canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            eventSystem.name = "EventSystem";

            var background = CreateImage("GameplayBackground", canvasObject.transform, artCatalog.GameplayBackground, false);
            Stretch(background.rectTransform);
            background.raycastTarget = false;

            var layout = CreateRect("Layout", canvasObject.transform);
            Place(layout, Vector2.zero, new Vector2(1080f, 1920f));

            var level = CreateImage("LevelHud", layout, artCatalog.LevelHudPanel, true);
            Place(level.rectTransform, new Vector2(-250f, 840f), new Vector2(460f, 160f));
            CreateLabel(level.transform, "LevelLabel", "12", font, new Vector2(70f, 8f), new Vector2(140f, 60f), 40f, Color.white);

            var coin = CreateImage("CoinHud", layout, artCatalog.CurrencyCounterFrame, true);
            Place(coin.rectTransform, new Vector2(230f, 860f), new Vector2(320f, 110f));
            var coinIcon = CreateImage("CoinIcon", coin.transform, artCatalog.CoinIcon, true);
            Place(coinIcon.rectTransform, new Vector2(-110f, 0f), new Vector2(72f, 72f));
            CreateLabel(coin.transform, "CoinLabel", "0", font, new Vector2(30f, 0f), new Vector2(140f, 60f), 36f, new Color(0.25f, 0.18f, 0.05f, 1f));

            var heart = CreateImage("HeartHud", layout, artCatalog.HeartIcon, true);
            Place(heart.rectTransform, new Vector2(250f, 745f), new Vector2(84f, 84f));
            CreateLabel(heart.transform, "HeartLabel", "5", font, new Vector2(0f, -8f), new Vector2(70f, 40f), 28f, Color.white);

            var settings = CreateImage("SettingsButton", layout, artCatalog.SettingsButton, true);
            Place(settings.rectTransform, new Vector2(460f, 850f), new Vector2(110f, 110f));
            var gear = CreateImage("GearIcon", settings.transform, artCatalog.GearIcon, true);
            Place(gear.rectTransform, Vector2.zero, new Vector2(64f, 64f));

            var progress = CreateImage("GlobalProgress", layout, artCatalog.GlobalProgressPanel, true);
            Place(progress.rectTransform, new Vector2(-420f, 500f), new Vector2(200f, 240f));
            CreateLabel(progress.transform, "ProgressLabel", "0 / 102", font, new Vector2(0f, -20f), new Vector2(170f, 70f), 28f, new Color(0.08f, 0.25f, 0.45f, 1f));

            var shelf = CreateImage("TankShelf", layout, artCatalog.TankShelf, true);
            Place(shelf.rectTransform, new Vector2(0f, 250f), new Vector2(1020f, 110f));

            var tankPositions = new[] { -375f, -125f, 125f, 375f };
            for (var i = 0; i < tankPositions.Length; i++)
            {
                var tank = (GameObject)PrefabUtility.InstantiatePrefab(tankPrefab, layout);
                tank.name = "TankSlot_" + i;
                Place(tank.GetComponent<RectTransform>(), new Vector2(tankPositions[i], 430f), new Vector2(240f, 280f));
                var locked = i >= 2;
                tank.transform.Find("LockedOverlay").gameObject.SetActive(locked);
                if (!locked)
                {
                    continue;
                }
            }

            var activeTanks = new[] { layout.Find("TankSlot_0"), layout.Find("TankSlot_1") };
            var badgeTypes = new[] { FishType.Orange, FishType.GreenStriped };
            var badgeText = new[] { "0/3", "1/3" };
            for (var i = 0; i < activeTanks.Length; i++)
            {
                var anchor = activeTanks[i].Find("TargetBadgeAnchor");
                var badge = (GameObject)PrefabUtility.InstantiatePrefab(badgePrefab, anchor);
                badge.name = "TargetBadge_" + i;
                Place(badge.GetComponent<RectTransform>(), new Vector2(0f, -70f), new Vector2(150f, 170f));
                var icon = badge.transform.Find("FishIcon").GetComponent<Image>();
                if (!fishCatalog.TryGetSprite(badgeTypes[i], out var badgeSprite))
                {
                    throw new InvalidOperationException("Missing badge fish sprite for " + badgeTypes[i]);
                }

                icon.sprite = badgeSprite;
                badge.GetComponentInChildren<TextMeshProUGUI>(true).text = badgeText[i];
            }

            var tankFish = (GameObject)PrefabUtility.InstantiatePrefab(fishPrefab, layout.Find("TankSlot_0/FishContainer/FishSlot_0"));
            tankFish.name = "PreviewFish";
            Stretch(tankFish.GetComponent<RectTransform>());
            tankFish.GetComponent<FishView>().Show(FishType.RedClown);

            var tray = (GameObject)PrefabUtility.InstantiatePrefab(trayPrefab, layout);
            tray.name = "WaitingTray";
            Place(tray.GetComponent<RectTransform>(), new Vector2(0f, 40f), new Vector2(1000f, 168f));

            var bubbleSpots = new[]
            {
                new Vector2(-240f, -260f),
                new Vector2(20f, -80f),
                new Vector2(270f, -340f)
            };
            var bubbleFish = new[] { FishType.Orange, FishType.GreenStriped, FishType.Blue };
            for (var i = 0; i < bubbleSpots.Length; i++)
            {
                var bubble = (GameObject)PrefabUtility.InstantiatePrefab(bubblePrefab, layout);
                bubble.name = "Bubble_" + i;
                Place(bubble.GetComponent<RectTransform>(), bubbleSpots[i], new Vector2(300f, 300f));
                var fish = (GameObject)PrefabUtility.InstantiatePrefab(fishPrefab, bubble.transform.Find("FishContainer"));
                fish.name = "Fish";
                Place(fish.GetComponent<RectTransform>(), Vector2.zero, new Vector2(150f, 150f));
                fish.GetComponent<FishView>().Show(bubbleFish[i]);
            }

            EnsureFolder("Assets/Game/Scenes/Dev");
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        private static string VerifyScene()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();
            var missingScripts = 0;
            var missingSprites = new StringBuilder();
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
                            missingScripts++;
                        }
                    }

                    var image = transforms[t].GetComponent<Image>();
                    if (image == null || image.color.a <= 0f)
                    {
                        continue;
                    }

                    var requiresSprite = transforms[t].name != "Shelf"
                        && !transforms[t].name.StartsWith("Slot_", StringComparison.Ordinal);
                    if (requiresSprite && image.sprite == null)
                    {
                        missingSprites.AppendLine(GetPath(transforms[t]));
                    }
                }
            }

            if (missingScripts > 0 || missingSprites.Length > 0)
            {
                throw new InvalidOperationException(
                    "Preview scene validation failed. Missing scripts: " + missingScripts + "\n" + missingSprites);
            }

            return "M1_SCENE_OPENED\nScene: " + ScenePath + "\nMissing scripts: 0\nCritical image sprites assigned.\n";
        }

        private static GameObject BuildPrefab(string path, string name, Action<GameObject> build)
        {
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }

            var root = new GameObject(name, typeof(RectTransform));
            try
            {
                build(root);
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                if (prefab == null)
                {
                    throw new InvalidOperationException("Failed to save prefab " + path);
                }

                return prefab;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                return asset;
            }

            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void Assign(SerializedObject serialized, string propertyName, string spritePath)
        {
            serialized.FindProperty(propertyName).objectReferenceValue = LoadSprite(spritePath);
        }

        private static Sprite LoadSprite(string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null)
            {
                return sprite;
            }

            var assets = AssetDatabase.LoadAllAssetsAtPath(path);
            for (var i = 0; i < assets.Length; i++)
            {
                if (assets[i] is Sprite loaded)
                {
                    return loaded;
                }
            }

            throw new FileNotFoundException("Sprite was not imported at " + path);
        }

        private static TMP_FontAsset LoadFont()
        {
            var guids = AssetDatabase.FindAssets("LiberationSans SDF t:TMP_FontAsset");
            if (guids.Length == 0)
            {
                throw new FileNotFoundException("LiberationSans SDF font asset is missing.");
            }

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guids[0]));
            if (font == null)
            {
                throw new FileNotFoundException("LiberationSans SDF could not be loaded.");
            }

            return font;
        }

        private static Image CreateImage(string name, Transform parent, Sprite sprite, bool preserveAspect)
        {
            var rect = CreateRect(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = preserveAspect;
            image.raycastTarget = false;
            image.color = Color.white;
            return image;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            return child.GetComponent<RectTransform>();
        }

        private static RectTransform CreateAnchor(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = CreateRect(name, parent);
            Place(rect, position, size);
            return rect;
        }

        private static TextMeshProUGUI CreateLabel(Transform parent, string name, string text, TMP_FontAsset font, Vector2 position, Vector2 size, float fontSize, Color color)
        {
            var rect = CreateRect(name, parent);
            Place(rect, position, size);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.text = text;
            label.fontSize = fontSize;
            label.alignment = TextAlignmentOptions.Center;
            label.color = color;
            label.raycastTarget = false;
            return label;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void Inset(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void EnsureFolder(string path)
        {
            if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            var folderName = Path.GetFileName(path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folderName);
        }

        private static string GetPath(Transform transform)
        {
            var path = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = transform.name + "/" + path;
            }

            return path;
        }
    }
}
