using FishPuzzle.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FishPuzzle.Presentation
{
    /// <summary>
    /// Explicit scene wiring for the gameplay canvas. Views must not search the scene for these objects.
    /// </summary>
    public sealed class GameplaySceneReferences : MonoBehaviour
    {
        [SerializeField] private Image _background;
        [SerializeField] private TextMeshProUGUI _levelLabel;
        [SerializeField] private TextMeshProUGUI _goldDisplay;
        [SerializeField] private TextMeshProUGUI _livesDisplay;
        [SerializeField] private TextMeshProUGUI _globalProgressDisplay;
        [SerializeField] private TankBoardView _tankBoard;
        [SerializeField] private WaitingTrayView _waitingTray;
        [SerializeField] private BubblePileView _bubblePile;

        public Image Background => _background;

        public TextMeshProUGUI LevelLabel => _levelLabel;

        public TextMeshProUGUI GoldDisplay => _goldDisplay;

        public TextMeshProUGUI LivesDisplay => _livesDisplay;

        public TextMeshProUGUI GlobalProgressDisplay => _globalProgressDisplay;

        public TankBoardView TankBoard => _tankBoard;

        public WaitingTrayView WaitingTray => _waitingTray;

        public BubblePileView BubblePile => _bubblePile;

        private void OnValidate()
        {
            ReportMissingReferences();
        }

        private void Awake()
        {
            ReportMissingReferences();
        }

        public bool HasCriticalReferences()
        {
            return _background != null
                && _levelLabel != null
                && _goldDisplay != null
                && _livesDisplay != null
                && _globalProgressDisplay != null
                && _tankBoard != null
                && _waitingTray != null
                && _bubblePile != null;
        }

        private void ReportMissingReferences()
        {
            if (!HasAnyReference() || HasCriticalReferences())
            {
                return;
            }

            GameLog.Error(
                nameof(GameplaySceneReferences),
                "Gameplay scene is missing a critical reference. Assign background, HUD labels, tank board, waiting tray, and bubble pile.");
        }

        private bool HasAnyReference()
        {
            return _background != null
                || _levelLabel != null
                || _goldDisplay != null
                || _livesDisplay != null
                || _globalProgressDisplay != null
                || _tankBoard != null
                || _waitingTray != null
                || _bubblePile != null;
        }
    }
}
