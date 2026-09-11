using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;
using Il2CppInterop.Runtime;

namespace JacksonPerks;

// 统一的补丁方法类（手动Patch模式）
// 所有Prefix/Postfix方法都在这里，由ManualPatcher调用
internal static class Patches
{
    // 治安检查防重复标志：同一天只触发一次，避免多次调用导致重复检查
    private static int _inspectionTriggeredDay = -1;

    // ============================================================
    // 按键诊断：InputActionManager.Update Postfix，第一次触发时 dump 全部 handler 按键
    // ============================================================
    private static bool _keysDumped = false;
    internal static void PostfixInputActionManagerUpdate(Il2Cpp.InputActionManager __instance)
    {
        if (__instance == null) return;
        try { RobinCrusoePerk.HandleHotkeys(); } catch { } // Z 键：改挂游戏原生每帧钩子（MelonLoader OnUpdate 不触发）
        if (_keysDumped) return;
        _keysDumped = true;
        try
        {
            var handlers = __instance.actionHandlers;
            if (handlers == null) {  return; }
            for (int i = 0; i < handlers.Count; i++)
            {
                var h = handlers[i];
                if (h == null) continue;
                string ks = "(无)";
                try
                {
                    var keys = h.keyListeners;
                    if (keys != null && keys.Count > 0)
                    {
                        var parts = new string[keys.Count];
                        for (int k = 0; k < keys.Count; k++) parts[k] = keys[k].ToString();
                        ks = string.Join(",", parts);
                    }
                }
                catch { }
                // 真实类型名（GetType() 在 IL2CPP 下返回基类）
                string tname = "?";
                try { tname = h.GetType().FullName; } catch { }
                try { tname = h.GetIl2CppType().FullName; } catch { }
            }
        }
        catch (Exception ex) { Core.LogMsg("[按键] Dump 异常: " + ex.Message); }
    }

    // ============================================================
    // 0. 交易方向记录（刀尖舔血：只在玩家卖出时加价）
    // 0=无/其他, 1=玩家买入(BuyMode), 2=玩家卖出(SellMode)
    // ============================================================
    public static int CurrentUITradeMode = 0;
    // 去重：记录上一次处理的item uniqueId，避免UI刷新时重复打日志/重复处理
    private static long _lastSellModeItemUid = -1;
    private static long _lastBuyModeItemUid = -1;

    // ============================================================
    // Bug修复：ClientNoExposeInjector NullReferenceException
    // 卖武器/酒给上层区收货员时，itemFeature为null导致原生方法崩溃
    // Prefix拦截：itemFeature为null时按"无NoExpose限制"返回true，跳过原方法（避免NRE且不阻塞售卖）
    // ============================================================
    public static bool PrefixClientNoExposeInjector(ItemFeature itemFeature, ref bool __result)
    {
        try
        {
            if (itemFeature == null)
            {
                // ISIL实锤：原生 null 会抛 NRE；语义上 null feature = 无 NoExpose 限制 = 不阻塞售卖
                __result = true;
                Core.LogMsg("[Bug修复] ClientNoExposeInjector: itemFeature为null，按无限制处理（true）");
                return false; // 跳过原方法
            }
        }
        catch (Exception ex) { Core.LogMsg("[Bug修复] PrefixClientNoExposeInjector异常: " + ex.Message); }
        return true; // 正常执行原方法
    }

    // UI 进入"玩家卖"模式（刀尖舔血只在此模式加价）
    public static void PostfixUIInitSellMode(GameItem item)
    {
        // 物品出售模式 = 玩家卖出（刀尖舔血只在此模式加价）
        CurrentUITradeMode = 2;
        if (item == null) return;
        long uid = 0;
        try { uid = item.uniqueId; } catch { }
        // 去重：同一个item重复触发（UI刷新）只处理一次
        if (uid == _lastSellModeItemUid) return;
        _lastSellModeItemUid = uid;
        FixTradeItemName(item); // 强制修正交易界面物品名
        TryAddTradeFeatureByUI(item); // 在交易UI初始化时给交易物品加价标签（能正常显示）
    }

    private static void TryAddTradeFeatureByUI(GameItem item)
    {
        try
        {
            if (item == null) return;
            bool isContraband = false;
            try { isContraband = item.IsTag("contraband"); } catch { }
            bool isAlcohol = TraitEffects.IsAlcohol(item);
            TryAddTradeFeature(item, isContraband, isAlcohol);
        }
        catch (Exception ex) { Core.LogMsg("[加价标签UI] 失败: " + ex.Message); }
    }

    // UI 进入"玩家买"模式：给当前物品加"友情价 -5%"原生标签（同时影响价格）
    public static void PostfixUIInitBuyMode(GameItem item)
    {
        // 物品购买模式 = 玩家买入
        CurrentUITradeMode = 1;
        if (item == null) return;
        long uid = 0;
        try { uid = item.uniqueId; } catch { }
        // 去重：同一个item重复触发（UI刷新）只处理一次
        if (uid == _lastBuyModeItemUid) return;
        _lastBuyModeItemUid = uid;
        FixTradeItemName(item); // 强制修正交易界面物品名
        TryApplyFriendDiscountFeature(item);
    }
    // 交易UI关闭：重置交易方向（避免离开交易后属性栏误加价），并移除折扣标签
    public static void PostfixUIClose()
    {
        CurrentUITradeMode = 0;
        _lastSellModeItemUid = -1; // 重置去重，下次打开交易UI重新处理
        _lastBuyModeItemUid = -1;
        RemoveFriendDiscountFeatures();
        RemoveTradeFeatures();
    }
    // ============================================================
    // 1. 特性注册补丁
    // ============================================================
    public static void PostfixInitStartingPerks()
    {
        try
        {
            CustomStartingPerks.EnsureRegistered();
            Core.LogMsg("[特性] 已注册所有自定义特性");
        }
        catch (Exception ex)
        {
            Core.LogMsg("[特性] 注册失败: " + ex.Message);
        }
    }

    // ============================================================
    // 2. 新游戏补丁（应用特性效果）
    // ============================================================
    public static void PostfixOnNewGame()
    {
        try
        {
            // 新游戏：重置 runID 缓存（否则 PerkStatePersistence 沿用旧档 key，计数不重置）
            try { PerkStatePersistence.ResetCache(); } catch { }
            // 新游戏：重置所有特性状态
            FrogPowerPerk.ResetState();
            CustomStartingPerks.NotifyNewGame();
            
            // 蛙哥妙妙箱改由 NewGameData.HandleInitialItem Postfix 给予（与拾荒者信物同一原生机制，
            // 读档不触发，从根本解决多刷/补发），此处不再启动协程
        }
        catch (Exception ex)
        {
            Core.LogMsg("[特性] 新游戏补丁失败: " + ex.Message);
        }
    }
    
    // Patch NewGameData.HandleInitialItem Postfix —— 与拾荒者信物同一原生开局物品机制。
    // HandleInitialItem 只在开新档时调用（读档走 LoadGame 分支），从根本解决多刷/补发。
    public static void HandleInitialItemPostfix()
    {
        try
        {
            if (FrogPowerPerk.IsActive() && !FrogPowerPerk._storageBoxGiven)
            {
                FrogPowerPerk.TryGiveStorageBox();
            }

            // 捡漏直觉：开局给拾荒者工具箱 + 加强探测器
            // 注意：不再在这里 ResetState——过天时 HandleInitialItem 可能被误触发导致计数清零。
            // 计数重置靠 QuitToMenu/OnMainMenu/NewGame 三个 Postfix。
            if (LuckScoutPerk.IsActive()) { try { PerkStatePersistence.ResetCache(); } catch { } LuckScoutPerk.TryGiveKit(); }

            // 命运之骰：开局给骰子（选特性才给，防重由静态标记）
            try { DestinyDicePerk.GiveIfActive(); } catch { }

            // 鲁滨逊的账本（职业 startType=14）：资金360 + 口粮 + 状态初始化
            try { RobinCrusoePerk.TrySetupNewRun(); } catch { }
        }
        catch (Exception ex)
        {
            Core.LogMsg("[蛙哥牛逼] HandleInitialItemPostfix异常: " + ex.Message);
        }
    }

    // 检查未来队列和客户端栈里是否已有博士（避免原生+我们双排）
    private static bool HasJacksonQueued()
    {
        try
        {
            var ps = PlayerStore.Instance;
            if (ps == null) return false;
            var queue = ps.futurStoreClientIdQueue;
            if (queue != null)
            {
                for (int i = 0; i < queue.Count; i++)
                    if (queue[i] == "inventorStorage" || queue[i] == "inventor_storage") return true;
            }
            var mgr = ps.storeClientManager;
            if (mgr != null)
            {
                var stack = mgr.clientStack;
                if (stack != null)
                {
                    for (int i = 0; i < stack.Count; i++)
                    {
                        var c = stack[i];
                        if (c != null && c.identifier != null &&
                            (c.identifier == "inventorStorage" || c.identifier == "inventor_storage")) return true;
                    }
                }
            }
        }
        catch { }
        return false;
    }
    // 博士调度去重：同一天只排一次（LoadGame + OnNewDay 可能同天触发）
    private static int _lastScheduledDay = -1;
    // 博士是新档未介绍的特殊客户，生成可能被 isJacksonIntroduced=false 跳过，排队前强制设为已介绍
    private static void EnsureJacksonIntroduced()
    {
        try
        {
            var scd = PlayerStore.GetStoreClientData();
            if (scd != null && !scd.isJacksonIntroduced)
            {
                scd.isJacksonIntroduced = true;
            }
        }
        catch (Exception ex) { Core.LogMsg("[博士之友] 设置isJacksonIntroduced失败: " + ex.Message); }
    }

    internal static bool ScheduleJacksonToday()
    {
        try
        {
            if (!DrJacksonFriendPerk.IsActive()) return false;
            if (PlayerStore.Instance == null) return false;
            int day = StoreStation.GetDayCounter();
            // 先检查队列/栈中是否已有博士（防双排），再判断间隔——顺序不能反
            if (HasJacksonQueued()) {  return false; }
            // 博士每7天来访一次：第1天必来，之后间隔>=7天才再来（不猜周几，按间隔计数）
            if (_lastScheduledDay >= 0 && (day - _lastScheduledDay) < 7)
            {
                return false;
            }
            _lastScheduledDay = day;
            EnsureJacksonIntroduced();
            try
            {
                PlayerStore.Instance.QueueFuturClient("inventorStorage", 1);
            }
            catch (Exception qex) { Core.LogMsg("[博士之友] QueueFuturClient 失败: " + qex.Message); return false; }

            return true;
        }
        catch (Exception ex)
        {
            Core.LogMsg("[博士之友] ScheduleJacksonToday失败: " + ex.Message);
            return false;
        }
    }


    // 进存档第一天补丁（开新档走NewGame不触发LoadGame，第一天用BeginDay确保排博士）
    public static void PostfixOnBeginDay()
    {
        try
        {
            ForceInspectionToday();
            ApplyBadLuck();
            

            if (DrJacksonFriendPerk.IsActive() && PlayerStore.Instance != null)
            {
                ScheduleJacksonToday();
            }

        }
        catch (Exception ex)
        {
            Core.LogMsg("[博士之友] BeginDay失败: " + ex.Message);
        }
    }




