using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace WagePerks;

public static partial class WageGirlSystem
{
    public static void PostfixGetDealMakerBonus(ref int __result)
    {
        try
        {
            if (!Exists()) return; // 蛙娘未出现 → 无增益
            __result += GetBargainBonus();
            // 10-16 P4a M6 精明：当日标记未消费 → 议价加成再 +5（≈首笔售价 ×1.05，封顶 100 保持；消费一次即失效）
            if (GetStat(K_SMART_DAY) == CurrentDay()) { __result += 5; SetStat(K_SMART_DAY, 0); }
            if (__result > 100) __result = 100;
        }
        catch (System.Exception ex) { Core.LogMsg("[WageGirlSystem.Budget] 异常: " + ex.Message); }
    }

    /// <summary>蛙娘卡升级接线（P0 M3 10-16 拆分）：议价加成 Lv1=50 / Lv2=55 / Lv3=60（封顶 100 保持）。
    /// 归属从蛙哥牛逼（原 60/65）改为蛙娘卡等级——再平衡 55/60（patch notes 标注），蛙娘在场基础 +50 无条件保留。</summary>
    private static int GetBargainBonus()
    {
        try
        {
            int lv = B2ServiceStore.GetEffectLevel(WageGirlPerk.PerkId);
            if (lv >= 3) return 60;
            if (lv >= 2) return 55;
            return 50;
        }
        catch { return 50; }
    }
    public static void PostfixApplyBudgetModifier(StoreClient __instance)
    {
        try
        {
            if (__instance == null) return;
            if (__instance.identifier == ENTITY_ID || __instance.identifier == "wage_brother") return; // 蛙娘自己+蛙哥固定预算不受好感倍率
            if (Patches._inBudgetOverride) return; // 防重入
            int budget = WageAPI.WageTrade.GetBudget(__instance); // 10-06 收敛：WageTrade 权威读（负值→0，含原 budget<=0 前防御）
            if (budget <= 0) return;
            // 酒徒宿醉：预算-10%（=议价-10%）
            if (WineLoverPerk.IsHungover())
            {
                long hungover = (long)(budget * 0.9f);
                WageAPI.WageTrade.OverrideBudget(__instance, hungover);
                LastBudgetInfo[__instance] = (0.9f, BudgetSource.WineHungover); // FIX17b：记录来源（显示+升级重算用）
                return;
            }
            // v1.3.1【7b】干燥空气：水酒客户预算+25%（不依赖蛙娘，独立 perk）
            if (DryAirPerk.IsActive() && BuysWaterOrBooze(__instance))
            {
                long dry = (long)(budget * DryAirPerk.GetWaterBoozeBudgetBonus());
                WageAPI.WageTrade.OverrideBudget(__instance, dry);
                LastBudgetInfo[__instance] = (DryAirPerk.GetWaterBoozeBudgetBonus(), BudgetSource.DryAir); // FIX17b
                return;
            }
            if (!Exists()) return; // 蛙娘未出现 → 无增益（DryAir 已独立处理）
            if (GetStat(K_LEAVE) > CurrentDay()) return; // #4 附带修复（P0）：跑路/离开中（Exists 跑路期为 true）不再应用好感预算倍率
            float mult = CurrentBudgetMult(__instance); // FIX17b：好感分档+蛙哥牛逼Lv3（现算，显示/重算共用）
            // 10-16 P4a M6 灵感：当日标记未消费 → 下个客户预算 ×1.2（单次，仍受 cap 约束；消费一次即失效）
            if (GetStat(K_INSPIRE_DAY) == CurrentDay()) { mult *= 1.2f; SetStat(K_INSPIRE_DAY, 0); }
            long newBudget = (long)(budget * mult);
            if (newBudget > BuildConfig.WageGirlBudgetCap) newBudget = BuildConfig.WageGirlBudgetCap;
            WageAPI.WageTrade.OverrideBudget(__instance, newBudget);
            LastBudgetInfo[__instance] = (mult, BudgetSource.Affection); // FIX17b
        }
        catch (System.Exception ex) { Core.LogMsg("[WageGirlSystem.Budget] 异常: " + ex.Message); }
    }

