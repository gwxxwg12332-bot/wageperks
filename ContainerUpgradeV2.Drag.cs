using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace JacksonPerks;
partial class ContainerUpgradeV2

{
    // ===== Drag =====

    // ===================== 蛙哥箱子升级链（独立挂载，不依赖鲁滨逊职业） =====================
    public static bool PrefixMayTarget_WageBox(GameItem __instance, GameItem targetItem, ref bool __result)
    {
        try
        {
            if (__instance == null || targetItem == null) return true;
            if (IsNuts(__instance) && IsWageBox(targetItem))
            {
                // 09-26 修白嫖bug：货架上没买的螺丝（非玩家所有）不能拖来升级
                bool owned = false;
                try { owned = Il2Cpp.GeneralHelper.IsItemOwned(__instance); } catch { }
                if (!owned) return true; // 非玩家所有：不拦截，原生处理
                __result = true; return false; // hover 可拖
            }
        }
        catch (System.Exception ex) { Core.LogMsg("[ContainerUpgradeV2] 异常: " + ex.Message); }
        return true;
    }
    public static bool PrefixCanTarget_WageBox(GameItem __instance, GameItem targetItem, ref bool __result)
    {
        return PrefixMayTarget_WageBox(__instance, targetItem, ref __result);
    }
    public static bool PrefixTarget_WageBox(GameItem __instance, GameItem targetItem)
    {
        try
        {
            if (__instance == null || targetItem == null) return true;
            if (!IsNuts(__instance) || !IsWageBox(targetItem)) return true;
            if (!IsDragRelease()) return true;
            // 09-26 修白嫖bug：货架上没买的螺丝不能被消耗升级
            bool owned = false;
            try { owned = Il2Cpp.GeneralHelper.IsItemOwned(__instance); } catch { }
            if (!owned) return true; // 非玩家所有：不拦截，原生放入
            if (TryUpgradeWageBox(__instance, targetItem)) return false; // 升级成功：拦截原生放入
        }
        catch (System.Exception ex) { Core.LogMsg("[ContainerUpgradeV2] 异常: " + ex.Message); }
        return true;
    }
    public static bool PrefixMayHaveValidInventorySlot_WageBox(GameItem __instance, GameItem item, ref bool __result)
    {
        try
        {
            if (__instance == null || item == null) return true;
            if (!IsNuts(item) || !IsWageBox(__instance)) return true;
            if (!IsDragRelease()) return true;
            if (TryUpgradeWageBox(item, __instance)) { __result = false; return false; }
        }
        catch (System.Exception ex) { Core.LogMsg("[ContainerUpgradeV2] 异常: " + ex.Message); }
        return true;
    }
}
