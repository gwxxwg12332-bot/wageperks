using Il2Cpp;
using HarmonyLib;
using UnityEngine;

namespace JacksonPerks;

/// <summary>
/// 兼容层兜底 patch（P0）：
/// 1. 价格兜底：最终价格 < 0 强制改成 0
/// 2. 预算兜底：原预算 > 0，最终预算 ≤ 0 时恢复原预算
/// 3. 声望兜底：声望倍率 < 0.1 时 clamp 到 0.1
/// </summary>
internal static class CompatibilityPatches
{
    // ===== 1. 价格兜底 =====
    // GameItem.GetNegociatedValue Postfix（最后执行 priority=0）
    private static float _zeroBuyDiagTime = 0f; // 0元购诊断节流（发布前删）
    public static void PostfixGetNegociatedValue(GameItem __instance, ref long __result)
    {
        try
        {
            if (!BuildConfig.CompatTradeClamp) return;
            if (__result < 0)
            {
                Core.LogMsg("[兼容] 价格兜底: " + __result + " → 0 (" + __instance.identifier + ")");
                __result = 0;
            }
            // 09-26 P2-10 诊断：成交价 ≤ 0 时打三方值上下文（0元购根因实测；预算/现金见成交日志）
            if (__result <= 0 && UnityEngine.Time.time - _zeroBuyDiagTime > 5f)
            {
                _zeroBuyDiagTime = UnityEngine.Time.time;
                string cli = "?";
                try { var ps = Il2Cpp.PlayerStore.Instance; if (ps != null && ps.currentClientInstance != null) cli = ps.currentClientInstance.GetClientBlueprint().identifier; } catch { }
                Core.LogMsg("[0元购诊断] item=" + (__instance.identifier ?? "?") + " negVal=" + __result + " client=" + cli);
            }
        } catch { }
    }

    // ===== 2. 预算兜底 =====
    // StoreClient.ApplyBudgetModifier Prefix（存原预算）
    private static readonly System.Collections.Generic.Dictionary<StoreClient, int> _origBudget = new();

    public static void PrefixApplyBudgetModifier(StoreClient __instance)
    {
        try
        {
            if (__instance == null) return;
            _origBudget[__instance] = __instance.clientBudget;
        } catch { }
    }

    // StoreClient.ApplyBudgetModifier Postfix（检查并恢复）
    public static void PostfixApplyBudgetModifier(StoreClient __instance)
    {
        try
        {
            if (__instance == null) return;
            if (!_origBudget.TryGetValue(__instance, out int orig)) return;
            _origBudget.Remove(__instance);

            if (!BuildConfig.CompatBudgetRestore) return;
            if (orig > 0 && __instance.clientBudget <= 0)
            {
                Core.LogMsg("[兼容] 预算兜底: " + __instance.clientBudget + " → " + orig + " (客户=" + __instance.identifier + ")");
                __instance.clientBudget = orig;
            }
        } catch { }
    }

    // ===== 3. 声望兜底 =====
    // BargainUIManager.ComputeTradeRepMultipliers Postfix（最后执行 priority=0）
    public static void PostfixTradeRepMultipliers(ref Il2CppSystem.ValueTuple<double, double> __result)
    {
        try
        {
            if (!BuildConfig.CompatRepClamp) return;
            double sell = __result.Item1;
            double buy = __result.Item2;
            bool changed = false;
            if (sell < 0.1) { sell = 0.1; changed = true; }
            if (buy < 0.1) { buy = 0.1; changed = true; }
            if (changed)
            {
                Core.LogMsg("[兼容] 声望倍率兜底: (" + __result.Item1 + "," + __result.Item2 + ") → (" + sell + "," + buy + ")");
                __result = new Il2CppSystem.ValueTuple<double, double>(sell, buy);
            }
        } catch { }
    }
}
