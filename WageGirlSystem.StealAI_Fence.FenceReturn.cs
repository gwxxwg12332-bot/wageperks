using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace WagePerks;

public static partial class WageGirlSystem
{
    // ===== 销赃/回归/清理 =====

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
                        GameGridInventory inv = em.frontInvinvElement;
                        // 10-06 二期收敛：手拼链 → WageItemGrant 通用入口（目标=前台库存 frontInvinvElement；防重叠契约内置：TryAcceptOnce 优先+UncheckedAcceptAll 兜底）
                        // 保留原防虚 JIT 注释语义：原③非虚替代 UncheckedAccept（防虚 JIT 崩溃）——入口内 UncheckedAcceptAll 兜底同源
                        WageAPI.WageItemGrant.GrantToInventory(it, inv);
                        names.Add(ModCannibalism.GetName(it));
                    }
                }
                catch (System.Exception ex) { Core.LogMsg("[WageGirlSystem.StealAI_Fence] 异常: " + ex.Message); }
            }
            if (names.Count > 0) ReportLine(LangHelper.T("蛙娘回来了，带了点东西回来：" + string.Join("、", names), "Wage Girl is back with: " + string.Join(", ", names)));
            else ReportLine(LangHelper.T("蛙娘回来了（没带什么值钱的东西）", "Wage Girl is back (empty-handed)"));
        }
        catch (System.Exception ex) { Core.LogMsg("[WageGirlSystem.StealAI_Fence] 异常: " + ex.Message); }
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
            catch (System.Exception ex) { Core.LogMsg("[WageGirlSystem.StealAI_Fence] 异常: " + ex.Message); }
        }
        catch (System.Exception ex) { Core.LogMsg("[WageGirlSystem.StealAI_Fence] 异常: " + ex.Message); }
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
            catch (System.Exception ex) { Core.LogMsg("[WageGirlSystem.StealAI_Fence] 异常: " + ex.Message); }
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
            if (leave > 0) { try { Il2Cpp.StoreUIManager.Instance.Notify(LangHelper.T("蛙娘不在店里，返程结算后再安排", "Wage Girl is out, wait for return"), "orange"); } catch { } return; }
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
            // 09-26 C口径：margin = 好感加成(0~+20%) + 基准(+10%) + 随机(-10%~+30%) → 期望 ≥ 0 → 期望产出 ≥ 投入（CFG：WageGirlFenceAffBonusPct/MarginBasePct/MarginLow/MarginHigh）
            float margin = (aff / (float)BuildConfig.WageGirlAffMax) * BuildConfig.WageGirlFenceAffBonusPct / 100f
                + BuildConfig.WageGirlFenceMarginBasePct / 100f
                + Core.Rng.Next(BuildConfig.WageGirlFenceMarginLow, BuildConfig.WageGirlFenceMarginHigh + 1) / 100f;
            long target = (long)(amt * (1f + margin));
            if (target < 1) target = 1;
            int cat = GetStat(K_FENCE_CAT);
            // cat==0 物资箱：10-06 D+ 拍板（Memos AArYGY6QQhE8QABQvVtjDF）——锁箱外在价值标价 300/箱（BuildConfig.WageGirlFenceBoxValue 可配=基准）
            //   10-06 新需求：蛙娘根据好感设定箱子价值——100 好感=基准 300/箱，越低越贵：线性 = 基准 × (2 - aff/100)（aff clamp [0,100]）
            //   aff=100→300、aff=50→450、aff=0→600；nBox = round(target/boxValue)；10-06 D1：取消"至少 1 箱"保底（可为 0）
            if (cat == 0)
            {
                int affBox = Math.Max(0, Math.Min(100, aff));
                double boxValue = BuildConfig.WageGirlFenceBoxValue * (2.0 - affBox / 100.0);
                int nBox = (int)Math.Round(target / boxValue);
                long filledTotal = 0; int boxOk = 0;
                for (int b = 0; b < nBox; b++)
                {
                    long filledVal = 0;
                    GameItem crate = CreateSupplyCrate((long)Math.Round(boxValue), out filledVal);
                    if (crate != null)
                    {
                        // 10-07 #2（需求修正）：蛙娘箱=玩家背包+已拥有（非柜台卖——A4 改回；小偷箱才带售价）
                        try { crate.unitValue = (long)Math.Round(boxValue); } catch { }
                        GiveToPlayerBackpack(crate);
                        filledTotal += filledVal; boxOk++;
                        // 10-06 A4（拆包实锤 memos/hQvCsbLJpdTsA6mQtkEHPU）：蛙娘箱=原生自动上锁（CreateLootCrate:1441）——
                        //   没钥匙=死物打不开 → 对应钥匙卡也上桌卖（5 箱↔5 卡）；映射=cheatsheet 19.4（MerchantHelper 同款）
                        //   10-06 D2（525BFCC1 拍板）：低好感不带钥匙卡回来（高好感才带，门槛 BuildConfig.WageGirlFenceCardAffThreshold 可配）
                        //   10-07 A4：钥匙卡改上桌卖（原 GrantToPlayerBackInv=直接送背包→玩家购买）
                        //   10-07 #2（需求修正）：蛙娘箱→背包已拥有 → 钥匙卡也随背包给（玩家自己的箱要能开）
                        try
                        {
                            string boxId = "?"; try { boxId = crate.identifier ?? "?"; } catch { }
                            if (affBox >= BuildConfig.WageGirlFenceCardAffThreshold)
                            {
                                string kcId = KeycardForBox(boxId);
                                GameItem kc = DirectoryMaster.Item(kcId, true);
                                if (kc != null)
                                {
                                    GiveToPlayerBackpack(kc);
                                    Core.LogMsg("[蛙娘] 销赃箱 " + boxId + " 钥匙卡 " + kcId + " 给玩家（背包）");
                                }
                                else Core.LogMsg("[蛙娘] 钥匙卡创建失败 " + kcId + "（箱 " + boxId + " 无钥匙可开）");
                            }
                            else Core.LogMsg("[蛙娘] 好感 " + affBox + " < " + BuildConfig.WageGirlFenceCardAffThreshold + " 不带钥匙卡（D2）");
                        }
                        catch (System.Exception ex) { Core.LogMsg("[蛙娘] 钥匙卡给背包异常: " + ex.Message); }
                    }
                }
                if (boxOk > 0) { int actualKeep = (int)(target - filledTotal); if (actualKeep > 0) SetStat(K_SAVINGS, GetStat(K_SAVINGS) + actualKeep); ReportLine(BuildFenceReport(amt, actualKeep, LangHelper.T(boxOk + "只物资箱", boxOk + " supply crates"))); }
                else { SetStat(K_SAVINGS, GetStat(K_SAVINGS) + amt); ReportLine(BuildFenceReport(amt, amt, LangHelper.T("（没弄到箱子，销赃款已存小金库）", "(no crate, fencing kept in savings)"))); }
                return;
            }
            // cat==5 指挥卡：1~3 张 cmd_keycard（CFG）+ 差额随机件数补足
            if (cat == 5)
            {
                int kcCount = Core.Rng.Next(BuildConfig.WageGirlFenceCardMin, BuildConfig.WageGirlFenceCardMax + 1); // 09-26 惊喜感：多张卡
                long kcVal = 0;
                var names5 = new System.Collections.Generic.List<string>();
                for (int j = 0; j < kcCount; j++)
                {
                    GameItem kc = null;
                    try { kc = DirectoryMaster.Item("cmd_keycard", true); } catch { }
                    if (kc != null) { GiveToPlayerBackpack(kc); kcVal += kc.unitValue; names5.Add(LangHelper.T("指挥卡","Keycard")); }
                }
                long remain = target - kcVal;
                if (remain <= 0) return; // 09-27 C3 拍板：指挥卡已值回 target，不再补件（防负 remain 边界）
                // 09-26 惊喜感：差额件数随机 3~8（CFG），不再 500/件封顶5；小额 target 按 remain/300 收紧防碎片
                int n5 = Math.Max(2, Math.Min(Core.Rng.Next(BuildConfig.WageGirlFenceItemMin, BuildConfig.WageGirlFenceItemMax + 1), (int)(remain / 300)));
                long per5 = remain / Math.Max(1, n5);
                long spent5 = 0;
                for (int i = 0; i < n5 && spent5 < remain; i++)
                {
                    GameItem it5 = FindItemNearValue(Math.Min(per5, remain - spent5), 0, false);
                    if (it5 == null) break;
                    GiveToPlayerBackpack(it5); spent5 += it5.unitValue; names5.Add(ModCannibalism.GetName(it5));
                }
                if (names5.Count > 0) { int actualKeep5 = (int)(target - kcVal - spent5); if (actualKeep5 > 0) SetStat(K_SAVINGS, GetStat(K_SAVINGS) + actualKeep5); ReportLine(BuildFenceReport(amt, actualKeep5, string.Join("、", names5))); }
                else ReportLine(LangHelper.T("蛙娘销赃回来了", "Wage Girl is back"));
                return;
            }
            // cat==6 医药品：1~2 支正品免疫宁（CFG）+ 差额随机件数补足
            if (cat == 6)
            {
                int imCount = Core.Rng.Next(BuildConfig.WageGirlFenceInjectorMin, BuildConfig.WageGirlFenceInjectorMax + 1); // 09-26 惊喜感：多支免疫宁
                long imVal = 0;
                var names6 = new System.Collections.Generic.List<string>();
                for (int j = 0; j < imCount; j++)
                {
                    GameItem im = CreateGenuineImmunivax();
                    if (im != null) { GiveToPlayerBackpack(im); imVal += im.unitValue; names6.Add(LangHelper.T("免疫宁","Immunity Shot")); }
                }
                long remain6 = target - imVal;
                if (remain6 <= 0) return; // 09-27 C3 拍板：免疫宁已值回 target，不再补件（防负 remain 边界）
                // 09-26 惊喜感：差额件数随机 3~8（CFG），不再 500/件封顶5
                int n6 = Math.Max(2, Math.Min(Core.Rng.Next(BuildConfig.WageGirlFenceItemMin, BuildConfig.WageGirlFenceItemMax + 1), (int)(remain6 / 300)));
                long per6 = remain6 / Math.Max(1, n6);
                long spent6 = 0;
                for (int i = 0; i < n6 && spent6 < remain6; i++)
                {
                    GameItem it6 = FindItemNearValue(Math.Min(per6, remain6 - spent6), 0, false);
                    if (it6 == null) break;
                    GiveToPlayerBackpack(it6); spent6 += it6.unitValue; names6.Add(ModCannibalism.GetName(it6));
                }
                if (names6.Count > 0) { int actualKeep6 = (int)(target - imVal - spent6); if (actualKeep6 > 0) SetStat(K_SAVINGS, GetStat(K_SAVINGS) + actualKeep6); ReportLine(BuildFenceReport(amt, actualKeep6, string.Join("、", names6))); }
                else ReportLine(LangHelper.T("蛙娘销赃回来了", "Wage Girl is back"));
                return;
            }
            // cat==7 模板：随机件数拆模块带回（09-26 惊喜感：不锁件数）
            if (cat == 7)
            {
                int n7 = Math.Max(2, Math.Min(Core.Rng.Next(BuildConfig.WageGirlFenceItemMin, BuildConfig.WageGirlFenceItemMax + 1), (int)(target / 300)));
                long per7 = target / Math.Max(1, n7);
                long spent7 = 0; var names7 = new System.Collections.Generic.List<string>();
                for (int i = 0; i < n7 && spent7 < target; i++)
                {
                    GameItem it7 = FindItemNearValue(Math.Min(per7, target - spent7), 7, false);
                    if (it7 == null) break;
                    GiveToPlayerBackpack(it7); spent7 += it7.unitValue; names7.Add(ModCannibalism.GetName(it7));
                }
                if (names7.Count > 0) { int actualKeep7 = (int)(target - spent7); if (actualKeep7 > 0) SetStat(K_SAVINGS, GetStat(K_SAVINGS) + actualKeep7); ReportLine(BuildFenceReport(amt, actualKeep7, string.Join("、", names7))); }
                else ReportLine(LangHelper.T("蛙娘销赃回来了", "Wage Girl is back"));
                return;
            }
            // 拆件：随机件数（不限制数量——用户拍板）；单件目标 = 总目标/件数
            int n = Core.Rng.Next(BuildConfig.WageGirlFenceItemMin, BuildConfig.WageGirlFenceItemMax + 1);
            long perTarget = target / Math.Max(1, n);
            long spent = 0;
            var names = new System.Collections.Generic.List<string>();
            for (int i = 0; i < n && spent < target; i++)
            {
                long itemTarget = Math.Min(perTarget, target - spent);
                // 30% 概率向上取整找货（geq=true），填平"只少不多"缺口
                bool geq = Core.Rng.Next(100) < 30;
                GameItem it = FindItemNearValue(itemTarget, cat, geq); // 单件 ≤ 单件目标、最接近（30% 允许略超）
                if (it == null) break;
                long v = it.unitValue;
                if (spent + v > target * 1.3) break; // 累计防超
                GiveToPlayerBackpack(it);
                spent += v;
                names.Add(ModCannibalism.GetName(it));
            }
            if (names.Count > 0) { int actualKeep = (int)(target - spent); if (actualKeep > 0) SetStat(K_SAVINGS, GetStat(K_SAVINGS) + actualKeep); ReportLine(BuildFenceReport(amt, actualKeep, string.Join("、", names))); }
            else ReportLine(LangHelper.T("蛙娘销赃回来了（没找到合适的货）", "Wage Girl is back (no good goods found)"));
        }
        catch (System.Exception ex) { Core.LogMsg("[WageGirlSystem.StealAI_Fence] 异常: " + ex.Message); }
    }

    // 09-21 新增：销赃夜报统一格式（09-26 C口径：target>amt 时 keep 为负 → 显示"贴补"）
    // 10-06 A4：销赃箱→对应钥匙卡（cheatsheet 19.4 / MerchantHelper 同款映射）；evidence_box 及其余→cmd_keycard
    private static string KeycardForBox(string boxId)
    {
        switch (boxId)
        {
            case "med_box": return "med_keycard";
            case "sec_box": return "sec_keycard";
            case "eng_box": return "eng_keycard";
            case "service_box": return "ser_keycard";
            default: return "cmd_keycard";
        }
    }

    private static string BuildFenceReport(int amt, int keep, string items) {
        int savings = GetStat(K_SAVINGS);
        string keepDesc = keep > 0
            ? LangHelper.T("克扣" + keep + "块", "kept " + keep)
            : LangHelper.T("贴补" + (-keep) + "块", "topped up " + (-keep));
        return LangHelper.T(
            "蛙娘销赃归来：投入" + amt + "块，" + keepDesc + "，小金库" + savings + "块。带了：" + items,
            "Wage Girl fenced: invested " + amt + ", " + keepDesc + ", savings " + savings + ". Brought: " + items);
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
                    GameItem giftBox = CreateSupplyCrate(gift.unitValue, out long unusedFilled2);
                    if (giftBox != null) AddToFront(giftBox); else AddToFront(gift);
                    ReportLine(LangHelper.T("蛙娘回来了，带了份物资箱补偿你", "Wage Girl is back with a supply crate to make up"));
                }
            else ReportLine(LangHelper.T("蛙娘回来了", "Wage Girl is back"));
            // 09-22 砍：跑路回归不偷（只带回来给玩家）
        }
        catch (System.Exception ex) { Core.LogMsg("[WageGirlSystem.StealAI_Fence] 异常: " + ex.Message); }
    }
}
