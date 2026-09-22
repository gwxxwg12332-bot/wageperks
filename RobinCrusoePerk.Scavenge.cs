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
    public static void PostfixCanScavenge(ref bool __result)
    {
        try
        {
            if (!IsActive()) return;
            if (IsBloodWeak() || IsForcedRest()) { __result = false; return; } // 09-20 M5：虚弱/恢复期禁出门
            // 三处同 cap（拆包 2.13.11.4：GetMaxScavAttempts/GetScavTimeLeft/CanScavenge 独立复制，须一致）
            __result = GetScavAttempts() < GetScavCap();
        }
        catch { }
    }
    public static void PostfixGetMaxScavAttempts(ref int __result)
    {
        try
        {
            if (!IsActive()) return;
            if (IsBloodWeak() || IsForcedRest()) { __result = 0; return; } // 09-20 M5
            __result = GetScavCap(); // 上限
        }
        catch { }
    }
    public static void PostfixGetScavTimeLeft(ref int __result)
    {
        try
        {
            if (!IsActive()) return;
            if (IsBloodWeak() || IsForcedRest()) { __result = 0; return; } // 09-20 M5
            // UI 显示：剩余 = 上限 - 已用（不为负，数字整对）
            __result = Math.Max(0, GetScavCap() - GetScavAttempts());
        }
        catch { }
    }
    // 拾荒次数 cap（v5.7）：受伤=0、病危=0；心情 ≥80 +2 / <40 -2；基底 5
    private static int GetScavCap()
    {
        try
        {
            // 捡漏直觉不再额外加拾荒次数（用户拍板 09-10：该加成有缩减 bug，特性不影响次数）
            // 09-12 硬爽版：捡漏直觉加回 +10（鲁滨逊走 GetScavCap 单一读口，LuckScout 侧 Postfix 已排除鲁滨逊防双加）
            // 09-20 回归修复：受伤且伤口未稳定 → cap 0 → 禁拾荒（打绷带 isWoundStable=true → cap 恢复，09-09 已拆实锤）
            if (IsWounded() && !IsWoundStable()) return 0;
            int cap = 5 + GetMoodScavBonus();
            if (BuildConfig.HardMode && LuckScoutPerk.IsActive()) cap += 10;
            if (IsHomebrewWineBuffActive()) cap += 1; // 顶级自酿 buff：拾荒次数+1（用户拍板 09-10）
            cap = Math.Max(1, cap);
            return cap;
        }
        catch { }
        return 5;
    }
    // 拾荒池多加官方药品+食物（用户拍板 09-09）：每次成功拾荒 60% 概率额外翻出 1 件（官方已开放）
    private static readonly string[] SCROUNGE_FOODS = {
        "morsel", "small_morsel", "cat_bar", "processed_meat", "small_raw_meat", "raw_meat",
        "processed_cheese", "cup_noodle", "processed_juice", "li_eat_snackbar", "processed_milk"
    };
    private static readonly string[] SCROUNGE_MEDS = {
        "bandage_item", "hemostatic_bandage_item", "topical_bandage_item", "phagimycin_pill",
        "med_bottle_blue", "med_bottle_red", "salve", "blood_bag"
    };
    public static void PostfixGetRandomScavengedItem(Il2CppSystem.Collections.Generic.List<GameItem> __result)
    {
        try
        {
            if (!IsActive()) return;
            if (__result == null) return;
            if (UnityEngine.Random.Range(0f, 1f) > 0.6f) return; // 60% 概率
            bool food = UnityEngine.Random.Range(0, 2) == 0;
            string[] pool = food ? SCROUNGE_FOODS : SCROUNGE_MEDS;
            string id = pool[UnityEngine.Random.Range(0, pool.Length)];
            GameItem item = DirectoryMaster.Item(id, true);
            if (item == null) return;
            __result.Add(item);
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] 拾荒额外掉落异常: " + ex.Message); }
    }
    // 拾荒消耗睡眠（v5.7+：外出拾荒睡眠 -15%，疲劳影响拾荒次数/效率）
    public static void PostfixScavengeDumpingGrounds()
    {
        try
        {
            if (!IsActive()) return;
            SetSleep(Math.Max(0, GetSleep() - SLEEP_SCAV_LOSS));
            // 09-13 用户拍板 v1 定稿：拾荒每次 -2 清洁（单一场景）
            SetClean(Math.Max(0, GetClean() - BuildConfig.CleanScavCost));
        }
        catch { }
    }

    private static int GetScavAttempts()
    {
        try
        {
            var ps = Il2Cpp.PlayerStore.Instance;
            if (ps == null) return 0;
            return ps.scavengingAttempts;
        }
        catch { }
        return 0;
    }
}
