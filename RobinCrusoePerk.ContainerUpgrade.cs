using System;
using HarmonyLib;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Il2Cpp;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace JacksonPerks;

// ============================================================
// 【空间站鲁滨逊】职业生存系统（startType=14）
// v4.2（2026-09-09，v4.2 终稿重构）：
//   - 饱食节点制：calorieBalance（卡，1单位=2200卡）替代 hunger 层数制，与原生 hunger(0-1000) 完全解耦
//   - 精神 5 档（昂扬/常态/低迷/低落/崩溃）：昂扬累计制（每2天+1%售价/+5%预算，封顶+5%/+25%，断档归零）
//   - 双击食物=摄入 cal（变质50%/腐烂20%）+ 已食用档 + 患病判定（变质10%/腐烂40%）
//   - 双击水=清零 thirstLevel（渴系统独立保留）
//   - 节点：濒饿(≤0且≥3天)/饥饿(≤0)/常态(1-5单位)/饱腹(>5单位)
//   - 粮仓充盈：余额≥7单位(15400卡) → 全店售价+5%
//   - 救场：连续≤0达5天 → 好心客户送食1-2份，不删档，归零
//   - 客流削减：低迷-1/低落-2/崩溃-4；禁外出：低落/崩溃
//   - 状态客户联动（Patches/Core 侧）：加价/概率权重/预算/出价
//
// 拆包锚点全部 [L1]（cheatsheet 2.3.9 / 2.3.10 / 2.3.12 / 2.5.16 / 4.6.8 / 4.6.9 / 设计AI v4.2）
// ============================================================
internal static partial class RobinCrusoePerk
{

    // ===== 物品判定 =====
    // 食物判定：硬编码清单 + 原生卡路里兜底（覆盖水培莓果/营养果/异种肉等遗漏）
    // 兜底规则：有 CALORIE_VALUE_TAG/CALORIE 标签 且 非饮品/药品/酒/种子 = 食物
    // 09-20 P2-4：熔炉模组白名单（furnace_module_ 开头不吞噬/炼蛊）
    internal static bool IsExcludedModule(GameItem item)
    {
        try {
            string id = item.identifier;
            if (string.IsNullOrEmpty(id)) return true;
            return id.StartsWith("furnace_module_");
        } catch { return true; }
    }

    // ===== 机器初始耗电 +2（拆包 09-10：GetMachinePowerUsage 是统一耗电读口；Postfix 兜底全机器，鲁滨逊职业内生效）=====
    public static void PostfixGetMachinePowerUsage(ref int __result)
    {
        try
        {
            if (!IsActive()) return;
            __result += 2;
        }
        catch { }
    }

