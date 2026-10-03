using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Il2Cpp;
using UnityEngine;

namespace WageSurvival;

internal static class SaveStore
{
    internal const string PERK_ID = "WageSurvival";

    // ===== 内存字典 =====
    private static readonly Dictionary<string, string> _mem = new Dictionary<string, string>();
    private static bool _dirty = false;

    // ===== 文件路径 =====
    private static string DirPath => Path.Combine(Application.persistentDataPath, "WageSurvival");
    private const string FILE_PREFIX = "wagesurvival_";
    private const string PENDING_KEY = "pending";
    private const string HEAD_VERSION = "#version=1";
    private const string HEAD_RUNID = "#runID=";

    // ===== Key解析（runID优先，未就绪用pending） =====
    private static string ResolveKey()
    {
        try
        {
            var ps = PlayerStore.Instance;
            if (ps != null)
            {
                string rid = ps.runID;
                if (!string.IsNullOrEmpty(rid))
                {
                    string key = Sanitize(rid);
                    Core.LogMsg($"[WageSurvival.SaveStore] ResolveKey: runID={rid} → key={key}");
                    return key;
                }
            }
            Core.LogMsg("[WageSurvival.SaveStore] ResolveKey: runID为空 → pending");
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival.SaveStore] ResolveKey异常: " + ex.Message); }
        return PENDING_KEY;
    }

    private static string Sanitize(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return PENDING_KEY;
        var sb = new StringBuilder(raw.Length);
        foreach (char c in raw)
        {
            if (char.IsLetterOrDigit(c) || c == '_' || c == '-') sb.Append(c);
            else sb.Append('_');
        }
        string s = sb.ToString();
        return s.Length > 64 ? s.Substring(0, 64) : s;
    }

    private static string FilePathFor(string key)
    {
        try
        {
            string dir = DirPath;
            if (string.IsNullOrEmpty(dir)) return null;
            return Path.Combine(dir, FILE_PREFIX + key + ".txt");
        }
        catch { return null; }
    }

    // ===== Get/Set（内存字典） =====
    internal static int GetInt(string key, int defaultValue = 0)
    {
        try
        {
            if (_mem.TryGetValue(key, out string s) && int.TryParse(s, out int v)) return v;
        }
        catch { }
        return defaultValue;
    }

    internal static void SetInt(string key, int value)
    {
        try { _mem[key] = value.ToString(); _dirty = true; } catch { }
    }

    internal static string GetString(string key, string defaultValue = "")
    {
        try
        {
            if (_mem.TryGetValue(key, out string s)) return s;
        }
        catch { }
        return defaultValue;
    }

    internal static void SetString(string key, string value)
    {
        try { _mem[key] = value; _dirty = true; } catch { }
    }

    // ===== Flush（打烊落盘） =====
    internal static void Flush()
    {
        try
        {
            if (!_dirty) return;
            string key = ResolveKey();
            string path = FilePathFor(key);
            if (path == null) return;

            Directory.CreateDirectory(DirPath);

            var sb = new StringBuilder();
            sb.Append(HEAD_VERSION).Append('\n');
            sb.Append(HEAD_RUNID).Append(key).Append('\n');
            sb.Append('#').Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")).Append('\n');
            foreach (var kv in _mem)
            {
                sb.Append(kv.Key).Append('=').Append((kv.Value ?? "").Replace('\n', ' ')).Append('\n');
            }
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            _dirty = false;
            Core.LogMsg($"[WageSurvival.SaveStore] Flush完成: {key} ({_mem.Count}条)");
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival.SaveStore] Flush异常: " + ex.Message); }
    }

    // ===== LoadGame（读档） =====
    internal static void LoadGame()
    {
        try
        {
            string key = ResolveKey();
            string path = FilePathFor(key);
            if (path == null || !File.Exists(path))
            {
                Core.LogMsg($"[WageSurvival.SaveStore] 无存档: {key}");
                return;
            }

            string content = File.ReadAllText(path, Encoding.UTF8);
            _mem.Clear();
            // 清其他缓存（原版踩坑：切档时旧缓存残留）
            SurvivalFood.ClearMemBlood(); // 清血量缓存
            SurvivalFood._pendingSkipDays = 0; // 清跳天状态
            SurvivalFood._jumping = false;
            SurvivalFood._jumpStartDay = -1;
            string[] lines = content.Split('\n');
            foreach (string line in lines)
            {
                string t = line.TrimEnd('\r');
                if (t.Length == 0 || t[0] == '#') continue;
                int eq = t.IndexOf('=');
                if (eq <= 0) continue;
                _mem[t.Substring(0, eq)] = t.Substring(eq + 1);
            }
            Core.LogMsg($"[WageSurvival.SaveStore] LoadGame完成: {key} ({_mem.Count}条)");
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival.SaveStore] LoadGame异常: " + ex.Message); }
    }

    // ===== 初始化默认值（新档） =====
    internal static void InitDefaults()
    {
        // 只设默认值，不覆盖已有值
        if (!_mem.ContainsKey("sat")) SetInt("sat", 100);
        if (!_mem.ContainsKey("thirst")) SetInt("thirst", 100);
        if (!_mem.ContainsKey("health")) SetInt("health", 100);
        if (!_mem.ContainsKey("mood")) SetInt("mood", 50);
        if (!_mem.ContainsKey("clean")) SetInt("clean", 100);
        if (!_mem.ContainsKey("sleep")) SetInt("sleep", 100);
        if (!_mem.ContainsKey("social")) SetInt("social", 50);
        _dirty = true;
    }

    // ===== 新档Postfix（原版踩坑：StartNewGame Postfix，不是GameMaster.NewGame）=====
    internal static void PostfixStartNewGame()
    {
        try
        {
            Core.LogMsg("[WageSurvival.SaveStore] StartNewGame触发——清_mem+重置状态");
            _mem.Clear(); // 清旧档缓存
            SurvivalFood._pendingSkipDays = 0; // 清跳天状态
            SurvivalFood._jumping = false;
            SurvivalFood._jumpStartDay = -1;
            // runID还没就绪——用pending文件
            InitDefaults(); // 设默认值
            Flush(); // 写到pending文件
            Core.LogMsg("[WageSurvival.SaveStore] StartNewGame完成——新档默认值已设");
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival.SaveStore] PostfixStartNewGame异常: " + ex.Message); }
    }
}
