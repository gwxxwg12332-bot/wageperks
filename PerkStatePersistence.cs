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
    // 09-22 runID时序修复：开局(runID空)时状态落 default_run，读档后真 key miss → 回退 default_run（惰性迁移，搬完删旧键）
    private static string DefaultKey(string perkId, string key)
    {
        return "WagesPerks_default_run_" + perkId + "_" + key;
    }
    // 存储int
    internal static void SetInt(string perkId, string key, int value)
    {
        try
        {
            string rid = GetRunId();
            string fullKey = MakeKey(perkId, key);
            PlayerPrefs.SetInt(fullKey, value);
            // 档归属明确(runID非空)时删 default_run 残留同 key——防旧档污染
            if (rid != "default_run")
            {
                string defKey = DefaultKey(perkId, key);
                if (PlayerPrefs.HasKey(defKey)) PlayerPrefs.DeleteKey(defKey);
            }
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
            // 回退 default_run + 惰性迁移（搬进真 key，删旧键——幂等）
            string defKey = DefaultKey(perkId, key);
            if (PlayerPrefs.HasKey(defKey))
            {
                int v = PlayerPrefs.GetInt(defKey, defaultValue);
                try { PlayerPrefs.SetInt(fullKey, v); PlayerPrefs.DeleteKey(defKey); PlayerPrefs.Save(); } catch { }
                return v;
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
            string rid = GetRunId();
            string fullKey = MakeKey(perkId, key);
            PlayerPrefs.SetFloat(fullKey, value);
            if (rid != "default_run")
            {
                string defKey = DefaultKey(perkId, key);
                if (PlayerPrefs.HasKey(defKey)) PlayerPrefs.DeleteKey(defKey);
            }
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
            string defKey = DefaultKey(perkId, key);
            if (PlayerPrefs.HasKey(defKey))
            {
                float v = PlayerPrefs.GetFloat(defKey, defaultValue);
                try { PlayerPrefs.SetFloat(fullKey, v); PlayerPrefs.DeleteKey(defKey); PlayerPrefs.Save(); } catch { }
                return v;
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
            string rid = GetRunId();
            string fullKey = MakeKey(perkId, key);
            PlayerPrefs.SetString(fullKey, value ?? "");
            if (rid != "default_run")
            {
                string defKey = DefaultKey(perkId, key);
                if (PlayerPrefs.HasKey(defKey)) PlayerPrefs.DeleteKey(defKey);
            }
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
            string defKey = DefaultKey(perkId, key);
            if (PlayerPrefs.HasKey(defKey))
            {
                string v = PlayerPrefs.GetString(defKey, defaultValue);
                try { PlayerPrefs.SetString(fullKey, v ?? ""); PlayerPrefs.DeleteKey(defKey); PlayerPrefs.Save(); } catch { }
                return v;
            }
        }
        catch { }
        return defaultValue;
    }

    // 09-22 新档防 default_run 残留污染：清指定特性的 default_run 旧 key（新档开局 runID 空窗口防误读旧档残留）
    internal static void CleanDefaultRun(string perkId, string[] keys)
    {
        try
        {
            if (keys == null) return;
            foreach (var k in keys)
            {
                string dk = DefaultKey(perkId, k);
                if (PlayerPrefs.HasKey(dk)) PlayerPrefs.DeleteKey(dk);
            }
            PlayerPrefs.Save();
        }
        catch { }
    }
    // 检查key是否存在
    internal static bool HasKey(string perkId, string key)
    {
        try
        {
            if (PlayerPrefs.HasKey(MakeKey(perkId, key))) return true;
            return PlayerPrefs.HasKey(DefaultKey(perkId, key)); // 回退：default_run 残留也算有
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