    // ============================================================
    // 招贼体质（20%概率）/ 刀尖舔血（100%概率）：触发治安部检查
    // OnNewDay（跨天）与 BeginDay（进档）都会调用，进档当天即可能检查
    // ============================================================
    public static void ForceInspectionToday()
    {
        try
        {
            if (!RiskTakerPerk.IsActive() && !ThiefMagnetPerk.IsActive())
            {
                return;
            }
            
            // 防重复：同一天只触发一次，避免多次调用导致重复检查
            int currentDay = 1;
            try { currentDay = StoreStation.GetDayCounter(); } catch { }
            if (_inspectionTriggeredDay == currentDay)
            {
                return;
            }
            
            // 概率判断：刀尖舔血100%，招贼体质20%（用确定性随机数，读档后一致）
            bool riskTakerActive = RiskTakerPerk.IsActive();
            bool thiefMagnetActive = ThiefMagnetPerk.IsActive();
            bool shouldTrigger = riskTakerActive; // 刀尖舔血100%触发
            if (!riskTakerActive && thiefMagnetActive)
            {
                // 只有招贼体质时，20%概率触发（确定性随机数）
                shouldTrigger = DeterministicRandom.NextBool("thief_magnet_inspection", currentDay, 0.2);
            }
            if (!shouldTrigger)
            {
                return;
            }

            // 刀尖舔血100%触发，招贼体质20%触发
            try
            {
                if (RiskTakerPerk.IsActive() || ThiefMagnetPerk.IsActive())
                {
                    PlayerStore instance = PlayerStore.Instance;
                    if (instance != null && instance.secData != null)
                    {

                        // 获取StoreClientManager
                        StoreClientManager manager = null;
                        try
                        {

                            var managerProp = typeof(PlayerStore).GetProperty("storeClientManager",
                                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            if (managerProp != null)
                            {
                                manager = managerProp.GetValue(instance) as StoreClientManager;
                            }
                            
                            // 尝试字段 storeClientManager
                            if (manager == null)
                            {
                                var managerField = typeof(PlayerStore).GetField("storeClientManager",
                                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                if (managerField != null)
                                {
                                    manager = managerField.GetValue(instance) as StoreClientManager;
                                }
                            }
                        }
                        catch (Exception mgrEx)
                        {
                            Core.LogMsg("[刀尖舔血] 获取storeClientManager失败: " + mgrEx.Message);
                        }
                        
                        if (manager != null)
                        {
                            
                            try
                            {
                                // 强制设置检查条件
                                manager.dayUntilInspection = 0;  // 距离下次检查0天
                                manager.daySinceInspection = 100; // 距离上次检查100天
                                manager.inspectionSeeded = true;   // 检查已播种
                                // 重置检查（可能会重新计算冷却时间，所以之后再设置一次）
                                manager.ResetInspection();
                                
                                // 再次强制设置检查条件
                                manager.dayUntilInspection = 0;
                                manager.daySinceInspection = 100;
                                manager.inspectionSeeded = true;
                                // 官方开始检查 API（HandleInspectionClient 只播种不生成）
                                try
                                {
                                    int personality = instance.secData.GetInspectorPersonality();
                                    ContrabandHelper.StartInspection(personality, false);
                                    // 标记今天已触发检查，防止重复
                                    try { _inspectionTriggeredDay = StoreStation.GetDayCounter(); } catch { _inspectionTriggeredDay = currentDay; }
                                }
                                catch (Exception siEx)
                                {
                                    Core.LogMsg("[治安检查] StartInspection失败: " + siEx.Message);
                                }
                                // 诊断：dump clientStack 看检查客户是否入栈
                                try
                                {
                                    var stack = manager.clientStack;
                                    int sc = stack != null ? stack.Count : -1;
                                    if (stack != null && sc > 0)
                                    {
                                        for (int si = 0; si < sc; si++)
                                        {
                                            try
                                            {
                                                StoreClient cl = stack[si];
                                            }
                                            catch { }
                                        }
                                    }
                                }
                                catch (Exception stEx)
                                {
                                    Core.LogMsg("[治安检查] clientStack诊断失败: " + stEx.Message);
                                }
                                
                            }
                            catch (Exception handleEx)
                            {
                                Core.LogMsg("[刀尖舔血] 设置检查条件失败: " + handleEx.Message);
                            }
                        }
                        else
                        {
                            
                            // 备用方法：ContrabandHelper.StartInspection
                            try
                            {
                                int personality = instance.secData.GetInspectorPersonality();
                                ContrabandHelper.StartInspection(personality, false);
                                // 标记今天已触发检查，防止重复
                                try { _inspectionTriggeredDay = StoreStation.GetDayCounter(); } catch { _inspectionTriggeredDay = currentDay; }
                            }
                            catch (Exception startEx)
                            {
                                Core.LogMsg("[刀尖舔血] StartInspection失败: " + startEx.Message);
                            }
                        }
                    }
                }
            }
            catch (Exception inspectEx)
            {
                Core.LogMsg("[刀尖舔血] 触发检查失败: " + inspectEx.Message);
            }
        }
        catch (Exception ex)
        {
            Core.LogMsg("[治安检查] 强制检查失败: " + ex.Message);
        }
    }


    // 霉运缠身：每天打烊必丢钱（100%）——BeginDay 与 OnNewDay 都会调用
    public static void ApplyBadLuck()
    {
        try
        {
            if (!BadLuckPerk.IsActive()) return;
            PlayerStore luckInst = PlayerStore.Instance;
            if (luckInst == null) { Core.LogMsg("[霉运缠身] PlayerStore为null"); return; }
            // 用确定性随机数，读档后丢钱金额一致
            int day = StoreStation.GetDayCounter();
            int loss = DeterministicRandom.Next("bad_luck", day, 50, 201);
            bool deducted = false;
            try
            {
                var moneyProp = typeof(PlayerStore).GetProperty("playerCash", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (moneyProp != null)
                {
                    int cur = (int)moneyProp.GetValue(luckInst);
                    moneyProp.SetValue(luckInst, Math.Max(0, cur - loss));
                    deducted = true;
                }
            }
            catch (Exception pex) { Core.LogMsg("[霉运缠身] 属性扣款失败: " + pex.Message); }
            if (!deducted)
            {
                try
                {
                    var moneyField = typeof(PlayerStore).GetField("playerCash", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (moneyField != null)
                    {
                        int cur = (int)moneyField.GetValue(luckInst);
                        moneyField.SetValue(luckInst, Math.Max(0, cur - loss));
                        deducted = true;
                    }
                }
                catch (Exception fex) { Core.LogMsg("[霉运缠身] 字段扣款失败: " + fex.Message); }
            }
            if (deducted)
            {
                Core.LastNightReportLine = "[霉运缠身] " + LangHelper.T("昨晚打烊时，有人趁夜色摸走了你", "Last night after closing, someone slipped in and took") + " " + loss + " " + LangHelper.T("信用点。", "credits.") + "";
            }
            else Core.LogMsg("[霉运缠身] 扣钱失败：playerCash 属性/字段均未找到");
        }
        catch (Exception luckEx)
        {
            Core.LogMsg("[霉运缠身] 失败: " + luckEx.Message);
        }
    }

    // ============================================================
    // 3. 新的一天补丁
    // ============================================================
    public static void PostfixOnNewDay()
    {
        try
        {
            // 好酒之徒：新的一天清除宿醉
            WineLoverPerk.ClearHangover();

            // 特殊NPC：每天重置处理记录（博士天天来需要每天重新放货）
            SpecialNpcManager.OnNewDay();

            // 特殊NPC：改为只走 HandleContentUnlockClient 通道（参考XIAOWO），禁用 QueueFuturClient 重复通道
            // SpecialNpcManager.ScheduleDailyNpcs();



            // 博士之友：每天安排博士来访（QueueFuturClient官方蓝图，货物自动初始化）
            try
            {
                if (DrJacksonFriendPerk.IsActive() && PlayerStore.Instance != null)
                {
                if (ScheduleJacksonToday()) { }
                }
            }
            catch (Exception ex)
            {
                Core.LogMsg("[博士之友] QueueFuturClient失败: " + ex.Message);
            }

            ForceInspectionToday();


            ApplyBadLuck();


        }
        catch (Exception ex)
        {
            Core.LogMsg("[特性] 新的一天补丁失败: " + ex.Message);
        }
    }

    // ============================================================
    // 3.4 博士来访频率补丁（每14天来一次，Prefix返回false阻止博士来）
    // ============================================================
    public static bool PrefixOnHandleJacksonStorage(StoreClientManager __instance)
    {
        try
        {
            return DrJacksonFriendPerk.HandleJacksonStoragePatch.Prefix(__instance);
        }
        catch (Exception ex)
        {
            Core.LogMsg("[博士之友] 频率控制失败: " + ex.Message);
            return true;
        }
    }

    // ============================================================
    // 3.5 客户添加补丁（关键修复：特殊NPC/博士都依赖AddClient事件触发）
    // ============================================================
    public static void PostfixOnAddClient(StoreClient storeClient)
    {
        if (storeClient == null) return;

        // 1. 博士之友：博士来店时扩充库存（加神经模组+高价值物品）
        try
        {
            DrJacksonFriendPerk.AddClientPatch.Postfix(storeClient);
        }
        catch (Exception ex)
        {
            Core.LogMsg("[博士之友] AddClient Postfix失败: " + ex.Message);
        }

        // 2. 特殊NPC：统一显示名字（官方蓝图生成后改名）
        try
        {











            NormalizeSpecialNpcName(storeClient);
        }
        catch (Exception ex)
        {
            Core.LogMsg("[特殊NPC] 统一名字失败: " + ex.Message);
        }
        // 3. 酒商：收酒无上限预算（OverrideBudget + clientCash 极大值）
        try
        {
            if (storeClient.identifier == "retired_winemaker")
            {
                // 酒商既要卖酒也要收购：意图改成 SELLNBUY（原来是 SELL 只有卖）
                storeClient.clientIntent = Il2Cpp.StoreClient.ClientIntent.SELLNBUY;
                // 预算 = 玩家背包酒类总价值*1.5（确保能买下当前所有酒），保底1000
                long wineVal = 0;
                try
                {
                    var invItems = Il2Cpp.EmporiumEntry.Instance.GetInvItems();
                    if (invItems != null)
                    {
                        foreach (var gi in invItems)
                        {
                            if (gi == null) continue;
                            string gid = gi.identifier;
                            if (string.IsNullOrEmpty(gid)) continue;
                            bool isW = gid.Contains("wine") || gid.Contains("beer") || gid.Contains("alcohol") || gid == "wine_bottle";
                            if (!isW) continue;
                            try { wineVal += gi.GetCurrentValue(); } catch { }
                        }
                    }
                }
                catch { }
                int budget = (int)System.Math.Min(wineVal * 1.5, int.MaxValue - 1);
                if (budget < 1000) budget = 1000;
                storeClient.OverrideBudget(budget);
                storeClient.clientCash = budget;
                storeClient.useClientBudget = true;
                // 收酒：往收购列表加酒类物品（让他主动收酒）
                string[] wineItems = { "beer_case", "red_beer", "wine_bottle", "wine_berry", "wine_bloomberry", "wine_gloomberry", "empty_beer_bottle" };
                if (storeClient.clientBuyingIdList == null)
                    storeClient.clientBuyingIdList = new Il2CppSystem.Collections.Generic.List<string>();
                foreach (string wid in wineItems)
                {
                    if (!storeClient.clientBuyingIdList.Contains(wid))
                        storeClient.clientBuyingIdList.Add(wid);
                }
                // 加收购标签 + 清理黑名单（蓝图可能把酒类设为拒绝收购，黑名单优先于白名单）
                string[] wineTags = { "alcohol", "wine", "beer", "drink", "beverage", "liquor", "booze" };
                if (storeClient.clientBuyingTagList == null)
                    storeClient.clientBuyingTagList = new Il2CppSystem.Collections.Generic.List<string>();
                foreach (string wt in wineTags)
                    if (!storeClient.clientBuyingTagList.Contains(wt))
                        storeClient.clientBuyingTagList.Add(wt);
                if (storeClient.clientBlackIdList != null)
                    foreach (string wid in wineItems)
                        storeClient.clientBlackIdList.Remove(wid);
                if (storeClient.clientBlackTagList != null)
                    foreach (string wt in wineTags)
                        storeClient.clientBlackTagList.Remove(wt);
            }
        }
        catch (Exception ex)
        {
            Core.LogMsg("[酒商] 设置预算失败: " + ex.Message);
        }
    }

    // 统一特殊NPC显示名（官方蓝图用官方名，改成我们的命名）
    private static void NormalizeSpecialNpcName(StoreClient client)
    {
        if (client == null) return;
        string id = client.identifier;

        string newName = null;
        switch (id)
        {
            case "retired_gunsmith":
                newName = LangHelper.T("退休枪匠", "Retired Gunsmith");
                break;
            case "retired_water_merchant":
                newName = LangHelper.T("水商", "Water Merchant");
                break;
            case "retired_winemaker":
                newName = LangHelper.T("酒商", "Winemaker");
                break;
        }

        if (newName != null && client.displayName != newName)
        {
            string oldName = client.displayName;
            client.displayName = newName;
        }
    }

    // ============================================================
    // 3.6 客户进店对话开始补丁（核心统一入口，补丁事件驱动，完全替代OnUpdate轮询）
    // 客户进店、交易区就绪时统一处理：对话修改 + 随机商品
    // ============================================================

    // 判断是否为治安部客户（治安部卖/买家不带随机物品）
    private static bool IsSecurityClient(StoreClient c)
    {
        try
        {
            if (c == null) return false;
            if (c.isSecurity) return true;
            string id = c.identifier ?? "";
            if (id == "patrolOfficer" || id == "security_inspector" || id == "lazy_security_inspector" ||
                id == "shady_security_inspector" || id == "security_officer" || id == "security_requisition_officer" ||
                id == "off_duty_officer" || id == "private_security_contractor" || id == "officer_lun")
                return true;
        }
        catch { }
        return false;
    }


    // ============================================================
    // 博士上货最稳方案（参考 ItemForge CustomerDropPatch）：
    // Patch PlayerStore.AddDirectSellingItemToTable Postfix —— 博士商店任何上货都走这个方法，
    // 不管入口是 PlaceInventorInventory / CreateInventor / 其他，只要博士上货就触发
    // Busy 防递归（我们上货内部也调 AddDirectSellingItemToTable）
    // ============================================================
    internal static bool _inJacksonInject = false;

    internal static long _lastSellLogTick = 0;

    internal static void PostfixAddDirectSellingItemToTable(PlayerStore __instance, GameItem gameItem)
    {
        if (_inJacksonInject || __instance == null) return;
        try
        {
            string seller = "";
            try
            {
                if (__instance.currentClientInstance != null)
                {
                    var cb = __instance.currentClientInstance.GetClientBlueprint();
                    if (cb != null) seller = cb.identifier ?? "";
                }
            }
            catch { }
            // 诊断：低频打印所有上货（确认博士商店是否走 AddDirectSellingItemToTable 及 seller 值）
            if (Environment.TickCount64 - _lastSellLogTick > 5000)
            {
                _lastSellLogTick = Environment.TickCount64;
            }
            if (seller != "inventor" && seller != "inventorStorage" && seller != "inventor_storage") return;
            _inJacksonInject = true;
            try { DrJacksonFriendPerk.AddJacksonGoodsToCounter(null); }
            finally { _inJacksonInject = false; }
        }
        catch (Exception ex) { Core.LogMsg("[博士之友] PostfixAddDirectSellingItemToTable 异常: " + ex.Message); }
    }

    // ============================================================
    // 博士商店开张权威点：Patch StoreClientList.PlaceInventorInventory Postfix
    // （玩家拜访博士 VisitUpgradeMerchant -> b__33_0 -> PlaceInventorInventory）
    // 方法执行 = 博士商店上货，无条件补我们的货（不判定 seller——博士商店场景 currentClientInstance 不一定是博士）
    // ============================================================
    internal static void PostfixPlaceInventorInventory(bool isVisitingPlayerStore)
    {
        try
        {
            DrJacksonFriendPerk.AddJacksonGoodsToCounter(null);
        }
        catch (Exception ex) { Core.LogMsg("[博士之友] PostfixPlaceInventorInventory 异常: " + ex.Message); }
    }

    public static void PostfixSpecialNpcStartDialogue(StoreUIManager __instance)
    {
        try
        {
            // OnNextClientArrived 的参数是 StoreUIManager（非客户），当前客户从 PlayerStore 取（v5.3.1 验证写法）
            StoreClient client = SpecialNpcManager.GetCurrentClient();
            if (client == null) return;
            string clientName = client.displayName ?? client.identifier ?? LangHelper.T("未知", "Unknown");
            bool isSpecial = SpecialNpcManager.IsSpecialNpc(client);

            // 1. 特殊NPC（商人之友/退休枪匠之友）：添加随机商品（防重复）
            if (isSpecial)
            {
                SpecialNpcManager.HandleSpecialNpcArrived(client);
            }

            // 2.0b 屠夫/李北文供应商（鲁滨逊职业内）：到店解锁电话簿 + 上货（wanted2=屠夫 / wanted6=李北文）
            try
            {
                string sid = client.identifier;
                if (sid == "wanted2" || sid == "wanted6")
                    RobinCrusoePerk.WantedSupplierOnArrived(client);
            }
            catch { }

            // 2.0 成瘾警官：伪装客户到达判定分支（通用事件，不依赖特性）
            try { AddictOfficerEvent.OnClientArrived(client); } catch { }

            // 2. 购买标签：对所有 BUY 客户补充需求标签（通用功能，不依赖蛙哥牛逼）
            // 已有标签保留；无标签从购买清单生成；都没有才随机生成
            if (!isSpecial && !IsSecurityClient(client) &&
                client.clientIntent == StoreClient.ClientIntent.BUY)
            {
                FrogPowerPerk.EnsureBuyTagsForClient(client);
            }

            // 3. 对话修改：已完全禁用（用户要求全改回原版对话，即使激活蛙哥牛逼也不改）
            // 蛙哥：普通客户（非特殊NPC）随机商品（保留此功能，仅禁用对话修改）
            if (FrogPowerPerk.IsActive() && !isSpecial && !IsSecurityClient(client) &&
                (client.clientIntent == StoreClient.ClientIntent.SELL ||
                 client.clientIntent == StoreClient.ClientIntent.SELLNBUY))
            {
                FrogPowerPerk.AddRandomItemsToCounter(client);
            }
            // 未选蛙哥牛逼时不再调用GlobalModifyDialogue改全局对话
        }
        catch (Exception ex)
        {
            Core.LogMsg("[客户到达] OnNextClientArrived补丁失败: " + ex.Message);
        }
    }

    // ============================================================
    // 3.9 特殊NPC交易台词补丁（DisplayClientText 拦截）
    // 开场白已在 StartMainDialogue 改为专属对话；
    // 但交易台词（卖货"我有些用不上的东西"、买货"我想买xxx"等原版台词）
    // 仍显示原版，这里拦截替换为符合身份的专属交易台词。
    // ============================================================
    public static void PrefixDisplayClientText(Dialogue dialogue)
    {
        try
        {
            if (dialogue == null) return;
            StoreClient client = SpecialNpcManager.GetCurrentClient();
            if (client == null) return;

            // 普通客户：出戏对话替换+诊断日志
            if (!SpecialNpcManager.IsSpecialNpc(client))
            {
                FixCringeDialogue(dialogue, client);
                return;
            }

            string id = client.identifier;
            string name = client.displayName ?? id;

            // 开场白（mainDialogue）：游戏在 StartMainDialogue 内部先显示原版，
            // 改 mainDialogue 字段太晚（已显示原版）。这里在显示前直接替换为专属开场白。
            if (client.mainDialogue != null && dialogue == client.mainDialogue)
            {
                string greeting = GetSpecialNpcGreeting(id);
                if (greeting != null)
                {
                    dialogue.SetText(name, greeting);
                }
                return;
            }

            // 交易台词（非开场白）：替换为专属交易台词
            string text = dialogue.plainText ?? "";
            bool isTradeLine = text.Contains("收不收") || text.Contains("想买") || text.Contains("要买") ||
                               text.Contains("有没有") || text.Contains("买点") || text.Contains("看看") ||
                               text.Contains("卖") || text.Contains("买") || text.Contains("收") ||
                               text.Contains("钱") || text.Contains("价") || text.Contains("东西");
            if (!isTradeLine) return;

            bool isBuying = client.clientIntent == StoreClient.ClientIntent.BUY;
            string newText = GetSpecialNpcTradeLine(id, isBuying);
            if (newText == null) return;

            dialogue.SetText(name, newText);
        }
        catch (Exception ex)
        {
            Core.LogMsg("[交易台词] 替换失败: " + ex.Message);
        }
    }

    // ============================================================
    // 普通客户出戏对话修复
    // ============================================================
    // 已知出戏对话替换规则：key=原文关键词，value=替换后的完整对话
    // 后续根据日志持续补充
    private static readonly Dictionary<string, string> _cringeReplacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        // 游客的出戏对话
        { "好有故事感啊", LangHelper.T("哇，这地方太有赛博朋克那味儿了！老板，这东西多少钱？我买个纪念。", "Wow, this place reeks of cyberpunk! Boss, how much is this? I want a souvenir.") },
        { "你这店开了多久了", LangHelper.T("哇，这地方太有赛博朋克那味儿了！老板，这东西多少钱？我买个纪念。", "Wow, this place reeks of cyberpunk! Boss, how much is this? I want a souvenir.") },
        { "你们这的东西都好特别啊", LangHelper.T("哇，你们这的东西都好有特色！我都想买，就是预算不太够，老板能便宜点不？", "Wow, your stuff is so distinctive! I want it all, but my budget's tight - can you do a deal, boss?") },
        { "有生锈的废料之类的吗", LangHelper.T("老板，有那种废旧零件、废铁之类的吗？我带回去当摆件，朋友看了肯定觉得酷！", "Boss, got any old parts or scrap? I'll take them home as decor - my friends will think it's cool!") },
        { "我想多来几种", LangHelper.T("多来几种，我挑挑，回去送朋友也合适。", "Give me a few kinds - I'll pick. Good as gifts back home.") },
        { "哎呀老板你好呀", LangHelper.T("哇老板你好！我是来旅游的，你们这有什么特色好东西吗？我想买点回去当纪念品。", "Wow, hi boss! I'm a tourist - got anything special here? I want souvenirs.") },
        { "来点吃的、喝的", LangHelper.T("来点吃的喝的，再来点能解压的好东西？回去送人也合适。", "Some food and drinks, plus something stress-relieving? Great for gifts.") },
        { "收不收信用卡", LangHelper.T("老板，你们这收不收信用芯片啊？我现金不太够了。什么？不收？那算了，我就买这个便宜点的吧，多少钱？", "Boss, do you take credit chips? I'm short on cash. What? No? Fine, I'll take this cheaper one - how much?") },
        { "裱起来放家里", LangHelper.T("有没有小型武器？你懂的，我想裱起来放家里展示，朋友来了肯定觉得有品位！", "Got any small weapons? You know - I want to frame one for display. Guests will think I've got class!") },
    };

    // 记录已输出的对话，避免重复日志
    private static readonly HashSet<string> _loggedDialogues = new HashSet<string>();

    private static void FixCringeDialogue(Dialogue dialogue, StoreClient client)
    {
        try
        {
            string clientName = client.displayName ?? LangHelper.T("未知", "Unknown");
            string clientId = client.identifier ?? LangHelper.T("未知", "Unknown");
            string originalText = dialogue.plainText ?? "";
            if (string.IsNullOrEmpty(originalText)) return;

            // 诊断日志：记录所有普通客户对话（去重）
            string logKey = clientId + "|" + originalText;
            if (!_loggedDialogues.Contains(logKey))
            {
                _loggedDialogues.Add(logKey);
                string shortText = originalText.Length > 150 ? originalText.Substring(0, 150) + "..." : originalText;
            }

            // 查找匹配的替换规则
            foreach (var kv in _cringeReplacements)
            {
                if (originalText.Contains(kv.Key, StringComparison.OrdinalIgnoreCase))
                {
                    dialogue.SetText(clientName, kv.Value);
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            Core.LogMsg("[对话修复] 失败: " + ex.Message);
        }
    }

    // 特殊NPC专属开场白（引用 FrogPowerPerk 已写好的对话库）
    private static string GetSpecialNpcGreeting(string id)
    {
        switch (id)
        {
            case "retired_gunsmith":
                return FrogPowerPerk.GunsmithDialogues[Core.Rng.Next(FrogPowerPerk.GunsmithDialogues.Length)];
            case "retired_water_merchant":
                return FrogPowerPerk.WaterMerchantDialogues[Core.Rng.Next(FrogPowerPerk.WaterMerchantDialogues.Length)];
            case "retired_winemaker":
                return FrogPowerPerk.AlcoholMerchantDialogues[Core.Rng.Next(FrogPowerPerk.AlcoholMerchantDialogues.Length)];
            case "inventorStorage":
            case "inventor_storage":
                return DrDialogues[Core.Rng.Next(DrDialogues.Length)];
            default:
                return null;
        }
    }

    private static string GetSpecialNpcTradeLine(string id, bool isBuying)
    {
        switch (id)
        {
            case "retired_gunsmith":
                return isBuying ? GunsmithBuyTradeLines[Core.Rng.Next(GunsmithBuyTradeLines.Length)]
                                : GunsmithSellTradeLines[Core.Rng.Next(GunsmithSellTradeLines.Length)];
            case "retired_water_merchant":
                return isBuying ? WaterBuyTradeLines[Core.Rng.Next(WaterBuyTradeLines.Length)]
                                : WaterSellTradeLines[Core.Rng.Next(WaterSellTradeLines.Length)];
            case "retired_winemaker":
                return isBuying ? WineBuyTradeLines[Core.Rng.Next(WineBuyTradeLines.Length)]
                                : WineSellTradeLines[Core.Rng.Next(WineSellTradeLines.Length)];
            case "inventorStorage":
            case "inventor_storage":
                return isBuying ? DrBuyTradeLines[Core.Rng.Next(DrBuyTradeLines.Length)]
                                : DrSellTradeLines[Core.Rng.Next(DrSellTradeLines.Length)];
            default:
                return null;
        }
    }

    // 退休枪匠 - 卖货台词
    private static readonly string[] GunsmithSellTradeLines = {
        LangHelper.T("军需库清出来的老货，你给看看，都是压箱底的好东西。", "Old stock from the quartermaster's stores - take a look, all hidden gems."),
        LangHelper.T("这些东西我留着也用不上了，你识货就收下，价好说。", "I've got no use for these anymore. If you know your stuff, take them - price is negotiable."),
        LangHelper.T("当年攒下的家伙事儿，如今用不上了，便宜给你这些识货的。", "Gear I hoarded back in the day - no use now. Cheap for someone who appreciates it."),
        LangHelper.T("枪械配件、弹药，我这有的是，你挑挑，别跟我客气。", "Gun parts and ammo, I've got plenty. Take your pick, don't be shy."),
        LangHelper.T("上边淘汰下来的枪件，我修了修还能用，你看看值多少。", "Gun parts scrapped up top - I fixed them up, still work. See what they're worth."),
        LangHelper.T("这把老伙计跟了我三十年，如今也该找个识货的下家了。", "This old friend served me thirty years. Time to find it a worthy new home.")
    };

    // 退休枪匠 - 买货台词
    private static readonly string[] GunsmithBuyTradeLines = {
        LangHelper.T("帮我留意点好零件，我这把老家伙还等着换件呢。", "Keep an eye out for good parts - this old piece of mine needs replacements."),
        LangHelper.T("有枪械配件和弹药就给我留着，这周要用。", "Set aside any gun parts and ammo - need them this week."),
        LangHelper.T("我这缺几样配件，你收的时候帮我留意着点。", "Missing a few parts. When you take in goods, keep me in mind."),
        LangHelper.T("老规矩，有好货先想着我，我给的价不亏你。", "Same as always - think of me first for the good stuff. I pay fair."),
        LangHelper.T("零件、火药、模具，有合适的都给我留着。", "Parts, powder, molds - hold onto anything suitable."),
        LangHelper.T("上次那批枪件不错，这次有类似的再叫我。", "Last batch of gun parts was good. Call me if similar comes in.")
    };

    // 水商 - 卖货台词
    private static readonly string[] WaterSellTradeLines = {
        LangHelper.T("新一批纯水，从净水厂直接运来的，你看看成色。", "Fresh batch of pure water, straight from the treatment plant. Check the quality."),
        LangHelper.T("这水干净，没有下层的怪味，你给个实在价。", "Clean water, no lower-level stink. Give me a fair price."),
        LangHelper.T("老顾客了，这桶水给你留的，看看要不要。", "You're a regular - saved this jug for you. Want it?"),
        LangHelper.T("纯水和优质水，我这都有，你挑挑看。", "Pure and premium water, I've got both. Take a look.")
    };

    // 水商 - 买货台词
    private static readonly string[] WaterBuyTradeLines = {
        LangHelper.T("有没有便宜的水源？只要能喝，我都要。", "Got any cheap water sources? If it's drinkable, I'll take it."),
        LangHelper.T("你收水吗？有好水源就给我留着。", "Do you buy water? Save any good sources for me."),
        LangHelper.T("这周的水不够卖，你有路子就匀我点。", "Out of water this week - spare me some if you have a source."),
        LangHelper.T("水质好点的水，你有就给我留着，价好说。", "Good quality water - save it for me, price is flexible.")
    };

    // 收酒商 - 卖货台词（只卖酿酒原料，不卖酒）
    private static readonly string[] WineSellTradeLines = {
        LangHelper.T("新到的一批精选葡萄，成色好得很，你酿酒会用得上。", "Fresh batch of select grapes, excellent quality - perfect for brewing."),
        LangHelper.T("这特级酵母我托人从上边弄来的，发酵力强，酿出来的酒品质高。", "Top-grade yeast smuggled from up top - strong fermentation, high-quality wine."),
        LangHelper.T("纯净水，没有下层区的怪味，酿酒用这个最合适。", "Pure water, no lower-level stink. Ideal for brewing."),
        LangHelper.T("这批原料质量好，你要是不要，我可就给别家了。", "Top-quality ingredients. If you don't take them, I'll find another buyer.")
    };

    // 收酒商 - 买货台词（收购玩家自酿酒）
    private static readonly string[] WineBuyTradeLines = {
        LangHelper.T("你酿的酒呢？拿出来我看看，价格好商量，绝对不让你亏。", "Show me your brew - price is flexible, I won't short you."),
        LangHelper.T("有自酿的好酒吗？果子酿的粮食酿的都行，我看看品质。", "Any good homebrew? Fruit or grain, doesn't matter - let me check the quality."),
        LangHelper.T("这周收的酒都卖完了，你酿的有富余就匀给我点。", "Sold out of wine this week - spare me some of your surplus."),
        LangHelper.T("老客户了，你酿的酒我信得过，有新酿的就叫我，价好说。", "You're a regular - I trust your brew. Call me when a new batch is ready, price is fair."),
        LangHelper.T("听说你又酿了批新酒？拿出来尝尝，品质好我给个公道价。", "Heard you've got a new batch? Let me taste it - good quality earns a fair price.")
    };

    // 博士（inventorStorage）- 开场白（贴合博士之友：老朋友/供货商/中间人）
    private static readonly string[] DrDialogues = {
        LangHelper.T("……是你啊。进来吧，这周的好货都给你备着了。", "...It's you. Come in - this week's good stock is set aside for you."),
        LangHelper.T("又见面了。我这儿的东西，别人可轻易摸不着。", "We meet again. My goods aren't easy for just anyone to get."),
        LangHelper.T("老朋友，今天给你带了几台像样的机器，你看看成色。", "Old friend - brought you a few decent machines today. Check them out."),
        LangHelper.T("你来得正好，这几样压箱底的设备，正好想着给你留的。", "Perfect timing - was saving these choice pieces of equipment for you."),
        LangHelper.T("老规矩，我这儿来的都是正经路子。这批货，你先挑。", "Same as always - everything here is legit. This batch, you pick first.")
    };

    // 博士 - 卖货台词（制造/能源设备）
    private static readonly string[] DrSellTradeLines = {
        LangHelper.T("熔炉模组和能量电池，刚从厂里匀出来，给你留着呢。", "Furnace modules and energy cells - just pulled from the factory, saved for you."),
        LangHelper.T("老朋友了，好东西自然先想着你。这批制造设备，你收不收？", "Old friend - good stuff goes to you first. Want this manufacturing gear?"),
        LangHelper.T("我这儿的东西都是别人拿不到的。熔炉、电池，你挑挑看。", "What I have, others can't get. Furnaces, batteries - take your pick."),
        LangHelper.T("设备我都替你验过了，能用。你收走，比从别处买划算得多。", "I've tested all this gear - it works. Buying from me beats anywhere else."),
        LangHelper.T("大机器和大储存，我这儿管够。你要的话，价好商量。", "Big machines and big storage - I've got plenty. Price is flexible.")
    };

    // 博士 - 买货台词（收材料）
    private static readonly string[] DrBuyTradeLines = {
        LangHelper.T("有材料就给我留着，我那几台机器正等着喂料呢。", "Save materials for me - my machines are hungry."),
        LangHelper.T("你收材料的时候帮我留意着点，金属、元件我都要。", "When you take in materials, keep me in mind - metals and components, I'll take both."),
        LangHelper.T("老伙计，有好材料先想着我，我出的价比市场公道。", "Old friend - think of me first for good materials. I pay above market."),
        LangHelper.T("这周的料不够了，你有渠道就匀我点，下回算你优惠。", "Short on materials this week - spare me some if you have a source. I'll make it up next time."),
        LangHelper.T("废料也好，稀有金属也罢，只要是能熔的，我都要。", "Scrap or rare metals - if it can be smelted, I'll take it.")
    };

    public static void PostfixOnHandleContentUnlockClient(StoreClientManager __instance)
    {
        if (__instance == null) return;
        SpecialNpcManager.HandleContentUnlockPostfix(__instance);
    }

    // ============================================================
    // 4. 特性UI补丁
    // ============================================================
    public static void PostfixPerkUiOpen(PerkUIController __instance)
    {
        try
        {
            // 确保特性已注册（特性选择界面打开时的最后机会）
            CustomStartingPerks.EnsureRegistered();
            Core.LogMsg("[特性UI] OpenUI时确保特性已注册");

            // 确保特性UI元素已创建
            CustomStartingPerks.EnsurePickerElements(__instance);
        }
        catch (Exception ex)
        {
            Core.LogMsg("[特性UI] 打开失败: " + ex.Message);
        }
    }

    // ============================================================
    // 5. 特性图标加载器补丁
    // ============================================================
    public static void PostfixIconLoaderStart(StartingPerkIconLoader __instance)
    {
        try
        {

            // 方法1：尝试perkIcons静态字典（按照ExtraPerks指南）
            bool injected = false;
            try
            {
                FieldInfo perkIconsField = typeof(StartingPerkIconLoader).GetField("perkIcons",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (perkIconsField != null)
                {
                    object perkIcons = perkIconsField.GetValue(null);
                    if (perkIcons != null)
                    {

                        PropertyInfo itemProp = perkIcons.GetType().GetProperty("Item");

                        foreach (CustomStartingPerk custom in CustomStartingPerks.All)
                        {
                            try
                            {
                                string cleanId = custom.Id.Replace("\0", "").Trim();
                                if (PerkIconLoader.HasCustomIcon(custom.Id))
                                {
                                    Sprite icon = PerkIconLoader.GetPerkIcon(custom.Id);
                                    if (icon != null && itemProp != null)
                                    {
                                        itemProp.SetValue(perkIcons, icon, new object[] { cleanId });
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                MelonLogger.Error("[特性图标] 注入图标失败 " + custom.Id + ": " + ex.Message);
                            }
                        }
                        injected = true;
                    }
                }
            }
            catch (Exception ex)
            {
                Core.LogMsg("[特性图标] 字典方式失败: " + ex.Message);
            }

            // 方法2：如果字典方式失败，用perkIconList列表
            if (!injected)
            {
                var perkIconListProp = __instance.GetType().GetProperty("perkIconList",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (perkIconListProp == null)
                {
                    return;
                }

                object perkIconList = perkIconListProp.GetValue(__instance);
                if (perkIconList == null)
                {
                    Type listType = typeof(Il2CppSystem.Collections.Generic.List<PerkIconEntry>);
                    perkIconList = Activator.CreateInstance(listType);
                    perkIconListProp.SetValue(__instance, perkIconList);
                }

                MethodInfo addMethod = perkIconList.GetType().GetMethod("Add");
                if (addMethod == null)
                {
                    return;
                }

                foreach (CustomStartingPerk custom in CustomStartingPerks.All)
                {
                    try
                    {
                        string cleanId = custom.Id.Replace("\0", "").Trim();
                        if (PerkIconLoader.HasCustomIcon(custom.Id))
                        {
                            Sprite icon = PerkIconLoader.GetPerkIcon(custom.Id);
                            if (icon != null)
                            {
                                PerkIconEntry entry = new PerkIconEntry();
                                entry.perkName = cleanId;
                                entry.Sprite = icon;
                                addMethod.Invoke(perkIconList, new object[] { entry });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Error("[特性图标] 注入图标失败 " + custom.Id + ": " + ex.Message);
                    }
                }

            }
        }
        catch (Exception ex)
        {
            MelonLogger.Error("[特性图标] StartingPerkIconLoaderStartPatch失败: " + ex.Message + "\n" + ex.StackTrace);
        }
    }

    // ============================================================
    // 6. 本地化补丁
    // ============================================================
    public static void PostfixGetLocalizedPerkTable(string key, ref string __result)
    {
        try
        {
            if (string.IsNullOrEmpty(key)) return;

            // 解析key格式：perk_{id}_name 或 perk_{id}_desc
            string perkId = null;
            bool isName = false;
            bool isDesc = false;

            if (key.StartsWith("perk_"))
            {
                string rest = key.Substring(5);
                if (rest.EndsWith("_name"))
                {
                    perkId = rest.Substring(0, rest.Length - 5);
                    isName = true;
                }
                else if (rest.EndsWith("_desc"))
                {
                    perkId = rest.Substring(0, rest.Length - 5);
                    isDesc = true;
                }
            }

            if (perkId == null) return;

            // 白名单加固（Bug4修复）：只用 CustomStartingPerks.Find 确认是自定义特性才覆盖，
            // 确保绝不误伤原版特性文本（原版特性走游戏本地化，保持中文）
            CustomStartingPerk matched = CustomStartingPerks.Find(perkId);
            if (matched == null) return;

            // 查找自定义特性
            if (CustomStartingPerks.TryGetLoc(perkId, out string displayName, out string description))
            {
                if (isName && !string.IsNullOrEmpty(displayName))
                {
                    __result = displayName;
                }
                else if (isDesc && !string.IsNullOrEmpty(description))
                {
                    __result = description;
                }
            }
        }
        catch (Exception ex)
        {
            Core.LogMsg("[本地化] 补丁失败: " + ex.Message);
        }
    }

    // ============================================================
    // 7. 特性元素启动补丁
    // ============================================================
    public static void PostfixStartingPerkElementStart(StartingPerkElement __instance)
    {
        try
        {
            CustomStartingPerks.EnsureElement(__instance);
        }
        catch (Exception ex)
        {
            Core.LogMsg("[特性UI] 元素启动失败: " + ex.Message);
        }
    }

    // ============================================================
        // ============================================================
    // 8. 物品价格补丁（刀尖舔血/好酒之徒）
    // 只在"玩家卖出"(SellMode)交易违禁品/酒类时加价；买入不加
    // 加价统一在 GetCurrentValue（交易UI显示价格第一时间生效），
    // GetNegociatedValue 检测内部是否已调用 GetCurrentValue 以防双重加价
    // ============================================================

    // 当前是否处于 GetNegociatedValue 计算链内
    private static bool _inNegociatedCalc = false;
    // GetNegociatedValue 计算链内调用 GetCurrentValue 的次数（>0 说明已加价）
    private static int _currentValueCallsInNegociated = 0;

    // GetNegociatedValue - Prefix
    public static void PrefixGameItemGetNegociatedValue(GameItem __instance)
    {
        _inNegociatedCalc = true;
        _currentValueCallsInNegociated = 0;
    }

    // GetNegociatedValue - Postfix（若内部未调用 GetCurrentValue，则此处补加价）
    public static void PostfixGameItemGetNegociatedValue(GameItem __instance, ref long __result)
    {
        _inNegociatedCalc = false;
        // 内部已通过 GetCurrentValue 加过价则不再加
        if (_currentValueCallsInNegociated == 0)
        {
            TryApplyTradeMarkup(__instance, ref __result);
        }
    }

    // GetCurrentValue - Prefix（记录是否在 GetNegociatedValue 链内）
    public static void PrefixGameItemGetCurrentValue(GameItem __instance)
    {
        if (_inNegociatedCalc) _currentValueCallsInNegociated++;
    }

    // GetCurrentValue - Postfix（交易UI显示价格第一时间加价）
    public static void PostfixGameItemGetCurrentValue(GameItem __instance, ref long __result)
    {
        TryApplyTradeMarkup(__instance, ref __result);
    }

    // 交易价加价公共逻辑：只在"玩家卖出"(SellMode)且为违禁品/酒类时加价
    private static void TryApplyTradeMarkup(GameItem item, ref long result)
    {
        try
        {
            // 信誉扫地：顾客不信任你，卖价-20%、买价+20%（所有势力好感一星后解除）
            if (BadReputationPerk.IsActive() && !BadReputationPerk.IsCleared())
            {
                if (Patches.CurrentUITradeMode == 2) { result = (long)(result * 0.80); TryAddBadReputationFeature(item, -20); }
                else if (Patches.CurrentUITradeMode == 1) { result = (long)(result * 1.20); TryAddBadReputationFeature(item, 20); }
            }

            // 鲁滨逊职业（startType=14）：买入食物×2（用户拍板 09-09：食物改双倍价格购买）/药×2（mode==1）；卖出状态修正（mode==2 饿/渴/病/囤粮）
            // 拆包实锤：买入/卖出定价统一走 GetNegociatedValue（BuyItem/SellItem 链），GetCurrentValue 不参与——倍率必须挂这里
            if (RobinCrusoePerk.IsActive())
            {
                if (Patches.CurrentUITradeMode == 1)
                {
                    TryAddNodeBuffFeature(item); // 购买面板：实时显示当前节点状态 buff（用户拍板）
                    if (RobinCrusoePerk.IsFood(item)) { result = (long)(result * 2.0); TryAddRobinsonBuyMarkup(item); return; }
                    if (RobinCrusoePerk.IsMedicine(item)) { result = (long)(result * 2.0); TryAddRobinsonBuyMarkup(item); return; }
                }
                else if (Patches.CurrentUITradeMode == 2)
                {
                    TryAddNodeBuffFeature(item); // 出售面板：实时显示当前节点状态 buff（用户拍板）
                    // 食物状态售价：腐烂-100% / 变质-90% / 食用过-80% / 新鲜+30%（正常不变）
                    if (RobinCrusoePerk.IsFood(item))
                    {
                        int fq = RobinCrusoePerk.GetFoodQuality(item);
                        if (fq == 3) result = 0;                                          // 腐烂 -100%
                        else if (fq == 2) result = (long)(result * 0.1);                  // 变质 -90%
                        else if (RobinCrusoePerk.IsEaten(item)) result = (long)(result * 0.2); // 食用过 -80%
                        else if (fq <= 0) result = (long)(result * 1.3);                  // 新鲜+30%（无标签-1=默认新鲜）
                        TryAddFoodQualityFeature(item, fq); // 报价面板"市场与商人"区同步显示（参考水质feature/刀尖舔血写法）
                    }
                    // v5.7 售价加成：粮仓充盈（饱食≥80连续7天 +5%）+ 昂扬累计（每2天+1% 封顶+5%）；心情不加售价
                    // 与状态客户加价相乘，不与心情叠加
                    double sellMul = 1.0 + RobinCrusoePerk.GetSellBonusPct() / 100.0;
                    if (sellMul > 1.0) result = (long)(result * sellMul);
                    // v5.9 豁出去了：违禁品卖出收益+50%（CompBuff，与品质/状态客户加价相乘）
                    double contraMul = RobinCrusoePerk.GetContraEffMult();
                    if (contraMul > 1.0 && RobinCrusoePerk.IsContrabandItem(item)) result = (long)(result * contraMul);
                    // 状态客户加价已按用户拍板（09-10）移除：所有价格变化必须在面板体现，无解释加价一律不要
                    // double statusMul = RobinCrusoePerk.GetStatusClientMarkup(GetCurrentTradingClient(), item);
                    // if (statusMul != 1.0) result = (long)(result * statusMul);
                }
            }

            if (Patches.CurrentUITradeMode != 2) return; // 只玩家卖时加价（买不加；买入折扣由 ItemFeature 原生标签驱动）


            // 违禁品判定：用原生 ContrabandHelper.IsContraband（读 CONTRABAND/CONTRABAND_ITEM_TAG 大写标签）
            bool isContraband = false;
            try { isContraband = ContrabandHelper.IsContraband(item); } catch { }
            if (!isContraband)
            {
                try { isContraband = item.IsTag("CONTRABAND"); } catch { }
            }
            if (!isContraband)
            {
                try { isContraband = item.IsTag("CONTRABAND_ITEM_TAG"); } catch { }
            }

            bool isAlcohol = TraitEffects.IsAlcohol(item);

            float multiplier = 1.0f;
            bool riskActive = false;
            try { riskActive = RiskTakerPerk.IsActive(); } catch { }
            if (riskActive && isContraband) multiplier *= 1.20f;
            if (WineLoverPerk.IsActive() && isAlcohol) multiplier *= 1.25f;

            if (multiplier > 1.0f)
            {
                result = (long)(result * multiplier);
                TryAddTradeFeature(item, isContraband, isAlcohol);
            }
        }
        catch
        { }
    }

    // ===== 鲁滨逊 v4.2 客户侧联动（预算/出价/当前交易客户）=====
    // 当前交易客户：PlayerStore.currentClientInstance(0xD0).storeClient(0x10)（dump.cs 实锤）
    private static StoreClient GetCurrentTradingClient()
    {
        try
        {
            var ps = Il2Cpp.PlayerStore.Instance;
            if (ps == null) return null;
            var inst = ps.currentClientInstance;
            if (inst == null) return null;
            return inst.storeClient;
        }
        catch { return null; }
    }

    // 客户预算联动（显式预算客户 useClientBudget=true）：ApplyBudgetModifier 原生修正后叠加精神预算加成
    // 常态+5% / 昂扬 每2天+5% 封顶+25%
    // clientBudget 是 private（dump.cs 实锤）→ 用公开 SetBudget/GetBudget（SetBudget 会调 ApplyBudgetModifier → 标志防递归）
    private static bool _inBudgetOverride = false;
    public static void PostfixStoreClientApplyBudgetModifier(StoreClient __instance)
    {
        try
        {
            if (__instance == null || !RobinCrusoePerk.IsActive() || _inBudgetOverride) return;
            int pct = RobinCrusoePerk.GetBudgetBonusPct();
            if (pct <= 0) return;
            _inBudgetOverride = true;
            try
            {
                int budget = __instance.GetBudget();
                __instance.SetBudget((int)(budget * (1.0 + pct / 100.0)));
            }
            finally { _inBudgetOverride = false; }
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] 预算联动异常: " + ex.Message); }
    }

    // 客户预算联动（clientCash 客户 useClientBudget=false，多数）：PickClient 返回后按同比例提高 clientCash
    // 用户拍板方案 B：预算联动对全部客户生效；clientCash/clientBudget 均 public（dump.cs 实锤）
    public static void PostfixStoreClientManagerPickClient(StoreClient __result)
    {
        try
        {
            if (__result == null || !RobinCrusoePerk.IsActive()) return;
            int pct = RobinCrusoePerk.GetBudgetBonusPct();
            if (pct <= 0) return;
            if (__result.useClientBudget) return; // 显式预算客户已在 ApplyBudgetModifier 改
            int cash = __result.clientCash;
            __result.clientCash = (int)(cash * (1.0 + pct / 100.0));
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] clientCash联动异常: " + ex.Message); }
    }

    // 普通客户出价上移（v5.7：心情≥80 时 +10~20%，取中值+15）：Patch OfferBuyingMarkup 入参 percent
    // 拆包实锤：BargainUIManager.OfferBuyingMarkup(percent) → ItemFeatureList.BargainBuyingMarkup(percent) → ItemFeature.valueModifier@0x6C
    public static void PrefixBargainUIManagerOfferBuyingMarkup(ref int percent)
    {
        try
        {
            if (!RobinCrusoePerk.IsActive()) return;
            if (RobinCrusoePerk.GetMood() >= 80) // v5.7：心情≥80 出价区间上移；40-59/<40 不压低（原生下限保护）
            {
                percent += 15;
            }
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] 出价上移异常: " + ex.Message); }
    }

    // v5.8-8 谈判方向1：议价成功率（心情≥80 +15 与 社交≥80 +10 取更高 + 节点 bargain±N，封装在 GetBargainBonusPct）
    // dump.cs 实锤：private static int GetDealMakerBonus()；GetBargainSuccessChance(BargainType,int) 只供 tooltip 显示（Core.cs 22156 注释）
    public static void PostfixGetDealMakerBonus(ref int __result)
    {
        try
        {
            if (!RobinCrusoePerk.IsActive()) return;
            __result += RobinCrusoePerk.GetBargainBonusPct(); // max(心情15,社交10) + 节点议价（门可罗雀-15/眼皮千斤-20/破罐-15等）
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] 谈判成功率异常: " + ex.Message); }
    }

    // v5.7 谈判方向2：客户更容易接受加价（心情≥80 → BargainBuyingMarkup feature valueModifier ×1.15）
    // 拆包回填1：<OfferBuyingMarkup>b__0 直接返回 ItemFeatureList.BargainBuyingMarkup(int percent)（valueModifier 决定接受度）
    public static void PostfixItemFeatureListBargainBuyingMarkup(ref Il2Cpp.ItemFeature __result)
    {
        try
        {
            if (!RobinCrusoePerk.IsActive() || __result == null) return;
            if (RobinCrusoePerk.GetMood() >= 80 && __result.valueModifier > 0)
                __result.valueModifier = (int)(__result.valueModifier * 1.15f);
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] 加价接受率异常: " + ex.Message); }
    }

    // v5.7 议价成交 +5 心情（每客户只算第一次成交，用户拍板）+ v5.8-8 接待计数（社交结算用，每单都计）
    private static readonly System.Collections.Generic.HashSet<long> _moodBoostedClients = new System.Collections.Generic.HashSet<long>();
    public static void PostfixStoreClientOnDealAccepted(Il2Cpp.StoreClient __instance)
    {
        try
        {
            if (RobinCrusoePerk.IsActive())
            {
                if (__instance != null && _moodBoostedClients.Add((long)__instance.Pointer)) // 每客户首单 +5，后续单不加
                    RobinCrusoePerk.BoostMood(5, LangHelper.T("成交一单", "Deal closed"));
                RobinCrusoePerk.RecordDeal(); // 接待数 +1（社交结算口径：每单都算）
                // v5.9 当日营业额：卖出成交（BUY=玩家卖货给客户）累计成交额；买入不累计
                try
                {
                    var ps = Il2Cpp.PlayerStore.Instance;
                    Il2Cpp.StoreClientInstance inst = null;
                    if (ps != null) inst = ps.currentClientInstance;
                    if (inst != null && __instance != null)
                    {
                        int intent = 0;
                        try
                        {
                            var f = inst.GetType().GetField("clientIntent", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                            if (f == null) f = inst.GetType().GetField("intent", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                            if (f != null) intent = (int)f.GetValue(inst);
                        }
                        catch { }
                        if (intent == 1) // BUY = 玩家卖出
                        {
                            // 成交额从当前交易物品价值取（反射失败则 0：空营业额不造假）
                            var itemField = inst.GetType().GetField("item", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                            if (itemField == null) itemField = inst.GetType().GetField("currentItem", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                            if (itemField != null && itemField.GetValue(inst) is GameItem sold)
                            {
                                try { RobinCrusoePerk.RecordRevenue((int)sold.GetCurrentValue()); } catch { }
                            }
                        }
                    }
                }
                catch { }
            }
        }
        catch { }
    }

    // GetValue - Postfix（玩家从NPC买时，UI可能用带vendor的GetValue算价，这里也打折扣）
    public static void PostfixGameItemGetValue(GameItem __instance, ref long __result)
    {
        try
        {
            // 折扣改由 ItemFeature 原生标签驱动（AccumulateFeatureStages 自动含 -5%），这里不再手动打折
            // 信誉扫地已统一在 TryApplyTradeMarkup（GetNegociatedValue/GetCurrentValue 链）修正 ±20%，此处不再叠加
        }
        catch (Exception ex)
        {
            Core.LogMsg("[之友折扣-GetValue] 失败: " + ex.Message);
        }
    }

    // ========== 之友折扣（ItemFeature 原生标签驱动） ==========
    private const string FRIEND_DISCOUNT_ID = "friend_discount";
    private static readonly Dictionary<long, GameItem> _discountedItems = new Dictionary<long, GameItem>();

    // 玩家进入买模式：给当前物品加"友情价 -5%"标签（同时价格自动-5%）
    // 直接给物品加"友情价 -5%"标签（不判断客户，由调用方决定何时适用；博士柜台货等场景用）
    public static void ApplyFriendDiscountToItem(GameItem item)
    {
        try
        {
            if (item == null || item.itemFeatures == null) return;

            bool exists = false;
            for (int j = 0; j < item.itemFeatures.Count; j++)
            {
                if (item.itemFeatures[j] != null && item.itemFeatures[j].identifier == FRIEND_DISCOUNT_ID)
                {
                    exists = true;
                    break;
                }
            }
            if (exists) return;

            // 复刻刀尖舔血的可靠写法：直接 itemFeatures.Add + preExposeValueModifier + usePreExposeValue=true，不暴露/不发现
            ItemFeature val = new ItemFeature();
            val.identifier = FRIEND_DISCOUNT_ID;
            val.featureType = ItemFeature.FeatureType.TemporaryBuying;  // 4
            val.valueStage = ItemFeature.ValueStage.Market;             // 1
            val.valueModifier = 0;
            val.preExposeValueModifier = -5;
            val.usePreExposeValue = true;
            val.isFeatureExposed = false;
            val.isFeatureDiscovered = false;
            val.publicDisplay = LangHelper.T("友情价", "Friend Price");
            val.actualDisplay = LangHelper.T("友情价", "Friend Price");
            item.itemFeatures.Add(val);   // 直接 Add，不走 AddItemFeature（避免字段被覆盖）

            long uid = 0;
            try { uid = item.uniqueId; } catch { }
            if (uid != 0 && !_discountedItems.ContainsKey(uid))
                _discountedItems[uid] = item;
        }
        catch (Exception ex)
        {
            Core.LogMsg("[友情价] 添加失败: " + ex.Message);
        }
    }

    private static void TryApplyFriendDiscountFeature(GameItem item)
    {
        try
        {
            if (item == null) return;
            if (GetFriendBuyDiscount(out _, out _) >= 1.0f) return; // 非之友客户不打折
            if (item.itemFeatures == null) return;

            // 防重复（遍历查找已存在的同 identifier 特性）
            bool exists = false;
            for (int j = 0; j < item.itemFeatures.Count; j++)
            {
                if (item.itemFeatures[j] != null && item.itemFeatures[j].identifier == FRIEND_DISCOUNT_ID)
                {
                    exists = true;
                    break;
                }
            }
            if (exists) return;

            // 复刻刀尖舔血的可靠写法：直接 itemFeatures.Add + preExposeValueModifier + usePreExposeValue=true，不暴露/不发现
            ItemFeature val = new ItemFeature();
            val.identifier = FRIEND_DISCOUNT_ID;
            val.featureType = ItemFeature.FeatureType.TemporaryBuying;  // 4
            val.valueStage = ItemFeature.ValueStage.Market;             // 1
            val.valueModifier = 0;
            val.preExposeValueModifier = -5;
            val.usePreExposeValue = true;
            val.isFeatureExposed = false;
            val.isFeatureDiscovered = false;
            val.publicDisplay = LangHelper.T("友情价", "Friend Price");
            val.actualDisplay = LangHelper.T("友情价", "Friend Price");
            item.itemFeatures.Add(val);   // 直接 Add，不走 AddItemFeature（避免字段被覆盖）

            long uid = 0;
            try { uid = item.uniqueId; } catch { }
            if (uid != 0 && !_discountedItems.ContainsKey(uid))
                _discountedItems[uid] = item;
        }
        catch (Exception ex)
        {
            Core.LogMsg("[友情价] 添加失败: " + ex.Message);
        }
    }

    // 交易关闭：移除所有已加的折扣标签
    private static void RemoveFriendDiscountFeatures()
    {
        try
        {
            foreach (var kv in _discountedItems)
            {
                if (kv.Value != null && kv.Value.IsAlreadyContainFeatureWithId(FRIEND_DISCOUNT_ID))
                {
                    kv.Value.RemoveItemFeatureByID(FRIEND_DISCOUNT_ID);
                }
            }
            _discountedItems.Clear();
        }
        catch (Exception ex)
        {
            Core.LogMsg("[之友折扣] 移除标签失败: " + ex.Message);
        }
    }

    // ============ 鲁滨逊：食物品质售价修正 → 报价面板显示 feature ============
    // 参考原生"水质变化影响价格"（WaterFeatureHelper.InitWaterFeature）与刀尖舔血 feature 写法：
    // 直接 itemFeatures.Add + publicDisplay/actualDisplay 承载显示文本；
    // modifier=0 不改价（价格由 TryApplyTradeMarkup 管），只让报价面板"市场与商人"区显示修正说明
    private static void TryAddFoodQualityFeature(GameItem item, int fq)
    {
        try
        {
            if (item == null || item.itemFeatures == null) return;
            if (!RobinCrusoePerk.IsActive()) return;
            bool eaten = RobinCrusoePerk.IsEaten(item);
            string disp = "";
            if (fq >= 3) disp = LangHelper.T("售价：-100%（腐烂）", "Price: -100% (Rotten)");
            else if (fq == 2) disp = LangHelper.T("售价：-90%（变质）", "Price: -90% (Spoiled)");
            else if (eaten) disp = LangHelper.T("售价：-80%（已食用）", "Price: -80% (Partially Eaten)");
            else if (fq <= 0) disp = LangHelper.T("售价：+30%（新鲜）", "Price: +30% (Fresh)");
            if (disp.Length == 0) return;

            // 防重复：已存在同 identifier 则更新显示文本
            for (int j = 0; j < item.itemFeatures.Count; j++)
            {
                if (item.itemFeatures[j] != null && item.itemFeatures[j].identifier == "wages_food_quality")
                {
                    item.itemFeatures[j].publicDisplay = disp;
                    item.itemFeatures[j].actualDisplay = disp;
                    return;
                }
            }

            ItemFeature val = new ItemFeature();
            val.identifier = "wages_food_quality";
            val.featureType = ItemFeature.FeatureType.TemporarySelling; // 5：卖出时显示
            val.valueStage = ItemFeature.ValueStage.Market;             // 1
            val.valueModifier = 0;
            val.preExposeValueModifier = 0;   // 不改价（价格由 TryApplyTradeMarkup 管）
            val.usePreExposeValue = false;
            val.isFeatureExposed = false;
            val.isFeatureDiscovered = false;
            val.publicDisplay = disp;
            val.actualDisplay = disp;
            item.itemFeatures.Add(val);
        }
        catch (Exception ex)
        {
            Core.LogMsg("[食物品质] feature失败: " + ex.Message);
        }
    }

    // ============ 鲁滨逊：节点状态 buff → 报价面板实时标签（用户拍板：出售/购买面板都显示）============
    // BUG-001 09-11：物品指针防重集合——同一缓存周期内每件物品只处理一次（GetTradeBuffDisplay 已缓存，O(N) 遍历只在首见跑）
    private static readonly HashSet<long> _nodeBuffItems = new HashSet<long>();
    internal static void ClearNodeBuffItems() { _nodeBuffItems.Clear(); }
    private static void TryAddNodeBuffFeature(GameItem item)
    {
        try
        {
            if (item == null || item.itemFeatures == null) return;
            if (!RobinCrusoePerk.IsActive()) return;
            string disp = RobinCrusoePerk.GetTradeBuffDisplay(); // BUG-001：缓存命中，便宜
            if (disp.Length == 0) return;
            long ptr = 0;
            try { ptr = item.Pointer.ToInt64(); } catch { }
            if (ptr != 0 && _nodeBuffItems.Contains(ptr)) return; // 本缓存周期已处理（防重复遍历）
            // 防重复：同 identifier 更新显示文本（实时跟随状态变化）
            for (int j = 0; j < item.itemFeatures.Count; j++)
            {
                if (item.itemFeatures[j] != null && item.itemFeatures[j].identifier == "wages_node_buff")
                {
                    item.itemFeatures[j].publicDisplay = disp;
                    item.itemFeatures[j].actualDisplay = disp;
                    if (ptr != 0) _nodeBuffItems.Add(ptr);
                    return;
                }
            }
            ItemFeature val = new ItemFeature();
            val.identifier = "wages_node_buff";
            val.featureType = ItemFeature.FeatureType.TemporarySelling; // 5：交易时显示
            val.valueStage = ItemFeature.ValueStage.Market;             // 1
            val.valueModifier = 0;      // 不改价（价格由 GetSellBonusPct/预算/议价管）
            val.preExposeValueModifier = 0;
            val.usePreExposeValue = false;
            val.isFeatureExposed = false;
            val.isFeatureDiscovered = false;
            val.publicDisplay = disp;
            val.actualDisplay = disp;
            item.itemFeatures.Add(val);
            if (ptr != 0) _nodeBuffItems.Add(ptr);
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] 节点buff feature失败: " + ex.Message); }
    }

    // ============ 刀尖舔血 / 好酒之徒 交易加价标签 ============
    private static readonly Dictionary<long, GameItem> _tradeFeatureItems = new Dictionary<long, GameItem>();

    // 在玩家卖出违禁品/酒类加价时，给物品添加一个"刀尖舔血/好酒之徒"标签
    // 完全复刻"没坏那会儿"的实现：直接 itemFeatures.Add + preExposeValueModifier + usePreExposeValue=true，不暴露/不发现
    private static void TryAddTradeFeature(GameItem item, bool isContraband, bool isAlcohol)
    {
        try
        {
            if (item == null) return;

            string featId = "";
            string featDisplay = "";
            if (RiskTakerPerk.IsActive() && isContraband)
            {
                featId = "risk_taker_markup";
                featDisplay = LangHelper.T("刀尖舔血加价", "Blade's Edge Markup");
            }
            else if (WineLoverPerk.IsActive() && isAlcohol)
            {
                featId = "wine_lover_markup";
                featDisplay = LangHelper.T("好酒之徒加价", "Wine Lover Markup");
            }
            if (featId == "" || item.itemFeatures == null) return;

            // 防重复（遍历查找已存在的同 identifier 特性）
            bool exists = false;
            for (int j = 0; j < item.itemFeatures.Count; j++)
            {
                if (item.itemFeatures[j] != null && item.itemFeatures[j].identifier == featId)
                {
                    exists = true;
                    break;
                }
            }
            if (exists) return;

            // feature 仅作面板说明：只显示不改价（价格由 TryApplyTradeMarkup multiplier 实际生效，防原生双重加价）
            ItemFeature val = new ItemFeature();
            val.identifier = featId;
            val.featureType = ItemFeature.FeatureType.TemporarySelling;   // 5
            val.valueStage = ItemFeature.ValueStage.Market;                // 1
            val.valueModifier = 0;
            val.preExposeValueModifier = 0;
            val.usePreExposeValue = false;
            val.isFeatureExposed = false;
            val.isFeatureDiscovered = false;
            val.publicDisplay = featDisplay;
            val.actualDisplay = featDisplay;
            item.itemFeatures.Add(val);   // 直接 Add，不走 AddItemFeature（避免字段被覆盖）

            long uid = 0;
            try { uid = item.uniqueId; } catch { }
            if (uid != 0 && !_tradeFeatureItems.ContainsKey(uid))
                _tradeFeatureItems[uid] = item;
        }
        catch (Exception ex)
        {
            Core.LogMsg("[标签-添加] 失败: " + ex.Message);
        }
    }

    // ============ 鲁滨逊 买食物/药双倍价标签（用户拍板 09-10：所有价格变化必须在面板体现） ============
    private static void TryAddRobinsonBuyMarkup(GameItem item)
    {
        try
        {
            if (item == null || item.itemFeatures == null) return;
            string disp = LangHelper.T("鲁滨逊·口粮双倍价", "Robinson·Ration x2");
            for (int j = 0; j < item.itemFeatures.Count; j++)
            {
                if (item.itemFeatures[j] != null && item.itemFeatures[j].identifier == "wages_robin_buy")
                {
                    item.itemFeatures[j].publicDisplay = disp;
                    item.itemFeatures[j].actualDisplay = disp;
                    return;
                }
            }
            ItemFeature val = new ItemFeature();
            val.identifier = "wages_robin_buy";
            val.featureType = ItemFeature.FeatureType.TemporaryBuying;
            val.valueStage = ItemFeature.ValueStage.Market;
            val.valueModifier = 0;
            val.preExposeValueModifier = 0; // ★ 只显示不改价（价格由 TryApplyTradeMarkup ×2.0 实际生效，防原生双重加价 ×4）
            val.usePreExposeValue = false;
            val.isFeatureExposed = false;
            val.isFeatureDiscovered = false;
            val.publicDisplay = disp;
            val.actualDisplay = disp;
            item.itemFeatures.Add(val);
        }
        catch { }
    }

    // ============ 信誉扫地 交易标签（顾客趁火打劫） ============
    // 复用刀尖舔血可靠写法：直接 itemFeatures.Add + preExposeValueModifier + usePreExposeValue=true
    private static void TryAddBadReputationFeature(GameItem item, int modifier)
    {
        try
        {
            if (item == null || item.itemFeatures == null) return;

            // 已存在则更新 modifier，避免重复堆叠
            for (int j = 0; j < item.itemFeatures.Count; j++)
            {
                if (item.itemFeatures[j] != null && item.itemFeatures[j].identifier == "bad_reputation")
                {
                    item.itemFeatures[j].preExposeValueModifier = 0; // ★ 只显示不改价（价格由 TryApplyTradeMarkup 管，防原生双重扣费 ±40%）
                    item.itemFeatures[j].usePreExposeValue = false;
                    return;
                }
            }

            ItemFeature val = new ItemFeature();
            val.identifier = "bad_reputation";
            val.featureType = modifier < 0 ? ItemFeature.FeatureType.TemporarySelling : ItemFeature.FeatureType.TemporaryBuying;
            val.valueStage = ItemFeature.ValueStage.Market;
            val.valueModifier = 0;
            val.preExposeValueModifier = 0; // ★ 只显示不改价（价格由 TryApplyTradeMarkup 管，防原生双重扣费 ±40%）
            val.usePreExposeValue = false;
            val.isFeatureExposed = false;
            val.isFeatureDiscovered = false;
            val.publicDisplay = LangHelper.T("信誉扫地", "Bad Reputation");
            val.actualDisplay = LangHelper.T("信誉扫地", "Bad Reputation");
            item.itemFeatures.Add(val);   // 直接 Add，不走 AddItemFeature

            long uid = 0;
            try { uid = item.uniqueId; } catch { }
            if (uid != 0 && !_tradeFeatureItems.ContainsKey(uid))
                _tradeFeatureItems[uid] = item;
        }
        catch (Exception ex)
        {
            Core.LogMsg("[信誉扫地] 加标签失败: " + ex.Message);
        }
    }

    private static void RemoveTradeFeatures()
    {
        try
        {
            foreach (var kv in _tradeFeatureItems)
            {
                if (kv.Value == null) continue;
                try { if (kv.Value.IsAlreadyContainFeatureWithId("刀尖舔血")) kv.Value.RemoveItemFeatureByID("刀尖舔血"); } catch { }
                try { if (kv.Value.IsAlreadyContainFeatureWithId("好酒之徒")) kv.Value.RemoveItemFeatureByID("好酒之徒"); } catch { }
                try { if (kv.Value.IsAlreadyContainFeatureWithId("信誉扫地")) kv.Value.RemoveItemFeatureByID("信誉扫地"); } catch { }
                try { if (kv.Value.IsAlreadyContainFeatureWithId("bad_reputation")) kv.Value.RemoveItemFeatureByID("bad_reputation"); } catch { }
            }
            _tradeFeatureItems.Clear();
        }
        catch (Exception ex)
        {
            Core.LogMsg("[加价标签] 移除失败: " + ex.Message);
        }
    }

    // 之友折扣倍率：玩家从对应"之友"NPC 买东西时 -5%（0.95），否则 1.0
    private static float GetFriendBuyDiscount(out string clientName, out string clientId)
    {
        clientName = "";
        clientId = "";
        try
        {
            StoreClient client = SpecialNpcManager.GetCurrentClient();
            if (client == null) return 1.0f;
            clientId = client.identifier ?? "";
            clientName = client.displayName ?? "";
            if (clientId == "retired_gunsmith" && RetiredGunsmithPerk.IsActive()) return 0.95f;
            if (clientId == "retired_water_merchant" && WaterMerchantPerk.IsActive()) return 0.95f;
            if (clientId == "retired_winemaker" && AlcoholMerchantPerk.IsActive()) return 0.95f;
            // 博士：按名字/ID判断（每14天来访的客户）
            if (DrJacksonFriendPerk.IsActive() &&
                (clientName.Contains("博士") || clientId.Contains("jackson") || clientId.Contains("inventor")))
                return 0.95f;
        }
        catch (Exception ex)
        {
            Core.LogMsg("[之友折扣] 判断失败: " + ex.Message);
        }
        return 1.0f;
    }



    // ============================================================
    // 10. 笑面虎：议价成功率+25%（GetDealMakerBonus Postfix，真实 roll 阈值提升）
    // 拆包依据：GetBargainSuccessChance 只被 GetBargainTooltip 调用（纯 tooltip 显示），
    // 真实 roll 在 OfferMarkup 内联：RNG.GetRandomInt(0,99) >= RoundToInt(100-2×percent+声望+标志+GetDealMakerBonus())
    // GetDealMakerBonus 参与 OfferMarkup/Blackmail/DemandBuyingDiscount 的真实判定，+25 = 成功率+25个百分点
    // ============================================================
    public static void PostfixDealMakerBonus(ref int __result)
    {
        try
        {
            if (SmilingTigerPerk.IsActive())
            {
                __result += 25;
            }
        }
        catch (Exception ex) { Core.LogMsg("[笑面虎] 成功率补丁失败: " + ex.Message); }
    }

    // ============================================================
    // 11. 笑面虎：客户好感获取-25%（议价成交后声望倍率 ×0.75）
    // ============================================================
    public static void PostfixTradeRepMultiplier(ref double __result)
    {
        try
        {
            if (SmilingTigerPerk.IsActive())
            {
                __result *= 0.75;
            }
        }
        catch (Exception ex) { Core.LogMsg("[笑面虎] 好感获取补丁失败: " + ex.Message); }
    }

    // ============================================================
    // 11.5 笑面虎：议价成功后不减客户预算
    // 拆包依据：
    //   - OfferMarkup 成功路径调 ModBudget(-成交价) 按预算比例扣一次
    //   - 成交结算 PlayerStore.SellItem 再调 ModBudget(-成交价) 扣一次
    //   - RecomputeTradeRepMultiplier 全游戏仅 6 个调用者（OfferMarkup/OfferDiscount/Blackmail/
    //     OfferBuyingMarkup/DemandBuyingDiscount/ThreatenAug），全在议价动作成功路径 → 精确的"议价成功"标志
    // 实现：拦截 StoreClient.ModBudget 的负值扣减（笑面虎激活 && 议价窗口/议价成功状态）
    // ============================================================
    private static bool _inBargainOffer = false;   // OfferMarkup/OfferDiscount 执行窗口
    private static bool _bargainSucceeded = false; // 议价动作成功后到成交/关闭前

    public static void PrefixOfferMarkup() { _inBargainOffer = true; }
    public static void PostfixOfferMarkup() { _inBargainOffer = false; }
    public static void PrefixOfferDiscount() { _inBargainOffer = true; }
    public static void PostfixOfferDiscount() { _inBargainOffer = false; }

    public static void PrefixRecomputeTradeRepMultiplier() { _bargainSucceeded = true; }

    public static bool PrefixStoreClientModBudget(StoreClient __instance, int budgetMod)
    {
        try
        {
            if (!SmilingTigerPerk.IsActive()) return true;
            if (budgetMod >= 0) return true; // 只拦扣减；加预算/买货收钱放行
            if (_inBargainOffer || _bargainSucceeded) return false; // 议价成功不扣客户预算
        }
        catch (Exception ex) { Core.LogMsg("[笑面虎] 预算拦截失败: " + ex.Message); }
        return true;
    }

    public static void PostfixPlayerStoreSellItem() { _bargainSucceeded = false; }

    public static void PostfixBargainCloseUI()
    {
        _inBargainOffer = false;
        _bargainSucceeded = false;
    }

    // ============================================================
    // 物品显示名补丁：GetDisplayName 返回空/问号时覆盖为中文名
    // 根因：这些物品本地化缺失，UI 显示走 GetDisplayName() 不读 name/customName
    // ============================================================
    public static void PostfixGameItemGetDisplayName(GameItem __instance, ref string __result)
    {
        try
        {
            if (__instance == null) return;
            string id = __instance.identifier;
            if (id == "wine_bottle")
            {
                // 酒瓶装荧光莓果酿/暗影莓果酿时显示内容物名（酿造产物 identifier=wine_bottle，name=内容物名）
                string nn = __instance.name;
                if (nn != null)
                {
                    if (nn.Contains("荧光莓果酿")) { __result = "荧光莓果酿"; return; }
                    if (nn.Contains("暗影莓果酿")) { __result = "暗影莓果酿"; return; }
                }
                __result = LangHelper.T("酒瓶", "Wine Bottle"); return;
            }
            string n = GetItemDisplayNameOverride(id);
            if (n != null) __result = n;
        }
        catch (Exception ex) { Core.LogMsg("[GetDisplayName] 异常: " + ex.Message); }
    }

    private static string GetItemDisplayNameOverride(string id)
    {
        switch (id)
        {
            case "wine_bloomberry": return LangHelper.T("荧光莓果酿", "Glowberry Wine");
            case "wine_gloomberry": return LangHelper.T("暗影莓果酿", "Gloomberry Wine");
            case "wine_berry": return LangHelper.T("莓果酿", "Berry Wine");
            case "wine_bottle": return LangHelper.T("酒瓶", "Wine Bottle");
            case "beer_case": return LangHelper.T("一箱啤酒", "Case of Beer");
            case "red_beer": return LangHelper.T("红魔鬼啤酒", "Red Devil Beer");
            case "empty_beer_bottle": return LangHelper.T("空啤酒瓶", "Empty Beer Bottle");
            case "wine_superyeast": return LangHelper.T("超级酵母", "Super Yeast");
            case "wine_yeast": return LangHelper.T("酿酒酵母", "Brewing Yeast");
            case "wine_yeast_infinite": return LangHelper.T("永续酵母", "Perpetual Yeast");
            case "wine_yeast_red": return LangHelper.T("红酵母", "Red Yeast");
            case "permit_gun_1": return LangHelper.T("枪支许可证（一级）", "Gun Permit (Tier 1)");
            case "permit_gun_2": return LangHelper.T("枪支许可证（二级）", "Gun Permit (Tier 2)");
            case "permit_gun_3": return LangHelper.T("枪支许可证（三级）", "Gun Permit (Tier 3)");
            case "blank_keycard": return LangHelper.T("空白钥匙卡", "Blank Keycard");
            default: return null;
        }
    }

    // ============================================================
    // ============================================================
    // 按ID取显示名补丁：UI 格子/槽位/工具提示走 GeneralHelper.GetDisplayNameFromID
    // 对本地化缺失的MOD物品直接返回中文名
    // ============================================================
    public static void PostfixGetDisplayNameFromID(string itemID, ref string __result)
    {
        try
        {
            if (string.IsNullOrEmpty(itemID)) return;
            string n = GetItemDisplayNameOverride(itemID);
            if (n != null)
            {
                if (itemID == "beer_case" || itemID == "wine_berry" || itemID == "wine_gloomberry")
                __result = n;
            }
        }
        catch { }
    }

    // 本地化物品名补丁：GetLocalizedItem 返回空/问号时覆盖为中文名
    // 根因：物品本地化缺失，UI 格子/交易界面走本地化表显示问号
    // ============================================================
    public static void PostfixGetLocalizedItem(string key, ref string __result)
    {
        try
        {
            if (string.IsNullOrEmpty(key)) return;
            if (key.Contains("beer_case") || key.Contains("wine_") || key.Contains("permit_gun") || key.Contains("blank_keycard"))
            { }
            if (string.IsNullOrEmpty(__result) || __result.Trim() == "?")
            {
                string n = GetLocalizedItemOverride(key);
                if (n != null) __result = n;
            }
        }
        catch { }
    }

    private static string GetLocalizedItemOverride(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        string direct = GetItemDisplayNameOverride(key);
        if (direct != null) return direct;
        string[] knownIds = {
            "wine_yeast_infinite","wine_yeast_red","wine_bloomberry","wine_gloomberry","empty_beer_bottle",
            "permit_gun_1","permit_gun_2","permit_gun_3","blank_keycard","wine_yeast","wine_berry",
            "wine_bottle","beer_case","red_beer"
        };
        foreach (string k in knownIds)
        {
            if (key.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0)
                return GetItemDisplayNameOverride(k);
        }
        return null;
    }

    // ============================================================
    // 通用本地化覆盖：GetLocalizedName / GetLocalizedUI 也可能被UI调用
    // ============================================================
    public static void PostfixGetLocalizedName(string key, ref string __result)
    {
        TryCoverLocalizationKey(key, ref __result);
    }

    public static void PostfixGetLocalizedUI(string key, ref string __result)
    {
        TryCoverLocalizationKey(key, ref __result);
    }

    private static void TryCoverLocalizationKey(string key, ref string __result)
    {
        try
        {
            if (string.IsNullOrEmpty(key)) return;
            if (key.Contains("beer_case") || key.Contains("wine_") || key.Contains("permit_gun") || key.Contains("blank_keycard"))
            if (string.IsNullOrEmpty(__result) || __result.Trim() == "?")
            {
                string n = GetLocalizedItemOverride(key);
                if (n != null) __result = n;
            }
        }
        catch { }
    }

    // ============================================================
    // 交易界面物品名强制修正：NegociationUIManager.itemName.text
    // ============================================================

    // 反射缓存：FixTradeItemName 用，避免每次交易UI打开都反射
    private static System.Reflection.PropertyInfo _negocInstanceProp;
    private static System.Reflection.FieldInfo _negocInstanceField;
    private static readonly System.Collections.Generic.Dictionary<System.Type, System.Reflection.FieldInfo> _itemNameFieldCache = new System.Collections.Generic.Dictionary<System.Type, System.Reflection.FieldInfo>();
    private static readonly System.Collections.Generic.Dictionary<System.Type, System.Reflection.PropertyInfo> _textPropCache = new System.Collections.Generic.Dictionary<System.Type, System.Reflection.PropertyInfo>();
    private static bool _negocCacheInit = false;

    private static void InitNegocCache()
    {
        if (_negocCacheInit) return;
        var mgrType = typeof(Il2Cpp.NegociationUIManager);
        _negocInstanceProp = mgrType.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        _negocInstanceField = mgrType.GetField("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        _negocCacheInit = true;
    }

    private static void FixTradeItemName(GameItem item)
    {
        try
        {
            if (item == null) return;
            string overrideName = GetItemDisplayNameOverride(item.identifier);
            if (overrideName == null) return;

            InitNegocCache();
            object mgr = null;
            if (_negocInstanceProp != null) mgr = _negocInstanceProp.GetValue(null);
            if (mgr == null && _negocInstanceField != null) mgr = _negocInstanceField.GetValue(null);
            if (mgr == null) return;

            // 缓存 itemName 字段（按运行时类型）
            System.Type mgrType = mgr.GetType();
            System.Reflection.FieldInfo itemNameField;
            if (!_itemNameFieldCache.TryGetValue(mgrType, out itemNameField))
            {
                itemNameField = mgrType.GetField("itemName", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                _itemNameFieldCache[mgrType] = itemNameField;
            }
            if (itemNameField == null) return;

            object tmp = itemNameField.GetValue(mgr);
            if (tmp == null) return;

            // 缓存 text 属性（按运行时类型）
            System.Type tmpType = tmp.GetType();
            System.Reflection.PropertyInfo textProp;
            if (!_textPropCache.TryGetValue(tmpType, out textProp))
            {
                textProp = tmpType.GetProperty("text");
                _textPropCache[tmpType] = textProp;
            }
            if (textProp != null)
            {
                textProp.SetValue(tmp, overrideName);
            }
        }
        catch (Exception ex) { Core.LogMsg("[名称修复] 失败: " + ex.Message); }
    }

    // ============================================================
    // Unity Localization 兜底：LocalizedStringDatabase.GenerateLocalizedString
    // ============================================================
    public static void PostfixGenerateLocalizedString(object tableEntryReference, ref string __result)
    {
        try
        {
            if (tableEntryReference == null) return;
            string key = null;
            try { var kProp = tableEntryReference.GetType().GetProperty("Key"); if (kProp != null) key = kProp.GetValue(tableEntryReference) as string; } catch { }
            if (string.IsNullOrEmpty(key)) return;
            if (key.Contains("beer_case") || key.Contains("wine_") || key.Contains("permit_gun") || key.Contains("blank_keycard"))
            if (string.IsNullOrEmpty(__result) || __result.Trim() == "?")
            {
                string n = GetLocalizedItemOverride(key);
                if (n != null) __result = n;
            }
        }
        catch { }
    }

    // ============================================================
    // 3.6b 博士之友：LoadGame 时也安排博士（第一天就有）
    // ============================================================
    // 延迟执行标志位：避免与其他mod（如小窝）的LoadGame Patch同时执行导致冲突闪退
    private static bool _pendingLoadGameRestore = false;
    private static int _loadGameRestoreDelayFrames = 0;

    public static void PostfixOnLoadGame()
    {
        try
        {
            // 不直接执行，设置延迟执行标志位，等游戏完全加载后在OnUpdate里执行
            _pendingLoadGameRestore = true;
            _loadGameRestoreDelayFrames = 30; // 延迟30帧（约0.5秒），等其他mod的LoadGame操作完成
        }
        catch (Exception ex)
        {
            Core.LogMsg("[LoadGame] PostfixOnLoadGame异常: " + ex.Message);
        }
    }

    // 在OnUpdate里执行延迟恢复（由Core.cs的OnUpdate调用）
    public static void UpdatePendingLoadGameRestore()
    {
        if (!_pendingLoadGameRestore) return;
        if (_loadGameRestoreDelayFrames > 0)
        {
            _loadGameRestoreDelayFrames--;
            return;
        }
        _pendingLoadGameRestore = false;
        try
        {
            // 读档：恢复所有特性状态
            if (FrogPowerPerk.IsActive())
            {
                FrogPowerPerk.LoadState();
            }
            // 好酒之徒：恢复宿醉状态
            if (WineLoverPerk.IsActive())
            {
                WineLoverPerk.LoadHangoverState();
            }

            if (DrJacksonFriendPerk.IsActive() && PlayerStore.Instance != null)
            {
                if (ScheduleJacksonToday()) { }
            }
        }
        catch (Exception ex)
        {
            Core.LogMsg("[博士之友] LoadGame QueueFuturClient失败: " + ex.Message);
        }
    }

    // 把蛙哥妙妙箱注册到游戏物品目录（读档时游戏走 DirectoryMaster.Item 原生工厂 → 窗口自动恢复）
    public static void PostfixInitDirectory(ItemDirectory __instance)
    {
        try { CustomStorageContainer.RegisterToDirectory(__instance); }
        catch (Exception ex) { Core.LogMsg("[自定义储物箱] PostfixInitDirectory: " + ex.Message); }
        try { LuckScoutBackpackUpgrade.RegisterToDirectory(__instance); }
        catch (Exception ex) { Core.LogMsg("[虚空珠] PostfixInitDirectory: " + ex.Message); }
    }

    // ============================================================
    // 博士夜晚商店神经模组：包装方法（ManualPatcher只能引用Patches类方法）
    // ============================================================
    public static void PostfixTradeSheetFoundryShop(object __result)
    {
        DrJacksonFriendPerk.FoundryShopPatch.Postfix(__result);
    }

    public static void PostfixTradeSheetEnergyFarmShop(object __result)
    {
        DrJacksonFriendPerk.EnergyFarmShopPatch.Postfix(__result);
    }

    public static void PrefixEmporiumShowAfterhourInv(EmporiumEntry __instance)
    {
        DrJacksonFriendPerk.EmporiumEntryShowAfterhourPatch.Prefix(__instance);
    }

    // ============================================================
    // 溶液拦截（09-11 用户确认）：酸性/碱性溶液不出现在任何池子
    // 拆包 2.5.45：唯一生成点 = MaterialDirectory.CommonChemicalSupplies → CreateAcidBottle/CreateBaseBottle → GraphUtils.EmporiumTryAdd（null 安全，返回 null 直接跳过）
    // ============================================================
    public static bool PrefixCreateAcidBottle(ref GameItem __result)
    {
        __result = null;
        return false;
    }

    public static bool PrefixCreateBaseBottle(ref GameItem __result)
    {
        __result = null;
        return false;
    }

    // ============================================================
    // 夜间报告：在每日晨报追加一行（霉运丢钱等）
    // 反射设置TMP文本（避免TMPro程序集编译期依赖）
    // ============================================================
    public static void PostfixStartOfDayOpenUI(StartOfDayUIManager __instance)
    {
        TryAppendNightReport(__instance);
    }

    public static void PostfixStartOfDayButtonClicked(StartOfDayUIManager __instance)
    {
        TryAppendNightReport(__instance);
    }

    public static void PostfixStartOfDayShowMorningReport(StartOfDayUIManager __instance)
    {
        TryAppendNightReport(__instance);
    }

    private static void TryAppendNightReport(StartOfDayUIManager __instance)
    {
        try
        {
            if (string.IsNullOrEmpty(Core.LastNightReportLine)) return;
            string line = Core.LastNightReportLine;
            Core.LastNightReportLine = null;
            if (__instance == null || __instance.startOfDayTMPPrefab == null || __instance.contentGroupObject == null)
            {
                Core.LogMsg("[夜间报告] 报告UI未就绪，无法追加");
                return;
            }
            GameObject row = UnityEngine.Object.Instantiate(__instance.startOfDayTMPPrefab, __instance.contentGroupObject.transform);
            if (row != null)
            {
                row.SetActive(true);
                bool done = false;
                try
                {
                    // 遍历自身及所有子物体，找 TMP 文本组件
                    Component[] all = row.GetComponentsInChildren<Component>(true);
                    if (all != null)
                    {
                        for (int k = 0; k < all.Length; k++)
                        {
                            Component c = all[k];
                            if (c == null) continue;
                            string cn = "";
                            try { cn = c.GetIl2CppType().FullName ?? ""; } catch { }
                            if (string.IsNullOrEmpty(cn))
                            {
                                try { cn = c.GetType().Name ?? ""; } catch { }
                            }
                            if (cn.Contains("TextMeshProUGUI") || cn.Contains("TMP_Text"))
                            {
                                // Il2Cpp 属性反射拿不到 text，改用 set_text/SetText 方法
                                bool setOk = false;
                                try
                                {
                                    MethodInfo[] ms = c.GetType().GetMethods();
                                    if (ms != null)
                                    {
                                        for (int mi2 = 0; mi2 < ms.Length; mi2++)
                                        {
                                            MethodInfo mi = ms[mi2];
                                            if (mi == null) continue;
                                            string mn = mi.Name ?? "";
                                            if (mn == "set_text" || mn == "SetText")
                                            {
                                                var ps = mi.GetParameters();
                                                if (ps != null && ps.Length == 1)
                                                {
                                                    mi.Invoke(c, new object[] { line });
                                                    setOk = true;
                                                    break;
                                                }
                                            }
                                        }
                                    }
                                }
                                catch (Exception mEx)
                                {
                                    Core.LogMsg("[夜间报告] set_text 调用失败: " + mEx.Message);
                                }
                                if (setOk) { done = true; break; }
                            }
                        }
                    }
                }
                catch (Exception innerEx)
                {
                    Core.LogMsg("[夜间报告] 反射设置文本失败: " + innerEx.Message);
                }
                if (!done)
                {
                    Core.LogMsg("[夜间报告] 未找到TMP文本组件，无法设置文本");
                    // 诊断：dump 实例行结构
                    try
                    {
                        Component[] allD = row.GetComponentsInChildren<Component>(true);
                        if (allD != null)
                        {
                            for (int k = 0; k < allD.Length; k++)
                            {
                                Component c = allD[k];
                                if (c == null) {  continue; }
                                string cn = ""; string go = "";
                                try { cn = c.GetIl2CppType().FullName ?? c.GetType().Name ?? ""; } catch { }
                                try { go = c.gameObject != null ? (c.gameObject.name ?? "") : ""; } catch { }
                            }
                        }
                    }
                    catch (Exception dumpEx)
                    {
                        Core.LogMsg("[夜间报告] 诊断dump失败: " + dumpEx.Message);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Core.LogMsg("[夜间报告] 追加失败: " + ex.Message);
        }
    }


    // RenderHandler.LoadFromAtlas - Prefix（sprite加载源头拦截，返回自定义sprite）
    public static bool PrefixLoadFromAtlas(string atlasPath, string name, ref Sprite __result)
    {
        try
        {
            if (name == CustomStorageContainer.CUSTOM_SPRITE_NAME)
            {
                Sprite customSprite = CustomStorageContainer.GetCustomSprite();
                if (customSprite != null)
                {
                    __result = customSprite;
                    return false; // 阻止原方法执行
                }
            }
        }
        catch (Exception ex)
        {
            Core.LogMsg("[自定义sprite] PrefixLoadFromAtlas异常: " + ex.Message);
        }
        return true; // 执行原方法
    }

    // ============================================================
    // 【诊断】客户接受物品判定（临时，测试后删除）
    // ============================================================
    public static void PostfixStoreClientIsClientBuying(StoreClient __instance, GameItem gameItem, ref bool __result)
    {
        try
        {
            if (__instance == null) return;
            string id = __instance.identifier ?? "?";
            string intent = "?";
            try { intent = __instance.clientIntent.ToString(); } catch { }
            string itemId = gameItem?.identifier ?? "null";
            string idList = "[]";
            try { if (__instance.clientBuyingIdList != null) idList = string.Join(",", __instance.clientBuyingIdList.ToArray()); } catch { }
            string tagList = "[]";
            try { if (__instance.clientBuyingTagList != null) tagList = string.Join(",", __instance.clientBuyingTagList.ToArray()); } catch { }
        }
        catch (Exception ex)
        {
            Core.LogMsg("[诊断判定] 异常: " + ex.Message);
        }
    }


    // ============================================================
    // 【决定性诊断】特性选择"点不了"：OnPointerClick 是否被调 + NewGameData 状态
    // 依据：ISIL 确认 OnPointerClick 内部检查 NewGameData.Instance 私有字段(0x48)，
    //       为 0 时走"取消/无操作"分支 return，不执行 SelectPerk → 点击无反应
    // ============================================================
    // ============================================================
    // 【根本修复】OnPointerClick 前强制恢复 NewGameData[0x48]=1
    // 游戏原生 .ctor/HardReset 都设 [0x48]=1（特性选择阶段=允许选择）
    // 运行时该字段被置 0 导致点击走无操作分支。Prefix 恢复正确状态。
    // ============================================================
    public static void PrefixOnPointerClick(StartingPerkElement __instance)
    {
        try
        {
            var ng = Il2Cpp.NewGameData.Instance;
            if (ng != null)
            {
                // ISIL 偏移是十进制：OnPointerClick 检查 [rax+48] = 0x30 = isInMainMenu
                byte b30 = System.Runtime.InteropServices.Marshal.ReadByte(ng.Pointer, 0x30);
                if (b30 == 0)
                {
                    System.Runtime.InteropServices.Marshal.WriteByte(ng.Pointer, 0x30, 1);
                }
            }
        }
        catch (Exception ex) { Core.LogMsg("[选点] Prefix异常: " + ex.Message); }
    }

    public static void PostfixOnPointerClick(StartingPerkElement __instance)
    {
        try
        {
        }
        catch (Exception ex) { Core.LogMsg("[选点] Postfix异常: " + ex.Message); }
    }

}
