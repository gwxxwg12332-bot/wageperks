using System;
using HarmonyLib;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Il2Cpp;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace JacksonPerks;
internal static partial class RobinCrusoePerk
{

    // ===== 金属锭/垃圾升级系统（用户拍板 09-09：拖 metal_ingot 到机器/模板 = 性能/效率/质量三维各 +1% 无限叠加；
    // 拖 junk 到容器 = 容量 +1 列宽，无限叠加。拖放拦截仿虚空珠模式）=====
    // 机器/模板升级 = 目标 TOTAL_PERCENTAGE_PERFORMANCE/QUALITY_BONUS_INT 各 +2 → Getter ×0.5 后实 +1（升级不受减半）；
    // 效率 = wageUpgradeEff +1 → ApplyBasicModuleEffect 速度直接 +1。容器 = wageUpgradeCap +1 → SetShape 宽+1（读档恢复照虚空珠）。
    public static bool PrefixMayTarget(GameItem __instance, GameItem targetItem, ref bool __result)
    {
        try
        {
            bool robC = IsActive();
            // 09-15 水商之友：非鲁滨逊档但水商之友激活 + 目标是瓶印机 → 允许金属锭升级
            if ((!robC && !(WaterMerchantPerk.IsActive() && IsBottlePrinter(targetItem))) || __instance == null || targetItem == null) return true;
            // 拖动中 MayTarget 会被反复调用：匹配即放行（hover 可拖），升级/消耗留给松手时的 Target/MayHaveValidInventorySlot
            if ((IsMetalIngot(__instance) && !IsMoreUpdateOwnedMachine(targetItem) && (IsMachine(targetItem) || targetItem.IsTag("MODULE_TAG")))
                || (IsJunk(__instance) && ContainerUpgradeV2.IsUpgradeableContainer(targetItem)))
            { __result = true; return false; }
        }
        catch (System.Exception ex) { Core.LogMsg("[RobinCrusoePerk.ContainerUpgrade] 异常: " + ex.Message); }
        return true;
    }

    public static bool PrefixCanTarget(GameItem __instance, GameItem targetItem, ref bool __result)
    {
        return PrefixMayTarget(__instance, targetItem, ref __result);
    }

    public static bool PrefixTarget(GameItem __instance, GameItem targetItem)
    {
        try
        {
            bool robC = IsActive();
            // 09-15 水商之友：非鲁滨逊档但水商之友激活 + 目标是瓶印机 → 允许金属锭升级
            if ((!robC && !(WaterMerchantPerk.IsActive() && IsBottlePrinter(targetItem))) || __instance == null || targetItem == null) return true;
            if (!IsDragRelease()) return true;
            if (IsMetalIngot(__instance) && !IsMoreUpdateOwnedMachine(targetItem) && (IsMachine(targetItem) || targetItem.IsTag("MODULE_TAG")))
            { if (TryUpgradeMachine(__instance, targetItem)) return false; }
            else if (IsJunk(__instance) && ContainerUpgradeV2.IsUpgradeableContainer(targetItem))
            {
                    if (TryUpgradeContainer(__instance, targetItem)) return false;
            }
        }
        catch (System.Exception ex) { Core.LogMsg("[RobinCrusoePerk.ContainerUpgrade] 异常: " + ex.Message); }
        return true;
    }

    // 容器升级：拖 junk 到容器物品（CONTAINER_TAG）→ 不放进去，升级容量 +1 列
    public static bool PrefixMayHaveValidInventorySlot(GameItem __instance, GameItem item, ref bool __result)
    {
        try
        {
            if (!IsActive() || __instance == null || item == null) return true;
            if (!IsJunk(item)) return true;
            if (!ContainerUpgradeV2.IsUpgradeableContainer(__instance)) return true;
            if (!IsDragRelease()) return true;
            if (TryUpgradeContainer(item, __instance)) { __result = false; return false; }
        }
        catch (System.Exception ex) { Core.LogMsg("[RobinCrusoePerk.ContainerUpgrade] 异常: " + ex.Message); }
        return true;
    }

