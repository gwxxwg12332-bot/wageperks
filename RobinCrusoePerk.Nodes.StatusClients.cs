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

    // ===== 状态客户判定（identifier 包含匹配，小写）=====
    internal static bool IsStatusClient(StoreClient client)
    {
        if (client == null) return false;
        try
        {
            string id = (client.identifier ?? "").ToLowerInvariant();
            foreach (string s in STATUS_CLIENT_IDS)
                if (id.Contains(s)) return true;
            // 工厂打标补充（desperate/wornOut/sickLowers 等未知 identifier）
            if (_statusClientSet.Contains(client)) return true;
        }
        catch (System.Exception ex) { Core.LogMsg("[RobinCrusoePerk.Nodes.StatusClients] 异常: " + ex.Message); }
        return false;
    }
    internal static string GetStatusClientKind(StoreClient client)
    {
        if (client == null) return "";
        try
        {
            string id = (client.identifier ?? "").ToLowerInvariant();
            if (id.Contains("thirsty")) return "thirsty";
            if (id.Contains("hungry") || id.Contains("chef")) return "hungry";
            if (id.Contains("spacermedical")) return "injured";
            if (id.Contains("sick")) return "sick";
            if (_statusClientSet.Contains(client)) return _statusClientKind.GetValueOrDefault(client, "");
        }
        catch (System.Exception ex) { Core.LogMsg("[RobinCrusoePerk.Nodes.StatusClients] 异常: " + ex.Message); }
        return "";
    }
    private static readonly HashSet<StoreClient> _statusClientSet = new HashSet<StoreClient>();
    private static readonly Dictionary<StoreClient, string> _statusClientKind = new Dictionary<StoreClient, string>();

    // 工厂 Postfix 打标（状态客户 7 工厂：thirsty/hungry/injured/sick×3/desperate/wornOut）
    // v5.9：节点 statusClient-N（蓬头垢面-30/门可罗雀-20/爱答不理-10）→ 该概率降级为普通客户（无加价/减价）
    public static void PostfixStatusClientFactory(StoreClient __result, string kind)
    {
        try
        {
            if (__result == null || !IsActive()) return;
            int cut = FxNum("statusClient");
            if (cut < 0 && UnityEngine.Random.value < (-cut) / 100.0f)
            {
                if (_statusClientSet.Contains(__result)) { _statusClientSet.Remove(__result); _statusClientKind.Remove(__result); }
                return; // 本轮降级为普通客户
            }
            _statusClientSet.Add(__result);
            if (!string.IsNullOrEmpty(kind)) _statusClientKind[__result] = kind;
        }
        catch (System.Exception ex) { Core.LogMsg("[RobinCrusoePerk.Nodes.StatusClients] 异常: " + ex.Message); }
    }

    // ===== 状态客户加价系数（TryApplyTradeMarkup mode==2 调用）=====
    // 饥饿→食物×1.2 / 口渴→水×1.25 / 受伤→药×1.3 / 生病→药×1.4 / 其他品类×0.85
    internal static double GetStatusClientMarkup(StoreClient client, GameItem item)
    {
        if (client == null || item == null || !IsStatusClient(client)) return 1.0;
        string kind = GetStatusClientKind(client);
        bool food = IsFood(item), drink = IsDrink(item), med = IsMedicine(item);
        switch (kind)
        {
            case "thirsty": return drink ? 1.25 : 0.85;
            case "hungry": return food ? 1.20 : 0.85;
            case "injured": return med ? 1.30 : 0.85;
            case "sick": return med ? 1.40 : 0.85;
            default: return 1.0;
        }
    }

    // ===== T1 状态客户工厂打标包装（Core 注册 7 工厂 Postfix）=====
    // 状态客户无独立状态字段（拆包实锤），用工厂 Postfix 记录对象引用 + kind，
    // 供 IsStatusClient / GetStatusClientKind / GetStatusClientMarkup 判定
    public static void PostfixCreateThirstySpacer(StoreClient __result) => PostfixStatusClientFactory(__result, "thirsty");
    public static void PostfixCreateHungrySpacer(StoreClient __result) => PostfixStatusClientFactory(__result, "hungry");
    public static void PostfixCreateSpacerChef(StoreClient __result) => PostfixStatusClientFactory(__result, "hungry");
    public static void PostfixCreateInjuredSpacer(StoreClient __result) => PostfixStatusClientFactory(__result, "injured");
    public static void PostfixCreateSickChildCaretaker(StoreClient __result) => PostfixStatusClientFactory(__result, "sick");
    public static void PostfixCreateDesperateAddict(StoreClient __result) => PostfixStatusClientFactory(__result, "desperate");
    public static void PostfixCreateWornOutSpacer(StoreClient __result) => PostfixStatusClientFactory(__result, "wornOut");
    public static void PostfixCreateSickLowers(StoreClient __result) => PostfixStatusClientFactory(__result, "sick");
}
