using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using FishPuzzle.Core;
using FishPuzzle.Domain;
using FishPuzzle.Presentation;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FishPuzzle.EditorTools
{
    /// <summary>
    /// Builds Gameplay.unity from M1 prefabs and Level_001.
    /// Batch entry point: FishPuzzle.EditorTools.M3GameplaySceneBuilder.BuildAndExit
    /// </summary>
    public static class M3GameplaySceneBuilder
    {
        public const string ScenePath = "Assets/Game/Scenes/Gameplay.unity";
        private const string PreviewScenePath = "Assets/Game/Scenes/Dev/M1_AssetPreview.unity";
        private const string LevelPath = "Assets/Game/Data/Levels/Level_001.asset";
        private const string ConfigPath = "Assets/Game/Data/Config/GameConfig.asset";
        private const string FishCatalogPath = "Assets/Game/Data/Config/FishVisualCatalog.asset";
        private const string ArtCatalogPath = "Assets/Game/Data/Config/GameplayArtCatalog.asset";
        private const string FishPrefabPath = "Assets/Game/Prefabs/Fish/PF_Fish.prefab";
        private const string BubblePrefabPath = "Assets/Game/Prefabs/Bubbles/PF_Bubble.prefab";
        private const string BadgePrefabPath = "Assets/Game/Prefabs/Tanks/PF_TargetBadge.prefab";
        private const string TankPrefabPath = "Assets/Game/Prefabs/Tanks/PF_TankSlot.prefab";
        private const string TrayPrefabPath = "Assets/Game/Prefabs/UI/PF_WaitingTray.prefab";

        public static void BuildAndExit()
        {
            var exitCode = 1;
            try
            {
                exitCode = Build() ? 0 : 1;
            }
            catch (Exception exception)
            {
                Debug.LogError("[M3GameplayScene] " + exception);
                exitCode = 1;
            }

            EditorApplication.Exit(exitCode);
        }

        public static bool Build()
        {
            var report = new StringBuilder();
            ExtendPrefabs();
            var sceneBytesBefore = new FileInfo(ToAbsolute(ScenePath)).Exists ? new FileInfo(ToAbsolute(ScenePath)).Length : 0;
            CreateScene();
            var savedBytes = new FileInfo(ToAbsolute(ScenePath)).Length;
            UpdateBuildSettings();
            var presentationOk = PreviewPresentation(report);
            var savedBytesAfter = new FileInfo(ToAbsolute(ScenePath)).Length;
            var sceneUnchanged = savedBytes == savedBytesAfter;
            report.AppendLine("SavedSceneBytes=" + savedBytes);
            report.AppendLine("SceneFileUnchangedAfterPreview=" + sceneUnchanged);
            report.AppendLine("SceneExistedBefore=" + (sceneBytesBefore > 0));
            WriteReport(report);
            var ok = presentationOk && sceneUnchanged;
            if (ok)
            {
                Debug.Log("[M3GameplayScene] PASS\n" + report);
            }
            else
            {
                Debug.LogError("[M3GameplayScene] FAIL\n" + report);
            }

            return ok;
        }

        private static void ExtendPrefabs()
        {
            EditPrefab(TankPrefabPath, root =>
            {
                var view = GetOrAdd<TankSlotView>(root);
                var serialized = new SerializedObject(view);
                serialized.FindProperty("_lockedOverlay").objectReferenceValue = root.transform.Find("LockedOverlay").gameObject;
                serialized.FindProperty("_badgeAnchor").objectReferenceValue = root.transform.Find("TargetBadgeAnchor");
                serialized.ApplyModifiedPropertiesWithoutUndo();
            });

            EditPrefab(BadgePrefabPath, root =>
            {
                var view = GetOrAdd<TargetBadgeView>(root);
                var serialized = new SerializedObject(view);
                serialized.FindProperty("_fishIcon").objectReferenceValue = root.transform.Find("FishIcon").GetComponent<Image>();
                serialized.FindProperty("_progressText").objectReferenceValue = root.GetComponentInChildren<TextMeshProUGUI>(true);
                serialized.ApplyModifiedPropertiesWithoutUndo();
            });

            EditPrefab(BubblePrefabPath, root =>
            {
                var layout = GetOrAdd<BubbleFishLayoutController>(root);
                var view = GetOrAdd<BubbleView>(root);
                var serialized = new SerializedObject(view);
                serialized.FindProperty("_fishContainer").objectReferenceValue = root.transform.Find("FishContainer");
                serialized.FindProperty("_fishLayout").objectReferenceValue = layout;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            });

            EditPrefab(TrayPrefabPath, root =>
            {
                var view = GetOrAdd<WaitingTrayView>(root);
                var serialized = new SerializedObject(view);
                serialized.FindProperty("_slotsRoot").objectReferenceValue = root.transform.Find("Slots");
                serialized.ApplyModifiedPropertiesWithoutUndo();
            });
        }

        private static void CreateScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var level = Load<LevelData>(LevelPath);
            var config = Load<GameConfig>(ConfigPath);
            var fishCatalog = Load<FishVisualCatalog>(FishCatalogPath);
            var art = Load<GameplayArtCatalog>(ArtCatalogPath);
            var fishPrefab = Load<GameObject>(FishPrefabPath);
            var bubblePrefab = Load<GameObject>(BubblePrefabPath);
            var badgePrefab = Load<GameObject>(BadgePrefabPath);
            var tankPrefab = Load<GameObject>(TankPrefabPath);
            var trayPrefab = Load<GameObject>(TrayPrefabPath);
            var font = LoadFont();
            var root = new GameObject("Gameplay");

            var cameraObject = new GameObject("Main Camera");
            cameraObject.transform.SetParent(root.transform, false);
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.04f, 0.16f, 0.28f, 1f);
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            cameraObject.tag = "MainCamera";
            cameraObject.AddComponent<AudioListener>();

            var systems = new GameObject("Systems");
            systems.transform.SetParent(root.transform, false);
            var bootstrapObject = new GameObject("LevelSceneBootstrapper");
            bootstrapObject.transform.SetParent(systems.transform, false);
            var references = bootstrapObject.AddComponent<GameplaySceneReferences>();
            var bootstrap = bootstrapObject.AddComponent<LevelSceneBootstrapper>();

            var canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(root.transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1
                | AdditionalCanvasShaderChannels.Normal
                | AdditionalCanvasShaderChannels.Tangent;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            var background = CreateImage("Background", canvasObject.transform, art.GameplayBackground, false);
            Stretch(background.rectTransform);
            background.color = Color.white;

            var safeAreaObject = CreateRect("SafeArea", canvasObject.transform);
            Stretch(safeAreaObject);
            safeAreaObject.gameObject.AddComponent<SafeAreaFitter>();

            var hud = CreateRect("HUD", safeAreaObject);
            Stretch(hud);
            var levelPanel = CreateImage("LevelPanel", hud, art.LevelHudPanel, true);
            Place(levelPanel.rectTransform, new Vector2(-340f, 860f), new Vector2(320f, 140f));
            var levelLabel = CreateLabel(levelPanel.transform, "LevelLabel", "1", font, new Vector2(24f, 0f), new Vector2(180f, 70f), 54f, Color.white);

            var goldPanel = CreateImage("GoldPanel", hud, art.CurrencyCounterFrame, true);
            Place(goldPanel.rectTransform, new Vector2(150f, 870f), new Vector2(300f, 100f));
            var coin = CreateImage("CoinIcon", goldPanel.transform, art.CoinIcon, true);
            Place(coin.rectTransform, new Vector2(-100f, 0f), new Vector2(64f, 64f));
            var goldLabel = CreateLabel(goldPanel.transform, "GoldLabel", "0", font, new Vector2(28f, 0f), new Vector2(140f, 60f), 36f, new Color(0.25f, 0.16f, 0.05f, 1f));

            var livesPanel = CreateImage("LivesPanel", hud, art.HeartIcon, true);
            Place(livesPanel.rectTransform, new Vector2(390f, 870f), new Vector2(92f, 92f));
            var livesLabel = CreateLabel(livesPanel.transform, "LivesLabel", "5", font, new Vector2(0f, -6f), new Vector2(70f, 46f), 30f, Color.white);

            var tankBoardObject = CreateRect("TankBoard", safeAreaObject);
            Place(tankBoardObject, new Vector2(0f, 690f), new Vector2(1040f, 360f));
            var tankBoard = tankBoardObject.gameObject.AddComponent<TankBoardView>();
            var shelf = CreateImage("Shelf", tankBoardObject, art.TankShelf, true);
            Place(shelf.rectTransform, new Vector2(0f, -130f), new Vector2(1020f, 90f));
            var tankSlots = CreateRect("Slots", tankBoardObject);
            Stretch(tankSlots);
            var tankSerialized = new SerializedObject(tankBoard);
            tankSerialized.FindProperty("_slotRoot").objectReferenceValue = tankSlots;
            tankSerialized.FindProperty("_horizontalSpacing").floatValue = 250f;
            tankSerialized.ApplyModifiedPropertiesWithoutUndo();

            var progressObject = CreateImage("GlobalProgress", safeAreaObject, art.GlobalProgressPanel, true);
            Place(progressObject.rectTransform, new Vector2(-400f, 310f), new Vector2(210f, 190f));
            var progressLabel = CreateLabel(progressObject.transform, "ProgressLabel", "0 / 36", font, new Vector2(0f, -18f), new Vector2(180f, 70f), 32f, new Color(0.08f, 0.25f, 0.45f, 1f));

            var trayObject = (GameObject)PrefabUtility.InstantiatePrefab(trayPrefab, safeAreaObject);
            trayObject.name = "WaitingTray";
            Place(trayObject.GetComponent<RectTransform>(), new Vector2(70f, 200f), new Vector2(860f, 150f));
            var waitingTray = trayObject.GetComponent<WaitingTrayView>();

            var bubbleField = CreateRect("BubbleField", safeAreaObject);
            Stretch(bubbleField);
            var bubblePile = bubbleField.gameObject.AddComponent<BubblePileView>();
            var pileSerialized = new SerializedObject(bubblePile);
            pileSerialized.FindProperty("_slotRoot").objectReferenceValue = bubbleField;
            pileSerialized.ApplyModifiedPropertiesWithoutUndo();

            var bottom = CreateRect("BottomReserve", safeAreaObject);
            Place(bottom, new Vector2(0f, -890f), new Vector2(1000f, 110f));
            var settings = CreateImage("SettingsButton", bottom, art.SettingsButton, true);
            Place(settings.rectTransform, new Vector2(430f, 0f), new Vector2(96f, 96f));
            var gear = CreateImage("GearIcon", settings.transform, art.GearIcon, true);
            Place(gear.rectTransform, Vector2.zero, new Vector2(58f, 58f));
            var locked = CreateImage("LockedFeatureButton", bottom, art.LockedButton, true);
            Place(locked.rectTransform, new Vector2(-430f, 0f), new Vector2(96f, 96f));
            var lockIcon = CreateImage("LockIcon", locked.transform, art.LockIcon, true);
            Place(lockIcon.rectTransform, Vector2.zero, new Vector2(52f, 52f));

            var overlay = CreateRect("OverlayRoot", canvasObject.transform);
            Stretch(overlay);

            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            eventSystem.transform.SetParent(root.transform, false);

            var referenceSerialized = new SerializedObject(references);
            referenceSerialized.FindProperty("_background").objectReferenceValue = background;
            referenceSerialized.FindProperty("_levelLabel").objectReferenceValue = levelLabel;
            referenceSerialized.FindProperty("_goldDisplay").objectReferenceValue = goldLabel;
            referenceSerialized.FindProperty("_livesDisplay").objectReferenceValue = livesLabel;
            referenceSerialized.FindProperty("_globalProgressDisplay").objectReferenceValue = progressLabel;
            referenceSerialized.FindProperty("_tankBoard").objectReferenceValue = tankBoard;
            referenceSerialized.FindProperty("_waitingTray").objectReferenceValue = waitingTray;
            referenceSerialized.FindProperty("_bubblePile").objectReferenceValue = bubblePile;
            referenceSerialized.ApplyModifiedPropertiesWithoutUndo();

            var bootstrapSerialized = new SerializedObject(bootstrap);
            bootstrapSerialized.FindProperty("_level").objectReferenceValue = level;
            bootstrapSerialized.FindProperty("_config").objectReferenceValue = config;
            bootstrapSerialized.FindProperty("_fishCatalog").objectReferenceValue = fishCatalog;
            bootstrapSerialized.FindProperty("_artCatalog").objectReferenceValue = art;
            bootstrapSerialized.FindProperty("_scene").objectReferenceValue = references;
            bootstrapSerialized.FindProperty("_tankSlotPrefab").objectReferenceValue = tankPrefab;
            bootstrapSerialized.FindProperty("_badgePrefab").objectReferenceValue = badgePrefab;
            bootstrapSerialized.FindProperty("_bubblePrefab").objectReferenceValue = bubblePrefab;
            bootstrapSerialized.FindProperty("_fishPrefab").objectReferenceValue = fishPrefab;
            bootstrapSerialized.FindProperty("_previewGoldDisplay").intValue = 0;
            bootstrapSerialized.FindProperty("_previewLivesDisplay").intValue = 5;
            bootstrapSerialized.ApplyModifiedPropertiesWithoutUndo();
            SetPrivate(bootstrap, "_level", level);
            SetPrivate(bootstrap, "_config", config);
            SetPrivate(bootstrap, "_fishCatalog", fishCatalog);
            SetPrivate(bootstrap, "_artCatalog", art);
            EditorUtility.SetDirty(bootstrap);
            if (bootstrap.Level == null || bootstrap.Level.PileLayout == null)
            {
                throw new InvalidOperationException(
                    "Level reference did not stick. persistent=" + EditorUtility.IsPersistent(level));
            }

            EnsureFolder("Assets/Game/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
        }

        private static bool PreviewPresentation(StringBuilder report)
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var bootstrap = UnityEngine.Object.FindAnyObjectByType<LevelSceneBootstrapper>();
            var valid = Require(report, bootstrap != null, "Bootstrapper exists in saved scene");
            if (bootstrap == null)
            {
                return false;
            }

            var bakedBubbles = UnityEngine.Object.FindObjectsByType<BubbleView>(FindObjectsInactive.Include).Length;
            valid &= Require(report, bakedBubbles == 0, "Saved scene has no baked BubbleViews");
            bootstrap.Initialize();

            valid &= Require(report, bootstrap.IsInitialized, "Preview initialize completed");
            valid &= Require(report, bootstrap.LevelId == "level_001", "Preview level id");
            valid &= Require(report, bootstrap.TankSlotCount == 4, "Preview tank count " + bootstrap.TankSlotCount);
            valid &= Require(report, bootstrap.UnlockedTankCount == 2, "Preview unlocked tanks " + bootstrap.UnlockedTankCount);
            valid &= Require(report, bootstrap.WaitingTraySlotCount == 5, "Preview tray slots " + bootstrap.WaitingTraySlotCount);
            valid &= Require(report, bootstrap.VisibleBubbleCount == 10, "Preview visible bubbles " + bootstrap.VisibleBubbleCount);
            valid &= Require(report, bootstrap.PendingBubbleCount == 2, "Preview pending bubbles " + bootstrap.PendingBubbleCount);
            valid &= Require(report, bootstrap.GlobalProgressLabel == "0 / 36", "Preview progress " + bootstrap.GlobalProgressLabel);

            var missingScripts = CountMissingScripts();
            var missingSprites = CountMissingRequiredSprites();
            var fish = UnityEngine.Object.FindObjectsByType<FishView>(FindObjectsInactive.Include);
            var fishWithoutSprites = 0;
            var fishOutsideContainer = 0;
            for (var i = 0; i < fish.Length; i++)
            {
                if (fish[i].CurrentSprite == null)
                {
                    fishWithoutSprites++;
                }

                if (fish[i].transform.parent == null || fish[i].transform.parent.name != "FishContainer")
                {
                    fishOutsideContainer++;
                }
            }

            valid &= Require(report, missingScripts == 0, "Missing scripts " + missingScripts);
            valid &= Require(report, missingSprites == 0, "Missing required sprites " + missingSprites);
            valid &= Require(report, fish.Length == 30, "Visible fish " + fish.Length);
            valid &= Require(report, fishWithoutSprites == 0, "Fish without sprites " + fishWithoutSprites);
            valid &= Require(report, fishOutsideContainer == 0, "Fish outside FishContainer " + fishOutsideContainer);
            valid &= Require(report, FrontCoversFish(), "BubbleFront draws after FishContainer");

            var canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();
            var imagePath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "M3GameplayView.png");
            CaptureCanvas(canvas, imagePath, report);
            report.AppendLine("Screenshot=" + imagePath);

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var reloadedBubbles = UnityEngine.Object.FindObjectsByType<BubbleView>(FindObjectsInactive.Include).Length;
            valid &= Require(report, reloadedBubbles == 0, "Reloaded scene still has no baked BubbleViews");
            return valid;
        }

        private static bool FrontCoversFish()
        {
            var bubbles = UnityEngine.Object.FindObjectsByType<BubbleView>(FindObjectsInactive.Include);
            if (bubbles.Length == 0)
            {
                return false;
            }

            for (var i = 0; i < bubbles.Length; i++)
            {
                var back = bubbles[i].transform.Find("BubbleBack");
                var fish = bubbles[i].transform.Find("FishContainer");
                var front = bubbles[i].transform.Find("BubbleFront");
                if (back == null || fish == null || front == null)
                {
                    return false;
                }

                if (back.GetSiblingIndex() >= fish.GetSiblingIndex() || fish.GetSiblingIndex() >= front.GetSiblingIndex())
                {
                    return false;
                }
            }

            return true;
        }

        private static int CountMissingScripts()
        {
            var missing = 0;
            var transforms = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
            for (var i = 0; i < transforms.Length; i++)
            {
                var components = transforms[i].GetComponents<Component>();
                for (var c = 0; c < components.Length; c++)
                {
                    if (components[c] == null)
                    {
                        missing++;
                    }
                }
            }

            return missing;
        }

        private static int CountMissingRequiredSprites()
        {
            var missing = 0;
            var images = UnityEngine.Object.FindObjectsByType<Image>(FindObjectsInactive.Include);
            for (var i = 0; i < images.Length; i++)
            {
                var image = images[i];
                if (image == null || image.color.a <= 0f)
                {
                    continue;
                }

                var name = image.gameObject.name;
                if (name == "Shelf" || name.StartsWith("Slot_", StringComparison.Ordinal))
                {
                    continue;
                }

                if (image.sprite == null)
                {
                    missing++;
                    Debug.LogError("[M3GameplayScene] Missing sprite on " + GetPath(image.transform));
                }
            }

            return missing;
        }

        private static void CaptureCanvas(Canvas canvas, string path, StringBuilder report)
        {
            if (canvas == null)
            {
                report.AppendLine("Capture=missing canvas");
                return;
            }

            var previousMode = canvas.renderMode;
            var previousCamera = canvas.worldCamera;
            var cameraObject = new GameObject("M3CaptureCamera");
            var camera = cameraObject.AddComponent<Camera>();
            var texture = new RenderTexture(1080, 1920, 24, RenderTextureFormat.ARGB32);
            Texture2D image = null;
            try
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.04f, 0.16f, 0.28f, 1f);
                camera.orthographic = true;
                camera.orthographicSize = 960f;
                camera.aspect = 1080f / 1920f;
                camera.transform.position = new Vector3(0f, 0f, -10f);
                camera.nearClipPlane = 0.3f;
                camera.farClipPlane = 100f;
                camera.targetTexture = texture;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 10f;
                Canvas.ForceUpdateCanvases();
                camera.Render();

                var previous = RenderTexture.active;
                RenderTexture.active = texture;
                image = new Texture2D(1080, 1920, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 1080, 1920), 0, 0);
                image.Apply();
                RenderTexture.active = previous;
                Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
                File.WriteAllBytes(path, image.EncodeToPNG());
                report.AppendLine("Capture=wrote " + new FileInfo(path).Length + " bytes");
            }
            finally
            {
                canvas.renderMode = previousMode;
                canvas.worldCamera = previousCamera;
                camera.targetTexture = null;
                if (image != null)
                {
                    UnityEngine.Object.DestroyImmediate(image);
                }

                UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        private static void UpdateBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(ScenePath, true)
            };
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(PreviewScenePath) != null)
            {
                scenes.Add(new EditorBuildSettingsScene(PreviewScenePath, true));
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void EditPrefab(string path, Action<GameObject> edit)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                edit(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static T GetOrAdd<T>(GameObject root) where T : Component
        {
            var component = root.GetComponent<T>();
            return component != null ? component : root.AddComponent<T>();
        }

        private static T Load<T>(string path) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                throw new FileNotFoundException("Missing asset " + path);
            }

            return asset;
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
            return image;
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

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var parent = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(parent))
            {
                parent = parent.Replace('\\', '/');
                if (!AssetDatabase.IsValidFolder(parent))
                {
                    EnsureFolder(parent);
                }
            }

            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static void SetPrivate(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                throw new MissingFieldException(target.GetType().Name, fieldName);
            }

            field.SetValue(target, value);
        }

        private static bool Require(StringBuilder report, bool condition, string label)
        {
            report.AppendLine((condition ? "OK " : "FAIL ") + label);
            return condition;
        }

        private static string GetPath(Transform transform)
        {
            var names = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                names = transform.name + "/" + names;
            }

            return names;
        }

        private static string ToAbsolute(string assetPath)
        {
            return Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static void WriteReport(StringBuilder report)
        {
            var logs = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs");
            Directory.CreateDirectory(logs);
            File.WriteAllText(Path.Combine(logs, "M3SceneReport.txt"), report.ToString());
        }
    }
}
