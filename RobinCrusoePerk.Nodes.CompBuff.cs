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
{  // 患病概率（节点 sick±N，每日结算用）

    // ===== v5.9 CompBuff（补偿 buff，Duration 制：进负面节点当天自动获得、按天倒计时、到期移除、期间实时生效）=====
    internal static readonly string[] COMP_BUFF_KEYS = { "eatEff","thirstEff50","thirstEff10","wearEff","drugEff","antiTheft","moodDamp","forage20","sell5","mood2","mood3","sleepR10","contraEff" };
    internal static int GetCompBuffDays(string eff) => WageSaveStore.GetInt(PERK_ID, "cb_" + eff, 0);
    internal static void SetCompBuffDays(string eff, int days) { WageSaveStore.SetInt(PERK_ID, "cb_" + eff, days > 0 ? days : 0); InvalidateTradeCaches(); }
    internal static void TickCompBuffs() { foreach (string k in COMP_BUFF_KEYS) { int d = GetCompBuffDays(k); if (d > 0) SetCompBuffDays(k, d - 1); } }
    internal static double GetEatEffMult() => GetCompBuffDays("eatEff") > 0 ? 1.5 : 1.0;          // 饿狼代谢 吃食物+50%
    internal static double GetThirstEffMult() { if (GetCompBuffDays("thirstEff50") > 0) return 0.5; if (GetCompBuffDays("thirstEff10") > 0) return 0.9; return 1.0; } // 耐旱-50%/省水-10%
    internal static double GetWearEffMult() => GetCompBuffDays("wearEff") > 0 ? 0.5 : 1.0;        // 糙人抗造 健康衰减-50%
    internal static double GetDrugEffMult() => GetCompBuffDays("drugEff") > 0 ? 1.5 : 1.0;        // 回光返照 药效+50%
    internal static double GetMoodDampMult() => GetCompBuffDays("moodDamp") > 0 ? 0.5 : 1.0;       // 摆烂反弹 心情掉速减半
    internal static double GetContraEffMult() => GetCompBuffDays("contraEff") > 0 ? 1.5 : 1.0;
    internal static double GetAntiTheftMult() => GetCompBuffDays("antiTheft") > 0 ? 0.5 : 1.0;     // 失眠警觉 偷窃概率-50%
    internal static int GetForageBonus() => GetCompBuffDays("forage20") > 0 ? 20 : 0;              // 独狼专注 觅食+20%
    internal static int GetCompBuffMoodBonus() { int v = 0; if (GetCompBuffDays("mood2") > 0) v += 2; if (GetCompBuffDays("mood3") > 0) v += 3; return v; } // 病中专注/松弛自洽/清静自处
    internal static double GetCompBuffSellBonus() => GetCompBuffDays("sell5") > 0 ? 5.0 : 0.0;     // 精打细算 卖出+5%
    internal static int GetCompBuffSleepRestore() => GetCompBuffDays("sleepR10") > 0 ? 10 : 0;     // 补觉高效 睡眠恢复+10%
    internal static string GetCompBuffLabel(string eff)
    {
        switch (eff)
        {
            case "eatEff": return LangHelper.T("饿狼代谢", "Ravenous Metabolism");
            case "thirstEff50": return LangHelper.T("耐旱体质", "Drought Resistance");
            case "thirstEff10": return LangHelper.T("省水习惯", "Water-Saving Habit");
            case "wearEff": return LangHelper.T("糙人抗造", "Tough Constitution");
            case "drugEff": return LangHelper.T("回光返照", "Last Gasp");
            case "antiTheft": return LangHelper.T("失眠警觉", "Insomniac Vigilance");
            case "moodDamp": return LangHelper.T("摆烂反弹", "Slacker Rebound");
            case "forage20": return LangHelper.T("独狼专注", "Lone Wolf Focus");
            case "sell5": return LangHelper.T("精打细算", "Penny Pincher");
            case "mood2": return LangHelper.T("心情+2", "Mood +2");
            case "mood3": return LangHelper.T("心情+3", "Mood +3");
            case "sleepR10": return LangHelper.T("补觉高效", "Efficient Napping");
            case "contraEff": return LangHelper.T("豁出去了", "Whatever It Takes");
        }
        return "";
    }
}
