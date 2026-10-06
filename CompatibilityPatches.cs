using Il2Cpp;
using HarmonyLib;
using UnityEngine;

namespace WagePerks;

/// <summary>
/// 兼容层兜底 patch（P0）：
/// 1. 价格兜底：最终价格 < 0 强制改成 0
/// 2. 预算兜底：原预算 > 0，最终预算 ≤ 0 时恢复原预算
/// （声望兜底已删：ref ValueTuple Postfix 绑定错位读幻觉，破坏原生议价倍率——见 cheatsheet L1 铁律）
/// </summary>
internal static class CompatibilityPatches
{
    // ===== 1. 价格兜底 =====
    // GameItem.GetNegociatedValue Postfix（最后执行 priority=0）
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
        } catch { }
    }

    // ===== 2. 预算兜底 =====
    // StoreClient.ApplyBudgetModifier Prefix（存原预算）
    private static readonly System.Collections.Generic.Dictionary<StoreClient, int> _origBudget = new();

    // 10-05 第三方溢出兜底：CustomerCreditBoost 的 TrackClient Postfix → BoostClient → OverrideBudget(num4)
    // （预算×(100+num3)/100 指数累乘 → int 溢出写负，原版 OverrideBudget 零 clamp 直写字段）
    // → 挂 OverrideBudget Postfix 源头 clamp：任何调用（我方+第三方）写入前夹到 [0, 21.47亿]
    public static void PostfixOverrideBudget(StoreClient __instance)
    {
        try
        {
            if (__instance == null) return;
            // 10-06 加强：蛙哥预算专用 clamp [0,1000]——CustomerCreditBoost TrackClient 指数累乘
            //   爆正（21亿）/溢出负都会覆盖蛙哥设计预算（工厂 100 / 收购扩展 1000）→ 恢复设计值，交易与 UI 正常
            if (__instance.identifier == "wage_brother")
            {
                if (__instance.clientBudget < 0 || __instance.clientBudget > 1000)
                {
                    Core.LogMsg("[兼容] 蛙哥预算兜底: " + __instance.clientBudget + " → 1000");
                    __instance.clientBudget = 1000;
                }
                return;
            }
            if (__instance.clientBudget < 0)
            {
                Core.LogMsg("[兼容] 预算溢出兜底: " + __instance.clientBudget + " → 0 (客户=" + __instance.identifier + ")");
                __instance.clientBudget = 0;
            }
            else if (__instance.clientBudget > 2147483646)
            {
                Core.LogMsg("[兼容] 预算上限兜底: " + __instance.clientBudget + " → 2147483646");
                __instance.clientBudget = 2147483646;
            }
        } catch { }
    }

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
            // 10-02 补：基础预算本身≤0的客户，加下限0
            if (__instance.clientBudget < 0) __instance.clientBudget = 0;
        } catch { }
    }
}
