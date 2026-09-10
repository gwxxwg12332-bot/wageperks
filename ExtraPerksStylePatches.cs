using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime;
using Il2CppSystem.Collections.Generic;
using MelonLoader;
using UnityEngine;

namespace JacksonPerks;

// ============================================================
// 按照ExtraPerks的方式实现本地化和图标注入
// ============================================================

// 本地化补丁：Patch LocHelper.GetLocalizedPerkTable
// 拦截 perk_{id}_name 和 perk_{id}_desc 键
// 用TargetMethod动态查找方法，解决重载歧义问题
// [HarmonyPatch]
internal static class PerkTableLocPatch
{
    static MethodBase TargetMethod()
    {
        // 动态查找GetLocalizedPerkTable方法
        MethodInfo[] methods = typeof(LocHelper).GetMethods(
            BindingFlags.Public | BindingFlags.Static);
        foreach (MethodInfo m in methods)
        {
            if (m.Name == "GetLocalizedPerkTable")
            {
                // 返回第一个匹配的方法
                return m;
            }
        }
        return null;
    }

    private static bool Prefix(string key, ref string __result)
    {
        try
        {
            // 键名约定：perk_{id}_name → DisplayName，perk_{id}_desc → Description
            if (key.StartsWith("perk_") && (key.EndsWith("_name") || key.EndsWith("_desc")))
            {
                // 提取特性ID
                string perkId = key.Substring(5, key.Length - 5 - (key.EndsWith("_name") ? 5 : 5));
                // 去掉可能的\0
                perkId = perkId.Replace("\0", "").Trim();

                // 白名单加固（Bug4修复）：只用 CustomStartingPerks.Find 确认是自定义特性才覆盖，确保绝不误伤原版特性文本
                if (CustomStartingPerks.Find(perkId) == null) return true;


                if (CustomStartingPerks.TryGetLoc(perkId, out var displayName, out var description))
                {
                    if (key.EndsWith("_name"))
                    {
                        __result = displayName;
                    }
                    else
                    {
                        __result = description;
                    }
                    return false; // 跳过原方法
                }
            }
        }
        catch (Exception ex)
        {
            MelonLogger.Error("[特性本地化] 失败: " + ex.Message);
        }
        return true; // 不匹配，走原方法
    }
}

// 图标加载器补丁：Patch StartingPerkIconLoader.Start
// 向 perkIconList 列表注入 PerkIconEntry 对象
// [HarmonyPatch(typeof(StartingPerkIconLoader), "Start")]
internal static class StartingPerkIconLoaderStartPatch
{
    private static void Postfix(StartingPerkIconLoader __instance)
    {
        try
        {

            // 方法1：尝试perkIcons静态字典（按照ExtraPerks指南）
            bool injected = false;
            try
            {
                FieldInfo perkIconsField = typeof(StartingPerkIconLoader).GetField("perkIcons",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (perkIconsField != null)
                {
                    object perkIcons = perkIconsField.GetValue(null);
                    if (perkIcons != null)
                    {

                        // 获取字典的索引器
                        PropertyInfo itemProp = perkIcons.GetType().GetProperty("Item");

                        foreach (CustomStartingPerk custom in CustomStartingPerks.All)
                        {
                            try
                            {
                                string cleanId = custom.Id.Replace("\0", "").Trim();
                                if (PerkIconLoader.HasCustomIcon(custom.Id))
                                {
                                    Sprite icon = PerkIconLoader.GetPerkIcon(custom.Id);
                                    if (icon != null && itemProp != null)
                                    {
                                        itemProp.SetValue(perkIcons, icon, new object[] { cleanId });
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                MelonLogger.Error("[特性图标] 注入图标失败 " + custom.Id + ": " + ex.Message);
                            }
                        }
                        injected = true;
                    }
                }
            }
            catch (Exception ex)
            {
                Core.LogMsg("[特性图标] 字典方式失败: " + ex.Message);
            }

            // 方法2：如果字典方式失败，用perkIconList列表
            if (!injected)
            {
                var perkIconListProp = __instance.GetType().GetProperty("perkIconList",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (perkIconListProp == null)
                {
                    return;
                }

                object perkIconList = perkIconListProp.GetValue(__instance);
                if (perkIconList == null)
                {
                    Type listType = typeof(Il2CppSystem.Collections.Generic.List<PerkIconEntry>);
                    perkIconList = Activator.CreateInstance(listType);
                    perkIconListProp.SetValue(__instance, perkIconList);
                }


                MethodInfo addMethod = perkIconList.GetType().GetMethod("Add");
                if (addMethod == null)
                {
                    return;
                }

                foreach (CustomStartingPerk custom in CustomStartingPerks.All)
                {
                    try
                    {
                        string cleanId = custom.Id.Replace("\0", "").Trim();
                        if (PerkIconLoader.HasCustomIcon(custom.Id))
                        {
                            Sprite icon = PerkIconLoader.GetPerkIcon(custom.Id);
                            if (icon != null)
                            {
                                PerkIconEntry entry = new PerkIconEntry();
                                entry.perkName = cleanId;
                                entry.Sprite = icon;
                                addMethod.Invoke(perkIconList, new object[] { entry });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Error("[特性图标] 注入图标失败 " + custom.Id + ": " + ex.Message);
                    }
                }

            }
        }
        catch (Exception ex)
        {
            MelonLogger.Error("[特性图标] StartingPerkIconLoaderStartPatch失败: " + ex.Message + "\n" + ex.StackTrace);
        }
    }
}
