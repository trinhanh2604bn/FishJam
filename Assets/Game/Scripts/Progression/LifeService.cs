namespace FishPuzzle.Progression
{
    /// <summary>
    /// Deducts the lose-life cost once per attempt.
    /// Reopening the lose panel does not deduct again.
    /// </summary>
    public sealed class LifeService
    {
        private bool _deductedThisAttempt;

        public void BeginAttempt()
        {
            _deductedThisAttempt = false;
        }

        public bool TryDeductForLoss(PlayerProgress progress, int cost)
        {
            if (_deductedThisAttempt || progress == null || cost < 0)
            {
                return false;
            }

            _deductedThisAttempt = true;
            progress.DeductLives(cost);
            return true;
        }
    }
}
