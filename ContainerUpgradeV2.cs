using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace JacksonPerks;

// ============================================================
// 【容器统一升级系统 v2】（用户拍板 09-12 最终稿）
// 蛙哥箱子（custom_storage_box）与鲁滨逊容器共用：
//   - 段位制（0-5，有上限）＋ 读档按段位重设（固定小整数，根治无限宽读档 bug）
//   - 蛙哥箱子：段0=3×3 → nuts_metal（螺丝）升级 5/10/20/40/80 → 满级 52×10
//   - 鲁滨逊容器：段0=原宽50%（开局减半）→ junk 升级 5/10/20/40/80 → 满级 100% 原宽
// 数据 tag（物品 tag，ES3 随档天然持久化）：
//   wb_stage  int 0-5  段位（两容器共用语义）
//   wb_orig_w int      鲁滨逊：官方原宽（减半时记录）
//   wb_page   int 0-1  翻页（预留；满级翻页为 Phase 2）
// 老档迁移：鲁滨逊旧 wageUpgradeCap > 0 → 满级；蛙哥旧箱（shape>3×3）→ 满级
// ============================================================
public static class ContainerUpgradeV2
{
    public const int MAX_STAGE = 5;

    // 蛙哥箱子段位网格表（段 k → 宽×高）
    public static readonly int[] WAGE_BOX_W = { 3, 10, 20, 32, 42, 52 };
    public static readonly int[] WAGE_BOX_H = { 3, 10, 10, 10, 10, 10 };

    // 升级消耗（段 k → 段 k+1 需材料数）
    public static readonly int[] UPGRADE_COSTS = { 5, 10, 20, 40, 80 };

