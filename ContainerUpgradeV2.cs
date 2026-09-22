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
//   - 蛙哥箱子：段0=3×3 → nuts_metal（螺丝）升级 5/10/20/40/80 → 满级 52×10 → 奖励第二个妙妙箱
//   - 鲁滨逊容器：段0=原宽50%（开局减半）→ junk 升级 5/10/20/40/80 → 满级 200% 原宽（×2）
// 数据 tag（物品 tag，ES3 随档天然持久化）：
//   wb_stage  int 0-5  段位（两容器共用语义）
//   wb_orig_w int      鲁滨逊：官方原宽（减半时记录）
//   满级奖励：蛙哥=第二个妙妙箱；鲁滨逊=容量×2（09-12 拍板，替代翻页）
// 老档迁移：鲁滨逊旧 wageUpgradeCap > 0 → 满级；蛙哥旧箱（shape>3×3）→ 满级
// ============================================================
public static class ContainerUpgradeV2
{
    public static int MAX_STAGE => BuildConfig.ContainerMaxStage;

    // 蛙哥箱子段位网格表（段 k → 宽×高）——09-14 CFG 化（BuildConfig.BoxWidths/BoxHeights）
    public static int[] WAGE_BOX_W => BuildConfig.BoxWidthsArr;
    public static int[] WAGE_BOX_H => BuildConfig.BoxHeightsArr;

    // 升级消耗（段 k → 段 k+1 需材料数）——09-14 CFG 化（BuildConfig.UpgradeCosts）
    public static int[] UPGRADE_COSTS => BuildConfig.UpgradeCostsArr;

    // ===================== tag 工具 =====================
    // 09-22 门面模式：内部调 TagHelper
    public static int GetTagIntSafe(GameItem item, string tag) => TagHelper.GetInt(item, tag);
    public static bool HasTag(GameItem item, string tag) => TagHelper.Has(item, tag);
    public static void SetTagIntValue(GameItem item, string tag, int value) => TagHelper.SetInt(item, tag, value);
    public static void AddTagInt(GameItem item, string tag, int delta) => TagHelper.AddInt(item, tag, delta);
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
        // ★ 09-14 拆包实证：GameItem.shape(0x198)=物品场景占地，绝不能写（写 w×h 全开矩形 → 箱子占地变内部尺寸、
        //   挤压桌面）。存档 itemShape 存的就是占地（本就不该变）；内部容量=GameGridInventory.inventoryShape(0x1B0)，
        //   由 grid.SetShape 维护 + 读档恢复链（PostfixLoadGame/SetContentWindow → RestoreWageBoxShape/RestoreCrusoeShape）按段位重设。
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

    // ===================== 可升级容器判定 =====================
    // 建筑容器白名单：storage_bay/machine_bay 等是 ItemCategory.Container 建筑模块，
    // 没有 CONTAINER_TAG（减半遍历靠 CONTAINER_TAG OR IsMachine 双通道，升级判定原本只有
    // CONTAINER_TAG → 拖 junk 到 storage_bay 完全不触发，用户实测"面板都没有"）。
    // 背包类（backpack_*）一律排除，不参与升级。
    private static readonly HashSet<string> BUILDING_CONTAINER_IDS = new HashSet<string>(new string[] {
        "storage_bay", "storage_bay_large", "machine_bay", "machine_bay_ext",
        "mini_smuggler_bay", "smuggler_bay", "smuggler_bay_mod", "smuggler_bay_mini",
        "chemist_storage_bay", "gunsmith_storage_bay", "makeshift_storage_bay",
        // 09-20 新增：物资箱子（蛙娘带回）
        "evidence_box", "med_box", "sec_box", "service_box", "eng_box",
        // 09-20 新增：电池充电器（走容器升级链）
        "recharger_base"
    });

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

    // ===================== 升级系统排除名单（09-13 用户拍板） =====================
    // 文档箱(dossier)/工具箱(toolbox) 已拖 junk 实证命中升级；收音机=cassette_player（磁带播放器，用户提供 id）。
    // 排除后：不参与拖 junk 升级、不开局减半、不读档恢复 shape——完全回原版。
    private static readonly HashSet<string> EXCLUDED_CONTAINER_IDS = new HashSet<string>(new string[] {
        "toolbox", "dossier", "cassette_player", "water_bottle_printer", "bottle_printer"
    });
    public static bool IsExcludedContainer(GameItem item)
    {
        try { if (item == null) return false; return EXCLUDED_CONTAINER_IDS.Contains((item.identifier ?? "").ToLowerInvariant()); } catch { return false; }
    }

