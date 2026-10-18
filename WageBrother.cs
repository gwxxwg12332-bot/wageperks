using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MelonLoader;
using UnityEngine;

namespace WagePerks;

// ============================================================
// v1.3.1【补11】蛙哥周期到访：每 N 天来一次，服务卡消负面perk
//   塞队：ScheduleWageBrotherToday() 照博士 QueueFuturClient("wage_brother",1)
//   交互：蛙哥到场柜台生成服务卡 wage_brother_card → 双击弹消perk界面
//   费用：Cost -2/-3→500，-7/-10→2000，-15→5000，-20→8000
// ============================================================
internal static class WageBrother
{
    internal const string CLIENT_ID = "wage_brother";
    internal const string CARD_ID = "wage_brother_card";
    private static bool _cardSpawned = false;
    private static bool _orderUsedThisVisit = false; // 10-16 P1-5.2：点货签本次到店已生效标记（CleanupCardIfGone 复位）
    // 10-07 C2-1/C2-2：名片/柜台货"该在"的状态随档键（对标小偷 KEY_THEFT_DAY 成功方案——
    //   柜台不随档+原一次性 180 帧补货超时=读档后柜台永远空；此键=持久愿望：上货成功置 1、清卡置 0，读档后按愿望重建柜台）
    private const string CARD_SPAWNED_KEY = "card_spawned";
    // 10-15 名片只发一次：card_given=1=已送过名片（首次见面送，解锁电话簿 1005），之后蛙哥到店不再发卡
    private const string CARD_GIVEN_KEY = "card_given";
    // 10-15 保护器随机出现概率加成（服务每级+10%，初始 30%，上限 90%）
    internal const string PROTECTOR_SPAWN_KEY = "protector_spawn_level";
    // ===== 10-16 P1 升级：5.1 收货标签 / 5.2 点货签 =====
    internal const string ORDER_TICKET_ID = "wage_order_ticket";
    internal const string ORDER_TICKET_TAG = "wage_order_tag";        // 点货签类型标记
    internal const string ORDER_TAG_PREFIX = "wage_order_";           // 品类编码前缀（tag 名=wage_order_<品类tag>）
    private const string ORDER_CD_KEY = "wage_order_cd";              // 全局 CD（上次生效日；WageSaveStore "wage_brother" 空间）
    private const string ORDER_USED_KEY = "wage_order_used_session";  // 本会话本次到店已生效标记（防同次多张）

    // 10-03 服务卡注册进物品库（PS_DebugTool/玩家生成工具调出=未注册ID→问号占位；照抄养蛊机RegisterOne模式）
    public static void RegisterCard(ItemDirectory dir)
    {
        try
        {
            if (dir == null) return;
            if (((Directory<GameItem>)(object)dir).Has(CARD_ID)) { Core.LogMsg("[蛙哥] 服务卡已在物品库"); return; }
            Il2CppSystem.Func<GameItem> factory = null;
            System.Func<GameItem> sf = () => CreateCard();
            factory = DelegateSupport.ConvertDelegate<Il2CppSystem.Func<GameItem>>((System.Delegate)sf);
            bool ok = ((Directory<GameItem>)(object)dir).Add(CARD_ID, factory);
            Core.LogMsg("[蛙哥] 服务卡 " + (ok ? "★ 已注册" : "⚠️ 注册失败") + " " + CARD_ID);
        }
        catch (Exception ex) { Core.LogMsg("[蛙哥] 服务卡注册异常: " + ex.Message); }
    }

    // 10-16 P1-5.2：点货签注册进物品库（品类 tag 在携带/购买时按实例 EnableTag("wage_order_<品类>") 编码）
    public static void RegisterOrderTicket(ItemDirectory dir)
    {
        try
        {
            if (dir == null) return;
            if (((Directory<GameItem>)(object)dir).Has(ORDER_TICKET_ID)) { Core.LogMsg("[蛙哥] 点货签已在物品库"); return; }
            Il2CppSystem.Func<GameItem> factory = null;
            System.Func<GameItem> sf = () => CreateOrderTicket();
            factory = DelegateSupport.ConvertDelegate<Il2CppSystem.Func<GameItem>>((System.Delegate)sf);
            bool ok = ((Directory<GameItem>)(object)dir).Add(ORDER_TICKET_ID, factory);
            Core.LogMsg("[蛙哥] 点货签 " + (ok ? "★ 已注册" : "⚠️ 注册失败") + " " + ORDER_TICKET_ID);
        }
        catch (Exception ex) { Core.LogMsg("[蛙哥] 点货签注册异常: " + ex.Message); }
    }

    // 点货签工厂：基础物品（形状/图标/类型标记）；品类由调用方 SetName + EnableTag("wage_order_<品类>") 编码
    internal static GameItem CreateOrderTicket()
    {
        try
        {
            GameItem t = ItemDirectory.CreateEmptyItem(null);
            if (t == null) return null;
            t.identifier = ORDER_TICKET_ID;
            t.SetName(LangHelper.T("点货签", "Order Ticket"));
            t.shortDescription = LangHelper.T("点单凭证：蛙哥到店时把绑定的品类临时加入他的收购清单（本次到店生效，3 天冷却）。", "Order ticket: adds the bound category to Wage Brother's buying list for this visit (3-day cooldown).");
            var gsb = new GridShapeBuilder(); gsb.SetDataFill(1, 1); t.SetShape(gsb.Build());
            try { t.EnableTag(ORDER_TICKET_TAG, true); } catch { }
            try { t.EnableTag("paper", true); } catch { } // 文档属性（销赃不带）
            return t;
        }
        catch (Exception ex) { Core.LogMsg("[蛙哥] CreateOrderTicket异常: " + ex.Message); return null; }
    }
    // 服务卡工厂：只建物品（不含上柜台；上柜台走 OnClientArrived）
    internal static GameItem CreateCard()
    {
        try
        {
            LoadCardSprite();
            GameItem card = ItemDirectory.CreateEmptyItem(null); // 无参构造不存在，照养蛊机CreateGuMachine:79
            card.identifier = CARD_ID;
            card.SetName(LangHelper.T("蛙哥的名片", "Wage Brother's Card"));
            // 10-16 名片文案重设计 v2 drop-in：蛙哥=活着的传奇/考官（非失踪谜题），服务清单+遗产+周期到店
            card.shortDescription = LangHelper.T(
                "蛙哥的名片。\n" +
                "空间站传说里的\"传奇当铺主\"——他把一整间当铺留给了你：妙妙箱、炼蛊的机器、不稳定的 AI、保护器，还有蛙娘。\n" +
                "他平时靠电话联络，每 7 天亲自到店一趟，带着高得离谱的预算，既收货，也带货。他能替你\"去邪气\"，也能拨动\"吞噬季\"的开关，甚至让你回到第一天。\n" +
                "拨 1005 联系他——全部服务已存入电话簿，随时叫货。",
                "Wage Brother's business card.\n" +
                "The \"legendary pawnshop owner\" of station legend — he left you an entire shop: the Wonder Box, the forging machine, an unstable AI, a protector core, and Wage Girl.\n" +
                "He reaches you by phone, and every 7 days he visits in person, carrying an absurdly large budget, buying and selling alike. He can \"purify\" you, flip the switch on the Cannibalism Season, and even send you back to Day 1.\n" +
                "Dial 1005 to reach him — all his services are saved to your phonebook, call anytime.");
            var gsb = new GridShapeBuilder(); gsb.SetDataFill(2, 1); card.SetShape(gsb.Build());
            card.SetSprite("custom_atlas", CARD_SPRITE_KEY);
            card.unitValue = 0; card.unitBaseValue = 0; // 名片不能卖
            try { card.EnableTag("paper", true); } catch { } // 文档属性标签（销赃时不带走）
            try { card.EnableTag("wage_bro_card", true); } catch { }
            return card;
        }
        catch (Exception ex) { Core.LogMsg("[蛙哥] CreateCard异常: " + ex.Message); return null; }
    }

    // 周期塞队（照博士 ScheduleJacksonToday）
    internal static bool ScheduleToday()
    {
        try
        {
            var ps = PlayerStore.Instance;
            if (ps == null) return false;
            // 10-01: 删门控，蛙哥改为通用事件（不再要求点蛙娘 perk）
            int day = StoreStation.GetDayCounter();
            bool queued = HasQueued();
            if (queued) { Core.LogMsg("[蛙哥] 诊断: day=" + day + " HasQueued=true → 跳过（残留队列/在场）"); return false; }
            int interval = BuildConfig.WageBrotherVisitInterval > 0 ? BuildConfig.WageBrotherVisitInterval : 10;
            int lastSched = WageSaveStore.GetInt("wage_brother", "last_scheduled_day", -1);
            if (lastSched >= 0 && day - lastSched < interval) { Core.LogMsg("[蛙哥] 诊断: day=" + day + " lastSched=" + lastSched + " 间隔不足(" + (day - lastSched) + "<" + interval + ") → 跳过"); return false; }
            Core.LogMsg("[蛙哥] 门控已删，day=" + day + " lastSched=" + lastSched + " 间隔=" + (day - lastSched) + " 尝试塞队 HasQueued=" + queued);
            // 10-05 时序修复：先排队成功再记 lastSched（原代码先记后排——若排队失败 lastSched 已更新 → 白等 7 天；用户"第3天来后第11天不来"候选根因）
            try { ps.QueueFuturClient(CLIENT_ID, 1); }
            catch (Exception ex) { Core.LogMsg("[蛙哥] 排队失败(不记 lastSched，明天重试): " + ex.Message); return false; }
            WageSaveStore.SetInt("wage_brother", "last_scheduled_day", day);
            Core.LogMsg("[蛙哥] 已排队，明天到访");
            return true;
        }
        catch (Exception ex) { Core.LogMsg("[蛙哥] ScheduleToday异常: " + ex.Message); return false; }
    }

    private static bool HasQueued()
    {
        try
        {
            var ps = PlayerStore.Instance; if (ps == null) return false;
            var q = ps.futurStoreClientIdQueue;
            if (q != null) for (int i = 0; i < q.Count; i++) if (q[i] == CLIENT_ID) return true;
            var mgr = ps.storeClientManager;
            if (mgr != null && mgr.clientStack != null) for (int j = 0; j < mgr.clientStack.Count; j++)
            { var c = mgr.clientStack[j]; if (c != null && c.identifier == CLIENT_ID) return true; }
        }
        catch { }
        return false;
    }

