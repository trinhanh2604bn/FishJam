using UnityEngine;

namespace FishPuzzle.Core
{
    /// <summary>
    /// Authoritative product configuration.
    /// Locked rules from cursor.md are stored here and on the default asset.
    /// Gameplay code must read this asset instead of copying those numbers.
    /// </summary>
    [CreateAssetMenu(fileName = "GameConfig", menuName = "Fish Puzzle/Game Config")]
    public sealed class GameConfig : ScriptableObject
    {
        [Header("Tanks")]
        [Tooltip("Physical tank positions on the shelf. Locked default is 4.")]
        [SerializeField] private int _maxTankSlots = 4;

        [Tooltip("Tanks unlocked at the start of a fresh attempt. Locked default is 2.")]
        [SerializeField] private int _defaultUnlockedTankCount = 2;

        [Tooltip("Fish required to complete one tank target group. Locked default is 3.")]
        [SerializeField] private int _tankCapacity = 3;

        [Header("Waiting Tray")]
        [Tooltip("Visual and logical waiting-tray slots. Locked default is 5.")]
        [SerializeField] private int _waitingTraySlotCount = 5;

        [Tooltip("Waiting fish that immediately lose the level. Locked default is 5.")]
        [SerializeField] private int _waitingTrayFailCount = 5;

        [Header("Progression")]
        [Tooltip("Score awarded once for a level win. Locked default is 20.")]
        [SerializeField] private int _levelCompleteScoreReward = 20;

        [Tooltip("Lives removed once for a level loss. Locked default is 1.")]
        [SerializeField] private int _loseLifeCost = 1;

        [Tooltip("Gold charged to unlock one extra tank. Locked default is 600.")]
        [SerializeField] private int _tankUnlockGoldCost = 600;

        [Tooltip("Rewarded-ad extra-tank unlock is allowed. The ad SDK is out of scope for this milestone.")]
        [SerializeField] private bool _extraTankAdUnlockAllowed = true;

        [Header("Bubbles")]
        [Tooltip("Distinct fish types required in a standard bubble. Locked default is 3.")]
        [SerializeField] private int _distinctFishTypesPerBubble = 3;

        [Tooltip("Maximum fish authored inside one bubble. Locked default is 5. Harder levels add bubbles, not fish per bubble.")]
        [SerializeField] private int _maxFishPerBubble = 5;

        [Header("Disabled Rules")]
        [Tooltip("Out-of-space rescue is disabled. Reaching the waiting-tray fail count loses immediately.")]
        [SerializeField] private bool _outOfSpaceRescueEnabled = false;

        [Tooltip("Fail timer is disabled. Levels do not fail by countdown.")]
        [SerializeField] private bool _failTimerEnabled = false;

        public int MaxTankSlots => _maxTankSlots;

        public int DefaultUnlockedTankCount => _defaultUnlockedTankCount;

        public int TankCapacity => _tankCapacity;

        public int WaitingTraySlotCount => _waitingTraySlotCount;

        public int WaitingTrayFailCount => _waitingTrayFailCount;

        public int LevelCompleteScoreReward => _levelCompleteScoreReward;

        public int LoseLifeCost => _loseLifeCost;

        public int TankUnlockGoldCost => _tankUnlockGoldCost;

        public bool ExtraTankAdUnlockAllowed => _extraTankAdUnlockAllowed;

        public int DistinctFishTypesPerBubble => _distinctFishTypesPerBubble;

        public int MaxFishPerBubble => _maxFishPerBubble;

        public bool OutOfSpaceRescueEnabled => _outOfSpaceRescueEnabled;

        public bool FailTimerEnabled => _failTimerEnabled;

        private void OnValidate()
        {
            ReportUnlessPositive(nameof(MaxTankSlots), _maxTankSlots);
            ReportUnlessPositive(nameof(TankCapacity), _tankCapacity);
            ReportUnlessPositive(nameof(WaitingTraySlotCount), _waitingTraySlotCount);
            ReportUnlessPositive(nameof(WaitingTrayFailCount), _waitingTrayFailCount);
            ReportUnlessPositive(nameof(DistinctFishTypesPerBubble), _distinctFishTypesPerBubble);
            ReportUnlessPositive(nameof(MaxFishPerBubble), _maxFishPerBubble);

            ReportIfNegative(nameof(LevelCompleteScoreReward), _levelCompleteScoreReward);
            ReportIfNegative(nameof(LoseLifeCost), _loseLifeCost);
            ReportIfNegative(nameof(TankUnlockGoldCost), _tankUnlockGoldCost);

            if (_defaultUnlockedTankCount < 0 || _defaultUnlockedTankCount > _maxTankSlots)
            {
                GameLog.Error(
                    nameof(GameConfig),
                    $"{nameof(DefaultUnlockedTankCount)} must be from 0 through {nameof(MaxTankSlots)} ({_maxTankSlots}). Current value: {_defaultUnlockedTankCount}. The value was left unchanged.");
            }

            if (_waitingTraySlotCount > 0 && _waitingTrayFailCount > _waitingTraySlotCount)
            {
                GameLog.Error(
                    nameof(GameConfig),
                    $"{nameof(WaitingTrayFailCount)} ({_waitingTrayFailCount}) is above {nameof(WaitingTraySlotCount)} ({_waitingTraySlotCount}), so the tray cannot reach the fail threshold. Both values were left unchanged.");
            }
        }

        private static void ReportUnlessPositive(string fieldName, int value)
        {
            if (value > 0)
            {
                return;
            }

            GameLog.Error(
                nameof(GameConfig),
                $"{fieldName} must be greater than 0. Current value: {value}. The value was left unchanged.");
        }

        private static void ReportIfNegative(string fieldName, int value)
        {
            if (value >= 0)
            {
                return;
            }

            GameLog.Error(
                nameof(GameConfig),
                $"{fieldName} must be greater than or equal to 0. Current value: {value}. The value was left unchanged.");
        }
    }
}
