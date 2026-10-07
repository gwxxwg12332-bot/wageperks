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
    // 10-07 C2-1/C2-2：名片/柜台货"该在"的状态随档键（对标小偷 KEY_THEFT_DAY 成功方案——
    //   柜台不随档+原一次性 180 帧补货超时=读档后柜台永远空；此键=持久愿望：上货成功置 1、清卡置 0，读档后按愿望重建柜台）
    private const string CARD_SPAWNED_KEY = "card_spawned";

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
    // 服务卡工厂：只建物品（不含上柜台；上柜台走 OnClientArrived）
    internal static GameItem CreateCard()
    {
        try
        {
            LoadCardSprite();
            GameItem card = ItemDirectory.CreateEmptyItem(null); // 无参构造不存在，照养蛊机CreateGuMachine:79
            card.identifier = CARD_ID;
            card.SetName("蛙哥名片·消perk 500~8000");
            card.shortDescription = LangHelper.T("双击：花信用点消除一项负面特性。费用按Cost分档：轻微500·较重2000·重5000·极重8000。招贼体质固定5000。消除声名狼藉时每势力另收3000。", "Double-click: pay credits to remove a negative perk. Fees by severity: 500/2000/5000/8000. Thief Magnet: 5000 fixed. Removing Infamous: +3000 per faction.");
            var gsb = new GridShapeBuilder(); gsb.SetDataFill(2, 1); card.SetShape(gsb.Build()); card.modifiedShape = gsb.Build();
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
            // 10-07 防两套（玩家"蛙哥没小退时刷了两套物品"）：OnClientArrived 无内部防重，正常到店事件链 + 读档轮询兜底
            //   可双触发（各自带防重互不感知）→ 全套上货两次。_cardSpawned=本会话已上货标志（加卡成功才置 true，
            //   蛙哥走后 CleanupCardIfGone 重置）→ 重复调用直接跳过并清轮询愿望；上货失败（_cardSpawned 未置）仍可重试。
            if (_cardSpawned)
            {
                _refillPending = false;
                Core.LogMsg("[蛙哥] OnClientArrived: 本会话已上过货，防重复跳过（清轮询愿望）");
                return;
            }
            try { if (client != null) { client.SetBudget(1109707341, 1000); client.clientIntent = StoreClient.ClientIntent.SELLNBUY; } } catch { } Core.LogMsg("[蛙哥] intent已设=" + (client != null ? client.clientIntent.ToString() : "null(上货模式)"));
            // 10-05 蛙哥收购扩展（拆包实锤：clientBuyingIdList id精确 + clientBuyingTagList tag精确 + SELLNBUY 买路径）
            // 苦力boy反馈"水卖谁啊"：蛙哥收购 水/电池/模组/日用品；预算 100→1000（拆包③建议500-1000，单品全覆盖+可收2-3件）
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
            try { LoadPortrait(); client.spriteName = "wage_brother_portrait"; try { client.possibleSprites.Clear(); client.possibleSprites.Add("wage_brother_portrait"); } catch { } try { if (StoreClientMono.Instance != null && StoreClientMono.Instance.image != null && _portrait != null) { StoreClientMono.Instance.image.sprite = _portrait; Core.LogMsg("[蛙哥] 立绘已刷"); } } catch (System.Exception exr) { Core.LogMsg("[蛙哥] 刷立绘异常: " + exr.Message); } } catch { } Core.LogMsg("[蛙哥] 立绘注入完成");
            // 不依赖门控，每次到场都加卡（柜台有卡则原生去重）`r`n            Core.LogMsg("[蛙哥] _cardSpawned=" + _cardSpawned + " 强制加卡");
            var ps = PlayerStore.Instance; if (ps == null) return;
            GameItem card = null;
            try { card = DirectoryMaster.Item(CARD_ID); Core.LogMsg("[蛙哥] DirectoryMaster("+CARD_ID+")=" + (card!=null?"ok":"null")); } catch (Exception ex) { Core.LogMsg("[蛙哥] 创建CARD_ID异常: " + ex.Message); }
            if (card == null)
            {
                // 未注册物品 fallback：用 cassette_player 占位
                try { card = DirectoryMaster.Item("cassette_player", true); Core.LogMsg("[蛙哥] cassette_player fallback=" + (card!=null?"ok":"null")); if (card != null) card.identifier = CARD_ID; } catch (Exception ex) { Core.LogMsg("[蛙哥] cassette_player异常: " + ex.Message); }
            }
            if (card != null)
            {
                try { LoadCardSprite(); Core.LogMsg("[蛙哥] 图标加载完成, _cardSprite=" + (_cardSprite != null)); card.SetName("蛙哥名片·消perk 500~8000"); card.shortDescription = LangHelper.T("双击：花信用点消除一项负面特性。费用按Cost分档：轻微500·较重2000·重5000·极重8000。招贼体质固定5000。消除声名狼藉时每势力另收3000。", "Double-click: pay credits to remove a negative perk. Fees by severity: 500/2000/5000/8000. Thief Magnet: 5000 fixed. Removing Infamous: +3000 per faction."); var gsb = new GridShapeBuilder(); gsb.SetDataFill(2, 1); card.SetShape(gsb.Build()); card.modifiedShape = gsb.Build(); card.SetSprite("custom_atlas", CARD_SPRITE_KEY); card.unitValue = 0; card.unitBaseValue = 0; // 名片不能卖
                    try { card.EnableTag("paper", true); } catch { } // 文档属性标签（销赃时不带走）
                } catch (System.Exception exload) { Core.LogMsg("[蛙哥] LoadCardSprite异常: " + exload.Message); }
                try { card.EnableTag("wage_bro_card", true); } catch { } try { var d = client.mainDialogue; if (d != null) { d.SetText("蛙哥", LangHelper.T("我来收点晦气。花信用点消一项负面特性，钱货两清。", "I collect trouble. Pay credits to remove a negative perk.")); d.endAction = null; if (d.nextDialogue != null) { d.nextDialogue.endAction = null; d.nextDialogue = null; } } } catch (System.Exception exd) { Core.LogMsg("[蛙哥] 清对话链异常: " + exd.Message); } Core.LogMsg("[蛙哥] 准备加卡: card=" + card.identifier); try { card.DisableTag("not_purchased", true); card.DisableTag("TAG_NOT_PURCHASED", true); card.EnableTag("IS_OWNED_TAG", true); PlayerStore.Instance.AddDirectSellingItemToTable(card, true, false, false, 0); card.DisableTag("not_purchased", true); card.EnableTag("IS_OWNED_TAG", true); Core.LogMsg("[蛙哥] 加卡调用返回,无异常"); } catch (System.Exception excard) { Core.LogMsg("[蛙哥] 服务卡上柜台异常: " + excard.Message); }
                
                _cardSpawned = true;
                // 10-07 C2-1/C2-2：名片/柜台货状态随档+立即落盘（对标小偷 KEY_THEFT_DAY——小退=进程退出，
                //   WageSaveStore 只在 SaveGame/EndDay Flush → 不 Flush 重进读文件=旧值=柜台空）
                try { WageSaveStore.SetInt("wage_brother", CARD_SPAWNED_KEY, 1); WageSaveStore.Flush(); } catch (System.Exception exf) { Core.LogMsg("[蛙哥] 名片状态落盘失败: " + exf.Message); }
                Core.LogMsg("[蛙哥] 到场，服务卡已上柜台");
            }
            // 小概率携带售卖AI制造机/养蛊机/保护器
            try
            {
                if (Core.Rng.Next(100) < 50)
                {
                    var aiGen = DirectoryMaster.Item("wage_ai_generator", true);
                    if (aiGen != null)
                    {
                        aiGen.DisableTag("not_purchased", true);
                        aiGen.EnableTag("IS_OWNED_TAG", true);
                        PlayerStore.Instance.AddDirectSellingItemToTable(aiGen, false, false, false, 0);
                        Core.LogMsg("[蛙哥] 携带AI制造机售卖");
                    }
                }
                if (Core.Rng.Next(100) < 30)
                {
                    var guMachine = DirectoryMaster.Item("wage_gu_machine", true);
                    if (guMachine != null)
                    {
                        guMachine.DisableTag("not_purchased", true);
                        guMachine.EnableTag("IS_OWNED_TAG", true);
                        PlayerStore.Instance.AddDirectSellingItemToTable(guMachine, false, false, false, 0);
                        Core.LogMsg("[蛙哥] 携带养蛊机售卖");
                    }
                }
                if (Core.Rng.Next(100) < 40)
                {
                    var protector = DirectoryMaster.Item("wage_protector_core", true);
                    if (protector != null)
                    {
                        protector.DisableTag("not_purchased", true);
                        protector.EnableTag("IS_OWNED_TAG", true);
                        PlayerStore.Instance.AddDirectSellingItemToTable(protector, false, false, false, 0);
                        Core.LogMsg("[蛙哥] 携带保护器售卖");
                    }
                }
            }
            catch (Exception exai) { Core.LogMsg("[蛙哥] 携带物品上柜台异常: " + exai.Message); }

            // 蛙哥的许可货架（10-04 用户拍板：一次只卖初级；阶梯解锁曾因HasPermit扫Emporium全量含货架→连锁全开，先停用等成长机制确认）
            try
            {
                AddPermitToCounter(WageBrokerPermitHelper.PERMIT_1_ID, 800); // 只卖一级
                // if (WageBrokerPermitHelper.HasPermit(1))
                //     AddPermitToCounter(WageBrokerPermitHelper.PERMIT_2_ID, 1500);
                // if (WageBrokerPermitHelper.HasPermit(2))
                //     AddPermitToCounter(WageBrokerPermitHelper.PERMIT_3_ID, 2500);
                Core.LogMsg("[蛙哥] 许可货架已上（仅一级），等级=" + WageBrokerPermitHelper.GetMaxPermitLevel());
            }
            catch (Exception expermit) { Core.LogMsg("[蛙哥] 许可货架异常: " + expermit.Message); }

            // 蛙哥的充电器货架（10-04 同上：一次只卖初级）
            try
            {
                AddChargerToCounter(WageBrokerChargerHelper.CHARGER_1_ID, 500); // 只卖一级
                // if (WageBrokerChargerHelper.HasCharger(1))
                //     AddChargerToCounter(WageBrokerChargerHelper.CHARGER_2_ID, 1000);
                // if (WageBrokerChargerHelper.HasCharger(2))
                //     AddChargerToCounter(WageBrokerChargerHelper.CHARGER_3_ID, 2000);
                Core.LogMsg("[蛙哥] 充电器货架已上（仅一级），等级=" + WageBrokerChargerHelper.GetMaxChargerLevel());
            }
            catch (Exception excharger) { Core.LogMsg("[蛙哥] 充电器货架异常: " + excharger.Message); }

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
            w.AddLabel(LangHelper.T("蛙哥：花钱消个负面特性。钱货两清。", "Wage Brother: pay to remove a negative perk. No refunds."), "wb_hint");
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
            foreach (var perk in CustomStartingPerks.All)
            {
                try
                {
                    if (perk.Cost >= 0) continue;
                    if (!StartingPerk.IsPerkActive(perk.Id)) continue;
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
    private static void ShowRepFactionWindow()
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
    internal static bool PrefixDoubleClickAction(Il2Cpp.GameItem newItem, UnityEngine.Vector2 mousePosition)
    {
        try
        {
            GameItem item = newItem; if (item == null) return true;
            bool isCard = false; try { isCard = item.IsTag("wage_bro_card"); } catch { }
            if (!isCard) return true; // 不是服务卡，放行原生
            if (PlayerStore.Instance == null) return false;
            ShowRemovePerkWindow();
            return false; // 拦截原生打开
        }
        catch { return true; }
    }

    // 10-03 诊断辅助：安全读标签
    private static bool SafeIsTag(GameItem it, string tag)
    {
        try { return it.IsTag(tag); } catch { return false; }
    }

    // ===================== 合成升级（10-04 用户拍板：蛙哥服务合成） =====================
    // 规则：许可合成只补差价（700/1000）；充电器合成补差价+原版充电涡轮（500+1涡轮 / 1000+2涡轮）
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
        catch { }
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
            var actP2 = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((System.Action)(() => { try { DoCraft(WageBrokerPermitHelper.PERMIT_1_ID, WageBrokerPermitHelper.PERMIT_2_ID, 700, null); } catch (Exception ex) { Core.LogMsg("[蛙哥] 许可合成异常: " + ex.Message); } }));
            w.AddButton(LangHelper.T("蛙哥的许可 → 二级 · 700信用点", "Permit → Tier 2 · 700 cr"), actP2, "wc_permit2");
            var actP3 = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((System.Action)(() => { try { DoCraft(WageBrokerPermitHelper.PERMIT_2_ID, WageBrokerPermitHelper.PERMIT_3_ID, 1000, null); } catch (Exception ex) { Core.LogMsg("[蛙哥] 许可合成异常: " + ex.Message); } }));
            w.AddButton(LangHelper.T("蛙哥的许可 → 三级 · 1000信用点", "Permit → Tier 3 · 1000 cr"), actP3, "wc_permit3");

            // 充电器（差价+原版涡轮+能量阵列+充能器；二级各1，三级各5）
            var c2Mats = new System.Collections.Generic.List<System.Tuple<string, int>>
            {
                System.Tuple.Create(CRAFT_TURBO_ID, 1),
                System.Tuple.Create(CRAFT_POWER_ARRAY_ID, 1),
                System.Tuple.Create(CRAFT_RECHARGER_ID, 1)
            };
            var actC2 = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((System.Action)(() => { try { DoCraft(WageBrokerChargerHelper.CHARGER_1_ID, WageBrokerChargerHelper.CHARGER_2_ID, 500, c2Mats); } catch (Exception ex) { Core.LogMsg("[蛙哥] 充电器合成异常: " + ex.Message); } }));
            w.AddButton(LangHelper.T("蛙哥的充电器 → 二级 · 500信用点 + 涡轮×1 + 阵列×1 + 充能器×1", "Charger → Tier 2 · 500 cr + turbo x1 + array x1 + recharger x1"), actC2, "wc_charger2");
            var c3Mats = new System.Collections.Generic.List<System.Tuple<string, int>>
            {
                System.Tuple.Create(CRAFT_TURBO_ID, 2),
                System.Tuple.Create(CRAFT_POWER_ARRAY_ID, 5),
                System.Tuple.Create(CRAFT_RECHARGER_ID, 5)
            };
            var actC3 = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((System.Action)(() => { try { DoCraft(WageBrokerChargerHelper.CHARGER_2_ID, WageBrokerChargerHelper.CHARGER_3_ID, 1000, c3Mats); } catch (Exception ex) { Core.LogMsg("[蛙哥] 充电器合成异常: " + ex.Message); } }));
            w.AddButton(LangHelper.T("蛙哥的充电器 → 三级 · 1000信用点 + 涡轮×2 + 阵列×5 + 充能器×5", "Charger → Tier 3 · 1000 cr + turbo x2 + array x5 + recharger x5"), actC3, "wc_charger3");
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
            w.BeginColumn(4f);
            w.AddLabel(LangHelper.T("【1】蛙哥是谁？", "[1] Who is Wage Brother?"), "wb_g1");
            w.AddLabel(LangHelper.T("他会定期到店拜访，柜台出现服务卡。双击服务卡打开他的服务界面：消业障、声望恢复、合成升级。", "He visits periodically; a service card appears at the counter. Double-click it to open his services: remove burdens, restore rep, craft upgrades."), "wb_g1d");
            w.AddLabel(LangHelper.T("【2】消业障", "[2] Remove Burden"), "wb_g2");
            w.AddLabel(LangHelper.T("花钱消除已选负面特性（按 Cost 计价，如霉运缠身 -10=2000 块）。声名狼藉特殊：5 势力逐个恢复，全部转正才消除。", "Pay to remove an active negative perk (priced by Cost, e.g. Bad Luck -10 = 2000 cr). Infamous is special: restore all 5 factions, it clears only when all are positive."), "wb_g2d");
            w.AddLabel(LangHelper.T("【3】声望恢复", "[3] Rep Restore"), "wb_g3");
            w.AddLabel(LangHelper.T("每个负势力 3000 块恢复至 0。5 势力全部转正自动消除声名狼藉。", "Restore any negative faction rep to 0 for 3000 cr each. All 5 positive = Infamous auto-removed."), "wb_g3d");
            w.AddLabel(LangHelper.T("【4】合成升级", "[4] Craft Upgrade"), "wb_g4");
            w.AddLabel(LangHelper.T("把低一级的货交给他补差价升级：许可 700/1000 块；充电器 500/1000 块 + 涡轮/能量阵列/充能器（二级各 1、三级 2/5/5）。", "Give him the lower tier and pay the difference: Permit 700/1000 cr; Charger 500/1000 cr + turbo/power array/recharger (T2: 1 each, T3: 2/5/5)."), "wb_g4d");
            w.AddButton(LangHelper.T("关闭", "Close"), DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((System.Action)(() => { try { CustomUIManager.Instance.CloseWindow("wb_guide"); } catch { } })), "wb_guide_close");
        }
        catch (Exception ex) { Core.LogMsg("[蛙哥] 指南异常: " + ex.Message); }
    }
}
