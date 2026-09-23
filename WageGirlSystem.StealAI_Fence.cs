using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace JacksonPerks;

// ============================================================
// 蛙娘系统（09-21 开工，话术 v9 拆包回填 9 项）
// 拍板：实体占地 2×3，全局常驻（不选任何特性也出现）
// 阶段 1：实体注册 + 六维状态 + 常驻面板 + 每日衰减 + 双击面板
// 阶段 2+：喂食/照顾好感、在场增益（预算×4+议价+50）、自动叫客+治安预判、
//          偷钱循环+自主偷拿、好物+销赃+跑路回归（后续迭代）
// ============================================================
public static partial class WageGirlSystem
{

    // 日常自主偷拿：饥饿/口渴/心情差自拿对应类恢复；心情好随机顺走（件数/价值按好感）
    private static void TrySnatch()
    {
        try
        {
            // 09-22 优化：好感≥N → 100% 不偷（害羞档位；CFG：WageGirlStealNoStealAff）
            if (GetAffection() >= BuildConfig.WageGirlStealNoStealAff) return;
            int sat = GetStat(K_SAT), th = GetStat(K_TH), mood = GetStat(K_MOOD);
            string mode = null; string msg = null;
            if (sat < 30) { mode = "food"; msg = "蛙娘饿坏了，偷吃了你的食物"; }
            else if (th < 30) { mode = "drink"; msg = "蛙娘渴坏了，偷喝了你的饮品"; }
            else if (mood < 30) { if (GetStat(K_CLEAN) >= 100) return; mode = "care"; msg = "蛙娘心情很差，拿走了你的日用品"; }
            else if (mood >= 60) { return; } // 09-22 砍：心情好不偷（回归只带回来给玩家）
            else return;
            int aff = GetAffection();
            int count = aff < 30 ? 1 : (aff < 70 ? 2 : 3);
            string valueMode = "random"; // 09-22 改：不按价值偷，随机挑
            int stolen = StealItems(mode, count, valueMode, out var stolenNames);
            if (stolen > 0)
            {
                SetSleepDebt(GetSleepDebt() + BuildConfig.WageGirlSnatchSleepDebt); // 偷拿熬夜 -N 睡眠（次日结算；CFG）
                if (mode == "food") SetStat(K_SAT, GetStat(K_SAT) + 30);
                else if (mode == "drink") SetStat(K_TH, GetStat(K_TH) + 30);
                else if (mode == "care") { SetStat(K_MOOD, GetStat(K_MOOD) + 20); SetStat(K_CLEAN, GetStat(K_CLEAN) + 10); }
                string sn = (stolenNames != null && stolenNames.Count > 0) ? "：" + string.Join("、", stolenNames) : "";
                ReportLine(LangHelper.T(msg + sn + "（" + stolen + " 件）", msg + " (" + (stolenNames != null ? string.Join(", ", stolenNames) : "") + ", " + stolen + " items)"));
            }
        }
        catch { }
    }

    // 真饮品判定：水量 > 0 或有卡路里值才算喝得到东西（空瓶/无记录的饮品不算）
    private static bool HasDrinkContent(GameItem it)
    {
        try { if (it == null) return false; } catch { return false; }
        try { if (RobinCrusoePerk.GetWaterMl(it) > 0) return true; } catch { }
        try { if (RobinCrusoePerk.GetCalorie(it) > 0) return true; } catch { }
        return false;
    }

