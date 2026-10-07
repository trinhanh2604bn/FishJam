using System;
using UnityEngine;

namespace FishPuzzle.Presentation
{
    /// <summary>Haptic moments, ordered from lightest to strongest.</summary>
    public enum HapticKind
    {
        Touch = 0,
        FishPress = 1,
        TankLanding = 2,
        BubblePop = 3,
        TankComplete = 4
    }

    /// <summary>
    /// Short presentation-only vibration. It never reads or writes gameplay state.
    /// Every request is counted; a request plays only when its own cooldown has passed and no equal or stronger
    /// pulse played in the last <see cref="GlobalGapSeconds"/>. Editor and non-Android players count but never vibrate.
    /// Any platform failure is swallowed and turns the device output off for the rest of the session.
    /// </summary>
    public sealed class HapticFeedbackService
    {
        public const float GlobalGapSeconds = 0.05f;

        private const int KindCount = 5;

        private static readonly int[] Durations = { 12, 18, 30, 35, 60 };
        private static readonly int[] Amplitudes = { 40, 70, 120, 150, 220 };
        private static readonly float[] Cooldowns = { 0.12f, 0.10f, 0.08f, 0.15f, 0.20f };

        private readonly int[] _requests = new int[KindCount];
        private readonly int[] _plays = new int[KindCount];
        private readonly float[] _lastPlayed = new float[KindCount];
        private Func<int, int, bool> _output;
        private Func<float> _clock;
        private float _lastAnyPlayed = float.NegativeInfinity;
        private int _lastAnyKind = -1;
        private int _deviceVibrations;

        /// <param name="output">Device pulse (milliseconds, amplitude 1–255). Null means no physical vibration.</param>
        /// <param name="clock">Seconds source. Null uses unscaled time.</param>
        public HapticFeedbackService(Func<int, int, bool> output, Func<float> clock = null)
        {
            _output = output;
            _clock = clock;
            for (var i = 0; i < KindCount; i++)
            {
                _lastPlayed[i] = float.NegativeInfinity;
            }
        }

        /// <summary>Platform service: native vibration on an Android player, a silent counter everywhere else.</summary>
        public static HapticFeedbackService Create()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            var vibrator = new AndroidVibrator();
            return new HapticFeedbackService(vibrator.Vibrate);
#else
            return new HapticFeedbackService(null);
#endif
        }

        public bool HasDeviceOutput => _output != null;

        /// <summary>Pulses actually sent to the device.</summary>
        public int DeviceVibrationCount => _deviceVibrations;

        public HapticKind? LastPlayed => _lastAnyKind >= 0 ? (HapticKind?)_lastAnyKind : null;

        public static int DurationMs(HapticKind kind)
        {
            return Durations[Index(kind)];
        }

        public static float CooldownSeconds(HapticKind kind)
        {
            return Cooldowns[Index(kind)];
        }

        public int RequestCount(HapticKind kind)
        {
            return _requests[Index(kind)];
        }

        public int PlayCount(HapticKind kind)
        {
            return _plays[Index(kind)];
        }

        /// <summary>Returns true when the pulse passed the throttle. Never throws.</summary>
        public bool Play(HapticKind kind)
        {
            var index = Index(kind);
            _requests[index]++;
            var now = Now();
            if (now - _lastPlayed[index] < Cooldowns[index])
            {
                return false;
            }

            if (_lastAnyKind >= index && now - _lastAnyPlayed < GlobalGapSeconds)
            {
                return false;
            }

            _lastPlayed[index] = now;
            _lastAnyPlayed = now;
            _lastAnyKind = index;
            _plays[index]++;
            if (_output == null)
            {
                return true;
            }

            try
            {
                if (_output(Durations[index], Amplitudes[index]))
                {
                    _deviceVibrations++;
                }
            }
            catch (Exception)
            {
                _output = null;
            }

            return true;
        }

        private float Now()
        {
            if (_clock != null)
            {
                try
                {
                    return _clock();
                }
                catch (Exception)
                {
                    _clock = null;
                }
            }

            return Time.unscaledTime;
        }

        private static int Index(HapticKind kind)
        {
            var index = (int)kind;
            return index < 0 || index >= KindCount ? 0 : index;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private sealed class AndroidVibrator
        {
            private const int DefaultAmplitude = -1;

            private AndroidJavaObject _vibrator;
            private AndroidJavaClass _effects;
            private bool _amplitudeControl;
            private bool _initialized;
            private bool _failed;

            public bool Vibrate(int milliseconds, int amplitude)
            {
                if (_failed)
                {
                    return false;
                }

                try
                {
                    if (!_initialized)
                    {
                        Initialize();
                    }

                    if (_vibrator == null)
                    {
                        _failed = true;
                        return false;
                    }

                    if (_effects != null)
                    {
                        var strength = _amplitudeControl ? Mathf.Clamp(amplitude, 1, 255) : DefaultAmplitude;
                        using (var effect = _effects.CallStatic<AndroidJavaObject>("createOneShot", (long)milliseconds, strength))
                        {
                            _vibrator.Call("vibrate", effect);
                        }
                    }
                    else
                    {
                        _vibrator.Call("vibrate", (long)milliseconds);
                    }

                    return true;
                }
                catch (Exception)
                {
                    _failed = true;
                    return false;
                }
            }

            private void Initialize()
            {
                _initialized = true;
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    _vibrator = activity != null ? activity.Call<AndroidJavaObject>("getSystemService", "vibrator") : null;
                }

                if (_vibrator == null || !_vibrator.Call<bool>("hasVibrator"))
                {
                    _vibrator = null;
                    return;
                }

                using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                {
                    if (version.GetStatic<int>("SDK_INT") >= 26)
                    {
                        _effects = new AndroidJavaClass("android.os.VibrationEffect");
                        _amplitudeControl = _vibrator.Call<bool>("hasAmplitudeControl");
                    }
                }
            }

            // Never called: its pulse is far too long. The reference makes the Android build add the VIBRATE permission.
            private static void RequestVibratePermission()
            {
                Handheld.Vibrate();
            }
        }
#endif
    }
}
