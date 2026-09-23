using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace JacksonPerks;

internal sealed partial class WagePowerPerk : CustomStartingPerk
{
    // NPC访问次数记录（区分第一次来和老顾客）
    private static Dictionary<string, int> _clientVisitCount = new Dictionary<string, int>();

    // 记录NPC访问次数，返回是否是第一次来
    private static bool RecordClientVisit(string clientId)
    {
        if (string.IsNullOrEmpty(clientId)) return false;
        if (!_clientVisitCount.ContainsKey(clientId))
        {
            _clientVisitCount[clientId] = 1;
            return true; // 第一次来
        }
        _clientVisitCount[clientId]++;
        return false;
    }

    // 获取NPC访问次数
    private static int GetClientVisitCount(string clientId)
    {
        if (string.IsNullOrEmpty(clientId) || !_clientVisitCount.ContainsKey(clientId)) return 0;
        return _clientVisitCount[clientId];
    }

    // 根据物品ID判断物品类别
    private static string GetItemCategoryString(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return "未知";
        itemId = itemId.ToLower();

        // 矿石/采矿相关
        if (itemId.Contains("ore") || itemId.Contains("mineral") || itemId.Contains("rare_ore") || itemId.Contains("scrap") || itemId.Contains("metal"))
            return "矿石";

        // 水相关
        if (itemId.Contains("water") || itemId.Contains("pure_water") || itemId.Contains("aqua") || itemId.Contains("hydro") || itemId.Contains("moisture"))
            return "水";

        // 化学/药品相关
        if (itemId.Contains("chem") || itemId.Contains("pill") || itemId.Contains("med") || itemId.Contains("drug") || itemId.Contains("pharma") || itemId.Contains("solution") || itemId.Contains("solvent") || itemId.Contains("reagent"))
            return "化学药品";

        // 武器相关
        if (itemId.Contains("gun") || itemId.Contains("pistol") || itemId.Contains("shotgun") || itemId.Contains("smg") || itemId.Contains("revolver") || itemId.Contains("ammo") || itemId.Contains("mag") || itemId.Contains("weapon"))
            return "武器";

        // 食品相关
        if (itemId.Contains("meat") || itemId.Contains("food") || itemId.Contains("fruit") || itemId.Contains("nutri") || itemId.Contains("meal") || itemId.Contains("ribwich") || itemId.Contains("morsel"))
            return "食品";

        // 电子/模组相关
        if (itemId.Contains("module") || itemId.Contains("chip") || itemId.Contains("neural") || itemId.Contains("electronic") || itemId.Contains("laser") || itemId.Contains("scanner") || itemId.Contains("printer"))
            return "电子模组";

        // 种子/农业相关
        if (itemId.Contains("seed") || itemId.Contains("farm") || itemId.Contains("hydroponic") || itemId.Contains("plant") || itemId.Contains("nutrient"))
            return "种子农业";

        // 酒精/饮料相关
        if (itemId.Contains("beer") || itemId.Contains("alcohol") || itemId.Contains("wine") || itemId.Contains("liquor") || itemId.Contains("drink") || itemId.Contains("soda"))
            return "酒精饮料";

        return "其他";
    }

    // 获取NPC交易物品的主要类别
    private static string GetClientMainItemCategory(StoreClient client)
    {
        try
        {
            // 买家：看clientBuyingIdList
            if (client.clientIntent == StoreClient.ClientIntent.BUY && client.clientBuyingIdList != null)
            {
                var categories = new Dictionary<string, int>();
                foreach (var itemId in client.clientBuyingIdList)
                {
                    string cat = GetItemCategoryString(itemId);
                    if (!categories.ContainsKey(cat)) categories[cat] = 0;
                    categories[cat]++;
                }
                if (categories.Count > 0)
                {
                    return categories.OrderByDescending(x => x.Value).First().Key;
                }
            }
        }
        catch { }
        return "其他";
    }

    // ============================================================
    // 智能推荐系统 - 统计玩家购买偏好，下次刷同类物品
    // ============================================================

    // 物品类别定义
    private enum ItemCategory
    {
        Weapon,         // 武器
        WeaponPart,     // 武器配件
        Ammo,           // 弹药
        Medicine,       // 药品
        Electronic,     // 电子设备
        Food,           // 食品
        Container,      // 容器/箱子
        Tool,           // 工具/材料
        Farm,           // 农场/种植
        Special,        // 特殊物品
        Unknown         // 未知
    }

