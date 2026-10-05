using System;
using Il2Cpp;

namespace WagePerks;

// ============================================================
// 蛙哥的许可 helper：统计背包里的许可数量/最高等级
// ============================================================
internal static class WageBrokerPermitHelper
{
    internal const string PERMIT_1_ID = "wage_permit_1";
    internal const string PERMIT_2_ID = "wage_permit_2";
    internal const string PERMIT_3_ID = "wage_permit_3";

    // 扫全当铺（顶层+容器内部递归），统计 3 级许可总张数。
    // 10-05 修复：原 GetAllItems 只扫当铺顶层——许可放容器/嵌套容器内扫不到 → 打烊加成/受伤保护失效（用户实测"许可不生效"）
    internal static int CountAllPermits()
    {
        try
        {
            int count = 0;
            foreach (var item in WageBrother.CollectAllItems())
            {
                if (item == null) continue;
                string id = item.identifier;
                if (id == PERMIT_1_ID || id == PERMIT_2_ID || id == PERMIT_3_ID)
                    count++;
            }
            return count;
        }
        catch (Exception ex) { Core.LogMsg("[蛙哥许可] CountAllPermits异常: " + ex.Message); return 0; }
    }

    // 取最高等级 0/1/2/3（全当铺递归）
    internal static int GetMaxPermitLevel()
    {
        try
        {
            int maxLevel = 0;
            foreach (var item in WageBrother.CollectAllItems())
            {
                if (item == null) continue;
                string id = item.identifier;
                if (id == PERMIT_3_ID) return 3; // 三级直接最高，不用再找
                if (id == PERMIT_2_ID && maxLevel < 2) maxLevel = 2;
                if (id == PERMIT_1_ID && maxLevel < 1) maxLevel = 1;
            }
            return maxLevel;
        }
        catch (Exception ex) { Core.LogMsg("[蛙哥许可] GetMaxPermitLevel异常: " + ex.Message); return 0; }
    }

    // 是否持有 ≥ 某等级
    internal static bool HasPermit(int level)
    {
        return GetMaxPermitLevel() >= level;
    }
}
