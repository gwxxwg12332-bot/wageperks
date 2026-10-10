using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace WagePerks;

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
        catch (System.Exception ex) { Core.LogMsg("[TraitEffects] 异常: " + ex.Message); }
        try
        {
            var idProp = item.GetType().GetProperty("id");
            if (idProp != null) return idProp.GetValue(item)?.ToString() ?? "";
            var idField = item.GetType().GetField("id");
            if (idField != null) return idField.GetValue(item)?.ToString() ?? "";
        }
        catch (System.Exception ex) { Core.LogMsg("[TraitEffects] 异常: " + ex.Message); }
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
        catch (System.Exception ex) { Core.LogMsg("[TraitEffects] 异常: " + ex.Message); }
        return 0;
    }

    // 检查物品是否是违禁品
    // 10-10 #38 收敛：唯一入口=WageAPI.FoodIdentifyHelper.IsContrabandItem（原版权威 ContrabandHelper.IsContraband + CONTRABAND/CONTRABAND_ITEM_TAG 双 tag 兜底）
    //   原实现：小写 IsTag("contraband")（非原版权威）+ GetTagReadonly + 硬编码 ID 列表（handmade_pistol/smg 等武器+麻醉品）——三实现标准不一致（TraitEffects/FenceReturn/FoodIdentifyHelper 各一套）
    //   收窄后：武器/工具不再因"是武器"自动判违禁——违禁=原版权威 tag（原版违禁武器 InitContrabandItem 必带 tag——cheatsheet :4548 麻醉品实锤，武器类比）；硬编码 ID 列表退役（列表内物品若原版带 tag 由原版权威命中）
    public static bool IsContraband(GameItem item)
    {
        if (item == null) return false;
        try { return WageAPI.FoodIdentifyHelper.IsContrabandItem(item); }
        catch (System.Exception ex) { Core.LogMsg("[TraitEffects] IsContraband 异常: " + ex.Message); return false; }
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
        catch (System.Exception ex) { Core.LogMsg("[TraitEffects] 异常: " + ex.Message); }
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
        catch (System.Exception ex) { Core.LogMsg("[TraitEffects] 异常: " + ex.Message); }
        return false;
    }
}
