using FishPuzzle.Core;
using FishPuzzle.Presentation;
using FishPuzzle.Progression;
using NUnit.Framework;
using UnityEditor;

namespace FishPuzzle.Tests.EditMode
{
    public sealed class M12ResultUiTests
    {
        private const string ConfigPath = "Assets/Game/Data/Config/GameConfig.asset";
        private const string ArtCatalogPath = "Assets/Game/Data/Config/GameplayArtCatalog.asset";

        [Test]
        public void Win_AwardsTwentyGoldOnce_EvenWhenPresentedRepeatedly()
        {
            var config = Config();
            var progression = new ProgressionRuntime(PlayerProgress.CreateDevelopmentDefaults());
            progression.BeginAttempt();

            Assert.That(progression.Settle(GameState.Win, config), Is.True);
            Assert.That(progression.Settle(GameState.Win, config), Is.False);
            Assert.That(progression.Settle(GameState.Win, config), Is.False);

            Assert.That(progression.Progress.Gold, Is.EqualTo(1020));
            Assert.That(progression.SettledOutcome, Is.EqualTo(GameState.Win));
        }

        [Test]
        public void Lose_DeductsOneHeartOnce_EvenWhenPresentedRepeatedly()
        {
            var config = Config();
            var progression = new ProgressionRuntime(PlayerProgress.CreateDevelopmentDefaults());
            progression.BeginAttempt();

            Assert.That(progression.Settle(GameState.Lose, config), Is.True);
            Assert.That(progression.Settle(GameState.Lose, config), Is.False);

            Assert.That(progression.Progress.Lives, Is.EqualTo(4));
            Assert.That(progression.SettledOutcome, Is.EqualTo(GameState.Lose));
        }

        [Test]
        public void OneAttempt_CannotSettleBothWinAndLose()
        {
            var config = Config();
            var progression = new ProgressionRuntime(PlayerProgress.CreateDevelopmentDefaults());
            progression.BeginAttempt();
            Assert.That(progression.Settle(GameState.Win, config), Is.True);
            Assert.That(progression.Settle(GameState.Lose, config), Is.False);
            Assert.That(progression.Progress.Lives, Is.EqualTo(5));

            progression.BeginAttempt();
            Assert.That(progression.Settle(GameState.Lose, config), Is.True);
            Assert.That(progression.Settle(GameState.Win, config), Is.False);
            Assert.That(progression.Progress.Gold, Is.EqualTo(1020));
            Assert.That(progression.Progress.Lives, Is.EqualTo(4));
        }

        [Test]
        public void RetryReplayNext_StartNewAttempts_WithoutReapplyingPreviousOutcome()
        {
            var config = Config();
            var progression = new ProgressionRuntime(PlayerProgress.CreateDevelopmentDefaults());
            progression.BeginAttempt();
            progression.Settle(GameState.Lose, config);

            // Retry: new attempt, nothing applied until the next authoritative outcome.
            progression.BeginAttempt();
            Assert.That(progression.SettledOutcome, Is.EqualTo(GameState.Boot));
            Assert.That(progression.Progress.Lives, Is.EqualTo(4));

            progression.Settle(GameState.Win, config);
            // Next Level / Replay: new attempt, the earlier win is not re-awarded.
            progression.BeginAttempt();
            Assert.That(progression.Progress.Gold, Is.EqualTo(1020));
            progression.Settle(GameState.Win, config);
            Assert.That(progression.Progress.Gold, Is.EqualTo(1040), "+20 Gold per won attempt.");
        }

        [Test]
        public void VietnameseTexts_AreFinal()
        {
            Assert.That(LosePanelView.TitleText, Is.EqualTo("Thua Màn!"));
            Assert.That(LosePanelView.RetryText, Is.EqualTo("Thử Lại"));
            Assert.That(LosePanelView.FormatCost(1), Is.EqualTo("-1"));
            Assert.That(WinPanelView.TitleText, Is.EqualTo("WELL DONE!"));
            Assert.That(WinPanelView.ProgressCaptionText, Is.EqualTo("Hoàn thành các màn chơi để mở khóa vật phẩm"));
            Assert.That(WinPanelView.ClaimText, Is.EqualTo("Nhận"));
            Assert.That(WinPanelView.ClaimDoubleText, Is.EqualTo("Nhận x2"));
            Assert.That(WinPanelView.FormatReward(20), Is.EqualTo("+20"));
            Assert.That(LevelPresentationFormatting.FormatLevelBadge("level_005"), Is.EqualTo("Màn 5"));
            Assert.That(LevelPresentationFormatting.FormatLevelBadge(12), Is.EqualTo("Màn 12"));
        }

        [Test]
        public void ResultArt_IsAssignedInCatalog()
        {
            var art = AssetDatabase.LoadAssetAtPath<GameplayArtCatalog>(ArtCatalogPath);
            Assert.That(art, Is.Not.Null, ArtCatalogPath);
            Assert.That(art.LevelBadge, Is.Not.Null);
            Assert.That(art.LevelBadgeFishIcon, Is.Not.Null);
            Assert.That(art.ResultPanel, Is.Not.Null);
            Assert.That(art.ProgressTrack, Is.Not.Null);
            Assert.That(art.ProgressFill, Is.Not.Null);
            Assert.That(art.RewardPlate, Is.Not.Null);
            Assert.That(art.Pearl, Is.Not.Null);
            Assert.That(art.ResultPanel.border.x, Is.GreaterThan(0f), "Result panel is 9-sliced.");
            Assert.That(art.ProgressTrack.border.x, Is.GreaterThan(0f), "Progress track is 9-sliced.");
            Assert.That(art.ProgressFill.border.x, Is.GreaterThan(0f), "Progress fill is 9-sliced.");
        }

        private static GameConfig Config()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            Assert.That(config, Is.Not.Null, ConfigPath);
            return config;
        }
    }
}
