using System;
using Il2Cpp;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace WagePerks;

// ============================================================
// B2 蛙哥服务面板（2026-10-13 B2 派活单）
// 入口：蛙哥服务卡双击（WageBrother.PrefixDoubleClickAction 改调本面板）。
// 实锤修正：蛙哥无电话（PatchRegistry.cs:54=进店 NPC，电话系统仅鲁滨逊），
//   「查看服务」入口落服务卡双击（蛙哥唯一玩家交互载体）。
// 面板=自建窗口（CustomUIManager.CreateWindow，照抄消业障窗口先例 WageBrother.cs:601），
//   零原版改动；数据源=CustomStartingPerks.All 21 白名单 + 吞噬季特殊条目。
// 支付=声望+现金补差（C5 蓝本：wildFavor 够→直扣；不够→wildFavor=0+差额 playerCash 补）。
// ============================================================
internal static class B2ServicePanel
{
    private const string WINDOW_ID = "wage_b2_window";

    internal static void ShowPanel()
    {
        try
        {
            if (PlayerStore.Instance == null) return;
            var mgr = CustomUIManager.Instance; if (mgr == null) return;
            if (mgr.IsOpen(WINDOW_ID)) mgr.CloseWindow(WINDOW_ID);
            var w = mgr.CreateWindow(WINDOW_ID, LangHelper.T("蛙哥 · 服务面板", "Wage Brother · Service Panel"), "overlay");
            if (w == null) return;
            w.SetSize(440, 780).SetPosition(Vector2.zero);
            w.BeginColumn(4f);

            var ps = PlayerStore.Instance;
            w.AddLabel(LangHelper.T("现金 " + ps.playerCash + " · 声望 " + ps.wildFavor,
                                     "Cash " + ps.playerCash + " · Favor " + ps.wildFavor), "b2_balance");

            foreach (var perk in CustomStartingPerks.All)
            {
                try
                {
                    bool selected = false;
                    try { selected = StartingPerk.IsPerkActive(perk.Id); } catch { }
                    int lv = B2ServiceStore.GetLevel(perk.Id);
                    int cost = perk.Cost;
                    string nm = perk.DisplayName;

                    if (cost < 0) // 负面：已消除 / 已选可消除 / 未选跳过
                    {
                        if (B2ServiceStore.IsEliminated(perk.Id))
                        {
                            w.AddLabel(LangHelper.T(nm + " · 已消除", nm + " · eliminated"), "b2_elim");
                            continue;
                        }
                        if (!selected) continue; // 无人买负面
                        int price = perk.Id == ThiefMagnetPerk.PerkId ? 5000 : WageBrother.PriceForCost(perk.Cost);
                        if (perk.Id == ThiefMagnetPerk.PerkId && ThiefMagnetSystem.StolenCount() > 0)
                        {
                            var lockedAct = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((System.Action)(() =>
                            {
                                Notify(LangHelper.T("被偷物品还没赎完，招贼体质暂时消不掉——先点「消业障」里的赎回", "Redeem stolen items first."));
                            }));
                            w.AddButton(LangHelper.T(nm + " · 先赎回被偷物品", nm + " · redeem first"), lockedAct, "b2_neg_lock_" + perk.Id);
                            continue;
                        }
                        string btnText = perk.Id == InfamousPerk.PerkId
                            ? LangHelper.T(nm + " · 消除（分势力恢复）", nm + " · remove (per faction)")
                            : LangHelper.T(nm + " · 消除 " + price + " 信用点", nm + " · remove " + price + " cr");
                        var act = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((System.Action)(() => { try { DoEliminate(perk, price); } catch (Exception ex) { Core.LogMsg("[B2] 消除异常: " + ex.Message); } }));
                        w.AddButton(btnText, act, "b2_neg_" + perk.Id);
                        continue;
                    }

                    // 正面 / 零价
                    if (lv >= 3)
                    {
                        w.AddLabel(LangHelper.T(nm + " · 已满级", nm + " · MAX"), "b2_max");
                        continue;
                    }
                    if (lv >= 1)
                    {
                        if (cost == 0) { w.AddLabel(LangHelper.T(nm + " · 已拥有", nm + " · owned"), "b2_owned"); continue; }
                        int price = UpgradePrice(perk, lv + 1);
                        var upAct = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((System.Action)(() => { try { DoUpgrade(perk, lv + 1, price); } catch (Exception ex) { Core.LogMsg("[B2] 升级异常: " + ex.Message); } }));
                        w.AddButton(LangHelper.T(nm + " · 升级Lv" + (lv + 1) + " · " + price + " 信用点", nm + " · Upgrade Lv" + (lv + 1) + " · " + price + " cr"), upAct, "b2_up_" + perk.Id);
                        continue;
                    }
                    if (selected)
                    {
                        if (cost == 0) { w.AddLabel(LangHelper.T(nm + " · 已拥有", nm + " · owned"), "b2_owned"); continue; }
                        int price = UpgradePrice(perk, 2);
                        var upAct = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((System.Action)(() => { try { DoUpgrade(perk, 2, price); } catch (Exception ex) { Core.LogMsg("[B2] 升级异常: " + ex.Message); } }));
                        w.AddButton(LangHelper.T(nm + " · 升级Lv2 · " + price + " 信用点", nm + " · Upgrade Lv2 · " + price + " cr"), upAct, "b2_up_" + perk.Id);
                        continue;
                    }
                    // 未拥有 → 购买
                    string buyText = cost == 0
                        ? LangHelper.T(nm + " · 免费获得", nm + " · get free")
                        : LangHelper.T(nm + " · 购买 " + BuyPrice(perk) + " 信用点", nm + " · buy " + BuyPrice(perk) + " cr");
                    var buyAct = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((System.Action)(() => { try { DoBuy(perk); } catch (Exception ex) { Core.LogMsg("[B2] 购买异常: " + ex.Message); } }));
                    w.AddButton(buyText, buyAct, "b2_buy_" + perk.Id);
                }
                catch (Exception ex) { Core.LogMsg("[B2] 条目异常 " + perk.Id + ": " + ex.Message); }
            }

            // ---- 吞噬季特殊条目 ----
            try { AppendCannibalSection(w); }
            catch (Exception ex) { Core.LogMsg("[B2] 吞噬季区异常: " + ex.Message); }

            // 更多服务入口（消业障：负面消除/赎回/合成；指南）
            var broAct = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((System.Action)(() => { try { WageBrother.ShowRemovePerkWindow(); } catch (Exception ex) { Core.LogMsg("[B2] 消业障异常: " + ex.Message); } }));
            w.AddButton(LangHelper.T("🔧 消业障（负面/赎回/合成）", "🔧 Remove/ Redeem/ Craft"), broAct, "b2_bro");
            var guideAct = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((System.Action)(() => { try { WageBrother.ShowWageBrotherGuide(); } catch (Exception ex) { Core.LogMsg("[B2] 指南异常: " + ex.Message); } }));
            w.AddButton(LangHelper.T("📖 蛙哥指南", "📖 Guide"), guideAct, "b2_guide");
            var closeAct = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((System.Action)(() => { try { CustomUIManager.Instance.CloseWindow(WINDOW_ID); } catch { } }));
            w.AddButton(LangHelper.T("关闭", "Close"), closeAct, "b2_close");
        }
        catch (Exception ex) { Core.LogMsg("[B2] ShowPanel异常: " + ex.Message); }
    }

