using HarmonyLib;
using System;
using System.Reflection;

namespace JacksonPerks;

// Harmony patch 统一注册 + 冲突自动检测
internal static class HookRegistry
{
    private static HarmonyLib.Harmony _harmony;

    public static void Init(string id)
    {
        try { _harmony = new HarmonyLib.Harmony(id); } catch { }
    }

    public static void Register(MethodBase method, string owner,
        HarmonyMethod prefix = null, HarmonyMethod postfix = null)
    {
        try
        {
            if (_harmony == null) return;
            var info = HarmonyLib.Harmony.GetPatchInfo(method);
            if (info != null && info.Owners != null)
            {
                foreach (var o in info.Owners)
                {
                    if (o != owner && o != "WagesPerks")
                    {
                        Core.LogMsg($"[Hook] {method.Name} 已被 {o} patch，跳过");
                        return;
                    }
                }
            }
            _harmony.Patch(method, prefix, postfix);
        }
        catch (Exception ex)
        {
            Core.LogMsg($"[Hook] {method.Name} 失败: {ex.Message}");
        }
    }
}