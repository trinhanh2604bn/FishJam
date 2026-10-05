using System;
using System.Diagnostics;
using UnityEngine;

namespace FishPuzzle.Core
{
    /// <summary>
    /// Category-prefixed logging. Info calls are compiled into the Editor and development player code,
    /// and compiled out of release player builds.
    /// </summary>
    public static class GameLog
    {
        // DEBUG is Unity's managed-code variant for the Editor and development players.
        // DEVELOPMENT_BUILD is deprecated for compile-time branching (UAC0009).
        [Conditional("UNITY_EDITOR")]
        [Conditional("DEBUG")]
        public static void Info(string category, string message)
        {
            Write(LogType.Log, category, message);
        }

        public static void Warning(string category, string message)
        {
            Write(LogType.Warning, category, message);
        }

        public static void Error(string category, string message)
        {
            Write(LogType.Error, category, message);
        }

        private static void Write(LogType type, string category, string message)
        {
            if (string.IsNullOrWhiteSpace(category))
            {
                throw new ArgumentException("Category must name the source of the log.", nameof(category));
            }

            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            var formatted = $"[{category}] {message}";
            switch (type)
            {
                case LogType.Warning:
                    UnityEngine.Debug.LogWarning(formatted);
                    break;
                case LogType.Error:
                    UnityEngine.Debug.LogError(formatted);
                    break;
                default:
                    UnityEngine.Debug.Log(formatted);
                    break;
            }
        }
    }
}
