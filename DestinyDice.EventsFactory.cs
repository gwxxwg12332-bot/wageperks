using System;

using System.Collections.Generic;

using Il2Cpp;

using Il2CppInterop.Runtime;

using Il2CppInterop.Runtime.InteropTypes;

using MelonLoader;

using UnityEngine;

namespace JacksonPerks
{
    // ============================================================

    // 命运骰子 - 无内部空间版

    // 直接"吃掉"物品：拖物品到命运骰子上 → 销毁物品 + 累计价值

    // 每满400价值触发1个随机事件

    // ============================================================

    public static partial class DestinyDice

    {



        // ===== 自造事件：下层区酒荒（进通用事件池 normalEventBlueprints，所有玩家随机遇到） =====

        private static bool _moddedEventsRegistered = false;



        public static void PostfixStoreEventOnDayStart()

        {

            try { EnsureModdedEventBlueprints(); }

            catch (Exception ex) { Core.LogMsg("[Wage's Perks] OnDayStart注入异常: " + ex.Message); }

            try { ModdedEventLinks(); }
            catch (Exception ex2) { Core.LogMsg("[Wage's Perks] 事件联动异常: " + ex2.Message); }
            // v2 激活制：每日打烊门槛回落一档（threshold>400 → -400，直到400）；value 不受影响
            try { RecedeDiceThreshold(); }
            catch (Exception ex3) { Core.LogMsg("[命运骰子] 门槛回落异常: " + ex3.Message); }

        }



        // 复合事件联动：活跃事件 → 安排客户/发放奖励（v1.2.1）

        private static bool _legacyGranted = false;

        public static void ModdedEventLinks()

        {

            var evtMgr = (Il2Cpp.StoreStation.instance != null) ? Il2Cpp.StoreStation.instance.storeEventManager : null;

            if (evtMgr == null) return;

            try

            {

                // 酒荒联动：收酒商提前到访（收购酒类）

                if (evtMgr.IsEventActive("lower_level_alcohol_shortage"))

                {

                    Il2Cpp.PlayerStore.Instance.QueueFuturClient("retired_winemaker", 0);


                }

                // 拍卖周联动：上层区阔佬客户到访

                if (evtMgr.IsEventActive("upper_level_auction_week"))

                {

                    Il2Cpp.PlayerStore.Instance.QueueFuturClient("ul_gun_buyer", 0);


                }

                // 流浪商人遗赠：送上战利品箱（每档只发一次）

                if (evtMgr.IsEventActive("wandering_merchant_legacy") && !_legacyGranted)

                {

                    _legacyGranted = true;

                    try

                    {

                        var box = Il2Cpp.DirectoryMaster.Item("sec_box", true);

                        if (box != null)

                        {

                            Il2Cpp.PlayerStore.Instance.AddDirectSellingItemToTable(box, false, false, false, 0);


                        }

                    }

                    catch (Exception exb) { Core.LogMsg("[Wage's Perks] 遗赠发箱失败: " + exb.Message); }

                }

            }

            catch (Exception ex) { Core.LogMsg("[Wage's Perks] 事件联动内部异常: " + ex.Message); }

        }



        // 幂等注册：OnDayStart 时确保蓝图已注入（normalEventBlueprints 此时已初始化）

        public static void EnsureModdedEventBlueprints()

        {

            try

            {

                if (_moddedEventsRegistered) return;

                var blueprints = Il2Cpp.StoreEventManager.normalEventBlueprints;

                if (blueprints == null) return;

                for (int i = 0; i < blueprints.Count; i++)

                {

                    try

                    {

                        if (blueprints[i] != null && blueprints[i].identifier == "lower_level_alcohol_shortage")

                        { _moddedEventsRegistered = true; return; }

                    }

                    catch { }

                }

                RegisterModdedEvent(blueprints, "lower_level_alcohol_shortage", () => CreateLowerLevelAlcoholShortage(), 10);

                RegisterModdedEvent(blueprints, "security_undercover_patrol", () => CreateSecurityUndercoverPatrol(), 8);

                RegisterModdedEvent(blueprints, "nutrifruit_new_release", () => CreateNutrifruitNewRelease(), 6);

                RegisterModdedEvent(blueprints, "dumping_grounds_cleanup", () => CreateDumpingGroundsCleanup(), 7);

                RegisterModdedEvent(blueprints, "black_market_sweep", () => CreateBlackMarketSweep(), 6);

                RegisterModdedEvent(blueprints, "upper_level_luxury_expo", () => CreateUpperLevelLuxuryExpo(), 5);

                // v1.2.1 复合型事件

                RegisterModdedEvent(blueprints, "black_market_arms_flow", () => CreateBlackMarketArmsFlow(), 5);

                RegisterModdedEvent(blueprints, "upper_level_auction_week", () => CreateUpperLevelAuctionWeek(), 5);

                RegisterModdedEvent(blueprints, "nutrifruit_crisis_hoard", () => CreateNutrifruitCrisisHoard(), 4);

                RegisterModdedEvent(blueprints, "train_heist_aftermath", () => CreateTrainHeistAftermath(), 5);

                RegisterModdedEvent(blueprints, "wandering_merchant_legacy", () => CreateWanderingMerchantLegacy(), 2);

                RegisterModdedEvent(blueprints, "drunk_riot", () => CreateDrunkRiot(), 5);

                _moddedEventsRegistered = true;

                Core.LogMsg("[Wage's Perks] 事件池注入: 12个mod事件已注册");

            }

            catch (Exception ex) { Core.LogMsg("[Wage's Perks] 事件池注入失败: " + ex.Message); }

        }