    // ===== MoreUpdate（MoreDeviceUpgrades v0.2.0）兼容让路（09-14）=====
    // 动态探测 MoreUpdate 已加载时，我方金属锭升级对它的 4 台配方机器让路（放行给 MoreUpdate 独占），
    // 消除"同一拖放双响应、metal_ingot 双消耗"。未装 MoreUpdate 时我方行为完全不变。
    private static bool? _moreUpdateLoaded;
    private static bool IsMoreUpdateLoaded()
    {
        try
        {
            if (_moreUpdateLoaded == null)
            {
                bool found = false;
                var asms = System.AppDomain.CurrentDomain.GetAssemblies();
                if (asms != null)
                    foreach (var a in asms)
                    {
                        if (a == null) continue;
                        string n = "";
                        try { n = a.GetName().Name ?? ""; } catch { }
                        if (n == "MoreDeviceUpgrades") { found = true; break; }
                    }
                _moreUpdateLoaded = found;
            }
            return _moreUpdateLoaded.Value;
        }
        catch { return false; }
    }

    // MoreUpdate 配方机器 ∩ 我方机器全集（拆包实锤：furnace/water_purifier/wine_rack/mirage_projector 有 metal_ingot 配方）
    private static readonly HashSet<string> MOREUPDATE_METAL_INGOT_MACHINES = new HashSet<string>(
        new[] { "furnace", "water_purifier", "wine_rack", "mirage_projector" });
    private static bool IsMoreUpdateOwnedMachine(GameItem target)
    {
        try
        {
            if (!IsMoreUpdateLoaded() || target == null) return false;
            string id = (target.identifier ?? "").ToLowerInvariant();
            if (!MOREUPDATE_METAL_INGOT_MACHINES.Contains(id)) return false;
            // 接力（09-14 拍板）：MoreUpdate 升满（cap reached）后我方接管继续升级——升满判定按拆包实锤
            if (id == "furnace" && GetTagIntSafe(target, "MOD_FURNACE_INGOT_UPGRADES") >= 10) return false;
            if (id == "water_purifier" && GetTagIntSafe(target, "MOD_PURIFIER_INGOT_UPGRADES") >= 10) return false;
            if (id == "wine_rack" && GetTagIntSafe(target, "MOD_WINERACK_WINE_WIDTH") >= 6) return false;
            // mirage_projector 永不升满（int.MaxValue）→ 始终归 MoreUpdate
            return true;
        }
        catch { return false; }
    }

    private static bool IsMetalIngot(GameItem item)
    {
        try { return item != null && (item.identifier ?? "").ToLowerInvariant() == "metal_ingot"; } catch { return false; }
    }

    private static bool IsJunk(GameItem item)
    {
        try { return item != null && (item.identifier ?? "").ToLowerInvariant() == "junk"; } catch { return false; }
    }

    // 水瓶打印机（水商之友专属升级目标——09-15 用户拍板：水商之友可升级瓶印机质量）
    private static bool IsBottlePrinter(GameItem item)
    {
        try { return item != null && (item.identifier ?? "").ToLowerInvariant() == "bottle_printer"; } catch { return false; }
    }

    private static bool IsDragRelease()
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

    // 机器白名单全集（拆包 2.5.30 [L1]：STANDARD_MACHINE_TAG 仅 8 台；"所有机器可升级"→ 自定义全集判定）
        internal static readonly HashSet<string> ALL_MACHINE_IDS = new HashSet<string>(new string[] { "alarm_system", "moisture_farm", "water_purifier", "mirage_projector", "desequencer", "furnace", "wine_rack", "turbo_booster", "bottle_printer", "box_dispenser", "cassette_player", "animal_feeder", "recharger_base", "fridge", "blender", "chem_finisher", "deal_maker", "heating_plate", "hydroponic", "broken_machine" });
    internal static bool IsMachine(GameItem item)
    {
        try
        {
            if (item == null) return false;
            if (item.IsTag("STANDARD_MACHINE_TAG")) return true;
            string id = (item.identifier ?? "").ToLowerInvariant();
            return ALL_MACHINE_IDS.Contains(id);
        }
        catch { return false; }
    }

