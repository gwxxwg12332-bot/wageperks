using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace JacksonPerks;

public static partial class WageGirlSystem
{
    internal static void ShowPanel()
    {
        try
        {
            var mgr = Il2Cpp.CustomUIManager.Instance;
            if (mgr == null) return;
            if (mgr.IsOpen("wage_girl_panel")) mgr.CloseWindow("wage_girl_panel");
            var b = mgr.CreateWindow("wage_girl_panel", LangHelper.T("蛙娘 · 状态", "Wage Girl · Status"), "overlay");
            if (b == null) return;
            b.SetSize(300, 560).SetPosition(Vector2.zero);
            try
            {
                var w = mgr.GetWindow("wage_girl_panel");
                if (w != null)
                {
                    var rt = w.Rect;
                    rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1, 1);
                    rt.anchoredPosition = new Vector2(-16, -16);
                }
            }
            catch { }
            b.BeginColumn(4f);
            // 09-20 优化：顶部好感度显示
            b.AddLabel(LangHelper.T("好感度：" + GetAffection() + "/100（" + GetAffLevelText() + "）", "Affection: " + GetAffection() + "/100 (" + GetAffLevelText() + ")"), "wg_aff");
            b.AddLabel(LangHelper.T("💰 小金库：" + GetStat(K_SAVINGS) + " 块", "💰 Savings: " + GetStat(K_SAVINGS) + " credits"), "wg_savings");
            b.AddLabel(LangHelper.T("饱食 ", "Satiety ") + GetStat(K_SAT) + "/100  " + GetStatText(K_SAT), "wg_sat_l");
            b.AddProgressBar(GetStat(K_SAT) / 100f, "wg_sat");
            b.AddLabel(LangHelper.T("口渴 ", "Thirst ") + GetStat(K_TH) + "/100  " + GetStatText(K_TH), "wg_th_l");
            b.AddProgressBar(GetStat(K_TH) / 100f, "wg_th");
            b.AddLabel(LangHelper.T("健康 ", "Health ") + GetStat(K_HEALTH) + "/100  " + GetStatText(K_HEALTH), "wg_h_l");
            b.AddProgressBar(GetStat(K_HEALTH) / 100f, "wg_h");
            b.AddLabel(LangHelper.T("心情 ", "Mood ") + GetStat(K_MOOD) + "/100  " + GetStatText(K_MOOD), "wg_m_l");
            b.AddProgressBar(GetStat(K_MOOD) / 100f, "wg_m");
            b.AddLabel(LangHelper.T("清洁 ", "Cleanliness ") + GetStat(K_CLEAN) + "/100  " + GetStatText(K_CLEAN), "wg_c_l");
            b.AddProgressBar(GetStat(K_CLEAN) / 100f, "wg_c");
            b.AddLabel(LangHelper.T("睡眠 ", "Sleep ") + GetStat(K_SLEEP) + "/100  " + GetStatText(K_SLEEP), "wg_s_l");
            b.AddProgressBar(GetStat(K_SLEEP) / 100f, "wg_s");
            b.AddLabel(LangHelper.T("💬 " + GetDailyLine(), "💬 " + GetDailyLine()), "wg_daily_line"); // 09-20 优化：日常随机台词
            // 外出/离家中：不显示销赃按钮（人不在店里——09-22 用户拍板）
            int leaveChk = GetStat(K_LEAVE);
            bool isOutChk = leaveChk > 0 && CurrentDay() < leaveChk;
            if (!isOutChk)
            {
                // 销赃类别按钮（09-22 用户拍板：可选项，点击循环切换：随机/食物饮品/日用品/武器工具）
                try
                {
                    string[] cats = { LangHelper.T("物资箱", "Supply Crate"), LangHelper.T("食物饮品", "Food/Drink"), LangHelper.T("日用品", "Daily"), LangHelper.T("武器工具", "Weapon/Tool"), LangHelper.T("随机", "Random"), LangHelper.T("指挥卡", "Keycard"), LangHelper.T("医药品", "Medicine"), LangHelper.T("模板", "Module") };
                    int curCat = GetStat(K_FENCE_CAT);
                    var catBtnOnClick = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((System.Action)(() => { try { int c = GetStat(K_FENCE_CAT) + 1; if (c > 7) c = 0; SetStat(K_FENCE_CAT, c); ShowPanel(); } catch (Exception ex) { Core.LogMsg("[蛙娘] 类别切换异常: " + ex.Message); } }));
                    b.AddButton(LangHelper.T("销赃类别（可选择）：" + cats[curCat], "Fence type (selectable): " + cats[curCat]), catBtnOnClick, "wg_fence_cat_btn");
                }
                catch { }
                // 销赃按钮（09-22 用户拍板：喂入违禁品累计，点按钮才出发；按钮文本带待销价值）
                try
                {
                    int famt = GetStat(K_FENCE_AMT);
                    var fenceBtnOnClick = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((System.Action)(() => { try { TryFence(); } catch (Exception ex) { Core.LogMsg("[蛙娘] 销赃异常: " + ex.Message); } }));
                    b.AddButton(LangHelper.T("销赃（待销 " + famt + "）", "Fence (" + famt + ")"), fenceBtnOnClick, "wg_fence_btn");
                    // 09-23 改：违禁品模式切换按钮（洗白 ↔ 销赃）
                    int wmode = GetStat(K_WASH_MODE);
                    var modeBtnOnClick = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((System.Action)(() => { try { int m = GetStat(K_WASH_MODE) == 0 ? 1 : 0; SetStat(K_WASH_MODE, m); ShowPanel(); } catch (Exception ex) { Core.LogMsg("[蛙娘] 模式切换异常: " + ex.Message); } }));
                    b.AddButton(LangHelper.T("违禁品模式（当前：" + (wmode == 0 ? "洗白" : "销赃") + "）", "Contraband mode (current: " + (wmode == 0 ? "Launder" : "Fence") + ")"), modeBtnOnClick, "wg_mode_btn");
                    // 模式说明
                    b.AddLabel(LangHelper.T(wmode == 0 ? "拖违禁品给蛙娘 → 洗白（消除标签，按等级扣费）" : "拖违禁品给蛙娘 → 累计销赃（点「销赃」按钮出发）", wmode == 0 ? "Feed contraband → launder (remove tag, cost by level)" : "Feed contraband → accumulate fence (press Fence to go)"), "wg_mode_hint");
                }
                catch { }
            }
            // 喂钱按钮
            try {
                int sel = _allowanceSel;
                var allowanceBtnOnClick = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((System.Action)(() => { try { GiveAllowance(sel); } catch (Exception ex) { Core.LogMsg("[蛙娘] 喂钱异常: " + ex.Message); } }));
                b.AddButton(LangHelper.T("给零花钱：" + sel, "Allowance: " + sel), allowanceBtnOnClick, "wg_allowance_btn");
                var cycleBtnOnClick = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((System.Action)(() => { try { CycleAllowanceSel(); } catch { } }));
                b.AddButton(LangHelper.T("换档位（下档）", "Switch tier"), cycleBtnOnClick, "wg_allowance_cycle");
                // 09-23 照顾指南按钮
                var guideBtnOnClick = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((System.Action)(() => { try { ShowGuide(); } catch (Exception ex) { Core.LogMsg("[蛙娘] 指南异常: " + ex.Message); } }));
                b.AddButton(LangHelper.T("📖 照顾指南", "📖 Care Guide"), guideBtnOnClick, "wg_guide_btn");
                // 09-23 新增：妙妙箱升级说明

            } catch { }
            b.AddLabel(LangHelper.T("💡 好好照顾她，她会越来越信任你", "💡 Take good care of her, and she will trust you more"), "wg_note");
            // 外出/离家出走状态（阶段 5+6：偷钱/销赃 1 天外出，跑路 14 天）
            try
            {
                int leave = GetStat(K_LEAVE);
                int today = CurrentDay();
                if (leave > 0 && today < leave)
                {
                    int reason = GetStat(K_LEAVE_REASON);
                    int daysLeft = leave - today;
                    string st;
                    if (reason == 2)
                        st = daysLeft <= 1
                            ? LangHelper.T("（离家出走中——明天归来）", "(Ran away - back tomorrow)")
                            : LangHelper.T("（离家出走了——" + daysLeft + " 天后归来）", "(Ran away - back in " + daysLeft + " days)");
                    else if (reason == 1)
                        st = daysLeft <= 1
                            ? LangHelper.T("（外出销赃——明天归来）", "(Out fencing - back tomorrow)")
                            : LangHelper.T("（外出销赃——" + daysLeft + " 天后归来）", "(Out fencing - back in " + daysLeft + " days)");
                    else
                        st = LangHelper.T("（外出中——明天归来）", "(Out - back tomorrow)");
                    b.AddLabel(st, "wg_leave");
                }
            }
            catch { }
        }
        catch (Exception ex) { Core.LogMsg("[蛙娘] 面板异常: " + ex.Message); }
    }
    internal static void ShowGuide()
    {
        try
        {
            var mgr = Il2Cpp.CustomUIManager.Instance;
            if (mgr == null) return;
            if (mgr.IsOpen("wg_guide")) mgr.CloseWindow("wg_guide");
            var b = mgr.CreateWindow("wg_guide", LangHelper.T("蛙娘照顾指南", "Wage Girl Care Guide"), "overlay");
            if (b == null) return;
            b.SetSize(420, 560).SetPosition(Vector2.zero);
            try { var w = mgr.GetWindow("wg_guide"); if (w != null) { var rt = w.Rect; rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1, 1); rt.anchoredPosition = new Vector2(-16, -200); } } catch { }
            b.BeginColumn(4f);
            // 第1章
            b.AddLabel(LangHelper.T("【1】蛙娘是谁？", "[1] Who is Wage Girl?"), "wg_g1");
            b.AddLabel(LangHelper.T("她是你的仿生女仆伙伴。你喂她、照顾她，她帮你干活；你不管她，她就偷钱、离家出走。核心循环：喂好吃的→六维涨→好感涨→她更卖力帮你→生意更好。", "She is your android maid. Feed and care for her, she works for you; neglect her and she steals and leaves. Loop: feed well -> stats rise -> affection rises -> she works harder -> better business."), "wg_g1d");
            // 第2章
            b.AddLabel(LangHelper.T("【2】六维是什么？", "[2] The 6 Stats"), "wg_g2");
            b.AddLabel(LangHelper.T("饱食/口渴/健康/心情/清洁/睡眠。满了不闹，低于30出问题（饿了偷吃东西、渴了偷喝饮料、心情差偷东西）。每日衰减：前5项各-2，睡眠自然恢复。", "Satiety/Thirst/Health/Mood/Clean/Sleep. Full=happy, below 30=causes trouble (steals food/drinks/stuff). Daily decay: first 5 -2 each, sleep recovers naturally."), "wg_g2d");
            // 第3章
            b.AddLabel(LangHelper.T("【3】怎么喂她？", "[3] How to Feed"), "wg_g3");
            b.AddLabel(LangHelper.T("直接拖东西给她。食物→饱食+健康，吃一口剩一半；饮料→口渴（看水质），瓶子留着；日用品→清洁+心情；违禁品→洗白或销赃。脏水掉健康，换花样喂防腻。", "Drag items to her. Food -> satiety+health, eats one bite leaves half; Drink -> thirst (by water quality), bottle kept; Daily goods -> clean+mood; Contraband -> launder or fence. Dirty water hurts health, vary diet to avoid boredom."), "wg_g3d");
            // 第4章
            b.AddLabel(LangHelper.T("【4】怎么涨好感？", "[4] Raising Affection"), "wg_g4");
            b.AddLabel(LangHelper.T("喂好吃的+1~3，照顾清洁+2~5，给零花钱前3次+1~3。好感<30偷≤50，30-70偷≤100，>70偷≤200（很少偷），>90几乎不偷。", "Good food +1~3, clean her +2~5, allowance first 3 times +1~3. Affection <30 steals <=50, 30-70 <=100, >70 <=200 (rarely), >90 almost never steals."), "wg_g4d");
            // 第5章
            b.AddLabel(LangHelper.T("【5】她会做什么？", "[5] What She Does"), "wg_g5");
            b.AddLabel(LangHelper.T("销赃：拖违禁品给她，2天回来带干净货。偷东西：心情差/好感低会偷。偷钱：每5天一次。跑路：连续5天六维低，离家14天。", "Fence: feed contraband, she returns in 2 days with clean goods. Steals: bad mood/low affection. Steals money: every 5 days. Leaves: 5 days low stats, gone 14 days."), "wg_g5d");
            // 第6章
            b.AddLabel(LangHelper.T("【6】小金库", "[6] Savings"), "wg_g6");
            b.AddLabel(LangHelper.T("销赃克扣的跑腿费存小金库。给零花钱100/300/500三档，前3次加好感。跑腿费好感越高越低（15%→5%）。", "Fencing commission goes to savings. Allowance 100/300/500, first 3 times gain affection. Commission drops from 15% to 5% as affection rises."), "wg_g6d");
        }
        catch (Exception ex) { Core.LogMsg("[蛙娘] 指南异常: " + ex.Message); }
    }
}
