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
    // 必须 [HarmonyPriority(300)]（低于夜猫子默认400，确保在其后跑，见设计稿§2.4）
    [HarmonyPriority(300)]
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

    // 拾荒受伤保护：RollMinorWound Prefix
    public static bool PrefixRollMinorWound()
    {
        try
        {
            int level = WageBrokerPermitHelper.GetMaxPermitLevel();
            if (level >= 3) return false; // 三级：完全免疫
            if (level >= 2) return UnityEngine.Random.value >= 0.5f; // 二级：50%免伤 = 概率减半
        }
        catch (Exception ex) { Core.LogMsg("[蛙哥许可] PrefixRollMinorWound异常: " + ex.Message); }
        return true; // 一级/无：走原生
    }

    // 拾荒受伤保护：RollMajorWound Prefix
    public static bool PrefixRollMajorWound()
    {
        try
        {
            int level = WageBrokerPermitHelper.GetMaxPermitLevel();
            if (level >= 3) return false; // 三级：完全免疫
            if (level >= 2) return UnityEngine.Random.value >= 0.5f; // 二级：50%免伤 = 概率减半
        }
        catch (Exception ex) { Core.LogMsg("[蛙哥许可] PrefixRollMajorWound异常: " + ex.Message); }
        return true; // 一级/无：走原生
    }

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
            foreach (var item in em.GetAllItems())
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
