using System;
using HarmonyLib;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Il2Cpp;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace JacksonPerks;
internal static partial class RobinCrusoePerk
{

    // 心情提升（上限 100）
    internal static void BoostMood(int amount, string reason)
    {
        try
        {
            int m = Math.Min(100, GetMood() + amount);
            SetMood(m);
            try { StoreUIManager.Instance.Notify(LangHelper.T("心情 +" + amount + "（" + reason + "）", "Mood +" + amount + " (" + reason + ")"), "yellow"); } catch { }
            RefreshStatusPanel(); // 心情实时刷新常驻面板
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] BoostMood 异常: " + ex.Message); }
    }

    // ===== v5.7 玩家常驻状态面板（拆包回填4：CustomUIManager overlay + 进度条，重建法最稳）=====
    // 饱食/口渴/健康进度条 + 具体数值（用户要求直观数值），心情与加成 label
    internal static void RefreshStatusPanel(bool force = false)
    {
        try
        {
            if (!_autoPopup && !force) return;  // 09-22 用户拍板：自动弹关闭时不打开（Z键强制打开除外）
            var mgr = Il2Cpp.CustomUIManager.Instance;
            if (mgr == null) return;
            if (mgr.IsOpen("rc_status")) mgr.CloseWindow("rc_status");
            var b = mgr.CreateWindow("rc_status", LangHelper.T("鲁滨逊 · 生存状态", "Robinson · Survival"), "overlay");
            if (b == null) return;
            int sat = GetSatiety(), th = GetThirstPct(), h = GetHealth();
            // 显示具体单位：饱食 100%=2200 kcal（v5.7 锁定）、口渴 100%=2000 ml（与喝水 200ml=10% 自洽）
            int satCal = (int)(sat * 22f);
            int thMl = (int)(th * 20f);
            b.SetSize(300, 500).SetPosition(Vector2.zero);
            // 固定右上角（09-10 用户拍板：锚点(1,1) pivot(1,1) 右上角内侧 16px，不随分辨率变化）
            try
            {
                var w = mgr.GetWindow("rc_status");
                if (w != null)
                {
                    var rt = w.Rect;
                    rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1, 1);
                    rt.anchoredPosition = new Vector2(-16, -16);
                }
            }
            catch (System.Exception ex) { Core.LogMsg("[RobinCrusoePerk.Survival] 异常: " + ex.Message); }
            b.BeginColumn(4f);
            b.AddLabel(LangHelper.T("饱食 ", "Satiety ") + satCal + "/2200 kcal", "sat_l");
            b.AddProgressBar(sat / 100f, "sat");
            b.AddLabel(LangHelper.T("口渴 ", "Thirst ") + thMl + "/2000 ml", "th_l");
            b.AddProgressBar(th / 100f, "th");
            b.AddLabel(LangHelper.T("健康 ", "Health ") + h + "/100", "h_l");
            b.AddProgressBar(h / 100f, "h");
            b.AddLabel(LangHelper.T("血量 ", "Blood ") + GetBlood() + "/6000", "blood_l");
            b.AddProgressBar(GetBlood() / (float)BLOOD_MAX, "blood");
            var sellBtnOnClick = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((System.Action)(() => { try { TrySellBlood(); } catch (Exception ex) { Core.LogMsg("[鲁滨逊] 面板卖血异常: " + ex.Message); } }));
            b.AddButton(LangHelper.T("卖血 -500ml", "Sell Blood -500ml"), sellBtnOnClick, "sell_blood_btn");
            // 09-22 自动弹出开关
            var autoPopupBtn = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((System.Action)(() => { _autoPopup = !_autoPopup; try { Il2Cpp.StoreUIManager.Instance.Notify(LangHelper.T(_autoPopup ? "鲁滨逊面板：自动弹开启" : "鲁滨逊面板：自动弹关闭", "Crusoe panel: auto-popup " + (_autoPopup ? "ON" : "OFF"))); } catch { } RefreshStatusPanel(); }));
            b.AddButton(LangHelper.T("自动弹出：" + (_autoPopup ? "开" : "关"), "Auto-popup: " + (_autoPopup ? "ON" : "OFF")), autoPopupBtn, "auto_popup_btn"); // 09-20 用户拍板：面板按钮为唯一采血入口（替代采血包）
            if (IsForcedRest()) b.AddLabel(LangHelper.T("昏迷中 · 剩余 " + WageSaveStore.GetInt(PERK_ID, "blood_rest", 0) + " 天", "Coma - " + WageSaveStore.GetInt(PERK_ID, "blood_rest", 0) + "d left"), "blood_rest_l");
            else if (IsBloodWeak()) b.AddLabel(LangHelper.T("虚弱（血量过低）", "Too weak (low blood)"), "blood_weak_l");
            // 新三状态（v5.7+ 用户拍板）：清洁/睡眠/社交 进度条+数值
            int clean = GetClean(), sleep = GetSleep(), social = GetSocial();
            b.AddLabel(LangHelper.T("清洁 ", "Cleanliness ") + clean + "/100", "clean_l");
            b.AddProgressBar(clean / 100f, "clean");
            b.AddLabel(LangHelper.T("睡眠 ", "Sleep ") + sleep + "/100", "sleep_l");
            b.AddProgressBar(sleep / 100f, "sleep");
            b.AddLabel(LangHelper.T("社交 ", "Social ") + social + "/100", "social_l");
            b.AddProgressBar(social / 100f, "social");
            // v5.8-8：逐节点状态显示（六状态 + 心情，每个当前节点一行：名称 + 锁定/抽取效果）
            b.AddLabel(LangHelper.T("── 节点状态 ──", "── Node Status ──"), "node");
            int[] allNodes = { SatietyNode(), ThirstNode(), HealthNode(), CleanNode(), SleepNode(), SocialNode(), MoodNode() };
            string domKey = GetStoredNodeKey();
            foreach (int n in allNodes)
            {
                if (n < 0 || n >= NODES.Length) continue;
                NodeDef d = NODES[n];
                string fxDesc = "";
                foreach (string f in d.Lock) { string lb = FxLabel(f); if (lb.Length > 0) fxDesc += lb + " "; }
                if (d.Key == domKey) // 主导节点：追加本次抽取效果
                {
                    string cur = GetNodeFx();
                    if (!string.IsNullOrEmpty(cur) && cur != "flavor") { string lb = FxLabel(cur); if (lb.Length > 0) fxDesc += lb + " "; }
                }
                string line = d.DisplayName;
                if (fxDesc.Length > 0) line += "｜" + fxDesc.Trim();
                b.AddLabel(line, "node");
            }
            int sellB = GetSellBonusPct(), budB = GetBudgetBonusPct(), moodNow = GetMood();
            // 09-21 改：心情加成单独标识，总售价单独一行
            int moodSell = moodNow >= 60 ? 10 : (moodNow < 40 ? -10 : 0);
            string moodLine = LangHelper.T("心情 ", "Mood ") + moodNow;
            if (moodSell != 0) moodLine += "｜" + LangHelper.T("售价", "Sell") + (moodSell > 0 ? "+" : "") + moodSell + "%";
            b.AddLabel(moodLine, "mood");
            // 总加成单独一行（列出所有分项）
            int granaryB = (GetGranaryDays() >= GRANARY_DAYS) ? 5 : 0;
            int elevB = Math.Min(ELEV_MAX, GetElevCount());
            int fxSellB = FxNum("sell");
            int compB = (int)GetCompBuffSellBonus();
            int moodSellB = moodNow >= 60 ? 10 : (moodNow < 40 ? -10 : 0);
            b.AddLabel(LangHelper.T("── 售价加成明细 ──", "── Sell Bonus Breakdown ──"), "sell_detail_hdr");
            if (granaryB != 0) b.AddLabel(LangHelper.T("粮仓 ", "Granary ") + (granaryB > 0 ? "+" : "") + granaryB + "%", "sell_granary");
            if (elevB != 0) b.AddLabel(LangHelper.T("昂扬 ", "Elevate ") + (elevB > 0 ? "+" : "") + elevB + "%", "sell_elev");
            if (fxSellB != 0) b.AddLabel(LangHelper.T("节点 ", "Nodes ") + (fxSellB > 0 ? "+" : "") + fxSellB + "%", "sell_nodes");
            if (compB != 0) b.AddLabel(LangHelper.T("精打细算 ", "Penny Pincher ") + (compB > 0 ? "+" : "") + compB + "%", "sell_comp");
            if (moodSellB != 0) b.AddLabel(LangHelper.T("心情 ", "Mood ") + (moodSellB > 0 ? "+" : "") + moodSellB + "%", "sell_mood");
            b.AddLabel(LangHelper.T("总售价 ", "Total Sell ") + (sellB > 0 ? "+" : "") + sellB + "%", "sell_total");
            if (budB != 0) b.AddLabel(LangHelper.T("总预算 ", "Total Budget ") + (budB > 0 ? "+" : "") + budB + "%", "budget_total");
            b.End();
            b.Show();
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] 状态面板异常: " + ex.Message); }
    }

}
