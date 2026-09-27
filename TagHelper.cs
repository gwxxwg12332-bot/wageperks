using Il2Cpp;
using Il2CppInterop.Runtime;
using System;
using System.Reflection;

namespace JacksonPerks;

// tag 读写统一（自动 try/catch）
internal static class TagHelper
{
    public static int GetInt(GameItem item, string tag)
    {
        try { var t = item.GetTagReadonly(tag); if (t != null) return t.valueInt; } catch { }
        return 0;
    }

    public static bool Has(GameItem item, string tag)
    {
        try { return item != null && item.GetTagReadonly(tag) != null; } catch { return false; }
    }

    public static void SetInt(GameItem item, string tag, int value)
    {
        try
        {
            if (!item.IsTag(tag)) item.EnableTag(tag, true);
            System.Action<TagState> sysAct = delegate (TagState state) { state.SetInt(value); };
            var il2cppAct = DelegateSupport.ConvertDelegate<Il2CppSystem.Action<TagState>>((System.Delegate)sysAct);
            item.ModifyTag(tag, il2cppAct, false);
        }
        catch (System.Exception ex) { Core.LogMsg("[TagHelper] 异常: " + ex.Message); }
    }

    public static void AddInt(GameItem item, string tag, int delta)
    {
        try { SetInt(item, tag, GetInt(item, tag) + delta); } catch { }
    }

    // v1.3.1：float tag 读写（动物属性 ANIMAL_*_TAG 是 float）
    public static float GetFloat(GameItem item, string tag)
    {
        try { var t = item.GetTagReadonly(tag); if (t != null) return t.GetFloat(); } catch { }
        return 0f;
    }

    public static void SetFloat(GameItem item, string tag, float value)
    {
        try
        {
            if (!item.IsTag(tag)) item.EnableTag(tag, true);
            System.Action<TagState> sysAct = delegate (TagState state) { state.SetFloat(value); };
            var il2cppAct = DelegateSupport.ConvertDelegate<Il2CppSystem.Action<TagState>>((System.Delegate)sysAct);
            item.ModifyTag(tag, il2cppAct, false);
        }
        catch (System.Exception ex) { Core.LogMsg("[TagHelper] SetFloat异常: " + ex.Message); }
    }
}