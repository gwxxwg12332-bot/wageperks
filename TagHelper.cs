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
        catch { }
    }

    public static void AddInt(GameItem item, string tag, int delta)
    {
        try { SetInt(item, tag, GetInt(item, tag) + delta); } catch { }
    }
}