    // ===================== 蛙哥箱识别（tag 或 identifier 兜底） =====================
    // 老档蛙哥箱（旧版创建）可能缺 CUSTOM_STORAGE_TAG（ES3 不保证 tag 保留），用 identifier 兜底
    public static bool IsWageBox(GameItem item)
    {
        try
        {
            if (item == null) return false;
            if (item.IsTag("CUSTOM_STORAGE_TAG")) return true;
            if (item.IsTag("WAGE_BOX_TAG")) return true; // 09-14 场景读档：CSTAG 丢但升级 tag 随档 → 专属标记识别
            if (item.IsTag("wage_box_type")) return true; // 09-14 带值 tag（用原生 IsTag——HasTag=GetTagReadonly!=null 对不存在 tag 恒 True，骰子/所有物品误判成蛙哥箱）
            return (item.identifier ?? "").ToLowerInvariant() == "custom_storage_box";
        }
        catch { return false; }
    }

    public static bool IsUpgradeableContainer(GameItem item)
    {
        try
        {
            if (item == null) return false;
            if (!BuildConfig.ContainerUpgradeEnabled) return false; // 09-20 CFG 关 → 不升级
            if (IsExcludedContainer(item)) return false; // 09-13：文档箱/工具箱/收音机不参与升级
            if (item.IsTag("VOID_BEAD_TAG") || IsWageBox(item)) return false; // 妙妙箱走独立螺丝升级链，不走鲁滨逊 junk 升级
            if (IsVoidBeadStorage(item)) return false;
            try { if (item.GetTagReadonly("CONTAINER_TAG") != null) return true; } catch { } // 09-23 修：IsTag 恒 True 坑 → GetTagReadonly != null
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

    // 09-20 优化：打烊批量消耗螺丝（SaveGame Postfix）
    public static void PrefixSaveGame(PlayerStore __instance) { try { ConsumeNutsAtClose(); } catch { } }

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
        catch { }
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
        catch { }
    }

    // 09-23 拖螺丝到妙妙箱松手 → 直接消耗 1 颗螺丝 +1 progress（和打烊消耗共存）
    private static bool ConsumeNutsDirectly(GameItem box, GameItem nuts)
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

    // 满级奖励第二个妙妙箱（09-12 用户拍板两个箱子方案：替代翻页，天然存档/读档/睡眠零冲突）
    private static readonly System.Collections.Generic.HashSet<IntPtr> _secondGiven = new System.Collections.Generic.HashSet<IntPtr>();
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
        catch { }
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
        catch { }
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
        catch { }
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

    // ===================== 蛙哥箱子升级链（独立挂载，不依赖鲁滨逊职业） =====================
    public static bool PrefixMayTarget_WageBox(GameItem __instance, GameItem targetItem, ref bool __result)
    {
        try
        {
            if (__instance == null || targetItem == null) return true;
            if (IsNuts(__instance) && IsWageBox(targetItem))
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
            if (!IsNuts(__instance) || !IsWageBox(targetItem)) return true;
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
            if (!IsNuts(item) || !IsWageBox(__instance)) return true;
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
            if (!IsWageBox(item)) return;
            int stage = GetTagIntSafe(item, "wb_stage");
            if (stage >= MAX_STAGE)
                builder.AddLine(LangHelper.T("◆ 妙妙箱：满级（" + WAGE_BOX_W[MAX_STAGE] + "×" + WAGE_BOX_H[MAX_STAGE] + "）· 奖励：第二个妙妙箱已入背包", "◆ Wage Box: MAX (" + WAGE_BOX_W[MAX_STAGE] + "×" + WAGE_BOX_H[MAX_STAGE] + ") · Reward: second box in backpack"),
                    true, (RenderHandler.ColorPalette)(-1), false, false, false, false, (RenderHandler.ColorPalette)(-1), (RenderHandler.ColorPalette)(-1), (RenderHandler.ColorPalette)(-1));
            else
                {
                    int progress = GetTagIntSafe(item, "wb_progress");
                    int need = UPGRADE_COSTS[Math.Min(stage, MAX_STAGE - 1)];
                    builder.AddLine(LangHelper.T("◆ 妙妙箱：段位 " + stage + "/5 · 升级进度 " + progress + "/" + need + "（拖螺丝到箱上直接升级，或放箱内过夜自动消耗）", "◆ Wage Box: Stage " + stage + "/5 · progress " + progress + "/" + need + " (drag nuts to box to upgrade, or leave inside overnight)"),
                        true, (RenderHandler.ColorPalette)(-1), false, false, false, false, (RenderHandler.ColorPalette)(-1), (RenderHandler.ColorPalette)(-1), (RenderHandler.ColorPalette)(-1));
                }
        }
        catch { }
    }
}
