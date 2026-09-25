using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Il2Cpp;
using UnityEngine;

namespace JacksonPerks;
partial class WageSaveStore

{
    // ===== Key =====

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
}
