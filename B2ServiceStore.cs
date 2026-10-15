using System;

namespace WagePerks;

// ============================================================
// B2 蛙哥服务面板 · 随档数据层（2026-10-13 B2 派活单）
// 所有购买/升级/消除/吞噬季状态存 WageSaveStore（runID 分区自动隔离新档；
// ResetForNewRun 清 _mem → 新档无残留）。旧档无键=默认值，不崩（验收·存档兼容）。
// 键约定（ns="b2"）：
//   owned_<perkId>    = int 等级（正面/零价：1=已购 2=2级 3=3级；负面消除：-1）
//   cannibal_owned    = 1 已购买开启吞噬季
//   cannibal_mode     = 0 单次日期 / 1 周期（默认周期，对齐原版每N天语义）
//   cannibal_day      = 单次模式触发日（第N天）
//   cannibal_interval = 周期模式间隔（每N天）
//   cannibal_last     = 上次触发起始日（防同日重复；暂未用，保留）
// ============================================================
internal static class B2ServiceStore
{
    private const string NS = "b2";

    // ---- 特性购买 / 升级 / 消除 ----
    internal static int GetLevel(string perkId)
        => WageSaveStore.GetInt(NS, "owned_" + perkId, 0);

    /// <summary>面板已购（正/零价）或已消除（负）——消除用独立标记，不影响正面。</summary>
    internal static bool IsOwned(string perkId)
        => GetLevel(perkId) >= 1;

    /// <summary>负面已消除（owned_<id> = -1）。</summary>
    internal static bool IsEliminated(string perkId)
        => GetLevel(perkId) == -1;

    internal static void SetLevel(string perkId, int lv)
        => WageSaveStore.SetInt(NS, "owned_" + perkId, lv);

    internal static void Eliminate(string perkId)
        => WageSaveStore.SetInt(NS, "owned_" + perkId, -1);

    /// <summary>
    /// 有效等级（阶段1 统一读取，兼容三来源）：
    ///   面板购/升级 → 1/2/3；开局已选（B2 无键但激活）→ 1 基础效果；已消除 → 0。
    /// 规格定稿 V3 §3「等级语义」：0 未购 / 1 基础效果 / 2 增强 / 3 进阶。
    /// </summary>
    internal static int GetEffectLevel(string perkId)
    {
        int lv = GetLevel(perkId);
        if (lv >= 1) return lv;
        if (lv < 0) return 0; // 已消除
        return Core.PerkActive(perkId) ? 1 : 0; // 开局已选=基础效果 Lv1
    }

    // ---- 吞噬季 ----
    internal static bool IsCannibalOwned
        => WageSaveStore.GetInt(NS, "cannibal_owned", 0) == 1;

    internal static void SetCannibalOwned(bool v)
        => WageSaveStore.SetInt(NS, "cannibal_owned", v ? 1 : 0);

    internal static int CannibalMode
        => WageSaveStore.GetInt(NS, "cannibal_mode", 1);

    internal static void SetCannibalMode(int m)
        => WageSaveStore.SetInt(NS, "cannibal_mode", m);

    internal static int CannibalDay
        => WageSaveStore.GetInt(NS, "cannibal_day", 0);

    internal static void SetCannibalDay(int d)
        => WageSaveStore.SetInt(NS, "cannibal_day", d);

    internal static int CannibalInterval
        => WageSaveStore.GetInt(NS, "cannibal_interval", 10);

    internal static void SetCannibalInterval(int n)
        => WageSaveStore.SetInt(NS, "cannibal_interval", n);

    internal static int CannibalLast
        => WageSaveStore.GetInt(NS, "cannibal_last", -1);

    internal static void SetCannibalLast(int day)
        => WageSaveStore.SetInt(NS, "cannibal_last", day);
}
