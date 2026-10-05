using System;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace WagePerks;

// ============================================================
// 蛙哥的许可补丁：外出次数加成 + 拾荒受伤保护
// ============================================================
internal static class WageBrokerPermitPatches
{
    // 打烊 Postfix：visitLeftTonight += 持有张数
    // 10-05 拆包实锤：XIAOWO三更行者 EndDay Postfix [HarmonyPriority(0)] + Math.Max(值,3) 只抬下限不覆盖；
    // 注册表传 -100（后跑叠加，先跑会被 Max 抬下限吞掉 1-2 张）。attribute 与注册统一为 -100（原 300 残留已改）。
    [HarmonyPriority(-100)]
    public static void PostfixEndDay()
    {
        try
        {
            int n = WageBrokerPermitHelper.CountAllPermits();
            if (n > 0 && PlayerStore.Instance != null)
            {
                PlayerStore.Instance.visitLeftTonight += n;
                Core.LogMsg("[蛙哥许可] 打烊加成: visitLeftTonight +" + n + " = " + PlayerStore.Instance.visitLeftTonight);
            }
        }
        catch (Exception ex) { Core.LogMsg("[蛙哥许可] PostfixEndDay异常: " + ex.Message); }
    }

    // 拾荒受伤免疫（10-05 拆包实锤：ScavHelper.RollMinorWound/RollMajorWound 全库 0 调用点=死 API，
    // 真实受伤链=ScavengeDumpingGrounds→HealthData.RollLuck→ReceiveMinorWound/ReceiveMajorWound（唯一施加点）。
    // 照鲁滨逊先例（RobinCrusoePerk.PrefixReceiveWound）改挂 HealthData。Prefix return false → 原生+Postfix 都不跑。
    private static bool PermitImmune()
    {
        try
        {
            int level = WageBrokerPermitHelper.GetMaxPermitLevel();
            if (level >= 3) return false; // 三级：完全免疫
            if (level >= 2) return UnityEngine.Random.value >= 0.5f; // 二级：50%免伤 = 概率减半
        }
        catch (Exception ex) { Core.LogMsg("[蛙哥许可] PermitImmune异常: " + ex.Message); }
        return true; // 一级/无：走原生
    }

    // 拾荒轻伤免疫（HealthData.ReceiveMinorWound Prefix）
    public static bool PrefixReceiveMinorWound() { return PermitImmune(); }

    // 拾荒重伤免疫（HealthData.ReceiveMajorWound Prefix）
    public static bool PrefixReceiveMajorWound() { return PermitImmune(); }

    // 夜间受伤免疫（HealthData.HandleNightlyWound Prefix——10-05 拆包实锤：夜间受伤不走 Receive*，OnSleep 概率判定后直接 dec 健康值）
    public static bool PrefixHandleNightlyWound() { return PermitImmune(); }

    // 蛙哥充电器：打烊自动给背包所有电池充电
    [HarmonyPriority(300)]
    public static void PostfixEndDayCharger()
    {
        try
        {
            int level = WageBrokerChargerHelper.GetMaxChargerLevel();
            if (level <= 0) return;

            var em = EmporiumEntry.Instance;
            if (em == null) return;

            int chargedCount = 0;
            // 10-05 增加充电范围：GetAllItems 不递归容器内部（WageBrother.cs:741 实锤）→ 改用 CollectAllItems（顶层+容器内部递归，深度8防环，养蛊机式死循环不出现）
            foreach (var item in WageBrother.CollectAllItems())
            {
                if (item == null) continue;
                if (!item.IsTag("power_source_item")) continue;

                if (level == 1)
                {
                    RobinCrusoePerk.AddTagInt(item, "power_source_item_energy", 3);
                }
                else if (level == 2)
                {
                    RobinCrusoePerk.AddTagInt(item, "power_source_item_energy", 6);
                }
                else if (level == 3)
                {
                    int maxEnergy = RobinCrusoePerk.GetTagIntSafe(item, "power_source_item_max_energy");
                    RobinCrusoePerk.AddTagInt(item, "power_source_item_energy", maxEnergy); // 满电
                }
                chargedCount++;
            }
            if (chargedCount > 0)
                Core.LogMsg("[蛙哥充电器] 打烊充电: level=" + level + " 充了" + chargedCount + "个电池");
        }
        catch (Exception ex) { Core.LogMsg("[蛙哥充电器] PostfixEndDayCharger异常: " + ex.Message); }
    }
}
