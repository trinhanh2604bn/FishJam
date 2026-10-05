namespace FishPuzzle.Domain
{
    /// <summary>
    /// Logical fish lifecycle from the tap contract.
    /// Tank and WaitingTray mean the fish is committed to that destination.
    /// </summary>
    public enum FishState
    {
        Idle = 0,
        Reserved = 1,
        InTransit = 2,
        Tank = 3,
        WaitingTray = 4,
        Consumed = 5
    }
}
