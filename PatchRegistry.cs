using HarmonyLib;
using Il2Cpp;
using MelonLoader;

namespace WageSurvival;
internal static class PatchRegistry
{
    // 10-10 #44：删除 ApplyGlobalConsumeOnly 死代码（原 :8-74 五挂点——让路时代入口，10-07 起 Core.cs:39 双装/单装统一走 ApplyAll）
    //   引用计数：全库 0 调用（仅 SurvivalConfigUI.cs:60/Core.cs:39/NewStartTypeUI.cs:19 注释提及）——死代码不注册
    //   5 挂点对比：DoubleClickAction/InputActionManager.Update/PlayerStore.StartNewGame/StoreEventManager.OnDayStart → ApplyAll 同方法已注册；
    //   MainMenuUIController.Awake(SurvivalConfigUI.PostfixAwake) → ApplyAll :222-235 NewStartTypeUI.PostfixAwake 内联 InjectMainMenuButton（NewStartTypeUI.cs:128）同功能已覆盖——无需补缺
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
            // 10-08 全新启动 tab 回归兜底：Start 晚于 Awake（字段注入完成），双通道防竞态错过
            var origS = AccessTools.Method(typeof(Il2Cpp.MainMenuUIController), "Start");
            if (origS != null)
            {
                var postS = AccessTools.Method(typeof(NewStartTypeUI), "PostfixStart");
                harmony.Patch(origS, postfix: new HarmonyMethod(postS));
                Core.LogMsg("[WageSurvival] Patch OK: MainMenuUIController.Start (新职业tab兜底)");
            }
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

        // ===== 阶段C：交易 12 处迁入（鲁滨逊门控 startType=15；WagePerks 侧同目标 12 处按实施单删除） =====
        // 10-07 鲁滨逊完全归 WS（决策 1）：交易归 WS——避让块已删（原 10-07 D-4 单边避让：WP 在场交易组不挂——现已撤销，
        // WS SurvivalTrade 唯一宿主；WP 侧交易 6 条 :236/:243-247 删除，:85-90/:266-267/:271 按方法体确认归属）
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

        // 10-10 #37 A 拍板：鲁滨逊 buy 食物/药品 ×2 迁 WS（主 mod Markup.cs 已删 ×2——双装只 ×2 一次）
        //   门控=IsRobinsonRun（15||12）；主 mod PostfixGameItemGetNegociatedValue（Patches.Trade.Markup.cs:70）buy 分支已无 ×2，双 Postfix 顺序无关
        try
        {
            var orig = AccessTools.Method(typeof(GameItem), "GetNegociatedValue");
            var post = AccessTools.Method(typeof(SurvivalTrade), "PostfixGameItemGetNegociatedValue");
            harmony.Patch(orig, postfix: new HarmonyMethod(post));
            Core.LogMsg("[WageSurvival] Patch OK: GameItem.GetNegociatedValue (鲁滨逊×2)");
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival] Patch FAIL: GetNegociatedValue×2 " + ex.Message); }

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

