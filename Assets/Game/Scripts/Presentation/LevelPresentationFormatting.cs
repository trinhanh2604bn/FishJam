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

        /// <summary>HUD badge and result header text, e.g. "Màn 5".</summary>
        public static string FormatLevelBadge(string levelId)
        {
            return "Màn " + FormatLevelLabel(levelId);
        }

        public static string FormatLevelBadge(int levelNumber)
        {
            return "Màn " + levelNumber.ToString(CultureInfo.InvariantCulture);
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
