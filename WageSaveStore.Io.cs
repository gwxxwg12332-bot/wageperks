using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Il2Cpp;
using UnityEngine;

namespace JacksonPerks;
partial class WageSaveStore

{
    // ===== Io =====

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
}
