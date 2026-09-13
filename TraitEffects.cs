using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace JacksonPerks;

// ============================================================
// 特性数值效果补丁
// 实现刀尖舔血、好酒之徒、笑面虎、捡漏直觉的实际游戏影响
// ============================================================
internal static class TraitEffects
{
    // 安全获取物品ID
    private static string GetItemId(GameItem item)
    {
        if (item == null) return "";
        try
        {
            // 优先用item.identifier（GameItem原生字段，IL2CPP interop已暴露）
            if (!string.IsNullOrEmpty(item.identifier)) return item.identifier;
        }
        catch { }
        try
        {
            var idProp = item.GetType().GetProperty("id");
            if (idProp != null) return idProp.GetValue(item)?.ToString() ?? "";
            var idField = item.GetType().GetField("id");
            if (idField != null) return idField.GetValue(item)?.ToString() ?? "";
        }
        catch { }
        return item.name ?? "";
    }

    // 安全获取物品价值
    private static int GetItemValue(GameItem item)
    {
        if (item == null) return 0;
        try
        {
            var valueProp = item.GetType().GetProperty("value");
            if (valueProp != null) return (int)valueProp.GetValue(item);
            var valueField = item.GetType().GetField("value");
            if (valueField != null) return (int)valueField.GetValue(item);
        }
        catch { }
        return 0;
    }

    // 检查物品是否是违禁品
    public static bool IsContraband(GameItem item)
    {
        if (item == null) return false;
        try
        {
            // 方法1：直接调用IsTag方法检查contraband标签（最准确）
            try
            {
                bool result = item.IsTag("contraband");
                return result;
            }
            catch { }

            // 方法2：用GetTagReadonly方法检查
            try
            {
                var tagState = item.GetTagReadonly("contraband");
                if (tagState != null)
                {
                    return tagState.IsEnabled();
                }
            }
            catch { }

            // 方法3：根据物品ID判断（扩展列表）
            string id = GetItemId(item);
            string name = item.name ?? "";
            string[] contrabandIds = {
                "dream_dust", "nightmare_dust", "unlicensed_phagimycin_pill",
                "handmade_pistol", "heavy_handmade_pistol", "smg", "shotgun",
                "c4", "smoke_grenade", "stun_gun",
                "system_capped_neural_core",
                "ancient_alien_relics", "blank_module", "crypto_module_cmd",
                "crypto_module_sec", "smuggler_bay_mod", "smuggler_bay_mini",
                "neural_core", "神经核心"
            };
            foreach (string cid in contrabandIds)
            {
                if (id.Contains(cid, StringComparison.OrdinalIgnoreCase) ||
                    name.Contains(cid, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            // 方法4：根据物品名称中的关键词判断
            string[] contrabandKeywords = { "违禁", "非法", "赃物", "走私", "未授权", "黑市" };
            foreach (string keyword in contrabandKeywords)
            {
                if (name.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        catch { }
        return false;
    }

    // 检查物品是否是酒类
    public static bool IsAlcohol(GameItem item)
    {
        if (item == null) return false;
        try
        {
            string id = GetItemId(item);
            // 排除：医用/外用酒精（rubbing_alcohol）不是酒类饮品，不享受酒类加价
            if (id.Contains("rubbing", StringComparison.OrdinalIgnoreCase)) return false;
            string name = item.name ?? "";
            // 玩家酿的酒名称如"荧光莓果酿"、"暗影莓果酿"，需要包含"酿"、"莓果"关键词
            // wine_bottle是玩家酿酒的基础ID，直接判断
            string[] alcoholKeywords = {
                "alcohol", "beer", "wine", "liquor", "vodka", "whiskey",
                "酒", "啤", "葡萄", "白干", "伏特加", "威士忌",
                "酿", "莓果", "wine_bottle"
            };
            foreach (string keyword in alcoholKeywords)
            {
                if (id.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                    name.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        catch { }
        return false;
    }

    // 检查物品是否是旧物/杂物
    public static bool IsJunk(GameItem item)
    {
        if (item == null) return false;
        try
        {
            string id = GetItemId(item);
            string[] junkKeywords = {
                "junk", "scrap", "old", "used", "broken", "rusty",
                "杂物", "旧物", "破烂", "废", "碎", "残"
            };
            foreach (string keyword in junkKeywords)
            {
                if (id.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            // 低价值物品视为杂物
            int value = GetItemValue(item);
            if (value > 0 && value < 20) return true;
        }
        catch { }
        return false;
    }
}
// ============================================================
// 补丁：新的一天处理好酒之徒宿醉
// ============================================================
// [HarmonyPatch(typeof(GameMaster), "OnNewDay")]
internal static class TraitHangoverPatch
{
    static void Postfix()
    {
        try
        {
            if (WineLoverPerk.IsActive())
            {
                // 30%概率宿醉
                if (Core.Rng.Next(10) < 3)
                {
                    // 宿醉效果通过全局变量标记，在议价补丁中检查
                    _isHungover = true;
                }
                else
                {
                    _isHungover = false;
                }
            }
        }
        catch (Exception ex)
        {
            MelonLogger.Error("[特性效果] 宿醉补丁失败: " + ex.Message);
        }
    }

    internal static bool _isHungover = false;
}
