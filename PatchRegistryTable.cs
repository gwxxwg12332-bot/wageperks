using System;
using System.Linq;
using Il2Cpp;
using Il2CppInterop.Runtime;
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
            new PatchEntry { Target = typeof(StoreEventManager), Method = "OnDayStart", Prefix = null, Postfix = "PostfixOnNewDay", Host = typeof(Patches), Priority = DEFAULT_PRIORITY, System = "Core", YieldMod = "XIAOWOTradePerks", Note = "" },
            new PatchEntry { Target = typeof(PlayerStore), Method = "BeginDay", Prefix = null, Postfix = "PostfixOnBeginDay", Host = typeof(Patches), Priority = DEFAULT_PRIORITY, System = "Core", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(PlayerStore), Method = "AddDirectSellingItemToTable", Prefix = "PrefixAddDirectSellingItemToTable", Postfix = null, Host = typeof(WaterMerchantPerk), Priority = DEFAULT_PRIORITY, System = "WaterMerchant", YieldMod = null, Note = "" },
            // L120/L121 动态GetType(PreBuildChemHelper) 在 ApplyAll 里特殊处理
            new PatchEntry { Target = typeof(StoreClientManager), Method = "AddClient", Prefix = null, Postfix = "PostfixOnAddClient", Host = typeof(Patches), Priority = DEFAULT_PRIORITY, System = "Core", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(PlayerStore), Method = "SaveGame", Prefix = null, Postfix = "PostfixSaveGame", Host = typeof(LuckScoutPerk), Priority = DEFAULT_PRIORITY, System = "LuckScout", YieldMod = null, Note = "只写内存禁 Flush" },
            new PatchEntry { Target = typeof(PlayerStore), Method = "LoadGame", Prefix = null, Postfix = "PostfixOnLoadGame", Host = typeof(Patches), Priority = DEFAULT_PRIORITY, System = "Core", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(PlayerStore), Method = "LoadGame", Prefix = null, Postfix = "PostfixLoadGame", Host = typeof(LuckScoutBackpackUpgrade), Priority = DEFAULT_PRIORITY, System = "LuckScout", YieldMod = null, Note = "" },
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
            new PatchEntry { Target = typeof(EscapeUIManager), Method = "OnMainMenu", Prefix = null, Postfix = "PostfixOnMainMenu", Host = typeof(LuckScoutPerk), Priority = DEFAULT_PRIORITY, System = "LuckScout", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(GameMaster), Method = "NewGame", Prefix = null, Postfix = "PostfixNewGame", Host = typeof(LuckScoutPerk), Priority = DEFAULT_PRIORITY, System = "LuckScout", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(ScavHelper), Method = "GetRandomScavengedItem", Prefix = null, Postfix = "PostfixGetRandomScavengedItem", Host = typeof(LuckScoutPerk), Priority = -1000, System = "LuckScout", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(ScavHelper), Method = "CreateTooltip", Prefix = null, Postfix = "PostfixCreateTooltip", Host = typeof(LuckScoutPerk), Priority = DEFAULT_PRIORITY, System = "LuckScout", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(EmporiumEntry), Method = "GetAllAfterhourOwnedItems", Prefix = null, Postfix = "PostfixGetAllAfterhourOwnedItems", Host = typeof(LuckScoutPerk), Priority = DEFAULT_PRIORITY, System = "LuckScout", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(PerkUIController), Method = "OpenUI", Prefix = null, Postfix = "PostfixPerkUiOpen", Host = typeof(Patches), Priority = DEFAULT_PRIORITY, System = "Core", YieldMod = "XIAOWOTradePerks;PerkPointMod", Note = "多值让路：XIAOWO+PerkPointMod（分号分隔，ShouldYield Split 解析）" },
            new PatchEntry { Target = typeof(PerkUIController), Method = "OnChange", Prefix = null, Postfix = null, Host = typeof(Patches), Priority = DEFAULT_PRIORITY, System = "Core", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(StartingPerkIconLoader), Method = "Start", Prefix = null, Postfix = "PostfixIconLoaderStart", Host = typeof(Patches), Priority = DEFAULT_PRIORITY, System = "Core", YieldMod = "XIAOWOTradePerks", Note = "" },
            new PatchEntry { Target = typeof(NetworkUpgrade), Method = "Unlock", Prefix = "Prefix", Postfix = "Postfix", Host = typeof(DetectiveUpgradePatch), Priority = DEFAULT_PRIORITY, System = "Detective", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(SecData), Method = "OnFixerUsed", Prefix = "Prefix", Postfix = null, Host = typeof(DetectiveFixerPatch), Priority = DEFAULT_PRIORITY, System = "Detective", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(SecData), Method = "CommitCrime", Prefix = "Prefix", Postfix = null, Host = typeof(DetectiveCommitCrimePatch), Priority = DEFAULT_PRIORITY, System = "Detective", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(NegociationUIManager), Method = "SellItem", Prefix = null, Postfix = "Postfix", Host = typeof(SoldContrabandCounterPatch), Priority = DEFAULT_PRIORITY, System = "ContrabandCounter", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(SecData), Method = "OnNewDay", Prefix = null, Postfix = "Postfix", Host = typeof(SoldEvidenceOnNewDayPatch), Priority = DEFAULT_PRIORITY, System = "SoldEvidence", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(SecData), Method = "OnFixerUsed", Prefix = null, Postfix = "Postfix", Host = typeof(WildeFixerPatch), Priority = DEFAULT_PRIORITY, System = "Wilde", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(BarterHelper), Method = "DoesTraderAcceptThisItemAsPayment", Prefix = null, Postfix = "Postfix", Host = typeof(CounterfeitWineTradeFix), Priority = DEFAULT_PRIORITY, System = "CounterfeitWine", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(StartingPerkElement), Method = "Start", Prefix = null, Postfix = "PostfixStartingPerkElementStart", Host = typeof(Patches), Priority = DEFAULT_PRIORITY, System = "Core", YieldMod = "XIAOWOTradePerks", Note = "" },
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
            new PatchEntry { Target = typeof(PlayerStore), Method = "SaveGame", Prefix = null, Postfix = "PostfixSaveGame", Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "只写内存禁 Flush" },
            new PatchEntry { Target = typeof(PlayerStore), Method = "SaveGame", Prefix = "PrefixSaveGame", Postfix = null, Host = typeof(ContainerUpgradeV2), Priority = DEFAULT_PRIORITY, System = "ContainerUpgradeV2", YieldMod = null, Note = "只写内存禁 Flush" },
            new PatchEntry { Target = typeof(PlayerStore), Method = "SaveGame", Prefix = null, Postfix = "PostfixSaveGame", Host = typeof(WageSaveStore), Priority = FLUSH_PRIORITY, System = "SaveStore", YieldMod = null, Note = "统一落盘层：各系统写内存后最后原子落盘（FLUSH_PRIORITY=0 保序）" },
            new PatchEntry { Target = typeof(PlayerStore), Method = "EndDay", Prefix = null, Postfix = "PostfixEndDay", Host = typeof(WageSaveStore), Priority = FLUSH_PRIORITY, System = "SaveStore", YieldMod = null, Note = "EndDay 第二落盘通道：原生 SaveGame 只在 InitialSave 调一次，打烊靠此通道" },
            new PatchEntry { Target = typeof(PlayerStore), Method = "EndDay", Prefix = null, Postfix = "PostfixEndDay", Host = typeof(WageBrokerPermitPatches), Priority = 300, System = "WageBroker", YieldMod = null, Note = "蛙哥许可：visitLeftTonight += 持有张数（priority=300 低于夜猫子400，确保后跑叠加）" },
            new PatchEntry { Target = typeof(PlayerStore), Method = "EndDay", Prefix = null, Postfix = "PostfixEndDayCharger", Host = typeof(WageBrokerPermitPatches), Priority = 300, System = "WageBroker", YieldMod = null, Note = "蛙哥充电器：打烊自动给背包电池充电" },
            new PatchEntry { Target = typeof(ScavHelper), Method = "RollMinorWound", Prefix = "PrefixRollMinorWound", Postfix = null, Host = typeof(WageBrokerPermitPatches), Priority = DEFAULT_PRIORITY, System = "WageBroker", YieldMod = null, Note = "蛙哥许可：拾荒轻伤保护" },
            new PatchEntry { Target = typeof(ScavHelper), Method = "RollMajorWound", Prefix = "PrefixRollMajorWound", Postfix = null, Host = typeof(WageBrokerPermitPatches), Priority = DEFAULT_PRIORITY, System = "WageBroker", YieldMod = null, Note = "蛙哥许可：拾荒重伤保护" },
            new PatchEntry { Target = typeof(StoreEventManager), Method = "OnDayStart", Prefix = null, Postfix = "OnDayStartPostfix", Host = typeof(ModCannibalism), Priority = DEFAULT_PRIORITY, System = "ModCannibalism", YieldMod = "XIAOWOTradePerks", Note = "" },
            new PatchEntry { Target = typeof(StoreEventManager), Method = "OnDayStart", Prefix = null, Postfix = "OnDayStartPostfix", Host = typeof(BatteryCannibalism), Priority = DEFAULT_PRIORITY, System = "BatteryCannibalism", YieldMod = "XIAOWOTradePerks", Note = "" },
            new PatchEntry { Target = typeof(StoreEventManager), Method = "OnDayStart", Prefix = null, Postfix = "OnDayStartPostfix", Host = typeof(GuMachineSystem), Priority = DEFAULT_PRIORITY, System = "GuMachine", YieldMod = "XIAOWOTradePerks", Note = "" },
            new PatchEntry { Target = typeof(StoreEventManager), Method = "OnDayStart", Prefix = null, Postfix = "PostfixOnDayStart", Host = typeof(WageGirlSystem), Priority = DEFAULT_PRIORITY, System = "WageGirl", YieldMod = "XIAOWOTradePerks", Note = "" },
            new PatchEntry { Target = typeof(StoreEventManager), Method = "OnDayStart", Prefix = null, Postfix = "PostfixStoreEventOnDayStart", Host = typeof(DestinyDice), Priority = DEFAULT_PRIORITY, System = "DestinyDice", YieldMod = "XIAOWOTradePerks", Note = "" },
            new PatchEntry { Target = typeof(StoreEventManager), Method = "OnDayStart", Prefix = null, Postfix = "PostfixUnifiedDayStart", Host = typeof(Patches), Priority = DEFAULT_PRIORITY, System = "Core", YieldMod = "XIAOWOTradePerks", Note = "" },
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
            new PatchEntry { Target = typeof(MainMenuUIController), Method = "Awake", Prefix = null, Postfix = "PostfixAwake", Host = typeof(NewStartTypeUI), Priority = DEFAULT_PRIORITY, System = "NewStartType", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(MainMenuUIController), Method = "ResetAllTab", Prefix = null, Postfix = "PostfixResetAllTab", Host = typeof(NewStartTypeUI), Priority = DEFAULT_PRIORITY, System = "NewStartType", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(NewGameData), Method = "GetStartDisplayName", Prefix = null, Postfix = "PostfixGetStartDisplayName", Host = typeof(NewStartTypeUI), Priority = DEFAULT_PRIORITY, System = "NewStartType", YieldMod = null, Note = "", ParameterTypes = new System.Type[] { typeof(NewGameData.StartType) } },
            new PatchEntry { Target = typeof(SaveFiles), Method = "BuildPreviewFromStore", Prefix = null, Postfix = "PostfixBuildPreviewFromStore", Host = typeof(NewStartTypeUI), Priority = DEFAULT_PRIORITY, System = "NewStartType", YieldMod = null, Note = "", ParameterTypes = new System.Type[] { typeof(PlayerStore), typeof(int) } },
            new PatchEntry { Target = typeof(PlayerStore), Method = "SaveGame", Prefix = "PrefixSaveGame", Postfix = "PostfixSaveGame", Host = typeof(NewStartTypeUI), Priority = DEFAULT_PRIORITY, System = "NewStartType", YieldMod = null, Note = "只写内存禁 Flush" },
            new PatchEntry { Target = typeof(PlayerStore), Method = "LoadGame", Prefix = null, Postfix = "PostfixLoadGame", Host = typeof(NewStartTypeUI), Priority = DEFAULT_PRIORITY, System = "NewStartType", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(StoreClientManager), Method = "HandleMinorClient", Prefix = "PrefixHandleMinorClient", Postfix = null, Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(StoreClientManager), Method = "PickClient", Prefix = "PrefixPickClient", Postfix = null, Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(MapUIManager), Method = "OpenGoOutsideConfirm", Prefix = "PrefixOpenGoOutsideConfirm", Postfix = null, Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "" },
            // 10-03 蛙哥交易站：地图按钮注入（暂时封存）
            // new PatchEntry { Target = typeof(MapUIManager), Method = "OpenUI", Prefix = null, Postfix = "PostfixMapOpenUI", Host = typeof(WageBrokerStation), Priority = DEFAULT_PRIORITY, System = "WageBroker", YieldMod = null, Note = "蛙哥交易站地图按钮" },
            // // 10-03 蛙哥交易站：场景门控（学Brewing）
            // new PatchEntry { Target = typeof(MapUIManager), Method = "VisitUpgradeMerchant", Prefix = "PrefixVisitUpgradeMerchant", Postfix = null, Host = typeof(WageBrokerStation), Priority = DEFAULT_PRIORITY, System = "WageBroker", YieldMod = null, Note = "进场景前还原" },
            // new PatchEntry { Target = typeof(MapUIManager), Method = "HandleLeaveUpgradeMerchant", Prefix = null, Postfix = "PostfixHandleLeaveUpgradeMerchant", Host = typeof(WageBrokerStation), Priority = DEFAULT_PRIORITY, System = "WageBroker", YieldMod = null, Note = "离开复位" },
            // new PatchEntry { Target = typeof(StoreClientList), Method = "PlaceInventorInventory", Prefix = "PrefixPlaceInventorInventory", Postfix = "PostfixPlaceInventorInventory", Host = typeof(WageBrokerStation), Priority = DEFAULT_PRIORITY, System = "WageBroker", YieldMod = null, Note = "场景铺货门控" },
            // new PatchEntry { Target = typeof(PlayerStore), Method = "AddDirectSellingItemToTable", Prefix = "PrefixAddDirectSellingItemToTable", Postfix = null, Host = typeof(WageBrokerStation), Priority = DEFAULT_PRIORITY, System = "WageBroker", YieldMod = null, Note = "拦原生货" },
            new PatchEntry { Target = typeof(StoreEventManager), Method = "OnDayStart", Prefix = null, Postfix = "PostfixOnNewDay", Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = "XIAOWOTradePerks", Note = "" },
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
            new PatchEntry { Target = typeof(ScavHelper), Method = "CanScavenge", Prefix = null, Postfix = "PostfixCanScavenge", Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(ScavHelper), Method = "GetMaxScavAttempts", Prefix = null, Postfix = "PostfixGetMaxScavAttempts", Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(ScavHelper), Method = "GetScavTimeLeft", Prefix = null, Postfix = "PostfixGetScavTimeLeft", Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(ScavHelper), Method = "GetRandomScavengedItem", Prefix = null, Postfix = "PostfixGetRandomScavengedItem", Host = typeof(RobinCrusoePerk), Priority = -1000, System = "Survival", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(MachineryHelper), Method = "GetCurrentPerformanceBonus", Prefix = null, Postfix = "PostfixGetCurrentPerformanceBonus", Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(MachineryHelper), Method = "GetCurrentQualityBonus", Prefix = null, Postfix = "PostfixGetCurrentQualityBonus", Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(ModuleHelper), Method = "ApplyBasicModuleEffect", Prefix = null, Postfix = "PostfixApplyBasicModuleEffect", Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(ModuleEffectHelper), Method = "ModifyTempStatFromBaseByPercentage", Prefix = null, Postfix = "PostfixModifyTempStatFromBaseByPercentage", Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(ModuleHelper), Method = "AddModuleStatLine", Prefix = null, Postfix = "PostfixAddModuleStatLine", Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(MachineryHelper), Method = "CreateMachineryTooltip", Prefix = null, Postfix = "PostfixCreateMachineryTooltip", Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(ModuleHelper), Method = "ApplyPerformanceWaterRecyclerEffect", Prefix = null, Postfix = "PostfixApplyPerformanceWaterRecyclerEffect", Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(MachineMoistureFarm), Method = "GetOutputVolume", Prefix = null, Postfix = "PostfixGetOutputVolume", Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(MachineTurboBoosterAdv), Method = "UpdateSprite", Prefix = null, Postfix = "PostfixUpdateSprite", Host = typeof(TurboBoostN), Priority = DEFAULT_PRIORITY, System = "TurboBoostN", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(MachineMoistureFarm), Method = "Fill", Prefix = "PrefixFill", Postfix = null, Host = typeof(TurboBoostN), Priority = DEFAULT_PRIORITY, System = "TurboBoostN", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(MachinePurifier), Method = "PurifyContainer", Prefix = null, Postfix = "PostfixPurifyContainer", Host = typeof(TurboBoostN), Priority = DEFAULT_PRIORITY, System = "TurboBoostN", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(WineHelper), Method = "OnAgeWine", Prefix = null, Postfix = "PostfixOnAgeWine", Host = typeof(TurboBoostN), Priority = DEFAULT_PRIORITY, System = "TurboBoostN", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(MachineProgressHelper), Method = "ContinueProgressTypeMachine", Prefix = null, Postfix = "PostfixContinueProgress", Host = typeof(TurboBoostN), Priority = DEFAULT_PRIORITY, System = "TurboBoostN", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(MachineHelper), Method = "OnMachineActioned", Prefix = null, Postfix = "PostfixOnMachineActioned", Host = typeof(TurboBoostN), Priority = DEFAULT_PRIORITY, System = "TurboBoostN", YieldMod = null, Note = "10-02 熔炉补产：补(N-1)*4个scrap" },
            new PatchEntry { Target = typeof(MachineTurboBoosterAdv), Method = "CreateMachineTooltip", Prefix = null, Postfix = "PostfixCreateTooltip", Host = typeof(TurboBoostN), Priority = DEFAULT_PRIORITY, System = "TurboBoostN", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(WaterHelper), Method = "RemoveContaminantFromContainer", Prefix = null, Postfix = "PostfixRemoveContaminantFromContainer", Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "" },
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
            new PatchEntry { Target = typeof(PlayerStore), Method = "StartNewGame", Prefix = null, Postfix = "PostfixStartNewGame", Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(PlayerStore), Method = "StartNewGame", Prefix = null, Postfix = "PostfixStartNewGame", Host = typeof(DrJacksonFriendPerk), Priority = DEFAULT_PRIORITY, System = "DrJacksonFriend", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(PlayerStore), Method = "StartNewGame", Prefix = null, Postfix = "PostfixStartNewGame", Host = typeof(WaterMerchantPerk), Priority = DEFAULT_PRIORITY, System = "WaterMerchant", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(PlayerStore), Method = "LoadGame", Prefix = null, Postfix = "PostfixLoadGame_IngotContainer", Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(LiquidContainerHelper), Method = "AutoSipFromContainer", Prefix = "PrefixAutoSipFromContainer", Postfix = null, Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(StoreClientList), Method = "PlaceSupplierInventory", Prefix = null, Postfix = "PostfixPlaceSupplierInventory", Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(EmporiumEntry), Method = "Start", Prefix = null, Postfix = "PostfixEmporiumEntryStart", Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(MachineryHelper), Method = "GetMachinePowerUsage", Prefix = null, Postfix = "PostfixGetMachinePowerUsage", Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(PlayerStore), Method = "HandleInsurance", Prefix = "PrefixHandleInsurance", Postfix = null, Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(ScavHelper), Method = "ScavengeDumpingGrounds", Prefix = null, Postfix = "PostfixScavengeDumpingGrounds", Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(StoreClientManager), Method = "CheckRentDay", Prefix = "PrefixCheckRentDay", Postfix = null, Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(StoreClientUniqueList.__c), Method = "_LandlordWholesale_b__22_0", Prefix = "PrefixLandlordWholesaleStock", Postfix = null, Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(StoreClientManager), Method = "HandleSupplierClient", Prefix = null, Postfix = "PostfixHandleSupplierClient", Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(PlayerStore), Method = "ExecuteGameOver", Prefix = "PrefixExecuteGameOver", Postfix = null, Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(StoreClientManager), Method = "HandleNormalClient", Prefix = null, Postfix = "PostfixHandleNormalClient", Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(StorePhoneClient), Method = "InitPhoneClientDict", Prefix = null, Postfix = "PostfixInitPhoneClientDict", Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "" },
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
            new PatchEntry { Target = typeof(StoreClient), Method = "ApplyBudgetModifier", Prefix = null, Postfix = "PostfixStoreClientApplyBudgetModifier", Host = typeof(Patches), Priority = DEFAULT_PRIORITY, System = "Trade", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(StoreClient), Method = "ApplyBudgetModifier", Prefix = null, Postfix = "PostfixApplyBudgetModifier", Host = typeof(WageGirlSystem), Priority = DEFAULT_PRIORITY, System = "WageGirl", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(BargainUIManager), Method = "GetDealMakerBonus", Prefix = null, Postfix = "PostfixGetDealMakerBonus", Host = typeof(WageGirlSystem), Priority = DEFAULT_PRIORITY, System = "WageGirl", YieldMod = null, Note = "双宿主 PostfixGetDealMakerBonus(WageGirlSystem+Patches) 疑似重复，待拆包核实是否双跑" },
            new PatchEntry { Target = typeof(GameItemElement), Method = "ApplyAnimationFrame", Prefix = "PrefixApplyAnimationFrame", Postfix = null, Host = typeof(WageGirlSystem), Priority = DEFAULT_PRIORITY, System = "WageGirl", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(GameItemElement), Method = "ResolveSpriteByName", Prefix = "PostfixResolveSpriteByName", Postfix = null, Host = typeof(WageGirlSystem), Priority = DEFAULT_PRIORITY, System = "WageGirl", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(StoreClientManager), Method = "PickClient", Prefix = null, Postfix = "PostfixStoreClientManagerPickClient", Host = typeof(Patches), Priority = DEFAULT_PRIORITY, System = "Core", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(BargainUIManager), Method = "OfferBuyingMarkup", Prefix = "PrefixBargainUIManagerOfferBuyingMarkup", Postfix = null, Host = typeof(Patches), Priority = DEFAULT_PRIORITY, System = "Trade", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(BargainUIManager), Method = "GetDealMakerBonus", Prefix = null, Postfix = "PostfixGetDealMakerBonus", Host = typeof(Patches), Priority = DEFAULT_PRIORITY, System = "Trade", YieldMod = null, Note = "双宿主 PostfixGetDealMakerBonus(WageGirlSystem+Patches) 疑似重复，待拆包核实是否双跑" },
            new PatchEntry { Target = typeof(ItemFeatureList), Method = "BargainBuyingMarkup", Prefix = null, Postfix = "PostfixItemFeatureListBargainBuyingMarkup", Host = typeof(Patches), Priority = DEFAULT_PRIORITY, System = "Trade", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(StoreClient), Method = "OnDealAccepted", Prefix = null, Postfix = "PostfixStoreClientOnDealAccepted", Host = typeof(Patches), Priority = DEFAULT_PRIORITY, System = "Trade", YieldMod = null, Note = "" },
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
            new PatchEntry { Target = typeof(MachineBottlePrinter.__c__DisplayClass6_0), Method = "Method_Internal_Void_String_Int32_0", Prefix = "PrefixTryPrint", Postfix = "PostfixTryPrint", Host = typeof(WaterMerchantPerk), Priority = DEFAULT_PRIORITY, System = "WaterMerchant", YieldMod = "BrewingExpansion", Note = "酿酒打印桶增产", UseByName = true },
            new PatchEntry { Target = typeof(MachineFeedDispenser.__c__DisplayClass7_0), Method = "_CreateFeedDispenser_b__3", Prefix = "PrefixFeedDispenserB3", Postfix = null, Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "饲料机", UseByName = true },
            new PatchEntry { Target = typeof(StoreClient), Method = "GetTradeRepMultiplier", Prefix = null, Postfix = "PostfixGetTradeRepMultiplier", Host = typeof(Patches), Priority = DEFAULT_PRIORITY, System = "Trade", YieldMod = null, Note = "交易声望倍率", UseByName = true },
            new PatchEntry { Target = typeof(BargainUIManager), Method = "ComputeRepPer1000Credits", Prefix = null, Postfix = "PostfixComputeRepPer1000Credits", Host = typeof(Patches), Priority = DEFAULT_PRIORITY, System = "Trade", YieldMod = null, Note = "声望换算", UseByName = true },
            new PatchEntry { Target = typeof(FoodItemHelper), Method = "CreateFoodItemTooltip", Prefix = "PrefixFoodTooltip", Postfix = "PostfixFoodTooltip", Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "食物tooltip", UseByName = true },
            new PatchEntry { Target = typeof(ContainerHelper), Method = "InitContainerItem", Prefix = null, Postfix = "PostfixInitContainerItem", Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = "NestedStorage", Note = "容器初始化", UseByName = true },
            new PatchEntry { Target = typeof(GunHelper), Method = "InitGun", Prefix = null, Postfix = "PostfixInitGun", Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "枪初始化", UseByName = true },
            new PatchEntry { Target = typeof(StoreClient), Method = "ClientExposeFeature", Prefix = "PrefixClientExposeFeature", Postfix = null, Host = typeof(Patches), Priority = DEFAULT_PRIORITY, System = "Trade", YieldMod = null, Note = "违禁品跳过曝光链", UseByName = true },
            // 3条 DoubleClickAction（带parameterTypes）
            new PatchEntry { Target = typeof(ItemMouseDoubleClickHandler), Method = "DoubleClickAction", Prefix = "PrefixDoubleClickAction", Postfix = null, Host = typeof(WageBrother), Priority = DEFAULT_PRIORITY, System = "WageBrother", YieldMod = null, Note = "双击蛙哥", ParameterTypes = new System.Type[] { typeof(GameItem), typeof(UnityEngine.Vector2) } },
            new PatchEntry { Target = typeof(ItemMouseDoubleClickHandler), Method = "DoubleClickAction", Prefix = null, Postfix = "PostfixDoubleClickAction", Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "双击喝", ParameterTypes = new System.Type[] { typeof(GameItem), typeof(UnityEngine.Vector2) } },
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
