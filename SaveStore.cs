using System;
using Il2Cpp;

namespace WageSurvival;

// ============================================================
// SaveStore 门面（2026-10-03 阶段C：三副本删除，转发 WageAPI.WageSaveStore，命名空间隔离 SurvivalGlobal）
// 现有调用点零改动——签名与原实现一致（2 参 key）。
// 落盘/读档统一归 WageAPI（SaveGame/EndDay Postfix + LoadGame Postfix + 帧轮询）。
// 独立 PERK_ID="SurvivalGlobal"（派活单要求；旧档 RobinCrusoe→SurvivalGlobal 迁移见 SurvivalMigrate）。
// ============================================================
internal static class SaveStore
{
    internal const string PERK_ID = "SurvivalGlobal";

    internal static int GetInt(string key, int defaultValue = 0)
        => WageAPI.WageSaveStore.GetInt(PERK_ID, key, defaultValue);

    internal static void SetInt(string key, int value)
        => WageAPI.WageSaveStore.SetInt(PERK_ID, key, value);

    internal static string GetString(string key, string defaultValue = "")
        => WageAPI.WageSaveStore.GetString(PERK_ID, key, defaultValue);

    internal static void SetString(string key, string value)
        => WageAPI.WageSaveStore.SetString(PERK_ID, key, value);

    internal static bool GetBool(string key, bool defaultValue = false)
        => WageAPI.WageSaveStore.GetBool(PERK_ID, key, defaultValue);

    internal static void SetBool(string key, bool value)
        => WageAPI.WageSaveStore.SetBool(PERK_ID, key, value);

    internal static float GetFloat(string key, float defaultValue = 0f)
        => WageAPI.WageSaveStore.GetFloat(PERK_ID, key, defaultValue);

    internal static void SetFloat(string key, float value)
        => WageAPI.WageSaveStore.SetFloat(PERK_ID, key, value);

    internal static bool HasKey(string key)
        => WageAPI.WageSaveStore.HasKey(PERK_ID, key);

    // 统一落盘归 WageAPI（priority=0 最后跑）；此处转发幂等（双调无害）
    internal static void Flush()
        => WageAPI.WageSaveStore.Flush();

    // 读档由 WageAPI 统一轮询承载；转发幂等（仅设挂起标志，不重复读文件）
    internal static void LoadGame()
        => WageAPI.WageSaveStore.LoadIfPending();

    // 只设默认值，不覆盖已有值
    internal static void InitDefaults()
    {
        if (!HasKey("sat")) SetInt("sat", 100);
        if (!HasKey("thirst")) SetInt("thirst", 100);
        if (!HasKey("health")) SetInt("health", 100);
        if (!HasKey("mood")) SetInt("mood", 50);
        if (!HasKey("clean")) SetInt("clean", 100);
        if (!HasKey("sleep")) SetInt("sleep", 100);
        if (!HasKey("social")) SetInt("social", 50);
    }

    // 新档：只清自己的命名空间 + 设默认（不碰其他 mod 数据；WagePerks 的 ResetForNewRun 清全量，顺序竞态由读默认兜底）
    internal static void PostfixStartNewGame()
    {
        try
        {
            Core.LogMsg("[WageSurvival] StartNewGame——清 SurvivalGlobal + 设默认值");
            WageAPI.WageSaveStore.ClearNamespace(PERK_ID);
            SurvivalFood.ClearMemBlood(); // 10-05 拆包实锤：血量跨档串值=内存缓存残留（档2继承档1 _memBlood），新档清缓存
            SurvivalFood._pendingSkipDays = 0;
            SurvivalFood._jumping = false;
            SurvivalFood._jumpStartDay = -1;
            InitDefaults();
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival] PostfixStartNewGame 异常: " + ex.Message); }
    }
}
