using System;
using System.Collections;
using System.Collections.Generic;
using FishPuzzle.Ads;
using FishPuzzle.Bubbles;
using FishPuzzle.Domain;
using FishPuzzle.Presentation;
using FishPuzzle.Progression;
using FishPuzzle.Tanks;
using UnityEngine;

namespace FishPuzzle.Core
{
    /// <summary>
    /// Binds one level attempt to the gameplay scene.
    /// Domain resolution finishes before presentation. Motion follows that result and snaps if a tween is interrupted.
    /// </summary>
    public sealed class GameFlowController : MonoBehaviour
    {
        private const int MaxTrailEmitsPerFrame = 6;
        private const float TrailSparkleFromProgress = 0.7f;
        private const int TrailSparkleEvery = 3;
        private const int LandingBubbleCount = 12;
        private const int LandingSparkleCount = 4;

        private readonly Dictionary<int, FishView> _viewsByFishId = new Dictionary<int, FishView>();
        private readonly List<GameObject> _ephemeral = new List<GameObject>();
        private readonly List<RectTransform> _reflowRects = new List<RectTransform>();
        private readonly List<Vector2> _reflowOrigins = new List<Vector2>();
        private readonly List<Vector2> _reflowTargets = new List<Vector2>();
        private readonly List<BubbleView> _introBubbles = new List<BubbleView>();
        private Coroutine _introRoutine;

        private LevelSession _session;
        private GameplaySceneReferences _scene;
        private FishVisualCatalog _fishCatalog;
        private GameplayArtCatalog _art;
        private AnimationTuning _tuning;
        private ProgressionRuntime _progression;
        private IRewardedAdService _rewardedAds;
        private Action _retryAttempt;
        private Action _progressionChanged;
        private Action _nextLevel;
        private Action _replayLevel;
        private Action _playAgain;
        private Func<bool> _hasNextLevel;
        private Func<int> _levelNumber;
        private Func<int> _levelCount;
        private LosePanelView _losePanel;
        private WinPanelView _winPanel;
        private UnlockModalView _unlockModal;
        private RectTransform _flightLayer;
        private BubbleView _poppingBubble;
        private bool _visualLock;
        private bool _presentationFinished = true;
        private bool _suppressOutcome;
        private bool _suppressPopVfx;
        private bool _suppressLandingSplash;
        private bool _suppressTouchVfx;
        private float _presentationElapsed;
        private FishView _pressedFish;
        private TouchFeedbackController _touch;
        private int _acceptedRouteCount;
        private int _tankSplashCount;
        private int _bubbleBurstEmissions;
        private float _lastBubblePopSeconds;
        private BubbleBurstPool _bursts;
        private readonly ComboStreakTracker _combo = new ComboStreakTracker();
        private AudioFeedbackService _audio;
        private HapticFeedbackService _haptics;
        private FishTrailEmitter _trail;
        private int _trailSampleMark;
        private int _trailSparkleTick;
        private ComboFeedbackView _comboView;
        private Func<float> _presentationClock;
        private bool _suppressJuiceVfx;
        private bool _outcomeSoundPlayed;
        private int _landingFxCount;
        private int _comboResetCount;
        private int _promotionCompletionCount;
        private ComboTier _lastComboTier;
        private int _frozenRejectCount;
        private int _iceChipPresentedCount;
        private int _iceBreakPresentedCount;

        public LevelSession Session => _session;

        public GameState State => _session != null ? _session.State : GameState.Boot;

        public bool IsLosePanelVisible => _losePanel != null && _losePanel.IsShown;

        public bool IsWinPanelVisible => _winPanel != null && _winPanel.IsShown;

        public bool IsPresentationBusy => _visualLock;

        /// <summary>True while the opening pile drop is playing. Input stays open; any action snaps the drop first.</summary>
        public bool IsLevelIntroPlaying => _introRoutine != null;

        public bool IsFishPressCaptured => _pressedFish != null;

        public int AcceptedRouteCount => _acceptedRouteCount;

        public int TankSplashCount => _tankSplashCount;

        public int BubbleBurstEmissionCount => _bubbleBurstEmissions;

        public int ActiveBubbleBurstCount => _bursts != null ? _bursts.ActiveCount : 0;

        public float LastBubblePopSeconds => _lastBubblePopSeconds;

        public TouchFeedbackController TouchFeedback => _touch;

        public bool IsUnlockModalVisible => _unlockModal != null && _unlockModal.IsShown;

        public UnlockModalView UnlockModal => _unlockModal;

        public WinPanelView WinPanel => _winPanel;

        public LosePanelView LosePanel => _losePanel;

        public AudioFeedbackService Audio => _audio;

        public HapticFeedbackService Haptics => _haptics;

        public AnimationTuning Tuning => _tuning;

        public FishTrailEmitter Trail => _trail;

        /// <summary>Trail bubbles requested along fish flights this attempt.</summary>
        public int TrailEmitCount => _trail != null ? _trail.EmitCount : 0;

        /// <summary>Correct tank landings that played the landing FX (splash, droplets, ripple, bounce, bubble burst, sound).</summary>
        public int LandingFxCount => _landingFxCount;

        public int ComboStreak => _combo.Streak;

        public float ComboWindowSeconds => _combo.Window;

        public ComboTier LastComboTier => _lastComboTier;

        /// <summary>Presses rejected because the fish sits in a Frozen Bubble this attempt.</summary>
        public int FrozenRejectCount => _frozenRejectCount;

        /// <summary>Frozen Bubble counter decrements presented this attempt (breaks excluded).</summary>
        public int IceChipPresentedCount => _iceChipPresentedCount;

        /// <summary>Frozen Bubbles turned into normal bubbles this attempt.</summary>
        public int IceBreakPresentedCount => _iceBreakPresentedCount;

        public int ComboResetCount => _comboResetCount;

        /// <summary>Tank completions reached through Waiting Tray auto-promotion this attempt.</summary>
        public int PromotionCompletionCount => _promotionCompletionCount;

        public ComboFeedbackView ComboView => _comboView;

        /// <summary>Presentation-only audio. Null is allowed and plays nothing.</summary>
        public void ConfigureFeedback(AudioFeedbackService audio)
        {
            _audio = audio;
        }

        /// <summary>Presentation-only vibration. Null is allowed and vibrates nothing.</summary>
        public void ConfigureHaptics(HapticFeedbackService haptics)
        {
            _haptics = haptics;
        }

        /// <summary>Test hook: replaces the clock used for the combo window. Null restores unscaled time.</summary>
        public void SetPresentationClock(Func<float> clock)
        {
            _presentationClock = clock;
        }

        /// <summary>Disables the M13 trail, landing bubble burst and combo text for this attempt (missing-VFX checks).</summary>
        public void SuppressJuiceVfx()
        {
            _suppressJuiceVfx = true;
            if (_trail != null)
            {
                _trail.ReleaseAll();
            }

            if (_comboView != null)
            {
                _comboView.Clear();
            }
        }

        public void Configure(
            ProgressionRuntime progression,
            IRewardedAdService rewardedAds,
            GameplayArtCatalog art,
            Action retryAttempt,
            Action progressionChanged)
        {
            _progression = progression;
            _rewardedAds = rewardedAds;
            _art = art;
            _retryAttempt = retryAttempt;
            _progressionChanged = progressionChanged;
        }

        /// <summary>
        /// Win panel requests. The level flow owner (bootstrapper) rebuilds the attempt.
        /// This controller never chooses the next LevelData itself.
        /// </summary>
        public void ConfigureLevelFlow(Action nextLevel, Action replayLevel, Action playAgain, Func<bool> hasNextLevel)
        {
            _nextLevel = nextLevel;
            _replayLevel = replayLevel;
            _playAgain = playAgain;
            _hasNextLevel = hasNextLevel;
        }

        /// <summary>Level number and sequence length for result headers and the win progress bar.</summary>
        public void ConfigureLevelInfo(Func<int> levelNumber, Func<int> levelCount)
        {
            _levelNumber = levelNumber;
            _levelCount = levelCount;
        }

        public void SuppressBubblePopVfx()
        {
            _suppressPopVfx = true;
        }

        public void SuppressLandingAndTouchVfx()
        {
            _suppressLandingSplash = true;
            _suppressTouchVfx = true;
            if (_touch != null)
            {
                _touch.Suppress();
            }
        }

        public void CompletePresentationNow()
        {
            FinishLevelIntro();
            if (_presentationFinished && !_visualLock)
            {
                return;
            }

            StopAllCoroutines();
            FinishPresentation();
        }

        public void AbandonAttemptVisuals()
        {
            ReleaseCapturedPress();
            if (_touch != null)
            {
                _touch.Clear();
            }

            _suppressOutcome = true;
            CompletePresentationNow();
            _visualLock = false;
            _presentationFinished = true;
            if (_session != null)
            {
                _session.ReleasePresentationHold();
            }

            _viewsByFishId.Clear();
            _session = null;
            CleanupEphemeral();
            if (_bursts != null)
            {
                _bursts.ReleaseAll();
            }

            ResetCombo();
            if (_trail != null)
            {
                _trail.ReleaseAll();
            }

            if (_losePanel != null)
            {
                _losePanel.Hide();
            }

            if (_winPanel != null)
            {
                _winPanel.Hide();
            }

            if (_unlockModal != null)
            {
                _unlockModal.Hide();
            }
        }

        public void Begin(LevelData level, GameConfig config, GameplaySceneReferences scene, FishVisualCatalog fishCatalog, AnimationTuning animationTuning)
        {
            if (_session != null)
            {
                return;
            }

            if (level == null || config == null || scene == null || scene.BubblePile == null || scene.TankBoard == null || scene.WaitingTray == null)
            {
                GameLog.Error(nameof(GameFlowController), "Gameplay flow did not start. Level, config, or scene references are missing.");
                return;
            }

            _scene = scene;
            _fishCatalog = fishCatalog;
            _suppressOutcome = false;
            _suppressPopVfx = false;
            _suppressLandingSplash = false;
            _suppressTouchVfx = false;
            _pressedFish = null;
            _acceptedRouteCount = 0;
            _tankSplashCount = 0;
            _bubbleBurstEmissions = 0;
            _lastBubblePopSeconds = 0f;
            if (_bursts != null)
            {
                _bursts.ReleaseAll();
            }

            _suppressJuiceVfx = false;
            _outcomeSoundPlayed = false;
            _landingFxCount = 0;
            _promotionCompletionCount = 0;
            _frozenRejectCount = 0;
            _iceChipPresentedCount = 0;
            _iceBreakPresentedCount = 0;
            ResetCombo();
            if (_trail != null)
            {
                _trail.ReleaseAll();
                _trail.ResetCounters();
            }

            _presentationFinished = true;
            _visualLock = false;
            if (animationTuning != null)
            {
                _tuning = animationTuning;
            }

            if (_tuning == null)
            {
                _tuning = AnimationTuning.RuntimeDefault();
            }

            _session = LevelSession.Start(level, config, false);
            _session.SetOutcomeHandler(OnAuthoritativeOutcome);
            EnsureTouchFeedback();
            EnsureTrail();
            BindVisibleFish();
            SyncIceViews();
            RefreshAllBadges();
            RefreshProgress();
            StartLevelIntro();
        }

