namespace FishPuzzle.Core
{
    /// <summary>
    /// High-level flow states. <see cref="LevelSession"/> owns the transitions for one attempt.
    /// </summary>
    public enum GameState
    {
        Boot = 0,
        LoadingLevel = 1,
        InitializingTanks = 2,
        InitializingPile = 3,
        PlayerInput = 4,
        RoutingFish = 5,
        ResolvingTank = 6,
        AssigningTarget = 7,
        AutoPromotingTray = 8,
        PoppingBubble = 9,
        SettlingBubblePile = 10,
        SpawningTopBubble = 11,
        TankUnlockModal = 12,
        Win = 13,
        Lose = 14,
        Paused = 15
    }
}
