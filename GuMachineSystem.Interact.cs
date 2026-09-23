using System;
using Il2Cpp;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace JacksonPerks;

public static partial class GuMachineSystem
{
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
