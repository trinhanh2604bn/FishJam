using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FishPuzzle.AssetPipeline
{
    /// <summary>
    /// Applies sprite import rules only under Assets/Game/Art.
    /// </summary>
    public sealed class GameArtTexturePostprocessor : AssetPostprocessor
    {
        private const string ArtRoot = "Assets/Game/Art/";

        private void OnPreprocessTexture()
        {
            var path = assetPath.Replace('\\', '/');
            if (!path.StartsWith(ArtRoot, StringComparison.Ordinal))
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;
            var fullPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, path);
            var knownSize = TryReadPng(fullPath, out var width, out var height, out var hasAlpha);

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.textureType = TextureImporterType.Sprite;
            settings.spriteMode = (int)SpriteImportMode.Single;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteExtrude = 1;
            settings.spriteGenerateFallbackPhysicsShape = false;
            settings.mipmapEnabled = false;
            settings.alphaIsTransparency = !knownSize || hasAlpha;
            settings.alphaSource = TextureImporterAlphaSource.FromInput;
            settings.sRGBTexture = true;
            settings.wrapMode = TextureWrapMode.Clamp;
            settings.filterMode = FilterMode.Bilinear;
            settings.npotScale = TextureImporterNPOTScale.None;
            settings.readable = false;
            importer.SetTextureSettings(settings);

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.streamingMipmaps = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.alphaIsTransparency = settings.alphaIsTransparency;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.sRGBTexture = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.isReadable = false;
            importer.spritePixelsPerUnit = 100f;

            var platform = importer.GetDefaultPlatformTextureSettings();
            platform.maxTextureSize = ResolveMaxSize(path, knownSize ? width : 0, knownSize ? height : 0);
            platform.textureCompression = TextureImporterCompression.Uncompressed;
            platform.crunchedCompression = false;
            importer.SetPlatformTextureSettings(platform);
        }

        internal static int ResolveMaxSize(string assetPath, int sourceWidth, int sourceHeight)
        {
            var path = assetPath.Replace('\\', '/');
            if (sourceWidth <= 0 || sourceHeight <= 0)
            {
                if (path.Contains("/Art/Backgrounds/"))
                {
                    return 2048;
                }

                if (path.Contains("/Art/Fish/") || path.Contains("/Art/Bubbles/") || path.Contains("/Art/VFX/"))
                {
                    return 512;
                }

                return 2048;
            }

            var longest = Math.Max(sourceWidth, sourceHeight);
            if (path.Contains("/Art/Backgrounds/"))
            {
                return Math.Max(2048, NextPowerOfTwo(longest));
            }

            if (path.Contains("/Art/Fish/"))
            {
                return longest > 512 ? ClampPowerOfTwo(NextPowerOfTwo(longest), 512, 2048) : 512;
            }

            if (path.Contains("/Art/Bubbles/") || path.Contains("/Art/VFX/"))
            {
                return ClampPowerOfTwo(NextPowerOfTwo(Math.Max(longest, 32)), 32, 512);
            }

            return ClampPowerOfTwo(NextPowerOfTwo(Math.Max(longest, 32)), 32, 4096);
        }

        private static int NextPowerOfTwo(int value)
        {
            var size = 32;
            var target = Math.Max(value, 1);
            while (size < target && size < 8192)
            {
                size *= 2;
            }

            return size;
        }

        private static int ClampPowerOfTwo(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            if (value > max)
            {
                return max;
            }

            return value;
        }

        private static bool TryReadPng(string fullPath, out int width, out int height, out bool hasAlpha)
        {
            width = 0;
            height = 0;
            hasAlpha = false;
            try
            {
                using var stream = File.OpenRead(fullPath);
                using var reader = new BinaryReader(stream);
                var signature = reader.ReadBytes(8);
                if (signature.Length != 8 || signature[0] != 0x89 || signature[1] != 0x50)
                {
                    return false;
                }

                byte colorType = 2;
                while (stream.Position + 8 <= stream.Length)
                {
                    var length = ReadInt32BigEndian(reader);
                    var type = System.Text.Encoding.ASCII.GetString(reader.ReadBytes(4));
                    if (type == "IHDR")
                    {
                        var data = reader.ReadBytes(length);
                        width = ReadInt32BigEndian(data, 0);
                        height = ReadInt32BigEndian(data, 4);
                        colorType = data[9];
                        hasAlpha = colorType == 4 || colorType == 6;
                        reader.ReadBytes(4);
                        if (colorType != 3)
                        {
                            return true;
                        }

                        continue;
                    }

                    if (type == "tRNS")
                    {
                        hasAlpha = true;
                        return width > 0;
                    }

                    if (type == "IDAT" || type == "IEND")
                    {
                        return width > 0;
                    }

                    stream.Seek(length + 4, SeekOrigin.Current);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[GameArtTexturePostprocessor] Could not read PNG header for " + fullPath + ". " + exception.Message);
                return false;
            }

            return width > 0;
        }

        private static int ReadInt32BigEndian(BinaryReader reader)
        {
            var data = reader.ReadBytes(4);
            return ReadInt32BigEndian(data, 0);
        }

        private static int ReadInt32BigEndian(byte[] data, int offset)
        {
            return (data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3];
        }
    }
}