    // ===== 供货商卖水药食物（拆包 09-10：PlaceSupplierInventory 是 supplier 上货入口；仿水商 MerchantHelper.AddItemToCounter 追加，同日不重复）=====
    private static int _supplierGoodsDay = -1;
    public static void PostfixPlaceSupplierInventory()
    {
        try
        {
            if (!IsActive()) return;
            int day = DeterministicSchedule.CurrentDay;
            if (day == _supplierGoodsDay) return; // 同日不重复追加
            _supplierGoodsDay = day;
            string[] sellItems = {
                "bottled_water",        // 瓶装水
                "small_bottled_water",  // 小瓶水
                "water_ration",         // 水配给
                "raw_meat",             // 生肉
                "processed_meat",       // 加工肉
                "cup_noodle",           // 杯面
                "bandage_item",         // 绷带
                "nutrient_tablet",      // 营养片
            };
            int added = 0;
            // 大瓶高品质水（拆包 09-10：WaterPremadeHelper.AccurateHighQualityWater(size) 一行生成指定品质大瓶）
            try
            {
                GameItem hq = Il2Cpp.WaterPremadeHelper.AccurateHighQualityWater("large_bottled_water");
                if (hq != null) { MerchantHelper.AddItemToCounter(hq, 100, false); added++; }
            }
            catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] 供货商加大瓶高品质水失败: " + ex.Message); }
            foreach (string wid in sellItems)
            {
                try
                {
                    GameItem w = DirectoryMaster.Item(wid, true);
                    if (w == null) { Core.LogMsg("[空间站鲁滨逊] 供货商加 " + wid + " 不存在"); continue; }
                    MerchantHelper.AddItemToCounter(w, 100, false);
                    added++;
                }
                catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] 供货商加 " + wid + " 失败: " + ex.Message); }
            }
            Core.LogMsg("[空间站鲁滨逊] 供货商已追加水/药/食物 " + added + " 件（day " + day + "）");
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] PostfixPlaceSupplierInventory 异常: " + ex.Message); }
    }
    // ===== 模板 0.5 总系数（用户拍板 09-09：模块/模板贡献减半；升级累加不受影响——MoreUpdate 写 TOTAL 标签，减半在读取端）=====
    // 拆包 2.5.26 [L1]：产出量走 GetCurrentPerformanceBonus/QualityBonus；处理速度走 ApplyBasicModuleEffect 内直读 TOTAL 标签（Getter 不覆盖）；
    // 原版模块走 ModifyTempStatFromBaseByPercentage；三个 TAG 减半后模块升级（AddVirtualBonus 累加）仍正常
    public static void PostfixGetCurrentPerformanceBonus(GameItem gameItem, ref int __result)
    {
        try
        {
            if (!IsActive() || gameItem == null) return;
            if (__result > 0) __result = Math.Max(0, (int)(__result * 0.5));
            // 机器升级（wageUpgradePct 独立 tag，不被模块聚合覆盖）：+1%/次，不减半
            int up = GetTagIntSafe(gameItem, "wageUpgradePct");
            if (up > 0) __result += up;
        }
        catch { }
    }
    public static void PostfixGetCurrentQualityBonus(GameItem gameItem, ref int __result)
    {
        try
        {
            if (!IsActive() || gameItem == null) return;
            if (__result > 0) __result = Math.Max(0, (int)(__result * 0.5));
            int up = GetTagIntSafe(gameItem, "wageUpgradePct");
            if (up > 0) __result += up;
        }
        catch { }
    }
    public static void PostfixApplyBasicModuleEffect(GameInventory invModule, GameItem item, GameItem system)
    {
        try
        {
            if (!IsActive() || item == null) return;
            var ts = item.GetTagReadonly("CURRENT_PROCESSING_SPEED_TAG");
            if (ts != null && ts.valueInt > 0) SetTagIntValue(item, "CURRENT_PROCESSING_SPEED_TAG", Math.Max(0, (int)(ts.valueInt * 0.5)));
            // v5.9 效率升级（用户拍板 09-09：金属锭拖机器 +1%，无限叠加）：速度 = 原×0.5 + 机器效率升级数
            // wageUpgradeEff 直接加（不减半），写 item 无则取 system
            GameItem target = item;
            if (target.GetTagReadonly("wageUpgradeEff") == null && system != null) target = system;
            var eff = target.GetTagReadonly("wageUpgradeEff");
            if (eff != null && eff.valueInt > 0 && ts != null && ts.valueInt > 0)
                SetTagIntValue(target, "CURRENT_PROCESSING_SPEED_TAG", Math.Max(0, (int)(ts.valueInt * 0.5) + eff.valueInt));
        }
        catch { }
    }
    public static void PostfixModifyTempStatFromBaseByPercentage(GameItem item, int percentage)
    {
        try
        {
            if (!IsActive() || item == null) return;
            foreach (string t in new[] { "TEMP_PERCENTAGE_PERFORMANCE_INT", "TEMP_PERCENTAGE_EFFICIENCY_INT", "TEMP_PERCENTAGE_QUALITY_INT" })
            {
                var ts = item.GetTagReadonly(t);
                if (ts != null && ts.valueInt > 0) SetTagIntValue(item, t, Math.Max(0, (int)(ts.valueInt * 0.5)));
            }
        }
        catch { }
    }
    internal static void SetTagIntValue(GameItem item, string tag, int value)
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
        catch { }
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
        catch { }
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
        catch { }
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
        catch { }
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
        catch { }
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

    // 【09-10 容器升级读档丢失修复】防双恢复：LoadGame 恢复成功 or SetContentWindow 恢复成功都标记，避免 SetShape(w+cap) 重复执行双加
    private static readonly HashSet<IntPtr> _rcRestoredContainers = new HashSet<IntPtr>();

    public static void PostfixLoadGame_IngotContainer()
    {
        try
        {
            _rcRestoredContainers.Clear(); // 读档：清容器恢复防重集合（新会话重新恢复）
            PerkStatePersistence.ResetCache(); // 读档切档：清旧层 runID 缓存（兼容读取仍走旧层，防 key 前缀串用导致状态节点全回默认，用户反馈 09-10）
            // 恢复保存点生存状态（SaveGame 快照）——"退出本天未保存重新进"当天扣减（拾荒-7等）应随读档回滚
            try
            {
                if (IsActive() && WageSaveStore.HasKey(PERK_ID, "saved_sleep"))
                {
                    SetSatiety(WageSaveStore.GetInt(PERK_ID, "saved_sat", 100));
                    SetThirstPct(WageSaveStore.GetInt(PERK_ID, "saved_th", 100));
                    SetHealth(WageSaveStore.GetInt(PERK_ID, "saved_hp", 100));
                    SetClean(WageSaveStore.GetInt(PERK_ID, "saved_clean", CLEAN_START));
                    SetSleep(WageSaveStore.GetInt(PERK_ID, "saved_sleep", SLEEP_START));
                    SetSocial(WageSaveStore.GetInt(PERK_ID, "saved_social", SOCIAL_START));
                }
            }
            catch { }
            EmporiumEntry emporium = EmporiumEntry.Instance;
            if (emporium == null) return;
            var allInvs = new System.Collections.Generic.List<GameInventory>();
            try { var v = emporium.backInvinvElement as GameInventory; if (v != null) allInvs.Add(v); } catch { }
            try { var v = emporium.backInvinvElementCounter as GameInventory; if (v != null) allInvs.Add(v); } catch { }
            try { var v = emporium.showcaseElement as GameInventory; if (v != null) allInvs.Add(v); } catch { }
            try { var v = emporium.invElement as GameInventory; if (v != null) allInvs.Add(v); } catch { }
            try { var v = emporium.frontInvinvElement as GameInventory; if (v != null) allInvs.Add(v); } catch { } // 09-10 补 frontInv（前台）——升级的包放前台时恢复漏找
            try { var v = emporium.hiddenElement as GameInventory; if (v != null) allInvs.Add(v); } catch { } // 09-14 补 hiddenElement（海报后边 2×2）——位置方案
            var visited = new HashSet<IntPtr>();
            var stack = new Stack<GameInventory>(allInvs);
            while (stack.Count > 0)
            {
                var inv = stack.Pop();
                if (inv == null || inv.childItems == null) continue;
                for (int i = 0; i < inv.childItems.Count; i++)
                {
                    var it = inv.childItems[i];
                    if (it == null || !visited.Add(it.Pointer)) continue;
                    try
                    {
                        var cw = it.contentWindow;
                        if (cw == null || cw.childElement == null) continue;
                        var inner = cw.childElement.Cast<GameGridInventory>();
                        if (inner != null && !allInvs.Contains(inner)) { allInvs.Add(inner); stack.Push(inner); }
                    }
                    catch { }
                }
            }
            int restored = 0;
            foreach (var inv in allInvs)
            {
                if (inv == null || inv.childItems == null) continue;
                foreach (var item in inv.childItems)
                {
                    if (item == null) continue;
                    try
                    {
                        // 09-14 位置方案：hiddenElement（海报后边）物品按索引 PlayerPrefs 强恢复（tag 全丢无法识别）
                        int _hidx = ContainerUpgradeV2.FindBoxInHidden(item);
                        int _hstage = ContainerUpgradeV2.GetHiddenStageByIndex(_hidx);
                        if (_hstage > 0) { ContainerUpgradeV2.RestoreWageBoxToStage(item, _hstage); _rcRestoredContainers.Add(item.Pointer); restored++; continue; }
                        if (ContainerUpgradeV2.IsWageBox(item))
                        {
                            try { if (!item.IsTag("CUSTOM_STORAGE_TAG")) item.EnableTag("CUSTOM_STORAGE_TAG"); } catch { } // 老档箱子补打 tag（09-13：缺 tag 导致升级挂点不识别）
                            ContainerUpgradeV2.RestoreWageBoxShape(item); // 蛙哥箱子：按段位恢复（含老档满级迁移）
                            _rcRestoredContainers.Add(item.Pointer);
                            restored++;
                            continue;
                        }
                        if (!item.IsTag("CONTAINER_TAG") || item.IsTag("VOID_BEAD_TAG") || ContainerUpgradeV2.IsVoidBeadStorage(item) || ContainerUpgradeV2.IsExcludedContainer(item)) continue;
                        // 容器v2：按段位恢复（含老档 wageUpgradeCap>0 → 满级迁移）；未升级老档保持现状
                        if (ContainerUpgradeV2.RestoreCrusoeShape(item)) { _rcRestoredContainers.Add(item.Pointer); restored++; }
                    }
                    catch { }
                }
            }
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] 读档恢复容器异常: " + ex.Message); }
    }

    // ===== 容器升级读档恢复（主修，抄虚空珠 PostfixSetContentWindow 挂点）：窗口构建后识别升级容器并恢复 SetShape =====
    public static void PostfixSetContentWindow_IngotContainer(GameItem __instance)
    {
        try
        {
            if (__instance == null) return;
            // 09-14 位置方案：海报后边 hiddenElement 物品 tag 全丢 → 按索引 PlayerPrefs 强恢复（优先于 IsWageBox）
            try
            {
                int _hidx = ContainerUpgradeV2.FindBoxInHidden(__instance);
                int _hstage = ContainerUpgradeV2.GetHiddenStageByIndex(_hidx);
                if (_hstage > 0) { ContainerUpgradeV2.RestoreWageBoxToStage(__instance, _hstage); _rcRestoredContainers.Add(__instance.Pointer); return; }
            }
            catch { }
            if (ContainerUpgradeV2.IsWageBox(__instance))
            {
                try { if (!__instance.IsTag("CUSTOM_STORAGE_TAG")) __instance.EnableTag("CUSTOM_STORAGE_TAG"); } catch { } // 老档补打
                if (_rcRestoredContainers.Add(__instance.Pointer)) ContainerUpgradeV2.RestoreWageBoxShape(__instance); // 蛙哥箱子
                return;
            }
            if (!IsActive()) return; // 普通容器恢复需要鲁滨逊激活；妙妙箱已在上面恢复
            if (!__instance.IsTag("CONTAINER_TAG") || __instance.IsTag("VOID_BEAD_TAG") || ContainerUpgradeV2.IsVoidBeadStorage(__instance) || ContainerUpgradeV2.IsExcludedContainer(__instance)) return;
            if (!ContainerUpgradeV2.HasTag(__instance, "wb_stage") && GetTagIntSafe(__instance, "wageUpgradeCap") <= 0) return; // 未升级老档不恢复
            if (!_rcRestoredContainers.Add(__instance.Pointer)) return; // 已恢复过：跳过防双加
            ContainerUpgradeV2.RestoreCrusoeShape(__instance);
        }
        catch { }
    }

    internal static int GetTagIntSafe(GameItem item, string tag)
    {
        try { var t = item.GetTagReadonly(tag); if (t != null) return t.valueInt; } catch { }
        return 0;
    }
    internal static void AddTagInt(GameItem item, string tag, int delta)
    {
        try
        {
            int v = GetTagIntSafe(item, tag);
            SetTagIntValue(item, tag, v + delta);
        }
        catch { }
    }

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
                    catch { }
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
        catch { }
    }

    // ===== 面板显示 0.5（用户拍板 09-09："机器面板显示的数字"；拆包：CreateModuleTooltip/AddModuleStatLine 直读 tag
    // 不经 Getter → 面板显示原值、产出已减半，两者不同源。此 Postfix 在统计显示行统一减半性能/效率/质量，面板=实际）=====
    public static void PostfixAddModuleStatLine(string statName, ref int baseValue, ref int tempValue)
    {
        try
        {
            if (!IsActive()) return;
            if (statName == null) return;
            // statName 可能是 tag 名或本地化显示名，双匹配（大小写不敏感）
            string n = statName.ToLowerInvariant();
            if (n.Contains("performance") || n.Contains("efficiency") || n.Contains("quality")
                || n.Contains("性能") || n.Contains("效率") || n.Contains("质量"))
            {
                if (baseValue > 0) baseValue = Math.Max(0, (int)(baseValue * 0.5));
                if (tempValue > 0) tempValue = Math.Max(0, (int)(tempValue * 0.5));
            }
        }
        catch { }
    }
    // ===== 机器 0.5 总系数（用户拍板 09-09；拆包 2.5.27 [L1]：逐台 Postfix ×0.5 含基础，不写 tag——写 -50 只有 2 条路径天然减半）=====
    // water_recycler 效率：ApplyPerformanceWaterRecyclerEffect(GameItem) 后效率 tag ×0.5（含 50 基础；船舶系统）
    public static void PostfixApplyPerformanceWaterRecyclerEffect(GameItem __0)
    {
        try
        {
            if (!IsActive() || __0 == null) return;
            var ts = __0.GetTagReadonly("WATER_RECYCLER_CURRENT_EFFICIENCY_INT");
            if (ts != null && ts.valueInt > 0) SetTagIntValue(__0, "WATER_RECYCLER_CURRENT_EFFICIENCY_INT", Math.Max(0, (int)(ts.valueInt * 0.5)));
        }
        catch { }
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
        catch { }
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
                catch { }
            }
        }
        catch { }
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
    // ===== 机器 0.5 总系数（拆包 2.5.28 补齐）=====
    // moisture_farm 产出量：MachineMoistureFarm.GetOutputVolume(GameItem)→int ×0.5（含基础）
    public static void PostfixGetOutputVolume(ref int __result)
    {
        try { if (IsActive() && __result > 0) __result = Math.Max(1, (int)(__result * 0.5)); } catch { }
    }
    // water_purifier 基础半：WaterHelper.RemoveContaminantFromContainer 返回移除量 ×0.5（净化慢一半；加成半已被模板0.5覆盖）
    public static void PostfixRemoveContaminantFromContainer(ref int __result)
    {
        try { if (IsActive() && __result > 0) __result = Math.Max(1, (int)(__result * 0.5)); } catch { }
    }

    // ===== 物品归属判定 =====
    private static bool _ownedInited = false;
    private static System.Reflection.MethodInfo _isItemOwned = null;
    // 物品是否在玩家背包或柜台（指针比较，柜台陈列/待售物品也判定为玩家可控）
    private static bool IsInBackpackOrCounter(GameItem item)
    {
        try
        {
            EmporiumEntry em = EmporiumEntry.Instance;
            if (em == null || item == null) return false;
            IntPtr targetPtr = item.Pointer;
            if (targetPtr == IntPtr.Zero) return false;
            foreach (GameInventory inv in new[] { (GameInventory)em.backInvinvElement, (GameInventory)em.frontInvinvElement })
            {
                if (inv == null || inv.childItems == null) continue;
                for (int i = 0; i < inv.childItems.Count; i++)
                {
                    var ci = inv.childItems[i];
                    if (ci != null && ci.Pointer == targetPtr) return true;
                }
            }
        }
        catch { }
        return false;
    }

    private static bool IsItemOwned(GameItem item)
    {
        if (item == null) return false;
        if (!_ownedInited)
        {
            _ownedInited = true;
            try
            {
                var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
                foreach (var assembly in assemblies)
                {
                    if (assembly.GetName().Name != "Assembly-CSharp") continue;
                    var type = assembly.GetType("GeneralHelper");
                    if (type == null)
                    {
                        var types = assembly.GetTypes();
                        foreach (var t2 in types) { if (t2.Name == "GeneralHelper") { type = t2; break; } }
                    }
                    if (type == null) break;
                    var methods = type.GetMethods(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                    foreach (var mi in methods)
                    {
                        if (mi.Name == "IsItemOwned" && mi.GetParameters().Length == 1)
                        { _isItemOwned = mi; break; }
                    }
                    break;
                }

            }
            catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] IsItemOwned 查找异常 " + ex.Message); }
        }
        if (_isItemOwned == null) return true;
        try { return (bool)_isItemOwned.Invoke(null, new object[] { item }); }
        catch { return true; }
    }

    // ===== 09-13 用户拍板：枪械改装全局关闭（恢复"王尔德枪匠未解锁"状态）+ 定制单删除 =====
    // A. Postfix GunHelper.InitGun：拆包 09-13 [L1] moddable=true → SetGameItemType("MODDABLE")，模组系统按 type 判定可装——
    // 全局移除 MODDABLE type → 所有枪不可加零件（不显示可加零件）
    public static void PostfixInitGun(Il2Cpp.GameItem __0)
    {
        try
        {
            if (__0 == null) return;
            var types = __0.GetGameItemType();
            if (types != null && types.Contains("MODDABLE"))
                __0.RemoveGameItemType("MODDABLE");
        }
        catch { }
    }
    // C. Prefix StoreClientListGun 订单生成：拦截定制单（拆包 09-13 [L1]：原生有 null 防护——客户端照常来店但无定制要求）
    public static bool PrefixBlockGunOrder() { return false; }
    // B. Prefix DirectoryMaster.Item：枪械模组 id 重定向无害物品（拆包 09-13 [L1]：全游戏物品创建统一入口，商店/奖励/全量随机都走它；
    // 模组物品无按 id 点名生成，只可能经全量池随机进入游戏 → 此处拦截全覆盖；重定向而非 null 防崩）
    private static System.Collections.Generic.HashSet<string> _gunModIds = null;
    private static readonly string[] _gunModIdFallback = {
        "rds_view", "rds_view2", "rds_makeshift_view", "silencer_view", "silencer2_view",
        "barrel_view", "compensator_view", "grip_view", "stock_view"
    };
    private static bool IsGunModIdentifier(string id)
    {
        try
        {
            if (string.IsNullOrEmpty(id)) return false;
            if (_gunModIds == null)
            {
                _gunModIds = new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
                foreach (var s in _gunModIdFallback) _gunModIds.Add(s);
                // 运行时枚举 GunModDirectory 注册表补全（失败兜底硬编码）
                try
                {
                    var ids = Il2Cpp.DirectoryMaster.GetIdentifierList<object>("GunModDirectory");
                    if (ids != null) { foreach (var s in ids) { if (!string.IsNullOrEmpty(s)) _gunModIds.Add(s); } }
                }
                catch { }
            }
            return _gunModIds.Contains(id);
        }
        catch { return false; }
    }
    public static bool PrefixDirectoryMasterItem(ref string identifier, bool isOwned)
    {
        try
        {
            if (identifier != null && IsGunModIdentifier(identifier))
                identifier = "scrap_metal"; // 重定向无害废金属（防崩；模组物品不再生成到任何池）
        }
        catch { }
        return true;
    }

    // 09-22 PostfixSort：右键排列后恢复升级容器 shape
    [HarmonyPostfix]
    static void PostfixSort(GameGridInventory inventory, bool bigFirst, bool fromEnd)
    {
        try
        {
            if (!IsActive()) return;
            var emporium = EmporiumEntry.Instance;
            if (emporium == null) return;
            var allInvs = new System.Collections.Generic.List<GameInventory>();
            try { var v = emporium.backInvinvElement as GameInventory; if (v != null) allInvs.Add(v); } catch { }
            try { var v = emporium.backInvinvElementCounter as GameInventory; if (v != null) allInvs.Add(v); } catch { }
            try { var v = emporium.showcaseElement as GameInventory; if (v != null) allInvs.Add(v); } catch { }
            try { var v = emporium.invElement as GameInventory; if (v != null) allInvs.Add(v); } catch { }
            try { var v = emporium.frontInvinvElement as GameInventory; if (v != null) allInvs.Add(v); } catch { }
            try { var v = emporium.hiddenElement as GameInventory; if (v != null) allInvs.Add(v); } catch { }
            foreach (var inv in allInvs)
            {
                if (inv == null || inv.childItems == null) continue;
                foreach (var child in inv.childItems)
                {
                    var item = child.TryCast<GameItem>();
                    if (item == null) continue;
                    if (!ContainerUpgradeV2.IsUpgradeableContainer(item)) continue;
                    int stage = ContainerUpgradeV2.GetTagIntSafe(item, "wb_stage");
                    if (stage > 0) ContainerShapeHelper.RestoreToStage(item, stage);
                }
            }
        }
        catch { }
    }
}
