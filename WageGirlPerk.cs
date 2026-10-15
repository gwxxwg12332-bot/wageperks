using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace WagePerks;

// ============================================================
// 伙伴型特性：蛙娘（10-15 拆分独立——不再并入蛙哥牛逼）
// 喂食/照顾提升六维与好感；在场客户预算×4、议价+50%；销赃；偷钱/跑路
// 独立可选（3点），也独立出现在 B2 蛙哥面板卡（可购买/升级）
// ============================================================
// [NonSelectablePerk] 已移除（10-15）：蛙娘独立，入 CustomStartingPerks.All，
//   开局特性选择界面可见 + B2 面板独立卡；IsActive 兼容三来源（开局已选/B2 购买/旧档实体存在）
internal sealed class WageGirlPerk : CustomStartingPerk
{
    internal const string PerkId = "蛙娘";

    internal override string Id => PerkId;
    internal override string DisplayName => LangHelper.T("蛙娘", "Wage Girl");
    internal override string Description => LangHelper.T("伙伴型特性（3点）。仿生女仆：喂食/照顾提升六维，在店时客户预算随好感提升（低×1.5/中×2.5/高×4）、议价+50%。喂她违禁品可洗白（消除标签）或销赃（带回干净货），克扣存小金库。给零花钱加好感（每日前三次）。妙妙箱放螺丝过夜自动升级。但心情差会偷钱货，连续不照顾会跑路14天。", "Partner perk (3 points). Biomimetic maid: feed and care raise 6 stats; in-store budget scales with affection (low x1.5/mid x2.5/high x4) and bargain +50%. Feed her contraband to launder (remove tag) or fence (bring back clean goods), kept money goes to savings. Allowance raises affection (first 3 times daily). Put nuts in Wage Box overnight to upgrade. But bad mood steals money/goods, neglect makes her leave for 14 days.");
    internal override int Cost => 3; // 09-23 用户拍板：综合考量 3 点
    internal override int Type => 0; // 正面特性（收益为主，偷钱为伴随代价）

    internal override void OnNewGame()
    {
    }

    // 10-15 拆分：独立激活。兼容三来源：开局已选（Core.PerkActive）/ B2 面板购买（GetEffectLevel≥1）；旧档实体存在由日切 Exists() 兜底
    internal static bool IsActive()
    {
        if (Core.PerkActive(PerkId)) return true;
        try { if (B2ServiceStore.GetEffectLevel(PerkId) >= 1) return true; } catch { }
        return false;
    }
}
