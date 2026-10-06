using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Il2Cpp;
using UnityEngine;

namespace WageAPI;

/// <summary>
/// 统一落盘层（2026-10-03 从 Wage's Perks 提炼，public 化）。
/// 分区读写原语 Get/Set（string/int/float/bool）+ runID 分区文件 + 落盘挂点（SaveGame/EndDay Postfix, priority=0）。
/// 所有 mod 共用同一落盘通道：各系统写内存（命名空间分区），统一 Flush 原子落盘。
/// 文件格式/路径与原 Wage's Perks 完全一致（Mods\WagesPerks\wages_data_&lt;runID&gt;.txt）——存档无缝兼容。
/// </summary>
public static class WageSaveStore
{
    private const string DIR_NAME = "WagesPerks";
    private const string FILE_PREFIX = "wages_data_";
    private const string PENDING_KEY = "pending";
    private const string HEAD_RUNID = "#runID=";
    private const string HEAD_SAVED = "#saved=";
    private const string HEAD_VERSION = "#storeVersion=1";
    private const int LOAD_DELAY_FRAMES = 30;   // 读档后延迟帧数（时序：容器/管理器未就绪）

    // ① 内存缓存 —— 唯一真相源，运行时只碰它
    private static readonly Dictionary<string, string> _mem = new Dictionary<string, string>();

    // 旧层(PlayerPrefs)迁移专用：runID 缓存（读旧 key 前缀用，切档必须刷新）
    private static string _legacyRunId = null;

    private static string _curKey = null;        // 当前文件键（runID 或 pending）
    private static bool _pendingLoad;            // 读档待加载标志（Postfix 只设它）
    private static int _pendingLoadFrames;       // 帧延迟计数
    private static bool _loadedOnce;             // 本次读档：键值文件是否已加载
    private static bool _loadingComplete = true;  // 写入门控：mod启动默认true，读档期间false，加载完true
    private static bool _dirty;                  // 有未落盘改动

    /// <summary>读档门控状态（对外只读）。</summary>
    public static bool LoadComplete => _loadingComplete;

    /// <summary>读档数据就绪事件（LoadIfPending 30 帧后触发一次）。各 mod 订阅做引用恢复/迁移。</summary>
    public static event Action GameLoaded;

    // ===================== 路径 =====================

    private static string DirPath
    {
        get
        {
            try { return Path.Combine(Application.dataPath, "..", "Mods", DIR_NAME); }
            catch { return null; }
        }
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

    // ===================== Key =====================

    // 解析当前存档标识：runID 优先；未就绪时返回 pending（避免状态落到公共键污染其他档）
    private static string ResolveKey()
    {
        try
        {
            var ps = PlayerStore.Instance;
            if (ps != null)
            {
                string rid = ps.runID;
                if (!string.IsNullOrEmpty(rid)) return Sanitize(rid);
            }
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSaveStore] 异常: " + ex.Message); }
        return PENDING_KEY;
    }

    // 文件名安全化：runID 理论上是标识符，但仍做白名单过滤防路径穿越 / 非法字符
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
        // 防超长文件名（Windows 260 路径限制）
        return s.Length > 64 ? s.Substring(0, 64) : s;
    }

    // ===================== Access（分区读写原语） =====================

    public static string GetString(string ns, string key, string def = "")
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

    public static void SetString(string ns, string key, string value)
    {
        try
        {
            if (!_loadingComplete) return; // 读档空窗期：丢弃所有写入，防默认值污染
            _mem[ns + "." + key] = value ?? "";
            _dirty = true;
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSaveStore] 异常: " + ex.Message); }
    }

