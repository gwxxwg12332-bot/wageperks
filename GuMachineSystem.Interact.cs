using System;
using Il2Cpp;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace WagePerks;

public static partial class GuMachineSystem
{
// 10-19 P3 A组#30 待确认用途（派活单§四：养蛊机拖拽·倾向补注册；未注册=现状未生效，拍板后接线或作废）
    public static bool PrefixItemMouseDragHandlerStartDrag(Il2Cpp.ItemMouseDragHandler __instance)
    {
        try
        {
            if (__instance != null && __instance.currentItem != null)
            {
                bool stuck = false; try { stuck = __instance.currentItem.IsTag("MODULE_STUCK_TAG"); } catch { }
                if (stuck) return false;
            }
        }
        catch (System.Exception ex) { Core.LogMsg("[GuMachineSystem.Interact] 异常: " + ex.Message); }
        return true;
    }
}
