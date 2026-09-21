using UnityEngine;

namespace JacksonPerks;

// 常用数值计算
internal static class MathHelper
{
    public static int Clamp(int v, int min, int max)
    {
        return v < min ? min : (v > max ? max : v);
    }

    public static float Lerp(float a, float b, float t)
    {
        return a + (b - a) * t;
    }

    public static int RandomRange(int min, int max)
    {
        return Random.Range(min, max + 1);
    }
}