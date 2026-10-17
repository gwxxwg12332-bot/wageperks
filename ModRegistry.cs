using System;
using System.Collections.Generic;

namespace WageAPI;

/// <summary>mod 注册信息（发现机制核心）。</summary>
public class ModInfo
{
    public string ModId = "";          // 唯一 ID（如 "WageSurvival"）
    public string Assembly = "";       // 程序集名（如 "WageSurvival.dll"）
    public string Version = "0.0.0";   // SemVer
    public string[] DependsOn = Array.Empty<string>();    // 依赖声明（如 "WageAPI:[1.0,2.0)"）
    public string[] Capabilities = Array.Empty<string>(); // 能力声明（如 "survival.nodes"）
}

/// <summary>
/// 发现机制（2026-10-03 新建）：Register/HasCapability/TryGetMod/依赖校验/同 ID 多版本告警。
/// 延迟绑定：运行期查 ModRegistry 取能力，不依赖 MelonLoader 加载顺序（架构决策 ⑥）。
/// </summary>
public static class ModRegistry
{
    private static readonly Dictionary<string, ModInfo> _mods = new Dictionary<string, ModInfo>(StringComparer.OrdinalIgnoreCase);
    private static readonly object _lock = new object();

    /// <summary>注册 mod 身份/版本/依赖/能力。同 ID 重复注册 → 多版本告警日志。</summary>
    public static void Register(ModInfo info)
    {
        if (info == null || string.IsNullOrEmpty(info.ModId)) return;
        lock (_lock)
        {
            if (_mods.TryGetValue(info.ModId, out var existing))
            {
                if (existing.Version != info.Version)
                {
                    Core.LogMsg($"[ModRegistry] 告警：ModId「{info.ModId}」多版本共存——已注册 {existing.Version}，新注册 {info.Version}（同 ID 多版本 DLL 检测）");
                }
                _mods[info.ModId] = info; // 后者覆盖（同进程最新为准）
                return;
            }
            _mods[info.ModId] = info;
            Core.LogMsg($"[ModRegistry] 已注册：{info.ModId} v{info.Version}（能力: {string.Join(",", info.Capabilities)}）");
        }
    }

    /// <summary>是否有 mod 声明了该能力。</summary>
    public static bool HasCapability(string capability)
    {
        if (string.IsNullOrEmpty(capability)) return false;
        lock (_lock)
        {
            foreach (var m in _mods.Values)
            {
                foreach (var c in m.Capabilities)
                {
                    if (string.Equals(c, capability, StringComparison.OrdinalIgnoreCase)) return true;
                }
            }
        }
        return false;
    }

    /// <summary>按 ModId 查注册信息。</summary>
    public static bool TryGetMod(string modId, out ModInfo info)
    {
        lock (_lock)
        {
            return _mods.TryGetValue(modId ?? "", out info);
        }
    }

    /// <summary>全部已注册 mod。</summary>
    public static ModInfo[] GetAllMods()
    {
        lock (_lock)
        {
            var list = new List<ModInfo>(_mods.Values);
            return list.ToArray();
        }
    }

    /// <summary>
    /// 依赖校验：检查 DependsOn 声明（格式 "ModId:[minVer,maxVer)" 或 "ModId:minVer"）。
    /// 返回 null = 全部满足；否则返回缺失/版本不满足项描述。
    /// </summary>
    public static string CheckDependencies(ModInfo info)
    {
        if (info == null || info.DependsOn == null) return null;
        foreach (var dep in info.DependsOn)
        {
            if (string.IsNullOrWhiteSpace(dep)) continue;
            string depId = dep;
            string range = null;
            int colon = dep.IndexOf(':');
            if (colon > 0)
            {
                depId = dep.Substring(0, colon).Trim();
                range = dep.Substring(colon + 1).Trim();
            }
            if (!_mods.TryGetValue(depId, out var m))
            {
                return $"依赖缺失：{dep}（未注册）";
            }
            if (!string.IsNullOrEmpty(range) && !VersionInRange(m.Version, range))
            {
                return $"依赖版本不满足：{dep}（实际 {m.Version}）";
            }
        }
        return null;
    }

    /// <summary>SemVer 区间判断（支持 "[a,b)" "a" ">=a" 简化形式）。</summary>
    private static bool VersionInRange(string ver, string range)
    {
        try
        {
            var v = new Version(ver.Split('-')[0]);
            range = range.Trim();
            if (range.StartsWith("["))
            {
                int comma = range.IndexOf(',');
                if (comma > 0)
                {
                    string minS = range.Substring(1, comma - 1).Trim();
                    string maxS = range.Substring(comma + 1).TrimEnd(')');
                    var min = string.IsNullOrEmpty(minS) ? null : new Version(minS.Split('-')[0]);
                    var max = string.IsNullOrEmpty(maxS) ? null : new Version(maxS.Split('-')[0]);
                    if (min != null && v < min) return false;
                    if (max != null && v >= max) return false;
                    return true;
                }
            }
            if (range.StartsWith(">="))
            {
                return v >= new Version(range.Substring(2).Trim().Split('-')[0]);
            }
            if (range.StartsWith(">"))
            {
                return v > new Version(range.Substring(1).Trim().Split('-')[0]);
            }
            return v.ToString() == range.Split('-')[0];
        }
        catch { return true; } // 解析失败不阻断（宽容判定，仅告警用）
    }
}
