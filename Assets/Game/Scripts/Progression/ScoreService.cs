namespace FishPuzzle.Progression
{
    /// <summary>
    /// Awards the level-complete score once per attempt.
    /// A second call for the same attempt does not add score.
    /// </summary>
    public sealed class ScoreService
    {
        private bool _awardedThisAttempt;

        public void BeginAttempt()
        {
            _awardedThisAttempt = false;
        }

        public bool AwardLevelCompletion(PlayerProgress progress, int reward)
        {
            if (_awardedThisAttempt || progress == null || reward < 0)
            {
                return false;
            }

            _awardedThisAttempt = true;
            progress.AddScore(reward);
            return true;
        }
    }
}
