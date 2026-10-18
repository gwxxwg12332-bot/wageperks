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
            var ids = new System.Collections.Generic.List<string>();
            foreach (var item in WageBrother.CollectAllItems())
            {
                if (item == null) continue;
                string id = item.identifier;
                if (id == null) continue;
                ids.Add(id);
                if (id == PERMIT_3_ID) { Core.LogMsg("[#许可诊断] GetMaxPermitLevel 命中三级, 扫到 " + ids.Count + " 项"); return 3; } // 三级直接最高，不用再找
                if (id == PERMIT_2_ID && maxLevel < 2) maxLevel = 2;
                if (id == PERMIT_1_ID && maxLevel < 1) maxLevel = 1;
            }
            // 10-10 #46 诊断：GetAllItems 含背包/柜台（拆包实锤）——扫描范围本应覆盖；打日志确认实际扫到量与命中（区分 id 不匹配/物品不存在/逻辑问题）
            if (maxLevel > 0 || ids.Count > 0)
                Core.LogMsg("[#许可诊断] GetMaxPermitLevel 扫到 " + ids.Count + " 项, maxLevel=" + maxLevel + ", 含许可?=" + (ids.Exists(x => x == PERMIT_1_ID || x == PERMIT_2_ID || x == PERMIT_3_ID)));
            return maxLevel;
        }
        catch (Exception ex) { Core.LogMsg("[蛙哥许可] GetMaxPermitLevel异常: " + ex.Message); return 0; }
    }

    // 是否持有 ≥ 某等级
// 10-19 P3 B组 休眠/封存（派活单§五：蛙哥交易站（tbl:157-162 已暂时封存）；特性未激活，不删防回滚需要；先看反馈再决定启封或清理）
    internal static bool HasPermit(int level)
    {
        return GetMaxPermitLevel() >= level;
    }
}
