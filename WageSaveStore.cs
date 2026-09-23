using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Il2Cpp;
using UnityEngine;

namespace JacksonPerks;

// ============================================================
// 阶段 1：per-slot 独立文件持久化层（路线 B2）
// ============================================================
// 为什么不用 PlayerPrefs（拆包实锤）：
//   - PlayerPrefs 是 Unity 全局偏好存储，**不跟随存档文件** → 只能靠 runID 前缀做逻辑隔离，
//     时序一错就串档。当前代码已为它打了 5 套补丁（清缓存 / 双写 / 惰性迁移 / default_run 残留 / ResetCache）。
//   - 参考实现 XIAOWOTradePerks 本身就有此缺陷（cheatsheet:7519 [L1]），PerkStatePersistence 的注释明写"参考其实现"。
// 为什么不用原生存档（拆包实锤）：
//   - ModHelper.ModData 被 IL2CPP 剥离成空壳（cheatsheet:9340 [L1]）
//   - ES3 强类型，PlayerStore 字段编译期固定，无法新增自定义字段（cheatsheet:7899/7915 [L2]）
//
// 三层模式（cheatsheet:9587-9591 [L1] 已定稿）：
//   ① 运行时 Set/Get → 只碰内存字典（零 I/O，绝不立即落盘）
//   ② 打烊 PlayerStore.SaveGame Postfix → Flush() 一次性落盘
//   ③ 读档 PlayerStore.LoadGame Postfix → OnLoadGame() 只设标志位；实际加载走轮询 LoadIfPending()
//   收益：强退正确回退（内存丢了，文件里是上次打烊值）
//
// 隔离设计：文件名 = runID（存档身份）
//   - runID 是每次开档生成的唯一标识 = "存档身份"，用它命名天然按档隔离
//   - 新档 runID 不同 → 新文件 → 天然读不到旧档数据（根治 default_run 污染，cheatsheet:9558）
//   - 开局 runID 为空窗口 → 落 pending 文件，runID 就绪后自动迁移（详见 ResolveKey/MigratePending）
//
// 时序铁律（cheatsheet:9584/3756/9558）：
//   - 读档确定性挂点 = PlayerStore.LoadGame Postfix（ModHook.OnGameLoadedNormal 实测不触发）
//   - Postfix 内 childItems 尚未就绪 → 只设标志位 + 帧计数，**绝不恢复引用类型**
//   - 幂等：重复加载无害
//
// 【阶段 6 待办】错误日志目前走 Core.LogMsg（Debug 门控），发布前需改为无条件输出
// ============================================================
// 旧层迁移（2026-09-24）：PerkStatePersistence 已删除——旧档读取逻辑内联为本文件
// Legacy* 私有方法（仅"新层 miss → 读旧层 PlayerPrefs → 回填新层"迁移通道使用）。
// 业务代码禁止直调旧层；新档不产生旧层数据，旧层仅剩历史档残留。
// ============================================================
internal static class WageSaveStore
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
    private static bool _loadedOnce;             // 本次读档：键值文件是否已加载（键值不依赖容器就绪，LoadGame Postfix 即可读）
    private static bool _dirty;                  // 有未落盘改动

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

    // ===================== ② 打烊落盘 =====================

    /// <summary>序列化：内存快照 → 可存储字符串（含文件头）。Flush 与 ValidateAfterLoad 复用。</summary>
    internal static string Serialize()
    {
        try
        {
            var sb = new StringBuilder();
            sb.Append(HEAD_VERSION).Append('\n');
            sb.Append(HEAD_RUNID).Append(GetRunIdRaw() ?? "").Append('\n');
            sb.Append(HEAD_SAVED).Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")).Append('\n');
            foreach (var kv in _mem)
            {
                // key 与 value 都不含换行（value 由 SetString 保证来源可控，这里再做一次防御）
                sb.Append(kv.Key).Append('=').Append((kv.Value ?? "").Replace('\n', ' ')).Append('\n');
            }
            return sb.ToString();
        }
        catch { return ""; }
    }

    /// <summary>打烊落盘（PlayerStore.SaveGame Postfix 调）。全量写内存快照，原子替换。</summary>
    internal static void Flush()
    {
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

            // 正式文件已建立 → 清掉同期的 pending 残留（防下次读档误并入旧数据）
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

    /// <summary>全局打烊落盘（PlayerStore.SaveGame Postfix，Core.cs 注册）。
    /// 所有系统的内存状态统一落盘——不依赖任何特性是否选中，存储层自治。</summary>
    public static void PostfixSaveGame()
    {
        Flush();
    }

    // ===================== ③ 读档 =====================

    /// <summary>读档（PlayerStore.LoadGame Postfix 调）。
    /// 键值文件同步读取（键值不依赖容器就绪）——消除"清缓存→30帧后读文件"空窗口：
    /// 空窗口内 GetStat/GetBlood 会把默认值物化进缓存 → 读档状态被刷丢（蛙娘六维刷0 / 鲁滨逊血量刷满）。
    /// 容器引用恢复仍走延迟 LoadIfPending → OnGameLoaded（childItems 就绪后）。</summary>
    internal static void OnLoadGame()
    {
        try
        {
            ClearMem();
            _pendingLoad = true;
            _pendingLoadFrames = 0;
            _loadedOnce = false;
            TryDoLoad(); // runID 未就绪时返回 false，由轮询下帧补读
            Core.LogMsg("[SaveStore] 读档挂点触发，键值" + (_loadedOnce ? "已同步加载" : "待轮询补读"));
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSaveStore] 异常: " + ex.Message); }
    }

    /// <summary>轮询驱动（每帧调，轻量）。文件未读则下帧补读；已读则只等延迟帧驱动 OnGameLoaded。</summary>
    internal static void LoadIfPending()
    {
        if (!_pendingLoad) return;
        try
        {
            if (!_loadedOnce)
            {
                TryDoLoad(); // runID 补就绪后读文件（空窗口 ≤1 帧）
                if (!_loadedOnce) return;
            }
            if (++_pendingLoadFrames < LOAD_DELAY_FRAMES) return;
            _pendingLoad = false;
            // 阶段2：文件数据就绪后驱动全部特性 OnGameLoaded。
            // 挂点必须在**这里**而不是 PlayerStore.LoadGame Postfix —— 后者执行时容器 childItems 尚未就绪，
            // 直接恢复引用类型必失败（阶段1 已用血泪验证，见 WageSaveStore.cs 顶部时序说明）。
            try { CustomStartingPerks.NotifyGameLoaded(); }
            catch (Exception ex2) { Core.LogMsg("[SaveStore] OnGameLoaded 驱动失败: " + ex2.Message); }
        }
        catch (Exception ex)
        {
            _pendingLoad = false;
            Core.LogMsg("[SaveStore] 加载失败: " + ex.Message);
        }
    }

    /// <summary>读文件（带 runID 就绪检测）。runID 未就绪（LoadGame 早期）返回 false，等轮询补读；
    /// 就绪则加载文件并标记 _loadedOnce。返回是否本次完成加载。</summary>
    private static bool TryDoLoad()
    {
        try
        {
            string key = ResolveKey();
            if (key == PENDING_KEY)
            {
                // runID 尚未就绪（LoadGame 存档头解析中）→ 放弃本次，等轮询下帧重试。
                // 不能读 pending 文件：pending 是"开局 runID 空窗期"数据，读档场景下可能属旧会话残留。
                return false;
            }
            string path = FilePathFor(key);
            if (path == null || !File.Exists(path))
            {
                // 正式文件未建立：可能是开局 runID 空窗期写入的 pending 数据 → 并入（同档早期数据不丢）
                string pending = FilePathFor(PENDING_KEY);
                if (pending != null && File.Exists(pending))
                {
                    ReadInto(pending);
                    File.Delete(pending);   // 并入后清理，避免下次重复并入
                    _curKey = key;
                    _loadedOnce = true;
                    Core.LogMsg("[SaveStore] 正式文件未建立，已从 pending 并入（" + _mem.Count + " 项）");
                    DumpForDiagnostics();
                    return true;
                }
                Core.LogMsg("[SaveStore] 无存档文件（新档）：" + key);
                _curKey = key;
                _loadedOnce = true;
                return true;
            }
            ReadInto(path);
            _curKey = key;
            _loadedOnce = true;
            Core.LogMsg("[SaveStore] 已加载 " + key + "（" + _mem.Count + " 项）");
            DumpForDiagnostics();
            return true;
        }
        catch
        {
            return false;
        }
    }

    // 【开发诊断 · 发布前删】输出已加载键值——用于验证"六维/好感是否恢复"
    private static void DumpForDiagnostics()
    {
        try
        {
            if (_mem.Count == 0) return;
            var sb = new StringBuilder("[SaveStore] 内容: ");
            bool first = true;
            foreach (var kv in _mem)
            {
                if (!first) sb.Append(", ");
                sb.Append(kv.Key).Append('=').Append(kv.Value);
                first = false;
            }
            Core.LogMsg(sb.ToString());
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSaveStore] 异常: " + ex.Message); }
    }

    private static void ReadInto(string path)
    {
        // 会话优先合并：加载 30 帧延迟窗口内，游戏事件（BeginDay/收租/霉运防重标记等）
        // 可能已写入 _mem——若让文件值直接覆盖，这些新标记会被旧值顶掉，当天事件重复触发。
        // 故先快照会话写入 → 读文件 → 会话值覆盖回写（会话比文件新）。
        var session = new Dictionary<string, string>(_mem);
        _mem.Clear();
        Deserialize(File.ReadAllText(path, Encoding.UTF8));
        foreach (var kv in session)
        {
            _mem[kv.Key] = kv.Value;
        }
        if (session.Count > 0) _dirty = true;
    }

    /// <summary>反序列化：存储字符串 → 内存（跳过文件头注释行）。ReadInto 与外部调用复用。</summary>
    internal static void Deserialize(string content)
    {
        try
        {
            if (string.IsNullOrEmpty(content)) return;
            string[] lines = content.Split('\n');
            foreach (string line in lines)
            {
                string t = line.TrimEnd('\r');
                if (t.Length == 0) continue;
                if (t[0] == '#') continue;              // 文件头注释
                int eq = t.IndexOf('=');
                if (eq <= 0) continue;
                _mem[t.Substring(0, eq)] = t.Substring(eq + 1);
            }
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSaveStore] 异常: " + ex.Message); }
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

    // ===================== 校验 =====================

    /// <summary>读档后自检：本次加载的 runID 是否与当前一致（防串档）。</summary>
    internal static bool ValidateRunId()
    {
        try
        {
            string key = ResolveKey();
            if (key == PENDING_KEY) return true;        // 空窗期不校验
            return _curKey == key;
        }
        catch { return true; }
    }

    /// <summary>读档后自检：runID 一致性（防串档）+ key 格式完整性。读档加载完成后调。</summary>
    internal static bool ValidateAfterLoad()
    {
        try
        {
            // ① runID 一致性：当前加载的文件键必须等于当前 runID
            if (!ValidateRunId())
            {
                Core.LogMsg("[SaveStore] 校验失败：runID 不一致（串档风险）");
                return false;
            }
            // ② key 格式完整性：必须是 "命名空间.键" 形式（如 "wage_girl.affection"）
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

    internal static string GetRunIdRaw()
    {
        try
        {
            var ps = PlayerStore.Instance;
            return ps != null ? (ps.runID ?? "") : "";
        }
        catch { return ""; }
    }

    // 【开发诊断】当前内存项数 —— 供 Debug 面板 / 日志使用
    internal static int Count { get { try { return _mem.Count; } catch { return 0; } } }
    internal static bool HasPendingChange { get { return _dirty; } }
}
