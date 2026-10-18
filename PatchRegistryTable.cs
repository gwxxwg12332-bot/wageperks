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
            public string Finalizer; // 10-16 审阅 P0-1/P0-2：落地时丢列的防御性 Finalizer（保险丝），ApplyAll 透传
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
            new PatchEntry { Target = typeof(EscapeUIManager), Method = "OnMainMenu", Prefix = null, Postfix = "ClosePanelsOnMainMenu", Host = typeof(Patches), Priority = DEFAULT_PRIORITY, System = "Core", YieldMod = null, Note = "10-18 主菜单面板残留：CustomUIManager窗口跨场景存活,guard只防新建不清理已存在→OnMainMenu主动关rc_status/wage_girl_panel" },
            new PatchEntry { Target = typeof(GameMaster), Method = "QuitToMenu", Prefix = null, Postfix = "ClosePanelsOnMainMenu", Host = typeof(Patches), Priority = DEFAULT_PRIORITY, System = "Core", YieldMod = null, Note = "10-18 同上：QuitToMenu(小退)路径也清理面板残留" },
            new PatchEntry { Target = typeof(GameMaster), Method = "NewGame", Prefix = null, Postfix = "PostfixNewGame", Host = typeof(LuckScoutPerk), Priority = DEFAULT_PRIORITY, System = "LuckScout", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(ScavHelper), Method = "GetRandomScavengedItem", Prefix = null, Postfix = "PostfixGetRandomScavengedItem", Finalizer = "FinalizerGetRandomScavengedItem", Host = typeof(LuckScoutPerk), Priority = -1000, System = "LuckScout", YieldMod = null, Note = "10-16 审阅 P0-2 接线：拾荒异常保险丝" },
            new PatchEntry { Target = typeof(ScavHelper), Method = "CreateTooltip", Prefix = null, Postfix = "PostfixCreateTooltip", Host = typeof(LuckScoutPerk), Priority = DEFAULT_PRIORITY, System = "LuckScout", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(EmporiumEntry), Method = "GetAllAfterhourOwnedItems", Prefix = null, Postfix = "PostfixGetAllAfterhourOwnedItems", Host = typeof(LuckScoutPerk), Priority = DEFAULT_PRIORITY, System = "LuckScout", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(PerkUIController), Method = "OpenUI", Prefix = null, Postfix = "PostfixPerkUiOpen", Finalizer = "FinalizerPerkUiOpen", Host = typeof(Patches), Priority = DEFAULT_PRIORITY, System = "Core", YieldMod = null, Note = "10-04共存：XIAOWO/PerkPointMod 不提供我方perk元素，让路=特性不可选；EnsurePickerElements自带防重复；10-16 审阅 P0-2 接线：OpenUI 崩溃保险丝（LocHelper 异步未就绪）" },
            new PatchEntry { Target = typeof(PerkUIController), Method = "OnChange", Prefix = null, Postfix = null, Finalizer = "FinalizerPerkUiOnChange", Host = typeof(Patches), Priority = DEFAULT_PRIORITY, System = "Core", YieldMod = null, Note = "10-16 审阅 P0-1 修复：原条目 Prefix/Postfix 全 null=空挂点假阳性；Finalizer 接线后非空挂点（孤儿保险丝找回）" },
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
            // 10-18 蛙娘门路 Lv3 直售免罪（设计稿《假货原价与违禁品免罪》§8.1-6）：只挂 CheckSoldContraband/CheckSoldChem，
            //   CheckSoldDirtyWater 签名待拆包确认后补（cheatsheet:4650 无该签名记录）
            new PatchEntry { Target = typeof(SecData), Method = "CheckSoldContraband", Prefix = "PrefixCheckSoldContraband", Postfix = null, Host = typeof(WageGirlConn.ConnDirectExemptPatch), Priority = DEFAULT_PRIORITY, System = "WageGirlConn", YieldMod = null, Note = "Lv3 直售免罪" },
            new PatchEntry { Target = typeof(SecData), Method = "CheckSoldChem", Prefix = "PrefixCheckSoldChem", Postfix = null, Host = typeof(WageGirlConn.ConnDirectExemptPatch), Priority = DEFAULT_PRIORITY, System = "WageGirlConn", YieldMod = null, Note = "Lv3 直售免罪" },
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
            new PatchEntry { Target = typeof(NegociationUIManager), Method = "RefreshUI", Prefix = null, Postfix = "PostfixRefreshUI", Host = typeof(Patches), Priority = DEFAULT_PRIORITY, System = "Trade", YieldMod = null, Note = "FIX17:预算显示追加原版值+加成来源(拆包:RefreshUI唯一写clientBudget TMP,11触发面)" },
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
            new PatchEntry { Target = typeof(HealthData), Method = "ReceiveMinorWound", Prefix = "PrefixReceiveMinorWound", Postfix = null, Host = typeof(WageBrokerPermitPatches), Priority = 100, System = "WageBroker", YieldMod = null, Note = "蛙哥许可：拾荒轻伤免疫（10-05 拆包实锤原 ScavHelper.RollMinorWound 死 API 白挂，改挂受伤唯一施加点 Receive*；10-10 #46 显式 Priority=100 先跑先拦——许可免疫先于鲁滨逊侧同 orig Prefix 判断，顺序依赖→显式约定）" },
            new PatchEntry { Target = typeof(HealthData), Method = "ReceiveMajorWound", Prefix = "PrefixReceiveMajorWound", Postfix = null, Host = typeof(WageBrokerPermitPatches), Priority = 100, System = "WageBroker", YieldMod = null, Note = "蛙哥许可：拾荒重伤免疫（同上，白挂修正；10-10 #46 显式 Priority=100）" },
            new PatchEntry { Target = typeof(HealthData), Method = "HandleNightlyWound", Prefix = "PrefixHandleNightlyWound", Postfix = null, Host = typeof(WageBrokerPermitPatches), Priority = 100, System = "WageBroker", YieldMod = null, Note = "蛙哥许可：夜间受伤免疫（10-05 拆包实锤夜间受伤不走 Receive*，OnSleep 概率判定直接 dec 健康值——三级免疫含夜间；10-10 #46 显式 Priority=100）" },
            new PatchEntry { Target = typeof(StoreEventManager), Method = "OnDayStart", Prefix = null, Postfix = "OnDayStartPostfix", Host = typeof(ModCannibalism), Priority = DEFAULT_PRIORITY, System = "ModCannibalism", YieldMod = null, Note = "10-04共存：XIAOWO现金流纯hash，双Postfix安全" },
            new PatchEntry { Target = typeof(StoreEventManager), Method = "OnDayStart", Prefix = null, Postfix = "OnDayStartPostfix", Host = typeof(BatteryCannibalism), Priority = DEFAULT_PRIORITY, System = "BatteryCannibalism", YieldMod = null, Note = "10-04共存：XIAOWO现金流纯hash，双Postfix安全" },
            new PatchEntry { Target = typeof(StoreEventManager), Method = "OnDayStart", Prefix = null, Postfix = "OnDayStartPostfix", Host = typeof(GuMachineSystem), Priority = DEFAULT_PRIORITY, System = "GuMachine", YieldMod = null, Note = "10-04共存：XIAOWO现金流纯hash，双Postfix安全" },
            new PatchEntry { Target = typeof(StoreEventManager), Method = "OnDayStart", Prefix = null, Postfix = "PostfixOnDayStart", Host = typeof(WageGirlSystem), Priority = DEFAULT_PRIORITY, System = "WageGirl", YieldMod = null, Note = "10-04共存：XIAOWO现金流纯hash，双Postfix安全" },
            new PatchEntry { Target = typeof(StoreEventManager), Method = "OnDayStart", Prefix = null, Postfix = "PostfixStoreEventOnDayStart", Host = typeof(DestinyDice), Priority = DEFAULT_PRIORITY, System = "DestinyDice", YieldMod = null, Note = "10-04共存：XIAOWO现金流纯hash，双Postfix安全；10-05门槛回落已挪EndDay，本挂点仅剩事件注入+联动" },
            new PatchEntry { Target = typeof(PlayerStore), Method = "EndDay", Prefix = null, Postfix = "PostfixEndDayDiceThreshold", Host = typeof(DestinyDice), Priority = DEFAULT_PRIORITY, System = "DestinyDice", YieldMod = null, Note = "10-05骰子门槛打烊回落：旧挂OnDayStart玩家感知打烊不回落且每天只回一档多掷净涨；改EndDay+归位400" },
            new PatchEntry { Target = typeof(StoreEventManager), Method = "OnDayStart", Prefix = null, Postfix = "PostfixUnifiedDayStart", Host = typeof(Patches), Priority = DEFAULT_PRIORITY, System = "Core", YieldMod = null, Note = "10-04共存：XIAOWO现金流纯hash，双Postfix安全" },
            // 10-16 闪退修复8：剧情 6 条已移至数组末尾（Host 轻量化=StoryPlotHooks）——
            //   原插入 79-84 位置使后续条目（GameItemElement #165）解析链加深崩栈；移末尾+轻 Host 后前段顺序对齐 1.3.6。
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
            // 10-19 WP 配置菜单：新增 Awake 挂点（按钮注入不依赖 tab，单装/双装都注入；210→211 条，主线程顺序单批，放表尾不影响顺序）
            new PatchEntry { Target = typeof(Il2Cpp.MainMenuUIController), Method = "Awake", Prefix = null, Postfix = "PostfixAwake", Host = typeof(WageConfigUI), Priority = DEFAULT_PRIORITY, System = "ConfigUI", YieldMod = null, Note = "10-19 WP 配置菜单主菜单按钮注入（不依赖 tab，单装/双装都注入）" },
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
            new PatchEntry { Target = typeof(HusbandryHelper), Method = "CreateItemTooltip", Prefix = null, Postfix = "PostfixCreateItemTooltip", Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = "Survival", Note = "10-11 所有权表 Step 3：生存段 handler（品质/售价/双击使用）——双装让路给 WS SurvivalFood.PostfixCreateItemTooltip（同目标逐字注入=词条重复根因）；WP 专属段见下条 WPOnly 永不让路" },
            new PatchEntry { Target = typeof(HusbandryHelper), Method = "CreateItemTooltip", Prefix = null, Postfix = "PostfixCreateItemTooltip_WPOnly", Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "10-11 所有权表 Step 3：WP 专属段（鼠王/机器/储存区段位）永不让路——双装时与 WS 生存段并存无重复" },
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
            // 10-16 闪退修复2：MachineTurboBoosterAdv.UpdateSprite 目标签名含 GameSlotInventory → Il2CppInterop 1.5.1 解析即 cctor 栈溢出（全库唯一含此类型的补丁，3 次启动固定崩此条）。
            // 挂点整体删除：PostfixUpdateSprite 只写死变量 _pendingN（无任何消费点，TurboBoostN.cs grep 实锤），涡轮 N 主路径=PrefixTarget/PostfixTarget(_turboN) 不受影响。
            new PatchEntry { Target = typeof(MachineMoistureFarm), Method = "Fill", Prefix = "PrefixFill", Postfix = null, Host = typeof(TurboBoostN), Priority = DEFAULT_PRIORITY, System = "TurboBoostN", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(MachinePurifier), Method = "PurifyContainer", Prefix = null, Postfix = "PostfixPurifyContainer", Host = typeof(TurboBoostN), Priority = DEFAULT_PRIORITY, System = "TurboBoostN", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(WineHelper), Method = "OnAgeWine", Prefix = null, Postfix = "PostfixOnAgeWine", Host = typeof(TurboBoostN), Priority = DEFAULT_PRIORITY, System = "TurboBoostN", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(MachineProgressHelper), Method = "ContinueProgressTypeMachine", Prefix = null, Postfix = "PostfixContinueProgress", Host = typeof(TurboBoostN), Priority = DEFAULT_PRIORITY, System = "TurboBoostN", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(MachineHelper), Method = "OnMachineActioned", Prefix = null, Postfix = "PostfixOnMachineActioned", Host = typeof(TurboBoostN), Priority = DEFAULT_PRIORITY, System = "TurboBoostN", YieldMod = null, Note = "10-02 熔炉补产：补(N-1)*4个scrap" },
            // 10-16 闪退修复3：MachineTurboBoosterAdv.CreateMachineTooltip 也崩（同类第2条）——该类含泛型方法签名 ValueTuple<PixelWindow,GameSlotInventory>，Il2CppInterop 1.5.1 解析该类即 cctor 栈溢出（连续两类方法 UpdateSprite/CreateMachineTooltip 均固定崩）→ 该类全部 Harmony 挂点移除。
            //   功能降级：互食加持 tooltip 行暂停显示（核心互食加速=PrefixTarget/PostfixTarget _turboN 主路径不受影响）；后续找 UI 事件链替代挂点或升级 interop 后恢复。
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
            new PatchEntry { Target = typeof(Il2Cpp.ItemMultiSelectHandler), Method = "EndGroupDrag", Prefix = "PrefixEndGroupDrag", Postfix = "PostfixEndGroupDrag", Finalizer = "FinalizerEndGroupDrag", Host = typeof(BatchDragUpgrade), Priority = DEFAULT_PRIORITY, System = "BatchDrag", YieldMod = null, Note = "10-16 审阅 P0-2 接线：批量拖拽异常保险丝" },
            // 10-07 阶段D D-2 补删（第5条）：PlayerStore.StartNewGame PostfixStartNewGame RobinCrusoePerk（原 :205）已删——WS SaveStore.PostfixStartNewGame 承接（清血量缓存+清命名空间+设默认六维，让路 4 点含 StartNewGame）
            new PatchEntry { Target = typeof(PlayerStore), Method = "StartNewGame", Prefix = null, Postfix = "PostfixStartNewGame", Host = typeof(DrJacksonFriendPerk), Priority = DEFAULT_PRIORITY, System = "DrJacksonFriend", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(PlayerStore), Method = "StartNewGame", Prefix = null, Postfix = "PostfixStartNewGame", Host = typeof(WaterMerchantPerk), Priority = DEFAULT_PRIORITY, System = "WaterMerchant", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(PlayerStore), Method = "LoadGame", Prefix = null, Postfix = "PostfixLoadGame_IngotContainer", Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "" },
            // 10-07 鲁滨逊完全归 WS WS-3（删 AutoSip 1）：LiquidContainerHelper.AutoSipFromContainer Prefix RobinCrusoePerk（原 :209）已删——WS SurvivalFood.PrefixAutoSipFromContainer 承接（任务#20 已复制，本波去双挂）
            // 10-07 鲁滨逊完全归 WS WS-3（删 P1 经济链 1）：StoreClientList.PlaceSupplierInventory Postfix RobinCrusoePerk（原 :210）已删——WS SurvivalEconomy.cs 承接（供货商卖水药食物，同日不重复）
            new PatchEntry { Target = typeof(EmporiumEntry), Method = "Start", Prefix = null, Postfix = "PostfixEmporiumEntryStart", Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "" },
            // 10-07 鲁滨逊完全归 WS WS-3（删 P1 经济链 6+耗电 1）：GetMachinePowerUsage/HandleInsurance/ScavengeDumpingGrounds/CheckRentDay/_LandlordWholesale_b__22_0/HandleSupplierClient/ExecuteGameOver/HandleNormalClient Postfix/Prefix RobinCrusoePerk（原 :212-219）已删——WS SurvivalEconomy.cs + SurvivalMachineMods.cs 承接
            // 10-16 闪退修复：StorePhoneClient.InitPhoneClientDict 返回泛型 Dictionary<long,StorePhoneClient> → Il2CppInterop 1.5.1 解析泛型 cctor 栈溢出（不可捕获）→ 进程被杀。
            // 鲁滨逊胡安8800/李北文8801 注册改由 TickWantedSupplierDaily→TryRegisterWantedPhones（BeginDay 每日兜底，RobinCrusoePerk.PhoneVendor.cs:58）承接，本条目删除。
            new PatchEntry { Target = typeof(StorePhoneClient), Method = "GetCallDialog", Prefix = null, Postfix = "PostfixGetCallDialog", Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "10-06 C2奥丁对话：强通帧覆盖为RevMerchantPhoneDialog(odin_hold+排期全走原版显示链)" },
            new PatchEntry { Target = typeof(PhoneUIManager), Method = "WillAnswerCall", Prefix = "PrefixWillAnswerCall", Postfix = null, Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(PhoneUIManager), Method = "WillAnswerCall", Prefix = null, Postfix = "PostfixWillAnswerCall", Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(PhoneUIManager), Method = "StartPhoneDialog", Prefix = "PrefixStartPhoneDialog", Postfix = null, Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(ContactElement), Method = "OnInit", Prefix = null, Postfix = "PostfixOnContactInit", Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(PhoneUIManager), Method = "AutoCall", Prefix = "PrefixAutoCall", Postfix = null, Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(PhoneUIManager), Method = "HandleCall", Prefix = "PrefixHandleCall", Postfix = null, Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "" },
            // 10-16 闪退修复4：StoreClientListWanted.CreateWanted6 在 interop 1.5.1/1.5.3 下挂载均稳定崩（目标/补丁签名均非泛型，机制=Il2CppInterop 类型解析栈溢出临界）→ 删挂点。
            //   功能降级：wanted6(李北文) 创建时改名取消，到店改名+电话解锁+上货由 WantedSupplierOnArrived 兜底（RobinCrusoePerk.PhoneVendor.cs:121 实锤），核心链路不受影响。
            new PatchEntry { Target = typeof(DirectoryMaster), Method = "Item", Prefix = "PrefixDirectoryMasterItem", Postfix = null, Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(WantedElement), Method = "OnArrested", Prefix = null, Postfix = "PostfixWantedElementOnArrested", Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(StoreUIManager), Method = "OnGenericArrived", Prefix = "PrefixStoreUIManagerOnGenericArrived", Postfix = null, Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "" },
            // 10-16 闪退修复5：AugHelper.CleanupKill 挂载崩（32MB 栈仍崩=单条 cctor 解析毒，类含 List<String[]> 泛型方法）→ 删挂点。
            //   功能降级：追杀清理后 wanted6/7 电话簿移除标记暂停（MarkWantedPhoneRemoved 无等价入口）；核心到店链 WantedSupplierOnArrived 不受影响。
            new PatchEntry { Target = typeof(AdvCalendarUIManager), Method = "OnCalendarButtonClicked", Prefix = null, Postfix = "PostfixOnCalendarButtonClicked", Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(StoreCalendar), Method = "Update", Prefix = null, Postfix = "PostfixStoreCalendarUpdate", Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(StartOfDayUIManager), Method = "InitPanel", Prefix = null, Postfix = "PostfixStartOfDayInitPanel", Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = null, Note = "" },
            // 10-07 鲁滨逊完全归 WS（决策 1：交易归 WS——删交易 6 之①）：StoreClient.ApplyBudgetModifier Postfix Host=Patches（原 :236）已删——WS SurvivalTrade.PostfixStoreClientApplyBudgetModifier 承接
            new PatchEntry { Target = typeof(StoreClient), Method = "OverrideBudget", Prefix = null, Postfix = "PostfixOverrideBudget", Host = typeof(CompatibilityPatches), Priority = DEFAULT_PRIORITY, System = "Compat", YieldMod = null, Note = "10-05第三方溢出兜底(CustomerCreditBoost指数累乘int溢出写负,原版零clamp); FIX14/15蛙哥分支=异常恢复1,109,707,341(负/爆正)" },
            new PatchEntry { Target = typeof(StoreClient), Method = "ModBudget", Prefix = null, Postfix = "PostfixModBudget", Host = typeof(CompatibilityPatches), Priority = DEFAULT_PRIORITY, System = "Compat", YieldMod = null, Note = "FIX15b层2:ModBudget(int)纯加减无clamp,蛙哥异常恢复初始(讲价折扣/加价+卖货结算3调用点)", ParameterTypes = new System.Type[] { typeof(int) } },
            new PatchEntry { Target = typeof(StoreClient), Method = "HasBudgetLeftToBuy", Prefix = "PrefixHasBudgetLeftToBuy", Postfix = null, Host = typeof(CompatibilityPatches), Priority = DEFAULT_PRIORITY, System = "Compat", YieldMod = null, Note = "FIX15b层3:判据=GetNegociatedValue<=clientBudget,判据前校正蛙哥异常预算(提前返回分支在读数前不误伤)", ParameterTypes = new System.Type[] { typeof(GameItem) } },
            new PatchEntry { Target = typeof(StoreClient), Method = "ApplyBudgetModifier", Prefix = null, Postfix = "PostfixApplyBudgetModifier", Host = typeof(WageGirlSystem), Priority = DEFAULT_PRIORITY, System = "WageGirl", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(BargainUIManager), Method = "GetDealMakerBonus", Prefix = null, Postfix = "PostfixGetDealMakerBonus", Host = typeof(WageGirlSystem), Priority = DEFAULT_PRIORITY, System = "WageGirl", YieldMod = null, Note = "已实锤叠加：WageGirlSystem 蛙娘在场+50封顶100 / Patches 鲁滨逊+GetBargainBonusPct，双宿主同名 Postfix" },
            new PatchEntry { Target = typeof(GameItemElement), Method = "ApplyAnimationFrame", Prefix = "PrefixApplyAnimationFrame", Postfix = null, Host = typeof(WageGirlSystem), Priority = DEFAULT_PRIORITY, System = "WageGirl", YieldMod = null, Note = "" },
            new PatchEntry { Target = typeof(GameItemElement), Method = "ResolveSpriteByName", Prefix = null, Postfix = "PostfixResolveSpriteByName", Host = typeof(WageGirlSystem), Priority = DEFAULT_PRIORITY, System = "WageGirl", YieldMod = null, Note = "10-16 审阅 P0-3 修复：原 Prefix 槽填 Postfix 方法（ref Sprite __result 被原方法覆盖=空转），改回 Postfix 槽" },
            new PatchEntry { Target = typeof(StoreClientManager), Method = "PickClient", Prefix = null, Postfix = "PostfixStoreClientManagerPickClient", Host = typeof(Patches), Priority = DEFAULT_PRIORITY, System = "Core", YieldMod = null, Note = "" },
            // 10-07 鲁滨逊完全归 WS（决策 1：交易归 WS——删交易 6 之②-⑥）：BargainUIManager.OfferBuyingMarkup/GetDealMakerBonus + ItemFeatureList.BargainBuyingMarkup + ItemFeature.GetActualDisplay + StoreClient.OnDealAccepted Host=Patches（原 :243-247）已删——WS SurvivalTrade.cs 承接（交易域唯一宿主）
            new PatchEntry { Target = typeof(PlayerStore), Method = "LoadGame", Prefix = null, Postfix = "PostfixLoadGameOldSaveHint", Host = typeof(NewStartTypeUI), Priority = DEFAULT_PRIORITY, System = "NewStartType", YieldMod = null, Note = "10-07 WS-4 旧档引导：无 WS 读鲁滨逊档弹提示装 WageSurvival（会话内一次）" },
            new PatchEntry { Target = typeof(StoreClient), Method = "StartMainDialogue", Prefix = null, Postfix = "PostfixStartMainDialogue", Host = typeof(ThiefMagnetSystem), Priority = DEFAULT_PRIORITY, System = "ThiefMagnet", YieldMod = null, Note = "10-06 小偷对话上桌（BarterOffer对小偷失效——StartMainDialogue=客户对话入口实锤 StoreClient.txt:6181）" },
            new PatchEntry { Target = typeof(PlayerStore), Method = "LoadGame", Prefix = null, Postfix = "PostfixLoadGameThiefMagnet", Host = typeof(ThiefMagnetSystem), Priority = DEFAULT_PRIORITY, System = "ThiefMagnet", YieldMod = null, Note = "10-06 小偷读档重挂（eventSourceId运行时标记不随档→双键判定+KEY_STOLEN重挂）" },
            new PatchEntry { Target = typeof(PlayerStore), Method = "OnItemBought", Prefix = null, Postfix = "PostfixPlayerStoreOnItemBought", Host = typeof(ThiefMagnetSystem), Priority = DEFAULT_PRIORITY, System = "ThiefMagnet", YieldMod = null, Note = "10-06 被偷物买回精确清账（OnItemBought=三条购买路径统一汇点，参数=被买物品——拆包5F6k3jY7Kz628HwFRxWAWR；OnDealAccepted死路不挂）" },
            new PatchEntry { Target = typeof(StoreClientListTier), Method = "CreateThirstySpacer", Prefix = null, Postfix = "PostfixCreateThirstySpacer", Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = "Survival", Note = "10-11 所有权表：WS 承接同目标（PatchRegistry.cs:330）→双装双 Postfix 双生成→让路" },
            new PatchEntry { Target = typeof(StoreClientListTier), Method = "CreateHungrySpacer", Prefix = null, Postfix = "PostfixCreateHungrySpacer", Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = "Survival", Note = "10-11 所有权表：WS 承接同目标→让路" },
            new PatchEntry { Target = typeof(StoreClientListTier), Method = "CreateSpacerChef", Prefix = null, Postfix = "PostfixCreateSpacerChef", Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = "Survival", Note = "10-11 所有权表：WS 承接同目标→让路" },
            new PatchEntry { Target = typeof(StoreClientListTier), Method = "CreateInjuredSpacer", Prefix = null, Postfix = "PostfixCreateInjuredSpacer", Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = "Survival", Note = "10-11 所有权表：WS 承接同目标→让路" },
            new PatchEntry { Target = typeof(StoreClientListTier), Method = "CreateSickChildCaretaker", Prefix = null, Postfix = "PostfixCreateSickChildCaretaker", Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = "Survival", Note = "10-11 所有权表：WS 承接同目标→让路" },
            new PatchEntry { Target = typeof(StoreClientListTierSubstance), Method = "CreateDesperateAddict", Prefix = null, Postfix = "PostfixCreateDesperateAddict", Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = "Survival", Note = "10-11 所有权表：WS 承接同目标→让路" },
            new PatchEntry { Target = typeof(StoreClientListTierSubstance), Method = "CreateWornOutSpacer", Prefix = null, Postfix = "PostfixCreateWornOutSpacer", Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = "Survival", Note = "10-11 所有权表：WS 承接同目标→让路" },
            new PatchEntry { Target = typeof(StoreClientListMinor), Method = "CreateSickLowers", Prefix = null, Postfix = "PostfixCreateSickLowers", Host = typeof(RobinCrusoePerk), Priority = DEFAULT_PRIORITY, System = "Survival", YieldMod = "Survival", Note = "10-11 所有权表：WS 承接同目标→让路" },

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
            // 10-14 B2-R：删服务卡双击挂点（查看服务改走电话 1005 → 原生面板；服务卡双击恢复原生打开）
            // 10-07 阶段D D-5：ItemMouseDoubleClickHandler.DoubleClickAction PostfixDoubleClickAction RobinCrusoePerk（原 :274）已删——WS SurvivalConsume.PostfixDoubleClickAction 承接（全局双击吃喝用）
            new PatchEntry { Target = typeof(ItemMouseDoubleClickHandler), Method = "DoubleClickAction", Prefix = null, Postfix = "PostfixDoubleClickAction", Host = typeof(WageGirlSystem), Priority = DEFAULT_PRIORITY, System = "WageGirl", YieldMod = null, Note = "双击喂蛙娘", ParameterTypes = new System.Type[] { typeof(GameItem), typeof(UnityEngine.Vector2) } },
            // ===== B2-R 蛙哥原生服务面板（10-14 派活单：电话1005 + 原生WildUI面板 22条注入 + 附赠开局物品） =====
            new PatchEntry { Target = typeof(NetworkUpgrade), Method = "InitUpgradeDict", Prefix = null, Postfix = "PostfixInitUpgradeDict", Host = typeof(B2WildPanel), Priority = DEFAULT_PRIORITY, System = "WageBroker", YieldMod = null, Note = "B2-R：22条注入networkUpgrade(0x150)，PlayerStore .ctor 每档重建" },
            new PatchEntry { Target = typeof(NetworkUpgrade), Method = "GetCost", Prefix = null, Postfix = "PostfixGetCost", Host = typeof(B2WildPanel), Priority = DEFAULT_PRIORITY, System = "WageBroker", YieldMod = null, Note = "B2-R：b2_条目定价分派（买/升/消除/吞噬季）" },
            new PatchEntry { Target = typeof(NetworkUpgrade), Method = "GetLocalizedTitle", Prefix = null, Postfix = "PostfixGetLocalizedTitle", Host = typeof(B2WildPanel), Priority = DEFAULT_PRIORITY, System = "WageBroker", YieldMod = null, Note = "B2-R：特性显示名" },
            new PatchEntry { Target = typeof(NetworkUpgrade), Method = "GetLocalizedSubtitle", Prefix = null, Postfix = "PostfixGetLocalizedSubtitle", Host = typeof(B2WildPanel), Priority = DEFAULT_PRIORITY, System = "WageBroker", YieldMod = null, Note = "B2-R：类型+等级/状态" },
            new PatchEntry { Target = typeof(NetworkUpgrade), Method = "GetLocalizedDescription", Prefix = null, Postfix = "PostfixGetLocalizedDescription", Host = typeof(B2WildPanel), Priority = DEFAULT_PRIORITY, System = "WageBroker", YieldMod = null, Note = "B2-R：效果+当前等级+吞噬季状态" },
            new PatchEntry { Target = typeof(NetworkUpgrade), Method = "GetLocalizedAlreadyBought", Prefix = null, Postfix = "PostfixGetLocalizedAlreadyBought", Host = typeof(B2WildPanel), Priority = DEFAULT_PRIORITY, System = "WageBroker", YieldMod = null, Note = "B2-R：满级/已消除/开局已选" },
            new PatchEntry { Target = typeof(NetworkUpgrade), Method = "IsUnlocked", Prefix = null, Postfix = "PostfixIsUnlocked", Host = typeof(B2WildPanel), Priority = DEFAULT_PRIORITY, System = "WageBroker", YieldMod = null, Note = "B2-R：按钮锁定态（满级/已消除锁；吞噬季恒可点=切换模式）" },
            new PatchEntry { Target = typeof(NetworkUpgrade), Method = "IsReady", Prefix = "PrefixIsReady", Postfix = null, Host = typeof(B2WildPanel), Priority = DEFAULT_PRIORITY, System = "WageBroker", YieldMod = null, Note = "B2-R：可点前置（未满级/未消除/招贼未赎回锁）；FIX5 改Prefix——原版IsReady内部IsHavePrerequisite对prerequisite=null NRE，Postfix来不及" },
            new PatchEntry { Target = typeof(NetworkUpgrade), Method = "IsHavePrerequisite", Prefix = "PrefixIsHavePrerequisite", Postfix = null, Host = typeof(B2WildPanel), Priority = DEFAULT_PRIORITY, System = "WageBroker", YieldMod = null, Note = "B2-R-FIX5：b2_条目无前置直接true，根治prerequisite=null NRE（日志22:07:00实锤）" },
            new PatchEntry { Target = typeof(WildUIManager), Method = "OpenUI", Prefix = null, Postfix = "PostfixOpenUI", Host = typeof(B2WildPanel), Priority = DEFAULT_PRIORITY, System = "WageBroker", YieldMod = null, Note = "B2-R2：蛙哥/王尔德分流（标题+元素显隐）", ParameterTypes = new System.Type[] { typeof(bool) } }, // 10-14 修复：OpenUI(bool debug=False) 带参，无参匹配静默失败
            new PatchEntry { Target = typeof(WildUIManager), Method = "CloseUI", Prefix = null, Postfix = "PostfixCloseUI", Host = typeof(B2WildPanel), Priority = DEFAULT_PRIORITY, System = "WageBroker", YieldMod = null, Note = "B2-R2：关闭面板复位 _wageMode（防污染王尔德路径）" },
            new PatchEntry { Target = typeof(StorePhoneClient), Method = "GetLocalizedDisplayName", Prefix = "PrefixGetLocalizedDisplayName", Postfix = null, Host = typeof(B2WildPanel), Priority = DEFAULT_PRIORITY, System = "WageBroker", YieldMod = null, Note = "B2-R2-H2：空 locID fallback displayName（修来电 name 行 in ClientName 错误，电话簿+通话同点全覆盖）" },
            new PatchEntry { Target = typeof(PlayerStore), Method = "StartNewGame", Prefix = null, Postfix = "PostfixStartNewGame_EnsurePhone", Host = typeof(B2WildPanel), Priority = DEFAULT_PRIORITY, System = "WageBroker", YieldMod = null, Note = "B2-R：蛙哥电话1005注册（电话簿直接可见）——10-16闪退修复：InitPhoneClientDict返回泛型Dictionary触发Il2CppInterop栈溢出，改挂StartNewGame(非泛型)；拆包实锤InitPhoneClientDict全库仅1调用=StartNewGame（PlayerStore.txt:2080）；读档走OnStoreGameLoaded兜底（Patches.Lifecycle.cs:541）" },
            new PatchEntry { Target = typeof(PhoneUIManager), Method = "StartPhoneDialog", Prefix = "PrefixStartPhoneDialog", Postfix = null, Host = typeof(B2WildPanel), Priority = DEFAULT_PRIORITY, System = "WageBroker", YieldMod = null, Note = "B2-R：1005拨通→打开原生查看服务面板", ParameterTypes = new System.Type[] { typeof(long) } }, // 10-14 修复：StartPhoneDialog(long) 带参，无参匹配静默失败
            new PatchEntry { Target = typeof(WildUIManager), Method = "Update", Prefix = null, Postfix = "PostfixUpdate", Host = typeof(B2WildPanel), Priority = DEFAULT_PRIORITY, System = "WageBroker", YieldMod = null, Note = "B2-R2-H3-FIX6：恩惠行蛙哥化（每帧替换王尔德→蛙哥，仅_wageMode期间；王尔德零开销）——拆包实锤恩惠行=Update每帧GetLocalizedUI SetText" },
            new PatchEntry { Target = typeof(CustomUIManager), Method = "ShowTooltip", Prefix = "PrefixShowTooltip", Postfix = "PostfixShowTooltip", Host = typeof(B2WildPanel), Priority = DEFAULT_PRIORITY, System = "WageBroker", YieldMod = null, Note = "B2-R2-H3-FIX13：tooltip蛙哥化（FIX6b实锤唯一入口ShowTooltip(实例,text参数)→Prefix ref替换含'王尔德'文本=蛙哥文案）+取证日志", ParameterTypes = new System.Type[] { typeof(string), typeof(CustomUITooltipTrigger) } },
            // 10-16 闪退修复9（二分法实锤）：剧情挂点 5 条（去掉 Nellis stub）
            //   实锤链：step1 删剧情6条=不崩 / step2 只挂 CheckStoryLine×3 实装=不崩 / 全量6条=崩
            //   → 毒条=CheckStoryLineNellis（dump.cs:39438 ret 0 极短 stub，Harmony 首字节补丁需 5 字节→溢出覆盖相邻方法→启动即崩 0xc00000fd GameAssembly.dll 0x280cf7）
            //   Nellis 剧情不受影响：MaybeAdvanceNellisLine 已在 Eleanor/OfficerLun/Georgio 三个 Postfix 内调度（StoryPlotSystem.cs:109）。
            new PatchEntry { Target = typeof(StoreClientManager), Method = "CheckStoryLineEleanor", Prefix = null, Postfix = "PostfixCheckStoryLineEleanor", Host = typeof(StoryPlotHooks), Priority = DEFAULT_PRIORITY, System = "StoryPlot", YieldMod = null, Note = "剧情mod P2：Eleanor 线 N1-N4 三路选项端到端（AddChoice+Mod*Reputation+60天结算报告）" },
            new PatchEntry { Target = typeof(StoreClientManager), Method = "CheckStoryLineOfficerLun", Prefix = null, Postfix = "PostfixCheckStoryLineOfficerLun", Host = typeof(StoryPlotHooks), Priority = DEFAULT_PRIORITY, System = "StoryPlot", YieldMod = null, Note = "剧情mod P2：OfficerLun 治安部线 N1-N4 三路选项端到端" },
            new PatchEntry { Target = typeof(StoreClientManager), Method = "CheckStoryLineGeorgio", Prefix = null, Postfix = "PostfixCheckStoryLineGeorgio", Host = typeof(StoryPlotHooks), Priority = DEFAULT_PRIORITY, System = "StoryPlot", YieldMod = null, Note = "剧情mod P2：Georgio 下层区线 N1/N3/N4 三路选项端到端" },
            new PatchEntry { Target = typeof(StoreEventManager), Method = "OnDayStart", Prefix = null, Postfix = "PostfixStoryNewsDayStart", Host = typeof(StoryPlotHooks), Priority = DEFAULT_PRIORITY, System = "StoryNews", YieldMod = null, Note = "剧情mod P0：新闻事件确定性注入（QueueFuturEvent 次日上报纸+价格联动，negociationDatas value 正=涨）" },
            new PatchEntry { Target = typeof(PlayerStore), Method = "ExecuteGameOver", Prefix = "PrefixExecuteGameOver", Postfix = null, Host = typeof(StoryPlotHooks), Priority = DEFAULT_PRIORITY, System = "StoryPlot", YieldMod = null, Note = "剧情mod 真结局：拦截 wages_* 新键短路原生（避免未匹配14键走rent默认文案），自调 GameOverUIManager.DisplayUI(mod文案,true) 档不结束" },
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

            // 10-16 闪退修复7（git 证据+实测闭环）：WagePerks_v1.3.6.dll（204条，主线程单批）用户实测【能进】；
            //   当前源码 208 条（新线程分批+32MB栈）实测崩 #165 GameItemElement.ApplyAnimationFrame（两轮独立运行同点复现，
            //   4MB stackalloc 探针过=maxStackSize 生效仍崩）→ 分批/新线程/大栈方案证伪；
            //   还原 1.3.6 挂载方式=【主线程顺序单批】（interop 已回滚 1.5.1 同步 1.3.6 环境，SHA256 已验证）。
            int ok = 0, fail = 0, yield = 0;
            foreach (var e in REGISTRY)
            {
                try
                {
                    // 10-11 所有权表 Step 2：YieldMod="Survival" 专用白名单让路（WS 承接同目标=双挂，非一刀切）
                    if (e.YieldMod == "Survival" && NewStartTypeUI.IsWageSurvivalPresent())
                    {
                        Core.LogMsg($"[统一注册] 让路(WS承接): [{e.System}] {e.Target.Name}.{e.Method}");
                        yield++; continue;
                    }
                    if (e.YieldMod != null && ModCompat.ShouldYield(e.Target, e.Method, out var mod))
                    {
                        Core.LogMsg($"[统一注册] 让路: [{e.System}] {e.Target.Name}.{e.Method} ← {mod}");
                        yield++; continue;
                    }
                    Core.LogMsg($"[统一注册] >>> [{e.System}] {e.Target.Name}.{e.Method}");
                    if (e.UseByName)
                    {
                        if (ManualPatcher.TryPatchByName(e.Target, e.Method, e.Prefix, e.Postfix, patchHost: e.Host)) ok++; else fail++;
                    }
                    else
                    {
                        if (ManualPatcher.TryPatch(e.Target, e.Method, e.Prefix, e.Postfix,
                                                   parameterTypes: e.ParameterTypes, patchHost: e.Host, priority: e.Priority,
                                                   finalizer: e.Finalizer)) ok++; else fail++;
                    }
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
        // 10-16 审阅 P1-3：ApplyBatch（分批+新线程）已被 10-16 闪退修复7 证伪废弃，整段删除（原 :350-386，零调用点死代码）
    }
}
