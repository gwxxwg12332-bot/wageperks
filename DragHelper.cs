using Il2Cpp;
using UnityEngine;

namespace JacksonPerks;

// 拖拽操作（判定 / 拖拽链模板）
internal static class DragHelper
{
    public static bool IsDragRelease()
    {
        try
        {
            var dragHandler = ItemMouseDragHandler.current;
            if (dragHandler == null || !dragHandler.IsDraggingItem) return false;
            if (Input.GetMouseButton(0)) return false;
            return true;
        }
        catch { return false; }
    }

    public static bool ShouldAllowDrag(GameItem dragged, GameItem target,
        System.Func<GameItem, bool> isDraggedType,
        System.Func<GameItem, bool> isTargetType)
    {
        try
        {
            if (dragged == null || target == null) return true;
            if (isDraggedType(dragged) && isTargetType(target)) return false;
        }
        catch { }
        return true;
    }
}