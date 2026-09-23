using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace JacksonPerks;

// ============================================================
// 蛙娘系统（09-21 开工，话术 v9 拆包回填 9 项）
// 拍板：实体占地 2×3，全局常驻（不选任何特性也出现）
// 阶段 1：实体注册 + 六维状态 + 常驻面板 + 每日衰减 + 双击面板
// 阶段 2+：喂食/照顾好感、在场增益（预算×4+议价+50）、自动叫客+治安预判、
//          偷钱循环+自主偷拿、好物+销赃+跑路回归（后续迭代）
// ============================================================
public static partial class WageGirlSystem
{
    public const string ENTITY_ID = "wage_girl";
    public const string ICON_ATLAS = "custom_atlas";
    public const string ICON = "wage_girl_icon";
    public const string TAG = "WAGE_GIRL_TAG";
    private const string NS = "wage_girl";
    // 09-23 CFG 化：六维初始/上限/每日衰减/好感上限/偷钱周期/零花钱档位 全部移到 BuildConfig（WageGirl* 系列）

    private const string K_SAT = "sat", K_TH = "th", K_HEALTH = "health", K_MOOD = "mood", K_CLEAN = "clean", K_SLEEP = "sleep", K_SLEEP_DEBT = "sleepDebt";
    private const string K_AFF = "affection", K_LAST_STEAL = "lastStealDay", K_LEAVE = "leaveDay", K_STARVE = "starveStreak";
    private const string K_EXIST = "exists";
    private const string K_STEAL_AMT = "lastStealAmount"; // 上次偷钱额（回归带物比例用）
    private const string K_LAST_GIFT = "lastGiftDay";     // 好物周期（阶段 6）
    private const string K_FENCE_AMT = "fenceAmount";     // 待销赃累计价值（喂入违禁品累加，点「销赃」才带走）
    private const string K_FENCE_PENDING = "fencePending"; // 本次销赃额（点击销赃时锁定，回归后 FenceReturn 读）
    private const string K_FENCE_CAT = "fenceCat";     // 销赃带回类别 0=随机 1=食物饮品 2=日用品 3=武器工具（面板按钮循环切换）
    private const string K_WASH_MODE = "washMode";
    private const string K_ALLOWANCE_COUNT = "allowanceCount";  // 09-23 新增：今天已给几次零花钱（前三次加好感）      // 09-23 新增：违禁品处理模式 0=洗白 1=销赃     // 销赃带回类别 0=随机 1=食物饮品 2=日用品 3=武器工具（面板按钮循环切换）
    private const string K_LEAVE_REASON = "leaveReason";  // 消失原因 0=偷钱 1=销赃 2=跑路（阶段 6）
	private const string K_SAVINGS = "savings";       // 小金库（跑腿费存起来）
    // v3 喂钱
    private const string K_ALLOWANCE = "allowance";       // 零花钱池（随档）
    private const int K_LEAVE_REASON_FEED = 3;             // 外出原因 3 = 喂钱逛街
    private static int _allowanceSel = 100;                // 档位循环状态（初始档 = BuildConfig.WageGirlAllowanceSteps[0]）


    static WageGirlSystem()
    {
        // 09-19 删除占位图标：用 13 状态动画帧
    }

    // ===================== 状态读写（内存优先 + 打烊落盘） =====================
    // ===== 09-23 统一读写层：所有蛙娘状态只走这两个方法，禁止直接调 PerkStatePersistence =====
    // 规则：
    //   GetStat(key)      → 优先读 _memStats，miss 读 PlayerPrefs（默认 0）
    //   GetStat(key, def) → 同上，miss 用 def 做默认值
    //   SetStat(key, v)   → 只写 _memStats，PostfixSaveGame 统一落盘
    // 这样读写永远一致：写进内存，读也从内存读，不会出现"写了但读到旧值"的 bug
    private static readonly Dictionary<string, int> _memStats = new Dictionary<string, int>();

    internal static int GetStat(string k) => GetStat(k, 0);
    internal static int GetStat(string k, int def) {
        try {
            if (_memStats.TryGetValue(k, out int v)) return v;
            // 09-23 阶段1：新层（WageSaveStore per-slot 文件）优先
            if (WageSaveStore.HasKey(NS, k)) {
                int nv = WageSaveStore.GetInt(NS, k, def);
                _memStats[k] = nv;
                return nv;
            }
            // 阶段1：旧层兼容已下沉进 WageSaveStore（HasKey/GetInt 自动认旧层并回填），
            // 此处不可达；key 全层缺失时直接返回默认值，不再把默认值物化进存储
            return def;
        } catch { return def; }
    }
    internal static void SetStat(string k, int v) {
        try {
            // 09-23 统一 clamp：六维 0-WageGirlStatMax（防止吃饭/日用品+15 超过上限；上限 CFG 可调）
            if (k == K_SAT || k == K_TH || k == K_HEALTH || k == K_MOOD || k == K_CLEAN || k == K_SLEEP)
                v = Math.Max(0, Math.Min(BuildConfig.WageGirlStatMax, v));
            _memStats[k] = v;
        } catch { }
    }
private static bool WasFedToday() { try { return GetStat("lastFedDay", 0) == CurrentDay(); } catch { return false; } }
    internal static bool Exists() {
    // 09-20 修：实体优先——天然随档，免疫 default_run 残留（根治连续开新档不出现）
    // 1) 实体在 → 存在
    try { if (ExistsInScene()) return true; } catch { }
    // 2) 跑路/离开中 → 视为存在，不补发
    try { if (GetStat(K_LEAVE) > CurrentDay()) return true; } catch { }
    // 3) 其余 → 不存在（补发）——忽略 K_EXIST 残留
    return false;
}

