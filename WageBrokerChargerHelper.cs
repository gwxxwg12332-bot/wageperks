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
            // 10-18 审阅定案（第五轮外）：原 GetAllItems 只扫当铺顶层——充电器放容器/嵌套容器内扫不到 → level=0 → PostfixEndDayCharger :83 return，一个电池都不充。
            //   孪生文件 WageBrokerPermitHelper.cs:17-18（10-05 已修同根因：许可放容器失效）。改用 CollectAllItems（顶层+容器内部递归，深度8防环）。
            int maxLevel = 0;
            foreach (var item in WageBrother.CollectAllItems())
            {
                if (item == null) continue;
                string id = ""; try { id = item.identifier ?? ""; } catch { } // 审阅补丁原文同款：identifier 反射读，包 try-catch 防异常中断扫描
                if (id == CHARGER_3_ID) return 3; // 三级直接最高，不用再找
                if (id == CHARGER_2_ID && maxLevel < 2) maxLevel = 2;
                if (id == CHARGER_1_ID && maxLevel < 1) maxLevel = 1;
            }
            return maxLevel;
        }
        catch (Exception ex) { Core.LogMsg("[蛙哥充电器] GetMaxChargerLevel异常: " + ex.Message); return 0; }
    }

    // 是否持有 ≥ 某等级
// 10-19 P3 B组 休眠/封存（派活单§五：蛙哥交易站（tbl:157-162 已暂时封存）；特性未激活，不删防回滚需要；先看反馈再决定启封或清理）
    internal static bool HasCharger(int level)
    {
        return GetMaxChargerLevel() >= level;
    }
}