        public bool OpenUnlockModal(int slotIndex)
        {
            if (_visualLock || _session == null || !_session.TryBeginUnlockModal(slotIndex))
            {
                return false;
            }

            FinishLevelIntro();

            ReleaseCapturedPress();

            EnsureUnlockModal();
            _unlockModal.Show(_session.Config.TankUnlockGoldCost);
            return true;
        }

        public TankUnlockResult ConfirmGoldUnlock()
        {
            if (_session == null || _progression == null)
            {
                return TankUnlockResult.Reject(-1);
            }

            var result = _session.TryUnlockWithGold(_session.PendingUnlockSlot, _progression.Progress, _progression.Wallet);
            _progressionChanged?.Invoke();
            if (result.InsufficientGold)
            {
                if (_unlockModal != null)
                {
                    _unlockModal.ShowInsufficientGold();
                }

                Sfx(SfxId.InsufficientGold);

                return result;
            }

            if (!result.Succeeded)
            {
                return result;
            }

            Sfx(SfxId.UnlockTank);
            if (_unlockModal != null)
            {
                _unlockModal.Hide();
            }

            ApplyExternalResolution();
            return result;
        }

        public void ConfirmRewardUnlock()
        {
            if (_session == null || _progression == null)
            {
                return;
            }

            var slot = _session.PendingUnlockSlot;
            var goldBefore = _progression.Progress.Gold;
            if (_rewardedAds == null || !_session.Config.ExtraTankAdUnlockAllowed)
            {
                return;
            }

            _rewardedAds.Show(
                () =>
                {
                    if (_session == null || _progression.Progress.Gold != goldBefore)
                    {
                        return;
                    }

                    var result = _session.TryUnlockWithReward(slot);
                    if (!result.Succeeded)
                    {
                        return;
                    }

                    Sfx(SfxId.UnlockTank);
                    if (_unlockModal != null)
                    {
                        _unlockModal.Hide();
                    }

                    ApplyExternalResolution();
                },
                () => { },
                () => { });
        }

        public void CloseUnlockModal()
        {
            if (_session != null)
            {
                _session.CancelUnlockModal();
            }

            if (_unlockModal != null)
            {
                _unlockModal.Hide();
            }
        }

        public void PresentCurrentOutcome()
        {
            if (_session == null)
            {
                return;
            }

            if (_session.State == GameState.Win)
            {
                ShowWin();
                return;
            }

            if (_session.State == GameState.Lose)
            {
                ShowLose();
            }
        }

        public void ApplyExternalResolution()
        {
            if (_session == null || _visualLock)
            {
                return;
            }

            FinishLevelIntro();
            _visualLock = true;
            _presentationFinished = false;
            _presentationElapsed = 0f;
            _session.HoldForPresentation(GameState.AutoPromotingTray);
            StartCoroutine(PlayExternal());
        }

        private void Update()
        {
            if (_combo.Streak > 0)
            {
                _combo.Expire(PresentationNow());
            }

            if (!_visualLock || _presentationFinished || _tuning == null)
            {
                return;
            }

            _presentationElapsed += Time.unscaledDeltaTime;
            if (_presentationElapsed <= _tuning.PresentationSafetySeconds)
            {
                return;
            }

            if (!_suppressOutcome)
            {
                GameLog.Error(
                    nameof(GameFlowController),
                    "Presentation exceeded its safety window and snapped to the authoritative state.");
            }

            CompletePresentationNow();
        }

        private void BindVisibleFish()
        {
            var visible = _scene.BubblePile.VisibleBubbles;
            for (var i = 0; i < visible.Count; i++)
            {
                var bubbleView = visible[i];
                if (bubbleView == null)
                {
                    continue;
                }

                BubbleRuntimeState bubble = null;
                var bubbles = _session.Bubbles;
                for (var b = 0; b < bubbles.Count; b++)
                {
                    if (bubbles[b] != null && bubbles[b].BubbleId == bubbleView.BubbleId)
                    {
                        bubble = bubbles[b];
                        break;
                    }
                }

                if (bubble == null)
                {
                    GameLog.Error(nameof(GameFlowController), "Visible bubble " + bubbleView.BubbleId + " has no runtime contents.");
                    continue;
                }

                _session.SetBubbleInPlay(bubble.BubbleId, true);
                var views = bubbleView.FishViews;
                var fish = bubble.Fish;
                if (views.Count != fish.Count)
                {
                    GameLog.Error(
                        nameof(GameFlowController),
                        "Bubble " + bubble.BubbleId + " has " + views.Count + " fish views and " + fish.Count + " runtime fish.");
                }

                var count = views.Count < fish.Count ? views.Count : fish.Count;
                for (var f = 0; f < count; f++)
                {
                    var view = views[f];
                    var runtimeFish = fish[f];
                    if (view == null || runtimeFish == null)
                    {
                        continue;
                    }

                    BindFishPointer(view, runtimeFish.Id, true);
                    _viewsByFishId.Add(runtimeFish.Id, view);
                }
            }
        }

        private void BindFishPointer(FishView view, int fishId, bool enableInput)
        {
            if (view == null)
            {
                return;
            }

            view.SetPresentationTuning(_tuning);
            view.BindInteraction(fishId, TryBeginFishPress, CommitPressedFish, CancelPressedFish);
            if (!enableInput)
            {
                view.ReleaseInteraction();
            }
        }

        private bool TryBeginFishPress(FishView view)
        {
            if (view == null || _visualLock || _pressedFish != null || _session == null || _session.State != GameState.PlayerInput)
            {
                return false;
            }

            if (_session.IsFishFrozen(view.FishId))
            {
                RejectFrozenPress(view);
                return false;
            }

            _pressedFish = view;
            view.BeginPress(_tuning);
            Sfx(SfxId.FishPress);
            Haptic(HapticKind.FishPress);
            return true;
        }

        /// <summary>Frozen fish: no reservation, no routing, no tray/tank change. Only a wobble and an ice click.</summary>
        private void RejectFrozenPress(FishView view)
        {
            _frozenRejectCount++;
            var bubble = view != null ? view.GetComponentInParent<BubbleView>() : null;
            if (bubble != null)
            {
                bubble.PlayFrozenReject();
            }

            Sfx(SfxId.ButtonTap, 1.7f, 0.55f);
        }

        /// <summary>Counter punch for each chipped Frozen Bubble; ice break when a counter reached 0.</summary>
        private void PresentIceUpdates(TurnResolution turn)
        {
            var pile = _scene != null ? _scene.BubblePile : null;
            if (turn == null || pile == null || turn.IceUpdates.Count == 0)
            {
                return;
            }

            var broke = false;
            for (var i = 0; i < turn.IceUpdates.Count; i++)
            {
                var update = turn.IceUpdates[i];
                if (update == null || !pile.TryGetByBubbleId(update.BubbleId, out var view) || view == null)
                {
                    continue;
                }

                if (update.Broke)
                {
                    broke = true;
                    _iceBreakPresentedCount++;
                    view.PlayIceBreak(_suppressJuiceVfx ? null : IceSparkleSprite());
                }
                else
                {
                    _iceChipPresentedCount++;
                    view.PlayIceChip(update.Remaining);
                }
            }

            if (broke)
            {
                Sfx(SfxId.BubblePop, 1.5f, 0.8f);
            }
            else
            {
                Sfx(SfxId.ButtonTap, 1.35f, 0.7f);
            }
        }

        private Sprite IceSparkleSprite()
        {
            if (_art == null)
            {
                return null;
            }

            return _art.SnowflakeIcon != null ? _art.SnowflakeIcon : _art.SmallBubbleParticle;
        }

        /// <summary>Snaps every visible bubble's frost overlay and counter to the session.</summary>
        private void SyncIceViews()
        {
            if (_scene == null || _scene.BubblePile == null)
            {
                return;
            }

            var visible = _scene.BubblePile.VisibleBubbles;
            for (var i = 0; i < visible.Count; i++)
            {
                SyncIceView(visible[i]);
            }
        }

        private void SyncIceView(BubbleView view)
        {
            if (view == null || _session == null)
            {
                return;
            }

            var runtime = _session.FindBubble(view.BubbleId);
            var remaining = runtime != null ? runtime.IceSelectionsRemaining : 0;
            if (remaining <= 0 && !view.IsIceShown)
            {
                return;
            }

            var font = _scene != null && _scene.GlobalProgressDisplay != null ? _scene.GlobalProgressDisplay.font : null;
            view.ShowIce(remaining, _art != null ? _art.BubbleFrostOverlay : null, font);
        }

        private void CommitPressedFish(FishView view)
        {
            if (_pressedFish != view || view == null)
            {
                return;
            }

            _pressedFish = null;
            view.EndPress();
            if (_visualLock || _session == null || _session.State != GameState.PlayerInput)
            {
                return;
            }

            HandleFishViewSelected(view);
        }

        private void CancelPressedFish(FishView view)
        {
            if (_pressedFish != view)
            {
                return;
            }

            _pressedFish = null;
            if (view != null)
            {
                view.EndPress();
            }
        }

        private void ReleaseCapturedPress()
        {
            var pressed = _pressedFish;
            _pressedFish = null;
            if (pressed != null)
            {
                pressed.EndPress();
            }
        }

        private void EnsureTouchFeedback()
        {
            if (_touch == null)
            {
                _touch = TouchFeedbackController.Create(TouchParent(), _art, _tuning);
                _touch.Rippled += () =>
                {
                    Sfx(SfxId.TouchRipple, 1f, 0.6f);
                    Haptic(HapticKind.Touch);
                };
            }
            else
            {
                _touch.Configure(_art, _tuning);
                _touch.Resume();
            }

            if (_suppressTouchVfx)
            {
                _touch.Suppress();
            }
        }

        private bool Sfx(SfxId id)
        {
            return Sfx(id, 1f, 1f);
        }

        private bool Sfx(SfxId id, float pitch, float volume)
        {
            if (_audio == null)
            {
                return false;
            }

            try
            {
                return _audio.Play(id, pitch, volume);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void Haptic(HapticKind kind)
        {
            if (_haptics == null)
            {
                return;
            }

            try
            {
                _haptics.Play(kind);
            }
            catch (Exception)
            {
            }
        }

        private void PlayOutcomeSound(SfxId id)
        {
            if (_outcomeSoundPlayed || _suppressOutcome)
            {
                return;
            }

            _outcomeSoundPlayed = true;
            Sfx(id);
        }

        private void BindButtonTaps(Component root)
        {
            if (root == null)
            {
                return;
            }

            var buttons = root.GetComponentsInChildren<UnityEngine.UI.Button>(true);
            for (var i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] != null)
                {
                    buttons[i].onClick.AddListener(() => Sfx(SfxId.ButtonTap));
                }
            }
        }