    internal static void SetExists(bool v) { try { SetStat(K_EXIST, v ? 1 : 0); } catch { } }

    // 蛙娘全部持久化 key（清 default_run 残留用）
    private static readonly string[] ALL_KEYS = new string[]
    {
        K_SAT, K_TH, K_HEALTH, K_MOOD, K_CLEAN, K_SLEEP, K_SLEEP_DEBT, K_AFF, K_LAST_STEAL, K_LEAVE,
        K_STARVE, K_EXIST, K_STEAL_AMT, K_LAST_GIFT, K_FENCE_AMT, K_FENCE_PENDING, K_FENCE_CAT, K_LEAVE_REASON
    };

    // 09-22 新档防串档：清 default_run 的蛙娘残留（A 档开局 runID 空时写的一次性 key 残留 → 新档误读误判）
    internal static void CleanDefaultRunOnNewGame()
    {
        try { PerkStatePersistence.CleanDefaultRun(NS, ALL_KEYS); } catch { }
    }

    // ===================== 阶段 5：偷钱循环 + 自主偷拿 + 回归（话术 v9） =====================
    private static int CurrentDay()
    {
        try { return Il2Cpp.StoreStation.GetDayCounter(); } catch { return 1; }
    }

    // 夜报三件套（09-17 统一规范：原生 AddNightLog + mod 队列 + 弹窗）
    // 09-22 门面模式：内部调 NotifyHelper.NightLog
    private static void ReportLine(string line)
    {
        try { NotifyHelper.NightLogRaw(line); } catch { }
    }

    private static void ModCashN(int n)
    {
        try { var ps = Il2Cpp.PlayerStore.Instance; if (ps != null) ps.ModCash(n); } catch { }
    }

    // ApplyAnimationFrame Prefix：蛙娘替换帧（美术方案——原生 Tick 调此方法时替换；帧索引由 OnUpdateTick 推进）
    // 读档后一次性重置（OnGameLoadedNormal hook）——不每帧调，避免闪烁/拖不动
    
            // 09-20 修：StartNewGame Postfix——GameMaster.NewGame从没执行，挪到这里
        public static void PostfixStartNewGame()
        {
            try {
                CleanDefaultRunOnNewGame();
                ResetForNewGame();
                _memStats.Clear(); // 防连续开新档进程内残留
                WageSaveStore.ResetForNewRun(); // 09-23 阶段1：清统一存储层内存 + pending 文件残留（防新档读到上一档残值）
            } catch { }
        }

        // 09-20 修：打烊 SaveGame 时把内存缓存落盘
        // 09-23 阶段1：落盘目标从 PlayerPrefs 改为 WageSaveStore per-slot 文件（路线 B2）
        public static void PostfixSaveGame()
        {
            try {
                foreach (var kv in _memStats)
                    WageSaveStore.SetInt(NS, kv.Key, kv.Value);
                WageSaveStore.Flush(); // 一次性写文件（原子替换，防强退写坏）
            } catch { }
        }

        // 09-20 修：新档硬重置蛙娘状态（根治初次偷拿不触发——lastStealDay残留）
        internal static void ResetForNewGame()
        {
            try {
                // 清 default_run 残留
                SetStat(K_LAST_STEAL, 0);
                SetStat(K_EXIST, 0);
                SetStat(K_LEAVE, 0);
                // 六维重置为初始值（CFG：WageGirlStatInit）
                SetStat("sat", BuildConfig.WageGirlStatInit);
                SetStat("th", BuildConfig.WageGirlStatInit);
                SetStat("health", BuildConfig.WageGirlStatInit);
                SetStat("mood", BuildConfig.WageGirlStatInit);
                SetStat("clean", BuildConfig.WageGirlStatInit);
                SetStat("sleep", BuildConfig.WageGirlStatInit);
                // 好感重置
                SetAffection(0);
            } catch { }
        }

    internal static void ClearMemStats() { try { _memStats.Clear(); } catch { } }

    public static void OnGameLoadedReset()
    {
        try {
            _memStats.Clear(); // 09-20 修：读档清内存缓存——下次GetStat自动从Prefs重载存档值
            _cachedGirlItem = null; _cacheRefreshFrames = 0; _curState = ""; _frameIndex = 0; _frameTimer = 0f;
            try { EnsureSprites(); } catch { } // 读档后确保动画帧已加载
            // 09-23 修：兜底校验——K_LEAVE 未到回归日 → 强制删实体
            try {
                int leaveDay = GetStat(K_LEAVE);
                if (leaveDay > 0 && CurrentDay() < leaveDay) {
                    RemoveGirlFromScene();
                }
            } catch { }
        } catch { }
    }
}
