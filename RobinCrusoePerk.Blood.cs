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
    // ===== 面板卖血按钮（09-20 用户拍板：替代采血包双击——唯一采血入口；删采血包物品+双击链）=====
    public static bool TrySellBlood()
    {
        try
        {
            if (!IsActive()) return false;
            if (Patches.CurrentUITradeMode != 0) return false;          // 交易模式不抽
            if (IsBloodWeak() || IsForcedRest())
            {
                try { StoreUIManager.Instance.Notify(LangHelper.T("身体虚弱/恢复期，无法抽血", "Too weak - cannot draw blood"), "red"); } catch { }
                return false;
            }
            int blood = GetBlood();
            if (blood < 500)
            {
                try { StoreUIManager.Instance.Notify(LangHelper.T("血量不足 500cc，无法抽血", "Not enough blood (need 500cc)"), "red"); } catch { }
                return false;
            }
            AddBlood(-500);
            // 09-20 用户拍板：抽血过多当场昏迷 3 天（血量 <3000 立即触发；血袋照常产出——抽血成功的代价）；昏迷当天立即禁出门/禁采血（IsForcedRest 即时生效）
            if (IsBloodWeak())
            {
                WageSaveStore.SetInt(PERK_ID, "blood_rest", 3);
                try { StoreUIManager.Instance.Notify(LangHelper.T("你因为失血过多昏迷了三天", "You passed out from blood loss - 3-day coma"), "red"); } catch { }
                try { var _ps = PlayerStore.Instance; if (_ps != null) _ps.AddNightLog(LangHelper.T("[鲁滨逊] 你因为失血过多昏迷了三天", "[Robinson] Passed out from blood loss - 3-day coma"), "#7FC97F"); } catch { } // ① 原生夜报（09-22 统一柔和绿）
                Core.AddNightReportLine(LangHelper.T("[鲁滨逊] 你因为失血过多昏迷了三天", "[Robinson] Passed out from blood loss - 3-day coma"));
                ForceComaSkip(); // 09-20 拍板：昏迷当天立刻强制过夜 ×3（跳过 3 天）
                try { RefreshStatusPanel(); } catch { }
            }
            bool bag = false;
            try
            {
                // 09-20 M2 拍板：产普通血袋 blood_bag（价值 200，走原生医疗品销路）；删 blue_blood_bag 路径
                GameItem bb = DirectoryMaster.Item("blood_bag", true);
                if (bb != null)
                {
                    try { bb.SetValue(200); } catch { }
                    var em = EmporiumEntry.Instance;
                    if (em != null && em.backInvinvElement != null)
                    {
                        var slot = em.backInvinvElement.TryFindOneValidInventorySlot(bb, false);
                        if (slot != null) { try { slot.TryAcceptOnce(); bag = true; } catch { } }
                        if (!bag) { try { ((GameInventory)em.backInvinvElement).UncheckedAccept(bb); bag = true; } catch { } }
                    }
                }
            }
            catch (System.Exception ex) { Core.LogMsg("[RobinCrusoePerk.Blood] 异常: " + ex.Message); }
            if (!bag) Core.LogMsg("[卖血] blood_bag 不存在或发放失败");
            try { Il2Cpp.HealthData.ReceiveMinorWound(); } catch { }
            try { StoreUIManager.Instance.Notify(LangHelper.T("抽血 500cc → 血袋（价值 200，血量 " + GetBlood() + "/6000）", "Drew 500cc -> blood bag (worth 200, blood " + GetBlood() + "/6000)"), "green"); } catch { }
            RefreshStatusPanel();
            return true;
        }
        catch (System.Exception ex) { Core.LogMsg("[RobinCrusoePerk.Blood] 异常: " + ex.Message); }
        return false;
    }

    // ===== 卖血系统（09-17 用户拍板：面板卖血按钮 500cc→血袋+轻伤；受伤扣血；虚弱<3000；睡觉/喝水/进食回血）=====
    internal const int BLOOD_MAX = 6000;
    private static int _memBlood = -1; // 09-20 修：血量内存缓存（打烊才落盘，防读档刷血）
    internal static int GetBlood() {
        try {
            if (_memBlood >= 0) return _memBlood;
            int saved = WageSaveStore.GetInt(PERK_ID, "blood", BLOOD_MAX);
            // 09-26 修：读档空窗期返回默认值但不物化进缓存（防默认值6000污染存档）
            if (!WageSaveStore.LoadComplete) return saved;
            _memBlood = saved;
            return saved;
        } catch { return BLOOD_MAX; }
    }
    internal static void SetBlood(int v) { try { _memBlood = Math.Max(0, Math.Min(BLOOD_MAX, v)); } catch { } }
    internal static void ClearMemBlood() { try { _memBlood = -1; } catch { } } // 读档清缓存
    internal static int AddBlood(int delta)
    {
        // 09-26 修：读档空窗期不要物化默认值（否则 SetBlood 会把默认值6000写进缓存，打烊写回存档覆盖真实值）
        if (!WageSaveStore.LoadComplete) return BLOOD_MAX;
        int b = Math.Max(0, Math.Min(BLOOD_MAX, GetBlood() + delta));
        SetBlood(b);
        if (GetBlood() <= 0) { try { ExecuteGameOverBy("blood_loss"); } catch { } } // 09-20 拍板：失血归零立即死亡（日常/跳天通用）
        return b;
    }

    internal static bool IsBloodWeak() { try { return GetBlood() < 3000; } catch { return false; } }
    // 09-20 M5 拍板：虚弱强化——强制休息期判定（休息中禁采血/禁出门）
    internal static bool IsForcedRest()
    {
        try { return WageSaveStore.GetInt(PERK_ID, "blood_rest", 0) > 0; }
        catch { return false; }
    }
    // 09-20 M5：虚弱强制休息 3 天 → 结束 ±20% 血量（默认 50/50）；每日结算调用
    internal static void TickBloodRest()
    {
        try
        {
            int rest = WageSaveStore.GetInt(PERK_ID, "blood_rest", 0);
            if (rest > 0)
            {
                rest--;
                WageSaveStore.SetInt(PERK_ID, "blood_rest", rest);
                if (rest == 0)
                {
                    bool good = Core.Rng.Next(2) == 0;
                    int delta = (int)(BLOOD_MAX * 0.2f); // 1200
                    AddBlood(good ? delta : -delta);
                    // 09-24 修：恢复期结束强制回血到安全线 3000——防止"结束随机扣血→仍<3000→下方 else-if 再设 rest=3"的
                    // 虚弱永续循环（卖血超过阈值后永远昏迷/禁出门/禁采血=游戏卡死）。虚弱期结束=身体恢复，必须给出安全出口。
                    if (GetBlood() < 3000) { AddBlood(3000 - GetBlood()); }
                    try { StoreUIManager.Instance.Notify(LangHelper.T("身体恢复期结束：血量 " + GetBlood() + "/6000", "Recovery over: blood " + GetBlood() + "/6000"), GetBlood() >= 3000 ? "green" : "red"); } catch { }
                }
                else
                {
                    try { StoreUIManager.Instance.Notify(LangHelper.T("身体虚弱，强制休息（剩余 " + rest + " 天）", "Too weak - forced rest (" + rest + "d left)"), "red"); } catch { }
                }
                RefreshStatusPanel();
            }
            else if (IsBloodWeak())
            {
                WageSaveStore.SetInt(PERK_ID, "blood_rest", 3);
                try { StoreUIManager.Instance.Notify(LangHelper.T("身体虚弱到极限，强制休息 3 天", "At your limit - forced 3-day rest"), "red"); } catch { }
                try { var _ps = PlayerStore.Instance; if (_ps != null) _ps.AddNightLog(LangHelper.T("[鲁滨逊] 血量过低，强制休息 3 天", "[Robinson] Too weak - forced 3-day rest"), "#7FC97F"); } catch { } // ① 原生夜报（09-22 统一柔和绿）
                Core.AddNightReportLine(LangHelper.T("[鲁滨逊] 血量过低，强制休息 3 天", "[Robinson] Too weak - forced 3-day rest"));
                RefreshStatusPanel();
            }
        }
        catch (System.Exception ex) { Core.LogMsg("[RobinCrusoePerk.Blood] 异常: " + ex.Message); }
    }

    public static bool PrefixReceiveWound()
    {
        try
        {
            if (!IsActive()) return true;
            if (IsHomebrewWineBuffActive()) return false; // 顶级自酿 buff：连续3天不受伤（用户拍板 09-10）
            int pct = GetMoodWoundPct(); // ≥80 -20% / <40 +20%（受伤几率修正）
            float avoid = 0.3f * (1f + pct / 100f); // 免伤基底 30%：≥80→36%（更不易伤）/<40→24%（更容易伤）
            if (IsBloodWeak()) avoid -= 0.3f; // 卖血虚弱（<3000）：受伤概率 +30%（09-17）
            if (UnityEngine.Random.value < avoid) return false;
        }
        catch (System.Exception ex) { Core.LogMsg("[RobinCrusoePerk.Blood] 异常: " + ex.Message); }
        return true;
    }
    // 受伤扣血（09-17 卖血）：轻伤 -200 / 重伤 -500（ReceiveMinorWound/MajorWound Postfix）
    public static void PostfixReceiveMinorWound()
    {
        try { if (!IsActive()) return; AddBlood(-200); RefreshStatusPanel(); } catch { }
    }
    public static void PostfixReceiveMajorWound()
    {
        try { if (!IsActive()) return; AddBlood(-500); RefreshStatusPanel(); } catch { }
    }
    // 读档恢复容器升级（照虚空珠 PostfixLoadGame：SetShape 不存档，按 wageUpgradeCap 重设）
    // 保存点快照：SaveGame 时存 6 维生存状态（读档恢复用；key 带 runID 自动隔离）
    public static void PostfixSaveGame()
    {
        try
        {
            if (!IsActive()) return;
            if (_memBlood >= 0) WageSaveStore.SetInt(PERK_ID, "blood", _memBlood); // 09-26 修：血量打烊落盘桥（删除诊断日志时保留）
            WageSaveStore.SetInt(PERK_ID, "saved_sat", GetSatiety());
            WageSaveStore.SetInt(PERK_ID, "saved_th", GetThirstPct());
            WageSaveStore.SetInt(PERK_ID, "saved_hp", GetHealth());
            WageSaveStore.SetInt(PERK_ID, "saved_clean", GetClean());
            WageSaveStore.SetInt(PERK_ID, "saved_sleep", GetSleep());
            WageSaveStore.SetInt(PERK_ID, "saved_social", GetSocial());
        }
        catch (System.Exception ex) { Core.LogMsg("[RobinCrusoePerk.Blood] 异常: " + ex.Message); }
    }
    // 受伤判定：woundState > 0（[L1] PlayerStore.healthData@0x2B8 → HealthData.woundState@0x24；IsSeriouslyWounded=woundState>5）
    private static bool IsWounded()
    {
        try
        {
            var ps = Il2Cpp.PlayerStore.Instance;
            if (ps == null || ps.healthData == null) return false;
            return ps.healthData.woundState > 0;
        }
        catch (System.Exception ex) { Core.LogMsg("[RobinCrusoePerk.Blood] 异常: " + ex.Message); }
        return false;
    }
    // 伤口稳定判定（打绷带/治疗后 isWoundStable=true，与捡漏直觉同语义；[L1] 原版 HealthData 字段）
    private static bool IsWoundStable()
    {
        try
        {
            var ps = Il2Cpp.PlayerStore.Instance;
            if (ps == null || ps.healthData == null) return false;
            return ps.healthData.isWoundStable;
        }
        catch (System.Exception ex) { Core.LogMsg("[RobinCrusoePerk.Blood] 异常: " + ex.Message); }
        return false;
    }
}