    // 10-06 F2（玩家反馈"退出重进蛙哥刷没了"）：读档补排——小退（不存档）丢 futurStoreClientIdQueue →
    // 重进 HasQueued=false，而 last_scheduled_day 间隔未到 → ScheduleToday 判定跳过 → 蛙哥要再等一整个 interval。
    // 读档时：lastSched 在 interval 内但队列空 → 补排（明天到）。
    public static void PostfixLoadGame()
    {
        try
        {
            var ps = PlayerStore.Instance;
            if (ps == null) return;
            int day = 0;
            try { day = StoreStation.GetDayCounter(); } catch { }
            int lastSched = WageSaveStore.GetInt("wage_brother", "last_scheduled_day", -1);
            // 10-07 C2-1/C2-2：名片/柜台货该在=存档状态优先（对标小偷 KEY_THEFT_DAY——柜台不随档，
            //   读档后无条件重建柜台货，不依赖 lastSched 推算；推算只决定"补排明天到"）
            int cardSpawned = WageSaveStore.GetInt("wage_brother", CARD_SPAWNED_KEY, 0);
            if (cardSpawned == 1)
            {
                _refillPending = true;
                Core.LogMsg("[蛙哥] 读档恢复：card_spawned=1（存档时柜台有货）→ 补货愿望已设（无条件，不依赖推算）");
            }
            if (lastSched < 0) return;
            int interval = BuildConfig.WageBrotherVisitInterval > 0 ? BuildConfig.WageBrotherVisitInterval : 10;
            if (day - lastSched >= interval) return; // 间隔已过=不在本周期（等下次正常调度）
            // 10-07 测试反馈 E（玩家"蛙哥到店有货→读档→柜台货没了"）：柜台补货标志无条件设——
            //   原版客户队列读档恢复（HasQueued=true）≠柜台有货（柜台不随档=空）；原代码 HasQueued return 挡住了补货。
            //   补货独立于队列补排：OnUpdateRefillCounter 内判蛙哥在店/柜台就绪/防重复才补（安全）。
            _refillPending = true;
            if (HasQueued()) { Core.LogMsg("[蛙哥] 读档恢复：队列已有（原版恢复）→ 补柜台货标志已设"); return; }
            // 小退丢队列：lastSched 在 interval 内但队列空 → 补排明天到
            Core.LogMsg("[蛙哥] 读档恢复：队列空但 lastSched=" + lastSched + " day=" + day + " 间隔未到 → 补排（明天到访）+补柜台货");
            try { ps.QueueFuturClient(CLIENT_ID, 1); }
            catch (Exception ex) { Core.LogMsg("[蛙哥] 读档补排失败: " + ex.Message); }
            // 10-07 F2-2（玩家反馈"蛙哥读档没有物品"）：读档时蛙哥已在店——对话已开过 → StartMainDialogue
            // 不重触发 HandleSpecialNpcArrived → OnClientArrived 不跑 → 柜台不随档=空（拆包嫌疑2已实锤）→ 主动补上货。
            // 10-07 #1（玩家反馈"读档补排后柜台仍空"）：LoadGame 时机柜台（EmporiumEntry/frontInvinvElement）未就绪 →
            //   OnClientArrived 里 AddDirectSellingItemToTable 静默失败 → 服务卡/携带物仍不上柜台。
            //   修复=改帧轮询：此处只设标志，FrameUpdate→OnUpdateRefillCounter 每帧重试（柜台就绪+蛙哥在店+柜台无卡才补，愿望驱动无超时）
            _refillPending = true;
        }
        catch (Exception ex) { Core.LogMsg("[蛙哥] PostfixLoadGame异常: " + ex.Message); }
    }

    // 10-07 #1（玩家反馈"读档补排后柜台仍空"）：读档补货改帧轮询——LoadGame 时机柜台（EmporiumEntry）未就绪
    //   → OnClientArrived 的 AddDirectSellingItemToTable 静默失败 → 柜台空。此处每帧重试直到柜台就绪+上货成功。
    // 10-07 C2-1/C2-2（玩家"蛙哥名片小退消失/柜台货读档失败"复现4次）：从"180帧一次性窗口"改为【愿望驱动】——
    //   card_spawned 随档=持久愿望（上货成功=1/清卡=0）；愿望在 + 柜台就绪 + 无卡 + 蛙哥本窗口该在 → 补货；
    //   补货成功才清标志；无永久超时（蛙哥晚到=次日/下周期到店→lastSched 更新→窗口成立→自然补上）。
    private static bool _refillPending = false;
    internal static void OnUpdateRefillCounter()
    {
        try
        {
            if (!_refillPending) return;
            // 愿望检查：card_spawned==1 才继续（读档设的愿望；清卡后=0 → 停止）
            if (WageSaveStore.GetInt("wage_brother", CARD_SPAWNED_KEY, 0) != 1)
            {
                _refillPending = false; // 愿望已清（蛙哥走了/清卡）→ 停止
                return;
            }
            var ps = PlayerStore.Instance; if (ps == null) return;
            // 二轮拆包落点（10-07）：判据从 clientStack 扫描（调度级在栈≠已进门）升级为 currentClient==蛙哥——
            //   实际交易客户（GetNextClient 设 currentClient → OnArrived 进门），非排队；非蛙哥客户开门时绝不提前上货
            StoreClient wc = null;
            try { var cur = SpecialNpcManager.GetCurrentClient(); if (cur != null && cur.identifier == CLIENT_ID) wc = cur; } catch { }
            // 柜台就绪？（AddDirectSellingItemToTable 依赖 EmporiumEntry 前台库存）
            var em = Il2Cpp.EmporiumEntry.Instance;
            if (em == null || em.frontInvinvElement == null) return; // 未就绪，下帧重试
            if (HasCardOnCounter()) { _refillPending = false; Core.LogMsg("[蛙哥] 补货轮询：柜台已有服务卡，完成"); return; } // 防重复加卡
            // 本窗口判定：只在蛙哥是当前交易客户才补货（二轮拆包实锤：clientStack=调度级入栈（HandleFutureClientQueue→AddClient，
            //   :3040-3045），在栈≠已进门；currentClient=GetNextClient 实际设定（→OnArrived :2371 进门）=实体级"蛙哥在场"。
            //   修复：物品跟随蛙哥出现而出现——非蛙哥客户开门时 wc==null → 绝不提前上货；
            //   蛙哥成为当前客户（读档 SetImmediatlyArrive/正常进门）→ wc 命中 → 本兜底补货，覆盖 C2-1/C2-2）。
            if (wc == null) return; // 蛙哥不是当前客户（未进门/别的客户在交易）→ 等，绝不提前上货
            Core.LogMsg("[蛙哥] 帧轮询补货：柜台就绪 + 蛙哥为当前客户 → OnClientArrived");
            OnClientArrived(wc);
            // 成功才清标志：上货失败（AddDirectSellingItemToTable 静默失败）→ 下帧继续重试（愿望还在）
            if (HasCardOnCounter()) { _refillPending = false; Core.LogMsg("[蛙哥] 补货完成，柜台有卡"); }
            else { Core.LogMsg("[蛙哥] 补货未确认（柜台无卡）→ 下帧重试"); }
        }
        catch (System.Exception ex) { Core.LogMsg("[蛙哥] OnUpdateRefillCounter异常: " + ex.Message); }
    }

    // 柜台是否已有服务卡（防重复加卡堆叠）
    private static bool HasCardOnCounter()
    {
        try
        {
            var em = Il2Cpp.EmporiumEntry.Instance; if (em == null) return false;
            var invs = new GameInventory[] { (GameInventory)em.frontInvinvElement, (GameInventory)em.showcaseElement };
            foreach (var inv in invs)
            {
                if (inv == null || inv.childItems == null) continue;
                for (int i = 0; i < inv.childItems.Count; i++)
                    if (inv.childItems[i] != null && inv.childItems[i].identifier == CARD_ID) return true;
            }
        }
        catch { }
        return false;
    }

