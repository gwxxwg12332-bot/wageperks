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

    private static int GetFoodDecayDay(GameItem item)
    {
        if (item == null) return 0;
        try
        {
            if (item.IsTag(FOOD_DECAY_DAY_TAG))
            {
                var ts = item.GetTagReadonly(FOOD_DECAY_DAY_TAG);
                if (ts != null) return ts.GetInt();
            }
        }
        catch (System.Exception ex) { Core.LogMsg("[RobinCrusoePerk.Food.Decay] 异常: " + ex.Message); }
        return StoreStation.GetDayCounter();
    }

    private static void SetFoodDecayDay(GameItem item, int day)
    {
        if (item == null) return;
        try
        {
            if (!item.IsTag(FOOD_DECAY_DAY_TAG)) item.EnableTag(FOOD_DECAY_DAY_TAG, true);
            System.Action<TagState> sysAct = delegate (TagState state) { state.SetInt(day); };
            var il2cppAct = DelegateSupport.ConvertDelegate<Il2CppSystem.Action<TagState>>((System.Delegate)sysAct);
            item.ModifyTag(FOOD_DECAY_DAY_TAG, il2cppAct, false);
        }
        catch (System.Exception ex) { Core.LogMsg("[RobinCrusoePerk.Food.Decay] 异常: " + ex.Message); }
    }

    private static int DecayFoodsAndCount(out int fresh, out int stale, out int rotten)
    {
        fresh = 0; stale = 0; rotten = 0;
        try
        {
            int today = StoreStation.GetDayCounter();
            var items = CollectAllItems();
            foreach (GameItem item in items)
            {
                if (item == null || !IsFood(item)) continue;
                int q = GetFoodQuality(item);
                if (q < 0) q = 0;
                int last = GetFoodDecayDay(item);
                if (today - last >= 2 && q < 3) { q++; SetFoodQuality(item, q); SetFoodDecayDay(item, today); }
                if (q <= 0) fresh++;
                else if (q == 1) { stale++; fresh++; }
                else if (q == 2) stale++;
                else rotten++;
            }
        }
        catch (System.Exception ex) { Core.LogMsg("[RobinCrusoePerk.Food.Decay] 异常: " + ex.Message); }
        return fresh + stale;
    }

    private static void TryInfect(double chance)
    {
        try
        {
            if (UnityEngine.Random.value < (float)chance)
            {
                SetHealth(Math.Max(0, GetHealth() - 40)); // v5.7 生病事件：健康-40（拆包回填1：原生无生病系统，mod 自建）
                RefreshStatusPanel(); // 生病实时刷新常驻面板（状态行 buff 跟随）
            }
        }
        catch (System.Exception ex) { Core.LogMsg("[RobinCrusoePerk.Food.Decay] 异常: " + ex.Message); }
    }

}
