using System;
using HarmonyLib;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Il2Cpp;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace WageSurvival;

internal static partial class SurvivalFood
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
            // 10-18 审阅《机制说明_卖血健康代价》§6.3 方案A 落地（用户拍板采纳）：
            //   抽血按次扣健康 15（新增"抽血专属代价"）——健康由纯展示值变为体力条（配合日结衰减×2 后果）
            SetHealth(Math.Max(0, GetHealth() - 15));
            // 09-20 用户拍板：抽血过多当场昏迷 3 天（血量 <3000 立即触发；血袋照常产出——抽血成功的代价）；昏迷当天立即禁出门/禁采血（IsForcedRest 即时生效）
            if (IsBloodWeak())
            {
                SaveStore.SetInt("blood_rest", 3);
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
                GameItem bb = WageAPI.WageItemFactory.Create("blood_bag"); // 10-06 收敛：WageItemFactory 权威创建（A 类纯创建）
                if (bb != null)
                {
                    try { bb.SetValue(200); } catch { }
                    var em = EmporiumEntry.Instance;
                    if (em != null && em.backInvinvElement != null)
                    {
                        // 10-06 二期修正：统一入口 GrantToInventory 显式目标（无所有权转移，对齐原 TryAcceptOnce/UncheckedAcceptAll 链原行为）；防重叠契约内置
                        bag = WageAPI.WageItemGrant.GrantToInventory(bb, em.backInvinvElement);
                    }
                }
            }
            catch (System.Exception ex) { Core.LogMsg("[RobinCrusoePerk.Blood] 异常: " + ex.Message); }
            if (!bag) Core.LogMsg("[卖血] blood_bag 不存在或发放失败");
            // 10-18 审阅方案A 落地：抽血路径不再调 ReceiveMinorWound()——原 :60 会伤口+2（3袋=6>5=重伤禁门）+
            //   PostfixReceiveMinorWound 回扣 -200 血 + 30% 免伤随机（Prefix 跳原方法但 Postfix 照扣，代价不确定）。
            //   去掉后：抽血代价=血量-500+健康-15+血袋（确定化）；伤口/重伤禁门节奏还给捡垃圾（ScavHelper 仍调受伤）。
            try { StoreUIManager.Instance.Notify(LangHelper.T("抽血 500cc → 血袋（价值 200，血量 " + GetBlood() + "/6000，健康 -15）", "Drew 500cc -> blood bag (worth 200, blood " + GetBlood() + "/6000, health -15)"), "green"); } catch { }
            RefreshStatusPanel();
            return true;
        }
        catch (System.Exception ex) { Core.LogMsg("[RobinCrusoePerk.Blood] 异常: " + ex.Message); }
        return false;
    }

    // ===== 卖血系统（09-17 用户拍板：面板卖血按钮 500cc→血袋+轻伤；受伤扣血；虚弱<3000；睡觉/喝水/进食回血）=====
    internal const int BLOOD_MAX = 6000;
    private static int _memBlood = -1; // 10-18 审阅 R2 定案：仅调试可见，不参与读取（恒 -1）
    internal static int GetBlood()
    {
        try
        {
            int v = SaveStore.GetInt("blood", BLOOD_MAX);
            // 键缺失时物化一次默认值（须在读档完成后，否则 SetInt 会被 WageAPI/WageSaveStore.cs:126 门控丢弃）
            if (!SaveStore.HasKey("blood") && WageAPI.WageSaveStore.LoadComplete) SaveStore.SetInt("blood", v);
            return Math.Max(0, Math.Min(BLOOD_MAX, v));
        }
        catch { return BLOOD_MAX; }
    }
    internal static void SetBlood(int v)
    {
        try
        {
            int c = Math.Max(0, Math.Min(BLOOD_MAX, v));
            _memBlood = c;                    // 仅调试
            SaveStore.SetInt("blood", c);     // ★ 实时落字典（与 WP 同源=唯一权威）
        }
        catch { }
    }
    internal static void ClearMemBlood() { try { _memBlood = -1; } catch { } } // 读档清缓存
    internal static int AddBlood(int delta)
    {
        try
        {
            // 10-10 四问题批#1：读档空窗期不物化默认值（对齐主 mod RobinCrusoePerk.Blood.cs:103）——防打烊写回 6000 覆盖真实血量
            // 10-18 审阅 R2 §4 改动4：原 return BLOOD_MAX 是"假满血"（调用方拿 6000 当真实值→IsBloodWeak 判定错）；
            //   改回 GetBlood()（直读字典，读档窗口期读到的就是当前持久化值）
            if (!WageAPI.WageSaveStore.LoadComplete) return GetBlood();
            int from = GetBlood(); // 审阅 07:29 §4.3：血量审计——一次运行定位 8 条写入路径各自来源
            int b = Math.Max(0, Math.Min(BLOOD_MAX, from + delta));
            SetBlood(b);
            if (GetBlood() <= 0) { try { ExecuteGameOverBy("blood_loss"); } catch { } }
            // 10-18 审阅复核 §3.3（07:09）：AddBlood 自身必须刷新——原 DrinkSip/EatBite 在 AddBlood **之前**调
            //   RefreshStatusPanel（SurvivalConsume.cs:45/46 → :286/:297），面板永远慢一步（卖血5500→喝水→显示仍5500）。
            //   统一在此刷，吃喝回血/受伤扣血/日结恢复全覆盖（对齐 BoostMood 写法）。
            try { RefreshStatusPanel(); } catch { }
            if (delta != 0) Core.LogMsg("[卖血审计] AddBlood(" + delta + ") " + from + " → " + b);
            return b;
        }
        catch { return BLOOD_MAX; }
    }

    internal static bool IsBloodWeak() { try { return GetBlood() < 3000; } catch { return false; } }
    // 09-20 M5 拍板：虚弱强化——强制休息期判定（休息中禁采血/禁出门）
    internal static bool IsForcedRest()
    {
        try { return SaveStore.GetInt("blood_rest", 0) > 0; }
        catch { return false; }
    }
    // 09-20 M5：虚弱强制休息 3 天 → 结束 ±20% 血量（默认 50/50）；每日结算调用
    internal static void TickBloodRest()
    {
        try
        {
            int rest = SaveStore.GetInt("blood_rest", 0);
            if (rest > 0)
            {
                rest--;
                SaveStore.SetInt("blood_rest", rest);
                if (rest == 0)
                {
                    bool good = Core.Rng.Next(2) == 0;
                    int delta = (int)(BLOOD_MAX * 0.2f); // 1200
                    Core.LogMsg("[卖血审计] 恢复期结束: rest→0, 随机=" + (good ? "+" : "-") + delta); // 审阅 07:29 §4.3：来源标记
                    AddBlood(good ? delta : -delta);
                    // 09-24 修：恢复期结束强制回血到安全线 3000——防止"结束随机扣血→仍<3000→下方 else-if 再设 rest=3"的
                    // 虚弱永续循环（卖血超过阈值后永远昏迷/禁出门/禁采血=游戏卡死）。虚弱期结束=身体恢复，必须给出安全出口。
                    if (GetBlood() < 3000)
                    {
                        Core.LogMsg("[卖血审计] 恢复期兜底: 强制补到 3000（原 " + GetBlood() + "）"); // 审阅 07:29 §4.3：来源标记
                        AddBlood(3000 - GetBlood());
                    }
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
                SaveStore.SetInt("blood_rest", 3);
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
    // 保存点快照：SaveGame 时存 6 维生存状态 + 血量 + Flush落盘
    public static void PostfixSaveGame()
    {
        try
        {
            // 10-18 审阅 R2 §4 改动3：删 `if (_memBlood >= 0) SetInt("blood", _memBlood)`——SetBlood 已实时写字典，
            //   这里再写只把旧缓存覆盖回去（双权威源）。改动6(选A)：快照补 saved_blood（原只 6 维，卖血后不存档
            //   退出重进血量不回滚=与 6 维不对称；Restore 用 AddBlood 回滚，复用归零死亡判定）。
            SaveStore.SetInt("saved_blood", GetBlood());
            SaveStore.SetInt("saved_sat", GetSatiety());
            SaveStore.SetInt("saved_th", GetThirstPct());
            SaveStore.SetInt("saved_hp", GetHealth());
            SaveStore.SetInt("saved_clean", GetClean());
            SaveStore.SetInt("saved_sleep", GetSleep());
            SaveStore.SetInt("saved_social", GetSocial());
            SaveStore.Flush(); // 打烊落盘
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival.Blood] PostfixSaveGame 异常: " + ex.Message); }
    }
    // 10-03 补：打烊落盘（踩WagePerks老坑——SaveGame只在进游戏时调一次，打烊不存档）
    public static void PostfixEndDay()
    {
        try
        {
            PostfixSaveGame();
            Core.LogMsg("[WageSurvival] EndDay Postfix → Flush落盘");
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival.Blood] PostfixEndDay 异常: " + ex.Message); }
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