    // 机器/模板升级：消耗金属锭。机器=独立 tag wageUpgradePct/wageUpgradeEff（STANDARD 机器 Getter Postfix 不减半加回，
    // 避免被模块聚合重写覆盖；非 STANDARD 机器无聚合读 TOTAL_PERCENTAGE_* → 走模板路径）；
    // 模板=TOTAL 性能/质量 +2（聚合读模板 tag 后 Getter ×0.5，实 +1）
    private static bool TryUpgradeMachine(GameItem ingot, GameItem target)
    {
        try
        {
            if (IsMoreUpdateOwnedMachine(target)) return false; // MoreUpdate 兼容让路：配方机器归 MoreUpdate 独占
            // 09-15 瓶印机专用升级：只写质量（TOTAL_PERCENTAGE_QUALITY_BONUS_INT +2，水商之友 Getter 无减半 → 实 +2/次；鲁滨逊 ×0.5 → 实 +1/次）
            if (IsBottlePrinter(target))
            {
                AddTagInt(target, "TOTAL_PERCENTAGE_QUALITY_BONUS_INT", 2);
                ConsumeOne(ingot);
                return true;
            }
            bool isMachine = IsMachine(target);
            if (isMachine && target.IsTag("STANDARD_MACHINE_TAG"))
            {
                AddTagInt(target, "wageUpgradePct", 1);
                AddTagInt(target, "wageUpgradeEff", 1);
            }
            else
            {
                // 模板 / 非 STANDARD 机器：写 TOTAL_PERCENTAGE_*（此类机器无模块聚合重写覆盖）
                AddTagInt(target, "TOTAL_PERCENTAGE_PERFORMANCE_BONUS_INT", 2);
                AddTagInt(target, "TOTAL_PERCENTAGE_QUALITY_BONUS_INT", 2);
            }
            ConsumeOne(ingot);
            return true;
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] 机器升级异常: " + ex.Message); return false; }
    }

