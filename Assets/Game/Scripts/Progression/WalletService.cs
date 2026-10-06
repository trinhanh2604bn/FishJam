namespace FishPuzzle.Progression
{
    /// <summary>
    /// The only gold mutator: level-win reward, tank-unlock spend, refund.
    /// UI reads the wallet result and does not change gold itself.
    /// </summary>
    public sealed class WalletService
    {
        private bool _awardedThisAttempt;

        /// <summary>True once the level-completion gold for the current attempt has been granted.</summary>
        public bool HasAwardedThisAttempt => _awardedThisAttempt;

        /// <summary>Starts a new attempt guard. Gold itself is never reset here; it persists across levels.</summary>
        public void BeginAttempt()
        {
            _awardedThisAttempt = false;
        }

        /// <summary>
        /// Grants the level-completion gold exactly once per attempt. Repeated calls (panel re-open,
        /// duplicate outcome callbacks) return false and change nothing.
        /// </summary>
        public bool AwardLevelCompletion(PlayerProgress progress, int reward)
        {
            if (progress == null || reward < 0 || _awardedThisAttempt)
            {
                return false;
            }

            progress.AddGold(reward);
            _awardedThisAttempt = true;
            return true;
        }

        public bool TrySpend(PlayerProgress progress, int amount)
        {
            if (progress == null || amount < 0)
            {
                return false;
            }

            return progress.TrySpendGold(amount);
        }

        public void Refund(PlayerProgress progress, int amount)
        {
            if (progress == null || amount <= 0)
            {
                return;
            }

            progress.AddGold(amount);
        }
    }
}
