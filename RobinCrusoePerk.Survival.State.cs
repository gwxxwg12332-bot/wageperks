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

    // ===== 三状态百分比制 + 心情（v5.7，PerkStatePersistence，runID 隔离）=====
    internal static int GetSatiety() => WageSaveStore.GetInt(PERK_ID, "sat", 100);          // 饱食 0-100
    internal static int GetThirstPct() => WageSaveStore.GetInt(PERK_ID, "thirst", 100);    // 口渴 0-100
    internal static int GetHealth() => WageSaveStore.GetInt(PERK_ID, "health", 100);       // 健康 0-100
    internal static int GetMood() => WageSaveStore.GetInt(PERK_ID, "mood", MOOD_START);    // 心情 0-100
    internal static int GetGranaryDays() => WageSaveStore.GetInt(PERK_ID, "granary", 0);   // 粮仓连续天数
    internal static int GetElevStreak() => WageSaveStore.GetInt(PERK_ID, "elevStreak", 0); // 昂扬连续天数
    internal static int GetElevCount() => WageSaveStore.GetInt(PERK_ID, "elevCount", 0);   // 昂扬累计（封顶5）
    internal static int GetStarveDays() => WageSaveStore.GetInt(PERK_ID, "starveDays", 0); // 濒饿持续（饱食<20或口渴<20）
    internal static int GetCritDays() => WageSaveStore.GetInt(PERK_ID, "critDays", 0);     // 病危持续（健康<20）
    internal static int GetThirstDeathDays() => WageSaveStore.GetInt(PERK_ID, "thirstDeath", 0); // 渴死持续（口渴<20）
    internal static void SetSatiety(int v) { WageSaveStore.SetInt(PERK_ID, "sat", v); InvalidateTradeCaches(); }
    internal static void SetThirstPct(int v) { WageSaveStore.SetInt(PERK_ID, "thirst", v); InvalidateTradeCaches(); }
    internal static void SetHealth(int v) { WageSaveStore.SetInt(PERK_ID, "health", v); InvalidateTradeCaches(); }
    internal static void SetMood(int v) { WageSaveStore.SetInt(PERK_ID, "mood", v); InvalidateTradeCaches(); }
    // 新三状态（v5.7+ 用户拍板：清洁度/睡眠/社交）
    internal static int GetClean() => WageSaveStore.GetInt(PERK_ID, "clean", CLEAN_START);       // 清洁 0-100
    internal static int GetSleep() => WageSaveStore.GetInt(PERK_ID, "sleep", SLEEP_START);       // 睡眠 0-100
    internal static int GetSocial() => WageSaveStore.GetInt(PERK_ID, "social", SOCIAL_START);    // 社交 0-100
    internal static void SetClean(int v) { WageSaveStore.SetInt(PERK_ID, "clean", v); InvalidateTradeCaches(); }
    internal static void SetSleep(int v) { WageSaveStore.SetInt(PERK_ID, "sleep", v); InvalidateTradeCaches(); }
    internal static void SetSocial(int v) { WageSaveStore.SetInt(PERK_ID, "social", v); InvalidateTradeCaches(); }

    // 09-22 新档防 default_run 残留污染：鲁滨逊开局全部持久化 key（TrySetupNewRun 18 + 运行期固定 3 + 补偿天数 cb_* 13）
    private static readonly string[] ALL_KEYS = new string[]
    {
        "robinson_hard","sat","thirst","health","blood","mood","granary","elevStreak","elevCount",
        "starveDays","thirstDeath","critDays","clean","sleep","social","nodeKey","nodeFxIdx","deals",
        "revenue","blood_rest","hbuffDay",
        "cb_eatEff","cb_thirstEff50","cb_thirstEff10","cb_wearEff","cb_drugEff","cb_antiTheft","cb_moodDamp",
        "cb_forage20","cb_sell5","cb_mood2","cb_mood3","cb_sleepR10","cb_contraEff"
    };
    internal static void CleanDefaultRunOnNewGame()
    {
        try { WageSaveStore.CleanLegacyDefaultRun(PERK_ID, ALL_KEYS); } catch { } // 2026-09-24 旧层门面已删：清理入口收拢至 WageSaveStore（清旧层 default_run 残留，属旧档卫生，新层无此问题）
    }

}
