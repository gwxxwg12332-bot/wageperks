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

    // ===== v5.8-8 节点系统：六状态独立档位 → 主导节点（多节点取最严重）=====
    private static int SatietyNode()
    {
        int s = GetSatiety();
        if (s < NODE_CRIT) return NODE_STARVING;
        if (s < NODE_BAD) return NODE_BELLY;
        if (s >= SATIETY_GOOD) return NODE_FED;
        return NODE_NONE;
    }
    private static int ThirstNode()
    {
        int t = GetThirstPct();
        if (t < NODE_CRIT) return NODE_THIRSTY;
        if (t < NODE_BAD) return NODE_DRYMOUTH;
        if (t >= THIRST_GOOD) return NODE_HYDRATED;
        return NODE_NONE;
    }
    private static int HealthNode()
    {
        int h = GetHealth();
        if (h < NODE_CRIT) return NODE_BEDRIDDEN;
        if (h < 40) return NODE_SICKLY;
        if (h >= HEALTH_GOOD) return NODE_ROBUST;
        return NODE_NONE;
    }
    private static int CleanNode()
    {
        int c = GetClean();
        if (c < 20) return NODE_DISHEVELED;
        if (c < 50) return NODE_GRIMY;
        if (c >= 80) return NODE_SPOTLESS;
        return NODE_NONE;
    }
    private static int SleepNode()
    {
        int s = GetSleep();
        if (s < 20) return NODE_HEAVYEYES;
        if (s < 50) return NODE_YAWNING;
        if (s >= 80) return NODE_RESTED;
        return NODE_NONE;
    }
    private static int SocialNode()
    {
        int s = GetSocial();
        if (s < 30) return NODE_DESERTED;
        if (s < 50) return NODE_COLDSHOULDER;
        if (s >= 80) return NODE_SOCIABLE;
        return NODE_NONE;
    }
    private static int MoodNode()
    {
        int m = GetMood();
        if (m < 40) return NODE_BROKEN;
        if (m < 60) return NODE_LISTLESS;
        return NODE_NONE;
    }
    // 主导节点：同时触发的节点中 severity 最高者（多节点不叠加，取最严重）
    internal static int GetDominantNode()
    {
        int best = NODE_NONE, bestSev = 0;
        int[] cand = { SatietyNode(), ThirstNode(), HealthNode(), CleanNode(), SleepNode(), SocialNode(), MoodNode() };
        foreach (int n in cand)
        {
            if (n < 0 || n >= NODES.Length) continue;
            int s = NODES[n].Sev;
            if (s > bestSev) { bestSev = s; best = n; }
        }
        return best;
    }
    // 当前锁定节点（打烊进入时锁定，节点期不变；PerkStatePersistence runID 隔离）
    internal static string GetStoredNodeKey() => SaveStore.GetString("nodeKey", "");
    internal static int GetNodeFxIdx() => SaveStore.GetInt("nodeFxIdx", -1);
    internal static string GetNodeFx()
    {
        int i = GetNodeFxIdx();
        NodeDef d = CurrentNode();
        return d != null && i >= 0 && i < d.Pool.Length ? d.Pool[i] : "";
    }
    private static NodeDef CurrentNode()
    {
        string key = GetStoredNodeKey();
        if (string.IsNullOrEmpty(key)) return null;
        foreach (NodeDef d in NODES) if (d.Key == key) return d;
        return null;
    }
    // 打烊调用：主导节点变化（进入/离开）→ 从该节点池随机抽 1 条并锁定；仍在同一节点 → 保持锁定
    // v5.9：nodeKey 变化当次先触发爆发事件（RollBurst：惩罚 + CompBuff 挂载/刷新），再抽池子（负面=纯恶性 / 正面=全良性）
    internal static void RollNodeFx()
    {
        int dom = GetDominantNode();
        string key = dom >= 0 ? NODES[dom].Key : "";
        if (key == GetStoredNodeKey()) return;   // 节点未变：锁定保持，不重抽、不爆发
        SaveStore.SetString("nodeKey", key);
        if (dom >= 0 && NODES[dom].Pool.Length > 0)
        {
            NodeDef d = NODES[dom];
            if (!d.IsPositive) RollBurst(d);     // v5.9：进负面节点当天触发爆发（惩罚+CompBuff）
            int idx = UnityEngine.Random.Range(0, d.Pool.Length);
            SaveStore.SetInt("nodeFxIdx", idx);
        }
        else SaveStore.SetInt("nodeFxIdx", -1);
    }
    // ===== v5.9 爆发事件（进负面节点当天打烊 1 次：大惩罚 + CompBuff 确定性补偿；同一节点停留多天不重复）=====
    private static void RollBurst(NodeDef d)
    {
        try
        {
            if (d == null || d.IsPositive) return;
            string desc = "";
            if (d.BurstPunish != null)
            {
                foreach (string p in d.BurstPunish)
                {
                    if (p == "lostItem") { int n = LostSmallItem(1); desc += n > 0 ? LangHelper.T("损失货物 ", "Lost goods ") : LangHelper.T("（无货可失）", "(nothing to lose)"); }
                    else if (p == "lostWaterItem") { LostWaterItem(); desc += LangHelper.T("水器损坏 ", "Water container damaged "); }
                    else if (p == "income-30" || p == "income-20" || p == "income-10") { int pct = Math.Abs(int.Parse(p.Substring(6))); int amt = ApplyIncomePunish(pct); desc += LangHelper.T("收入-" + pct + "%（-" + amt + "） ", "Income -" + pct + "% (-" + amt + ") "); }
                    else if (p == "health-15") { SetHealth(Math.Max(0, GetHealth() - 15)); desc += LangHelper.T("健康-15 ", "Health -15 "); }
                    else if (p == "sat+30") { SetSatiety(Math.Min(100, GetSatiety() + 30)); desc += LangHelper.T("饱食+30 ", "Satiety +30 "); }
                    else if (p == "mood-15") { SetMood(Math.Max(0, GetMood() - 15)); desc += LangHelper.T("心情-15 ", "Mood -15 "); }
                    else if (p == "clientToday-50") { _burstClientCut = 50; desc += LangHelper.T("今日客流-50% ", "Today's customers -50% "); }
                    else if (p == "healChance50")
                    {
                        if (UnityEngine.Random.value < 0.5f) { SetHealth(Math.Min(100, GetHealth() + 10)); desc += LangHelper.T("好心人送药+10 ", "Kind customer sends medicine +10 "); }
                    }
                    else if (p == "moodEncChance30")
                    {
                        if (UnityEngine.Random.value < 0.3f) { SetMood(Math.Min(100, GetMood() + 5)); desc += LangHelper.T("自我消化+5 ", "Self-soothe +5 "); }
                    }
                }
            }
            // CompBuff 挂载：同 buff 在身时刷新剩余天数、不叠加数值（P0 修正）
            if (!string.IsNullOrEmpty(d.CompBuff) && d.CompBuffDur > 0)
            {
                int cur = GetCompBuffDays(d.CompBuff);
                if (cur < d.CompBuffDur) SetCompBuffDays(d.CompBuff, d.CompBuffDur);
                string cbTxt = GetCompBuffLabel(d.CompBuff);
                desc += (cbTxt.Length > 0 ? cbTxt : d.CompBuff) + " " + d.CompBuffDur + LangHelper.T("天 ", "d ");
            }
            try { StoreUIManager.Instance.Notify(LangHelper.T("爆发·", "Burst·") + d.DisplayName + LangHelper.T("：", ": ") + desc.Trim(), "red"); } catch { }
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] RollBurst 异常: " + ex.Message); }
    }
    // 当日客流削减标记（病恹恹爆发：当日客流-50%，打烊结算后清零）
    internal static int _burstClientCut = 0;
}
