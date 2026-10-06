using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FishPuzzle.Presentation
{
    /// <summary>
    /// Shared result-modal pieces. Missing sprites fall back to flat colors so a panel can still open.
    /// </summary>
    public static class ModalChrome
    {
        public static readonly Vector2 CardSize = new Vector2(675f, 900f);

        public static Image CreateDim(Transform parent, string name)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            root.transform.SetParent(parent, false);
            Stretch(root.GetComponent<RectTransform>());
            var image = root.GetComponent<Image>();
            image.color = new Color(0.02f, 0.1f, 0.18f, 0.78f);
            image.raycastTarget = true;
            return image;
        }

        public static RectTransform CreateCard(Transform parent, Sprite panel)
        {
            var cardObject = new GameObject("Card", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            cardObject.transform.SetParent(parent, false);
            var card = cardObject.GetComponent<RectTransform>();
            card.anchorMin = new Vector2(0.5f, 0.5f);
            card.anchorMax = new Vector2(0.5f, 0.5f);
            card.pivot = new Vector2(0.5f, 0.5f);
            card.sizeDelta = CardSize;
            var image = cardObject.GetComponent<Image>();
            image.raycastTarget = true;
            if (panel != null)
            {
                image.sprite = panel;
                image.preserveAspect = true;
                image.color = Color.white;
            }
            else
            {
                image.color = new Color(0.16f, 0.55f, 0.86f, 0.98f);
            }

            return card;
        }

        public static Image CreateSprite(Transform parent, string name, Sprite sprite, Vector2 position, Vector2 size, bool raycast)
        {
            var host = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            host.transform.SetParent(parent, false);
            var rect = host.GetComponent<RectTransform>();
            Place(rect, position, size);
            var image = host.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = raycast;
            image.color = sprite != null ? Color.white : new Color(1f, 1f, 1f, 0f);
            return image;
        }

        public static TextMeshProUGUI CreateLabel(
            Transform parent,
            string name,
            string text,
            TMP_FontAsset font,
            float fontSize,
            Vector2 position,
            Vector2 size,
            Color color)
        {
            var labelObject = new GameObject(name, typeof(RectTransform));
            labelObject.SetActive(false);
            labelObject.transform.SetParent(parent, false);
            Place(labelObject.GetComponent<RectTransform>(), position, size);
            var label = labelObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.text = text;
            label.fontSize = fontSize;
            label.alignment = TextAlignmentOptions.Center;
            label.color = color;
            label.raycastTarget = false;
            labelObject.SetActive(true);
            return label;
        }

        public static Button CreateButton(
            Transform parent,
            string name,
            string text,
            TMP_FontAsset font,
            Vector2 position,
            Vector2 size,
            Sprite face,
            Color fallback,
            Color textColor,
            Sprite icon)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            Place(buttonObject.GetComponent<RectTransform>(), position, size);
            var image = buttonObject.GetComponent<Image>();
            image.raycastTarget = true;
            if (face != null)
            {
                image.sprite = face;
                image.preserveAspect = true;
                image.color = Color.white;
            }
            else
            {
                image.color = fallback;
            }

            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            if (icon != null)
            {
                var iconImage = CreateSprite(buttonObject.transform, "Icon", icon, new Vector2(-size.x * 0.32f, 0f), new Vector2(72f, 72f), false);
                iconImage.raycastTarget = false;
            }

            CreateLabel(
                buttonObject.transform,
                "Label",
                text,
                font,
                40f,
                icon != null ? new Vector2(36f, 0f) : Vector2.zero,
                new Vector2(icon != null ? size.x * 0.62f : size.x * 0.8f, 80f),
                textColor);
            return button;
        }

        public static void Fit(RectTransform card)
        {
            if (card == null)
            {
                return;
            }

            var parent = card.parent as RectTransform;
            if (parent == null)
            {
                return;
            }

            Canvas.ForceUpdateCanvases();
            var width = parent.rect.width;
            var height = parent.rect.height;
            if (width < 1f || height < 1f)
            {
                card.localScale = Vector3.one;
                return;
            }

            const float margin = 48f;
            var scale = 1f;
            if (card.sizeDelta.x + margin > width)
            {
                scale = Mathf.Min(scale, (width - margin) / card.sizeDelta.x);
            }

            if (card.sizeDelta.y + margin > height)
            {
                scale = Mathf.Min(scale, (height - margin) / card.sizeDelta.y);
            }

            card.localScale = new Vector3(Mathf.Max(0.55f, scale), Mathf.Max(0.55f, scale), 1f);
        }

        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
        }
    }
}