    // ===================== tag 工具 =====================
    public static int GetTagIntSafe(GameItem item, string tag)
    {
        try { var t = item.GetTagReadonly(tag); if (t != null) return t.valueInt; } catch { }
        return 0;
    }
    public static bool HasTag(GameItem item, string tag)
    {
        try { return item != null && item.GetTagReadonly(tag) != null; } catch { return false; }
    }
    public static void SetTagIntValue(GameItem item, string tag, int value)
    {
        try
        {
            if (!item.IsTag(tag)) item.EnableTag(tag, true);
            System.Action<TagState> sysAct = delegate (TagState state) { state.SetInt(value); };
            var il2cppAct = DelegateSupport.ConvertDelegate<Il2CppSystem.Action<TagState>>((System.Delegate)sysAct);
            item.ModifyTag(tag, il2cppAct, false);
        }
        catch { }
    }
    public static void AddTagInt(GameItem item, string tag, int delta)
    {
        try { SetTagIntValue(item, tag, GetTagIntSafe(item, tag) + delta); } catch { }
    }
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
            if (cw != null && cw.childElement != null) { var v = cw.childElement.Cast<GameGridInventory>(); if (v != null) return v; }
        }
        catch { }
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
        catch { }
    }
    // 全开放矩形形状（'0'=可放）；字符串重载自动 ValidateBackground（同虚空珠路径）
    private static void SetFullRect(GameGridInventory grid, int w, int h)
    {
        try { grid.SetShape(new string('0', w * h), w); } catch { try { grid.SetShape("", w); } catch { } }
        try { grid.Validate(); } catch { }
    }

    // ===================== 同帧防重 =====================
    // 松手时 MayHaveValidInventorySlot 与 Target 两个挂点都会触发升级链（旧版"一次扣N个"被数量不足挡掉，
    // 逐颗版暴露双计数）。同一容器同一帧只处理一次，第二次调用视为"已消耗"直接拦截。
    private static readonly Dictionary<IntPtr, int> _lastUpgradeFrame = new Dictionary<IntPtr, int>();
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

    // ===================== 蛙哥箱子升级 =====================
    // 拖 nuts_metal 到蛙哥箱子 → 段位+1（消耗 5/10/20/40/80）
    public static bool TryUpgradeWageBox(GameItem nuts, GameItem box)
    {
        try
        {
            if (nuts == null || box == null) return false;
            if (ConsumedThisFrame(box.Pointer)) return true; // 同帧已消耗：防双计数
            int stage = GetTagIntSafe(box, "wb_stage");
            if (stage >= MAX_STAGE) return false; // 满级：nuts 正常放入（不再消耗）
            ConsumeOne(nuts); // 逐颗消耗：每拖 1 颗立即扣 1
            int progress = GetTagIntSafe(box, "wb_progress") + 1;
            int need = UPGRADE_COSTS[stage];
            if (progress < need)
            {
                SetTagIntValue(box, "wb_progress", progress);
                try { StoreUIManager.Instance.Notify(LangHelper.T("妙妙箱 升级进度 " + progress + "/" + need, "Wage Box progress " + progress + "/" + need), "white"); } catch { }
                return true; // 已消耗，拦截放入
            }
            var grid = GetContainerGrid(box);
            if (grid == null) { Core.LogMsg("[容器v2] 蛙哥箱子升段失败：取不到内部库存"); return true; }
            int targetW = WAGE_BOX_W[stage + 1], targetH = WAGE_BOX_H[stage + 1];
            SetFullRect(grid, targetW, targetH);
            AddTagInt(box, "wb_stage", 1);
            SetTagIntValue(box, "wb_progress", 0); // 达标升段，进度清零重计
            try { StoreUIManager.Instance.Notify(LangHelper.T("妙妙箱升级！段位 " + (stage + 1) + "/5（" + targetW + "×" + targetH + "）", "Wage Box upgraded! Stage " + (stage + 1) + "/5 (" + targetW + "×" + targetH + ")"), "white"); } catch { }
            return true;
        }
        catch (Exception ex) { Core.LogMsg("[容器v2] 蛙哥箱子升级异常: " + ex.Message); return false; }
    }

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
            bool hasStage = HasTag(box, "wb_stage");
            int stage = GetTagIntSafe(box, "wb_stage");
            if (!hasStage)
            {
                // 老档迁移：v1.1.5 老箱（52×10）→ 满级；否则新档段0兜底
                if (w > WAGE_BOX_W[0] || h > WAGE_BOX_H[0]) { stage = MAX_STAGE; SetTagIntValue(box, "wb_stage", MAX_STAGE); }
                else { stage = 0; SetTagIntValue(box, "wb_stage", 0); }
            }
            if (stage < 0) stage = 0;
            if (stage > MAX_STAGE) stage = MAX_STAGE;
            int targetW = WAGE_BOX_W[stage], targetH = WAGE_BOX_H[stage];
            if (w == targetW && h == targetH) return;
            SetFullRect(grid, targetW, targetH);
        }
        catch { }
    }

    // ===================== 鲁滨逊容器段位 =====================
    // 段位 k（0-5）：宽 = floor(原宽 × (50% + 10%k))，高度不变，最小 1
    public static int GetCrusoeTargetWidth(int origW, int stage)
    {
        try
        {
            if (origW <= 0) return 1;
            int s = Math.Max(0, Math.Min(MAX_STAGE, stage));
            return Math.Max(1, (int)Math.Floor(origW * (0.5 + 0.1 * s)));
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
            bool hasStage = HasTag(box, "wb_stage");
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
            if (origW <= 0) origW = w * 2; // 兜底：段0=半宽 → 原宽≈2×当前宽
            int targetW = GetCrusoeTargetWidth(origW, stage);
            if (w == targetW) return false;
            SetFullRect(grid, targetW, h);
            return true;
        }
        catch { return false; }
    }

    // ===================== 蛙哥箱子升级链（独立挂载，不依赖鲁滨逊职业） =====================
    public static bool PrefixMayTarget_WageBox(GameItem __instance, GameItem targetItem, ref bool __result)
    {
        try
        {
            if (__instance == null || targetItem == null) return true;
            if (IsNuts(__instance) && targetItem.IsTag("CUSTOM_STORAGE_TAG"))
            { __result = true; return false; } // hover 可拖
        }
        catch { }
        return true;
    }
    public static bool PrefixCanTarget_WageBox(GameItem __instance, GameItem targetItem, ref bool __result)
    {
        return PrefixMayTarget_WageBox(__instance, targetItem, ref __result);
    }
    public static bool PrefixTarget_WageBox(GameItem __instance, GameItem targetItem)
    {
        try
        {
            if (__instance == null || targetItem == null) return true;
            if (!IsNuts(__instance) || !targetItem.IsTag("CUSTOM_STORAGE_TAG")) return true;
            if (!IsDragRelease()) return true;
            if (TryUpgradeWageBox(__instance, targetItem)) return false; // 升级成功：拦截原生放入
        }
        catch { }
        return true;
    }
    public static bool PrefixMayHaveValidInventorySlot_WageBox(GameItem __instance, GameItem item, ref bool __result)
    {
        try
        {
            if (__instance == null || item == null) return true;
            if (!IsNuts(item) || !__instance.IsTag("CUSTOM_STORAGE_TAG")) return true;
            if (!IsDragRelease()) return true;
            if (TryUpgradeWageBox(item, __instance)) { __result = false; return false; }
        }
        catch { }
        return true;
    }

    // ===================== 蛙哥箱子 tooltip（独立挂载，非鲁滨逊场景也显示） =====================
    public static void PostfixWageBoxTooltip(RichTextBuilder builder, GameItem item)
    {
        try
        {
            if (builder == null || item == null) return;
            if (!item.IsTag("CUSTOM_STORAGE_TAG")) return;
            int stage = GetTagIntSafe(item, "wb_stage");
            if (stage >= MAX_STAGE)
                builder.AddLine(LangHelper.T("◆ 妙妙箱：满级（" + WAGE_BOX_W[MAX_STAGE] + "×" + WAGE_BOX_H[MAX_STAGE] + "）", "◆ Wage Box: MAX (" + WAGE_BOX_W[MAX_STAGE] + "×" + WAGE_BOX_H[MAX_STAGE] + ")"),
                    true, (RenderHandler.ColorPalette)(-1), false, false, false, false, (RenderHandler.ColorPalette)(-1), (RenderHandler.ColorPalette)(-1), (RenderHandler.ColorPalette)(-1));
            else
                {
                    int progress = GetTagIntSafe(item, "wb_progress");
                    int need = UPGRADE_COSTS[Math.Min(stage, MAX_STAGE - 1)];
                    builder.AddLine(LangHelper.T("◆ 妙妙箱：段位 " + stage + "/5 · 升级进度 " + progress + "/" + need, "◆ Wage Box: Stage " + stage + "/5 · progress " + progress + "/" + need),
                        true, (RenderHandler.ColorPalette)(-1), false, false, false, false, (RenderHandler.ColorPalette)(-1), (RenderHandler.ColorPalette)(-1), (RenderHandler.ColorPalette)(-1));
                }
        }
        catch { }
    }
}
