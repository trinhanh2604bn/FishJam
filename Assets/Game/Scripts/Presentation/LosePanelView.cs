using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FishPuzzle.Presentation
{
    /// <summary>
    /// Level-fail modal: "Màn N" header, "Thua Màn!", broken heart with "-1", "Thử Lại", close button.
    /// Retry asks the flow to rebuild the same level. Close collapses the modal into a small
    /// "Thử Lại" button so the lost board stays visible without a soft-lock.
    /// This view never deducts lives; it only displays the cost that the flow already applied.
    /// </summary>
    public sealed class LosePanelView : MonoBehaviour
    {
        public const string TitleText = "Thua Màn!";
        public const string RetryText = "Thử Lại";
        public const string CloseText = "Đóng";

        private static readonly Vector2 LoseCardSize = new Vector2(780f, 1000f);

        [SerializeField] private TextMeshProUGUI _label;
        [SerializeField] private TextMeshProUGUI _header;
        [SerializeField] private TextMeshProUGUI _lifeCost;
        [SerializeField] private Button _retryButton;
        [SerializeField] private Button _closeButton;
        [SerializeField] private Button _collapsedRetryButton;
        [SerializeField] private RectTransform _card;

        private Action _onRetry;
        private ModalPopIn _pop;
        private bool _retrySent;

        public bool IsShown => gameObject.activeSelf;

        public bool IsCollapsed => _collapsedRetryButton != null && _collapsedRetryButton.gameObject.activeSelf;

        /// <summary>Main lose title, "Thua Màn!".</summary>
        public string Message => _label != null ? _label.text : string.Empty;

        /// <summary>Header ribbon text, e.g. "Màn 5".</summary>
        public string HeaderText => _header != null ? _header.text : string.Empty;

        public string LifeCostText => _lifeCost != null ? _lifeCost.text : string.Empty;

        public Button RetryButton => _retryButton;

        public Button CloseButton => _closeButton;

        public Button CollapsedRetryButton => _collapsedRetryButton;

        public RectTransform Card => _card;

        public static LosePanelView Create(Transform parent, TMP_FontAsset font, Action onRetry, GameplayArtCatalog art = null, int lifeCost = 1, int levelNumber = 1)
        {
            var dim = ModalChrome.CreateDim(parent, "LosePanel");
            dim.color = new Color(0.01f, 0.04f, 0.1f, 0.8f);
            var card = ModalChrome.CreateCard(dim.transform, art != null ? art.ModalPanel : null);
            card.sizeDelta = LoseCardSize;

            var header = ModalChrome.CreateSprite(card, "Header", art != null ? art.ModalHeader : null, new Vector2(0f, 468f), new Vector2(600f, 166f), false);
            if (art == null || art.ModalHeader == null)
            {
                header.color = new Color(0.1f, 0.45f, 0.9f, 1f);
            }

            var headerLabel = ResultUiStyle.Label(
                card,
                "HeaderLabel",
                LevelPresentationFormatting.FormatLevelBadge(levelNumber),
                font,
                new Vector2(0f, 478f),
                new Vector2(480f, 100f),
                70f,
                Color.white,
                ResultUiStyle.DeepNavy);

            var close = ModalChrome.CreateButton(
                card,
                "CloseButton",
                string.Empty,
                font,
                new Vector2(352f, 452f),
                new Vector2(132f, 132f),
                art != null ? art.ButtonClose : null,
                new Color(0.86f, 0.16f, 0.16f, 1f),
                Color.white,
                null);
            var closeIcon = ModalChrome.CreateSprite(close.transform, "CloseIcon", art != null ? art.CloseIcon : null, Vector2.zero, new Vector2(66f, 66f), false);
            if (art == null || art.CloseIcon == null)
            {
                var x = ModalChrome.CreateLabel(close.transform, "CloseX", "X", font, 64f, Vector2.zero, new Vector2(100f, 100f), Color.white);
                ResultUiStyle.Chunky(x, 64f, Color.white, ResultUiStyle.HeartRed);
                closeIcon.enabled = false;
            }

            var inner = ModalChrome.CreateSprite(card, "InnerPanel", art != null ? art.ModalInner : null, new Vector2(0f, 70f), new Vector2(640f, 520f), false);
            inner.preserveAspect = false;
            if (art == null || art.ModalInner == null)
            {
                inner.color = new Color(0.98f, 0.92f, 0.86f, 1f);
            }

            var title = ResultUiStyle.Label(card, "LoseLabel", TitleText, font, new Vector2(0f, 262f), new Vector2(580f, 100f), 74f, ResultUiStyle.Navy, new Color(1f, 1f, 1f, 0.6f));
            title.outlineWidth = 0.06f;

            if (art != null && art.FailFlash != null)
            {
                var flash = ModalChrome.CreateSprite(card, "FailFlash", art.FailFlash, new Vector2(0f, 40f), new Vector2(360f, 360f), false);
                var flashColor = flash.color;
                flashColor.a = 0.35f;
                flash.color = flashColor;
            }

            var heart = ModalChrome.CreateSprite(card, "BrokenHeart", art != null ? art.BrokenHeartIcon : null, new Vector2(0f, 36f), new Vector2(330f, 330f), false);
            if (art == null || art.BrokenHeartIcon == null)
            {
                heart.color = new Color(0.86f, 0.1f, 0.14f, 1f);
            }

            var cost = ResultUiStyle.Label(card, "LifeCost", FormatCost(lifeCost), font, new Vector2(118f, -62f), new Vector2(220f, 150f), 126f, Color.white, ResultUiStyle.HeartRed);
            cost.outlineWidth = 0.28f;

            var retry = ModalChrome.CreateButton(
                card,
                "RetryButton",
                RetryText,
                font,
                new Vector2(0f, -330f),
                new Vector2(540f, 184f),
                art != null ? art.ButtonGreenLarge : null,
                new Color(0.2f, 0.75f, 0.28f, 1f),
                Color.white,
                null);
            ResultUiStyle.StyleButtonLabel(retry, 76f, ResultUiStyle.ButtonGreenText);

            var collapsed = ModalChrome.CreateButton(
                parent,
                "LoseCollapsedRetryButton",
                RetryText,
                font,
                Vector2.zero,
                new Vector2(360f, 128f),
                art != null ? art.ButtonGreenLarge : null,
                new Color(0.2f, 0.75f, 0.28f, 1f),
                Color.white,
                null);
            ResultUiStyle.StyleButtonLabel(collapsed, 52f, ResultUiStyle.ButtonGreenText);
            var collapsedRect = collapsed.transform as RectTransform;
            collapsedRect.anchorMin = new Vector2(0.5f, 0f);
            collapsedRect.anchorMax = new Vector2(0.5f, 0f);
            collapsedRect.pivot = new Vector2(0.5f, 0f);
            collapsedRect.anchoredPosition = new Vector2(0f, 140f);
            collapsed.gameObject.SetActive(false);

            var view = dim.gameObject.AddComponent<LosePanelView>();
            view._label = title;
            view._header = headerLabel;
            view._lifeCost = cost;
            view._retryButton = retry;
            view._closeButton = close;
            view._collapsedRetryButton = collapsed;
            view._card = card;
            view._onRetry = onRetry;
            view._pop = ModalPopIn.Attach(dim.gameObject, card);
            retry.onClick.AddListener(view.HandleRetry);
            close.onClick.AddListener(view.HandleClose);
            collapsed.onClick.AddListener(view.HandleRetry);
            view.Show(lifeCost, levelNumber);
            return view;
        }

        public void Show(int lifeCost, int levelNumber)
        {
            if (_header != null)
            {
                _header.text = LevelPresentationFormatting.FormatLevelBadge(levelNumber);
            }

            Show(lifeCost);
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

            if (_pop != null)
            {
                _pop.Complete();
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
            return "-" + lifeCost;
        }

        private void OnDestroy()
        {
            if (_collapsedRetryButton != null)
            {
                // Plain Destroy: during scene unload the button is already being destroyed and cannot be reparented.
                if (Application.isPlaying)
                {
                    Destroy(_collapsedRetryButton.gameObject);
                }
                else
                {
                    SceneObjectCleanup.DestroyObject(_collapsedRetryButton.gameObject);
                }
            }
        }
    }
}
