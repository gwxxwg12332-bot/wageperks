using System.Diagnostics;

namespace JacksonPerks;

// 日志统一（级别 + 前缀 + Debug 自动关）
internal static class LoggerHelper
{
    [Conditional("DEBUG")]
    public static void Debug(string tag, string msg)
    {
        Core.LogMsg($"[{tag}] {msg}");
    }

    public static void Info(string tag, string msg)
    {
        Core.LogMsg($"[{tag}] {msg}");
    }

    public static void Warn(string tag, string msg)
    {
        Core.LogMsg($"[Warn][{tag}] {msg}");
    }

    public static void Error(string tag, string msg)
    {
        Core.LogMsg($"[Error][{tag}] {msg}");
    }
}