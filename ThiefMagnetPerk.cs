using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace WagePerks;

// ============================================================
// 负面特性：招贼体质
// 治安部检查概率+20%，可疑客户更多（红色负面）
// ============================================================
internal sealed class ThiefMagnetPerk : CustomStartingPerk
{
    internal const string PerkId = "招贼体质";

    internal override string Id => PerkId;
    internal override string DisplayName => LangHelper.T("招贼体质", "Thief Magnet");
    internal override string Description => LangHelper.T("你天生招贼，可疑顾客特别爱光顾你的店。治安部检查概率+20%且无法豁免；每天夜里可能被偷走价值最高的物品（次日小贩会半价卖回）；黑市买家每天上门；小偷经常来店里踩点。夜里锁好门。", "Naturally attracts thieves. Inspection +20 percent (cannot be exempted); the most valuable item may be stolen nightly (the fence sells it back at half price next day); black-market buyers visit daily; thieves often case the shop.");
    internal override int Cost => -8;   // 10-06 用户拍板：-2 → -8（负面给点）
    internal override int Type => 1;    // 负面特性显示为红色

    internal override void OnNewGame() { }

    internal static bool IsActive()
    {
        return Core.PerkActive(PerkId);
    }

}
