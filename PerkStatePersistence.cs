using System;
using System.Collections.Generic;
using Il2Cpp;
using UnityEngine;
namespace JacksonPerks;
// ============================================================
// 特性状态持久化工具（参考XIAOWOTradePerks的PlayerPrefs实现）
// 用PlayerPrefs存储特性状态，key带runID，读档后恢复
// ============================================================
internal static class PerkStatePersistence
{
    // 缓存当前runID
    private static string _cachedRunId = null;
    // 获取当前runID（用于持久化的key前缀）
    private static string GetRunId()
    {
        if (!string.IsNullOrEmpty(_cachedRunId)) return _cachedRunId;
        try
        {
            var ps = PlayerStore.Instance;
            if (ps != null)
            {
                string rid = ps.runID ?? "";
                if (!string.IsNullOrEmpty(rid))
                {
                    _cachedRunId = rid;
                    try { } catch { }
                    return rid;
                }
            }
        }
        catch { }
        // 不缓存 default_run：PlayerStore.runID 可能在游戏开始后才赋值，缓存会导致永远用 default_run
        return "default_run";
    }
    // 新游戏时重置缓存
    internal static void ResetCache()
    {
        _cachedRunId = null;
    }
    // 生成带runID的key
    private static string MakeKey(string perkId, string key)
    {
        return "WagesPerks_" + GetRunId() + "_" + perkId + "_" + key;
    }
    // 存储int
    internal static void SetInt(string perkId, string key, int value)
    {
        try
        {
            PlayerPrefs.SetInt(MakeKey(perkId, key), value);
            PlayerPrefs.Save();
        }
        catch { }
    }
    // 读取int
    internal static int GetInt(string perkId, string key, int defaultValue = 0)
    {
        try
        {
            string fullKey = MakeKey(perkId, key);
            if (PlayerPrefs.HasKey(fullKey))
            {
                return PlayerPrefs.GetInt(fullKey, defaultValue);
            }
        }
        catch { }
        return defaultValue;
    }
    // 存储float
    internal static void SetFloat(string perkId, string key, float value)
    {
        try
        {
            PlayerPrefs.SetFloat(MakeKey(perkId, key), value);
            PlayerPrefs.Save();
        }
        catch { }
    }
    // 读取float
    internal static float GetFloat(string perkId, string key, float defaultValue = 0f)
    {
        try
        {
            string fullKey = MakeKey(perkId, key);
            if (PlayerPrefs.HasKey(fullKey))
            {
                return PlayerPrefs.GetFloat(fullKey, defaultValue);
            }
        }
        catch { }
        return defaultValue;
    }
    // 存储bool
    internal static void SetBool(string perkId, string key, bool value)
    {
        SetInt(perkId, key, value ? 1 : 0);
    }
    // 读取bool
    internal static bool GetBool(string perkId, string key, bool defaultValue = false)
    {
        return GetInt(perkId, key, defaultValue ? 1 : 0) == 1;
    }
    // 存储string
    internal static void SetString(string perkId, string key, string value)
    {
        try
        {
            PlayerPrefs.SetString(MakeKey(perkId, key), value ?? "");
            PlayerPrefs.Save();
        }
        catch { }
    }
    // 读取string
    internal static string GetString(string perkId, string key, string defaultValue = "")
    {
        try
        {
            string fullKey = MakeKey(perkId, key);
            if (PlayerPrefs.HasKey(fullKey))
            {
                return PlayerPrefs.GetString(fullKey, defaultValue);
            }
        }
        catch { }
        return defaultValue;
    }
    // 检查key是否存在
    internal static bool HasKey(string perkId, string key)
    {
        try
        {
            return PlayerPrefs.HasKey(MakeKey(perkId, key));
        }
        catch { }
        return false;
    }
    // 删除某个特性的所有状态
    // 【已知限制】Unity PlayerPrefs 无枚举 API，无法列出已写入的 key。
    // key 格式带 runID+perkId（如 WagesPerks_<runID>_<perkId>_<key>），
    // 不同存档/不同特性天然隔离，残留仅为垃圾数据，不会串档或误读。
    // 因此此处不实现物理删除；新档由 ResetCache() 清缓存、新 key 覆盖旧 key。
    internal static void ClearPerkState(string perkId)
    {
        try
        {
            _cachedRunId = null; // 顺带清 runID 缓存，避免切档后 key 前缀串用
        }
        catch { }
    }
}