        private static void RegisterModdedEvent(Il2CppSystem.Collections.Generic.List<Il2Cpp.StoreEventBlueprint> blueprints, string id, System.Func<Il2Cpp.StoreEvent> factory, int weight)

        {

            try

            {

                System.Func<Il2Cpp.StoreEvent> sysFunc = factory;

                var il2cppFunc = DelegateSupport.ConvertDelegate<Il2CppSystem.Func<Il2Cpp.StoreEvent>>((System.Delegate)sysFunc);

                var bp = new Il2Cpp.StoreEventBlueprint(il2cppFunc, weight, id);

                blueprints.Add(bp);


            }

            catch (Exception ex) { Core.LogMsg("[Wage's Perks] 事件注册失败 " + id + ": " + ex.Message); }

        }



        // 事件蓝图：下层区酒荒（用户样板文案）

        public static Il2Cpp.StoreEvent CreateLowerLevelAlcoholShortage()

        {

            var evt = new Il2Cpp.StoreEvent();

            evt.identifier = "lower_level_alcohol_shortage";

            evt.newsName = "LOWER LEVEL CELLARS RUN DRY!";

            evt.newsDescription = "A fire gutted the lower-level brewery, wiping out months of stock. The drunks are restless, and a queue is already forming at the black market door. Alcohol prices are expected to surge.";

            evt.displayName = "Lower Level Alcohol Shortage";

            evt.duration = 3;

            evt.eventType = Il2Cpp.StoreEvent.EventType.NORMALE;

            evt.eventArea = Il2Cpp.StoreEvent.EventArea.LOWER;

            if (evt.negociationDatas == null)

                evt.negociationDatas = new Il2CppSystem.Collections.Generic.List<Il2Cpp.NegociationData>();

            evt.negociationDatas.Add(new Il2Cpp.NegociationData("ALCOHOL", 60, evt.newsName, evt.displayName));

            return evt;

        }



        // 事件蓝图：安保部换装巡逻（便衣盯梢，武器/违禁品风险升）

        public static Il2Cpp.StoreEvent CreateSecurityUndercoverPatrol()

        {

            var evt = new Il2Cpp.StoreEvent();

            evt.identifier = "security_undercover_patrol";

            evt.newsName = "SECURITY GOES UNDERCOVER!";

            evt.newsDescription = "Security officers are patrolling in plain clothes to catch shady deals. Everyone is on edge - contraband and weapon trades are getting risky. Expect security gear and weapon prices to rise.";

            evt.displayName = "Undercover Patrol";

            evt.duration = 3;

            evt.eventType = Il2Cpp.StoreEvent.EventType.NORMALE;

            evt.eventArea = Il2Cpp.StoreEvent.EventArea.LOWER;

            if (evt.negociationDatas == null)

                evt.negociationDatas = new Il2CppSystem.Collections.Generic.List<Il2Cpp.NegociationData>();

            evt.negociationDatas.Add(new Il2Cpp.NegociationData("WEAPON", 30, evt.newsName, evt.displayName));

            evt.negociationDatas.Add(new Il2Cpp.NegociationData("CONTRABAND", 30, evt.newsName, evt.displayName));

            return evt;

        }



        // 事件蓝图：营养果新品发布会（食品/零食热销）

        public static Il2Cpp.StoreEvent CreateNutrifruitNewRelease()

