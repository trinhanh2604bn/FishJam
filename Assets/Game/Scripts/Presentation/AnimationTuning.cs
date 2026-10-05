using FishPuzzle.Core;
using UnityEngine;

namespace FishPuzzle.Presentation
{
    /// <summary>
    /// One place for presentation timing. Gameplay code reads these values and does not keep its own durations.
    /// </summary>
    [CreateAssetMenu(fileName = "AnimationTuning", menuName = "Fish Puzzle/Animation Tuning")]
    public sealed class AnimationTuning : ScriptableObject
    {
        [Header("Fish")]
        [SerializeField] private float _fishTapSquashDuration = 0.04f;
        [SerializeField] private float _fishRouteDuration = 0.24f;
        [SerializeField] private float _fishLandingBounceDuration = 0.09f;
        [SerializeField] private float _fishPressLift = 8f;
        [SerializeField] private float _fishPressScale = 1.05f;
        [SerializeField] private float _fishPressWiggleDegrees = 2f;
        [SerializeField] private float _fishPressBob = 1.5f;
        [SerializeField] private float _fishArcHeight = 140f;
        [SerializeField] private float _fishArcLateral = 48f;

        [Header("Tank And Tray")]
        [SerializeField] private float _tankResolveDuration = 0.18f;
        [SerializeField] private float _tankTargetSwapDuration = 0.12f;
        [SerializeField] private float _trayAutoMoveDuration = 0.2f;
        [SerializeField] private float _trayAutoMoveStagger = 0.02f;
        [SerializeField] private float _landingOvershoot = 0.16f;

        [Header("Bubbles")]
        [SerializeField] private float _bubbleFishReflowDuration = 0.14f;
        [SerializeField] private float _bubblePopDuration = 0.22f;
        [SerializeField] private float _bubbleFallDuration = 0.19f;
        [SerializeField] private float _bubbleSlideDuration = 0.19f;
        [SerializeField] private float _bubbleLandingBounceDuration = 0.05f;
        [SerializeField] private float _bubbleTopSpawnDuration = 0.24f;
        [SerializeField] private float _bubblePathArc = 22f;
        [SerializeField] private float _topSpawnOffset = 340f;

        [Header("Touch")]
        [SerializeField] private float _touchRippleDuration = 0.36f;
        [SerializeField] private float _touchBubbleInterval = 0.11f;
        [SerializeField] private float _touchBubbleLifetime = 0.42f;
        [SerializeField] private int _touchBubbleLimit = 6;

        [Header("Recovery")]
        [SerializeField] private float _maxFrameStep = 0.05f;
        [SerializeField] private float _presentationSafetySeconds = 12f;

        public float FishTapSquashDuration => Mathf.Max(0f, _fishTapSquashDuration);

        public float FishRouteDuration => Mathf.Max(0f, _fishRouteDuration);

        public float FishLandingBounceDuration => Mathf.Max(0f, _fishLandingBounceDuration);

        public float FishPressLift => _fishPressLift > 0f ? _fishPressLift : 8f;

        public float FishPressScale => _fishPressScale > 1f ? _fishPressScale : 1.05f;

        public float FishPressWiggleDegrees => _fishPressWiggleDegrees > 0f ? _fishPressWiggleDegrees : 2f;

        public float FishPressBob => _fishPressBob > 0f ? _fishPressBob : 1.5f;

        public float FishArcHeight => _fishArcHeight;

        public float FishArcLateral => _fishArcLateral;

        public float TankResolveDuration => Mathf.Max(0f, _tankResolveDuration);

        public float TankTargetSwapDuration => Mathf.Max(0f, _tankTargetSwapDuration);

        public float TrayAutoMoveDuration => Mathf.Max(0f, _trayAutoMoveDuration);

        public float TrayAutoMoveStagger => Mathf.Max(0f, _trayAutoMoveStagger);

        public float LandingOvershoot => Mathf.Max(0f, _landingOvershoot);

        public float BubbleFishReflowDuration => Mathf.Max(0f, _bubbleFishReflowDuration);

        public float BubblePopDuration => Mathf.Max(0f, _bubblePopDuration);

        public float BubbleFallDuration => Mathf.Max(0f, _bubbleFallDuration);

        public float BubbleSlideDuration => Mathf.Max(0f, _bubbleSlideDuration);

