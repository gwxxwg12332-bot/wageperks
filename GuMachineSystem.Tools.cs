using System;
using Il2Cpp;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace JacksonPerks;

public static partial class GuMachineSystem
{
    private static Sprite SpriteFromPixels(Color[] pixels, int w, int h)
    {
        try
        {
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.hideFlags = HideFlags.DontSave;
            tex.SetPixels(pixels);
            tex.Apply();
            Sprite sp = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
            sp.hideFlags = HideFlags.DontSave;
            return sp;
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
        catch { }
    }
}