        {

            var evt = new Il2Cpp.StoreEvent();

            evt.identifier = "nutrifruit_new_release";

            evt.newsName = "NUTRIFRUIT™ NEW FLAVOR LAUNCH!";

            evt.newsDescription = "Nutrifruit Co. unveils its new flavor at a grand launch party. Upper-level folks are stocking up on snacks for the hype. Expect food and treat prices to rise.";

            evt.displayName = "Nutrifruit New Release";

            evt.duration = 4;

            evt.eventType = Il2Cpp.StoreEvent.EventType.NORMALE;

            evt.eventArea = Il2Cpp.StoreEvent.EventArea.UPPER;

            if (evt.negociationDatas == null)

                evt.negociationDatas = new Il2CppSystem.Collections.Generic.List<Il2Cpp.NegociationData>();

            evt.negociationDatas.Add(new Il2Cpp.NegociationData("FOOD", 25, evt.newsName, evt.displayName));

            evt.negociationDatas.Add(new Il2Cpp.NegociationData("TREAT", 25, evt.newsName, evt.displayName));

            return evt;

        }



        // 事件蓝图：垃圾场大清理（材料供应充足降价）

        public static Il2Cpp.StoreEvent CreateDumpingGroundsCleanup()

        {

            var evt = new Il2Cpp.StoreEvent();

            evt.identifier = "dumping_grounds_cleanup";

            evt.newsName = "DUMPING GROUNDS CLEANUP BEGINS!";

            evt.newsDescription = "Station Command has ordered a massive cleanup of the dumping grounds. Scavengers are hauling out loads of scrap and parts - a golden opportunity for bargain hunters. Expect material prices to fall.";

            evt.displayName = "Dumping Grounds Cleanup";

            evt.duration = 3;

            evt.eventType = Il2Cpp.StoreEvent.EventType.NORMALE;

            evt.eventArea = Il2Cpp.StoreEvent.EventArea.LOWER;

            if (evt.negociationDatas == null)

                evt.negociationDatas = new Il2CppSystem.Collections.Generic.List<Il2Cpp.NegociationData>();

            evt.negociationDatas.Add(new Il2Cpp.NegociationData("MATERIAL", -25, evt.newsName, evt.displayName));

            return evt;

        }



        // 事件蓝图：黑市码头大扫荡（违禁品价格飙升）

        public static Il2Cpp.StoreEvent CreateBlackMarketSweep()

        {

            var evt = new Il2Cpp.StoreEvent();

            evt.identifier = "black_market_sweep";

            evt.newsName = "BLACK MARKET PORT SWEPT!";

            evt.newsDescription = "Security raided the black market docks, seizing shipments and burning contraband. Smugglers are laying low and goods are scarce. Expect contraband prices to surge.";

            evt.displayName = "Black Market Sweep";

            evt.duration = 3;

            evt.eventType = Il2Cpp.StoreEvent.EventType.NORMALE;

            evt.eventArea = Il2Cpp.StoreEvent.EventArea.ALL;

            if (evt.negociationDatas == null)

                evt.negociationDatas = new Il2CppSystem.Collections.Generic.List<Il2Cpp.NegociationData>();

            evt.negociationDatas.Add(new Il2Cpp.NegociationData("CONTRABAND", 100, evt.newsName, evt.displayName));

            return evt;

        }



        // 事件蓝图：上层区奢华展（奢侈品价格走高）

        public static Il2Cpp.StoreEvent CreateUpperLevelLuxuryExpo()

        {

            var evt = new Il2Cpp.StoreEvent();

            evt.identifier = "upper_level_luxury_expo";

            evt.newsName = "UPPER LEVEL LUXURY EXPO!";

            evt.newsDescription = "The upper level is hosting a grand luxury expo. Hedonists are splurging on fine goods and rare treasures. Expect luxury prices to soar.";

            evt.displayName = "Luxury Expo";

            evt.duration = 4;

            evt.eventType = Il2Cpp.StoreEvent.EventType.NORMALE;

            evt.eventArea = Il2Cpp.StoreEvent.EventArea.UPPER;

            if (evt.negociationDatas == null)

                evt.negociationDatas = new Il2CppSystem.Collections.Generic.List<Il2Cpp.NegociationData>();

            evt.negociationDatas.Add(new Il2Cpp.NegociationData("LUXURY_ITEM", 40, evt.newsName, evt.displayName));

            return evt;

        }



        // 复合事件：黑市军火流入（赃物流入，武器/违禁品降价）

        public static Il2Cpp.StoreEvent CreateBlackMarketArmsFlow()

