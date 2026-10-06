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

    // 10-06 议价加成可见性（主控拍板方案①）：GetActualDisplay Postfix——bargainBuyingMarkup 词条且加成生效时
    // 显示追加"（含鲁滨逊出价加成）"。纯显示层标注，行为零改动；valueModifier 已含叠加（1.15×(percent+15)）数值无需重算。
    public static void PostfixItemFeatureGetActualDisplay(ItemFeature __instance, ref string __result)
    {
        try
        {
            if (__instance == null || string.IsNullOrEmpty(__result)) return;
            if (!string.Equals(__instance.identifier, "bargainBuyingMarkup", StringComparison.Ordinal)) return;
            if (!(IsRobinsonRun() && SurvivalFood.GetMood() >= 80)) return;
            if (__result.Contains("鲁滨逊出价加成")) return;
            __result = __result + "（含鲁滨逊出价加成）";
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival] 议价词条标注异常: " + ex.Message); }
    }

    public static void PostfixStoreClientOnDealAccepted(StoreClient __instance)
    {
        try
        {
            // 10-05 修复：交易记数全局生效（社交日结算 StoreEventManager.OnDayStart 对所有档跑，
            // 普通档交易也必须 RecordDeal，否则 deals 恒 0 → 打烊结算社交只掉(无交易-5)不加）
            RecordDeal();
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

    // ===== 10-06 复刻 WP Patches.Trade.Markup.cs:409 AddTradeLabel：加价挂 ItemFeature 词条 → tooltip/详情显示原因+百分比 =====
    // WP 侧既有实现（信誉扫地+20%/笑面虎加价+25%等）；WS 侧售价加成此前只改 __result 不挂词条 → 玩家看不到加价原因。
    // 10-06 判重修复：identifier== 字符串比较在 Il2Cpp 下失效（北极星截图骰子笑面虎加价 20 条同款坑）→ string.Equals(Ordinal)+同 id 自愈清理
    internal static void AddPriceLabel(GameItem item, string labelId, string display, int percent)
    {
        try
        {
            if (item == null || string.IsNullOrEmpty(labelId) || item.itemFeatures == null) return;
            bool found = false;
            for (int i = item.itemFeatures.Count - 1; i >= 0; i--)
            {
                if (item.itemFeatures[i] != null && string.Equals(item.itemFeatures[i].identifier, labelId, StringComparison.Ordinal))
                {
                    if (found)
                    {
                        item.itemFeatures.RemoveAt(i);
                        continue;
                    }
                    item.itemFeatures[i].preExposeValueModifier = percent;
                    item.itemFeatures[i].usePreExposeValue = percent != 0;
                    item.itemFeatures[i].publicDisplay = display;
                    item.itemFeatures[i].actualDisplay = display;
                    found = true;
                }
            }
            if (found) return;
            ItemFeature f = new ItemFeature();
            f.identifier = labelId;
            f.featureType = percent >= 0 ? ItemFeature.FeatureType.TemporarySelling : ItemFeature.FeatureType.TemporaryBuying;
            f.valueStage = ItemFeature.ValueStage.Market;
            f.valueModifier = 0;
            f.preExposeValueModifier = percent;
            f.usePreExposeValue = percent != 0;
            f.initiallyShown = true;
            f.isFeatureMatch = true;
            f.isFeatureExposed = true;
            f.isExposable = true;
            f.isFeatureDiscovered = false;
            f.publicDisplay = display;
            f.actualDisplay = display;
            item.itemFeatures.Add(f);
        }
        catch { }
    }
}
