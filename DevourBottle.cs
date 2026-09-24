using Il2Cpp;

namespace JacksonPerks;

/// <summary>
/// 吞噬瓶：拖水瓶上去时，水合并进吞噬瓶
/// </summary>
internal static class DevourBottle
{
    public const string BOTTLE_ID = "water_jug";

    private const string MODE_TAG = "devour_mode"; // true=吞噬模式, false=普通模式

    public static bool IsDevourMode(GameItem bottle)
    {
        try { return bottle.IsTag(MODE_TAG); } catch { return true; } // 默认吞噬模式
    }

    public static void ToggleMode(GameItem bottle)
    {
        try
        {
            bool now = !IsDevourMode(bottle);
            bottle.EnableTag(MODE_TAG, now);
            Core.LogMsg("[吞噬瓶] 切换模式: " + (now ? "吞噬模式" : "普通模式"));
            try { StoreUIManager.Instance.Notify(now ? "吞噬模式：拖水瓶上去合并水+容量" : "普通模式：水瓶正常放置", "white"); } catch { }
        } catch { }
    }

    // GameItem.MayTarget Prefix：水瓶可以放到吞噬瓶上
    public static bool PrefixMayTarget(GameItem __instance, GameItem targetItem, ref bool __result)
    {
        try
        {
            // __instance = 目标格子里的物品（吞噬瓶）
            // targetItem = 正在拖的物品（水瓶）
            if (__instance == null || targetItem == null) return true;
            if (__instance.identifier != BOTTLE_ID) return true;
            if (!IsDevourMode(__instance)) return true; // 普通模式：不拦截
            // 拖的是水瓶
            if (targetItem.IsTag("LIQUID_CONTAINER_TAG"))
            {
                __result = true;
                return false;
            }
        } catch { }
        return true;
    }

    // GameItem.DoubleClickAction Postfix：双击吞噬瓶切换模式
    public static void PostfixDoubleClick(GameItem __instance)
    {
        try
        {
            if (__instance == null) return;
            if (__instance.identifier != BOTTLE_ID) return;
            ToggleMode(__instance);
        } catch { }
    }

    // ItemMouseDragHandler.EndDrag Postfix：拖完后合并水
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
            if (target == null) return;

            Core.LogMsg("[吞噬瓶] EndDrag: target=[" + target.identifier + "] BOTTLE_ID=[" + BOTTLE_ID + "] equal=" + (target.identifier == BOTTLE_ID) + " drag=" + dragItem.identifier);
            if (target.identifier == BOTTLE_ID)
            {
                Core.LogMsg("[吞噬瓶] 目标是吞噬瓶, devourMode=" + IsDevourMode(target) + " dragIsLiquid=" + dragItem.IsTag("LIQUID_CONTAINER_TAG"));
                if (IsDevourMode(target) && dragItem.IsTag("LIQUID_CONTAINER_TAG"))
                {
                    // 合并水：把 dragItem 的水加到 target 里
                    int ml = RobinCrusoePerk.GetWaterMl(dragItem);
                    // 读水瓶容量
                    int dragCap = 0;
                    try { dragCap = WaterHelper.GetCurrentCapacityML(dragItem); } catch { }
                    Core.LogMsg("[吞噬瓶] dragCap=" + dragCap + "ml ml=" + ml + "ml");
                    // 转移液体
                    try { WaterHelper.TransferLiquid(dragItem, target); } catch (System.Exception ex) { Core.LogMsg("[吞噬瓶] TransferLiquid异常: " + ex.Message); }
                    // 容量继承：吞噬瓶容量 += 水瓶容量
                    if (dragCap > 0)
                    {
                        try
                        {
                            int oldCap = WaterHelper.GetCurrentCapacityML(target);
                            WaterHelper.InitLiquidContainerItem(target, oldCap + dragCap, 100, false, false, false, true);
                            Core.LogMsg("[吞噬瓶] 容量继承: +" + dragCap + "ml = " + (oldCap + dragCap) + "ml");
                        } catch (System.Exception ex) { Core.LogMsg("[吞噬瓶] 容量继承失败: " + ex.Message); }
                    }
                    // dragItem 销毁
                    try { dragItem.Destroy(); } catch { }
                    Core.LogMsg("[吞噬瓶] 合并水: +" + ml + "ml");
                }
            }
        } catch (System.Exception ex) { Core.LogMsg("[吞噬瓶] EndDrag 异常: " + ex.Message); }
    }
}
