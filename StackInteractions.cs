using Il2Cpp;

namespace JacksonPerks;

/// <summary>
/// 堆叠系统交互：MayTarget 放行 + EndDrag 登记 + 每帧 Tick
/// </summary>
internal static class StackInteractions
{
    // GameItem.MayTarget Prefix：同类物品允许互相放置
    public static bool PrefixMayTarget(GameItem __instance, GameItem targetItem, ref bool __result)
    {
        try
        {
            Core.LogMsg("[堆叠] MayTarget: target=" + (__instance != null ? __instance.identifier : "null") + " drag=" + (targetItem != null ? targetItem.identifier : "null"));
            if (StackSystem.IsStackable(__instance, targetItem))
            {
                __result = true;
                return false;
            }
        } catch { }
        return true; // 放行其他
    }

    // ItemMouseDragHandler.EndDrag Postfix
    public static void PostfixEndDrag(ItemMouseDragHandler __instance)
    {
        try
        {
            if (__instance == null) return;
            GameItem dragItem = null;
            try { dragItem = __instance.lastItem; } catch { }
            if (dragItem == null) return;

            GameItem target = null;
            try { target = __instance.currentItem; } catch { }

            StackSystem.OnEndDrag(dragItem, target);
        } catch (System.Exception ex) { Core.LogMsg("[堆叠] EndDrag 异常: " + ex.Message); }
    }

    // InputActionManager.Update Postfix：每帧 Tick
    public static void PostfixFrameUpdate()
    {
        try { StackSystem.OnFrameTick(); } catch { }
    }

    // ItemMultiSelectHandler.EndGroupDrag Postfix：全选拖拽后修复堆叠
    public static void PostfixEndGroupDrag(ItemMultiSelectHandler __instance)
    {
        try
        {
            Core.LogMsg("[堆叠] EndGroupDrag 触发");
        } catch { }
    }

    // GameItem.OnRightClick Postfix：堆叠物品右键加拆分选项
    public static void PostfixOnRightClick(GameItem __instance)
    {
        try
        {
            if (__instance == null) return;
            int count = StackSystem.GetCount(__instance);
            if (count <= 1) return;
            Core.LogMsg("[堆叠] 右键: " + __instance.identifier + " ×" + count);
            // TODO: 加右键菜单（拆1个/拆一半）
        } catch { }
    }
}
