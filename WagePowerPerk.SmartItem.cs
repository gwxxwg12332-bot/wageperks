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

    // 物品池（216个有效物品，已删除打印机/水培化学/无效模板/名片）
    internal static readonly string[] ItemPool = {
    "dream_cap","handmade_pistol","heavy_handmade_pistol","heavy_pistol_ammo","makeshift_storage_bay","phagimycin_pill","revolver","shotgun","skincare_cream","small_pistol_ammo","small_pistol_ammo_p","smg","stun_gun","scav_token","storage_bay_large","machine_bay","storage_bay","smuggler_bay","toolbox","evidence_box","sec_box","eng_box","med_box","service_box","c4","c4_set","smoke_grenade","eng_keycard","med_keycard","sec_keycard","ser_keycard","cmd_keycard","sup_keycard","advanced_flux_agent","aug_scanner","black_lamp","blood_bag","blue_blood_bag","flashbang_grenade","furnace","heavy_pistol_ammo_nl","heavy_pistol_ammo_p","kotton_fabric","mirage_projector","neuroactive_perfume","pheromone_perfume","powerblock","recharger","restricted_chemical","rubbing_alcohol","salve","security_alarm","turbo_booster","turbo_booster_adv","wine_superyeast","wine_yeast_infinite","zyanide_pill","node_medium","stun_baton","surgery_tool","tazer","bandage_item","hemostatic_bandage_item","topical_bandage_item","black_injector","pink_injector","pink_alt_injector","large_purple_injector","pure_white_injector","water_filter_adv","bottled_water_premium","fanny_pack","combat_knife","combat_machete","bottle_printer","wine_rack","portable_water_purifier","water_purifier","small_raw_meat","raw_meat",};

    // 获取玩家当前现金（用于按预算过滤物品）
    private static long GetPlayerCash()
    {
        try
        {
            PlayerStore store = PlayerStore.Instance;
            if (store != null)
            {
                FieldInfo[] fields = typeof(PlayerStore).GetFields(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (FieldInfo f in fields)
                {
                    string name = f.Name.ToLower();
                    if (name.Contains("cash") || name.Contains("money") || name.Contains("currency"))
                    {
                        object val = f.GetValue(store);
                        if (val is long longVal) return longVal;
                        if (val is int intVal) return intVal;
                    }
                }
            }
        }
        catch { }
        return 500; // 默认500（新手期）
    }

    // 物品价值缓存（避免每次创建临时GameItem对象）
    private static readonly Dictionary<string, long> _itemValueCache = new Dictionary<string, long>();

    // 获取物品基础价值（带缓存）
    private static long GetItemBaseValue(string itemId)
    {
        if (_itemValueCache.TryGetValue(itemId, out long cached))
            return cached;

        long value = 0;
        try
        {
            if (DirectoryMaster.Has<GameItem>(itemId))
            {
                GameItem tempItem = DirectoryMaster.Item(itemId, false);
                if (tempItem != null)
                {
                    try { value = tempItem.unitBaseValue; } catch { }
                    if (value <= 0) try { value = tempItem.unitValue; } catch { }
                }
            }
        }
        catch { }

        _itemValueCache[itemId] = value;
        return value;
    }

    // 按预算智能随机选择物品（确保玩家能买得起，批发价不超过剩余预算）
    internal static string SmartRandomItemByBudget(long remainingBudget = -1)
    {
        try
        {
            long budget = remainingBudget;
            if (budget < 0)
            {
                long playerCash = GetPlayerCash();
                budget = (long)(playerCash * 0.5); // 默认：蛙哥推荐物品总预算不超过玩家现金50%（给客户自带物品留空间）
            }
            if (budget < 50) budget = 50; // 最低预算50

            ItemCategory topCategory = GetTopCategory();
            List<string> affordableItems = new List<string>();

            // 先收集所有买得起的物品（用缓存获取价值，不创建临时对象）
            foreach (string itemId in ItemPool)
            {
                long itemValue = GetItemBaseValue(itemId);
                // 批发价按基础价值的60%估算（卖家卖给玩家的价格通常比基础价值低）
                long wholesalePrice = (long)(itemValue * 0.6);
                if (wholesalePrice < 1) wholesalePrice = 1;

                if (wholesalePrice <= budget)
                {
                    affordableItems.Add(itemId);
                }
            }

            // 如果没有买得起的物品，返回最便宜的raw_meat
            if (affordableItems.Count == 0)
                return "raw_meat";

            // 50%概率选玩家偏好类别的物品（如果有）
            if (topCategory != ItemCategory.Unknown && _purchaseStats.ContainsKey(topCategory) && _purchaseStats[topCategory] >= 3)
            {
                if (Core.Rng.Next(2) == 0)
                {
                    List<string> categoryAffordable = affordableItems.FindAll(id => GetItemCategory(id) == topCategory);
                    if (categoryAffordable.Count > 0)
                        return categoryAffordable[Core.Rng.Next(categoryAffordable.Count)];
                }
            }

            // 完全随机选买得起的物品
            return affordableItems[Core.Rng.Next(affordableItems.Count)];
        }
        catch (Exception ex)
        {
            Core.LogMsg("[蛙哥牛逼] SmartRandomItemByBudget失败: " + ex.Message);
            return "raw_meat";
        }
    }

    // 获取玩家经济状态
    private enum PlayerEconomy { Poor, Normal, Rich, Loaded }

    private static PlayerEconomy GetPlayerEconomy()
    {
        try
        {
            // 尝试从PlayerStore获取现金
            PlayerStore store = PlayerStore.Instance;
            if (store != null)
            {
                // 反射查找现金相关字段
                FieldInfo[] fields = typeof(PlayerStore).GetFields(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (FieldInfo f in fields)
                {
                    string name = f.Name.ToLower();
                    if (name.Contains("cash") || name.Contains("money") || name.Contains("currency"))
                    {
                        object val = f.GetValue(store);
                        if (val is long longVal)
                        {
                            if (longVal < 500) return PlayerEconomy.Poor;
                            if (longVal < 5000) return PlayerEconomy.Normal;
                            if (longVal < 20000) return PlayerEconomy.Rich;
                            return PlayerEconomy.Loaded;
                        }
                        if (val is int intVal)
                        {
                            if (intVal < 500) return PlayerEconomy.Poor;
                            if (intVal < 5000) return PlayerEconomy.Normal;
                            if (intVal < 20000) return PlayerEconomy.Rich;
                            return PlayerEconomy.Loaded;
                        }
                    }
                }
            }
        }
        catch { }
        // 默认根据购买次数判断
        if (_totalPurchases < 10) return PlayerEconomy.Poor;
        if (_totalPurchases < 30) return PlayerEconomy.Normal;
        if (_totalPurchases < 80) return PlayerEconomy.Rich;
        return PlayerEconomy.Loaded;
    }

    // 获取玩家经验等级
    private enum PlayerLevel { Newbie, Experienced, Veteran, Legend }

    private static PlayerLevel GetPlayerLevel()
    {
        if (_gameDay <= 7) return PlayerLevel.Newbie;
        if (_gameDay <= 21) return PlayerLevel.Experienced;
        if (_gameDay <= 50) return PlayerLevel.Veteran;
        return PlayerLevel.Legend;
    }

    // 获取玩家声誉状态
    private enum PlayerReputation { Shady, Neutral, Reputable, Legendary }

    private static PlayerReputation GetPlayerReputation()
    {
        // 根据购买次数、检查次数、违禁品销售综合判断
        int score = _totalPurchases - _inspectionCount * 5 - _contrabandSold * 3;
        if (score < 0) return PlayerReputation.Shady;
        if (score < 20) return PlayerReputation.Neutral;
        if (score < 60) return PlayerReputation.Reputable;
        return PlayerReputation.Legendary;
    }

    // 获取物品类别
    private static ItemCategory GetItemCategory(string itemId)
    {
        if (ItemCategoryMap.TryGetValue(itemId, out var category))
        {
            return category;
        }
        return ItemCategory.Unknown;
    }

    // 记录玩家购买
    private static void RecordPurchase(string itemId)
    {
        try
        {
            ItemCategory category = GetItemCategory(itemId);
            if (!_purchaseStats.ContainsKey(category))
            {
                _purchaseStats[category] = 0;
            }
            _purchaseStats[category]++;
            _totalPurchases++;
            // 每10次购买保存一次状态（避免频繁写入PlayerPrefs）
            if (_totalPurchases % 10 == 0)
            {
                SaveState();
            }
        }
        catch
        { }
    }

    // 获取玩家最偏好的类别
    private static ItemCategory GetTopCategory()
    {
        ItemCategory top = ItemCategory.Unknown;
        int maxCount = 0;
        foreach (var kvp in _purchaseStats)
        {
            if (kvp.Value > maxCount && kvp.Key != ItemCategory.Unknown)
            {
                maxCount = kvp.Value;
                top = kvp.Key;
            }
        }
        return top;
    }

    // 智能随机选择物品（优先选择玩家偏好类别的物品）
    internal static string SmartRandomItem()
    {
        try
        {
            ItemCategory topCategory = GetTopCategory();

            // 如果玩家有明确偏好（购买超过3次），50%概率选同类物品
            if (topCategory != ItemCategory.Unknown && _purchaseStats[topCategory] >= 3)
            {
                if (Core.Rng.Next(2) == 0)
                {
                    // 从偏好类别中随机选（允许可开箱容器）
                    List<string> categoryItems = new List<string>();
                    foreach (string itemId in ItemPool)
                    {
                        ItemCategory ic = GetItemCategory(itemId);
                        if (ic == topCategory && (ic != ItemCategory.Container || IsLootableContainer(itemId)))
                        {
                            categoryItems.Add(itemId);
                        }
                    }
                    if (categoryItems.Count > 0)
                    {
                        string selected = categoryItems[Core.Rng.Next(categoryItems.Count)];
                        return selected;
                    }
                }
            }

            // 否则完全随机（允许非容器 + 可开箱容器）
            for (int attempt = 0; attempt < 20; attempt++)
            {
                string candidate = ItemPool[Core.Rng.Next(ItemPool.Length)];
                ItemCategory cat = GetItemCategory(candidate);
                if (cat != ItemCategory.Container || IsLootableContainer(candidate))
                {
                    return candidate;
                }
            }
            return "raw_meat";
        }
        catch
        {
            for (int attempt = 0; attempt < 20; attempt++)
            {
                string candidate = ItemPool[Core.Rng.Next(ItemPool.Length)];
                ItemCategory cat = GetItemCategory(candidate);
                if (cat != ItemCategory.Container || IsLootableContainer(candidate))
                {
                    return candidate;
                }
            }
            return "raw_meat";
        }
    }

    // 可开箱容器白名单：只保留游戏原版带内容的箱子（LootCrate*）；expedition_box/toolbox/trashcan 游戏里本来就是空箱子，
    // storage_bay/smuggler_bay 等是建筑模块——都不进随机
    private static bool IsLootableContainer(string itemId)
    {
        if (itemId == "evidence_box" || itemId == "sec_box" || itemId == "med_box" ||
            itemId == "eng_box" || itemId == "service_box")
            return true;
        return false;
    }

    // 创建带内容的可开箱箱子：优先用 PreBuiltItemHelper.LootCrate*（原版掉落），否则塞通用内容
    private static GameItem CreateLootCrate(string itemId)
    {
        try
        {
            var map = new System.Collections.Generic.Dictionary<string, string> {
                { "evidence_box", "LootCrateEvidence" },
                { "med_box", "LootCrateMedical" },
                { "sec_box", "LootCrateSecurity" },
                { "service_box", "LootCrateService" },
                { "eng_box", "LootCrateEngineering" }
            };
            if (map.TryGetValue(itemId, out string methodName))
            {
                var t = typeof(Il2Cpp.PreBuiltItemHelper);
                var mi = t.GetMethod(methodName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (mi != null)
                {
                    var crate = mi.Invoke(null, null) as GameItem;
                    if (crate != null)
                    {
                        try { Il2Cpp.LockHelper.LockUpContainer(crate); } catch { }
                        return crate;
                    }
                }
            }
            // 映射不到（expedition_box/toolbox/trashcan）：DirectoryMaster 创建 + 塞通用内容
            var gi = DirectoryMaster.Item(itemId, true);
            if (gi != null)
            {
                try
                {
                    var loot = new Il2CppSystem.Collections.Generic.List<GameItem>();
                    string[] ids = { "raw_meat", "med_bottle_blue", "energy_credit", "morsel" };
                    foreach (string id in ids)
                    {
                        try { var li = DirectoryMaster.Item(id, true); if (li != null) loot.Add(li); } catch { }
                    }
                    Il2Cpp.DropTableFournitureDirectory.AddLootToFourniture(gi, loot);
                }
                catch { }
            }
            return gi;
        }
        catch (Exception ex)
        {
            Core.LogMsg("[蛙哥牛逼] CreateLootCrate失败 " + itemId + ": " + ex.Message);
            return null;
        }
    }

    // ============================================================
    // 买家需求标签：从物品读取业务标签（烟酒品食物等），用于UI显示所需物品
    // ============================================================
    private static readonly Dictionary<string, string[]> _itemBuyTagsCache = new Dictionary<string, string[]>();

    // 系统/状态标签黑名单（不显示为需求标签）
    private static readonly HashSet<string> _nonBuyTagBlacklist = new HashSet<string>
    {
        "TAG_NOT_PURCHASED", "not_purchased", "stolen", "TAG_STOLEN", "contraband",
        "CONTAINER_TAG", "ITEM_HIDDEN_TAG", "SYSTEM_TAG", "SYSTEM_TAG_UTILITY",
        "hidden", "owned", "custom", "locked", "TAG_LOCKED", "lock",
        "crypto", "evidence", "wanted", "arrested", "reported", "illegal",
        "TAG_USEABLE", "USEABLE", "TAG_DISPOSABLE", "DISPOSABLE"
    };

    // 类别fallback标签（反射读取tagSystem失败时兜底）
    private static readonly Dictionary<ItemCategory, string[]> CategoryBuyTagFallback = new Dictionary<ItemCategory, string[]>
    {
        { ItemCategory.Weapon, new[] { "weapon", "gun" } },
        { ItemCategory.WeaponPart, new[] { "weapon", "gun_part" } },
        { ItemCategory.Ammo, new[] { "ammo" } },
        { ItemCategory.Medicine, new[] { "medical", "medicine" } },
        { ItemCategory.Electronic, new[] { "electronic", "module" } },
        { ItemCategory.Food, new[] { "food" } },
        { ItemCategory.Container, new[] { "container" } },
        { ItemCategory.Tool, new[] { "tool" } },
        { ItemCategory.Farm, new[] { "farm", "plant" } },
        { ItemCategory.Special, new[] { "rare" } },
        { ItemCategory.Unknown, new[] { "misc" } },
    };

    // 获取物品的批发价（基础价值*60%，与SmartRandomItemByBudget一致）
    private static long GetItemWholesalePrice(string itemId)
    {
        long itemValue = GetItemBaseValue(itemId);
        long wholesalePrice = (long)(itemValue * 0.6);
        if (wholesalePrice < 1) wholesalePrice = 1;
        return wholesalePrice;
    }

    // 读取物品的业务标签（反射tagSystem，过滤黑名单，只收集启用标签）
    private static string[] GetItemBuyTags(string itemId)
    {
        if (_itemBuyTagsCache.TryGetValue(itemId, out var cached)) return cached;
        List<string> tags = new List<string>();
        try
        {
            if (DirectoryMaster.Has<GameItem>(itemId))
            {
                GameItem temp = DirectoryMaster.Item(itemId, false);
                if (temp != null)
                {
                    var fi = temp.GetType().GetField("tagSystem", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (fi == null) fi = temp.GetType().GetField("myTags", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (fi != null)
                    {
                        object ts = fi.GetValue(temp);
                        if (ts != null)
                        {
                            var dictProp = ts.GetType().GetProperty("dict", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                            if (dictProp != null)
                            {
                                object dict = dictProp.GetValue(ts);
                                if (dict is System.Collections.IDictionary idict)
                                {
                                    foreach (System.Collections.DictionaryEntry de in idict)
                                    {
                                        string key = de.Key?.ToString() ?? "";
                                        if (string.IsNullOrEmpty(key) || _nonBuyTagBlacklist.Contains(key)) continue;
                                        if (key.StartsWith("TAG_") || key.StartsWith("tag_") || key.StartsWith("ITEM_")) continue;
                                        // 只收集启用的标签
                                        bool enabled = false;
                                        try
                                        {
                                            object val = de.Value;
                                            if (val != null)
                                            {
                                                var m = val.GetType().GetMethod("IsEnabled");
                                                if (m != null) enabled = (bool)m.Invoke(val, null);
                                            }
                                        }
                                        catch { enabled = false; }
                                        if (!enabled) continue;
                                        if (!tags.Contains(key)) tags.Add(key);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        catch { }
        if (tags.Count == 0)
        {
            // fallback：类别映射标签
            ItemCategory cat = GetItemCategory(itemId);
            if (CategoryBuyTagFallback.TryGetValue(cat, out var fb))
                tags.AddRange(fb);
        }
        _itemBuyTagsCache[itemId] = tags.ToArray();
        return tags.ToArray();
    }

    // 确保 BUY 客户有购买需求标签（clientBuyingTagList），让 UI 显示客户想买什么
    internal static void EnsureBuyTagsForClient(StoreClient client)
    {
        try
        {
            if (client == null || client.clientIntent != StoreClient.ClientIntent.BUY) return;

            // 诊断：打印每个 BUY 客户的标签/清单状态
            try
            {
                string _curTag = "[]";
                if (client.clientBuyingTagList != null) _curTag = string.Join(",", client.clientBuyingTagList.ToArray());
                string _curId = "[]";
                if (client.clientBuyingIdList != null) _curId = string.Join(",", client.clientBuyingIdList.ToArray());
            }
            catch { }

            // 已有标签，跳过
            if (client.clientBuyingTagList != null && client.clientBuyingTagList.Count > 0) return;
            
            // 收集物品ID：优先用已有购买清单，否则随机生成
            List<string> items = new List<string>();
            if (client.clientBuyingIdList != null)
            {
                foreach (var id in client.clientBuyingIdList)
                    if (!string.IsNullOrEmpty(id) && !items.Contains(id))
                        items.Add(id);
            }
            
            // 没有购买清单：用 SetRandomBuyItems 生成（同时设置 ID 和标签）
            if (items.Count == 0)
            {
                SetRandomBuyItems(client);
                return;
            }
            
            // 有购买清单：从物品生成标签
            List<string> tags = new List<string>();
            foreach (var id in items)
            {
                try
                {
                    foreach (var t in GetItemBuyTags(id))
                        if (!tags.Contains(t)) tags.Add(t);
                }
                catch { }
            }
            
            if (client.clientBuyingTagList == null)
                client.clientBuyingTagList = new Il2CppSystem.Collections.Generic.List<string>();
            else
                client.clientBuyingTagList.Clear();
            foreach (var t in tags)
                client.clientBuyingTagList.Add(t);
            
        }
        catch (Exception ex)
        {
            Core.LogMsg("[蛙哥牛逼] EnsureBuyTagsForClient异常: " + ex.Message);
        }
    }

    // 买家：设置随机购买清单
    internal static void SetRandomBuyItems(StoreClient client)
    {
        try
        {
            int count = Core.Rng.Next(3, 7);
            List<string> selectedItems = new List<string>();

            for (int i = 0; i < count; i++)
            {
                string itemId = ItemPool[Core.Rng.Next(ItemPool.Length)];
                if (DirectoryMaster.Has<GameItem>(itemId) && !selectedItems.Contains(itemId))
                {
                    selectedItems.Add(itemId);
                }
            }

            if (selectedItems.Count == 0)
            {
                selectedItems.Add("raw_meat");
                selectedItems.Add("morsel");
            }

            if (client.clientBuyingIdList == null)
                client.clientBuyingIdList = new Il2CppSystem.Collections.Generic.List<string>();
            else
                client.clientBuyingIdList.Clear();

            foreach (string itemId in selectedItems)
                client.clientBuyingIdList.Add(itemId);

            // 设置所需物品标签（游戏UI显示买家需求用，如烟酒品食物，玩家不用看对话就懂）
            try
            {
                List<string> selectedTags = new List<string>();
                foreach (string itemId in selectedItems)
                {
                    string[] tags = GetItemBuyTags(itemId);
                    foreach (string t in tags)
                        if (!selectedTags.Contains(t))
                            selectedTags.Add(t);
                }

                if (client.clientBuyingTagList == null)
                    client.clientBuyingTagList = new Il2CppSystem.Collections.Generic.List<string>();
                else
                    client.clientBuyingTagList.Clear();

                foreach (string tag in selectedTags)
                    client.clientBuyingTagList.Add(tag);
            }
            catch (Exception ex)
            {
                Core.LogMsg("[蛙哥牛逼] 设置需求标签失败: " + ex.Message);
            }
        }
        catch
        { }
    }
}
