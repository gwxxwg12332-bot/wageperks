using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace JacksonPerks;

public static partial class WageGirlSystem
{
    // ===================== 物品信息缓存（销赃精确匹配用；首次销赃回归时构建一次，此后复用） =====================
    private class ItemInfo
    {
        public long Value;
        public bool FoodDrink;
        public bool Daily;
        public bool WeaponTool;
        public bool Module;
    }
    private static System.Collections.Generic.Dictionary<string, ItemInfo> _itemInfoCache;    // ===== 物品筛选/生成工具 =====

    // 按类别找物品（跑路回归礼物用）
    private static GameItem FindCategoryItem(string mode)
    {
        try
        {
            var ids = DirectoryMaster.GetIdentifierList<GameItem>(null);
            if (ids == null || ids.Count == 0) return null;
            var pool = new System.Collections.Generic.List<string>();
            for (int i = 0; i < ids.Count; i++)
            {
                if (string.IsNullOrEmpty(ids[i]) || ids[i] == ENTITY_ID) continue;
                if (System.Array.IndexOf(Core.ExcludedItemIds, ids[i]) >= 0) continue;
                pool.Add(ids[i]);
            }
            int tries = 0;
            while (tries < 12 && pool.Count > 0)
            {
                int idx = Core.Rng.Next(pool.Count);
                string id = pool[idx];
                try
                {
                    var g = DirectoryMaster.Item(id, true);
                    if (g == null) { pool.RemoveAt(idx); tries++; continue; }
                    if (g.IsTag("STANDARD_MACHINE_TAG") || g.IsTag("CONTAINER_TAG")) { pool.RemoveAt(idx); tries++; continue; }
                    bool match = false;
                    if (mode == "food" && RobinCrusoePerk.IsFood(g)) match = true;
                    else if (mode == "drink" && RobinCrusoePerk.IsDrink(g)) match = true;
                    else if (mode == "care" && RobinCrusoePerk.IsDailyNeed(g)) match = true;
                    else if (mode == "medicine" && (RobinCrusoePerk.IsFood(g) || RobinCrusoePerk.IsDailyNeed(g))) match = true;
                    else if (mode == "sleep") match = true; // 睡眠类无对应 → 随机 1 件
                    if (match) return g;
                    pool.RemoveAt(idx); tries++;
                }
                catch { pool.RemoveAt(idx); tries++; }
            }
            return null;
        }
        catch { return null; }
    }
    private static void EnsureItemCache()
    {
        if (_itemInfoCache != null) return;
        try
        {
            _itemInfoCache = new System.Collections.Generic.Dictionary<string, ItemInfo>();
            var ids = DirectoryMaster.GetIdentifierList<GameItem>(null);
            if (ids == null) return;
            foreach (var id in ids)
            {
                try
                {
                    if (string.IsNullOrEmpty(id) || id == ENTITY_ID) continue;
                    if (System.Array.IndexOf(Core.ExcludedItemIds, id) >= 0) continue;
                    var g = DirectoryMaster.Item(id, true);
                    if (g == null) continue;
                    if (IsContraband(g)) continue; // 09-22 销赃带回过滤违禁品（只带合法货）
                    if (id.StartsWith("wage_")) continue; // 09-23 新增：mod 物品不销赃
                    if (id.Contains("book") || id.Contains("guide") || id.Contains("paper") || id.Contains("note")) continue; // 09-23 新增：文档类不销赃
                    // 09-23 修复「蛙娘会带回无法移动的场景物品」：
                    //   FindItemNearValue 第二轮会遍历【全物品缓存】，原实现只按类别+价值筛选，
                    //   于是能挑出 storage_bay / machine_bay_ext 这类建筑模块——它们只能摆在场景里、
                    //   拖不进背包（拆包：ContainerUpgradeV2.BUILDING_CONTAINER_IDS = 建筑容器清单）。
                    //   统一在此处挡掉建筑件/场景机器/系统固定件，三个带回入口（销赃/礼物/跑路）同时生效。
                    if (ContainerUpgradeV2.IsBuildingContainerId(id)) continue;
                    bool fixture = false;
                    try { fixture = g.IsTag("STANDARD_MACHINE_TAG") || g.IsTag("SYSTEM_TAG") || g.IsTag("SYSTEM_TAG_UTILITY") || g.IsTag("ITEM_HIDDEN_TAG"); } catch { }
                    if (fixture) continue;
                    var info = new ItemInfo();
                    // 预估价值（GetCurrentValue 优先——与销赃累计口径一致；失败退 unitValue）
                    try { info.Value = (int)g.GetCurrentValue(); } catch { }
                    if (info.Value <= 0) { try { info.Value = g.unitValue; } catch { } }
                    info.FoodDrink = RobinCrusoePerk.IsFood(g) || RobinCrusoePerk.IsDrink(g);
                    info.Daily = RobinCrusoePerk.IsDailyNeed(g);
                    info.WeaponTool = IsWeaponOrTool(id);
            try {
                info.Module = false;
                if (g.IsTag("MODULE_TAG") && id != "system_module_ruined" && id != GuMachineSystem.AI_MODULE_ID) {
                    int p = RobinCrusoePerk.GetTagIntSafe(g, "BONUS_PERCENTAGE_PERFORMANCE_INT");
                    int e = RobinCrusoePerk.GetTagIntSafe(g, "BONUS_PERCENTAGE_EFFICIENCY_INT");
                    int q = RobinCrusoePerk.GetTagIntSafe(g, "BONUS_PERCENTAGE_QUALITY_INT");
                    info.Module = (p + e + q) > 0; // 三维至少一个>0
                }
            } catch { }
                    _itemInfoCache[id] = info;
                    try { g.Destroy(); } catch { }
                }
                catch (System.Exception ex) { Core.LogMsg("[WageGirlSystem.StealAI_Fence] 异常: " + ex.Message); }
            }
        }
        catch (System.Exception ex) { Core.LogMsg("[WageGirlSystem.StealAI_Fence] 异常: " + ex.Message); }
    }

