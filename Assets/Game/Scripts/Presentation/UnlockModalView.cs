using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FishPuzzle.Presentation
{
    /// <summary>
    /// Extra-tank unlock modal. Buttons report choices. They do not change gold or tank state.
    /// </summary>
    public sealed class UnlockModalView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _status;
        [SerializeField] private Button _goldButton;
        [SerializeField] private Button _rewardButton;
        [SerializeField] private Button _closeButton;
        [SerializeField] private RectTransform _card;

        private Action _onGold;
        private Action _onReward;
        private Action _onClose;

        public bool IsShown => gameObject.activeSelf;

        public string Status => _status != null ? _status.text : string.Empty;

        public Button GoldButton => _goldButton;

        public Button RewardButton => _rewardButton;

        public Button CloseButton => _closeButton;

        public static UnlockModalView Create(
            Transform parent,
            TMP_FontAsset font,
            Sprite coinIcon,
            Action onGold,
            Action onReward,
            Action onClose,
            GameplayArtCatalog art = null)
        {
            var panel = art != null ? art.ModalPanel : null;
            var header = art != null ? art.ModalHeader : null;
            var green = art != null ? art.ButtonGreenLarge : null;
            var blue = art != null ? art.ButtonBlueLarge : null;
            var closeFace = art != null ? art.ButtonClose : null;
            var closeIcon = art != null ? art.CloseIcon : null;
            var adIcon = art != null ? art.RewardedAdIcon : null;
            var tank = art != null ? art.TankBack : null;
            if (coinIcon == null && art != null)
            {
                coinIcon = art.CoinIcon;
            }

            var dim = ModalChrome.CreateDim(parent, "UnlockModal");
            var card = ModalChrome.CreateCard(dim.transform, panel);
            var close = ModalChrome.CreateButton(
                card,
                "CloseButton",
                string.Empty,
                font,
                new Vector2(292f, 400f),
                new Vector2(88f, 88f),
                closeFace,
                new Color(0.85f, 0.18f, 0.16f, 1f),
                Color.white,
                closeIcon);
            var closeMark = close.transform.Find("Icon") as RectTransform;
            if (closeMark != null)
            {
                closeMark.anchoredPosition = Vector2.zero;
                closeMark.sizeDelta = new Vector2(36f, 36f);
            }
            ModalChrome.CreateSprite(card, "Header", header, new Vector2(0f, 300f), new Vector2(480f, 133f), false);
            ModalChrome.CreateLabel(card, "Title", "Unlock Tank", font, 48f, new Vector2(0f, 300f), new Vector2(380f, 80f), Color.white);
            if (tank != null)
            {
                ModalChrome.CreateSprite(card, "TankPreview", tank, new Vector2(0f, 145f), new Vector2(140f, 140f), false);
            }

            var textColor = new Color(0.05f, 0.16f, 0.28f, 1f);
            var gold = ModalChrome.CreateButton(
                card,
                "GoldButton",
                "600 Gold",
                font,
                new Vector2(0f, -55f),
                new Vector2(468f, 161f),
                green,
                new Color(0.95f, 0.72f, 0.2f, 1f),
                textColor,
                coinIcon);
            var reward = ModalChrome.CreateButton(
                card,
                "RewardButton",
                "Free",
                font,
                new Vector2(0f, -245f),
                new Vector2(468f, 166f),
                blue,
                new Color(0.25f, 0.62f, 0.92f, 1f),
                textColor,
                adIcon);
            var status = ModalChrome.CreateLabel(card, "Status", string.Empty, font, 32f, new Vector2(0f, -400f), new Vector2(520f, 64f), new Color(0.55f, 0.08f, 0.1f, 1f));

            var view = dim.gameObject.AddComponent<UnlockModalView>();
            view._status = status;
            view._goldButton = gold;
            view._rewardButton = reward;
            view._closeButton = close;
            view._card = card;
            view._onGold = onGold;
            view._onReward = onReward;
            view._onClose = onClose;
            gold.onClick.AddListener(view.HandleGold);
            reward.onClick.AddListener(view.HandleReward);
            close.onClick.AddListener(view.HandleClose);
            dim.gameObject.SetActive(false);
            return view;
        }

        public void Show(int goldCost)
        {
            if (_goldButton != null)
            {
                var label = _goldButton.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null)
                {
                    label.text = goldCost.ToString() + " Gold";
                }
            }

            if (_status != null)
            {
                _status.text = string.Empty;
            }

            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            ModalChrome.Fit(_card);
        }

        public void ShowInsufficientGold()
        {
            if (_status != null)
            {
                _status.text = "Not enough Gold";
            }

            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            ModalChrome.Fit(_card);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void HandleGold()
        {
            if (_onGold != null)
            {
                _onGold();
            }
        }

        private void HandleReward()
        {
            if (_onReward != null)
            {
                _onReward();
            }
        }

        private void HandleClose()
        {
            if (_onClose != null)
            {
                _onClose();
            }
        }
    }
}
