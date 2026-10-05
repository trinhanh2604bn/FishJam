using FishPuzzle.Core;

namespace FishPuzzle.Progression
{
    /// <summary>
    /// One play-session owner for score, gold, and lives.
    /// Create it with the level bootstrap. Do not store it in a static instance.
    /// </summary>
    public sealed class ProgressionRuntime
    {
        private readonly ScoreService _score;
        private readonly WalletService _wallet;
        private readonly LifeService _lives;

        public ProgressionRuntime(PlayerProgress progress, ScoreService score, WalletService wallet, LifeService lives)
        {
            Progress = progress;
            _score = score;
            _wallet = wallet;
            _lives = lives;
        }

        public ProgressionRuntime(PlayerProgress progress)
            : this(progress, new ScoreService(), new WalletService(), new LifeService())
        {
        }

        public PlayerProgress Progress { get; }

        public WalletService Wallet => _wallet;

        public void BeginAttempt()
        {
            _score.BeginAttempt();
            _lives.BeginAttempt();
        }

        public bool Settle(GameState state, GameConfig config)
        {
            if (config == null || Progress == null)
            {
                return false;
            }

            if (state == GameState.Win)
            {
                return _score.AwardLevelCompletion(Progress, config.LevelCompleteScoreReward);
            }

            if (state == GameState.Lose)
            {
                return _lives.TryDeductForLoss(Progress, config.LoseLifeCost);
            }

            return false;
        }
    }
}
