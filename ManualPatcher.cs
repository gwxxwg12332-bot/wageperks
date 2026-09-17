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

            _harmony.Patch(method,
                prefix: prefix == null ? null : new HarmonyMethod(host, prefix),
                postfix: postfix == null ? null : new HarmonyMethod(host, postfix));
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
            _harmony.Patch(method,
                prefix: prefix == null ? null : new HarmonyMethod(host, prefix),
                postfix: postfix == null ? null : new HarmonyMethod(host, postfix));
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
                    _harmony.Patch(m,
                        prefix: prefix == null ? null : new HarmonyMethod(host, prefix),
                        postfix: postfix == null ? null : new HarmonyMethod(host, postfix));
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
}
