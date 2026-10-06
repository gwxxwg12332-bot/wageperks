using System;
using HarmonyLib;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Il2Cpp;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace WageSurvival;

// ===== 10-07 鲁滨逊完全归 WS WS-3：拾荒域 5 方法搬迁（WP RobinCrusoePerk.Scavenge.cs 同源，方法体搬迁非重写）=====
// 适配标注：
//   - GetScavCap 去掉 HardMode/LuckScout 分支（WS 无 BuildConfig/LuckScoutPerk——双装时 LuckScout 加成由 WP LuckScout 侧挂点（已排除鲁滨逊）负责；
//     单装 WS 无 LuckScout 特性=无加成；回执标注：双装鲁滨逊档 HardMode+捡漏直觉 拾荒+10 丢失=小回归）
//   - SLEEP_SCAV_LOSS=15 / CleanScavCost=2 固定常量（WP BuildConfig 配置未迁——后续可配置化 FxNum）
internal static partial class SurvivalFood
{
    // 拾荒 5（WP Scavenge.cs:28/:39/:49/:91/:142 同源——门控 IsActive=WS 全局）
    public static void PostfixCanScavenge(ref bool __result)
    {
        try
        {
            if (!IsActive() || !SurvivalTrade.IsRobinsonRun()) return;
            if (IsBloodWeak() || IsForcedRest()) { __result = false; return; }
            __result = GetScavAttempts() < GetScavCap();
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival.Scavenge] 异常: " + ex.Message); }
    }
    public static void PostfixGetMaxScavAttempts(ref int __result)
    {
        try
        {
            if (!IsActive() || !SurvivalTrade.IsRobinsonRun()) return;
            if (IsBloodWeak() || IsForcedRest()) { __result = 0; return; }
            __result = GetScavCap();
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival.Scavenge] 异常: " + ex.Message); }
    }
    public static void PostfixGetScavTimeLeft(ref int __result)
    {
        try
        {
            if (!IsActive() || !SurvivalTrade.IsRobinsonRun()) return;
            if (IsBloodWeak() || IsForcedRest()) { __result = 0; return; }
            __result = Math.Max(0, GetScavCap() - GetScavAttempts());
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival.Scavenge] 异常: " + ex.Message); }
    }
    private static int GetScavCap()
    {
        try
        {
            if (IsWounded() && !IsWoundStable()) return 0;
            int cap = 5 + GetMoodScavBonus();
            // 10-07 搬迁适配：HardMode/LuckScout +10 分支去除（WS 无 BuildConfig/LuckScoutPerk——双装由 WP LuckScout 挂点负责，回执标注）
            if (IsHomebrewWineBuffActive()) cap += 1;
            cap = Math.Max(1, cap);
            return cap;
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival.Scavenge] 异常: " + ex.Message); }
        return 5;
    }
    private static readonly string[] SCROUNGE_FOODS = {
        "morsel", "small_morsel", "cat_bar", "processed_meat", "small_raw_meat", "raw_meat",
        "processed_cheese", "cup_noodle", "processed_juice", "li_eat_snackbar", "processed_milk"
    };
    private static readonly string[] SCROUNGE_MEDS = {
        "bandage_item", "hemostatic_bandage_item", "topical_bandage_item", "phagimycin_pill",
        "med_bottle_blue", "med_bottle_red", "salve", "blood_bag"
    };
    // 拾荒 60% 额外掉落 1 件官方食物/药 + 哨兵 1% 正品免疫宁（10-06 用户拍板哨兵门控 1%）
    public static void PostfixGetRandomScavengedItem(Il2CppSystem.Collections.Generic.List<GameItem> __result)
    {
        try
        {
            if (!IsActive() || !SurvivalTrade.IsRobinsonRun()) return;
            if (__result == null) return;
            try
            {
                if (Il2Cpp.HealthData.IsSentinel() && UnityEngine.Random.Range(0, 100) < 1)
                {
                    TryAddImmunivax(__result);
                    Core.LogMsg("[空间站鲁滨逊] 哨兵综合症拾荒 1% 免疫宁判定命中");
                }
            }
            catch (System.Exception exg) { Core.LogMsg("[空间站鲁滨逊] 哨兵免疫宁判定异常: " + exg.Message); }
            if (UnityEngine.Random.Range(0f, 1f) > 0.6f) return;
            bool food = UnityEngine.Random.Range(0, 2) == 0;
            string[] pool = food ? SCROUNGE_FOODS : SCROUNGE_MEDS;
            string id = pool[UnityEngine.Random.Range(0, pool.Length)];
            GameItem item = DirectoryMaster.Item(id, true);
            if (item == null) return;
            __result.Add(item);
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] 拾荒额外掉落异常: " + ex.Message); }
    }
    private static void TryAddImmunivax(Il2CppSystem.Collections.Generic.List<GameItem> __result)
    {
        try
        {
            GameItem it = Il2Cpp.PreBuiltItemHelper.CreateRealGenuineInjector();
            if (it == null) { Core.LogMsg("[空间站鲁滨逊] 正品免疫宁创建失败（CreateRealGenuineInjector null）"); return; }
            try { Il2Cpp.GeneralHelper.SetItemOwned(it, true); } catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] 免疫宁 SetItemOwned 异常: " + ex.Message); }
            __result.Add(it);
            Core.LogMsg("[空间站鲁滨逊] 拾荒正品免疫宁掉落: " + (it.identifier ?? "?") + "（哨兵综合症·1%）");
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] 免疫宁掉落异常: " + ex.Message); }
    }
    // 拾荒守卫（priority -1000 后置跑；Finalizer 兜异常补跑）
    public static System.Exception FinalizerGetRandomScavengedItem(Il2CppSystem.Collections.Generic.List<GameItem> __result, System.Exception __exception)
    {
        if (__exception != null)
        {
            try { PostfixGetRandomScavengedItem(__result); } catch { }
            Core.LogMsg("[拾荒守卫] GetRandomScavengedItem 异常被 Finalizer 兜住，已补跑额外掉落追加");
        }
        return null;
    }
    // 拾荒消耗睡眠 -15 + 清洁 -2（WP SLEEP_SCAV_LOSS=15 / BuildConfig.CleanScavCost=2 固定）
    public static void PostfixScavengeDumpingGrounds()
    {
        try
        {
            if (!IsActive() || !SurvivalTrade.IsRobinsonRun()) return;
            SetSleep(Math.Max(0, GetSleep() - 15));
            SetClean(Math.Max(0, GetClean() - 2));
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival.Scavenge] 异常: " + ex.Message); }
    }
    private static int GetScavAttempts()
    {
        try
        {
            var ps = Il2Cpp.PlayerStore.Instance;
            if (ps == null) return 0;
            return ps.scavengingAttempts;
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival.Scavenge] 异常: " + ex.Message); }
        return 0;
    }
}
