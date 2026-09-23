using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace JacksonPerks;

public static partial class WageGirlSystem
{
    public static void PostfixGetDealMakerBonus(ref int __result)
    {
        try
        {
            if (!Exists()) return; // 蛙娘未出现 → 无增益
            __result += 50;
            if (__result > 100) __result = 100;
        }
        catch (System.Exception ex) { Core.LogMsg("[WageGirlSystem.Budget] 异常: " + ex.Message); }
    }
    public static void PostfixApplyBudgetModifier(StoreClient __instance)
    {
        try
        {
            if (__instance == null) return;
            if (!Exists()) return; // 蛙娘未出现 → 无增益
            if (__instance.identifier == ENTITY_ID) return; // 蛙娘自己不是客户时不受益
            if (Patches._inBudgetOverride) return; // 防重入：鲁滨逊 SetBudget 会再次触发 ApplyBudgetModifier → 本条 Postfix 重入（倍率嵌套）
            int budget = __instance.GetBudget();
            // 【开发诊断 · 发布前删】观测 Postfix 读到的原生最终预算（节流 5s）
            if (Time.time - _budgetDiagTime > 5f)
            {
                _budgetDiagTime = Time.time;
                Core.LogMsg("[预算诊断] Postfix触发 client=" + (__instance.identifier ?? "?") + " GetBudget=" + budget + " useClientBudget=" + __instance.useClientBudget + " clientCash=" + __instance.clientCash);
            }
            if (budget <= 0) return; // 防御①：原生算完 ≤0 → 不覆盖（mod 绝不写 0）
            int affB = GetAffection();
            float mult = affB < BuildConfig.WageGirlBudgetAffLow ? BuildConfig.WageGirlBudgetMultLow
                : (affB < BuildConfig.WageGirlBudgetAffMid ? BuildConfig.WageGirlBudgetMultMid : BuildConfig.WageGirlBudgetMultHigh);
            long newBudget = (long)(budget * mult);
            if (newBudget > BuildConfig.WageGirlBudgetCap) newBudget = BuildConfig.WageGirlBudgetCap; // 上限防溢出
            __instance.OverrideBudget((int)newBudget); // 防御③：只写一次，不触发原生重算
        }
        catch (System.Exception ex) { Core.LogMsg("[WageGirlSystem.Budget] 异常: " + ex.Message); }
    }
}