    // ---- 定价（开发期定，回传建议） ----
    // 购买价 = Cost × 500（零价=0 免费）；升级价：升到2级=买价，升到3级=买价×2。
    internal static int BuyPrice(CustomStartingPerk perk)
    {
        int c = perk.Cost;
        if (c <= 0) return 0;
        return c * 500;
    }

    internal static int UpgradePrice(CustomStartingPerk perk, int toLevel)
    {
        int baseP = BuyPrice(perk);
        if (baseP <= 0) return 0;
        return toLevel == 2 ? baseP : baseP * 2;
    }

    // ---- 支付：声望优先，不够→声望清零+差额现金补（C5 蓝本） ----
    private static bool Pay(int cost)
    {
        try
        {
            var ps = PlayerStore.Instance; if (ps == null) return false;
            if (cost <= 0) return true;
            int favor = ps.wildFavor;
            if (favor >= cost)
            {
                ps.wildFavor = favor - cost;
                Core.LogMsg("[B2] 支付 " + cost + "（声望直扣，剩 " + ps.wildFavor + "）");
                return true;
            }
            int diff = cost - favor;
            ps.wildFavor = 0;
            if (ps.playerCash < diff) return false;
            ps.playerCash -= diff;
            Core.LogMsg("[B2] 支付 " + cost + "（声望 " + favor + " 清零 + 现金补差 " + diff + "）");
            return true;
        }
        catch (Exception ex) { Core.LogMsg("[B2] 支付异常: " + ex.Message); return false; }
    }

