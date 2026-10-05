namespace FishPuzzle.Tanks
{
    /// <summary>
    /// Logical state of one tank slot during a level attempt.
    /// </summary>
    public enum TankState
    {
        Locked = 0,
        WaitingForTarget = 1,
        AcceptingFish = 2,
        ResolvingTriple = 3,
        CompletedNoMoreTargets = 4
    }
}
