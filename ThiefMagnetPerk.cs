using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace JacksonPerks;

// ============================================================
// 负面特性：招贼体质
// 治安部检查概率+20%，可疑客户更多（红色负面）
// ============================================================
internal sealed class ThiefMagnetPerk : CustomStartingPerk
{
    internal const string PerkId = "招贼体质";

    internal override string Id => PerkId;
    internal override string DisplayName => LangHelper.T("招贼体质", "Thief Magnet");
    internal override string Description => LangHelper.T("你天生招贼，可疑顾客特别爱光顾你的店。治安部检查概率+20%，小偷、骗子和可疑客户出现频率大幅上升。夜里锁门要锁好。", "Naturally attracts thieves. Inspection +20 percent, suspicious customers increased.");
    internal override int Cost => -2;   // 返还2点
    internal override int Type => 1;    // 负面特性显示为红色

    internal override void OnNewGame() { }

    internal static bool IsActive()
    {
        return Core.PerkActive(PerkId);
    }

}
