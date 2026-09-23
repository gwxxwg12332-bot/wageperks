using System;
using HarmonyLib;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Il2Cpp;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace JacksonPerks;

// ============================================================
// 【空间站鲁滨逊】职业生存系统（startType=14）
// v4.2（2026-09-09，v4.2 终稿重构）：
//   - 饱食节点制：calorieBalance（卡，1单位=2200卡）替代 hunger 层数制，与原生 hunger(0-1000) 完全解耦
//   - 精神 5 档（昂扬/常态/低迷/低落/崩溃）：昂扬累计制（每2天+1%售价/+5%预算，封顶+5%/+25%，断档归零）
//   - 双击食物=摄入 cal（变质50%/腐烂20%）+ 已食用档 + 患病判定（变质10%/腐烂40%）
//   - 双击水=清零 thirstLevel（渴系统独立保留）
//   - 节点：濒饿(≤0且≥3天)/饥饿(≤0)/常态(1-5单位)/饱腹(>5单位)
//   - 粮仓充盈：余额≥7单位(15400卡) → 全店售价+5%
//   - 救场：连续≤0达5天 → 好心客户送食1-2份，不删档，归零
//   - 客流削减：低迷-1/低落-2/崩溃-4；禁外出：低落/崩溃
//   - 状态客户联动（Patches/Core 侧）：加价/概率权重/预算/出价
//
// 拆包锚点全部 [L1]（cheatsheet 2.3.9 / 2.3.10 / 2.3.12 / 2.5.16 / 4.6.8 / 4.6.9 / 设计AI v4.2）
// ============================================================
internal static partial class RobinCrusoePerk
{
    // ===== BUG-001 09-11：交易价格计算链缓存 =====
    // 卡顿根因（拆包实锤）：hover/批量转移每件物品算价 → GetCurrentValue Postfix → TryApplyTradeMarkup → GetTradeBuffDisplay/GetSellBonusPct 每次重建节点文本
    // 节点状态只在 Set*/打烊结算/抽取时变 → Set* 里失效缓存，价格计算链直接读缓存
    private static string _tradeBuffCache = null;   // 报价面板文本（null = 需重建）
    private static int _sellBonusCache = -999;      // 售价加成（-999 = 失效）
    private static int _budgetBonusCache = -999;    // 预算加成
    private static int _bargainBonusCache = -999;   // 议价加成
    internal static void InvalidateTradeCaches()
    {
        _tradeBuffCache = null;
        _sellBonusCache = -999;
        _budgetBonusCache = -999;
        _bargainBonusCache = -999;
        Patches.ClearNodeBuffItems(); // 面板 feature 防重集合一并清：新状态周期内所有物品重新刷新显示文本
    }

