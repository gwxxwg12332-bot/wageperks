using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace JacksonPerks;

// ============================================================
// 负面特性：童叟无欺（原笑面虎重做 09-19）
// 声誉增长速度 +25%，卖出商品收益 -25%
// ============================================================
internal sealed class SmilingTigerPerk : CustomStartingPerk
{
    internal const string PerkId = "童叟无欺";

    internal override string Id => PerkId;
    internal override string DisplayName => LangHelper.T("童叟无欺", "Honest Dealer");
    internal override string Description => LangHelper.T("做生意童叟无欺：声誉增长速度 +25%，客户更信任你；但你的售价也得公道——卖出商品收益 -25%。", "Honest dealing: reputation gain +25 percent, but you sell at fair prices - sale income -25 percent.");
    internal override int Cost => -10; // 09-20 用户拍板：1→-10
    internal override int Type => 1; // 负面红色
    internal override string[] IncompatibleIds => new[] { "笑面虎" }; // 与笑面虎互斥

    internal override void OnNewGame()
    {
    }

    internal static bool IsActive()
    {
        return Core.PerkActive(PerkId);
    }
}
