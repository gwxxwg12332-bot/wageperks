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
}
}
