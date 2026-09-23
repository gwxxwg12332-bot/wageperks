using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace JacksonPerks;

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
            try { WageSaveStore.SetBool(PerkId, "hungover", true); } catch { }
        }
    }

    // 新的一天清除宿醉
    public static void ClearHangover()
    {
        if (_hungover)
        {
            _hungover = false;
            // 清除PlayerPrefs中的宿醉状态
            try { WageSaveStore.SetBool(PerkId, "hungover", false); } catch { }
        }
    }

    // 从PlayerPrefs恢复宿醉状态（读档时调用）
    public static void LoadHangoverState()
    {
        try
        {
            if (WageSaveStore.HasKey(PerkId, "hungover"))
            {
                _hungover = WageSaveStore.GetBool(PerkId, "hungover", false);
            }
        }
        catch (System.Exception ex) { Core.LogMsg("[NewTraits] 异常: " + ex.Message); }
    }
}
