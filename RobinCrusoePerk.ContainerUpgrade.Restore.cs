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

    // 【09-10 容器升级读档丢失修复】防双恢复：LoadGame 恢复成功 or SetContentWindow 恢复成功都标记，避免 SetShape(w+cap) 重复执行双加
    private static readonly HashSet<IntPtr> _rcRestoredContainers = new HashSet<IntPtr>();

    public static void PostfixLoadGame_IngotContainer()
    {
        try
        {
            _rcRestoredContainers.Clear(); // 读档：清容器恢复防重集合（新会话重新恢复）
            // 旧层 runID 缓存刷新已收拢至 WageSaveStore（兼容读取入口自动 EnsureLegacyFresh），此处不再手写
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
            catch (System.Exception ex) { Core.LogMsg("[RobinCrusoePerk.ContainerUpgrade] 异常: " + ex.Message); }
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
                    catch (System.Exception ex) { Core.LogMsg("[RobinCrusoePerk.ContainerUpgrade] 异常: " + ex.Message); }
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
                    catch (System.Exception ex) { Core.LogMsg("[RobinCrusoePerk.ContainerUpgrade] 异常: " + ex.Message); }
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
            catch (System.Exception ex) { Core.LogMsg("[RobinCrusoePerk.ContainerUpgrade] 异常: " + ex.Message); }
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
        catch (System.Exception ex) { Core.LogMsg("[RobinCrusoePerk.ContainerUpgrade] 异常: " + ex.Message); }
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
        catch (System.Exception ex) { Core.LogMsg("[RobinCrusoePerk.ContainerUpgrade] 异常: " + ex.Message); }
    }

}
