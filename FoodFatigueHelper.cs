using System;
using System.Collections.Generic;

namespace JacksonPerks;

// ============================================================
// 吃腻机制（蛙娘+鲁滨逊复用）
// ============================================================
internal static class FoodFatigueHelper
{
    // 内存缓存：最近3天吃了什么（key = systemId_day）
    private static readonly Dictionary<string, List<string>> _recentFood = new();

    /// <summary>
    /// 获取吃腻倍率（1.0 / 0.5 / 0.25）
    /// </summary>
    public static float GetMultiplier(string foodId, string systemId)
    {
        try
        {
            if (string.IsNullOrEmpty(foodId)) return 1.0f;

            // 解析最近3天吃了什么
            var recent = GetRecentFood(systemId);
            if (recent.Count < 2) return 1.0f;

            // 连续2天同一种 → 0.5
            int streak2 = (recent[^1] == foodId && recent[^2] == foodId) ? 1 : 0;
            if (recent.Count >= 3 && recent[^3] == foodId) streak2++;

            if (streak2 >= 2) return 0.25f; // 连续3天
            if (streak2 >= 1) return 0.5f;  // 连续2天
            return 1.0f;
        }
        catch { return 1.0f; }
    }

    /// <summary>
    /// 记录今天吃了这个食物
    /// </summary>
    public static void RecordFood(string foodId, string systemId)
    {
        try
        {
            if (string.IsNullOrEmpty(foodId)) return;
            var list = GetRecentFood(systemId);
            list.Add(foodId);
            // 只保留最近3天
            if (list.Count > 3) list.RemoveAt(0);
            _recentFood[systemId] = list;
        }
        catch { }
    }

    private static List<string> GetRecentFood(string systemId)
    {
        _recentFood.TryGetValue(systemId, out var list);
        return list ?? new List<string>();
    }
}