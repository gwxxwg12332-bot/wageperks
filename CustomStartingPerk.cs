using System;
using System.Reflection;
using Il2Cpp;
using Il2CppSystem.Collections.Generic;

namespace JacksonPerks;

internal abstract class CustomStartingPerk
{
    internal abstract string Id { get; }
    internal abstract string DisplayName { get; }
    internal abstract string Description { get; }
    internal virtual int Cost => 1;
    internal virtual int MaxSlot => 0;
    internal virtual int Type => 0;
    internal virtual string[] IncompatibleIds => Array.Empty<string>();

    internal abstract void OnNewGame();
    internal virtual void OnNewDay() { }

    internal StartingPerk Create()
    {
        List<string> val = new List<string>();
        foreach (string text in IncompatibleIds)
        {
            val.Add(text);
        }
        StartingPerk perk = new StartingPerk
        {
            id = Id,
            cost = Cost,
            maxSlot = MaxSlot,
            incompatiblePerks = val
        };
        // 设置 type：0=POSITIVE绿 1=NEGATIVE红 2=NEUTRAL黄（先直接赋值，失败再反射兜底）
        try
        {
            perk.type = (StartingPerk.StartingPerkType)Type;
        }
        catch (Exception typeEx)
        {
            Core.LogMsg("[特性] " + Id + " 直接赋值type失败: " + typeEx.Message);
            try
            {
                var typeField = typeof(StartingPerk).GetField("type", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (typeField != null)
                {
                    typeField.SetValue(perk, Enum.ToObject(typeField.FieldType, Type));
                }

            }
            catch (Exception reflEx)
            {
                Core.LogMsg("[特性] " + Id + " 反射type也失败: " + reflEx.Message);
            }
        }
        return perk;
    }
}