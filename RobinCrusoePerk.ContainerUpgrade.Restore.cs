using System;
using HarmonyLib;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Il2Cpp;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace WagePerks;
internal static partial class RobinCrusoePerk
{

    // 【09-10 容器升级读档丢失修复】防双恢复：LoadGame 恢复成功 or SetContentWindow 恢复成功都标记，避免 SetShape(w+cap) 重复执行双加
    private static readonly HashSet<IntPtr> _rcRestoredContainers = new HashSet<IntPtr>();

    public static void PostfixLoadGame_IngotContainer()
    {
        try
        {
            _rcRestoredContainers.Clear(); // 读档：清容器恢复防重集合（新会话重新恢复）
            // 10-10 子项7c：读档清跨档静态缓存（样板=WageGirlSystem.State.OnGameLoadedReset :179 读档清缓存语义；本挂点=PlayerStore.LoadGame Postfix 表:198，无条件执行）
            //   tooltip 加速缓存（清 Day 防同地址复用误命中） + 骰子卸载按钮目标（防主菜单残留）+ 妙妙箱最近内部库存（防新档误用旧档库存）
            ClearTooltipCaches();
            DestinyDice.ClearActiveDice();
            CustomStorageContainer.ClearLastCreatedInventory();
            // 10-10 #48 C6：读档清跨档 static 残留（涡轮拖拽未消费残留→地址复用误补产；虚空珠列表跨档累积）——与 7c 同挂点不重复清
            TurboBoostN.ClearTurboState();
            LuckScoutBackpackUpgrade.ClearBeadItems();
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
                    // 10-18 审阅 R2 §4 改动6(选A)：血量回滚与 6 维一致——卖血后不存档退出重进血量应回保存点
                    //   （原只回滚 6 维，血量保留卖出后的值=不对称）。用 AddBlood 而非 SetBlood：复用归零死亡判定。
                    AddBlood(WageSaveStore.GetInt(PERK_ID, "saved_blood", BLOOD_MAX) - GetBlood());
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
                        // 10-04 容器冲突修复（篡夺的意志作者分析实锤）：BFS 只递归"我们自己拥有/升级过的容器"，
                        // 其他 mod 自定义容器（如篡夺戒指 6×3）一律不读 contentWindow——防触发其内部异常导致
                        // 鲁滨逊 Z面板/热量/进食整链失效。判定只走 tag（不碰 contentWindow，安全）。
                        bool ours = false;
                        try { ours = ContainerUpgradeV2.IsWageBox(it); } catch { }
                        if (!ours)
                        {
                            try { if (ContainerUpgradeV2.GetTagIntSafe(it, "wageUpgradeCap") > 0) ours = true; } catch { }
                            try { if (ContainerUpgradeV2.GetTagIntSafe(it, "wb_stage") > 0) ours = true; } catch { }
                        }
                        if (!ours) continue;
                        var cw = it.contentWindow;
                        if (cw == null || cw.childElement == null) continue;
                        var inner = cw.childElement.TryCast<GameGridInventory>();
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
                        // 10-13 Step4①②：黑金独立身份（WBG_BOX_TAG/identifier）→ 清妙妙箱三 tag（防恢复链"照顾"黑金
                        //   + 防补发第二箱）+ 跳过全部妙妙箱恢复路径（含前置 GetHiddenStageByIndex/GetStagePersist——先于 IsWageBox）
                        if (ContainerUpgradeV2.IsBlackGoldBox(item))
                        {
                            try { item.DisableTag("wb_stage"); item.DisableTag("wb_progress"); item.DisableTag("wb_second_given"); } catch { }
                            continue;
                        }
                        // 09-14 位置方案：hiddenElement（海报后边）物品按索引 PlayerPrefs 强恢复（tag 全丢无法识别）
                        int _hidx = ContainerUpgradeV2.FindBoxInHidden(item);
                        int _hstage = ContainerUpgradeV2.GetHiddenStageByIndex(_hidx, item);
                        if (_hstage > 0) { ContainerUpgradeV2.RestoreWageBoxToStage(item, _hstage); _rcRestoredContainers.Add(item.Pointer); restored++; continue; }
                        // 10-06 妙妙箱"重启回退 9 格"根治：持久化段位读取提前到 IsWageBox 判定之前——
                        //   读档后场景物品 tag 全丢 → IsWageBox false → 旧链整个跳过（恢复不执行，箱子回退初始）。
                        //   有持久化记录（wage_stage_{loc}_{idx}>0）= 升级过的妙妙箱 → 直接恢复（记录只在妙妙箱升段时写，普通物品/普通容器无记录不误伤）
                        int _pstage0 = ContainerUpgradeV2.GetStagePersist(item);
                        if (_pstage0 > 0) { ContainerUpgradeV2.RestoreWageBoxToStage(item, _pstage0); _rcRestoredContainers.Add(item.Pointer); restored++; continue; }
                        if (ContainerUpgradeV2.IsWageBox(item))
                        {
                            // 10-06 根治"三级变一级"：优先读 WageSaveStore 持久化段位（tag 读档丢场景）
                            int _pstage = ContainerUpgradeV2.GetStagePersist(item);
                            if (_pstage > 0) { ContainerUpgradeV2.RestoreWageBoxToStage(item, _pstage); _rcRestoredContainers.Add(item.Pointer); restored++; continue; }
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
            // 10-13 Step4①②：黑金独立身份 → 清三 tag + 跳过全部妙妙箱恢复路径（含前置 GetHiddenStageByIndex/GetStagePersist）
            if (ContainerUpgradeV2.IsBlackGoldBox(__instance))
            {
                try { __instance.DisableTag("wb_stage"); __instance.DisableTag("wb_progress"); __instance.DisableTag("wb_second_given"); } catch { }
                return;
            }
            // 09-14 位置方案：海报后边 hiddenElement 物品 tag 全丢 → 按索引 PlayerPrefs 强恢复（优先于 IsWageBox）
            try
            {
                int _hidx = ContainerUpgradeV2.FindBoxInHidden(__instance);
                int _hstage = ContainerUpgradeV2.GetHiddenStageByIndex(_hidx, __instance);
                if (_hstage > 0) { ContainerUpgradeV2.RestoreWageBoxToStage(__instance, _hstage); _rcRestoredContainers.Add(__instance.Pointer); return; }
            }
            catch (System.Exception ex) { Core.LogMsg("[RobinCrusoePerk.ContainerUpgrade] 异常: " + ex.Message); }
            // 10-06 妙妙箱"重启回退 9 格"根治（兜底挂点）：持久化段位读取提前到 IsWageBox 之前——
            //   本挂点=窗口构建（玩家打开箱子）触发，覆盖背包/柜台/后库任意位置；
            //   tag 读档丢场景 IsWageBox false → 旧链跳过 → 提前读取直接恢复
            try
            {
                int _pstage0 = ContainerUpgradeV2.GetStagePersist(__instance);
                if (_pstage0 > 0) { ContainerUpgradeV2.RestoreWageBoxToStage(__instance, _pstage0); _rcRestoredContainers.Add(__instance.Pointer); return; }
            }
            catch (System.Exception ex) { Core.LogMsg("[RobinCrusoePerk.ContainerUpgrade] 持久化恢复异常: " + ex.Message); }
            if (ContainerUpgradeV2.IsWageBox(__instance))
            {
                // 10-06 根治"三级变一级"：优先读 WageSaveStore 持久化段位（tag 读档丢场景）
                int _pstage = ContainerUpgradeV2.GetStagePersist(__instance);
                if (_pstage > 0) { ContainerUpgradeV2.RestoreWageBoxToStage(__instance, _pstage); _rcRestoredContainers.Add(__instance.Pointer); return; }
                try { if (!__instance.IsTag("CUSTOM_STORAGE_TAG")) __instance.EnableTag("CUSTOM_STORAGE_TAG"); } catch { } // 老档补打
                // 10-06 修"妙妙箱有时变回"：去掉防重集合限制——RestoreWageBoxShape 幂等（同尺寸 return），
                //   LoadGame 恢复时机早→原版后续可能重置 shape→窗口构建（玩家视角最近）恢复兜底，每次打开都校正
                ContainerUpgradeV2.RestoreWageBoxShape(__instance); // 蛙哥箱子
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
