using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace JacksonPerks;

internal sealed class DrJacksonFriendPerk : CustomStartingPerk
{
    internal const string PerkId = "博士之友";

    internal override string Id => PerkId;
    internal override string DisplayName => LangHelper.T("博士之友", "Doctor Friend");
    internal override string Description => LangHelper.T("杰克逊博士，这位神秘的黑市商人，向来只与他信任的人交易。传说中，能成为博士之友的当铺老板，每周二便会收到博士的秘密拜访。选择此特性：博士每周到访一次，售卖大型机器与大型储存箱，购买享受友情价 -5%。", "Doctor Jackson, a mysterious black-market merchant, only trades with those he trusts. Legend says the pawnbroker who becomes the Doctor's friend receives a secret visit every Tuesday. Choose this perk: the Doctor visits once a week, selling large machines and large storage boxes, and everything you buy from him gets a 5% friend discount.");
    internal override int Cost => 2;
    internal override int Type => 0;

    private const int VISIT_INTERVAL_DAYS = 7; // 一周来一次
    private const int EXTRA_ITEMS_COUNT = 8; // 额外售卖物品数量

    // 上次博士来访的游戏天数
    private static int _lastJacksonVisitDay = -1;

    // 博士售卖的额外物品池（高价值物品）
    private static readonly string[] PremiumItems = {
        "system_capped_neural_core", // 神经模组
        "machine_bay_ext", // 机器区扩建
        "storage_bay_large", // 储藏区扩建
        "smuggler_bay_mod", // 走私者暗格改进
        
        "hydroponic", // 水培箱
        "xray_scanner", // X光扫描仪
        "chem_scanner", // 化学扫描仪
        "evaporator", // 蒸发器
        "vending_machine", // 自动售货机
        "backpack_large_military", // 大型军用背包
        "armor_lining", // 护甲衬里
    };
    // 博士本次进店是否已放货（每次博士进栈时重置）

    internal override void OnNewGame()
    {
        _lastJacksonVisitDay = -1;
    }

    internal static bool IsActive()
    {
        return Core.PerkActive(PerkId);
    }

    // 判断客户是不是博士
    private static bool IsJackson(StoreClient client)
    {
        if (client == null) return false;
        string name = client.displayName ?? client.identifier ?? "";
        return name.Contains("博士") || name.Contains("Jackson") || name.Contains("jackson");
    }

    // 获取当前游戏天数
    private static int GetCurrentDay()
    {
        try
        {
            // 反射搜索所有可能的day字段
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (Assembly asm in assemblies)
            {
                if (!asm.GetName().Name.StartsWith("Assembly-CSharp")) continue;
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException ex) { types = ex.Types; }
                foreach (Type t in types)
                {
                    if (t == null) continue;
                    PropertyInfo[] props = t.GetProperties(BindingFlags.Public | BindingFlags.Static);
                    foreach (PropertyInfo p in props)
                    {
                        if ((p.Name == "currentDay" || p.Name == "CurrentDay" || p.Name == "day") &&
                            p.PropertyType == typeof(int))
                        {
                            try
                            {
                                object val = p.GetValue(null);
                                if (val != null && (int)val > 0)
                                {
                                    return (int)val;
                                }
                            }
                            catch { }
                        }
                    }
                }
            }
        }
        catch
        { }
        return -1;
    }

    // ============================================================
    // 补丁1: 控制博士来访频率（两周一次）
    // ============================================================
//     [HarmonyPatch(typeof(StoreClientManager), "HandleJacksonStorage")]
    public static class HandleJacksonStoragePatch
    {
        internal static bool Prefix(StoreClientManager __instance)
        {
            if (!IsActive()) return true;

            try
            {
                int currentDay = GetCurrentDay();

                if (currentDay < 0) return true;

                if (_lastJacksonVisitDay < 0)
                {
                    _lastJacksonVisitDay = currentDay;
                    return true;
                }

                int daysSinceLastVisit = currentDay - _lastJacksonVisitDay;
                if (daysSinceLastVisit < VISIT_INTERVAL_DAYS)
                {
                    return false;
                }

                _lastJacksonVisitDay = currentDay;
                return true;
            }
            catch (Exception ex)
            {
                MelonLogger.Error("[博士之友] HandleJacksonStorage Prefix失败: " + ex.Message);
                return true;
            }
        }
    }

    // ============================================================
    // 补丁2: 当博士生成时，扩充他的售卖库存并加入神经模组
    // ============================================================
