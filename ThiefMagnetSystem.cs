using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace WagePerks;

// ============================================================
// 招贼体质大改（决策#10 派活单 20261006，用户拍板）
// 在"治安检查+20%且无法豁免（快件单已做）"基础上新增 4 机制：
//   ① 每日丢价值最高物品（ThiefMagnetDailyTheftEnabled，夜里被偷，次日小贩半价卖回）
//   ② 黑市必上门（ThiefMagnetBlackMarketEnabled+Chance 默认100%可调）
//   ③ 小偷跳脸（ThiefMagnetVisitEnabled，小偷上门踩点，40% 概率）
//   ④ 小偷次日回卖（与①联动：被偷=被拿走，次日小偷上门把货半价卖回）
// 事件通道互斥：回卖/跳脸/黑市 同日只触发一个上门事件（event_channel_last_day），
//   AddictOfficerEvent 让路（同一通道）。
// 模式：仿 AddictOfficerEvent（日结调度/客户生成/夜报）+ BadLuck（日结防重/玩家提示）。
// ============================================================
internal static class ThiefMagnetSystem
{
    private const string NS = "thief_magnet";
    private const string KEY_THEFT_DAY = "theft_last_day";        // 每日偷盗防重
    private const string KEY_STOLEN = "stolen_ids";               // 被偷物品 id（逗号分隔，供次日回卖/读档兜底）
    private const string KEY_CHANNEL = "event_channel_last_day";  // 上门事件通道（与 AddictOfficer 共享）

    // 被偷物品对象快照（内存保留完整状态——改名/品质/tag/内部内容全保留；跨天同会话存活）
    // 读档兜底：对象丢失 → 按 stolen_ids 用 WageItemFactory 按 id 重建（基础状态）
    private static readonly List<GameItem> _stolenItems = new List<GameItem>();

    // 会话内映射（10-06 拆包校准 JXXoUguSncb6m6JoPxCHGb）：上桌实例 → 被偷 id
    // uniqueId 不随档（内存计数器）/ 实例 tag 不随档 → 用映射：上桌时登记，OnItemBought 查表按实例精确清账（同 id 另一件不误清）
    // 跨读档：映射空 → 读档重挂 KEY_STOLEN 重建 → 上桌时重新登记（天然重建）；过天清理防残留
    private static readonly Dictionary<GameItem, string> _stolenMap = new Dictionary<GameItem, string>();

    // ============ 日结入口（Patches.Lifecycle.PostfixOnBeginDay 调用——ApplyBadLuck 同链） ============
    internal static void OnBeginDay()
    {
        try
        {
            if (!ThiefMagnetPerk.IsActive()) { Core.LogMsg("[招贼体质] OnBeginDay跳过：特性未激活"); return; }
            int day = GetDay();

            // ① 每日偷盗（夜里发生，每晚必偷——独立于上门事件，不被黑市/跳脸 return 阻断）
            if (BuildConfig.ThiefMagnetDailyTheftEnabled)
            {
                _stolenMap.Clear(); // 过天清理会话映射（旧上桌物品已销毁/清除，防残留；当天上桌会重新登记）
                RunDailyTheft(day);
            }

            // ② 小偷上门（10-06 用户澄清：小偷=黑市同一角色）
            //    跳脸=小偷昨晚偷了货 → 今天上门半价卖回；同时带黑市的货来卖（BlackMarketEnabled）
            //    每日按 BlackMarketChance（默认 1.0=必来）；与 AddictOfficer 事件通道互斥
            //    10-06 A7：无偷盗史（新档第一天/从未被偷）→ 小偷不上门（无偷盗则无跳脸）
            bool gate = BuildConfig.ThiefMagnetVisitEnabled || BuildConfig.ThiefMagnetBlackMarketEnabled;
            bool hasTheftHistory = WageSaveStore.GetInt(NS, KEY_THEFT_DAY, -1) != -1;
            bool chan = IsChannelFree(day);
            float rnd = UnityEngine.Random.value;
            Core.LogMsg("[招贼体质] OnBeginDay day=" + day + " gate=" + gate + " 偷盗史=" + hasTheftHistory + " 通道空=" + chan + " random=" + rnd + " 阈值=" + BuildConfig.ThiefMagnetBlackMarketChance);
            if (gate && hasTheftHistory && chan
                && rnd < BuildConfig.ThiefMagnetBlackMarketChance)
            {
                SpawnThiefVisit(day);
            }
        }
        catch (System.Exception ex) { Core.LogMsg("[招贼体质] OnBeginDay失败: " + ex.Message); }
    }

