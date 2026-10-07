using System.Collections;
using System.Collections.Generic;
using FishPuzzle.Core;
using FishPuzzle.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FishPuzzle.Presentation
{
    /// <summary>
    /// Presents one bubble definition. It does not pop, route, or move the bubble.
    /// The bubble shell (BubbleBack, BubbleFront, frost) scales with the authored fish count; fish keep their fixed size.
    /// Frozen Bubbles show FrostOverlay and a large IceCounter above BubbleFront. Neither blocks raycasts.
    /// </summary>
    public sealed class BubbleView : MonoBehaviour
    {
        [SerializeField] private RectTransform _fishContainer;
        [SerializeField] private BubbleFishLayoutController _fishLayout;

        public const float IceCounterFontSize = 132f;
        public const float IceChipSeconds = 0.22f;
        public const float IceBreakSeconds = 0.38f;
        public const float FrozenRejectSeconds = 0.28f;

        private static readonly Color FrostTint = new Color(0.86f, 0.95f, 1f, 0.92f);
        private static readonly Color CounterOutline = new Color(0.1f, 0.33f, 0.62f, 1f);

        private readonly List<FishView> _fishViews = new List<FishView>();
        private Image _frostOverlay;
        private TextMeshProUGUI _iceCounter;
        private Coroutine _iceRoutine;
        private Coroutine _rejectRoutine;
        private int _iceShownValue;

        public string BubbleId { get; private set; }

        /// <summary>Unscaled bubble rect. The bubble_back art fills <see cref="VisibleDiameterFraction"/> of it.</summary>
        public const float BaseSize = 300f;

        /// <summary>Opaque diameter of bubble_back.png relative to the rect (432 of 512 px).</summary>
        public const float VisibleDiameterFraction = 0.85f;

        /// <summary>Shell scale from <see cref="VisualScaleForFishCount"/>. It never changes fish size.</summary>
        public float VisualScale { get; private set; } = 1f;

        /// <summary>Whole-bubble scale chosen by the pile so bubbles pack like marbles. Applies to shell and fish alike.</summary>
        public float ContentScale { get; private set; } = 1f;

        /// <summary>Radius of the visible bubble in pile-field units.</summary>
        public float VisibleRadius => BaseSize * 0.5f * VisibleDiameterFraction * VisualScale * ContentScale;

        public bool IsIceShown => _frostOverlay != null && _frostOverlay.gameObject.activeSelf;

        /// <summary>Counter value currently displayed; 0 when no ice is shown.</summary>
        public int IceCounterValue => IsIceShown ? _iceShownValue : 0;

        public Image FrostOverlay => _frostOverlay;

        public TextMeshProUGUI IceCounter => _iceCounter;

        public int IceBreakPlayCount { get; private set; }

        public int FrozenRejectPlayCount { get; private set; }

        /// <summary>
        /// Relative shell scale by contained fish: 2 = 0.88, 3 = 0.93, 4 = 0.98, 5 = 1.03.
        /// Kept close to 1 so packed bubbles still touch like marbles.
        /// Counts outside 2..5 clamp to the nearest entry.
        /// </summary>
        public static float VisualScaleForFishCount(int fishCount)
        {
            if (fishCount <= 2)
            {
                return 0.88f;
            }

            if (fishCount == 3)
            {
                return 0.93f;
            }

            if (fishCount == 4)
            {
                return 0.98f;
            }

            return 1.03f;
        }

        public int SlotId { get; private set; }

        public void SetSlot(int slotId)
        {
            SlotId = slotId;
        }

        public BubbleDefinition SourceDefinition { get; private set; }

        public IReadOnlyList<FishView> FishViews => _fishViews;

        public void Bind(BubbleDefinition definition, int slotId, FishVisualCatalog catalog, GameObject fishPrefab)
        {
            AllowFishRaycasts();
            ClearFish();
            SourceDefinition = definition;
            SlotId = slotId;
            BubbleId = definition != null ? definition.BubbleId : string.Empty;
            ApplyVisualScale(definition != null && definition.Fishes != null ? definition.Fishes.Count : 3);
            HideIce();
            if (definition == null || definition.Fishes == null || _fishContainer == null || fishPrefab == null)
            {
                GameLog.Error(nameof(BubbleView), "Bubble " + BubbleId + " could not bind fish. Definition, container, or fish prefab is missing.");
                return;
            }

            if (catalog == null)
            {
                GameLog.Error(nameof(BubbleView), "Bubble " + BubbleId + " has no FishVisualCatalog.");
                return;
            }

            var rects = new List<RectTransform>(definition.Fishes.Count);
            for (var i = 0; i < definition.Fishes.Count; i++)
            {
                var fishObject = Instantiate(fishPrefab, _fishContainer);
                fishObject.name = "Fish_" + i;
                var rect = fishObject.GetComponent<RectTransform>();
                rects.Add(rect);
                var view = fishObject.GetComponent<FishView>();
                if (view == null)
                {
                    GameLog.Error(nameof(BubbleView), "PF_Fish is missing FishView.");
                    continue;
                }

                view.BindCatalog(catalog);
                view.Show(definition.Fishes[i]);
                _fishViews.Add(view);
            }

            if (_fishLayout != null)
            {
                _fishLayout.Apply(rects);
            }
        }

        public void ReleaseFish(FishView fish)
        {
            if (fish == null)
            {
                return;
            }

            _fishViews.Remove(fish);
        }

        public void DisableInput()
        {
            var images = GetComponentsInChildren<Image>(true);
            for (var i = 0; i < images.Length; i++)
            {
                if (images[i] != null)
                {
                    images[i].raycastTarget = false;
                }
            }

            for (var i = 0; i < _fishViews.Count; i++)
            {
                if (_fishViews[i] != null)
                {
                    _fishViews[i].ReleaseInteraction();
                }
            }
        }

        public void SnapFishLayout()
        {
            ApplyLayout();
        }

        /// <summary>Snaps the frozen visuals to <paramref name="remaining"/>. 0 hides frost and counter.</summary>
        public void ShowIce(int remaining, Sprite frost, TMP_FontAsset font)
        {
            StopIceRoutines();
            if (remaining <= 0)
            {
                HideIce();
                return;
            }

            EnsureIceVisuals(frost, font);
            _iceShownValue = remaining;
            _frostOverlay.gameObject.SetActive(true);
            _frostOverlay.color = FrostTint;
            _frostOverlay.rectTransform.localScale = Vector3.one;
            if (_iceCounter != null)
            {
                _iceCounter.gameObject.SetActive(true);
                _iceCounter.text = remaining.ToString();
                _iceCounter.alpha = 1f;
                _iceCounter.rectTransform.localScale = Vector3.one;
            }
        }

        /// <summary>Counter decrement: new value with a small scale punch and frost flash.</summary>
        public void PlayIceChip(int remaining)
        {
            if (!IsIceShown)
            {
                return;
            }

            if (remaining <= 0)
            {
                PlayIceBreak(null);
                return;
            }

            StopIceRoutines();
            _iceShownValue = remaining;
            if (_iceCounter != null)
            {
                _iceCounter.text = remaining.ToString();
            }

            _iceRoutine = isActiveAndEnabled ? StartCoroutine(IceChipRoutine()) : null;
        }

        /// <summary>Frost cracks and fades, sparkles burst, and the bubble stays as a normal bubble.</summary>
        public void PlayIceBreak(Sprite sparkle)
        {
            if (!IsIceShown)
            {
                return;
            }

            StopIceRoutines();
            IceBreakPlayCount++;
            _iceShownValue = 0;
            if (_iceCounter != null)
            {
                _iceCounter.text = "0";
            }

            if (!isActiveAndEnabled)
            {
                HideIce();
                return;
            }

            _iceRoutine = StartCoroutine(IceBreakRoutine(sparkle));
        }

        /// <summary>Tap on a frozen fish: a short wobble. Nothing is selected.</summary>
        public void PlayFrozenReject()
        {
            FrozenRejectPlayCount++;
            if (!isActiveAndEnabled)
            {
                return;
            }

            if (_rejectRoutine != null)
            {
                StopCoroutine(_rejectRoutine);
            }

            _rejectRoutine = StartCoroutine(RejectRoutine());
        }

        /// <summary>Pile packing scale for the whole bubble (shell and fish). 1 outside a sized pile field.</summary>
        public void SetContentScale(float contentScale)
        {
            ContentScale = contentScale > 0.01f ? contentScale : 1f;
            ApplyScales();
        }

        private void ApplyVisualScale(int fishCount)
        {
            VisualScale = VisualScaleForFishCount(fishCount);
            ApplyScales();
        }

        private void ApplyScales()
        {
            var shell = Vector3.one * (VisualScale * ContentScale);
            ScaleChild("BubbleBack", shell);
            ScaleChild("BubbleFront", shell);
            ScaleChild("ModifierContainer", shell);
            var content = Vector3.one * ContentScale;
            ScaleChild("FishContainer", content);
            ScaleChild("InteractionArea", content);
        }

        private void ScaleChild(string childName, Vector3 scale)
        {
            var child = transform.Find(childName);
            if (child != null)
            {
                child.localScale = scale;
            }
        }

        private void EnsureIceVisuals(Sprite frost, TMP_FontAsset font)
        {
            var host = transform.Find("ModifierContainer") as RectTransform;
            if (host == null)
            {
                host = new GameObject("ModifierContainer", typeof(RectTransform)).GetComponent<RectTransform>();
                host.SetParent(transform, false);
                Stretch(host);
                host.localScale = Vector3.one * (VisualScale * ContentScale);
            }

            // BubbleBack -> Fish -> BubbleFront -> FrostOverlay -> IceCounter.
            var front = transform.Find("BubbleFront");
            if (front != null && host.GetSiblingIndex() < front.GetSiblingIndex())
            {
                host.SetSiblingIndex(front.GetSiblingIndex());
            }

            if (_frostOverlay == null)
            {
                var overlay = new GameObject("FrostOverlay", typeof(RectTransform), typeof(Image));
                overlay.transform.SetParent(host, false);
                _frostOverlay = overlay.GetComponent<Image>();
                _frostOverlay.raycastTarget = false;
                _frostOverlay.preserveAspect = true;
                Stretch(_frostOverlay.rectTransform);
                // The frost art fills ~97% of its texture, the bubble ~85%: inset so the frost hugs the bubble.
                var inset = BaseSize * 0.06f;
                _frostOverlay.rectTransform.offsetMin = new Vector2(inset, inset);
                _frostOverlay.rectTransform.offsetMax = new Vector2(-inset, -inset);
            }

            if (frost != null)
            {
                _frostOverlay.sprite = frost;
            }

            if (_iceCounter == null)
            {
                var counter = new GameObject("IceCounter", typeof(RectTransform));
                counter.transform.SetParent(host, false);
                var rect = counter.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(220f, 180f);
                rect.anchoredPosition = Vector2.zero;
                _iceCounter = counter.AddComponent<TextMeshProUGUI>();
                if (font != null)
                {
                    _iceCounter.font = font;
                }

                _iceCounter.alignment = TextAlignmentOptions.Center;
                _iceCounter.textWrappingMode = TextWrappingModes.NoWrap;
                ResultUiStyle.Chunky(_iceCounter, IceCounterFontSize, Color.white, CounterOutline, 0.26f);
                _iceCounter.raycastTarget = false;
            }

            _frostOverlay.transform.SetAsLastSibling();
            _iceCounter.transform.SetAsLastSibling();
        }

        private void HideIce()
        {
            _iceShownValue = 0;
            if (_frostOverlay != null)
            {
                _frostOverlay.gameObject.SetActive(false);
            }

            if (_iceCounter != null)
            {
                _iceCounter.gameObject.SetActive(false);
            }

            transform.localRotation = Quaternion.identity;
        }

        private void StopIceRoutines()
        {
            if (_iceRoutine != null)
            {
                StopCoroutine(_iceRoutine);
                _iceRoutine = null;
            }

            if (_rejectRoutine != null)
            {
                StopCoroutine(_rejectRoutine);
                _rejectRoutine = null;
                transform.localRotation = Quaternion.identity;
            }
        }

        private IEnumerator IceChipRoutine()
        {
            var elapsed = 0f;
            while (elapsed < IceChipSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / IceChipSeconds);
                var punch = 1f + (0.35f * Mathf.Sin(t * Mathf.PI));
                if (_iceCounter != null)
                {
                    _iceCounter.rectTransform.localScale = Vector3.one * punch;
                }

                if (_frostOverlay != null)
                {
                    _frostOverlay.color = Color.Lerp(Color.white, FrostTint, t);
                }

                yield return null;
            }

            if (_iceCounter != null)
            {
                _iceCounter.rectTransform.localScale = Vector3.one;
            }

            if (_frostOverlay != null)
            {
                _frostOverlay.color = FrostTint;
            }

            _iceRoutine = null;
        }

        private IEnumerator IceBreakRoutine(Sprite sparkle)
        {
            SpawnIceSparkles(sparkle);
            var elapsed = 0f;
            while (elapsed < IceBreakSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / IceBreakSeconds);
                var crack = t < 0.25f ? Mathf.Sin(t * 4f * Mathf.PI * 3f) * (1f - (t * 4f)) * 4f : 0f;
                if (_frostOverlay != null)
                {
                    var color = FrostTint;
                    color.a = FrostTint.a * (1f - PresentationMotion.EaseOutQuad(t));
                    _frostOverlay.color = color;
                    _frostOverlay.rectTransform.localScale = Vector3.one * (1f + (0.18f * t));
                    _frostOverlay.rectTransform.localRotation = Quaternion.Euler(0f, 0f, crack);
                }

                if (_iceCounter != null)
                {
                    _iceCounter.alpha = 1f - t;
                    _iceCounter.rectTransform.localScale = Vector3.one * (1f + (0.5f * t));
                }

                yield return null;
            }

            if (_frostOverlay != null)
            {
                _frostOverlay.rectTransform.localRotation = Quaternion.identity;
            }

            _iceRoutine = null;
            HideIce();
        }

        private IEnumerator RejectRoutine()
        {
            var elapsed = 0f;
            while (elapsed < FrozenRejectSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / FrozenRejectSeconds);
                transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * Mathf.PI * 6f) * 5f * (1f - t));
                yield return null;
            }

            transform.localRotation = Quaternion.identity;
            _rejectRoutine = null;
        }

        private void SpawnIceSparkles(Sprite sparkle)
        {
            if (sparkle == null)
            {
                return;
            }

            const int count = 8;
            for (var i = 0; i < count; i++)
            {
                var shard = new GameObject("IceSparkle", typeof(RectTransform), typeof(Image));
                shard.transform.SetParent(transform, false);
                var image = shard.GetComponent<Image>();
                image.sprite = sparkle;
                image.raycastTarget = false;
                image.color = new Color(0.9f, 0.97f, 1f, 1f);
                var rect = image.rectTransform;
                rect.sizeDelta = new Vector2(34f, 34f);
                var angle = (i / (float)count) * Mathf.PI * 2f;
                StartCoroutine(SparkleRoutine(rect, image, new Vector2(Mathf.Cos(angle), Mathf.Sin(angle))));
            }
        }

        private IEnumerator SparkleRoutine(RectTransform rect, Image image, Vector2 direction)
        {
            var elapsed = 0f;
            while (elapsed < IceBreakSeconds && rect != null)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / IceBreakSeconds);
                rect.anchoredPosition = direction * Mathf.Lerp(60f, 170f, PresentationMotion.EaseOutQuad(t));
                rect.localScale = Vector3.one * Mathf.Lerp(1.1f, 0.4f, t);
                image.color = new Color(0.9f, 0.97f, 1f, 1f - t);
                yield return null;
            }

            if (rect != null)
            {
                DestroyObject(rect.gameObject);
            }
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        public void CollectFishRects(List<RectTransform> rects)
        {
            if (rects == null)
            {
                return;
            }

            rects.Clear();
            for (var i = 0; i < _fishViews.Count; i++)
            {
                if (_fishViews[i] == null)
                {
                    continue;
                }

                var rect = _fishViews[i].transform as RectTransform;
                if (rect != null)
                {
                    rects.Add(rect);
                }
            }
        }

        public void ClearFish()
        {
            for (var i = _fishViews.Count - 1; i >= 0; i--)
            {
                if (_fishViews[i] != null)
                {
                    DestroyObject(_fishViews[i].gameObject);
                }
            }

            _fishViews.Clear();
            if (_fishContainer == null)
            {
                return;
            }

            for (var i = _fishContainer.childCount - 1; i >= 0; i--)
            {
                DestroyObject(_fishContainer.GetChild(i).gameObject);
            }
        }

        private void ApplyLayout()
        {
            if (_fishLayout == null)
            {
                return;
            }

            var rects = new List<RectTransform>(_fishViews.Count);
            for (var i = 0; i < _fishViews.Count; i++)
            {
                if (_fishViews[i] == null)
                {
                    continue;
                }

                var rect = _fishViews[i].transform as RectTransform;
                if (rect != null)
                {
                    rects.Add(rect);
                }
            }

            _fishLayout.Apply(rects);
        }

        private void AllowFishRaycasts()
        {
            var area = transform.Find("InteractionArea");
            if (area == null)
            {
                return;
            }

            var image = area.GetComponent<Image>();
            if (image != null)
            {
                image.raycastTarget = false;
            }
        }

        private static void DestroyObject(GameObject target)
        {
            SceneObjectCleanup.DestroyObject(target);
        }
    }
}