    // 从店里找目标偷拿：mode 限定类别；count 件数；valueMode highest/random/low
    private static int StealItems(string mode, int count, string valueMode, out System.Collections.Generic.List<string> stolenNames)
    {
        stolenNames = new System.Collections.Generic.List<string>();
        try
        {
            EmporiumEntry em = EmporiumEntry.Instance;
            if (em == null) return 0;
            var invs = new GameInventory[] {
                (GameInventory)em.frontInvinvElement,
                (GameInventory)em.showcaseElement,
                (GameInventory)em.invElement
            };
            var candidates = new System.Collections.Generic.List<GameItem>();
            foreach (var inv in invs)
            {
                if (inv == null || inv.childItems == null) continue;
                for (int i = 0; i < inv.childItems.Count; i++)
                {
                    var it = inv.childItems[i];
                    if (it == null) continue;
                    if (it.identifier == ENTITY_ID) continue;

                    // 09-22 白名单制：只偷三个白名单内的物品（不靠排除词）
                    bool allow = false;
                    if (mode == "food") allow = RobinCrusoePerk.IsFood(it);
                    // 09-23 修复「没有饮品的时候蛙娘依然会偷喝」：DRINK_IDS 含 empty_beer_bottle
                    // 这类空容器、以及部分饮品无水量/无卡路里记录 → 会被 IsDrink 判成饮品而"偷喝"，
                    // 实际一口都喝不到。加"真饮品"校验：店里没有能喝的东西 → 候选为空 → 不偷不报。
                    else if (mode == "drink") allow = RobinCrusoePerk.IsDrink(it) && HasDrinkContent(it);
                    else if (mode == "care") allow = RobinCrusoePerk.IsDailyNeed(it);
                    else allow = RobinCrusoePerk.IsFood(it) || RobinCrusoePerk.IsDrink(it) || RobinCrusoePerk.IsDailyNeed(it);
                    if (!allow) continue;
                    // 偷拿价值上限：好感<30 偷≤Low，30-70 偷≤Mid，>70 偷≤High（CFG：WageGirlSnatchValue*）
                    int aff = GetAffection();
                    int maxVal = aff < 30 ? BuildConfig.WageGirlSnatchValueLow : (aff < 70 ? BuildConfig.WageGirlSnatchValueMid : BuildConfig.WageGirlSnatchValueHigh);
                    if (it.unitValue > maxVal) continue;
                    // [蛙诊] 诊断日志（发布前删）
                    Core.LogMsg("[蛙诊] 进候选: id=" + (it.identifier ?? "?") + " val=" + it.unitValue + " isFood=" + RobinCrusoePerk.IsFood(it) + " isDrink=" + RobinCrusoePerk.IsDrink(it) + " isDaily=" + RobinCrusoePerk.IsDailyNeed(it) + " mode=" + mode);
                    candidates.Add(it);
                }
            }
            if (candidates.Count == 0) return 0;
            var picked = new System.Collections.Generic.List<GameItem>();
            if (valueMode == "highest")
            {
                GameItem best = candidates[0];
                foreach (var c in candidates) if (c.unitValue > best.unitValue) best = c;
                picked.Add(best);
            }
            else if (valueMode == "low")
            {
                var pool = new System.Collections.Generic.List<GameItem>(candidates);
                pool.Sort((a, b) => a.unitValue.CompareTo(b.unitValue));
                for (int i = 0; i < Math.Min(count, pool.Count); i++) picked.Add(pool[i]);
            }
            else
            {
                var pool = new System.Collections.Generic.List<GameItem>(candidates);
                for (int i = 0; i < Math.Min(count, pool.Count); i++)
                {
                    int idx = Core.Rng.Next(pool.Count);
                    picked.Add(pool[idx]);
                    pool.RemoveAt(idx);
                }
            }
            int stolen = 0; long stolenVal = 0;
            foreach (var p in picked)
            {
                try { stolenVal += p.unitValue; } catch { }
                try { p.Destroy(); stolen++; } catch { try { if (p.parentInventory != null) { p.parentInventory.Expel(p); stolen++; } } catch { } }
            }
            try { SetStat("stolenValue", GetStat("stolenValue", 0) + (int)Math.Min(stolenVal, int.MaxValue)); } catch { }
            return stolen;
        }
        catch { stolenNames = null; return 0; }
    }

