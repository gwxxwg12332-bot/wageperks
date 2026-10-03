using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppInterop.Runtime;

namespace WageSurvival;

// ============================================================
// SurvivalTrade（2026-10-03 阶段C：交易 12 处逻辑从 WagePerks.Patches.Trade.Budget.cs 迁入自挂）
// 门控：startType=15（鲁滨逊职业权威值 NewStartTypeUI.cs:29 / RobinCrusoePerk.Survival.cs:29）且节点/心情加成函数内部生效（0 时不改）。
// 评审补丁保留：-999 哨兵缓存（BUGS.md:18-27 性能史，缓存实现留在 SurvivalFxEval）+ _inBudgetOverride 防递归 + _moodBoostedClients 防刷心情。
// ============================================================
internal static class SurvivalTrade
{
    private static bool _inBudgetOverride = false;
    private static readonly HashSet<string> _moodBoostedClients = new HashSet<string>();

    internal static bool IsRobinsonRun()
    {
        try
        {
            var ps = PlayerStore.Instance;
            return ps != null && (int)ps.startType == 15;
        }
        catch { return false; }
    }

    private static string MoodKey(StoreClient c)
    {
        try
        {
            string rid = "";
            PlayerStore ps = PlayerStore.Instance;
            if (ps != null) rid = ps.runID ?? "";
            return rid + "|" + StoreStation.GetDayCounter() + "|" + (long)c.Pointer;
        }
        catch { return null; }
    }

    public static void PostfixStoreClientApplyBudgetModifier(StoreClient __instance)
    {
        try
        {
            if (__instance == null || !IsRobinsonRun() || _inBudgetOverride) return;
            int budgetBonusPct = SurvivalFood.GetBudgetBonusPct();
            if (budgetBonusPct <= 0) return;
            _inBudgetOverride = true;
            try
            {
                int budget = __instance.GetBudget();
                __instance.SetBudget((int)((double)budget * (1.0 + (double)budgetBonusPct / 100.0)));
            }
            finally { _inBudgetOverride = false; }
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival] 预算联动异常: " + ex.Message); }
    }

    public static void PostfixStoreClientManagerPickClient(StoreClient __result)
    {
        try
        {
            if (__result != null && IsRobinsonRun())
            {
                int budgetBonusPct = SurvivalFood.GetBudgetBonusPct();
                if (budgetBonusPct > 0 && !__result.useClientBudget)
                {
                    int clientCash = __result.clientCash;
                    int newCash = (int)((double)clientCash * (1.0 + (double)budgetBonusPct / 100.0));
                    if (newCash < 0) newCash = 0;
                    __result.clientCash = newCash;
                }
            }
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival] clientCash联动异常: " + ex.Message); }
    }

    public static void PrefixBargainUIManagerOfferBuyingMarkup(ref int percent)
    {
        try
        {
            if (IsRobinsonRun() && SurvivalFood.GetMood() >= 80) percent += 15;
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival] 出价上移异常: " + ex.Message); }
    }

    public static void PostfixGetDealMakerBonus(ref int __result)
    {
        try
        {
            if (IsRobinsonRun()) __result += SurvivalFood.GetBargainBonusPct();
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival] 谈判成功率异常: " + ex.Message); }
    }

    public static void PostfixItemFeatureListBargainBuyingMarkup(ref ItemFeature __result)
    {
        try
        {
            if (IsRobinsonRun() && __result != null && SurvivalFood.GetMood() >= 80 && __result.valueModifier > 0)
            {
                __result.valueModifier = (int)((float)__result.valueModifier * 1.15f);
            }
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival] 加价接受率异常: " + ex.Message); }
    }

    public static void PostfixStoreClientOnDealAccepted(StoreClient __instance)
    {
        try
        {
            if (!IsRobinsonRun()) return;
            string mk = MoodKey(__instance);
            if (__instance != null && mk != null && _moodBoostedClients.Add(mk))
            {
                SurvivalFood.BoostMood(5, LangHelper.T("成交一单", "Deal closed"));
            }
            RecordDeal();
            try
            {
                PlayerStore instance = PlayerStore.Instance;
                StoreClientInstance storeClientInstance = null;
                if (instance != null) storeClientInstance = instance.currentClientInstance;
                if (storeClientInstance == null || __instance == null) return;
                int num = 0;
                try
                {
                    System.Reflection.FieldInfo field = storeClientInstance.GetType().GetField("clientIntent", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                    if (field == null) field = storeClientInstance.GetType().GetField("intent", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                    if (field != null) num = (int)field.GetValue(storeClientInstance);
                }
                catch { }
                if (num != 1) return;
                System.Reflection.FieldInfo field2 = storeClientInstance.GetType().GetField("item", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (field2 == null) field2 = storeClientInstance.GetType().GetField("currentItem", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (field2 != null && field2.GetValue(storeClientInstance) is GameItem gameItem)
                {
                    try { RecordRevenue((int)gameItem.GetCurrentValue(useRetailMarkup: false, withChild: true, includeEvents: true, forceMarkup: false, 0L)); return; }
                    catch { return; }
                }
            }
            catch { }
        }
        catch { }
    }

    // 接待计数（社交结算用：SurvivalState.PostfixOnDayStart 读"deals"）
    internal static void RecordDeal()
    {
        try { SaveStore.SetInt("deals", SaveStore.GetInt("deals", 0) + 1); } catch { }
    }

    // 当日营业额累计（BUY 分支调用；打烊收入惩罚后清零）
    internal static void RecordRevenue(int amount)
    {
        if (amount <= 0) return;
        SaveStore.SetInt("revenue", SaveStore.GetInt("revenue", 0) + amount);
    }

    internal static int GetTodayRevenue() => SaveStore.GetInt("revenue", 0);
}