        private float PresentationNow()
        {
            if (_presentationClock != null)
            {
                try
                {
                    return _presentationClock();
                }
                catch (Exception)
                {
                    _presentationClock = null;
                }
            }

            return Time.unscaledTime;
        }

        private void ResetCombo()
        {
            _combo.Reset();
            _lastComboTier = ComboTier.None;
            _comboResetCount++;
            if (_comboView != null)
            {
                _comboView.Clear();
            }
        }

        /// <summary>
        /// Presentation-only streak step for one completed tank group. Never touches score, gold or targets.
        /// A failure to show text or play sound is swallowed so it cannot stall the completion coroutine.
        /// </summary>
        private void PresentComboStep()
        {
            var tier = _combo.RegisterCompletion(PresentationNow());
            _lastComboTier = tier;
            Sfx(SfxId.Combo, ComboStreakTracker.Pitch(tier), Mathf.Lerp(0.8f, 1f, ((int)tier - 1) / 4f));
            if (_suppressJuiceVfx)
            {
                return;
            }

            try
            {
                EnsureComboView();
                if (_comboView != null)
                {
                    _comboView.Show(tier, ComboAnchor());
                }
            }
            catch (Exception)
            {
            }
        }

        private void EnsureComboView()
        {
            if (_comboView != null || _scene == null || _scene.GlobalProgressDisplay == null)
            {
                return;
            }

            var canvas = _scene.GlobalProgressDisplay.canvas;
            if (canvas == null)
            {
                return;
            }

            Sprite sparkle = null;
            if (_art != null)
            {
                sparkle = _art.CoinSparkle != null ? _art.CoinSparkle : _art.SuccessBurst;
            }

            _comboView = ComboFeedbackView.Create(
                canvas.transform,
                canvas.transform.Find("OverlayRoot"),
                _scene.GlobalProgressDisplay.font,
                sparkle);
        }

        /// <summary>
        /// Middle-upper gameplay area: between the bottom of the tank board and the top of the Waiting Tray,
        /// so the text never sits on tank targets, the tray or the bubble pile.
        /// </summary>
        private Vector3 ComboAnchor()
        {
            var corners = new Vector3[4];
            var tankBoard = _scene != null && _scene.TankBoard != null ? _scene.TankBoard.transform as RectTransform : null;
            var tray = _scene != null && _scene.WaitingTray != null ? _scene.WaitingTray.transform as RectTransform : null;
            if (tankBoard != null && tray != null)
            {
                tankBoard.GetWorldCorners(corners);
                var boardBottom = corners[0].y;
                var centerX = (corners[0].x + corners[2].x) * 0.5f;
                tray.GetWorldCorners(corners);
                var trayTop = corners[1].y;
                return new Vector3(centerX, (boardBottom + trayTop) * 0.5f, corners[1].z);
            }

            var canvas = _scene != null && _scene.GlobalProgressDisplay != null ? _scene.GlobalProgressDisplay.canvas : null;
            var root = canvas != null ? canvas.transform as RectTransform : null;
            if (root == null)
            {
                return Vector3.zero;
            }

            root.GetWorldCorners(corners);
            return Vector3.Lerp(corners[0], corners[2], 0.5f) + new Vector3(0f, (corners[2].y - corners[0].y) * 0.2f, 0f);
        }

        private void EnsureTrail()
        {
            var sprite = _art != null ? _art.SmallBubbleParticle : null;
            if (sprite == null)
            {
                sprite = ProceduralVfxSprite.Dot;
            }

            if (_trail != null)
            {
                _trail.SetSprite(sprite);
                var existingParent = TrailParent();
                if (existingParent != null && _trail.transform.parent != existingParent)
                {
                    _trail.transform.SetParent(existingParent, false);
                }

                OrderTrailUnderFish();
                return;
            }

            var parent = TrailParent();
            if (parent == null)
            {
                return;
            }

            _trail = FishTrailEmitter.Create(parent, sprite);
            OrderTrailUnderFish();
        }

        private RectTransform TrailParent()
        {
            if (_flightLayer != null)
            {
                return _flightLayer.parent as RectTransform;
            }

            if (_scene == null)
            {
                return null;
            }

            var canvas = _scene.GlobalProgressDisplay != null ? _scene.GlobalProgressDisplay.canvas : null;
            if (canvas != null)
            {
                return canvas.transform as RectTransform;
            }

            return _scene.BubblePile != null ? _scene.BubblePile.transform.parent as RectTransform : null;
        }

        /// <summary>Trail FX sits on the same canvas as the flight layer, one sibling beneath the flying fish.</summary>
        private void OrderTrailUnderFish()
        {
            if (_trail != null)
            {
                _trail.transform.SetAsLastSibling();
            }

            if (_flightLayer != null)
            {
                _flightLayer.SetAsLastSibling();
            }
        }

        private float TrailInterval(bool toTray, float routeProgress)
        {
            return _trail != null
                ? _trail.NextInterval(toTray, routeProgress)
                : FishTrailEmitter.MaxInterval * (toTray ? FishTrailEmitter.TrayIntervalFactor : 1f);
        }

        /// <summary>
        /// Advances the trail clock and emits every bubble that came due this frame, spread along the segment
        /// the fish covered since <paramref name="from"/>, so slow frames keep the density without bunching.
        /// Tank routes add a light glint now and then near the destination.
        /// </summary>
        private void TickTrail(RectTransform fish, bool toTray, float step, float routeProgress, ref float clock, ref float interval, ref Vector3 from)
        {
            if (fish == null)
            {
                return;
            }

            var to = fish.position;
            clock += step;
            var emitted = 0;
            while (clock >= interval && emitted < MaxTrailEmitsPerFrame)
            {
                clock -= interval;
                var lag = step > 0f ? Mathf.Clamp01(clock / step) : 0f;
                var position = Vector3.LerpUnclamped(to, from, lag);
                EmitTrailAt(position);
                if (!toTray && routeProgress >= TrailSparkleFromProgress && (++_trailSparkleTick % TrailSparkleEvery) == 0)
                {
                    EmitTrailSparkle(position);
                }

                interval = TrailInterval(toTray, routeProgress);
                emitted++;
            }

            if (clock > interval)
            {
                clock = 0f;
            }

            from = to;
        }

        private void MarkTrailSamples()
        {
            _trailSampleMark = _trail != null ? _trail.SpawnCount : 0;
        }

        /// <summary>Development proof that this flight left bubbles at several points along the route.</summary>
        private void AssertTrailFollowedRoute(bool fullTrail)
        {
            if (!fullTrail || _suppressJuiceVfx || _trail == null)
            {
                return;
            }

            if (_trail.SamplesSinceFollowRoute(_trailSampleMark))
            {
                return;
            }

            GameLog.Error(
                nameof(GameFlowController),
                "Fish trail did not sample distinct positions along the route. Spawns " + _trail.SpawnCount + ".");
        }

        private void EmitTrail(RectTransform fish)
        {
            if (_suppressJuiceVfx || _trail == null || fish == null)
            {
                return;
            }

            try
            {
                _trail.EmitCurrent(fish);
            }
            catch (Exception)
            {
            }
        }

        private void EmitTrailAt(Vector3 worldPosition)
        {
            if (_suppressJuiceVfx || _trail == null)
            {
                return;
            }

            try
            {
                _trail.Emit(worldPosition);
            }
            catch (Exception)
            {
            }
        }

        private void EmitTrailSparkle(Vector3 worldPosition)
        {
            if (_suppressJuiceVfx || _trail == null)
            {
                return;
            }

            try
            {
                _trail.EmitSparkle(worldPosition);
            }
            catch (Exception)
            {
            }
        }

        private void EmitLandingBubbles(Vector3 worldPosition)
        {
            if (_suppressJuiceVfx || _trail == null)
            {
                return;
            }

            try
            {
                _trail.Burst(worldPosition, LandingBubbleCount, LandingSparkleCount);
            }
            catch (Exception)
            {
            }
        }

        private Transform TouchParent()
        {
            if (_scene != null && _scene.Background != null && _scene.Background.canvas != null)
            {
                return _scene.Background.canvas.transform;
            }

            if (_scene != null && _scene.GlobalProgressDisplay != null && _scene.GlobalProgressDisplay.canvas != null)
            {
                return _scene.GlobalProgressDisplay.canvas.transform;
            }

            return transform;
        }

        /// <summary>
        /// Correct tank landing: plop sound, splash + droplets + ripple and a short bubble burst around the fish, then the landing haptic.
        /// The small tank bounce follows in <see cref="BounceFish"/>. Every landing runs this, including the third fish.
        /// </summary>
        private void PlayTankSplash(RectTransform anchor)
        {
            if (anchor == null)
            {
                return;
            }

            _landingFxCount++;
            Sfx(SfxId.TankLand);
            if (!_suppressLandingSplash)
            {
                PlayLandingVisuals(anchor);
            }

            Haptic(HapticKind.TankLanding);
        }

        private void PlayLandingVisuals(RectTransform anchor)
        {
            EmitLandingBubbles(anchor.TransformPoint(anchor.rect.center));

            EnsureFlightLayer();
            if (_flightLayer == null)
            {
                return;
            }

            Sprite splash = null;
            Sprite droplet = null;
            Sprite ring = null;
            if (_art != null)
            {
                splash = _art.TankSplash;
                ring = _art.TouchRipple;
            }

            droplet = ProceduralVfxSprite.Droplet;
            var droplets = _tuning != null ? _tuning.TankSplashDropletCount : 6;
            var splashCap = _tuning != null ? _tuning.TankSplashPoolCap : TankSplashView.DefaultPoolCap;
            TankSplashView splashView = null;
            try
            {
                splashView = TankSplashView.Play(
                    _flightLayer,
                    anchor.position,
                    splash,
                    droplet,
                    ring,
                    true,
                    droplets,
                    splashCap);
            }
            catch (Exception)
            {
                splashView = null;
            }
            if (splashView != null)
            {
                _tankSplashCount++;
            }
        }

        private Transform TankTransform(int slotIndex)
        {
            if (_scene == null || _scene.TankBoard == null)
            {
                return null;
            }

            var slots = _scene.TankBoard.Slots;
            if (slotIndex < 0 || slotIndex >= slots.Count || slots[slotIndex] == null)
            {
                return null;
            }

            return slots[slotIndex].transform;
        }