    // 物品类别映射
    private static readonly Dictionary<string, ItemCategory> ItemCategoryMap = new Dictionary<string, ItemCategory>
    {
        // 武器
        {"handmade_pistol", ItemCategory.Weapon},
        {"heavy_handmade_pistol", ItemCategory.Weapon},
        {"revolver", ItemCategory.Weapon},
        {"shotgun", ItemCategory.Weapon},
        {"smg", ItemCategory.Weapon},
        {"stun_gun", ItemCategory.Weapon},
        {"printed_gun", ItemCategory.Weapon},
        // 武器配件
        {"silencer_makeshift", ItemCategory.WeaponPart},
        {"laser_toy", ItemCategory.WeaponPart},
        {"laserz", ItemCategory.WeaponPart},
        {"internal_srings", ItemCategory.WeaponPart},
        {"ammo_display", ItemCategory.WeaponPart},
        // 弹药
        {"heavy_pistol_ammo", ItemCategory.Ammo},
        {"heavy_pistol_ammo_ml", ItemCategory.Ammo},
        {"small_pistol_ammo", ItemCategory.Ammo},
        {"small_pistol_ammo_ml", ItemCategory.Ammo},
        {"small_pistol_ammo_p", ItemCategory.Ammo},
        {"mag_22", ItemCategory.Ammo},
        // 药品
        {"med_bottle_blue", ItemCategory.Medicine},
        {"med_bottle_orange", ItemCategory.Medicine},
        {"med_bottle_red", ItemCategory.Medicine},
        {"med_bottle_small_pink", ItemCategory.Medicine},
        {"med_bottle_violet", ItemCategory.Medicine},
        {"phagimycin_pill", ItemCategory.Medicine},
        {"unlicensed_phagimycin_pill", ItemCategory.Medicine},
        {"zymide_pill", ItemCategory.Medicine},
        {"chem_finisher", ItemCategory.Medicine},
        {"chem_module", ItemCategory.Medicine},
        {"chem_scanner", ItemCategory.Medicine},
        {"chemistry_guide", ItemCategory.Medicine},
        {"common_medical", ItemCategory.Medicine},
        // 电子设备
        {"evaporator", ItemCategory.Electronic},
        {"hydroponic", ItemCategory.Electronic},
        {"printer_chip", ItemCategory.Electronic},
        {"printer_module_metal", ItemCategory.Electronic},
        {"common_electronic", ItemCategory.Electronic},
        // 食品
        {"raw_meat", ItemCategory.Food},
        {"small_raw_meat", ItemCategory.Food},
        {"morsel", ItemCategory.Food},
        {"small_morsel", ItemCategory.Food},
        {"ribwich", ItemCategory.Food},
        {"coffee_bean", ItemCategory.Food},
        {"meal_cap", ItemCategory.Food},
        {"skincare_cream", ItemCategory.Food},
        // 容器/箱子
        {"machine_bay", ItemCategory.Container},
        {"storage_bay", ItemCategory.Container},
        {"storage_bay_large", ItemCategory.Container},
        {"storage_bay_chem", ItemCategory.Container},
        {"storage_bay_gun", ItemCategory.Container},
        {"smuggler_bay", ItemCategory.Container},
        {"smuggler_bay_mini", ItemCategory.Container},
        {"mini_smuggler_bay", ItemCategory.Container},
        {"makeshift_storage_bay", ItemCategory.Container},
        {"backpack_large_military", ItemCategory.Container},
        {"expedition_box", ItemCategory.Container},
        // 工具/材料
        {"100ml_cylinder", ItemCategory.Tool},
        {"1ml_dropper", ItemCategory.Tool},
        {"25ml_cylinder", ItemCategory.Tool},
        {"50ml_cylinder", ItemCategory.Tool},
        {"5ml_dropper", ItemCategory.Tool},
        {"canister_ubs", ItemCategory.Tool},
        {"kotton_fiber", ItemCategory.Tool},
        {"fillerweed", ItemCategory.Tool},
        {"hydrored_filter", ItemCategory.Tool},
        {"postitt", ItemCategory.Tool},
        {"cert1", ItemCategory.Tool},
        {"scav_token", ItemCategory.Tool},
        {"common_chemical", ItemCategory.Tool},
        {"common_ore", ItemCategory.Tool},
        {"metal_ingot", ItemCategory.Tool},
        {"glass_shard", ItemCategory.Tool},
        {"toolbox", ItemCategory.Tool},
        {"trashcan", ItemCategory.Tool},
        {"scrap_metal", ItemCategory.Tool},
        {"screwdriver", ItemCategory.Tool},
        {"welder", ItemCategory.Tool},
        {"wirecutter", ItemCategory.Tool},
        {"crowbar", ItemCategory.Tool},
        {"hatchet", ItemCategory.Tool},
        {"evidence_box", ItemCategory.Container},
        {"sec_box", ItemCategory.Container},
        {"eng_box", ItemCategory.Container},
        {"med_box", ItemCategory.Container},
        {"service_box", ItemCategory.Container},
        {"c4", ItemCategory.Weapon},
        {"c4_set", ItemCategory.Weapon},
        {"smoke_grenade", ItemCategory.Weapon},
        {"dream_dust", ItemCategory.Medicine},
        {"nightmare_dust", ItemCategory.Medicine},
        {"black_injector", ItemCategory.Medicine},
        {"pink_injector", ItemCategory.Medicine},
        {"pink_alt_injector", ItemCategory.Medicine},
        {"large_purple_injector", ItemCategory.Medicine},
        {"pure_white_injector", ItemCategory.Medicine},
        // 农场/种植
        {"dream_cap", ItemCategory.Farm},
        {"dream_cap_seed", ItemCategory.Farm},
        {"nightmare_cap", ItemCategory.Farm},
        {"hydrored_seed", ItemCategory.Farm},
        {"bloomberry_seed", ItemCategory.Farm},
        {"hydroponic_nutrient_tablet", ItemCategory.Farm},
        {"farm_module_filterweed_production", ItemCategory.Farm},
        {"farm_module_hydrored_filter", ItemCategory.Farm},
        {"farm_module_smuggler", ItemCategory.Farm},
        // 特殊物品
        {"expedition_log", ItemCategory.Special},
        {"scav_journal", ItemCategory.Special},
        {"sign_expedition", ItemCategory.Special},
        {"dead", ItemCategory.Special},
    };

