using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace JacksonPerks;

// ============================================================
// 负面特性：治安部眼线
// 每15天治安部突击检查：全店（含暗格+海报夹层+货架表面）违禁品全没收（红色负面，Cost -15）
// 复用：AddictOfficerEvent.IsSmugglerBay / GetInnerInventory（已提 internal）、Core.LastNightReportLine、PerkStatePersistence
// ============================================================
internal sealed class DarkGridInspectorPerk : CustomStartingPerk
{
    internal const string PerkId = "治安部眼线";

    internal override string Id => PerkId;
    internal override string DisplayName => LangHelper.T("治安部眼线", "Security Informant");
    internal override string Description => LangHelper.T(
        "治安部的眼睛从未离开过你——你的一举一动都被记录在案。每15天他们上门突击检查，翻出暗格和海报夹层里的违禁品，一律没收，不限数量。满星信誉也拦不住他们。",
        "The Security Department's eyes never leave you - every move you make is on record. Every 15 days they raid your shop, uncovering contraband even in hidden compartments and behind posters - all confiscated, no limit. Full reputation won't stop them.");
    internal override int Cost => -15;   // 返还15点（最大负面，09-16 用户拍板）
    internal override int Type => 1;    // 负面特性显示为红色

    internal override void OnNewGame() { }

    internal static bool IsActive()
    {
        return Core.PerkActive(PerkId);
    }

    // ============ 每日调度 ============
    // 阶段2 改造（2026-09-23）：原先这里是 `internal static new void OnNewDay()`
    // —— static 方法用 new 隐藏了基类的实例虚方法，形成"同名两个 OnNewDay，一死一活"的认知陷阱，
    //    且靠独立挂 OnDayStartPostfix 驱动。现改为标准 override，由 CustomStartingPerks.NotifyDayStart() 统一驱动。
    internal override void OnDayStart()
    {
        try
        {
            if (!IsActive()) return;
            if (PlayerStore.Instance == null) return;
            int day = 1; try { day = StoreStation.GetDayCounter(); } catch { }
            if (day <= 0 || day % BuildConfig.InspectInterval != 0) return;
            if (day == WageSaveStore.GetInt("dark_grid_inspector", "last_trigger_day", -1)) return;
            WageSaveStore.SetInt("dark_grid_inspector", "last_trigger_day", day);
            RunInspection(day);
        }
        catch (Exception ex) { Core.LogMsg("[治安部眼线] 调度失败: " + ex.Message); }
    }

