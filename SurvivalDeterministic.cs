using System;
using System.Security.Cryptography;
using System.Text;
using Il2Cpp;

namespace WageSurvival;

/// <summary>
/// 确定性调度工具（10-07 鲁滨逊完全归 WS WS-3 搬迁：WP DeterministicSchedule.cs 同源复制）
/// 用 runID 作为随机数种子，同一存档同一天的事件是确定的，读档后不变。
/// </summary>
internal static class DeterministicSchedule
{
    /// <summary>当前游戏天数（从 StoreStation 获取）</summary>
    internal static int CurrentDay
    {
        get
        {
            try { return Math.Max(0, StoreStation.GetDayCounter()); }
            catch { return 0; }
        }
    }

    /// <summary>当前周数（第1天=第0周）</summary>
    internal static int CurrentWeek => Math.Max(0, (CurrentDay - 1) / 7);

    /// <summary>获取当前存档的runID（用于隔离不同存档）</summary>
    internal static string GetRunKey()
    {
        try
        {
            PlayerStore instance = PlayerStore.Instance;
            if (instance != null && !string.IsNullOrEmpty(instance.runID))
            {
                return instance.runID;
            }
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival.DeterministicSchedule] 异常: " + ex.Message); }
        return "no-run";
    }

    /// <summary>获取每日状态key（runID + "|" + currentDay）</summary>
    internal static string GetDailyKey()
    {
        return GetRunKey() + "|" + CurrentDay;
    }

    /// <summary>获取每周状态key（runID + "|" + currentWeek）</summary>
    internal static string GetWeeklyKey()
    {
        return GetRunKey() + "|" + CurrentWeek;
    }
}
