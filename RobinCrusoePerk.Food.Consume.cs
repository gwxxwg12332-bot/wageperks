using System;
using HarmonyLib;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Il2Cpp;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace WagePerks;
// ===== 10-07 阶段D D-5：删 Food 消费实现 partial（双击吃喝用链——WS SurvivalConsume.PostfixDoubleClickAction 承接全局）=====
// 已删：PostfixDoubleClickAction / DrinkAlcohol / EatBite / DrinkSip / DrinkBeverage / UseNarcotic / TreatWithMedicine /
//       UseDailyNeed / IsNonEdibleMachine / IsBottleContainerOnly / IsHomebrewWine / GetBeverageCalories / IsWageSurvivalLoaded 缓存
// 保留（被 WP 其他文件引用——引用审计 10-07）：IsBeverage / IsSnack / IsEmptyBottle（RobinCrusoePerk.Food.Tooltip.cs :168/:171/:174）
//       + IsHomebrewWineBuffActive（RobinCrusoePerk.Blood.cs:160 / RobinCrusoePerk.Scavenge.cs:71——顶级自酿 buff 受伤免疫/拾荒+1）
// 禁整类删（P1.2）——RobinCrusoePerk 类保留共用成员（GetTagIntSafe/SetTagIntValue/IsExcludedModule/GetTradeBuffDisplay/GetMood）
internal static partial class RobinCrusoePerk
{
    // 顶级自酿 buff：连续3天不受伤 + 拾荒次数+1（Blood.cs:160 / Scavenge.cs:71 引用——保留）
    private static bool IsHomebrewWineBuffActive()
    {
        try
        {
            if (!IsActive()) return false;
            int start = WageSaveStore.GetInt(PERK_ID, "hbuffDay", -1);
            if (start < 0) return false;
            int now = DeterministicSchedule.CurrentDay;
            return now - start <= 2;
        }
        catch (System.Exception ex) { Core.LogMsg("[RobinCrusoePerk.Food.Consume] 异常: " + ex.Message); }
        return false;
    }

    // 饮品判定（Food.Tooltip.cs:168 引用——保留）
    private static bool IsBeverage(GameItem item)
    {
        try { return item != null && BEVERAGE_IDS.Contains((item.identifier ?? "").ToLowerInvariant()); } catch { return false; }
    }

    // 零食判定（Food.Tooltip.cs:171 引用——保留）
    private static bool IsSnack(GameItem item)
    {
        try { return item != null && SNACK_IDS.Contains((item.identifier ?? "").ToLowerInvariant()); } catch { return false; }
    }

    // 空瓶判定（Food.Tooltip.cs:174 引用——保留；10-05 实锤：玩家"空酒瓶"实际 id=wine_bottle，IsAlc 关键词"wine"误判当酒喝）
    private static bool IsEmptyBottle(GameItem item)
    {
        try
        {
            if (item == null) return false;
            string id = (item.identifier ?? "").ToLowerInvariant();
            if (id == "empty_beer_bottle") return true;
            if (id == "wine_bottle" || id == "beer_bottle" || id.Contains("bottle")) return GetWaterMl(item) <= 0;
            return false;
        }
        catch { return false; }
    }
}