    // 玩家购买统计（各类别购买次数）
    private static Dictionary<ItemCategory, int> _purchaseStats = new Dictionary<ItemCategory, int>();

    // 玩家总购买次数
    private static int _totalPurchases = 0;

    // 游戏天数（用于剧情阶段）
    private static int _gameDay = 1;

    // 玩家行为记录
    private static int _inspectionCount = 0;      // 被检查次数
    private static int _contrabandSold = 0;       // 卖过的违禁品数量
    private static int _totalSales = 0;            // 总销售次数
    private static bool _hasBeenRobbed = false;    // 是否被抢过
    private static bool _hasBribed = false;        // 是否贿赂过

    // ============================================================
    // 特性状态持久化（读档后恢复，新游戏重置）
    // ============================================================
    internal static void SaveState()
    {
        try
        {
            string perkId = "蛙哥牛逼";
            WageSaveStore.SetInt(perkId, "totalPurchases", _totalPurchases);
            WageSaveStore.SetInt(perkId, "totalSales", _totalSales);
            WageSaveStore.SetInt(perkId, "inspectionCount", _inspectionCount);
            WageSaveStore.SetInt(perkId, "contrabandSold", _contrabandSold);
            WageSaveStore.SetInt(perkId, "gameDay", _gameDay);
            WageSaveStore.SetBool(perkId, "hasBeenRobbed", _hasBeenRobbed);
            WageSaveStore.SetBool(perkId, "hasBribed", _hasBribed);
        }
        catch (Exception ex)
        {
            Core.LogMsg("[蛙哥牛逼] 状态保存失败: " + ex.Message);
        }
    }

    internal static void LoadState()
    {
        try
        {
            string perkId = "蛙哥牛逼";
            if (!WageSaveStore.HasKey(perkId, "totalPurchases"))
            {
                return;
            }
            _totalPurchases = WageSaveStore.GetInt(perkId, "totalPurchases", 0);
            _totalSales = WageSaveStore.GetInt(perkId, "totalSales", 0);
            _inspectionCount = WageSaveStore.GetInt(perkId, "inspectionCount", 0);
            _contrabandSold = WageSaveStore.GetInt(perkId, "contrabandSold", 0);
            _gameDay = WageSaveStore.GetInt(perkId, "gameDay", 1);
            _hasBeenRobbed = WageSaveStore.GetBool(perkId, "hasBeenRobbed", false);
            _hasBribed = WageSaveStore.GetBool(perkId, "hasBribed", false);
        }
        catch (Exception ex)
        {
            Core.LogMsg("[蛙哥牛逼] 状态恢复失败: " + ex.Message);
        }
    }

    internal static void ResetState()
    {
        try
        {
            string perkId = "蛙哥牛逼";
            WageSaveStore.ClearNamespace(perkId);
            // 阶段1：旧层 ResetCache（清 runID 缓存）无新层等价物——新层 runID 实时解析无缓存，新档由 ResetForNewRun 全局清
            try { WageSaveStore.SetBool(perkId, "storageBoxGiven", false); } catch { }
            _totalPurchases = 0;
            _totalSales = 0;
            _inspectionCount = 0;
            _contrabandSold = 0;
            _gameDay = 1;
            _hasBeenRobbed = false;
            _hasBribed = false;
            _purchaseStats.Clear();
            _clientVisitCount.Clear();
            _storageBoxGiven = false; // 关键：新游戏时重置蛙哥妙妙箱给予状态，否则新档不会再给
        }
        catch (Exception ex)
        {
            Core.LogMsg("[蛙哥牛逼] 状态重置失败: " + ex.Message);
        }
    }
}
