using System;
using Il2Cpp;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace WagePerks;

internal static partial class RobinCrusoePerk
{
    // ============================================================
    // 鲁滨逊生存指南（2026-10-05 引导 MVP：复制蛙娘照顾指南 wg_guide 模式）
    // 入口：rc_status 面板「📖 生存指南」按钮（RobinCrusoePerk.Survival.Panel.cs）
    // 解决玩家反馈"蛙哥mod没说的鲁滨逊Z键开面板"——功能存在但无人引导
    // ============================================================
    internal static void ShowCrusoeGuide()
    {
        try
        {
            var mgr = Il2Cpp.CustomUIManager.Instance;
            if (mgr == null) return;
            if (mgr.IsOpen("rc_guide")) mgr.CloseWindow("rc_guide");
            var b = mgr.CreateWindow("rc_guide", LangHelper.T("鲁滨逊 · 生存指南", "Robinson · Survival Guide"), "overlay");
            if (b == null) return;
            b.SetSize(420, 560).SetPosition(Vector2.zero);
            try
            {
                var w = mgr.GetWindow("rc_guide");
                if (w != null)
                {
                    var rt = w.Rect;
                    rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1, 1);
                    rt.anchoredPosition = new Vector2(-16, -200);
                }
            }
            catch (System.Exception ex) { Core.LogMsg("[鲁滨逊] 指南定位异常: " + ex.Message); }
            b.BeginColumn(4f);
            // 第1章
            b.AddLabel(LangHelper.T("【1】生存系统是什么？", "[1] What is Survival?"), "rc_g1");
            b.AddLabel(LangHelper.T("双击食物/水吃喝，每天打烊结算饱食/口渴/健康/清洁/睡眠/社交六维。状态太差会生病、被迫休息甚至昏迷。按 Z 键随时打开本面板。", "Double-click food/water to eat & drink. Six stats settle at closing: Satiety/Thirst/Health/Clean/Sleep/Social. Too low = illness, forced rest, coma. Press Z to open this panel anytime."), "rc_g1d");
            // 第2章
            b.AddLabel(LangHelper.T("【2】如何恢复六维？", "[2] How to Restore the Six Stats"), "rc_g2");
            b.AddLabel(LangHelper.T("饱食=双击食物吃一口；口渴=双击水（优质>普通>脏水，脏水掉健康）；健康=双击医疗用品按价值恢复30~100；心情=喝酒/抽烟/零食/成交一单；清洁=日用品（牙膏/厕纸等）；睡眠=麻醉品或睡觉；社交=多交易。未购买的商品不能使用。", "Satiety=double-click food (one bite); Thirst=double-click water (Pure>Plain>Dirty, dirty hurts health); Health=medical items restore 30-100 by value; Mood=alcohol/smoking/snacks/deals; Clean=daily goods (toothpaste/toilet paper); Sleep=narcotics or sleeping; Social=more trades. Unpurchased items cannot be used."), "rc_g2d");
            // 第3章
            b.AddLabel(LangHelper.T("【3】六维是什么？", "[3] The Six Stats"), "rc_g3");
            b.AddLabel(LangHelper.T("饱食 100%=2200 卡，口渴 100%=2000ml，健康、清洁、睡眠、社交。每天衰减，低状态出问题：清洁低易生病、睡眠低心情差、社交低掉心情。", "Satiety 100%=2200kcal, Thirst 100%=2000ml, plus Health/Clean/Sleep/Social. Daily decay; low stats cause trouble: low clean = illness, low sleep = bad mood, low social = mood loss."), "rc_g3d");
            // 第4章
            b.AddLabel(LangHelper.T("【4】卖血", "[4] Selling Blood"), "rc_g4");
            b.AddLabel(LangHelper.T("面板点「卖血 -500ml」按钮，血袋价值 200 信用点。血量上限 6000，过低会虚弱、被迫休息甚至昏迷三天。进食/喝水回少量血。", "Press 'Sell Blood -500ml' on the panel. Blood bag worth 200 cr. Max 6000; too low = weak, forced rest, 3-day coma. Eating/drinking regains a little blood."), "rc_g4d");
            // 第5章
            b.AddLabel(LangHelper.T("【5】节点系统", "[5] Node System"), "rc_g5");
            b.AddLabel(LangHelper.T("每天打烊结算抽取节点：锁定或抽取效果影响售价、外出等。主导节点追加本次抽取效果。面板逐节点显示当前状态。", "Nodes settle at closing: locked/drawn effects affect sell price, outings, etc. The dominant node adds its draw effect. The panel lists each node's current state."), "rc_g5d");
            // 第6章
            b.AddLabel(LangHelper.T("【6】特性成长", "[6] Perk Growth"), "rc_g6");
            b.AddLabel(LangHelper.T("每累计 50 天 +1 特性点 +1 槽（全局继承）；困难模式 +1 槽；10 点以上负面特性 +1 槽。", "Every 50 global days: +1 point +1 slot (global inheritance); Hard mode +1 slot; 10+ negative perks +1 slot."), "rc_g6d");
        }
        catch (Exception ex) { Core.LogMsg("[鲁滨逊] 指南异常: " + ex.Message); }
    }
}
