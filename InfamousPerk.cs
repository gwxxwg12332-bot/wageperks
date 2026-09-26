using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace JacksonPerks;

// ============================================================
// 负面特性：声名狼藉（极限开局）
// 四势力声望 -99 + 开局现金 5000
// ============================================================
internal sealed class InfamousPerk : CustomStartingPerk
{
    internal const string PerkId = "声名狼藉";

    internal override string Id => PerkId;
    internal override string DisplayName => LangHelper.T("声名狼藉", "Infamous");
    internal override string Description => LangHelper.T(
        "你在空间站臭名昭著——五势力声望 -99，做一点坏事就被通缉。第二天会收到 5000 信用点补偿金。高风险高回报，活着回来再说。",
        "You are infamous across the station - all five factions at -99 rep. One wrong move and you are wanted. You get 5000 credits on day 2. High risk, high reward.");
    internal override int Cost => -10;
    internal override int Type => 1; // 负面红色

    internal override void OnNewGame() { }

    internal static bool IsActive() => Core.PerkActive(PerkId);

    // PlayerStore.StartNewGame Postfix：声望初始化（5000 改到第二天 OnDayStart 送）
    public static void PostfixStartNewGame()
    {
        try
        {
            if (!IsActive()) return;
            Core.LogMsg("[声名狼藉] PostfixStartNewGame 触发");
            // 五势力声望绝对值（差值补法）
            string[] factionIds = { "FACTION_SECURITY", "FACTION_UPPER_LEVEL", "FACTION_REVOLUTION", "FACTION_LOWER_LEVEL", "FACTION_BLACK_MARKET" };
            int[] targetValues = { -99, -99, -99, -99, -99 };
            for (int i = 0; i < factionIds.Length; i++)
            {
                try
                {
                    var rep = StoreReputation.GetStoreReputation(factionIds[i]);
                    if (rep == null) { Core.LogMsg("[声名狼藉] " + factionIds[i] + " = null"); continue; }
                    int cur = (int)rep.GetReputationExact();
                    if (cur <= targetValues[i]) // 09-26 B修正版：已更负/达标：跳过，防正值回升（差值为正会冲正）
                    {
                        Core.LogMsg("[声名狼藉] " + factionIds[i] + " cur=" + cur + " 已更负/达标，跳过");
                        continue;
                    }
                    int diff = targetValues[i] - cur; // 此处 diff 恒 < 0，只向下逼近
                    rep.ModReputation(diff);
                    int after = (int)rep.GetReputationExact();
                    Core.LogMsg("[声名狼藉] " + factionIds[i] + " cur=" + cur + " diff=" + diff + " after=" + after);
                    // 09-26 兜底：黑市声望特殊（diff=-99 实际扣 -198），补正到 -99
                    if (factionIds[i] == "FACTION_BLACK_MARKET" && after != -99)
                    {
                        rep.ModReputation(-99 - after);
                        after = (int)rep.GetReputationExact();
                        Core.LogMsg("[声名狼藉] 黑市兜底修正: after=" + after);
                    }
                }
                catch (Exception ex) { Core.LogMsg("[声名狼藉] " + factionIds[i] + " 异常: " + ex.Message); }
            }
        }
        catch (Exception ex) { Core.LogMsg("[声名狼藉] PostfixStartNewGame 异常: " + ex.Message); }
    }
    // 第二天 OnDayStart 送 5000（阶段2 收敛：散落 Postfix 已删，由 PostfixUnifiedDayStart 统一驱动）
    internal override void OnDayStart()
    {
        try
        {
            bool active = IsActive();
            int day = StoreStation.GetDayCounter();
            int got = WageSaveStore.GetInt(PerkId, "got_money", 0);
            Core.LogMsg("[声名狼藉] OnDayStart: active=" + active + " day=" + day + " got=" + got);
            if (!active) return;
            if (day != 2) return;  // 第二天送5000
            if (got > 0) return;
            WageSaveStore.SetInt(PerkId, "got_money", 1);
            var ps = Il2Cpp.PlayerStore.Instance;
            if (ps != null) { ps.playerCash += 5000; }
            NotifyHelper.NightLogRaw("声名狼藉：你收到了 5000 补偿金");
            Core.LogMsg("[声名狼藉] 第1天送5000 OK");
        }
        catch (System.Exception ex) { Core.LogMsg("[声名狼藉] OnDayStart 异常: " + ex.Message); }
    }

    // 声名狼藉：强开保险服务解锁 + 价格双倍（拆包：保险解锁依赖黑市声望，声名狼藉-99被原生锁）
    public static void PostfixUpdateCost(Il2Cpp.StoreService __instance)
    {
        try
        {
            if (!IsActive()) return;
            if (__instance == null) return;
            string sid = "";
            try { sid = __instance.id; } catch { }
            Core.LogMsg("[声名狼藉] UpdateCost: sid=" + sid + " unlocked=" + __instance.unlocked + " cost=" + __instance.cost);
            if (sid != "INSURANCE_SERVICE") return;
            __instance.unlocked = true;   // 强开解锁
            __instance.cost *= 2;          // 价格双倍
            Core.LogMsg("[声名狼藉] 保险服务强开 unlocked=true cost=" + __instance.cost);
        }
        catch (System.Exception ex) { Core.LogMsg("[声名狼藉] PostfixUpdateCost 异常: " + ex.Message); }
    }
}
