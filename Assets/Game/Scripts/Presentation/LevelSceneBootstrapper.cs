using FishPuzzle.Ads;
using FishPuzzle.Core;
using FishPuzzle.Domain;
using FishPuzzle.Progression;
using TMPro;
using UnityEngine;

namespace FishPuzzle.Presentation
{
    /// <summary>
    /// Builds the gameplay presentation from level data and starts the level attempt in Play Mode.
    /// </summary>
    public sealed class LevelSceneBootstrapper : MonoBehaviour
    {
        [SerializeField] private LevelData _level;
        [SerializeField] private GameConfig _config;
        [SerializeField] private FishVisualCatalog _fishCatalog;
        [SerializeField] private GameplayArtCatalog _artCatalog;
        [SerializeField] private GameplaySceneReferences _scene;
        [SerializeField] private GameObject _tankSlotPrefab;
        [SerializeField] private GameObject _badgePrefab;
        [SerializeField] private GameObject _bubblePrefab;
        [SerializeField] private GameObject _fishPrefab;
        [SerializeField] private int _previewGoldDisplay;
        [SerializeField] private int _previewLivesDisplay = 5;

        [Header("Runtime Inspection")]
        [SerializeField] private string _inspectedLevelId;
        [SerializeField] private int _inspectedVisibleBubbles;
        [SerializeField] private int _inspectedPendingBubbles;
        [SerializeField] private int _inspectedTankSlots;
        [SerializeField] private int _inspectedUnlockedTanks;
        [SerializeField] private int _inspectedWaitingTraySlots;
        [SerializeField] private string _inspectedProgress;

        private bool _initialized;
        private GameFlowController _flow;
        private ProgressionRuntime _progression;
        private MockRewardedAdService _rewardedAds;
        private TextMeshProUGUI _scoreLabel;

        public LevelData Level => _level;

        public bool IsInitialized => _initialized;

        public string LevelId => _level != null ? _level.LevelId : string.Empty;

        public int VisibleBubbleCount => _scene != null && _scene.BubblePile != null ? _scene.BubblePile.VisibleBubbleCount : 0;

        public int PendingBubbleCount => _scene != null && _scene.BubblePile != null ? _scene.BubblePile.PendingBubbleCount : 0;

        public int NextBubbleQueueIndex => _scene != null && _scene.BubblePile != null ? _scene.BubblePile.NextQueueIndex : 0;

        public int TankSlotCount => _scene != null && _scene.TankBoard != null ? _scene.TankBoard.Slots.Count : 0;

        public int UnlockedTankCount => _scene != null && _scene.TankBoard != null ? _scene.TankBoard.UnlockedPresentationCount : 0;

        public int WaitingTraySlotCount => _scene != null && _scene.WaitingTray != null ? _scene.WaitingTray.VisualSlotCount : 0;

        public int CollectedFishDisplay =>
            _flow != null && _flow.Session != null ? _flow.Session.Progress.CollectedFishCount : 0;

        public GameFlowController Flow => _flow;

        public ProgressionRuntime Progression => _progression;

        public string ScoreLabel => _scoreLabel != null ? _scoreLabel.text : string.Empty;

        public int TotalFishRequired => _level != null ? _level.TotalFishRequired : 0;

        public string GlobalProgressLabel =>
            _scene != null && _scene.GlobalProgressDisplay != null
                ? _scene.GlobalProgressDisplay.text
                : string.Empty;

        public TankBoardView TankBoard => _scene != null ? _scene.TankBoard : null;

        public WaitingTrayView WaitingTray => _scene != null ? _scene.WaitingTray : null;

        public BubblePileView BubblePile => _scene != null ? _scene.BubblePile : null;

        private void Awake()
        {
            Initialize();
        }

        public void Initialize()
        {
            if (_initialized)
            {
                GameLog.Warning(
                    nameof(LevelSceneBootstrapper),
                    "Initialize was requested again for " + LevelId + ". The existing presentation was left unchanged.");
                return;
            }

            if (!HasRequiredDependencies())
            {
                GameLog.Error(
                    nameof(LevelSceneBootstrapper),
                    "Gameplay presentation was not built. Assign LevelData, GameConfig, catalogs, scene references, and prefabs.");
                return;
            }

            if (_scene.Background != null && _artCatalog.GameplayBackground != null)
            {
                _scene.Background.sprite = _artCatalog.GameplayBackground;
            }

            _scene.LevelLabel.text = LevelPresentationFormatting.FormatLevelLabel(_level.LevelId);
            _scene.GoldDisplay.text = LevelPresentationFormatting.FormatCount(_previewGoldDisplay);
            _scene.LivesDisplay.text = LevelPresentationFormatting.FormatCount(_previewLivesDisplay);
            _scene.GlobalProgressDisplay.text = LevelPresentationFormatting.FormatProgress(CollectedFishDisplay, _level.TotalFishRequired);

            PresentAttemptVisuals();

            if (Application.isPlaying)
            {
                EnsureProgression();
                StartGameplay();
                BindLockedTankClicks();
                RefreshProgressionHud();
            }

            _initialized = true;
            CaptureInspection();
            GameLog.Info(
                nameof(LevelSceneBootstrapper),
                "Level " + _inspectedLevelId
                + " visible bubbles " + _inspectedVisibleBubbles
                + " pending bubbles " + _inspectedPendingBubbles
                + " tank slots " + _inspectedTankSlots
                + " unlocked tanks " + _inspectedUnlockedTanks
                + " waiting tray slots " + _inspectedWaitingTraySlots
                + " progress " + _inspectedProgress);
        }