    // 回归带物：按偷钱额 × 好感比例预算，随机生成物品（单件/累计价值 ≤ 预算）放入柜台
    private static void GiveBackItem(int stealAmt)
    {
        try
        {
            int aff = GetAffection();
            float ratio = 0.5f + (aff / 100f) * 1.5f; // 09-22 优化：好感 0→50%、100→200%（高好感喂钱是投资）
            long budget = Math.Max(1, (int)(stealAmt * ratio));
            EmporiumEntry em = EmporiumEntry.Instance;
            var ids = DirectoryMaster.GetIdentifierList<GameItem>(null);
            if (ids == null || ids.Count == 0) { ReportLine(LangHelper.T("蛙娘回来了（没带什么值钱的东西）", "Wage Girl is back (empty-handed)")); return; }
            var pool = new System.Collections.Generic.List<string>();
            for (int i = 0; i < ids.Count; i++)
            {
                if (string.IsNullOrEmpty(ids[i]) || ids[i] == ENTITY_ID) continue;
                if (System.Array.IndexOf(Core.ExcludedItemIds, ids[i]) >= 0) continue; // 全局黑名单（rare_electronic 等）
                pool.Add(ids[i]);
            }
            long spent = 0;
            var names = new System.Collections.Generic.List<string>();
            for (int i = 0; i < 2 && pool.Count > 0 && spent < budget; i++)
            {
                GameItem it = null; string id = null; int tries = 0;
                while (tries < 12 && pool.Count > 0)
                {
                    int idx = Core.Rng.Next(pool.Count);
                    id = pool[idx];
                    try
                    {
                        it = DirectoryMaster.Item(id, true);
                        if (it == null) { pool.RemoveAt(idx); tries++; continue; }
                        if (it.IsTag("STANDARD_MACHINE_TAG") || it.IsTag("CONTAINER_TAG")) { pool.RemoveAt(idx); tries++; continue; }
                        long v = it.unitValue;
                        if (v <= 0 || v > budget - spent) { pool.RemoveAt(idx); tries++; continue; } // 超预算/无价值 → 换
                        break;
                    }
                    catch { pool.RemoveAt(idx); tries++; }
                }
                if (it == null) continue;
                spent += it.unitValue;
                try
                {
                    if (em != null && em.frontInvinvElement != null)
                    {
                        var inv = (GameInventory)em.frontInvinvElement;
                        var slot = em.frontInvinvElement.TryFindOneValidInventorySlot(it, false);
                        if (slot != null) { try { slot.TryAcceptOnce(); } catch { } }
                        else inv.UncheckedAccept(it);
                        names.Add(ModCannibalism.GetName(it));
                    }
                }
                catch { }
            }
            if (names.Count > 0) ReportLine(LangHelper.T("蛙娘回来了，带了点东西回来：" + string.Join("、", names), "Wage Girl is back with: " + string.Join(", ", names)));
            else ReportLine(LangHelper.T("蛙娘回来了（没带什么值钱的东西）", "Wage Girl is back (empty-handed)"));
        }
        catch { }
    }

    // 偷钱消失：从场景所有网格 + 容器内部移除蛙娘实体（回归时 TryGiveToBackpack 重发）
    private static void RemoveGirlFromScene()
    {
        try
        {
            EmporiumEntry em = EmporiumEntry.Instance;
            if (em == null) return;
            var invs = new GameInventory[] {
                (GameInventory)em.invElement,
                (GameInventory)em.backInvinvElement,
                (GameInventory)em.backInvinvElementCounter,
                (GameInventory)em.frontInvinvElement,
                (GameInventory)em.showcaseElement
            };
            foreach (var inv in invs) RemoveGirlFromInv(inv);
            // 兜底：全量找蛙娘移除（含 5 网格外/容器内部漏网）
            try
            {
                var all = PlayerStore.Instance.FindAllItem();
                if (all != null)
                {
                    for (int i = 0; i < all.Count; i++)
                    {
                        var it = all[i];
                        if (it == null || it.identifier != ENTITY_ID) continue;
                        try { it.parentInventory?.Expel(it); } catch { }
                        try { it.Destroy(); } catch { }
                    }
                }
            }
            catch { }
        }
        catch { }
    }

