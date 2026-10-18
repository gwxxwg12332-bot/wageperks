using System;
using HarmonyLib;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Il2Cpp;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace WageSurvival;

internal static partial class SurvivalFood
{

    // 心情提升（上限 100）
    internal static void BoostMood(int amount, string reason)
    {
        try
        {
            int m = Math.Min(100, GetMood() + amount);
            SetMood(m);
            try { StoreUIManager.Instance.Notify(LangHelper.T("心情 +" + amount + "（" + reason + "）", "Mood +" + amount + " (" + reason + ")"), "yellow"); } catch { }
            RefreshStatusPanel();
        }
        catch (Exception ex) { Core.LogMsg("[WageSurvival] BoostMood 异常: " + ex.Message); }
    }

    // ===== 生存状态面板（Z键打开）=====
    // 10-06 鲁滨逊档（双装）：Z 键面板=WP 常驻面板（WS 让路）——WS 吃喝后的刷新转发 WP，避免"吃一口面板不动"
    // 10-18 审阅 §3.4 定案（拆包侧对照放行）：反射转发删除——改用 CustomUIManager.GetElement + SetText/SetProgress
    //   直写（跨 mod 契约：rc_status 的 elementId，见对照_拆包侧_审阅夜报滚动与WS刷新_20261018.md §十）。
    //   收益：不依赖 WP internal 类型/方法名（改名不静默失效）、绕过 _autoPopup 门控、只改文本/填充值（不重建面板）。
    private static void RefreshWpPanelInPlace()
    {
        try
        {
            var mgr = Il2Cpp.CustomUIManager.Instance;
            if (mgr == null) return;
            if (!mgr.IsOpen("rc_status")) { LogOnce("notopen", "rc_status 未打开，跳过直写"); return; } // 10-18 审阅 §3.4：成功/未开各打一次，区分三种情况
            int sat = GetSatiety(), th = GetThirstPct(), h = GetHealth(), bl = GetBlood();
            LogOnce("ok", "直写 OK: blood=" + bl + " sat=" + sat + " th=" + th);
            SetEl(mgr, "sat_l", LangHelper.T("饱食 ", "Satiety ") + (int)(sat * 22f) + "/2200 kcal");
            SetBar(mgr, "sat", sat / 100f);
            SetEl(mgr, "th_l", LangHelper.T("口渴 ", "Thirst ") + (int)(th * 20f) + "/2000 ml");
            SetBar(mgr, "th", th / 100f);
            SetEl(mgr, "h_l", LangHelper.T("健康 ", "Health ") + h + "/100");
            SetBar(mgr, "h", h / 100f);
            SetEl(mgr, "blood_l", LangHelper.T("血量 ", "Blood ") + bl + "/6000");
            SetBar(mgr, "blood", bl / (float)BLOOD_MAX);
            SetEl(mgr, "clean_l", LangHelper.T("清洁 ", "Cleanliness ") + GetClean() + "/100");
            SetBar(mgr, "clean", GetClean() / 100f);
            SetEl(mgr, "sleep_l", LangHelper.T("睡眠 ", "Sleep ") + GetSleep() + "/100");
            SetBar(mgr, "sleep", GetSleep() / 100f);
            SetEl(mgr, "social_l", LangHelper.T("社交 ", "Social ") + GetSocial() + "/100");
            SetBar(mgr, "social", GetSocial() / 100f);
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival] 面板直写异常: " + ex.Message); }
    }

    private static void SetEl(Il2Cpp.CustomUIManager m, string id, string text)
    {
        try { var e = m.GetElement("rc_status", id); if (e != null) e.SetText(text); }
        catch (System.Exception ex) { LogOnce("SetEl:" + id, ex.Message); }
    }
    private static void SetBar(Il2Cpp.CustomUIManager m, string id, float v)
    {
        try { var e = m.GetElement("rc_status", id); if (e != null) e.SetProgress(UnityEngine.Mathf.Clamp01(v)); }
        catch (System.Exception ex) { LogOnce("SetBar:" + id, ex.Message); }
    }
    private static readonly System.Collections.Generic.HashSet<string> _logged = new();
    private static void LogOnce(string k, string msg)
    {
        // 审阅 07:29 §5：前缀去掉"失败"——成功日志曾显示"面板直写失败 ok: 直写 OK:..."（文案错位）
        if (_logged.Add(k)) Core.LogMsg("[WageSurvival] 面板直写: " + k + " | " + msg);
    }

