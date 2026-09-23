using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace JacksonPerks;

// ============================================================
// 伙伴型特性：蛙娘（09-23 Perk 化——Cost 3 用户拍板）
// 喂食/照顾提升六维与好感；在场客户预算×4、议价+50；销赃；偷钱/跑路
// ============================================================
// [NonSelectablePerk]：有意不登记进 CustomStartingPerks.All —— 蛙娘并入蛙哥牛逼，
// 不单独出现在特性选择界面（IsActive 委托给 WagePowerPerk）。有此标注后，DEBUG 漏登记断言会豁免它，
// 使断言报警真正等于"漏登记"。
[NonSelectablePerk]
internal sealed class WageGirlPerk : CustomStartingPerk
{
    internal const string PerkId = "蛙娘";

    internal override string Id => PerkId;
    internal override string DisplayName => LangHelper.T("蛙娘", "Wage Girl");
    internal override string Description => LangHelper.T("伙伴型特性（3点）。蛙哥留下的仿生女仆：喂食/照顾提升六维，在店时客户预算×4、议价+50%。喂她违禁品可洗白（消除标签）或销赃（带回干净货），克扣存小金库。给零花钱加好感（每日前三次）。妙妙箱放螺丝过夜自动升级。但心情差会偷钱货，连续不照顾会跑路14天。", "Partner perk (3 points). Wage's biomimetic maid: feed and care raise 6 stats; in-store budget x4 and bargain +50%. Feed her contraband to launder (remove tag) or fence (bring back clean goods), kept money goes to savings. Allowance raises affection (first 3 times daily). Put nuts in Wage Box overnight to upgrade. But bad mood steals money/goods, neglect makes her leave for 14 days.");
    internal override int Cost => 3; // 09-23 用户拍板：综合考量 3 点
    internal override int Type => 0; // 正面特性（收益为主，偷钱为伴随代价）

    internal override void OnNewGame()
    {
    }

    internal static bool IsActive()
    {
        return WagePowerPerk.IsActive(); // 09-23 蛙娘并入蛙哥牛逼：选蛙哥牛逼就有蛙娘
    }
}
