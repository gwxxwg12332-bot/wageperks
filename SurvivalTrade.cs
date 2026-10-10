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

    // 10-08 v5 拆包实锤（Memos 9vmfDvdr23Dv98wyKvoSB9 / cheatsheet）：
    //   StartType 枚举全表（dump.cs:31259-31277）：NONE=0...ROCK_BOTTOM=12(一贫如洗)...RANCHER=13；15=mod 自定义（枚举外值）
    //   一贫如洗(12)=原生鲁滨逊-like 玩法（hardMode=难度开关非玩法标识）→ 门控扩展：15 || 12；水商(1)/学徒(2) 等不生效
    internal static bool IsRobinsonRun()
    {
        try
        {
            var ps = PlayerStore.Instance;
            int st = ps != null ? (int)ps.startType : -1;
            return st == 15 || st == 12;
        }
        catch { return false; }
    }

    // ===== 10-10 #37 A 拍板：鲁滨逊 buy 食物/药品 ×2 从主 mod 迁 WS（原子迁移，同一 commit）=====
    // 主 mod Patches.Trade.Markup.cs ApplyRobinsonMarkup buy ×2 分支已删（原 :215-226）+ TryAddRobinsonBuyMarkup 已删（Robinson.cs:175）
    // → 本方法承接：门控=IsRobinsonRun（15||12 权威，SurvivalTrade.cs:21）+ buy（非 owned）+ 食物/药品 + 非被偷半价 → ×2 + wages_robin_buy 标签
    // 双装只 ×2 一次（主 mod buy 分支已不再改 result）；单装 WS 有 ×2；非鲁滨逊不触发（门控唯一=IsRobinsonRun）
    public static void PostfixGameItemGetNegociatedValue(GameItem __instance, ref long __result)
    {
        try
        {
            if (__instance == null || string.IsNullOrEmpty(__instance.identifier)) return;
            if (!IsRobinsonRun()) return;
            bool sell = false;
            try { sell = Il2Cpp.GeneralHelper.IsItemOwned(__instance); } catch { }
            if (sell) return; // 只 buy（玩家卖出给客户）
            if (HasStolenHalfPriceFeature(__instance)) return; // 被偷物半价保持，不 ×2（10-07 P1-3 语义随迁）
            if (FoodIdentifyHelper.IsFood(__instance))
            {
                __result = (long)((double)__result * 2.0);
                TryAddRobinsonBuyMarkup(__instance);
            }
            else if (FoodIdentifyHelper.IsMedicine(__instance))
            {
                __result = (long)((double)__result * 2.0);
                TryAddRobinsonBuyMarkup(__instance);
            }
        }
        catch (Exception ex) { Core.LogMsg("[WageSurvival] PostfixGetNegociatedValue ×2 异常: " + ex.Message); }
    }

    // 被偷物半价标记检测（stolen_half_price feature → 买回保持半价，不叠 ×2）——主 mod Patches.Trade.Markup.cs:270 等价迁入
    private static bool HasStolenHalfPriceFeature(GameItem item)
    {
        try
        {
            if (item == null || item.itemFeatures == null) return false;
            for (int i = 0; i < item.itemFeatures.Count; i++)
                if (item.itemFeatures[i] != null && string.Equals(item.itemFeatures[i].identifier, "stolen_half_price", StringComparison.Ordinal)) return true;
        }
        catch { }
        return false;
    }

    // wages_robin_buy 标签（主 mod Patches.Trade.Robinson.cs:175 等价迁入——"鲁滨逊·口粮双倍价"，+100% 表达 ×2）
    private static void TryAddRobinsonBuyMarkup(GameItem item)
    {
        try
        {
            if (item == null || item.itemFeatures == null) return;
            // 自愈：清历史堆积 + 排除类型不加（骰子类——主 mod 原 excludedType 含虚空珠/妙妙箱，WS 侧仅保留骰子 tag 判，其余类型非食物/药品路径不会到）
            bool excludedType = false;
            try { excludedType = item.IsTag("destiny_dice_tag"); } catch { }
            int keepMax = excludedType ? 0 : 1;
            int found = 0;
            for (int j = item.itemFeatures.Count - 1; j >= 0; j--)
            {
                if (item.itemFeatures[j] != null && item.itemFeatures[j].identifier == "wages_robin_buy") { found++; if (found > keepMax) item.itemFeatures.RemoveAt(j); }
            }
            if (excludedType) return;
            string text = LangHelper.T("鲁滨逊·口粮双倍价", "Robinson·Ration x2");
            for (int i = 0; i < item.itemFeatures.Count; i++)
            {
                if (item.itemFeatures[i] != null && item.itemFeatures[i].identifier == "wages_robin_buy")
                {
                    item.itemFeatures[i].publicDisplay = text;
                    item.itemFeatures[i].actualDisplay = text;
                    item.itemFeatures[i].preExposeValueModifier = 100;
                    item.itemFeatures[i].usePreExposeValue = true;
                    return;
                }
            }
            Il2Cpp.ItemFeature itemFeature = new Il2Cpp.ItemFeature();
            itemFeature.identifier = "wages_robin_buy";
            itemFeature.featureType = Il2Cpp.ItemFeature.FeatureType.TemporaryBuying;
            itemFeature.valueStage = Il2Cpp.ItemFeature.ValueStage.Market;
            itemFeature.valueModifier = 0;
            itemFeature.preExposeValueModifier = 100;
            itemFeature.usePreExposeValue = true;
            itemFeature.initiallyShown = true;
            itemFeature.isFeatureMatch = true;
            itemFeature.isFeatureExposed = true;
            itemFeature.isExposable = true;
            itemFeature.isFeatureDiscovered = false;
            itemFeature.publicDisplay = text;
            itemFeature.actualDisplay = text;
            item.itemFeatures.Add(itemFeature);
        }
        catch { }
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
            // 10-06 修复单#4：蛙哥预算固定 1109707341（永不被任何情况干扰）——联动跳过蛙哥（双装 WP+WS 时防止被 ×(1+bonus%)）
            string bid = ""; try { bid = __instance.identifier ?? ""; } catch { }
            if (bid == "wage_brother") return;
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
                // 10-06 修复单#4：蛙哥预算固定——PickClient 现金加成同样跳过蛙哥
                string cid = ""; try { cid = __result.identifier ?? ""; } catch { }
                if (cid == "wage_brother") return;
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
            // 10-10 四问题批#2：删 10-08 二分诊断日志（Debug 门控"用完必删"；拼字符串+访单例=崩溃/性能放大面）
            if (IsRobinsonRun() && SurvivalFood.GetMood() >= 80) { percent += 15; }
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival] 出价上移异常: " + ex.Message); }
    }

    public static void PostfixGetDealMakerBonus(ref int __result)
    {
        try
        {
            // 10-10 四问题批#2：删 10-08 二分诊断日志（v5 startType 结论已实锤入 cheatsheet，双处写入一致由 U1 部署日志确认）
            if (IsRobinsonRun()) __result += SurvivalFood.GetBargainBonusPct();
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival] 谈判成功率异常: " + ex.Message); }
    }

    public static void PostfixItemFeatureListBargainBuyingMarkup(ref ItemFeature __result)
    {
        try
        {
            // 10-10 四问题批#2：删 10-08 二分诊断日志。加价接受率 ×1.15 为意图行为（崩溃根因=主 mod 残留双挂叠加，四问题批#3a 已删死代码）
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

    // 10-07 交易递归防御（Memos KrGLxcw5B4yqEvLb5sTHqQ）：OnDealAccepted Postfix 全挂点防重入——
    // 递归进入（原版 OnDealAccepted→SellItem→…→OnDealAccepted 链）自动跳过，不再二次处理（cheatsheet 7.6 ThreadStatic 模式）
    [ThreadStatic] private static bool _inDealAcceptedPostfix;
    public static void PostfixStoreClientOnDealAccepted(StoreClient __instance)
    {
        if (_inDealAcceptedPostfix) return;
        _inDealAcceptedPostfix = true;
        try
        {
            // 10-05 修复：交易记数全局生效（社交日结算 StoreEventManager.OnDayStart 对所有档跑，
            // 普通档交易也必须 RecordDeal，否则 deals 恒 0 → 打烊结算社交只掉(无交易-5)不加）
            // 10-06 用户拍板：社交即时增加（成交一单 +5）；RecordDeal 只调一次（原 10-05 双调=每单 deals+2 的 bug 一并修）
            RecordDeal();
            if (!IsRobinsonRun()) return;
            string mk = MoodKey(__instance);
            if (__instance != null && mk != null && _moodBoostedClients.Add(mk))
            {
                SurvivalFood.BoostMood(5, LangHelper.T("成交一单", "Deal closed"));
            }
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
        finally { _inDealAcceptedPostfix = false; }
    }

    // 接待计数（社交结算用：SurvivalState.PostfixOnDayStart 读"deals"）
    // 10-06 用户拍板：社交即时增加——成交一单立即 +5（不再等日结），deals 仅作日结防扣标记
    internal static void RecordDeal()
    {
        try
        {
            SaveStore.SetInt("deals", SaveStore.GetInt("deals", 0) + 1);
            int soc = Math.Min(100, SaveStore.GetInt("social", 50) + 5);
            SaveStore.SetInt("social", soc);
            try { StoreUIManager.Instance.Notify(LangHelper.T("成交一单 社交 +5", "Deal closed, Social +5"), "green"); } catch { }
            SurvivalFood.RefreshStatusPanel(); // 面板社交实时刷新
            Core.LogMsg("[WageSurvival] 成交一单 社交即时+5 → " + soc);
        }
        catch { }
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
            // 10-10 四问题批#3b：进程级防重第二层（复合键 labelId+pointer；itemFeatures 同 id 判重为第一层，见下）
            try
            {
                string key = labelId + "|" + item.Pointer.ToInt64();
                if (Patches._nodeBuffItems.Contains(key)) return;
                Patches._nodeBuffItems.Add(key);
            }
            catch { } // 指针读取失败（交互期销毁防御）→ 降级仅依赖 itemFeatures 判重
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