        public float BubbleLandingBounceDuration => Mathf.Max(0f, _bubbleLandingBounceDuration);

        public float BubbleTopSpawnDuration => Mathf.Max(0f, _bubbleTopSpawnDuration);

        public float BubblePathArc => _bubblePathArc;

        public float TopSpawnOffset => Mathf.Max(0f, _topSpawnOffset);

        public float TouchRippleDuration => _touchRippleDuration > 0f ? _touchRippleDuration : 0.36f;

        public float TouchBubbleInterval => _touchBubbleInterval > 0f ? _touchBubbleInterval : 0.11f;

        public float TouchBubbleLifetime => _touchBubbleLifetime > 0f ? _touchBubbleLifetime : 0.42f;

        public int TouchBubbleLimit => _touchBubbleLimit > 0 ? _touchBubbleLimit : 6;

        public float MaxFrameStep => _maxFrameStep > 0f ? _maxFrameStep : 0.05f;

        public float PresentationSafetySeconds => _presentationSafetySeconds > 0f ? _presentationSafetySeconds : 12f;

        public static AnimationTuning RuntimeDefault()
        {
            var tuning = CreateInstance<AnimationTuning>();
            tuning.name = "AnimationTuningRuntime";
            tuning.hideFlags = HideFlags.HideAndDontSave;
            return tuning;
        }

        private void OnValidate()
        {
            ReportIfNegative(nameof(FishTapSquashDuration), _fishTapSquashDuration);
            ReportIfNegative(nameof(FishRouteDuration), _fishRouteDuration);
            ReportIfNegative(nameof(FishLandingBounceDuration), _fishLandingBounceDuration);
            ReportIfNegative(nameof(FishPressLift), _fishPressLift);
            ReportIfNegative(nameof(FishPressScale), _fishPressScale);
            ReportIfNegative(nameof(FishPressWiggleDegrees), _fishPressWiggleDegrees);
            ReportIfNegative(nameof(FishPressBob), _fishPressBob);
            ReportIfNegative(nameof(TankResolveDuration), _tankResolveDuration);
            ReportIfNegative(nameof(TankTargetSwapDuration), _tankTargetSwapDuration);
            ReportIfNegative(nameof(TrayAutoMoveDuration), _trayAutoMoveDuration);
            ReportIfNegative(nameof(TrayAutoMoveStagger), _trayAutoMoveStagger);
            ReportIfNegative(nameof(LandingOvershoot), _landingOvershoot);
            ReportIfNegative(nameof(BubbleFishReflowDuration), _bubbleFishReflowDuration);
            ReportIfNegative(nameof(BubblePopDuration), _bubblePopDuration);
            ReportIfNegative(nameof(BubbleFallDuration), _bubbleFallDuration);
            ReportIfNegative(nameof(BubbleSlideDuration), _bubbleSlideDuration);
            ReportIfNegative(nameof(BubbleLandingBounceDuration), _bubbleLandingBounceDuration);
            ReportIfNegative(nameof(BubbleTopSpawnDuration), _bubbleTopSpawnDuration);
            ReportIfNegative(nameof(TopSpawnOffset), _topSpawnOffset);
            ReportIfNegative(nameof(TouchRippleDuration), _touchRippleDuration);
            ReportIfNegative(nameof(TouchBubbleInterval), _touchBubbleInterval);
            ReportIfNegative(nameof(TouchBubbleLifetime), _touchBubbleLifetime);
            ReportIfNegative(nameof(TouchBubbleLimit), _touchBubbleLimit);
            ReportIfNegative(nameof(MaxFrameStep), _maxFrameStep);
            ReportIfNegative(nameof(PresentationSafetySeconds), _presentationSafetySeconds);
        }

        private static void ReportIfNegative(string fieldName, float value)
        {
            if (value >= 0f)
            {
                return;
            }

            GameLog.Error(
                nameof(AnimationTuning),
                fieldName + " must be greater than or equal to 0. Current value: " + value + ". The value was left unchanged.");
        }

        private static void ReportIfNegative(string fieldName, int value)
        {
            if (value >= 0)
            {
                return;
            }

            GameLog.Error(
                nameof(AnimationTuning),
                fieldName + " must be greater than or equal to 0. Current value: " + value + ". The value was left unchanged.");
        }
    }
}