    public static int GetInt(string ns, string key, int def = 0)
    {
        try
        {
            string k = ns + "." + key;
            if (_mem.TryGetValue(k, out string s) && int.TryParse(s, out int v)) return v;
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

    public static void SetInt(string ns, string key, int value)
    {
        SetString(ns, key, value.ToString());
    }

    public static float GetFloat(string ns, string key, float def = 0f)
    {
        try
        {
            string k = ns + "." + key;
            if (_mem.TryGetValue(k, out string s) && float.TryParse(s, out float v)) return v;
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

    public static void SetFloat(string ns, string key, float value)
    {
        SetString(ns, key, value.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    public static bool GetBool(string ns, string key, bool def = false)
    {
        try
        {
            string k = ns + "." + key;
            if (_mem.TryGetValue(k, out string s)) return s == "1";
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

    public static void SetBool(string ns, string key, bool value)
    {
        SetString(ns, key, value ? "1" : "0");
    }

    // 清空指定命名空间的所有键（新游戏/重置用）
    public static void ClearNamespace(string ns)
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

    public static bool HasKey(string ns, string key)
    {
        try
        {
            if (_mem.ContainsKey(ns + "." + key)) return true;
            EnsureLegacyFresh();
            return LegacyHasKey(ns, key);
        }
        catch { return false; }
    }

    // ===================== 枚举/删除（旧档迁移用，评审补丁 P1-6） =====================

    /// <summary>枚举指定命名空间下的全部完整 key（"ns.key"）。迁移用。</summary>
    public static List<string> EnumerateKeys(string ns)
    {
        var result = new List<string>();
        try
        {
            string prefix = ns + ".";
            foreach (var k in _mem.Keys)
            {
                if (k.StartsWith(prefix)) result.Add(k);
            }
        }
        catch { }
        return result;
    }

    /// <summary>删除指定完整 key（"ns.key"）。迁移清旧用。</summary>
    public static void RemoveKey(string fullKey)
    {
        try
        {
            if (_mem.Remove(fullKey)) _dirty = true;
        }
        catch { }
    }

    /// <summary>读内存快照（迁移复制用，防枚举中修改）。</summary>
    public static List<KeyValuePair<string, string>> Snapshot()
    {
        var result = new List<KeyValuePair<string, string>>();
        try { foreach (var kv in _mem) result.Add(kv); } catch { }
        return result;
    }

    // ===================== 新档 / 清理 =====================

    public static void ResetForNewRun()
    {
        try
        {
            _mem.Clear();
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

    // ===================== 序列化 =====================

    /// <summary>序列化：内存快照 → 可存储字符串（含文件头）。</summary>
    public static string Serialize()
    {
        try
        {
            var sb = new StringBuilder();
            sb.Append(HEAD_VERSION).Append('\n');
            sb.Append(HEAD_RUNID).Append(GetRunIdRaw() ?? "").Append('\n');
            sb.Append(HEAD_SAVED).Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")).Append('\n');
            foreach (var kv in _mem)
            {
                sb.Append(kv.Key).Append('=').Append((kv.Value ?? "").Replace('\n', ' ')).Append('\n');
            }
            return sb.ToString();
        }
        catch { return ""; }
    }

    /// <summary>反序列化：存储字符串 → 内存（跳过文件头注释行）。</summary>
    public static void Deserialize(string content)
    {
        try
        {
            if (string.IsNullOrEmpty(content)) return;
            string[] lines = content.Split('\n');
            foreach (string line in lines)
            {
                string t = line.TrimEnd('\r');
                if (t.Length == 0) continue;
                if (t[0] == '#') continue;
                int eq = t.IndexOf('=');
                if (eq <= 0) continue;
                _mem[t.Substring(0, eq)] = t.Substring(eq + 1);
            }
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSaveStore] 异常: " + ex.Message); }
    }

    private static void ReadInto(string path)
    {
        // 会话优先合并：加载 30 帧延迟窗口内，游戏事件可能已写入 _mem——
        // 若让文件值直接覆盖，这些新标记会被旧值顶掉。故先快照会话写入 → 读文件 → 会话值覆盖回写。
        var session = new Dictionary<string, string>(_mem);
        _mem.Clear();
        Deserialize(File.ReadAllText(path, Encoding.UTF8));
        foreach (var kv in session)
        {
            _mem[kv.Key] = kv.Value;
        }
        if (session.Count > 0) _dirty = true;
    }

    // ===================== 落盘 =====================

    /// <summary>打烊落盘（SaveGame/EndDay Postfix 调）。全量写内存快照，原子替换。</summary>
    public static void Flush()
    {
        // 读档门控守卫：LoadGame 异步（ES3 回调恢复 runID），读档未完成禁止落盘（防空数据覆盖正式档）
        if (!_loadingComplete)
        {
            try { Core.LogMsg("[SaveStore] 读档未完成，跳过落盘（防空数据覆盖正式档）"); } catch { }
            return;
        }
        try
        {
            string key = ResolveKey();
            string path = FilePathFor(key);
            if (string.IsNullOrEmpty(path)) return;

            string dir = DirPath;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            // 原子写：先写 .tmp 再替换，防强退写坏正式文件
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, Serialize(), new UTF8Encoding(false));
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);

            // 正式文件已建立 → 清掉同期的 pending 残留
            if (key != PENDING_KEY)
            {
                string pending = FilePathFor(PENDING_KEY);
                if (pending != null && File.Exists(pending)) File.Delete(pending);
            }

            _dirty = false;
            _curKey = key;
            Core.LogMsg("[SaveStore] 已落盘 " + key + "（" + _mem.Count + " 项）");
        }
        catch (Exception ex)
        {
            Core.LogMsg("[SaveStore] 落盘失败: " + ex.Message);
        }
    }

    // ===================== 校验 =====================

    public static bool ValidateRunId()
    {
        try
        {
            string key = ResolveKey();
            if (key == PENDING_KEY) return true;
            return _curKey == key;
        }
        catch { return true; }
    }

    public static bool ValidateAfterLoad()
    {
        try
        {
            if (!ValidateRunId())
            {
                Core.LogMsg("[SaveStore] 校验失败：runID 不一致（串档风险）");
                return false;
            }
            foreach (var k in _mem.Keys)
            {
                if (string.IsNullOrEmpty(k) || k.IndexOf('.') <= 0)
                {
                    Core.LogMsg("[SaveStore] 校验失败：异常 key「" + k + "」");
                    return false;
                }
            }
            return true;
        }
        catch { return false; }
    }

    public static string GetRunIdRaw()
    {
        try
        {
            var ps = PlayerStore.Instance;
            return ps != null ? (ps.runID ?? "") : "";
        }
        catch { return ""; }
    }

    // ===================== Lifecycle（读档） =====================

    /// <summary>打烊落盘（PlayerStore.SaveGame Postfix，WageAPI Core 注册，priority=0 最后跑）。</summary>
    public static void PostfixSaveGame()
    {
        Flush();
    }

    /// <summary>打烊落盘第二挂点（PlayerStore.EndDay Postfix）。原生 SaveGame 只在进游戏调一次，打烊靠此通道。</summary>
    public static void PostfixEndDay()
    {
        try { Core.LogMsg("[SaveStore] EndDay Postfix → Flush（打烊落盘）"); } catch { }
        Flush();
    }

    /// <summary>10-06 A8：小退落盘（GameMaster.QuitToMenu Postfix）。小退（不存档退出重进）不触发 SaveGame/EndDay，
    /// 状态（KEY_STOLEN 等）只存内存 → 重进丢失；QuitToMenu 时补一次 Flush（只写内存快照，不依赖 PlayerStore，安全）。</summary>
    public static void PostfixQuitToMenu()
    {
        try
        {
            if (!_dirty) return; // 无未落盘改动不写（防每次回主菜单白写）
            Core.LogMsg("[SaveStore] QuitToMenu Postfix → Flush（小退落盘）");
            Flush();
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSaveStore] QuitToMenu Flush异常: " + ex.Message); }
    }

    /// <summary>读档（PlayerStore.LoadGame Postfix 调）。键值文件同步读取，容器恢复走延迟轮询。</summary>
    public static void OnLoadGame()
    {
        try
        {
            _mem.Clear();
            _loadingComplete = false; // 读档期间禁止写入，防空窗期默认值污染存档
            _pendingLoad = true;
            _pendingLoadFrames = 0;
            _loadedOnce = false;
            TryDoLoad(); // runID 未就绪时返回 false，由轮询下帧补读
            Core.LogMsg("[SaveStore] 读档挂点触发，键值" + (_loadedOnce ? "已同步加载" : "待轮询补读"));
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSaveStore] 异常: " + ex.Message); }
    }

    /// <summary>轮询驱动（每帧调，轻量）。文件未读则下帧补读；已读则只等延迟帧驱动 GameLoaded。</summary>
    public static void LoadIfPending()
    {
        if (!_pendingLoad) return;
        try
        {
            if (!_loadedOnce)
            {
                TryDoLoad();
                if (!_loadedOnce) return;
            }
            if (++_pendingLoadFrames < LOAD_DELAY_FRAMES) return;
            _pendingLoad = false;
            // 阶段2：文件数据就绪后触发各 mod 的读档恢复（挂点在 LoadGame Postfix 之外——容器 childItems 未就绪）
            try { GameLoaded?.Invoke(); }
            catch (Exception ex2) { Core.LogMsg("[SaveStore] GameLoaded 驱动失败: " + ex2.Message); }
        }
        catch (Exception ex)
        {
            _pendingLoad = false;
            Core.LogMsg("[SaveStore] 加载失败: " + ex.Message);
        }
    }

    /// <summary>读文件（带 runID 就绪检测）。未就绪返回 false，等轮询补读。</summary>
    private static bool TryDoLoad()
    {
        try
        {
            string key = ResolveKey();
            if (key == PENDING_KEY)
            {
                return false;
            }
            string path = FilePathFor(key);
            if (path == null || !File.Exists(path))
            {
                string pending = FilePathFor(PENDING_KEY);
                if (pending != null && File.Exists(pending))
                {
                    ReadInto(pending);
                    File.Delete(pending);
                    _curKey = key;
                    _loadedOnce = true;
                    _loadingComplete = true;
                    Core.LogMsg("[SaveStore] 正式文件未建立，已从 pending 并入（" + _mem.Count + " 项）");
                    return true;
                }
                Core.LogMsg("[SaveStore] 无存档文件（新档）：" + key);
                _curKey = key;
                _loadedOnce = true;
                _loadingComplete = true;
                return true;
            }
            ReadInto(path);
            _curKey = key;
            _loadedOnce = true;
            _loadingComplete = true;
            Core.LogMsg("[SaveStore] 已加载 " + key + "（" + _mem.Count + " 项）");
            return true;
        }
        catch
        {
            return false;
        }
    }

    // ===================== Legacy（PlayerPrefs 迁移通道） =====================

    // 旧层(PerkStatePersistence)缓存 runID 可能来自上一档；兼容读取前强制刷新
    private static void EnsureLegacyFresh()
    {
        try { _legacyRunId = null; } catch { }
    }

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
        // 不缓存 default_run：PlayerStore.runID 可能在游戏开始后才赋值
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

    private static bool LegacyGetBool(string perkId, string key, bool defaultValue = false)
    {
        return LegacyGetInt(perkId, key, defaultValue ? 1 : 0) == 1;
    }

    /// <summary>新档防 default_run 残留污染：清指定特性的 default_run 旧 key。</summary>
    public static void CleanLegacyDefaultRun(string perkId, string[] keys)
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

    // ===================== 诊断 =====================

    public static int Count { get { try { return _mem.Count; } catch { return 0; } } }
    public static bool HasPendingChange { get { return _dirty; } }
}
