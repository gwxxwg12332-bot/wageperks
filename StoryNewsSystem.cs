using System;
using Il2Cpp;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using MelonLoader;

namespace WagePerks
{
    // ============================================================
    // 剧情mod P0+报纸5栏：剧情新闻事件系统（测试版 2026-10-16）
    // 目标：验证「新闻 → 价格 → 报纸」全链在游戏内真实出现（拆包实锤闭环）
    //   - 价格驱动：StoreEvent.negociationDatas（NegociationData.itemType/value）
    //     GetCurrentValue 内 price += value（GameItem.txt:19071-19072，value 正=涨负=跌）
    //   - 报纸：GetActiveEvents → SelectLayout 按事件数选布局 → PopulateUI（无挤出直接截断）
    //   - 注入：StoreEventManager.QueueFuturEvent(evt, 1, true) 次日进链（R2 实锤 mod 插队点）
    //   - 5 栏轮播：复用原生 OperationList 工厂（cheatsheet :6977-6983 实锤，数值/价格/报纸全原生），
    //     6 事件一循环，每 7 天注入下一条：
    //       第 9 天  rev_mobilization      （Rev 栏，WEAPON/MEDICAL/ARMOR 全 +40）
    //       第 16 天 bm_gang_war           （BM 栏）
    //       第 23 天 sec_raid_black_market （Sec 栏，NARCOTIC+50 WEAPON+40）
    //       第 30 天 ll_riot               （LL 栏，FOOD+30 WEAPON+50）
    //       第 37 天 rev_raid_upper_level   （UL 栏，HOUSEHOLD_GOOD-30）
    //       第 44 天 story_upper_medical_shortage（自定义，MEDICAL+40）
    // 文案：原生事件 newsName/newsDescription 覆盖为中文（零破折号、不预判涨跌方向），
    //       identifier/negociationDatas 保留原生=价格链真实、报纸防叠用原生 identifier
    // 复用：WageSaveStore KV 持久化 / DestinyDice 事件工厂模式 / PatchRegistryTable 声明式注册
    // ============================================================
    internal static class StoryNewsSystem
    {
        private const string NS = "story_news";
        private const string KEY_FIRST_INJECT = "first_inject_day"; // 首次注入日（-1=未初始化）
        private const string KEY_LAST_INJECT = "last_inject_day";   // 上次注入日（注入节奏 = 每 7 天）
        private const string KEY_STAGE = "stage";                   // 轮播索引（0-5，%6 循环）

        // ============ 自定义事件（照 DestinyDice.EventsFactory 模式） ============
        // 上层区医疗物资短缺：MEDICAL +40 涨（itemType 大写键实锤；value 正=涨）
        // 商品=上城区医疗用品（设计文档 Eleanor 线商品，非虚构新物品）
        public static Il2Cpp.StoreEvent CreateUpperMedicalShortage()
        {
            var evt = new Il2Cpp.StoreEvent();
            evt.identifier = "story_upper_medical_shortage";
            evt.newsName = "上层区医疗物资告急";
            evt.newsDescription = "上层区的医疗供应链中断，诊所与私人医生都在抢购药品。传闻治安部上周查获一批走私药物，上城区医疗用品价格应声上涨。";
            evt.displayName = "上层区医疗短缺";
            evt.duration = 3;
            evt.eventType = Il2Cpp.StoreEvent.EventType.NORMALE;
            evt.eventArea = Il2Cpp.StoreEvent.EventArea.UPPER;
            if (evt.negociationDatas == null)
                evt.negociationDatas = new Il2CppSystem.Collections.Generic.List<Il2Cpp.NegociationData>();
            evt.negociationDatas.Add(new Il2Cpp.NegociationData("MEDICAL", 40, evt.newsName, evt.displayName));
            return evt;
        }

        // ============ 5 栏轮播：原生事件工厂 + 中文文案覆盖 ============
        private static Il2Cpp.StoreEvent Localize(Il2Cpp.StoreEvent src, string name, string desc)
        {
            src.newsName = name;
            src.newsDescription = desc;
            return src;
        }

