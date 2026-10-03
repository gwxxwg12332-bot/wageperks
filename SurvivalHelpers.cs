using System;
using System.Collections.Generic;
using Il2Cpp;
using UnityEngine;
using MelonLoader;

namespace WageSurvival;
internal static partial class SurvivalFood
{
    internal static bool IsActive() => true; // 全局生效，不检测职业

    internal static void LogMsg(string msg) => Melon<Core>.Logger.Msg(msg);

    private static bool IsInDoctorNightInventory(GameItem item) => false;

    internal static int GetWaterMl(GameItem item)
    {
        if (item == null) return 0;
        // 10-03 照老mod写：优先读 LIQUID_CONTAINER_CURRENT tag（原只读GetCurrentCapacityML=0喝不了）
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
        catch { }
        try { return (int)Il2Cpp.WaterHelper.GetCurrentCapacityML(item); } catch { return 0; }
    }

    internal static int GetItemBaseValue(GameItem item)
    {
        try { return (int)item.unitBaseValue; } catch { return 100; }
    }

    internal static int GetCalLeft(GameItem item)
    {
        try
        {
            int cal = item.GetTagValue(SurvivalFood.CAL_LEFT_TAG, -1);
            if (cal > 0) return cal;
            return GetCalorie(item);
        }
        catch { return 0; }
    }

    internal static void SetCalLeft(GameItem item, int v)
    {
        try { item.SetTagValue(SurvivalFood.CAL_LEFT_TAG, v); } catch { }
    }

    internal static int GetCalorie(GameItem item)
    {
        try
        {
            if (item.IsTag("CALORIE_VALUE_TAG")) return item.GetTagValue("CALORIE_VALUE_TAG", 300);
            if (item.IsTag("CALORIE")) return item.GetTagValue("CALORIE", 300);
        }
        catch { }
        return 300;
    }

    internal static int GetFoodQuality(GameItem item)
    {
        try { return item.GetTagValue(SurvivalFood.FOOD_Q_TAG, 1); } catch { return 1; }
    }

    internal static void SetFoodQuality(GameItem item, int q)
    {
        try { item.SetTagValue(SurvivalFood.FOOD_Q_TAG, q); } catch { }
    }

    internal static bool TryExpel(GameItem item)
    {
        try { item.Expel(); return true; } catch { return false; }
    }

    internal static bool IsAlc(GameItem item) => FoodIdentifyHelper.IsAlc(item);
    internal static bool IsTobacco(GameItem item) => FoodIdentifyHelper.IsTobacco(item);
    internal static bool IsNarcotic(GameItem item) => FoodIdentifyHelper.IsNarcotic(item);
    internal static bool IsLottery(GameItem item) => FoodIdentifyHelper.IsLottery(item);
    internal static bool IsFood(GameItem item) => FoodIdentifyHelper.IsFood(item);
    internal static bool IsDrink(GameItem item) => FoodIdentifyHelper.IsDrink(item);
    internal static bool IsClean(GameItem item) => FoodIdentifyHelper.IsClean(item);
    internal static bool IsMedicine(GameItem item) => FoodIdentifyHelper.IsMedicine(item);
    internal static bool IsDailyNeed(GameItem item) => FoodIdentifyHelper.IsDailyNeed(item);

    // ===== Nodes系统辅助方法（简化版）=====
    internal static int ApplyIncomePunish(int pct) => 0;
    internal static int LostSmallItem(int n) => 0;
    internal static void LostWaterItem() { }

    // ===== FxEval辅助方法（简化版）=====
    internal const int ELEV_MAX = 5;
    internal static int GetElevCount() => SaveStore.GetInt("elevCount", 0);
    internal static int GetGranaryDays() => SaveStore.GetInt("granary", 0);

    // ===== Blood辅助方法（简化版）=====
    internal static bool _autoPopup = false;
    internal static void ExecuteGameOverBy(string reason) { }

    // ===== 昏迷跳天（方案A：帧钩子异步逐轮日切）=====
    internal static int _pendingSkipDays = 0; // 待跳天数
    internal static bool _jumping = false; // 防重入
    internal static int _jumpStartDay = -1; // 跳天开始时的dayCounter

    internal static void ForceComaSkip()
    {
        try
        {
            try { Il2Cpp.StoreUIManager.Instance.CloseAllUI(); } catch { }
            _pendingSkipDays = 3; // 写待跳天数，帧钩子执行
            _jumping = false;
            _jumpStartDay = -1;
            Core.LogMsg($"[WageSurvival] 昏迷跳天：待跳{_pendingSkipDays}天（帧钩子执行）");
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival] ForceComaSkip异常: " + ex.Message); }
    }

    // 挂FrameUpdate（InputActionManager.Update Postfix）里调
    internal static void TickSkipDays()
    {
        try
        {
            if (_pendingSkipDays <= 0 || _jumping) return;

            // 第一轮：记录开始天数
            if (_jumpStartDay < 0)
            {
                try
                {
                    _jumpStartDay = Il2Cpp.StoreStation.Instance.dayCounter;
                    Core.LogMsg($"[WageSurvival] 跳天开始 dayCounter={_jumpStartDay} 待跳={_pendingSkipDays}");
                }
                catch (System.Exception ex) { Core.LogMsg("[WageSurvival] 读dayCounter异常: " + ex.Message); return; }
            }

            // 发起一轮日切
            _jumping = true;
            try
            {
                int currentDay = Il2Cpp.StoreStation.Instance.dayCounter;
                Core.LogMsg($"[WageSurvival] 发起一轮日切 dayCounter={currentDay}");
                var shutter = Il2Cpp.StoreShutterButton.Instance;
                if (shutter != null)
                {
                    shutter.EndAndClose(); // 完整关门流程（异步动画链）
                }
                else
                {
                    Core.LogMsg("[WageSurvival] StoreShutterButton.Instance为空，跳天失败");
                    _pendingSkipDays = 0;
                    _jumping = false;
                    return;
                }
            }
            catch (System.Exception ex)
            {
                Core.LogMsg("[WageSurvival] 日切异常: " + ex.Message);
                _pendingSkipDays = 0;
                _jumping = false;
                return;
            }

            // 等日切完成（轮间等待——下一轮检查dayCounter）
            System.Threading.Thread.Sleep(1000); // 等1秒让动画链跑完
            _pendingSkipDays--;
            _jumping = false;

            if (_pendingSkipDays <= 0)
            {
                int endDay = -1;
                try { endDay = Il2Cpp.StoreStation.Instance.dayCounter; } catch { }
                Core.LogMsg($"[WageSurvival] 跳天完成！dayCounter {_jumpStartDay} → {endDay}");
            }
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival] TickSkipDays异常: " + ex.Message); }
    }
}