using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FishPuzzle.Presentation
{
    /// <summary>
    /// Full-screen fade and a short "Level N" banner shown when a level attempt starts.
    /// Presentation only. It never changes level, session, or progression state.
    /// The fade blocks raycasts while it is opaque so no fish can be tapped during a rebuild.
    /// The banner never blocks input.
    /// </summary>
    public sealed class LevelTransitionView : MonoBehaviour
    {
        private Image _fade;
        private CanvasGroup _bannerGroup;
        private TextMeshProUGUI _bannerLabel;
        private Coroutine _bannerRoutine;

        public bool IsFadeBlocking => _fade != null && _fade.raycastTarget && _fade.color.a > 0.01f;

        public bool IsBannerVisible => _bannerGroup != null && _bannerGroup.gameObject.activeSelf && _bannerGroup.alpha > 0.01f;

        public string BannerText => _bannerLabel != null ? _bannerLabel.text : string.Empty;

        public static LevelTransitionView Create(Transform parent, TMP_FontAsset font)
        {
            var root = new GameObject("LevelTransition", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            Stretch(root.GetComponent<RectTransform>());
            var view = root.AddComponent<LevelTransitionView>();

            var bannerObject = new GameObject("LevelBanner", typeof(RectTransform), typeof(CanvasGroup), typeof(CanvasRenderer), typeof(Image));
            bannerObject.transform.SetParent(root.transform, false);
            var bannerRect = bannerObject.GetComponent<RectTransform>();
            bannerRect.anchorMin = new Vector2(0f, 0.5f);
            bannerRect.anchorMax = new Vector2(1f, 0.5f);
            bannerRect.pivot = new Vector2(0.5f, 0.5f);
            bannerRect.anchoredPosition = new Vector2(0f, 80f);
            bannerRect.sizeDelta = new Vector2(0f, 180f);
            var bannerImage = bannerObject.GetComponent<Image>();
            bannerImage.color = new Color(0.02f, 0.1f, 0.18f, 0.62f);
            bannerImage.raycastTarget = false;
            view._bannerGroup = bannerObject.GetComponent<CanvasGroup>();
            view._bannerGroup.blocksRaycasts = false;
            view._bannerGroup.interactable = false;
            view._bannerGroup.alpha = 0f;

            var labelObject = new GameObject("LevelBannerLabel", typeof(RectTransform));
            labelObject.SetActive(false);
            labelObject.transform.SetParent(bannerObject.transform, false);
            Stretch(labelObject.GetComponent<RectTransform>());
            var label = labelObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.fontSize = 96f;
            label.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false;
            labelObject.SetActive(true);
            view._bannerLabel = label;
            bannerObject.SetActive(false);

            var fadeObject = new GameObject("Fade", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fadeObject.transform.SetParent(root.transform, false);
            Stretch(fadeObject.GetComponent<RectTransform>());
            view._fade = fadeObject.GetComponent<Image>();
            view._fade.color = new Color(0.02f, 0.08f, 0.14f, 0f);
            view._fade.raycastTarget = false;
            return view;
        }

        public IEnumerator FadeOut(float seconds)
        {
            yield return Fade(0f, 1f, seconds, true);
        }

        public IEnumerator FadeIn(float seconds)
        {
            yield return Fade(CurrentFadeAlpha(), 0f, seconds, false);
        }

        public void ClearFade()
        {
            SetFade(0f, false);
        }

        public void ShowBanner(string text, float seconds)
        {
            if (_bannerGroup == null || _bannerLabel == null)
            {
                return;
            }

            transform.SetAsLastSibling();
            _bannerLabel.text = text;
            if (_bannerRoutine != null)
            {
                StopCoroutine(_bannerRoutine);
                _bannerRoutine = null;
            }

            _bannerGroup.gameObject.SetActive(true);
            if (!isActiveAndEnabled || seconds <= 0f)
            {
                _bannerGroup.alpha = 1f;
                return;
            }

            _bannerRoutine = StartCoroutine(PlayBanner(seconds));
        }

        public void HideBanner()
        {
            if (_bannerRoutine != null)
            {
                StopCoroutine(_bannerRoutine);
                _bannerRoutine = null;
            }

            if (_bannerGroup != null)
            {
                _bannerGroup.alpha = 0f;
                _bannerGroup.gameObject.SetActive(false);
            }
        }

        private IEnumerator PlayBanner(float seconds)
        {
            var fadeTime = Mathf.Min(0.2f, seconds * 0.25f);
            var hold = Mathf.Max(0f, seconds - (fadeTime * 2f));
            yield return BannerAlpha(0f, 1f, fadeTime);
            var elapsed = 0f;
            while (elapsed < hold)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            yield return BannerAlpha(1f, 0f, fadeTime);
            _bannerGroup.gameObject.SetActive(false);
            _bannerRoutine = null;
        }

        private IEnumerator BannerAlpha(float from, float to, float seconds)
        {
            var elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                _bannerGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / seconds));
                yield return null;
            }

            _bannerGroup.alpha = to;
        }

        private IEnumerator Fade(float from, float to, float seconds, bool block)
        {
            transform.SetAsLastSibling();
            SetFade(from, true);
            var elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                SetFade(Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / seconds)), true);
                yield return null;
            }

            SetFade(to, block);
        }

        private float CurrentFadeAlpha()
        {
            return _fade != null ? _fade.color.a : 0f;
        }

        private void SetFade(float alpha, bool block)
        {
            if (_fade == null)
            {
                return;
            }

            var color = _fade.color;
            color.a = alpha;
            _fade.color = color;
            _fade.raycastTarget = block && alpha > 0.01f;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
