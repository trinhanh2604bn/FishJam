using FishPuzzle.Core;

namespace FishPuzzle.Progression
{
    /// <summary>
    /// One play-session owner for gold and lives (score is legacy and no longer awarded or shown).
    /// Created once by the level bootstrap and kept across Next Level / Replay / Retry, so gold and
    /// hearts persist between attempts. Do not store it in a static instance.
    /// </summary>
    public sealed class ProgressionRuntime
    {
        private readonly ScoreService _score;
        private readonly WalletService _wallet;
        private readonly LifeService _lives;
        private GameState _settledOutcome = GameState.Boot;

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

        /// <summary>Win or Lose once the current attempt has been settled; Boot before that.</summary>
        public GameState SettledOutcome => _settledOutcome;

        public void BeginAttempt()
        {
            _score.BeginAttempt();
            _wallet.BeginAttempt();
            _lives.BeginAttempt();
            _settledOutcome = GameState.Boot;
        }

        public bool Settle(GameState state, GameConfig config)
        {
            if (config == null || Progress == null)
            {
                return false;
            }

            // One authoritative outcome per attempt: a Win and a Lose can never both settle,
            // and repeated Win/Lose calls (panel re-open, PresentCurrentOutcome) change nothing.
            if (_settledOutcome != GameState.Boot)
            {
                return false;
            }

            var applied = false;
            if (state == GameState.Win)
            {
                // M12.1: the level-win reward is Gold, granted once per attempt by the wallet.
                applied = _wallet.AwardLevelCompletion(Progress, config.LevelCompleteGoldReward);
            }
            else if (state == GameState.Lose)
            {
                applied = _lives.TryDeductForLoss(Progress, config.LoseLifeCost);
            }

            if (applied)
            {
                _settledOutcome = state;
            }

            return applied;
        }
    }
}