    // 武器/工具类别判定（id 关键词匹配，照 IsToolOrKeyOrContainer 先例）
    private static bool IsWeaponOrTool(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        string i = id.ToLowerInvariant();
        return i.Contains("weapon") || i.Contains("tool") || i.Contains("knife") || i.Contains("gun")
            || i.Contains("pistol") || i.Contains("shotgun") || i.Contains("smg") || i.Contains("rifle")
            || i.Contains("tazer") || i.Contains("stun") || i.Contains("grenade") || i.Contains("baton")
            || i.Contains("machete") || i.Contains("sword") || i.Contains("axe") || i.Contains("c4")
            || i.Contains("screwdriver") || i.Contains("welder") || i.Contains("flashlight") || i.Contains("scanner")
            || i.Contains("hammer") || i.Contains("wrench") || i.Contains("surgery") || i.Contains("combat");
    }

    // 类别匹配（cat 0=随机 1=食物饮品 2=日用品 3=武器工具）
    private static bool CategoryMatch(ItemInfo info, int cat)
    {
        if (info == null) return false;
        if (cat == 0) return true;
        if (cat == 1) return info.FoodDrink;
        if (cat == 2) return info.Daily;
        if (cat == 3) return info.WeaponTool;
        if (cat == 7) return info.Module;
        return true;
    }

    // 找价值最接近 target 的普通物品（精确遍历缓存；geq=true 要求 ≥ target；false 要求 ≤ target）
    private static GameItem FindItemNearValue(long target, int cat, bool geq)
    {
        try
        {
            EnsureItemCache();
            if (_itemInfoCache == null) return null;
            // 09-23 改：优先从蛙哥牛逼精选好物池选
            string[] goodPool = WagePowerPerk.ItemPool;
            string bestId = null; long bestDiff = long.MaxValue;
            // 第一轮：好物池
            foreach (var id in goodPool) {
                if (!_itemInfoCache.TryGetValue(id, out var info)) continue;
                if (info == null || info.Value <= 0) continue;
                if (!CategoryMatch(info, cat)) continue;
                if (geq && info.Value < target) continue;
                if (!geq && info.Value > target) continue;
                long diff = Math.Abs(info.Value - target);
                if (diff < bestDiff) { bestDiff = diff; bestId = id; }
            }
            // 第二轮：好物池没找到，从全物品找
            if (bestId == null) {
                foreach (var kv in _itemInfoCache)
                {
                    var info = kv.Value;
                    if (info == null || info.Value <= 0) continue;
                    if (!CategoryMatch(info, cat)) continue;
                    if (geq && info.Value < target) continue;
                    if (!geq && info.Value > target) continue;
                    long diff = Math.Abs(info.Value - target);
                    if (diff < bestDiff) { bestDiff = diff; bestId = kv.Key; }
                }
            }
            if (bestId == null) return null;
            return DirectoryMaster.Item(bestId, true);
        }
        catch { return null; }
    }

    // 物品放入柜台（frontInvinvElement）
    private static void AddToFront(GameItem it)
    {
        try
        {
            if (it == null) return;
            EmporiumEntry em = EmporiumEntry.Instance;
            if (em == null || em.backInvinvElement == null) return;
            var inv = (GameInventory)em.backInvinvElement;
            inv.UncheckedAccept(it); // 主仓库(后库)
        }
        catch (System.Exception ex) { Core.LogMsg("[WageGirlSystem.StealAI_Fence] 异常: " + ex.Message); }
    }