//     [HarmonyPatch(typeof(StoreClientManager), "AddClient")]
    public static class AddClientPatch
    {
        internal static void Postfix(StoreClient storeClient)
        {
            if (!IsActive()) return;
            if (storeClient == null) return;
            try
            {
                string clientName = storeClient.displayName ?? "?";
                string clientId = storeClient.identifier ?? "?";
                bool isJackson = IsJackson(storeClient);
                if (!isJackson) return;
                // 博士新进栈（放货防重已改为柜台去重，不再需要会话标记）
                // 博士买卖清单统一在 StartMainDialogue 的 AddJacksonGoodsToCounter 设置（避免 AddClient 时机被覆盖）

            }
            catch (Exception ex)
            {
                MelonLogger.Error("[博士之友] AddClient Postfix失败: " + ex.Message);
            }
        }
    }

    // 扩充博士的售卖库存
    private static void ExpandJacksonInventory(StoreClient client)
    {
        try
        {
            if (client.clientBuyingIdList == null)
            {
                client.clientBuyingIdList = new Il2CppSystem.Collections.Generic.List<string>();
            }
            else
            { }

            // 09-11 用户确认：博士不带神经模组（此方法为预留/死代码，同步清理）
            // 1. 一次卖 3 个大机器 + 3 个大储存（各加3次，不做Contains去重）
            for (int i = 0; i < 3; i++)
                client.clientBuyingIdList.Add("machine_bay_ext");
            for (int i = 0; i < 3; i++)
                client.clientBuyingIdList.Add("storage_bay_large");


            // 4. 设置clientIntent为SELL
            try
            {
                client.clientIntent = StoreClient.ClientIntent.SELL;
            }
            catch (Exception ex)
            {
                Core.LogMsg("[博士之友] 设置clientIntent失败: " + ex.Message);
            }
        }
        catch (Exception ex)
        {
            MelonLogger.Error("[博士之友] ExpandJacksonInventory失败: " + ex.Message);
        }
    }

    // ============================================================
    // 判断是否是博士客户（inventorStorage 或 displayName 匹配）
    // ============================================================
    internal static bool IsJacksonClient(StoreClient client)
    {
        if (client == null) return false;
        string id = client.identifier ?? "";
        if (id == "inventorStorage" || id == "inventor_storage") return true;
        return IsJackson(client);
    }

    // ============================================================
    // 博士上货权威钩子：ModHook.OnPlaceInventorInventoryItemEarly/Late（参考 ModuleWorkbench）
    // 之前用 OnNextClientArrived → GetCurrentClient 链路从未触发（日志无 [特殊NPC]）；
    // ModHook 是游戏原生博士补货事件，参考模组实锤可用
    // ============================================================
    private static Il2CppSystem.Action<Il2CppSystem.Collections.Generic.List<GameItem>> _onInventorStock;

    internal static void RegisterInventorStockHook()
    {
        try
        {
            if (_onInventorStock == null)
            {
                // 委托类型必须是 Il2CppSystem.Action<List<GameItem>>（ModHook 期望）；
                // 关键：new System.Action 的参数类型也必须是 Il2Cpp List（类型指针匹配，否则 ConvertDelegate 报 mismatched native type pointers）
                _onInventorStock = Il2CppInterop.Runtime.DelegateSupport.ConvertDelegate<Il2CppSystem.Action<Il2CppSystem.Collections.Generic.List<GameItem>>>(
                    (System.Delegate)new System.Action<Il2CppSystem.Collections.Generic.List<GameItem>>(OnInventorStock));
                ModHook.add_OnPlaceInventorInventoryItemEarly(_onInventorStock);
                ModHook.add_OnPlaceInventorInventoryItemLate(_onInventorStock);
            }
        }
        catch (Exception ex) { Core.LogMsg("[博士之友] RegisterInventorStockHook 异常: " + ex.Message); }
    }

    private static void OnInventorStock(Il2CppSystem.Collections.Generic.List<GameItem> playerItems)
    {
        try
        {
            if (!IsActive()) return;
            StoreClient client = null;
            try
            {
                if (PlayerStore.Instance != null && PlayerStore.Instance.currentClientInstance != null)
                    client = PlayerStore.Instance.currentClientInstance.GetClientBlueprint();
            }
            catch { }
            AddJacksonGoodsToCounter(client);
        }
        catch (Exception ex) { Core.LogMsg("[博士之友] OnInventorStock 异常: " + ex.Message); }
    }

    // 柜台是否已有同 id 的货（不同夜晚重复触发时防堆叠）
    private static bool CounterHasOnFront(string itemId)
    {
        try
        {
            EmporiumEntry em = EmporiumEntry.Instance;
            if (em == null || em.frontInvinvElement == null || em.frontInvinvElement.items == null) return false;
            foreach (var it in em.frontInvinvElement.items)
            {
                if (it != null && it.identifier == itemId) return true;
            }
        }
        catch { }
        return false;
    }

    // ============================================================
    // 博士进店放货：3大机器 + 3大储存 直接放柜台（参考ExtraDoctorBoxes经验）
    // AddDirectSellingItemToTable 验证可行（酒商卖酒成功）
    // 门控（09-10 用户确认）：仅鲁滨逊职业补货；博士之友/其他开局一律不补（原生博士货保留）
    // ============================================================
    internal static void AddJacksonGoodsToCounter(StoreClient client)
    {
        if (!IsActive()) return;   // 博士之友激活即补货（09-11 用户确认：任何开局都补，不再限定鲁滨逊）
        try
        {
            // 每次博士补货事件都尝试上货（参考 ModuleWorkbench：Early 每次重新评估，防堆叠靠 CounterHasOnFront）
            // 不再用一次性 _goodsAddedThisSession：玩家买走货后下次夜晚自动补
            PlayerStore instance = PlayerStore.Instance;
            if (instance == null) {  return; }

            // 博士只卖：1 大机器 + 1 大储存（09-11 用户确认：只留这两样，不带食品/水/药品/神经模组，任何开局一致）
            List<string> goods = new List<string> { "machine_bay_ext", "storage_bay_large" };
            // 09-12 硬爽版：固定加卖受限神经模组 + 未受限神经模组（用户拍板）
            if (BuildConfig.HARD_MODE)
            {
                goods.Add("system_capped_neural_core");
            }
            int added = 0;
            foreach (string itemId in goods)
            {
                try
                {
                    if (CounterHasOnFront(itemId)) {  continue; }
                    if (!DirectoryMaster.Has<GameItem>(itemId)) {  continue; }
                    GameItem item = DirectoryMaster.Item(itemId, true);
                    if (item == null) {  continue; }
                    // 博士之友激活：给博士卖的货直接加友情价 -5%（不依赖UI买模式）
                    if (IsActive()) { try { Patches.ApplyFriendDiscountToItem(item); } catch { } }
                    // 用通用方法添加到柜台（清标签+克隆+添加+再清标签，heat=0确保干净）
                    GameItem sellItem = MerchantHelper.AddItemToCounter(item, 0, false);
                    if (sellItem != null)
                    {
                        added++;
                        // 验证：确认赃物热度已清除（GetTagReadonly返回null说明已清除）
                        try {
                            var heatTag = sellItem.GetTagReadonly("STOLEN_VALUE_INT");
                            bool isStolen = StolenHelper.IsStolenItem(sellItem);
                        } catch { }
                    }
                }
                catch (Exception ex) { Core.LogMsg("[博士之友] 添加" + itemId + "失败: " + ex.Message); }
            }

            // 意图：卖家
            try { client.clientIntent = StoreClient.ClientIntent.SELL; } catch { }
        }
        catch (Exception ex)
        {
            Core.LogMsg("[博士之友] AddJacksonGoodsToCounter失败: " + ex.Message);
        }
    }

    // ============================================================
    // 补丁3: 玩家进入博士商店（Emporium）时，添加神经模组到博士的库存
    // 09-11 神经模组逻辑全局禁用，防重标记字段已删除

    // ============================================================
    // 补丁3a: OnArriveStore方法的Postfix，玩家进入商店时重置标记
    // ============================================================
