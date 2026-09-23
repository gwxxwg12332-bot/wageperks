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

    internal static int GetFoodQuality(GameItem item)
    {
        if (item == null) return -1;
        try
        {
            if (!item.IsTag(FOOD_Q_TAG)) return -1;
            var ts = item.GetTagReadonly(FOOD_Q_TAG);
            return ts != null ? ts.GetInt() : -1;
        }
        catch { return -1; }
    }

    internal static void SetFoodQuality(GameItem item, int value)
    {
        if (item == null) return;
        try
        {
            if (value < 0) { item.DisableTag(FOOD_Q_TAG, true); return; }
            if (!item.IsTag(FOOD_Q_TAG)) item.EnableTag(FOOD_Q_TAG, true);
            System.Action<TagState> sysAct = delegate (TagState state) { state.SetInt(value); };
            var il2cppAct = DelegateSupport.ConvertDelegate<Il2CppSystem.Action<TagState>>((System.Delegate)sysAct);
            item.ModifyTag(FOOD_Q_TAG, il2cppAct, false);
        }
        catch (System.Exception ex) { Core.LogMsg("[RobinCrusoePerk.Food.Attributes] 异常: " + ex.Message); }
    }

    internal static int GetCalorie(GameItem item)
    {
        if (item == null) return 0;
        try
        {
            if (item.IsTag("CALORIE_VALUE_TAG"))
            {
                var ts = item.GetTagReadonly("CALORIE_VALUE_TAG");
                if (ts != null) return Math.Max(0, ts.GetInt());
            }
        }
        catch (System.Exception ex) { Core.LogMsg("[RobinCrusoePerk.Food.Attributes] 异常: " + ex.Message); }
        try
        {
            if (item.IsTag("CALORIE"))
            {
                var ts = item.GetTagReadonly("CALORIE");
                if (ts != null) return Math.Max(0, ts.GetInt());
            }
        }
        catch (System.Exception ex) { Core.LogMsg("[RobinCrusoePerk.Food.Attributes] 异常: " + ex.Message); }
        return 300;
    }
    // 已食用判定：剩余卡路里 < 满量

    internal static bool IsEaten(GameItem item)
    {
        if (item == null || !IsFood(item)) return false;
        try
        {
            if (!item.IsTag(CAL_LEFT_TAG)) return false;
            return GetCalLeft(item) < GetCalorie(item);
        }
        catch { return false; }
    }

    internal static int GetCalLeft(GameItem item)
    {
        if (item == null) return 0;
        try
        {
            if (item.IsTag(CAL_LEFT_TAG))
            {
                var ts = item.GetTagReadonly(CAL_LEFT_TAG);
                if (ts != null) return Math.Max(0, ts.GetInt());
            }
        }
        catch (System.Exception ex) { Core.LogMsg("[RobinCrusoePerk.Food.Attributes] 异常: " + ex.Message); }
        return GetCalorie(item);
    }

    internal static void SetCalLeft(GameItem item, int value)
    {
        if (item == null) return;
        try
        {
            if (value <= 0) { item.DisableTag(CAL_LEFT_TAG, true); return; }
            if (!item.IsTag(CAL_LEFT_TAG)) item.EnableTag(CAL_LEFT_TAG, true);
            System.Action<TagState> sysAct = delegate (TagState state) { state.SetInt(value); };
            var il2cppAct = DelegateSupport.ConvertDelegate<Il2CppSystem.Action<TagState>>((System.Delegate)sysAct);
            item.ModifyTag(CAL_LEFT_TAG, il2cppAct, false);
        }
        catch (System.Exception ex) { Core.LogMsg("[RobinCrusoePerk.Food.Attributes] 异常: " + ex.Message); }
    }

    internal static int GetEffectiveCal(GameItem item)
    {
        int q = GetFoodQuality(item);
        int cal = GetCalLeft(item);
        if (q >= 3) return (int)(cal * 0.2);
        if (q == 2) return (int)(cal * 0.5);
        return cal;
    }

    internal static int GetWaterMl(GameItem item)
    {
        if (item == null) return 0;
        // 优先读 LIQUID_CONTAINER_CURRENT tag（09-11 日志实锤：ModifyTag 扣 tag 成功但 GetCurrentCapacityML 读内层液体注册表恒 2000 不同步；原版喝水/UI 都以 tag 为权威）
        try
        {
            if (item.IsTag("LIQUID_CONTAINER_CURRENT"))
            {
                var ts = item.GetTagReadonly("LIQUID_CONTAINER_CURRENT");
                if (ts != null)
                {
                    try { return Math.Max(0, (int)ts.GetFloat()); } catch { return Math.Max(0, ts.GetInt()); }
                }
            }
        }
        catch (System.Exception ex) { Core.LogMsg("[RobinCrusoePerk.Food.Attributes] 异常: " + ex.Message); }
        try { return Math.Max(0, WaterHelper.GetCurrentCapacityML(item)); } catch { }
        return BOTTLE_ML;
    }

}
