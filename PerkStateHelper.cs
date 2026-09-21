using System.Collections.Generic;
using System.Linq;

namespace JacksonPerks;

// 状态读写（runID 处理 + 内存缓存 + 打烊落盘）
internal static class PerkStateHelper
{
    private static readonly Dictionary<string, int> _memCache = new();

    public static int GetInt(string ns, string key, int defaultValue = 0)
    {
        string fullKey = $"{ns}_{key}";
        if (_memCache.TryGetValue(fullKey, out int v)) return v;
        int saved = PerkStatePersistence.GetInt(ns, key, defaultValue);
        _memCache[fullKey] = saved;
        return saved;
    }

    public static void SetInt(string ns, string key, int value)
    {
        string fullKey = $"{ns}_{key}";
        _memCache[fullKey] = value;
    }

    public static void Flush(string ns)
    {
        foreach (var kv in _memCache)
        {
            if (kv.Key.StartsWith(ns + "_"))
            {
                string key = kv.Key.Substring(ns.Length + 1);
                PerkStatePersistence.SetInt(ns, key, kv.Value);
            }
        }
    }

    public static void Clear(string ns)
    {
        var keys = _memCache.Keys.Where(k => k.StartsWith(ns + "_")).ToList();
        foreach (var k in keys) _memCache.Remove(k);
    }

    public static void ResetForNewRun(string ns, params string[] keys)
    {
        foreach (var key in keys)
        {
            PerkStatePersistence.SetInt(ns, key, 0);
            _memCache.Remove($"{ns}_{key}");
        }
    }
}