namespace FishPuzzle.Tanks
{
    /// <summary>
    /// How long an extra-tank unlock lasts. The first implementation default is LevelAttempt.
    /// </summary>
    public enum TankUnlockScope
    {
        LevelAttempt = 0,
        Level = 1,
        Permanent = 2
    }
}
