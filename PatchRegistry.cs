using HarmonyLib;
using Il2Cpp;
using MelonLoader;

namespace WageSurvival;
internal static class PatchRegistry
{
    // 10-05 修复：让路模式保留"全局消费链"挂点（双击吃喝用）。
    // 背景：让路前 WagePerks 侧 RobinCrusoePerk.PostfixDoubleClickAction 有 IsWageSurvivalLoaded→return（WageSurvival 在场即让出）；
    // 若 WageSurvival 完全让路（零挂点）→ 双向互让 → 双击吃喝用无人处理（吃饭/日用品/喝酒全失效）。
    // 仅保留双击挂点：WagePerks 侧对应挂点让路（不叠加）；SaveStore/交易/衰减等仍归 WagePerks。
    // 注意：原 ApplyAll 第1项挂载引用 SurvivalFood.PostfixDoubleClickAction 是错的（方法实际在 SurvivalConsume）→ 此处用正确类。
    internal static void ApplyGlobalConsumeOnly()
    {
        var harmony = new HarmonyLib.Harmony("com.wagesurvival.consume");
        try
        {
            var orig = AccessTools.Method(typeof(ItemMouseDoubleClickHandler), "DoubleClickAction");
            var post = AccessTools.Method(typeof(SurvivalFood), "PostfixDoubleClickAction");
            harmony.Patch(orig, postfix: new HarmonyMethod(post));
            Core.LogMsg("[WageSurvival] 让路保留 Patch OK: DoubleClickAction（全局吃喝用）");
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival] 让路保留 Patch FAIL: DoubleClickAction " + ex.Message); }

        // 10-06 让路保留扩展：Z 键面板链（InputActionManager.Update Postfix）。
        // 背景（日志实锤）：双装时 WS 让路只挂消费链 → 普通档（startType=2）面板无主
        // （WP 的 HandleHotkeys 只认 startType==15，普通档 IsActive=false 不呼出）→ 玩家反馈"面板没有呼出来"。
        // PostfixFrameUpdate 内部已加 IsRobinsonRun 门控：鲁滨逊档让 WP 的 Z 键，非鲁滨逊档由本链处理。
        try
        {
            var orig2 = AccessTools.Method(typeof(Il2Cpp.InputActionManager), "Update");
            var post2 = AccessTools.Method(typeof(SurvivalFood), "PostfixFrameUpdate");
            harmony.Patch(orig2, postfix: new HarmonyMethod(post2));
            Core.LogMsg("[WageSurvival] 让路保留 Patch OK: InputActionManager.Update（Z键面板，非鲁滨逊档）");
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival] 让路保留 Patch FAIL: InputActionManager.Update " + ex.Message); }
    }

