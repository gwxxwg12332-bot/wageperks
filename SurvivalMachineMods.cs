using System;
using HarmonyLib;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Il2Cpp;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace WageSurvival;

// ===== 10-07 鲁滨逊完全归 WS WS-3：机器/模块域 9 方法搬迁（WP RobinCrusoePerk.ContainerUpgrade.MachineMods.cs 同源）=====
// 适配标注：
//   - CrusoeEffHalfEnabled/Ratio（WP BuildConfig）= WS 固定常量 0.5f（回执标注：WS 版效率减半固定开启 0.5，无配置开关；
//     需玩家反馈才配置化）
//   - GetOutputVolume 去掉 DryAirPerk 分支（WS 无 DryAir——双装时 WP DryAirPerk 独立挂点叠加 ×0.5，数学等价原 WP 单方法体）
//   - CreateMachineryTooltip（养蛊机/瓶印机特性域）按方法体归属不搬——WP 保留该挂点（回执标注）
internal static partial class SurvivalFood
{
    // 机器初始耗电 +2（统一耗电读口）
    public static void PostfixGetMachinePowerUsage(ref int __result)
    {
        try
        {
            if (!IsActive() || !SurvivalTrade.IsRobinsonRun()) return;
            __result += 2;
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival.ContainerUpgrade] 异常: " + ex.Message); }
    }

    // 模板 0.5 总系数（性能/质量读取端减半 + wageUpgradePct 机器升级 +1%/次不减半）
    public static void PostfixGetCurrentPerformanceBonus(GameItem gameItem, ref int __result)
    {
        try
        {
            if (!IsActive() || !SurvivalTrade.IsRobinsonRun() || gameItem == null) return;
            if (__result > 0) __result = Math.Max(0, (int)(__result * 0.5));
            int up = GetTagIntSafe(gameItem, "wageUpgradePct");
            if (up > 0) __result += up;
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival.ContainerUpgrade] 异常: " + ex.Message); }
    }

    public static void PostfixGetCurrentQualityBonus(GameItem gameItem, ref int __result)
    {
        try
        {
            if (!IsActive() || !SurvivalTrade.IsRobinsonRun() || gameItem == null) return;
            if (__result > 0) __result = Math.Max(0, (int)(__result * 0.5));
            int up = GetTagIntSafe(gameItem, "wageUpgradePct");
            if (up > 0) __result += up;
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival.ContainerUpgrade] 异常: " + ex.Message); }
    }

    // 10-07 #13（用户拍板）：机器效率减半比例（配置门控——关=1f 原值；MelonPreferences WageSurvival 分类）
    private static float EffMult() { return SurvivalConfig.EffHalfEnabled ? SurvivalConfig.EffRatio : 1f; }

    public static void PostfixApplyBasicModuleEffect(GameInventory invModule, GameItem item, GameItem system)
    {
        try
        {
            if (!IsActive() || !SurvivalTrade.IsRobinsonRun() || item == null) return;
            var ts = item.GetTagReadonly("CURRENT_PROCESSING_SPEED_TAG");
            if (ts != null && ts.valueInt > 0) SetTagIntValue(item, "CURRENT_PROCESSING_SPEED_TAG", Math.Max(0, (int)(ts.valueInt * EffMult())));
            GameItem target = item;
            if (target.GetTagReadonly("wageUpgradeEff") == null && system != null) target = system;
            var eff = target.GetTagReadonly("wageUpgradeEff");
            if (eff != null && eff.valueInt > 0 && ts != null && ts.valueInt > 0)
                SetTagIntValue(target, "CURRENT_PROCESSING_SPEED_TAG", Math.Max(0, (int)(ts.valueInt * EffMult()) + eff.valueInt));
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival.ContainerUpgrade] 异常: " + ex.Message); }
    }