    // ============ 突击检查：全店（海报夹层+暗格内部+货架表面）违禁品全没收，不限件数 ============
    private static void RunInspection(int day)
    {
        var haul = new List<(GameItem item, GameInventory inv, int lvl)>();
        try
        {
            // 1. 海报/隐藏区（EmporiumEntry.hiddenElement）
            GameGridInventory hidden = EmporiumEntry.Instance.hiddenElement;
            if (hidden != null && hidden.childItems != null)
            {
                for (int i = 0; i < hidden.childItems.Count; i++)
                {
                    GameItem c = hidden.childItems[i];
                    if (c == null) continue;
                    try { int lvl = ContrabandHelper.GetContrabandLevel(c); if (lvl > 0) haul.Add((c, hidden, lvl)); } catch { }
                }
            }

            // 2. 容器/带舱机器内部违禁品（09-22 用户拍板"都翻"：暗格 smugger_bay + 蛙哥箱 CUSTOM_STORAGE_TAG +
            //    通用容器 CONTAINER_TAG + 带舱机器（养蛊机/AI生成器）——凡有内部库存一律翻，不限 smugger_bay）
            foreach (GameItem shopItem in EmporiumEntry.Instance.GetAllItems())
            {
                if (shopItem == null) continue;
                GameInventory inner = AddictOfficerEvent.GetInnerInventory(shopItem);
                if (inner == null || inner.childItems == null) continue;
                for (int i = 0; i < inner.childItems.Count; i++)
                {
                    GameItem c = inner.childItems[i];
                    if (c == null) continue;
                    // 去重：GetAllItems 递归返回容器内部物品时，同一件可能被翻两次
                    bool dup = false;
                    for (int d = 0; d < haul.Count; d++) { if (haul[d].item == c) { dup = true; break; } }
                    if (dup) continue;
                    try { int lvl = ContrabandHelper.GetContrabandLevel(c); if (lvl > 0) haul.Add((c, inner, lvl)); } catch { }
                }
            }

            // 3. 货架/柜台/展示柜/后背包表面直接违禁品（主要网格；暗格容器本身跳过——内部已在 2 处理）
            EmporiumEntry em = EmporiumEntry.Instance;
            if (em != null)
            {
                GameInventory[] surfaces = new GameInventory[]
                {
                    em.invElement as GameInventory,
                    em.backInvinvElement as GameInventory,
                    em.backInvinvElementCounter as GameInventory,
                    em.frontInvinvElement as GameInventory,
                    em.showcaseElement as GameInventory
                };
                foreach (GameInventory g in surfaces)
                {
                    if (g == null || g.childItems == null) continue;
                    for (int i = 0; i < g.childItems.Count; i++)
                    {
                        GameItem c = g.childItems[i];
                        if (c == null) continue;
                        if (AddictOfficerEvent.IsSmugglerBay(c)) continue;
                        try { int lvl = ContrabandHelper.GetContrabandLevel(c); if (lvl > 0) haul.Add((c, g, lvl)); } catch { }
                    }
                }
            }
        }
        catch (Exception ex) { Core.LogMsg("[治安部眼线] 扫描失败: " + ex.Message); }

        if (haul.Count == 0)
        {
            try { var _ps = PlayerStore.Instance; if (_ps != null) _ps.AddNightLog(LangHelper.T(
                "治安部突击检查！眼线把暗格和海报夹层翻了个底朝天，这次没搜到违禁品。",
                "Security raid! Informants tore through hidden compartments and behind posters - nothing found this time."), "#7FC97F"); } catch { } // ① 原生夜报（拆包：打开报告自动显示）
            Core.AddNightReportLine(LangHelper.T(
                "治安部突击检查！眼线把暗格和海报夹层翻了个底朝天，这次没搜到违禁品。",
                "Security raid! Informants tore through hidden compartments and behind posters - nothing found this time."));
            try { StoreUIManager.Instance.Notify(LangHelper.T("治安部突击检查！这次没搜到违禁品", "Security raid! Nothing found this time"), "yellow"); } catch { }
            return;
        }

        // 全没收（不限件数）
        int seized = 0;
        foreach (var s in haul)
        {
            try { s.inv.Expel(s.item); seized++; }
            catch (Exception ex) { Core.LogMsg("[治安部眼线] 没收失败: " + ex.Message); }
        }
        if (seized == 0) return;

        // 没收清单（最多列 5 种 + 其余计数）——写进第二天晨报/夜间报告
        var names = new List<string>();
        foreach (var s in haul)
        {
            string nm = "";
            try { nm = s.item.name ?? s.item.identifier ?? ""; } catch { }
            if (!string.IsNullOrEmpty(nm) && !names.Contains(nm)) names.Add(nm);
            if (names.Count >= 5) break;
        }
        string listStr = names.Count > 0 ? string.Join("、", names) : "";
        if (names.Count >= 5 && haul.Count > 5) listStr += " 等";

        try { var _ps = PlayerStore.Instance; if (_ps != null) _ps.AddNightLog(LangHelper.T(
            "治安部突击检查！眼线翻出了所有隐秘角落，没收了 " + seized + " 件违禁品" + (listStr.Length > 0 ? "：" + listStr : "") + "。",
            "Security raid! Informants confiscated " + seized + " contraband items" + (listStr.Length > 0 ? ": " + listStr : "") + " from hidden compartments."), "#7FC97F"); } catch { } // ① 原生夜报（09-22 统一柔和绿）
        Core.AddNightReportLine(LangHelper.T(
            "治安部突击检查！眼线翻出了所有隐秘角落，没收了 " + seized + " 件违禁品" + (listStr.Length > 0 ? "：" + listStr : "") + "。",
            "Security raid! Informants confiscated " + seized + " contraband items" + (listStr.Length > 0 ? ": " + listStr : "") + " from hidden compartments."));
        try { StoreUIManager.Instance.Notify(LangHelper.T("治安部突击检查！没收 " + seized + " 件违禁品", "Security raid! " + seized + " contraband confiscated"), "red"); } catch { }
    }
}
