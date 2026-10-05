using FishPuzzle.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FishPuzzle.Presentation
{
    /// <summary>
    /// Hanging target badge. It displays the tank target and fill supplied by the level attempt.
    /// </summary>
    public sealed class TargetBadgeView : MonoBehaviour
    {
        [SerializeField] private Image _fishIcon;
        [SerializeField] private TextMeshProUGUI _progressText;

        public FishType PreviewFishType { get; private set; }

        public string PreviewProgressText => _progressText != null ? _progressText.text : string.Empty;

        public void ShowPreviewTarget(FishType fishType, int tankCapacity, FishVisualCatalog catalog)
        {
            ShowTarget(fishType, 0, tankCapacity, catalog);
        }

        public void ShowTarget(FishType fishType, int fillCount, int tankCapacity, FishVisualCatalog catalog)
        {
            PreviewFishType = fishType;
            if (_progressText != null)
            {
                _progressText.text = LevelPresentationFormatting.FormatProgress(fillCount, tankCapacity);
            }

            if (_fishIcon == null)
            {
                return;
            }

            if (catalog != null && catalog.TryGetSprite(fishType, out var sprite))
            {
                _fishIcon.sprite = sprite;
                _fishIcon.enabled = sprite != null;
                return;
            }

            _fishIcon.sprite = null;
            _fishIcon.enabled = false;
        }

        public void ShowInactive()
        {
            if (_progressText != null)
            {
                _progressText.text = string.Empty;
            }

            if (_fishIcon != null)
            {
                _fishIcon.sprite = null;
                _fishIcon.enabled = false;
            }
        }
    }
}