    public static void PostfixModifyTempStatFromBaseByPercentage(GameItem item, int percentage)
    {
        try
        {
            if (!IsActive() || !SurvivalTrade.IsRobinsonRun() || item == null) return;
            foreach (string t in new[] { "TEMP_PERCENTAGE_PERFORMANCE_INT", "TEMP_PERCENTAGE_EFFICIENCY_INT", "TEMP_PERCENTAGE_QUALITY_INT" })
            {
                var ts = item.GetTagReadonly(t);
                if (ts != null && ts.valueInt > 0) SetTagIntValue(item, t, Math.Max(0, (int)(ts.valueInt * EffMult())));
            }
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival.ContainerUpgrade] 异常: " + ex.Message); }
    }

    // 面板显示 0.5（统计显示行统一减半性能/效率/质量——配置门控）
    public static void PostfixAddModuleStatLine(string statName, ref int baseValue, ref int tempValue)
    {
        try
        {
            if (!IsActive() || !SurvivalTrade.IsRobinsonRun()) return;
            if (statName == null) return;
            string n = statName.ToLowerInvariant();
            if (n.Contains("performance") || n.Contains("efficiency") || n.Contains("quality")
                || n.Contains("性能") || n.Contains("效率") || n.Contains("质量"))
            {
                if (baseValue > 0) baseValue = Math.Max(0, (int)(baseValue * EffMult()));
                if (tempValue > 0) tempValue = Math.Max(0, (int)(tempValue * EffMult()));
            }
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival.ContainerUpgrade] 异常: " + ex.Message); }
    }

    // water_recycler 效率 tag ×0.5（含 50 基础——配置门控）
    public static void PostfixApplyPerformanceWaterRecyclerEffect(GameItem __0)
    {
        try
        {
            if (!IsActive() || !SurvivalTrade.IsRobinsonRun() || __0 == null) return;
            var ts = __0.GetTagReadonly("WATER_RECYCLER_CURRENT_EFFICIENCY_INT");
            if (ts != null && ts.valueInt > 0)
            {
                SetTagIntValue(__0, "WATER_RECYCLER_CURRENT_EFFICIENCY_INT", Math.Max(0, (int)(ts.valueInt * EffMult())));
            }
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival.ContainerUpgrade] 异常: " + ex.Message); }
    }

    // moisture_farm 产出量 ×0.5（含基础；DryAir 分支裁剪——双装由 WP DryAirPerk 独立挂点叠加；#13 配置门控）
    public static void PostfixGetOutputVolume(ref int __result)
    {
        try {
            if (__result <= 0) return;
            if (IsActive() && SurvivalTrade.IsRobinsonRun())
            {
                __result = Math.Max(1, (int)(__result * EffMult()));
            }
        } catch { }
    }

    // water_purifier 净化量 ×0.5（#13 配置门控）
    public static void PostfixRemoveContaminantFromContainer(ref int __result)
    {
        try
        {
            if (IsActive() && SurvivalTrade.IsRobinsonRun() && __result > 0)
            {
                __result = Math.Max(1, (int)(__result * EffMult()));
            }
        } catch { }
    }

    // ===== tag 辅助（WP MachineMods.cs:94-120 同源搬迁）=====
    internal static bool IsExcludedModule(GameItem item)
    {
        try {
            string id = item.identifier;
            if (string.IsNullOrEmpty(id)) return true;
            return id.StartsWith("furnace_module_");
        } catch { return true; }
    }
    internal static void SetTagIntValue(GameItem item, string tag, int value)
    {
        try
        {
            if (!item.IsTag(tag)) item.EnableTag(tag, true);
            System.Action<TagState> sysAct = delegate (TagState state) { state.SetInt(value); };
            var il2cppAct = DelegateSupport.ConvertDelegate<Il2CppSystem.Action<TagState>>((System.Delegate)sysAct);
            item.ModifyTag(tag, il2cppAct, false);
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival.ContainerUpgrade] 异常: " + ex.Message); }
    }
    internal static int GetTagIntSafe(GameItem item, string tag)
    {
        try { var t = item.GetTagReadonly(tag); if (t != null) return t.valueInt; } catch { }
        return 0;
    }
    internal static void AddTagInt(GameItem item, string tag, int delta)
    {
        try
        {
            int v = GetTagIntSafe(item, tag);
            SetTagIntValue(item, tag, v + delta);
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival.ContainerUpgrade] 异常: " + ex.Message); }
    }
}
