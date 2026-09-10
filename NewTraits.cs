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
    internal override string Description => LangHelper.T("高风险高回报的赌徒特性。违禁品买卖价+20%，利润丰厚；但代价是每天开张必遭治安部强制检查。利润越高，越可能翻车。", "High-risk high-reward. Contraband price +20 percent, but you face a mandatory security inspection every day. Higher profit, higher risk.");
    internal override int Cost => 0;
    internal override int Type => 2; // 混合特性显示为黄色

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

// ============================================================
// 混合特性：好酒之徒
// 酒类买卖价+25%，打烊后可能宿醉
// ============================================================
internal sealed class WineLoverPerk : CustomStartingPerk
{
    internal const string PerkId = "好酒之徒";

    internal override string Id => PerkId;
    internal override string DisplayName => LangHelper.T("好酒之徒", "Wine Lover");
    internal override string Description => LangHelper.T("内行懂酒，酒类买卖价+25%，客户觉得你是懂行的人。但代价是打烊后可能'宿醉'：次日-1客户或议价-10%。白天懂酒，晚上头痛。", "Alcohol connoisseur. Alcohol price +25 percent. Possible hangover after closing.");
    internal override int Cost => 1;
    internal override int Type => 2; // 混合特性显示为黄色
    private static bool _hungover = false;

    internal override void OnNewGame()
    {
        _hungover = false;
    }

    internal static bool IsActive()
    {
        return Core.PerkActive(PerkId);
    }

    // 获取酒类价格加成
    public static float GetAlcoholPriceBonus()
    {
        return IsActive() ? 1.25f : 1.0f;
    }

    // 检查是否宿醉
    public static bool IsHungover()
    {
        return _hungover;
    }

    // 打烊后roll宿醉
    public static void RollHangover()
    {
        if (!IsActive()) return;
                // 用确定性随机数，读档后宿醉概率一致
                int day = StoreStation.GetDayCounter();
                if (DeterministicRandom.NextBool("wine_lover_hangover", day, 0.3)) // 30%概率宿醉
        {
            _hungover = true;
            // 保存宿醉状态到PlayerPrefs（读档后恢复）
            try { PerkStatePersistence.SetBool(PerkId, "hungover", true); } catch { }
        }
    }

    // 新的一天清除宿醉
    public static void ClearHangover()
    {
        if (_hungover)
        {
            _hungover = false;
            // 清除PlayerPrefs中的宿醉状态
            try { PerkStatePersistence.SetBool(PerkId, "hungover", false); } catch { }
        }
    }

    // 从PlayerPrefs恢复宿醉状态（读档时调用）
    public static void LoadHangoverState()
    {
        try
        {
            if (PerkStatePersistence.HasKey(PerkId, "hungover"))
            {
                _hungover = PerkStatePersistence.GetBool(PerkId, "hungover", false);
            }
        }
        catch { }
    }
}

// ============================================================
// 混合特性：笑面虎
// 议价成功率+25%，客户好感获取-25%
// ============================================================
internal sealed class SmilingTigerPerk : CustomStartingPerk
{
    internal const string PerkId = "笑面虎";

    internal override string Id => PerkId;
    internal override string DisplayName => LangHelper.T("笑面虎", "Smiling Tiger");
    internal override string Description => LangHelper.T("微笑着宰客。议价成功率+25%，客户更容易接受你的报价；但客户好感获取-25%，他们觉得被你算计了。钱赚到了，朋友没了。", "Smile while ripping off. Negotiation +25 percent, reputation gain -25 percent.");
    internal override int Cost => 1;
    internal override int Type => 2; // 混合特性显示为黄色（25%声望惩罚太重，议价收益不明显）

    internal override void OnNewGame()
    {
    }

    internal static bool IsActive()
    {
        return Core.PerkActive(PerkId);
    }

    // 获取议价成功率加成
    public static float GetBargainSuccessBonus()
    {
        return IsActive() ? 1.25f : 1.0f;
    }

    // 获取客户好感惩罚
    public static float GetReputationPenalty()
    {
        return IsActive() ? 0.75f : 1.0f;
    }
}

// ============================================================
// 补丁：新的一天处理好酒之徒宿醉
// ============================================================
// [HarmonyPatch(typeof(GameMaster), "OnNewDay")]
internal static class NewTraitsNewDayPatch
{
    static void Postfix()
    {
        // 好酒之徒：新的一天清除宿醉
        WineLoverPerk.ClearHangover();
    }
}

// ============================================================
// 补丁：打烊后处理好酒之徒宿醉
// ============================================================
// [HarmonyPatch(typeof(PlayerStore), "DismissCurrentClient")]
internal static class NewTraitsDismissPatch
{
    static void Postfix()
    {
        // 好酒之徒：最后一个客户走后roll宿醉（简化处理）
        // 实际应该在打烊时触发，这里简化
    }
}

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

// ============================================================
// 负面特性：霉运缠身
// 每天打烊后可能丢钱（红色负面）
// ============================================================
internal sealed class BadLuckPerk : CustomStartingPerk
{
    internal const string PerkId = "霉运缠身";

    internal override string Id => PerkId;
    internal override string DisplayName => LangHelper.T("霉运缠身", "Bad Luck");
    internal override string Description => LangHelper.T("你仿佛被诅咒了。每天打烊后都会丢失一笔钱（50-200信用点），财运尽散。命运在跟你开玩笑。", "Seems cursed. Lose 50-200 credits every day after closing.");
    internal override int Cost => -10;   // 返还10点（每天50-200平均125/天×永久）
    internal override int Type => 1;    // 负面特性显示为红色

    internal override void OnNewGame() { }

    internal static bool IsActive()
    {
        return Core.PerkActive(PerkId);
    }
}

// ============================================================
// 负面特性：信誉扫地
// 卖价-10%，买价+10%（红色负面）
// ============================================================
internal sealed class BadReputationPerk : CustomStartingPerk
{
    internal const string PerkId = "信誉扫地";

    internal override string Id => PerkId;
    internal override string DisplayName => LangHelper.T("信誉扫地", "Bad Reputation");
    internal override string Description => LangHelper.T("你的名声很差，顾客不信任你。卖东西价格-20%，买东西价格+20%——顾客总想趁火打劫。想翻身，先挽回名声。", "Poor reputation. Sell price -20 percent, buy price +20 percent.");
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
