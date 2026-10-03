using System;

namespace WagePerks;

// ============================================================
// WageSaveStore 门面（2026-10-03 阶段B：提炼至 WageAPI 后旧类保留、方法体转发）
// 评审补丁：删 Wage's Perks 自己的落盘挂点（PatchRegistryTable SaveStore 条目已删，防双落盘），
// 统一落盘通道归 WageAPI（SaveGame/EndDay Postfix，priority=0 最后跑）。
// 现有调用点零改动——签名与原实现完全一致。
// ============================================================
public static class WageSaveStore
{
    public static string GetString(string ns, string key, string def = "")
        => WageAPI.WageSaveStore.GetString(ns, key, def);

    public static void SetString(string ns, string key, string value)
        => WageAPI.WageSaveStore.SetString(ns, key, value);

    public static int GetInt(string ns, string key, int def = 0)
        => WageAPI.WageSaveStore.GetInt(ns, key, def);

    public static void SetInt(string ns, string key, int value)
        => WageAPI.WageSaveStore.SetInt(ns, key, value);

    public static float GetFloat(string ns, string key, float def = 0f)
        => WageAPI.WageSaveStore.GetFloat(ns, key, def);

    public static void SetFloat(string ns, string key, float value)
        => WageAPI.WageSaveStore.SetFloat(ns, key, value);

    public static bool GetBool(string ns, string key, bool def = false)
        => WageAPI.WageSaveStore.GetBool(ns, key, def);

    public static void SetBool(string ns, string key, bool value)
        => WageAPI.WageSaveStore.SetBool(ns, key, value);

    public static void ClearNamespace(string ns)
        => WageAPI.WageSaveStore.ClearNamespace(ns);

    public static bool HasKey(string ns, string key)
        => WageAPI.WageSaveStore.HasKey(ns, key);

    public static void ResetForNewRun()
        => WageAPI.WageSaveStore.ResetForNewRun();

    public static void CleanLegacyDefaultRun(string perkId, string[] keys)
        => WageAPI.WageSaveStore.CleanLegacyDefaultRun(perkId, keys);

    public static string Serialize()
        => WageAPI.WageSaveStore.Serialize();

    public static void Deserialize(string content)
        => WageAPI.WageSaveStore.Deserialize(content);

    public static bool ValidateRunId()
        => WageAPI.WageSaveStore.ValidateRunId();

    public static bool ValidateAfterLoad()
        => WageAPI.WageSaveStore.ValidateAfterLoad();

    public static string GetRunIdRaw()
        => WageAPI.WageSaveStore.GetRunIdRaw();

    public static void Flush()
        => WageAPI.WageSaveStore.Flush();

    public static void PostfixSaveGame()
        => WageAPI.WageSaveStore.PostfixSaveGame();

    public static void PostfixEndDay()
        => WageAPI.WageSaveStore.PostfixEndDay();

    public static void OnLoadGame()
        => WageAPI.WageSaveStore.OnLoadGame();

    public static void LoadIfPending()
        => WageAPI.WageSaveStore.LoadIfPending();

    public static bool LoadComplete => WageAPI.WageSaveStore.LoadComplete;

    public static int Count => WageAPI.WageSaveStore.Count;

    public static bool HasPendingChange => WageAPI.WageSaveStore.HasPendingChange;

    /// <summary>读档数据就绪事件透传（Wage's Perks 侧：NotifyGameLoaded + 重洗白扫描）。</summary>
    public static event Action GameLoaded
    {
        add { WageAPI.WageSaveStore.GameLoaded += value; }
        remove { WageAPI.WageSaveStore.GameLoaded -= value; }
    }
}
