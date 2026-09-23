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
    internal const string PERK_ID = "RobinCrusoe";
    internal const int START_TYPE = 14;

    // 新三状态（用户拍板 09-09：清洁度/睡眠/社交）
    internal static int CLEAN_START => BuildConfig.CleanStart;      // 清洁度初始（CFG 可调）
    internal static int SLEEP_START => BuildConfig.SleepStart;      // 睡眠初始（CFG 可调）
    internal static int SOCIAL_START => BuildConfig.SocialStart;      // 社交初始（CFG 可调）
    internal static int DAILY_CLEAN_LOSS => BuildConfig.CleanDailyLoss;   // 清洁每日衰减（CFG 可调）
    internal static int DAILY_SLEEP_GAIN => BuildConfig.DailySleepGain;  // 睡眠打烊（CFG 可调）
    internal static int SLEEP_SCAV_LOSS => BuildConfig.SleepScavLoss;    // 外出拾荒睡眠 -%（CFG 可调）
    internal static int DAILY_SOCIAL_GAIN => BuildConfig.DailySocialGain;  // 社交每日 +（开店接待，CFG 可调）
    internal static int DAILY_SOCIAL_LOSS => BuildConfig.DailySocialLoss;         // 濒危分界线
    internal static int MOOD_START => BuildConfig.MoodStart;        // 心情初始值（CFG 可调）
    internal static int DAILY_SAT_LOSS => BuildConfig.DailySatLoss;    // 饱食每日 -%（CFG 可调）
    internal static int DAILY_THIRST_LOSS => BuildConfig.DailyThirstLoss; // 口渴每日 -%（CFG 可调）
    internal static int DAILY_HEALTH_GAIN => BuildConfig.DailyHealthGain;       // 粮仓连续天数（CFG 可调）
    internal static int ELEV_EVERY => BuildConfig.ElevEvery;         // 昂扬结算间隔（CFG 可调）
    internal static int ELEV_MAX => BuildConfig.ElevMax;           // 昂扬累计封顶（CFG 可调）
    internal static int MOOD_UP => BuildConfig.MoodUp;            // 三项全好每日+（CFG 可调）
    internal static int MOOD_DOWN => BuildConfig.MoodDown;


    // ===== Z 键调出/关闭状态面板（用户拍板；特性界面/主菜单不响应，硬约束守护）=====
    // 09-12 实锤：InputActionManager.Update 每帧可被多次调用（多实例/多Patch）→ 必须同帧去重，否则一次按键开→关双翻转，面板打不开
    private static int _zKeyFrame = -1;
    private static bool _autoPopup = true;  // 09-22 新增：自动弹面板开关（按 Z 切换）
    internal static void HandleHotkeys()
    {
        try
        {
            // 09-22 改：按 Z 只打开面板，不碰开关
            if (!UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Z)) return;
            int _zFrame = UnityEngine.Time.frameCount;
            if (_zFrame == _zKeyFrame) return;
            _zKeyFrame = _zFrame;
            if (!IsActive()) return;
            if (!IsActive()) return;
            var mgr = Il2Cpp.CustomUIManager.Instance;
            if (mgr != null && mgr.IsOpen("rc_status")) { mgr.CloseWindow("rc_status"); } else { RefreshStatusPanel(force: true); }
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] HandleHotkeys 异常: " + ex.Message); }
    }
    // ===== 激活判定（职业，双来源）=====
    internal static bool IsActive()
    {
        try
        {
            var ps = PlayerStore.Instance;
            if (ps != null && (int)ps.startType == START_TYPE) return true;
            var ng = Il2Cpp.NewGameData.Instance;
            if (ng != null && (int)ng.startType == START_TYPE) return true;
        }
        catch (System.Exception ex) { Core.LogMsg("[RobinCrusoePerk.Survival] 异常: " + ex.Message); }
        return false;
    }












    // ===== 双击食用（吃一口/喝一口/吃药治病）=====


}
