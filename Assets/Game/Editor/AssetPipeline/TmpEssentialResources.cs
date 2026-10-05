using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FishPuzzle.AssetPipeline
{
    /// <summary>
    /// Copies TextMesh Pro essential resources into the project so UI text has a font asset.
    /// </summary>
    public static class TmpEssentialResources
    {
        public static void EnsureImported()
        {
            if (AssetDatabase.FindAssets("t:TMP_Settings").Length > 0
                && AssetDatabase.FindAssets("LiberationSans SDF t:TMP_FontAsset").Length > 0)
            {
                return;
            }

            var packagePath = FindPackage();
            var temp = Path.Combine(Path.GetTempPath(), "FishPuzzleTmpEssential");
            if (Directory.Exists(temp))
            {
                Directory.Delete(temp, true);
            }

            Directory.CreateDirectory(temp);
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "tar",
                Arguments = "-xf \"" + packagePath + "\" -C \"" + temp + "\"",
                UseShellExecute = false,
                CreateNoWindow = true
            });
            if (process == null)
            {
                throw new InvalidOperationException("Could not start tar to unpack TMP Essential Resources.");
            }

            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException("tar exited with code " + process.ExitCode + " while unpacking TMP Essential Resources.");
            }

            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            foreach (var directory in Directory.GetDirectories(temp))
            {
                var pathnameFile = Path.Combine(directory, "pathname");
                if (!File.Exists(pathnameFile))
                {
                    continue;
                }

                var destination = File.ReadAllText(pathnameFile).Trim().Replace('\\', '/');
                if (!destination.StartsWith("Assets/", StringComparison.Ordinal))
                {
                    continue;
                }

                var projectDestination = Path.Combine(projectRoot, destination.Replace('/', Path.DirectorySeparatorChar));
                var assetFile = Path.Combine(directory, "asset");
                var metaFile = Path.Combine(directory, "asset.meta");
                if (File.Exists(assetFile))
                {
                    var parent = Path.GetDirectoryName(projectDestination);
                    if (!string.IsNullOrEmpty(parent))
                    {
                        Directory.CreateDirectory(parent);
                    }

                    File.Copy(assetFile, projectDestination, true);
                    if (File.Exists(metaFile))
                    {
                        File.Copy(metaFile, projectDestination + ".meta", true);
                    }
                }
                else if (File.Exists(metaFile))
                {
                    Directory.CreateDirectory(projectDestination);
                    File.Copy(metaFile, projectDestination + ".meta", true);
                }
            }

            AssetDatabase.Refresh();
            if (AssetDatabase.FindAssets("LiberationSans SDF t:TMP_FontAsset").Length == 0)
            {
                throw new InvalidOperationException("TMP Essential Resources imported, but LiberationSans SDF was not found.");
            }
        }

        private static string FindPackage()
        {
            var editorDirectory = Path.GetDirectoryName(EditorApplication.applicationPath);
            var candidates = new[]
            {
                Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Library", "PackageCache"),
                Path.Combine(editorDirectory ?? string.Empty, "Data", "Resources", "PackageManager", "BuiltInPackages")
            };

            for (var i = 0; i < candidates.Length; i++)
            {
                if (string.IsNullOrEmpty(candidates[i]) || !Directory.Exists(candidates[i]))
                {
                    continue;
                }

                var matches = Directory.GetFiles(candidates[i], "TMP Essential Resources.unitypackage", SearchOption.AllDirectories);
                if (matches.Length > 0)
                {
                    return matches[0];
                }
            }

            throw new FileNotFoundException("TMP Essential Resources.unitypackage was not found. Add com.unity.ugui before creating text.");
        }
    }
}