        private void HandleFishViewSelected(FishView view)
        {
            if (_visualLock || view == null || _session == null)
            {
                return;
            }

            FinishLevelIntro();
            _visualLock = true;
            _presentationFinished = false;
            _presentationElapsed = 0f;
            FishSelectionResult result;
            try
            {
                result = _session.TrySelectFish(view.FishId, committed => DetachForFlight(view, committed));
            }
            catch
            {
                _visualLock = false;
                _presentationFinished = true;
                throw;
            }

            if (result == null || !result.Accepted)
            {
                _visualLock = false;
                _presentationFinished = true;
                return;
            }

            _acceptedRouteCount++;
            Sfx(SfxId.FishLaunch);
            var turn = _session.LastTurn ?? TurnResolution.Empty;
            PresentIceUpdates(turn);
            if (turn.PoppedBubble)
            {
                DisableBubbleInput(turn.PoppedBubbleId);
            }

            _session.HoldForPresentation(GameState.RoutingFish);
            StartCoroutine(PlayTurn(view, result));
        }

        private IEnumerator PlayTurn(FishView view, FishSelectionResult result)
        {
            Coroutine settle = null;
            try
            {
                RefreshProgress();
                StageBadges(result, _session.LastTurn);
                var turn = _session.LastTurn ?? TurnResolution.Empty;
                if (_session.State != GameState.Lose && turn.PoppedBubble)
                {
                    settle = StartCoroutine(PopAndSettle(turn));
                }

                yield return RouteSelectedFish(view, result);
                if (result.Outcome == FishSelectionOutcome.RoutedToTank && result.TankCompleted)
                {
                    var completedType = view != null ? view.DisplayedType : result.NextTarget;
                    yield return PresentCompletion(
                        result.TankSlotIndex,
                        completedType,
                        result.HasNextTarget,
                        result.NextTarget,
                        result.ConsumedFishIds,
                        false);
                }
                else if (result.Outcome == FishSelectionOutcome.RoutedToTank)
                {
                    RefreshTankBadge(result.TankSlotIndex);
                    Sfx(SfxId.TankFill);
                }

                if (_session.State != GameState.Lose)
                {
                    yield return PresentPromotions(turn);
                }

                if (settle != null)
                {
                    yield return settle;
                }
            }
            finally
            {
                FinishPresentation();
            }
        }

        private IEnumerator PopAndSettle(TurnResolution turn)
        {
            if (turn == null || _session == null)
            {
                yield break;
            }

            _session.HoldForPresentation(GameState.PoppingBubble);
            yield return PopBubble(turn.PoppedBubbleId);
            if (_session.State == GameState.Lose)
            {
                yield break;
            }

            yield return PlayPile(turn);
        }

        private IEnumerator PlayExternal()
        {
            try
            {
                RefreshProgress();
                StageBadges(null, _session.LastTurn);
                yield return PresentPromotions(_session.LastTurn ?? TurnResolution.Empty);
            }
            finally
            {
                FinishPresentation();
            }
        }

        private IEnumerator RouteSelectedFish(FishView view, FishSelectionResult result)
        {
            CaptureReflow(result.SourceBubbleId);
            var anchor = result.Outcome == FishSelectionOutcome.RoutedToTank
                ? TankAnchor(result.TankSlotIndex, result.LandedOrdinal)
                : TrayAnchor(result.TraySlotIndex);
            var toTank = result.Outcome == FishSelectionOutcome.RoutedToTank;
            var padding = toTank ? 0f : 8f;
            var flightSeconds = toTank ? _tuning.FishRouteDuration : _tuning.TrayRouteDuration;
            StartCoroutine(Squash(view != null ? view.transform : null, _tuning.FishTapSquashDuration));
            yield return FlyAndReflow(view, anchor, flightSeconds, toTank);
            if (view != null && anchor != null)
            {
                Place(view, anchor, padding);
            }

            ApplyReflow(1f);
            if (toTank)
            {
                PlayTankSplash(anchor);
            }
            else
            {
                Sfx(SfxId.TrayLand);
            }

            var tank = toTank ? TankTransform(result.TankSlotIndex) : null;
            yield return BounceFish(view != null ? view.transform : null, tank, _tuning.FishLandingBounceDuration);
        }

        private IEnumerator PresentPromotions(TurnResolution turn)
        {
            var promotions = turn != null ? turn.Promotions : null;
            if (promotions == null || promotions.Count == 0)
            {
                yield break;
            }

            _session.HoldForPresentation(GameState.AutoPromotingTray);
            var slots = new FishView[_session.Tray.SlotCount];
            CollectTraySlots(slots);
            for (var i = 0; i < promotions.Count; i++)
            {
                var promotion = promotions[i];
                if (promotion == null)
                {
                    continue;
                }

                if (i > 0)
                {
                    yield return Wait(_tuning.TrayAutoMoveStagger);
                }

                _viewsByFishId.TryGetValue(promotion.FishId, out var view);
                var anchor = TankAnchor(promotion.TankSlotIndex, promotion.LandedOrdinal);
                yield return FlyToAnchor(view, anchor, 0f, _tuning.FishRouteDuration, TankTransform(promotion.TankSlotIndex));
                var sourceIndex = promotion.SourceTrayIndex;
                if (!SlotMatches(slots, sourceIndex, promotion.FishId))
                {
                    sourceIndex = IndexOfFish(slots, promotion.FishId);
                }

                if (sourceIndex >= 0)
                {
                    yield return CompactTray(slots, sourceIndex);
                }

                if (promotion.CompletedTank)
                {
                    var completedType = view != null ? view.DisplayedType : promotion.NextTarget;
                    yield return PresentCompletion(
                        promotion.TankSlotIndex,
                        completedType,
                        promotion.HasNextTarget,
                        promotion.NextTarget,
                        promotion.ConsumedFishIds,
                        true);
                }
                else
                {
                    RefreshTankBadge(promotion.TankSlotIndex);
                    Sfx(SfxId.TankFill);
                }
            }
        }

        private IEnumerator PresentCompletion(int slotIndex, FishType completedType, bool hasNext, FishType nextTarget, int[] consumed, bool fromPromotion)
        {
            _session.HoldForPresentation(GameState.ResolvingTank);
            var capacity = CapacityOf(slotIndex);
            var badge = BadgeFor(slotIndex);
            if (badge != null)
            {
                badge.ShowTarget(completedType, capacity, capacity, _fishCatalog);
            }

            if (fromPromotion)
            {
                _promotionCompletionCount++;
            }

            Sfx(SfxId.TankComplete);
            Haptic(HapticKind.TankComplete);
            PresentComboStep();

            yield return null;
            yield return Wait(_tuning.TankResolveDuration);
            yield return BounceCompletion(slotIndex, consumed);
            ClearConsumed(consumed);
            _session.HoldForPresentation(GameState.AssigningTarget);
            if (badge != null)
            {
                if (hasNext)
                {
                    badge.ShowTarget(nextTarget, 0, capacity, _fishCatalog);
                }
                else
                {
                    badge.ShowInactive();
                }
            }

            yield return Wait(_tuning.TankTargetSwapDuration);
        }

        /// <summary>
        /// Settles the pile as one staggered wave: lower bubbles start first, each following bubble one beat later,
        /// and top spawns continue the same beat. A bubble with several hops travels them without stopping.
        /// </summary>
        private IEnumerator PlayPile(TurnResolution turn)
        {
            var running = new List<Coroutine>();
            var stagger = _tuning.BubbleSettleStagger;
            if (turn.PileMoves.Count > 0)
            {
                _session.HoldForPresentation(GameState.SettlingBubblePile);
                var paths = AdoptPileMoves(turn.PileMoves);
                for (var i = 0; i < paths.Count; i++)
                {
                    if (i > 0)
                    {
                        yield return Wait(stagger);
                    }

                    running.Add(StartCoroutine(MoveBubbleAlong(paths[i].Rect, paths[i].Origin, paths[i].Slots)));
                }
            }

            if (turn.Spawns.Count > 0)
            {
                if (running.Count > 0)
                {
                    yield return Wait(stagger);
                }

                _session.HoldForPresentation(GameState.SpawningTopBubble);
                for (var i = 0; i < turn.Spawns.Count; i++)
                {
                    if (i > 0)
                    {
                        yield return Wait(stagger);
                    }

                    running.Add(StartCoroutine(SpawnBubble(turn.Spawns[i].BubbleId, turn.Spawns[i].SlotId)));
                }
            }

            for (var i = 0; i < running.Count; i++)
            {
                yield return running[i];
            }
        }

        private sealed class PilePath
        {
            public RectTransform Rect;
            public Vector2 Origin;
            public readonly List<int> Slots = new List<int>();
        }

        /// <summary>
        /// Applies every slot change in resolver order before any motion starts, so parallel tweens never
        /// fight over slot ownership. Returns one path per bubble in first-move order (lower rows first).
        /// </summary>
        private List<PilePath> AdoptPileMoves(IReadOnlyList<BubblePileMove> moves)
        {
            var paths = new List<PilePath>();
            var byId = new Dictionary<string, PilePath>();
            var pile = _scene != null ? _scene.BubblePile : null;
            if (pile == null)
            {
                return paths;
            }

            for (var i = 0; i < moves.Count; i++)
            {
                var move = moves[i];
                if (!pile.TryGetByBubbleId(move.BubbleId, out var view) || view == null)
                {
                    continue;
                }

                if (!byId.TryGetValue(move.BubbleId, out var path))
                {
                    var rect = view.transform as RectTransform;
                    path = new PilePath { Rect = rect, Origin = rect != null ? rect.anchoredPosition : Vector2.zero };
                    byId.Add(move.BubbleId, path);
                    paths.Add(path);
                }

                pile.AdoptSlot(view, move.ToSlotId);
                path.Slots.Add(move.ToSlotId);
            }

            return paths;
        }

        private IEnumerator MoveBubbleAlong(RectTransform rect, Vector2 origin, List<int> slots)
        {
            var pile = _scene != null ? _scene.BubblePile : null;
            if (rect == null || pile == null || slots == null)
            {
                yield break;
            }

            var from = origin;
            var moved = false;
            for (var hop = 0; hop < slots.Count; hop++)
            {
                if (!pile.TryGetSlotPosition(slots[hop], out var destination))
                {
                    continue;
                }

                var duration = Mathf.Abs(destination.x - from.x) > Mathf.Abs(destination.y - from.y)
                    ? _tuning.BubbleSlideDuration
                    : _tuning.BubbleFallDuration;
                var elapsed = 0f;
                while (elapsed < duration && rect != null)
                {
                    elapsed += Step();
                    var sample = PresentationMotion.Sample(rect != null, elapsed, duration);
                    rect.anchoredPosition = PresentationMotion.FallSlide(from, destination, sample.T, _tuning.BubblePathArc);
                    if (sample.Completed)
                    {
                        break;
                    }

                    yield return null;
                }

                if (rect == null)
                {
                    yield break;
                }

                rect.anchoredPosition = destination;
                from = destination;
                moved = true;
            }

            if (moved && rect != null)
            {
                Sfx(SfxId.BubbleSettle);
                yield return BounceRect(rect, _tuning.BubbleLandingBounceDuration);
            }
        }

