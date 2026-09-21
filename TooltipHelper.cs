using Il2Cpp;
using System;
using System.Collections.Generic;

namespace JacksonPerks;

// 提示框修饰器统一注册
internal static class TooltipHelper
{
    private static readonly List<Func<GameItem, string, string>> _modifiers = new();

    public static void RegisterModifier(Func<GameItem, string, string> modifier)
    {
        _modifiers.Add(modifier);
    }

    public static string ApplyModifiers(GameItem item, string originalText)
    {
        string result = originalText;
        foreach (var mod in _modifiers)
        {
            try { result = mod(item, result); } catch { }
        }
        return result;
    }
}