    private static void Notify(string msg)
    {
        try { StoreUIManager.Instance.Notify(msg); } catch { }
    }

    // ---- 动作 ----
    private static void DoBuy(CustomStartingPerk perk)
    {
        int price = BuyPrice(perk);
        if (!Pay(price)) { Notify(LangHelper.T("钱不够", "Not enough credits")); return; }
        B2ServiceStore.SetLevel(perk.Id, 1);
        Notify(LangHelper.T("已获得特性：" + perk.DisplayName, "Perk acquired: " + perk.DisplayName));
        Core.LogMsg("[B2] 已购买 " + perk.Id + "（" + price + "）");
        ShowPanel();
    }

    private static void DoUpgrade(CustomStartingPerk perk, int toLevel, int price)
    {
        if (!Pay(price)) { Notify(LangHelper.T("钱不够", "Not enough credits")); return; }
        B2ServiceStore.SetLevel(perk.Id, toLevel);
        Notify(LangHelper.T(perk.DisplayName + " 升级到 Lv" + toLevel, perk.DisplayName + " upgraded to Lv" + toLevel));
        Core.LogMsg("[B2] 已升级 " + perk.Id + " → Lv" + toLevel + "（" + price + "）");
        ShowPanel();
    }

    private static void DoEliminate(CustomStartingPerk perk, int price)
    {
        if (perk.Id == InfamousPerk.PerkId)
        {
            // 声名狼藉特殊：分势力恢复窗口（全正才真正消除，走既有逻辑）
            try { WageBrother.ShowRepFactionWindow(); } catch { }
            return;
        }
        if (!Pay(price)) { Notify(LangHelper.T("钱不够", "Not enough credits")); return; }
        B2ServiceStore.Eliminate(perk.Id);
        Notify(LangHelper.T("已消除负面特性：" + perk.DisplayName, "Negative perk removed: " + perk.DisplayName));
        Core.LogMsg("[B2] 已消除 " + perk.Id + "（" + price + "）");
        ShowPanel();
    }

