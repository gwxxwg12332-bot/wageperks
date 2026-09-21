using System;
using System.Collections.Generic;
using System.Linq;

namespace JacksonPerks;

// ============================================================
// 特性状态持久化（内存缓存 + 打烊落盘 + 读档清缓存）
// ============================================================
internal static class PerkStateStore
{
    // 内存缓存（运行时）
    private static readonly Dictionary<string, int> _memCache = new();

    /// <summary>
    /// 读状态（优先内存）
    /// </summary>
    public static int Get(string perkId, string key, int defaultValue)
    {
        string fullKey = $"{perkId}_{key}";
        if (_memCache.TryGetValue(fullKey, out int v)) return v;

        int saved = PerkStatePersistence.GetInt(perkId, key, defaultValue);
        _memCache[fullKey] = saved;
        return saved;
    }

    /// <summary>
    /// 写状态（只写内存，不落盘）
    /// </summary>
    public static void Set(string perkId, string key, int value)
    {
        string fullKey = $"{perkId}_{key}";
        _memCache[fullKey] = value;
    }

    /// <summary>
    /// 打烊落盘（SaveGame Postfix 调）
    /// </summary>
    public static void Flush(string perkId)
    {
        foreach (var kv in _memCache.ToList())
        {
            if (kv.Key.StartsWith(perkId + "_"))
            {
                string key = kv.Key.Substring(perkId.Length + 1);
                PerkStatePersistence.SetInt(perkId, key, kv.Value);
            }
        }
    }

    /// <summary>
    /// 读档清缓存（LoadGame Postfix 调）
    /// </summary>
    public static void Clear(string perkId)
    {
        var keys = _memCache.Keys.Where(k => k.StartsWith(perkId + "_")).ToList();
        foreach (var k in keys) _memCache.Remove(k);
    }
}