        private void StartLevelIntro()
        {
            FinishLevelIntro();
            var pile = _scene != null ? _scene.BubblePile : null;
            var duration = _tuning != null ? _tuning.LevelIntroDuration : 0f;
            if (pile == null || duration <= 0f || !Application.isPlaying || !isActiveAndEnabled)
            {
                return;
            }

            _introBubbles.Clear();
            var visible = pile.VisibleBubbles;
            for (var i = 0; i < visible.Count; i++)
            {
                if (visible[i] != null && visible[i].transform is RectTransform)
                {
                    _introBubbles.Add(visible[i]);
                }
            }

            if (_introBubbles.Count == 0)
            {
                return;
            }

            var drop = IntroDropDistance(pile);
            PlaceIntroBubbles(pile, drop);
            _introRoutine = StartCoroutine(PlayLevelIntro(pile, drop, duration));
        }

        /// <summary>One drop distance for the whole pile, large enough that the lowest bubble starts above the screen.</summary>
        private float IntroDropDistance(BubblePileView pile)
        {
            var drop = _tuning.LevelIntroMinDrop;
            var first = _introBubbles[0].transform as RectTransform;
            var field = first != null ? first.parent as RectTransform : null;
            var canvas = field != null ? field.GetComponentInParent<Canvas>() : null;
            var root = canvas != null ? canvas.rootCanvas.transform as RectTransform : null;
            if (field == null || root == null)
            {
                return drop;
            }

            var corners = new Vector3[4];
            root.GetWorldCorners(corners);
            var screenTop = field.InverseTransformPoint(corners[1]).y;
            var lowest = float.MaxValue;
            var tallest = 0f;
            for (var i = 0; i < _introBubbles.Count; i++)
            {
                var rect = _introBubbles[i].transform as RectTransform;
                if (rect == null || !pile.TryGetSlotPosition(_introBubbles[i].SlotId, out var slot))
                {
                    continue;
                }

                lowest = Mathf.Min(lowest, slot.y);
                tallest = Mathf.Max(tallest, rect.rect.height);
            }

            if (lowest == float.MaxValue)
            {
                return drop;
            }

            return Mathf.Max(drop, screenTop - lowest + tallest);
        }

        private void PlaceIntroBubbles(BubblePileView pile, float drop)
        {
            for (var i = 0; i < _introBubbles.Count; i++)
            {
                var view = _introBubbles[i];
                if (view == null || !(view.transform is RectTransform rect) || !pile.TryGetSlotPosition(view.SlotId, out var slot))
                {
                    continue;
                }

                rect.anchoredPosition = slot + new Vector2(0f, drop);
            }
        }

        /// <summary>The whole pile drops in together, lands on the same frame and bounces once.</summary>
        private IEnumerator PlayLevelIntro(BubblePileView pile, float drop, float duration)
        {
            var delay = _tuning.LevelIntroDelay;
            var elapsed = 0f;
            while (elapsed < delay)
            {
                elapsed += Step();
                PlaceIntroBubbles(pile, drop);
                yield return null;
            }

            Sfx(SfxId.TopSpawn);
            elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Step();
                var sample = PresentationMotion.Sample(pile != null, elapsed, duration);
                PlaceIntroBubbles(pile, drop * (1f - PresentationMotion.EaseOutCubic(sample.T)));
                if (sample.Completed)
                {
                    break;
                }

                yield return null;
            }

            PlaceIntroBubbles(pile, 0f);
            Sfx(SfxId.BubbleSettle);
            var bounce = _tuning.BubbleLandingBounceDuration;
            elapsed = 0f;
            while (elapsed < bounce)
            {
                elapsed += Step();
                var sample = PresentationMotion.Sample(true, elapsed, bounce);
                SetIntroScale(PresentationMotion.BounceScale(sample.T, _tuning.LandingOvershoot));
                if (sample.Completed)
                {
                    break;
                }

                yield return null;
            }

            SetIntroScale(Vector3.one);
            _introBubbles.Clear();
            _introRoutine = null;
        }

        private void SetIntroScale(Vector3 scale)
        {
            for (var i = 0; i < _introBubbles.Count; i++)
            {
                if (_introBubbles[i] != null)
                {
                    _introBubbles[i].transform.localScale = scale;
                }
            }
        }

        /// <summary>Stops the opening drop and puts every bubble on its slot at rest.</summary>
        private void FinishLevelIntro()
        {
            if (_introRoutine == null)
            {
                return;
            }

            StopCoroutine(_introRoutine);
            _introRoutine = null;
            var pile = _scene != null ? _scene.BubblePile : null;
            if (pile != null)
            {
                PlaceIntroBubbles(pile, 0f);
            }

            SetIntroScale(Vector3.one);
            _introBubbles.Clear();
        }

        private IEnumerator PopBubble(string bubbleId)
        {
            _lastBubblePopSeconds = 0f;
            var pile = _scene != null ? _scene.BubblePile : null;
            if (pile == null || !pile.TryGetByBubbleId(bubbleId, out var view) || view == null)
            {
                yield break;
            }

            view.DisableInput();
            if (!pile.TryDetach(bubbleId, out view) || view == null)
            {
                yield break;
            }

            _poppingBubble = view;
            var rect = view.transform as RectTransform;
            var parent = rect != null ? rect.parent as RectTransform : null;
            var origin = rect != null ? rect.anchoredPosition : Vector2.zero;
            EmitBubbleBurst(parent, origin, view.VisibleRadius);
            Sfx(SfxId.BubblePop);
            Haptic(HapticKind.BubblePop);

            var duration = _tuning != null ? _tuning.BubblePopDuration : 0.07f;
            _lastBubblePopSeconds = duration;
            var elapsed = 0f;
            while (elapsed < duration && rect != null)
            {
                elapsed += Step();
                var sample = PresentationMotion.Sample(rect != null, elapsed, duration);
                rect.localScale = Vector3.one * PresentationMotion.PopScale(sample.T);
                if (sample.Completed)
                {
                    break;
                }

                yield return null;
            }

            if (rect != null)
            {
                rect.localScale = Vector3.zero;
            }

            DestroyPoppingBubble();
        }

        private void EmitBubbleBurst(RectTransform parent, Vector2 origin, float bubbleRadius = 0f)
        {
            if (_suppressPopVfx || parent == null)
            {
                return;
            }

            var popSprite = _art != null ? _art.BubblePopParticle : null;
            var smallSprite = _art != null ? _art.SmallBubbleParticle : null;
            if (!BubblePopVfx.CanPlay(popSprite, smallSprite))
            {
                return;
            }

            try
            {
                EnsureBurstPool(parent);
                if (_bursts == null)
                {
                    return;
                }

                var lifetime = _tuning != null ? _tuning.BubbleBurstLifetime : 1.0f;
                var count = _tuning != null ? _tuning.BubbleBurstCount : 32;
                var emitted = _bursts.Emit(origin, smallSprite, popSprite, lifetime, count, bubbleRadius);
                if (emitted > 0)
                {
                    _bubbleBurstEmissions += emitted;
                }
            }
            catch (Exception)
            {
            }
        }

        private void EnsureBurstPool(RectTransform parent)
        {
            if (parent == null)
            {
                return;
            }

            var cap = _tuning != null ? _tuning.BubbleBurstPoolCap : BubbleBurstPool.DefaultPoolCap;
            if (_bursts == null)
            {
                _bursts = BubbleBurstPool.Create(parent, cap);
                return;
            }

            if (_bursts.transform.parent != parent)
            {
                _bursts.transform.SetParent(parent, false);
            }
        }

        private IEnumerator SpawnBubble(string bubbleId, int slotId)
        {
            var pile = _scene.BubblePile;
            if (pile == null || !pile.TryGetSlotPosition(slotId, out var destination))
            {
                yield break;
            }

            var start = destination + new Vector2(0f, _tuning.TopSpawnOffset);
            var view = pile.CreateIncoming(bubbleId, slotId, start);
            if (view == null)
            {
                yield break;
            }

            BindBubbleFish(view, bubbleId, false);
            SyncIceView(view);
            yield return null;
            Sfx(SfxId.TopSpawn);
            var rect = view.transform as RectTransform;
            var duration = _tuning.BubbleTopSpawnDuration;
            var elapsed = 0f;
            while (elapsed < duration && rect != null)
            {
                elapsed += Step();
                var sample = PresentationMotion.Sample(rect != null, elapsed, duration);
                var eased = PresentationMotion.Hop(sample.T);
                rect.anchoredPosition = Vector2.Lerp(start, destination, eased);
                if (sample.Completed)
                {
                    break;
                }

                yield return null;
            }

            if (rect != null)
            {
                rect.anchoredPosition = destination;
                yield return BounceRect(rect, _tuning.BubbleLandingBounceDuration);
            }
        }

        private IEnumerator FlyAndReflow(FishView view, RectTransform anchor, float seconds, bool hop)
        {
            var rect = view != null ? view.transform as RectTransform : null;
            EnsureFlightLayer();
            if (rect != null && _flightLayer != null && rect.parent != _flightLayer)
            {
                rect.SetParent(_flightLayer, true);
            }

            var start = rect != null ? rect.position : Vector3.zero;
            var end = anchor != null ? anchor.TransformPoint(anchor.rect.center) : start;
            var control = CurveControl(start, end, hop);
            var reflowSeconds = _tuning.BubbleFishReflowDuration;
            var duration = Mathf.Max(seconds, reflowSeconds);
            if (duration <= 0f)
            {
                if (rect != null)
                {
                    rect.position = end;
                }

                ApplyReflow(1f);
                yield break;
            }

            var startScale = rect != null ? rect.localScale.x : 1f;
            var squash = Mathf.Min(_tuning.FishTapSquashDuration, seconds * 0.5f);
            var elapsed = 0f;
            var toTray = !hop;
            var trailClock = 0f;
            var trailInterval = TrailInterval(toTray, 0f);
            var trailFrom = start;
            MarkTrailSamples();
            EmitTrail(rect);
            while (elapsed < duration)
            {
                var step = Step();
                elapsed += step;
                if (rect != null)
                {
                    var sample = PresentationMotion.Sample(true, elapsed, seconds);
                    var along = hop ? PresentationMotion.RoutePace(sample.T) : PresentationMotion.EaseOutQuad(sample.T);
                    rect.position = PresentationMotion.QuadraticBezier(start, control, end, along);
                    if (!Mathf.Approximately(startScale, 1f) && elapsed >= squash)
                    {
                        var shrink = seconds - squash > 0f ? Mathf.Clamp01((elapsed - squash) / (seconds - squash)) : 1f;
                        rect.localScale = Vector3.one * Mathf.Lerp(startScale, 1f, PresentationMotion.EaseOutQuad(shrink));
                    }

                    if (!sample.Completed)
                    {
                        TickTrail(rect, toTray, step, sample.T, ref trailClock, ref trailInterval, ref trailFrom);
                    }
                }

                ApplyReflow(reflowSeconds <= 0f ? 1f : Mathf.Clamp01(elapsed / reflowSeconds));
                if (elapsed >= duration)
                {
                    break;
                }

                yield return null;
            }

            if (rect != null)
            {
                rect.position = end;
            }

            AssertTrailFollowedRoute(hop);
            ApplyReflow(1f);
        }

