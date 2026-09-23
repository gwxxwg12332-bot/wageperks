using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Il2Cpp;
using MelonLoader;
using UnityEngine;
using Il2CppInterop.Runtime;

namespace JacksonPerks;

// ============================================================
// 成瘾警官巡查事件（通用世界事件，总是发生，不依赖任何特性）
// 机制：周期性伪装购买 → 无货记仇 → 次日巡查没收暗格违禁品（全店总共 2 件）
// 复用：Core.Rng / PerkStatePersistence / LangHelper / Core.LogMsg / Core.LastNightReportLine
// ============================================================
internal static class AddictOfficerEvent
{
    private const string NS = "addict_officer";
    private const string KEY_NEXT = "next_trigger_day";   // 下次伪装进店日
    private const string KEY_GRUDGE = "grudge_flag";      // 记仇 flag
    private const string KEY_LAST = "last_trigger_day";   // 上次触发日（防同一天重复）

    // ============ 每日调度（StoreEventManager.OnDayStart Postfix 调用——ModHook 在 Demo 不触发，09-17 已回退） ============
    internal static void OnNewDay()
    {
        try
        {
            if (PlayerStore.Instance == null) return;
            int day = GetDay();

            // 1. 次日巡查：记仇 flag=true → 没收 → 清 flag → 巡查日不再伪装（防连锁）
            if (WageSaveStore.GetBool(NS, KEY_GRUDGE, false))
            {
                WageSaveStore.SetBool(NS, KEY_GRUDGE, false);
                RunInspection(day);
                return;
            }

            // 2. 伪装进店：到触发日；防同一天重复
            // 首次（next<0）自动初始化：第 3~5 天第一次来，之后每 2~4 天一次
            int next = WageSaveStore.GetInt(NS, KEY_NEXT, -1);
            if (next < 0) { next = day + 3 + Core.Rng.Next(3); WageSaveStore.SetInt(NS, KEY_NEXT, next); }
            if (day >= next && day != WageSaveStore.GetInt(NS, KEY_LAST, -1))
            {
                SpawnDisguisedOfficer(day);
                WageSaveStore.SetInt(NS, KEY_NEXT, day + 2 + Core.Rng.Next(3)); // 间隔 2~4 天
            }
        }
        catch (Exception ex) { Core.LogMsg("[AddictOfficer] OnNewDay失败: " + ex.Message); }
    }

    // ============ OnDayStart Postfix（StoreEventManager.OnDayStart——吞噬季同挂点，已验证可靠） ============
    internal static void OnDayStartPostfix()
    {
        try { OnNewDay(); } catch (Exception ex) { Core.LogMsg("[AddictOfficer] OnDayStart失败: " + ex.Message); }
        // 阶段2 注：治安部眼线已改为 override CustomStartingPerk.OnDayStart()，由统一驱动入口驱动，
        // 此处仍不代调——防双重触发。（本类本身 09-21 已封存，Core.cs 里的 OnDayStart 注册为关闭状态）
    }

    // ============ 伪装成瘾警官进店 ============
    private static void SpawnDisguisedOfficer(int day)
    {
        WageSaveStore.SetInt(NS, KEY_LAST, day);
        try
        {
            // 【实测教训】CreateInspectionClient 是检查客户（clientIntent=INSPECTION），
            // 强制 isSecurity=false 仍会触发原生治安检查。改用普通买家 CreateFlexiBuyer
            // （clientIntent=BUY，无检查行为，伪装成瘾者找麻醉品最自然）。
            StoreClient officer = StoreClientList.CreateFlexiBuyer();
            if (officer == null) { Core.LogMsg("[AddictOfficer] 创建伪装客户失败"); return; }

            // 伪装成普通平民：去警徽 + 平民外观（spriteName/clientFaction/isSecurity 解耦）
            try { officer.isSecurity = false; } catch { }
            try { officer.spriteName = SpriteDict.GetRandomLowerLevelSpriteName(); } catch { }

            // 来源标记 + 进店文案
            officer.eventSourceId = "addict_officer";
            SetDialogue(officer, LangHelper.T(
                "一名神色疲惫的男子压低帽檐走入店内，眼神躲闪，似乎在寻找特殊货品。",
                "A weary man pulls his cap low, eyes darting as he scans for something special."));

            GetStoreClientManager()?.AddClient(officer);
        }
        catch (Exception ex) { Core.LogMsg("[AddictOfficer] 伪装进店失败: " + ex.Message); }
    }