    // 单个网格内移除蛙娘（含顶层容器 contentWindow 内部递归）
    private static void RemoveGirlFromInv(GameInventory inv)
    {
        if (inv == null || inv.childItems == null) return;
        for (int i = inv.childItems.Count - 1; i >= 0; i--)
        {
            try
            {
                var it = inv.childItems[i];
                if (it == null) continue;
                if (it.identifier == ENTITY_ID)
                {
                    try { it.parentInventory?.Expel(it); } catch { }
                    try { it.Destroy(); } catch { }
                    try { inv.childItems.RemoveAt(i); } catch { }
                    continue;
                }
                if (it.contentWindow != null)
                {
                    var inner = AddictOfficerEvent.GetInnerInventory(it);
                    if (inner != null) RemoveGirlFromInv(inner);
                }
            }
            catch { }
        }
    }

    // ===================== 阶段 6：好物 / 销赃 / 跑路回归（09-22 用户拍板并行） =====================

    // 违禁品/武器/赃物判定（09-21 扩：武器+赃物也收）
    private static bool IsContraband(GameItem it)
    {
        try { if (Il2Cpp.ContrabandHelper.GetContrabandLevel(it) > 0) return true; } catch { }
        // 武器类也收
        try { string id = (it.identifier ?? "").ToLowerInvariant(); if (IsWeaponOrTool(id)) return true; } catch { }
        // 赃物（stolen tag）也收
        try { if (it.IsTag("stolen") || it.IsTag("TAG_STOLEN")) return true; } catch { }
        return false;
    }

