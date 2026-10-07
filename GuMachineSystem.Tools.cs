using System;
using Il2Cpp;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace WagePerks;

public static partial class GuMachineSystem
{
    private static Sprite SpriteFromPixels(Color[] pixels, int w, int h)
    {
        try
        {
            // 10-07 修复：统一走 WagePixelSprites.CreatePixelSprite（SetPixels 行序 Y 翻转——AI 模组像素数组同 getdata 序，直接写上下颠倒）
            return WagePixelSprites.CreatePixelSprite(pixels, w, h, 100f);
        }
        catch (Exception ex) { Core.LogMsg("[养蛊机] SpriteFromPixels 异常: " + ex.Message); return null; }
    }
    private static void SetField(GameItem it, string field, object val)
    {
        try
        {
            var f = typeof(GameItem).GetField(field,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (f != null) f.SetValue(it, val);
        }
        catch (System.Exception ex) { Core.LogMsg("[GuMachineSystem.Tools] 异常: " + ex.Message); }
    }
}
