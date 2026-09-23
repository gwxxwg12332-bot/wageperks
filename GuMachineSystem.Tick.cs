using System;
using Il2Cpp;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace JacksonPerks;

public static partial class GuMachineSystem
{
    private static void TryGuMachineTick(GameItem gu, int day)
    {
        try
        {
            var grid = GetGuGrid(gu);
            if (grid == null) return;
            int charge = RobinCrusoePerk.GetTagIntSafe(gu, GU_CHARGE_TAG);
            // 09-15 用户拍板：充能中（charge>0）舱内模组禁止移出——打 MODULE_STUCK_TAG（原生"卡住"语义：PlayerStore 可用性判定 + 卸载链 + tooltip 全拦）
            if (charge > 0) LockGuModules(grid);
            // 09-19 修复：charge=0 摘除舱内全部 STUCK_TAG——原实现只打不摘导致产出永久锁死（卡住根因）；顺带恢复存量卡住的档
            else UnlockGuModules(grid);
            if (charge < BuildConfig.GuChargeDays)
            {
                // 09-19 P1 充能天数差分：同日打烊不重复 +1（根治双挂点重复计/读档后卡住）；LAST_DAY 创建时已记，随 tag 读档保留
                int lastDay = RobinCrusoePerk.GetTagIntSafe(gu, GU_LAST_DAY_TAG);
                if (lastDay < day)
                {
                    RobinCrusoePerk.AddTagInt(gu, GU_CHARGE_TAG, 1);
                    RobinCrusoePerk.SetTagIntValue(gu, GU_LAST_DAY_TAG, day);
                }
                return; // 未满不炼
            }
            // 收集模组（排除报废 ruined——不参与炼蛊）
            var mods = new System.Collections.Generic.List<GameItem>();
            if (grid.childItems != null) foreach (var m in grid.childItems)
            {
                if (m == null) continue;
                string id = ""; try { id = m.identifier ?? ""; } catch { }
                if (id == "system_module_ruined") continue;
                // 09-19 防御性排除电池（power_source_item——若电池带 MODULE_TAG 会误入炼蛊/抽卡原料，历史反馈"炼蛊机练电池"）
                bool isBat = false; try { isBat = m.IsTag("power_source_item"); } catch { }
                if (isBat) continue;
                bool isMod = false; try { isMod = m.IsTag("MODULE_TAG") && !RobinCrusoePerk.IsExcludedModule(m); } catch { }
                if (!isMod) continue;
                mods.Add(m); // 09-19 修复：产出模组不再排除——练过的蛊可重复吞噬（原 FORGED 排除导致产出占舱又不算原料，凑不齐 2 个永不炼）
            }
            if (mods.Count < 2) return; // 满3但模组不足——保持满等放模组
            // 09-18 基底选三属性总值最高的模组（高特性优先——参考电池互吞规则；产出体 id = 该模组 id）
            GameItem baseMod = null;
            int bestScore = -1;
            foreach (var m in mods)
            {
                int s = 0;
                s += RobinCrusoePerk.GetTagIntSafe(m, "BONUS_PERCENTAGE_PERFORMANCE_INT");
                s += RobinCrusoePerk.GetTagIntSafe(m, "BONUS_PERCENTAGE_EFFICIENCY_INT");
                s += RobinCrusoePerk.GetTagIntSafe(m, "BONUS_PERCENTAGE_QUALITY_INT");
                if (s > bestScore) { bestScore = s; baseMod = m; }
            }
            if (baseMod == null) baseMod = mods[0];
            string baseId = "system_module_overclock";
            try { string bid = baseMod.identifier ?? ""; if (bid.Length > 0) baseId = bid; } catch { }
            // 三属性之和 ×1.2（上限 150）
            int perf = 0, eff = 0, qual = 0;
            foreach (var m in mods)
            {
                perf += RobinCrusoePerk.GetTagIntSafe(m, "BONUS_PERCENTAGE_PERFORMANCE_INT");
                eff += RobinCrusoePerk.GetTagIntSafe(m, "BONUS_PERCENTAGE_EFFICIENCY_INT");
                qual += RobinCrusoePerk.GetTagIntSafe(m, "BONUS_PERCENTAGE_QUALITY_INT");
            }
            int newPerf = Math.Min(BuildConfig.GuForgeCap, (int)(perf * BuildConfig.GuForgeMult / 100f));
            int newEff = Math.Min(BuildConfig.GuForgeCap, (int)(eff * BuildConfig.GuForgeMult / 100f));
            int newQual = Math.Min(BuildConfig.GuForgeCap, (int)(qual * BuildConfig.GuForgeMult / 100f));
            // 生成强化模组（基底 id 为产出体）
            GameItem result = null;
            try { result = DirectoryMaster.Item(baseId); } catch { }
            if (result == null)
            {
                // 09-19 P2 失败语义：合成失败 = 原料销毁 + 产报废模组（不留悬垂原料）；充能归零重来
                foreach (var m in mods)
                {
                    try { m.parentInventory?.Expel(m); } catch { }
                    try { m.Destroy(); } catch { }
                }
                GameItem scrap = null;
                try { scrap = DirectoryMaster.Item("system_module_ruined"); } catch { }
                if (scrap != null)
                {
                    try { RobinCrusoePerk.SetTagIntValue(scrap, "BONUS_PERCENTAGE_PERFORMANCE_INT", 0); } catch { }
                    try { RobinCrusoePerk.SetTagIntValue(scrap, "BONUS_PERCENTAGE_EFFICIENCY_INT", 0); } catch { }
                    try { RobinCrusoePerk.SetTagIntValue(scrap, "BONUS_PERCENTAGE_QUALITY_INT", 0); } catch { }
                    try { Il2Cpp.GraphUtils.TryAcceptAllMid(grid, scrap, -1); } catch { }
                }
                string fline = LangHelper.T("养蛊机炼蛊失败：投入模组报废", "Swarm Forge forging failed: input modules scrapped");
                try { StoreUIManager.Instance.Notify(fline); } catch { }
                try { var ps = PlayerStore.Instance; if (ps != null) ps.AddNightLog(fline, "#7FC97F"); } catch { } // 09-22 统一柔和绿
                Core.AddNightReportLine(fline);
                RobinCrusoePerk.AddTagInt(gu, GU_CHARGE_TAG, -3);
                return;
            }
            // 09-19 修复：产出纯含炼蛊属性——DirectoryMaster.Item 新建的原生模组自带 base 属性（overclock 原生 perf=36/eff=-44，拆包 diff 实证），
            // 直接 AddTagInt 会叠加在原生 base 上（产出=36+newPerf），再投入炼蛊时 36/-44 被 ×1.2 反复放大——"属性叠加不丢"被污染
            try { RobinCrusoePerk.SetTagIntValue(result, "BONUS_PERCENTAGE_PERFORMANCE_INT", 0); } catch { }
            try { RobinCrusoePerk.SetTagIntValue(result, "BONUS_PERCENTAGE_EFFICIENCY_INT", 0); } catch { }
            try { RobinCrusoePerk.SetTagIntValue(result, "BONUS_PERCENTAGE_QUALITY_INT", 0); } catch { }
            if (newPerf > 0) RobinCrusoePerk.AddTagInt(result, "BONUS_PERCENTAGE_PERFORMANCE_INT", newPerf);
            if (newEff > 0) RobinCrusoePerk.AddTagInt(result, "BONUS_PERCENTAGE_EFFICIENCY_INT", newEff);
            if (newQual > 0) RobinCrusoePerk.AddTagInt(result, "BONUS_PERCENTAGE_QUALITY_INT", newQual);
            try { result.EnableTag("MODULE_TAG"); } catch { } // 09-19 保险：确保产出可被下一轮收集（InitContrabandItem 清 tag 未实锤，显式补打幂等）
            try { Il2Cpp.ContrabandHelper.InitContrabandItem(result, 3); } catch { } // 09-16 高级违禁品(level 3)
            try { result.shortDescription = (result.shortDescription ?? "") + LangHelper.T("（违禁原因：炼蛊融合产物，蕴含被禁的模组融合技术）", " (Contraband: forged fusion product with outlawed module-merging tech)"); } catch { }
            // 09-19 修复：先清空原料腾格子、再入产出——原"先入产出后清空"导致产出(2×2 需 4 格)被原料占格
            // → TryAcceptAllMid(-1) 找不到空间静默失败（catch 吞掉）→ 产出丢失（用户反馈"炼蛊成功的模组消失"）
            foreach (var m in mods)
            {
                try { m.parentInventory?.Expel(m); } catch { }
                try { m.Destroy(); } catch { }
            }
            // 09-15 用户拍板：生产成果放入机器舱（玩家打开机器取出；TryAcceptAllMid 接受 GameGridInventory）
            try { Il2Cpp.GraphUtils.TryAcceptAllMid(grid, result, -1); } catch { }
            // 充能归零
            RobinCrusoePerk.AddTagInt(gu, GU_CHARGE_TAG, -3);
            // 通知（弹窗 + 晨报 + 日历 Tab）
            string name = "养蛊模组";
            try { name = result.GetDisplayName(); } catch { try { name = result.name ?? baseId; } catch { } }
            string line = LangHelper.T(
                "养蛊机炼成 " + name + " · 性能+" + newPerf + " 效率+" + newEff + " 质量+" + newQual + "（×1.2）",
                "Swarm Forge forged " + name + " · Perf+" + newPerf + " Eff+" + newEff + " Qual+" + newQual + " (x1.2)");
            try { StoreUIManager.Instance.Notify(line); } catch { }
            try { var ps = PlayerStore.Instance; if (ps != null) ps.AddNightLog(line, "#7FC97F"); } catch { } // 09-19 亮绿(#00FF00)改柔和绿——刺眼反馈
            Core.AddNightReportLine(line);
        }
        catch { }
    }
    private static System.Collections.Generic.List<GameItem> FindAiGenerators()
    {
        var result = new System.Collections.Generic.List<GameItem>();
        try
        {
            if (EmporiumEntry.Instance != null)
            {
                var all = EmporiumEntry.Instance.GetAllItems();
                if (all != null) foreach (var it in all) { if (it != null && it.identifier == AI_GENERATOR_ID) result.Add(it); }
            }
        }
        catch { }
        return result;
    }
    private static void TryAiGeneratorTick(GameItem gen, int day)
    {
        try
        {
            int lastDay = RobinCrusoePerk.GetTagIntSafe(gen, AI_LAST_DAY_TAG);
            if (lastDay == day) return; // 每天 1 次
            var grid = GetGuGrid(gen);
            if (grid == null) { return; }
            // 收集模组 + 找保护器
            var mods = new System.Collections.Generic.List<GameItem>();
            GameItem protector = null;
            if (grid.childItems != null) foreach (var m in grid.childItems)
            {
                if (m == null) continue;
                string id = ""; try { id = m.identifier ?? ""; } catch { }
                if (id == PROTECTOR_ID) { protector = m; continue; }
                if (id == "system_module_ruined") continue;
                bool isBat = false; try { isBat = m.IsTag("power_source_item"); } catch { } // 09-19 防御性排除电池
                if (isBat) continue;
                bool isMod = false; try { isMod = m.IsTag("MODULE_TAG") && !RobinCrusoePerk.IsExcludedModule(m); } catch { }
                if (isMod) mods.Add(m);
            }
            if (mods.Count < 2) return; // 模组不足不抽（不记日——补料后当天可抽；抽卡成功/失败后才记日防同日重复）
            bool safe = protector != null;
            if (safe)
            {
                // 阉割版：消耗 1 个保护器
                try { protector.parentInventory?.Expel(protector); } catch { }
                try { protector.Destroy(); } catch { }
            }
            // 抽卡：不稳定版 50% 成功 / 50% 失败；阉割版 100%
            bool success = true;
            if (!safe)
            {
                int roll = 0; try { roll = Core.Rng.Next(100); } catch { roll = 0; }
                success = roll < BuildConfig.AiSuccessPct;
            }
            if (success)
            {
                // 09-15 用户拍板：生成器产出"不稳定AI模组"实物（不是机器属性吸收）——属性 = 投入模组之和，上限 cap
                int cap = safe ? BuildConfig.AiStableCap : BuildConfig.AiUnstableCap;
                int perf = 0, eff = 0, qual = 0;
                foreach (var m in mods)
                {
                    perf += RobinCrusoePerk.GetTagIntSafe(m, "BONUS_PERCENTAGE_PERFORMANCE_INT");
                    eff += RobinCrusoePerk.GetTagIntSafe(m, "BONUS_PERCENTAGE_EFFICIENCY_INT");
                    qual += RobinCrusoePerk.GetTagIntSafe(m, "BONUS_PERCENTAGE_QUALITY_INT");
                }
                int newP = Math.Min(cap, perf);
                int newE = Math.Min(cap, eff);
                int newQ = Math.Min(cap, qual);
                long sumVal = 0; foreach (var mv in mods) { try { sumVal += mv.unitValue; } catch { } }
                GameItem result = null;
                try { result = DirectoryMaster.Item(AI_MODULE_ID); } catch { }
                if (result != null)
                {
                    // 09-19 吸取养蛊机教训：清原生 base（保险，防注册自带属性混入）+ 补 MODULE_TAG（防不可收集）
                    try { RobinCrusoePerk.SetTagIntValue(result, "BONUS_PERCENTAGE_PERFORMANCE_INT", 0); } catch { }
                    try { RobinCrusoePerk.SetTagIntValue(result, "BONUS_PERCENTAGE_EFFICIENCY_INT", 0); } catch { }
                    try { RobinCrusoePerk.SetTagIntValue(result, "BONUS_PERCENTAGE_QUALITY_INT", 0); } catch { }
                    if (newP > 0) RobinCrusoePerk.AddTagInt(result, "BONUS_PERCENTAGE_PERFORMANCE_INT", newP);
                    if (newE > 0) RobinCrusoePerk.AddTagInt(result, "BONUS_PERCENTAGE_EFFICIENCY_INT", newE);
                    if (newQ > 0) RobinCrusoePerk.AddTagInt(result, "BONUS_PERCENTAGE_QUALITY_INT", newQ);
                    try { result.EnableTag("MODULE_TAG"); } catch { }
                    try { result.unitValue = (int)sumVal; result.unitBaseValue = (int)sumVal; } catch { } // 价值=吞噬原料之和
                }
                // 09-19 吸取养蛊机教训：先清空原料腾格子、再入产出——原"先入产出后清空"导致 2×2 产出被原料占格
                // → TryAcceptAllMid(-1) 静默失败（catch 吞掉）→ 产出丢失（"炼蛊成功的模组消失"同根因）
                foreach (var m in mods)
                {
                    try { m.parentInventory?.Expel(m); } catch { }
                    try { m.Destroy(); } catch { }
                }
                if (result != null)
                {
                    // 实物放入生成器舱
                    try { Il2Cpp.GraphUtils.TryAcceptAllMid(grid, result, -1); } catch { }
                }
                string modeName = safe ? LangHelper.T("阉割版", "Stable") : LangHelper.T("不稳定版", "Unstable");
                string line = LangHelper.T(
                    "AI 生成器产出 不稳定AI模组 · 性能+" + newP + " 效率+" + newE + " 质量+" + newQ + "（" + modeName + "）",
                    "Neural Generator produced Unstable AI Module · Perf+" + newP + " Eff+" + newE + " Qual+" + newQ + " (" + modeName + ")");
                try { StoreUIManager.Instance.Notify(line); } catch { }
                try { var ps = PlayerStore.Instance; if (ps != null) ps.AddNightLog(line, "#7FC97F"); } catch { } // 09-19 成功色统一柔和绿（原金色）
                Core.AddNightReportLine(line);
            }
            else
            {
                // 09-15 用户拍板：30% 失败 = 投入模组变报废模组（生成器保留）
                foreach (var m in mods)
                {
                    try { m.parentInventory?.Expel(m); } catch { }
                    try { m.Destroy(); } catch { }
                }
                GameItem scrap = null;
                try { scrap = DirectoryMaster.Item("system_module_ruined"); } catch { }
                if (scrap != null)
                {
                    // 09-19 P2 失败语义：报废模组三属性清零（不参与后续吞噬、可卖）
                    try { RobinCrusoePerk.SetTagIntValue(scrap, "BONUS_PERCENTAGE_PERFORMANCE_INT", 0); } catch { }
                    try { RobinCrusoePerk.SetTagIntValue(scrap, "BONUS_PERCENTAGE_EFFICIENCY_INT", 0); } catch { }
                    try { RobinCrusoePerk.SetTagIntValue(scrap, "BONUS_PERCENTAGE_QUALITY_INT", 0); } catch { }
                    try { Il2Cpp.GraphUtils.TryAcceptAllMid(grid, scrap, -1); } catch { }
                }
                string line = LangHelper.T(
                    "AI 生成器不稳定爆发：投入模组报废",
                    "Neural Generator unstable burst: input modules scrapped");
                try { StoreUIManager.Instance.Notify(line); } catch { }
                try { var ps = PlayerStore.Instance; if (ps != null) ps.AddNightLog(line, "#7FC97F"); } catch { } // 09-22 统一柔和绿
                Core.AddNightReportLine(line);
            }
            RobinCrusoePerk.AddTagInt(gen, AI_LAST_DAY_TAG, day);
        }
        catch { }
    }
}