    // 效果数值查询（v5.8-8 修正：Lock 效果按当前节点实时聚合，不依赖打烊锁定——面板显示与实际生效永远一致）
    private static int FxVal(string fx, string prefix)
    {
        if (string.IsNullOrEmpty(fx) || !fx.StartsWith(prefix)) return 0;
        int n; return int.TryParse(fx.Substring(prefix.Length), out n) ? n : 0;
    }
    internal static int FxNum(string prefix)
    {
        int v = 0;
        var activeNodes = AllActiveNodes();
        
        
        
        foreach (int n in activeNodes) // Lock 基础效果：实时
        {
            if (n < 0 || n >= NODES.Length) continue;
            
            foreach (string f in NODES[n].Lock) v += FxVal(f, prefix);
        }
        NodeDef d = CurrentNode(); // 抽取项：仅当锁定节点仍为主导节点时生效（打烊抽、离开重抽）
        if (d != null)
        {
            int dom = GetDominantNode();
            if (dom >= 0 && d.Key == NODES[dom].Key)
            {
                string cur = GetNodeFx();
                if (!string.IsNullOrEmpty(cur)) v += FxVal(cur, prefix);
            }
        }
        return v;
    }
    internal static bool FxBool(string fx)
    {
        foreach (int n in AllActiveNodes()) // Lock 基础效果：实时
        {
            if (n < 0 || n >= NODES.Length) continue;
            if (Array.IndexOf(NODES[n].Lock, fx) >= 0) return true;
        }
        NodeDef d = CurrentNode(); // 抽取项
        if (d != null)
        {
            int dom = GetDominantNode();
            if (dom >= 0 && d.Key == NODES[dom].Key && GetNodeFx() == fx) return true;
        }
        return false;
    }
    // 当前所有激活节点（六状态 + 心情；中间档无节点）
    private static int[] AllActiveNodes()
    {
        return new int[] { SatietyNode(), ThirstNode(), HealthNode(), CleanNode(), SleepNode(), SocialNode(), MoodNode() };
    }
    // 报价面板节点 buff 文本（用户拍板 v2：只显示影响交易的效果 sell/bargain/budget + 末尾总百分比）
    internal static string GetTradeBuffDisplay()
    {
        try
        {
            if (_tradeBuffCache != null) return _tradeBuffCache; // BUG-001：缓存命中直接返回
            // 09-21 Bug3：删掉前面的遍历循环，只保留总加成（tooltip 太长）
            var sb = new System.Text.StringBuilder();
            int sellB = GetSellBonusPct(), bargainB = GetBargainBonusPct(), budgetB = GetBudgetBonusPct();
            string total = LangHelper.T("总：", "Total: ");
            if (sellB != 0) total += LangHelper.T("售价", "Sell ") + (sellB > 0 ? "+" : "") + sellB + "% ";
            if (bargainB != 0) total += LangHelper.T("议价", "Bargaining ") + (bargainB > 0 ? "+" : "") + bargainB + "% ";
            if (budgetB != 0) total += LangHelper.T("预算", "Budget ") + (budgetB > 0 ? "+" : "") + budgetB + "% ";
            if (total.Length > 2) sb.Append(total.Trim());
            _tradeBuffCache = sb.ToString().Trim(); // BUG-001：写缓存
            return _tradeBuffCache;
        }
        catch { return ""; }
    }
    // 交易相关效果才显示在报价面板（售价/议价/预算；客流/外出/拾荒/健康/心情等不显示）
    private static bool IsTradeFx(string fx)
    {
        return fx.StartsWith("sell") || fx.StartsWith("bargain") || fx.StartsWith("budget");
    }
    // 效果中文标签（面板/播报）
    private static string FxLabel(string fx)
    {
        if (string.IsNullOrEmpty(fx)) return "";
        switch (fx)
        {
            case "client-2": return LangHelper.T("客流-2", "Customers -2");
            case "client-1": return LangHelper.T("客流-1", "Customers -1");
            case "noOutside": return LangHelper.T("禁外出", "No Outside");
            case "noScav": return LangHelper.T("禁拾荒", "No Scavenging");
            case "flavor": return "";
            case "heal+10": return LangHelper.T("好心人送药", "Kind customer sends medicine");
            case "moodEnc+5": return LangHelper.T("路人鼓励", "Passerby cheers you up");
        }
        if (fx.StartsWith("sell")) return LangHelper.T("售价" + fx.Substring(4) + "%", "Sell " + fx.Substring(4) + "%");
        if (fx.StartsWith("bargain")) return LangHelper.T("议价" + fx.Substring(7) + "%", "Bargaining " + fx.Substring(7) + "%");
        if (fx.StartsWith("budget")) return LangHelper.T("预算" + fx.Substring(6) + "%", "Budget " + fx.Substring(6) + "%");
        if (fx.StartsWith("scav")) return LangHelper.T("拾荒" + fx.Substring(4) + "次", "Scavenging " + fx.Substring(4));
        if (fx.StartsWith("mood")) return LangHelper.T("心情" + fx.Substring(4), "Mood " + fx.Substring(4));
        if (fx.StartsWith("satD")) return LangHelper.T("饱食衰减" + fx.Substring(4) + "%", "Satiety loss " + fx.Substring(4) + "%");
        if (fx.StartsWith("thD")) return LangHelper.T("口渴衰减" + fx.Substring(3) + "%", "Thirst loss " + fx.Substring(3) + "%");
        if (fx.StartsWith("hD")) return LangHelper.T("健康衰减" + fx.Substring(2) + "%", "Health loss " + fx.Substring(2) + "%");
        if (fx.StartsWith("hR")) return LangHelper.T("健康恢复" + fx.Substring(2) + "%", "Health recovery " + fx.Substring(2) + "%");
        if (fx.StartsWith("cleanD")) return LangHelper.T("清洁衰减" + fx.Substring(6) + "%", "Cleanliness loss " + fx.Substring(6) + "%");
        if (fx.StartsWith("cleanR")) return LangHelper.T("清洁恢复" + fx.Substring(6) + "%", "Cleanliness recovery " + fx.Substring(6) + "%");
        if (fx.StartsWith("sleepR")) return LangHelper.T("睡眠恢复" + fx.Substring(6) + "%", "Sleep recovery " + fx.Substring(6) + "%");
        if (fx.StartsWith("socD")) return LangHelper.T("社交衰减" + fx.Substring(4) + "%", "Social loss " + fx.Substring(4) + "%");
        if (fx.StartsWith("socR")) return LangHelper.T("社交恢复" + fx.Substring(4) + "%", "Social recovery " + fx.Substring(4) + "%");
        if (fx.StartsWith("wound")) return LangHelper.T("受伤" + fx.Substring(5) + "%", "Injury " + fx.Substring(5) + "%");
        if (fx.StartsWith("sick")) return LangHelper.T("患病" + fx.Substring(4) + "%", "Illness " + fx.Substring(4) + "%");
        if (fx == "lostItem") return LangHelper.T("损失货物", "Lost goods");
        if (fx == "lostWaterItem") return LangHelper.T("水器损坏", "Water container damaged");
        if (fx.StartsWith("statusClient")) return LangHelper.T("状态客户-" + fx.Substring(13) + "%", "Customers from status -" + fx.Substring(13) + "%");
        if (fx.StartsWith("drop")) return LangHelper.T("掉落率" + fx.Substring(4) + "%", "Drop rate " + fx.Substring(4) + "%");
        return fx;
    }
    // 心情档位（v5.7 心情值替代精神 5 档）
    internal static int GetMoodTier()
    {
        int m = GetMood();
        if (m >= SATIETY_GOOD) return MOOD_HIGH;
        if (m >= 60) return MOOD_NORMAL;
        if (m >= 40) return MOOD_LOW;
        return MOOD_CRIT;
    }
    // 售价加成：粮仓 +5% + 昂扬累计 + 节点（sell±N，如蓬头垢面锁 sell-30 / 吃饱喝足锁 sell+5）
    internal static int GetSellBonusPct()
    {
        if (_sellBonusCache != -999) return _sellBonusCache;
        int granary = (GetGranaryDays() >= GRANARY_DAYS) ? 5 : 0;
        int elev = Math.Min(ELEV_MAX, GetElevCount());
        int fxSell = FxNum("sell");
        int comp = (int)GetCompBuffSellBonus();
        // 09-21 修：心情加成（>=60 → +10, <40 → -10）
        int moodSell = GetMood() >= 60 ? 10 : (GetMood() < 40 ? -10 : 0);
        int bonus = granary + elev + fxSell + comp + moodSell;
        
        _sellBonusCache = bonus;
        return bonus;
    }
    // 客户预算：max(心情, 昂扬) + 节点负向（budget±N 叠加；正向取更高）
    internal static int GetBudgetBonusPct()
    {
        if (_budgetBonusCache != -999) return _budgetBonusCache; // BUG-001：缓存
        int moodB = 0, m = GetMood();
        if (m >= SATIETY_GOOD) moodB = 15;
        else if (m >= 40) moodB = -5;
        else moodB = -15;
        int elevB = Math.Min(25, GetElevCount() * 5);
        int baseB = Math.Max(moodB, elevB);
        int nodeB = FxNum("budget");
        _budgetBonusCache = nodeB < 0 ? baseB + nodeB : Math.Max(baseB, nodeB);
        return _budgetBonusCache;
    }
    // 客流削减（节点池：client-2 / client-1 锁定；病恹恹爆发当日另按 50% 隔一skip一）
    internal static int GetClientReduction() => -FxNum("client");
    internal static int GetBurstClientCut() => _burstClientCut;  // 病恹恹爆发：当日客流-50% 标记（打烊结算后清零）
    // 禁外出（节点池：noOutside 锁定）
    internal static bool IsForbiddenOutside() => FxBool("noOutside");
    // 禁拾荒（节点池：noScav 锁定）
    internal static bool IsForbiddenScavenge() => FxBool("noScav");
    // 拾荒次数：心情 ≥80 +2 / <40 -2 + 节点（scav±N，如哈欠锁 scav-1 / 壮得像驴锁 scav+2）
    internal static int GetMoodScavBonus()
    {
        int b = FxNum("scav");
        int t = GetMoodTier();
        if (t == MOOD_HIGH) b += 2;
        if (t == MOOD_CRIT) b -= 2;
        return b;
    }
    // 议价成功率：max(心情≥80+15, 社交≥80+10) + 节点（bargain±N）
    internal static int GetBargainBonusPct()
    {
        if (_bargainBonusCache != -999) return _bargainBonusCache; // BUG-001：缓存
        int socialB = GetSocial() >= 80 ? 10 : 0;
        int moodB = GetMood() >= 80 ? 15 : 0;
        int b = Math.Max(socialB, moodB);
        b += FxNum("bargain");
        _bargainBonusCache = b;
        return b;
    }
    internal static int GetMoodScavDropPct() // 拾荒掉落率：≥80 +20% / <40 -20% + 节点（drop-20 破罐破摔池恶）
    {
        int t = GetMoodTier();
        int v = t == MOOD_HIGH ? 20 : t == MOOD_CRIT ? -20 : 0;
        return v + FxNum("drop");
    }
    internal static int GetMoodWoundPct()    // 受伤几率：≥80 -20% / <40 +20% + 节点（wound±N）
    {
        int t = GetMoodTier();
        int v = t == MOOD_HIGH ? -20 : t == MOOD_CRIT ? 20 : 0;
        return v + FxNum("wound");
    }
    internal static int GetSickChanceAdd() => FxNum("sick");
}