//     [HarmonyPatch(typeof(EmporiumEntry), "OnArriveStore")]
    // ============================================================
    // 补丁3c: ShowAfterhourInv方法的Postfix，显示夜间库存时添加
    // ============================================================
//     [HarmonyPatch(typeof(EmporiumEntry), "ShowAfterhourInv")]
    public static class EmporiumEntryShowAfterhourPatch
    {
        internal static void Prefix(EmporiumEntry __instance)
        {
            // 09-11 用户确认：博士不带神经模组（全局禁用，含夜晚商店）
            return;
        }
    }

    // 通用方法：添加神经模组到博士库存（只添加到docInvElement，然后刷新UI）
    // 通用方法：添加神经模组到博士库存（afterhourInventory）
    private static void AddNeuralCoreToDoctorInv(EmporiumEntry instance)
    {
        try
        {
            // 使用afterhourInventory（夜晚库存 = 博士售卖库存）
            GameGridInventory afterhourInv = instance.afterhourInventory;
            if (afterhourInv != null)
            {
                
                // dump当前库存物品数量
                var invCountProp = afterhourInv.GetType().GetProperty("Count");
                if (invCountProp != null)
                {
                    int invCount = (int)invCountProp.GetValue(afterhourInv);
                }
                
                GameItem neuralCore = DirectoryMaster.Item("system_capped_neural_core", true);
                if (neuralCore != null)
                {
                    // 尝试用UncheckedAccept添加
                    var acceptMethod = afterhourInv.GetType().GetMethod("UncheckedAccept");
                    if (acceptMethod != null)
                    {
                        bool result = (bool)acceptMethod.Invoke(afterhourInv, new object[] { neuralCore });
                        if (result)
                        {
                        }
                    }
                    else
                    {
                        var addMethod = afterhourInv.GetType().GetMethod("Add");
                        if (addMethod != null)
                        {
                            addMethod.Invoke(afterhourInv, new object[] { neuralCore });
                        }
                    }
                }
            }
            else
            { }
        }
        catch (Exception ex)
        {
            MelonLogger.Error("[博士之友] AddNeuralCoreToDoctorInv失败: " + ex.Message);
        }
    }

    // 直接操作afterhourInventory添加物品
    private static void AddToAfterhourInventoryDirectly(EmporiumEntry instance, GameItem item)
    {
        try
        {
            if (instance.afterhourInventory == null)
            {
                return;
            }

            // 尝试调用AddItem方法
            MethodInfo addMethod = instance.afterhourInventory.GetType().GetMethod("AddItem",
                BindingFlags.Public | BindingFlags.Instance);
            if (addMethod != null)
            {
                addMethod.Invoke(instance.afterhourInventory, new object[] { item });
                return;
            }

            // 尝试调用InsertItem方法
            MethodInfo insertMethod = instance.afterhourInventory.GetType().GetMethod("InsertItem",
                BindingFlags.Public | BindingFlags.Instance);
            if (insertMethod != null)
            {
                insertMethod.Invoke(instance.afterhourInventory, new object[] { item, 0, 0 });
                return;
            }

            // 尝试调用Add方法
            MethodInfo addMethod2 = instance.afterhourInventory.GetType().GetMethod("Add",
                BindingFlags.Public | BindingFlags.Instance);
            if (addMethod2 != null)
            {
                addMethod2.Invoke(instance.afterhourInventory, new object[] { item });
                return;
            }

        }
        catch (Exception ex)
        {
            MelonLogger.Error("[博士之友] 直接添加到afterhourInventory失败: " + ex.Message);
        }
    }


    // 诊断补丁：同时Patch多个BarterHelper方法，看看哪个会在交易时触发