        private static Il2Cpp.StoreEvent CreateStageEvent(int stage)
        {
            switch (stage % 6)
            {
                case 0:
                {
                    var e = Il2Cpp.OperationListRev.Mobilization();
                    return Localize(e, "革命军全面动员", "革命军发布总动员令，武器与药品被大量征购，军需官深夜挨家敲商铺的门。");
                }
                case 1:
                {
                    var e = Il2Cpp.OperationListBM.GangWar();
                    return Localize(e, "帮派火并升级", "两伙帮派为码头控制权大打出手，械斗从夜市烧到仓库区，黑市货源一夜吃紧。");
                }
                case 2:
                {
                    var e = Il2Cpp.OperationListSec.RaidBlackMarket();
                    return Localize(e, "治安部夜袭黑市", "治安部突袭黑市窝点，缴获一批违禁品，黑市供货链被打断。");
                }
                case 3:
                {
                    var e = Il2Cpp.OperationListLL.Riot();
                    return Localize(e, "下层区暴动", "下层区因配给粮断供爆发骚乱，商铺被砸，治安部已封锁通往码头的路口。");
                }
                case 4:
                {
                    var e = Il2Cpp.OperationListRev.RaidUpperLevel();
                    return Localize(e, "革命军夜袭上城区", "革命军小股部队夜袭上城区补给站，商户连夜抛售家具与日用品。");
                }
                default:
                    return CreateUpperMedicalShortage();
            }
        }

        // ============ OnDayStart Postfix（PatchRegistryTable 注册） ============
        public static void PostfixStoryNewsDayStart()
        {
            try { MaybeQueueNewsEvent(); }
            catch (Exception ex) { Core.LogMsg("[StoryNews] 日更注入异常: " + ex.Message); }
        }

        private static void MaybeQueueNewsEvent()
        {
            var evtMgr = (Il2Cpp.StoreStation.instance != null) ? Il2Cpp.StoreStation.instance.storeEventManager : null;
            if (evtMgr == null) return;

            int day;
            try { day = StoreStation.GetDayCounter(); } catch { return; }

            // 首次注入日初始化：第 9 天（给玩家熟悉期）
            int first = WageSaveStore.GetInt(NS, KEY_FIRST_INJECT, -1);
            if (first < 0)
            {
                first = day + 9;
                WageSaveStore.SetInt(NS, KEY_FIRST_INJECT, first);
                Core.LogMsg("[StoryNews] 首次剧情新闻注入日=" + first);
            }
            if (day < first) return;

            // 注入节奏：每 7 天一次（简单可预期，方便玩家验证；周期/条件化留实测后迭代）
            int last = WageSaveStore.GetInt(NS, KEY_LAST_INJECT, -1);
            if (last >= 0 && day < last + 7) return;

            // 轮播取档：stage 从 0 起，每注入一条 +1（%6 循环，6 周一循环覆盖五栏+自定义）
            int stage = WageSaveStore.GetInt(NS, KEY_STAGE, 0);
            Il2Cpp.StoreEvent evt = null;
            try { evt = CreateStageEvent(stage); } catch (Exception ex) { Core.LogMsg("[StoryNews] 事件工厂失败 stage=" + stage + ": " + ex.Message); }
            if (evt == null) return;

            // 该事件已在链上时顺延（原生 identifier 防叠；不推进 stage，下周重试同事件）
            try { if (evtMgr.IsEventActive(evt.identifier)) return; } catch { }

            // 注入：次日上报纸 + 价格联动生效
            try
            {
                evtMgr.QueueFuturEvent(evt, 1, true);
                WageSaveStore.SetInt(NS, KEY_LAST_INJECT, day);
                WageSaveStore.SetInt(NS, KEY_STAGE, stage + 1);
                Core.LogMsg("[StoryNews] 剧情新闻已注入: " + evt.identifier + "（stage " + stage + "），第 " + day + " 天");
            }
            catch (Exception ex) { Core.LogMsg("[StoryNews] QueueFuturEvent 失败: " + ex.Message); }
        }
    }
}
