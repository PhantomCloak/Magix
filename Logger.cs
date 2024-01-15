using System;
using UnityEngine;

public enum LogLevel {
    Verbose = 0,
    Warning = 1,
    Error = 2
}

public static class Logger
{
    public static LogLevel Level = LogLevel.Warning;

    public static void LogVerbose(string message)
    {
        if(Level > LogLevel.Verbose)
        {
            return;
        }

        Debug.Log($"[LOG VERBOSE {GetTime()}]:" + message);
    }

    public static void Log(string category, string message, Color color)
    {
        if(Level > LogLevel.Warning)
        {
            return;
        }
        Debug.Log(string.Format("<color=#{0:X2}{1:X2}{2:X2}>{3}</color>", (byte)(color.r * 255f), (byte)(color.g * 255f), (byte)(color.b * 255f), $"[LOG {category}] {GetTime()}]:" + message));
    }

    public static void Log(string message)
    {
        if(Level > LogLevel.Warning)
        {
            return;
        }

        Debug.Log($"[LOG INFO {GetTime()}]:" + message);
    }

    public static void LogWarn(string message)
    {
        if(Level > LogLevel.Warning)
        {
            return;
        }

        Debug.LogWarning($"[LOG WARN {GetTime()}]:" + message);
    }

    public static void LogError(string message)
    {
        if(Level > LogLevel.Error)
        {
            return;
        }

        Debug.LogError($"[LOG ERROR {GetTime()}]:" + message);
    }

    private static string GetTime()
    {
        return DateTime.Now.ToString("dd':'HH':'mm':'ss");
    }
}
