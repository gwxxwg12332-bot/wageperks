using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MelonLoader;
using UnityEngine;

namespace JacksonPerks;

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

    // 周期塞队（照博士 ScheduleJacksonToday）
    internal static bool ScheduleToday()
    {
        try
        {
            var ps = PlayerStore.Instance;
            if (ps == null) return false;
            if (!Core.PerkActive("蛙娘")) return false; // 门控：没点蛙娘 perk 不塞队
            int day = StoreStation.GetDayCounter();
            if (HasQueued()) return false;
            int interval = BuildConfig.WageBrotherVisitInterval > 0 ? BuildConfig.WageBrotherVisitInterval : 10;
            int lastSched = WageSaveStore.GetInt("wage_brother", "last_scheduled_day", -1);
            if (lastSched >= 0 && day - lastSched < interval) return false;
            WageSaveStore.SetInt("wage_brother", "last_scheduled_day", day);
            ps.QueueFuturClient(CLIENT_ID, 1);
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
            if (client == null || client.identifier != CLIENT_ID) { if (Core.DebugMode) Core.LogMsg("[蛙哥] OnClientArrived: identifier=" + (client!=null?client.identifier:"null")+" 不是蛙哥,跳过"); return; } Core.LogMsg("[蛙哥] OnClientArrived: 是蛙哥,开始生成服务卡");
            try { client.SetBudget(1109707341, 100); client.clientIntent = StoreClient.ClientIntent.SELLNBUY; } catch { }
            try { LoadPortrait(); client.spriteName = "wage_brother_portrait"; try { client.possibleSprites.Clear(); client.possibleSprites.Add("wage_brother_portrait"); } catch { } try { if (StoreClientMono.Instance != null && StoreClientMono.Instance.image != null && _portrait != null) { StoreClientMono.Instance.image.sprite = _portrait; Core.LogMsg("[蛙哥] 立绘已刷"); } } catch (System.Exception exr) { Core.LogMsg("[蛙哥] 刷立绘异常: " + exr.Message); } } catch { }
            // 不依赖门控，每次到场都加卡（柜台有卡则原生去重）`r`n            Core.LogMsg("[蛙哥] _cardSpawned=" + _cardSpawned + " 强制加卡");
            var ps = PlayerStore.Instance; if (ps == null) return;
            GameItem card = null;
            try { card = DirectoryMaster.Item(CARD_ID); Core.LogMsg("[蛙哥] DirectoryMaster("+CARD_ID+")=" + (card!=null?"ok":"null")); } catch (Exception ex) { Core.LogMsg("[蛙哥] 创建CARD_ID异常: " + ex.Message); }
            if (card == null)
            {
                // 未注册物品 fallback：用 cassette_player 占位
                try { card = DirectoryMaster.Item("joe_card", true); Core.LogMsg("[蛙哥] joe_card fallback=" + (card!=null?"ok":"null")); } catch (Exception ex) { Core.LogMsg("[蛙哥] joe_card异常: " + ex.Message); }
            }
            if (card != null)
            {
                try { LoadCardSprite(); card.SetName("蛙哥名片"); card.shortDescription = LangHelper.T("双击：花信用点消除一项负面特性。", "Double-click: pay credits to remove a negative perk."); card.SetSprite("custom_atlas", CARD_SPRITE_KEY); var gsb = new GridShapeBuilder(); gsb.SetDataFill(2, 1); card.SetShape(gsb.Build()); card.modifiedShape = gsb.Build(); } catch { }
                try { card.EnableTag("wage_bro_card", true); } catch { } try { var d = client.mainDialogue; if (d != null) { d.SetText("蛙哥", LangHelper.T("我来收点晦气。花信用点消一项负面特性，钱货两清。", "I collect trouble. Pay credits to remove a negative perk.")); d.endAction = null; if (d.nextDialogue != null) { d.nextDialogue.endAction = null; d.nextDialogue = null; } } } catch (System.Exception exd) { Core.LogMsg("[蛙哥] 清对话链异常: " + exd.Message); } try { MerchantHelper.AddItemToCounter(card, 0, false); } catch (System.Exception excard) { Core.LogMsg("[蛙哥] 服务卡上柜台异常: " + excard.Message); }
                
                _cardSpawned = true;
                Core.LogMsg("[蛙哥] 到场，服务卡已上柜台");
            }
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
            Core.LogMsg("[蛙哥] 资源=" + resName + " size=" + png.Length + " icType=" + (icType!=null?icType.FullName:"null")); Core.LogMsg("[蛙哥] LoadImage前"); icType.GetMethod("LoadImage", new Type[] { typeof(Texture2D), typeof(Il2CppStructArray<byte>) }).Invoke(null, new object[] { tex, (Il2CppStructArray<byte>)png });
            Core.LogMsg("[蛙哥] LoadImage后 tex=" + tex.width + "x" + tex.height); _cardSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 1f);
            Core.LogMsg("[蛙哥] 服务卡图标加载 " + tex.width + "x" + tex.height); // atlasCache直写注释：索引器导致卡死，走PrefixLoadFromAtlas拦截
        } catch (System.Exception ex) { Core.LogMsg("[蛙哥] 服务卡图标加载失败: " + ex.Message); }
    }
    public static bool PrefixLoadFromAtlas(string atlasPath, string name, ref Sprite __result)
    {
        try { if (atlasPath == "custom_atlas" && name == CARD_SPRITE_KEY) { Core.LogMsg("[蛙哥] LoadFromAtlas: " + name + " cs=" + (_cardSprite!=null?"ok":"null")); if (_cardSprite != null) { Core.LogMsg("[蛙哥] 拦截!"); __result = _cardSprite; return false; } } } catch { }
        return true;
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
            _portrait = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 8f);
            Core.LogMsg("[蛙哥] Sprite.Create后, Instance=" + (SpriteDict.Instance!=null?"ok":"null"));
            UnityEngine.Object.DontDestroyOnLoad(tex); UnityEngine.Object.DontDestroyOnLoad(_portrait); SpriteDict.Instance.spriteDictionary["wage_brother_portrait"] = _portrait; Core.LogMsg("[蛙哥] 立绘加载成功");
        } catch (System.Exception ex) { Core.LogMsg("[蛙哥] 立绘加载失败: " + ex.Message); }
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
                if (isCard) { try { it.parentInventory?.Expel(it); } catch { } try { it.Destroy(); } catch { } }
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
                    string btnText = LangHelper.T(nm + "（" + cpy + "点，" + pr + "块）", nm + " (" + cpy + "pt, " + pr + "cr)");
                    w.AddButton(btnText, act, "wb_perk_" + listed);
                    listed++;
                }
                catch { }
            }
            if (listed == 0) w.AddLabel(LangHelper.T("没有可消除的负面特性", "No removable negative perks"), "wb_empty");
        }
        catch (Exception ex) { Core.LogMsg("[蛙哥] ShowWindow异常: " + ex.Message); }
    }

    private static void DoRemovePerk(string perkId, int price)
    {
        try
        {
            var ps = PlayerStore.Instance; if (ps == null) return;
            if (ps.playerCash < price) { StoreUIManager.Instance.Notify(LangHelper.T("钱不够", "Not enough credits")); return; }
            ps.playerCash -= price;
            StartingPerk.RemovePerk(perkId);
            // 消除声名狼藉：5势力声望差值补回0
            if (perkId == "声名狼藉")
            {
                try
                {
                    string[] factionIds = { "FACTION_SECURITY", "FACTION_UPPER_LEVEL", "FACTION_REVOLUTION", "FACTION_LOWER_LEVEL", "FACTION_BLACK_MARKET" };
                    foreach (var fid in factionIds)
                    {
                        try
                        {
                            var rep = StoreReputation.GetStoreReputation(fid);
                            if (rep == null) continue;
                            int cur = (int)rep.GetReputationExact();
                            if (cur < 0) rep.ModReputation(-cur); // 负值补回0
                        }
                        catch { }
                    }
                    Core.LogMsg("[蛙哥] 声名狼藉已消除，5势力声望回正");
                }
                catch (Exception ex) { Core.LogMsg("[蛙哥] 声望回正异常: " + ex.Message); }
            }
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
}
