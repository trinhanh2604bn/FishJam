using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FishPuzzle.Presentation
{
    /// <summary>
    /// Minimal extra-tank unlock modal. Buttons report choices. They do not change gold or tank state.
    /// </summary>
    public sealed class UnlockModalView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _status;
        [SerializeField] private Button _goldButton;
        [SerializeField] private Button _rewardButton;
        [SerializeField] private Button _closeButton;

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
            Action onClose)
        {
            var root = new GameObject("UnlockModal", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            root.transform.SetParent(parent, false);
            Stretch(root.GetComponent<RectTransform>());
            var dim = root.GetComponent<Image>();
            dim.color = new Color(0.03f, 0.05f, 0.09f, 0.82f);
            dim.raycastTarget = true;

            var cardObject = new GameObject("Card", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            cardObject.transform.SetParent(root.transform, false);
            var card = cardObject.GetComponent<RectTransform>();
            card.anchorMin = new Vector2(0.5f, 0.5f);
            card.anchorMax = new Vector2(0.5f, 0.5f);
            card.pivot = new Vector2(0.5f, 0.5f);
            card.sizeDelta = new Vector2(760f, 720f);
            cardObject.GetComponent<Image>().color = new Color(0.12f, 0.2f, 0.28f, 0.98f);
            cardObject.GetComponent<Image>().raycastTarget = true;

            var title = CreateLabel(card, "Title", "Unlock Tank", font, 64f, new Vector2(0f, 250f), new Vector2(640f, 90f));
            title.alignment = TextAlignmentOptions.Center;

            var gold = CreateButton(card, "GoldButton", "600 Gold", font, new Vector2(0f, 80f), new Color(0.95f, 0.72f, 0.2f, 1f), coinIcon);
            var reward = CreateButton(card, "RewardButton", "Free", font, new Vector2(0f, -70f), new Color(0.25f, 0.72f, 0.42f, 1f), null);
            var close = CreateButton(card, "CloseButton", "Close", font, new Vector2(0f, -230f), new Color(0.35f, 0.4f, 0.48f, 1f), null);
            var status = CreateLabel(card, "Status", string.Empty, font, 36f, new Vector2(0f, -340f), new Vector2(640f, 70f));
            status.alignment = TextAlignmentOptions.Center;
            status.color = new Color(1f, 0.55f, 0.45f, 1f);

            var view = root.AddComponent<UnlockModalView>();
            view._status = status;
            view._goldButton = gold;
            view._rewardButton = reward;
            view._closeButton = close;
            view._onGold = onGold;
            view._onReward = onReward;
            view._onClose = onClose;
            gold.onClick.AddListener(view.HandleGold);
            reward.onClick.AddListener(view.HandleReward);
            close.onClick.AddListener(view.HandleClose);
            root.SetActive(false);
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
        }

        public void ShowInsufficientGold()
        {
            if (_status != null)
            {
                _status.text = "Not enough Gold";
            }

            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void HandleGold()
        {
            _onGold?.Invoke();
        }

        private void HandleReward()
        {
            _onReward?.Invoke();
        }

        private void HandleClose()
        {
            _onClose?.Invoke();
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static TextMeshProUGUI CreateLabel(
            Transform parent,
            string name,
            string text,
            TMP_FontAsset font,
            float fontSize,
            Vector2 position,
            Vector2 size)
        {
            var labelObject = new GameObject(name, typeof(RectTransform));
            labelObject.SetActive(false);
            labelObject.transform.SetParent(parent, false);
            var rect = labelObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var label = labelObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.text = text;
            label.fontSize = fontSize;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false;
            labelObject.SetActive(true);
            return label;
        }

        private static Button CreateButton(
            Transform parent,
            string name,
            string text,
            TMP_FontAsset font,
            Vector2 position,
            Color color,
            Sprite icon)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(520f, 120f);
            var image = buttonObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = true;
            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;

            if (icon != null)
            {
                var iconObject = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                iconObject.transform.SetParent(buttonObject.transform, false);
                var iconRect = iconObject.GetComponent<RectTransform>();
                iconRect.anchorMin = new Vector2(0f, 0.5f);
                iconRect.anchorMax = new Vector2(0f, 0.5f);
                iconRect.pivot = new Vector2(0.5f, 0.5f);
                iconRect.anchoredPosition = new Vector2(70f, 0f);
                iconRect.sizeDelta = new Vector2(72f, 72f);
                var iconImage = iconObject.GetComponent<Image>();
                iconImage.sprite = icon;
                iconImage.preserveAspect = true;
                iconImage.raycastTarget = false;
            }

            CreateLabel(buttonObject.transform, "Label", text, font, 42f, icon != null ? new Vector2(28f, 0f) : Vector2.zero, new Vector2(360f, 80f));
            return button;
        }
    }
}