//     [HarmonyPatch(typeof(BarterHelper), "GetVendorOverrallSellMultiplier")]
    public static class BarterHelperSellMultiplierPatch
    {
        private static bool _neuralCoreAddedToBarter = false;

        static void Postfix(GameCharacterItem gci, GameItem item)
        {
            try
            {
                if (!IsActive()) return;
                if (gci == null) return;

                string charName = gci.identifier ?? gci.name ?? "未知";
                string charId = gci.identifier ?? "";

                if (!_neuralCoreAddedToBarter)
                {

                    if (gci.barterBuyInventory != null)
                    {

                        if (charName.Contains("博士") || charName.Contains("Jackson") || charName.Contains("jackson") ||
                            charId.Contains("inventor") || charId.Contains("doctor") || charId.Contains("jackson"))
                        {

                            GameItem neuralCore = DirectoryMaster.Item("system_capped_neural_core", true);
                            if (neuralCore != null)
                            {
                                bool result = gci.barterBuyInventory.UncheckedAccept(neuralCore);
                                if (result)
                                {
                                    _neuralCoreAddedToBarter = true;
                                }
                            }
                        }
                    }
                    else
                    { }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error("[博士之友] BarterHelperSellMultiplierPatch失败: " + ex.Message);
            }
        }
    }

    // 补丁：Patch TraderFactory.CreateDoctor()，在博士创建时保存引用
//     [HarmonyPatch(typeof(TraderFactory), "CreateDoctor")]
    public static class TraderFactoryCreateDoctorPatch
    {
        internal static GameCharacterItem _doctorInstance = null;

        static void Postfix(GameCharacterItem __result)
        {
            try
            {
                if (__result != null)
                {
                    _doctorInstance = __result;
                    string docName = __result.identifier ?? __result.name ?? "未知";
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error("[博士之友] TraderFactoryCreateDoctorPatch失败: " + ex.Message);
            }
        }
    }

    // 补丁：Patch TraderFactory.CreateTrader()，保存商人引用（夜晚博士可能通过这个方法创建）
//     [HarmonyPatch(typeof(TraderFactory), "CreateTrader")]
    public static class TraderFactoryCreateTraderPatch
    {
        internal static GameCharacterItem _lastTrader = null;

        static void Postfix(GameCharacterItem __result)
        {
            try
            {
                if (__result != null)
                {
                    _lastTrader = __result;
                    string traderName = __result.identifier ?? __result.name ?? "未知";

                    // 如果是博士，也保存到_doctorInstance
                    if (traderName.Contains("博士") || traderName.Contains("Jackson") || traderName.Contains("jackson") ||
                        traderName.Contains("inventor") || traderName.Contains("doctor") || traderName.Contains("clinic"))
                    {
                        TraderFactoryCreateDoctorPatch._doctorInstance = __result;
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error("[博士之友] TraderFactoryCreateTraderPatch失败: " + ex.Message);
            }
        }
    }

    // Patch TradeSheet.GetFoundryShop() - 往熔炉商店添加神经模组
//     [HarmonyPatch(typeof(TradeSheet), "GetFoundryShop")]
    // Patch TradeSheet.GetFoundryShop() - 每次调用都往熔炉商店添加神经模组
//     [HarmonyPatch(typeof(TradeSheet), "GetFoundryShop")]
    public static class FoundryShopPatch
    {
        internal static void Postfix(object __result)
        {
            // 09-11 用户确认：博士不带神经模组（全局禁用，含熔炉商店）
            return;
        }
    }

    // Patch TradeSheet.GetEnergyFarmShop() - 往能量农场商店添加神经模组
//     [HarmonyPatch(typeof(TradeSheet), "GetEnergyFarmShop")]
    // Patch TradeSheet.GetEnergyFarmShop() - 每次调用都往能量农场商店添加神经模组
//     [HarmonyPatch(typeof(TradeSheet), "GetEnergyFarmShop")]
    public static class EnergyFarmShopPatch
    {
        internal static void Postfix(object __result)
        {
            // 09-11 用户确认：博士不带神经模组（全局禁用，含能量农场商店）
            return;
        }
    }

}

