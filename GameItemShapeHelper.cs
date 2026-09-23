using System;
using System.Reflection;
using Il2Cpp;

namespace JacksonPerks;

// ============================================================
// 物品 shape 反射工具（优雅写 modifiedShape，不重置动画帧）
// ============================================================
internal static class GameItemShapeHelper
{
    // 缓存字段（性能）
    private static FieldInfo _modifiedShapeField;

    static GameItemShapeHelper()
    {
        try
        {
            _modifiedShapeField = typeof(GameItem).GetField(
                "<modifiedShape>k__BackingField",
                BindingFlags.NonPublic | BindingFlags.Instance);
        }
        catch (System.Exception ex) { Core.LogMsg("[GameItemShapeHelper] 异常: " + ex.Message); }
    }

    /// <summary>
    /// 优雅设置物品 modifiedShape（不重置动画帧）
    /// </summary>
    public static void SetModifiedShape(GameItem item, int width, int height)
    {
        if (item == null || _modifiedShapeField == null) return;
        try
        {
            // 构造新 GridShape
            var shape = new GridShapeBuilder();
            shape.width = width;
            shape.height = height;
            // 直接写字段（不触发动画帧重置）
            _modifiedShapeField.SetValue(item, shape);
        }
        catch (System.Exception ex) { Core.LogMsg("[GameItemShapeHelper] 异常: " + ex.Message); }
    }
}