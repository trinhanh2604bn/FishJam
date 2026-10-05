using System;

namespace FishPuzzle.Ads
{
    /// <summary>
    /// Rewarded-ad boundary. Opening or closing an ad does not unlock a tank.
    /// The tank unlocks only from the reward callback. No ad SDK lives behind this interface yet.
    /// </summary>
    public interface IRewardedAdService
    {
        void Show(Action onRewardEarned, Action onClosedWithoutReward, Action onFailed);
    }
}
