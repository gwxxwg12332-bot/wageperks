using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime;
using MelonLoader;
using UnityEngine;

namespace JacksonPerks;

// ============================================================
// v1.3.1【补11】蛙哥周期到访：每 N 天来一次，服务卡消负面perk
//   塞队：ScheduleWageBrotherToday() 照博士 QueueFuturClient("wage_brother",1)
//   交互：蛙哥到场柜台生成服务卡 wage_brother_card → 双击弹消perk界面
//   费用：Cost -2/-3→500，-7/-10→2000，-15→5000，-20→8000
// ============================================================
internal static class WageBrother
{
    internal const string CLIENT_ID = "wage_brother";
    internal const string CARD_ID = "wage_brother_card";
    private static int _lastScheduledDay = -1;
    private static bool _cardSpawned = false;

    // 周期塞队（照博士 ScheduleJacksonToday）
    internal static bool ScheduleToday()
    {
        try
        {
            var ps = PlayerStore.Instance;
            if (ps == null) return false;
            int day = StoreStation.GetDayCounter();
            if (HasQueued()) return false;
            int interval = BuildConfig.WageBrotherVisitInterval > 0 ? BuildConfig.WageBrotherVisitInterval : 10;
            if (_lastScheduledDay >= 0 && day - _lastScheduledDay < interval) return false;
            _lastScheduledDay = day;
            ps.QueueFuturClient(CLIENT_ID, 1);
            Core.LogMsg("[蛙哥] 已排队，明天到访");
            return true;
        }
        catch (Exception ex) { Core.LogMsg("[蛙哥] ScheduleToday异常: " + ex.Message); return false; }
    }

    private static bool HasQueued()
    {
        try
        {
            var ps = PlayerStore.Instance; if (ps == null) return false;
            var q = ps.futurStoreClientIdQueue;
            if (q != null) for (int i = 0; i < q.Count; i++) if (q[i] == CLIENT_ID) return true;
            var mgr = ps.storeClientManager;
            if (mgr != null && mgr.clientStack != null) for (int j = 0; j < mgr.clientStack.Count; j++)
            { var c = mgr.clientStack[j]; if (c != null && c.identifier == CLIENT_ID) return true; }
        }
        catch { }
        return false;
    }

    // 蛙哥到场 → 柜台生成服务卡
    internal static void OnClientArrived(StoreClient client)
    {
        try
        {
            if (client == null || client.identifier != CLIENT_ID) return;
            if (_cardSpawned) return;
            var ps = PlayerStore.Instance; if (ps == null) return;
            GameItem card = null;
            try { card = DirectoryMaster.Item(CARD_ID); } catch { }
            if (card == null)
            {
                // 未注册物品 fallback：用 cassette_player 占位
                try { card = DirectoryMaster.Item("cassette_player"); } catch { }
            }
            if (card != null)
            {
                try { card.EnableTag("wage_bro_card", true); } catch { }
                ps.AddDirectSellingItemToTable(card, false, true, false, 100);
                _cardSpawned = true;
                Core.LogMsg("[蛙哥] 到场，服务卡已上柜台");
            }
        }
        catch (Exception ex) { Core.LogMsg("[蛙哥] OnClientArrived异常: " + ex.Message); }
    }

    // 蛙哥走后清服务卡（每日调用）
    internal static void CleanupCardIfGone()
    {
        try
        {
            if (!_cardSpawned) return;
            if (HasQueued()) return; // 蛙哥还在
            _cardSpawned = false;
            // 清掉柜台上的服务卡（wage_bro_card tag）
            var em = EmporiumEntry.Instance; if (em == null) return;
            var all = em.GetAllItems();
            foreach (var it in all)
            {
                if (it == null) continue;
                bool isCard = false; try { isCard = it.IsTag("wage_bro_card"); } catch { }
                if (isCard) { try { it.parentInventory?.Expel(it); } catch { } try { it.Destroy(); } catch { } }
            }
            Core.LogMsg("[蛙哥] 走了，服务卡已清");
        }
        catch { }
    }

