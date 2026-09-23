using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using MelonLoader;
using UnityEngine;

namespace JacksonPerks
{

    public static partial class DestinyDice
{
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

                    catch (System.Exception ex) { Core.LogMsg("[DestinyDice.EventsFactory.Entry] 异常: " + ex.Message); }

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
}
}