    // ============ 客户到达判定分支（Patches.PostfixSpecialNpcStartDialogue 调用） ============
    internal static void OnClientArrived(StoreClient client)
    {
        try
        {
            if (client == null) return;
            string src = "";
            try { src = client.eventSourceId ?? ""; } catch { }
            if (src != "addict_officer") return;

            bool hasNarc = HasNarcoticInShop();

            if (hasNarc)
            {
                // 分支A：有货 → 正常购买，安静离开，不记仇（默认包庇黑市）
                SetDialogue(client, LangHelper.T(
                    "男子快速交易后迅速离开，街道一切如常。",
                    "The man completes his deal quickly and slips away. All is quiet."));
            }
            else
            {
                // 分支B：无货 → 记仇 flag=true
                WageSaveStore.SetBool(NS, KEY_GRUDGE, true);
                SetDialogue(client, LangHelper.T(
                    "对方扫视货架后一无所获，面色阴沉，默不作声离去，你隐隐感到不安。",
                    "He scans the shelves, finds nothing, and leaves in grim silence. Unease settles in."));
            }
        }
        catch (Exception ex) { Core.LogMsg("[AddictOfficer] 判定失败: " + ex.Message); }
    }

    // ============ 次日巡查 + 没收（全店总共 2 件，全局 contrabandLevel 降序） ============
    private static void RunInspection(int day)
    {

        var haul = new List<(GameItem item, GameInventory inv, int lvl)>();
        try
        {
            // 1. 海报/隐藏区（EmporiumEntry.hiddenElement）——原生巡查在 StorePoster 激活时检查这里，
            //    玩家放在海报后边的物品存于此处，必须纳入没收
            GameGridInventory hidden = EmporiumEntry.Instance.hiddenElement;
            if (hidden != null && hidden.childItems != null)
            {
                for (int i = 0; i < hidden.childItems.Count; i++)
                {
                    GameItem c = hidden.childItems[i];
                    if (c == null) continue;
                    try
                    {
                        int lvl = ContrabandHelper.GetContrabandLevel(c);
                        if (lvl > 0) haul.Add((c, hidden, lvl));
                    }
                    catch (System.Exception ex) { Core.LogMsg("[AddictOfficerEvent] 异常: " + ex.Message); }
                }
            }

            // 2. 走私者暗格容器内部（smuggler_bay 前缀 / ITEM_HIDDEN_TAG+CONTAINER_TAG 双标签）
            foreach (GameItem shopItem in EmporiumEntry.Instance.GetAllItems())
            {
                if (shopItem == null || !IsSmugglerBay(shopItem)) continue;
                GameInventory inner = GetInnerInventory(shopItem);
                if (inner == null || inner.childItems == null) continue;
                for (int i = 0; i < inner.childItems.Count; i++)
                {
                    GameItem c = inner.childItems[i];
                    if (c == null) continue;
                    try
                    {
                        int lvl = ContrabandHelper.GetContrabandLevel(c);
                        if (lvl > 0) haul.Add((c, inner, lvl));
                    }
                    catch (System.Exception ex) { Core.LogMsg("[AddictOfficerEvent] 异常: " + ex.Message); }
                }
            }
        }
        catch (Exception ex) { Core.LogMsg("[AddictOfficer] 扫描暗格/海报失败: " + ex.Message); }

        var seized = haul.OrderByDescending(x => x.lvl).Take(2).ToList();
        if (seized.Count == 0)
        {
            return;
        }

        foreach (var s in seized)
        {
            try { s.inv.Expel(s.item); }
            catch (Exception ex) { Core.LogMsg("[AddictOfficer] 没收失败: " + ex.Message); continue; }
        }

        try { var _ps = PlayerStore.Instance; if (_ps != null) _ps.AddNightLog(LangHelper.T(
            "例行公务巡查！执勤警官严格检查了所有隐秘区域，从你的暗格中查获并没收了 " + seized.Count + " 件违禁品。",
            "Routine inspection! Officers searched hidden compartments and confiscated " + seized.Count + " contraband items."), "#7FC97F"); } catch { } // ① 原生夜报（09-22 统一柔和绿）
        Core.AddNightReportLine(LangHelper.T(
            "例行公务巡查！执勤警官严格检查了所有隐秘区域，从你的暗格中查获并没收了 " + seized.Count + " 件违禁品。",
            "Routine inspection! Officers searched hidden compartments and confiscated " + seized.Count + " contraband items."));
    }

