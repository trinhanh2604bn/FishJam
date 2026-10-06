using System.IO;
using FishPuzzle.Core;
using FishPuzzle.Domain;
using FishPuzzle.Progression;
using NUnit.Framework;
using UnityEditor;

namespace FishPuzzle.Tests.EditMode
{
    /// <summary>
    /// M12.1: Win rewards +20 Gold (not Score), Gold and Hearts persist across attempts and levels,
    /// and the standalone Score HUD is gone.
    /// </summary>
    public sealed class M121HudProgressionTests
    {
        private const string ConfigPath = "Assets/Game/Data/Config/GameConfig.asset";
        private const string CatalogPath = "Assets/Game/Data/Config/LevelCatalog.asset";
        private const string BootstrapperPath = "Assets/Game/Scripts/Presentation/LevelSceneBootstrapper.cs";

        [Test]
        public void StandaloneScoreHud_IsNotUsed()
        {
            Assert.That(typeof(FishPuzzle.Presentation.LevelSceneBootstrapper).GetProperty("ScoreLabel"), Is.Null);
            var source = File.ReadAllText(BootstrapperPath);
            Assert.That(source, Does.Not.Contain("\"ScoreLabel\""), "No runtime Score HUD object is created.");
            Assert.That(source, Does.Not.Contain("Progress.Score"), "The HUD binds Gold and Hearts only.");
            Assert.That(typeof(FishPuzzle.Presentation.WinPanelView).GetProperty("ScoreText"), Is.Null);
            Assert.That(typeof(FishPuzzle.Presentation.WinPanelView).GetProperty("GoldText"), Is.Not.Null);
        }

        [Test]
        public void Win_GoldProgression_1000_1020_Duplicate_Next_1040()
        {
            var config = Config();
            Assert.That(config.LevelCompleteGoldReward, Is.EqualTo(20));
            var progression = new ProgressionRuntime(PlayerProgress.CreateDevelopmentDefaults());
            Assert.That(progression.Progress.Gold, Is.EqualTo(1000));

            progression.BeginAttempt();
            Assert.That(progression.Settle(GameState.Win, config), Is.True);
            Assert.That(progression.Progress.Gold, Is.EqualTo(1020));

            Assert.That(progression.Settle(GameState.Win, config), Is.False, "Second outcome callback.");
            Assert.That(progression.Wallet.AwardLevelCompletion(progression.Progress, 20), Is.False, "Wallet guard.");
            Assert.That(progression.Progress.Gold, Is.EqualTo(1020));

            // Next level: same runtime, new attempt. Gold is not reset.
            progression.BeginAttempt();
            Assert.That(progression.Progress.Gold, Is.EqualTo(1020));

            Assert.That(progression.Settle(GameState.Win, config), Is.True);
            Assert.That(progression.Progress.Gold, Is.EqualTo(1040));
            Assert.That(progression.Progress.Score, Is.EqualTo(0), "Score is no longer awarded.");
            Assert.That(progression.Progress.Lives, Is.EqualTo(5), "Win does not change hearts.");
        }

        [Test]
        public void TankUnlock_DeductsSixHundred_FromCurrentGold_ThenWinAddsTwenty()
        {
            var config = Config();
            var progress = PlayerProgress.Create(0, 1040, 5);
            var progression = new ProgressionRuntime(progress);
            progression.BeginAttempt();
            var session = LevelSession.Start(Catalog().Levels[0], config, false);
            session.SetOutcomeHandler(state => progression.Settle(state, config));

            Assert.That(session.TryUnlockWithGold(2, progress, progression.Wallet).Succeeded, Is.True);
            Assert.That(progress.Gold, Is.EqualTo(440));

            Assert.That(progression.Settle(GameState.Win, config), Is.True);
            Assert.That(progress.Gold, Is.EqualTo(460));
        }

        [Test]
        public void Hearts_Lose_Retry_LoseAgain_Next_Win()
        {
            var config = Config();
            var progression = new ProgressionRuntime(PlayerProgress.CreateDevelopmentDefaults());
            Assert.That(progression.Progress.Lives, Is.EqualTo(5));

            progression.BeginAttempt();
            Assert.That(progression.Settle(GameState.Lose, config), Is.True);
            Assert.That(progression.Progress.Lives, Is.EqualTo(4));
            Assert.That(progression.Settle(GameState.Lose, config), Is.False, "Duplicate Lose.");
            Assert.That(progression.Progress.Lives, Is.EqualTo(4));

            // Retry.
            progression.BeginAttempt();
            Assert.That(progression.Progress.Lives, Is.EqualTo(4));

            Assert.That(progression.Settle(GameState.Lose, config), Is.True);
            Assert.That(progression.Progress.Lives, Is.EqualTo(3));

            // Retry then win, then next level: hearts keep their current value and are never restored by a win.
            progression.BeginAttempt();
            Assert.That(progression.Settle(GameState.Win, config), Is.True);
            Assert.That(progression.Progress.Lives, Is.EqualTo(3));
            progression.BeginAttempt();
            Assert.That(progression.Progress.Lives, Is.EqualTo(3));
            Assert.That(progression.Progress.Gold, Is.EqualTo(1020));
        }

        [Test]
        public void BeginAttempt_NeverResetsGoldOrHearts()
        {
            var progression = new ProgressionRuntime(PlayerProgress.Create(0, 37, 2));
            for (var i = 0; i < 3; i++)
            {
                progression.BeginAttempt();
            }

            Assert.That(progression.Progress.Gold, Is.EqualTo(37));
            Assert.That(progression.Progress.Lives, Is.EqualTo(2));
        }

        private static LevelCatalog Catalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null, CatalogPath);
            return catalog;
        }

        private static GameConfig Config()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            Assert.That(config, Is.Not.Null, ConfigPath);
            return config;
        }
    }
}