    // 蛙哥到场 → 柜台生成服务卡
    internal static void OnClientArrived(StoreClient client)
    {
        try
        {
            // 10-07 P0-1/P0-2：读档补货可传 null（上货模式）——读档后拿不到蛙哥 client 实例但仍需补柜台货；
            //   client==null 时跳过 client 相关块（SetBudget/立绘/对话——各自 try/catch 已兜 NRE），只走上货链。
            if (client != null && client.identifier != CLIENT_ID) { if (Core.DebugMode) Core.LogMsg("[蛙哥] OnClientArrived: identifier=" + (client!=null?client.identifier:"null")+" 不是蛙哥,跳过"); return; } Core.LogMsg("[蛙哥] OnClientArrived 入口, client=" + (client!=null?client.identifier:"null"));
            // 10-16 环⑧：蛙哥每 7 天到店=一次考官点评（按纵轴进度变台词：看摊的→招牌没砸→五边混出名堂→M3 后顾问/超越/断绝）；放防重前=每次到店都点评
            try { StoryPlotSystem.MaybeFrogVisitComment(); } catch { }
            // 10-07 防两套（玩家"蛙哥没小退时刷了两套物品"）：OnClientArrived 无内部防重，正常到店事件链 + 读档轮询兜底
            //   可双触发（各自带防重互不感知）→ 全套上货两次。_cardSpawned=本会话已上货标志（加卡成功才置 true，
            //   蛙哥走后 CleanupCardIfGone 重置）→ 重复调用直接跳过并清轮询愿望；上货失败（_cardSpawned 未置）仍可重试。
            if (_cardSpawned)
            {
                _refillPending = false;
                Core.LogMsg("[蛙哥] OnClientArrived: 本会话已上过货，防重复跳过（清轮询愿望）");
                return;
            }
            try { if (client != null) { client.SetBudget(1109707341, 10000000); client.clientIntent = StoreClient.ClientIntent.SELLNBUY; } } catch { } Core.LogMsg("[蛙哥] intent已设=" + (client != null ? client.clientIntent.ToString() : "null(上货模式)"));
            // 10-05 蛙哥收购扩展（拆包实锤：clientBuyingIdList id精确 + clientBuyingTagList tag精确 + SELLNBUY 买路径）
            // 苦力boy反馈"水卖谁啊"：蛙哥收购 水/电池/模组/日用品；预算 100→1000（拆包③给出区间 500-1000，取上限 1000：单品全覆盖且可收 2-3 件）
            try
            {
                if (client.clientBuyingIdList == null) client.clientBuyingIdList = new Il2CppSystem.Collections.Generic.List<string>();
                if (client.clientBuyingTagList == null) client.clientBuyingTagList = new Il2CppSystem.Collections.Generic.List<string>();
                // 水（拆包②：5 id，普通水 unitValue=0——价下限兜底待用户拍板，见回传）
                client.clientBuyingIdList.Add("bottled_water"); client.clientBuyingIdList.Add("bottled_water_premium");
                client.clientBuyingIdList.Add("small_bottled_water"); client.clientBuyingIdList.Add("large_bottled_water");
                client.clientBuyingIdList.Add("water_jug");
                // 日用品（拆包②：DAILY_NEED_KEYS 11 id）
                client.clientBuyingIdList.Add("toothpaste"); client.clientBuyingIdList.Add("toilet_paper"); client.clientBuyingIdList.Add("shampoo");
                client.clientBuyingIdList.Add("paper_towel"); client.clientBuyingIdList.Add("box_tampon"); client.clientBuyingIdList.Add("pack_condom");
                client.clientBuyingIdList.Add("skincare_cream"); client.clientBuyingIdList.Add("salve"); client.clientBuyingIdList.Add("rubbing_alcohol");
                client.clientBuyingIdList.Add("neuroactive_perfume"); client.clientBuyingIdList.Add("pheromone_perfume");
                // 电池 + 模组（拆包②：tag 精确——power_source_item / MODULE）
                client.clientBuyingTagList.Add("power_source_item");
                client.clientBuyingTagList.Add("MODULE");
            }
            catch (System.Exception exb) { Core.LogMsg("[蛙哥] 收购清单设置异常: " + exb.Message); }
            // 10-16 P1 蛙哥升级：5.1 自定义收货标签（Lv2+ 门控，白名单校验）+ 5.2 点货签（CD 门控）——均在原生清单后追加不覆盖
            TryApplyCustomBuyList(client);
            SpawnOrderTickets();
            TryApplyOrderTicket(client);
            try { LoadPortrait(); client.spriteName = "wage_brother_portrait"; try { client.possibleSprites.Clear(); client.possibleSprites.Add("wage_brother_portrait"); } catch { } try { if (StoreClientMono.Instance != null && StoreClientMono.Instance.image != null && _portrait != null) { StoreClientMono.Instance.image.sprite = _portrait; Core.LogMsg("[蛙哥] 立绘已刷"); } } catch (System.Exception exr) { Core.LogMsg("[蛙哥] 刷立绘异常: " + exr.Message); } } catch { } Core.LogMsg("[蛙哥] 立绘注入完成");
            // 10-15 名片只发一次：card_given=1（已送过+电话簿已解锁）→ 之后蛙哥到店不再发卡，服务全走电话 1005 面板
            //   注意：只跳过【发卡块】——携带物品/许可货架/充电器货架是柜台服务，不依赖名片，必须照常上货
            bool cardGivenOnce = WageSaveStore.GetInt("wage_brother", CARD_GIVEN_KEY, 0) == 1;
            if (!cardGivenOnce)
            {
                // _cardSpawned 防重已在方法入口处理（return），此处直接走发卡
                GameItem card = null;
            try { card = DirectoryMaster.Item(CARD_ID); Core.LogMsg("[蛙哥] DirectoryMaster("+CARD_ID+")=" + (card!=null?"ok":"null")); } catch (Exception ex) { Core.LogMsg("[蛙哥] 创建CARD_ID异常: " + ex.Message); }
            if (card == null)
            {
                // 未注册物品 fallback：用 cassette_player 占位
                try { card = DirectoryMaster.Item("cassette_player", true); Core.LogMsg("[蛙哥] cassette_player fallback=" + (card!=null?"ok":"null")); if (card != null) card.identifier = CARD_ID; } catch (Exception ex) { Core.LogMsg("[蛙哥] cassette_player异常: " + ex.Message); }
            }
            if (card != null)
            {
                try { LoadCardSprite(); Core.LogMsg("[蛙哥] 图标加载完成, _cardSprite=" + (_cardSprite != null)); card.SetName(LangHelper.T("蛙哥的名片", "Wage Brother's Card")); card.shortDescription = LangHelper.T(
                    "蛙哥的名片。\n" +
                    "空间站传说里的\"传奇当铺主\"——他把一整间当铺留给了你：妙妙箱、炼蛊的机器、不稳定的 AI、保护器，还有蛙娘。\n" +
                    "他平时靠电话联络，每 7 天亲自到店一趟，带着高得离谱的预算，既收货，也带货。他能替你\"去邪气\"，也能拨动\"吞噬季\"的开关，甚至让你回到第一天。\n" +
                    "拨 1005 联系他——全部服务已存入电话簿，随时叫货。",
                    "Wage Brother's business card.\n" +
                    "The \"legendary pawnshop owner\" of station legend — he left you an entire shop: the Wonder Box, the forging machine, an unstable AI, a protector core, and Wage Girl.\n" +
                    "He reaches you by phone, and every 7 days he visits in person, carrying an absurdly large budget, buying and selling alike. He can \"purify\" you, flip the switch on the Cannibalism Season, and even send you back to Day 1.\n" +
                    "Dial 1005 to reach him — all his services are saved to your phonebook, call anytime."); var gsb = new GridShapeBuilder(); gsb.SetDataFill(2, 1); card.SetShape(gsb.Build()); card.SetSprite("custom_atlas", CARD_SPRITE_KEY); card.unitValue = 0; card.unitBaseValue = 0; // 名片不能卖
                    try { card.EnableTag("paper", true); } catch { } // 文档属性标签（销赃时不带走）
                } catch (System.Exception exload) { Core.LogMsg("[蛙哥] LoadCardSprite异常: " + exload.Message); }
                try { card.EnableTag("wage_bro_card", true); } catch { } try { var d = client.mainDialogue; if (d != null) { d.SetText("蛙哥", LangHelper.T("我来收点晦气。花信用点消一项负面特性，钱货两清。", "I collect trouble. Pay credits to remove a negative perk.")); d.endAction = null; if (d.nextDialogue != null) { d.nextDialogue.endAction = null; d.nextDialogue = null; } } } catch (System.Exception exd) { Core.LogMsg("[蛙哥] 清对话链异常: " + exd.Message); } Core.LogMsg("[蛙哥] 准备加卡: card=" + card.identifier); try { card.DisableTag("not_purchased", true); card.DisableTag("TAG_NOT_PURCHASED", true); card.EnableTag("IS_OWNED_TAG", true); PlayerStore.Instance.AddDirectSellingItemToTable(card, true, false, false, 0); card.DisableTag("not_purchased", true); card.EnableTag("IS_OWNED_TAG", true); Core.LogMsg("[蛙哥] 加卡调用返回,无异常"); } catch (System.Exception excard) { Core.LogMsg("[蛙哥] 服务卡上柜台异常: " + excard.Message); }
                
                _cardSpawned = true;
                // 10-07 C2-1/C2-2：名片/柜台货状态随档+立即落盘（对标小偷 KEY_THEFT_DAY——小退=进程退出，
                //   WageSaveStore 只在 SaveGame/EndDay Flush → 不 Flush 重进读文件=旧值=柜台空）
                // 10-15 名片只发一次：card_given=1 落盘（下次到店不再发）+ 幂等解锁电话簿 1005（电话簿直接可见、直接可拨）
                try { WageSaveStore.SetInt("wage_brother", CARD_GIVEN_KEY, 1); WageSaveStore.SetInt("wage_brother", CARD_SPAWNED_KEY, 1); WageSaveStore.Flush(); } catch (System.Exception exf) { Core.LogMsg("[蛙哥] 名片状态落盘失败: " + exf.Message); }
                try { B2WildPanel.EnsurePhoneRegistered(); try { StoreUIManager.Instance.Notify(LangHelper.T("蛙哥的电话已存入电话簿：1005", "Wage Brother's number saved to phonebook: 1005"), "green"); } catch { } Core.LogMsg("[蛙哥] 电话簿已解锁 1005"); } catch (System.Exception exp) { Core.LogMsg("[蛙哥] 解锁电话簿异常: " + exp.Message); }
                Core.LogMsg("[蛙哥] 到场，名片已上柜台（首次）");
            }
            } // end if(!cardGivenOnce) 发卡块——携带物在块外照常上货
            // 10-15 用户拍板：AI生成器/养蛊机只走服务（电话簿 1005 发放，不再随机携带）；
            // 保护器随机出现：初始 30%，服务可花钱加概率（每级 +10%，上限 90%）
            try
            {
                int spawnLevel = WageSaveStore.GetInt("wage_brother", PROTECTOR_SPAWN_KEY, 0);
                int protectorChance = 30 + spawnLevel * 10;
                if (protectorChance > 90) protectorChance = 90;
                if (Core.Rng.Next(100) < protectorChance)
                {
                    var protector = DirectoryMaster.Item("wage_protector_core", true);
                    if (protector != null)
                    {
                        protector.DisableTag("not_purchased", true);
                        protector.EnableTag("IS_OWNED_TAG", true);
                        PlayerStore.Instance.AddDirectSellingItemToTable(protector, false, false, false, 0);
                        Core.LogMsg("[蛙哥] 携带保护器售卖（概率=" + protectorChance + "%）");
                    }
                }
            }
            catch (Exception exai) { Core.LogMsg("[蛙哥] 携带物品上柜台异常: " + exai.Message); }

            // 10-15 用户拍板：柜台不再上许可/充电器（已搬进电话簿 1005，购买后发柜台）；携带物（AI/养蛊/保护器）照常

            // 清wanted7模板残留PEAT/JUICE
            try
            {
                var emp = Il2Cpp.EmporiumEntry.Instance; if (emp == null) return;
                var showcase = emp.showcaseElement as GameInventory;
                if (showcase == null || showcase.childItems == null) return;
                for (int i = showcase.childItems.Count - 1; i >= 0; i--)
                {
                    var it = showcase.childItems[i];
                    if (it == null) continue;
                    string id = it.identifier ?? "";
                    if (id == "processed_meat" || id == "processed_juice" || id == "peat")
                    {
                        showcase.childItems.RemoveAt(i);
                        Core.LogMsg("[蛙哥] 清柜台残留: " + id);
                    }
                }
            }
            catch (Exception exclean) { Core.LogMsg("[蛙哥] 清柜台异常: " + exclean.Message); }
        }
        catch (Exception ex) { Core.LogMsg("[蛙哥] OnClientArrived异常: " + ex.Message); }
    }

    // ===================== 10-16 P1 蛙哥升级：5.1 收货标签 / 5.2 点货签 =====================
    // 收购安全白名单（正面允许的 tag；系统/禁售 tag 由 IsSafeBuyTag 拒绝）
    private static readonly HashSet<string> OrderTagWhitelist = new HashSet<string>
    {
        "power_source_item", "MODULE", "CONTAINER_TAG", "WEAPON", "NARCOTIC", "FOOD", "MEDICAL", "LUXURY_ITEM", "DAILY_NEED", "DAILY_NEED_TAG"
    };
    private static readonly HashSet<string> OrderTagBlocklist = new HashSet<string>
    {
        "not_purchased", "TAG_NOT_PURCHASED", "stolen", "TAG_STOLEN", "contraband", "IS_OWNED_TAG", "EATEN_TAG",
        "ITEM_HIDDEN_TAG", "SYSTEM_TAG", "SYSTEM_TAG_UTILITY", "hidden", "owned", "locked", "TAG_LOCKED",
        "wage_bro_card", "paper", ORDER_TICKET_TAG, "destiny_dice_tag", "wage_washed", "STANDARD_MACHINE_TAG"
    };
    private static bool IsSafeBuyTag(string tag)
    {
        if (string.IsNullOrEmpty(tag)) return false;
        if (OrderTagBlocklist.Contains(tag)) return false;
        if (tag.StartsWith("TAG_") || tag.StartsWith("tag_") || tag.StartsWith("ITEM_")) return false;
        if (tag.StartsWith(ORDER_TAG_PREFIX)) return false; // 点货签自身品类编码不进白名单
        return OrderTagWhitelist.Contains(tag);
    }
    private static bool IsSafeBuyId(string id)
    {
        try
        {
            if (string.IsNullOrEmpty(id)) return false;
            if (Core.ExcludedItemIds != null && System.Array.IndexOf(Core.ExcludedItemIds, id) >= 0) return false;
            if (!DirectoryMaster.Has<GameItem>(id)) return false;
            var g = DirectoryMaster.Item(id, false);
            if (g == null) return false;
            if (g.IsTag("STANDARD_MACHINE_TAG") || g.IsTag("CONTAINER_TAG")) return false; // 机器/容器不当收购目标
            return true;
        }
        catch { return false; }
    }

