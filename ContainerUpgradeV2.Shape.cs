using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace JacksonPerks;
partial class ContainerUpgradeV2

{
    // ===== Shape =====
    public static void ConsumeOne(GameItem item)
    {
        try
        {
            if (item == null) return;
            int c = item.unitCount - 1;
            if (c <= 0) item.Destroy(); else item.SetUnitCount(c);
        }
        catch { try { item.Destroy(); } catch { } }
    }

    // ===================== 容器网格工具 =====================
    public static GameGridInventory GetContainerGrid(GameItem item)
    {
        try
        {
            var cw = item.contentWindow;
            if (cw != null && cw.childElement != null) { var v = cw.childElement.TryCast<GameGridInventory>(); if (v != null) return v; }
        }
        catch (System.Exception ex) { Core.LogMsg("[ContainerUpgradeV2] 异常: " + ex.Message); }
        return null;
    }
    public static void GetShapeWH(GameGridInventory inv, ref int w, ref int h)
    {
        try
        {
            if (inv == null || inv.inventoryShape == null) return;
            w = inv.inventoryShape.width;
            h = inv.inventoryShape.height;
        }
        catch (System.Exception ex) { Core.LogMsg("[ContainerUpgradeV2] 异常: " + ex.Message); }
    }
    // 全开放矩形形状（'0'=可放）；字符串重载自动 ValidateBackground（同虚空珠路径）
    private static void SetFullRect(GameGridInventory grid, int w, int h)
    {
        try { grid.SetShape(new string('0', w * h), w); } catch { try { grid.SetShape("", w); } catch { } }
        try { grid.Validate(); } catch { }
        // ★ 09-14 拆包实证：GameItem.shape(0x198)=物品场景占地，绝不能写（写 w×h 全开矩形 → 箱子占地变内部尺寸、
        //   挤压桌面）。存档 itemShape 存的就是占地（本就不该变）；内部容量=GameGridInventory.inventoryShape(0x1B0)，
        //   由 grid.SetShape 维护 + 读档恢复链（PostfixLoadGame/SetContentWindow → RestoreWageBoxShape/RestoreCrusoeShape）按段位重设。
    }
    public static bool ConsumedThisFrame(IntPtr ptr)
    {
        try
        {
            int frame = UnityEngine.Time.frameCount;
            int last = 0;
            if (_lastUpgradeFrame.TryGetValue(ptr, out last) && last == frame) return true;
            _lastUpgradeFrame[ptr] = frame;
            return false;
        }
        catch { return false; }
    }
}