        // ===== 10-07 鲁滨逊完全归 WS WS-2：状态客户 8 工厂接线（P0——拆包 YJdTSrCfBnEJiDvKcY759K：定义在 SurvivalStatusClients.cs:86-93 但注册点缺失） =====
        // Target/方法名对齐 WP PatchRegistryTable.cs:251-258（已删）——同一工厂 Postfix 打标（状态客户无独立状态字段）
        try
        {
            AccessTools.Method(typeof(StoreClientListTier), "CreateThirstySpacer");
            var p1 = AccessTools.Method(typeof(SurvivalFood), "PostfixCreateThirstySpacer");
            harmony.Patch(AccessTools.Method(typeof(StoreClientListTier), "CreateThirstySpacer"), postfix: new HarmonyMethod(p1));
            Core.LogMsg("[WageSurvival] Patch OK: StoreClientListTier.CreateThirstySpacer");
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival] Patch FAIL: CreateThirstySpacer " + ex.Message); }
        try
        {
            harmony.Patch(AccessTools.Method(typeof(StoreClientListTier), "CreateHungrySpacer"), postfix: new HarmonyMethod(AccessTools.Method(typeof(SurvivalFood), "PostfixCreateHungrySpacer")));
            Core.LogMsg("[WageSurvival] Patch OK: StoreClientListTier.CreateHungrySpacer");
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival] Patch FAIL: CreateHungrySpacer " + ex.Message); }
        try
        {
            harmony.Patch(AccessTools.Method(typeof(StoreClientListTier), "CreateSpacerChef"), postfix: new HarmonyMethod(AccessTools.Method(typeof(SurvivalFood), "PostfixCreateSpacerChef")));
            Core.LogMsg("[WageSurvival] Patch OK: StoreClientListTier.CreateSpacerChef");
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival] Patch FAIL: CreateSpacerChef " + ex.Message); }
        try
        {
            harmony.Patch(AccessTools.Method(typeof(StoreClientListTier), "CreateInjuredSpacer"), postfix: new HarmonyMethod(AccessTools.Method(typeof(SurvivalFood), "PostfixCreateInjuredSpacer")));
            Core.LogMsg("[WageSurvival] Patch OK: StoreClientListTier.CreateInjuredSpacer");
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival] Patch FAIL: CreateInjuredSpacer " + ex.Message); }
        try
        {
            harmony.Patch(AccessTools.Method(typeof(StoreClientListTier), "CreateSickChildCaretaker"), postfix: new HarmonyMethod(AccessTools.Method(typeof(SurvivalFood), "PostfixCreateSickChildCaretaker")));
            Core.LogMsg("[WageSurvival] Patch OK: StoreClientListTier.CreateSickChildCaretaker");
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival] Patch FAIL: CreateSickChildCaretaker " + ex.Message); }
        try
        {
            harmony.Patch(AccessTools.Method(typeof(StoreClientListTierSubstance), "CreateDesperateAddict"), postfix: new HarmonyMethod(AccessTools.Method(typeof(SurvivalFood), "PostfixCreateDesperateAddict")));
            Core.LogMsg("[WageSurvival] Patch OK: StoreClientListTierSubstance.CreateDesperateAddict");
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival] Patch FAIL: CreateDesperateAddict " + ex.Message); }
        try
        {
            harmony.Patch(AccessTools.Method(typeof(StoreClientListTierSubstance), "CreateWornOutSpacer"), postfix: new HarmonyMethod(AccessTools.Method(typeof(SurvivalFood), "PostfixCreateWornOutSpacer")));
            Core.LogMsg("[WageSurvival] Patch OK: StoreClientListTierSubstance.CreateWornOutSpacer");
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival] Patch FAIL: CreateWornOutSpacer " + ex.Message); }
        try
        {
            harmony.Patch(AccessTools.Method(typeof(StoreClientListMinor), "CreateSickLowers"), postfix: new HarmonyMethod(AccessTools.Method(typeof(SurvivalFood), "PostfixCreateSickLowers")));
            Core.LogMsg("[WageSurvival] Patch OK: StoreClientListMinor.CreateSickLowers");
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival] Patch FAIL: CreateSickLowers " + ex.Message); }

