using System;
using System.Collections.Generic;
using Il2Cpp;

namespace WageSurvival;
public static class FoodIdentifyHelper
{
    internal static readonly HashSet<string> FOOD_IDS = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
        "processed_meat","small_morsel","morsel","processed_cheese","meat_scrap",
        "bottled_water","soda_red","energy_drink","nudka","red_beer",
        "processed_juice","cup_noodle","raw_meat","small_raw_meat",
        "cat_bar","li_eat_snackbar","neuroactive_perfume","small_bite","bite",
        "jerky_item","hardtack_item","soup_item","stew_item","salad_item",
        "fruit_item","veg_item","grain_item","bread_item","cheese_item",
        "egg_item","fish_item","insect_item","mushroom_item","herb_item",
        "coffee_item","tea_item","chocolate_item","candy_item","gum_item",
        "potato_item","carrot_item","tomato_item","apple_item","banana_item",
        "orange_item","grape_item","melon_item","berry_item","nut_item",
        "seed_item","bean_item","rice_item","pasta_item","flour_item",
        "sugar_item","salt_item","spice_item","oil_item","vinegar_item"
    };

    // 清洁用品ID列表（双击加清洁）
    internal static readonly HashSet<string> CLEAN_IDS = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
        "toilet_paper","toothpaste","soap","shampoo","towel","wet_wipe"
    };

    internal static readonly HashSet<string> DRINK_IDS = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
        "bottled_water","soda_red","energy_drink","nudka","red_beer",
        "processed_juice","coffee_item","tea_item","milk_item","juice_item",
        "water_bottle","soda_can","energy_can","beer_bottle","wine_bottle"
    };

    internal static readonly HashSet<string> ALC_IDS = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
        // 10-03 照老mod：ALC_IDS只有3个（red_beer/nudka/empty_beer_bottle）；其他酒靠ALCOHOL_VALUE标签兜底
        "red_beer","nudka","empty_beer_bottle"
    };

    internal static readonly HashSet<string> TOBACCO_IDS = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
        "cigarette_item","cigar_item","tobacco_pouch","pipe_item","joint_item"
    };

    internal static readonly HashSet<string> NARC_IDS = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
        "pills_item","powder_item","liquid_item","patch_item","syringe_item"
    };

    internal static readonly HashSet<string> LOTTERY_IDS = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
        "lottery_ticket","scratch_card","raffle_ticket"
    };

    internal static readonly Dictionary<string, bool> DAILY_NEED_CLEAN = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase) {
        {"bottled_water",true},{"soda_red",true},{"energy_drink",true},{"nudka",true},{"red_beer",true},
        {"processed_juice",true},{"cup_noodle",true},{"processed_meat",true},{"small_morsel",true},
        {"morsel",true},{"processed_cheese",true},{"meat_scrap",true}
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

    public static bool IsFood(GameItem item)
    {
        if (item == null) return false;
        try { if (FOOD_IDS.Contains(GetId(item))) return true; } catch { }
        try
        {
            bool hasCal = item.IsTag("CALORIE_VALUE_TAG") || item.IsTag("CALORIE");
            if (!hasCal) return false;
            if (IsMedicine(item)) return false;
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

    public static bool IsClean(GameItem item)
    {
        if (item == null) return false;
        try { return CLEAN_IDS.Contains(GetId(item)); } catch { return false; }
    }

    public static bool IsDailyNeed(GameItem item)
    {
        try { return item != null && DAILY_NEED_CLEAN.ContainsKey((item.identifier ?? "").ToLowerInvariant()); } catch { return false; }
    }
}
