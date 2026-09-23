using System;
using HarmonyLib;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Il2Cpp;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace JacksonPerks;
internal static partial class RobinCrusoePerk
{

    internal static bool IsExcludedModule(GameItem item)
    {
        try {
            string id = item.identifier;
            if (string.IsNullOrEmpty(id)) return true;
            return id.StartsWith("furnace_module_");
        } catch { return true; }
    }

    // ===== 机器初始耗电 +2（拆包 09-10：GetMachinePowerUsage 是统一耗电读口；Postfix 兜底全机器，鲁滨逊职业内生效）=====
    public static void PostfixGetMachinePowerUsage(ref int __result)
    {
        try
        {
            if (!IsActive()) return;
            __result += 2;
        }
        catch (System.Exception ex) { Core.LogMsg("[RobinCrusoePerk.ContainerUpgrade] 异常: " + ex.Message); }
    }

    // ===== 模板 0.5 总系数（用户拍板 09-09：模块/模板贡献减半；升级累加不受影响——MoreUpdate 写 TOTAL 标签，减半在读取端）=====
    // 拆包 2.5.26 [L1]：产出量走 GetCurrentPerformanceBonus/QualityBonus；处理速度走 ApplyBasicModuleEffect 内直读 TOTAL 标签（Getter 不覆盖）；
    // 原版模块走 ModifyTempStatFromBaseByPercentage；三个 TAG 减半后模块升级（AddVirtualBonus 累加）仍正常
    public static void PostfixGetCurrentPerformanceBonus(GameItem gameItem, ref int __result)
    {
        try
        {
            if (!IsActive() || gameItem == null) return;
            if (__result > 0) __result = Math.Max(0, (int)(__result * 0.5));
            // 机器升级（wageUpgradePct 独立 tag，不被模块聚合覆盖）：+1%/次，不减半
            int up = GetTagIntSafe(gameItem, "wageUpgradePct");
            if (up > 0) __result += up;
        }
        catch (System.Exception ex) { Core.LogMsg("[RobinCrusoePerk.ContainerUpgrade] 异常: " + ex.Message); }
    }

    public static void PostfixGetCurrentQualityBonus(GameItem gameItem, ref int __result)
    {
        try
        {
            if (!IsActive() || gameItem == null) return;
            if (__result > 0) __result = Math.Max(0, (int)(__result * 0.5));
            int up = GetTagIntSafe(gameItem, "wageUpgradePct");
            if (up > 0) __result += up;
        }
        catch (System.Exception ex) { Core.LogMsg("[RobinCrusoePerk.ContainerUpgrade] 异常: " + ex.Message); }
    }

    public static void PostfixApplyBasicModuleEffect(GameInventory invModule, GameItem item, GameItem system)
    {
        try
        {
            if (!IsActive() || item == null) return;
            var ts = item.GetTagReadonly("CURRENT_PROCESSING_SPEED_TAG");
            if (ts != null && ts.valueInt > 0) SetTagIntValue(item, "CURRENT_PROCESSING_SPEED_TAG", Math.Max(0, (int)(ts.valueInt * 0.5)));
            // v5.9 效率升级（用户拍板 09-09：金属锭拖机器 +1%，无限叠加）：速度 = 原×0.5 + 机器效率升级数
            // wageUpgradeEff 直接加（不减半），写 item 无则取 system
            GameItem target = item;
            if (target.GetTagReadonly("wageUpgradeEff") == null && system != null) target = system;
            var eff = target.GetTagReadonly("wageUpgradeEff");
            if (eff != null && eff.valueInt > 0 && ts != null && ts.valueInt > 0)
                SetTagIntValue(target, "CURRENT_PROCESSING_SPEED_TAG", Math.Max(0, (int)(ts.valueInt * 0.5) + eff.valueInt));
        }
        catch (System.Exception ex) { Core.LogMsg("[RobinCrusoePerk.ContainerUpgrade] 异常: " + ex.Message); }
    }

