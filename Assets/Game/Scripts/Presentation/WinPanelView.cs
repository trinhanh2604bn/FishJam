using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FishPuzzle.Presentation
{
    /// <summary>
    /// Minimal full-screen win notice. It does not award score or change progression.
    /// </summary>
    public sealed class WinPanelView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _label;

        public bool IsShown => gameObject.activeSelf;

        public string Message => _label != null ? _label.text : string.Empty;

        public static WinPanelView Create(Transform parent, TMP_FontAsset font)
        {
            var root = new GameObject("WinPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            root.transform.SetParent(parent, false);
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = root.GetComponent<Image>();
            image.color = new Color(0.05f, 0.16f, 0.12f, 0.9f);
            image.raycastTarget = true;

            var labelObject = new GameObject("WinLabel", typeof(RectTransform));
            labelObject.SetActive(false);
            labelObject.transform.SetParent(root.transform, false);
            var labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0.5f, 0.5f);
            labelRect.anchorMax = new Vector2(0.5f, 0.5f);
            labelRect.pivot = new Vector2(0.5f, 0.5f);
            labelRect.sizeDelta = new Vector2(640f, 180f);
            var label = labelObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.text = "Win";
            label.fontSize = 84f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false;
            labelObject.SetActive(true);

            var view = root.AddComponent<WinPanelView>();
            view._label = label;
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
    }
}
