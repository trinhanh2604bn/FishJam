using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FishPuzzle.Presentation
{
    /// <summary>
    /// Minimal full-screen lose notice. Retry asks the flow to rebuild the attempt.
    /// This view does not deduct lives.
    /// </summary>
    public sealed class LosePanelView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _label;
        [SerializeField] private Button _retryButton;

        private Action _onRetry;

        public bool IsShown => gameObject.activeSelf;

        public string Message => _label != null ? _label.text : string.Empty;

        public Button RetryButton => _retryButton;

        public static LosePanelView Create(Transform parent, TMP_FontAsset font, Action onRetry)
        {
            var root = new GameObject("LosePanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            root.transform.SetParent(parent, false);
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = root.GetComponent<Image>();
            image.color = new Color(0.04f, 0.07f, 0.12f, 0.88f);
            image.raycastTarget = true;

            var labelObject = new GameObject("LoseLabel", typeof(RectTransform));
            labelObject.SetActive(false);
            labelObject.transform.SetParent(root.transform, false);
            var labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0.5f, 0.5f);
            labelRect.anchorMax = new Vector2(0.5f, 0.5f);
            labelRect.pivot = new Vector2(0.5f, 0.5f);
            labelRect.anchoredPosition = new Vector2(0f, 80f);
            labelRect.sizeDelta = new Vector2(640f, 180f);
            var label = labelObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.text = "Lose";
            label.fontSize = 84f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false;
            labelObject.SetActive(true);

            var buttonObject = new GameObject("RetryButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(root.transform, false);
            var buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
            buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
            buttonRect.pivot = new Vector2(0.5f, 0.5f);
            buttonRect.anchoredPosition = new Vector2(0f, -120f);
            buttonRect.sizeDelta = new Vector2(420f, 120f);
            var buttonImage = buttonObject.GetComponent<Image>();
            buttonImage.color = new Color(0.95f, 0.45f, 0.38f, 1f);
            buttonImage.raycastTarget = true;
            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = buttonImage;

            var retryLabelObject = new GameObject("Label", typeof(RectTransform));
            retryLabelObject.SetActive(false);
            retryLabelObject.transform.SetParent(buttonObject.transform, false);
            var retryLabelRect = retryLabelObject.GetComponent<RectTransform>();
            retryLabelRect.anchorMin = Vector2.zero;
            retryLabelRect.anchorMax = Vector2.one;
            retryLabelRect.offsetMin = Vector2.zero;
            retryLabelRect.offsetMax = Vector2.zero;
            var retryLabel = retryLabelObject.AddComponent<TextMeshProUGUI>();
            retryLabel.font = font;
            retryLabel.text = "Retry";
            retryLabel.fontSize = 48f;
            retryLabel.alignment = TextAlignmentOptions.Center;
            retryLabel.color = Color.white;
            retryLabel.raycastTarget = false;
            retryLabelObject.SetActive(true);

            var view = root.AddComponent<LosePanelView>();
            view._label = label;
            view._retryButton = button;
            view._onRetry = onRetry;
            button.onClick.AddListener(view.HandleRetry);
            view.Show();
            return view;
        }

        public void Show()
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void HandleRetry()
        {
            _onRetry?.Invoke();
        }
    }
}
