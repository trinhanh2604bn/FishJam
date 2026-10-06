using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FishPuzzle.Presentation
{
    /// <summary>
    /// Soft white outline, glow and slight brightening while a fish is held.
    /// Built once per fish from extra UI Images; the fish sprite itself is never modified.
    /// All highlight Images share one silhouette material and never block raycasts.
    /// </summary>
    public sealed class FishPressHighlight : MonoBehaviour
    {
        public const string SilhouetteShaderResource = "FishPuzzle/UISilhouette";

        private const float OutlineDistance = 5f;
        private const float PulseCycle = 0.7f;
        private const float GlowPadding = 0.32f;

        private static Material _silhouette;
        private static bool _silhouetteResolved;

        private RectTransform _visual;
        private Image _visualImage;
        private RectTransform _glow;
        private Image _glowImage;
        private RectTransform _outline;
        private Image _outlineImage;
        private Image _brightenImage;
        private float _time;
        private bool _built;

        public bool IsShown { get; private set; }

        public bool HasOutline => _outlineImage != null;

        public float OutlineAlpha => IsShown && _outlineImage != null ? _outlineImage.color.a : 0f;

        public float BrightenAlpha => IsShown && _brightenImage != null ? _brightenImage.color.a : 0f;

        /// <summary>Shared silhouette material, created once. Null when the shader is unavailable.</summary>
        public static Material SilhouetteMaterial
        {
            get
            {
                if (_silhouetteResolved)
                {
                    return _silhouette;
                }

                _silhouetteResolved = true;
                var shader = Resources.Load<Shader>(SilhouetteShaderResource);
                if (shader == null || !shader.isSupported)
                {
                    return null;
                }

                _silhouette = new Material(shader)
                {
                    name = "FishSilhouette (shared)",
                    hideFlags = HideFlags.HideAndDontSave,
                };
                return _silhouette;
            }
        }

        public void Show(RectTransform visual)
        {
            if (visual == null)
            {
                return;
            }

            if (!_built || _visual != visual)
            {
                Build(visual);
            }

            _time = 0f;
            IsShown = true;
            SetVisible(true);
            Follow();
            Apply();
            enabled = true;
        }

        public void Hide()
        {
            IsShown = false;
            SetVisible(false);
            enabled = false;
        }

        private void LateUpdate()
        {
            if (!IsShown)
            {
                enabled = false;
                return;
            }

            _time += Time.unscaledDeltaTime;
            Follow();
            Apply();
        }

        private void OnDisable()
        {
            if (IsShown)
            {
                // Disabled with the fish (pooling / scene teardown): leave nothing visible behind.
                IsShown = false;
                SetVisible(false);
            }
        }

        private void Build(RectTransform visual)
        {
            _visual = visual;
            _visualImage = visual.GetComponent<Image>();
            var sprite = _visualImage != null ? _visualImage.sprite : null;
            var parent = visual.parent as RectTransform;
            if (parent == null)
            {
                parent = transform as RectTransform;
            }

            var material = SilhouetteMaterial;
            var index = visual.parent == parent ? visual.GetSiblingIndex() : 0;

            _glowImage = CreateImage("PressGlow", parent, ProceduralVfxSprite.Dot, null, out _glow);
            _glow.SetSiblingIndex(index);
            if (material != null && sprite != null)
            {
                _outlineImage = CreateImage("PressOutline", parent, sprite, material, out _outline);
                _outline.SetSiblingIndex(index + 1);
                _outlineImage.preserveAspect = _visualImage.preserveAspect;
                _outline.gameObject.AddComponent<SilhouetteOutlineEffect>().Distance = OutlineDistance;

                _brightenImage = CreateImage("PressBrighten", visual, sprite, material, out var brighten);
                _brightenImage.preserveAspect = _visualImage.preserveAspect;
                brighten.anchorMin = Vector2.zero;
                brighten.anchorMax = Vector2.one;
                brighten.offsetMin = Vector2.zero;
                brighten.offsetMax = Vector2.zero;
            }

            _built = true;
            SetVisible(false);
        }

        private void Follow()
        {
            if (_visual == null)
            {
                return;
            }

            var sprite = _visualImage != null ? _visualImage.sprite : null;
            if (_outline != null)
            {
                Mirror(_outline, 1f);
                if (_outlineImage.sprite != sprite && sprite != null)
                {
                    _outlineImage.sprite = sprite;
                    _brightenImage.sprite = sprite;
                }
            }

            if (_glow != null)
            {
                Mirror(_glow, 1f + GlowPadding);
            }
        }

        private void Mirror(RectTransform target, float scale)
        {
            target.anchorMin = _visual.anchorMin;
            target.anchorMax = _visual.anchorMax;
            target.pivot = _visual.pivot;
            target.anchoredPosition = _visual.anchoredPosition;
            target.sizeDelta = _visual.sizeDelta;
            target.localRotation = _visual.localRotation;
            target.localScale = _visual.localScale * scale;
        }

        private void Apply()
        {
            var pulse = 0.5f + (0.5f * Mathf.Sin((_time / PulseCycle) * Mathf.PI * 2f));
            SetAlpha(_outlineImage, Mathf.Lerp(0.78f, 1f, pulse));
            SetAlpha(_glowImage, Mathf.Lerp(0.30f, 0.48f, pulse));
            SetAlpha(_brightenImage, Mathf.Lerp(0.12f, 0.2f, pulse));
        }

        private void SetVisible(bool visible)
        {
            if (_glow != null)
            {
                _glow.gameObject.SetActive(visible);
            }

            if (_outline != null)
            {
                _outline.gameObject.SetActive(visible);
            }

            if (_brightenImage != null)
            {
                _brightenImage.gameObject.SetActive(visible);
            }
        }

        private static void SetAlpha(Image image, float alpha)
        {
            if (image == null)
            {
                return;
            }

            var color = image.color;
            color.a = alpha;
            image.color = color;
        }

        private static Image CreateImage(string name, RectTransform parent, Sprite sprite, Material material, out RectTransform rect)
        {
            var host = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            rect = host.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            var image = host.GetComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            image.color = Color.white;
            if (material != null)
            {
                image.material = material;
            }

            return image;
        }
    }

    /// <summary>
    /// Replaces a UI quad with copies offset in eight directions (no centre copy).
    /// Combined with the silhouette material this draws a solid outline behind the sprite.
    /// </summary>
    public sealed class SilhouetteOutlineEffect : BaseMeshEffect
    {
        private static readonly List<UIVertex> Source = new List<UIVertex>();
        private static readonly List<UIVertex> Output = new List<UIVertex>();

        public float Distance = 5f;

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh == null)
            {
                return;
            }

            Source.Clear();
            Output.Clear();
            vh.GetUIVertexStream(Source);
            for (var d = 0; d < 8; d++)
            {
                var angle = d * Mathf.PI * 0.25f;
                var offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * Distance;
                for (var i = 0; i < Source.Count; i++)
                {
                    var vertex = Source[i];
                    vertex.position += offset;
                    Output.Add(vertex);
                }
            }

            vh.Clear();
            vh.AddUIVertexTriangleStream(Output);
        }
    }
}
