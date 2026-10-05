using System;
using System.Collections.Generic;
using FishPuzzle.Core;
using FishPuzzle.Domain;
using UnityEditor;
using UnityEngine;

namespace FishPuzzle.EditorTools
{
    /// <summary>
    /// Editor-only batch check for authored levels. Runtime code does not reference this type.
    /// </summary>
    public static class LevelValidationMenu
    {
        public const string GameConfigPath = "Assets/Game/Data/Config/GameConfig.asset";
        public const string LevelsFolder = "Assets/Game/Data/Levels";

        [MenuItem("Tools/Fish Puzzle/Validate All Levels")]
        public static void ValidateAllLevels()
        {
            Run();
        }

        public static bool Run()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(GameConfigPath);
            if (config == null)
            {
                Debug.LogError("[LevelValidation] FAIL GameConfig was not found at " + GameConfigPath + ".");
                return false;
            }

            if (!AssetDatabase.IsValidFolder(LevelsFolder))
            {
                Debug.LogError("[LevelValidation] FAIL Level folder is missing: " + LevelsFolder + ".");
                return false;
            }

            var guids = AssetDatabase.FindAssets(string.Empty, new[] { LevelsFolder });
            var levelPaths = new List<string>();
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
                {
                    levelPaths.Add(path);
                }
            }

            levelPaths.Sort(StringComparer.Ordinal);
            if (levelPaths.Count == 0)
            {
                Debug.LogError("[LevelValidation] FAIL No level assets were found under " + LevelsFolder + ".");
                return false;
            }

            var validator = new LevelValidator();
            var passed = 0;
            var failed = 0;
            for (var i = 0; i < levelPaths.Count; i++)
            {
                var path = levelPaths[i];
                var level = AssetDatabase.LoadAssetAtPath<LevelData>(path);
                if (level == null)
                {
                    failed++;
                    Debug.LogError("[LevelValidation] FAIL " + path + " | The asset did not load as LevelData. The script may be missing.");
                    continue;
                }

                var result = validator.Validate(level, config);
                if (result.IsValid)
                {
                    passed++;
                    Debug.Log("[LevelValidation] PASS " + level.LevelId + " (" + path + ")");
                    continue;
                }

                failed++;
                Debug.LogError("[LevelValidation] FAIL " + level.LevelId + " (" + path + ")");
                for (var issueIndex = 0; issueIndex < result.Issues.Count; issueIndex++)
                {
                    Debug.LogError("[LevelValidation] " + result.Issues[issueIndex]);
                }
            }

            var summary = "[LevelValidation] Validate All Levels: " + passed + " passed, " + failed + " failed. GameConfig: " + GameConfigPath + ".";
            if (failed == 0)
            {
                Debug.Log(summary);
            }
            else
            {
                Debug.LogError(summary);
            }

            return failed == 0;
        }
    }
}
