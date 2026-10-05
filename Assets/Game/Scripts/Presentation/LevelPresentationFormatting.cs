using System;
using System.Globalization;

namespace FishPuzzle.Presentation
{
    /// <summary>
    /// Display strings for HUD preview. These are not domain identifiers and are not localized yet.
    /// </summary>
    public static class LevelPresentationFormatting
    {
        public static string FormatLevelLabel(string levelId)
        {
            const string prefix = "level_";
            if (string.IsNullOrEmpty(levelId) || !levelId.StartsWith(prefix, StringComparison.Ordinal))
            {
                return levelId ?? string.Empty;
            }

            var suffix = levelId.Substring(prefix.Length);
            if (int.TryParse(suffix, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
            {
                return number.ToString(CultureInfo.InvariantCulture);
            }

            return levelId;
        }

        public static string FormatProgress(int collected, int totalRequired)
        {
            return collected.ToString(CultureInfo.InvariantCulture)
                + " / "
                + totalRequired.ToString(CultureInfo.InvariantCulture);
        }

        public static string FormatCount(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
