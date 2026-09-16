using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MelonLoader;

namespace JacksonPerks;

// 手动Patch基础类（防御性Patch模式，参考 AugPresenceGuard 27.4/27.6 经验）
// 不用[HarmonyPatch] attribute，目标方法不存在时打日志继续，不崩溃
// 通用化：patchHost 参数支持任意类作为 Patch 宿主（默认 Patches，不再硬编码）
// 挂载成功/失败都有日志（开发版可见，排查挂载问题一清二楚）
internal static class ManualPatcher
{
    private static HarmonyLib.Harmony _harmony;

    internal static void Init(HarmonyLib.Harmony harmony)
    {
        _harmony = harmony;
    }

    // 尝试Patch一个方法（默认宿主类 Patches，可用 patchHost 指定其他类）
    internal static void TryPatch(Type type, string name,
        string prefix = null, string postfix = null,
        Type[] parameterTypes = null, Type patchHost = null)
    {
        Type host = patchHost ?? typeof(Patches);
        try
        {
            MethodBase method = parameterTypes != null
                ? AccessTools.Method(type, name, parameterTypes)
                : AccessTools.Method(type, name);

            if (method == null)
            {
                Core.LogMsg($"[Patch失败] {type.Name}.{name} 未找到，补丁未应用");
                return;
            }

            ApplyPatch(method, host, prefix, postfix, $"{type.Name}.{name}");
        }
        catch (Exception ex)
        {
            Core.LogMsg($"[Patch失败] {type.Name}.{name}: {ex.Message}");
        }
    }

    // 按方法名精确挂载（兜底：私有带参方法 AccessTools.Method 带参重载在 Il2Cpp 互操作下
    // 参数类型比较可能失败（如 BargainUIManager.ComputeTradeRepMultiplier），按名+首匹配直接挂；方法名须唯一）
    internal static void TryPatchByName(Type type, string name,
        string prefix = null, string postfix = null, Type patchHost = null)
    {
        Type host = patchHost ?? typeof(Patches);
        try
        {
            MethodBase method = null;
            foreach (MethodInfo mi in AccessTools.GetDeclaredMethods(type))
            {
                if (mi.Name == name) { method = mi; break; }
            }
            if (method == null)
            {
                Core.LogMsg($"[Patch失败] {type.Name}.{name} 按名未找到，补丁未应用");
                return;
            }
            ApplyPatch(method, host, prefix, postfix, $"{type.Name}.{name}");
        }
        catch (Exception ex)
        {
            Core.LogMsg($"[Patch失败] {type.Name}.{name}: {ex.Message}");
        }
    }

    // 尝试Patch所有同名方法（重载）
    internal static void TryPatchAllOverloads(Type type, string name,
        string prefix = null, string postfix = null, Type patchHost = null)
    {
        Type host = patchHost ?? typeof(Patches);
        try
        {
            int count = 0;
            foreach (MethodInfo m in AccessTools.GetDeclaredMethods(type))
            {
                if (m.Name != name) continue;
                try
                {
                    ApplyPatch(m, host, prefix, postfix, $"{type.Name}.{name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name))})");
                    count++;
                }
                catch (Exception ex)
                {
                    Core.LogMsg($"[Patch失败] {type.Name}.{name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name))}): {ex.Message}");
                }
            }
            if (count > 0) { }
            else
                Core.LogMsg($"[Patch失败] {type.Name}.{name} 无匹配重载");
        }
        catch (Exception ex)
        {
            Core.LogMsg($"[Patch失败] {type.Name}.{name}: {ex.Message}");
        }
    }

    // ===== 兼容层（09-16 玩家 15 mods 崩溃根因）：Harmony 双 detour 防崩 =====
    // 背景：EmptyNukeBarrel_Rare 先 patch 了与本 mod 重叠的方法（ScavengeDumpingGrounds /
    // GetRandomScavengedItem / 目录 InitDirectory），本 mod 后加载再 detour 同一方法 →
    // MonoMod 复制已被替换的原方法 → CLR fatal（try/catch 拦不住，进程级崩溃）。
    // 方案：patch 前查 Harmony.GetPatchInfo，若该方法已被其他 owner patch → 跳过（功能降级不崩）。

    // 冲突检测：目标方法 patch 列表里是否存在非本 mod 的 owner（有 → 跳过，返回 true）
    private static bool IsConflictOwned(MethodBase method, out string owners)
    {
        owners = "";
        try
        {
            HarmonyLib.Patches info = HarmonyLib.PatchProcessor.GetPatchInfo(method);
            if (info == null) return false;
            var list = new System.Collections.Generic.HashSet<string>();
            if (info.Prefixes != null)
                foreach (var p in info.Prefixes)
                    if (!string.Equals(p.owner, _harmony.Id, StringComparison.OrdinalIgnoreCase)
                        && !ModCompat.IsSafeCoexistOwner(p.owner))
                        list.Add(p.owner);
            if (info.Postfixes != null)
                foreach (var p in info.Postfixes)
                    if (!string.Equals(p.owner, _harmony.Id, StringComparison.OrdinalIgnoreCase)
                        && !ModCompat.IsSafeCoexistOwner(p.owner))
                        list.Add(p.owner);
            if (list.Count == 0) return false;
            owners = string.Join(", ", list);
            return true;
        }
        catch
        {
            // GetPatchInfo 异常 = Harmony 状态异常，后续 Patch 大概率也崩 → 防崩优先，保守跳过
            owners = "(GetPatchInfo异常)";
            return true;
        }
    }

    // 统一挂载：前置日志（崩溃时最后一条 = 真凶）→ 主动让路（ModCompat 已知冲突表）→ 冲突检测 → Patch
    private static void ApplyPatch(MethodBase method, Type host, string prefix, string postfix, string label)
    {
        Core.LogMsg($"[Patch] {label} 开始");
        // 09-19 主动让路：已知冲突表命中 + 对方 mod 已加载 → 提前跳过（防"我们先挂、对方后挂"崩；不等运行时 GetPatchInfo）
        if (ModCompat.ShouldYield(method.DeclaringType, method.Name, out string compatMod))
        {
            Core.LogMsg($"[Patch让路-兼容] {label} 检测到「{compatMod}」已加载，主动让路（功能降级回原生，防 Harmony 双 detour 崩溃）");
            return;
        }
        if (IsConflictOwned(method, out string owners))
        {
            Core.LogMsg($"[Patch跳过-冲突] {label} 已被其他mod patch（owner: {owners}），跳过避免 Harmony 双 detour 崩溃");
            return;
        }
        _harmony.Patch(method,
            prefix: prefix == null ? null : new HarmonyMethod(host, prefix),
            postfix: postfix == null ? null : new HarmonyMethod(host, postfix));
    }
}
