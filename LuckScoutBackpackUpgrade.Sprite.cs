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
    // ===== Sprite =====



    // Patch RenderHandler.LoadFromAtlas

    public static bool PrefixLoadFromAtlas(string atlasPath, string name, ref Sprite __result)

    {

        try

        {

            if (atlasPath == CUSTOM_ATLAS && name == CUSTOM_SPRITE_KEY && _customSprite != null)

            {

                __result = _customSprite;

                return false;

            }

        }

        catch (System.Exception ex) { Core.LogMsg("[LuckScoutBackpackUpgrade] 异常: " + ex.Message); }

        return true;

    }
}
