using System;
using System.Collections;
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
    /// One Gameplay scene plays every level in <see cref="LevelCatalog"/>.
    /// Next Level, Replay, Retry, and Play Again rebuild the same scene objects in place.
    /// </summary>
    public sealed class LevelSceneBootstrapper : MonoBehaviour
    {
        [Tooltip("Ordered levels. When assigned, the start level comes from this catalog instead of the Level field.")]
        [SerializeField] private LevelCatalog _catalog;
        [SerializeField] private int _startLevelIndex;
        [Tooltip("Fallback level when no catalog is assigned. Overwritten at runtime by the catalog selection.")]
        [SerializeField] private LevelData _level;
        [SerializeField] private GameConfig _config;
        [SerializeField] private FishVisualCatalog _fishCatalog;
        [SerializeField] private GameplayArtCatalog _artCatalog;
        [SerializeField] private AnimationTuning _animationTuning;
        [SerializeField] private GameplaySceneReferences _scene;
        [SerializeField] private GameObject _tankSlotPrefab;
        [SerializeField] private GameObject _badgePrefab;
        [SerializeField] private GameObject _bubblePrefab;
        [SerializeField] private GameObject _fishPrefab;
        [SerializeField] private int _previewGoldDisplay;
        [SerializeField] private int _previewLivesDisplay = 5;
        [SerializeField] private float _transitionFadeSeconds = 0.25f;
        [SerializeField] private float _levelBannerSeconds = 1.1f;

        [Header("Runtime Inspection")]
        [SerializeField] private string _inspectedLevelId;
        [SerializeField] private int _inspectedVisibleBubbles;
        [SerializeField] private int _inspectedPendingBubbles;
        [SerializeField] private int _inspectedTankSlots;
        [SerializeField] private int _inspectedUnlockedTanks;
        [SerializeField] private int _inspectedWaitingTraySlots;
        [SerializeField] private string _inspectedProgress;

        /// <summary>
        /// Test and development hook. When set before the Gameplay scene loads, the next Initialize plays only
        /// this LevelData instead of the catalog. It is consumed once and cleared. Never set in shipping code.
        /// </summary>
        public static LevelData StartLevelOverride { get; set; }

        private bool _initialized;
        private GameFlowController _flow;
        private ProgressionRuntime _progression;
        private MockRewardedAdService _rewardedAds;
        private TextMeshProUGUI _scoreLabel;
        private LevelSequence _sequence;
        private LevelTransitionView _transition;
        private bool _transitioning;
        private int _levelLoadCount;

        public LevelData Level => _level;

        public LevelCatalog Catalog => _catalog;

        public LevelSequence Sequence => _sequence;

        public int CurrentLevelIndex => _sequence != null ? _sequence.CurrentIndex : 0;

        public int CurrentLevelNumber => _sequence != null ? _sequence.CurrentNumber : 1;

        public bool HasNextLevel => _sequence != null && _sequence.HasNext;

        public bool IsTransitioning => _transitioning;

        /// <summary>Number of attempt rebuilds after the first start (retry, replay, next level, play again).</summary>
        public int LevelLoadCount => _levelLoadCount;

        public LevelTransitionView Transition => _transition;

        public string LevelLabel => _scene != null && _scene.LevelLabel != null ? _scene.LevelLabel.text : string.Empty;

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

            SelectStartLevel();
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

            if (Application.isPlaying && !IsPlayable(_level))
            {
                return;
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
                ShowLevelBanner();
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

        /// <summary>
        /// Lose panel Retry. Rebuilds the same level immediately.
        /// Lives were already deducted once by the Lose transition and are not deducted again.
        /// </summary>
        public void RetryAttempt()
        {
            if (!CanRebuild() || _flow.State != GameState.Lose)
            {
                return;
            }

            RebuildAttempt(_sequence.CurrentIndex);
        }

        /// <summary>Win panel Next Level. Fades, rebuilds the scene with the next LevelData, then fades back.</summary>
        public bool RequestNextLevel()
        {
            if (!CanRebuild() || _flow.State != GameState.Win || !_sequence.HasNext)
            {
                return false;
            }

            StartCoroutine(TransitionTo(_sequence.CurrentIndex + 1));
            return true;
        }

        /// <summary>Win panel Replay. Replays the level that was just won.</summary>
        public bool RequestReplayLevel()
        {
            if (!CanRebuild() || _flow.State != GameState.Win)
            {
                return false;
            }

            StartCoroutine(TransitionTo(_sequence.CurrentIndex));
            return true;
        }

        /// <summary>Win panel Play Again after the last level. Restarts the sequence from the first level.</summary>
        public bool RequestPlayAgain()
        {
            if (!CanRebuild() || _flow.State != GameState.Win || _sequence.HasNext)
            {
                return false;
            }

            StartCoroutine(TransitionTo(0));
            return true;
        }

        private bool CanRebuild()
        {
            return Application.isPlaying
                && _initialized
                && !_transitioning
                && _flow != null
                && _progression != null
                && _sequence != null;
        }

        private IEnumerator TransitionTo(int levelIndex)
        {
            _transitioning = true;
            EnsureTransitionView();
            if (_transition != null)
            {
                yield return _transition.FadeOut(_transitionFadeSeconds);
            }

            try
            {
                RebuildAttempt(levelIndex);
            }
            catch (Exception exception)
            {
                GameLog.Error(nameof(LevelSceneBootstrapper), "Level transition failed: " + exception);
            }

            // Let destroyed objects from the previous attempt leave the hierarchy before revealing the board.
            yield return null;
            if (_transition != null)
            {
                yield return _transition.FadeIn(_transitionFadeSeconds);
                _transition.ClearFade();
            }

            _transitioning = false;
        }

        /// <summary>
        /// Development and test command. Rebuilds the scene with the catalog level at <paramref name="levelIndex"/>
        /// without a fade. Not available in release players.
        /// </summary>
        public bool DebugLoadLevel(int levelIndex)
        {
            if (!(Application.isEditor || Debug.isDebugBuild) || !CanRebuild() || _sequence.Get(levelIndex) == null)
            {
                return false;
            }

            RebuildAttempt(levelIndex);
            return _sequence.CurrentIndex == levelIndex;
        }

        private void RebuildAttempt(int levelIndex)
        {
            var next = _sequence.Get(levelIndex);
            if (!IsPlayable(next))
            {
                return;
            }

            _flow.AbandonAttemptVisuals();
            _progression.BeginAttempt();
            DestroyRemainingFish();
            SelectSequenceIndex(levelIndex);
            _level = next;
            _scene.LevelLabel.text = LevelPresentationFormatting.FormatLevelLabel(_level.LevelId);
            PresentAttemptVisuals();
            BindLockedTankClicks();
            _flow.Begin(_level, _config, _scene, _fishCatalog, _animationTuning);
            RefreshProgressionHud();
            _levelLoadCount++;
            CaptureInspection();
            ShowLevelBanner();
            GameLog.Info(
                nameof(LevelSceneBootstrapper),
                "Loaded " + _level.LevelId
                + " (" + CurrentLevelNumber + "/" + _sequence.Count + ")"
                + " progress " + _inspectedProgress
                + " visible bubbles " + _inspectedVisibleBubbles
                + " pending bubbles " + _inspectedPendingBubbles);
        }

        private void SelectSequenceIndex(int levelIndex)
        {
            if (levelIndex == _sequence.CurrentIndex)
            {
                return;
            }

            if (levelIndex < _sequence.CurrentIndex)
            {
                _sequence.Restart();
            }

            while (_sequence.CurrentIndex < levelIndex && _sequence.TryAdvance())
            {
            }
        }

        private void SelectStartLevel()
        {
            _sequence = null;
            var startOverride = StartLevelOverride;
            StartLevelOverride = null;
            if (startOverride != null)
            {
                _level = startOverride;
                _sequence = new LevelSequence(new[] { startOverride });
                return;
            }

            if (_catalog != null && _catalog.Count > 0)
            {
                try
                {
                    var start = Mathf.Clamp(_startLevelIndex, 0, _catalog.Count - 1);
                    _sequence = new LevelSequence(_catalog.Levels, start);
                    _level = _sequence.Current;
                    return;
                }
                catch (ArgumentException exception)
                {
                    GameLog.Error(
                        nameof(LevelSceneBootstrapper),
                        "LevelCatalog " + _catalog.name + " is not usable: " + exception.Message + " Falling back to the Level field.");
                }
            }

            if (_level != null)
            {
                _sequence = new LevelSequence(new[] { _level });
            }
        }

        private bool IsPlayable(LevelData level)
        {
            if (level == null || _config == null)
            {
                GameLog.Error(nameof(LevelSceneBootstrapper), "Level was not started. LevelData or GameConfig is missing.");
                return false;
            }

            var result = new LevelValidator().Validate(level, _config);
            if (result.IsValid)
            {
                return true;
            }

            for (var i = 0; i < result.Issues.Count; i++)
            {
                GameLog.Error(nameof(LevelSceneBootstrapper), level.LevelId + ": " + result.Issues[i].Message);
            }

            GameLog.Error(nameof(LevelSceneBootstrapper), "Level " + level.LevelId + " failed validation and was not started.");
            return false;
        }

        private void ShowLevelBanner()
        {
            EnsureTransitionView();
            if (_transition != null && _level != null)
            {
                _transition.ShowBanner(
                    "Level " + LevelPresentationFormatting.FormatLevelLabel(_level.LevelId),
                    _levelBannerSeconds);
            }
        }

        private void EnsureTransitionView()
        {
            if (_transition != null || _scene == null || _scene.GlobalProgressDisplay == null)
            {
                return;
            }

            var canvas = _scene.GlobalProgressDisplay.canvas;
            var overlay = canvas != null ? canvas.transform.Find("OverlayRoot") : null;
            var parent = overlay != null ? overlay : (canvas != null ? canvas.transform : _scene.GlobalProgressDisplay.transform);
            _transition = LevelTransitionView.Create(parent, _scene.GlobalProgressDisplay.font);
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
            _flow.ConfigureLevelFlow(
                () => RequestNextLevel(),
                () => RequestReplayLevel(),
                () => RequestPlayAgain(),
                () => HasNextLevel);
            _flow.Begin(_level, _config, _scene, _fishCatalog, _animationTuning);
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
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(-8f, -96f);
            rect.sizeDelta = new Vector2(150f, 56f);
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
                    SceneObjectCleanup.DestroyObject(fish[i].gameObject);
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
