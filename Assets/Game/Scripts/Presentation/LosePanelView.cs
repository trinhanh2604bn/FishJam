using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FishPuzzle.Presentation
{
    /// <summary>
    /// Level-fail notice. Retry asks the flow to rebuild the attempt.
    /// Close collapses the notice into a small Retry button so the lost board stays visible without a soft-lock.
    /// This view does not deduct lives.
    /// </summary>
    public sealed class LosePanelView : MonoBehaviour
    {
        public const string RetryText = "RETRY";
        public const string CloseText = "CLOSE";

        [SerializeField] private TextMeshProUGUI _label;
        [SerializeField] private TextMeshProUGUI _lifeCost;
        [SerializeField] private Button _retryButton;
        [SerializeField] private Button _closeButton;
        [SerializeField] private Button _collapsedRetryButton;
        [SerializeField] private RectTransform _card;

        private Action _onRetry;
        private bool _retrySent;

        public bool IsShown => gameObject.activeSelf;

        public bool IsCollapsed => _collapsedRetryButton != null && _collapsedRetryButton.gameObject.activeSelf;

        /// <summary>Title text. The label renders in upper case.</summary>
        public string Message => _label != null ? _label.text : string.Empty;

        public string LifeCostText => _lifeCost != null ? _lifeCost.text : string.Empty;

        public Button RetryButton => _retryButton;

        public Button CloseButton => _closeButton;

        public Button CollapsedRetryButton => _collapsedRetryButton;

        public static LosePanelView Create(Transform parent, TMP_FontAsset font, Action onRetry, GameplayArtCatalog art = null, int lifeCost = 1)
        {
            var dim = ModalChrome.CreateDim(parent, "LosePanel");
            var card = ModalChrome.CreateCard(dim.transform, art != null ? art.ModalPanel : null);
            ModalChrome.CreateSprite(card, "Header", art != null ? art.ModalHeader : null, new Vector2(0f, 300f), new Vector2(520f, 144f), false);
            var title = ModalChrome.CreateLabel(card, "LoseLabel", "Lose", font, 68f, new Vector2(0f, 300f), new Vector2(420f, 90f), Color.white);
            title.fontStyle |= FontStyles.UpperCase;
            if (art != null && art.FailFlash != null)
            {
                var flash = ModalChrome.CreateSprite(card, "FailFlash", art.FailFlash, new Vector2(0f, 90f), new Vector2(210f, 210f), false);
                var flashColor = flash.color;
                flashColor.a = 0.55f;
                flash.color = flashColor;
            }

            var hasHeart = art != null && art.BrokenHeartIcon != null;
            if (hasHeart)
            {
                ModalChrome.CreateSprite(card, "BrokenHeart", art.BrokenHeartIcon, new Vector2(-120f, 90f), new Vector2(150f, 150f), false);
            }

            var cost = ModalChrome.CreateLabel(
                card,
                "LifeCost",
                FormatCost(lifeCost),
                font,
                60f,
                hasHeart ? new Vector2(80f, 90f) : new Vector2(0f, 90f),
                new Vector2(300f, 100f),
                new Color(0.55f, 0.08f, 0.1f, 1f));
            var retry = ModalChrome.CreateButton(
                card,
                "RetryButton",
                RetryText,
                font,
                new Vector2(0f, -170f),
                new Vector2(430f, 140f),
                art != null ? art.ButtonGreenLarge : null,
                new Color(0.2f, 0.75f, 0.28f, 1f),
                new Color(0.05f, 0.16f, 0.28f, 1f),
                null);
            var close = ModalChrome.CreateButton(
                card,
                "CloseButton",
                CloseText,
                font,
                new Vector2(0f, -320f),
                new Vector2(380f, 112f),
                art != null ? art.ButtonBlueLarge : null,
                new Color(0.2f, 0.52f, 0.9f, 1f),
                new Color(0.05f, 0.16f, 0.28f, 1f),
                null);

            var collapsed = ModalChrome.CreateButton(
                parent,
                "LoseCollapsedRetryButton",
                RetryText,
                font,
                Vector2.zero,
                new Vector2(340f, 116f),
                art != null ? art.ButtonGreenLarge : null,
                new Color(0.2f, 0.75f, 0.28f, 1f),
                new Color(0.05f, 0.16f, 0.28f, 1f),
                null);
            var collapsedRect = collapsed.transform as RectTransform;
            collapsedRect.anchorMin = new Vector2(0.5f, 0f);
            collapsedRect.anchorMax = new Vector2(0.5f, 0f);
            collapsedRect.pivot = new Vector2(0.5f, 0f);
            collapsedRect.anchoredPosition = new Vector2(0f, 140f);
            collapsed.gameObject.SetActive(false);

            var view = dim.gameObject.AddComponent<LosePanelView>();
            view._label = title;
            view._lifeCost = cost;
            view._retryButton = retry;
            view._closeButton = close;
            view._collapsedRetryButton = collapsed;
            view._card = card;
            view._onRetry = onRetry;
            retry.onClick.AddListener(view.HandleRetry);
            close.onClick.AddListener(view.HandleClose);
            collapsed.onClick.AddListener(view.HandleRetry);
            view.Show(lifeCost);
            return view;
        }

        public void Show(int lifeCost)
        {
            if (_lifeCost != null)
            {
                _lifeCost.text = FormatCost(lifeCost);
            }

            Show();
        }

        public void Show()
        {
            _retrySent = false;
            SetCollapsed(false);
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            ModalChrome.Fit(_card);
        }

        public void Hide()
        {
            SetCollapsed(false);
            gameObject.SetActive(false);
        }

        private void HandleRetry()
        {
            if (_retrySent || (!IsShown && !IsCollapsed))
            {
                return;
            }

            _retrySent = true;
            _onRetry?.Invoke();
        }

        private void HandleClose()
        {
            if (!IsShown)
            {
                return;
            }

            gameObject.SetActive(false);
            SetCollapsed(true);
        }

        private void SetCollapsed(bool collapsed)
        {
            if (_collapsedRetryButton == null)
            {
                return;
            }

            _collapsedRetryButton.gameObject.SetActive(collapsed);
            if (collapsed)
            {
                _collapsedRetryButton.transform.SetAsLastSibling();
            }
        }

        public static string FormatCost(int lifeCost)
        {
            return "-" + lifeCost + " Heart";
        }

        private void OnDestroy()
        {
            if (_collapsedRetryButton != null)
            {
                SceneObjectCleanup.DestroyObject(_collapsedRetryButton.gameObject);
            }
        }
    }
}
