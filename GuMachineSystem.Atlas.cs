using System;
using Il2Cpp;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace JacksonPerks;

public static partial class GuMachineSystem
{
    public static bool PrefixLoadFromAtlas(string atlasPath, string name, ref Sprite __result)
    {
        try
        {
            if (atlasPath == ICON_ATLAS)
            {
                if (_guSprite == null)
                {
                    _guSprite = LoadPngSprite("gu_machine.png");
                    _aiSprite = LoadPngSprite("ai_generator.png");
                    _protectorSprite = LoadPngSprite("protector_core.png");
                    Core.LogMsg("[养蛊机] 懒加载图标: gu=" + (_guSprite!=null?"✓":"✗") + " ai=" + (_aiSprite!=null?"✓":"✗") + " prot=" + (_protectorSprite!=null?"✓":"✗"));
                }
                if (name == GU_ICON && _guSprite != null) { __result = _guSprite; return false; }
                if (name == AI_ICON && _aiSprite != null) { __result = _aiSprite; return false; }
                if (name == PROTECTOR_ICON && _protectorSprite != null) { __result = _protectorSprite; return false; }
                if (name == AI_MODULE_ICON && _aiModuleSprite != null) { __result = _aiModuleSprite; return false; }
            }
        }
        catch (System.Exception ex) { Core.LogMsg("[GuMachineSystem.Atlas] 异常: " + ex.Message); }
        return true;
    }
}
