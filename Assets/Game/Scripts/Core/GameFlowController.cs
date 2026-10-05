using System;
using System.Collections;
using System.Collections.Generic;
using FishPuzzle.Ads;
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
        private readonly Dictionary<int, FishView> _viewsByFishId = new Dictionary<int, FishView>();
        private readonly List<GameObject> _ephemeral = new List<GameObject>();
        private readonly List<RectTransform> _reflowRects = new List<RectTransform>();
        private readonly List<Vector2> _reflowOrigins = new List<Vector2>();
        private readonly List<Vector2> _reflowTargets = new List<Vector2>();

        private LevelSession _session;
        private GameplaySceneReferences _scene;
        private FishVisualCatalog _fishCatalog;
        private GameplayArtCatalog _art;
        private AnimationTuning _tuning;
        private ProgressionRuntime _progression;
        private IRewardedAdService _rewardedAds;
        private Action _retryAttempt;
        private Action _progressionChanged;
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

        public LevelSession Session => _session;

        public GameState State => _session != null ? _session.State : GameState.Boot;

        public bool IsLosePanelVisible => _losePanel != null && _losePanel.IsShown;

        public bool IsWinPanelVisible => _winPanel != null && _winPanel.IsShown;

        public bool IsPresentationBusy => _visualLock;

        public bool IsFishPressCaptured => _pressedFish != null;

        public int AcceptedRouteCount => _acceptedRouteCount;

        public int TankSplashCount => _tankSplashCount;

        public TouchFeedbackController TouchFeedback => _touch;

        public bool IsUnlockModalVisible => _unlockModal != null && _unlockModal.IsShown;

        public UnlockModalView UnlockModal => _unlockModal;

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
            BindVisibleFish();
            RefreshAllBadges();
            RefreshProgress();
        }

        public bool OpenUnlockModal(int slotIndex)
        {
            if (_visualLock || _session == null || !_session.TryBeginUnlockModal(slotIndex))
            {
                return false;
            }

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

                return result;
            }

            if (!result.Succeeded)
            {
                return result;
            }

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

            _visualLock = true;
            _presentationFinished = false;
            _presentationElapsed = 0f;
            _session.HoldForPresentation(GameState.AutoPromotingTray);
            StartCoroutine(PlayExternal());
        }

        private void Update()
        {
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

            _pressedFish = view;
            view.BeginPress(_tuning);
            return true;
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

        private void PlayTankSplash(RectTransform anchor)
        {
            if (_suppressLandingSplash || anchor == null)
            {
                return;
            }

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
                droplet = _art.SplashDroplet != null ? _art.SplashDroplet : _art.SmallBubbleParticle;
                ring = _art.TouchRipple;
            }

            var splashView = TankSplashView.Play(_flightLayer, anchor.position, splash, droplet, ring, true);
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
            var turn = _session.LastTurn ?? TurnResolution.Empty;
            if (turn.PoppedBubble)
            {
                DisableBubbleInput(turn.PoppedBubbleId);
            }

            _session.HoldForPresentation(GameState.RoutingFish);
            StartCoroutine(PlayTurn(view, result));
        }

        private IEnumerator PlayTurn(FishView view, FishSelectionResult result)
        {
            try
            {
                RefreshProgress();
                StageBadges(result, _session.LastTurn);
                yield return RouteSelectedFish(view, result);
                if (result.Outcome == FishSelectionOutcome.RoutedToTank && result.TankCompleted)
                {
                    var completedType = view != null ? view.DisplayedType : result.NextTarget;
                    yield return PresentCompletion(
                        result.TankSlotIndex,
                        completedType,
                        result.HasNextTarget,
                        result.NextTarget,
                        result.ConsumedFishIds);
                }
                else if (result.Outcome == FishSelectionOutcome.RoutedToTank)
                {
                    RefreshTankBadge(result.TankSlotIndex);
                }

                var turn = _session.LastTurn ?? TurnResolution.Empty;
                if (_session.State != GameState.Lose)
                {
                    yield return PresentPromotions(turn);
                }

                if (_session.State != GameState.Lose && turn.PoppedBubble)
                {
                    _session.HoldForPresentation(GameState.PoppingBubble);
                    yield return PopBubble(turn.PoppedBubbleId);
                    yield return PlayPile(turn);
                }
            }
            finally
            {
                FinishPresentation();
            }
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
            yield return Squash(view != null ? view.transform : null, _tuning.FishTapSquashDuration);
            yield return FlyAndReflow(view, anchor, _tuning.FishRouteDuration);
            if (view != null && anchor != null)
            {
                Place(view, anchor, padding);
            }

            ApplyReflow(1f);
            if (toTank)
            {
                PlayTankSplash(anchor);
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
                yield return FlyToAnchor(view, anchor, 0f, _tuning.TrayAutoMoveDuration, TankTransform(promotion.TankSlotIndex));
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
                        promotion.ConsumedFishIds);
                }
                else
                {
                    RefreshTankBadge(promotion.TankSlotIndex);
                }
            }
        }

        private IEnumerator PresentCompletion(int slotIndex, FishType completedType, bool hasNext, FishType nextTarget, int[] consumed)
        {
            _session.HoldForPresentation(GameState.ResolvingTank);
            var capacity = CapacityOf(slotIndex);
            var badge = BadgeFor(slotIndex);
            if (badge != null)
            {
                badge.ShowTarget(completedType, capacity, capacity, _fishCatalog);
            }

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

        private IEnumerator PlayPile(TurnResolution turn)
        {
            if (turn.PileMoves.Count > 0)
            {
                _session.HoldForPresentation(GameState.SettlingBubblePile);
                for (var i = 0; i < turn.PileMoves.Count; i++)
                {
                    yield return MoveBubble(turn.PileMoves[i].BubbleId, turn.PileMoves[i].ToSlotId);
                }
            }

            if (turn.Spawns.Count > 0)
            {
                _session.HoldForPresentation(GameState.SpawningTopBubble);
                for (var i = 0; i < turn.Spawns.Count; i++)
                {
                    yield return SpawnBubble(turn.Spawns[i].BubbleId, turn.Spawns[i].SlotId);
                }
            }
        }

        private IEnumerator PopBubble(string bubbleId)
        {
            var pile = _scene.BubblePile;
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
            var popSprite = _suppressPopVfx || _art == null ? null : _art.BubblePopParticle;
            var smallSprite = _suppressPopVfx || _art == null ? null : _art.SmallBubbleParticle;
            if (BubblePopVfx.CanPlay(popSprite, smallSprite))
            {
                BubblePopVfx.Spawn(parent, origin, popSprite, smallSprite, _ephemeral);
            }

            var duration = _tuning.BubblePopDuration;
            var elapsed = 0f;
            while (elapsed < duration && rect != null)
            {
                elapsed += Step();
                var sample = PresentationMotion.Sample(rect != null, elapsed, duration);
                rect.localScale = Vector3.one * PresentationMotion.PopScale(sample.T);
                BubblePopVfx.Animate(_ephemeral, origin, sample.T);
                if (sample.Completed)
                {
                    break;
                }

                yield return null;
            }

            DestroyPoppingBubble();
            DestroyEphemeral();
        }

        private IEnumerator MoveBubble(string bubbleId, int toSlotId)
        {
            var pile = _scene.BubblePile;
            if (pile == null || !pile.TryGetByBubbleId(bubbleId, out var view) || view == null)
            {
                yield break;
            }

            var rect = view.transform as RectTransform;
            if (rect == null || !pile.TryGetSlotPosition(toSlotId, out var destination))
            {
                pile.AdoptSlot(view, toSlotId);
                yield break;
            }

            var origin = rect.anchoredPosition;
            pile.AdoptSlot(view, toSlotId);
            var duration = Mathf.Abs(destination.x - origin.x) > Mathf.Abs(destination.y - origin.y)
                ? _tuning.BubbleSlideDuration
                : _tuning.BubbleFallDuration;
            var elapsed = 0f;
            while (elapsed < duration && rect != null)
            {
                elapsed += Step();
                var sample = PresentationMotion.Sample(rect != null, elapsed, duration);
                rect.anchoredPosition = PresentationMotion.FallSlide(origin, destination, sample.T, _tuning.BubblePathArc);
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
            yield return null;
            var rect = view.transform as RectTransform;
            var duration = _tuning.BubbleTopSpawnDuration;
            var elapsed = 0f;
            while (elapsed < duration && rect != null)
            {
                elapsed += Step();
                var sample = PresentationMotion.Sample(rect != null, elapsed, duration);
                var eased = PresentationMotion.EaseInQuad(sample.T);
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

        private IEnumerator FlyAndReflow(FishView view, RectTransform anchor, float seconds)
        {
            var rect = view != null ? view.transform as RectTransform : null;
            EnsureFlightLayer();
            if (rect != null && _flightLayer != null && rect.parent != _flightLayer)
            {
                rect.SetParent(_flightLayer, true);
            }

            var start = rect != null ? rect.position : Vector3.zero;
            var end = anchor != null ? anchor.TransformPoint(anchor.rect.center) : start;
            var control = CurveControl(start, end);
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

            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Step();
                if (rect != null)
                {
                    var sample = PresentationMotion.Sample(true, elapsed, seconds);
                    rect.position = PresentationMotion.QuadraticBezier(start, control, end, PresentationMotion.EaseOutQuad(sample.T));
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
            var control = CurveControl(start, end);
            var elapsed = 0f;
            while (elapsed < seconds && rect != null)
            {
                elapsed += Step();
                var sample = PresentationMotion.Sample(rect != null, elapsed, seconds);
                rect.position = PresentationMotion.QuadraticBezier(start, control, end, PresentationMotion.EaseOutQuad(sample.T));
                if (sample.Completed)
                {
                    break;
                }

                yield return null;
            }

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

            var elapsed = 0f;
            while (elapsed < seconds && target != null)
            {
                elapsed += Step();
                var sample = PresentationMotion.Sample(target != null, elapsed, seconds);
                target.localScale = PresentationMotion.BounceScale(sample.T, _tuning.LandingOvershoot);
                if (sample.Completed)
                {
                    break;
                }

                yield return null;
            }

            if (target != null)
            {
                target.localScale = Vector3.one;
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
                    tank.localScale = PresentationMotion.BounceScale(sample.T, _tuning.LandingOvershoot * 0.5f);
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
                rect.localScale = Vector3.one;
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
                if (!BubbleFishLayoutController.TryGetPlacement(i, _reflowRects.Count, out var target, out _))
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

        private Vector3 CurveControl(Vector3 start, Vector3 end)
        {
            var scale = _flightLayer != null ? _flightLayer.lossyScale : Vector3.one;
            var control = (start + end) * 0.5f;
            control.y += _tuning.FishArcHeight * scale.y;
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
            _flightLayer.SetAsLastSibling();
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
            OnAuthoritativeOutcome(GameState.Win);
            LockAllFishInput();
            if (_winPanel != null)
            {
                _winPanel.Show();
                return;
            }

            if (_scene.GlobalProgressDisplay == null)
            {
                GameLog.Error(nameof(GameFlowController), "Win was committed, but the gameplay canvas could not be found.");
                return;
            }

            var canvas = _scene.GlobalProgressDisplay.canvas;
            var overlay = canvas != null ? canvas.transform.Find("OverlayRoot") : null;
            var parent = overlay != null ? overlay : _scene.GlobalProgressDisplay.transform;
            _winPanel = WinPanelView.Create(parent, _scene.GlobalProgressDisplay.font);
        }

        private void ShowLose()
        {
            OnAuthoritativeOutcome(GameState.Lose);
            LockAllFishInput();
            if (_losePanel != null)
            {
                _losePanel.Show();
                return;
            }

            if (_scene.GlobalProgressDisplay == null)
            {
                GameLog.Error(nameof(GameFlowController), "Lose was committed, but the gameplay canvas could not be found.");
                return;
            }

            var canvas = _scene.GlobalProgressDisplay.canvas;
            var overlay = canvas != null ? canvas.transform.Find("OverlayRoot") : null;
            var parent = overlay != null ? overlay : _scene.GlobalProgressDisplay.transform;
            _losePanel = LosePanelView.Create(parent, _scene.GlobalProgressDisplay.font, _retryAttempt);
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
                CloseUnlockModal);
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
