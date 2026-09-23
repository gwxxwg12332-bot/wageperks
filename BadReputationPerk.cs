using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace JacksonPerks;

// ============================================================
// 负面特性：信誉扫地
// 卖价-10%，买价+10%（红色负面）
// ============================================================
internal sealed class BadReputationPerk : CustomStartingPerk
{
    internal const string PerkId = "信誉扫地";

    internal override string Id => PerkId;
    internal override string DisplayName => LangHelper.T("信誉扫地", "Bad Reputation");
    internal override string Description => LangHelper.T("你的名声很差，顾客不信任你。卖东西价格-20%，买东西价格+20%。想翻身：提升 4 个势力好感各到一星（≥20），全部达标后负面效果自动解除。", "Poor reputation. Sell price -20%, buy price +20%. To clear: raise all 4 factions to 1-star (≥20); effect auto-disables when all met.");
    internal override int Cost => -7;   // 返还7点（实际卖-20%/买+20%，4势力好感全20才解除，50天+）
    internal override int Type => 1;    // 负面特性显示为红色

    internal override void OnNewGame() { }

    internal static bool IsActive()
    {
        return Core.PerkActive(PerkId);
    }

    // 信誉扫地解除：每个势力好感达到一星（>=20）后，负面效果消失
    internal static bool IsCleared()
    {
        try
        {
            int total = 0, cleared = 0;
            StoreReputation[] factions = new StoreReputation[]
            {
                StoreReputation.GetSecFaction(),
                StoreReputation.GetRevFaction(),
                StoreReputation.GetBMFaction(),
                StoreReputation.GetULFaction()
            };
            foreach (StoreReputation fr in factions)
            {
                if (fr == null) continue;
                total++;
                int rep = 0;
                try { rep = fr.GetReputation(); } catch (Exception ex) { Core.LogMsg("[信誉扫地] 读取好感失败: " + ex.Message); }
                string fid = "";
                try { fid = fr.factionId ?? ""; } catch { }
                if (rep >= 20) cleared++;
            }
            bool all = total > 0 && cleared >= total;
            return all;
        }
        catch (Exception ex)
        {
            Core.LogMsg("[信誉扫地] 解除检查失败: " + ex.Message);
            return false;
        }
    }
}
