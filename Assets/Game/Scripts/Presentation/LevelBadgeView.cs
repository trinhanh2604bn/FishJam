using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FishPuzzle.Presentation
{
    /// <summary>
    /// Top-left HUD level badge: navy pill, fish icon on the left, "Màn N" centered in the remaining space.
    /// Restyles the scene's existing LevelPanel/LevelLabel at runtime, so GameplaySceneReferences keep working.
    /// </summary>
    public sealed class LevelBadgeView : MonoBehaviour
    {
        public static readonly Vector2 BadgeSize = new Vector2(292f, 92f);
        private const float IconSize = 80f;

        [SerializeField] private TextMeshProUGUI _label;
        [SerializeField] private Image _icon;

        public string Text => _label != null ? _label.text : string.Empty;

        public Image Icon => _icon;

        public static LevelBadgeView Apply(TextMeshProUGUI label, GameplayArtCatalog art)
        {
            if (label == null)
            {
                return null;
            }

            var panel = label.transform.parent as RectTransform;
            if (panel == null)
            {
                return null;
            }

            var badge = panel.GetComponent<LevelBadgeView>();
            if (badge != null)
            {
                return badge;
            }

            badge = panel.gameObject.AddComponent<LevelBadgeView>();
            badge._label = label;

            // Keep the badge in the HUD row: same left margin, vertically centered on the coin/heart row.
            var centerY = panel.anchoredPosition.y + (panel.sizeDelta.y * (0.5f - panel.pivot.y));
            panel.pivot = new Vector2(0f, 0.5f);
            panel.sizeDelta = BadgeSize;
            panel.anchoredPosition = new Vector2(panel.anchoredPosition.x, centerY);

            var background = panel.GetComponent<Image>();
            if (background != null)
            {
                if (art != null && art.LevelBadge != null)
                {
                    background.sprite = art.LevelBadge;
                    background.color = Color.white;
                }
                else
                {
                    background.sprite = null;
                    background.color = new Color(0.08f, 0.16f, 0.42f, 0.95f);
                }

                background.type = Image.Type.Simple;
                background.preserveAspect = false;
                background.raycastTarget = false;
            }

            var iconObject = new GameObject("LevelFishIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconObject.transform.SetParent(panel, false);
            var iconRect = iconObject.GetComponent<RectTransform>();
            ResultUiStyle.Anchor(iconRect, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(46f, 2f), new Vector2(IconSize, IconSize));
            badge._icon = iconObject.GetComponent<Image>();
            badge._icon.sprite = art != null ? (art.LevelBadgeFishIcon != null ? art.LevelBadgeFishIcon : null) : null;
            badge._icon.preserveAspect = true;
            badge._icon.raycastTarget = false;
            badge._icon.enabled = badge._icon.sprite != null;

            var labelRect = label.rectTransform;
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.pivot = new Vector2(0.5f, 0.5f);
            labelRect.offsetMin = new Vector2(badge._icon.enabled ? 86f : 18f, 6f);
            labelRect.offsetMax = new Vector2(-16f, -6f);
            label.alignment = TextAlignmentOptions.Center;
            label.enableAutoSizing = true;
            label.fontSizeMin = 28f;
            label.fontSizeMax = 46f;
            ResultUiStyle.Chunky(label, 46f, Color.white, ResultUiStyle.DeepNavy, 0.2f, false);
            label.enableAutoSizing = true;
            return badge;
        }

        public void SetLevel(int levelNumber)
        {
            if (_label != null)
            {
                _label.text = LevelPresentationFormatting.FormatLevelBadge(levelNumber);
            }
        }
    }
}