    // 正品免疫宁（09-23 修复「蛙娘带回的正品免疫宁无法使用」）
    // 根因（拆包实锤 _Demo_20260915_cpp2il / InsInjectorHelper）：
    //   Init(item)              → 只打 2 个基础标签（不含正品数据）
    //   SetGenuine(item)        → ModifyTag ×7，写入序列号/厂商/型号/真值等正品数据
    //   SetExpired/SetCounterfeit/SetUnusable → 三者都【先调 SetGenuine】再叠加坏标记
    // ⇒ SetGenuine 是"可用正品"的必要前提。而 CreateRealInjector() 与旧代码都只调了
    //   DirectoryMaster.Item（等价 Init），缺这 7 项 → 物品不可用。
    private static GameItem CreateGenuineImmunivax()
    {
        GameItem im = null;
        try { im = Il2Cpp.InsInjectorHelper.CreateRealInjector(); } catch { }
        if (im == null) { try { im = DirectoryMaster.Item("large_purple_injector", true); } catch { } }
        if (im == null) return null;
        try { Il2Cpp.InsInjectorHelper.SetGenuine(im); } catch { }
        return im;
    }

    // 物资箱：CreateLootCrate 随机箱 + 内部按 ItemPool 填充到目标价值
    private static GameItem CreateSupplyCrate(long targetValue, out long filledValue)
    {
        try
        {
            filledValue = 0;
            // 09-23 修复「物资箱有 1% 概率没有物资」之一：目标价值为 0 → 主循环一次都不进 → 空箱
            if (targetValue < 1) targetValue = 1; filledValue = 0;
            string[] boxes = { "evidence_box", "med_box", "sec_box", "service_box", "eng_box" };
            string bid = boxes[Core.Rng.Next(boxes.Length)];
            GameItem crate = CustomStorageContainer.CreateLootCrate(bid);
            if (crate == null) return null;
            GameInventory inv = null;
            try
            {
                var cw = crate.contentWindow;
                if (cw != null)
                {
                    var prop = cw.GetType().GetProperty("inventory", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    inv = prop != null ? (prop.GetValue(cw) as GameInventory) : null;
                }
            }
            catch (System.Exception ex) { Core.LogMsg("[WageGirlSystem.StealAI_Fence] 异常: " + ex.Message); }
            if (inv == null) { filledValue = crate != null ? crate.unitValue : 0; return crate; }
            var basePool = WagePowerPerk.ItemPool ?? new string[0];
            long spent = 0; int tries = 0, filled = 0;
            bool wantContraband = Core.Rng.Next(100) < 5; // 好物95% / 违禁5%
            var pool = new System.Collections.Generic.List<string>(basePool);
            while (spent < targetValue && tries < 40 && pool.Count > 0)
            {
                int idx = Core.Rng.Next(pool.Count);
                string id = pool[idx]; pool.RemoveAt(idx);
                GameItem it = null;
                try { it = DirectoryMaster.Item(id, true); } catch { }
                if (it == null) { tries++; continue; }
                bool isContra = false;
                try { isContra = ContrabandHelper.GetContrabandLevel(it) > 0; } catch { }
                if (wantContraband != isContra) { tries++; continue; } // 分流不符跳过
                try { inv.UncheckedAccept(it); spent += it.unitValue; filled++; } catch { tries++; }
            }
            // 09-23 修复「物资箱有 1% 概率没有物资」之二：主循环可能因违禁分流不符（5% 分支尤甚）、
            // 创建失败或 UncheckedAccept 抛错而一件都没塞进去 → 空箱。
            // 兜底：忽略违禁偏好，从全池硬性塞入至少 1 件——保证物资箱永不为空。
            if (filled == 0)
            {
                var fb = new System.Collections.Generic.List<string>(basePool);
                int fbTries = 0;
                while (filled == 0 && fbTries < 40 && fb.Count > 0)
                {
                    int idx = Core.Rng.Next(fb.Count);
                    string id = fb[idx]; fb.RemoveAt(idx);
                    fbTries++;
                    GameItem it = null;
                    try { it = DirectoryMaster.Item(id, true); } catch { }
                    if (it == null) continue;
                    try { inv.UncheckedAccept(it); filled++; } catch { }
                }
            }
            filledValue = spent; // 09-24 修：返回实际塞入物品总价值，克扣按这个算
            return crate;
        }
        catch { filledValue = 0; return null; }
    }
}
