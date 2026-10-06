using FishPuzzle.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FishPuzzle.Tests.EditMode
{
    public sealed class GameConfigBootstrapTests
    {
        private const string AssetPath = "Assets/Game/Data/Config/GameConfig.asset";

        [Test]
        public void DefaultAsset_MatchesLockedProductRules()
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameConfig>(AssetPath);

            Assert.That(asset, Is.Not.Null, "Missing GameConfig asset at " + AssetPath + ".");
            AssertLockedValues(asset);
        }

        [Test]
        public void NewInstance_UsesLockedProductDefaults()
        {
            var config = ScriptableObject.CreateInstance<GameConfig>();
            try
            {
                AssertLockedValues(config);
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

        private static void AssertLockedValues(GameConfig config)
        {
            Assert.That(config.MaxTankSlots, Is.EqualTo(4));
            Assert.That(config.DefaultUnlockedTankCount, Is.EqualTo(2));
            Assert.That(config.TankCapacity, Is.EqualTo(3));
            Assert.That(config.WaitingTraySlotCount, Is.EqualTo(5));
            Assert.That(config.WaitingTrayFailCount, Is.EqualTo(5));
            Assert.That(config.LevelCompleteGoldReward, Is.EqualTo(20));
            Assert.That(config.LoseLifeCost, Is.EqualTo(1));
            Assert.That(config.TankUnlockGoldCost, Is.EqualTo(600));
            Assert.That(config.DistinctFishTypesPerBubble, Is.EqualTo(3));
            Assert.That(config.MaxFishPerBubble, Is.EqualTo(5));
            Assert.That(config.ExtraTankAdUnlockAllowed, Is.True);
            Assert.That(config.OutOfSpaceRescueEnabled, Is.False);
            Assert.That(config.FailTimerEnabled, Is.False);
        }
    }
}