        // ===== 10-07 鲁滨逊完全归 WS WS-3：P1 四域搬迁 22 条注册（拾荒5/机器7/模块3/经济链7；方法体=SurvivalScavenge/SurvivalMachineMods/SurvivalEconomy 新文件） =====
        // 适配标注：GetRandomScavengedItem priority=-1000（后置跑——对齐 WP PatchRegistryTable:172）；CreateMachineryTooltip 不迁（养蛊机/瓶印机特性域，WP 保留 :178）
        void RegisterP1(System.Type target, string method, string hostMethod, bool prefix = false, bool postfix = true, int priority = 0, string finalizer = null)
        {
            try
            {
                var orig = AccessTools.Method(target, method);
                var hm = AccessTools.Method(typeof(SurvivalFood), hostMethod);
                var hmFinalizer = finalizer == null ? null : new HarmonyMethod(AccessTools.Method(typeof(SurvivalFood), finalizer));
                if (prefix && postfix)
                    harmony.Patch(orig, prefix: new HarmonyMethod(hm) { priority = priority }, postfix: new HarmonyMethod(hm) { priority = priority }, finalizer: hmFinalizer);
                else if (prefix)
                    harmony.Patch(orig, prefix: new HarmonyMethod(hm) { priority = priority }, finalizer: hmFinalizer);
                else
                    harmony.Patch(orig, postfix: new HarmonyMethod(hm) { priority = priority }, finalizer: hmFinalizer);
                Core.LogMsg("[WageSurvival] Patch OK: " + target.Name + "." + method + " (" + hostMethod + ")" + (finalizer != null ? " + Finalizer " + finalizer : ""));
            }
            catch (System.Exception ex) { Core.LogMsg("[WageSurvival] Patch FAIL: " + target.Name + "." + method + " " + ex.Message); }
        }
        // 拾荒 5
        RegisterP1(typeof(ScavHelper), "CanScavenge", "PostfixCanScavenge");
        RegisterP1(typeof(ScavHelper), "GetMaxScavAttempts", "PostfixGetMaxScavAttempts");
        RegisterP1(typeof(ScavHelper), "GetScavTimeLeft", "PostfixGetScavTimeLeft");
        RegisterP1(typeof(ScavHelper), "GetRandomScavengedItem", "PostfixGetRandomScavengedItem", priority: -1000, finalizer: "FinalizerGetRandomScavengedItem"); // 10-17 接线：拾荒异常兜底（对齐 WP PatchRegistryTable:65）
        RegisterP1(typeof(ScavHelper), "ScavengeDumpingGrounds", "PostfixScavengeDumpingGrounds");
        // 机器/模块 7+3（含 GetMachinePowerUsage——拆包机器域同打包；CreateMachineryTooltip 不迁）
        RegisterP1(typeof(MachineryHelper), "GetCurrentPerformanceBonus", "PostfixGetCurrentPerformanceBonus");
        RegisterP1(typeof(MachineryHelper), "GetCurrentQualityBonus", "PostfixGetCurrentQualityBonus");
        RegisterP1(typeof(MachineryHelper), "GetMachinePowerUsage", "PostfixGetMachinePowerUsage");
        RegisterP1(typeof(ModuleHelper), "ApplyBasicModuleEffect", "PostfixApplyBasicModuleEffect");
        RegisterP1(typeof(ModuleEffectHelper), "ModifyTempStatFromBaseByPercentage", "PostfixModifyTempStatFromBaseByPercentage");
        RegisterP1(typeof(ModuleHelper), "AddModuleStatLine", "PostfixAddModuleStatLine");
        RegisterP1(typeof(ModuleHelper), "ApplyPerformanceWaterRecyclerEffect", "PostfixApplyPerformanceWaterRecyclerEffect");
        RegisterP1(typeof(MachineMoistureFarm), "GetOutputVolume", "PostfixGetOutputVolume");
        RegisterP1(typeof(WaterHelper), "RemoveContaminantFromContainer", "PostfixRemoveContaminantFromContainer");
        // 经济链 7
        RegisterP1(typeof(StoreClientManager), "CheckRentDay", "PrefixCheckRentDay", prefix: true, postfix: false);
        RegisterP1(typeof(StoreClientUniqueList.__c), "_LandlordWholesale_b__22_0", "PrefixLandlordWholesaleStock", prefix: true, postfix: false);
        RegisterP1(typeof(StoreClientManager), "HandleSupplierClient", "PostfixHandleSupplierClient");
        RegisterP1(typeof(PlayerStore), "HandleInsurance", "PrefixHandleInsurance", prefix: true, postfix: false);
        RegisterP1(typeof(PlayerStore), "ExecuteGameOver", "PrefixExecuteGameOver", prefix: true, postfix: false);
        RegisterP1(typeof(StoreClientManager), "HandleNormalClient", "PostfixHandleNormalClient");
        RegisterP1(typeof(StoreClientList), "PlaceSupplierInventory", "PostfixPlaceSupplierInventory");

        // 旧档迁移（幂等）：RobinCrusoe → SurvivalGlobal
        try
        {
            WageAPI.WageSaveStore.GameLoaded += SurvivalMigrate.OnStoreGameLoaded;
            Core.LogMsg("[WageSurvival] 旧档迁移订阅 OK");
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival] 旧档迁移订阅 FAIL: " + ex.Message); }
    }
}
