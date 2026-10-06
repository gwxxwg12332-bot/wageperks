using System;
using System.Linq;
using Il2Cpp;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSystem;
using Il2CppSystem.Collections.Generic;

namespace WagePerks
{
    /// <summary>阶段6：统一Patch注册入口（声明式注册表）。平移不改语义。</summary>
    internal static class PatchRegistryTable
    {
        public const int FLUSH_PRIORITY   = 0;
        public const int LAST_PRIORITY    = -1000;
        public const int DEFAULT_PRIORITY = 400;

        public sealed class PatchEntry
        {
            public System.Type   Target;
            public string Method;
            public string Prefix;
            public string Postfix;
            public System.Type   Host;
            public int    Priority;
            public string System;
            public string YieldMod;
            public string Note;
            public bool   UseByName; // true=走TryPatchByName（名字匹配，非typeof MethodInfo）
            public System.Type[] ParameterTypes; // 重载方法参数类型（null=无重载）
        }

        internal static readonly PatchEntry[] REGISTRY = new PatchEntry[]
        {
            new PatchEntry { Target = typeof(StartingPerkList), Method = "InitStartingPerk", Prefix = null, Postfix = "PostfixInitStartingPerks", Host = typeof(Patches), Priority = DEFAULT_PRIORITY, System = "Core", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(GameMaster), Method = "NewGame", Prefix = null, Postfix = "PostfixOnNewGame", Host = typeof(Patches), Priority = DEFAULT_PRIORITY, System = "Core", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(NewGameData), Method = "HandleInitialItem", Prefix = null, Postfix = "HandleInitialItemPostfix", Host = typeof(Patches), Priority = DEFAULT_PRIORITY, System = "Core", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(PlayerStore), Method = "HandleSkipIntro", Prefix = null, Postfix = "PostfixHandleSkipIntro", Host = typeof(WandererPerk), Priority = DEFAULT_PRIORITY, System = "Wanderer", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(StoreEventManager), Method = "OnDayStart", Prefix = null, Postfix = "PostfixOnNewDay", Host = typeof(Patches), Priority = DEFAULT_PRIORITY, System = "Core", YieldMod = null, Note = "10-04共存：XIAOWO现金流=纯hash行情读算，双Postfix安全" },
            new PatchEntry { Target = typeof(PlayerStore), Method = "BeginDay", Prefix = null, Postfix = "PostfixOnBeginDay", Host = typeof(Patches), Priority = DEFAULT_PRIORITY, System = "Core", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(PlayerStore), Method = "AddDirectSellingItemToTable", Prefix = "PrefixAddDirectSellingItemToTable", Postfix = null, Host = typeof(WaterMerchantPerk), Priority = DEFAULT_PRIORITY, System = "WaterMerchant", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(PlayerStore), Method = "AddDirectSellingItemToTable", Prefix = "PrefixAddDirectSellingItemToTable", Postfix = null, Host = typeof(ThiefMagnetSystem), Priority = DEFAULT_PRIORITY, System = "ThiefMagnet", YieldMod = null, Note = "10-06 三任务①声名狼藉黑市货/物证箱绕过销毁链（小偷场景手动放前台）" },
            // L120/L121 动态GetType(PreBuildChemHelper) 在 ApplyAll 里特殊处理
            new PatchEntry { Target = typeof(StoreClientManager), Method = "AddClient", Prefix = null, Postfix = "PostfixOnAddClient", Host = typeof(Patches), Priority = DEFAULT_PRIORITY, System = "Core", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(PlayerStore), Method = "SaveGame", Prefix = null, Postfix = "PostfixSaveGame", Host = typeof(LuckScoutPerk), Priority = DEFAULT_PRIORITY, System = "LuckScout", YieldMod = null, Note = "只写内存禁 Flush" },
            new PatchEntry { Target = typeof(PlayerStore), Method = "LoadGame", Prefix = null, Postfix = "PostfixOnLoadGame", Host = typeof(Patches), Priority = DEFAULT_PRIORITY, System = "Core", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(PlayerStore), Method = "LoadGame", Prefix = null, Postfix = "PostfixLoadGame", Host = typeof(LuckScoutBackpackUpgrade), Priority = DEFAULT_PRIORITY, System = "LuckScout", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(PlayerStore), Method = "LoadGame", Prefix = null, Postfix = "PostfixLoadGameBottlePrinter", Host = typeof(WaterMerchantPerk), Priority = DEFAULT_PRIORITY, System = "WaterMerchant", YieldMod = null, Note = "水瓶机质量读档恢复(独立tag wageBottleQlty+WageSaveStore双写)" },
            new PatchEntry { Target = typeof(PlayerStore), Method = "LoadGame", Prefix = null, Postfix = "PostfixLoadGameGift", Host = typeof(WageGirlSystem), Priority = DEFAULT_PRIORITY, System = "WageGirl", YieldMod = null, Note = "10-05蛙娘好物读档补发(日结发放不入档丢失修复,180帧窗口检测重发)" },
            new PatchEntry { Target = typeof(PlayerStore), Method = "LoadGame", Prefix = null, Postfix = "PostfixLoadGame", Host = typeof(WageBrother), Priority = DEFAULT_PRIORITY, System = "WageBrother", YieldMod = null, Note = "10-06 F2读档蛙哥补排(小退丢futur队列→重进间隔未到不调度→补排明天到)" },
            new PatchEntry { Target = typeof(StoreClientManager), Method = "HandleContentUnlockClient", Prefix = null, Postfix = "PostfixOnHandleContentUnlockClient", Host = typeof(Patches), Priority = DEFAULT_PRIORITY, System = "Core", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(InputActionManager), Method = "Update", Prefix = null, Postfix = "PostfixInputActionManagerUpdate", Host = typeof(Patches), Priority = DEFAULT_PRIORITY, System = "Core", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(InventorySortHelper), Method = "Sort", Prefix = null, Postfix = "PostfixSort", Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(StoreUIManager), Method = "OnNextClientArrived", Prefix = null, Postfix = "PostfixSpecialNpcStartDialogue", Host = typeof(Patches), Priority = DEFAULT_PRIORITY, System = "Core", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(DialogUIManager), Method = "DisplayClientText", Prefix = "PrefixDisplayClientText", Postfix = null, Host = typeof(Patches), Priority = DEFAULT_PRIORITY, System = "Core", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(ScavHelper), Method = "GetMaxScavAttempts", Prefix = null, Postfix = "PostfixGetMaxScavAttempts", Host = typeof(LuckScoutPerk), Priority = DEFAULT_PRIORITY, System = "LuckScout", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(ScavHelper), Method = "GetScavTimeLeft", Prefix = null, Postfix = "PostfixGetScavTimeLeft", Host = typeof(LuckScoutPerk), Priority = DEFAULT_PRIORITY, System = "LuckScout", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(ScavHelper), Method = "CanScavenge", Prefix = "PrefixCanScavenge", Postfix = "PostfixCanScavenge", Host = typeof(LuckScoutPerk), Priority = DEFAULT_PRIORITY, System = "LuckScout", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(ScavHelper), Method = "ScavengeDumpingGrounds", Prefix = "PrefixScavengeDumpingGrounds", Postfix = "PostfixScavengeDumpingGrounds", Host = typeof(LuckScoutPerk), Priority = DEFAULT_PRIORITY, System = "LuckScout", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(GameMaster), Method = "QuitToMenu", Prefix = null, Postfix = "PostfixQuitToMenu", Host = typeof(LuckScoutPerk), Priority = DEFAULT_PRIORITY, System = "LuckScout", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(GameMaster), Method = "QuitToMenu", Prefix = null, Postfix = "PostfixQuitToMenu", Host = typeof(WageAPI.WageSaveStore), Priority = DEFAULT_PRIORITY, System = "SaveStore", YieldMod = null, Note = "10-06 A8小退落盘(QuitToMenu不触发SaveGame/EndDay→Flush补一次,KEY_STOLEN等小退不丢)" },
            new PatchEntry { Target = typeof(EscapeUIManager), Method = "OnMainMenu", Prefix = null, Postfix = "PostfixOnMainMenu", Host = typeof(LuckScoutPerk), Priority = DEFAULT_PRIORITY, System = "LuckScout", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(GameMaster), Method = "NewGame", Prefix = null, Postfix = "PostfixNewGame", Host = typeof(LuckScoutPerk), Priority = DEFAULT_PRIORITY, System = "LuckScout", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(ScavHelper), Method = "GetRandomScavengedItem", Prefix = null, Postfix = "PostfixGetRandomScavengedItem", Host = typeof(LuckScoutPerk), Priority = -1000, System = "LuckScout", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(ScavHelper), Method = "CreateTooltip", Prefix = null, Postfix = "PostfixCreateTooltip", Host = typeof(LuckScoutPerk), Priority = DEFAULT_PRIORITY, System = "LuckScout", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(EmporiumEntry), Method = "GetAllAfterhourOwnedItems", Prefix = null, Postfix = "PostfixGetAllAfterhourOwnedItems", Host = typeof(LuckScoutPerk), Priority = DEFAULT_PRIORITY, System = "LuckScout", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(PerkUIController), Method = "OpenUI", Prefix = null, Postfix = "PostfixPerkUiOpen", Host = typeof(Patches), Priority = DEFAULT_PRIORITY, System = "Core", YieldMod = null, Note = "10-04共存：XIAOWO/PerkPointMod 不提供我方perk元素，让路=特性不可选；EnsurePickerElements自带防重复" },
            new PatchEntry { Target = typeof(PerkUIController), Method = "OnChange", Prefix = null, Postfix = null, Host = typeof(Patches), Priority = DEFAULT_PRIORITY, System = "Core", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(StartingPerkIconLoader), Method = "Start", Prefix = null, Postfix = "PostfixIconLoaderStart", Host = typeof(Patches), Priority = DEFAULT_PRIORITY, System = "Core", YieldMod = null, Note = "10-04共存：XIAOWO 不注册我方assetId图标，让路=图标消失；只注入我方HasCustomIcon条目安全" },
            // ===== 10-04 本地化四挂点补迁（对齐 cheatsheet 16.1.1）=====
            // 阶段6迁移时漏迁 LocHelper.GetLocalizedPerkTable（旧区块 PatchRegistry.cs:179 已废弃死代码）→ 当前版翻译必然失效。
            // 补迁 ③ + 补挂 ①②④：Prefix 短路（return false）→ XIAOWO 同方法 Postfix 不执行，安全共存。
            new PatchEntry { Target = typeof(StartingPerk), Method = "GetLocalizedDisplayName", Prefix = "PrefixPerkDisplayName", Postfix = null, Host = typeof(Patches), Priority = DEFAULT_PRIORITY, System = "Core", YieldMod = null, Note = "①特性名（选择界面+状态栏）：Prefix短路 return false" },
            new PatchEntry { Target = typeof(StartingPerk), Method = "GetLocalizedDescription", Prefix = "PrefixPerkDescription", Postfix = null, Host = typeof(Patches), Priority = DEFAULT_PRIORITY, System = "Core", YieldMod = null, Note = "②特性描述：Prefix短路 return false" },
            new PatchEntry { Target = typeof(LocHelper), Method = "GetLocalizedPerkTable", Prefix = null, Postfix = "PostfixGetLocalizedPerkTable", Host = typeof(Patches), Priority = DEFAULT_PRIORITY, System = "Core", YieldMod = null, Note = "③perk_{id}_name/desc查表：阶段6漏迁恢复", ParameterTypes = new System.Type[] { typeof(string), typeof(Il2CppReferenceArray<Il2CppSystem.Object>) } },
            new PatchEntry { Target = typeof(StartingPerkElement), Method = "SetTooltipContent", Prefix = null, Postfix = "PostfixPerkTooltip", Host = typeof(Patches), Priority = DEFAULT_PRIORITY, System = "Core", YieldMod = null, Note = "④tooltip文本直写（cheatsheet 16.1.1 最关键点）", ParameterTypes = new System.Type[] { typeof(StartingPerk) } },
            new PatchEntry { Target = typeof(NetworkUpgrade), Method = "Unlock", Prefix = "Prefix", Postfix = "Postfix", Host = typeof(DetectiveUpgradePatch), Priority = DEFAULT_PRIORITY, System = "Detective", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(SecData), Method = "OnFixerUsed", Prefix = "Prefix", Postfix = null, Host = typeof(DetectiveFixerPatch), Priority = DEFAULT_PRIORITY, System = "Detective", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(SecData), Method = "CommitCrime", Prefix = "Prefix", Postfix = null, Host = typeof(DetectiveCommitCrimePatch), Priority = DEFAULT_PRIORITY, System = "Detective", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(NegociationUIManager), Method = "SellItem", Prefix = null, Postfix = "Postfix", Host = typeof(SoldContrabandCounterPatch), Priority = DEFAULT_PRIORITY, System = "ContrabandCounter", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(SecData), Method = "OnNewDay", Prefix = null, Postfix = "Postfix", Host = typeof(SoldEvidenceOnNewDayPatch), Priority = DEFAULT_PRIORITY, System = "SoldEvidence", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(SecData), Method = "OnFixerUsed", Prefix = null, Postfix = "Postfix", Host = typeof(WildeFixerPatch), Priority = DEFAULT_PRIORITY, System = "Wilde", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(BarterHelper), Method = "DoesTraderAcceptThisItemAsPayment", Prefix = null, Postfix = "Postfix", Host = typeof(CounterfeitWineTradeFix), Priority = DEFAULT_PRIORITY, System = "CounterfeitWine", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(StartingPerkElement), Method = "Start", Prefix = null, Postfix = "PostfixStartingPerkElementStart", Host = typeof(Patches), Priority = DEFAULT_PRIORITY, System = "Core", YieldMod = null, Note = "10-04共存：XIAOWO 不处理我方元素，让路=元素裸奔（名字/图标无）" },
            new PatchEntry { Target = typeof(GameItem), Method = "GetNegociatedValue", Prefix = "PrefixGameItemGetNegociatedValue", Postfix = "PostfixGameItemGetNegociatedValue", Host = typeof(Patches), Priority = DEFAULT_PRIORITY, System = "Trade", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(NegociationUIManager), Method = "InitUIWithItemSellMode", Prefix = null, Postfix = "PostfixUIInitSellMode", Host = typeof(Patches), Priority = DEFAULT_PRIORITY, System = "Trade", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(ClientCanExposeFunc), Method = "ClientNoExposeInjector", Prefix = "PrefixClientNoExposeInjector", Postfix = null, Host = typeof(Patches), Priority = DEFAULT_PRIORITY, System = "Trade", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(ItemFeature), Method = "GetClientExposeDialog", Prefix = "PrefixGetClientExposeDialog", Postfix = null, Host = typeof(Patches), Priority = DEFAULT_PRIORITY, System = "Trade", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(NegociationUIManager), Method = "InitUIWithItemBuyMode", Prefix = null, Postfix = "PostfixUIInitBuyMode", Host = typeof(Patches), Priority = DEFAULT_PRIORITY, System = "Trade", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(NegociationUIManager), Method = "CloseUI", Prefix = null, Postfix = "PostfixUIClose", Host = typeof(Patches), Priority = DEFAULT_PRIORITY, System = "Trade", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(GameItem), Method = "GetDisplayName", Prefix = null, Postfix = "PostfixGameItemGetDisplayName", Host = typeof(Patches), Priority = DEFAULT_PRIORITY, System = "Core", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(StartOfDayUIManager), Method = "OpenUI", Prefix = null, Postfix = "PostfixStartOfDayOpenUI", Host = typeof(Patches), Priority = DEFAULT_PRIORITY, System = "Core", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(StartOfDayUIManager), Method = "ShowMorningReport", Prefix = null, Postfix = "PostfixStartOfDayShowMorningReport", Host = typeof(Patches), Priority = DEFAULT_PRIORITY, System = "Core", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(StartOfDayUIManager), Method = "OnStartDayButtonClicked", Prefix = null, Postfix = "PostfixStartOfDayButtonClicked", Host = typeof(Patches), Priority = DEFAULT_PRIORITY, System = "Core", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(RenderHandler), Method = "LoadFromAtlas", Prefix = "PrefixLoadFromAtlas", Postfix = null, Host = typeof(Patches), Priority = DEFAULT_PRIORITY, System = "Core", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(RenderHandler), Method = "LoadFromAtlas", Prefix = "PrefixLoadFromAtlas", Postfix = null, Host = typeof(LuckScoutBackpackUpgrade), Priority = DEFAULT_PRIORITY, System = "LuckScout", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(RenderHandler), Method = "LoadFromAtlas", Prefix = "PrefixLoadFromAtlas", Postfix = null, Host = typeof(DestinyDice), Priority = DEFAULT_PRIORITY, System = "DestinyDice", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(RenderHandler), Method = "LoadFromAtlas", Prefix = "PrefixLoadFromAtlas", Postfix = null, Host = typeof(GuMachineSystem), Priority = DEFAULT_PRIORITY, System = "GuMachine", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(RenderHandler), Method = "LoadFromAtlas", Prefix = "PrefixLoadFromAtlas", Postfix = null, Host = typeof(WageGirlSystem), Priority = DEFAULT_PRIORITY, System = "WageGirl", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(RenderHandler), Method = "LoadFromAtlas", Prefix = "PrefixLoadFromAtlas", Postfix = null, Host = typeof(WageBrother), Priority = DEFAULT_PRIORITY, System = "WageBrother", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(PlayerStore), Method = "SaveGame", Prefix = null, Postfix = "PostfixSaveGame", Host = typeof(WageGirlSystem), Priority = DEFAULT_PRIORITY, System = "WageGirl", YieldMod = null, Note = "只写内存禁 Flush" },
            new PatchEntry { Target = typeof(PlayerStore), Method = "StartNewGame", Prefix = null, Postfix = "PostfixStartNewGame", Host = typeof(WageGirlSystem), Priority = DEFAULT_PRIORITY, System = "WageGirl", YieldMod = null, Note = "" },
            // 10-07 阶段D D-2（删4挂点①）：PlayerStore.SaveGame PostfixSaveGame RobinCrusoePerk（:103 原行）已删——WS SurvivalFood.PostfixSaveGame 承接（双装/单装 WS 存档生存状态）
            new PatchEntry { Target = typeof(PlayerStore), Method = "SaveGame", Prefix = "PrefixSaveGame", Postfix = null, Host = typeof(ContainerUpgradeV2), Priority = DEFAULT_PRIORITY, System = "ContainerUpgradeV2", YieldMod = null, Note = "只写内存禁 Flush" },
            // 10-03 阶段B：SaveStore 落盘挂点（原92/93行）已删——统一落盘通道归 WageAPI（SaveGame/EndDay Postfix priority=0 最后跑，Core.cs 注册），防双落盘
            new PatchEntry { Target = typeof(PlayerStore), Method = "EndDay", Prefix = null, Postfix = "PostfixEndDay", Host = typeof(WageBrokerPermitPatches), Priority = -100, System = "WageBroker", YieldMod = null, Note = "蛙哥许可：visitLeftTonight += 持有张数（10-05 实锤：夜猫子=XIAOWO三更行者 [HarmonyPriority(0)] 后跑 Math.Max(原值,3) 设下限 → 我们 priority 300 先跑被 max 吞；改 -100 后跑叠加）" },
            new PatchEntry { Target = typeof(PlayerStore), Method = "EndDay", Prefix = null, Postfix = "PostfixEndDayCharger", Host = typeof(WageBrokerPermitPatches), Priority = 300, System = "WageBroker", YieldMod = null, Note = "蛙哥充电器：打烊自动给背包电池充电" },
            new PatchEntry { Target = typeof(HealthData), Method = "ReceiveMinorWound", Prefix = "PrefixReceiveMinorWound", Postfix = null, Host = typeof(WageBrokerPermitPatches), Priority = DEFAULT_PRIORITY, System = "WageBroker", YieldMod = null, Note = "蛙哥许可：拾荒轻伤免疫（10-05 拆包实锤原 ScavHelper.RollMinorWound 死 API 白挂，改挂受伤唯一施加点 Receive*）" },
            new PatchEntry { Target = typeof(HealthData), Method = "ReceiveMajorWound", Prefix = "PrefixReceiveMajorWound", Postfix = null, Host = typeof(WageBrokerPermitPatches), Priority = DEFAULT_PRIORITY, System = "WageBroker", YieldMod = null, Note = "蛙哥许可：拾荒重伤免疫（同上，白挂修正）" },
            new PatchEntry { Target = typeof(HealthData), Method = "HandleNightlyWound", Prefix = "PrefixHandleNightlyWound", Postfix = null, Host = typeof(WageBrokerPermitPatches), Priority = DEFAULT_PRIORITY, System = "WageBroker", YieldMod = null, Note = "蛙哥许可：夜间受伤免疫（10-05 拆包实锤夜间受伤不走 Receive*，OnSleep 概率判定直接 dec 健康值——三级免疫含夜间）" },
            new PatchEntry { Target = typeof(StoreEventManager), Method = "OnDayStart", Prefix = null, Postfix = "OnDayStartPostfix", Host = typeof(ModCannibalism), Priority = DEFAULT_PRIORITY, System = "ModCannibalism", YieldMod = null, Note = "10-04共存：XIAOWO现金流纯hash，双Postfix安全" },
            new PatchEntry { Target = typeof(StoreEventManager), Method = "OnDayStart", Prefix = null, Postfix = "OnDayStartPostfix", Host = typeof(BatteryCannibalism), Priority = DEFAULT_PRIORITY, System = "BatteryCannibalism", YieldMod = null, Note = "10-04共存：XIAOWO现金流纯hash，双Postfix安全" },
            new PatchEntry { Target = typeof(StoreEventManager), Method = "OnDayStart", Prefix = null, Postfix = "OnDayStartPostfix", Host = typeof(GuMachineSystem), Priority = DEFAULT_PRIORITY, System = "GuMachine", YieldMod = null, Note = "10-04共存：XIAOWO现金流纯hash，双Postfix安全" },
            new PatchEntry { Target = typeof(StoreEventManager), Method = "OnDayStart", Prefix = null, Postfix = "PostfixOnDayStart", Host = typeof(WageGirlSystem), Priority = DEFAULT_PRIORITY, System = "WageGirl", YieldMod = null, Note = "10-04共存：XIAOWO现金流纯hash，双Postfix安全" },
            new PatchEntry { Target = typeof(StoreEventManager), Method = "OnDayStart", Prefix = null, Postfix = "PostfixStoreEventOnDayStart", Host = typeof(DestinyDice), Priority = DEFAULT_PRIORITY, System = "DestinyDice", YieldMod = null, Note = "10-04共存：XIAOWO现金流纯hash，双Postfix安全；10-05门槛回落已挪EndDay，本挂点仅剩事件注入+联动" },
            new PatchEntry { Target = typeof(PlayerStore), Method = "EndDay", Prefix = null, Postfix = "PostfixEndDayDiceThreshold", Host = typeof(DestinyDice), Priority = DEFAULT_PRIORITY, System = "DestinyDice", YieldMod = null, Note = "10-05骰子门槛打烊回落：旧挂OnDayStart玩家感知打烊不回落且每天只回一档多掷净涨；改EndDay+归位400" },
            new PatchEntry { Target = typeof(StoreEventManager), Method = "OnDayStart", Prefix = null, Postfix = "PostfixUnifiedDayStart", Host = typeof(Patches), Priority = DEFAULT_PRIORITY, System = "Core", YieldMod = null, Note = "10-04共存：XIAOWO现金流纯hash，双Postfix安全" },
            new PatchEntry { Target = typeof(PlayerStore), Method = "SaveGame", Prefix = null, Postfix = "PostfixSaveGame", Host = typeof(Patches), Priority = DEFAULT_PRIORITY, System = "Core", YieldMod = null, Note = "统一驱动：只写内存禁 Flush，WageSaveStore 收尾落盘" },
            new PatchEntry { Target = typeof(NewsUIManager), Method = "PopulateUI", Prefix = null, Postfix = "PostfixNewsPopulateUI", Host = typeof(DestinyDice), Priority = DEFAULT_PRIORITY, System = "DestinyDice", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(NewsUIManager), Method = "PopulateUI", Prefix = null, Postfix = "PostfixNewsPopulateUI", Host = typeof(ModCannibalism), Priority = DEFAULT_PRIORITY, System = "ModCannibalism", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(ModuleHelper), Method = "CreateModuleTooltip", Prefix = null, Postfix = "PostfixCreateModuleTooltip", Host = typeof(ModCannibalism), Priority = DEFAULT_PRIORITY, System = "ModCannibalism", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(AdvCalendarUIManager), Method = "OnNightlyReportButtonClicked", Prefix = null, Postfix = "PostfixOnNightlyReportButtonClicked", Host = typeof(ModCannibalism), Priority = DEFAULT_PRIORITY, System = "ModCannibalism", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(AdvCalendarUIManager), Method = "OpenUIFromNightlyReport", Prefix = null, Postfix = "PostfixOpenUIFromNightlyReport", Host = typeof(ModCannibalism), Priority = DEFAULT_PRIORITY, System = "ModCannibalism", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(AdvCalendarUIManager), Method = "OnStatusButtonClicked", Prefix = null, Postfix = "PostfixOnStatusButtonClicked", Host = typeof(ModCannibalism), Priority = DEFAULT_PRIORITY, System = "ModCannibalism", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(NewsUIManager), Method = "ToggleUI", Prefix = null, Postfix = "PostfixNewsPopulateUI", Host = typeof(ModCannibalism), Priority = DEFAULT_PRIORITY, System = "ModCannibalism", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(NewsUIManager), Method = "ToggleUI", Prefix = null, Postfix = "PostfixNewsToggleUI", Host = typeof(DestinyDice), Priority = DEFAULT_PRIORITY, System = "DestinyDice", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(NewsUIManager), Method = "CloseUI", Prefix = null, Postfix = "PostfixNewsCloseUI", Host = typeof(DestinyDice), Priority = DEFAULT_PRIORITY, System = "DestinyDice", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(InputActionManager), Method = "Update", Prefix = null, Postfix = "PostfixNewsInputUpdate", Host = typeof(DestinyDice), Priority = DEFAULT_PRIORITY, System = "DestinyDice", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(GameItem), Method = "MayHaveValidInventorySlot", Prefix = "PrefixMayHaveValidInventorySlot", Postfix = null, Host = typeof(DestinyDice), Priority = DEFAULT_PRIORITY, System = "DestinyDice", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(GameItem), Method = "MayTarget", Prefix = "PrefixMayTarget", Postfix = null, Host = typeof(DestinyDice), Priority = DEFAULT_PRIORITY, System = "DestinyDice", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(GameItem), Method = "CanTarget", Prefix = "PrefixCanTarget", Postfix = null, Host = typeof(DestinyDice), Priority = DEFAULT_PRIORITY, System = "DestinyDice", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(GameItem), Method = "Target", Prefix = "PrefixTarget", Postfix = null, Host = typeof(DestinyDice), Priority = DEFAULT_PRIORITY, System = "DestinyDice", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(GameItem), Method = "Target", Prefix = "PrefixTarget", Postfix = null, Host = typeof(TurboBoostN), Priority = DEFAULT_PRIORITY, System = "TurboBoostN", YieldMod = null, Note = "10-02 吞噬涡轮二次维系：Prefix补CURRENT_CHARGE+READY" },
            new PatchEntry { Target = typeof(GameItem), Method = "Target", Prefix = "PostfixTarget", Postfix = null, Host = typeof(TurboBoostN), Priority = DEFAULT_PRIORITY, System = "TurboBoostN", YieldMod = null, Note = "设_turboMachine/_turboN标记" },
            new PatchEntry { Target = typeof(ItemMouseDoubleClickHandler), Method = "DoubleClickAction", Prefix = "PrefixDoubleClickAction", Postfix = null, Host = typeof(DestinyDice), Priority = DEFAULT_PRIORITY, System = "DestinyDice", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(ContainerItemDirectory), Method = "InitDirectory", Prefix = null, Postfix = "PostfixInitDirectory", Host = typeof(Patches), Priority = DEFAULT_PRIORITY, System = "Core", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(AmenitiesItemDirectory), Method = "InitDirectory", Prefix = null, Postfix = "PostfixInitDirectory", Host = typeof(Patches), Priority = DEFAULT_PRIORITY, System = "Core", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(ModItemDirectory), Method = "InitDirectory", Prefix = null, Postfix = "PostfixInitDirectory", Host = typeof(Patches), Priority = DEFAULT_PRIORITY, System = "Core", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(StartingPerkElement), Method = "OnPointerClick", Prefix = "PrefixOnPointerClick", Postfix = "PostfixOnPointerClick", Host = typeof(Patches), Priority = DEFAULT_PRIORITY, System = "Core", YieldMod = null, Note = "" },
            // 10-07 鲁滨逊完全归 WS（删开局 6）：NewStartTypeUI 6 处（MainMenuUIController.Awake/ResetAllTab、NewGameData.GetStartDisplayName、SaveFiles.BuildPreviewFromStore、PlayerStore.SaveGame/LoadGame，原 :140-145）已删——WS NewStartTypeUI.cs 承接（单装 WP 无鲁滨逊开局 tab=归 WS 既定语义）
            new PatchEntry { Target = typeof(StoreClientManager), Method = "HandleMinorClient", Prefix = "PrefixHandleMinorClient", Postfix = null, Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "" },
            // 10-07 阶段D D-2（删4挂点②）：StoreClientManager.PickClient PrefixPickClient RobinCrusoePerk（原 :147）已删——WS SurvivalTrade.PostfixStoreClientManagerPickClient 承接（鲁滨逊预算，startType=15 门控）
            // 10-07 阶段D D-2（删4挂点③）：MapUIManager.OpenGoOutsideConfirm PrefixOpenGoOutsideConfirm RobinCrusoePerk（原 :148）已删——WS SurvivalFood.PrefixOpenGoOutsideConfirm 承接（禁外出）
            // 10-03 蛙哥交易站：地图按钮注入（暂时封存）
            // new PatchEntry { Target = typeof(MapUIManager), Method = "OpenUI", Prefix = null, Postfix = "PostfixMapOpenUI", Host = typeof(WageBrokerStation), Priority = DEFAULT_PRIORITY, System = "WageBroker", YieldMod = null, Note = "蛙哥交易站地图按钮" },
            // // 10-03 蛙哥交易站：场景门控（学Brewing）
            // new PatchEntry { Target = typeof(MapUIManager), Method = "VisitUpgradeMerchant", Prefix = "PrefixVisitUpgradeMerchant", Postfix = null, Host = typeof(WageBrokerStation), Priority = DEFAULT_PRIORITY, System = "WageBroker", YieldMod = null, Note = "进场景前还原" },
            // new PatchEntry { Target = typeof(MapUIManager), Method = "HandleLeaveUpgradeMerchant", Prefix = null, Postfix = "PostfixHandleLeaveUpgradeMerchant", Host = typeof(WageBrokerStation), Priority = DEFAULT_PRIORITY, System = "WageBroker", YieldMod = null, Note = "离开复位" },
            // new PatchEntry { Target = typeof(StoreClientList), Method = "PlaceInventorInventory", Prefix = "PrefixPlaceInventorInventory", Postfix = "PostfixPlaceInventorInventory", Host = typeof(WageBrokerStation), Priority = DEFAULT_PRIORITY, System = "WageBroker", YieldMod = null, Note = "场景铺货门控" },
            // new PatchEntry { Target = typeof(PlayerStore), Method = "AddDirectSellingItemToTable", Prefix = "PrefixAddDirectSellingItemToTable", Postfix = null, Host = typeof(WageBrokerStation), Priority = DEFAULT_PRIORITY, System = "WageBroker", YieldMod = null, Note = "拦原生货" },
            // 10-07 阶段D D-2（删4挂点④）：StoreEventManager.OnDayStart PostfixOnNewDay RobinCrusoePerk（原 :156）已删——WS SurvivalFood.PostfixOnDayStart（衰减）+ SurvivalNightReport.AppendLedgerReport（账本夜报，D-1 先补）承接
            new PatchEntry { Target = typeof(StoreClientManager), Method = "HandleInspectionClient", Prefix = "PrefixHandleInspectionClient", Postfix = "PostfixHandleInspectionClient", Host = typeof(Patches), Priority = DEFAULT_PRIORITY, System = "Core", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(StoreReputation), Method = "IsPerkUnlocked", Prefix = null, Postfix = "PostfixIsPerkUnlocked", Host = typeof(Patches), Priority = DEFAULT_PRIORITY, System = "Trade", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(GameItem), Method = "MayTarget", Prefix = "PrefixMayTarget", Postfix = null, Host = typeof(WageGirlSystem), Priority = DEFAULT_PRIORITY, System = "WageGirl", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(GameItem), Method = "CanTarget", Prefix = "PrefixCanTarget", Postfix = null, Host = typeof(WageGirlSystem), Priority = DEFAULT_PRIORITY, System = "WageGirl", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(GameItem), Method = "Target", Prefix = "PrefixTarget", Postfix = null, Host = typeof(WageGirlSystem), Priority = DEFAULT_PRIORITY, System = "WageGirl", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(StoreClient), Method = "CanClientExposeAnyFeature", Prefix = "PrefixCanClientExposeAnyFeature", Postfix = null, Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(HusbandryHelper), Method = "CreateItemTooltip", Prefix = null, Postfix = "PostfixCreateItemTooltip", Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(HusbandryHelper), Method = "CreateItemTooltip", Prefix = null, Postfix = "PostfixWageBoxTooltip", Host = typeof(ContainerUpgradeV2), Priority = DEFAULT_PRIORITY, System = "ContainerUpgradeV2", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(HealthData), Method = "ReceiveMinorWound", Prefix = "PrefixReceiveWound", Postfix = null, Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(HealthData), Method = "ReceiveMajorWound", Prefix = "PrefixReceiveWound", Postfix = null, Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(HealthData), Method = "ReceiveMinorWound", Prefix = null, Postfix = "PostfixReceiveMinorWound", Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(HealthData), Method = "ReceiveMajorWound", Prefix = null, Postfix = "PostfixReceiveMajorWound", Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "" },
            // 10-07 鲁滨逊完全归 WS WS-3（删 P1 拾荒 4）：ScavHelper.CanScavenge/GetMaxScavAttempts/GetScavTimeLeft/GetRandomScavengedItem Postfix RobinCrusoePerk（原 :169-172）已删——WS SurvivalScavenge.cs 承接（含哨兵 1% 免疫宁）
            // 10-07 鲁滨逊完全归 WS WS-3（删 P1 机器/模块 5）：MachineryHelper.GetCurrentPerformanceBonus/GetCurrentQualityBonus + ModuleHelper.ApplyBasicModuleEffect + ModuleEffectHelper.ModifyTempStatFromBaseByPercentage + ModuleHelper.AddModuleStatLine Postfix RobinCrusoePerk（原 :173-177）已删——WS SurvivalMachineMods.cs 承接（CrusoeEffHalf 固定 0.5）
            // 10-07 鲁滨逊完全归 WS WS-3（删 P1 机器 2）：ModuleHelper.ApplyPerformanceWaterRecyclerEffect + MachineMoistureFarm.GetOutputVolume Postfix RobinCrusoePerk（原 :179-180）已删——WS SurvivalMachineMods.cs 承接（GetOutputVolume DryAir 分支裁剪——双装由 WP DryAirPerk 独立挂点叠加）
            // 10-07 鲁滨逊完全归 WS WS-3（删 P1 净化 1）：WaterHelper.RemoveContaminantFromContainer Postfix RobinCrusoePerk（原 :188）已删——WS SurvivalMachineMods.cs 承接
            new PatchEntry { Target = typeof(MachineryHelper), Method = "CreateMachineryTooltip", Prefix = null, Postfix = "PostfixCreateMachineryTooltip", Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "" },
            // 10-07 鲁滨逊完全归 WS WS-3（删 P1 机器 2）：ModuleHelper.ApplyPerformanceWaterRecyclerEffect + MachineMoistureFarm.GetOutputVolume Postfix RobinCrusoePerk（原 :179-180）已删——WS SurvivalMachineMods.cs 承接（GetOutputVolume DryAir 分支裁剪——双装由 WP DryAirPerk 独立挂点叠加）
            // 10-07 鲁滨逊完全归 WS WS-3（删 P1 净化 1）：WaterHelper.RemoveContaminantFromContainer Postfix RobinCrusoePerk（原 :188）已删——WS SurvivalMachineMods.cs 承接
            new PatchEntry { Target = typeof(MachineTurboBoosterAdv), Method = "UpdateSprite", Prefix = null, Postfix = "PostfixUpdateSprite", Host = typeof(TurboBoostN), Priority = DEFAULT_PRIORITY, System = "TurboBoostN", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(MachineMoistureFarm), Method = "Fill", Prefix = "PrefixFill", Postfix = null, Host = typeof(TurboBoostN), Priority = DEFAULT_PRIORITY, System = "TurboBoostN", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(MachinePurifier), Method = "PurifyContainer", Prefix = null, Postfix = "PostfixPurifyContainer", Host = typeof(TurboBoostN), Priority = DEFAULT_PRIORITY, System = "TurboBoostN", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(WineHelper), Method = "OnAgeWine", Prefix = null, Postfix = "PostfixOnAgeWine", Host = typeof(TurboBoostN), Priority = DEFAULT_PRIORITY, System = "TurboBoostN", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(MachineProgressHelper), Method = "ContinueProgressTypeMachine", Prefix = null, Postfix = "PostfixContinueProgress", Host = typeof(TurboBoostN), Priority = DEFAULT_PRIORITY, System = "TurboBoostN", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(MachineHelper), Method = "OnMachineActioned", Prefix = null, Postfix = "PostfixOnMachineActioned", Host = typeof(TurboBoostN), Priority = DEFAULT_PRIORITY, System = "TurboBoostN", YieldMod = null, Note = "10-02 熔炉补产：补(N-1)*4个scrap" },
            new PatchEntry { Target = typeof(MachineTurboBoosterAdv), Method = "CreateMachineTooltip", Prefix = null, Postfix = "PostfixCreateTooltip", Host = typeof(TurboBoostN), Priority = DEFAULT_PRIORITY, System = "TurboBoostN", YieldMod = null, Note = "" },
            // 10-07 鲁滨逊完全归 WS WS-3（删 P1 净化 1）：WaterHelper.RemoveContaminantFromContainer Postfix RobinCrusoePerk（原 :188）已删——WS SurvivalMachineMods.cs 承接
            new PatchEntry { Target = typeof(GameItem), Method = "MayTarget", Prefix = "PrefixMayTarget", Postfix = null, Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(GameItem), Method = "CanTarget", Prefix = "PrefixCanTarget", Postfix = null, Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(GameItem), Method = "Target", Prefix = "PrefixTarget", Postfix = null, Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(GameItem), Method = "MayHaveValidInventorySlot", Prefix = "PrefixMayHaveValidInventorySlot", Postfix = null, Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(GameItem), Method = "MayTarget", Prefix = "PrefixMayTarget_WageBox", Postfix = null, Host = typeof(ContainerUpgradeV2), Priority = DEFAULT_PRIORITY, System = "ContainerUpgradeV2", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(GameItem), Method = "CanTarget", Prefix = "PrefixCanTarget_WageBox", Postfix = null, Host = typeof(ContainerUpgradeV2), Priority = DEFAULT_PRIORITY, System = "ContainerUpgradeV2", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(GameItem), Method = "Target", Prefix = "PrefixTarget_WageBox", Postfix = null, Host = typeof(ContainerUpgradeV2), Priority = DEFAULT_PRIORITY, System = "ContainerUpgradeV2", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(GameItem), Method = "MayHaveValidInventorySlot", Prefix = "PrefixMayHaveValidInventorySlot_WageBox", Postfix = null, Host = typeof(ContainerUpgradeV2), Priority = DEFAULT_PRIORITY, System = "ContainerUpgradeV2", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(GameItem), Method = "MayHaveValidInventorySlot", Prefix = "PrefixMayHaveValidInventorySlot", Postfix = null, Host = typeof(GuMachineSystem), Priority = DEFAULT_PRIORITY, System = "GuMachine", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(GameItem), Method = "MayTarget", Prefix = "PrefixMayTarget", Postfix = null, Host = typeof(LuckScoutBackpackUpgrade), Priority = DEFAULT_PRIORITY, System = "LuckScout", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(GameItem), Method = "CanTarget", Prefix = "PrefixCanTarget", Postfix = null, Host = typeof(LuckScoutBackpackUpgrade), Priority = DEFAULT_PRIORITY, System = "LuckScout", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(GameItem), Method = "Target", Prefix = "PrefixTarget", Postfix = null, Host = typeof(LuckScoutBackpackUpgrade), Priority = DEFAULT_PRIORITY, System = "LuckScout", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(PlayerStore), Method = "StartNewGame", Prefix = null, Postfix = "PostfixStartNewGame", Host = typeof(WandererPerk), Priority = DEFAULT_PRIORITY, System = "Wanderer", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(PlayerStore), Method = "StartNewGame", Prefix = null, Postfix = "PostfixStartNewGame", Host = typeof(InfamousPerk), Priority = DEFAULT_PRIORITY, System = "Infamous", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(Il2Cpp.StoreService), Method = "UpdateCost", Prefix = null, Postfix = "PostfixUpdateCost", Host = typeof(InfamousPerk), Priority = DEFAULT_PRIORITY, System = "Infamous", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(Il2Cpp.ItemMultiSelectHandler), Method = "EndGroupDrag", Prefix = "PrefixEndGroupDrag", Postfix = "PostfixEndGroupDrag", Host = typeof(BatchDragUpgrade), Priority = DEFAULT_PRIORITY, System = "BatchDrag", YieldMod = null, Note = "" },
            // 10-07 阶段D D-2 补删（第5条）：PlayerStore.StartNewGame PostfixStartNewGame RobinCrusoePerk（原 :205）已删——WS SaveStore.PostfixStartNewGame 承接（清血量缓存+清命名空间+设默认六维，让路 4 点含 StartNewGame）
            new PatchEntry { Target = typeof(PlayerStore), Method = "StartNewGame", Prefix = null, Postfix = "PostfixStartNewGame", Host = typeof(DrJacksonFriendPerk), Priority = DEFAULT_PRIORITY, System = "DrJacksonFriend", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(PlayerStore), Method = "StartNewGame", Prefix = null, Postfix = "PostfixStartNewGame", Host = typeof(WaterMerchantPerk), Priority = DEFAULT_PRIORITY, System = "WaterMerchant", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(PlayerStore), Method = "LoadGame", Prefix = null, Postfix = "PostfixLoadGame_IngotContainer", Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "" },
            // 10-07 鲁滨逊完全归 WS WS-3（删 AutoSip 1）：LiquidContainerHelper.AutoSipFromContainer Prefix RobinCrusoePerk（原 :209）已删——WS SurvivalFood.PrefixAutoSipFromContainer 承接（任务#20 已复制，本波去双挂）
            // 10-07 鲁滨逊完全归 WS WS-3（删 P1 经济链 1）：StoreClientList.PlaceSupplierInventory Postfix RobinCrusoePerk（原 :210）已删——WS SurvivalEconomy.cs 承接（供货商卖水药食物，同日不重复）
            new PatchEntry { Target = typeof(EmporiumEntry), Method = "Start", Prefix = null, Postfix = "PostfixEmporiumEntryStart", Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "" },
            // 10-07 鲁滨逊完全归 WS WS-3（删 P1 经济链 6+耗电 1）：GetMachinePowerUsage/HandleInsurance/ScavengeDumpingGrounds/CheckRentDay/_LandlordWholesale_b__22_0/HandleSupplierClient/ExecuteGameOver/HandleNormalClient Postfix/Prefix RobinCrusoePerk（原 :212-219）已删——WS SurvivalEconomy.cs + SurvivalMachineMods.cs 承接
            new PatchEntry { Target = typeof(StorePhoneClient), Method = "InitPhoneClientDict", Prefix = null, Postfix = "PostfixInitPhoneClientDict", Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(StorePhoneClient), Method = "GetCallDialog", Prefix = null, Postfix = "PostfixGetCallDialog", Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "10-06 C2奥丁对话：强通帧覆盖为RevMerchantPhoneDialog(odin_hold+排期全走原版显示链)" },
            new PatchEntry { Target = typeof(PhoneUIManager), Method = "WillAnswerCall", Prefix = "PrefixWillAnswerCall", Postfix = null, Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(PhoneUIManager), Method = "WillAnswerCall", Prefix = null, Postfix = "PostfixWillAnswerCall", Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(PhoneUIManager), Method = "StartPhoneDialog", Prefix = "PrefixStartPhoneDialog", Postfix = null, Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(ContactElement), Method = "OnInit", Prefix = null, Postfix = "PostfixOnContactInit", Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(PhoneUIManager), Method = "AutoCall", Prefix = "PrefixAutoCall", Postfix = null, Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(PhoneUIManager), Method = "HandleCall", Prefix = "PrefixHandleCall", Postfix = null, Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(StoreClientListWanted), Method = "CreateWanted6", Prefix = null, Postfix = "PostfixCreateWanted6", Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(DirectoryMaster), Method = "Item", Prefix = "PrefixDirectoryMasterItem", Postfix = null, Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(WantedElement), Method = "OnArrested", Prefix = null, Postfix = "PostfixWantedElementOnArrested", Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(StoreUIManager), Method = "OnGenericArrived", Prefix = "PrefixStoreUIManagerOnGenericArrived", Postfix = null, Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(AugHelper), Method = "CleanupKill", Prefix = null, Postfix = "PostfixAugHelperCleanupKill", Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(AdvCalendarUIManager), Method = "OnCalendarButtonClicked", Prefix = null, Postfix = "PostfixOnCalendarButtonClicked", Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(StoreCalendar), Method = "Update", Prefix = null, Postfix = "PostfixStoreCalendarUpdate", Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(StartOfDayUIManager), Method = "InitPanel", Prefix = null, Postfix = "PostfixStartOfDayInitPanel", Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "" },
            // 10-07 鲁滨逊完全归 WS（决策 1：交易归 WS——删交易 6 之①）：StoreClient.ApplyBudgetModifier Postfix Host=Patches（原 :236）已删——WS SurvivalTrade.PostfixStoreClientApplyBudgetModifier 承接
            new PatchEntry { Target = typeof(StoreClient), Method = "OverrideBudget", Prefix = null, Postfix = "PostfixOverrideBudget", Host = typeof(CompatibilityPatches), Priority = DEFAULT_PRIORITY, System = "Compat", YieldMod = null, Note = "10-05第三方溢出兜底(CustomerCreditBoost指数累乘int溢出写负,原版零clamp)" },
            new PatchEntry { Target = typeof(StoreClient), Method = "ApplyBudgetModifier", Prefix = null, Postfix = "PostfixApplyBudgetModifier", Host = typeof(WageGirlSystem), Priority = DEFAULT_PRIORITY, System = "WageGirl", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(BargainUIManager), Method = "GetDealMakerBonus", Prefix = null, Postfix = "PostfixGetDealMakerBonus", Host = typeof(WageGirlSystem), Priority = DEFAULT_PRIORITY, System = "WageGirl", YieldMod = null, Note = "已实锤叠加：WageGirlSystem 蛙娘在场+50封顶100 / Patches 鲁滨逊+GetBargainBonusPct，双宿主同名 Postfix" },
            new PatchEntry { Target = typeof(GameItemElement), Method = "ApplyAnimationFrame", Prefix = "PrefixApplyAnimationFrame", Postfix = null, Host = typeof(WageGirlSystem), Priority = DEFAULT_PRIORITY, System = "WageGirl", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(GameItemElement), Method = "ResolveSpriteByName", Prefix = "PostfixResolveSpriteByName", Postfix = null, Host = typeof(WageGirlSystem), Priority = DEFAULT_PRIORITY, System = "WageGirl", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(StoreClientManager), Method = "PickClient", Prefix = null, Postfix = "PostfixStoreClientManagerPickClient", Host = typeof(Patches), Priority = DEFAULT_PRIORITY, System = "Core", YieldMod = null, Note = "" },
            // 10-07 鲁滨逊完全归 WS（决策 1：交易归 WS——删交易 6 之②-⑥）：BargainUIManager.OfferBuyingMarkup/GetDealMakerBonus + ItemFeatureList.BargainBuyingMarkup + ItemFeature.GetActualDisplay + StoreClient.OnDealAccepted Host=Patches（原 :243-247）已删——WS SurvivalTrade.cs 承接（交易域唯一宿主）
            new PatchEntry { Target = typeof(PlayerStore), Method = "LoadGame", Prefix = null, Postfix = "PostfixLoadGameOldSaveHint", Host = typeof(NewStartTypeUI), Priority = DEFAULT_PRIORITY, System = "NewStartType", YieldMod = null, Note = "10-07 WS-4 旧档引导：无 WS 读鲁滨逊档弹提示装 WageSurvival（会话内一次）" },
            new PatchEntry { Target = typeof(StoreClient), Method = "StartMainDialogue", Prefix = null, Postfix = "PostfixStartMainDialogue", Host = typeof(ThiefMagnetSystem), Priority = DEFAULT_PRIORITY, System = "ThiefMagnet", YieldMod = null, Note = "10-06 小偷对话上桌（BarterOffer对小偷失效——StartMainDialogue=客户对话入口实锤 StoreClient.txt:6181）" },
            new PatchEntry { Target = typeof(PlayerStore), Method = "LoadGame", Prefix = null, Postfix = "PostfixLoadGameThiefMagnet", Host = typeof(ThiefMagnetSystem), Priority = DEFAULT_PRIORITY, System = "ThiefMagnet", YieldMod = null, Note = "10-06 小偷读档重挂（eventSourceId运行时标记不随档→双键判定+KEY_STOLEN重挂）" },
            new PatchEntry { Target = typeof(PlayerStore), Method = "OnItemBought", Prefix = null, Postfix = "PostfixPlayerStoreOnItemBought", Host = typeof(ThiefMagnetSystem), Priority = DEFAULT_PRIORITY, System = "ThiefMagnet", YieldMod = null, Note = "10-06 被偷物买回精确清账（OnItemBought=三条购买路径统一汇点，参数=被买物品——拆包5F6k3jY7Kz628HwFRxWAWR；OnDealAccepted死路不挂）" },
            new PatchEntry { Target = typeof(StoreClientListTier), Method = "CreateThirstySpacer", Prefix = null, Postfix = "PostfixCreateThirstySpacer", Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(StoreClientListTier), Method = "CreateHungrySpacer", Prefix = null, Postfix = "PostfixCreateHungrySpacer", Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(StoreClientListTier), Method = "CreateSpacerChef", Prefix = null, Postfix = "PostfixCreateSpacerChef", Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(StoreClientListTier), Method = "CreateInjuredSpacer", Prefix = null, Postfix = "PostfixCreateInjuredSpacer", Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(StoreClientListTier), Method = "CreateSickChildCaretaker", Prefix = null, Postfix = "PostfixCreateSickChildCaretaker", Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(StoreClientListTierSubstance), Method = "CreateDesperateAddict", Prefix = null, Postfix = "PostfixCreateDesperateAddict", Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(StoreClientListTierSubstance), Method = "CreateWornOutSpacer", Prefix = null, Postfix = "PostfixCreateWornOutSpacer", Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(StoreClientListMinor), Method = "CreateSickLowers", Prefix = null, Postfix = "PostfixCreateSickLowers", Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "" },

            // ===== 补迁11条（TryPatchByName ×8 + DoubleClickAction ×3） =====
            // 8条 TryPatchByName
            // 10-04 修复：YieldMod=BrewingExpansion 会让路瓶印机装水挂点 → 装了酿酒拓展就打印无水（Latest.log 实锤已加载）
            // 双方 Harmony patch 安全共存：对方=酒桶增产（不同 bottleId），我方=瓶印机装水（仅 large_bottled_water→water_jug+质量装水），互不干扰
            new PatchEntry { Target = typeof(MachineBottlePrinter.__c__DisplayClass6_0), Method = "Method_Internal_Void_String_Int32_0", Prefix = "PrefixTryPrint", Postfix = "PostfixTryPrint", Host = typeof(WaterMerchantPerk), Priority = DEFAULT_PRIORITY, System = "WaterMerchant", YieldMod = null, Note = "瓶印机装水(水商之友)；与BrewingExpansion酒桶共存", UseByName = true },
            // 10-07 鲁滨逊完全归 WS（删饲料机 1）：MachineFeedDispenser._CreateFeedDispenser_b__3 PrefixFeedDispenserB3 RobinCrusoePerk（原 :265）已删——WS SurvivalFood.PrefixFeedDispenserB3 承接（任务#20 已复制，本波去双挂）
            new PatchEntry { Target = typeof(StoreClient), Method = "GetTradeRepMultiplier", Prefix = null, Postfix = "PostfixGetTradeRepMultiplier", Host = typeof(Patches), Priority = DEFAULT_PRIORITY, System = "Trade", YieldMod = null, Note = "交易声望倍率", UseByName = true },
            new PatchEntry { Target = typeof(BargainUIManager), Method = "ComputeRepPer1000Credits", Prefix = null, Postfix = "PostfixComputeRepPer1000Credits", Host = typeof(Patches), Priority = DEFAULT_PRIORITY, System = "Trade", YieldMod = null, Note = "声望换算", UseByName = true },
            // 10-07 鲁滨逊完全归 WS（删食物tooltip 1）：FoodItemHelper.CreateFoodItemTooltip PrefixFoodTooltip/PostfixFoodTooltip RobinCrusoePerk（原 :268）已删——WS SurvivalFood 承接（任务#20 已复制，本波去双挂）
            new PatchEntry { Target = typeof(ContainerHelper), Method = "InitContainerItem", Prefix = null, Postfix = "PostfixInitContainerItem", Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "10-04共存：我方鲁滨逊容器减半(改容量) vs NestedStorage RelaxInv(清校验委托)——不同数据面安全", UseByName = true },
            new PatchEntry { Target = typeof(GunHelper), Method = "InitGun", Prefix = null, Postfix = "PostfixInitGun", Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "枪初始化", UseByName = true },
            new PatchEntry { Target = typeof(StoreClient), Method = "ClientExposeFeature", Prefix = "PrefixClientExposeFeature", Postfix = null, Host = typeof(Patches), Priority = DEFAULT_PRIORITY, System = "Trade", YieldMod = null, Note = "违禁品跳过曝光链", UseByName = true },
            // 3条 DoubleClickAction（带parameterTypes）
            new PatchEntry { Target = typeof(ItemMouseDoubleClickHandler), Method = "DoubleClickAction", Prefix = "PrefixDoubleClickAction", Postfix = null, Host = typeof(WageBrother), Priority = DEFAULT_PRIORITY, System = "WageBrother", YieldMod = null, Note = "双击蛙哥", ParameterTypes = new System.Type[] { typeof(GameItem), typeof(UnityEngine.Vector2) } },
            // 10-07 阶段D D-5：ItemMouseDoubleClickHandler.DoubleClickAction PostfixDoubleClickAction RobinCrusoePerk（原 :274）已删——WS SurvivalConsume.PostfixDoubleClickAction 承接（全局双击吃喝用）
            new PatchEntry { Target = typeof(ItemMouseDoubleClickHandler), Method = "DoubleClickAction", Prefix = null, Postfix = "PostfixDoubleClickAction", Host = typeof(WageGirlSystem), Priority = DEFAULT_PRIORITY, System = "WageGirl", YieldMod = null, Note = "双击喂蛙娘", ParameterTypes = new System.Type[] { typeof(GameItem), typeof(UnityEngine.Vector2) } },
        };