        private IEnumerator FlyToAnchor(FishView view, RectTransform anchor, float padding, float seconds, Transform tank)
        {
            if (view == null || anchor == null)
            {
                yield break;
            }

            var rect = view.transform as RectTransform;
            EnsureFlightLayer();
            if (rect == null || _flightLayer == null)
            {
                Place(view, anchor, padding);
                PlayTankSplash(anchor);
                yield return BounceFish(view.transform, tank, _tuning.FishLandingBounceDuration);
                yield break;
            }

            if (rect.parent != _flightLayer)
            {
                rect.SetParent(_flightLayer, true);
            }

            var start = rect.position;
            var end = anchor.TransformPoint(anchor.rect.center);
            var control = CurveControl(start, end, true);
            var elapsed = 0f;
            var trailClock = 0f;
            var trailInterval = TrailInterval(false, 0f);
            var trailFrom = start;
            Sfx(SfxId.FishFly, 1f, 0.7f);
            MarkTrailSamples();
            EmitTrail(rect);
            while (elapsed < seconds && rect != null)
            {
                var step = Step();
                elapsed += step;
                var sample = PresentationMotion.Sample(rect != null, elapsed, seconds);
                rect.position = PresentationMotion.QuadraticBezier(start, control, end, PresentationMotion.RoutePace(sample.T));
                if (sample.Completed)
                {
                    break;
                }

                TickTrail(rect, false, step, sample.T, ref trailClock, ref trailInterval, ref trailFrom);
                yield return null;
            }

            AssertTrailFollowedRoute(true);
            if (view != null)
            {
                Place(view, anchor, padding);
                PlayTankSplash(anchor);
                yield return BounceFish(view.transform, tank, _tuning.FishLandingBounceDuration);
            }
        }

        private IEnumerator CompactTray(FishView[] slots, int removedIndex)
        {
            if (slots == null || removedIndex < 0 || removedIndex >= slots.Length)
            {
                yield break;
            }

            var moving = new List<FishView>();
            var targets = new List<int>();
            for (var i = removedIndex + 1; i < slots.Length; i++)
            {
                if (slots[i] == null)
                {
                    continue;
                }

                moving.Add(slots[i]);
                targets.Add(i - 1);
            }

            for (var i = removedIndex + 1; i < slots.Length; i++)
            {
                slots[i - 1] = slots[i];
            }

            slots[slots.Length - 1] = null;
            if (moving.Count == 0)
            {
                yield break;
            }

            var starts = new Vector3[moving.Count];
            var ends = new Vector3[moving.Count];
            for (var i = 0; i < moving.Count; i++)
            {
                var fishRect = moving[i] != null ? moving[i].transform as RectTransform : null;
                var destination = TrayAnchor(targets[i]);
                starts[i] = fishRect != null ? fishRect.position : Vector3.zero;
                ends[i] = destination != null ? destination.TransformPoint(destination.rect.center) : starts[i];
                if (fishRect != null && _flightLayer != null)
                {
                    fishRect.SetParent(_flightLayer, true);
                }
            }

            var seconds = _tuning.BubbleFishReflowDuration;
            var elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Step();
                var sample = PresentationMotion.Sample(true, elapsed, seconds);
                for (var i = 0; i < moving.Count; i++)
                {
                    if (moving[i] == null)
                    {
                        continue;
                    }

                    moving[i].transform.position = Vector3.Lerp(starts[i], ends[i], PresentationMotion.EaseOutQuad(sample.T));
                }

                if (sample.Completed)
                {
                    break;
                }

                yield return null;
            }

