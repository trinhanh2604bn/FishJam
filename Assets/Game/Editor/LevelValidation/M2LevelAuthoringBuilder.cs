using System;
using System.Globalization;
using System.IO;
using System.Text;
using FishPuzzle.Bubbles;
using FishPuzzle.Domain;
using UnityEditor;
using UnityEngine;

namespace FishPuzzle.EditorTools
{
    /// <summary>
    /// Writes the standard 10-slot pile and Level_001 through Unity serialization.
    /// Batch entry point: FishPuzzle.EditorTools.M2LevelAuthoringBuilder.BuildAndExit
    /// </summary>
    public static class M2LevelAuthoringBuilder
    {
        public const string LayoutPath = "Assets/Game/Data/BubblePileLayouts/BPL_Standard_10.asset";
        public const string LevelPath = "Assets/Game/Data/Levels/Level_001.asset";

        private static readonly FishType[] TargetGroups =
        {
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
            FishType.GreenStriped
        };

        private static readonly BubbleAuthoring[] Bubbles =
        {
            new BubbleAuthoring("L001_B001", FishType.GreenStriped, FishType.RedClown, FishType.PinkStriped),
            new BubbleAuthoring("L001_B002", FishType.Orange, FishType.RedClown, FishType.PinkStriped),
            new BubbleAuthoring("L001_B003", FishType.Orange, FishType.GreenStriped, FishType.PinkStriped),
            new BubbleAuthoring("L001_B004", FishType.Orange, FishType.GreenStriped, FishType.RedClown),
            new BubbleAuthoring("L001_B005", FishType.GreenStriped, FishType.RedClown, FishType.PinkStriped),
            new BubbleAuthoring("L001_B006", FishType.Orange, FishType.RedClown, FishType.PinkStriped),
            new BubbleAuthoring("L001_B007", FishType.Orange, FishType.GreenStriped, FishType.PinkStriped),
            new BubbleAuthoring("L001_B008", FishType.Orange, FishType.GreenStriped, FishType.RedClown),
            new BubbleAuthoring("L001_B009", FishType.GreenStriped, FishType.RedClown, FishType.PinkStriped),
            new BubbleAuthoring("L001_B010", FishType.Orange, FishType.RedClown, FishType.PinkStriped),
            new BubbleAuthoring("L001_B011", FishType.Orange, FishType.GreenStriped, FishType.PinkStriped),
            new BubbleAuthoring("L001_B012", FishType.Orange, FishType.GreenStriped, FishType.RedClown)
        };

        // Local anchored positions for a center-pivoted 1080x1920 layout.
        // The waiting tray sits near y=40, so the pile occupies the area below it.
        // Row 0 is the bottom row. Sorting order increases toward the front (lower rows).
        private static readonly SlotAuthoring[] Slots =
        {
            new SlotAuthoring(0, -125f, -725f, 0, 0, 40, false),
            new SlotAuthoring(1, 125f, -725f, 0, 1, 41, false),
            new SlotAuthoring(2, -250f, -550f, 1, 0, 30, false, 0),
            new SlotAuthoring(3, 0f, -550f, 1, 1, 31, false, 0, 1),
            new SlotAuthoring(4, 250f, -550f, 1, 2, 32, false, 1),
            new SlotAuthoring(5, -125f, -375f, 2, 0, 20, false, 2, 3),
            new SlotAuthoring(6, 125f, -375f, 2, 1, 21, false, 3, 4),
            new SlotAuthoring(7, -250f, -200f, 3, 0, 10, true, 5),
            new SlotAuthoring(8, 0f, -200f, 3, 1, 11, true, 5, 6),
            new SlotAuthoring(9, 250f, -200f, 3, 2, 12, true, 6)
        };

        [MenuItem("Tools/Fish Puzzle/Author M2 Level Data")]
        public static void AuthorFromMenu()
        {
            BuildAssets();
        }

