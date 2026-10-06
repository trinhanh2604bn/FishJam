using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FishPuzzle.Presentation
{
    /// <summary>
    /// Shared look for result screens and the HUD level badge: chunky outlined text, sliced panels,
    /// anchored placement. Presentation only.
    /// </summary>
    public static class ResultUiStyle
    {
        public static readonly Color Navy = new Color(0.08f, 0.16f, 0.42f, 1f);
        public static readonly Color DeepNavy = new Color(0.05f, 0.1f, 0.3f, 1f);
        public static readonly Color ButtonGreenText = new Color(0.05f, 0.32f, 0.06f, 1f);
        public static readonly Color ButtonBlueText = new Color(0.06f, 0.18f, 0.5f, 1f);
        public static readonly Color HeartRed = new Color(0.62f, 0.05f, 0.08f, 1f);

        /// <summary>White (or tinted) bold text with a dark outline and a soft drop underlay.</summary>
        public static void Chunky(TMP_Text label, float fontSize, Color face, Color outline, float outlineWidth = 0.22f, bool underlay = true)
        {
            if (label == null)
            {
                return;
            }

            label.fontSize = fontSize;
            label.fontStyle |= FontStyles.Bold;
            label.color = face;
            label.overflowMode = TextOverflowModes.Overflow;
            label.raycastTarget = false;
            if (!Application.isPlaying)
            {
                return;
            }

            // fontMaterial creates a per-label material instance, so outlines never leak into other text.
            label.outlineColor = outline;
            label.outlineWidth = outlineWidth;
            if (underlay)
            {
                var material = label.fontMaterial;
                material.EnableKeyword(ShaderUtilities.Keyword_Underlay);
                material.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(outline.r * 0.6f, outline.g * 0.6f, outline.b * 0.6f, 0.85f));
                material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0f);
                material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.7f);
                material.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.4f);
                material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.15f);
            }
        }

        public static void VerticalGradient(TMP_Text label, Color top, Color bottom)
        {
            if (label == null)
            {
                return;
            }

            label.enableVertexGradient = true;
            label.colorGradient = new VertexGradient(top, top, bottom, bottom);
            label.color = Color.white;
        }

        public static TextMeshProUGUI Label(Transform parent, string name, string text, TMP_FontAsset font, Vector2 position, Vector2 size, float fontSize, Color face, Color outline)
        {
            var label = ModalChrome.CreateLabel(parent, name, text, font, fontSize, position, size, face);
            Chunky(label, fontSize, face, outline);
            return label;
        }

        public static Image Sliced(Transform parent, string name, Sprite sprite, Vector2 position, Vector2 size, Color fallback)
        {
            var image = ModalChrome.CreateSprite(parent, name, sprite, position, size, false);
            if (sprite != null)
            {
                image.preserveAspect = false;
                image.type = sprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
            }
            else
            {
                image.color = fallback;
            }

            return image;
        }

        public static void Anchor(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        /// <summary>Restyles the label that <see cref="ModalChrome.CreateButton"/> adds.</summary>
        public static TextMeshProUGUI StyleButtonLabel(Button button, float fontSize, Color outline)
        {
            if (button == null)
            {
                return null;
            }

            var label = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
            {
                var rect = label.rectTransform;
                rect.sizeDelta = new Vector2(rect.sizeDelta.x, fontSize * 1.4f);
                Chunky(label, fontSize, Color.white, outline, 0.24f);
            }

            return label;
        }
    }
}
