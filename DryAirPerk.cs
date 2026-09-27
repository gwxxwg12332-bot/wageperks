using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace JacksonPerks;

// ============================================================
// 负面特性：干燥空气（Cost=-3，返还3点特性点）
// 效果：集水器(evaporator)产出×0.5；水酒客户预算+25%
// ============================================================
internal sealed class DryAirPerk : CustomStartingPerk
{
    internal const string PerkId = "干燥空气";

    internal override string Id => PerkId;
    internal override string DisplayName => LangHelper.T("干燥空气", "Dry Air");
    internal override string Description => LangHelper.T("空气干燥，集水器(蒸发器)产量减半；但干燥环境提升了水酒的保存与流通价值，买水酒的客户预算+25%。", "Dry air halves evaporator output, but water/booze customers bring +25 percent budget.");
    internal override int Cost => -3;   // 09-28 v1.3.1：返还3点特性点
    internal override int Type => 1;     // 负面特性显示为红色

    internal override void OnNewGame() { }

    internal static bool IsActive()
    {
        return Core.PerkActive(PerkId);
    }

    // 集水器产出倍率（激活时×0.5）
    internal static float GetEvaporatorOutputMult()
    {
        return IsActive() ? 0.5f : 1.0f;
    }

    // 水酒客户预算加成（激活时+25%）
    internal static float GetWaterBoozeBudgetBonus()
    {
        return IsActive() ? 1.25f : 1.0f;
    }
}