        public static void ApplyAll()
        {
            // 特殊：动态GetType(PreBuildChemHelper)——不能放静态REGISTRY
            try
            {
                System.Type type = System.Type.GetType("PreBuildChemHelper, Assembly-CSharp");
                if (type != null)
                {
                    ManualPatcher.TryPatch(type, "CreateAcidBottle", "PrefixCreateAcidBottle");
                    ManualPatcher.TryPatch(type, "CreateBaseBottle", "PrefixCreateBaseBottle");
                }
            }
            catch { }

            int ok = 0, fail = 0, yield = 0;
            foreach (var e in REGISTRY)
            {
                try
                {
                    if (e.YieldMod != null && ModCompat.ShouldYield(e.Target, e.Method, out var mod))
                    {
                        Core.LogMsg($"[统一注册] 让路: [{e.System}] {e.Target.Name}.{e.Method} ← {mod}");
                        yield++; continue;
                    }
                    if (e.UseByName)
                    {
                        ManualPatcher.TryPatchByName(e.Target, e.Method, e.Prefix, e.Postfix, patchHost: e.Host);
                    }
                    else
                    {
                        ManualPatcher.TryPatch(e.Target, e.Method, e.Prefix, e.Postfix,
                                               parameterTypes: e.ParameterTypes, patchHost: e.Host, priority: e.Priority);
                    }
                    ok++;
                }
                catch (System.Exception ex)
                {
                    Core.LogMsg($"[统一注册] 异常: [{e.System}] {e.Target.Name}.{e.Method}: {ex.Message}");
                    fail++;
                }
            }
            Core.LogMsg($"[统一注册] Patch 挂载: 成功 {ok} / 失败 {fail} / 让路 {yield}（共 {REGISTRY.Length}）");
            foreach (var group in REGISTRY.GroupBy(e => e.Target.Name + "." + e.Method).Where(g => g.Count() >= 2))
                Core.LogMsg($"[统一注册] 多挂 {group.Key} ×{group.Count()}: {string.Join(",", group.Select(e => e.System))}");
        }
    }
}
