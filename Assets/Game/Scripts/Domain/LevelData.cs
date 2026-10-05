using System.Collections.Generic;
using FishPuzzle.Bubbles;
using UnityEngine;

namespace FishPuzzle.Domain
{
    /// <summary>
    /// Authoring data for one level before gameplay begins.
    /// Validation lives in <see cref="LevelValidator"/>, not on this asset.
    /// </summary>
    [CreateAssetMenu(fileName = "Level", menuName = "Fish Puzzle/Level")]
    public sealed class LevelData : ScriptableObject
    {
        [Tooltip("Stable technical id, such as level_001. Not a display string.")]
        [SerializeField] private string _levelId;

        [Tooltip("Tanks unlocked when a fresh attempt of this level starts.")]
        [SerializeField] private int _initialUnlockedTankCount = 2;

        [Tooltip("Fish that must be collected to win. Must equal target group count times tank capacity.")]
        [SerializeField] private int _totalFishRequired;

        [Tooltip("One entry is one target group of GameConfig.TankCapacity fish, in assignment order.")]
        [SerializeField] private List<FishType> _targetGroupQueue = new List<FishType>();

        [Tooltip("Bubbles in deal order. The queue may be longer than the visible pile.")]
        [SerializeField] private List<BubbleDefinition> _bubbleQueue = new List<BubbleDefinition>();

        [SerializeField] private BubblePileLayout _pileLayout;

        public string LevelId => _levelId;

        public int InitialUnlockedTankCount => _initialUnlockedTankCount;

        public int TotalFishRequired => _totalFishRequired;

        public IReadOnlyList<FishType> TargetGroupQueue => _targetGroupQueue;

        public IReadOnlyList<BubbleDefinition> BubbleQueue => _bubbleQueue;

        public BubblePileLayout PileLayout => _pileLayout;
    }
}
