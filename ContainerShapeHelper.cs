using System;
using Il2Cpp;

namespace JacksonPerks;

// ============================================================
// 容器形状统一管理（升级/恢复同一张表）
// ============================================================
internal static class ContainerShapeHelper
{
    /// <summary>
    /// 获取容器升级后目标宽度（统一表）
    /// </summary>
    /// <param name="origW">官方原宽（未减半）</param>
    /// <param name="stage">目标段位</param>
    public static int GetTargetWidth(int origW, int stage)
    {
        if (origW <= 0) return 0;
        // 减半容器：origW * (0.5 + 0.3 * stage)
        return (int)(origW * (0.5 + 0.3 * stage));
    }

    /// <summary>
    /// 恢复容器到指定段位（统一入口）
    /// </summary>
    public static void RestoreToStage(GameItem box, int stage)
    {
        try
        {
            if (box == null) return;
            var grid = GetContainerGrid(box);
            if (grid == null) return;
            int origW = GetOrigWidth(box);
            int targetW = GetTargetWidth(origW, stage);
            int targetH = grid.inventoryShape.height;
            // 已对，不重设
            if (grid.inventoryShape.width == targetW) return;
            SetFullRect(grid, targetW, targetH);
        }
        catch { }
    }

    private static GameGridInventory GetContainerGrid(GameItem box)
    {
        try
        {
            var cw = box.contentWindow;
            if (cw == null || cw.childElement == null) return null;
            return cw.childElement.Cast<GameGridInventory>();
        }
        catch { return null; }
    }

    private static int GetOrigWidth(GameItem box)
    {
        try { return ContainerUpgradeV2.GetTagIntSafe(box, "wb_orig_w"); }
        catch { return 0; }
    }

    private static void SetFullRect(GameGridInventory grid, int w, int h)
    {
        try { grid.SetShape(new string('0', w * h), w); }
        catch { }
    }
}
