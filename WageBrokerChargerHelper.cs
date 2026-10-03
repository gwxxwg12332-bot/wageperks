using System;
using Il2Cpp;

namespace WagePerks;

// ============================================================
// 蛙哥充电器 helper：统计背包里的充电器最高等级
// ============================================================
internal static class WageBrokerChargerHelper
{
    internal const string CHARGER_1_ID = "wage_charger_1";
    internal const string CHARGER_2_ID = "wage_charger_2";
    internal const string CHARGER_3_ID = "wage_charger_3";

    // 取最高等级 0/1/2/3
    internal static int GetMaxChargerLevel()
    {
        try
        {
            var em = EmporiumEntry.Instance;
            if (em == null) return 0;

            int maxLevel = 0;
            foreach (var item in em.GetAllItems())
            {
                if (item == null) continue;
                string id = item.identifier;
                if (id == CHARGER_3_ID) return 3; // 三级直接最高，不用再找
                if (id == CHARGER_2_ID && maxLevel < 2) maxLevel = 2;
                if (id == CHARGER_1_ID && maxLevel < 1) maxLevel = 1;
            }
            return maxLevel;
        }
        catch (Exception ex) { Core.LogMsg("[蛙哥充电器] GetMaxChargerLevel异常: " + ex.Message); return 0; }
    }

    // 是否持有 ≥ 某等级
    internal static bool HasCharger(int level)
    {
        return GetMaxChargerLevel() >= level;
    }
}
