using System;
using System.Collections.Generic;
using Il2Cpp;

namespace WageAPI;

/// <summary>
/// 跨 mod 物品分类判定（2026-10-03 从 Wage's Perks RobinCrusoePerk.Food.Identify 提炼，public 化）。
/// ID 集合为老 mod 拆包实锤权威值（v0.1.2 复制版存在漂移，已按权威值重写——评审补丁 P1-1）。
/// 无状态纯函数，零依赖。属性读取（GetCalLeft/GetCalorie 等）归各 Food 系统，不在此层。
/// </summary>
public static class FoodIdentifyHelper
{
    // 食物 17（[L1] FoodItemDirectory + 运行日志；wine_berry 酿酒莓可食用实锤）
    public static readonly HashSet<string> FOOD_IDS = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "beis_icecream", "processed_milk", "processed_juice", "processed_meat", "bottle_hot_sauce",
        "cat_bar", "li_eat_snackbar", "processed_cheese", "raw_meat", "small_raw_meat",
        "morsel", "small_morsel", "fat_meat", "meat_scrap", "cup_noodle",
        "wine_berry", "bloomberry"
    };
    // 水 14（[L1]）
    public static readonly HashSet<string> DRINK_IDS = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "bottled_water", "bottled_water_premium", "small_bottled_water", "large_bottled_water", "water_jug",
        "mini_bottle", "soda_red", "energy_drink", "nudka", "galaxy_blend",
        "red_beer", "empty_beer_bottle", "water_ration", "water_ration_small"
    };
    // 酒精（拆包回填4）：red_beer / nudka / 发酵酒全系（ALCOHOL_VALUE 标签兜底）
    public static readonly HashSet<string> ALC_IDS = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    { "red_beer", "nudka", "empty_beer_bottle" };
    // 烟草（拆包回填5）：cig_red（CIGARETTE_TAG 兜底）
    public static readonly HashSet<string> TOBACCO_IDS = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    { "cig_red", "cig_red_stack" };
    // 麻醉品（拆包回填6）：oxycodone_pill / dream_dust / injector 系（NARCOTIC 兜底）
    public static readonly HashSet<string> NARC_IDS = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    { "oxycodone_pill", "dream_dust", "dream_cap_extract", "injector", "injector_pink", "injector_pure_white_s", "black_injector" };
    // 彩票（拆包回填7）：scratch / scratch_stack（LOTTERY_TICKET_TAG 兜底）
    public static readonly HashSet<string> LOTTERY_IDS = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    { "scratch", "scratch_stack" };
    // 日用品判定 key（清洁系统 v1 白名单 key 集；具体恢复值带 BuildConfig 留在 Food 消费系统）
    public static readonly HashSet<string> DAILY_NEED_KEYS = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "toothpaste", "toilet_paper", "shampoo", "paper_towel",
        "box_tampon", "pack_condom", "skincare_cream", "salve", "rubbing_alcohol",
        "neuroactive_perfume", "pheromone_perfume"
    };

    private static string GetId(GameItem item)
    {
        try { return item.identifier ?? ""; } catch { return ""; }
    }

    public static bool IsContrabandItem(GameItem item)
    {
        try { if (ContrabandHelper.IsContraband(item)) return true; } catch { }
        try { if (item.IsTag("CONTRABAND")) return true; } catch { }
        try { if (item.IsTag("CONTRABAND_ITEM_TAG")) return true; } catch { }
        return false;
    }

    /// <summary>别名：派活单 9 判定清单命名。</summary>
    public static bool IsContraband(GameItem item)
    {
        return IsContrabandItem(item);
    }

    public static bool IsFood(GameItem item)
    {
        if (item == null) return false;
        try { if (FOOD_IDS.Contains(GetId(item))) return true; } catch { }
        try
        {
            bool hasCal = item.IsTag("CALORIE_VALUE_TAG") || item.IsTag("CALORIE");
            if (!hasCal) return false;
            if (IsMedicine(item)) return false; // 饮料类放开（有卡路里的饮料也算食物）；药品仍排除
            string id = GetId(item);
            if (id.Contains("wine") || id.Contains("beer") || id.Contains("_seed") || id.Contains("seed_") || id.Contains("pill")) return false;
            return true;
        }
        catch { return false; }
    }

    public static bool IsDrink(GameItem item)
    {
        if (item == null) return false;
        try { return DRINK_IDS.Contains(GetId(item)); } catch { return false; }
    }

    public static bool IsMedicine(GameItem item)
    {
        if (item == null) return false;
        try { if (item.IsTag("MEDICAL")) return true; } catch { }
        try { if (item.IsTag("medical")) return true; } catch { }
        string id = GetId(item);
        string[] kw = { "pill", "injector", "bandage", "salve", "antitoxin", "stim", "blood_bag", "zerochew", "smelling_salt", "syringe" };
        foreach (string k in kw)
            if (id.Contains(k, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    public static bool IsAlc(GameItem item)
    {
        if (item == null) return false;
        try { if (ALC_IDS.Contains(GetId(item))) return true; } catch { }
        try { if (item.IsTag("ALCOHOL_VALUE")) return true; } catch { }
        // 10-04 扩展：其他 mod 酒精兜底（ID 含酒精关键词；排除已知食物/水/药品/烟草/酿酒原料防误判）
        string id = GetId(item);
        if (string.IsNullOrEmpty(id)) return false;
        if (FOOD_IDS.Contains(id) || DRINK_IDS.Contains(id) || NARC_IDS.Contains(id) || TOBACCO_IDS.Contains(id)) return false;
        if (id.Equals("wine_yeast", StringComparison.OrdinalIgnoreCase) || id.Equals("wine_superyeast", StringComparison.OrdinalIgnoreCase)
            || id.Equals("wine_berry", StringComparison.OrdinalIgnoreCase) || id.Equals("bloomberry", StringComparison.OrdinalIgnoreCase)) return false;
        string[] kw = { "wine", "beer", "vodka", "whiskey", "whisky", "rum", "gin", "tequila", "brandy", "liqueur", "liquor", "alcohol", "cider", "mead", "sake", "baijiu", "moonshine" };
        foreach (string k in kw)
            if (id.Contains(k, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    public static bool IsTobacco(GameItem item)
    {
        if (item == null) return false;
        try { if (TOBACCO_IDS.Contains(GetId(item))) return true; } catch { }
        try { if (item.IsTag("CIGARETTE_TAG")) return true; } catch { }
        return false;
    }

    public static bool IsNarcotic(GameItem item)
    {
        if (item == null) return false;
        try { if (NARC_IDS.Contains(GetId(item))) return true; } catch { }
        try { if (item.IsTag("NARCOTIC")) return true; } catch { }
        return false;
    }

    public static bool IsLottery(GameItem item)
    {
        if (item == null) return false;
        try { if (LOTTERY_IDS.Contains(GetId(item))) return true; } catch { }
        try { if (item.IsTag("LOTTERY_TICKET_TAG")) return true; } catch { }
        return false;
    }

    public static bool IsDailyNeed(GameItem item) // 蛙娘喂食照顾共用判定
    {
        try { return item != null && DAILY_NEED_KEYS.Contains((item.identifier ?? "").ToLowerInvariant()); } catch { return false; }
    }
}
