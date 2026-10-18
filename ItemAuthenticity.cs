using System;
using Il2Cpp;

namespace WagePerks;

// ============================================================
// 物品真伪/来源甄别（2026-10-18 五项需求 §3 定案）
// 口径 = DestinyDice.Core.cs:231-279 原文迁移（不新增判定规则，消除双份口径）
// 使用点：寄卖 ConsignMode 裁决表（§3.2）、DestinyDice 吸收价值
// ============================================================
internal static class ItemAuthenticity
{
    /// <summary>假货判定。口径 = DestinyDice.Core.cs:231-259 原文（realCounterfeit / fakeGenuine / CATEGORY_GENUINE_INJECTOR 显示差异 / ISSUE_LABEL / 贴标水）。</summary>
    internal static bool IsFake(GameItem item)
    {
        try
        {
            if (item == null) return false;
            if (item.IsAlreadyContainFeatureWithId("realCounterfeit")) return true;  // 香烟假货
            if (item.IsAlreadyContainFeatureWithId("fakeGenuine")) return true;      // 邮票假/高仿烟
            var inj = item.FindItemFeatureByCategory("CATEGORY_GENUINE_INJECTOR");
            if (inj != null
                && inj.GetBaseFakeDisplay() != inj.GetBaseRealDisplay()) return true; // 注射器非真品
            if (item.IsTag("ISSUE_LABEL")) return true;                              // 通用问题标记
            // P2-14（审阅 20261018）：ISSUE_LABEL 双链路口径不同——寄卖链路按规格 §3.2 第3行优先拒收（该分支在寄卖裁决表不可达）；
            //   本分支保留：DestinyDice 吸收价值链路仍用（规格 3.3 验收第 6 条「两链路口径一致」以外场景）
            if (IsMislabeledWater(item)) return true;                                // 贴标水
            return false;
        }
        catch { return false; }
    }

    // 贴标水判定：水 feature 假显示 ≠ 真显示（照抄原生 CheckSoldDirtyWater 对比；原文 DestinyDice.Core.cs:249-259）
    private static bool IsMislabeledWater(GameItem item)
    {
        try
        {
            if (!item.IsTag("LIQUID_CONTAINER_TAG")) return false;
            var wf = Il2Cpp.WaterFeatureHelper.GetWaterFeature(item);
            if (wf == null) return false;
            return wf.GetBaseFakeDisplay() != wf.GetBaseRealDisplay();
        }
        catch { return false; }
    }

    /// <summary>假货修正值（百分数偏移）。取 realCounterfeit / fakeGenuine / CATEGORY_GENUINE_INJECTOR 的 GetFakeValueModifier()；无命中返回 -90（即按一折）。</summary>
    internal static int FakeModifier(GameItem item)
    {
        try
        {
            if (item == null) return -90;
            var f = item.FindItemFeatureByID("realCounterfeit")
                 ?? item.FindItemFeatureByID("fakeGenuine")
                 ?? item.FindItemFeatureByCategory("CATEGORY_GENUINE_INJECTOR");
            if (f != null) return f.GetFakeValueModifier();
            return -90; // 无修正可读 = 一折（贴标水/无修正假货）
        }
        catch { return -90; }
    }

    /// <summary>赃物判定。口径 = MerchantHelper.cs:125-127（stolen / TAG_STOLEN / STOLEN_VALUE_INT &gt; 0）。</summary>
    internal static bool IsStolen(GameItem item)
    {
        try
        {
            if (item == null) return false;
            if (item.IsTag("stolen") || item.IsTag("TAG_STOLEN")) return true;
            var heatTag = item.GetTagReadonly("STOLEN_VALUE_INT");
            if (heatTag != null && heatTag.GetInt() > 0) return true;
            return false;
        }
        catch { return false; }
    }
}
