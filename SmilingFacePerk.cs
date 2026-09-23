using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace JacksonPerks;

// ============================================================
// 正面特性：笑面虎（与童叟无欺互斥）
// 所有商品售价 +25%（正面绿色）
// ============================================================
internal sealed class SmilingFacePerk : CustomStartingPerk
{
    internal const string PerkId = "笑面虎";

    internal override string Id => PerkId;
    internal override string DisplayName => LangHelper.T("笑面虎", "Smiling Tiger");
    internal override string Description => LangHelper.T("你总是笑脸迎人，顾客愿意为你的笑容多掏钱：所有商品售价 +25%，客户更愿意为你的笑脸买单；但声誉获取 -25%。但笑脸背后也有代价：声誉获取 -25%——你这么会做人，治安部反而觉得你可疑。", "Always smiling, customers pay more: sale price +25 percent. But too smooth: reputation gain -25 percent.");
    internal override int Cost => 1;
    internal override int Type => 2; // 混合黄色（售价+25% / 声誉-25%）
    internal override string[] IncompatibleIds => new[] { "童叟无欺" }; // 与童叟无欺互斥

    internal override void OnNewGame()
    {
    }

    internal static bool IsActive()
    {
        return Core.PerkActive(PerkId);
    }
}
