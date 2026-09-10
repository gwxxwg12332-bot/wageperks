using System;
using System.Text;
using Il2Cpp;

namespace JacksonPerks;

// ============================================================
// 确定性随机数工具（参考XIAOWOTradePerks的FNV-1a hash实现）
// 用hash(runID+类型+day/week)代替random，读档不重随机
// ============================================================
internal static class DeterministicRandom
{
    // FNV-1a hash算法（32位）
    private static uint Fnv1aHash(string input)
    {
        if (string.IsNullOrEmpty(input)) return 0;
        uint hash = 2166136261; // FNV offset basis
        foreach (char c in input)
        {
            hash ^= c;
            hash *= 16777619; // FNV prime
        }
        return hash;
    }

    // 获取当前runID（用于确定性随机数的种子）
    private static string GetRunId()
    {
        try
        {
            // 尝试从PlayerStore获取runID（用反射查找可能的字段名）
            if (PlayerStore.Instance != null)
            {
                var type = PlayerStore.Instance.GetType();
                // 尝试常见的字段名
                string[] possibleFields = { "saveID", "saveId", "runID", "runId", "gameID", "gameId", "sessionID", "sessionId" };
                foreach (var fieldName in possibleFields)
                {
                    var field = type.GetField(fieldName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (field != null)
                    {
                        var value = field.GetValue(PlayerStore.Instance);
                        if (value != null && !string.IsNullOrEmpty(value.ToString()))
                        {
                            return value.ToString();
                        }
                    }
                }
            }
        }
        catch { }
        // 兜底：用固定字符串
        return "default_run";
    }

    // 基于类型和天数的确定性随机数（0.0 ~ 1.0）
    internal static double NextDouble(string type, int day)
    {
        string seed = GetRunId() + "_" + type + "_" + day;
        uint hash = Fnv1aHash(seed);
        return (hash % 10000) / 10000.0;
    }

    // 基于类型和天数的确定性整数（0 ~ maxValue-1）
    internal static int Next(string type, int day, int maxValue)
    {
        if (maxValue <= 0) return 0;
        string seed = GetRunId() + "_" + type + "_" + day;
        uint hash = Fnv1aHash(seed);
        return (int)(hash % (uint)maxValue);
    }

    // 基于类型和天数的确定性整数（minValue ~ maxValue-1）
    internal static int Next(string type, int day, int minValue, int maxValue)
    {
        if (maxValue <= minValue) return minValue;
        return minValue + Next(type, day, maxValue - minValue);
    }

    // 基于类型和天数的确定性布尔值（probability为true的概率，0.0~1.0）
    internal static bool NextBool(string type, int day, double probability)
    {
        return NextDouble(type, day) < probability;
    }

    // 从数组中确定性选择一个元素
    internal static T Choose<T>(string type, int day, T[] array)
    {
        if (array == null || array.Length == 0) return default(T);
        int index = Next(type, day, array.Length);
        return array[index];
    }

    // 从列表中确定性选择一个元素
    internal static T Choose<T>(string type, int day, System.Collections.Generic.List<T> list)
    {
        if (list == null || list.Count == 0) return default(T);
        int index = Next(type, day, list.Count);
        return list[index];
    }
}