    // 定价：Cost → 费用
    internal static int PriceForCost(int cost)
    {
        if (cost <= -20) return 8000;
        if (cost <= -15) return 5000;
        if (cost <= -7) return 2000;
        return 500; // -2/-3
    }

    // 弹消perk界面
    internal static void ShowRemovePerkWindow()
    {
        try
        {
            if (PlayerStore.Instance == null) return;
            var mgr = CustomUIManager.Instance; if (mgr == null) return;
            if (mgr.IsOpen("wage_bro_window")) mgr.CloseWindow("wage_bro_window");
            var w = mgr.CreateWindow("wage_bro_window", LangHelper.T("蛙哥 · 消业障", "Wage Brother · Remove Burden"), "overlay");
            if (w == null) return;
            w.SetSize(360, 480).SetPosition(Vector2.zero);
            w.BeginColumn(4f);
            w.AddLabel(LangHelper.T("蛙哥：花钱消个负面特性。钱货两清。", "Wage Brother: pay to remove a negative perk. No refunds."), "wb_hint");
            var ps = PlayerStore.Instance;
            w.AddLabel(LangHelper.T("当前现金：" + ps.playerCash, "Cash: " + ps.playerCash), "wb_cash");
            // 列已选 Cost<0 负面perk
            int listed = 0;
            foreach (var perk in CustomStartingPerks.All)
            {
                try
                {
                    if (perk.Cost >= 0) continue; // 只负面
                    if (!StartingPerk.IsPerkActive(perk.Id)) continue;
                    int price = PriceForCost(perk.Cost);
                    string nm = perk.DisplayName;
                    int cpy = perk.Cost;
                    int pr = price;
                    bool canAfford = ps.playerCash >= pr;
                    string btnText = LangHelper.T(nm + "（" + cpy + "点，" + pr + "块）", nm + " (" + cpy + "pt, " + pr + "cr)");
                    var act = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((System.Action)(() => { try { DoRemovePerk(perk.Id, pr); } catch (Exception ex) { Core.LogMsg("[蛙哥] 消perk异常: " + ex.Message); } }));
                    w.AddButton(btnText, act, "wb_perk_" + listed);
                    listed++;
                }
                catch { }
            }
            if (listed == 0) w.AddLabel(LangHelper.T("没有可消除的负面特性", "No removable negative perks"), "wb_empty");
        }
        catch (Exception ex) { Core.LogMsg("[蛙哥] ShowWindow异常: " + ex.Message); }
    }

    private static void DoRemovePerk(string perkId, int price)
    {
        try
        {
            var ps = PlayerStore.Instance; if (ps == null) return;
            if (ps.playerCash < price) { StoreUIManager.Instance.Notify(LangHelper.T("钱不够", "Not enough credits")); return; }
            ps.playerCash -= price;
            StartingPerk.RemovePerk(perkId);
            Core.LogMsg("[蛙哥] 已消除 " + perkId + "，扣 " + price);
            StoreUIManager.Instance.Notify(LangHelper.T("蛙哥收了" + price + "块，" + perkId + "消了", "Wage Brother took " + price + ", removed " + perkId));
            // 清服务卡
            _cardSpawned = false;
            CleanupCardIfGone();
            // 关窗口
            try { if (CustomUIManager.Instance != null) CustomUIManager.Instance.CloseWindow("wage_bro_window"); } catch { }
        }
        catch (Exception ex) { Core.LogMsg("[蛙哥] DoRemovePerk异常: " + ex.Message); }
    }

    // 服务卡双击 Prefix（OpenContentAction 识别 wage_bro_card）
    internal static bool PrefixDoubleClickAction(Il2Cpp.GameItem newItem, UnityEngine.Vector2 mousePosition)
    {
        try
        {
            GameItem item = newItem; if (item == null) return true;
            bool isCard = false; try { isCard = item.IsTag("wage_bro_card"); } catch { }
            if (!isCard) return true; // 不是服务卡，放行原生
            if (PlayerStore.Instance == null) return false;
            ShowRemovePerkWindow();
            return false; // 拦截原生打开
        }
        catch { return true; }
    }
}
