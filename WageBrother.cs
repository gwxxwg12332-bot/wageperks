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
            card.shortDescription = LangHelper.T("双击：花信用点消除一项负面特性。费用按Cost分档：轻微500·较重2000·重5000·极重8000。消除声名狼藉时每势力另收3000。", "Double-click: pay credits to remove a negative perk. Fees by severity: 500/2000/5000/8000. Removing Infamous: +3000 per faction.");
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

    // 蛙哥到场 → 柜台生成服务卡
    internal static void OnClientArrived(StoreClient client)
    {
        try
        {
            if (client == null || client.identifier != CLIENT_ID) { if (Core.DebugMode) Core.LogMsg("[蛙哥] OnClientArrived: identifier=" + (client!=null?client.identifier:"null")+" 不是蛙哥,跳过"); return; } Core.LogMsg("[蛙哥] OnClientArrived 入口, client=" + client.identifier);
            try { client.SetBudget(1109707341, 100); client.clientIntent = StoreClient.ClientIntent.SELLNBUY; } catch { } Core.LogMsg("[蛙哥] intent已设=" + client.clientIntent);
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
                try { LoadCardSprite(); Core.LogMsg("[蛙哥] 图标加载完成, _cardSprite=" + (_cardSprite != null)); card.SetName("蛙哥名片·消perk 500~8000"); card.shortDescription = LangHelper.T("双击：花信用点消除一项负面特性。费用按Cost分档：轻微500·较重2000·重5000·极重8000。消除声名狼藉时每势力另收3000。", "Double-click: pay credits to remove a negative perk. Fees by severity: 500/2000/5000/8000. Removing Infamous: +3000 per faction."); var gsb = new GridShapeBuilder(); gsb.SetDataFill(2, 1); card.SetShape(gsb.Build()); card.modifiedShape = gsb.Build(); card.SetSprite("custom_atlas", CARD_SPRITE_KEY); card.unitValue = 0; card.unitBaseValue = 0; // 名片不能卖
                    try { card.EnableTag("paper", true); } catch { } // 文档属性标签（销赃时不带走）
                } catch (System.Exception exload) { Core.LogMsg("[蛙哥] LoadCardSprite异常: " + exload.Message); }
                try { card.EnableTag("wage_bro_card", true); } catch { } try { var d = client.mainDialogue; if (d != null) { d.SetText("蛙哥", LangHelper.T("我来收点晦气。花信用点消一项负面特性，钱货两清。", "I collect trouble. Pay credits to remove a negative perk.")); d.endAction = null; if (d.nextDialogue != null) { d.nextDialogue.endAction = null; d.nextDialogue = null; } } } catch (System.Exception exd) { Core.LogMsg("[蛙哥] 清对话链异常: " + exd.Message); } Core.LogMsg("[蛙哥] 准备加卡: card=" + card.identifier); try { card.DisableTag("not_purchased", true); card.DisableTag("TAG_NOT_PURCHASED", true); card.EnableTag("IS_OWNED_TAG", true); PlayerStore.Instance.AddDirectSellingItemToTable(card, true, false, false, 0); card.DisableTag("not_purchased", true); card.EnableTag("IS_OWNED_TAG", true); Core.LogMsg("[蛙哥] 加卡调用返回,无异常"); } catch (System.Exception excard) { Core.LogMsg("[蛙哥] 服务卡上柜台异常: " + excard.Message); }
                
                _cardSpawned = true;
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
            var asm = System.Reflection.Assembly.GetExecutingAssembly();
            string resName = null;
            foreach (var n in asm.GetManifestResourceNames()) if (n.EndsWith("wage_brother_card.png")) { resName = n; break; }
            if (resName == null) { Core.LogMsg("[蛙哥] 立绘资源未找到"); return; }
            using var st = asm.GetManifestResourceStream(resName);
            byte[] png = new byte[st.Length]; st.Read(png, 0, png.Length);
            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point; tex.wrapMode = TextureWrapMode.Clamp; tex.mipMapBias = 0;
            Type icType = null;
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies()) { Type[] ts; try { ts = a.GetTypes(); } catch (System.Reflection.ReflectionTypeLoadException ex) { ts = ex.Types; } foreach (var t in ts) if (t != null && t.Name == "ImageConversion") { icType = t; break; } if (icType != null) break; }
            if (icType == null) { Core.LogMsg("[蛙哥] ImageConversion未就绪，等下次再试"); return; } // 启动早期时序问题：PrefixLoadFromAtlas会反复调用
            Core.LogMsg("[蛙哥] 资源=" + resName + " size=" + png.Length + " icType=" + (icType!=null?icType.FullName:"null")); Core.LogMsg("[蛙哥] LoadImage前"); icType.GetMethod("LoadImage", new Type[] { typeof(Texture2D), typeof(Il2CppStructArray<byte>) }).Invoke(null, new object[] { tex, (Il2CppStructArray<byte>)png });
            Core.LogMsg("[蛙哥] LoadImage后 tex=" + tex.width + "x" + tex.height); _cardSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 500f);
            // 10-03 修：服务卡sprite必须进SpriteDict（读档重建渲染查字典，仅静态字段=旧档显示问号；立绘/许可同模式）
            try { if (SpriteDict.Instance != null && SpriteDict.Instance.spriteDictionary != null && _cardSprite != null) { SpriteDict.Instance.spriteDictionary[CARD_SPRITE_KEY] = _cardSprite; Core.LogMsg("[蛙哥] 服务卡sprite已注入SpriteDict"); } else { Core.LogMsg("[蛙哥] SpriteDict未就绪，等服务卡渲染兜底重写"); } } catch (System.Exception exdict) { Core.LogMsg("[蛙哥] 服务卡注入SpriteDict异常: " + exdict.Message); }
            Core.LogMsg("[蛙哥] 服务卡图标加载 " + tex.width + "x" + tex.height); // atlasCache直写注释：索引器导致卡死，走PrefixLoadFromAtlas拦截
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

    // 10-04 贴图重制：许可/充电器全部 64x64 POT（对齐养蛊机），ppu=200→显示0.32单位=游戏2×2占格原生标准（cheatsheet: PPU=图宽/(占格×0.16)=64/0.32=200）
    private static float GetIconPPU(string spriteKey)
    {
        return 200f; // 64px→0.32单位（2×2物品原生标准）
    }

    // 加载许可/充电器图标到SpriteDict
    private static void LoadPermitChargerIcon(string fileName, string spriteKey)
    {
        try
        {
            if (SpriteDict.Instance == null) { Core.LogMsg("[蛙哥] SpriteDict未就绪，跳过预加载(懒加载兜底): " + spriteKey); return; } // 10-03 启动早期时序守卫（原预加载NRE根因）
            var asm = System.Reflection.Assembly.GetExecutingAssembly();
            string resName = null;
            foreach (var n in asm.GetManifestResourceNames()) if (n.EndsWith(fileName)) { resName = n; break; }
            if (resName == null) { Core.LogMsg("[蛙哥] 图标资源未找到: " + fileName); return; }

            using var st = asm.GetManifestResourceStream(resName);
            byte[] png = new byte[st.Length]; st.Read(png, 0, png.Length);
            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;

            Type icType = null;
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies()) { Type[] ts; try { ts = a.GetTypes(); } catch (System.Reflection.ReflectionTypeLoadException ex) { ts = ex.Types; } foreach (var t in ts) if (t != null && t.Name == "ImageConversion") { icType = t; break; } if (icType != null) break; }
            if (icType == null) { Core.LogMsg("[蛙哥] ImageConversion未就绪，跳过(懒加载兜底): " + spriteKey); return; }
            icType.GetMethod("LoadImage", new Type[] { typeof(Texture2D), typeof(Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<byte>) }).Invoke(null, new object[] { tex, (Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<byte>)png });

            Sprite sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), GetIconPPU(spriteKey)); // 64x64@200f=0.32单位（2×2原生标准）
            SpriteDict.Instance.spriteDictionary[spriteKey] = sp;
            UnityEngine.Object.DontDestroyOnLoad(tex);
            UnityEngine.Object.DontDestroyOnLoad(sp);
            Core.LogMsg("[蛙哥] 图标加载: " + spriteKey + " " + tex.width + "x" + tex.height);
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
            w.SetSize(360, 480).SetPosition(Vector2.zero);
            w.BeginColumn(4f);
            w.AddLabel(LangHelper.T("蛙哥：花钱消个负面特性。钱货两清。", "Wage Brother: pay to remove a negative perk. No refunds."), "wb_hint");
            var ps = PlayerStore.Instance;
            w.AddLabel(LangHelper.T("当前现金：" + ps.playerCash, "Cash: " + ps.playerCash), "wb_cash");
            // 列已选 Cost<0 负面perk
            int listed = 0;
            foreach (var perk in CustomStartingPerks.All)
            {
                try
                {
                    if (perk.Cost >= 0) continue;
                    if (!StartingPerk.IsPerkActive(perk.Id)) continue;
                    int price = PriceForCost(perk.Cost);
                    string nm = perk.DisplayName;
                    int cpy = perk.Cost;
                    int pr = price;
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
    private static System.Collections.Generic.List<GameItem> CollectAllItems()
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
            // 5. 销毁低级物品
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
        }
        catch (Exception ex) { Core.LogMsg("[蛙哥] ShowCraftWindow异常: " + ex.Message); }
    }
}