        public static void BuildAndExit()
        {
            var exitCode = 1;
            try
            {
                exitCode = BuildAssets() ? 0 : 1;
            }
            catch (Exception exception)
            {
                Debug.LogError("[M2LevelAuthoring] " + exception);
                exitCode = 1;
            }

            EditorApplication.Exit(exitCode);
        }

        public static bool BuildAssets()
        {
            EnsureFolder("Assets/Game/Data/BubblePileLayouts");
            EnsureFolder("Assets/Game/Data/Levels");

            var layout = LoadOrCreate<BubblePileLayout>(LayoutPath);
            var level = LoadOrCreate<LevelData>(LevelPath);
            WriteLayout(layout);
            WriteLevel(level, layout);
            EditorUtility.SetDirty(layout);
            EditorUtility.SetDirty(level);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(LayoutPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(LevelPath, ImportAssetOptions.ForceUpdate);

            layout = AssetDatabase.LoadAssetAtPath<BubblePileLayout>(LayoutPath);
            level = AssetDatabase.LoadAssetAtPath<LevelData>(LevelPath);
            Selection.objects = new UnityEngine.Object[] { layout, level };
            Selection.activeObject = level;
            EditorGUIUtility.PingObject(level);

            var report = new StringBuilder();
            var valid = Verify(level, layout, report);
            var allLevelsValid = LevelValidationMenu.Run();
            report.AppendLine("ValidateAllLevels=" + (allLevelsValid ? "PASS" : "FAIL"));
            valid = valid && allLevelsValid;

            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var logsFolder = Path.Combine(projectRoot, "Logs");
            Directory.CreateDirectory(logsFolder);
            var reportPath = Path.Combine(logsFolder, "M2AuthoringReport.txt");
            File.WriteAllText(reportPath, report.ToString());
            if (valid)
            {
                Debug.Log("[M2LevelAuthoring] PASS\n" + report);
            }
            else
            {
                Debug.LogError("[M2LevelAuthoring] FAIL\n" + report);
            }

            return valid;
        }

        private static void WriteLayout(BubblePileLayout layout)
        {
            var serialized = new SerializedObject(layout);
            var slots = serialized.FindProperty("_slots");
            slots.arraySize = Slots.Length;
            for (var i = 0; i < Slots.Length; i++)
            {
                var slot = Slots[i];
                var element = slots.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("_slotId").intValue = slot.Id;
                element.FindPropertyRelative("_anchoredPosition").vector2Value = new Vector2(slot.X, slot.Y);
                element.FindPropertyRelative("_row").intValue = slot.Row;
                element.FindPropertyRelative("_column").intValue = slot.Column;
                element.FindPropertyRelative("_sortingOrder").intValue = slot.SortingOrder;
                element.FindPropertyRelative("_canReceiveSpawnFromTop").boolValue = slot.TopSpawn;
                var downs = element.FindPropertyRelative("_downCandidateSlotIds");
                downs.arraySize = slot.Down.Length;
                for (var downIndex = 0; downIndex < slot.Down.Length; downIndex++)
                {
                    downs.GetArrayElementAtIndex(downIndex).intValue = slot.Down[downIndex];
                }
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WriteLevel(LevelData level, BubblePileLayout layout)
        {
            var serialized = new SerializedObject(level);
            serialized.FindProperty("_levelId").stringValue = "level_001";
            serialized.FindProperty("_initialUnlockedTankCount").intValue = 2;
            serialized.FindProperty("_totalFishRequired").intValue = 36;
            serialized.FindProperty("_pileLayout").objectReferenceValue = layout;

            var targets = serialized.FindProperty("_targetGroupQueue");
            targets.arraySize = TargetGroups.Length;
            for (var i = 0; i < TargetGroups.Length; i++)
            {
                targets.GetArrayElementAtIndex(i).intValue = (int)TargetGroups[i];
            }

            var bubbles = serialized.FindProperty("_bubbleQueue");
            bubbles.arraySize = Bubbles.Length;
            for (var i = 0; i < Bubbles.Length; i++)
            {
                var element = bubbles.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("_bubbleId").stringValue = Bubbles[i].Id;
                element.FindPropertyRelative("_modifier").intValue = (int)BubbleModifier.None;
                var fishes = element.FindPropertyRelative("_fishes");
                fishes.arraySize = 3;
                fishes.GetArrayElementAtIndex(0).intValue = (int)Bubbles[i].First;
                fishes.GetArrayElementAtIndex(1).intValue = (int)Bubbles[i].Second;
                fishes.GetArrayElementAtIndex(2).intValue = (int)Bubbles[i].Third;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static bool Verify(LevelData level, BubblePileLayout layout, StringBuilder report)
        {
            var valid = true;
            valid &= Require(report, level != null, "Level_001 loaded");
            valid &= Require(report, layout != null, "BPL_Standard_10 loaded");
            if (level == null || layout == null)
            {
                return false;
            }

            valid &= Require(report, HasScript(level), "Level_001 script assigned");
            valid &= Require(report, HasScript(layout), "BPL_Standard_10 script assigned");
            valid &= Require(report, level.LevelId == "level_001", "levelId is level_001");
            valid &= Require(report, level.InitialUnlockedTankCount == 2, "initialUnlockedTankCount is 2");
            valid &= Require(report, level.TotalFishRequired == 36, "totalFishRequired is 36");
            valid &= Require(report, level.PileLayout == layout, "pile layout reference resolved");
            valid &= Require(report, AssetDatabase.GetAssetPath(level.PileLayout) == LayoutPath, "pile layout path is BPL_Standard_10");
            valid &= Require(report, level.TargetGroupQueue != null && level.TargetGroupQueue.Count == TargetGroups.Length, "target group count is 12");
            valid &= Require(report, level.BubbleQueue != null && level.BubbleQueue.Count == Bubbles.Length, "bubble count is 12");
            valid &= Require(report, layout.Slots != null && layout.Slots.Count == Slots.Length, "slot count is 10");

            if (level.TargetGroupQueue != null)
            {
                for (var i = 0; i < TargetGroups.Length && i < level.TargetGroupQueue.Count; i++)
                {
                    valid &= Require(report, level.TargetGroupQueue[i] == TargetGroups[i], "target " + i + " is " + TargetGroups[i]);
                }
            }

            if (level.BubbleQueue != null)
            {
                for (var i = 0; i < level.BubbleQueue.Count; i++)
                {
                    var bubble = level.BubbleQueue[i];
                    valid &= Require(report, bubble != null, "bubble " + i + " is not null");
                    if (bubble == null || i >= Bubbles.Length)
                    {
                        continue;
                    }

                    valid &= Require(report, bubble.BubbleId == Bubbles[i].Id, "bubble id " + Bubbles[i].Id);
                    valid &= Require(report, bubble.Modifier == BubbleModifier.None, Bubbles[i].Id + " modifier is None");
                    valid &= Require(report, bubble.Fishes != null && bubble.Fishes.Count == 3, Bubbles[i].Id + " has 3 fish");
                    if (bubble.Fishes == null || bubble.Fishes.Count < 3)
                    {
                        continue;
                    }

                    valid &= Require(report, bubble.Fishes[0] == Bubbles[i].First, Bubbles[i].Id + " fish 0");
                    valid &= Require(report, bubble.Fishes[1] == Bubbles[i].Second, Bubbles[i].Id + " fish 1");
                    valid &= Require(report, bubble.Fishes[2] == Bubbles[i].Third, Bubbles[i].Id + " fish 2");
                }
            }

            if (layout.Slots != null)
            {
                for (var i = 0; i < layout.Slots.Count; i++)
                {
                    var slot = layout.Slots[i];
                    valid &= Require(report, slot != null, "slot index " + i + " is not null");
                    if (slot == null)
                    {
                        continue;
                    }

                    SlotAuthoring expected = default;
                    var found = false;
                    for (var candidate = 0; candidate < Slots.Length; candidate++)
                    {
                        if (Slots[candidate].Id == slot.SlotId)
                        {
                            expected = Slots[candidate];
                            found = true;
                            break;
                        }
                    }

                    valid &= Require(report, found, "slot id " + slot.SlotId + " is part of the standard layout");
                    if (!found)
                    {
                        continue;
                    }

                    valid &= Require(report, slot.Row == expected.Row, "slot " + slot.SlotId + " row");
                    valid &= Require(report, slot.Column == expected.Column, "slot " + slot.SlotId + " column");
                    valid &= Require(report, slot.SortingOrder == expected.SortingOrder, "slot " + slot.SlotId + " sortingOrder");
                    valid &= Require(report, slot.CanReceiveSpawnFromTop == expected.TopSpawn, "slot " + slot.SlotId + " top spawn");
                    valid &= Require(
                        report,
                        Approximately(slot.AnchoredPosition.x, expected.X) && Approximately(slot.AnchoredPosition.y, expected.Y),
                        "slot " + slot.SlotId + " position " + slot.AnchoredPosition.x.ToString("0.###", CultureInfo.InvariantCulture)
                        + "," + slot.AnchoredPosition.y.ToString("0.###", CultureInfo.InvariantCulture));
                    valid &= Require(report, SameIds(slot.DownCandidateSlotIds, expected.Down), "slot " + slot.SlotId + " down candidates");
                }
            }

            var config = AssetDatabase.LoadAssetAtPath<FishPuzzle.Core.GameConfig>(LevelValidationMenu.GameConfigPath);
            valid &= Require(report, config != null, "GameConfig loaded for verification");
            if (config != null)
            {
                var result = new LevelValidator().Validate(level, config);
                valid &= Require(report, result.IsValid, "Level_001 validation");
                for (var i = 0; i < result.Issues.Count; i++)
                {
                    report.AppendLine("ISSUE " + result.Issues[i]);
                }
            }

            report.AppendLine("Selected=" + (Selection.activeObject != null ? Selection.activeObject.name : "none"));
            return valid;
        }

        private static bool HasScript(UnityEngine.Object asset)
        {
            var serialized = new SerializedObject(asset);
            var script = serialized.FindProperty("m_Script");
            return script != null && script.objectReferenceValue != null;
        }

        private static bool SameIds(System.Collections.Generic.IReadOnlyList<int> actual, int[] expected)
        {
            if (actual == null || expected == null || actual.Count != expected.Length)
            {
                return false;
            }

            for (var i = 0; i < expected.Length; i++)
            {
                if (actual[i] != expected[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static bool Approximately(float actual, float expected)
        {
            return Mathf.Abs(actual - expected) < 0.01f;
        }

        private static bool Require(StringBuilder report, bool condition, string label)
        {
            report.AppendLine((condition ? "OK " : "FAIL ") + label);
            return condition;
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                return asset;
            }

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var parent = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(parent))
            {
                return;
            }

            parent = parent.Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private readonly struct BubbleAuthoring
        {
            public BubbleAuthoring(string id, FishType first, FishType second, FishType third)
            {
                Id = id;
                First = first;
                Second = second;
                Third = third;
            }

            public string Id { get; }

            public FishType First { get; }

            public FishType Second { get; }

            public FishType Third { get; }
        }

        private readonly struct SlotAuthoring
        {
            public SlotAuthoring(int id, float x, float y, int row, int column, int sortingOrder, bool topSpawn, params int[] down)
            {
                Id = id;
                X = x;
                Y = y;
                Row = row;
                Column = column;
                SortingOrder = sortingOrder;
                TopSpawn = topSpawn;
                Down = down ?? Array.Empty<int>();
            }

            public int Id { get; }

            public float X { get; }

            public float Y { get; }

            public int Row { get; }

            public int Column { get; }

            public int SortingOrder { get; }

            public bool TopSpawn { get; }

            public int[] Down { get; }
        }
    }
}
