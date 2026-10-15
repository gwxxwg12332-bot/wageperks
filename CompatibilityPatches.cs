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

    // FIX14：蛙哥预算常量（与 WageBrother.cs:219 / PatchRegistry.cs:54 的 SetBudget(1109707341, 10000000) 第一参数同值收口）
    //   游戏程序集签名：SetBudget(int budget, int budgetConversion)——第一参数=预算，第二=conversion（10-15 玩家拍板=10000000）
    //   设计目标（玩家拍板 10-15）：蛙哥每次到店初始预算固定 1,109,707,341；成交后允许正常扣减（ModBudget 直减字段）；
    //   下次到店再恢复初始值。旧"任何 >1000 改回 1000"兜底与设计值直接冲突（会把 1.1B 压成 1000）→ 已废。
    private const int WAGE_BROTHER_BUDGET = 1_109_707_341;

    // 10-05 第三方溢出兜底：CustomerCreditBoost 的 TrackClient Postfix → BoostClient → OverrideBudget(num4)
    // （预算×(100+num3)/100 指数累乘 → int 溢出写负，原版 OverrideBudget 零 clamp 直写字段）
    // → 挂 OverrideBudget Postfix 源头 clamp：任何调用（我方+第三方）写入前夹到 [0, 21.47亿]
    public static void PostfixOverrideBudget(StoreClient __instance)
    {
        try
        {
            if (__instance == null) return;
            // FIX14：蛙哥分支——只防异常（溢出写负 / 覆盖型爆正），恢复初始预算；
            //   不再压 >1000（旧逻辑与 1,109,707,341 设计值冲突）；成交扣减正常递减，不强制补满
            //   FIX15：补正溢出防护——>初始值+5% 视为第三方覆盖型爆正（指数累乘等），恢复初始
            if (__instance.identifier == "wage_brother")
            {
                if (__instance.clientBudget < 0 || __instance.clientBudget > WAGE_BROTHER_BUDGET + WAGE_BROTHER_BUDGET / 20)
                {
                    Core.LogMsg("[兼容] 蛙哥预算异常恢复: " + __instance.clientBudget + " → " + WAGE_BROTHER_BUDGET);
                    __instance.clientBudget = WAGE_BROTHER_BUDGET;
                }
                return;
            }
            if (__instance.clientBudget < 0)
            {
                Core.LogMsg("[兼容] 预算溢出兜底: " + __instance.clientBudget + " → 0 (客户=" + __instance.identifier + ")");
                __instance.clientBudget = 0;
            }
            // 10-10 #35 P0-1：上限 21.47 亿→1000 万（与 Npc.Client.cs:117 BUDGET_CAP 同值收口，58/117/WageTrade 三处同值）
            //   叠加 mod 再乘溢出为负→不买东西（10-05 负预算 L1 第二根因，cheatsheet:6330-6334）——上限收在业务值防溢出
            else if (__instance.clientBudget > 10_000_000)
            {
                Core.LogMsg("[兼容] 预算上限兜底: " + __instance.clientBudget + " → 10000000 (客户=" + __instance.identifier + ")");
                __instance.clientBudget = 10_000_000;
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
