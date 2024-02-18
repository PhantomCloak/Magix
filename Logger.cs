using System;
using UnityEngine;

namespace Magix.Diagnostics
{
    public enum LogLevel
    {
        Verbose = 0,
        Warning = 1,
        Error = 2
    }

    enum LogType
    {
        Normal = 0,
        Warning = 1,
        Error = 2
    }

    public static class MagixLogger
    {
        public static LogLevel Level = LogLevel.Verbose;

        public static void LogVerbose(string message)
        {
            if (Level > LogLevel.Verbose)
            {
                return;
            }

            Log("VERBOSE", message, LogType.Normal, Color.white);
        }

        public static void Log(string message)
        {
            if (Level > LogLevel.Warning)
            {
                return;
            }

            Log("INF", message, LogType.Normal, Color.white);
        }

        public static void LogWarn(string message)
        {
            if (Level > LogLevel.Warning)
            {
                return;
            }

            Log("WARN", message, LogType.Normal, Color.yellow);
        }

        public static void LogError(string message)
        {
            if (Level > LogLevel.Error)
            {
                return;
            }

            Log("ERR", message, LogType.Error, Color.red);
        }

        private static string GetTime()
        {
            return DateTime.Now.ToString("dd':'HH':'mm':'ss");
        }

        private static void Log(string category, string message, LogType type, Color color)
        {
            if (Level > LogLevel.Warning)
            {
                return;
            }
            var msg = string.Format("<color=#{0:X2}{1:X2}{2:X2}>{3}</color>{4}", (byte)(color.r * 255f), (byte)(color.g * 255f), (byte)(color.b * 255f), $"[LOG {category}] {GetTime()}]:", message);

            if (type == LogType.Normal)
                Debug.Log(msg);
            else if (type == LogType.Warning)
                Debug.LogWarning(msg);
            else if (type == LogType.Error)
                Debug.LogError(msg);
        }
    }
}
