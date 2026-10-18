using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace WagePerks;
partial class ContainerUpgradeV2

{
    // ===== Restore =====
    // 10-18 总控 P0 加固修订：本会话已见过的尺寸不符（防同帧双调用当场凑"两次"——
    //   RobinCrusoePerk.ContainerUpgrade.Restore.cs:110/:115 同帧两次 GetStagePersist，若无本判据旁键当场升级=一次即清）
    private static readonly System.Collections.Generic.HashSet<string> _sawMismatchThisSession = new System.Collections.Generic.HashSet<string>();

    // 蛙哥箱子读档恢复：按 wb_stage 重设网格；老档（无 wb_stage 且 shape>3×3）→ 满级迁移
    public static void RestoreWageBoxShape(GameItem box)
    {
        try
        {
            if (box == null) return;
            // 10-10 四问题批#4：白名单防御纵深——只处理确证妙妙箱（IsWageBox 含黑金精确排除 Detect.cs:38）。
            //   当前调用侧（PostfixLoadGame_IngotContainer:100 / PostfixSetContentWindow_IngotContainer:147）已在 IsWageBox 分支内，
            //   本判定防未来新增裸调用路径误把任意容器当妙妙箱 SetFullRect 改容积（段位回溯/容积破坏根因防护）。
            if (!IsWageBox(box)) return;
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

    // ===================== 10-06 根治：段位持久化（WageSaveStore 随档，不依赖 tag）=====================
    // 根因实锤：wb_stage 是物品 tag——场景库存物品读档后 tag 全丢（Restore.cs 旧注释），
    //   shape 也被重置 → RestoreWageBoxShape 按尺寸推断=0 级 → "三级妙妙箱变一级"。
    // 方案：升段写点（打烊消耗/拖螺丝）双写 WageSaveStore（key=箱子所在库存+childItems 索引，
    //   原版库存顺序读档稳定——hiddenElement 索引方案同款先例），恢复链优先读持久化段位。
    private static string LocKeyOf(GameInventory inv)
    {
        try
        {
            var e = EmporiumEntry.Instance;
            if (e == null || inv == null) return null;
            if (inv == (e.backInvinvElement as GameInventory)) return "back";
            if (inv == (e.backInvinvElementCounter as GameInventory)) return "backC";
            if (inv == (e.showcaseElement as GameInventory)) return "show";
            if (inv == (e.invElement as GameInventory)) return "main";
            if (inv == (e.frontInvinvElement as GameInventory)) return "front";
            if (inv == (e.hiddenElement as GameInventory)) return "hid";
        }
        catch (System.Exception ex) { Core.LogMsg("[ContainerUpgradeV2] LocKeyOf 异常: " + ex.Message); }
        return null;
    }

    public static int FindIndexInInv(GameInventory inv, GameItem box)
    {
        try
        {
            if (inv == null || inv.childItems == null || box == null) return -1;
            for (int i = 0; i < inv.childItems.Count; i++)
                if (inv.childItems[i] != null && inv.childItems[i].Pointer == box.Pointer) return i;
        }
        catch (System.Exception ex) { Core.LogMsg("[ContainerUpgradeV2] FindIndexInInv 异常: " + ex.Message); }
        return -1;
    }

    // 升段/初始化后调用：按箱子当前所在库存+索引写段位到 WageSaveStore（随档）
    public static void SetStagePersist(GameItem box, int stage)
    {
        try
        {
            var e = EmporiumEntry.Instance;
            if (e == null || box == null) return;
            GameInventory[] invs = new GameInventory[] {
                e.backInvinvElement as GameInventory,
                e.backInvinvElementCounter as GameInventory,
                e.showcaseElement as GameInventory,
                e.invElement as GameInventory,
                e.frontInvinvElement as GameInventory,
                e.hiddenElement as GameInventory,
            };
            foreach (var inv in invs)
            {
                int idx = FindIndexInInv(inv, box);
                if (idx >= 0)
                {
                    string loc = LocKeyOf(inv);
                    if (loc != null) WageSaveStore.SetInt("SurvivalGlobal", "wage_stage_" + loc + "_" + idx, stage);
                    return;
                }
            }
        }
        catch (System.Exception ex) { Core.LogMsg("[ContainerUpgradeV2] SetStagePersist 异常: " + ex.Message); }
    }

    // 恢复链调用：按箱子当前所在库存+索引读持久化段位（-1=无记录）
    public static int GetStagePersist(GameItem box)
    {
        try
        {
            // 10-18 拍板补读（M22：写入点 :232 注释明示"双写/兜底"=半成品补读，不删）：
            //   先查 uniqueId 兜底键 wage_stage_u{uniqueId}（SurvivalGlobal ns 与写入一致）。
            //   uniqueId=内存计数器不随档（ThiefMagnetSystem.cs:34 / Restore.cs:149 实锤）——
            //   同会话稳定：容器挪动后位置键失效时兜底命中（身份锚定，全局递增不误伤）；
            //   跨会话 uniqueId 重分配 → 未命中 → 回退位置键（兼容旧档/跨会话，行为不变）。
            if (box != null)
            {
                int uidStage = WageSaveStore.GetInt("SurvivalGlobal", "wage_stage_u" + box.uniqueId, -1);
                if (uidStage > 0)
                {
                    // 10-18 总控同域新风险修复：uid 兜底原无校验——uniqueId=内存计数器跨会话重号无法排除
                    //   （PlayerStore.cs:442 currentUniqueId 运行时计数器 / SaveItemNode.cs:15 带 uniqueId 字段），
                    //   重号+位置占用=从 uid 通道强恢复误伤（不经 IsWageBox/尺寸闸门）。加尺寸校验同位置键：
                    //   不符=降级未命中（回退位置键，不恢复不写盘）。
                    var g0 = GetContainerGrid(box);
                    int cw = 0, ch = 0; GetShapeWH(g0, ref cw, ref ch);
                    int ts = uidStage > MAX_STAGE ? MAX_STAGE : uidStage;
                    if (cw == WAGE_BOX_W[ts] && ch == WAGE_BOX_H[ts])
                    {
                        Core.LogMsg("[容器v2] uniqueId 兜底命中 " + box.uniqueId + " stage=" + uidStage);
                        return uidStage;
                    }
                    Core.LogMsg("[容器v2] uniqueId 兜底尺寸不符(降级未命中): uid=" + box.uniqueId + " stage=" + uidStage + " 实际=" + cw + "x" + ch + " 期望=" + WAGE_BOX_W[ts] + "x" + WAGE_BOX_H[ts]);
                }
            }
            var e = EmporiumEntry.Instance;
            if (e == null || box == null) return -1;
            GameInventory[] invs = new GameInventory[] {
                e.backInvinvElement as GameInventory,
                e.backInvinvElementCounter as GameInventory,
                e.showcaseElement as GameInventory,
                e.invElement as GameInventory,
                e.frontInvinvElement as GameInventory,
                e.hiddenElement as GameInventory,
            };
            foreach (var inv in invs)
            {
                int idx = FindIndexInInv(inv, box);
                if (idx >= 0)
                {
                    string loc = LocKeyOf(inv);
                    if (loc != null)
                    {
                        // 10-07 阶段D D-3：ns 随迁 SurvivalGlobal + 旧档兜底迁移（旧 RobinCrusoe 值读入写回新 ns，幂等一次性升级）
                        int s = WageSaveStore.GetInt("SurvivalGlobal", "wage_stage_" + loc + "_" + idx, -1);
                        if (s <= 0)
                        {
                            s = WageSaveStore.GetInt("RobinCrusoe", "wage_stage_" + loc + "_" + idx, -1);
                            if (s > 0) WageSaveStore.SetInt("SurvivalGlobal", "wage_stage_" + loc + "_" + idx, s);
                        }
                        // 10-18 审阅 P0-② 落地：**移除位置键命中→反写 uid 键**（原 10-18 增强）——
                        //   审阅实证：若位置键值来自误伤（普通容器占了曾升级箱的 loc+idx），反写会把误伤段位
                        //   洗白进 uid 键并自我延续（同会话兜底反复命中错误值）。且反写的"跨会话转同会话兜底"
                        //   价值低：跨会话恢复后 RestoreWageBoxToStage 已打回 wb_stage tag（:214），同会话挪动
                        //   不丢 tag，无需 uid 兜底。uid 兜底读（:121）保留（用户拍板补读——重进当天场景有效）。
                        if (s > 0)
                        {
                            // 10-18 P0 根治：位置键命中后加**尺寸匹配**身份校验——
                            //   升级动作把网格改成 WAGE_BOX_W/H[stage]（SetFullRect），故"当前网格==该段位尺寸"
                            //   才是本箱（读档后 tag/identifier/uniqueId 全丢，尺寸是唯一随档可验证的身份特征）；
                            //   位置被普通容器占用/键残留时尺寸不符 → 不误伤。
                            // 10-18 总控改进（清键不可逆防护）：**连续两次不符才清键**——
                            //   首次不符只记旁键（wage_stage_mismatch_{loc}_{idx}）+ 跳过不恢复（防误伤）；
                            //   二次读档仍不符（旁键已存在）→ 确认真残留 → 清位置键+旁键（自愈）。
                            if (box != null)
                            {
                                var g0 = GetContainerGrid(box);
                                int cw = 0, ch = 0; GetShapeWH(g0, ref cw, ref ch);
                                int ts = s > MAX_STAGE ? MAX_STAGE : s;
                                string misKey = "wage_stage_mismatch_" + loc + "_" + idx;
                                // 10-18 总控修订：成功匹配即清旁键（防"二次确认"退化跨事件累积——
                                //   旧实现不清旁键：误伤留 mis=1 后位置恢复正常，下次误伤第一次即清=一次即删）
                                if (cw == WAGE_BOX_W[ts] && ch == WAGE_BOX_H[ts])
                                {
                                    try { if (WageSaveStore.GetInt("SurvivalGlobal", misKey, 0) != 0) WageSaveStore.SetInt("SurvivalGlobal", misKey, 0); } catch { }
                                    return s;
                                }
                                int mis = WageSaveStore.GetInt("SurvivalGlobal", misKey, 0);
                                // 10-18 总控修订：升级判据=跨会话确认——旁键(上次会话见过)+本会话首次见才清键；
                                //   同帧双调用（RobinCrusoe:110/:115）第二调用 _saw 已含 → 不升级（不当场凑数）
                                if (mis > 0 && !_sawMismatchThisSession.Contains(misKey))
                                {
                                    _sawMismatchThisSession.Add(misKey);
                                    try
                                    {
                                        WageSaveStore.SetInt("SurvivalGlobal", "wage_stage_" + loc + "_" + idx, -1);
                                        WageSaveStore.SetInt("SurvivalGlobal", misKey, -1);
                                    }
                                    catch { }
                                    Core.LogMsg("[容器v2] 尺寸不符×2(误伤防护确认): loc=" + loc + " idx=" + idx + " stage=" + s + " 实际=" + cw + "x" + ch + " 期望=" + WAGE_BOX_W[ts] + "x" + WAGE_BOX_H[ts] + " → 清键自愈");
                                }
                                else
                                {
                                    // 10-18 总控 B4：同会话"首次"日志抑制——Add 返回 true=本会话首见才写旁键+打日志；
                                    //   同会话后续不符（_saw 已含）静默（行为幂等，防测试协议误读"多行首次"）
                                    if (_sawMismatchThisSession.Add(misKey))
                                    {
                                        try { WageSaveStore.SetInt("SurvivalGlobal", misKey, 1); } catch { }
                                        Core.LogMsg("[容器v2] 尺寸不符(误伤防护首次): loc=" + loc + " idx=" + idx + " stage=" + s + " 实际=" + cw + "x" + ch + " 期望=" + WAGE_BOX_W[ts] + "x" + WAGE_BOX_H[ts] + " → 跳过不恢复(待二次确认)");
                                    }
                                }
                            }
                            return -1;
                        }
                    }
                }
            }
        }
        catch (System.Exception ex) { Core.LogMsg("[ContainerUpgradeV2] GetStagePersist 异常: " + ex.Message); }
        return -1;
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
    // 10-18 P0 根治：加 box 参数做尺寸匹配身份校验（同 GetStagePersist）——hidden 位置曾有升级箱→移走→普通容器占位时
    //   尺寸不符 → 不误伤 + 清残留键（wage_stage_idx_{idx}）自愈。
    public static int GetHiddenStageByIndex(int idx, GameItem box)
    {
        if (idx < 0) return -1;
        // 10-07 阶段D D-3：ns 随迁 SurvivalGlobal + 旧档兜底迁移
        int s = WageSaveStore.GetInt("SurvivalGlobal", "wage_stage_idx_" + idx, -1);
        if (s <= 0)
        {
            s = WageSaveStore.GetInt("RobinCrusoe", "wage_stage_idx_" + idx, -1);
            if (s > 0) WageSaveStore.SetInt("SurvivalGlobal", "wage_stage_idx_" + idx, s);
        }
        if (s > 0 && box != null)
        {
            var g0 = GetContainerGrid(box);
            int cw = 0, ch = 0; GetShapeWH(g0, ref cw, ref ch);
            int ts = s > MAX_STAGE ? MAX_STAGE : s;
            // 10-18 总控修订：成功匹配即清旁键（同 GetStagePersist）
            string misKey = "wage_stage_mismatch_idx_" + idx;
            if (cw == WAGE_BOX_W[ts] && ch == WAGE_BOX_H[ts])
            {
                try { if (WageSaveStore.GetInt("SurvivalGlobal", misKey, 0) != 0) WageSaveStore.SetInt("SurvivalGlobal", misKey, 0); } catch { }
                return s;
            }
            int mis = WageSaveStore.GetInt("SurvivalGlobal", misKey, 0);
            // 10-18 总控修订：升级判据=跨会话确认（会话级 _saw 防同帧凑数）
            if (mis > 0 && !_sawMismatchThisSession.Contains(misKey))
            {
                _sawMismatchThisSession.Add(misKey);
                try
                {
                    WageSaveStore.SetInt("SurvivalGlobal", "wage_stage_idx_" + idx, -1);
                    WageSaveStore.SetInt("SurvivalGlobal", misKey, -1);
                }
                catch { }
                Core.LogMsg("[容器v2] hidden尺寸不符×2(误伤防护确认): idx=" + idx + " stage=" + s + " 实际=" + cw + "x" + ch + " 期望=" + WAGE_BOX_W[ts] + "x" + WAGE_BOX_H[ts] + " → 清键自愈");
            }
            else
            {
                _sawMismatchThisSession.Add(misKey);
                try { WageSaveStore.SetInt("SurvivalGlobal", misKey, 1); } catch { }
                Core.LogMsg("[容器v2] hidden尺寸不符(误伤防护首次): idx=" + idx + " stage=" + s + " 实际=" + cw + "x" + ch + " 期望=" + WAGE_BOX_W[ts] + "x" + WAGE_BOX_H[ts] + " → 跳过不恢复(待二次确认)");
            }
            return -1;
        }
        return s;
    }
        // 10-19 P3 C组清理：SetHiddenStageByIndex() 已删（死方法零调用，派活单§六；如需恢复见 git 历史）
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
