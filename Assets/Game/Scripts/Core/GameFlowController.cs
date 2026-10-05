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
    /// Domain resolution finishes before presentation. Motion is a short snap-backed tween.
    /// </summary>
    public sealed class GameFlowController : MonoBehaviour
    {
        private const float PopSeconds = 0.08f;
        private const float MoveSeconds = 0.12f;
        private const float SpawnSeconds = 0.14f;
        private const float TopEntryOffset = 340f;

        private readonly Dictionary<int, FishView> _viewsByFishId = new Dictionary<int, FishView>();

        private LevelSession _session;
        private GameplaySceneReferences _scene;
        private FishVisualCatalog _fishCatalog;
        private GameplayArtCatalog _art;
        private ProgressionRuntime _progression;
        private IRewardedAdService _rewardedAds;
        private Action _retryAttempt;
        private Action _progressionChanged;
        private LosePanelView _losePanel;
        private WinPanelView _winPanel;
        private UnlockModalView _unlockModal;
        private bool _visualLock;

        public LevelSession Session => _session;

        public GameState State => _session != null ? _session.State : GameState.Boot;

        public bool IsLosePanelVisible => _losePanel != null && _losePanel.IsShown;

        public bool IsWinPanelVisible => _winPanel != null && _winPanel.IsShown;

        public bool IsPresentationBusy => _visualLock;

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

        public void AbandonAttemptVisuals()
        {
            StopAllCoroutines();
            _visualLock = false;
            if (_session != null)
            {
                _session.ReleasePresentationHold();
            }

            _viewsByFishId.Clear();
            _session = null;
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

        public void Begin(LevelData level, GameConfig config, GameplaySceneReferences scene, FishVisualCatalog fishCatalog)
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
            _session = LevelSession.Start(level, config, false);
            _session.SetOutcomeHandler(OnAuthoritativeOutcome);
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
            if (_session == null)
            {
                return;
            }

            var turn = _session.LastTurn ?? TurnResolution.Empty;
            ApplyPromotions(turn);
            SyncTray();
            RefreshAllBadges();
            RefreshProgress();
            if (_session.State == GameState.Win)
            {
                LockAllFishInput();
                ShowWin();
            }
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

                    view.BindInteraction(runtimeFish.Id, HandleFishViewSelected);
                    _viewsByFishId.Add(runtimeFish.Id, view);
                }
            }
        }

        private void HandleFishViewSelected(FishView view)
        {
            if (_visualLock || view == null || _session == null)
            {
                return;
            }

            var result = _session.TrySelectFish(view.FishId, committed => ApplyCommittedFish(view, committed));
            if (result == null || !result.Accepted || _session.State == GameState.Lose)
            {
                return;
            }

            ApplyFollowThrough();
        }

        private void ApplyFollowThrough()
        {
            var turn = _session.LastTurn ?? TurnResolution.Empty;
            ApplyPromotions(turn);
            SyncTray();
            RefreshAllBadges();
            RefreshProgress();
            if (_session.State == GameState.Lose)
            {
                LockAllFishInput();
                return;
            }

            if (NeedsMotion(turn))
            {
                StartCoroutine(PlayPileMotion(turn));
                return;
            }

            if (_session.State == GameState.Win)
            {
                LockAllFishInput();
                ShowWin();
            }
        }

        private void ApplyPromotions(TurnResolution turn)
        {
            var promotions = turn.Promotions;
            for (var i = 0; i < promotions.Count; i++)
            {
                var promotion = promotions[i];
                if (promotion == null)
                {
                    continue;
                }

                if (promotion.CompletedTank)
                {
                    ClearConsumed(promotion.ConsumedFishIds);
                    continue;
                }

                if (_viewsByFishId.TryGetValue(promotion.FishId, out var view) && view != null)
                {
                    PlaceInTank(view, promotion.TankSlotIndex, promotion.LandedOrdinal);
                }
            }
        }

        private void SyncTray()
        {
            var tray = _session.Tray;
            for (var i = 0; i < tray.Count; i++)
            {
                var fish = tray.GetFishAt(i);
                if (fish == null || !_viewsByFishId.TryGetValue(fish.Id, out var view) || view == null)
                {
                    continue;
                }

                PlaceInTray(view, i);
            }
        }

        private IEnumerator PlayPileMotion(TurnResolution turn)
        {
            _visualLock = true;
            try
            {
                if (turn.PoppedBubble)
                {
                    _session.HoldForPresentation(GameState.PoppingBubble);
                    yield return PopBubble(turn.PoppedBubbleId);
                }

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
            finally
            {
                SnapPileToSession();
                _visualLock = false;
                _session.ReleasePresentationHold();
                if (_session.State == GameState.Win)
                {
                    LockAllFishInput();
                    ShowWin();
                }
            }
        }

        private IEnumerator PopBubble(string bubbleId)
        {
            var pile = _scene.BubblePile;
            if (pile == null || !pile.TryDetach(bubbleId, out var view) || view == null)
            {
                yield break;
            }

            var transformToScale = view.transform;
            var elapsed = 0f;
            while (elapsed < PopSeconds && view != null)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = PopSeconds <= 0f ? 1f : Mathf.Clamp01(elapsed / PopSeconds);
                transformToScale.localScale = Vector3.one * (1f - t);
                yield return null;
            }

            if (view != null)
            {
                Destroy(view.gameObject);
            }
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
            yield return LerpAnchor(rect, origin, destination, MoveSeconds);
        }

        private IEnumerator SpawnBubble(string bubbleId, int slotId)
        {
            var pile = _scene.BubblePile;
            if (pile == null || !pile.TryGetSlotPosition(slotId, out var destination))
            {
                yield break;
            }

            var start = destination + new Vector2(0f, TopEntryOffset);
            var view = pile.CreateIncoming(bubbleId, slotId, start);
            if (view == null)
            {
                yield break;
            }

            BindBubbleFish(view, bubbleId);
            var rect = view.transform as RectTransform;
            yield return LerpAnchor(rect, start, destination, SpawnSeconds);
        }

        private void BindBubbleFish(BubbleView view, string bubbleId)
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

            _session.SetBubbleInPlay(bubbleId, true);
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

                fishView.BindInteraction(runtimeFish.Id, HandleFishViewSelected);
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
                    Destroy(view.gameObject);
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
                    BindBubbleFish(created, bubble.BubbleId);
                }
            }
        }

        private static IEnumerator LerpAnchor(RectTransform rect, Vector2 from, Vector2 to, float seconds)
        {
            if (rect == null)
            {
                yield break;
            }

            if (seconds <= 0f)
            {
                rect.anchoredPosition = to;
                yield break;
            }

            var elapsed = 0f;
            while (elapsed < seconds)
            {
                if (rect == null)
                {
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / seconds);
                rect.anchoredPosition = Vector2.Lerp(from, to, t);
                yield return null;
            }

            if (rect != null)
            {
                rect.anchoredPosition = to;
            }
        }

        private static bool NeedsMotion(TurnResolution turn)
        {
            return turn.PoppedBubble || turn.PileMoves.Count > 0 || turn.Spawns.Count > 0;
        }

        private void ApplyCommittedFish(FishView view, FishSelectionResult result)
        {
            if (view == null || result == null || !result.Accepted)
            {
                return;
            }

            ReleaseFromBubble(view, result.SourceBubbleId);
            if (result.Outcome == FishSelectionOutcome.RoutedToTank)
            {
                PlaceInTank(view, result.TankSlotIndex, result.LandedOrdinal);
                if (result.TankCompleted)
                {
                    ClearConsumed(result.ConsumedFishIds);
                }

                RefreshTankBadge(result.TankSlotIndex);
                RefreshProgress();
                return;
            }

            PlaceInTray(view, result.TraySlotIndex);
            if (result.Outcome == FishSelectionOutcome.Lost)
            {
                ShowLose();
            }
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
            var slots = _scene.TankBoard.Slots;
            if (slotIndex < 0 || slotIndex >= slots.Count || slots[slotIndex] == null)
            {
                GameLog.Error(nameof(GameFlowController), "Tank slot " + slotIndex + " is missing.");
                view.ReleaseInteraction();
                return;
            }

            var anchor = slots[slotIndex].GetFishAnchor(ordinal);
            if (anchor == null)
            {
                GameLog.Error(nameof(GameFlowController), "Tank " + slotIndex + " has no fish anchor " + ordinal + ".");
                view.ReleaseInteraction();
                return;
            }

            Place(view, anchor, 0f);
        }

        private void PlaceInTray(FishView view, int slotIndex)
        {
            var anchor = _scene.WaitingTray.GetSlotAnchor(slotIndex);
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
                view.gameObject.SetActive(false);
                Destroy(view.gameObject);
            }
        }

        private void OnDestroy()
        {
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
            var slots = _scene.TankBoard.Slots;
            var count = slots.Count < _session.Tanks.Count ? slots.Count : _session.Tanks.Count;
            for (var i = 0; i < count; i++)
            {
                RefreshTankBadge(i);
            }
        }

        private void RefreshTankBadge(int slotIndex)
        {
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
            if (_scene.GlobalProgressDisplay == null || _session == null)
            {
                return;
            }

            _scene.GlobalProgressDisplay.text = LevelPresentationFormatting.FormatProgress(
                _session.Progress.CollectedFishCount,
                _session.Progress.TotalFishRequired);
        }
    }
}
