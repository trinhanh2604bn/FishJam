using System;
using FishPuzzle.Core;
using FishPuzzle.Domain;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FishPuzzle.Presentation
{
    /// <summary>
    /// Displays one fish sprite. A press is visual only. Release asks the flow to route once.
    /// </summary>
    public sealed class FishView : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler, ICancelHandler
    {
        [SerializeField] private Image _visual;
        [SerializeField] private FishVisualCatalog _catalog;
        [SerializeField] private FishType _displayedType;

        private Func<FishView, bool> _tryBeginPress;
        private Action<FishView> _onCommit;
        private Action<FishView> _onCancel;
        private FishPressFeedback _pressFeedback;
        private FishPressHighlight _highlight;
        private FishIdleSway _idleSway;
        private AnimationTuning _tuning;
        private Rect _pressScreenRect;
        private Vector2 _pressScreenPosition;
        private bool _gestureActive;

        public int FishId { get; private set; }

        public FishType DisplayedType => _displayedType;

        public Sprite CurrentSprite => _visual != null ? _visual.sprite : null;

        public bool IsPressed => _pressFeedback != null && _pressFeedback.IsPressed;

        public float PressLift => _pressFeedback != null ? _pressFeedback.Lift : 0f;

        public float PressScale => _pressFeedback != null ? _pressFeedback.Scale : 1f;

        public float PressAngle => _pressFeedback != null ? _pressFeedback.Angle : 0f;

        public bool IsHighlighted => _highlight != null && _highlight.IsShown;

        public FishPressHighlight Highlight => _highlight;

        public void SetPresentationTuning(AnimationTuning tuning)
        {
            _tuning = tuning;
            PlayIdle();
        }

        public void BindInteraction(int fishId, Func<FishView, bool> tryBeginPress, Action<FishView> onCommit, Action<FishView> onCancel)
        {
            FishId = fishId;
            _tryBeginPress = tryBeginPress;
            _onCommit = onCommit;
            _onCancel = onCancel;
            _gestureActive = false;
            EndPress();
            SetRaycastTarget(true);
        }

        public void ReleaseInteraction()
        {
            var cancel = _onCancel;
            var notify = _gestureActive;
            _gestureActive = false;
            _tryBeginPress = null;
            _onCommit = null;
            _onCancel = null;
            EndPress();
            SetRaycastTarget(false);
            if (notify && cancel != null)
            {
                cancel(this);
            }
        }

        public void BeginPress(AnimationTuning tuning)
        {
            if (tuning != null)
            {
                _tuning = tuning;
            }

            Idle().Pause();
            Press().Begin(PressRect(), _tuning);
            Highlighter().Show(PressRect());
        }

        public void AdvancePress(float delta)
        {
            if (_pressFeedback != null)
            {
                _pressFeedback.Tick(delta);
            }
        }

        public void EndPress()
        {
            if (_pressFeedback != null)
            {
                _pressFeedback.End();
            }

            if (_highlight != null)
            {
                _highlight.Hide();
            }

            PlayIdle();
        }

        private void OnEnable()
        {
            PlayIdle();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_gestureActive || eventData == null || _tryBeginPress == null || !_tryBeginPress(this))
            {
                return;
            }

            _gestureActive = true;
            CapturePressRect(eventData.position);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!_gestureActive)
            {
                return;
            }

            _gestureActive = false;
            if (IsReleaseOverFish(eventData))
            {
                if (_onCommit != null)
                {
                    _onCommit(this);
                }

                return;
            }

            if (_onCancel != null)
            {
                _onCancel(this);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData == null)
            {
                return;
            }
        }

        public void OnCancel(BaseEventData eventData)
        {
            if (!_gestureActive)
            {
                return;
            }

            _gestureActive = false;
            if (_onCancel != null)
            {
                _onCancel(this);
            }
        }

        public void BindCatalog(FishVisualCatalog catalog)
        {
            _catalog = catalog;
        }

        public void Show(FishType fishType)
        {
            _displayedType = fishType;
            if (_visual == null)
            {
                GameLog.Error(nameof(FishView), "Fish visual Image is not assigned.");
                return;
            }

            if (_catalog == null)
            {
                GameLog.Error(nameof(FishView), "FishVisualCatalog is not assigned.");
                _visual.sprite = null;
                return;
            }

            if (!_catalog.TryGetSprite(fishType, out var sprite))
            {
                GameLog.Error(nameof(FishView), "No sprite is mapped for FishType." + fishType + ".");
                _visual.sprite = null;
                return;
            }

            _visual.sprite = sprite;
            _visual.enabled = true;
            PlayIdle();
        }

        private void PlayIdle()
        {
            if (IsPressed)
            {
                return;
            }

            Idle().Play(PressRect(), _tuning);
        }

        private FishIdleSway Idle()
        {
            if (_idleSway == null)
            {
                _idleSway = GetComponent<FishIdleSway>();
                if (_idleSway == null)
                {
                    _idleSway = gameObject.AddComponent<FishIdleSway>();
                }
            }

            return _idleSway;
        }

        private FishPressFeedback Press()
        {
            if (_pressFeedback == null)
            {
                _pressFeedback = GetComponent<FishPressFeedback>();
                if (_pressFeedback == null)
                {
                    _pressFeedback = gameObject.AddComponent<FishPressFeedback>();
                }
            }

            return _pressFeedback;
        }

        private FishPressHighlight Highlighter()
        {
            if (_highlight == null)
            {
                _highlight = GetComponent<FishPressHighlight>();
                if (_highlight == null)
                {
                    _highlight = gameObject.AddComponent<FishPressHighlight>();
                }
            }

            return _highlight;
        }

        private RectTransform PressRect()
        {
            if (_visual != null)
            {
                return _visual.rectTransform;
            }

            var image = GetComponentInChildren<Image>(true);
            return image != null ? image.rectTransform : transform as RectTransform;
        }

        private void CapturePressRect(Vector2 screenPosition)
        {
            _pressScreenPosition = screenPosition;
            var rect = transform as RectTransform;
            if (rect == null)
            {
                _pressScreenRect = new Rect(screenPosition.x - 48f, screenPosition.y - 48f, 96f, 96f);
                return;
            }

            var canvas = GetComponentInParent<Canvas>();
            Camera camera = null;
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                camera = canvas.worldCamera;
            }

            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            for (var i = 0; i < corners.Length; i++)
            {
                var screen = RectTransformUtility.WorldToScreenPoint(camera, corners[i]);
                min = Vector2.Min(min, screen);
                max = Vector2.Max(max, screen);
            }

            const float pad = 36f;
            _pressScreenRect = Rect.MinMaxRect(min.x - pad, min.y - pad, max.x + pad, max.y + pad);
        }

        private bool IsReleaseOverFish(PointerEventData eventData)
        {
            if (eventData == null)
            {
                return false;
            }

            var insideRect = _pressScreenRect.width >= 1f
                && _pressScreenRect.height >= 1f
                && _pressScreenRect.Contains(eventData.position);
            var nearPress = Vector2.Distance(eventData.position, _pressScreenPosition) <= 48f;
            return insideRect || nearPress;
        }

        private void SetRaycastTarget(bool raycastOn)
        {
            if (_visual != null)
            {
                _visual.raycastTarget = raycastOn;
            }
        }
    }
}
