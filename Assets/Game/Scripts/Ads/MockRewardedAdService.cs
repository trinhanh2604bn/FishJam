using System;

namespace FishPuzzle.Ads
{
    public enum MockRewardedAdOutcome
    {
        RewardEarned = 0,
        ClosedWithoutReward = 1,
        Failed = 2
    }

    /// <summary>
    /// Editor stand-in for a rewarded ad. It finishes immediately with the prepared outcome.
    /// The default outcome is a successful reward. This type does not call an ad SDK.
    /// </summary>
    public sealed class MockRewardedAdService : IRewardedAdService
    {
        private MockRewardedAdOutcome _nextOutcome = MockRewardedAdOutcome.RewardEarned;

        public MockRewardedAdOutcome NextOutcome => _nextOutcome;

        public void SetNextOutcome(MockRewardedAdOutcome outcome)
        {
            _nextOutcome = outcome;
        }

        public void Show(Action onRewardEarned, Action onClosedWithoutReward, Action onFailed)
        {
            if (_nextOutcome == MockRewardedAdOutcome.RewardEarned)
            {
                onRewardEarned?.Invoke();
                return;
            }

            if (_nextOutcome == MockRewardedAdOutcome.Failed)
            {
                onFailed?.Invoke();
                return;
            }

            onClosedWithoutReward?.Invoke();
        }
    }
}