    // ---- 吞噬季区 ----
    private static void AppendCannibalSection(CustomUIBuilder w)
    {
        if (B2ServiceStore.IsCannibalOwned)
        {
            int mode = B2ServiceStore.CannibalMode;
            string modeText = mode == 0
                ? LangHelper.T("单次 · 第" + B2ServiceStore.CannibalDay + "天", "Once · Day " + B2ServiceStore.CannibalDay)
                : LangHelper.T("周期 · 每" + B2ServiceStore.CannibalInterval + "天", "Every " + B2ServiceStore.CannibalInterval + " days");
            w.AddLabel(LangHelper.T("🦠 吞噬季 · 已开启 · " + modeText, "🦠 Cannibalism ON · " + modeText), "b2_can_state");

            var toggleAct = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((System.Action)(() => { try { ToggleCannibalMode(); } catch (Exception ex) { Core.LogMsg("[B2] 模式切换异常: " + ex.Message); } }));
            w.AddButton(LangHelper.T("切换模式（单次/周期）", "Switch mode (once/periodic)"), toggleAct, "b2_can_mode");

            if (mode == 0)
            {
                var plusAct = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((System.Action)(() => { B2ServiceStore.SetCannibalDay(B2ServiceStore.CannibalDay + 1); ShowPanel(); }));
                w.AddButton(LangHelper.T("吞噬日 +1（当前第" + B2ServiceStore.CannibalDay + "天）", "Trigger day +1 (now Day " + B2ServiceStore.CannibalDay + ")"), plusAct, "b2_can_day_plus");
                var minusAct = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((System.Action)(() => { B2ServiceStore.SetCannibalDay(Math.Max(0, B2ServiceStore.CannibalDay - 1)); ShowPanel(); }));
                w.AddButton(LangHelper.T("吞噬日 -1（当前第" + B2ServiceStore.CannibalDay + "天）", "Trigger day -1 (now Day " + B2ServiceStore.CannibalDay + ")"), minusAct, "b2_can_day_minus");
                var nowAct = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((System.Action)(() => { try { B2ServiceStore.SetCannibalDay(DeterministicSchedule.CurrentDay); ShowPanel(); Notify(LangHelper.T("吞噬季将在今天（第" + DeterministicSchedule.CurrentDay + "天）开始", "Cannibalism starts today (Day " + DeterministicSchedule.CurrentDay + ")")); } catch { } }));
                w.AddButton(LangHelper.T("设为今天开始", "Start today"), nowAct, "b2_can_day_now");
            }
            else
            {
                var plusAct = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((System.Action)(() => { B2ServiceStore.SetCannibalInterval(Math.Min(30, B2ServiceStore.CannibalInterval + 1)); ShowPanel(); }));
                w.AddButton(LangHelper.T("周期 +1 天（当前每" + B2ServiceStore.CannibalInterval + "天）", "Interval +1 day (now every " + B2ServiceStore.CannibalInterval + " days)"), plusAct, "b2_can_int_plus");
                var minusAct = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((System.Action)(() => { B2ServiceStore.SetCannibalInterval(Math.Max(2, B2ServiceStore.CannibalInterval - 1)); ShowPanel(); }));
                w.AddButton(LangHelper.T("周期 -1 天（当前每" + B2ServiceStore.CannibalInterval + "天）", "Interval -1 day (now every " + B2ServiceStore.CannibalInterval + " days)"), minusAct, "b2_can_int_minus");
            }
        }
        else
        {
            w.AddLabel(LangHelper.T("🦠 吞噬季 · 未开启（机器模组/电池互食盛宴）", "🦠 Cannibalism Season OFF"), "b2_can_off");
            var buyAct = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((System.Action)(() => { try { DoBuyCannibal(); } catch (Exception ex) { Core.LogMsg("[B2] 吞噬季购买异常: " + ex.Message); } }));
            w.AddButton(LangHelper.T("购买吞噬季 · 3000 信用点", "Buy Cannibalism Season · 3000 cr"), buyAct, "b2_can_buy");
        }
    }

    private static void DoBuyCannibal()
    {
        if (!Pay(3000)) { Notify(LangHelper.T("钱不够", "Not enough credits")); return; }
        B2ServiceStore.SetCannibalOwned(true);
        B2ServiceStore.SetCannibalMode(1); // 默认周期（对齐原版语义）
        B2ServiceStore.SetCannibalInterval(10);
        B2ServiceStore.SetCannibalDay(0);
        Notify(LangHelper.T("吞噬季已开启！默认每 10 天一次，可调周期或改单次日期", "Cannibalism Season ON! Every 10 days by default."));
        Core.LogMsg("[B2] 已购买吞噬季（3000）");
        ShowPanel();
    }

    private static void ToggleCannibalMode()
    {
        int m = B2ServiceStore.CannibalMode == 0 ? 1 : 0;
        B2ServiceStore.SetCannibalMode(m);
        Core.LogMsg("[B2] 吞噬季模式 → " + (m == 0 ? "单次" : "周期"));
        ShowPanel();
    }
}
