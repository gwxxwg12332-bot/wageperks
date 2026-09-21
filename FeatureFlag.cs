using System.Collections.Generic;

namespace JacksonPerks;

// 功能开关统一管理
internal static class FeatureFlag
{
    private static readonly Dictionary<string, bool> _flags = new();

    public static bool IsEnabled(string flagName)
    {
        if (_flags.TryGetValue(flagName, out bool v)) return v;
        return true; // 默认开
    }

    public static void SetEnabled(string flagName, bool enabled)
    {
        _flags[flagName] = enabled;
    }
}