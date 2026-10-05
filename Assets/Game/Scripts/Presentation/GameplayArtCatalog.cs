using System.Collections.Generic;
using FishPuzzle.Core;
using UnityEngine;

namespace FishPuzzle.Presentation
{
    /// <summary>
    /// Serialized gameplay presentation sprites.
    /// Required references are the visual foundation. Optional references may be absent in M1.
    /// </summary>
    [CreateAssetMenu(fileName = "GameplayArtCatalog", menuName = "Fish Puzzle/Gameplay Art Catalog")]
    public sealed class GameplayArtCatalog : ScriptableObject
    {
        [Header("Required")]
        [SerializeField] private Sprite _gameplayBackground;
        [SerializeField] private Sprite _bubbleBack;
        [SerializeField] private Sprite _bubbleFront;
        [SerializeField] private Sprite _bubbleFrostOverlay;
        [SerializeField] private Sprite _bubblePopParticle;
        [SerializeField] private Sprite _smallBubbleParticle;
        [SerializeField] private Sprite _tankBack;
        [SerializeField] private Sprite _tankFrontGlass;
        [SerializeField] private Sprite _tankLockedPlus;
        [SerializeField] private Sprite _tankShelf;
        [SerializeField] private Sprite _targetBadgeFrame;
        [SerializeField] private Sprite _levelHudPanel;
        [SerializeField] private Sprite _currencyCounterFrame;
        [SerializeField] private Sprite _coinIcon;
        [SerializeField] private Sprite _greenPlusIcon;
        [SerializeField] private Sprite _heartIcon;
        [SerializeField] private Sprite _brokenHeartIcon;
        [SerializeField] private Sprite _globalProgressPanel;
        [SerializeField] private Sprite _settingsButton;
        [SerializeField] private Sprite _gearIcon;
        [SerializeField] private Sprite _lockedButton;
        [SerializeField] private Sprite _lockIcon;

        [Header("Optional")]
        [SerializeField] private Sprite _tankWaterHighlight;
        [SerializeField] private Sprite _waitingTraySlot;
        [SerializeField] private Sprite _waitingTrayShelf;
        [SerializeField] private Sprite _winBackground;
        [SerializeField] private Sprite _snowflakeIcon;
        [SerializeField] private Sprite _touchRipple;
        [SerializeField] private Sprite _tankSplash;
        [SerializeField] private Sprite _splashDroplet;

        public Sprite GameplayBackground => _gameplayBackground;
        public Sprite BubbleBack => _bubbleBack;
        public Sprite BubbleFront => _bubbleFront;
        public Sprite BubbleFrostOverlay => _bubbleFrostOverlay;
        public Sprite BubblePopParticle => _bubblePopParticle;
        public Sprite SmallBubbleParticle => _smallBubbleParticle;
        public Sprite TankBack => _tankBack;
        public Sprite TankFrontGlass => _tankFrontGlass;
        public Sprite TankLockedPlus => _tankLockedPlus;
        public Sprite TankShelf => _tankShelf;
        public Sprite TargetBadgeFrame => _targetBadgeFrame;
        public Sprite LevelHudPanel => _levelHudPanel;
        public Sprite CurrencyCounterFrame => _currencyCounterFrame;
        public Sprite CoinIcon => _coinIcon;
        public Sprite GreenPlusIcon => _greenPlusIcon;
        public Sprite HeartIcon => _heartIcon;
        public Sprite BrokenHeartIcon => _brokenHeartIcon;
        public Sprite GlobalProgressPanel => _globalProgressPanel;
        public Sprite SettingsButton => _settingsButton;
        public Sprite GearIcon => _gearIcon;
        public Sprite LockedButton => _lockedButton;
        public Sprite LockIcon => _lockIcon;

