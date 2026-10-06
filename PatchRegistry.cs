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

        // 10-06 让路保留扩展2：新档清血量缓存（玩家反馈"非鲁滨逊开档血量继承下一个档"）。
        // 背景：让路模式只挂消费链（AddBlood 写 _memBlood）；StartNewGame 清缓存挂点在 ApplyAll（鲁滨逊档归 WP），
        // 双装非鲁滨逊档开新档 → _memBlood 残留上一档血量 → 继承。补挂 StartNewGame → SaveStore.PostfixStartNewGame
        // （清命名空间+清血量缓存+设默认值；新档本就该重置生存状态，与 WP TrySetupNewRun 语义一致，无冲突）。
        try
        {
            var orig3 = AccessTools.Method(typeof(PlayerStore), "StartNewGame");
            var post3 = AccessTools.Method(typeof(SaveStore), "PostfixStartNewGame");
            harmony.Patch(orig3, postfix: new HarmonyMethod(post3));
            Core.LogMsg("[WageSurvival] 让路保留 Patch OK: PlayerStore.StartNewGame（清血量缓存）");
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival] 让路保留 Patch FAIL: StartNewGame " + ex.Message); }
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

        // 13. 10-06 WS 单装修复：特性选择界面点不动（cheatsheet 5.6 [L1] isInMainMenu 拦截）——Prefix 点击前恢复
        try
        {
            var orig = AccessTools.Method(typeof(Il2Cpp.StartingPerkElement), "OnPointerClick");
            var pre = AccessTools.Method(typeof(SurvivalFood), "PrefixOnPointerClick");
            harmony.Patch(orig, prefix: new HarmonyMethod(pre));
            Core.LogMsg("[WageSurvival] Patch OK: StartingPerkElement.OnPointerClick (isInMainMenu恢复)");
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival] Patch FAIL: OnPointerClick " + ex.Message); }

        // 14. 10-06 P0 拆分缺口修复：NewStartTypeUI（空间站鲁滨逊开局 tab，WP 复制）——单装 WS 可选鲁滨逊（startType=15 门控可达）
        try
        {
            var origA = AccessTools.Method(typeof(Il2Cpp.MainMenuUIController), "Awake");
            var postA = AccessTools.Method(typeof(NewStartTypeUI), "PostfixAwake");
            harmony.Patch(origA, postfix: new HarmonyMethod(postA));
            Core.LogMsg("[WageSurvival] Patch OK: MainMenuUIController.Awake (新职业tab克隆)");
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival] Patch FAIL: NewStartTypeUI.Awake " + ex.Message); }
        try
        {
            var origB = AccessTools.Method(typeof(Il2Cpp.MainMenuUIController), "ResetAllTab");
            var postB = AccessTools.Method(typeof(NewStartTypeUI), "PostfixResetAllTab");
            harmony.Patch(origB, postfix: new HarmonyMethod(postB));
            Core.LogMsg("[WageSurvival] Patch OK: MainMenuUIController.ResetAllTab");
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival] Patch FAIL: NewStartTypeUI.ResetAllTab " + ex.Message); }
        try
        {
            var origC = AccessTools.Method(typeof(Il2Cpp.NewGameData), "GetStartDisplayName", new System.Type[] { typeof(Il2Cpp.NewGameData.StartType) });
            var postC = AccessTools.Method(typeof(NewStartTypeUI), "PostfixGetStartDisplayName");
            harmony.Patch(origC, postfix: new HarmonyMethod(postC));
            Core.LogMsg("[WageSurvival] Patch OK: NewGameData.GetStartDisplayName");
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival] Patch FAIL: NewStartTypeUI.GetStartDisplayName " + ex.Message); }
        try
        {
            var origD = AccessTools.Method(typeof(Il2Cpp.SaveFiles), "BuildPreviewFromStore", new System.Type[] { typeof(Il2Cpp.PlayerStore), typeof(int) });
            var postD = AccessTools.Method(typeof(NewStartTypeUI), "PostfixBuildPreviewFromStore");
            harmony.Patch(origD, postfix: new HarmonyMethod(postD));
            Core.LogMsg("[WageSurvival] Patch OK: SaveFiles.BuildPreviewFromStore");
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival] Patch FAIL: NewStartTypeUI.BuildPreviewFromStore " + ex.Message); }
        try
        {
            var origE = AccessTools.Method(typeof(PlayerStore), "SaveGame");
            var preE = AccessTools.Method(typeof(NewStartTypeUI), "PrefixSaveGame");
            var postE = AccessTools.Method(typeof(NewStartTypeUI), "PostfixSaveGame");
            harmony.Patch(origE, prefix: new HarmonyMethod(preE), postfix: new HarmonyMethod(postE));
            Core.LogMsg("[WageSurvival] Patch OK: PlayerStore.SaveGame (12↔15)");
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival] Patch FAIL: NewStartTypeUI.SaveGame " + ex.Message); }
        try
        {
            var origF = AccessTools.Method(typeof(PlayerStore), "LoadGame");
            var postF = AccessTools.Method(typeof(NewStartTypeUI), "PostfixLoadGame");
            harmony.Patch(origF, postfix: new HarmonyMethod(postF));
            Core.LogMsg("[WageSurvival] Patch OK: PlayerStore.LoadGame (15恢复)");
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival] Patch FAIL: NewStartTypeUI.LoadGame " + ex.Message); }

        // 15. 10-06 P1 拆分缺口修复：阶段C 边界内 4 挂点（自动喝水/满卡tooltip/饲料机/食物tooltip）——WP 复制（WS SurvivalFood.TooltipSupply.cs）
        try
        {
            var orig = AccessTools.Method(typeof(LiquidContainerHelper), "AutoSipFromContainer");
            var pre = AccessTools.Method(typeof(SurvivalFood), "PrefixAutoSipFromContainer");
            harmony.Patch(orig, prefix: new HarmonyMethod(pre));
            Core.LogMsg("[WageSurvival] Patch OK: LiquidContainerHelper.AutoSipFromContainer");
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival] Patch FAIL: AutoSipFromContainer " + ex.Message); }
        try
        {
            var orig = AccessTools.Method(typeof(FoodItemHelper), "CreateFoodItemTooltip");
            var pre = AccessTools.Method(typeof(SurvivalFood), "PrefixFoodTooltip");
            var post = AccessTools.Method(typeof(SurvivalFood), "PostfixFoodTooltip");
            harmony.Patch(orig, prefix: new HarmonyMethod(pre), postfix: new HarmonyMethod(post));
            Core.LogMsg("[WageSurvival] Patch OK: FoodItemHelper.CreateFoodItemTooltip (满卡虚高)");
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival] Patch FAIL: CreateFoodItemTooltip " + ex.Message); }
        try
        {
            var orig = AccessTools.Method(typeof(Il2Cpp.MachineFeedDispenser.__c__DisplayClass7_0), "_CreateFeedDispenser_b__3");
            var pre = AccessTools.Method(typeof(SurvivalFood), "PrefixFeedDispenserB3");
            harmony.Patch(orig, prefix: new HarmonyMethod(pre));
            Core.LogMsg("[WageSurvival] Patch OK: MachineFeedDispenser._CreateFeedDispenser_b__3 (饲料机)");
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival] Patch FAIL: FeedDispenserB3 " + ex.Message); }
        try
        {
            var orig = AccessTools.Method(typeof(HusbandryHelper), "CreateItemTooltip");
            var post = AccessTools.Method(typeof(SurvivalFood), "PostfixCreateItemTooltip");
            harmony.Patch(orig, postfix: new HarmonyMethod(post));
            Core.LogMsg("[WageSurvival] Patch OK: HusbandryHelper.CreateItemTooltip (食物tooltip)");
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival] Patch FAIL: CreateItemTooltip " + ex.Message); }

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
            var orig = AccessTools.Method(typeof(ItemFeature), "GetActualDisplay");
            var post = AccessTools.Method(typeof(SurvivalTrade), "PostfixItemFeatureGetActualDisplay");
            harmony.Patch(orig, postfix: new HarmonyMethod(post));
            Core.LogMsg("[WageSurvival] Patch OK: ItemFeature.GetActualDisplay");
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival] Patch FAIL: GetActualDisplay " + ex.Message); }

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
