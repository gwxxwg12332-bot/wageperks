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

    // ===== 机器 tooltip 升级提示（拆包 2.5.31/2.5.32 复核：机器悬停 = MachineryHelper.CreateMachineryTooltip(RichTextBuilder, GameItem)
    // 2 参 public static；AddTooltipModuleBonus 真实签名 = string×3（默认值），非 int×3——挂入口 CreateMachineryTooltip 最省事）=====
    public static void PostfixCreateMachineryTooltip(RichTextBuilder builder, GameItem item)
    {
        try
        {
            bool robC = IsActive();
            // 09-15 养蛊机/生成器 tooltip（全局机器，不绑定职业）：充能/抽卡进度可视化
            if (item != null && (item.identifier == GuMachineSystem.GU_MACHINE_ID || item.identifier == GuMachineSystem.AI_GENERATOR_ID))
            {
                if (builder == null) return;
                if (item.identifier == GuMachineSystem.GU_MACHINE_ID)
                {
                    int charge = GetTagIntSafe(item, GuMachineSystem.GU_CHARGE_TAG);
                    builder.AddLine(LangHelper.T(
                        "◆ 充能 " + charge + "/3（打烊 +1，满 3 自动炼蛊·需舱内≥2模组）",
                        "◆ Charge " + charge + "/3 (+1 at close, auto-forge at 3, needs ≥2 modules)"), bold: true);
                }
                else
                {
                    // 09-19 P3：显示当前模式（读舱内保护器实时判定）+ 失败结果提示
                    bool hasProt = false;
                    try
                    {
                        var ggrid = GuMachineSystem.GetGuGrid(item);
                        if (ggrid != null && ggrid.childItems != null)
                            foreach (var m in ggrid.childItems)
                                if (m != null) { string mid = ""; try { mid = m.identifier ?? ""; } catch { } if (mid == GuMachineSystem.PROTECTOR_ID) { hasProt = true; break; } }
                    }
                    catch (System.Exception ex) { Core.LogMsg("[RobinCrusoePerk.ContainerUpgrade] 异常: " + ex.Message); }
                    string mode = hasProt
                        ? LangHelper.T("阉割版（100%成功，上限75%）", "Stable (100% success, cap 75%)")
                        : LangHelper.T("不稳定版（50%成功，失败产报废模组）", "Unstable (50% success, fail -> scrap module)");
                    builder.AddLine(LangHelper.T(
                        "◆ 打烊自动抽卡（舱内≥2模组）· 当前：" + mode,
                        "◆ Auto-draw at close (≥2 modules) · Now: " + mode), bold: true);
                }
                return;
            }
            if ((!robC && !(WaterMerchantPerk.IsActive() && IsBottlePrinter(item))) || builder == null || item == null) return;
            // 09-15 瓶印机专属 tooltip：显示质量实际加成（读 Getter 自动适配两职业倍率）
            if (IsBottlePrinter(item))
            {
                int q = 0;
                try { q = Il2Cpp.MachineryHelper.GetCurrentQualityBonus(item); } catch { }
                builder.AddLine(LangHelper.T(
                    "◆ 金属锭升级：质量 +" + q + "%（拖 metal_ingot 继续 +2%）",
                    "◆ Ingot upgrade: Quality +" + q + "% (drag metal_ingot +2%/each)"), bold: true);
                // 09-22 用户拍板：100质量出普通水
                builder.AddLine(LangHelper.T(
                    "◆ 100 质量出普通水",
                    "◆ 100 quality -> plain water"), bold: true);
                // 09-22 用户拍板：电子元件升瓶型满级 6000ml
                builder.AddLine(LangHelper.T(
                    "◆ 电子元件升级瓶型：满级打印 6000ml 超大瓶",
                    "◆ Electronic parts upgrade bottle type: max prints 6000ml jug"), bold: true);
                builder.AddLine(CustomStartingPerks.CommunityNote);
                return;
            }
            if (!IsMachine(item)) return;
            int pct = GetTagIntSafe(item, "wageUpgradePct");
            int effv = GetTagIntSafe(item, "wageUpgradeEff");
            if (pct <= 0 && effv <= 0)
            {
                builder.AddLine(LangHelper.T("◆ 金属锭升级：拖 metal_ingot 到机器 +1%/次（性能/效率/质量）", "◆ Ingot upgrade: drag metal_ingot to machine +1%/each (Perf/Eff/Quality)"), bold: true);
                return;
            }
            builder.AddLine(LangHelper.T("◆ 金属锭升级：性能+" + pct + "% 效率+" + effv + "%（拖 metal_ingot 继续 +1%）", "◆ Ingot upgrade: Perf +" + pct + "% Eff +" + effv + "% (keep dragging metal_ingot +1%)"), bold: true);
        }
        catch (System.Exception ex) { Core.LogMsg("[RobinCrusoePerk.ContainerUpgrade] 异常: " + ex.Message); }
    }

    // ===== 容器获得即减半（用户拍板 09-09：改挂点——任何容器/机器储存区物品获得时减半，可拖 junk 升级恢复）=====
    // ContainerHelper.InitContainerItem = 容器物品初始化统一入口（custom_storage_box 走它；官方容器同链）
    public static void PostfixInitContainerItem(GameInventory __0, GameItem __1)
    {
        try
        {
            if (!IsActive() || __0 == null || __1 == null) return;
            if (!__1.IsTag("CONTAINER_TAG") || __1.IsTag("VOID_BEAD_TAG") || ContainerUpgradeV2.IsWageBox(__1) || ContainerUpgradeV2.IsVoidBeadStorage(__1) || ContainerUpgradeV2.IsExcludedContainer(__1)) return;
            if (ContainerUpgradeV2.HasTag(__1, "wb_stage")) return; // 容器v2：已有段位（读档/已减半）→ 不重复减半
            ShrinkInv(__0 as GameGridInventory, GetId(__1) + "(容器获得减半)", __1);
            try { __1.EnableTag("CONTAINER_TOOLTIP_TAG"); } catch { } // 容量行显示门控（拆包 2.5.32）
        }
        catch (System.Exception ex) { Core.LogMsg("[RobinCrusoePerk.ContainerUpgrade] 异常: " + ex.Message); }
    }

    // ===== 容器开局容量减半（用户拍板 09-09 修正2：玩家主背包恢复原样；减半对象=机器上储存区/机器箱子/背包内容器物品）=====
    public static void PostfixEmporiumEntryStart(EmporiumEntry __instance)
    {
        try
        {
            if (!IsActive() || __instance == null) return;
            // 遍历柜台+背包内所有容器物品（machine_bay/机器内嵌箱子/custom_storage_box）与机器内部库存，减半
            ShrinkContainerItems(__instance.frontInvinvElement as GameInventory);
            ShrinkContainerItems(__instance.backInvinvElement as GameInventory);
            ShrinkContainerItems(__instance.invElement as GameInventory);
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] 容器减半异常: " + ex.Message); }
    }

    // 遍历库存内的容器物品，对其内部库存减半（储存箱子/柜子等）
    private static void ShrinkContainerItems(GameInventory inv)
    {
        try
        {
            if (inv == null || inv.childItems == null) return;
            var list = new System.Collections.Generic.List<GameItem>();
            foreach (var item in inv.childItems) { if (item != null) list.Add(item); }
            foreach (var item in list)
            {
                try
                {
                    if (item.IsTag("VOID_BEAD_TAG") || ContainerUpgradeV2.IsWageBox(item) || ContainerUpgradeV2.IsVoidBeadStorage(item) || ContainerUpgradeV2.IsExcludedContainer(item)) continue;
                    if (!item.IsTag("CONTAINER_TAG") && !IsMachine(item)) continue;
                    if (ContainerUpgradeV2.HasTag(item, "wb_stage")) continue; // 容器v2：已按段位管理，不重复减半
                    var grid = GetContainerGrid(item);
                    if (grid != null) ShrinkInv(grid, GetId(item) + "(储存区/机器箱)", item);
                }
                catch (System.Exception ex) { Core.LogMsg("[RobinCrusoePerk.ContainerUpgrade] 异常: " + ex.Message); }
            }
        }
        catch (System.Exception ex) { Core.LogMsg("[RobinCrusoePerk.ContainerUpgrade] 异常: " + ex.Message); }
    }

    // 真减半：读运行时 inventoryShape（GridShape 接口，实际 GridShapeBuilder 实现）的 width/height → SetShape(半宽, 高)
    // 保底：现有物品数 +5 格；宽度下限 4（防极端容器）
    private static void ShrinkInv(GameGridInventory inv, string label, GameItem item = null)
    {
        if (inv == null) return;
        // 拆包 2.5.32 六：容器容量行显示 = CONTAINER_TAG + CONTAINER_TOOLTIP_TAG（EnableTag 后原生 tooltip 自动显示容量）
        try
        {
            var shape = inv.inventoryShape;
            if (shape == null) {  return; }
            int w = shape.width, h = shape.height;
            if (w <= 0 || h <= 0) { Core.LogMsg("[空间站鲁滨逊] " + label + " 宽高异常(" + w + "x" + h + ")，跳过减半"); return; }
            int items = 0;
            try { items = inv.childItems != null ? inv.childItems.Count : 0; } catch { }
            int nw = Math.Max(4, w / 2);
            int cap = nw * h;
            if (items + 5 > cap) nw = Math.Max(4, (int)Math.Ceiling((items + 5) / (double)h)); // 保底不丢货
            // 容器v2：减半 = 段0（50%）；记录段位 + 官方原宽（读档按段位重设）
            if (item != null) { ContainerUpgradeV2.SetTagIntValue(item, "wb_stage", 0); ContainerUpgradeV2.SetTagIntValue(item, "wb_orig_w", w); }
            // 字符串重载（同 L1778：自动 ValidateBackground；全开放矩形 '0'=可放）
            inv.SetShape(new string('0', nw * h), nw);
            try { inv.Validate(); } catch { }
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] " + label + " 减半异常: " + ex.Message); }
    }

}