    // 面板「销赃」按钮：带走当前累计待销赃价值，消失 2 天（第 2 天整天消失、第 3 天回）
    private static void TryFence()
    {
        try
        {
            if (!Exists()) return;
            if (Patches.CurrentUITradeMode != 0) { try { Il2Cpp.StoreUIManager.Instance.Notify(LangHelper.T("交易模式下不能销赃", "Can't fence while trading"), "orange"); } catch { } return; }
            int leave = GetStat(K_LEAVE);
            if (leave > 0 && CurrentDay() < leave) { try { Il2Cpp.StoreUIManager.Instance.Notify(LangHelper.T("蛙娘不在店里", "Wage Girl is out"), "orange"); } catch { } return; }
            int amt = GetStat(K_FENCE_AMT);
            if (amt <= 0) { try { Il2Cpp.StoreUIManager.Instance.Notify(LangHelper.T("没有违禁品可销——先拖违禁品给蛙娘吃掉", "No contraband to fence - feed her contraband first"), "orange"); } catch { } return; }
            SetStat(K_FENCE_PENDING, amt);
            SetStat(K_FENCE_AMT, 0);
                        SetSleepDebt(GetSleepDebt() + BuildConfig.WageGirlFenceSleepDebt); // 销赃外出熬夜 -N 睡眠（次日结算；CFG）
            SetStat(K_LEAVE, CurrentDay() + BuildConfig.WageGirlFenceDays);
            SetStat(K_LEAVE_REASON, 1);
            _curState = "away"; _curAnimSprites = _spAway; _stateFrameSec = 0.125f; _leavingTimer = 0.5f; // 播away挥手帧再移除
            ReportLine(LangHelper.T("蛙娘带着 " + amt + " 价值的货出去销赃了（" + BuildConfig.WageGirlFenceDays + " 天后回来）", "Wage Girl took " + amt + " worth of goods to fence (back in " + BuildConfig.WageGirlFenceDays + " days)"));
            try { if (Il2Cpp.CustomUIManager.Instance != null && Il2Cpp.CustomUIManager.Instance.IsOpen("wage_girl_panel")) ShowPanel(); } catch { }
        }
        catch (Exception ex) { Core.LogMsg("[蛙娘] 销赃异常: " + ex.Message); }
    }
private static void FenceReturn()
    {
        try
        {
            int amt = GetStat(K_FENCE_PENDING);
            SetStat(K_FENCE_PENDING, 0);
            if (amt <= 0) { ReportLine(LangHelper.T("蛙娘销赃回来了", "Wage Girl is back from fencing")); return; }
            int aff = GetAffection();
            // 跑腿费：BasePct% 起步，好感满降 10 个百分点 → 最低 FeeMinPct%（CFG：WageGirlFenceFeeBasePct/MinPct）
            float fee = BuildConfig.WageGirlFenceFeeBasePct / 100f - (aff / (10f * BuildConfig.WageGirlAffMax));
            if (fee < BuildConfig.WageGirlFenceFeeMinPct / 100f) fee = BuildConfig.WageGirlFenceFeeMinPct / 100f;
            // 09-23 改：不预算克扣，实际克扣 = amt - spentTotal
            long target = (long)(amt * (1f - fee));
            if (target < 1) target = 1;
            int cat = GetStat(K_FENCE_CAT);
            // cat==0 物资箱：CreateLootCrate 随机箱 + 内部按 ItemPool 填充到目标价值
            if (cat == 0)
            {
                GameItem crate = CreateSupplyCrate(target);
                if (crate != null) { AddToFront(crate); long crateVal = crate.unitValue; int actualKeep = (int)(amt - crateVal); if (actualKeep > 0) SetStat(K_SAVINGS, GetStat(K_SAVINGS) + actualKeep); ReportLine(BuildFenceReport(amt, actualKeep, LangHelper.T("一只物资箱", "a supply crate"))); }
                else ReportLine(BuildFenceReport(amt, 0, LangHelper.T("（没弄到箱子）", "(no crate)")));
                return;
            }
            // cat==5 指挥卡：cmd_keycard + 差额按随机物品补足
            if (cat == 5)
            {
                GameItem kc = null;
                try { kc = DirectoryMaster.Item("cmd_keycard", true); } catch { }
                long kcVal = 0;
                var names5 = new System.Collections.Generic.List<string>();
                if (kc != null) { AddToFront(kc); kcVal = kc.unitValue; names5.Add(LangHelper.T("指挥卡","Keycard")); }
                long remain = target - kcVal;
                int n5 = (int)Math.Max(1, Math.Min(5, remain / 500));
                long per5 = remain / Math.Max(1, n5);
                long spent5 = 0;
                for (int i = 0; i < n5 && spent5 < remain; i++)
                {
                    GameItem it5 = FindItemNearValue(Math.Min(per5, remain - spent5), 0, false);
                    if (it5 == null) break;
                    AddToFront(it5); spent5 += it5.unitValue; names5.Add(ModCannibalism.GetName(it5));
                }
                if (names5.Count > 0) { int actualKeep5 = (int)(amt - kcVal - spent5); if (actualKeep5 > 0) SetStat(K_SAVINGS, GetStat(K_SAVINGS) + actualKeep5); ReportLine(BuildFenceReport(amt, actualKeep5, string.Join("、", names5))); }
                else ReportLine(LangHelper.T("蛙娘销赃回来了", "Wage Girl is back"));
                return;
            }
            // cat==6 医药品：必带免疫宁(large_purple_injector) + 差额补医疗物品
            if (cat == 6)
            {
                GameItem im = CreateGenuineImmunivax();
                long imVal = 0;
                var names6 = new System.Collections.Generic.List<string>();
                if (im != null) { AddToFront(im); imVal = im.unitValue; names6.Add(LangHelper.T("免疫宁","Immunity Shot")); }
                long remain6 = target - imVal;
                int n6 = (int)Math.Max(1, Math.Min(5, remain6 / 500));
                long per6 = remain6 / Math.Max(1, n6);
                long spent6 = 0;
                for (int i = 0; i < n6 && spent6 < remain6; i++)
                {
                    GameItem it6 = FindItemNearValue(Math.Min(per6, remain6 - spent6), 0, false);
                    if (it6 == null) break;
                    AddToFront(it6); spent6 += it6.unitValue; names6.Add(ModCannibalism.GetName(it6));
                }
                if (names6.Count > 0) { int actualKeep6 = (int)(amt - imVal - spent6); if (actualKeep6 > 0) SetStat(K_SAVINGS, GetStat(K_SAVINGS) + actualKeep6); ReportLine(BuildFenceReport(amt, actualKeep6, string.Join("、", names6))); }
                else ReportLine(LangHelper.T("蛙娘销赃回来了", "Wage Girl is back"));
                return;
            }
            // cat==7 模板：按价值拆件带回（复用 FindItemNearValue，模块优先）
            if (cat == 7)
            {
                int n7 = (int)Math.Max(1, Math.Min(5, target / 500));
                long per7 = target / Math.Max(1, n7);
                long spent7 = 0; var names7 = new System.Collections.Generic.List<string>();
                for (int i = 0; i < n7 && spent7 < target; i++)
                {
                    GameItem it7 = FindItemNearValue(Math.Min(per7, target - spent7), 7, false);
                    if (it7 == null) break;
                    AddToFront(it7); spent7 += it7.unitValue; names7.Add(ModCannibalism.GetName(it7));
                }
                if (names7.Count > 0) { int actualKeep7 = (int)(amt - spent7); if (actualKeep7 > 0) SetStat(K_SAVINGS, GetStat(K_SAVINGS) + actualKeep7); ReportLine(BuildFenceReport(amt, actualKeep7, string.Join("、", names7))); }
                else ReportLine(LangHelper.T("蛙娘销赃回来了", "Wage Girl is back"));
                return;
            }
            // 拆件：每 500 价值 1 件（1-5 件）；单件目标 = 总目标/件数
            int n = (int)Math.Max(1, Math.Min(5, target / 500));
            long perTarget = target / n;
            long spent = 0;
            var names = new System.Collections.Generic.List<string>();
            for (int i = 0; i < n && spent < target; i++)
            {
                long itemTarget = Math.Min(perTarget, target - spent);
                GameItem it = FindItemNearValue(itemTarget, cat, false); // 单件 ≤ 单件目标、最接近
                if (it == null) break;
                long v = it.unitValue;
                if (spent + v > target * 1.3) break; // 累计防超
                AddToFront(it);
                spent += v;
                names.Add(ModCannibalism.GetName(it));
            }
            if (names.Count > 0) { int actualKeep = (int)(amt - spent); if (actualKeep > 0) SetStat(K_SAVINGS, GetStat(K_SAVINGS) + actualKeep); ReportLine(BuildFenceReport(amt, actualKeep, string.Join("、", names))); }
            else ReportLine(LangHelper.T("蛙娘销赃回来了（没找到合适的货）", "Wage Girl is back (no good goods found)"));
        }
        catch { }
    }