    // ============ 工具方法 ============
    private static bool HasNarcoticInShop()
    {
        try
        {
            foreach (GameItem it in EmporiumEntry.Instance.GetAllItems())
            {
                if (it == null) continue;
                try { if (ChemicalFeedbackHelper.IsNarcotic(it)) return true; } catch { }
            }
        }
        catch (System.Exception ex) { Core.LogMsg("[AddictOfficerEvent] 异常: " + ex.Message); }
        return false;
    }

    internal static bool IsSmugglerBay(GameItem item)
    {
        // 判据1：identifier 前缀 smuggler_bay（最可靠）
        try { string id = item.identifier; if (!string.IsNullOrEmpty(id) && id.StartsWith("smuggler_bay")) return true; } catch { }
        // 判据2：隐藏 + 容器 双标签
        try { if (item.IsTag("ITEM_HIDDEN_TAG") && item.IsTag("CONTAINER_TAG")) return true; } catch { }
        return false;
    }

    // 容器内部库存（09-19 拆包实锤 L1）：
    // 原生 PixelWindow 无 inventory 字段（dump.cs L61980/L61984-62053），内部网格经
    // PixelWindow.Attach(GameGridInventory) 挂在 childElement（DirectoryUtils.txt L66-126）；
    // 反射 inventory 对原生暗格恒 null（升级暗格搜不到的根因）。
    // 通道1 inventory 反射：兼容蛙哥箱等自建 contentWindow；通道2 childElement：原生容器正路
    // （复用 ContainerUpgradeV2.GetContainerGrid，与升级系统/养蛊机/虚空珠同一已验证路径）。
    internal static GameInventory GetInnerInventory(GameItem container)
    {
        try
        {
            var w = container.contentWindow;
            if (w == null) return null;
            try
            {
                var prop = w.GetType().GetProperty("inventory", BindingFlags.Public | BindingFlags.Instance);
                var inv = prop?.GetValue(w) as GameInventory;
                if (inv != null) return inv;
            }
            catch (System.Exception ex) { Core.LogMsg("[AddictOfficerEvent] 异常: " + ex.Message); }
            try
            {
                var grid = ContainerUpgradeV2.GetContainerGrid(container);
                if (grid != null) return grid;
            }
            catch (System.Exception ex) { Core.LogMsg("[AddictOfficerEvent] 异常: " + ex.Message); }
            return null;
        }
        catch { return null; }
    }

    private static StoreClientManager GetStoreClientManager()
    {
        try
        {
            PlayerStore store = PlayerStore.Instance;
            if (store == null) return null;
                            return store.storeClientManager;
        }
        catch { return null; }
    }

    private static void SetDialogue(StoreClient client, string text)
    {
        try
        {
            string speaker = (client.displayName ?? "").Trim();
            if (client.mainDialogue != null) client.mainDialogue.SetText(speaker, text);
            else { var d = new Dialogue(); d.SetText(speaker, text); client.mainDialogue = d; }
        }
        catch (System.Exception ex) { Core.LogMsg("[AddictOfficerEvent] 异常: " + ex.Message); }
    }

    private static int GetDay() { try { return StoreStation.GetDayCounter(); } catch { return 1; } }
    private static string GetId(GameItem it) { try { return it.identifier ?? "?"; } catch { return "?"; } }

}