        public Sprite TankWaterHighlight => _tankWaterHighlight;
        public Sprite WaitingTraySlot => _waitingTraySlot;
        public Sprite WaitingTrayShelf => _waitingTrayShelf;
        public Sprite WinBackground => _winBackground;
        public Sprite SnowflakeIcon => _snowflakeIcon;
        public Sprite TouchRipple => _touchRipple;
        public Sprite TankSplash => _tankSplash;
        public Sprite SplashDroplet => _splashDroplet;

        public List<string> GetMissingRequiredReferences()
        {
            var missing = new List<string>();
            AddIfMissing(missing, _gameplayBackground, nameof(GameplayBackground));
            AddIfMissing(missing, _bubbleBack, nameof(BubbleBack));
            AddIfMissing(missing, _bubbleFront, nameof(BubbleFront));
            AddIfMissing(missing, _bubbleFrostOverlay, nameof(BubbleFrostOverlay));
            AddIfMissing(missing, _bubblePopParticle, nameof(BubblePopParticle));
            AddIfMissing(missing, _smallBubbleParticle, nameof(SmallBubbleParticle));
            AddIfMissing(missing, _tankBack, nameof(TankBack));
            AddIfMissing(missing, _tankFrontGlass, nameof(TankFrontGlass));
            AddIfMissing(missing, _tankLockedPlus, nameof(TankLockedPlus));
            AddIfMissing(missing, _tankShelf, nameof(TankShelf));
            AddIfMissing(missing, _targetBadgeFrame, nameof(TargetBadgeFrame));
            AddIfMissing(missing, _levelHudPanel, nameof(LevelHudPanel));
            AddIfMissing(missing, _currencyCounterFrame, nameof(CurrencyCounterFrame));
            AddIfMissing(missing, _coinIcon, nameof(CoinIcon));
            AddIfMissing(missing, _greenPlusIcon, nameof(GreenPlusIcon));
            AddIfMissing(missing, _heartIcon, nameof(HeartIcon));
            AddIfMissing(missing, _brokenHeartIcon, nameof(BrokenHeartIcon));
            AddIfMissing(missing, _globalProgressPanel, nameof(GlobalProgressPanel));
            AddIfMissing(missing, _settingsButton, nameof(SettingsButton));
            AddIfMissing(missing, _gearIcon, nameof(GearIcon));
            AddIfMissing(missing, _lockedButton, nameof(LockedButton));
            AddIfMissing(missing, _lockIcon, nameof(LockIcon));
            return missing;
        }

        private void OnValidate()
        {
            if (!HasAnyAssignedSprite())
            {
                return;
            }

            var missing = GetMissingRequiredReferences();
            if (missing.Count == 0)
            {
                return;
            }

            GameLog.Error(
                nameof(GameplayArtCatalog),
                "Missing required sprites: " + string.Join(", ", missing) + ".");
        }

        private bool HasAnyAssignedSprite()
        {
            return _gameplayBackground != null
                || _bubbleBack != null
                || _bubbleFront != null
                || _bubbleFrostOverlay != null
                || _bubblePopParticle != null
                || _smallBubbleParticle != null
                || _tankBack != null
                || _tankFrontGlass != null
                || _tankLockedPlus != null
                || _tankShelf != null
                || _targetBadgeFrame != null
                || _levelHudPanel != null
                || _currencyCounterFrame != null
                || _coinIcon != null
                || _greenPlusIcon != null
                || _heartIcon != null
                || _brokenHeartIcon != null
                || _globalProgressPanel != null
                || _settingsButton != null
                || _gearIcon != null
                || _lockedButton != null
                || _lockIcon != null
                || _tankWaterHighlight != null
                || _waitingTraySlot != null
                || _waitingTrayShelf != null
                || _winBackground != null
                || _snowflakeIcon != null
                || _touchRipple != null
                || _tankSplash != null
                || _splashDroplet != null;
        }

        private static void AddIfMissing(List<string> missing, Sprite sprite, string name)
        {
            if (sprite == null)
            {
                missing.Add(name);
            }
        }
    }
}
