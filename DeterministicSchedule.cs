using System;
using System.Security.Cryptography;
using System.Text;
using Il2Cpp;

namespace JacksonPerks;

/// <summary>
/// 确定性调度工具（参考XIAOWO Trade Perks实现）
/// 用 runID 作为随机数种子，同一存档同一天的事件是确定的，读档后不变。
/// 用 runID + "|" + currentDay 作为状态key，天然隔离不同存档。
/// </summary>
internal static class DeterministicSchedule
{
    /// <summary>当前游戏天数（从StoreStation获取）</summary>
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
        catch { }
        return "no-run";
    }

    /// <summary>获取runID的哈希值（用于确定性随机数种子）</summary>
    internal static int GetRunHash()
    {
        string runKey = GetRunKey();
        using (MD5 md5 = MD5.Create())
        {
            byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(runKey));
            return BitConverter.ToInt32(hash, 0);
        }
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

    /// <summary>
    /// 生成确定性随机数（0.0 ~ 1.0）
    /// 同一存档同一天同一salt返回相同值，读档后不变。
    /// </summary>
    /// <param name="salt">随机数种子盐值（如"gunsmith_visit"）</param>
    /// <returns>0.0 ~ 1.0的确定性随机数</returns>
    internal static double DeterministicRandom(string salt)
    {
        string seedStr = GetRunKey() + "|" + CurrentDay + "|" + salt;
        using (MD5 md5 = MD5.Create())
        {
            byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(seedStr));
            uint value = BitConverter.ToUInt32(hash, 0);
            return (double)value / uint.MaxValue;
        }
    }

    /// <summary>
    /// 生成确定性随机整数（min ~ max-1）
    /// </summary>
    internal static int DeterministicRange(int min, int max, string salt)
    {
        if (max <= min) return min;
        double r = DeterministicRandom(salt);
        return min + (int)(r * (max - min));
    }

    /// <summary>
    /// 确定性概率判定
    /// </summary>
    /// <param name="probability">概率（0.0 ~ 1.0）</param>
    /// <param name="salt">随机数种子盐值</param>
    /// <returns>是否命中</returns>
    internal static bool DeterministicChance(double probability, string salt)
    {
        return DeterministicRandom(salt) < probability;
    }
}
