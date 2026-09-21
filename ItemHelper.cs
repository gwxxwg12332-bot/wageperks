using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppInterop.Runtime;

namespace JacksonPerks;

// 物品操作（类型判定 / 食物 / 水）——门面模式完整实现
internal static class ItemHelper
{
    // ===== 食物白名单（从 RobinCrusoePerk 完整复制）=====
    private static readonly HashSet<string> FOOD_IDS = new HashSet<string>
    {
        "beis_icecream", "processed_milk", "processed_juice", "processed_meat", "bottle_hot_sauce",
        "cat_bar", "li_eat_snackbar", "processed_cheese", "raw_meat", "small_raw_meat",
        "morsel", "small_morsel", "fat_meat", "meat_scrap", "cup_noodle",
        "wine_berry", "bloomberry"
    };

    // ===== 饮料白名单（从 RobinCrusoePerk 完整复制）=====
    private static readonly HashSet<string> DRINK_IDS = new HashSet<string>
    {
        "bottled_water", "bottled_water_premium", "small_bottled_water", "large_bottled_water", "water_jug",
        "mini_bottle", "soda_red", "energy_drink", "nudka", "galaxy_blend",
        "red_beer", "empty_beer_bottle", "water_ration", "water_ration_small"
    };

    // ===== tag 常量 =====
    internal const string CAL_LEFT_TAG = "WAGES_CAL_LEFT";

    // ===== 类型判定 =====
    public static bool IsFood(GameItem item)
    {
        if (item == null) return false;
        try { return FOOD_IDS.Contains(GetId(item)); } catch { return false; }
    }

    public static bool IsDrink(GameItem item)
    {
        if (item == null) return false;
        try { return DRINK_IDS.Contains(GetId(item)); } catch { return false; }
    }

    public static bool IsDailyNeed(GameItem item)
    {
        return RobinCrusoePerk.IsDailyNeed(item);
    }

    // ===== 食物卡路里 =====
    public static int GetCalorie(GameItem item)
    {
        if (item == null) return 0;
        try
        {
            if (item.IsTag("CALORIE_VALUE_TAG"))
            {
                var ts = item.GetTagReadonly("CALORIE_VALUE_TAG");
                if (ts != null) return Math.Max(0, ts.GetInt());
            }
        }
        catch { }
        try
        {
            if (item.IsTag("CALORIE"))
            {
                var ts = item.GetTagReadonly("CALORIE");
                if (ts != null) return Math.Max(0, ts.GetInt());
            }
        }
        catch { }
        return 300;
    }

    public static int GetCalLeft(GameItem item)
    {
        if (item == null) return 0;
        try
        {
            if (item.IsTag(CAL_LEFT_TAG))
            {
                var ts = item.GetTagReadonly(CAL_LEFT_TAG);
                if (ts != null) return Math.Max(0, ts.GetInt());
            }
        }
        catch { }
        return GetCalorie(item);
    }

    public static void SetCalLeft(GameItem item, int value)
    {
        if (item == null) return;
        try
        {
            if (value <= 0) { item.DisableTag(CAL_LEFT_TAG, true); return; }
            if (!item.IsTag(CAL_LEFT_TAG)) item.EnableTag(CAL_LEFT_TAG, true);
            System.Action<TagState> sysAct = delegate (TagState state) { state.SetInt(value); };
            var il2cppAct = DelegateSupport.ConvertDelegate<Il2CppSystem.Action<TagState>>((System.Delegate)sysAct);
            item.ModifyTag(CAL_LEFT_TAG, il2cppAct, false);
        }
        catch { }
    }

    // ===== 水 =====
    public static int GetWaterMl(GameItem item)
    {
        if (item == null) return 0;
        try
        {
            if (item.IsTag("LIQUID_CONTAINER_CURRENT"))
            {
                var ts = item.GetTagReadonly("LIQUID_CONTAINER_CURRENT");
                if (ts != null)
                {
                    try { return Math.Max(0, (int)ts.GetFloat()); } catch { return Math.Max(0, ts.GetInt()); }
                }
            }
        }
        catch { }
        try { return Math.Max(0, WaterHelper.GetCurrentCapacityML(item)); } catch { }
        return 2000;
    }

    // ===== 内部工具 =====
    private static string GetId(GameItem item)
    {
        try { return (item.identifier ?? "").ToLowerInvariant(); } catch { return ""; }
    }
}
