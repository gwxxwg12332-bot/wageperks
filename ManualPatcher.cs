using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MelonLoader;

namespace JacksonPerks;

// 手动Patch基础类（防御性Patch模式，参考 AugPresenceGuard 27.4/27.6 经验）
// 不用[HarmonyPatch] attribute，目标方法不存在时打日志继续，不崩溃
// 通用化：patchHost 参数支持任意类作为 Patch 宿主（默认 Patches，不再硬编码）
//
// ⚠️ 设计原则（2026-09-23 明确）：本类**只负责挂载**，刻意不含任何"冲突检测/主动让路/拦截"逻辑。
// 历史事故：旧版曾用 IsConflictOwned 无差别拦截"已被其他 mod patch"的方法，
// 结果常驻调试工具先挂过 PlayerStore.LoadGame 后，我们自己的读档恢复链被整个跳过
// （表现为面板/吃喝失效、房租计制失效）。Harmony 允许不同 owner 的补丁共存，
// **拦截别人 = 同时废掉自己**。要保证功能生效，就不能让路。
internal static class ManualPatcher
{
    private static HarmonyLib.Harmony _harmony;

    // ===== 补丁挂载自检（2026-09-23）=====
    // 目的：保证"我们的功能确实生效"。挂载失败原本是静默的（只打零散日志），
    // 游戏更新导致方法名变化时功能会无声失效、很难发现。这里做计数 + 收尾汇总，
    // 让"哪些补丁没挂上"一眼可见。**纯日志，不改变任何挂载行为。**
    internal static int PatchOkCount { get; private set; }
    internal static int PatchFailCount { get; private set; }
    private static readonly System.Collections.Generic.List<string> _patchFailDetails
        = new System.Collections.Generic.List<string>();

    private static void MarkOk() { PatchOkCount++; }

    private static void MarkFail(string what, string why)
    {
        PatchFailCount++;
        if (_patchFailDetails.Count < 60)
            _patchFailDetails.Add(what + (string.IsNullOrEmpty(why) ? "" : "  —  " + why));
    }

    /// <summary>所有补丁注册跑完后调用一次，输出挂载汇总。失败项即"功能不会生效"的清单。</summary>
    internal static void LogPatchSummary()
    {
        try
        {
            Core.LogMsg($"[Patch自检] 补丁挂载: 成功 {PatchOkCount} / 失败 {PatchFailCount}");
            if (PatchFailCount == 0)
            {
                Core.LogMsg("[Patch自检] 全部补丁已应用 ✅");
                return;
            }
            Core.LogMsg("[Patch自检] ===== 以下补丁未应用（对应功能不会生效）=====");
            foreach (string d in _patchFailDetails) Core.LogMsg("[Patch自检]   " + d);
        }
        catch { }
    }

    internal static void Init(HarmonyLib.Harmony harmony)
    {
        _harmony = harmony;
    }

    // 尝试Patch一个方法（默认宿主类 Patches，可用 patchHost 指定其他类）
    // priority：可选 Harmony 优先级（默认 400）。postfix 高优先级先跑；
    // 需要"所有系统写完后再执行"的全局门面（如统一落盘 Flush）应传低优先级（如 0）保证最后跑
    internal static void TryPatch(Type type, string name,
        string prefix = null, string postfix = null,
        Type[] parameterTypes = null, Type patchHost = null, int? priority = null)
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
                MarkFail($"{type.Name}.{name}", "方法未找到");
                return;
            }

            HarmonyMethod hmPrefix = prefix == null ? null : new HarmonyMethod(host, prefix);
            HarmonyMethod hmPostfix = postfix == null ? null : new HarmonyMethod(host, postfix);
            if (priority.HasValue)
            {
                if (hmPrefix != null) hmPrefix.priority = priority.Value;
                if (hmPostfix != null) hmPostfix.priority = priority.Value;
            }
            _harmony.Patch(method, prefix: hmPrefix, postfix: hmPostfix);
            MarkOk();
        }
        catch (Exception ex)
        {
            Core.LogMsg($"[Patch失败] {type.Name}.{name}: {ex.Message}");
            MarkFail($"{type.Name}.{name}", ex.Message);
        }
    }

    // 按方法名精确挂载（兜底：私有带参方法 AccessTools.Method 带参重载在 Il2Cpp 互操作下
    // 参数类型比较可能失败（如 BargainUIManager 的议价声誉倍率方法），按名+首匹配直接挂；方法名须唯一）
    // ⚠️ 用本方法做兜底时，方法名必须与**当前游戏版本**一致——游戏更新会改名
    //   （实例：ComputeTradeRepMultiplier → ComputeTradeRepMultipliers）。
    //   失效检测手段：启动后看 [Patch自检] 汇总，或用 tools/check_patches.py 离线核对。
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
                MarkFail($"{type.Name}.{name}（按名）", "方法未找到");
                return;
            }
            _harmony.Patch(method,
                prefix: prefix == null ? null : new HarmonyMethod(host, prefix),
                postfix: postfix == null ? null : new HarmonyMethod(host, postfix));
            MarkOk();
        }
        catch (Exception ex)
        {
            Core.LogMsg($"[Patch失败] {type.Name}.{name}: {ex.Message}");
            MarkFail($"{type.Name}.{name}（按名）", ex.Message);
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
                    _harmony.Patch(m,
                        prefix: prefix == null ? null : new HarmonyMethod(host, prefix),
                        postfix: postfix == null ? null : new HarmonyMethod(host, postfix));
                    count++;
                }
                catch (Exception ex)
                {
                    Core.LogMsg($"[Patch失败] {type.Name}.{name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name))}): {ex.Message}");
                    MarkFail($"{type.Name}.{name}（重载）", ex.Message);
                }
            }
            if (count > 0) { PatchOkCount += count; }
            else
            {
                Core.LogMsg($"[Patch失败] {type.Name}.{name} 无匹配重载");
                MarkFail($"{type.Name}.{name}（重载）", "无匹配重载");
            }
        }
        catch (Exception ex)
        {
            Core.LogMsg($"[Patch失败] {type.Name}.{name}: {ex.Message}");
            MarkFail($"{type.Name}.{name}（重载）", ex.Message);
        }
    }
}