    // 容器升级（v2 段位制，用户拍板 09-12）：段0-5，junk 消耗 5/10/20/40/80，满级后 junk 正常放入
    // 段位换算：宽 = floor(wb_orig_w × (50% + 30%k))；段0=开局减半(50%)，段5=原宽2倍(200%)
    private static bool TryUpgradeContainer(GameItem junk, GameItem container)
    {
        try
        {
            if (ContainerUpgradeV2.ConsumedThisFrame(container.Pointer)) return true; // 同帧已消耗：防双计数（MayHaveValidInventorySlot+Target 双挂点）
            var grid = GetContainerGrid(container);
            if (grid == null) { Core.LogMsg("[空间站鲁滨逊] 容器升级失败：取不到内部库存 " + GetId(container)); return false; }
            int w = 0, h = 0;
            GetShapeWH(grid, ref w, ref h);
            if (w <= 0 || h <= 0) { Core.LogMsg("[空间站鲁滨逊] 容器升级失败：宽高异常 " + w + "x" + h); return false; }
            int stage = ContainerUpgradeV2.GetTagIntSafe(container, "wb_stage");
            if (stage >= ContainerUpgradeV2.MAX_STAGE) return false; // 满级：junk 正常放入（不再消耗）
            ContainerUpgradeV2.ConsumeOne(junk); // 逐颗消耗：每拖 1 个 junk 立即扣 1
            int progress = ContainerUpgradeV2.GetTagIntSafe(container, "wb_progress") + 1;
            int need = ContainerUpgradeV2.UPGRADE_COSTS[stage];
            if (progress < need)
            {
                ContainerUpgradeV2.SetTagIntValue(container, "wb_progress", progress);
                try { StoreUIManager.Instance.Notify(LangHelper.T("储存区 升级进度 " + progress + "/" + need, "Storage progress " + progress + "/" + need), "white"); } catch { }
                return true; // 已消耗，拦截放入
            }
            int targetW, targetH = h;
            if (ContainerUpgradeV2.IsWageBox(container)) {
                targetW = ContainerUpgradeV2.WAGE_BOX_W[stage + 1];
                targetH = ContainerUpgradeV2.WAGE_BOX_H[stage + 1];
            } else {
                int origW = ContainerUpgradeV2.GetTagIntSafe(container, "wb_orig_w");
                if (origW > 0)
                    targetW = ContainerUpgradeV2.GetCrusoeTargetWidth(origW, stage + 1);
                else
                    targetW = w + 1;
            }
            ContainerUpgradeV2.AddTagInt(container, "wb_stage", 1);
            ContainerUpgradeV2.SetTagIntValue(container, "wb_progress", 0); // 达标升段，进度清零重计
            try { WageSaveStore.SetInt(PERK_ID, "wage_stage_u" + container.uniqueId, stage + 1); } catch { } // 09-14 双写：场景位置 tags 不随档，PlayerPrefs 兜底
            try { if (ContainerUpgradeV2.IsUpgradeableContainer(container)) container.EnableTag("CONTAINER_TOOLTIP_TAG"); } catch { } // 拆包 2.5.32：容量行显示门控
            // 字符串重载（自动 ValidateBackground，虚空珠同路径）——全开放矩形 '0'=可放
            try { grid.SetShape(new string('0', targetW * targetH), targetW); } catch { try { grid.SetShape("", targetW); } catch { } }
            try { grid.Validate(); } catch { }
            try { StoreUIManager.Instance.Notify(LangHelper.T((stage + 1) >= ContainerUpgradeV2.MAX_STAGE ? "储存区满级！容量翻倍（宽 " + targetW + "）" : "储存区升级！段位 " + (stage + 1) + "/" + ContainerUpgradeV2.MAX_STAGE + "（宽 " + targetW + "）", (stage + 1) >= ContainerUpgradeV2.MAX_STAGE ? "Storage MAX! 2x capacity (width " + targetW + ")" : "Storage upgraded! Stage " + (stage + 1) + "/" + ContainerUpgradeV2.MAX_STAGE + " (width " + targetW + ")"), "white"); } catch { }
            try { Core.LogMsg("[容器v2] " + GetId(container) + " 升段 stage=" + (stage + 1) + " w=" + w + "->" + targetW + (ContainerUpgradeV2.IsWageBox(container) ? " (妙妙箱)" : "")); } catch { }
            return true;
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] 容器升级异常: " + ex.Message); return false; }
    }

    private static GameGridInventory GetContainerGrid(GameItem item)
    {
        try
        {
            var cw = item.contentWindow;
            if (cw != null && cw.childElement != null) { var v = cw.childElement.Cast<GameGridInventory>(); if (v != null) return v; }
        }
        catch (System.Exception ex) { Core.LogMsg("[RobinCrusoePerk.ContainerUpgrade] 异常: " + ex.Message); }
        return null;
    }

    // 容器宽高：inventoryShape 类型化直读（拆包 09-10 [L1]：GameGridInventory.inventoryShape public / GridShapeBuilder.width/height public）
    private static void GetShapeWH(GameGridInventory inv, ref int w, ref int h)
    {
        try
        {
            if (inv == null || inv.inventoryShape == null) return;
            w = inv.inventoryShape.width;
            h = inv.inventoryShape.height;
        }
        catch (System.Exception ex) { Core.LogMsg("[RobinCrusoePerk.ContainerUpgrade] 异常: " + ex.Message); }
    }

    private static void ConsumeOne(GameItem item)
    {
        try
        {
            int c = item.unitCount - 1;
            if (c <= 0) item.Destroy(); else item.SetUnitCount(c);
        }
        catch { try { item.Destroy(); } catch { } }
    }

}
