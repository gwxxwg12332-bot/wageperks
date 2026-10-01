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
            if (__instance.identifier == ENTITY_ID || __instance.identifier == "wage_brother") return; // 蛙娘自己+蛙哥固定预算不受好感倍率
            if (Patches._inBudgetOverride) return; // 防重入
            int budget = __instance.GetBudget();
            if (budget <= 0) return;
            // 酒徒宿醉：预算-10%（=议价-10%）
            if (WineLoverPerk.IsHungover())
            {
                long hungover = (long)(budget * 0.9f);
                __instance.OverrideBudget((int)hungover);
                return;
            }
            // v1.3.1【7b】干燥空气：水酒客户预算+25%（不依赖蛙娘，独立 perk）
            if (DryAirPerk.IsActive() && BuysWaterOrBooze(__instance))
            {
                long dry = (long)(budget * DryAirPerk.GetWaterBoozeBudgetBonus());
                __instance.OverrideBudget((int)dry);
                return;
            }
            if (!Exists()) return; // 蛙娘未出现 → 无增益（DryAir 已独立处理）
            int affB = GetAffection();
            float mult = affB < BuildConfig.WageGirlBudgetAffLow ? BuildConfig.WageGirlBudgetMultLow
                : (affB < BuildConfig.WageGirlBudgetAffMid ? BuildConfig.WageGirlBudgetMultMid : BuildConfig.WageGirlBudgetMultHigh);
            long newBudget = (long)(budget * mult);
            if (newBudget > BuildConfig.WageGirlBudgetCap) newBudget = BuildConfig.WageGirlBudgetCap;
            __instance.OverrideBudget((int)newBudget);
        }
        catch (System.Exception ex) { Core.LogMsg("[WageGirlSystem.Budget] 异常: " + ex.Message); }
    }
    // v1.3.1【7b】：客户购买清单含水/酒物品？
    private static readonly System.Collections.Generic.HashSet<string> WaterBoozeIds = new System.Collections.Generic.HashSet<string>(new string[] {
        "wine_bottle","red_beer","nudka","beer","alcohol","wine_berry","beer_case","beer_bottle",
        "water_bottle","bottled_water","empty_bottle","wine","whiskey","vodka","rum","champagne","sake"
    });
    private static bool BuysWaterOrBooze(StoreClient c)
    {
        try {
            if (c.clientBuyingIdList == null) return false;
            foreach (var id in c.clientBuyingIdList) { if (id != null && WaterBoozeIds.Contains(id)) return true; }
        } catch { }
        return false;
    }
}
