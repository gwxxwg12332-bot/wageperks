using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace JacksonPerks;

// ============================================================
// 负面特性：霉运缠身
// 每天打烊后可能丢钱（红色负面）
// ============================================================
internal sealed class BadLuckPerk : CustomStartingPerk
{
    internal const string PerkId = "霉运缠身";

    internal override string Id => PerkId;
    internal override string DisplayName => LangHelper.T("霉运缠身", "Bad Luck");
    internal override string Description => LangHelper.T("你仿佛被诅咒了。每天打烊后丢失一笔钱（1-200信用点），财运尽散。命运在跟你开玩笑。", "Seems cursed. Lose 1-200 credits every night.");
    internal override int Cost => -10;   // 09-23 用户拍板：-15→-10
    internal override int Type => 1;    // 负面特性显示为红色

    internal override void OnNewGame() { }

    internal static bool IsActive()
    {
        return Core.PerkActive(PerkId);
    }
}
