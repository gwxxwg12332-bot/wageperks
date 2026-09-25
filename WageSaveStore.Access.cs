using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Il2Cpp;
using UnityEngine;

namespace JacksonPerks;
partial class WageSaveStore

{
    // ===== Access =====

    internal static string GetString(string ns, string key, string def = "")
    {
        try
        {
            string k = ns + "." + key;
            if (_mem.TryGetValue(k, out string v)) return v;
            // 旧层兼容（迁移期）：新层 miss → 从旧层(PlayerPrefs)读 → 回填新层，打烊即完成迁移
            EnsureLegacyFresh();
            if (LegacyHasKey(ns, key))
            {
                string legacy = LegacyGetString(ns, key, def);
                _mem[k] = legacy;
                _dirty = true;
                return legacy;
            }
            return def;
        }
        catch { return def; }
    }

    internal static void SetString(string ns, string key, string value)
    {
        try
        {
            if (!_loadingComplete) return; // 09-26 读档空窗期：丢弃所有写入，防默认值污染
            _mem[ns + "." + key] = value ?? "";
            _dirty = true;
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSaveStore] 异常: " + ex.Message); }
    }

    internal static int GetInt(string ns, string key, int def = 0)
    {
        try
        {
            string k = ns + "." + key;
            if (_mem.TryGetValue(k, out string s) && int.TryParse(s, out int v)) return v;
            // 旧层兼容（迁移期）：新层 miss → 从旧层(PlayerPrefs)读 → 回填新层
            EnsureLegacyFresh();
            if (LegacyHasKey(ns, key))
            {
                int legacy = LegacyGetInt(ns, key, def);
                _mem[k] = legacy.ToString();
                _dirty = true;
                return legacy;
            }
            return def;
        }
        catch { return def; }
    }

    internal static void SetInt(string ns, string key, int value)
    {
        SetString(ns, key, value.ToString());
    }

    internal static float GetFloat(string ns, string key, float def = 0f)
    {
        try
        {
            string k = ns + "." + key;
            if (_mem.TryGetValue(k, out string s) && float.TryParse(s, out float v)) return v;
            // 旧层兼容（迁移期）：新层 miss → 从旧层(PlayerPrefs)读 → 回填新层
            EnsureLegacyFresh();
            if (LegacyHasKey(ns, key))
            {
                float legacy = LegacyGetFloat(ns, key, def);
                _mem[k] = legacy.ToString(System.Globalization.CultureInfo.InvariantCulture);
                _dirty = true;
                return legacy;
            }
            return def;
        }
        catch { return def; }
    }

    internal static void SetFloat(string ns, string key, float value)
    {
        SetString(ns, key, value.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    internal static bool GetBool(string ns, string key, bool def = false)
    {
        try
        {
            string k = ns + "." + key;
            if (_mem.TryGetValue(k, out string s)) return s == "1";
            // 旧层兼容（迁移期）：旧层 bool 实为 int 0/1，按 int 读取
            EnsureLegacyFresh();
            if (LegacyHasKey(ns, key))
            {
                bool legacy = LegacyGetBool(ns, key, def);
                _mem[k] = legacy ? "1" : "0";
                _dirty = true;
                return legacy;
            }
            return def;
        }
        catch { return def; }
    }

    internal static void SetBool(string ns, string key, bool value)
    {
        SetString(ns, key, value ? "1" : "0");
    }

    // 清空指定命名空间的所有键（新游戏/重置用）—— 旧层 ClearPerkState 的新层等价物。
    // 旧层靠"key 内嵌 runID，新档天然 miss"实现伪清除；新层文件即 runID，需显式清内存。
    internal static void ClearNamespace(string ns)
    {
        try
        {
            string prefix = ns + ".";
            var toRemove = new List<string>();
            foreach (var k in _mem.Keys)
            {
                if (k.StartsWith(prefix)) toRemove.Add(k);
            }
            foreach (var k in toRemove) _mem.Remove(k);
            if (toRemove.Count > 0) _dirty = true;
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSaveStore] 异常: " + ex.Message); }
    }

    internal static bool HasKey(string ns, string key)
    {
        // 迁移期：新层 miss 时也要认旧层(PlayerPrefs)的 key，
        // 否则 "if (HasKey) x = Get(...)" 模式的旧档数据会被静默跳过
        try
        {
            if (_mem.ContainsKey(ns + "." + key)) return true;
            EnsureLegacyFresh();
            return LegacyHasKey(ns, key);
        }
        catch { return false; }
    }

    // ===================== 新档 / 清理 =====================

    /// <summary>新档重置：清内存 + 删本档旧文件残留（开局 runID 空窗防误读）。</summary>
    internal static void ResetForNewRun()
    {
        try
        {
            ClearMem();
            // pending 文件是本轮会话的临时落点，新档必须清掉，否则新档开局会读到上一档残值
            string pending = FilePathFor(PENDING_KEY);
            if (pending != null && File.Exists(pending)) File.Delete(pending);
            _curKey = null;
            _dirty = false;
            Core.LogMsg("[SaveStore] 新档重置完成");
        }
        catch (Exception ex)
        {
            Core.LogMsg("[SaveStore] 新档重置失败: " + ex.Message);
        }
    }

    private static void ClearMem()
    {
        try { _mem.Clear(); } catch { }
    }
}