    // 09-21 新增：销赃夜报统一格式
    private static string BuildFenceReport(int amt, int keep, string items) {
        int savings = GetStat(K_SAVINGS);
        return LangHelper.T(
            "蛙娘销赃归来：收入" + amt + "块，克扣" + keep + "块，小金库" + savings + "块。带了：" + items,
            "Wage Girl fenced: income " + amt + ", kept " + keep + ", savings " + savings + ". Brought: " + items);
    }

    // 09-21 新增：洗白所有违禁品
    private static void TryWashAll() {
        try {
            if (Patches.CurrentUITradeMode != 0) { try { Il2Cpp.StoreUIManager.Instance.Notify(LangHelper.T("交易模式下不能洗白", "Can't launder while trading"), "orange"); } catch { } return; }
            var all = EmporiumEntry.Instance.GetAllItems();
            var washList = new System.Collections.Generic.List<GameItem>();
            foreach (var it in all) {
                if (it == null) continue;
                try { if (Il2Cpp.ContrabandHelper.GetContrabandLevel(it) > 0) washList.Add(it); } catch { }
            }
            if (washList.Count == 0) {
                try { Il2Cpp.StoreUIManager.Instance.Notify(LangHelper.T("店里没有违禁品", "No contraband in store"), "orange"); } catch { }
                return;
            }
            int cost = washList.Count * BuildConfig.WageGirlWashCostPerItem;
            int savings = GetStat(K_SAVINGS);
            if (savings < cost) {
                try { Il2Cpp.StoreUIManager.Instance.Notify(LangHelper.T("小金库余额不足（需要 " + cost + " 块）", "Not enough savings (need " + cost + ")"), "orange"); } catch { }
                return;
            }
            SetStat(K_SAVINGS, savings - cost);
            foreach (var it in washList) {
                try { it.DisableTag("CONTRABAND_ITEM_TAG", true); } catch { }
                try { it.DisableTag("contraband", true); } catch { }
            }
            ReportLine(LangHelper.T("蛙娘洗白了 " + washList.Count + " 件违禁品（-" + cost + " 块小金库）", "Wage Girl laundered " + washList.Count + " contraband (-" + cost + " savings)"));
            try { if (Il2Cpp.CustomUIManager.Instance != null && Il2Cpp.CustomUIManager.Instance.IsOpen("wage_girl_panel")) ShowPanel(); } catch { }
        } catch (Exception ex) { Core.LogMsg("[蛙娘] 洗白异常: " + ex.Message); }
    }