    internal static void ApplyAll()
    {
        // 2026-10-03 阶段C：双mod分工重定义——鲁滨逊生存（吃喝/六维/节点/交易/面板/血）归本mod（ns=SurvivalGlobal），
        // WagePerks 删自身 Survival 落盘/交易/衰减挂点（阶段D），保留行为修饰挂点并读 SurvivalGlobal。
        // v0.1.2 让路机制（startType==14 且恒 false）已废除。
        Core.LogMsg("[WageSurvival] 阶段C：全局吃喝 + 鲁滨逊生存（交易门控 startType=15）");

        var harmony = new HarmonyLib.Harmony("com.wagesurvival");

        // 1. 双击吃喝（全局——WagePerks 侧已让路/删除后无叠加）
        try
        {
            var orig = AccessTools.Method(typeof(ItemMouseDoubleClickHandler), "DoubleClickAction");
            var post = AccessTools.Method(typeof(SurvivalFood), "PostfixDoubleClickAction");
            harmony.Patch(orig, postfix: new HarmonyMethod(post));
            Core.LogMsg("[WageSurvival] Patch OK: ItemMouseDoubleClickHandler.DoubleClickAction");
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival] Patch FAIL: DoubleClickAction " + ex.Message); }

        // 2. Z键快捷键（全局）
        try
        {
            var orig = AccessTools.Method(typeof(Il2Cpp.InputActionManager), "Update");
            var post = AccessTools.Method(typeof(SurvivalFood), "PostfixFrameUpdate");
            harmony.Patch(orig, postfix: new HarmonyMethod(post));
            Core.LogMsg("[WageSurvival] Patch OK: InputActionManager.Update");
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival] Patch FAIL: InputActionManager.Update " + ex.Message); }

        // 3. 读档（InitDefaults 兜底；实际读文件由 WageAPI 统一承载）
        try
        {
            var origLoad = AccessTools.Method(typeof(PlayerStore), "LoadGame");
            var postLoad = AccessTools.Method(typeof(SurvivalFood), "PostfixLoadGame");
            harmony.Patch(origLoad, postfix: new HarmonyMethod(postLoad));
            Core.LogMsg("[WageSurvival] Patch OK: PlayerStore.LoadGame");
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival] Patch FAIL: LoadGame " + ex.Message); }

        // 4. 存档落盘（血量/存档快照写内存；原子落盘由 WageAPI SaveGame/EndDay Postfix 收尾）
        try
        {
            var origSave = AccessTools.Method(typeof(PlayerStore), "SaveGame");
            var postSave = AccessTools.Method(typeof(SurvivalFood), "PostfixSaveGame");
            harmony.Patch(origSave, postfix: new HarmonyMethod(postSave));
            Core.LogMsg("[WageSurvival] Patch OK: PlayerStore.SaveGame");
            var origEndDay = AccessTools.Method(typeof(PlayerStore), "EndDay");
            var postEndDay = AccessTools.Method(typeof(SurvivalFood), "PostfixEndDay");
            harmony.Patch(origEndDay, postfix: new HarmonyMethod(postEndDay));
            Core.LogMsg("[WageSurvival] Patch OK: PlayerStore.EndDay");
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival] Patch FAIL: SaveGame/EndDay " + ex.Message); }

        // 5. 每天衰减/结算（全局；WagePerks 侧同目标挂点阶段D删除后无叠加）
        try
        {
            var orig = AccessTools.Method(typeof(StoreEventManager), "OnDayStart");
            var post = AccessTools.Method(typeof(SurvivalFood), "PostfixOnDayStart");
            harmony.Patch(orig, postfix: new HarmonyMethod(post));
            Core.LogMsg("[WageSurvival] Patch OK: StoreEventManager.OnDayStart");
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival] Patch FAIL: OnDayStart " + ex.Message); }

        // 6. 售价（饱食/心情/Nodes/昂扬/粮仓，全局）
        try
        {
            var orig = AccessTools.Method(typeof(GameItem), "GetCurrentValue");
            var post = AccessTools.Method(typeof(SurvivalFood), "PostfixGetCurrentValue");
            harmony.Patch(orig, postfix: new HarmonyMethod(post));
            Core.LogMsg("[WageSurvival] Patch OK: GameItem.GetCurrentValue");
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival] Patch FAIL: GetCurrentValue " + ex.Message); }

        // 7. 预算加成（非鲁滨逊——鲁滨逊由 SurvivalTrade 门控 startType=15，避免双跑）
        try
        {
            var orig = AccessTools.Method(typeof(StoreClientManager), "PickClient");
            var post = AccessTools.Method(typeof(SurvivalFood), "PostfixPickClient");
            harmony.Patch(orig, postfix: new HarmonyMethod(post));
            Core.LogMsg("[WageSurvival] Patch OK: StoreClientManager.PickClient (非鲁滨逊)");
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival] Patch FAIL: PickClient " + ex.Message); }

        // 8. 交易声望倍率（心情/Nodes，全局）
        try
        {
            var orig = AccessTools.Method(typeof(StoreClient), "GetTradeRepMultiplier");
            var post = AccessTools.Method(typeof(SurvivalFood), "PostfixGetTradeRepMultiplier");
            harmony.Patch(orig, postfix: new HarmonyMethod(post));
            Core.LogMsg("[WageSurvival] Patch OK: StoreClient.GetTradeRepMultiplier");
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival] Patch FAIL: GetTradeRepMultiplier " + ex.Message); }

        // 9. 禁外出（低落/崩溃时禁外出，全局）
        try
        {
            var orig = AccessTools.Method(typeof(MapUIManager), "OpenGoOutsideConfirm");
            var pre = AccessTools.Method(typeof(SurvivalFood), "PrefixOpenGoOutsideConfirm");
            harmony.Patch(orig, prefix: new HarmonyMethod(pre));
            Core.LogMsg("[WageSurvival] Patch OK: MapUIManager.OpenGoOutsideConfirm");
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival] Patch FAIL: OpenGoOutsideConfirm " + ex.Message); }

        // 10. 特性成长：每天累计天数（全局）
        try
        {
            var orig = AccessTools.Method(typeof(StoreEventManager), "OnDayStart");
            var post = AccessTools.Method(typeof(SurvivalGrowthHelper), "OnDayStart");
            harmony.Patch(orig, postfix: new HarmonyMethod(post));
            Core.LogMsg("[WageSurvival] Patch OK: PerkGrowth.OnDayStart");
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival] Patch FAIL: PerkGrowth.OnDayStart " + ex.Message); }

        // 11. 特性成长：开局应用加成（全局）
        try
        {
            var orig = AccessTools.Method(typeof(Il2Cpp.PerkUIController), "OpenUI");
            var post = AccessTools.Method(typeof(SurvivalGrowthHelper), "ApplyOnPerkUiOpen");
            harmony.Patch(orig, postfix: new HarmonyMethod(post));
            Core.LogMsg("[WageSurvival] Patch OK: PerkGrowth.ApplyOnPerkUiOpen");
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival] Patch FAIL: PerkGrowth.ApplyOnPerkUiOpen " + ex.Message); }

        // 12. 新档：清 SurvivalGlobal + 设默认（全局）
        try
        {
            var orig = AccessTools.Method(typeof(PlayerStore), "StartNewGame");
            var post = AccessTools.Method(typeof(SaveStore), "PostfixStartNewGame");
            harmony.Patch(orig, postfix: new HarmonyMethod(post));
            Core.LogMsg("[WageSurvival] Patch OK: PlayerStore.StartNewGame");
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival] Patch FAIL: PlayerStore.StartNewGame " + ex.Message); }

        // ===== 阶段C：交易 12 处迁入（鲁滨逊门控 startType=15；WagePerks 侧同目标 12 处调用阶段D删除） =====
        try
        {
            var orig = AccessTools.Method(typeof(StoreClient), "ApplyBudgetModifier");
            var post = AccessTools.Method(typeof(SurvivalTrade), "PostfixStoreClientApplyBudgetModifier");
            harmony.Patch(orig, postfix: new HarmonyMethod(post));
            Core.LogMsg("[WageSurvival] Patch OK: StoreClient.ApplyBudgetModifier");
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival] Patch FAIL: ApplyBudgetModifier " + ex.Message); }

        try
        {
            var orig = AccessTools.Method(typeof(StoreClientManager), "PickClient");
            var post = AccessTools.Method(typeof(SurvivalTrade), "PostfixStoreClientManagerPickClient");
            harmony.Patch(orig, postfix: new HarmonyMethod(post));
            Core.LogMsg("[WageSurvival] Patch OK: StoreClientManager.PickClient (鲁滨逊)");
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival] Patch FAIL: PickClient(交易) " + ex.Message); }

        try
        {
            var orig = AccessTools.Method(typeof(BargainUIManager), "OfferBuyingMarkup");
            var pre = AccessTools.Method(typeof(SurvivalTrade), "PrefixBargainUIManagerOfferBuyingMarkup");
            harmony.Patch(orig, prefix: new HarmonyMethod(pre));
            Core.LogMsg("[WageSurvival] Patch OK: BargainUIManager.OfferBuyingMarkup");
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival] Patch FAIL: OfferBuyingMarkup " + ex.Message); }

        try
        {
            var orig = AccessTools.Method(typeof(BargainUIManager), "GetDealMakerBonus");
            var post = AccessTools.Method(typeof(SurvivalTrade), "PostfixGetDealMakerBonus");
            harmony.Patch(orig, postfix: new HarmonyMethod(post));
            Core.LogMsg("[WageSurvival] Patch OK: BargainUIManager.GetDealMakerBonus");
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival] Patch FAIL: GetDealMakerBonus " + ex.Message); }

        try
        {
            var orig = AccessTools.Method(typeof(ItemFeatureList), "BargainBuyingMarkup");
            var post = AccessTools.Method(typeof(SurvivalTrade), "PostfixItemFeatureListBargainBuyingMarkup");
            harmony.Patch(orig, postfix: new HarmonyMethod(post));
            Core.LogMsg("[WageSurvival] Patch OK: ItemFeatureList.BargainBuyingMarkup");
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival] Patch FAIL: BargainBuyingMarkup " + ex.Message); }

        try
        {
            var orig = AccessTools.Method(typeof(StoreClient), "OnDealAccepted");
            var post = AccessTools.Method(typeof(SurvivalTrade), "PostfixStoreClientOnDealAccepted");
            harmony.Patch(orig, postfix: new HarmonyMethod(post));
            Core.LogMsg("[WageSurvival] Patch OK: StoreClient.OnDealAccepted");
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival] Patch FAIL: OnDealAccepted " + ex.Message); }

        // 旧档迁移（幂等）：RobinCrusoe → SurvivalGlobal
        try
        {
            WageAPI.WageSaveStore.GameLoaded += SurvivalMigrate.OnStoreGameLoaded;
            Core.LogMsg("[WageSurvival] 旧档迁移订阅 OK");
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival] 旧档迁移订阅 FAIL: " + ex.Message); }
    }
}