    // ============ 每日偷盗（夜里被偷价值最高物品，进小偷库存供次日回卖） ============
    private static void RunDailyTheft(int day)
    {
        if (WageSaveStore.GetInt(NS, KEY_THEFT_DAY, -1) == day) return;
        EmporiumEntry em = EmporiumEntry.Instance;
        if (em == null) return;

        var candidates = new List<(GameItem item, GameInventory inv, long value)>();
        try
        {
            foreach (GameInventory inv in CollectInventories(em))
            {
                if (inv == null || inv.childItems == null) continue;
                for (int i = 0; i < inv.childItems.Count; i++)
                {
                    GameItem it = inv.childItems[i];
                    if (it == null) continue;
                    // 10-06 A1：容器不被偷（妙妙箱/腰包/背包/物资箱等有内部库存的）——只偷普通物品
                    try { if (it.contentWindow != null) continue; } catch { }
                    // 排除在售（柜台/货架待售）——not_purchased 标记（GuMachineSystem.cs:33 同款）
                    bool onSale = false;
                    try { onSale = it.IsTag("not_purchased") || it.IsTag("TAG_NOT_PURCHASED"); } catch { }
                    if (onSale) continue;
                    long v = 0;
                    try { v = it.GetCurrentValue(); } catch { }
                    candidates.Add((it, inv, v));
                }
            }
        }
        catch (System.Exception ex) { Core.LogMsg("[招贼体质] 扫描库存失败: " + ex.Message); }

        if (candidates.Count == 0) return;

        int count = Math.Max(1, BuildConfig.ThiefMagnetDailyTheftCount);
        // 10-06 A3：被偷物种类随机化——不再固定偷"价值最高那几件"（同一种会被重复偷）
        //   从高价值区 top K（count×3，至少 5 件）中随机取 count 件 → 价值仍优先、种类自然分散
        var ranked = candidates.OrderByDescending(x => x.value).ToList();
        int poolSize = Math.Max(count * 3, Math.Min(5, ranked.Count));
        var stolen = ranked.Take(Math.Min(poolSize, ranked.Count)).OrderBy(_ => Core.Rng.Next()).Take(count).ToList();
        if (stolen.Count == 0) return;

        var stolenIds = new List<string>();
        long totalValue = 0;
        foreach (var s in stolen)
        {
            string id = "?";
            try { id = s.item.identifier ?? "?"; } catch { }
            stolenIds.Add(id);
            totalValue += s.value;
            try { s.inv.Expel(s.item); } // 移除=被偷走（AddictOfficer 没收同款；不 Destroy 防悬垂——虚空珠教训）
            catch (System.Exception ex) { Core.LogMsg("[招贼体质] 偷盗移除失败: " + ex.Message); }
            _stolenItems.Add(s.item); // 内存快照：保留完整状态（改名/品质/tag），次日回卖原对象放回
        }

        // 记录被偷 id（次日回卖用）
        // 10-06 A6 根因修（525BFCC1 拍板）：KEY_STOLEN 改为追加合并——原覆盖写导致连续多天被偷时旧记录丢失（玩家"统计不到所有物品"）
        try
        {
            string prevStored = WageSaveStore.GetString(NS, KEY_STOLEN, "");
            var merged = new List<string>();
            if (!string.IsNullOrEmpty(prevStored)) merged.AddRange(prevStored.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
            merged.AddRange(stolenIds);
            WageSaveStore.SetString(NS, KEY_STOLEN, string.Join(",", merged));
        }
        catch { WageSaveStore.SetString(NS, KEY_STOLEN, string.Join(",", stolenIds)); }
        WageSaveStore.SetInt(NS, KEY_THEFT_DAY, day);
        // 10-07 A8（玩家反馈"小退后小偷再上门不带被偷物"）：小偷状态需即时落盘——WageSaveStore 只在
        //   SaveGame/EndDay 时 Flush，进程退出（Alt+F4/关闭游戏=小退）时 _dirty 未落盘 → 重进读文件=旧值
        //   （无 KEY_STOLEN/KEY_THEFT_DAY）→ 读档重挂判定 hasStolen=false → 被偷物丢失。这里立即 Flush 防丢。
        try { WageSaveStore.Flush(); } catch (System.Exception ex) { Core.LogMsg("[招贼体质] 偷盗状态落盘失败: " + ex.Message); }

        try { var _ps = PlayerStore.Instance; if (_ps != null) _ps.AddNightLog("[招贼体质] " + LangHelper.T("昨晚打烊后，有人趁黑摸进店里，偷走了你的", "Last night after closing, someone slipped in and stole your") + " " + stolenIds.Count + " " + LangHelper.T("件物品（价值", " item(s) worth") + " " + totalValue + "）。", "#7FC97F"); } catch { }
        Core.AddNightReportLine("[招贼体质] " + LangHelper.T("昨晚打烊后，有人趁黑摸进店里，偷走了你的", "Last night after closing, someone slipped in and stole your") + " " + stolenIds.Count + " " + LangHelper.T("件物品（价值", " item(s) worth") + " " + totalValue + "）。");
    }

    // ============ 小偷次日回卖（10-06 改为 BarterOffer 交易界面卖——AttachThiefResaleOffer 接管；原 DoResale 放后库已废弃） ============

    // ============ 小偷上门（跳脸+回卖+带黑市货，占事件通道） ============
    // 10-06 用户拍板：直接调用原版小偷（CreateThief——形象/名字/对话全原版，不用自定义对话）
    // 上桌方式（10-06 二修）：BarterOffer 对小偷失效（日志实锤 hasStolen=True 但无上桌——
    //   小偷对话不触发 ShowCurrentSet）→ 改挂 StartMainDialogue Postfix（客户对话入口实锤 StoreClient.txt:6181），对话时直接上桌
    private static void SpawnThiefVisit(int day)
    {
        bool hasStolen = !string.IsNullOrEmpty(WageSaveStore.GetString(NS, KEY_STOLEN, ""));
        StoreClient thief = null;
        try { thief = StoreClientList.CreateThief(); } catch { thief = null; } // 原版小偷（形象/名字/对话=原版）
        if (thief == null) thief = StoreClientList.CreateFlexiBuyer(); // 兜底
        if (thief == null) { Core.LogMsg("[招贼体质] 创建小偷客户失败"); return; }
        thief.eventSourceId = "thief_magnet_visit";
        _pendingResale = hasStolen; // 待上桌标记（对话时执行）

        GetStoreClientManager()?.AddClient(thief);
        Core.LogMsg("[招贼体质] 小偷客户已生成并加入队列 eventSourceId=" + thief.eventSourceId + " sprite=" + thief.spriteName + " hasStolen=" + hasStolen);
        WageSaveStore.SetInt(NS, KEY_CHANNEL, day);
        // 10-07 A8：KEY_CHANNEL（小偷上门日）同样即时落盘——防进程退出丢标记 → 重进读档当天判定不到小偷日
        try { WageSaveStore.Flush(); } catch (System.Exception ex) { Core.LogMsg("[招贼体质] 上门标记落盘失败: " + ex.Message); }
    }

    // 待上桌标记（对话 Postfix 用——防重复上桌）
    private static bool _pendingResale = false;

    // ============ 小偷对话上桌（StartMainDialogue Postfix——客户对话入口） ============
    // 被偷物散件上桌（半价，随其他售卖物品一起，不放箱子）+ 黑市货散件上桌（违禁标记）
    // 判定（10-06 三修）：eventSourceId 是运行时标记读档不随档（重进变情报贩子无货）——
    //   改双键判定：eventSourceId=="thief_magnet_visit"（本会话）|| identifier=="thief"（原版存档字段，读档后仍在）
    internal static void PostfixStartMainDialogue(StoreClient __instance)
    {
        try
        {
            if (__instance == null) return;
            string id = "";
            string src = "";
            try { id = __instance.identifier ?? ""; } catch { }
            try { src = __instance.eventSourceId ?? ""; } catch { }
            if (src != "thief_magnet_visit" && id != "thief") return; // 双键判定（读档兼容）
            if (!_pendingResale) { /* 无待上桌货（未偷/已上过）*/ return; }
            _pendingResale = false;

            // ① 被偷物散件上桌（半价）——不进箱子
            List<GameItem> pool = _stolenItems.ToList();
            _stolenItems.Clear();
            if (pool.Count == 0)
            {
                // 读档兜底：对象丢失 → 按 stolen_ids 重建（基础状态）
                string stored = WageSaveStore.GetString(NS, KEY_STOLEN, "");
                foreach (var id2 in stored.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var it = WageAPI.WageItemFactory.Create(id2, true);
                    if (it != null) pool.Add(it);
                }
            }
            int restored = 0;
            foreach (var it in pool)
            {
                try { it.unitValue = it.unitValue / 2; } catch { } // 半价卖回（交易价半价；买回/赎回时恢复原价——见 OnItemBought/RedeemStolenItems）
                // 10-06 拆包实锤（memos/HGEhRccFpfXTq4NbLyWvXf）：tooltip 走 BOUGHT_PRICE_TAG 买入价分支（GameItemElement TagState[0x48]）——
                //   被偷物带买入价 tag → tooltip 显示原价=半价不显示。上桌前清掉 + 挂"半价卖回"显示标注（valueModifier=0 纯显示不改价）
                try { it.DisableTag("BOUGHT_PRICE_TAG", true); } catch { }
                AddHalfPriceFeature(it);
                // 会话内映射登记（实例→被偷id）——OnItemBought 查表精确清账（拆包校准 JXXoUguSncb6m6JoPxCHGb）
                try { if (!string.IsNullOrEmpty(it.identifier)) _stolenMap[it] = it.identifier; } catch { }
                Il2Cpp.PlayerStore.Instance.AddDirectSellingItemToTable(it, false, false, false, 0); // 散件上桌（isOwend=false=客户卖品）
                restored++;
            }
            // 10-06 用户拍板：KEY_STOLEN 保留（不清）——小偷那买不起 → 蛙哥服务卡赎回（被偷物都会被找到，只是换主人）
            //   读档兜底依赖 KEY_STOLEN 重建；蛙哥服务卡 RedeemStolenItems 赎回成功才清
            if (restored > 0) Core.LogMsg("[招贼体质] 被偷物散件上桌 " + restored + " 件（半价，小偷对话）");

            // ② 黑市货散件上桌（违禁标记）
            int bm = 0;
            foreach (string bid in BlackMarketGoodsIds)
            {
                if (UnityEngine.Random.value < 0.4f) // 每件 40% 概率（平均 2 件）
                {
                    GameItem bit = MerchantHelper.AddItemToCounter(bid, 0, false, true); // ignorePerk=true：10-06 拆包——原版 AddDirectSellingItemToTable 内部声望销毁链（LLDISTRUSTED/BM_DISTRUSTED+faction+IsContraband→DestroyIfUnplaced），声名狼藉玩家黑市货被销毁；ignorePerk 疑似跳过该检查（语义拆包未定位，先按拆包AI方案A实测）
                    if (bit != null)
                    {
                        try { bit.EnableTag("contraband"); bit.EnableTag("CONTRABAND"); bit.EnableTag("CONTRABAND_ITEM_TAG"); } catch { } // 10-07 检查同类谬误：原版权威违禁 tag=CONTRABAND_ITEM_TAG（拆包 IsContraband 查它，cheatsheet:8076）——仅写 contraband/CONTRABAND 不被原版识别=玩家买走无违禁风险
                        // 10-06 B1：黑市货按黑市声望加价（声望 < -40 开始计数，越低加价越多）
                        //   pct = min(100%, (-40 - 声望)/100)——-40→+0%，-99→+59%；黑市讨厌你 → 卖你高价
                        try
                        {
                            var bmRep = StoreReputation.GetStoreReputation("FACTION_BLACK_MARKET");
                            if (bmRep != null)
                            {
                                int bmVal = (int)bmRep.GetReputationExact();
                                if (bmVal < -40)
                                {
                                    float pct = Math.Min(1f, (-40 - bmVal) / 100f);
                                    bit.unitValue = (long)(bit.unitValue * (1f + pct));
                                    // 10-06 B1（525BFCC1 拍板）：加价数值可见——写 tag → GetDisplayName Postfix 追加"（黑市加价 +X%）"
                                    try { ContainerUpgradeV2.SetTagIntValue(bit, "BLACK_MARKET_MARKUP_PCT", (int)(pct * 100)); } catch { }
                                    // 10-07 #9（玩家反馈"无原因显示"）：加价 feature 词条 → 原版词条区显示（GetDisplayName 名称行+词条区双保险）
                                    AddBlackMarketMarkupFeature(bit, (int)(pct * 100));
                                    Core.LogMsg("[招贼体质] 黑市货加价 " + bid + " 黑市声望" + bmVal + " +" + (int)(pct * 100) + "%");
                                }
                            }
                        }
                        catch { }
                        bm++;
                    }
                }
            }
            if (bm > 0) Core.LogMsg("[招贼体质] 黑市货散件上桌 " + bm + " 件（小偷对话）");

            // ③ 百分百带一个箱子（10-06 用户拍板）：原版小偷箱子概率性 → 对话上桌时检查柜台，
            //    没有 crate 就补一个原版随机物资箱（PreBuiltItemHelper.LootCrateEvidence=原版工厂，非自建）
            try
            {
                if (!HasCrateOnCounter())
                {
                    GameItem crate = CreateOriginalLootCrate();
                    if (crate != null)
                    {
                        Il2Cpp.PlayerStore.Instance.AddDirectSellingItemToTable(crate, false, false, false, 0);
                        Core.LogMsg("[招贼体质] 补原版随机物资箱 evidence_box（百分百有箱）");
                    }
                }
            }
            catch (System.Exception ex) { Core.LogMsg("[招贼体质] 补箱失败: " + ex.Message); }
        }
        catch (System.Exception ex) { Core.LogMsg("[招贼体质] 小偷对话上桌失败: " + ex.Message); }
    }

    // 10-06 拆包落地（memos/HGEhRccFpfXTq4NbLyWvXf）：半价卖回显示标注——valueModifier=0 纯显示不改价
    // （价格已由 unitValue/2 生效——NegociationUIManager 7 处价格全走 GetNegociatedValue=unitValue 基础链）；
    // feature 词条挂物品 → tooltip/交易列表显示"半价卖回"（玩家"没有半价卖的标签"修复）
    private static void AddHalfPriceFeature(GameItem item)    {
        try
        {
            if (item == null || item.itemFeatures == null) return;
            bool flag = false;
            for (int i = 0; i < item.itemFeatures.Count; i++)
            {
                if (item.itemFeatures[i] != null && item.itemFeatures[i].identifier == "stolen_half_price") { flag = true; break; }
            }
            if (flag) return;
            ItemFeature f = new ItemFeature();
            f.identifier = "stolen_half_price";
            f.featureType = ItemFeature.FeatureType.TemporaryBuying;
            f.valueStage = ItemFeature.ValueStage.Market;
            f.valueModifier = 0;
            f.preExposeValueModifier = 0;
            f.usePreExposeValue = false;
            f.initiallyShown = true;
            f.isFeatureMatch = true;
            f.isFeatureExposed = true;
            f.isExposable = true;
            f.isFeatureDiscovered = false;
            f.publicDisplay = LangHelper.T("半价卖回", "Half Price Resale");
            f.actualDisplay = LangHelper.T("半价卖回", "Half Price Resale");
            item.itemFeatures.Add(f);
        }
        catch (System.Exception ex) { Core.LogMsg("[招贼体质] 半价标注失败: " + ex.Message); }
    }

    // 10-07 #9（玩家反馈"黑市加价无原因显示"）：加价 feature 词条挂物品 → 原版词条区显示"黑市加价 +X%"
    //   （与半价卖回 AddHalfPriceFeature 同模式；GetDisplayName 名称行追加=双保险——玩家 hover 词条区/名称行必见）
    private static void AddBlackMarketMarkupFeature(GameItem item, int pct)
    {
        try
        {
            if (item == null || item.itemFeatures == null) return;
            for (int i = 0; i < item.itemFeatures.Count; i++)
                if (item.itemFeatures[i] != null && item.itemFeatures[i].identifier == "black_market_markup") return; // 防重复
            ItemFeature f = new ItemFeature();
            f.identifier = "black_market_markup";
            f.featureType = ItemFeature.FeatureType.TemporaryBuying;
            f.valueStage = ItemFeature.ValueStage.Market;
            f.valueModifier = 0; // 纯显示不改价（价格已在 unitValue 乘过——防双算）
            f.preExposeValueModifier = 0;
            f.usePreExposeValue = false;
            f.initiallyShown = true;
            f.isFeatureMatch = true;
            f.isFeatureExposed = true;
            f.isExposable = true;
            f.isFeatureDiscovered = false;
            f.publicDisplay = LangHelper.T("黑市加价 +" + pct + "%", "Black Market markup +" + pct + "%");
            f.actualDisplay = LangHelper.T("黑市加价 +" + pct + "%", "Black Market markup +" + pct + "%");
            item.itemFeatures.Add(f);
        }
        catch (System.Exception ex) { Core.LogMsg("[招贼体质] 黑市加价标注失败: " + ex.Message); }
    }

    // 10-06 修复单#1（买回后状态清除→原价可卖）：恢复 unitValue ×2（上桌时 /2 过）+ 移除"半价卖回"feature
    private static void RestoreFullPrice(GameItem item)
    {
        try
        {
            if (item == null) return;
            try { item.unitValue = item.unitValue * 2; } catch { }
            if (item.itemFeatures != null)
            {
                for (int i = item.itemFeatures.Count - 1; i >= 0; i--)
                {
                    try { if (item.itemFeatures[i] != null && item.itemFeatures[i].identifier == "stolen_half_price") item.itemFeatures.RemoveAt(i); } catch { }
                }
            }
            Core.LogMsg("[招贼体质] 买回恢复原价+清半价feature");
        }
        catch (System.Exception ex) { Core.LogMsg("[招贼体质] 恢复原价失败: " + ex.Message); }
    }

    // ============ 读档重挂（LoadGame Postfix） ============
    // eventSourceId/_pendingResale 是运行时标记不随档——读档后小偷客户重建丢标记→不上桌（用户反馈"重进变情报贩子无货"）
    // 10-06 二修：读档当天若是小偷日且被偷物未卖（KEY_STOLEN 非空）→ 重新生成小偷上门（读档=重放当天：
    //   原版客户队列不存 AddClient 运行时客户，读档后队列丢；KEY_CHANNEL==day 会让 OnBeginDay 的
    //   IsChannelFree 拦掉重新生成——这里绕过通道判定直接 SpawnThiefVisit）
    internal static void PostfixLoadGameThiefMagnet()
    {
        try
        {
            int day = GetDay();
            // 10-07 测试反馈 D（玩家"当天被偷→读档→小偷消失"）：KEY_CHANNEL 只在 SpawnThiefVisit（小偷上门）时写——
            //   若读档当天=丢物日（KEY_THEFT_DAY，小偷应上门但还没上）→ KEY_CHANNEL!=day → 重挂漏判。
            //   放宽：丢物日或上门日都重挂（KEY_STOLEN 非空为前提）。
            if (WageSaveStore.GetInt(NS, KEY_CHANNEL, -1) != day && WageSaveStore.GetInt(NS, KEY_THEFT_DAY, -1) != day) return; // 今天不是小偷日/丢物日
            bool hasStolen = !string.IsNullOrEmpty(WageSaveStore.GetString(NS, KEY_STOLEN, ""));
            if (!hasStolen) return; // 已卖过/没偷
            SpawnThiefVisit(day); // 绕过 IsChannelFree 直接重新生成（当天该来的小偷在重进后重新上门）
            Core.LogMsg("[招贼体质] 读档重挂：小偷重新上门 day=" + day);
        }
        catch (System.Exception ex) { Core.LogMsg("[招贼体质] 读档重挂失败: " + ex.Message); }
    }

    // 柜台（frontInv）是否有 crate（原版物资箱/证据箱系列）
    private static bool HasCrateOnCounter()
    {
        try
        {
            EmporiumEntry em = EmporiumEntry.Instance;
            if (em == null) return false;
            GameInventory front = em.frontInvinvElement as GameInventory;
            if (front == null || front.childItems == null) return false;
            for (int i = 0; i < front.childItems.Count; i++)
            {
                GameItem it = front.childItems[i];
                if (it == null) continue;
                string id = "";
                try { id = it.identifier ?? ""; } catch { }
                if (IsCrateId(id)) return true;
            }
            return false;
        }
        catch { return false; }
    }

    private static bool IsCrateId(string id)
    {
        switch (id)
        {
            case "evidence_box": case "med_box": case "sec_box": case "service_box":
            case "eng_box": case "loot_crate": case "expedition_box": case "toolbox": case "trashcan":
                return true;
            default: return false;
        }
    }

    // 原版随机物资箱（PreBuiltItemHelper.LootCrateEvidence——拆包实锤 LootCrateEvidence :1023）
    private static GameItem CreateOriginalLootCrate()
    {
        try
        {
            var t = typeof(Il2Cpp.PreBuiltItemHelper);
            var mi = t.GetMethod("LootCrateEvidence", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (mi == null) return null;
            var crate = mi.Invoke(null, null) as GameItem;
            if (crate != null) { try { Il2Cpp.LockHelper.LockUpContainer(crate); } catch { } }
            return crate;
        }
        catch (System.Exception ex) { Core.LogMsg("[招贼体质] 原版箱创建失败: " + ex.Message); return null; }
    }

    // 黑市货 id（10-06 改由 AttachThiefSellOffer 在交易界面上桌卖——不再放后库）
    private static readonly string[] BlackMarketGoodsIds = new string[]
    {
        "oxycodone_pill", "dream_dust", "injector", "dream_cap_extract", "cig_red"
    };

    // ============ 声名狼藉黑市特判（10-06 三任务修复单①，主控拍板：保持敌视但特判） ============
    // 拆包实锤（memos/HGEhRccFpfXTq4NbLyWvXf）：AddDirectSellingItemToTable 内部声望销毁链
    //   forcedHeat==0 → LLDISTRUSTED 链 / ≠0 → BM_DISTRUSTED 链：IsPerkUnlocked + 客户faction + IsContraband → DestroyIfUnplaced
    //   声名狼藉玩家 + 小偷（黑市faction）卖黑市货/物证箱 → 命中销毁 = 货没了（玩家反馈"没有百分百带箱子/黑市货没来卖"）
    // 特判：命中"小偷场景 + 黑市货/物证箱"→ 手动放前台绕过销毁链（交易结算走正常链=玩家可买）
    private static bool _thiefBlackGoodsBypass = false;
    public static bool PrefixAddDirectSellingItemToTable(GameItem gameItem, bool isOwend, bool isStolen, bool ignorePerk, int forcedHeat)
    {
        try
        {
            if (gameItem == null || isOwend) return true; // 玩家自己的货放行原版
            if (_thiefBlackGoodsBypass) return true; // 防递归（回退分支再入）
            if (!(Core.PerkActive("声名狼藉") || Core.PerkActive("人神共愤"))) return true;
            // 当前客户=小偷（原版 CreateThief 的 identifier，与 PostfixStartMainDialogue 双键判定一致）
            bool isThief = false;
            try
            {
                var ps = PlayerStore.Instance;
                if (ps != null && ps.currentClientInstance != null)
                {
                    // currentClientInstance 是 StoreClientInstance——GetClientBlueprint() 拿 StoreClient（Patches.Npc.Client.cs:242 同款）
                    var bp = ps.currentClientInstance.GetClientBlueprint();
                    if (bp != null)
                    {
                        string cid = ""; try { cid = bp.identifier ?? ""; } catch { }
                        // 10-07 #7（玩家反馈"突破声望限违禁品直购仍不行"）：Prefix 原单键（identifier=="thief"）与 Postfix 双键不一致——
                        //   eventSourceId=="thief_magnet_visit"（本会话事件触发）|| identifier=="thief"（原版存档字段）——事件触发/读档场景 identifier 可能非 "thief" → 特判漏判 → 黑市货走原版销毁链
                        string esrc = ""; try { esrc = bp.eventSourceId ?? ""; } catch { }
                        if (esrc == "thief_magnet_visit" || cid == "thief") isThief = true;
                    }
                }
            }
            catch { }
            if (!isThief) return true;
            // 物品=黑市货 / 物证箱 / 违禁标记
            string gid = ""; try { gid = gameItem.identifier ?? ""; } catch { }
            bool isBlack = false;
            foreach (var b in BlackMarketGoodsIds) { if (b == gid) { isBlack = true; break; } }
            if (!isBlack && gid == "evidence_box") isBlack = true;
            if (!isBlack) { try { isBlack = gameItem.IsTag("contraband") || gameItem.IsTag("CONTRABAND") || gameItem.IsTag("CONTRABAND_ITEM_TAG"); } catch { } } // 10-07：补原版权威 tag 判定
            if (!isBlack) return true;
            // 绕过销毁链：手动放前台（客户卖品区——玩家可买）
            _thiefBlackGoodsBypass = true;
            try
            {
                Core.LogMsg("[招贼体质] 声名狼藉特判：黑市货/箱子绕过销毁链上桌 " + gid);
                var em = Il2Cpp.EmporiumEntry.Instance;
                if (em != null && em.frontInvinvElement != null)
                {
                    // 10-07 玩家反馈"黑市货直接已拥有可双击"：手动 UncheckedAcceptAll 绕过原版 AddDirectSellingItemToTable
                    //   的归属标记设置（原版 isOwend=false 会给物品挂 not_purchased=客户卖品需购买）——这里手动补标记，
                    //   否则前台物品被当玩家所有可自由拿走/开箱。
                    try { gameItem.EnableTag("not_purchased", true); try { gameItem.EnableTag("TAG_NOT_PURCHASED", true); } catch { } } catch { } // 10-07 同类谬误：读端 6 处双查（not_purchased/TAG_NOT_PURCHASED 历史不确定）——写端双写全覆盖，防单写不被识别
                    try { gameItem.DisableTag("IS_OWNED_TAG", true); } catch { }
                    var l = new Il2CppSystem.Collections.Generic.List<GameItem>();
                    l.Add(gameItem);
                    em.frontInvinvElement.UncheckedAcceptAll(l);
                }
                else
                {
                    // 前台不可用回退：ignorePerk=true 再走原版（拆包方案A——二次进入被 _thiefBlackGoodsBypass 放行）
                    PlayerStore.Instance.AddDirectSellingItemToTable(gameItem, isOwend, isStolen, true, forcedHeat);
                }
            }
            finally { _thiefBlackGoodsBypass = false; }
            return false; // 拦原版（销毁链不执行）
        }
        catch (System.Exception ex) { Core.LogMsg("[招贼体质] 黑市特判异常: " + ex.Message); }
        return true;
    }

    // ============ 蛙哥服务卡赎回被偷物（10-06 用户拍板） ============
    // 被偷物都会找到，只是暂时换主人：小偷半价卖（买不起）→ 蛙哥服务卡花信用点赎回（半价）
    // 未赎回完（KEY_STOLEN 非空）之前，招贼体质特性无法消除（WageBrother 消perk界面锁定）
    internal static string StolenListSummary()
    {
        try
        {
            string stored = WageSaveStore.GetString(NS, KEY_STOLEN, "");
            if (string.IsNullOrEmpty(stored)) return "";
            var ids = stored.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            return ids.Length + LangHelper.T(" 件待赎回", " items to redeem");
        }
        catch { return ""; }
    }

    internal static int StolenCount()
    {
        try
        {
            string stored = WageSaveStore.GetString(NS, KEY_STOLEN, "");
            if (string.IsNullOrEmpty(stored)) return 0;
            return stored.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Length;
        }
        catch { return 0; }
    }

    // 10-06 A6：赎回明细（列举所有未赎回物品 + 各自半价）——蛙哥服务卡赎回区逐行显示
    //   按 KEY_STOLEN 的 id 分组（同 id 多件合并 ×N），名字经 WageItemFactory 重建拿（与读档兜底同源，防空档/未知 id 显示原始 id）
    internal static List<string> StolenItemDetails()
    {
        var rows = new List<string>();
        try
        {
            string stored = WageSaveStore.GetString(NS, KEY_STOLEN, "");
            if (string.IsNullOrEmpty(stored)) return rows;
            var ids = stored.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            var seen = new Dictionary<string, int>();
            foreach (var id in ids) seen[id] = seen.ContainsKey(id) ? seen[id] + 1 : 1;
            foreach (var kv in seen)
            {
                string nm = kv.Key;
                long baseVal = 1;
                GameItem probe = null;
                try { probe = WageAPI.WageItemFactory.Create(kv.Key, false); } catch { }
                if (probe != null)
                {
                    try { nm = ModCannibalism.GetName(probe); } catch { }
                    try { baseVal = probe.unitValue; } catch { }
                    try { probe.Destroy(); } catch { }
                }
                rows.Add(nm + " ×" + kv.Value + LangHelper.T("（半价 " + (baseVal / 2) * kv.Value + "）", " (half " + (baseVal / 2) * kv.Value + ")"));
            }
        }
        catch (System.Exception ex) { Core.LogMsg("[招贼体质] StolenItemDetails异常: " + ex.Message); }
        return rows;
    }

    // 赎回：按 stolen_ids 重建物品（半价）塞玩家后库，扣款后清 KEY_STOLEN
    internal static bool RedeemStolenItems()
    {
        try
        {
            var ps = Il2Cpp.PlayerStore.Instance;
            if (ps == null) return false;
            string stored = WageSaveStore.GetString(NS, KEY_STOLEN, "");
            if (string.IsNullOrEmpty(stored)) { StoreUIManager.Instance.Notify(LangHelper.T("没有待赎回的被偷物品", "Nothing to redeem")); return true; }

            var ids = stored.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            var items = new List<GameItem>();
            long totalHalf = 0;
            foreach (var id in ids)
            {
                var it = WageAPI.WageItemFactory.Create(id, true);
                if (it == null) continue;
                long baseVal = 0;
                try { baseVal = it.unitValue; } catch { }
                if (baseVal <= 0) baseVal = 1; // 防 0 值免费赎
                totalHalf += baseVal / 2;
                // 10-06 修复单#1：赎回物品保持原价（扣款已半价——找回被偷物=花半价拿回原价物品，可原价再卖）
                items.Add(it);
            }
            if (items.Count == 0) { StoreUIManager.Instance.Notify(LangHelper.T("被偷物品重建失败", "Failed to rebuild stolen items")); return false; }

            if (ps.playerCash < totalHalf) { StoreUIManager.Instance.Notify(LangHelper.T("钱不够赎回（需 " + totalHalf + "）", "Not enough credits (" + totalHalf + ")")); return false; }
            ps.playerCash -= (int)totalHalf;

            int ok = 0;
            foreach (var it in items)
            {
                try { if (WageAPI.WageItemGrant.GrantToPlayerBackInv(it, true)) ok++; } catch { }
            }
            WageSaveStore.SetString(NS, KEY_STOLEN, "");
            // 10-07 A8：赎回清空后即时落盘——防进程退出前未 Flush → 重进 KEY_STOLEN 残留 → 被偷物重复重建/重复赎回
            try { WageSaveStore.Flush(); } catch { }
            Core.LogMsg("[招贼体质] 蛙哥服务卡赎回 " + ok + " 件，扣 " + totalHalf);
            StoreUIManager.Instance.Notify(LangHelper.T("赎回 " + ok + " 件，扣 " + totalHalf + " 信用点。被偷的东西都找到了。", "Redeemed " + ok + " items for " + totalHalf + " cr."));
            return true;
        }
        catch (System.Exception ex) { Core.LogMsg("[招贼体质] 赎回失败: " + ex.Message); return false; }
    }

    // ============ 被偷物买回精确清账（OnItemBought Postfix——拆包5F6k3jY7Kz628HwFRxWAWR + JXXoUguSncb6m6JoPxCHGb） ============
    // OnDealAccepted 是死路（hostLine 常传 null，内部无被买物品）——OnItemBought(GameItem, int cost)
    //   = 三条购买路径（客户卖品/柜台/谈判UI）统一汇点，参数直接=被买物品
    // 清账优先走会话内映射（实例精确——同 id 另一件不误清）；未命中 fallback 按 id
    internal static void PostfixPlayerStoreOnItemBought(GameItem __0)
    {
        try
        {
            if (__0 == null) return;
            string id = "";
            try { id = __0.identifier ?? ""; } catch { }
            if (string.IsNullOrEmpty(id)) return;
            string stored = WageSaveStore.GetString(NS, KEY_STOLEN, "");
            if (string.IsNullOrEmpty(stored)) return;

            // ① 会话内映射优先（精确实例）
            if (_stolenMap.TryGetValue(__0, out string mapId) && !string.IsNullOrEmpty(mapId))
            {
                _stolenMap.Remove(__0); // 移除映射（买走=清账完成）
                var ids = new List<string>(stored.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
                if (ids.Remove(mapId))
                {
                    WageSaveStore.SetString(NS, KEY_STOLEN, string.Join(",", ids));
                    RestoreFullPrice(__0); // 10-06 修复单#1：买回后恢复原价（unitValue ×2）+ 移除"半价卖回"feature——原价可卖
                    Core.LogMsg("[招贼体质] 被偷物买回 " + mapId + "（实例精确清账，剩 " + ids.Count + " 件待赎回）");
                    return;
                }
            }

            // ② fallback 按 id（映射未命中——读档重建/对象销毁场景）
            var ids2 = new List<string>(stored.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
            if (!ids2.Contains(id)) return;
            ids2.Remove(id); // 按件移除（首个匹配）
            WageSaveStore.SetString(NS, KEY_STOLEN, string.Join(",", ids2));
            RestoreFullPrice(__0); // 10-06 修复单#1：买回后恢复原价 + 移除半价 feature
            Core.LogMsg("[招贼体质] 被偷物买回 " + id + "（按id兜底清账，剩 " + ids2.Count + " 件待赎回）");
        }
        catch (System.Exception ex) { Core.LogMsg("[招贼体质] 清账失败: " + ex.Message); }
    }

    // ============ 工具方法 ============
    private static bool IsChannelFree(int day)
    {
        return WageSaveStore.GetInt(NS, KEY_CHANNEL, -1) != day;
    }

    private static List<GameInventory> CollectInventories(EmporiumEntry em)
    {
        // 已实锤 4 库存（ContainerUpgradeV2.Upgrade.cs:38-39 / DarkGridInspectorPerk.cs:97-98 同源）
        var list = new List<GameInventory>();
        try { list.Add(em.invElement as GameInventory); } catch { }
        try { list.Add(em.frontInvinvElement as GameInventory); } catch { }
        try { list.Add(em.backInvinvElement as GameInventory); } catch { }
        try { list.Add(em.backInvinvElementCounter as GameInventory); } catch { }
        return list;
    }

    private static void DeductCash(EmporiumEntry em, long amount)
    {
        try
        {
            var store = PlayerStore.Instance;
            if (store == null) return;
            System.Reflection.PropertyInfo property = typeof(PlayerStore).GetProperty("playerCash", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null)
            {
                int cur = (int)property.GetValue(store);
                property.SetValue(store, Math.Max(0, cur - (int)amount));
                return;
            }
            System.Reflection.FieldInfo field = typeof(PlayerStore).GetField("playerCash", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null)
            {
                int cur = (int)field.GetValue(store);
                field.SetValue(store, Math.Max(0, cur - (int)amount));
            }
        }
        catch (System.Exception ex) { Core.LogMsg("[招贼体质] 扣款失败: " + ex.Message); }
    }

    private static StoreClientManager GetStoreClientManager()
    {
        try
        {
            PlayerStore store = PlayerStore.Instance;
            if (store == null) return null;
            return store.storeClientManager;
        }
        catch { return null; }
    }

    private static void SetDialogue(StoreClient client, string text)
    {
        try
        {
            string speaker = (client.displayName ?? "").Trim();
            if (client.mainDialogue != null) client.mainDialogue.SetText(speaker, text);
            else { var d = new Dialogue(); d.SetText(speaker, text); client.mainDialogue = d; }
        }
        catch (System.Exception ex) { Core.LogMsg("[招贼体质] 对话设置失败: " + ex.Message); }
    }

    private static int GetDay() { try { return StoreStation.GetDayCounter(); } catch { return 1; } }
    private static string SafeId(GameItem it) { try { return it.identifier ?? "?"; } catch { return "?"; } }
}