        public void RetryAttempt()
        {
            if (!Application.isPlaying || !_initialized || _flow == null || _flow.State != GameState.Lose || _progression == null)
            {
                return;
            }

            _flow.AbandonAttemptVisuals();
            _progression.BeginAttempt();
            DestroyRemainingFish();
            PresentAttemptVisuals();
            BindLockedTankClicks();
            _flow.Begin(_level, _config, _scene, _fishCatalog);
            RefreshProgressionHud();
            CaptureInspection();
        }

        private void EnsureProgression()
        {
            if (_progression != null)
            {
                return;
            }

            _progression = new ProgressionRuntime(PlayerProgress.CreateDevelopmentDefaults());
            _progression.BeginAttempt();
            _rewardedAds = new MockRewardedAdService();
        }

        private void StartGameplay()
        {
            _flow = GetComponent<GameFlowController>();
            if (_flow == null)
            {
                _flow = gameObject.AddComponent<GameFlowController>();
            }

            _flow.Configure(_progression, _rewardedAds, _artCatalog, RetryAttempt, RefreshProgressionHud);
            _flow.Begin(_level, _config, _scene, _fishCatalog);
        }

        private void PresentAttemptVisuals()
        {
            _scene.TankBoard.PresentInitialBoard(
                _config.MaxTankSlots,
                _level.InitialUnlockedTankCount,
                _tankSlotPrefab,
                _badgePrefab,
                _level,
                _config,
                _fishCatalog);
            _scene.WaitingTray.PresentEmpty();
            _scene.BubblePile.PresentInitialQueue(
                _level.BubbleQueue,
                _level.PileLayout,
                _bubblePrefab,
                _fishPrefab,
                _fishCatalog);
        }

        private void BindLockedTankClicks()
        {
            if (_flow == null || _scene.TankBoard == null)
            {
                return;
            }

            var slots = _scene.TankBoard.Slots;
            for (var i = 0; i < slots.Count; i++)
            {
                if (slots[i] != null)
                {
                    slots[i].BindLockedClick(slot => _flow.OpenUnlockModal(slot));
                }
            }
        }

        private void RefreshProgressionHud()
        {
            if (_progression == null || _scene == null)
            {
                return;
            }

            if (_scene.GoldDisplay != null)
            {
                _scene.GoldDisplay.text = LevelPresentationFormatting.FormatCount(_progression.Progress.Gold);
            }

            if (_scene.LivesDisplay != null)
            {
                _scene.LivesDisplay.text = LevelPresentationFormatting.FormatCount(_progression.Progress.Lives);
            }

            EnsureScoreLabel();
            if (_scoreLabel != null)
            {
                _scoreLabel.text = LevelPresentationFormatting.FormatCount(_progression.Progress.Score);
            }
        }

        private void EnsureScoreLabel()
        {
            if (_scoreLabel != null || _scene.GoldDisplay == null)
            {
                return;
            }

            var goldPanel = _scene.GoldDisplay.transform.parent as RectTransform;
            var hud = goldPanel != null ? goldPanel.parent : _scene.GoldDisplay.transform.parent;
            if (hud == null)
            {
                return;
            }

            var labelObject = new GameObject("ScoreLabel", typeof(RectTransform));
            labelObject.SetActive(false);
            labelObject.transform.SetParent(hud, false);
            var rect = labelObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(-210f, 780f);
            rect.sizeDelta = new Vector2(220f, 70f);
            _scoreLabel = labelObject.AddComponent<TextMeshProUGUI>();
            _scoreLabel.font = _scene.GoldDisplay.font;
            _scoreLabel.fontSize = 40f;
            _scoreLabel.alignment = TMPro.TextAlignmentOptions.Center;
            _scoreLabel.color = new Color(0.18f, 0.16f, 0.12f, 1f);
            _scoreLabel.raycastTarget = false;
            _scoreLabel.text = LevelPresentationFormatting.FormatCount(_progression.Progress.Score);
            labelObject.SetActive(true);
        }

        private static void DestroyRemainingFish()
        {
            var fish = FindObjectsByType<FishView>(FindObjectsInactive.Include);
            for (var i = 0; i < fish.Length; i++)
            {
                if (fish[i] != null)
                {
                    DestroyImmediate(fish[i].gameObject);
                }
            }
        }

        private void CaptureInspection()
        {
            _inspectedLevelId = LevelId;
            _inspectedVisibleBubbles = VisibleBubbleCount;
            _inspectedPendingBubbles = PendingBubbleCount;
            _inspectedTankSlots = TankSlotCount;
            _inspectedUnlockedTanks = UnlockedTankCount;
            _inspectedWaitingTraySlots = WaitingTraySlotCount;
            _inspectedProgress = GlobalProgressLabel;
        }

        private bool HasRequiredDependencies()
        {
            return _level != null
                && _config != null
                && _fishCatalog != null
                && _artCatalog != null
                && _scene != null
                && _scene.HasCriticalReferences()
                && _tankSlotPrefab != null
                && _badgePrefab != null
                && _bubblePrefab != null
                && _fishPrefab != null
                && _level.PileLayout != null
                && _level.BubbleQueue != null
                && _level.TargetGroupQueue != null;
        }
    }
}