        {

            var evt = new Il2Cpp.StoreEvent();

            evt.identifier = "black_market_arms_flow";

            evt.newsName = "BLACK MARKET ARMS FLOW!";

            evt.newsDescription = "Stolen weapons from the recent heists are flooding the black market. Smugglers are dumping stock at bargain prices. Expect weapon and contraband prices to fall.";

            evt.displayName = "Black Market Arms Flow";

            evt.duration = 3;

            evt.eventType = Il2Cpp.StoreEvent.EventType.NORMALE;

            evt.eventArea = Il2Cpp.StoreEvent.EventArea.LOWER;

            if (evt.negociationDatas == null)

                evt.negociationDatas = new Il2CppSystem.Collections.Generic.List<Il2Cpp.NegociationData>();

            evt.negociationDatas.Add(new Il2Cpp.NegociationData("WEAPON", -25, evt.newsName, evt.displayName));

            evt.negociationDatas.Add(new Il2Cpp.NegociationData("CONTRABAND", -20, evt.newsName, evt.displayName));

            return evt;

        }



        // 复合事件：上层区拍卖周（奢侈品涨 + 上层客户到访）

        public static Il2Cpp.StoreEvent CreateUpperLevelAuctionWeek()

        {

            var evt = new Il2Cpp.StoreEvent();

            evt.identifier = "upper_level_auction_week";

            evt.newsName = "UPPER LEVEL AUCTION WEEK!";

            evt.newsDescription = "The upper level is hosting a week-long auction. Collectors are bidding fiercely on rare goods and luxury items. Expect luxury prices to soar - and wealthy customers to visit your shop.";

            evt.displayName = "Upper Level Auction Week";

            evt.duration = 4;

            evt.eventType = Il2Cpp.StoreEvent.EventType.NORMALE;

            evt.eventArea = Il2Cpp.StoreEvent.EventArea.UPPER;

            if (evt.negociationDatas == null)

                evt.negociationDatas = new Il2CppSystem.Collections.Generic.List<Il2Cpp.NegociationData>();

            evt.negociationDatas.Add(new Il2Cpp.NegociationData("LUXURY_ITEM", 40, evt.newsName, evt.displayName));

            evt.negociationDatas.Add(new Il2Cpp.NegociationData("SUBSTANCE", 20, evt.newsName, evt.displayName));

            return evt;

        }



        // 复合事件：营养果危机囤积（食品跌 + 违禁品涨）

        public static Il2Cpp.StoreEvent CreateNutrifruitCrisisHoard()

        {

            var evt = new Il2Cpp.StoreEvent();

            evt.identifier = "nutrifruit_crisis_hoard";

            evt.newsName = "NUTRIFRUIT CRISIS AND BLACK MARKET HOARDING!";

            evt.newsDescription = "Another Nutrifruit recall has sparked panic, while black marketeers hoard supplies to drive prices up. Expect food prices to fall and contraband prices to surge.";

            evt.displayName = "Nutrifruit Crisis Hoarding";

            evt.duration = 3;

            evt.eventType = Il2Cpp.StoreEvent.EventType.NORMALE;

            evt.eventArea = Il2Cpp.StoreEvent.EventArea.ALL;

            if (evt.negociationDatas == null)

                evt.negociationDatas = new Il2CppSystem.Collections.Generic.List<Il2Cpp.NegociationData>();

            evt.negociationDatas.Add(new Il2Cpp.NegociationData("FOOD", -30, evt.newsName, evt.displayName));

            evt.negociationDatas.Add(new Il2Cpp.NegociationData("CONTRABAND", 40, evt.newsName, evt.displayName));

            return evt;

        }



        // 复合事件：火车劫案余波（补给箱跌 + 门禁卡涨）

        public static Il2Cpp.StoreEvent CreateTrainHeistAftermath()

        {

            var evt = new Il2Cpp.StoreEvent();

            evt.identifier = "train_heist_aftermath";

            evt.newsName = "TRAIN HEIST AFTERMATH!";

            evt.newsDescription = "Following the recent train heist, stolen supply crates are circulating at low prices, while Security has tightened control over access cards. Expect crate prices to fall and card prices to rise.";

            evt.displayName = "Train Heist Aftermath";

            evt.duration = 3;

            evt.eventType = Il2Cpp.StoreEvent.EventType.NORMALE;

            evt.eventArea = Il2Cpp.StoreEvent.EventArea.ALL;

            if (evt.negociationDatas == null)

                evt.negociationDatas = new Il2CppSystem.Collections.Generic.List<Il2Cpp.NegociationData>();

            evt.negociationDatas.Add(new Il2Cpp.NegociationData("SUPPLY_CRATE", -25, evt.newsName, evt.displayName));

            evt.negociationDatas.Add(new Il2Cpp.NegociationData("ACCESS_CARD", 25, evt.newsName, evt.displayName));

            return evt;

        }



        // 复合事件：流浪商人遗赠（奖励：治安战利品箱送上柜台）

