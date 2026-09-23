using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace JacksonPerks;

// ============================================================
// 混合特性：刀尖舔血
// 违禁品买卖价+20%，检查频率+30%
// ============================================================
internal sealed class RiskTakerPerk : CustomStartingPerk
{
    internal const string PerkId = "刀尖舔血";

    internal override string Id => PerkId;
    internal override string DisplayName => LangHelper.T("刀尖舔血", "Blood Blade");
    internal override string Description => LangHelper.T("高风险高回报的赌徒特性（2点）。违禁品买卖价+20%，利润丰厚；代价是治安部永远盯着你——每天强制检查，连满信誉豁免也无效。吞噬季每 10 天降临：机器里的模组会互相吞噬融合，产出高级违禁品。利润越高，越可能翻车。", "High-risk high-reward gambler (2 points). Contraband price +20 percent, but Security is always watching: mandatory inspection every day — even max reputation won't spare you. Every 10 days, Cannibalism Season strikes: modules in machines devour each other, yielding high-grade contraband. Higher profit, higher risk.");
    internal override int Cost => 2; // 09-16 用户拍板：需要 2 特性点
    internal override int Type => 0; // 09-17 用户拍板：正面特性（绿色）；违禁品收益是主要面向，强制检查为伴随代价

    internal override void OnNewGame()
    {
    }

    internal static bool IsActive()
    {
        return Core.PerkActive(PerkId);
    }

    // 获取违禁品价格加成
    public static float GetContrabandPriceBonus()
    {
        return IsActive() ? 1.20f : 1.0f;
    }

    // 获取检查频率加成
    public static float GetInspectionChanceBonus()
    {
        return IsActive() ? 1.30f : 1.0f;
    }
}











