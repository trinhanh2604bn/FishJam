using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FishPuzzle.Presentation
{
    /// <summary>
    /// Full-screen level-complete reward screen: "WELL DONE!", +20 Gold reward stage, level progress panel,
    /// "Nhận" and "Nhận x2". It never awards gold; it only shows what the wallet already applied.
    /// "Nhận" continues (next level, or play again after the last level).
    /// "Nhận x2" is presentation-only until rewarded-ad doubling exists: it continues exactly like
    /// "Nhận" and grants nothing extra, so the +20 rule stays exactly-once.
    /// </summary>
    public sealed class WinPanelView : MonoBehaviour
    {
        public const string TitleText = "WELL DONE!";
        public const string ProgressCaptionText = "Hoàn thành các màn chơi để mở khóa vật phẩm";
        public const string AllLevelsCompletedText = "Đã hoàn thành tất cả các màn!";
        public const string ClaimText = "Nhận";
        public const string ClaimDoubleText = "Nhận x2";
        public const string ReplayText = "Chơi lại";

        private const float TrackWidth = 660f;
        private const float TrackHeight = 74f;
        private const float FillInset = 8f;

        [SerializeField] private TextMeshProUGUI _label;
        [SerializeField] private TextMeshProUGUI _allDone;
        [SerializeField] private TextMeshProUGUI _reward;
        [SerializeField] private TextMeshProUGUI _gold;
        [SerializeField] private TextMeshProUGUI _caption;
        [SerializeField] private TextMeshProUGUI _progressLabel;
        [SerializeField] private RectTransform _progressFill;
        [SerializeField] private RectTransform _stage;
        [SerializeField] private Button _primaryButton;
        [SerializeField] private TextMeshProUGUI _primaryLabel;
        [SerializeField] private Button _doubleButton;
        [SerializeField] private Button _replayButton;

        private Action _onNextLevel;
        private Action _onReplay;
        private Action _onPlayAgain;
        private ModalPopIn _pop;
        private bool _hasNextLevel = true;
        private bool _requestSent;
        private float _progressFraction;

        public bool IsShown => gameObject.activeSelf;

        /// <summary>Celebration title, "WELL DONE!".</summary>
        public string Message => _label != null ? _label.text : string.Empty;

        public string Subtitle => _allDone != null && _allDone.gameObject.activeSelf ? _allDone.text : string.Empty;

        public string RewardText => _reward != null ? _reward.text : string.Empty;

        /// <summary>Top-right Gold counter, showing the wallet balance after the +20 was applied.</summary>
        public string GoldText => _gold != null ? _gold.text : string.Empty;

        public string ProgressCaption => _caption != null ? _caption.text : string.Empty;

        public string ProgressText => _progressLabel != null ? _progressLabel.text : string.Empty;

        public float ProgressFraction => _progressFraction;

        public Button PrimaryButton => _primaryButton;

        public string PrimaryButtonText => _primaryLabel != null ? _primaryLabel.text : string.Empty;

        public Button DoubleButton => _doubleButton;

        public Button ReplayButton => _replayButton;

        public bool HasNextLevel => _hasNextLevel;

        public static WinPanelView Create(
            Transform parent,
            TMP_FontAsset font,
            GameplayArtCatalog art,
            Action onNextLevel,
            Action onReplay,
            Action onPlayAgain)
        {
            var dim = ModalChrome.CreateDim(parent, "WinPanel");
            dim.color = new Color(0.04f, 0.32f, 0.62f, 1f);
            if (art != null && art.WinBackground != null)
            {
                var backdrop = ModalChrome.CreateSprite(dim.transform, "WinBackground", art.WinBackground, Vector2.zero, Vector2.zero, false);
                Stretch(backdrop.rectTransform);
                backdrop.preserveAspect = false;
                var fitter = backdrop.gameObject.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fitter.aspectRatio = art.WinBackground.rect.width / Mathf.Max(1f, art.WinBackground.rect.height);
                backdrop.transform.SetAsFirstSibling();
            }

            var root = dim.transform;

            // Top row: replay (left) and gold counter (right).
            var replay = ModalChrome.CreateButton(root, "ReplayButton", ReplayText, font, Vector2.zero, new Vector2(230f, 96f), art != null ? art.ButtonBlueLarge : null, new Color(0.2f, 0.52f, 0.9f, 1f), Color.white, null);
            ResultUiStyle.Anchor(replay.transform as RectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(36f, -56f), new Vector2(230f, 96f));
            ResultUiStyle.StyleButtonLabel(replay, 38f, ResultUiStyle.ButtonBlueText);

            var counter = ModalChrome.CreateSprite(root, "GoldCounter", art != null ? art.CurrencyCounterFrame : null, Vector2.zero, Vector2.zero, false);
            counter.preserveAspect = false;
            if (art == null || art.CurrencyCounterFrame == null)
            {
                counter.color = new Color(1f, 0.97f, 0.92f, 1f);
            }

            ResultUiStyle.Anchor(counter.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-36f, -60f), new Vector2(270f, 90f));
            ModalChrome.CreateSprite(counter.transform, "CoinIcon", art != null ? art.CoinIcon : null, new Vector2(-120f, 0f), new Vector2(110f, 110f), false);
            var gold = ModalChrome.CreateLabel(counter.transform, "GoldValue", "0", font, 50f, new Vector2(30f, 0f), new Vector2(180f, 72f), ResultUiStyle.Navy);
            gold.fontStyle |= FontStyles.Bold;

            // Title.
            var title = ModalChrome.CreateLabel(root, "WinLabel", TitleText, font, 150f, Vector2.zero, new Vector2(1060f, 220f), Color.white);
            ResultUiStyle.Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -330f), new Vector2(1060f, 220f));
            ResultUiStyle.Chunky(title, 150f, Color.white, new Color(0.62f, 0.27f, 0.02f, 1f), 0.26f);
            ResultUiStyle.VerticalGradient(title, new Color(1f, 0.93f, 0.36f, 1f), new Color(1f, 0.62f, 0.1f, 1f));
            title.characterSpacing = -2f;

            var allDone = ModalChrome.CreateLabel(root, "AllDoneLabel", AllLevelsCompletedText, font, 46f, Vector2.zero, new Vector2(960f, 80f), Color.white);
            ResultUiStyle.Anchor(allDone.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -470f), new Vector2(960f, 80f));
            ResultUiStyle.Chunky(allDone, 46f, Color.white, ResultUiStyle.DeepNavy);

            // Reward stage (center): burst, coin pile, pearls, "+20" plate.
            var stageObject = new GameObject("RewardStage", typeof(RectTransform));
            stageObject.transform.SetParent(root, false);
            var stage = stageObject.GetComponent<RectTransform>();
            ResultUiStyle.Anchor(stage, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 90f), new Vector2(700f, 640f));
            if (art != null && art.SuccessBurst != null)
            {
                var burst = ModalChrome.CreateSprite(stage, "SuccessBurst", art.SuccessBurst, new Vector2(0f, 60f), new Vector2(620f, 620f), false);
                var burstColor = burst.color;
                burstColor.a = 0.85f;
                burst.color = burstColor;
            }

            var coin = art != null ? art.CoinIcon : null;
            var pearl = art != null ? art.Pearl : null;
            ModalChrome.CreateSprite(stage, "CoinBackLeft", coin, new Vector2(-150f, 10f), new Vector2(150f, 150f), false);
            ModalChrome.CreateSprite(stage, "CoinBackRight", coin, new Vector2(150f, 20f), new Vector2(150f, 150f), false);
            ModalChrome.CreateSprite(stage, "PearlLeft", pearl, new Vector2(-240f, -70f), new Vector2(76f, 76f), false);
            ModalChrome.CreateSprite(stage, "PearlRight", pearl, new Vector2(232f, -84f), new Vector2(64f, 64f), false);
            ModalChrome.CreateSprite(stage, "PearlTop", pearl, new Vector2(118f, 150f), new Vector2(52f, 52f), false);
            ModalChrome.CreateSprite(stage, "CoinFront", coin, new Vector2(0f, 60f), new Vector2(270f, 270f), false);
            if (art != null && art.CoinSparkle != null)
            {
                ModalChrome.CreateSprite(stage, "SparkleA", art.CoinSparkle, new Vector2(150f, 190f), new Vector2(90f, 90f), false);
                ModalChrome.CreateSprite(stage, "SparkleB", art.CoinSparkle, new Vector2(-190f, 150f), new Vector2(64f, 64f), false);
            }

            ResultUiStyle.Sliced(stage, "RewardPlate", art != null ? art.RewardPlate : null, new Vector2(0f, -190f), new Vector2(360f, 150f), new Color(0.3f, 0.62f, 1f, 1f));
            ModalChrome.CreateSprite(stage, "RewardCoin", coin, new Vector2(-82f, -190f), new Vector2(96f, 96f), false);
            if (pearl != null)
            {
                ModalChrome.CreateSprite(stage, "PlatePearlLeft", pearl, new Vector2(-170f, -136f), new Vector2(40f, 40f), false);
                ModalChrome.CreateSprite(stage, "PlatePearlRight", pearl, new Vector2(170f, -136f), new Vector2(40f, 40f), false);
            }

            var reward = ResultUiStyle.Label(stage, "Reward", "+20", font, new Vector2(46f, -190f), new Vector2(220f, 110f), 90f, Color.white, ResultUiStyle.DeepNavy);

            // Progress panel.
            var panel = ResultUiStyle.Sliced(root, "ProgressPanel", art != null ? art.ResultPanel : null, Vector2.zero, Vector2.zero, new Color(0.18f, 0.42f, 0.9f, 1f));
            ResultUiStyle.Anchor(panel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 330f), new Vector2(920f, 320f));
            var caption = ModalChrome.CreateLabel(panel.transform, "ProgressCaption", ProgressCaptionText, font, 46f, new Vector2(0f, 62f), new Vector2(800f, 130f), Color.white);
            ResultUiStyle.Chunky(caption, 46f, Color.white, ResultUiStyle.DeepNavy);
            caption.lineSpacing = -12f;
            var track = ResultUiStyle.Sliced(panel.transform, "ProgressTrack", art != null ? art.ProgressTrack : null, new Vector2(-30f, -72f), new Vector2(TrackWidth, TrackHeight), new Color(0.14f, 0.12f, 0.5f, 1f));
            var fill = ResultUiStyle.Sliced(track.transform, "ProgressFill", art != null ? art.ProgressFill : null, Vector2.zero, Vector2.zero, new Color(0.3f, 0.82f, 0.3f, 1f));
            var fillRect = fill.rectTransform;
            fillRect.anchorMin = new Vector2(0f, 0.5f);
            fillRect.anchorMax = new Vector2(0f, 0.5f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.anchoredPosition = new Vector2(FillInset, 0f);
            fillRect.sizeDelta = new Vector2(TrackHeight - (2f * FillInset), TrackHeight - (2f * FillInset));
            var progressLabel = ModalChrome.CreateLabel(track.transform, "ProgressLabel", "0/0", font, 44f, Vector2.zero, new Vector2(300f, 70f), Color.white);
            ResultUiStyle.Chunky(progressLabel, 44f, Color.white, ResultUiStyle.DeepNavy);
            ModalChrome.CreateSprite(panel.transform, "ProgressGift", pearl != null ? pearl : coin, new Vector2(330f, -72f), new Vector2(118f, 118f), false);

            // Buttons.
            var primary = ModalChrome.CreateButton(root, "ClaimButton", ClaimText, font, Vector2.zero, new Vector2(430f, 160f), art != null ? art.ButtonGreenLarge : null, new Color(0.2f, 0.75f, 0.28f, 1f), Color.white, null);
            ResultUiStyle.Anchor(primary.transform as RectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(-232f, 165f), new Vector2(430f, 160f));
            var primaryLabel = ResultUiStyle.StyleButtonLabel(primary, 64f, ResultUiStyle.ButtonGreenText);
            var doubled = ModalChrome.CreateButton(root, "ClaimDoubleButton", ClaimDoubleText, font, Vector2.zero, new Vector2(430f, 160f), art != null ? art.ButtonBlueLarge : null, new Color(0.2f, 0.52f, 0.9f, 1f), Color.white, art != null ? art.RewardedAdIcon : null);
            ResultUiStyle.Anchor(doubled.transform as RectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(232f, 165f), new Vector2(430f, 160f));
            ResultUiStyle.StyleButtonLabel(doubled, 56f, ResultUiStyle.ButtonBlueText);

            var view = dim.gameObject.AddComponent<WinPanelView>();
            view._label = title;
            view._allDone = allDone;
            view._reward = reward;
            view._gold = gold;
            view._caption = caption;
            view._progressLabel = progressLabel;
            view._progressFill = fillRect;
            view._stage = stage;
            view._primaryButton = primary;
            view._primaryLabel = primaryLabel;
            view._doubleButton = doubled;
            view._replayButton = replay;
            view._onNextLevel = onNextLevel;
            view._onReplay = onReplay;
            view._onPlayAgain = onPlayAgain;
            view._pop = ModalPopIn.Attach(dim.gameObject, stage, title.rectTransform);
            primary.onClick.AddListener(view.HandlePrimary);
            doubled.onClick.AddListener(view.HandleDouble);
            replay.onClick.AddListener(view.HandleReplay);
            dim.gameObject.SetActive(false);
            return view;
        }

        /// <param name="rewardGold">Gold granted for this win (shown with the coin icon as "+20").</param>
        /// <param name="gold">Wallet gold after the +20 was applied by the flow.</param>
        /// <param name="completedLevels">Levels completed in the sequence, including this one.</param>
        public void Show(int rewardGold, bool hasNextLevel, int gold, int completedLevels, int totalLevels)
        {
            _hasNextLevel = hasNextLevel;
            _requestSent = false;
            if (_reward != null)
            {
                _reward.text = FormatReward(rewardGold);
            }

            if (_gold != null)
            {
                _gold.text = LevelPresentationFormatting.FormatCount(gold);
            }

            if (_allDone != null)
            {
                _allDone.gameObject.SetActive(!hasNextLevel);
            }

            if (_primaryLabel != null)
            {
                _primaryLabel.text = ClaimText;
            }

            SetProgress(completedLevels, totalLevels);
            SetButtonsInteractable(true);
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            if (_pop != null)
            {
                _pop.Play();
            }
        }

        public void Hide()
        {
            if (_pop != null)
            {
                _pop.Complete();
            }

            gameObject.SetActive(false);
        }

        public static string FormatReward(int rewardGold)
        {
            return "+" + rewardGold;
        }

        private void SetProgress(int completed, int total)
        {
            var safeTotal = Mathf.Max(1, total);
            var safeCompleted = Mathf.Clamp(completed, 0, safeTotal);
            _progressFraction = (float)safeCompleted / safeTotal;
            if (_progressLabel != null)
            {
                _progressLabel.text = safeCompleted + "/" + safeTotal;
            }

            if (_progressFill != null)
            {
                var minWidth = TrackHeight - (2f * FillInset);
                var fullWidth = TrackWidth - (2f * FillInset);
                var width = Mathf.Max(minWidth, fullWidth * _progressFraction);
                _progressFill.sizeDelta = new Vector2(width, _progressFill.sizeDelta.y);
                _progressFill.gameObject.SetActive(_progressFraction > 0f);
            }
        }

        private void HandlePrimary()
        {
            if (!TryBeginRequest())
            {
                return;
            }

            if (_hasNextLevel)
            {
                _onNextLevel?.Invoke();
            }
            else
            {
                _onPlayAgain?.Invoke();
            }
        }

        private void HandleDouble()
        {
            // Rewarded-ad doubling is not implemented. Continue exactly like "Nhận" without extra reward.
            HandlePrimary();
        }

        private void HandleReplay()
        {
            if (!TryBeginRequest())
            {
                return;
            }

            _onReplay?.Invoke();
        }

        private bool TryBeginRequest()
        {
            if (!IsShown || _requestSent)
            {
                return false;
            }

            _requestSent = true;
            SetButtonsInteractable(false);
            return true;
        }

        private void SetButtonsInteractable(bool interactable)
        {
            if (_primaryButton != null)
            {
                _primaryButton.interactable = interactable;
            }

            if (_doubleButton != null)
            {
                _doubleButton.interactable = interactable;
            }

            if (_replayButton != null)
            {
                _replayButton.interactable = interactable;
            }
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