    // ===== FIX17b：预算倍率记录 + 显示/升级重算（修复「Lv3 升级后当前客户预算不重算」「显示层倍率与覆盖层不一致」） =====
    internal enum BudgetSource { None, WineHungover, DryAir, Affection }
    internal static readonly System.Collections.Generic.Dictionary<StoreClient, (float Mult, BudgetSource Src)> LastBudgetInfo = new();
    internal static bool TryGetBudgetInfo(StoreClient c, out float mult, out BudgetSource src)
    {
        mult = 1f; src = BudgetSource.None;
        try { if (c != null && LastBudgetInfo.TryGetValue(c, out var v)) { mult = v.Mult; src = v.Src; return true; } } catch { }
        return false;
    }

    /// <summary>好感分档现算倍率（含蛙哥牛逼 Lv3 ×5）——覆盖层/显示层/重算层共用同一源，杜绝口径漂移。</summary>
    internal static float CurrentBudgetMult(StoreClient c)
    {
        try
        {
            if (c != null && (c.identifier == ENTITY_ID || c.identifier == "wage_brother")) return 1f;
            double affB = GetAffectionExact();
            float mult = affB < BuildConfig.WageGirlBudgetAffLow ? 1f // #4（P0）：敌意档不再乘（原×1.5→×1.0，低好感不奖励）
                : (affB < BuildConfig.WageGirlBudgetAffMid ? BuildConfig.WageGirlBudgetMultMid : BuildConfig.WageGirlBudgetMultHigh);
            if (B2ServiceStore.GetEffectLevel(WagePowerPerk.PerkId) >= 3 && affB >= BuildConfig.WageGirlBudgetAffMid)
                mult = 5f;
            return mult;
        }
        catch { return 1f; }
    }

    /// <summary>升级后重算当前客户预算：基于「已应用倍率反推基础值」（防累乘），再按新等级倍率覆盖。</summary>
    internal static void ReapplyBudgetFor(StoreClient c)
    {
        try
        {
            if (c == null || Patches._inBudgetOverride) return;
            if (!LastBudgetInfo.TryGetValue(c, out var info) || info.Mult <= 0f) return; // 未覆盖过不动
            int cur = WageAPI.WageTrade.GetBudget(c);
            if (cur <= 0) return;
            int baseVal = (int)(cur / info.Mult); // 反推基础预算（=原版预算，无累乘）
            float newMult = CurrentBudgetMult(c);
            if (System.Math.Abs(newMult - info.Mult) < 0.001f) return; // 倍率没变不重算
            long target = (long)(baseVal * newMult);
            if (target > BuildConfig.WageGirlBudgetCap) target = BuildConfig.WageGirlBudgetCap;
            Patches._inBudgetOverride = true;
            try
            {
                WageAPI.WageTrade.OverrideBudget(c, target);
                LastBudgetInfo[c] = (newMult, BudgetSource.Affection);
                Core.LogMsg("[预算显示] 升级重算: " + c.identifier + " 基础=" + baseVal + " ×" + newMult + " → " + target);
            }
            finally { Patches._inBudgetOverride = false; }
        }
        catch (System.Exception ex) { Core.LogMsg("[预算显示] 重算异常: " + ex.Message); }
    }
    // v1.3.1【7b】：客户购买清单含水/酒物品？
    private static readonly System.Collections.Generic.HashSet<string> WaterBoozeIds = new System.Collections.Generic.HashSet<string>(new string[] {
        "wine_bottle","red_beer","nudka","beer","alcohol","wine_berry","beer_case","beer_bottle",
        "water_bottle","bottled_water","empty_bottle","wine","whiskey","vodka","rum","champagne","sake"
    });
    internal static bool BuysWaterOrBooze(StoreClient c)
    {
        try {
            if (c.clientBuyingIdList == null) return false;
            foreach (var id in c.clientBuyingIdList) { if (id != null && WaterBoozeIds.Contains(id)) return true; }
        } catch { }
        return false;
    }
}