    // 5.1 收货标签：蛙哥牛逼 Lv2+ → 配置的 id/tag 白名单校验后追加（不覆盖原生清单）；上限 Lv2=2 / Lv3=4 个（合计）
    private static void TryApplyCustomBuyList(StoreClient client)
    {
        try
        {
            if (client == null) return;
            int lv = B2ServiceStore.GetEffectLevel(WagePowerPerk.PerkId);
            if (lv < 2) { Core.LogMsg("[蛙哥] 5.1 等级不足跳过（lv=" + lv + "，需 Lv2+）"); return; }
            int cap = lv >= 3 ? 4 : 2;
            int added = 0;
            foreach (var raw in (BuildConfig.WageBrotherCustomBuyTags ?? "").Split(new char[] { ',', '，' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string t = raw.Trim();
                if (added >= cap) break;
                if (!IsSafeBuyTag(t)) { Core.LogMsg("[蛙哥] 5.1 拦截非法标签: " + t); continue; }
                if (client.clientBuyingTagList == null) client.clientBuyingTagList = new Il2CppSystem.Collections.Generic.List<string>();
                if (!client.clientBuyingTagList.Contains(t)) { client.clientBuyingTagList.Add(t); added++; Core.LogMsg("[蛙哥] 5.1 追加收购标签: " + t); }
            }
            foreach (var raw in (BuildConfig.WageBrotherCustomBuyIds ?? "").Split(new char[] { ',', '，' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string id = raw.Trim();
                if (added >= cap) break;
                if (!IsSafeBuyId(id)) { Core.LogMsg("[蛙哥] 5.1 拦截非法id: " + id); continue; }
                if (client.clientBuyingIdList == null) client.clientBuyingIdList = new Il2CppSystem.Collections.Generic.List<string>();
                if (!client.clientBuyingIdList.Contains(id)) { client.clientBuyingIdList.Add(id); added++; Core.LogMsg("[蛙哥] 5.1 追加收购id: " + id); }
            }
            Core.LogMsg("[蛙哥] 5.1 收货标签完成（lv=" + lv + " 上限=" + cap + " 追加=" + added + "）");
        }
        catch (Exception ex) { Core.LogMsg("[蛙哥] 5.1 收货标签异常: " + ex.Message); }
    }

    // 5.2 携带：按配置 OrderTargetTags 每品类生成 1 张点货签上柜台（售价 BuildConfig.WageBrotherOrderPrice）；
    //   柜台已有任何点货签则跳过（防读档补货/重复调用积累）
    private static void SpawnOrderTickets()
    {
        try
        {
            var tags = (BuildConfig.WageBrotherOrderTargetTags ?? "").Split(new char[] { ',', '，' }, StringSplitOptions.RemoveEmptyEntries);
            if (tags.Length == 0) return;
            var emp = Il2Cpp.EmporiumEntry.Instance;
            if (emp != null)
            {
                var sh = emp.showcaseElement as GameInventory;
                if (sh != null && sh.childItems != null)
                    for (int i = 0; i < sh.childItems.Count; i++)
                    {
                        var it = sh.childItems[i];
                        if (it == null) continue;
                        bool isTicket = false; try { isTicket = it.IsTag(ORDER_TICKET_TAG); } catch { }
                        if (isTicket) { Core.LogMsg("[蛙哥] 5.2 柜台已有点货签，跳过携带"); return; }
                    }
            }
            foreach (var raw in tags)
            {
                string t = raw.Trim();
                if (!IsSafeBuyTag(t)) { Core.LogMsg("[蛙哥] 5.2 拦截非法点货签品类: " + t); continue; }
                var tk = DirectoryMaster.Item(ORDER_TICKET_ID, true);
                if (tk == null) continue;
                try { tk.EnableTag(ORDER_TAG_PREFIX + t, true); } catch { }
                try { tk.SetName(LangHelper.T("点货签：" + t, "Order Ticket: " + t)); } catch { }
                try { tk.unitValue = BuildConfig.WageBrotherOrderPrice; tk.unitBaseValue = BuildConfig.WageBrotherOrderPrice; } catch { }
                try { tk.DisableTag("not_purchased", true); tk.DisableTag("TAG_NOT_PURCHASED", true); tk.EnableTag("IS_OWNED_TAG", true); } catch { }
                try { PlayerStore.Instance.AddDirectSellingItemToTable(tk, true, false, false, 0); } catch (Exception ex) { Core.LogMsg("[蛙哥] 5.2 点货签上柜台异常: " + ex.Message); }
                Core.LogMsg("[蛙哥] 5.2 点货签上柜台: " + t);
            }
        }
        catch (Exception ex) { Core.LogMsg("[蛙哥] 5.2 携带点货签异常: " + ex.Message); }
    }

    // 读取物品实例的业务标签（反射 tagSystem 字典，只收集启用项）——点货签品类扫描用
    private static List<string> GetInstanceTags(GameItem item)
    {
        var tags = new List<string>();
        try
        {
            if (item == null) return tags;
            var fi = item.GetType().GetField("tagSystem", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (fi == null) fi = item.GetType().GetField("myTags", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (fi != null)
            {
                object ts = fi.GetValue(item);
                if (ts != null)
                {
                    var dictProp = ts.GetType().GetProperty("dict", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                    if (dictProp != null)
                    {
                        object dict = dictProp.GetValue(ts);
                        if (dict is System.Collections.IDictionary idict)
                        {
                            foreach (System.Collections.DictionaryEntry de in idict)
                            {
                                string key = de.Key?.ToString() ?? "";
                                if (string.IsNullOrEmpty(key)) continue;
                                bool enabled = false;
                                try { object val = de.Value; if (val != null) { var m = val.GetType().GetMethod("IsEnabled"); if (m != null) enabled = (bool)m.Invoke(val, null); } } catch { enabled = false; }
                                if (enabled) tags.Add(key);
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex) { Core.LogMsg("[蛙哥] GetInstanceTags异常: " + ex.Message); }
        return tags;
    }

    // 5.2 生效：扫描柜台找点货签（wage_order_tag）→ 读 wage_order_ 前缀品类 → CD 门控（全局键，默认 3 天）→
    //   临时追加收购清单（不写常驻配置=本次到店失效）；单次到店最多 1 张生效
    private static void TryApplyOrderTicket(StoreClient client)
    {
        try
        {
            if (client == null) return;
            if (_orderUsedThisVisit) return; // 本会话本次到店已生效（CleanupCardIfGone 复位）
            int day = 0; try { day = Il2Cpp.StoreStation.GetDayCounter(); } catch { }
            int last = WageSaveStore.GetInt("wage_brother", ORDER_CD_KEY, -1);
            int cd = BuildConfig.WageBrotherOrderCd;
            if (cd > 0 && last >= 0 && day - last < cd)
            {
                Core.LogMsg("[蛙哥] 5.2 点货签冷却中（day=" + day + " last=" + last + " cd=" + cd + "）");
                return;
            }
            var emp = Il2Cpp.EmporiumEntry.Instance;
            if (emp == null) return;
            var showcase = emp.showcaseElement as GameInventory;
            if (showcase == null || showcase.childItems == null) return;
            for (int i = 0; i < showcase.childItems.Count; i++)
            {
                var it = showcase.childItems[i];
                if (it == null) continue;
                bool isTicket = false; try { isTicket = it.IsTag(ORDER_TICKET_TAG); } catch { }
                if (!isTicket) continue;
                var tags = GetInstanceTags(it);
                string target = null;
                for (int j = 0; j < tags.Count; j++)
                {
                    if (tags[j].StartsWith(ORDER_TAG_PREFIX) && tags[j] != ORDER_TICKET_TAG)
                    {
                        target = tags[j].Substring(ORDER_TAG_PREFIX.Length);
                        break;
                    }
                }
                if (string.IsNullOrEmpty(target)) continue;
                if (!IsSafeBuyTag(target)) { Core.LogMsg("[蛙哥] 5.2 点货签品类非法被拦: " + target); continue; }
                if (client.clientBuyingTagList == null) client.clientBuyingTagList = new Il2CppSystem.Collections.Generic.List<string>();
                if (!client.clientBuyingTagList.Contains(target)) client.clientBuyingTagList.Add(target);
                WageSaveStore.SetInt("wage_brother", ORDER_CD_KEY, day);
                _orderUsedThisVisit = true;
                try { Il2Cpp.StoreUIManager.Instance.Notify(LangHelper.T("点货签生效：蛙哥本次收购「" + target + "」", "Order ticket active: Wage Brother buys \"" + target + "\" this visit"), "green"); } catch { }
                Core.LogMsg("[蛙哥] 5.2 点货签生效: " + target + "（CD 至 day+" + cd + "）");
                break;
            }
        }
        catch (Exception ex) { Core.LogMsg("[蛙哥] 5.2 点货签生效异常: " + ex.Message); }
    }

    private static Sprite _portrait = null;
    private static Sprite _cardSprite = null;
    internal const string CARD_SPRITE_KEY = "wage_brother_card_sprite";
    // 主动刷新客户 SpriteRenderer（照 PlayerStore:10511-10523 原生链）
    private static void RefreshClientSprite(StoreClient client)
    {
        try
        {
            var sprite = SpriteDict.Instance.GetSprite("wage_brother_portrait");
            Core.LogMsg("[蛙哥] GetSprite=" + (sprite != null ? "ok" : "null"));
            if (sprite == null) return;
            // _ClientGameObject 是客户实体（日志已实锤），直接刷它的 SpriteRenderer
            foreach (var sr in UnityEngine.Object.FindObjectsOfType<SpriteRenderer>())
            {
                if (sr == null) continue;
                try { if (sr.gameObject != null && sr.gameObject.name == "_ClientGameObject") { sr.sprite = sprite; Core.LogMsg("[蛙哥] 立绘已刷新到 _ClientGameObject"); } }
                catch { }
            }
        }
        catch (Exception ex) { Core.LogMsg("[蛙哥] RefreshSprite异常: " + ex.Message); }
    }

    internal static void LoadCardSprite()
    {
        try
        {
            if (_cardSprite != null) return;
            // 10-07 统一载入：WagePixelSprites Color[]（妙妙箱式，32x16 PPU 500 保持原显示尺寸）
            _cardSprite = WagePixelSprites.WageBrotherCardSprite();
            if (_cardSprite == null) { Core.LogMsg("[蛙哥] 服务卡像素数组生成失败"); return; }
            // 10-03 修：服务卡sprite必须进SpriteDict（读档重建渲染查字典，仅静态字段=旧档显示问号；立绘/许可同模式）
            try { if (SpriteDict.Instance != null && SpriteDict.Instance.spriteDictionary != null && _cardSprite != null) { SpriteDict.Instance.spriteDictionary[CARD_SPRITE_KEY] = _cardSprite; Core.LogMsg("[蛙哥] 服务卡sprite已注入SpriteDict"); } else { Core.LogMsg("[蛙哥] SpriteDict未就绪，等服务卡渲染兜底重写"); } } catch (System.Exception exdict) { Core.LogMsg("[蛙哥] 服务卡注入SpriteDict异常: " + exdict.Message); }
            Core.LogMsg("[蛙哥] 服务卡图标加载（像素数组）"); // atlasCache直写注释：索引器导致卡死，走PrefixLoadFromAtlas拦截
        } catch (System.Exception ex) { Core.LogMsg("[蛙哥] 服务卡图标加载失败: " + ex.Message); }
    }
    public static bool PrefixLoadFromAtlas(string atlasPath, string name, ref Sprite __result)
    {
        try
        {
            if (atlasPath != "custom_atlas") return true;

            // 服务卡
            if (name == CARD_SPRITE_KEY)
            {
                if (_cardSprite == null) LoadCardSprite();
                if (_cardSprite != null) { __result = _cardSprite; return false; }
                // 10-03 兜底：若上面注入失败（SpriteDict早期null），渲染时补写（此时字典必然就绪）
                try { if (SpriteDict.Instance != null && SpriteDict.Instance.spriteDictionary != null && _cardSprite != null && !SpriteDict.Instance.spriteDictionary.ContainsKey(CARD_SPRITE_KEY)) { SpriteDict.Instance.spriteDictionary[CARD_SPRITE_KEY] = _cardSprite; } } catch { }
                return true;
            }

            // 许可/充电器——懒加载兜底
            Sprite sp = GetPermitChargerSprite(name);
            if (sp != null) { __result = sp; return false; }
        }
        catch { }
        return true;
    }

    // 懒加载许可/充电器图标
    private static Sprite GetPermitChargerSprite(string spriteKey)
    {
        try
        {
            // 先查SpriteDict
            if (SpriteDict.Instance.spriteDictionary.ContainsKey(spriteKey))
                return SpriteDict.Instance.spriteDictionary[spriteKey];

            // 现场加载
            string fileName = null;
            if (spriteKey == "wage_permit_1_sprite") fileName = "wage_permit_1.png";
            else if (spriteKey == "wage_permit_2_sprite") fileName = "wage_permit_2.png";
            else if (spriteKey == "wage_permit_3_sprite") fileName = "wage_permit_3.png";
            else if (spriteKey == "wage_charger_1_sprite") fileName = "wage_charger_1.png";
            else if (spriteKey == "wage_charger_2_sprite") fileName = "wage_charger_2.png";
            else if (spriteKey == "wage_charger_3_sprite") fileName = "wage_charger_3.png";
            if (fileName == null) return null;

            LoadPermitChargerIcon(fileName, spriteKey);
            if (SpriteDict.Instance.spriteDictionary.ContainsKey(spriteKey))
                return SpriteDict.Instance.spriteDictionary[spriteKey];
            return null;
        }
        catch { return null; }
    }
    internal static void LoadPortrait()
    {
        try
        {
            if (_portrait != null) { Core.LogMsg("[蛙哥] 准备注入SpriteDict"); SpriteDict.Instance.spriteDictionary["wage_brother_portrait"] = _portrait; return; }
            var asm = System.Reflection.Assembly.GetExecutingAssembly();
            string resName = null;
            foreach (var n in asm.GetManifestResourceNames()) if (n.EndsWith("wage_brother_portrait.png")) { resName = n; break; }
            if (resName == null) { Core.LogMsg("[蛙哥] 立绘资源未找到"); return; }
            using var st = asm.GetManifestResourceStream(resName);
            byte[] png = new byte[st.Length]; st.Read(png, 0, png.Length);
            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point; tex.wrapMode = TextureWrapMode.Clamp; tex.mipMapBias = 0;
            tex.wrapMode = TextureWrapMode.Clamp;
            Type icType = null;
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies()) { Type[] ts; try { ts = a.GetTypes(); } catch (System.Reflection.ReflectionTypeLoadException ex) { ts = ex.Types; } foreach (var t in ts) if (t != null && t.Name == "ImageConversion") { icType = t; break; } if (icType != null) break; }
            Core.LogMsg("[蛙哥] 资源=" + resName + " size=" + png.Length + " icType=" + (icType!=null?icType.FullName:"null"));
            Core.LogMsg("[蛙哥] LoadImage前"); icType.GetMethod("LoadImage", new Type[] { typeof(Texture2D), typeof(Il2CppStructArray<byte>) }).Invoke(null, new object[] { tex, (Il2CppStructArray<byte>)png });
            Core.LogMsg("[蛙哥] LoadImage后 tex=" + tex.width + "x" + tex.height);
            _portrait = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 500f);
            Core.LogMsg("[蛙哥] Sprite.Create后, Instance=" + (SpriteDict.Instance!=null?"ok":"null"));
            UnityEngine.Object.DontDestroyOnLoad(tex); UnityEngine.Object.DontDestroyOnLoad(_portrait); SpriteDict.Instance.spriteDictionary["wage_brother_portrait"] = _portrait; Core.LogMsg("[蛙哥] 立绘加载成功");
        } catch (System.Exception ex) { Core.LogMsg("[蛙哥] 立绘加载失败: " + ex.Message); }
    }
    // 上许可物品到柜台
    private static void AddPermitToCounter(string permitId, int price)
    {
        try
        {
            var item = DirectoryMaster.Item(permitId, true);
            if (item == null) return;

            // 设置名称和描述
            if (permitId == WageBrokerPermitHelper.PERMIT_1_ID)
            {
                item.SetName(LangHelper.T("蛙哥的许可（一级）", "Wage's Permit (Tier 1)"));
                item.shortDescription = LangHelper.T("每晚外出次数 +1（可叠加）。拾荒时仍可能受伤。", "+1 night outing per night (stacks). Scavenging may still cause injuries.");
            }
            else if (permitId == WageBrokerPermitHelper.PERMIT_2_ID)
            {
                item.SetName(LangHelper.T("蛙哥的许可（二级）", "Wage's Permit (Tier 2)"));
                item.shortDescription = LangHelper.T("每晚外出次数 +1（可叠加）。拾荒受伤概率减半。", "+1 night outing per night (stacks). Scavenging injury chance halved.");
            }
            else if (permitId == WageBrokerPermitHelper.PERMIT_3_ID)
            {
                item.SetName(LangHelper.T("蛙哥的许可（三级）", "Wage's Permit (Tier 3)"));
                item.shortDescription = LangHelper.T("每晚外出次数 +1（可叠加）。拾荒时完全不会受伤。", "+1 night outing per night (stacks). Complete immunity to scavenging injuries.");
            }

            item.unitValue = price;
            item.unitBaseValue = price;

            item.DisableTag("not_purchased", true);
            item.EnableTag("IS_OWNED_TAG", true);
            Core.LogMsg("[蛙哥] " + permitId + " 上柜前标签: owned=" + SafeIsTag(item, "IS_OWNED_TAG") + " np=" + SafeIsTag(item, "not_purchased") + " np2=" + SafeIsTag(item, "TAG_NOT_PURCHASED"));
            PlayerStore.Instance.AddDirectSellingItemToTable(item, false, false, false, 0);
            Core.LogMsg("[蛙哥] 上许可: " + permitId + " 价格=" + price);
        }
        catch (Exception ex) { Core.LogMsg("[蛙哥] AddPermitToCounter异常: " + permitId + " " + ex.Message); }
    }

    // 预加载许可/充电器图标（修复懒加载死锁）
    internal static void LoadAllIcons()
    {
        try
        {
            LoadCardSprite(); // 服务卡
            // 许可图标
            LoadPermitChargerIcon("wage_permit_1.png", "wage_permit_1_sprite");
            LoadPermitChargerIcon("wage_permit_2.png", "wage_permit_2_sprite");
            LoadPermitChargerIcon("wage_permit_3.png", "wage_permit_3_sprite");
            // 充电器图标
            LoadPermitChargerIcon("wage_charger_1.png", "wage_charger_1_sprite");
            LoadPermitChargerIcon("wage_charger_2.png", "wage_charger_2_sprite");
            LoadPermitChargerIcon("wage_charger_3.png", "wage_charger_3_sprite");
            Core.LogMsg("[蛙哥] 许可/充电器图标预加载完成");
        }
        catch (Exception ex) { Core.LogMsg("[蛙哥] LoadAllIcons异常: " + ex.Message); }
    }

    // 加载许可/充电器图标到SpriteDict
    private static void LoadPermitChargerIcon(string fileName, string spriteKey)
    {
        try
        {
            if (SpriteDict.Instance == null) { Core.LogMsg("[蛙哥] SpriteDict未就绪，跳过预加载(懒加载兜底): " + spriteKey); return; } // 10-03 启动早期时序守卫（原预加载NRE根因）
            // 10-07 统一载入：WagePixelSprites Color[]（妙妙箱式，32x32 PPU 200 保持 2×2 占格标准）
            Sprite sp = WagePixelSprites.PermitChargerSprite(fileName);
            if (sp == null) { Core.LogMsg("[蛙哥] 图标像素数组未找到: " + fileName); return; }
            SpriteDict.Instance.spriteDictionary[spriteKey] = sp;
            UnityEngine.Object.DontDestroyOnLoad(sp);
            Core.LogMsg("[蛙哥] 图标加载(像素数组): " + spriteKey);
        }
        catch (Exception ex) { Core.LogMsg("[蛙哥] LoadPermitChargerIcon异常 " + fileName + ": " + ex.Message + " | " + (ex.StackTrace != null ? ex.StackTrace.Split('\n')[0] : "")); }
    }

    // 上充电器物品到柜台
    private static void AddChargerToCounter(string chargerId, int price)
    {
        try
        {
            var item = DirectoryMaster.Item(chargerId, true);
            if (item == null) return;

            // 设置名称和描述
            if (chargerId == WageBrokerChargerHelper.CHARGER_1_ID)
            {
                item.SetName(LangHelper.T("蛙哥充电器（一级）", "Wage's Charger (Tier 1)"));
                item.shortDescription = LangHelper.T("每晚自动给背包所有电池充3点电量（拥有即生效，不消耗）。", "Automatically charges all batteries in your backpack by 3 per night (persistent, not consumed).");
            }
            else if (chargerId == WageBrokerChargerHelper.CHARGER_2_ID)
            {
                item.SetName(LangHelper.T("蛙哥充电器（二级）", "Wage's Charger (Tier 2)"));
                item.shortDescription = LangHelper.T("每晚自动给背包所有电池充6点电量（拥有即生效，不消耗）。", "Automatically charges all batteries in your backpack by 6 per night (persistent, not consumed).");
            }
            else if (chargerId == WageBrokerChargerHelper.CHARGER_3_ID)
            {
                item.SetName(LangHelper.T("蛙哥充电器（三级）", "Wage's Charger (Tier 3)"));
                item.shortDescription = LangHelper.T("每晚自动给背包所有电池充满电（拥有即生效，不消耗）。", "Fully charges all batteries in your backpack every night (persistent, not consumed).");
            }

            item.unitValue = price;
            item.unitBaseValue = price;

            item.DisableTag("not_purchased", true);
            item.EnableTag("IS_OWNED_TAG", true);
            Core.LogMsg("[蛙哥] " + chargerId + " 上柜前标签: owned=" + SafeIsTag(item, "IS_OWNED_TAG") + " np=" + SafeIsTag(item, "not_purchased") + " np2=" + SafeIsTag(item, "TAG_NOT_PURCHASED"));
            PlayerStore.Instance.AddDirectSellingItemToTable(item, false, false, false, 0);
            Core.LogMsg("[蛙哥] 上充电器: " + chargerId + " 价格=" + price);
        }
        catch (Exception ex) { Core.LogMsg("[蛙哥] AddChargerToCounter异常: " + chargerId + " " + ex.Message); }
    }

    // 蛙哥走后清服务卡（每日调用）
    internal static void CleanupCardIfGone()
    {
        try
        {
            if (!_cardSpawned) return;
            if (HasQueued()) return; // 蛙哥还在
            _cardSpawned = false;
            _orderUsedThisVisit = false; // 10-16 P1-5.2：蛙哥走后复位点货签"本次到店"标记（下次到店可再生效）
            // 10-07 C2-1/C2-2：清卡=清除补货愿望（同步落盘——防读档重建柜台时按旧愿望补出蛙哥不在的卡）
            try { WageSaveStore.SetInt("wage_brother", CARD_SPAWNED_KEY, 0); WageSaveStore.Flush(); } catch (System.Exception exf) { Core.LogMsg("[蛙哥] 名片状态清盘失败: " + exf.Message); }
            // 清掉柜台上的服务卡（wage_bro_card tag）
            var em = EmporiumEntry.Instance; if (em == null) return;
            var all = em.GetAllItems();
            foreach (var it in all)
            {
                if (it == null) continue;
                bool isCard = false; try { isCard = it.IsTag("wage_bro_card"); } catch { }
                if (isCard) { try { it.Destroy(); } catch { } } // 销毁语义：直接Destroy
            }
            Core.LogMsg("[蛙哥] 走了，服务卡已清");
        }
        catch { }
    }

    // 定价：Cost → 费用
    internal static int PriceForCost(int cost)
    {
        if (cost <= -20) return 8000;
        if (cost <= -15) return 5000;
        if (cost <= -7) return 2000;
        return 500; // -2/-3
    }

    // 10-18 设计稿 §3.5-1：消业障「不可删」名单——mod 系统特性（删了拆断系统链路：精神错乱删后 MadnessRoller.IsLocked 仍 true）
    private static readonly System.Collections.Generic.HashSet<string> NonRemovablePerks = new System.Collections.Generic.HashSet<string>
    {
        "精神错乱", "蛙娘", "蛙哥牛逼", "捡漏直觉", "命运之骰", "流浪者"
    };

    // 弹消perk界面
    internal static void ShowRemovePerkWindow()
    {
        try
        {
            if (PlayerStore.Instance == null) return;
            var mgr = CustomUIManager.Instance; if (mgr == null) return;
            if (mgr.IsOpen("wage_bro_window")) mgr.CloseWindow("wage_bro_window");
            var w = mgr.CreateWindow("wage_bro_window", LangHelper.T("蛙哥 · 消业障", "Wage Brother · Remove Burden"), "overlay");
            if (w == null) return;
            // 10-07 #8（玩家反馈"赎回面板没有统计到所有物品"）：明细行数上限 8 → 16 + 面板加高（480→560）——8 行截断=超过 8 种物品只显示前 8 + 总数
            w.SetSize(360, 560).SetPosition(Vector2.zero);            w.BeginColumn(4f);
            w.AddLabel(LangHelper.T("蛙哥：花钱消个负面特性。钱货两清。（只消特性，不追回已发生的声望/戒断/物品）", "Wage Brother: pay to remove a negative perk. No refunds. (Perk removed only; reputation/addiction already applied stays)"), "wb_hint");
            var ps = PlayerStore.Instance;
            w.AddLabel(LangHelper.T("当前现金：" + ps.playerCash, "Cash: " + ps.playerCash), "wb_cash");
            // 10-06 修复单#3：赎回区提前到 perk 列表之前（原位置在列表尾被挤出可视区——玩家反馈"服务卡没看到赎回"）
            //   招贼体质·被偷物赎回：小偷半价卖回买不起 → 蛙哥服务卡赎回；未赎回完（KEY_STOLEN 非空）之前，招贼体质特性无法消除
            try
            {
                int stolenCount = ThiefMagnetSystem.StolenCount();
                if (stolenCount > 0)
                {
                    w.AddLabel(LangHelper.T("被偷物品 " + stolenCount + " 件待赎回（半价找回）", "Stolen items: " + stolenCount + " to redeem (half price)"), "wb_stolen_hint");
                    // 10-06 A6：赎回区列举所有未赎回物品 + 各自价格（现状只显示件数——玩家"统计不到所有物品"）
                    try
                    {
                        var details = ThiefMagnetSystem.StolenItemDetails();
                        for (int di = 0; di < details.Count && di < 16; di++)
                            w.AddLabel(details[di], "wb_stolen_item");
                        if (details.Count > 16) w.AddLabel(LangHelper.T("…共 " + stolenCount + " 件", "... " + stolenCount + " total"), "wb_stolen_item");
                    }
                    catch (Exception exd) { Core.LogMsg("[蛙哥] 赎回明细异常: " + exd.Message); }
                    var redeemAct = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((System.Action)(() => { try { ThiefMagnetSystem.RedeemStolenItems(); ShowRemovePerkWindow(); } catch (Exception ex) { Core.LogMsg("[蛙哥] 赎回异常: " + ex.Message); } }));
                    w.AddButton(LangHelper.T("🔒 赎回被偷物品（半价）", "🔒 Redeem stolen items (half)"), redeemAct, "wb_redeem");
                }
            }
            catch (Exception exs) { Core.LogMsg("[蛙哥] 赎回区异常: " + exs.Message); }
            // 列已选 Cost<0 负面perk
            int listed = 0;
            // 10-18 §3.3：去重（②原版段也遍历 startingPerks——mod 特性若同在其中，先列后跳过）
            var seen = new System.Collections.Generic.HashSet<string>();
            foreach (var perk in CustomStartingPerks.All)
            {
                try
                {
                    if (perk.Cost >= 0) continue;
                    if (!StartingPerk.IsPerkActive(perk.Id)) continue;
                    seen.Add(perk.Id);
                    // 10-06 修复单#2：招贼体质消除费用固定 5000（特性 Cost=-8 本应 2000 档——特判）
                    int price = perk.Id == ThiefMagnetPerk.PerkId ? 5000 : PriceForCost(perk.Cost);
                    string nm = perk.DisplayName;
                    int cpy = perk.Cost;
                    int pr = price;
                    // 10-06 招贼体质锁定：被偷物品未赎回完（KEY_STOLEN 非空）之前不能消除该特性
                    if (perk.Id == ThiefMagnetPerk.PerkId && ThiefMagnetSystem.StolenCount() > 0)
                    {
                        var lockedAct = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((System.Action)(() => {
                            StoreUIManager.Instance.Notify(LangHelper.T("被偷物品还没赎完，招贼体质暂时消不掉——先点上面的「赎回被偷物品」", "Redeem your stolen items first before removing Thief Magnet."));
                        }));
                        string lockedText = LangHelper.T(nm + " · 先赎回被偷物品", nm + " · redeem stolen first");
                        w.AddButton(lockedText, lockedAct, "wb_perk_" + listed);
                        listed++;
                        continue;
                    }
                    var act = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((System.Action)(() => { try { DoRemovePerk(perk.Id, pr); } catch (Exception ex) { Core.LogMsg("[蛙哥] 消perk异常: " + ex.Message); } }));
                    string btnText = LangHelper.T(nm, nm);
                    // 10-03 补：非声名狼藉perk按钮加对应价格（声名狼藉走特殊每势力3000计费）
                    if (perk.Id != "声名狼藉") btnText += LangHelper.T(" · " + pr + "信用点", " · " + pr + " cr");
                    // 10-03 补：声名狼藉按钮加剩余总额（剩余负势力×3000）
                    if (perk.Id == "声名狼藉")
                    {
                        int negCount = 0;
                        try
                        {
                            string[] factionIds = { "FACTION_SECURITY", "FACTION_UPPER_LEVEL", "FACTION_REVOLUTION", "FACTION_LOWER_LEVEL", "FACTION_BLACK_MARKET" };
                            foreach (var fid in factionIds)
                            {
                                var rep = Il2Cpp.StoreReputation.GetStoreReputation(fid);
                                if (rep != null && (int)rep.GetReputationExact() < 0) negCount++;
                            }
                        }
                        catch { }
                        if (negCount > 0) btnText += LangHelper.T("（剩余 " + negCount + "×3000）", "（" + negCount + "×3000 left）");
                    }
                    w.AddButton(btnText, act, "wb_perk_" + listed);
                    listed++;
                }
                catch { }
            }
            // 10-18 设计稿 §3.2/3.3：② 原版负面特性——CustomStartingPerks.All 只枚举 mod 特性，
            //   原版（关节炎/成瘾/文盲/罪犯…）一个都不在 = 「关节炎删不掉」的直接原因（枚举源缺口）。
            //   遍历 PlayerStore.startingPerks 中 type==NEGATIVE 条目；NonRemovable + maxSlot>0 跳过。
            try
            {
                var plist = PlayerStore.Instance != null ? PlayerStore.Instance.startingPerks : null;
                if (plist != null)
                {
                    for (int i = 0; i < plist.Count; i++)
                    {
                        var p = plist[i];
                        if (p == null || string.IsNullOrEmpty(p.id)) continue;
                        if (seen.Contains(p.id)) continue;       // mod 特性已在 ① 列出
                        if ((int)p.type != 1) continue;          // 只收 NEGATIVE
                        if (p.maxSlot > 0) continue;             // 加槽特性不给删（槽账脱节）
                        if (NonRemovablePerks.Contains(p.id)) continue; // 系统特性不给删
                        seen.Add(p.id);
                        int price = p.id == "degenerative_osteoarthritis" ? 5000 : PriceForCost((int)p.cost);
                        string nm = "";
                        try { nm = p.GetLocalizedDisplayName(); } catch { nm = p.id; }
                        string pid = p.id;
                        int pr = price;
                        var act = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((System.Action)(() => { try { DoRemovePerk(pid, pr); } catch (Exception ex) { Core.LogMsg("[蛙哥] 消原版perk异常: " + ex.Message); } }));
                        string btnText = LangHelper.T(nm + " · " + pr + "信用点", nm + " · " + pr + " cr");
                        w.AddButton(btnText, act, "wb_perk_" + listed);
                        listed++;
                    }
                }
            }
            catch (Exception exp) { Core.LogMsg("[蛙哥] 原版perk列表异常: " + exp.Message); }
            if (listed == 0) w.AddLabel(LangHelper.T("没有可消除的负面特性", "No removable negative perks"), "wb_empty");
            // 10-04 合成升级入口（蛙哥服务合成：许可/充电器 2、3 级）
            var craftAct = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((System.Action)(() => { try { ShowCraftWindow(); } catch (Exception ex) { Core.LogMsg("[蛙哥] 合成窗口异常: " + ex.Message); } }));
            w.AddButton(LangHelper.T("合成升级 · 升级蛙哥的货", "Craft Upgrade"), craftAct, "wb_craft");
            // 10-05 引导 MVP：蛙哥指南按钮（3 窗口共用总览）
            var wbGuideBtn = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((System.Action)(() => { try { ShowWageBrotherGuide(); } catch (Exception ex) { Core.LogMsg("[蛙哥] 指南异常: " + ex.Message); } }));
            w.AddButton(LangHelper.T("📖 蛙哥指南", "📖 Guide"), wbGuideBtn, "wb_guide_btn");
        }
        catch (Exception ex) { Core.LogMsg("[蛙哥] ShowWindow异常: " + ex.Message); }
    }

    private static void DoRemovePerk(string perkId, int price)
    {
        try
        {
            var ps = PlayerStore.Instance; if (ps == null) return;
            // 10-02 改：声名狼藉先不扣2000、不RemovePerk——弹势力窗口，分次恢复，全正才消
            if (perkId == "声名狼藉")
            {
                try { ShowRepFactionWindow(); return; }
                catch (Exception ex) { Core.LogMsg("[蛙哥] 弹势力窗口异常: " + ex.Message); }
                return;
            }
            if (ps.playerCash < price) { StoreUIManager.Instance.Notify(LangHelper.T("钱不够", "Not enough credits")); return; }
            ps.playerCash -= price;
            StartingPerk.RemovePerk(perkId);
            Core.LogMsg("[蛙哥] 已消除 " + perkId + "，扣 " + price);
            StoreUIManager.Instance.Notify(LangHelper.T("蛙哥收了" + price + "块，" + perkId + "消了", "Wage Brother took " + price + ", removed " + perkId));
            // 清服务卡
            _cardSpawned = false;
            CleanupCardIfGone();
            // 关窗口
            try { if (CustomUIManager.Instance != null) CustomUIManager.Instance.CloseWindow("wage_bro_window"); } catch { }
        }
        catch (Exception ex) { Core.LogMsg("[蛙哥] DoRemovePerk异常: " + ex.Message); }
    }

    // 五势力声望恢复窗口（每势力3000）
    internal static void ShowRepFactionWindow()
    {
        try
        {
            var mgr = CustomUIManager.Instance; if (mgr == null) return;
            if (mgr.IsOpen("wage_rep_window")) mgr.CloseWindow("wage_rep_window");
            var w = mgr.CreateWindow("wage_rep_window", LangHelper.T("蛙哥 · 声望恢复", "Wage Brother · Rep Restore"), "overlay");
            if (w == null) return;
            w.SetSize(360, 400).SetPosition(Vector2.zero);
            w.BeginColumn(4f);
            w.AddLabel(LangHelper.T("蛙哥：声望恢复，每个势力3000，点哪个恢复哪个。", "Wage Brother: rep restore, 3000 per faction, click to restore."), "wr_hint");
            var ps = PlayerStore.Instance;
            w.AddLabel(LangHelper.T("当前现金：" + ps.playerCash, "Cash: " + ps.playerCash), "wr_cash");
            string[] factionIds = { "FACTION_SECURITY", "FACTION_UPPER_LEVEL", "FACTION_REVOLUTION", "FACTION_LOWER_LEVEL", "FACTION_BLACK_MARKET" };
            string[] factionNames = { "治安部", "上层", "革命派", "下层", "黑市" };
            for (int i = 0; i < factionIds.Length; i++)
            {
                string fid = factionIds[i];
                string fnm = factionNames[i];
                try
                {
                    var rep = StoreReputation.GetStoreReputation(fid);
                    if (rep == null) continue;
                    int cur = (int)rep.GetReputationExact();
                    if (cur >= 0) continue; // 已恢复跳过
                    string btnText = LangHelper.T(fnm + "（" + cur + "→0，3000块）", fnm + " (" + cur + "→0, 3000cr)");
                    var act = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((System.Action)(() =>
                    {
                        try
                        {
                            var p = PlayerStore.Instance; if (p == null) return;
                            if (p.playerCash < 3000) { StoreUIManager.Instance.Notify(LangHelper.T("钱不够", "Not enough credits")); return; }
                            p.playerCash -= 3000;
                            var r = StoreReputation.GetStoreReputation(fid);
                            if (r != null) { int c = (int)r.GetReputationExact(); if (c < 0) r.ModReputation(-c); }
                            Core.LogMsg("[蛙哥] " + fnm + " 声望恢复，扣3000");
                            StoreUIManager.Instance.Notify(LangHelper.T(fnm + "声望恢复", fnm + " rep restored"));
                            // 10-02 改：检查5势力全≥0→自动消声名狼藉
                            if (CheckAllFactionsPositive())
                            {
                                StartingPerk.RemovePerk("声名狼藉");
                                Core.LogMsg("[蛙哥] 5势力全正，声名狼藉已消");
                                StoreUIManager.Instance.Notify(LangHelper.T("5势力全正，声名狼藉已消！", "All factions positive, Infamous removed!"));
                                _cardSpawned = false;
                                CleanupCardIfGone();
                                try { if (CustomUIManager.Instance != null) CustomUIManager.Instance.CloseWindow("wage_rep_window"); } catch { }
                                try { if (CustomUIManager.Instance != null) CustomUIManager.Instance.CloseWindow("wage_bro_window"); } catch { }
                                return;
                            }
                            ShowRepFactionWindow(); // 刷新
                        }
                        catch (Exception ex) { Core.LogMsg("[蛙哥] 恢复声望异常: " + ex.Message); }
                    }));
                    w.AddButton(btnText, act, "wr_fac_" + i);
                }
                catch { }
            }
            // 10-05 引导 MVP：蛙哥指南按钮
            var wbGuideBtn2 = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((System.Action)(() => { try { ShowWageBrotherGuide(); } catch (Exception ex) { Core.LogMsg("[蛙哥] 指南异常: " + ex.Message); } }));
            w.AddButton(LangHelper.T("📖 蛙哥指南", "📖 Guide"), wbGuideBtn2, "wb_guide_btn");
            w.AddButton(LangHelper.T("关闭", "Close"), DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((System.Action)(() => { try { CustomUIManager.Instance.CloseWindow("wage_rep_window"); } catch { } })), "wr_close");
        }
        catch (Exception ex) { Core.LogMsg("[蛙哥] ShowRepFactionWindow异常: " + ex.Message); }
    }

    // 10-02 新增：检查5势力全≥0
    private static bool CheckAllFactionsPositive()
    {
        try
        {
            string[] factionIds = { "FACTION_SECURITY", "FACTION_UPPER_LEVEL", "FACTION_REVOLUTION", "FACTION_LOWER_LEVEL", "FACTION_BLACK_MARKET" };
            foreach (var fid in factionIds)
            {
                var rep = StoreReputation.GetStoreReputation(fid);
                if (rep == null) return false;
                if ((int)rep.GetReputationExact() < 0) return false;
            }
            return true;
        }
        catch { return false; }
    }

    // 服务卡双击 Prefix（OpenContentAction 识别 wage_bro_card）
    // 10-14 B2-R：移除自建服务面板入口，服务卡双击恢复原生打开（查看服务改走电话 1005 → 原生面板）
    internal static bool PrefixDoubleClickAction(Il2Cpp.GameItem newItem, UnityEngine.Vector2 mousePosition)
    {
        return true; // 全部放行原生（B2-R 不再拦截）
    }

    // 10-03 诊断辅助：安全读标签
    private static bool SafeIsTag(GameItem it, string tag)
    {
        try { return it.IsTag(tag); } catch { return false; }
    }

    // ===================== 合成升级（10-04 用户拍板：蛙哥服务合成） =====================
    // 规则：许可合成只补差价（20000/20000，10-15 价格重设计）；充电器合成补差价+原版涡轮/能量阵列/充能器（二级各1、三级2/5/5；差价 20000/25000）
    private const string CRAFT_TURBO_ID = "turbo_booster"; // 原版充电涡轮（游戏原生物品）
    private const string CRAFT_POWER_ARRAY_ID = "powerblock"; // 能量阵列（价值200实锤：all_item_values.txt:127 + 游戏日志"能量阵列(200)"）
    private const string CRAFT_RECHARGER_ID = "recharger"; // 充能器（价值150实锤：all_item_values.txt:126）

    // 材料显示名（不足提示用）
    private static string CraftMatName(string id)
    {
        if (id == CRAFT_TURBO_ID) return LangHelper.T("原版充电涡轮", "Turbo Booster");
        if (id == CRAFT_POWER_ARRAY_ID) return LangHelper.T("能量阵列", "Power Array");
        if (id == CRAFT_RECHARGER_ID) return LangHelper.T("充能器", "Recharger");
        return id;
    }

    // 递归收集容器内部物品（含嵌套容器；深度上限防环——吸取容器遍历炸/死循环教训，全 try-catch + null 守卫 + 索引访问）
    // 容器内部库存走 AddictOfficerEvent.GetInnerInventory（contentWindow反射+GetContainerGrid 双通道，拆包实锤 L1）
    private static void CollectContainerItemsRecursive(GameItem container, System.Collections.Generic.List<GameItem> outList, int depth)
    {
        if (container == null || depth > 8) return;
        try
        {
            var inner = AddictOfficerEvent.GetInnerInventory(container);
            if (inner == null || inner.childItems == null) return;
            for (int i = 0; i < inner.childItems.Count; i++)
            {
                GameItem c = null;
                try { c = inner.childItems[i]; } catch { continue; }
                if (c == null) continue;
                outList.Add(c);
                CollectContainerItemsRecursive(c, outList, depth + 1);
            }
        }
        catch { }
    }

    // 收集全当铺所有物品：EmporiumEntry 顶层 + 每个容器内部递归（GetAllItems 不递归容器内部——游戏API.md 实锤）
    // 10-10 #46 回滚修正：GetAllItems 原版含背包/柜台（拆包实锤 EmporiumEntry.txt:22621 [rsi+30h]invElement / :22661 [rsi+98h]backInvinvElement / :22702 [rsi+88h]frontInvinvElement）
    //   ——"不含背包"前提错误，10-10 早前补背包遍历=冗余且引入重复计数（CountAllPermits 双计→打烊加成翻倍）→ 已回滚。真实根因另查（id 比对/调用链/读档时序）。
    internal static System.Collections.Generic.List<GameItem> CollectAllItems()
    {
        var list = new System.Collections.Generic.List<GameItem>();
        try
        {
            var em = Il2Cpp.EmporiumEntry.Instance;
            if (em == null) return list;
            foreach (var item in em.GetAllItems())
            {
                if (item == null) continue;
                list.Add(item);
                CollectContainerItemsRecursive(item, list, 0);
            }
        }
        catch (System.Exception ex) { Core.LogMsg("[WageBrother] CollectAllItems 异常: " + ex.Message); } // 10-10 #46：静默 catch 补日志（与 #39 同型卫生，保留）
        return list;
    }

    // 是否归玩家所有：not_purchased 标签已清除 = 已购买/已拥有（MerchantHelper 经验 4922，防柜台白嫖）
    private static bool IsOwnedByPlayer(GameItem item)
    {
        if (item == null) return false;
        try { return !item.IsTag("not_purchased") && !item.IsTag("TAG_NOT_PURCHASED"); } catch { return false; }
    }

    // 统计玩家已持有的物品数（全容器递归）
    private static int CountOwnedItems(string itemId)
    {
        int n = 0;
        foreach (var item in CollectAllItems())
        {
            if (item == null) continue;
            string id = ""; try { id = item.identifier ?? ""; } catch { }
            if (id != itemId) continue;
            if (IsOwnedByPlayer(item)) n++;
        }
        return n;
    }

    private static GameItem FindOwnedItem(string itemId)
    {
        foreach (var item in CollectAllItems())
        {
            if (item == null) continue;
            string id = ""; try { id = item.identifier ?? ""; } catch { }
            if (id != itemId) continue;
            if (IsOwnedByPlayer(item)) return item;
        }
        return null;
    }

    // 执行合成：低级物品 → 高级物品（materials = 需求材料列表 (id, 数量)；许可传空表）
    private static void DoCraft(string lowId, string highId, int diff, System.Collections.Generic.List<System.Tuple<string, int>> materials)
    {
        try
        {
            var ps = PlayerStore.Instance; if (ps == null) return;
            // 1. 必须持有低级（已购买，非柜台待售）
            GameItem low = FindOwnedItem(lowId);
            if (low == null) { StoreUIManager.Instance.Notify(LangHelper.T("需要先持有低一级的货（买过才行）", "Need lower tier item (purchased)")); return; }
            // 2. 现金够差价
            if (ps.playerCash < diff) { StoreUIManager.Instance.Notify(LangHelper.T("差价不够：" + diff + " 信用点", "Need " + diff + " cr")); return; }
            // 3. 材料够（充电器才要材料；缺哪样提示哪样，全容器递归统计）
            if (materials != null)
            {
                foreach (var m in materials)
                {
                    int owned = CountOwnedItems(m.Item1);
                    if (owned < m.Item2)
                    {
                        string matName = CraftMatName(m.Item1);
                        StoreUIManager.Instance.Notify(LangHelper.T(matName + "不够（需 " + m.Item2 + " 个）", "Need " + m.Item2 + " " + m.Item1));
                        return;
                    }
                }
            }
            // 4. 扣差价
            ps.playerCash -= diff;
            // 5. 销毁低级物品（10-05 用户实测：充电器升级"把里面电池全收走"——Destroy 连带销毁内部子物品；
            // 先取出内部物品 Expel 回主背包再销毁，通用防丢失）
            try
            {
                var inner = new System.Collections.Generic.List<GameItem>();
                CollectContainerItemsRecursive(low, inner, 0);
                if (inner.Count > 0)
                {
                    var em = Il2Cpp.EmporiumEntry.Instance;
                    if (em != null && em.invElement != null)
                    {
                        var toKeep = new Il2CppSystem.Collections.Generic.List<GameItem>();
                        foreach (var c in inner)
                        {
                            if (c == null) continue;
                            try { c.parentInventory?.Expel(c); } catch { }
                            toKeep.Add(c);
                        }
                        if (toKeep.Count > 0)
                        {
                            try { em.invElement.UncheckedAcceptAll(toKeep); Core.LogMsg("[蛙哥] 合成前取出低级内部物品 " + toKeep.Count + " 个"); } catch (System.Exception ex) { Core.LogMsg("[蛙哥] 取出内部物品异常: " + ex.Message); }
                        }
                    }
                }
            }
            catch { }
            try { low.Destroy(); } catch (Exception exd) { Core.LogMsg("[蛙哥] 合成销毁低级异常: " + exd.Message); }
            // 6. 销毁材料（全容器递归找，逐材料销毁足额）
            if (materials != null)
            {
                foreach (var m in materials)
                {
                    int need = m.Item2;
                    foreach (var item in CollectAllItems())
                    {
                        if (need <= 0) break;
                        if (item == null) continue;
                        string id = ""; try { id = item.identifier ?? ""; } catch { }
                        if (id != m.Item1) continue;
                        if (!IsOwnedByPlayer(item)) continue;
                        try { item.Destroy(); need--; } catch { }
                    }
                }
            }
            // 7. 发放高级物品（isOwend=true 直接归玩家）
            var high = DirectoryMaster.Item(highId, true);
            if (high != null)
            {
                try
                {
                    high.DisableTag("not_purchased", true);
                    high.DisableTag("TAG_NOT_PURCHASED", true);
                    high.EnableTag("IS_OWNED_TAG", true);
                    ps.AddDirectSellingItemToTable(high, true, false, false, 0);
                }
                catch (Exception exh) { Core.LogMsg("[蛙哥] 合成发放高级异常: " + exh.Message); }
            }
            StoreUIManager.Instance.Notify(LangHelper.T("合成完成：" + highId + " 已入账", "Crafted: " + highId));
            string matLog = "";
            if (materials != null) { foreach (var m in materials) matLog += " " + m.Item1 + "x" + m.Item2; }
            Core.LogMsg("[蛙哥] 合成: " + lowId + " → " + highId + " 扣" + diff + " 材料:" + matLog);
            // 关合成窗口
            try { if (CustomUIManager.Instance != null) CustomUIManager.Instance.CloseWindow("wage_craft_window"); } catch { }
        }
        catch (Exception ex) { Core.LogMsg("[蛙哥] DoCraft异常: " + ex.Message); }
    }

    // 合成升级窗口
    internal static void ShowCraftWindow()
    {
        try
        {
            var mgr = CustomUIManager.Instance; if (mgr == null) return;
            if (mgr.IsOpen("wage_craft_window")) mgr.CloseWindow("wage_craft_window");
            var w = mgr.CreateWindow("wage_craft_window", LangHelper.T("蛙哥 · 合成升级", "Wage Brother · Craft Upgrade"), "overlay");
            if (w == null) return;
            w.SetSize(380, 440).SetPosition(Vector2.zero);
            w.BeginColumn(4f);
            w.AddLabel(LangHelper.T("蛙哥：把低一级的货交给我，补差价帮你升一级。充电器升级还要原版充电涡轮、能量阵列和充能器。", "Wage Brother: give me the lower tier, pay the difference to upgrade. Chargers also need turbo, power array and recharger."), "wc_hint");
            var ps = PlayerStore.Instance;
            w.AddLabel(LangHelper.T("当前现金：" + ps.playerCash, "Cash: " + ps.playerCash), "wc_cash");

            // 许可（只补差价）
            var actP2 = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((System.Action)(() => { try { DoCraft(WageBrokerPermitHelper.PERMIT_1_ID, WageBrokerPermitHelper.PERMIT_2_ID, 20000, null); } catch (Exception ex) { Core.LogMsg("[蛙哥] 许可合成异常: " + ex.Message); } }));
            w.AddButton(LangHelper.T("蛙哥的许可 → 二级 · 20000信用点", "Permit → Tier 2 · 20000 cr"), actP2, "wc_permit2");
            var actP3 = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((System.Action)(() => { try { DoCraft(WageBrokerPermitHelper.PERMIT_2_ID, WageBrokerPermitHelper.PERMIT_3_ID, 20000, null); } catch (Exception ex) { Core.LogMsg("[蛙哥] 许可合成异常: " + ex.Message); } }));
            w.AddButton(LangHelper.T("蛙哥的许可 → 三级 · 20000信用点", "Permit → Tier 3 · 20000 cr"), actP3, "wc_permit3");

            // 充电器（差价+原版涡轮+能量阵列+充能器；二级各1，三级各5）
            var c2Mats = new System.Collections.Generic.List<System.Tuple<string, int>>
            {
                System.Tuple.Create(CRAFT_TURBO_ID, 1),
                System.Tuple.Create(CRAFT_POWER_ARRAY_ID, 1),
                System.Tuple.Create(CRAFT_RECHARGER_ID, 1)
            };
            var actC2 = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((System.Action)(() => { try { DoCraft(WageBrokerChargerHelper.CHARGER_1_ID, WageBrokerChargerHelper.CHARGER_2_ID, 20000, c2Mats); } catch (Exception ex) { Core.LogMsg("[蛙哥] 充电器合成异常: " + ex.Message); } }));
            w.AddButton(LangHelper.T("蛙哥的充电器 → 二级 · 20000信用点 + 涡轮×1 + 阵列×1 + 充能器×1", "Charger → Tier 2 · 20000 cr + turbo x1 + array x1 + recharger x1"), actC2, "wc_charger2");
            var c3Mats = new System.Collections.Generic.List<System.Tuple<string, int>>
            {
                System.Tuple.Create(CRAFT_TURBO_ID, 2),
                System.Tuple.Create(CRAFT_POWER_ARRAY_ID, 5),
                System.Tuple.Create(CRAFT_RECHARGER_ID, 5)
            };
            var actC3 = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((System.Action)(() => { try { DoCraft(WageBrokerChargerHelper.CHARGER_2_ID, WageBrokerChargerHelper.CHARGER_3_ID, 25000, c3Mats); } catch (Exception ex) { Core.LogMsg("[蛙哥] 充电器合成异常: " + ex.Message); } }));
            w.AddButton(LangHelper.T("蛙哥的充电器 → 三级 · 25000信用点 + 涡轮×2 + 阵列×5 + 充能器×5", "Charger → Tier 3 · 25000 cr + turbo x2 + array x5 + recharger x5"), actC3, "wc_charger3");
            // 10-05 引导 MVP：蛙哥指南按钮
            var wbGuideBtn3 = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((System.Action)(() => { try { ShowWageBrotherGuide(); } catch (Exception ex) { Core.LogMsg("[蛙哥] 指南异常: " + ex.Message); } }));
            w.AddButton(LangHelper.T("📖 蛙哥指南", "📖 Guide"), wbGuideBtn3, "wb_guide_btn");
        }
        catch (Exception ex) { Core.LogMsg("[蛙哥] ShowCraftWindow异常: " + ex.Message); }
    }

    // ============================================================
    // 10-05 引导 MVP：蛙哥总览指南（复制蛙娘照顾指南 wg_guide 模式）
    // 入口：消业障/声望/合成 3 窗口「📖 蛙哥指南」按钮
    // ============================================================
    internal static void ShowWageBrotherGuide()
    {
        try
        {
            var mgr = CustomUIManager.Instance; if (mgr == null) return;
            if (mgr.IsOpen("wb_guide")) mgr.CloseWindow("wb_guide");
            var w = mgr.CreateWindow("wb_guide", LangHelper.T("蛙哥 · 服务指南", "Wage Brother · Service Guide"), "overlay");
            if (w == null) return;
            w.SetSize(420, 500).SetPosition(Vector2.zero);
            try
            {
                var gw = mgr.GetWindow("wb_guide");
                if (gw != null)
                {
                    var rt = gw.Rect;
                    rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1, 1);
                    rt.anchoredPosition = new Vector2(-16, -200);
                }
            }
            catch (System.Exception ex) { Core.LogMsg("[蛙哥] 指南定位异常: " + ex.Message); }
            // §4（2026-10-18，Q2 实锤后）：指南改滚动容器（BeginScroll 440=窗口高500-60 底部区，为关闭按钮留 60；End 与 Begin 配对）
            w.BeginScroll(440f);
            w.BeginColumn(4f);
            w.AddLabel(LangHelper.T("【1】蛙哥是谁？", "[1] Who is Wage Brother?"), "wb_g1");
            PanelZoomHelper.SizeGuideLabel(w, 380f); // §4：滚动条目撑开高度
            w.AddLabel(LangHelper.T("他定期到店拜访，柜台出现服务卡（消业障/声望恢复/合成升级）。第一次见面送名片：电话拨打 1005 打开「蛙哥人脉网络」——蛙哥服务面板所在地。", "He visits periodically; a service card appears at the counter (remove burden / rep restore / craft). First meeting gives a business card: dial 1005 to open the Wage Brother Network - his service panel."), "wb_g1d");
            PanelZoomHelper.SizeGuideLabel(w, 380f); // §4：滚动条目撑开高度
            w.AddLabel(LangHelper.T("【2】服务面板（电话簿 1005）", "[2] Service Panel (Phone 1005)"), "wb_g2");
            PanelZoomHelper.SizeGuideLabel(w, 380f); // §4：滚动条目撑开高度
            w.AddLabel(LangHelper.T("解锁条件：蛙哥牛逼 Lv2 + 蛙娘好感≥40（面板顶部横幅看好感）。服务：流浪者（重回第一天、刷新物品）；养蛊机/AI生成器（解锁后电话购买）；保护器（随机出现，可花钱加概率）；许可/充电器 1→3 级直购（三级充电器几万级）。", "Unlock: Wage Brother Power Lv2 + Wage Girl affection >= 40 (banner shows affection). Services: Wanderer (reset to day 1, refresh items); Gu Machine / AI Generator (buy by phone after unlock); Protector (random spawn, pay to raise chance); Permit/Charger T1->T3 direct buy (T3 charger costs tens of thousands)."), "wb_g2d");
            PanelZoomHelper.SizeGuideLabel(w, 380f); // §4：滚动条目撑开高度
            w.AddLabel(LangHelper.T("【3】消业障", "[3] Remove Burden"), "wb_g3");
            PanelZoomHelper.SizeGuideLabel(w, 380f); // §4：滚动条目撑开高度
            w.AddLabel(LangHelper.T("消除负面特性（B2 面板负面卡或到店服务卡）：按 Cost 计价 -20→8000、-15→5000、-7→2000、其余 500；招贼体质固定 5000；声名狼藉 5 势力逐个恢复，全部转正才消除。", "Remove a negative perk (B2 panel negative card or counter card): by Cost -20->8000, -15->5000, -7->2000, others 500; Thief Magnet fixed 5000; Infamous restores all 5 factions, clears only when all are positive."), "wb_g3d");
            PanelZoomHelper.SizeGuideLabel(w, 380f); // §4：滚动条目撑开高度
            w.AddLabel(LangHelper.T("【4】合成升级", "[4] Craft Upgrade"), "wb_g4");
            PanelZoomHelper.SizeGuideLabel(w, 380f); // §4：滚动条目撑开高度
            w.AddLabel(LangHelper.T("把低一级的货交给他补差价升级：许可 20000/20000 信用点；充电器 20000/25000 + 涡轮/能量阵列/充能器（二级各 1、三级 2/5/5）。", "Give him the lower tier and pay the difference: Permit 20000/20000 cr; Charger 20000/25000 cr + turbo/power array/recharger (T2: 1 each, T3: 2/5/5)."), "wb_g4d");
            PanelZoomHelper.SizeGuideLabel(w, 380f); // §4：滚动条目撑开高度
            PanelZoomHelper.FinishGuideScroll(w, 4f); // 10-18：content 高按子项实高定死 + 补 CFS（否则滚不动）
            w.End(); // 退出 column（§4）
            w.End(); // 退出 scroll（§4）
            // §蛙哥布局（第五轮 20261018）：关闭按钮必须在滚动区外，否则随内容滚走被 RectMask2D 裁掉
            w.AddButton(LangHelper.T("关闭", "Close"), DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((System.Action)(() => { try { CustomUIManager.Instance.CloseWindow("wb_guide"); } catch { } })), "wb_guide_close");
        }
        catch (Exception ex) { Core.LogMsg("[蛙哥] 指南异常: " + ex.Message); }
    }
}
