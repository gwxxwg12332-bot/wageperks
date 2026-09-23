using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSystem;
using Il2CppSystem.Collections.Generic;
using Il2CppSystem.Reflection;
using Il2CppTMPro;
using JacksonPerks;
using MelonLoader;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace JacksonPerks;

public static class PatchRegistry
{
	public static void ApplyAll()
	{
		try
		{
			System.Func<StoreClient> @delegate = StoreClientListWanted.CreateWanted7;
			StoreClientListDict.storeClientDict["wanted7"] = DelegateSupport.ConvertDelegate<Il2CppSystem.Func<StoreClient>>(@delegate);
		}
		catch (System.Exception ex)
		{
			Core.LogMsg("[WagePerks] 注入 wanted7 失败: " + ex.Message);
		}
		try
		{
			System.Func<StoreClient> delegate2 = StoreClientListWanted.CreateWanted6;
			StoreClientListDict.storeClientDict["wanted6"] = DelegateSupport.ConvertDelegate<Il2CppSystem.Func<StoreClient>>(delegate2);
		}
		catch (System.Exception ex2)
		{
			Core.LogMsg("[WagePerks] 注入 wanted6 失败: " + ex2.Message);
		}
		try
		{
			ManualPatcher.TryPatch(typeof(StartingPerkList), "InitStartingPerk", null, "PostfixInitStartingPerks");
			ManualPatcher.TryPatch(typeof(GameMaster), "NewGame", null, "PostfixOnNewGame");
			ManualPatcher.TryPatch(typeof(NewGameData), "HandleInitialItem", null, "HandleInitialItemPostfix");
			// 09-21 拆包实锤：四件唯一发放点 = PlayerStore.HandleSkipIntro（EmporiumEntry.Start L7742）→ 流浪者清+发挂此处（清完 InitialSave 不入档）
			ManualPatcher.TryPatch(typeof(PlayerStore), "HandleSkipIntro", null, "PostfixHandleSkipIntro", null, typeof(WandererPerk));
			// 阶段2 迁移（2026-09-23）：每日权威信号从 StoreClientManager.OnNewDay 改为 StoreEventManager.OnDayStart。
			// 拆包依据：两者同链(BeginDay→StartDay→OnDayStart→OnNewDay)，每天各 1 次，OnDayStart 先；
			// 而 StoreClientManager.OnNewDay 方法体仅 8 字节（私有计数自增），且其宿主为 null 时会被静默 return
			// （OnDayStart 侧为 Interrupt，可靠）→ 后者不配当权威信号。
			// ⚠️ 顺序变化：本方法逻辑从链中段前移到链首，回归重点看跨系统一致性（如证据条与销赃额是否仍同天结算）。
			ManualPatcher.TryPatch(typeof(StoreEventManager), "OnDayStart", null, "PostfixOnNewDay");
			// 阶段2 保留不动：BeginDay Postfix 是链尾（BeginDay→StartDay→OnDayStart→OnNewDay→SecData→CommissaryData→回到 BeginDay Postfix），
			// 承载"所有日切子系统跑完后"的逻辑（TickWantedSupplierDaily）。迁到 OnDayStart 会把它从链尾挪到链首，
			// 顺序变化面远大于收益，故**有意保留**。它与 PostfixOnNewDay 有重叠调用（ForceInspectionToday/ApplyBadLuck/ScheduleJacksonToday），
			// 但三者各自有幂等键（_inspectionTriggeredDay / BadLuck.last_day / _lastScheduledDay），重复调用安全。
			ManualPatcher.TryPatch(typeof(PlayerStore), "BeginDay", null, "PostfixOnBeginDay");
			ManualPatcher.TryPatch(typeof(PlayerStore), "AddDirectSellingItemToTable", "PrefixAddDirectSellingItemToTable", null, null, typeof(WaterMerchantPerk));
			ManualPatcher.TryPatchByName(typeof(MachineBottlePrinter.__c__DisplayClass6_0), "Method_Internal_Void_String_Int32_0", "PrefixTryPrint", "PostfixTryPrint", typeof(WaterMerchantPerk));
			ManualPatcher.TryPatchByName(typeof(MachineFeedDispenser.__c__DisplayClass7_0), "_CreateFeedDispenser_b__3", "PrefixFeedDispenserB3", null, typeof(RobinCrusoePerk));
			try
			{
				System.Type type = System.Type.GetType("PreBuildChemHelper, Assembly-CSharp");
				if (type != null)
				{
					ManualPatcher.TryPatch(type, "CreateAcidBottle", "PrefixCreateAcidBottle");
					ManualPatcher.TryPatch(type, "CreateBaseBottle", "PrefixCreateBaseBottle");
				}
			}
			catch
			{
			}
			ManualPatcher.TryPatch(typeof(StoreClientManager), "AddClient", null, "PostfixOnAddClient", new System.Type[1] { typeof(StoreClient) });
			ManualPatcher.TryPatch(typeof(PlayerStore), "SaveGame", null, "PostfixSaveGame", null, typeof(LuckScoutPerk));
			ManualPatcher.TryPatch(typeof(PlayerStore), "LoadGame", null, "PostfixOnLoadGame");
			ManualPatcher.TryPatch(typeof(PlayerStore), "LoadGame", null, "PostfixLoadGame", null, typeof(LuckScoutBackpackUpgrade));
			ManualPatcher.TryPatch(typeof(TradeSheet), "GetFoundryShop", null, "PostfixTradeSheetFoundryShop");
			ManualPatcher.TryPatch(typeof(TradeSheet), "GetEnergyFarmShop", null, "PostfixTradeSheetEnergyFarmShop");
			ManualPatcher.TryPatch(typeof(StoreClientManager), "HandleContentUnlockClient", null, "PostfixOnHandleContentUnlockClient");
					ManualPatcher.TryPatch(typeof(InputActionManager), "Update", null, "PostfixInputActionManagerUpdate");
		ManualPatcher.TryPatch(typeof(InventorySortHelper), "Sort", null, "PostfixSort", null, typeof(RobinCrusoePerk)); // 09-22 右键排列后恢复 shape
			ManualPatcher.TryPatch(typeof(StoreUIManager), "OnNextClientArrived", null, "PostfixSpecialNpcStartDialogue");
			ManualPatcher.TryPatch(typeof(DialogUIManager), "DisplayClientText", "PrefixDisplayClientText", null, new System.Type[1] { typeof(Dialogue) });
			ManualPatcher.TryPatch(typeof(ScavHelper), "GetMaxScavAttempts", null, "PostfixGetMaxScavAttempts", null, typeof(LuckScoutPerk));
			ManualPatcher.TryPatch(typeof(ScavHelper), "GetScavTimeLeft", null, "PostfixGetScavTimeLeft", null, typeof(LuckScoutPerk));
			ManualPatcher.TryPatch(typeof(ScavHelper), "CanScavenge", "PrefixCanScavenge", "PostfixCanScavenge", null, typeof(LuckScoutPerk));
			ManualPatcher.TryPatch(typeof(ScavHelper), "ScavengeDumpingGrounds", "PrefixScavengeDumpingGrounds", "PostfixScavengeDumpingGrounds", null, typeof(LuckScoutPerk));
			ManualPatcher.TryPatch(typeof(GameMaster), "QuitToMenu", null, "PostfixQuitToMenu", null, typeof(LuckScoutPerk));
			ManualPatcher.TryPatch(typeof(EscapeUIManager), "OnMainMenu", null, "PostfixOnMainMenu", null, typeof(LuckScoutPerk));
			ManualPatcher.TryPatch(typeof(GameMaster), "NewGame", null, "PostfixNewGame", null, typeof(LuckScoutPerk));
			ManualPatcher.TryPatch(typeof(ScavHelper), "GetRandomScavengedItem", null, "PostfixGetRandomScavengedItem", null, typeof(LuckScoutPerk));
			ManualPatcher.TryPatch(typeof(ScavHelper), "CreateTooltip", null, "PostfixCreateTooltip", new System.Type[2]
			{
				typeof(RichTextBuilder),
				typeof(GameItem)
			}, typeof(LuckScoutPerk));
			ManualPatcher.TryPatch(typeof(EmporiumEntry), "GetAllAfterhourOwnedItems", null, "PostfixGetAllAfterhourOwnedItems", null, typeof(LuckScoutPerk));
			ManualPatcher.TryPatch(typeof(PerkUIController), "OpenUI", null, "PostfixPerkUiOpen");
			ManualPatcher.TryPatch(typeof(StartingPerkIconLoader), "Start", null, "PostfixIconLoaderStart");
			ManualPatcher.TryPatch(typeof(NetworkUpgrade), "Unlock", "Prefix", "Postfix", null, typeof(DetectiveUpgradePatch));
			ManualPatcher.TryPatch(typeof(SecData), "OnFixerUsed", "Prefix", null, null, typeof(DetectiveFixerPatch));
			ManualPatcher.TryPatch(typeof(SecData), "CommitCrime", "Prefix", null, null, typeof(DetectiveCommitCrimePatch));
			ManualPatcher.TryPatch(typeof(NegociationUIManager), "SellItem", null, "Postfix", null, typeof(SoldContrabandCounterPatch));
			ManualPatcher.TryPatch(typeof(SecData), "OnNewDay", null, "Postfix", null, typeof(SoldEvidenceOnNewDayPatch));
			ManualPatcher.TryPatch(typeof(SecData), "OnFixerUsed", null, "Postfix", null, typeof(WildeFixerPatch));
			ManualPatcher.TryPatch(typeof(BarterHelper), "DoesTraderAcceptThisItemAsPayment", null, "Postfix", null, typeof(CounterfeitWineTradeFix));
			ManualPatcher.TryPatchAllOverloads(typeof(LocHelper), "GetLocalizedPerkTable", null, "PostfixGetLocalizedPerkTable");
			ManualPatcher.TryPatch(typeof(StartingPerkElement), "Start", null, "PostfixStartingPerkElementStart");
			ManualPatcher.TryPatch(typeof(GameItem), "GetNegociatedValue", "PrefixGameItemGetNegociatedValue", "PostfixGameItemGetNegociatedValue");
			ManualPatcher.TryPatchAllOverloads(typeof(GameItem), "GetCurrentValue", "PrefixGameItemGetCurrentValue", "PostfixGameItemGetCurrentValue");
			ManualPatcher.TryPatchAllOverloads(typeof(GameItem), "GetValue", null, "PostfixGameItemGetValue");
			ManualPatcher.TryPatch(typeof(NegociationUIManager), "InitUIWithItemSellMode", null, "PostfixUIInitSellMode");
			ManualPatcher.TryPatch(typeof(ClientCanExposeFunc), "ClientNoExposeInjector", "PrefixClientNoExposeInjector", null, new System.Type[1] { typeof(ItemFeature) });
			ManualPatcher.TryPatch(typeof(ItemFeature), "GetClientExposeDialog", "PrefixGetClientExposeDialog");
			ManualPatcher.TryPatch(typeof(NegociationUIManager), "InitUIWithItemBuyMode", null, "PostfixUIInitBuyMode");
			ManualPatcher.TryPatch(typeof(NegociationUIManager), "CloseUI", null, "PostfixUIClose");
			ManualPatcher.TryPatch(typeof(GameItem), "GetDisplayName", null, "PostfixGameItemGetDisplayName");
			ManualPatcher.TryPatch(typeof(BargainUIManager), "GetDealMakerBonus", null, "PostfixDealMakerBonus", new System.Type[0]);
			// 09-23 游戏版本适配：方法由 ComputeTradeRepMultiplier(返回 double) 改为
			// ComputeTradeRepMultipliers(返回 ValueTuple<double,double>)，故宿主 Postfix 同步改名并改签名。
			// 依据：_Demo_20260915_cpp2il IsilDump\Assembly-CSharp\BargainUIManager.txt:8752
			ManualPatcher.TryPatchByName(typeof(BargainUIManager), "ComputeTradeRepMultipliers", null, "PostfixTradeRepMultipliers");
			ManualPatcher.TryPatch(typeof(StartOfDayUIManager), "OpenUI", null, "PostfixStartOfDayOpenUI");
			ManualPatcher.TryPatch(typeof(StartOfDayUIManager), "ShowMorningReport", null, "PostfixStartOfDayShowMorningReport");
			ManualPatcher.TryPatch(typeof(StartOfDayUIManager), "OnStartDayButtonClicked", null, "PostfixStartOfDayButtonClicked");
			Diagnostics.ApplyPatches();
			ManualPatcher.TryPatch(typeof(RenderHandler), "LoadFromAtlas", "PrefixLoadFromAtlas");
			ManualPatcher.TryPatch(typeof(RenderHandler), "LoadFromAtlas", "PrefixLoadFromAtlas", null, null, typeof(LuckScoutBackpackUpgrade));
			ManualPatcher.TryPatch(typeof(RenderHandler), "LoadFromAtlas", "PrefixLoadFromAtlas", null, null, typeof(DestinyDice));
			ManualPatcher.TryPatch(typeof(RenderHandler), "LoadFromAtlas", "PrefixLoadFromAtlas", null, null, typeof(GuMachineSystem));
			ManualPatcher.TryPatch(typeof(RenderHandler), "LoadFromAtlas", "PrefixLoadFromAtlas", null, null, typeof(WageGirlSystem)); // 09-20 蛙娘图标链
			ManualPatcher.TryPatch(typeof(PlayerStore), "SaveGame", null, "PostfixSaveGame", null, typeof(WageGirlSystem)); // 09-20 蛙娘打烊落盘内存缓存
			ManualPatcher.TryPatch(typeof(PlayerStore), "StartNewGame", null, "PostfixStartNewGame", null, typeof(WageGirlSystem)); // 09-20 蛙娘新档硬重置
			ManualPatcher.TryPatch(typeof(PlayerStore), "SaveGame", null, "PostfixSaveGame", null, typeof(RobinCrusoePerk)); // 09-20 鲁滨逊打烊落盘血量
			ManualPatcher.TryPatch(typeof(PlayerStore), "SaveGame", "PrefixSaveGame", null, null, typeof(ContainerUpgradeV2)); // 09-23 修：妙妙箱打烊吃螺丝改 Prefix（存档前跑，否则读档回退）
			ManualPatcher.TryPatch(typeof(PlayerStore), "SaveGame", null, "PostfixSaveGame", null, typeof(WageSaveStore), 0); // 阶段1：统一持久化层全局落盘门面——priority 0 保证最后跑（所有系统的 Set 先进内存再一次性原子落盘）

			ManualPatcher.TryPatch(typeof(StoreEventManager), "OnDayStart", null, "OnDayStartPostfix", null, typeof(ModCannibalism));
			ManualPatcher.TryPatch(typeof(StoreEventManager), "OnDayStart", null, "OnDayStartPostfix", null, typeof(BatteryCannibalism));
			// 09-21 封存：成瘾警官事件 OnDayStart 挂点关闭
			// 阶段2：治安部眼线不再单独占坑 —— 已改为 override CustomStartingPerk.OnDayStart()，
			// 由统一驱动入口 PostfixUnifiedDayStart 驱动（原 static new OnNewDay 陷阱已拆除，勿再单独注册）
			ManualPatcher.TryPatch(typeof(StoreEventManager), "OnDayStart", null, "OnDayStartPostfix", null, typeof(GuMachineSystem));
			ManualPatcher.TryPatch(typeof(StoreEventManager), "OnDayStart", null, "PostfixOnDayStart", null, typeof(WageGirlSystem)); // 09-21 蛙娘：全局常驻——每日六维衰减+首次发放（方法名 PostfixOnDayStart）
			ManualPatcher.TryPatch(typeof(StoreEventManager), "OnDayStart", null, "PostfixOnDayStart", null, typeof(InfamousPerk)); // 09-21 声名狼藉：第1天送5000
			ManualPatcher.TryPatch(typeof(StoreEventManager), "OnDayStart", null, "PostfixOnDayStart", null, typeof(HatedByAllPerk)); // 09-22 人神共愤：每天扣声望+扣钱
			ManualPatcher.TryPatch(typeof(StoreEventManager), "OnDayStart", null, "PostfixStoreEventOnDayStart", null, typeof(DestinyDice));
			// 阶段2 统一生命周期入口（详见 Patches.PostfixUnifiedDayStart / PostfixSaveGame 注释）
			ManualPatcher.TryPatch(typeof(StoreEventManager), "OnDayStart", null, "PostfixUnifiedDayStart"); // 驱动全部特性 OnDayStart（幂等去重 + 逐特性异常隔离）
			ManualPatcher.TryPatch(typeof(PlayerStore), "SaveGame", null, "PostfixSaveGame"); // 驱动全部特性 OnSaveGame；默认 priority(400) 高于 WageSaveStore 的 0 → 先写内存，统一层最后落盘
			ManualPatcher.TryPatch(typeof(NewsUIManager), "PopulateUI", null, "PostfixNewsPopulateUI", null, typeof(DestinyDice));
			ManualPatcher.TryPatch(typeof(NewsUIManager), "PopulateUI", null, "PostfixNewsPopulateUI", null, typeof(ModCannibalism));
			ManualPatcher.TryPatch(typeof(ModuleHelper), "CreateModuleTooltip", null, "PostfixCreateModuleTooltip", null, typeof(ModCannibalism));
			ManualPatcher.TryPatch(typeof(AdvCalendarUIManager), "OnNightlyReportButtonClicked", null, "PostfixOnNightlyReportButtonClicked", null, typeof(ModCannibalism));
			ManualPatcher.TryPatch(typeof(AdvCalendarUIManager), "OpenUIFromNightlyReport", null, "PostfixOpenUIFromNightlyReport", null, typeof(ModCannibalism));
			ManualPatcher.TryPatch(typeof(AdvCalendarUIManager), "OnStatusButtonClicked", null, "PostfixOnStatusButtonClicked", null, typeof(ModCannibalism));
			ManualPatcher.TryPatch(typeof(NewsUIManager), "ToggleUI", null, "PostfixNewsPopulateUI", null, typeof(ModCannibalism));
			ManualPatcher.TryPatch(typeof(NewsUIManager), "ToggleUI", null, "PostfixNewsToggleUI", null, typeof(DestinyDice));
			ManualPatcher.TryPatch(typeof(NewsUIManager), "CloseUI", null, "PostfixNewsCloseUI", null, typeof(DestinyDice));
			ManualPatcher.TryPatch(typeof(InputActionManager), "Update", null, "PostfixNewsInputUpdate", null, typeof(DestinyDice));
			ManualPatcher.TryPatch(typeof(GameItem), "MayHaveValidInventorySlot", "PrefixMayHaveValidInventorySlot", null, null, typeof(DestinyDice));
			ManualPatcher.TryPatch(typeof(GameItem), "MayTarget", "PrefixMayTarget", null, null, typeof(DestinyDice));
			ManualPatcher.TryPatch(typeof(GameItem), "CanTarget", "PrefixCanTarget", null, null, typeof(DestinyDice));
			ManualPatcher.TryPatch(typeof(GameItem), "Target", "PrefixTarget", null, null, typeof(DestinyDice));
			ManualPatcher.TryPatch(typeof(ItemMouseDoubleClickHandler), "DoubleClickAction", "PrefixDoubleClickAction", null, null, typeof(DestinyDice));
			Core.LogMsg("[Patch] 命运骰子拖放吸收已注册");
			ManualPatcher.TryPatch(typeof(ContainerItemDirectory), "InitDirectory", null, "PostfixInitDirectory");
			ManualPatcher.TryPatch(typeof(AmenitiesItemDirectory), "InitDirectory", null, "PostfixInitDirectory");
			ManualPatcher.TryPatch(typeof(ModItemDirectory), "InitDirectory", null, "PostfixInitDirectory");
			Core.LogMsg("[Patch] LoadFromAtlas自定义sprite拦截已注册");
			ManualPatcher.TryPatch(typeof(StartingPerkElement), "OnPointerClick", "PrefixOnPointerClick", "PostfixOnPointerClick", new System.Type[1] { typeof(PointerEventData) });
			ManualPatcher.TryPatch(typeof(MainMenuUIController), "Awake", null, "PostfixAwake", null, typeof(NewStartTypeUI));
			ManualPatcher.TryPatch(typeof(MainMenuUIController), "ResetAllTab", null, "PostfixResetAllTab", null, typeof(NewStartTypeUI));
			ManualPatcher.TryPatch(typeof(NewGameData), "GetStartDisplayName", null, "PostfixGetStartDisplayName", patchHost: typeof(NewStartTypeUI), parameterTypes: new System.Type[1] { typeof(NewGameData.StartType) });
			ManualPatcher.TryPatch(typeof(SaveFiles), "BuildPreviewFromStore", null, "PostfixBuildPreviewFromStore", patchHost: typeof(NewStartTypeUI), parameterTypes: new System.Type[2]
			{
				typeof(PlayerStore),
				typeof(int)
			});
			ManualPatcher.TryPatch(typeof(PlayerStore), "SaveGame", "PrefixSaveGame", "PostfixSaveGame", null, typeof(NewStartTypeUI));
			ManualPatcher.TryPatch(typeof(PlayerStore), "SaveGame", null, "PostfixSaveGame", null, typeof(RobinCrusoePerk));
			ManualPatcher.TryPatch(typeof(PlayerStore), "LoadGame", null, "PostfixLoadGame", null, typeof(NewStartTypeUI));
			ManualPatcher.TryPatch(typeof(StoreClientManager), "HandleMinorClient", "PrefixHandleMinorClient", null, null, typeof(RobinCrusoePerk));
			ManualPatcher.TryPatch(typeof(StoreClientManager), "PickClient", "PrefixPickClient", null, null, typeof(RobinCrusoePerk));
			ManualPatcher.TryPatch(typeof(MapUIManager), "OpenGoOutsideConfirm", "PrefixOpenGoOutsideConfirm", null, null, typeof(RobinCrusoePerk));
			// 阶段2 迁移：鲁滨逊每日结算同迁 OnDayStart（原注释"原挂 StoreClientManager.OnNewDay 触发时机不可靠"已被拆包证实）
			ManualPatcher.TryPatch(typeof(StoreEventManager), "OnDayStart", null, "PostfixOnNewDay", null, typeof(RobinCrusoePerk));
			ManualPatcher.TryPatch(typeof(StoreClientManager), "HandleInspectionClient", "PrefixHandleInspectionClient", "PostfixHandleInspectionClient");
			ManualPatcher.TryPatch(typeof(StoreReputation), "IsPerkUnlocked", null, "PostfixIsPerkUnlocked");
			ManualPatcher.TryPatch(typeof(ItemMouseDoubleClickHandler), "DoubleClickAction", null, "PostfixDoubleClickAction", patchHost: typeof(RobinCrusoePerk), parameterTypes: new System.Type[2]
			{
				typeof(GameItem),
				typeof(Vector2)
			});
			// 09-21 蛙娘：双击实体开面板（全局，不依赖鲁滨逊特性——独立 Postfix，多 Postfix 共存）
			ManualPatcher.TryPatch(typeof(ItemMouseDoubleClickHandler), "DoubleClickAction", null, "PostfixDoubleClickAction", patchHost: typeof(WageGirlSystem), parameterTypes: new System.Type[2]
			{
				typeof(GameItem),
				typeof(Vector2)
			});
			// 09-21 蛙娘阶段 2：拖放喂食/喝水/照顾（照命运骰子拖放吸收链；多 Prefix 共存——只认蛙娘目标）
			ManualPatcher.TryPatch(typeof(GameItem), "MayTarget", "PrefixMayTarget", null, null, typeof(WageGirlSystem));
			ManualPatcher.TryPatch(typeof(GameItem), "CanTarget", "PrefixCanTarget", null, null, typeof(WageGirlSystem));
			ManualPatcher.TryPatch(typeof(GameItem), "Target", "PrefixTarget", null, null, typeof(WageGirlSystem));
			// C 卖血（09-17）：双击采血包 → 抽血 Prefix（先于原生双击）
			ManualPatcher.TryPatch(typeof(StoreClient), "CanClientExposeAnyFeature", "PrefixCanClientExposeAnyFeature", null, null, typeof(RobinCrusoePerk));
			ManualPatcher.TryPatch(typeof(HusbandryHelper), "CreateItemTooltip", null, "PostfixCreateItemTooltip", null, typeof(RobinCrusoePerk));
			ManualPatcher.TryPatchByName(typeof(FoodItemHelper), "CreateFoodItemTooltip", "PrefixFoodTooltip", "PostfixFoodTooltip", typeof(RobinCrusoePerk));
			ManualPatcher.TryPatch(typeof(HusbandryHelper), "CreateItemTooltip", null, "PostfixWageBoxTooltip", null, typeof(ContainerUpgradeV2));
			ManualPatcher.TryPatch(typeof(HealthData), "ReceiveMinorWound", "PrefixReceiveWound", null, null, typeof(RobinCrusoePerk));
			ManualPatcher.TryPatch(typeof(HealthData), "ReceiveMajorWound", "PrefixReceiveWound", null, null, typeof(RobinCrusoePerk));
			// C 卖血（09-17）：受伤扣血 Postfix（轻伤 -200 / 重伤 -500）
			ManualPatcher.TryPatch(typeof(HealthData), "ReceiveMinorWound", null, "PostfixReceiveMinorWound", null, typeof(RobinCrusoePerk));
			ManualPatcher.TryPatch(typeof(HealthData), "ReceiveMajorWound", null, "PostfixReceiveMajorWound", null, typeof(RobinCrusoePerk));
			ManualPatcher.TryPatch(typeof(ScavHelper), "CanScavenge", null, "PostfixCanScavenge", null, typeof(RobinCrusoePerk));
			ManualPatcher.TryPatch(typeof(ScavHelper), "GetMaxScavAttempts", null, "PostfixGetMaxScavAttempts", null, typeof(RobinCrusoePerk));
			ManualPatcher.TryPatch(typeof(ScavHelper), "GetScavTimeLeft", null, "PostfixGetScavTimeLeft", null, typeof(RobinCrusoePerk));
			ManualPatcher.TryPatch(typeof(ScavHelper), "GetRandomScavengedItem", null, "PostfixGetRandomScavengedItem", null, typeof(RobinCrusoePerk));
			ManualPatcher.TryPatch(typeof(MachineryHelper), "GetCurrentPerformanceBonus", null, "PostfixGetCurrentPerformanceBonus", null, typeof(RobinCrusoePerk));
			ManualPatcher.TryPatch(typeof(MachineryHelper), "GetCurrentQualityBonus", null, "PostfixGetCurrentQualityBonus", null, typeof(RobinCrusoePerk));
			ManualPatcher.TryPatch(typeof(ModuleHelper), "ApplyBasicModuleEffect", null, "PostfixApplyBasicModuleEffect", new System.Type[3]
			{
				typeof(GameInventory),
				typeof(GameItem),
				typeof(GameItem)
			}, typeof(RobinCrusoePerk));
			ManualPatcher.TryPatch(typeof(ModuleEffectHelper), "ModifyTempStatFromBaseByPercentage", null, "PostfixModifyTempStatFromBaseByPercentage", new System.Type[2]
			{
				typeof(GameItem),
				typeof(int)
			}, typeof(RobinCrusoePerk));
			ManualPatcher.TryPatch(typeof(ModuleHelper), "AddModuleStatLine", null, "PostfixAddModuleStatLine", new System.Type[4]
			{
				typeof(RichTextBuilder),
				typeof(string),
				typeof(int),
				typeof(int)
			}, typeof(RobinCrusoePerk));
			ManualPatcher.TryPatch(typeof(MachineryHelper), "CreateMachineryTooltip", null, "PostfixCreateMachineryTooltip", new System.Type[2]
			{
				typeof(RichTextBuilder),
				typeof(GameItem)
			}, typeof(RobinCrusoePerk));
			ManualPatcher.TryPatch(typeof(ModuleHelper), "ApplyPerformanceWaterRecyclerEffect", null, "PostfixApplyPerformanceWaterRecyclerEffect", null, typeof(RobinCrusoePerk));
			ManualPatcher.TryPatch(typeof(MachineMoistureFarm), "GetOutputVolume", null, "PostfixGetOutputVolume", null, typeof(RobinCrusoePerk));
			ManualPatcher.TryPatch(typeof(WaterHelper), "RemoveContaminantFromContainer", null, "PostfixRemoveContaminantFromContainer", null, typeof(RobinCrusoePerk));
			ManualPatcher.TryPatch(typeof(GameItem), "MayTarget", "PrefixMayTarget", null, null, typeof(RobinCrusoePerk));
			ManualPatcher.TryPatch(typeof(GameItem), "CanTarget", "PrefixCanTarget", null, null, typeof(RobinCrusoePerk));
			ManualPatcher.TryPatch(typeof(GameItem), "Target", "PrefixTarget", null, null, typeof(RobinCrusoePerk));
			ManualPatcher.TryPatch(typeof(GameItem), "MayHaveValidInventorySlot", "PrefixMayHaveValidInventorySlot", null, null, typeof(RobinCrusoePerk));
			ManualPatcher.TryPatch(typeof(GameItem), "MayTarget", "PrefixMayTarget_WageBox", null, null, typeof(ContainerUpgradeV2));
			ManualPatcher.TryPatch(typeof(GameItem), "CanTarget", "PrefixCanTarget_WageBox", null, null, typeof(ContainerUpgradeV2));
			ManualPatcher.TryPatch(typeof(GameItem), "Target", "PrefixTarget_WageBox", null, null, typeof(ContainerUpgradeV2));
			ManualPatcher.TryPatch(typeof(GameItem), "MayHaveValidInventorySlot", "PrefixMayHaveValidInventorySlot_WageBox", null, null, typeof(ContainerUpgradeV2));
			ManualPatcher.TryPatch(typeof(GameItem), "MayTarget", "PrefixMayTarget", null, null, typeof(LuckScoutBackpackUpgrade));
			ManualPatcher.TryPatch(typeof(GameItem), "CanTarget", "PrefixCanTarget", null, null, typeof(LuckScoutBackpackUpgrade));
			ManualPatcher.TryPatch(typeof(GameItem), "Target", "PrefixTarget", null, null, typeof(LuckScoutBackpackUpgrade));
			ManualPatcher.TryPatch(typeof(PlayerStore), "StartNewGame", null, "PostfixStartNewGame", null, typeof(WandererPerk));
			ManualPatcher.TryPatch(typeof(PlayerStore), "StartNewGame", null, "PostfixStartNewGame", null, typeof(InfamousPerk)); // 声名狼藉
			ManualPatcher.TryPatch(typeof(PlayerStore), "StartNewGame", null, "PostfixStartNewGame", null, typeof(RobinCrusoePerk)); // 09-21 发放后清+重发（根治"清了白清"）
			ManualPatcher.TryPatch(typeof(PlayerStore), "StartNewGame", null, "PostfixStartNewGame", null, typeof(DrJacksonFriendPerk)); // 阶段2 CR-15：基类 OnNewGame 挂的 GameMaster.NewGame 实测从不触发，改挂此处重置来访日
			ManualPatcher.TryPatch(typeof(PlayerStore), "LoadGame", null, "PostfixLoadGame_IngotContainer", null, typeof(RobinCrusoePerk));
			ManualPatcher.TryPatch(typeof(LiquidContainerHelper), "AutoSipFromContainer", "PrefixAutoSipFromContainer", null, null, typeof(RobinCrusoePerk));
			ManualPatcher.TryPatch(typeof(StoreClientList), "PlaceSupplierInventory", null, "PostfixPlaceSupplierInventory", null, typeof(RobinCrusoePerk));
			ManualPatcher.TryPatch(typeof(EmporiumEntry), "Start", null, "PostfixEmporiumEntryStart", null, typeof(RobinCrusoePerk));
			ManualPatcher.TryPatchByName(typeof(ContainerHelper), "InitContainerItem", null, "PostfixInitContainerItem", typeof(RobinCrusoePerk));
			ManualPatcher.TryPatch(typeof(MachineryHelper), "GetMachinePowerUsage", null, "PostfixGetMachinePowerUsage", null, typeof(RobinCrusoePerk));
			ManualPatcher.TryPatch(typeof(PlayerStore), "HandleInsurance", "PrefixHandleInsurance", null, null, typeof(RobinCrusoePerk));
			ManualPatcher.TryPatch(typeof(ScavHelper), "ScavengeDumpingGrounds", null, "PostfixScavengeDumpingGrounds", null, typeof(RobinCrusoePerk));
			ManualPatcher.TryPatch(typeof(StoreClientManager), "CheckRentDay", "PrefixCheckRentDay", null, null, typeof(RobinCrusoePerk));
			ManualPatcher.TryPatch(typeof(StoreClientUniqueList.__c), "_LandlordWholesale_b__22_0", "PrefixLandlordWholesaleStock", null, null, typeof(RobinCrusoePerk));
			ManualPatcher.TryPatch(typeof(StoreClientManager), "HandleSupplierClient", null, "PostfixHandleSupplierClient", null, typeof(RobinCrusoePerk));
			ManualPatcher.TryPatch(typeof(PlayerStore), "ExecuteGameOver", "PrefixExecuteGameOver", null, null, typeof(RobinCrusoePerk));
			ManualPatcher.TryPatch(typeof(StoreClientManager), "HandleNormalClient", null, "PostfixHandleNormalClient", null, typeof(RobinCrusoePerk));
			ManualPatcher.TryPatch(typeof(StorePhoneClient), "InitPhoneClientDict", null, "PostfixInitPhoneClientDict", null, typeof(RobinCrusoePerk));
			ManualPatcher.TryPatch(typeof(PhoneUIManager), "WillAnswerCall", "PrefixWillAnswerCall", null, null, typeof(RobinCrusoePerk));
			ManualPatcher.TryPatch(typeof(PhoneUIManager), "WillAnswerCall", null, "PostfixWillAnswerCall", null, typeof(RobinCrusoePerk));
			ManualPatcher.TryPatch(typeof(PhoneUIManager), "StartPhoneDialog", "PrefixStartPhoneDialog", null, null, typeof(RobinCrusoePerk));
			ManualPatcher.TryPatch(typeof(ContactElement), "OnInit", null, "PostfixOnContactInit", null, typeof(RobinCrusoePerk));
			ManualPatcher.TryPatch(typeof(StoreClientListWanted), "CreateWanted6", null, "PostfixCreateWanted6", null, typeof(RobinCrusoePerk));
			ManualPatcher.TryPatchByName(typeof(GunHelper), "InitGun", null, "PostfixInitGun", typeof(RobinCrusoePerk));
			ManualPatcher.TryPatch(typeof(DirectoryMaster), "Item", "PrefixDirectoryMasterItem", null, null, typeof(RobinCrusoePerk));
			ManualPatcher.TryPatch(typeof(WantedElement), "OnArrested", null, "PostfixWantedElementOnArrested", null, typeof(RobinCrusoePerk));
			ManualPatcher.TryPatch(typeof(StoreUIManager), "OnGenericArrived", "PrefixStoreUIManagerOnGenericArrived", null, null, typeof(RobinCrusoePerk));
			ManualPatcher.TryPatch(typeof(AugHelper), "CleanupKill", null, "PostfixAugHelperCleanupKill", null, typeof(RobinCrusoePerk));
			ManualPatcher.TryPatch(typeof(AdvCalendarUIManager), "OnCalendarButtonClicked", null, "PostfixOnCalendarButtonClicked", null, typeof(RobinCrusoePerk));
			ManualPatcher.TryPatch(typeof(StoreCalendar), "Update", null, "PostfixStoreCalendarUpdate", null, typeof(RobinCrusoePerk));
			ManualPatcher.TryPatch(typeof(StartOfDayUIManager), "InitPanel", null, "PostfixStartOfDayInitPanel", null, typeof(RobinCrusoePerk));
			ManualPatcher.TryPatch(typeof(StoreClient), "ApplyBudgetModifier", null, "PostfixStoreClientApplyBudgetModifier", null, typeof(Patches));
			ManualPatcher.TryPatch(typeof(StoreClient), "ApplyBudgetModifier", null, "PostfixApplyBudgetModifier", null, typeof(WageGirlSystem)); // 09-21 蛙娘在场：客户预算 x4（+300%）
			ManualPatcher.TryPatch(typeof(BargainUIManager), "GetDealMakerBonus", null, "PostfixGetDealMakerBonus", null, typeof(WageGirlSystem)); // 09-21 蛙娘在场：议价 +50（GetDealMakerBonus=显示+实际判定共用，拆包二次实锤）
			ManualPatcher.TryPatch(typeof(GameItemElement), "ApplyAnimationFrame", "PrefixApplyAnimationFrame", null, null, typeof(WageGirlSystem)); // 09-22 蛙娘动画帧（GoFishing CustomItemAnimationPatch 先例模式）
			ManualPatcher.TryPatch(typeof(GameItemElement), "ResolveSpriteByName", "PostfixResolveSpriteByName", null, null, typeof(WageGirlSystem)); // 拆包实锤：拦截sprite解析入口,蛙娘永远给mod图标
			// 09-21 信誉扣减减半（用户拍板：减信誉少50%）——实例版 ModReputation(double)，议价 5 处入口
			ManualPatcher.TryPatch(typeof(StoreReputation), "ModReputation", "PrefixModReputation", null, new System.Type[1] { typeof(double) }, typeof(Patches));
				// 09-22 制卡降上城区声望：mod 违禁品跳过客户曝光链（ClientExposeFeature，曝光=扣声望-4+划词条+对话）
				ManualPatcher.TryPatchByName(typeof(StoreClient), "ClientExposeFeature", "PrefixClientExposeFeature", null, typeof(Patches));
				// 诊断（用完删）：static ModReputation(String,int,bool) 日志——确认曝光扣声望走 static 版（-4）
				ManualPatcher.TryPatch(typeof(StoreReputation), "ModReputation", "PrefixModReputationStatic", null, new System.Type[3] { typeof(string), typeof(int), typeof(bool) }, typeof(Patches));
			
			
			
			ManualPatcher.TryPatch(typeof(StoreClientManager), "PickClient", null, "PostfixStoreClientManagerPickClient", null, typeof(Patches));
			ManualPatcher.TryPatch(typeof(BargainUIManager), "OfferBuyingMarkup", "PrefixBargainUIManagerOfferBuyingMarkup", null, null, typeof(Patches));
			ManualPatcher.TryPatch(typeof(BargainUIManager), "GetDealMakerBonus", null, "PostfixGetDealMakerBonus", null, typeof(Patches));
			ManualPatcher.TryPatch(typeof(ItemFeatureList), "BargainBuyingMarkup", null, "PostfixItemFeatureListBargainBuyingMarkup", null, typeof(Patches));
			ManualPatcher.TryPatch(typeof(StoreClient), "OnDealAccepted", null, "PostfixStoreClientOnDealAccepted", null, typeof(Patches));
			ManualPatcher.TryPatch(typeof(StoreClientListTier), "CreateThirstySpacer", null, "PostfixCreateThirstySpacer", null, typeof(RobinCrusoePerk));
			ManualPatcher.TryPatch(typeof(StoreClientListTier), "CreateHungrySpacer", null, "PostfixCreateHungrySpacer", null, typeof(RobinCrusoePerk));
			ManualPatcher.TryPatch(typeof(StoreClientListTier), "CreateSpacerChef", null, "PostfixCreateSpacerChef", null, typeof(RobinCrusoePerk));
			ManualPatcher.TryPatch(typeof(StoreClientListTier), "CreateInjuredSpacer", null, "PostfixCreateInjuredSpacer", null, typeof(RobinCrusoePerk));
			ManualPatcher.TryPatch(typeof(StoreClientListTier), "CreateSickChildCaretaker", null, "PostfixCreateSickChildCaretaker", null, typeof(RobinCrusoePerk));
			ManualPatcher.TryPatch(typeof(StoreClientListTierSubstance), "CreateDesperateAddict", null, "PostfixCreateDesperateAddict", null, typeof(RobinCrusoePerk));
			ManualPatcher.TryPatch(typeof(StoreClientListTierSubstance), "CreateWornOutSpacer", null, "PostfixCreateWornOutSpacer", null, typeof(RobinCrusoePerk));
			ManualPatcher.TryPatch(typeof(StoreClientListMinor), "CreateSickLowers", null, "PostfixCreateSickLowers", null, typeof(RobinCrusoePerk));
		}
		catch (System.Exception ex3)
		{
			Core.LogMsg("[Patch] 应用补丁失败: " + ex3.Message);
		}
		// 阶段3（2026-09-23）：补丁挂载自检——汇总成功/失败数，失败项即"功能不会生效"的清单。
		// 放在 try/catch 之后，保证即使中途抛异常也能输出已挂载情况。
		// 说明：**不引入任何冲突检测/让路逻辑** —— 拦截其他 mod 等于同时废掉我们自己的补丁（历史事故）。
		// 阶段3：冲突防护——列出已加载的已知冲突 mod
		ModCompat.LogLoadedConflicts();
		ManualPatcher.LogPatchSummary();
	}
}