            for (var i = 0; i < moving.Count; i++)
            {
                var destination = TrayAnchor(targets[i]);
                if (moving[i] != null && destination != null)
                {
                    Place(moving[i], destination, 8f);
                }
            }
        }

        private IEnumerator Squash(Transform target, float seconds)
        {
            if (target == null || seconds <= 0f)
            {
                yield break;
            }

            var origin = target.localScale;
            var elapsed = 0f;
            while (elapsed < seconds && target != null)
            {
                elapsed += Step();
                var sample = PresentationMotion.Sample(target != null, elapsed, seconds);
                target.localScale = Vector3.Scale(origin, PresentationMotion.BounceScale(sample.T, _tuning.LandingOvershoot));
                if (sample.Completed)
                {
                    break;
                }

                yield return null;
            }

            if (target != null)
            {
                target.localScale = origin;
            }
        }

        private IEnumerator BounceFish(Transform fish, Transform tank, float seconds)
        {
            if ((fish == null && tank == null) || seconds <= 0f)
            {
                yield break;
            }

            var fishOrigin = fish != null ? fish.localScale : Vector3.one;
            var elapsed = 0f;
            while (elapsed < seconds && (fish != null || tank != null))
            {
                elapsed += Step();
                var sample = PresentationMotion.Sample(fish != null || tank != null, elapsed, seconds);
                if (fish != null)
                {
                    fish.localScale = Vector3.Scale(fishOrigin, PresentationMotion.BounceScale(sample.T, _tuning.LandingOvershoot));
                }

                if (tank != null)
                {
                    tank.localScale = PresentationMotion.BounceScale(sample.T, _tuning.LandingOvershoot * 0.32f);
                }

                if (sample.Completed)
                {
                    break;
                }

                yield return null;
            }

            if (fish != null)
            {
                fish.localScale = fishOrigin;
            }

            if (tank != null)
            {
                tank.localScale = Vector3.one;
            }
        }

        private IEnumerator BounceRect(Transform target, float seconds)
        {
            if (target == null)
            {
                yield break;
            }

            var origin = target.localScale;
            if (seconds <= 0f)
            {
                target.localScale = origin;
                yield break;
            }

            var elapsed = 0f;
            while (elapsed < seconds && target != null)
            {
                elapsed += Step();
                var sample = PresentationMotion.Sample(target != null, elapsed, seconds);
                target.localScale = Vector3.Scale(origin, PresentationMotion.BounceScale(sample.T, _tuning.LandingOvershoot));
                if (sample.Completed)
                {
                    break;
                }

                yield return null;
            }

            if (target != null)
            {
                target.localScale = origin;
            }
        }

        private IEnumerator BounceCompletion(int slotIndex, int[] consumed)
        {
            var slots = _scene.TankBoard.Slots;
            Transform tank = null;
            if (slotIndex >= 0 && slotIndex < slots.Count && slots[slotIndex] != null)
            {
                tank = slots[slotIndex].transform;
            }

            var fish = new List<Transform>();
            if (consumed != null)
            {
                for (var i = 0; i < consumed.Length; i++)
                {
                    if (_viewsByFishId.TryGetValue(consumed[i], out var view) && view != null)
                    {
                        fish.Add(view.transform);
                    }
                }
            }

            var seconds = _tuning.FishLandingBounceDuration;
            var elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Step();
                var sample = PresentationMotion.Sample(true, elapsed, seconds);
                var scale = PresentationMotion.BounceScale(sample.T, _tuning.LandingOvershoot);
                if (tank != null)
                {
                    tank.localScale = scale;
                }

                for (var i = 0; i < fish.Count; i++)
                {
                    if (fish[i] != null)
                    {
                        fish[i].localScale = scale;
                    }
                }

                if (sample.Completed)
                {
                    break;
                }

                yield return null;
            }

            if (tank != null)
            {
                tank.localScale = Vector3.one;
            }

            for (var i = 0; i < fish.Count; i++)
            {
                if (fish[i] != null)
                {
                    fish[i].localScale = Vector3.one;
                }
            }
        }

        private float Step()
        {
            var cap = _tuning != null ? _tuning.MaxFrameStep : 0.05f;
            return Mathf.Min(Time.unscaledDeltaTime, cap);
        }

        private IEnumerator Wait(float seconds)
        {
            if (seconds <= 0f)
            {
                yield break;
            }

            var elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Step();
                yield return null;
            }
        }

        private void FinishPresentation()
        {
            if (_presentationFinished)
            {
                return;
            }

            _presentationFinished = true;
            try
            {
                DestroyPoppingBubble();
                DestroyEphemeral();
                SnapPileToSession();
                SnapBubbleLayouts();
                ReconcileFishViews();
                SyncIceViews();
                ResetFeedbackScales();
                RefreshAllBadges();
                RefreshProgress();
            }
            finally
            {
                _visualLock = false;
                _presentationElapsed = 0f;
                if (_session != null)
                {
                    _session.ReleasePresentationHold();
                }

                EnableSettledBubbleInput();
                if (!_suppressOutcome && _session != null)
                {
                    if (_session.State == GameState.Win)
                    {
                        LockAllFishInput();
                        ShowWin();
                    }
                    else if (_session.State == GameState.Lose)
                    {
                        LockAllFishInput();
                        ShowLose();
                    }
                }
            }
        }

        private void DetachForFlight(FishView view, FishSelectionResult result)
        {
            if (view == null || result == null || !result.Accepted)
            {
                return;
            }

            var rect = view.transform as RectTransform;
            EnsureFlightLayer();
            if (rect != null && _flightLayer != null)
            {
                rect.SetParent(_flightLayer, true);
                rect.localRotation = Quaternion.identity;
                // Keep the in-bubble size (pile packing may enlarge fish); FlyAndReflow eases it back to 1.
                rect.localScale = Vector3.one * Mathf.Max(0.01f, rect.localScale.x);
            }

            view.ReleaseInteraction();
            ReleaseFromBubble(view, result.SourceBubbleId);
        }

        private void CaptureReflow(string bubbleId)
        {
            _reflowRects.Clear();
            _reflowOrigins.Clear();
            _reflowTargets.Clear();
            if (_scene == null || _scene.BubblePile == null || string.IsNullOrEmpty(bubbleId))
            {
                return;
            }

            if (!_scene.BubblePile.TryGetByBubbleId(bubbleId, out var bubble) || bubble == null)
            {
                return;
            }

            bubble.CollectFishRects(_reflowRects);
            for (var i = 0; i < _reflowRects.Count; i++)
            {
                var rect = _reflowRects[i];
                BubbleFishLayoutController.Prepare(rect);
                _reflowOrigins.Add(rect != null ? rect.anchoredPosition : Vector2.zero);
                if (!BubbleFishLayoutController.TryGetPosition(i, _reflowRects.Count, out var target))
                {
                    target = Vector2.zero;
                }

                _reflowTargets.Add(target);
            }
        }

        private void ApplyReflow(float t)
        {
            var eased = PresentationMotion.EaseOutQuad(t);
            for (var i = 0; i < _reflowRects.Count; i++)
            {
                var rect = _reflowRects[i];
                if (rect == null)
                {
                    continue;
                }

                rect.anchoredPosition = Vector2.Lerp(_reflowOrigins[i], _reflowTargets[i], eased);
            }
        }

        private Vector3 CurveControl(Vector3 start, Vector3 end, bool hop)
        {
            var scale = _flightLayer != null ? _flightLayer.lossyScale : Vector3.one;
            var control = (start + end) * 0.5f;
            var lift = _tuning.FishArcHeight * Mathf.Abs(scale.y);
            if (hop)
            {
                control.y = Mathf.Max(start.y, end.y) + lift;
            }
            else
            {
                control.y += lift;
            }

            var direction = end.x >= start.x ? -1f : 1f;
            control.x += _tuning.FishArcLateral * scale.x * direction;
            return control;
        }

        private void DisableBubbleInput(string bubbleId)
        {
            if (_scene == null || _scene.BubblePile == null || string.IsNullOrEmpty(bubbleId))
            {
                return;
            }

            if (_scene.BubblePile.TryGetByBubbleId(bubbleId, out var view) && view != null)
            {
                view.DisableInput();
            }
        }

        private void BindBubbleFish(BubbleView view, string bubbleId, bool enableInput)
        {
            BubbleRuntimeState runtime = null;
            var bubbles = _session.Bubbles;
            for (var i = 0; i < bubbles.Count; i++)
            {
                if (bubbles[i] != null && bubbles[i].BubbleId == bubbleId)
                {
                    runtime = bubbles[i];
                    break;
                }
            }

            if (runtime == null)
            {
                GameLog.Error(nameof(GameFlowController), "Spawned bubble " + bubbleId + " has no runtime state.");
                return;
            }

            _session.SetBubbleInPlay(bubbleId, enableInput);
            var views = view.FishViews;
            var fish = runtime.Fish;
            var count = views.Count < fish.Count ? views.Count : fish.Count;
            if (views.Count != fish.Count)
            {
                GameLog.Error(
                    nameof(GameFlowController),
                    "Bubble " + bubbleId + " has " + views.Count + " fish views and " + fish.Count + " runtime fish.");
            }

            for (var i = 0; i < count; i++)
            {
                var fishView = views[i];
                var runtimeFish = fish[i];
                if (fishView == null || runtimeFish == null || _viewsByFishId.ContainsKey(runtimeFish.Id))
                {
                    continue;
                }

                BindFishPointer(fishView, runtimeFish.Id, enableInput);

                _viewsByFishId.Add(runtimeFish.Id, fishView);
            }
        }

        private void SnapPileToSession()
        {
            var pile = _scene != null ? _scene.BubblePile : null;
            if (pile == null || _session == null)
            {
                return;
            }

            var visible = pile.VisibleBubbles;
            for (var i = visible.Count - 1; i >= 0; i--)
            {
                var view = visible[i];
                if (view == null)
                {
                    continue;
                }

                if (!_session.TryGetOccupiedSlot(view.BubbleId, out var slotId))
                {
                    pile.TryDetach(view.BubbleId, out _);
                    SceneObjectCleanup.DestroyObject(view.gameObject);
                    continue;
                }

                pile.AdoptSlot(view, slotId);
                if (pile.TryGetSlotPosition(slotId, out var position) && view.transform is RectTransform rect)
                {
                    rect.anchoredPosition = position;
                    rect.localScale = Vector3.one;
                }
            }

            var bubbles = _session.Bubbles;
            for (var i = 0; i < bubbles.Count; i++)
            {
                var bubble = bubbles[i];
                if (bubble == null || bubble.HasPopped || !_session.TryGetOccupiedSlot(bubble.BubbleId, out var slotId))
                {
                    continue;
                }

                if (pile.TryGetByBubbleId(bubble.BubbleId, out _))
                {
                    continue;
                }

                if (!pile.TryGetSlotPosition(slotId, out var position))
                {
                    continue;
                }

                var created = pile.CreateIncoming(bubble.BubbleId, slotId, position);
                if (created != null)
                {
                    BindBubbleFish(created, bubble.BubbleId, false);
                }
            }
        }

        private void SnapBubbleLayouts()
        {
            if (_scene == null || _scene.BubblePile == null)
            {
                return;
            }

            var visible = _scene.BubblePile.VisibleBubbles;
            for (var i = 0; i < visible.Count; i++)
            {
                if (visible[i] != null)
                {
                    visible[i].SnapFishLayout();
                }
            }
        }

        private void ReconcileFishViews()
        {
            var remove = new List<int>();
            foreach (var pair in _viewsByFishId)
            {
                var view = pair.Value;
                if (view == null)
                {
                    remove.Add(pair.Key);
                    continue;
                }

                if (TryFindTankOrdinal(pair.Key, out var slot, out var ordinal))
                {
                    PlaceInTank(view, slot, ordinal);
                    continue;
                }

                if (TryFindTraySlot(pair.Key, out var traySlot))
                {
                    PlaceInTray(view, traySlot);
                    continue;
                }

                if (IsDisplayedInsideBubble(view))
                {
                    continue;
                }

                remove.Add(pair.Key);
                view.ReleaseInteraction();
                SceneObjectCleanup.DestroyObject(view.gameObject);
            }

            for (var i = 0; i < remove.Count; i++)
            {
                _viewsByFishId.Remove(remove[i]);
            }
        }

        private void EnableSettledBubbleInput()
        {
            if (_session == null || _scene == null || _scene.BubblePile == null || _session.State != GameState.PlayerInput)
            {
                return;
            }

            var visible = _scene.BubblePile.VisibleBubbles;
            for (var i = 0; i < visible.Count; i++)
            {
                var bubble = visible[i];
                if (bubble != null && _session.TryGetOccupiedSlot(bubble.BubbleId, out _))
                {
                    _session.SetBubbleInPlay(bubble.BubbleId, true);
                }
            }

            foreach (var pair in _viewsByFishId)
            {
                if (pair.Value == null)
                {
                    continue;
                }

                if (IsDisplayedInsideBubble(pair.Value))
                {
                    BindFishPointer(pair.Value, pair.Key, true);
                }
                else
                {
                    pair.Value.ReleaseInteraction();
                }
            }
        }

        private void ResetFeedbackScales()
        {
            if (_scene == null || _scene.TankBoard == null)
            {
                return;
            }

            var slots = _scene.TankBoard.Slots;
            for (var i = 0; i < slots.Count; i++)
            {
                if (slots[i] == null)
                {
                    continue;
                }

                slots[i].transform.localScale = Vector3.one;
                if (slots[i].PreviewBadge != null)
                {
                    slots[i].PreviewBadge.transform.localScale = Vector3.one;
                }
            }
        }

        private void DestroyPoppingBubble()
        {
            if (_poppingBubble == null)
            {
                return;
            }

            var view = _poppingBubble;
            _poppingBubble = null;
            SceneObjectCleanup.DestroyObject(view.gameObject);
        }

        private void DestroyEphemeral()
        {
            for (var i = _ephemeral.Count - 1; i >= 0; i--)
            {
                if (_ephemeral[i] != null)
                {
                    SceneObjectCleanup.DestroyObject(_ephemeral[i]);
                }
            }

            _ephemeral.Clear();
        }

        private void CleanupEphemeral()
        {
            DestroyPoppingBubble();
            DestroyEphemeral();
        }

        private void StageBadges(FishSelectionResult selected, TurnResolution turn)
        {
            if (_session == null || _scene == null || _scene.TankBoard == null)
            {
                return;
            }

            var slots = _scene.TankBoard.Slots;
            var count = slots.Count < _session.Tanks.Count ? slots.Count : _session.Tanks.Count;
            for (var i = 0; i < count; i++)
            {
                var tank = _session.Tanks[i];
                if (tank == null || !tank.IsUnlocked || IsVisuallyCompleting(i, selected, turn))
                {
                    continue;
                }

                RefreshTankBadge(i);
            }
        }

        private static bool IsVisuallyCompleting(int slotIndex, FishSelectionResult selected, TurnResolution turn)
        {
            if (selected != null && selected.TankCompleted && selected.TankSlotIndex == slotIndex)
            {
                return true;
            }

            if (turn == null)
            {
                return false;
            }

            var promotions = turn.Promotions;
            for (var i = 0; i < promotions.Count; i++)
            {
                var promotion = promotions[i];
                if (promotion != null && promotion.CompletedTank && promotion.TankSlotIndex == slotIndex)
                {
                    return true;
                }
            }

            return false;
        }

        private void CollectTraySlots(FishView[] slots)
        {
            for (var i = 0; i < slots.Length; i++)
            {
                slots[i] = null;
                var anchor = TrayAnchor(i);
                if (anchor == null)
                {
                    continue;
                }

                slots[i] = anchor.GetComponentInChildren<FishView>(false);
            }
        }

        private static bool SlotMatches(FishView[] slots, int index, int fishId)
        {
            return slots != null && index >= 0 && index < slots.Length && slots[index] != null && slots[index].FishId == fishId;
        }

        private static int IndexOfFish(FishView[] slots, int fishId)
        {
            if (slots == null)
            {
                return -1;
            }

            for (var i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null && slots[i].FishId == fishId)
                {
                    return i;
                }
            }

            return -1;
        }

        private bool TryFindTankOrdinal(int fishId, out int slotIndex, out int ordinal)
        {
            slotIndex = -1;
            ordinal = -1;
            if (_session == null)
            {
                return false;
            }

            var tanks = _session.Tanks;
            for (var i = 0; i < tanks.Count; i++)
            {
                var tank = tanks[i];
                if (tank == null)
                {
                    continue;
                }

                var contained = tank.ContainedFish;
                for (var f = 0; f < contained.Count; f++)
                {
                    if (contained[f] != null && contained[f].Id == fishId)
                    {
                        slotIndex = tank.SlotIndex;
                        ordinal = f;
                        return true;
                    }
                }
            }

            return false;
        }

        private bool TryFindTraySlot(int fishId, out int slotIndex)
        {
            slotIndex = -1;
            if (_session == null)
            {
                return false;
            }

            for (var i = 0; i < _session.Tray.Count; i++)
            {
                var fish = _session.Tray.GetFishAt(i);
                if (fish != null && fish.Id == fishId)
                {
                    slotIndex = i;
                    return true;
                }
            }

            return false;
        }

        private bool IsDisplayedInsideBubble(FishView view)
        {
            if (view == null || _scene == null || _scene.BubblePile == null)
            {
                return false;
            }

            var visible = _scene.BubblePile.VisibleBubbles;
            for (var i = 0; i < visible.Count; i++)
            {
                var bubble = visible[i];
                if (bubble == null)
                {
                    continue;
                }

                var fish = bubble.FishViews;
                for (var f = 0; f < fish.Count; f++)
                {
                    if (fish[f] == view)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void EnsureFlightLayer()
        {
            if (_flightLayer != null)
            {
                return;
            }

            var canvas = _scene != null && _scene.GlobalProgressDisplay != null ? _scene.GlobalProgressDisplay.canvas : null;
            if (canvas == null)
            {
                return;
            }

            var layer = new GameObject("FishFlightLayer", typeof(RectTransform));
            _flightLayer = layer.GetComponent<RectTransform>();
            _flightLayer.SetParent(canvas.transform, false);
            _flightLayer.anchorMin = Vector2.zero;
            _flightLayer.anchorMax = Vector2.one;
            _flightLayer.offsetMin = Vector2.zero;
            _flightLayer.offsetMax = Vector2.zero;
            _flightLayer.pivot = new Vector2(0.5f, 0.5f);
            OrderTrailUnderFish();
        }

        private RectTransform TankAnchor(int slotIndex, int ordinal)
        {
            if (_scene == null || _scene.TankBoard == null)
            {
                return null;
            }

            var slots = _scene.TankBoard.Slots;
            if (slotIndex < 0 || slotIndex >= slots.Count || slots[slotIndex] == null)
            {
                return null;
            }

            var count = 1;
            if (_session != null && slotIndex < _session.Tanks.Count && _session.Tanks[slotIndex] != null)
            {
                count = _session.Tanks[slotIndex].FillCount;
            }

            slots[slotIndex].LayoutContainedFish(count);
            return slots[slotIndex].GetFishAnchor(ordinal);
        }

        private RectTransform TrayAnchor(int slotIndex)
        {
            if (_scene == null || _scene.WaitingTray == null)
            {
                return null;
            }

            return _scene.WaitingTray.GetSlotAnchor(slotIndex);
        }

        private TargetBadgeView BadgeFor(int slotIndex)
        {
            if (_scene == null || _scene.TankBoard == null)
            {
                return null;
            }

            var slots = _scene.TankBoard.Slots;
            if (slotIndex < 0 || slotIndex >= slots.Count || slots[slotIndex] == null)
            {
                return null;
            }

            return slots[slotIndex].PreviewBadge ?? _scene.TankBoard.EnsureBadge(slotIndex);
        }

        private int CapacityOf(int slotIndex)
        {
            if (_session == null || slotIndex < 0 || slotIndex >= _session.Tanks.Count || _session.Tanks[slotIndex] == null)
            {
                return 3;
            }

            return _session.Tanks[slotIndex].Capacity;
        }

        private void ReleaseFromBubble(FishView view, string bubbleId)
        {
            var bubbles = _scene.BubblePile.VisibleBubbles;
            for (var i = 0; i < bubbles.Count; i++)
            {
                var bubble = bubbles[i];
                if (bubble != null && bubble.BubbleId == bubbleId)
                {
                    bubble.ReleaseFish(view);
                    return;
                }
            }
        }

        private void PlaceInTank(FishView view, int slotIndex, int ordinal)
        {
            var anchor = TankAnchor(slotIndex, ordinal);
            if (anchor == null)
            {
                GameLog.Error(nameof(GameFlowController), "Tank slot " + slotIndex + " has no fish anchor " + ordinal + ".");
                view.ReleaseInteraction();
                return;
            }

            Place(view, anchor, 0f);
        }

        private void PlaceInTray(FishView view, int slotIndex)
        {
            var anchor = TrayAnchor(slotIndex);
            if (anchor == null)
            {
                GameLog.Error(nameof(GameFlowController), "Waiting tray slot " + slotIndex + " is missing.");
                view.ReleaseInteraction();
                return;
            }

            Place(view, anchor, 8f);
        }

        private static void Place(FishView view, RectTransform anchor, float padding)
        {
            var rect = view.transform as RectTransform;
            if (rect == null)
            {
                view.ReleaseInteraction();
                return;
            }

            rect.SetParent(anchor, false);
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(padding, padding);
            rect.offsetMax = new Vector2(-padding, -padding);
            view.ReleaseInteraction();
        }

        private void ClearConsumed(int[] fishIds)
        {
            if (fishIds == null)
            {
                return;
            }

            for (var i = 0; i < fishIds.Length; i++)
            {
                if (!_viewsByFishId.TryGetValue(fishIds[i], out var view) || view == null)
                {
                    continue;
                }

                _viewsByFishId.Remove(fishIds[i]);
                view.ReleaseInteraction();
                SceneObjectCleanup.DestroyObject(view.gameObject);
            }
        }

        private void OnDestroy()
        {
            ReleaseCapturedPress();
            if (_touch != null)
            {
                SceneObjectCleanup.DestroyObject(_touch.gameObject);
                _touch = null;
            }

            _suppressOutcome = true;
            _visualLock = false;
            if (_session != null)
            {
                _session.ReleasePresentationHold();
            }
        }

        private void ShowWin()
        {
            // Settle first (idempotent per attempt) so the panel shows the already-updated score.
            OnAuthoritativeOutcome(GameState.Win);
            LockAllFishInput();
            PlayOutcomeSound(SfxId.Win);
            if (_winPanel == null)
            {
                var parent = OverlayParent();
                if (parent == null)
                {
                    GameLog.Error(nameof(GameFlowController), "Win was committed, but the gameplay canvas could not be found.");
                    return;
                }

                _winPanel = WinPanelView.Create(
                    parent,
                    _scene.GlobalProgressDisplay.font,
                    _art,
                    () => _nextLevel?.Invoke(),
                    () => _replayLevel?.Invoke(),
                    () => _playAgain?.Invoke());
                BindButtonTaps(_winPanel);
            }

            var levelNumber = LevelNumber();
            _winPanel.Show(
                GoldReward(),
                HasNextLevel(),
                _progression != null && _progression.Progress != null ? _progression.Progress.Gold : 0,
                levelNumber,
                Math.Max(levelNumber, LevelCount()));
        }

        private void ShowLose()
        {
            // Settle first (idempotent per attempt) so the HUD already shows the deducted heart.
            OnAuthoritativeOutcome(GameState.Lose);
            LockAllFishInput();
            ResetCombo();
            PlayOutcomeSound(SfxId.Lose);
            if (_losePanel != null)
            {
                _losePanel.Show(LifeCost(), LevelNumber());
                return;
            }

            var parent = OverlayParent();
            if (parent == null)
            {
                GameLog.Error(nameof(GameFlowController), "Lose was committed, but the gameplay canvas could not be found.");
                return;
            }

            _losePanel = LosePanelView.Create(parent, _scene.GlobalProgressDisplay.font, _retryAttempt, _art, LifeCost(), LevelNumber());
            BindButtonTaps(_losePanel);
        }

        private Transform OverlayParent()
        {
            if (_scene == null || _scene.GlobalProgressDisplay == null)
            {
                return null;
            }

            var canvas = _scene.GlobalProgressDisplay.canvas;
            var overlay = canvas != null ? canvas.transform.Find("OverlayRoot") : null;
            return overlay != null ? overlay : _scene.GlobalProgressDisplay.transform;
        }

        private int LevelNumber()
        {
            return _levelNumber != null ? Math.Max(1, _levelNumber()) : 1;
        }

        private int LevelCount()
        {
            return _levelCount != null ? Math.Max(1, _levelCount()) : 1;
        }

        private void LockAllFishInput()
        {
            foreach (var pair in _viewsByFishId)
            {
                if (pair.Value != null)
                {
                    pair.Value.ReleaseInteraction();
                }
            }
        }

        private void RefreshAllBadges()
        {
            if (_scene == null || _scene.TankBoard == null || _session == null)
            {
                return;
            }

            var slots = _scene.TankBoard.Slots;
            var count = slots.Count < _session.Tanks.Count ? slots.Count : _session.Tanks.Count;
            for (var i = 0; i < count; i++)
            {
                RefreshTankBadge(i);
            }
        }

        private void RefreshTankBadge(int slotIndex)
        {
            if (_scene == null || _scene.TankBoard == null || _session == null)
            {
                return;
            }

            var slots = _scene.TankBoard.Slots;
            if (slotIndex < 0 || slotIndex >= slots.Count || slotIndex >= _session.Tanks.Count)
            {
                return;
            }

            var slot = slots[slotIndex];
            var tank = _session.Tanks[slotIndex];
            if (slot == null || tank == null || !tank.IsUnlocked)
            {
                return;
            }

            var badge = slot.PreviewBadge ?? _scene.TankBoard.EnsureBadge(slotIndex);
            if (badge == null)
            {
                return;
            }

            if (!tank.HasTarget)
            {
                badge.ShowInactive();
                return;
            }

            badge.ShowTarget(tank.CurrentTarget, tank.FillCount, tank.Capacity, _fishCatalog);
        }

        private void OnAuthoritativeOutcome(GameState state)
        {
            if (_progression == null || _session == null || _session.Config == null || _session.State != state)
            {
                return;
            }

            _progression.Settle(state, _session.Config);
            _progressionChanged?.Invoke();
        }

        private void EnsureUnlockModal()
        {
            if (_unlockModal != null || _scene == null || _scene.GlobalProgressDisplay == null)
            {
                return;
            }

            var canvas = _scene.GlobalProgressDisplay.canvas;
            var overlay = canvas != null ? canvas.transform.Find("OverlayRoot") : null;
            var parent = overlay != null ? overlay : _scene.GlobalProgressDisplay.transform;
            var coin = _art != null ? _art.CoinIcon : null;
            _unlockModal = UnlockModalView.Create(
                parent,
                _scene.GlobalProgressDisplay.font,
                coin,
                () => ConfirmGoldUnlock(),
                ConfirmRewardUnlock,
                CloseUnlockModal,
                _art);
            BindButtonTaps(_unlockModal);
        }

        private bool HasNextLevel()
        {
            return _hasNextLevel != null && _hasNextLevel();
        }

        private int GoldReward()
        {
            if (_session != null && _session.Config != null)
            {
                return _session.Config.LevelCompleteGoldReward;
            }

            return 20;
        }

        private int LifeCost()
        {
            if (_session != null && _session.Config != null)
            {
                return _session.Config.LoseLifeCost;
            }

            return 1;
        }

        private void RefreshProgress()
        {
            if (_scene == null || _scene.GlobalProgressDisplay == null || _session == null)
            {
                return;
            }

            _scene.GlobalProgressDisplay.text = LevelPresentationFormatting.FormatProgress(
                _session.Progress.CollectedFishCount,
                _session.Progress.TotalFishRequired);
        }
    }
}