        public static Il2Cpp.StoreEvent CreateWanderingMerchantLegacy()

        {

            var evt = new Il2Cpp.StoreEvent();

            evt.identifier = "wandering_merchant_legacy";

            evt.newsName = "WANDERING MERCHANT'S LEGACY!";

            evt.newsDescription = "An old wandering merchant passed away at the station gates, leaving his belongings to the first pawnshop that helped him in hard times. A gift has been delivered to your shop.";

            evt.displayName = "Wandering Merchant's Legacy";

            evt.duration = 2;

            evt.eventType = Il2Cpp.StoreEvent.EventType.NORMALE;

            evt.eventArea = Il2Cpp.StoreEvent.EventArea.ALL;

            if (evt.negociationDatas == null)

                evt.negociationDatas = new Il2CppSystem.Collections.Generic.List<Il2Cpp.NegociationData>();

            evt.negociationDatas.Add(new Il2Cpp.NegociationData("MATERIAL", 5, evt.newsName, evt.displayName));

            return evt;

        }



        // 复合事件：酒鬼闹事（酒跌 + 医疗涨）

        public static Il2Cpp.StoreEvent CreateDrunkRiot()

        {

            var evt = new Il2Cpp.StoreEvent();

            evt.identifier = "drunk_riot";

            evt.newsName = "DRUNKS RIOT IN THE LOWER LEVEL!";

            evt.newsDescription = "A brawl between drunks spilled onto the streets, smashing crates of booze and injuring bystanders. Alcohol stocks are damaged, while medical demand rises.";

            evt.displayName = "Drunk Riot";

            evt.duration = 2;

            evt.eventType = Il2Cpp.StoreEvent.EventType.NORMALE;

            evt.eventArea = Il2Cpp.StoreEvent.EventArea.LOWER;

            if (evt.negociationDatas == null)

                evt.negociationDatas = new Il2CppSystem.Collections.Generic.List<Il2Cpp.NegociationData>();

            evt.negociationDatas.Add(new Il2Cpp.NegociationData("ALCOHOL", -20, evt.newsName, evt.displayName));

            evt.negociationDatas.Add(new Il2Cpp.NegociationData("MEDICAL", 15, evt.newsName, evt.displayName));

            return evt;

        }



        public static string TriggerRandomEvent(GameItem dice)
        {

            try

            {

                // 从游戏原生事件蓝图池随机挑一个，明天触发（参照MoreEvents模式）

                var evtMgr = (StoreStation.instance != null) ? StoreStation.instance.storeEventManager : null;

                if (evtMgr == null)

                {


                    return null;

                }

                var blueprints = StoreEventManager.normalEventBlueprints;

                if (blueprints == null || blueprints.Count == 0)

                {


                    return null;

                }



                // 随机挑一个蓝图（排除当前正在进行的，避免重复）

                StoreEventBlueprint bp = null;

                for (int attempt = 0; attempt < 5; attempt++)

                {

                    int idx = UnityEngine.Random.Range(0, blueprints.Count);

                    var candidate = blueprints[idx];

                    if (candidate == null || candidate.storeEventFunc == null) continue;

                    try

                    {

                        if (!evtMgr.IsEventActive(candidate.identifier)) { bp = candidate; break; }

                    }

                    catch { bp = candidate; break; }

                }

                if (bp == null)

                {

                    for (int i = 0; i < blueprints.Count; i++)

                    {

                        var candidate = blueprints[i];

                        if (candidate != null && candidate.storeEventFunc != null) { bp = candidate; break; }

                    }

                }

                if (bp == null) { Core.LogMsg("[命运骰子] 事件蓝图全为空"); return null; }



                StoreEvent evt = bp.storeEventFunc.Invoke();

                if (evt == null) { Core.LogMsg("[命运骰子] 事件实例化失败"); return null; }



                // 汉化事件文本（显示名/新闻标题/新闻描述）

                LocalizeEvent(evt);



                // 明天触发（IsKnown=true，玩家能看到预告）

                evtMgr.QueueFuturEvent(evt, 1, true);




                // 触发计数（写标签 + 更新面板标题）

                if (dice != null)

                {

                    int trig = GetTagInt(dice, DICE_TRIGGER_TAG);

                    SetTagInt(dice, DICE_TRIGGER_TAG, trig + 1);
                    int cur = GetTagInt(dice, DICE_VALUE_TAG);

                    UpdateDicePanelTitle(dice, cur);

                }
                return evt.identifier;
            }

            catch (Exception ex)

            {

                Core.LogMsg("[命运骰子] 触发事件异常: " + ex.Message);

            
                return null;
            }
                    }
}

}