    internal static void RefreshStatusPanel(bool force = false)
    {
        try
        {
            if (Core._yieldMode && SurvivalTrade.IsRobinsonRun())
            {
                RefreshWpPanelInPlace();
                return; // 鲁滨逊档 WS 面板不创建（让路）
            }
        }
        catch { /* 直写失败静默——不影响吃喝主链 */ }
        try
        {
            var mgr = Il2Cpp.CustomUIManager.Instance;
            if (mgr == null) return;
            bool isOpen = mgr.IsOpen("ws_status");
            if (!isOpen && !force && !_autoPopup) return;
            if (mgr.IsOpen("ws_status")) mgr.CloseWindow("ws_status");
            var b = mgr.CreateWindow("ws_status", LangHelper.T("生存状态", "Survival"), "overlay");
            if (b == null) return;
            int sat = GetSatiety(), th = GetThirstPct(), h = GetHealth();
            int satCal = (int)(sat * 22f);
            int thMl = (int)(th * 20f);
            b.SetSize(300, 500).SetPosition(Vector2.zero);
            try { b.SetDraggable(true); } catch { } // 10-07 位置记忆：允许拖拽，位置由 PanelZoomHelper 持久化
            // 固定右上角
            try
            {
                var w = mgr.GetWindow("ws_status");
                if (w != null)
                {
                    var rt = w.Rect;
                    rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1, 1);
                    rt.anchoredPosition = new Vector2(-16, -16);
                }
            }
            catch (System.Exception ex) { Core.LogMsg("[WageSurvival] 面板定位异常: " + ex.Message); }
            // 10-07 面板缩放：重建后重放上次缩放值（localScale 不随窗口重建保留）
            try { PanelZoomHelper.ApplySavedScale("ws_status"); } catch { }
            b.BeginColumn(4f);
            b.AddLabel(LangHelper.T("饱食 ", "Satiety ") + satCal + "/2200 kcal", "sat_l");
            b.AddProgressBar(sat / 100f, "sat");
            b.AddLabel(LangHelper.T("口渴 ", "Thirst ") + thMl + "/2000 ml", "th_l");
            b.AddProgressBar(th / 100f, "th");
            b.AddLabel(LangHelper.T("健康 ", "Health ") + h + "/100", "h_l");
            b.AddProgressBar(h / 100f, "h");
            b.AddLabel(LangHelper.T("血量 ", "Blood ") + GetBlood() + "/6000", "blood_l");
            b.AddProgressBar(GetBlood() / (float)BLOOD_MAX, "blood");
            var sellBtnOnClick = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((System.Action)(() => { try { TrySellBlood(); } catch (Exception ex) { Core.LogMsg("[WageSurvival] 面板卖血异常: " + ex.Message); } }));
            b.AddButton(LangHelper.T("卖血 -500ml", "Sell Blood -500ml"), sellBtnOnClick, "sell_blood_btn");
            var autoPopupBtn = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((System.Action)(() => { _autoPopup = !_autoPopup; try { Il2Cpp.StoreUIManager.Instance.Notify(LangHelper.T(_autoPopup ? "生存面板：自动弹开启" : "生存面板：自动弹关闭", "Survival panel: auto-popup " + (_autoPopup ? "ON" : "OFF"))); } catch { } RefreshStatusPanel(); }));
            b.AddButton(LangHelper.T("自动弹出：" + (_autoPopup ? "开" : "关"), "Auto-popup: " + (_autoPopup ? "ON" : "OFF")), autoPopupBtn, "auto_popup_btn");
            // 10-05 引导 MVP：生存指南按钮（复制蛙娘照顾指南模式）
            var guideBtn = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((System.Action)(() => { try { ShowSurvivalGuide(); } catch (Exception ex) { LogMsg("[WageSurvival] 指南按钮异常: " + ex.Message); } }));
            b.AddButton(LangHelper.T("📖 生存指南", "📖 Survival Guide"), guideBtn, "ws_guide_btn");
            if (IsForcedRest()) b.AddLabel("昏迷中 · 剩余 " + SaveStore.GetInt("blood_rest", 0) + " 天", "blood_rest_l");
            else if (IsBloodWeak()) b.AddLabel(LangHelper.T("虚弱（血量过低）", "Too weak (low blood)"), "blood_weak_l");
            // 新三状态：清洁/睡眠/社交
            int clean = GetClean(), sleep = GetSleep(), social = GetSocial();
            b.AddLabel(LangHelper.T("清洁 ", "Cleanliness ") + clean + "/100", "clean_l");
            b.AddProgressBar(clean / 100f, "clean");
            b.AddLabel(LangHelper.T("睡眠 ", "Sleep ") + sleep + "/100", "sleep_l");
            b.AddProgressBar(sleep / 100f, "sleep");
            b.AddLabel(LangHelper.T("社交 ", "Social ") + social + "/100", "social_l");
            b.AddProgressBar(social / 100f, "social");
            b.AddLabel(LangHelper.T("── 特性成长 ──", "-- Perk Growth --"), "growth_h");
            b.AddLabel(LangHelper.T("每累计50天 +1点 +1槽（全局继承）", "Every 50 global days +1 pt +1 slot"), "growth_l1");
            b.AddLabel(LangHelper.T("困难模式 +1槽 | 10点负面特性 +1槽", "Hard mode +1 slot | 10+ neg perks +1 slot"), "growth_l2");
            // 节点状态
            b.AddLabel(LangHelper.T("── 节点状态 ──", "── Node Status ──"), "node");
            int[] allNodes = { SatietyNode(), ThirstNode(), HealthNode(), CleanNode(), SleepNode(), SocialNode(), MoodNode() };
            string domKey = GetStoredNodeKey();
            foreach (int n in allNodes)
            {
                if (n < 0 || n >= NODES.Length) continue;
                NodeDef d = NODES[n];
                string fxDesc = "";
                foreach (string f in d.Lock) { string lb = FxLabel(f); if (lb.Length > 0) fxDesc += lb + " "; }
                if (d.Key == domKey)
                {
                    string cur = GetNodeFx();
                    if (!string.IsNullOrEmpty(cur) && cur != "flavor") { string lb = FxLabel(cur); if (lb.Length > 0) fxDesc += lb + " "; }
                }
                string line = d.DisplayName;
                if (fxDesc.Length > 0) line += "｜" + fxDesc.Trim();
                b.AddLabel(line, "node");
            }
            int sellB = GetSellBonusPct(), budB = GetBudgetBonusPct(), moodNow = GetMood();
            int moodSell = moodNow >= 60 ? 10 : (moodNow < 40 ? -10 : 0);
            string moodLine = LangHelper.T("心情 ", "Mood ") + moodNow;
            if (moodSell != 0) moodLine += "｜" + LangHelper.T("售价", "Sell") + (moodSell > 0 ? "+" : "") + moodSell + "%";
            b.AddLabel(moodLine, "mood");
            // 售价加成明细
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
            // 哨兵监控
            try
            {
                if (Il2Cpp.HealthData.IsSentinel())
                {
                    var ps = PlayerStore.Instance;
                    if (ps != null && ps.healthData != null)
                    {
                        b.AddLabel(LangHelper.T("── 哨兵 ──", "-- Sentinel --"), "sentinel_h");
                        int days = ps.healthData.daysSinceSentinelInjection;
                        int pool = ps.healthData.sentinelPool;
                        string icon = days < 7 ? "✅" : (days < 10 ? "💡" : (days < 14 ? "⚠️" : "🚨"));
                        string suggest = days < 7 ? "" : (days < 10 ? "准备打针" : (days < 14 ? "尽快打针" : "立即打针"));
                        string line = icon + LangHelper.T("距打针 ", "Days: ") + days + LangHelper.T("天 | 池 ", " | Pool: ") + pool;
                        if (!string.IsNullOrEmpty(suggest)) line += " | " + suggest;
                        b.AddLabel(line, "sentinel_status");
                        if (days >= 14) b.AddLabel("🚨 " + LangHelper.T("立即打真品免疫宁！", "Inject genuine Immunivax NOW!"), "sentinel_critical");
                    }
                }
            }
            catch (Exception ex) { Core.LogMsg("[WageSurvival] 哨兵面板异常: " + ex.Message); }
            // 10-07 数值安全线分色（≥2/3 绿 / ≥1/3 黄 / <1/3 红）
            try
            {
                PanelZoomHelper.ApplyStatusColors("ws_status",
                    ("sat", sat / 100f), ("th", th / 100f), ("h", h / 100f), ("blood", GetBlood() / (float)BLOOD_MAX),
                    ("clean", clean / 100f), ("sleep", sleep / 100f), ("social", social / 100f));
            }
            catch { }
            b.End();
            b.Show();
        }
        catch (Exception ex) { Core.LogMsg("[WageSurvival] 状态面板异常: " + ex.Message); }
    }

    // ============================================================
    // 10-05 引导 MVP：生存指南（复制蛙娘照顾指南 wg_guide 模式）
    // 入口：ws_status 面板「📖 生存指南」按钮
    // ============================================================
    internal static void ShowSurvivalGuide()
    {
        try
        {
            var mgr = Il2Cpp.CustomUIManager.Instance;
            if (mgr == null) return;
            if (mgr.IsOpen("ws_guide")) mgr.CloseWindow("ws_guide");
            var b = mgr.CreateWindow("ws_guide", LangHelper.T("生存 · 指南", "Survival · Guide"), "overlay");
            if (b == null) return;
            b.SetSize(420, 560).SetPosition(Vector2.zero);
            try
            {
                var w = mgr.GetWindow("ws_guide");
                if (w != null)
                {
                    var rt = w.Rect;
                    rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1, 1);
                    rt.anchoredPosition = new Vector2(-16, -200);
                }
            }
            catch (System.Exception ex) { Core.LogMsg("[WageSurvival] 指南定位异常: " + ex.Message); }
            b.BeginColumn(4f);
            b.AddLabel(LangHelper.T("【1】生存系统是什么？", "[1] What is Survival?"), "ws_g1");
            b.AddLabel(LangHelper.T("双击食物/水吃喝，每天打烊结算饱食/口渴/健康/清洁/睡眠/社交六维。状态太差会生病、被迫休息甚至昏迷。按 Z 键随时打开本面板。", "Double-click food/water to eat & drink. Six stats settle at closing: Satiety/Thirst/Health/Clean/Sleep/Social. Too low = illness, forced rest, coma. Press Z to open this panel anytime."), "ws_g1d");
            b.AddLabel(LangHelper.T("【2】如何恢复六维？", "[2] How to Restore the Six Stats"), "ws_g2");
            b.AddLabel(LangHelper.T("饱食=双击食物吃一口；口渴=双击水（优质>普通>脏水，脏水掉健康）；健康=双击医疗用品按价值恢复30~100；心情=喝酒/抽烟/零食/成交一单；清洁=日用品（牙膏/厕纸等）；睡眠=麻醉品或睡觉；社交=多交易。未购买的商品不能使用。", "Satiety=double-click food (one bite); Thirst=double-click water (Pure>Plain>Dirty, dirty hurts health); Health=medical items restore 30-100 by value; Mood=alcohol/smoking/snacks/deals; Clean=daily goods (toothpaste/toilet paper); Sleep=narcotics or sleeping; Social=more trades. Unpurchased items cannot be used."), "ws_g2d");
            b.AddLabel(LangHelper.T("【3】六维是什么？", "[3] The Six Stats"), "ws_g3");
            b.AddLabel(LangHelper.T("饱食 100%=2200 卡，口渴 100%=2000ml，健康、清洁、睡眠、社交。每天衰减，低状态出问题：清洁低易生病、睡眠低心情差、社交低掉心情。", "Satiety 100%=2200kcal, Thirst 100%=2000ml, plus Health/Clean/Sleep/Social. Daily decay; low stats cause trouble: low clean = illness, low sleep = bad mood, low social = mood loss."), "ws_g3d");
            b.AddLabel(LangHelper.T("【4】卖血", "[4] Selling Blood"), "ws_g4");
            b.AddLabel(LangHelper.T("面板点「卖血 -500ml」按钮，血袋价值 200 信用点。血量上限 6000，过低会虚弱、被迫休息甚至昏迷三天。进食/喝水回少量血。", "Press 'Sell Blood -500ml' on the panel. Blood bag worth 200 cr. Max 6000; too low = weak, forced rest, 3-day coma. Eating/drinking regains a little blood."), "ws_g4d");
            b.AddLabel(LangHelper.T("【5】节点系统", "[5] Node System"), "ws_g5");
            b.AddLabel(LangHelper.T("每天打烊结算抽取节点：锁定或抽取效果影响售价、外出等。主导节点追加本次抽取效果。面板逐节点显示当前状态。", "Nodes settle at closing: locked/drawn effects affect sell price, outings, etc. The dominant node adds its draw effect. The panel lists each node's current state."), "ws_g5d");
            b.AddLabel(LangHelper.T("【6】特性成长", "[6] Perk Growth"), "ws_g6");
            b.AddLabel(LangHelper.T("每累计 50 天 +1 特性点 +1 槽（全局继承）；困难模式 +1 槽；10 点以上负面特性 +1 槽。", "Every 50 global days: +1 point +1 slot (global inheritance); Hard mode +1 slot; 10+ negative perks +1 slot."), "ws_g6d");
            b.AddButton(LangHelper.T("关闭", "Close"), DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((System.Action)(() => { try { CustomUIManager.Instance.CloseWindow("ws_guide"); } catch { } })), "ws_guide_close");
        }
        catch (Exception ex) { Core.LogMsg("[WageSurvival] 指南异常: " + ex.Message); }
    }

}
