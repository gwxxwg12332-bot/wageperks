using System;

using System.Collections.Generic;

using System.Runtime.InteropServices;

using Il2Cpp;

using Il2CppInterop.Runtime;

using Il2CppInterop.Runtime.InteropTypes;

using HarmonyLib;

using MelonLoader;

using UnityEngine;



namespace JacksonPerks;
partial class LuckScoutBackpackUpgrade


{
    // ===== Drag =====



    public static bool PrefixMayTarget(GameItem __instance, GameItem targetItem, ref bool __result)

    {

        if ((IsBead(__instance) && IsJunk(targetItem)) || (IsBead(targetItem) && IsJunk(__instance)))

        { __result = true; return false; }

        return true;

    }



    public static bool PrefixCanTarget(GameItem __instance, GameItem targetItem, ref bool __result)

    {

        return PrefixMayTarget(__instance, targetItem, ref __result);

    }



    public static bool PrefixTarget(GameItem __instance, GameItem targetItem)

    {

        GameItem bead = null, junk = null;

        if (IsBead(__instance) && IsJunk(targetItem)) { bead = __instance; junk = targetItem; }

        else if (IsBead(targetItem) && IsJunk(__instance)) { bead = targetItem; junk = __instance; }

        if (bead != null) { DoUpgrade(junk, bead); return false; }

        return true;

    }
}
