using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace JacksonPerks;
partial class ContainerUpgradeV2

{
    // ===== Upgrade =====

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
            // 09-23 拖螺丝到妙妙箱松手 → 直接消耗螺丝 +1 progress（和打烊消耗共存，不重复）
            return ConsumeNutsDirectly(box, nuts);
        }
        catch (Exception ex) { Core.LogMsg("[容器v2] 蛙哥箱子升级异常: " + ex.Message); return false; }
    }

    // 09-20 优化：打烊批量消耗螺丝（遍历妙妙箱内部库存，吃掉全部螺丝叠加升级进度）
    public static void ConsumeNutsAtClose()
    {
        try
        {
            var emporium = EmporiumEntry.Instance;
            Core.LogMsg("[妙妙箱] ConsumeNutsAtClose Prefix 跑了, emporium=" + (emporium != null));
            if (emporium == null) return;
            var allInvs = new System.Collections.Generic.List<GameInventory>();
            try { var v = emporium.backInvinvElement as GameInventory; if (v != null) allInvs.Add(v); } catch { }
            try { var v = emporium.backInvinvElementCounter as GameInventory; if (v != null) allInvs.Add(v); } catch { }
            try { var v = emporium.showcaseElement as GameInventory; if (v != null) allInvs.Add(v); } catch { }
            try { var v = emporium.invElement as GameInventory; if (v != null) allInvs.Add(v); } catch { }
            try { var v = emporium.frontInvinvElement as GameInventory; if (v != null) allInvs.Add(v); } catch { }
            try { var v = emporium.hiddenElement as GameInventory; if (v != null) allInvs.Add(v); } catch { }
            // 递归遍历所有背包 + 容器内部
            var visited = new System.Collections.Generic.HashSet<IntPtr>();
            var stack = new System.Collections.Generic.Stack<GameInventory>(allInvs);
            while (stack.Count > 0) {
                var inv = stack.Pop();
                if (inv == null || inv.childItems == null) continue;
                for (int i = 0; i < inv.childItems.Count; i++) {
                    var it = inv.childItems[i];
                    if (it == null) continue;
                    if (!visited.Contains(it.Pointer)) visited.Add(it.Pointer);
                    if (IsWageBox(it)) ConsumeNutsInBox(it);
                    // 递归进容器内部
                    var inner = GetContainerGrid(it);
                    if (inner != null && !visited.Contains(inner.Pointer)) { visited.Add(inner.Pointer); stack.Push(inner); }
                }
            }
        }
        catch (System.Exception ex) { Core.LogMsg("[ContainerUpgradeV2] 异常: " + ex.Message); }
    }

    // 妙妙箱内部库存螺丝批量消耗
    private static void ConsumeNutsInBox(GameItem box)
    {
        try
        {
            int stage = GetTagIntSafe(box, "wb_stage");
            if (stage >= MAX_STAGE) return;
            var grid = GetContainerGrid(box);
            if (grid == null || grid.childItems == null) return;
            var nutsList = new System.Collections.Generic.List<GameItem>();
            for (int i = 0; i < grid.childItems.Count; i++) {
                var it = grid.childItems[i];
                if (it == null) continue;
                if (IsNuts(it)) nutsList.Add(it);
            }
            if (nutsList.Count == 0) return;
            int progress = GetTagIntSafe(box, "wb_progress");
            int need = UPGRADE_COSTS[stage];
            int consumed = 0;
            for (int i = 0; i < nutsList.Count; i++) {
                try { ConsumeOne(nutsList[i]); consumed++; } catch { }
            }
            progress += consumed;
            while (progress >= need && stage < MAX_STAGE) {
                int targetW = WAGE_BOX_W[stage + 1], targetH = WAGE_BOX_H[stage + 1];
                SetFullRect(grid, targetW, targetH);
                AddTagInt(box, "wb_stage", 1);
                progress -= need;
                stage++;
                need = UPGRADE_COSTS[Math.Min(stage, MAX_STAGE - 1)];
                try { StoreUIManager.Instance.Notify(LangHelper.T("妙妙箱升级！段位 " + (stage) + "/5", "Wage Box upgraded! Stage " + stage + "/5"), "white"); } catch { }
            }
            SetTagIntValue(box, "wb_progress", progress);
            if (stage >= MAX_STAGE) TryGiveSecondWageBox(box);
        }
        catch (System.Exception ex) { Core.LogMsg("[ContainerUpgradeV2] 异常: " + ex.Message); }
    }

    // 09-23 拖螺丝到妙妙箱松手 → 直接消耗 1 颗螺丝 +1 progress（和打烊消耗共存）
    internal static bool ConsumeNutsDirectly(GameItem box, GameItem nuts)
    {
        try
        {
            int stage = GetTagIntSafe(box, "wb_stage");
            if (stage >= MAX_STAGE) return false; // 满级：螺丝正常放入
            var grid = GetContainerGrid(box);
            if (grid == null) return false;
            ConsumeOne(nuts); // 消耗螺丝
            int progress = GetTagIntSafe(box, "wb_progress") + 1;
            int need = UPGRADE_COSTS[stage];
            if (progress < need) {
                SetTagIntValue(box, "wb_progress", progress);
                try { StoreUIManager.Instance.Notify(LangHelper.T("妙妙箱升级进度 " + progress + "/" + need, "Wage Box progress " + progress + "/" + need), "white"); } catch { }
                return true; // 已消耗，拦截放入
            }
            // 满了升段
            int targetW = WAGE_BOX_W[stage + 1], targetH = WAGE_BOX_H[stage + 1];
            SetFullRect(grid, targetW, targetH);
            AddTagInt(box, "wb_stage", 1);
            SetTagIntValue(box, "wb_progress", progress - need);
            try { StoreUIManager.Instance.Notify(LangHelper.T("妙妙箱升级！段位 " + (stage + 1) + "/5", "Wage Box upgraded! Stage " + (stage + 1) + "/5"), "white"); } catch { }
            if (stage + 1 >= MAX_STAGE) TryGiveSecondWageBox(box);
            return true; // 已消耗，拦截放入
        }
        catch { return false; }
    }
    public static void TryGiveSecondWageBox(GameItem box)
    {
        try
        {
            if (box == null || _secondGiven.Contains(box.Pointer)) return;
            if (box.IsTag("wb_second_given")) return; // 已发过
            var emporium = EmporiumEntry.Instance;
            if (emporium == null || emporium.backInvinvElement == null) return; // 未就绪：下次升级动作再试（升级可重复触发）
            var second = CustomStorageContainer.CreateContainer();
            if (second == null) { Core.LogMsg("[容器v2] 创建第二个妙妙箱失败"); return; }
            second.DisableTag("TAG_NOT_PURCHASED", true);
            second.DisableTag("not_purchased", true);
            emporium.backInvinvElement.TryFindOneValidInventorySlot(second, false);
            if (((GameInventory)emporium.backInvinvElement).UncheckedAccept(second))
            {
                emporium.TransferOwnershipBackInv();
                emporium.TransferOwnedItemBackToInv();
                box.EnableTag("wb_second_given", true);
                _secondGiven.Add(box.Pointer);
                try { StoreUIManager.Instance.Notify(LangHelper.T("满级！第二个妙妙箱已放入背包", "Maxed! Second Wage Box added to inventory"), "white"); } catch { }
            }
        }
        catch (Exception ex) { Core.LogMsg("[容器v2] 发第二个妙妙箱异常: " + ex.Message); }
    }
}
