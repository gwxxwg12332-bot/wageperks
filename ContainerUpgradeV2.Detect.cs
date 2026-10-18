using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace WagePerks;
partial class ContainerUpgradeV2

{
    // ===== Detect =====

    public static bool IsBuildingContainerId(string id)
    {
        try
        {
            if (string.IsNullOrEmpty(id)) return false;
            string low = id.ToLowerInvariant();
            return BUILDING_CONTAINER_IDS.Contains(low);
        }
        catch { return false; }
    }
    public static bool IsExcludedContainer(GameItem item)
    {
        try { if (item == null) return false; return EXCLUDED_CONTAINER_IDS.Contains((item.identifier ?? "").ToLowerInvariant()); } catch { return false; }
    }

    // ===================== 蛙哥箱识别（tag 或 identifier 兜底） =====================
    // 老档蛙哥箱（旧版创建）可能缺 CUSTOM_STORAGE_TAG（ES3 不保证 tag 保留），用 identifier 兜底
    // 10-08 拆包 AI A3 误命中实锤：黑金典藏箱（wage_blackgold_box 独立 mod）打 CUSTOM_STORAGE_TAG 防"拖 junk 宽+1"劫持，
    //   但被本判定识别为妙妙箱 → 妙妙箱恢复链把黑金内部网格 20×20 改 52×10（容量破坏）+ 可能补发第二个妙妙箱。
    //   修复：黑金 identifier 精确排除（黑金保留 tag 防劫持 + 恢复链不再误命中；妙妙箱/原版箱不受影响）。
    // 10-13 Step4④：读档后场景物品 tag/identifier 全丢（拆包实锤）→ identifier 排除失效场景，WBG_BOX_TAG 独立随档身份兜底
    public static bool IsWageBox(GameItem item)
    {
        try
        {
            if (item == null) return false;
            if ((item.identifier ?? "").ToLowerInvariant() == "wage_blackgold_box") return false; // 10-08 黑金排除
            if (item.IsTag("WBG_BOX_TAG")) return false; // 10-13 黑金独立随档身份（identifier 丢场景兜底）
            if (item.IsTag("CUSTOM_STORAGE_TAG")) return true;
            // 10-18 审阅 P3 落地：删除 WAGE_BOX_TAG 死分支——全四仓零 EnableTag 写入点（仅本行读取）=恒 false；
            //   且 Restore.cs:149 实锤"场景物品读档后 tag 全丢"→ 补写也无效（读档后仍丢）。CSTAG/wage_box_type 已覆盖识别。
            if (item.IsTag("wage_box_type")) return true; // 09-14 带值 tag（用原生 IsTag——HasTag=GetTagReadonly!=null 对不存在 tag 恒 True，骰子/所有物品误判成蛙哥箱）
            return (item.identifier ?? "").ToLowerInvariant() == "custom_storage_box";
        }
        catch { return false; }
    }

    // 10-13 Step4①④：黑金独立判定（WBG_BOX_TAG 优先，identifier 兜底）——妙妙箱恢复链前置路径（GetHiddenStageByIndex/GetStagePersist
    //   先于 IsWageBox 执行）用它排除黑金，不依赖 CUSTOM_STORAGE_TAG（黑金也打该 tag，会误命中）
    public static bool IsBlackGoldBox(GameItem item)
    {
        try
        {
            if (item == null) return false;
            if (item.IsTag("WBG_BOX_TAG")) return true;
            return (item.identifier ?? "").ToLowerInvariant() == "wage_blackgold_box";
        }
        catch { return false; }
    }

    public static bool IsUpgradeableContainer(GameItem item)
    {
        try
        {
            if (item == null) return false;
            if ((item.identifier ?? "").ToLowerInvariant() == "wage_blackgold_box") return false; // 10-08 黑金排除（固定 20×20，禁止 junk 宽+1 劫持）
            if (item.IsTag("WBG_BOX_TAG")) return false; // 10-13 黑金独立随档身份（identifier 丢场景兜底）
            if (!BuildConfig.ContainerUpgradeEnabled) return false; // 09-20 CFG 关 → 不升级
            if (IsExcludedContainer(item)) return false; // 09-13：文档箱/工具箱/收音机不参与升级
            if (item.IsTag("VOID_BEAD_TAG") || IsWageBox(item)) return false; // 妙妙箱走独立螺丝升级链，不走鲁滨逊 junk 升级
            if (IsVoidBeadStorage(item)) return false;
            try { if (item.IsTag("CONTAINER_TAG")) return true; } catch { } // 09-23 修：IsTag 恒 True 坑 → GetTagReadonly != null
            return IsBuildingContainerId(item.identifier ?? "");
        }
        catch { return false; }
    }

    // ===================== 虚空珠储物袋排除 =====================
    // 虚空珠储物袋（void_bead_storage）EnableTag 的是 backpack/BACKPACK_TAG/CONTAINER_TAG，
    // VOID_BEAD_TAG 在每颗珠子上、储物袋没有 → 鲁滨逊容器系统会误劫持（存档实锤 wb_stage=1/progress=5 写在储物袋上，
    // SetShape 又被虚空珠 600 帧恢复轮询覆盖 → 升级"无变化"）。按 identifier 精确排除。
    public static bool IsVoidBeadStorage(GameItem item)
    {
        try
        {
            if (item == null) return false;
            string id = (item.identifier ?? "").ToLowerInvariant();
            if (id == "void_bead_storage") return true;
            // 注意：不能用 IsTag("BACKPACK_TAG") 排除——"BACKPACK_TAG" 是原版背包 tag，
            // 普通腰包/背包也有（存档实锤 wb_stage=1 写在普通腰包上，用户要升级腰包）。
            // 虚空珠储物袋 EnableTag(BACKPACK_TAG 常量="VOID_BEAD_TAG")，已被 VOID_BEAD_TAG 判定覆盖。
            return false;
        }
        catch { return false; }
    }

    // ===================== 物品判定 =====================
    public static bool IsNuts(GameItem item)
    {
        try { return item != null && (item.identifier ?? "").ToLowerInvariant() == "nuts_metal"; } catch { return false; }
    }
    public static bool IsDragRelease()
    {
        try
        {
            var dragHandler = Il2Cpp.ItemMouseDragHandler.current;
            if (dragHandler == null || !dragHandler.IsDraggingItem) return false;
            if (UnityEngine.Input.GetMouseButton(0)) return false; // 按住=拖动中；松手才触发
            return true;
        }
        catch { return false; }
    }
}