    public static void PostfixModifyTempStatFromBaseByPercentage(GameItem item, int percentage)
    {
        try
        {
            if (!IsActive() || item == null) return;
            foreach (string t in new[] { "TEMP_PERCENTAGE_PERFORMANCE_INT", "TEMP_PERCENTAGE_EFFICIENCY_INT", "TEMP_PERCENTAGE_QUALITY_INT" })
            {
                var ts = item.GetTagReadonly(t);
                if (ts != null && ts.valueInt > 0) SetTagIntValue(item, t, Math.Max(0, (int)(ts.valueInt * 0.5)));
            }
        }
        catch (System.Exception ex) { Core.LogMsg("[RobinCrusoePerk.ContainerUpgrade] 异常: " + ex.Message); }
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
        catch (System.Exception ex) { Core.LogMsg("[RobinCrusoePerk.ContainerUpgrade] 异常: " + ex.Message); }
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
        catch (System.Exception ex) { Core.LogMsg("[RobinCrusoePerk.ContainerUpgrade] 异常: " + ex.Message); }
    }

    // ===== 面板显示 0.5（用户拍板 09-09："机器面板显示的数字"；拆包：CreateModuleTooltip/AddModuleStatLine 直读 tag
    // 不经 Getter → 面板显示原值、产出已减半，两者不同源。此 Postfix 在统计显示行统一减半性能/效率/质量，面板=实际）=====
    public static void PostfixAddModuleStatLine(string statName, ref int baseValue, ref int tempValue)
    {
        try
        {
            if (!IsActive()) return;
            if (statName == null) return;
            // statName 可能是 tag 名或本地化显示名，双匹配（大小写不敏感）
            string n = statName.ToLowerInvariant();
            if (n.Contains("performance") || n.Contains("efficiency") || n.Contains("quality")
                || n.Contains("性能") || n.Contains("效率") || n.Contains("质量"))
            {
                if (baseValue > 0) baseValue = Math.Max(0, (int)(baseValue * 0.5));
                if (tempValue > 0) tempValue = Math.Max(0, (int)(tempValue * 0.5));
            }
        }
        catch (System.Exception ex) { Core.LogMsg("[RobinCrusoePerk.ContainerUpgrade] 异常: " + ex.Message); }
    }

    // ===== 机器 0.5 总系数（用户拍板 09-09；拆包 2.5.27 [L1]：逐台 Postfix ×0.5 含基础，不写 tag——写 -50 只有 2 条路径天然减半）=====
    // water_recycler 效率：ApplyPerformanceWaterRecyclerEffect(GameItem) 后效率 tag ×0.5（含 50 基础；船舶系统）
    public static void PostfixApplyPerformanceWaterRecyclerEffect(GameItem __0)
    {
        try
        {
            if (!IsActive() || __0 == null) return;
            var ts = __0.GetTagReadonly("WATER_RECYCLER_CURRENT_EFFICIENCY_INT");
            if (ts != null && ts.valueInt > 0) SetTagIntValue(__0, "WATER_RECYCLER_CURRENT_EFFICIENCY_INT", Math.Max(0, (int)(ts.valueInt * 0.5)));
        }
        catch (System.Exception ex) { Core.LogMsg("[RobinCrusoePerk.ContainerUpgrade] 异常: " + ex.Message); }
    }

    // ===== 机器 0.5 总系数（拆包 2.5.28 补齐）=====
    // moisture_farm 产出量：MachineMoistureFarm.GetOutputVolume(GameItem)→int ×0.5（含基础）
    public static void PostfixGetOutputVolume(ref int __result)
    {
        try { if (IsActive() && __result > 0) __result = Math.Max(1, (int)(__result * 0.5)); } catch { }
    }

    // water_purifier 基础半：WaterHelper.RemoveContaminantFromContainer 返回移除量 ×0.5（净化慢一半；加成半已被模板0.5覆盖）
    public static void PostfixRemoveContaminantFromContainer(ref int __result)
    {
        try { if (IsActive() && __result > 0) __result = Math.Max(1, (int)(__result * 0.5)); } catch { }
    }

}
