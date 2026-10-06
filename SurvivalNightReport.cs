using System;
using Il2Cpp;

namespace WageSurvival;

// ===== 10-07 阶段D D-1：账本夜报实装（迁移自 WP RobinCrusoePerk.Survival.Patches.cs PostfixOnNewDay 尾部夜报逻辑）=====
// 背景：WS Core.AddNightReportLine 原为空实现（:79）→ WS 侧夜报线全丢；WP PatchRegistryTable :156（PostfixOnNewDay 账本夜报挂点）
//       阶段D 删除后，账本夜报由本文件承接（先补后删——实施单 9TchyxmjkTja5TZkfPxCtw）。
// 数据源对齐 WP 语义：六维=WS 命名空间读取（GetSatiety/GetThirstPct/...）；节点=SurvivalNodeEval.CurrentNode；
//       口粮=DecayFoods 计数（SurvivalState.cs 改造返回元组）。
internal static partial class SurvivalFood
{
    // 账本夜报：日结算时调用（PostfixOnDayStart 尾部）——原版原生夜报通道（AddNightLog 柔和绿 #7FC97F，对齐 WP :202-204）
    internal static void AppendLedgerReport()
    {
        try
        {
            var ps = PlayerStore.Instance;
            if (ps == null) return;

            int day = StoreStation.GetDayCounter();
            int sat = GetSatiety();
            int th = GetThirstPct();
            int h = GetHealth();
            int mood = GetMood();
            int sellB = GetSellBonusPct();
            int budB = GetBudgetBonusPct();
            NodeDef dn = CurrentNode();

            string nodeTxt;
            if (dn == null)
            {
                nodeTxt = LangHelper.T("状态平稳（饱食" + sat + " 口渴" + th + " 健康" + h + "）", "Stable (" + sat + " satiety / " + th + " thirst / " + h + " health)");
            }
            else
            {
                string fxDesc = "";
                foreach (string f in dn.Lock) { string lb = FxLabel(f); if (lb.Length > 0) fxDesc += lb + " "; }
                string cur = GetNodeFx();
                if (!string.IsNullOrEmpty(cur) && cur != "flavor") { string lb = FxLabel(cur); if (lb.Length > 0) fxDesc += lb + " "; }
                nodeTxt = dn.DisplayName + LangHelper.T("：", ": ") + dn.DisplayTag +
                    (fxDesc.Length > 0 ? "｜" + fxDesc.Trim() : "") +
                    LangHelper.T("（饱食" + sat + " 口渴" + th + " 健康" + h + "）", " (" + sat + " satiety / " + th + " thirst / " + h + " health)");
            }
            string moodTxt = LangHelper.T("心情 ", "Mood ") + mood +
                (sellB > 0 || budB > 0 ? "｜" + LangHelper.T("售价+" + sellB + "% 预算+" + budB + "%", "Sell +" + sellB + "% Budget +" + budB + "%") : "");
            string granaryTxt = "";
            int gd = SaveStore.GetInt("granary", 0);
            if (gd >= 7) granaryTxt = LangHelper.T("｜★粮仓充盈 售价+5%", "| Granary full, Sell +5%");
            else if (gd > 0) granaryTxt = LangHelper.T("｜粮仓 " + gd + "/7 天", "| Granary " + gd + "/7 days");

            // 口粮统计（10-07 阶段D：DecayFoods 改返回计数——承接 WP 账本口粮行；edible=新鲜+变质）
            var (fresh, stale, rotten) = CountFoods();

            ps.AddNightLog(LangHelper.T("—— 鲁滨逊的账本 · 第 " + day + " 天 ——", "-- Robinson's Ledger · Day " + day + " --"), "#7FC97F");
            ps.AddNightLog(nodeTxt + "｜" + moodTxt + granaryTxt, "#7FC97F");
            ps.AddNightLog(LangHelper.T("口粮：新鲜 " + fresh + "｜变质 " + stale + "｜腐烂 " + rotten + "（共" + (fresh + stale) + "份可吃）",
                "Rations: fresh " + fresh + " | stale " + stale + " | rotten " + rotten + " (" + (fresh + stale) + " edible)"), "#7FC97F");

            // 打烊 Notify（对齐 WP :208——玩家当天即时可见结算摘要）
            try
            {
                var ui = StoreUIManager.Instance;
                if (ui != null) ui.Notify(LangHelper.T("第" + day + "天：", "Day " + day + ": ") + nodeTxt + "｜" + moodTxt, "white");
            }
            catch { }
        }
        catch (Exception ex)
        {
            Core.LogMsg("[WageSurvival] AppendLedgerReport 异常: " + ex.Message);
        }
    }

    // 口粮计数（10-07 阶段D：原 DecayFoods 无计数——账本口粮行数据源；q0=新鲜/q1=变质/q2=腐烂）
    private static (int fresh, int stale, int rotten) CountFoods()
    {
        int fresh = 0, stale = 0, rotten = 0;
        try
        {
            var em = EmporiumEntry.Instance;
            if (em == null) return (0, 0, 0);
            foreach (GameItem item in em.GetAllItems())
            {
                if (item == null || !IsFood(item)) continue;
                int q = GetFoodQuality(item);
                if (q == 0) fresh++;
                else if (q == 1) stale++;
                else if (q == 2) rotten++;
            }
        }
        catch { }
        return (fresh, stale, rotten);
    }
}
