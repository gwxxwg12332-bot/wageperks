using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace JacksonPerks;
partial class ContainerUpgradeV2

{
    // ===== Restore =====

    // 蛙哥箱子读档恢复：按 wb_stage 重设网格；老档（无 wb_stage 且 shape>3×3）→ 满级迁移
    public static void RestoreWageBoxShape(GameItem box)
    {
        try
        {
            if (box == null) return;
            var grid = GetContainerGrid(box);
            if (grid == null) return;
            int w = 0, h = 0;
            GetShapeWH(grid, ref w, ref h);
            if (w <= 0 || h <= 0) return;
            bool hasStage = box.IsTag("wb_stage"); // 09-14 弃用 HasTag（GetTagReadonly 对不存在 tag 返回非 null → 恒 True 误判）改用原生 IsTag
            int stage = GetTagIntSafe(box, "wb_stage");
            if (!hasStage)
            {
                // 按尺寸推断（老档迁移）：形状>初始（3×3）→ 满级（不缩水）；未升级 → 段 0
                if (w > WAGE_BOX_W[0] || h > WAGE_BOX_H[0]) { stage = MAX_STAGE; SetTagIntValue(box, "wb_stage", MAX_STAGE); }
                else { stage = 0; SetTagIntValue(box, "wb_stage", 0); }
            }
            if (stage < 0) stage = 0;
            if (stage > MAX_STAGE) stage = MAX_STAGE;
            if (stage >= MAX_STAGE && !box.IsTag("wb_second_given")) TryGiveSecondWageBox(box); // 老档满级箱读档补发第二个
            int targetW = WAGE_BOX_W[stage], targetH = WAGE_BOX_H[stage];
            if (w == targetW && h == targetH) return;
            SetFullRect(grid, targetW, targetH);
        }
        catch (System.Exception ex) { Core.LogMsg("[ContainerUpgradeV2] 异常: " + ex.Message); }
    }

    // ===================== 09-14 位置方案（hiddenElement 海报后边 2×2）=====================
    // 场景物品读档后 tag/identifier/uniqueId 全丢（HasTag 误判）→ 无法从物品识别
    // 用 childItems 索引 + PlayerPrefs 关联（场景存档按顺序恢复，索引稳定）
    public static int FindBoxInHidden(GameItem box)
    {
        try
        {
            var emporium = EmporiumEntry.Instance;
            if (emporium == null || box == null) return -1;
            var hid = emporium.hiddenElement as GameGridInventory;
            if (hid == null || hid.childItems == null) return -1;
            for (int i = 0; i < hid.childItems.Count; i++)
                if (hid.childItems[i] != null && hid.childItems[i].Pointer == box.Pointer) return i;
        }
        catch (System.Exception ex) { Core.LogMsg("[ContainerUpgradeV2] 异常: " + ex.Message); }
        return -1;
    }
    // 按索引从 PlayerPrefs 读段位（-1=无记录）
    public static int GetHiddenStageByIndex(int idx)
    {
        if (idx < 0) return -1;
        return WageSaveStore.GetInt("RobinCrusoe", "wage_stage_idx_" + idx, -1);
    }
    // 记录 hiddenElement 索引段位
    public static void SetHiddenStageByIndex(int idx, int stage)
    {
        if (idx < 0) return;
        WageSaveStore.SetInt("RobinCrusoe", "wage_stage_idx_" + idx, stage);
    }
    // 按指定段位强恢复（不依赖 tag——场景物品 tag 全丢）
    public static void RestoreWageBoxToStage(GameItem box, int stage)
    {
        try
        {
            if (box == null || stage <= 0) return;
            var grid = GetContainerGrid(box);
            if (grid == null) return;
            int w = 0, h = 0; GetShapeWH(grid, ref w, ref h);
            if (w <= 0 || h <= 0) return;
            if (stage > MAX_STAGE) stage = MAX_STAGE;
            int targetW = WAGE_BOX_W[stage], targetH = WAGE_BOX_H[stage];
            if (w == targetW && h == targetH) return;
            SetFullRect(grid, targetW, targetH);
            try { SetTagIntValue(box, "wb_stage", stage); } catch { }
        }
        catch (System.Exception ex) { Core.LogMsg("[ContainerUpgradeV2] 异常: " + ex.Message); }
    }

    // ===================== 鲁滨逊容器段位 =====================
    // 段位 k（0-5）：宽 = floor(原宽 × (50% + 30%k))，高度不变，最小 1
    // 满级 = 原宽 × 200%（比官方原尺寸大一倍，用户拍板 09-12）
    public static int GetCrusoeTargetWidth(int origW, int stage)
    {
        try
        {
            if (origW <= 0) return 1;
            int s = Math.Max(0, Math.Min(MAX_STAGE, stage));
            return Math.Max(1, (int)Math.Floor(origW * (0.5 + 0.3 * s)));
        }
        catch { return 1; }
    }

    // 鲁滨逊容器读档恢复：按 wb_stage + wb_orig_w 换算宽；老档 wageUpgradeCap>0 → 满级迁移
    // 返回是否执行了 SetShape（供防重集合使用）
    public static bool RestoreCrusoeShape(GameItem box)
    {
        try
        {
            if (box == null) return false;
            var grid = GetContainerGrid(box);
            if (grid == null) return false;
            int w = 0, h = 0;
            GetShapeWH(grid, ref w, ref h);
            if (w <= 0 || h <= 0) return false;
            bool hasStage = box.IsTag("wb_stage"); // 09-14 弃用 HasTag（恒 True 误判）改用原生 IsTag
            int stage = GetTagIntSafe(box, "wb_stage");
            int origW = GetTagIntSafe(box, "wb_orig_w");
            if (!hasStage)
            {
                // 老档迁移：旧 wageUpgradeCap > 0 → 满级（不缩水）；未升级 → 保持现状（不迁移）
                int cap = GetTagIntSafe(box, "wageUpgradeCap");
                if (cap <= 0) return false;
                stage = MAX_STAGE;
                SetTagIntValue(box, "wb_stage", MAX_STAGE);
                if (origW <= 0) { origW = w; SetTagIntValue(box, "wb_orig_w", w); }
            }
            int targetW;
            if (origW > 0)
                targetW = GetCrusoeTargetWidth(origW, stage); // 减半容器：恢复语义
            else
                targetW = w + stage; // 未减半容器（腰包类）：官方宽 + 每段 1 列（读档 ES3 恢复默认 shape=官方宽）
            if (w == targetW) return false;
            SetFullRect(grid, targetW, h);
            return true;
        }
        catch { return false; }
    }
}
