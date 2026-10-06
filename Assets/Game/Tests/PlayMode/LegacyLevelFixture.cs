using FishPuzzle.Domain;
using FishPuzzle.Presentation;
using NUnit.Framework;

namespace FishPuzzle.Tests.PlayMode
{
    /// <summary>
    /// M3-M10 PlayMode tests were written against the original 12-bubble, 36-fish Level_001.
    /// Since M11.2 the shipped Level_001 is a 3-bubble onboarding level, so those tests start the
    /// Gameplay scene on the preserved legacy fixture instead. The fixture is test data only.
    /// </summary>
    internal static class LegacyLevelFixture
    {
        public const string Path = "Assets/Game/Tests/Fixtures/LegacyLevel_036.asset";

        public static void UseForNextSceneLoad()
        {
#if UNITY_EDITOR
            var level = UnityEditor.AssetDatabase.LoadAssetAtPath<LevelData>(Path);
            Assert.That(level, Is.Not.Null, "Missing legacy fixture " + Path);
            LevelSceneBootstrapper.StartLevelOverride = level;
#else
            Assert.Ignore("Legacy level fixture is editor-only.");
#endif
        }
    }
}