    // 跑路回归：带最低维度对应类别礼物 + 先偷 1 件
    private static void RunawayReturn()
    {
        try
        {
            int sat = GetStat(K_SAT), th = GetStat(K_TH), health = GetStat(K_HEALTH), mood = GetStat(K_MOOD), clean = GetStat(K_CLEAN), sleep = GetStat(K_SLEEP);
            string mode = "food"; int min = sat;
            if (th < min) { min = th; mode = "drink"; }
            if (health < min) { min = health; mode = "medicine"; }
            if (mood < min) { min = mood; mode = "care"; }
            if (clean < min) { min = clean; mode = "care"; }
            if (sleep < min) { min = sleep; mode = "sleep"; }
            GameItem gift = FindCategoryItem(mode);
            if (gift != null)
                {
                    GameItem giftBox = CreateSupplyCrate(gift.unitValue);
                    if (giftBox != null) AddToFront(giftBox); else AddToFront(gift);
                    ReportLine(LangHelper.T("蛙娘回来了，带了份物资箱补偿你", "Wage Girl is back with a supply crate to make up"));
                }
            else ReportLine(LangHelper.T("蛙娘回来了", "Wage Girl is back"));
            // 09-22 砍：跑路回归不偷（只带回来给玩家）
        }
        catch { }
    }

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

    // ===================== 物品信息缓存（销赃精确匹配用；首次销赃回归时构建一次，此后复用） =====================
    private class ItemInfo
    {
        public long Value;
        public bool FoodDrink;
        public bool Daily;
        public bool WeaponTool;
        public bool Module;
    }
    private static System.Collections.Generic.Dictionary<string, ItemInfo> _itemInfoCache;
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
                catch { }
            }
        }
        catch { }
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
        catch { }
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
    private static GameItem CreateSupplyCrate(long targetValue)
    {
        try
        {
            // 09-23 修复「物资箱有 1% 概率没有物资」之一：目标价值为 0 → 主循环一次都不进 → 空箱
            if (targetValue < 1) targetValue = 1;
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
            catch { }
            if (inv == null) return crate; // 取不到内部库存 → 原样返回（箱自带原版内容）
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
            return crate;
        }
        catch { return null; }
    }
}
