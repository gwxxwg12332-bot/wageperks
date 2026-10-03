using System;
using System.Collections.Generic;

namespace WageSurvival;

// ============================================================
// SurvivalMigrate（2026-10-03 阶段C：旧档一次性迁移 RobinCrusoe → SurvivalGlobal，幂等标记）
// 触发：WageAPI.WageSaveStore.GameLoaded（读档数据就绪后）。
// 幂等：SurvivalGlobal.migrated == 1 即跳过；无旧键也标记（防重复扫描）。
// 旧键保留不删（WagePerks 阶段D 改指 SurvivalGlobal 前其残留挂点仍读 RobinCrusoe；删除动作留到确认双模稳定后）。
// ============================================================
internal static class SurvivalMigrate
{
    private const string MIGRATED_KEY = "migrated";

    internal static void OnStoreGameLoaded()
    {
        try
        {
            if (WageAPI.WageSaveStore.GetInt("SurvivalGlobal", MIGRATED_KEY, 0) != 0)
            {
                Core.LogMsg("[WageSurvival] 旧档迁移已完成，跳过");
                return;
            }
            var keys = WageAPI.WageSaveStore.EnumerateKeys("RobinCrusoe");
            if (keys == null || keys.Count == 0)
            {
                WageAPI.WageSaveStore.SetInt("SurvivalGlobal", MIGRATED_KEY, 1);
                Core.LogMsg("[WageSurvival] 无旧档 RobinCrusoe 数据，标记迁移完成");
                return;
            }
            int n = 0;
            foreach (string k in keys)
            {
                if (string.IsNullOrEmpty(k)) continue;
                WageAPI.WageSaveStore.SetString("SurvivalGlobal", k, WageAPI.WageSaveStore.GetString("RobinCrusoe", k, ""));
                n++;
            }
            WageAPI.WageSaveStore.SetInt("SurvivalGlobal", MIGRATED_KEY, 1);
            Core.LogMsg($"[WageSurvival] 旧档迁移完成: {n} 键 RobinCrusoe→SurvivalGlobal");
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival] 旧档迁移异常: " + ex.Message); }
    }
}
