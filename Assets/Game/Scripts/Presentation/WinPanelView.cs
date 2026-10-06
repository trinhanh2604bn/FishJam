using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FishPuzzle.Presentation
{
    /// <summary>
    /// Level-complete notice. It does not award score or change progression.
    /// Buttons only forward requests to the level flow owner.
    /// </summary>
    public sealed class WinPanelView : MonoBehaviour
    {
        public const string NextLevelText = "NEXT LEVEL";
        public const string PlayAgainText = "PLAY AGAIN";
        public const string ReplayText = "REPLAY";
        public const string LevelCompletedText = "Level completed";
        public const string AllLevelsCompletedText = "All levels completed";

        [SerializeField] private TextMeshProUGUI _label;
        [SerializeField] private TextMeshProUGUI _subtitle;
        [SerializeField] private TextMeshProUGUI _reward;
        [SerializeField] private RectTransform _card;
        [SerializeField] private Button _primaryButton;
        [SerializeField] private TextMeshProUGUI _primaryLabel;
        [SerializeField] private Button _replayButton;

        private Action _onNextLevel;
        private Action _onReplay;
        private Action _onPlayAgain;
        private bool _hasNextLevel = true;
        private bool _requestSent;

        public bool IsShown => gameObject.activeSelf;

        /// <summary>Title text. The label renders in upper case.</summary>
        public string Message => _label != null ? _label.text : string.Empty;

        public string Subtitle => _subtitle != null ? _subtitle.text : string.Empty;

        public string RewardText => _reward != null ? _reward.text : string.Empty;

        public Button PrimaryButton => _primaryButton;

        public string PrimaryButtonText => _primaryLabel != null ? _primaryLabel.text : string.Empty;

        public Button ReplayButton => _replayButton;

        public bool HasNextLevel => _hasNextLevel;

        public static WinPanelView Create(Transform parent, TMP_FontAsset font, GameplayArtCatalog art = null, int rewardPoints = 20)
        {
            return Create(parent, font, art, rewardPoints, null, null, null, true);
        }

        public static WinPanelView Create(
            Transform parent,
            TMP_FontAsset font,
            GameplayArtCatalog art,
            int rewardPoints,
            Action onNextLevel,
            Action onReplay,
            Action onPlayAgain,
            bool hasNextLevel)
        {
            var dim = ModalChrome.CreateDim(parent, "WinPanel");
            if (art != null && art.WinBackground != null)
            {
                var backdrop = ModalChrome.CreateSprite(dim.transform, "WinBackground", art.WinBackground, Vector2.zero, new Vector2(1080f, 1920f), false);
                Stretch(backdrop.rectTransform);
                backdrop.preserveAspect = false;
                backdrop.transform.SetAsFirstSibling();
            }

            var card = ModalChrome.CreateCard(dim.transform, art != null ? art.ModalPanel : null);
            ModalChrome.CreateSprite(card, "Header", art != null ? art.ModalHeader : null, new Vector2(0f, 300f), new Vector2(520f, 144f), false);
            var title = ModalChrome.CreateLabel(card, "WinLabel", "Win", font, 68f, new Vector2(0f, 300f), new Vector2(420f, 90f), Color.white);
            title.fontStyle |= FontStyles.UpperCase;
            var subtitle = ModalChrome.CreateLabel(
                card,
                "Subtitle",
                LevelCompletedText,
                font,
                44f,
                new Vector2(0f, 186f),
                new Vector2(560f, 64f),
                new Color(0.12f, 0.28f, 0.48f, 1f));

            if (art != null && art.SuccessBurst != null)
            {
                var burst = ModalChrome.CreateSprite(card, "SuccessBurst", art.SuccessBurst, new Vector2(0f, 10f), new Vector2(200f, 200f), false);
                var burstColor = burst.color;
                burstColor.a = 0.9f;
                burst.color = burstColor;
            }

            ModalChrome.CreateSprite(card, "RewardPlate", art != null ? art.ModalInner : null, new Vector2(0f, 10f), new Vector2(440f, 250f), false);
            var hasCoin = art != null && art.CoinIcon != null;
            if (hasCoin)
            {
                ModalChrome.CreateSprite(card, "Coin", art.CoinIcon, new Vector2(-130f, 10f), new Vector2(88f, 88f), false);
            }

            if (art != null && art.CoinSparkle != null)
            {
                ModalChrome.CreateSprite(card, "CoinSparkle", art.CoinSparkle, new Vector2(170f, 100f), new Vector2(64f, 64f), false);
            }

            var reward = ModalChrome.CreateLabel(
                card,
                "Reward",
                FormatReward(rewardPoints),
                font,
                58f,
                hasCoin ? new Vector2(40f, 10f) : new Vector2(0f, 10f),
                new Vector2(300f, 90f),
                new Color(0.12f, 0.28f, 0.48f, 1f));

            var primary = ModalChrome.CreateButton(
                card,
                "NextLevelButton",
                NextLevelText,
                font,
                new Vector2(0f, -200f),
                new Vector2(430f, 136f),
                art != null ? art.ButtonGreenLarge : null,
                new Color(0.2f, 0.75f, 0.28f, 1f),
                new Color(0.05f, 0.16f, 0.28f, 1f),
                null);
            var replay = ModalChrome.CreateButton(
                card,
                "ReplayButton",
                ReplayText,
                font,
                new Vector2(0f, -340f),
                new Vector2(380f, 112f),
                art != null ? art.ButtonBlueLarge : null,
                new Color(0.2f, 0.52f, 0.9f, 1f),
                new Color(0.05f, 0.16f, 0.28f, 1f),
                null);

            var view = dim.gameObject.AddComponent<WinPanelView>();
            view._label = title;
            view._subtitle = subtitle;
            view._reward = reward;
            view._card = card;
            view._primaryButton = primary;
            view._primaryLabel = primary.GetComponentInChildren<TextMeshProUGUI>(true);
            view._replayButton = replay;
            view._onNextLevel = onNextLevel;
            view._onReplay = onReplay;
            view._onPlayAgain = onPlayAgain;
            primary.onClick.AddListener(view.HandlePrimary);
            replay.onClick.AddListener(view.HandleReplay);
            view.Show(rewardPoints, hasNextLevel);
            return view;
        }

        public void Show(int rewardPoints, bool hasNextLevel)
        {
            _hasNextLevel = hasNextLevel;
            if (_subtitle != null)
            {
                _subtitle.text = hasNextLevel ? LevelCompletedText : AllLevelsCompletedText;
            }

            if (_primaryLabel != null)
            {
                _primaryLabel.text = hasNextLevel ? NextLevelText : PlayAgainText;
            }

            _requestSent = false;
            SetButtonsInteractable(true);
            Show(rewardPoints);
        }

        public void Show(int rewardPoints)
        {
            if (_reward != null)
            {
                _reward.text = FormatReward(rewardPoints);
            }

            Show();
        }

        public void Show()
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            ModalChrome.Fit(_card);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        public static string FormatReward(int rewardPoints)
        {
            return "+" + rewardPoints + " Score";
        }

        private void HandlePrimary()
        {
            if (!IsShown || _requestSent)
            {
                return;
            }

            _requestSent = true;

            // One request per panel opening. The flow owner rebuilds the attempt and hides the panel.
            SetButtonsInteractable(false);
            if (_hasNextLevel)
            {
                _onNextLevel?.Invoke();
            }
            else
            {
                _onPlayAgain?.Invoke();
            }
        }

        private void HandleReplay()
        {
            if (!IsShown || _requestSent)
            {
                return;
            }

            _requestSent = true;
            SetButtonsInteractable(false);
            _onReplay?.Invoke();
        }

        private void SetButtonsInteractable(bool interactable)
        {
            if (_primaryButton != null)
            {
                _primaryButton.interactable = interactable;
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
