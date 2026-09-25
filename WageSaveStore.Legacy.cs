using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Il2Cpp;
using UnityEngine;

namespace JacksonPerks;
partial class WageSaveStore

{
    // ===== Legacy =====

    // ===================== ① 运行时读写（只碰内存） =====================

    // 旧层(PerkStatePersistence)缓存 runID 可能来自上一档；兼容读取前强制刷新。
    // 根治"切档后旧缓存串档"（原 4 处业务侧 ResetCache 调用已收拢至此，业务代码不再手写）
    private static void EnsureLegacyFresh()
    {
        try { _legacyRunId = null; } catch { }
    }

    // ===================== 旧层读取器（PlayerPrefs 迁移通道，仅本文件私有使用） =====================
    // 原 PerkStatePersistence 逻辑内联（key 格式 WagesPerks_<runID>_<perkId>_<key>，含 default_run 惰性迁移）

    private static string LegacyGetRunId()
    {
        if (!string.IsNullOrEmpty(_legacyRunId)) return _legacyRunId;
        try
        {
            var ps = PlayerStore.Instance;
            if (ps != null)
            {
                string rid = ps.runID ?? "";
                if (!string.IsNullOrEmpty(rid))
                {
                    _legacyRunId = rid;
                    return rid;
                }
            }
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSaveStore] 异常: " + ex.Message); }
        // 不缓存 default_run：PlayerStore.runID 可能在游戏开始后才赋值，缓存会导致永远用 default_run
        return "default_run";
    }

    private static string LegacyKey(string perkId, string key)
    {
        return "WagesPerks_" + LegacyGetRunId() + "_" + perkId + "_" + key;
    }

    private static string LegacyDefaultKey(string perkId, string key)
    {
        return "WagesPerks_default_run_" + perkId + "_" + key;
    }

    // 检查旧层 key 是否存在（真 key 优先，default_run 残留回退）
    private static bool LegacyHasKey(string perkId, string key)
    {
        try
        {
            if (PlayerPrefs.HasKey(LegacyKey(perkId, key))) return true;
            return PlayerPrefs.HasKey(LegacyDefaultKey(perkId, key));
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSaveStore] 异常: " + ex.Message); }
        return false;
    }

    // 读旧层 string（default_run 残留 → 惰性迁移：搬进真 key，删旧键——幂等）
    private static string LegacyGetString(string perkId, string key, string defaultValue = "")
    {
        try
        {
            string fullKey = LegacyKey(perkId, key);
            if (PlayerPrefs.HasKey(fullKey))
            {
                return PlayerPrefs.GetString(fullKey, defaultValue);
            }
            string defKey = LegacyDefaultKey(perkId, key);
            if (PlayerPrefs.HasKey(defKey))
            {
                string v = PlayerPrefs.GetString(defKey, defaultValue);
                try { PlayerPrefs.SetString(fullKey, v ?? ""); PlayerPrefs.DeleteKey(defKey); PlayerPrefs.Save(); } catch { }
                return v;
            }
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSaveStore] 异常: " + ex.Message); }
        return defaultValue;
    }

    // 读旧层 int（default_run 惰性迁移同 GetString）
    private static int LegacyGetInt(string perkId, string key, int defaultValue = 0)
    {
        try
        {
            string fullKey = LegacyKey(perkId, key);
            if (PlayerPrefs.HasKey(fullKey))
            {
                return PlayerPrefs.GetInt(fullKey, defaultValue);
            }
            string defKey = LegacyDefaultKey(perkId, key);
            if (PlayerPrefs.HasKey(defKey))
            {
                int v = PlayerPrefs.GetInt(defKey, defaultValue);
                try { PlayerPrefs.SetInt(fullKey, v); PlayerPrefs.DeleteKey(defKey); PlayerPrefs.Save(); } catch { }
                return v;
            }
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSaveStore] 异常: " + ex.Message); }
        return defaultValue;
    }

    // 读旧层 float（default_run 惰性迁移同 GetString）
    private static float LegacyGetFloat(string perkId, string key, float defaultValue = 0f)
    {
        try
        {
            string fullKey = LegacyKey(perkId, key);
            if (PlayerPrefs.HasKey(fullKey))
            {
                return PlayerPrefs.GetFloat(fullKey, defaultValue);
            }
            string defKey = LegacyDefaultKey(perkId, key);
            if (PlayerPrefs.HasKey(defKey))
            {
                float v = PlayerPrefs.GetFloat(defKey, defaultValue);
                try { PlayerPrefs.SetFloat(fullKey, v); PlayerPrefs.DeleteKey(defKey); PlayerPrefs.Save(); } catch { }
                return v;
            }
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSaveStore] 异常: " + ex.Message); }
        return defaultValue;
    }

    // 读旧层 bool（旧层 bool 实为 int 0/1）
    private static bool LegacyGetBool(string perkId, string key, bool defaultValue = false)
    {
        return LegacyGetInt(perkId, key, defaultValue ? 1 : 0) == 1;
    }

    // 新档防 default_run 残留污染：清指定特性的 default_run 旧 key（新档开局 runID 空窗口防误读旧档残留）
    internal static void CleanLegacyDefaultRun(string perkId, string[] keys)
    {
        try
        {
            if (keys == null) return;
            foreach (var k in keys)
            {
                string dk = LegacyDefaultKey(perkId, k);
                if (PlayerPrefs.HasKey(dk)) PlayerPrefs.DeleteKey(dk);
            }
            PlayerPrefs.Save();
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSaveStore] 异常: " + ex.Message); }
    }
}
