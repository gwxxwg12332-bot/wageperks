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

[assembly: MelonInfo(typeof(Core), "Wage's Perks", "1.2.2", "gwxxwg12332", null)]
[assembly: MelonGame("Questing Goose Studio", "Probably Stolen")]

namespace JacksonPerks;

public static class BuildConfig
{
	private static bool? _hardMode = null;

	public static int[] BoxWidthsArr = new int[6] { 3, 10, 20, 32, 42, 52 };

	public static int[] BoxHeightsArr = new int[6] { 3, 10, 10, 10, 10, 10 };

	public static int[] UpgradeCostsArr = new int[5] { 5, 10, 20, 40, 80 };

	public static bool HardMode
	{
		get
		{
			if (!_hardMode.HasValue)
			{
				try
				{
					_hardMode = MelonPreferences.GetEntryValue<bool>("WagesPerks", "HardMode");
				}
				catch
				{
					_hardMode = false;
				}
			}
			return _hardMode.Value;
		}
	}

	public static int CleanDailyLoss => GetInt("CleanDailyLoss", 2);

	public static int CleanScavCost => GetInt("CleanScavCost", 2);

	public static int CleanToothpaste => GetInt("CleanToothpaste", 15);

	public static int CleanToiletPaper => GetInt("CleanToiletPaper", 30);

	public static int CleanShampoo => GetInt("CleanShampoo", 45);

	public static int CleanPaperTowel => GetInt("CleanPaperTowel", 45);

	public static int BeverageSatiety => GetInt("BeverageSatiety", 10);

	public static int BeverageThirst => GetInt("BeverageThirst", 15);

	public static int SnackMood => GetInt("SnackMood", 10);

	public static int AlcoholMood => GetInt("AlcoholMood", 15);

	public static int NarcoticMood => GetInt("NarcoticMood", 20);

	public static int DailySatLoss => GetInt("DailySatLoss", 20);

	public static int DailyThirstLoss => GetInt("DailyThirstLoss", 25);

	public static int DailyHealthGain => GetInt("DailyHealthGain", 10);

	public static int DailySleepGain => GetInt("DailySleepGain", 30);

	public static int SleepScavLoss => GetInt("SleepScavLoss", 7);

	public static int DailySocialGain => GetInt("DailySocialGain", 5);

	public static int DailySocialLoss => GetInt("DailySocialLoss", 5);

	public static int MoodUp => GetInt("MoodUp", 5);

	public static int MoodDown => GetInt("MoodDown", 10);

	public static int MoodStart => GetInt("MoodStart", 60);

	public static int SipMl => GetInt("SipMl", 200);

	public static int CleanStart => GetInt("CleanStart", 100);

	public static int SleepStart => GetInt("SleepStart", 100);

	public static int SocialStart => GetInt("SocialStart", 50);

	public static int GranaryDays => GetInt("GranaryDays", 7);

	public static int ElevEvery => GetInt("ElevEvery", 2);

	public static int ElevMax => GetInt("ElevMax", 5);

	public static int ButcherVisitDay => GetInt("ButcherVisitDay", 13);

	public static int LiBeiwenVisitDay => GetInt("LiBeiwenVisitDay", 20);

	public static int CallArriveDays => GetInt("CallArriveDays", 2);

	public static int CallCooldownDays => GetInt("CallCooldownDays", 3);

	public static int BaseChance => GetInt("BaseChance", 0);

	public static int UpgradeChance => GetInt("UpgradeChance", 2);

	public static int LevelupEvery => GetInt("LevelupEvery", 15);

	public static int LuckMaxChance => GetInt("LuckMaxChance", 25);

	public static int LuckMaxChanceHard => GetInt("LuckMaxChanceHard", 50);

	public static int CannibalInterval => GetInt("CannibalInterval", 10);

	public static int CannibalAbsorbPct => GetInt("CannibalAbsorbPct", 10);

	public static int CannibalCap => GetInt("CannibalCap", 150);

	public static int BatteryInterval => GetInt("BatteryInterval", 8);

	public static int InspectInterval => GetInt("InspectInterval", 15);

	public static int GuMachinePrice => GetInt("GuMachinePrice", 3000);

	public static int GuChargeDays => GetInt("GuChargeDays", 3);

	public static int GuForgeMult => GetInt("GuForgeMult", 120);

	public static int GuForgeCap => GetInt("GuForgeCap", 150);

	public static int AiGenPrice => GetInt("AiGenPrice", 5000);

	public static int AiSuccessPct => GetInt("AiSuccessPct", 50);

	public static int AiStableCap => GetInt("AiStableCap", 75);

	public static int AiUnstableCap => GetInt("AiUnstableCap", 150);

	public static int ProtectorPrice => GetInt("ProtectorPrice", 1500);

	public static int DoctorBuybackPct => GetInt("DoctorBuybackPct", 80);

	public static int DoctorSupplyPricePct => GetInt("DoctorSupplyPricePct", 60);

	public static int DoctorSupplyCountMin => GetInt("DoctorSupplyCountMin", 3);

	public static int DoctorSupplyCountMax => GetInt("DoctorSupplyCountMax", 5);

	public static int ProtectorSupplyCount => GetInt("ProtectorSupplyCount", 3);

	public static int DoctorVisitInterval => GetInt("DoctorVisitInterval", 7);

	public static int DoctorExtraItems => GetInt("DoctorExtraItems", 8);

	public static int NeuralChanceHard => GetInt("NeuralChanceHard", 50);

	public static int NeuralChanceNormal => GetInt("NeuralChanceNormal", 3);

	public static int NeuralRollChance => GetInt("NeuralRollChance", 2);

	public static int DiceTriggerValue => GetInt("DiceTriggerValue", 400);

	public static int DiceUninstallRefund => GetInt("DiceUninstallRefund", 50);

	public static int AlcoholVisitInterval => GetInt("AlcoholVisitInterval", 7);

	public static int WaterVisitInterval => GetInt("WaterVisitInterval", 7);

	public static int GunsmithVisitInterval => GetInt("GunsmithVisitInterval", 7);

	public static int ContainerMaxStage => GetInt("ContainerMaxStage", 5);

	public static void InitPrefs()
	{
		try
		{
			MelonPreferences_Category melonPreferences_Category = MelonPreferences.CreateCategory("WagesPerks", "Wage's Perks");
			melonPreferences_Category.CreateEntry("HardMode", default_value: false, "硬爽模式：稀有率上限50% / 拾荒+10 / 神经模组进均匀池 / 博士夜卖受限模组 / 开局精选好货");
			melonPreferences_Category.CreateEntry("CleanDailyLoss", 2, "清洁每日衰减量");
			melonPreferences_Category.CreateEntry("CleanScavCost", 2, "拾荒清洁消耗");
			melonPreferences_Category.CreateEntry("CleanToothpaste", 15, "牙膏恢复清洁");
			melonPreferences_Category.CreateEntry("CleanToiletPaper", 30, "厕纸恢复清洁");
			melonPreferences_Category.CreateEntry("CleanShampoo", 45, "洗涤剂恢复清洁");
			melonPreferences_Category.CreateEntry("CleanPaperTowel", 45, "纸巾恢复清洁");
			melonPreferences_Category.CreateEntry("BeverageSatiety", 10, "饮品饱食恢复");
			melonPreferences_Category.CreateEntry("BeverageThirst", 15, "饮品口渴恢复");
			melonPreferences_Category.CreateEntry("SnackMood", 10, "零食心情恢复");
			melonPreferences_Category.CreateEntry("AlcoholMood", 15, "酒类心情恢复");
			melonPreferences_Category.CreateEntry("NarcoticMood", 20, "麻醉品心情恢复");
			melonPreferences_Category.CreateEntry("DailySatLoss", 20, "饱食每日衰减");
			melonPreferences_Category.CreateEntry("DailyThirstLoss", 25, "口渴每日衰减");
			melonPreferences_Category.CreateEntry("DailyHealthGain", 10, "健康每日恢复");
			melonPreferences_Category.CreateEntry("DailySleepGain", 30, "打烊睡眠恢复");
			melonPreferences_Category.CreateEntry("SleepScavLoss", 7, "拾荒睡眠消耗");
			melonPreferences_Category.CreateEntry("DailySocialGain", 5, "社交每日+（接待）");
			melonPreferences_Category.CreateEntry("DailySocialLoss", 5, "社交每日-（独处）");
			melonPreferences_Category.CreateEntry("MoodUp", 5, "三项全好每日心情+");
			melonPreferences_Category.CreateEntry("MoodDown", 10, "任一项低每日心情-");
			melonPreferences_Category.CreateEntry("MoodStart", 60, "心情初始值");
			melonPreferences_Category.CreateEntry("SipMl", 200, "一口喝水量(ml)");
			melonPreferences_Category.CreateEntry("CleanStart", 100, "清洁初始值");
			melonPreferences_Category.CreateEntry("SleepStart", 100, "睡眠初始值");
			melonPreferences_Category.CreateEntry("SocialStart", 50, "社交初始值");
			melonPreferences_Category.CreateEntry("GranaryDays", 7, "粮仓连续天数门槛");
			melonPreferences_Category.CreateEntry("ElevEvery", 2, "昂扬结算间隔(天)");
			melonPreferences_Category.CreateEntry("ElevMax", 5, "昂扬累计封顶");
			melonPreferences_Category.CreateEntry("ButcherVisitDay", 13, "胡安首次到店天(0-based)");
			melonPreferences_Category.CreateEntry("LiBeiwenVisitDay", 20, "李北文首次到店天(0-based)");
			melonPreferences_Category.CreateEntry("CallArriveDays", 2, "电话叫货到店天数");
			melonPreferences_Category.CreateEntry("CallCooldownDays", 3, "电话冷却天数");
			melonPreferences_Category.CreateEntry("BaseChance", 0, "稀有率初始(%)");
			melonPreferences_Category.CreateEntry("UpgradeChance", 2, "每级稀有率+(%)");
			melonPreferences_Category.CreateEntry("LevelupEvery", 15, "每N次拾荒升1级");
			melonPreferences_Category.CreateEntry("LuckMaxChance", 25, "稀有物发现几率上限(%)——标准版");
			melonPreferences_Category.CreateEntry("LuckMaxChanceHard", 50, "稀有物发现几率上限(%)——硬爽版");
			melonPreferences_Category.CreateEntry("DoctorVisitInterval", 7, "博士来访间隔(天)");
			melonPreferences_Category.CreateEntry("DoctorExtraItems", 8, "博士夜店加货数量");
			melonPreferences_Category.CreateEntry("NeuralChanceHard", 50, "硬爽博士夜受限模组概率(%)");
			melonPreferences_Category.CreateEntry("NeuralChanceNormal", 3, "普通博士夜受限模组概率(%)");
			melonPreferences_Category.CreateEntry("NeuralRollChance", 2, "普通拾荒独立roll受限模组(%)");
			melonPreferences_Category.CreateEntry("DiceTriggerValue", 400, "骰子触发累计值");
			melonPreferences_Category.CreateEntry("DiceUninstallRefund", 50, "卸载返还(%)");
			melonPreferences_Category.CreateEntry("AlcoholVisitInterval", 7, "酒商来访间隔(天)");
			melonPreferences_Category.CreateEntry("WaterVisitInterval", 7, "水商来访间隔(天)");
			melonPreferences_Category.CreateEntry("GunsmithVisitInterval", 7, "枪匠来访间隔(天)");
			melonPreferences_Category.CreateEntry("ContainerMaxStage", 5, "蛙哥箱段位上限");
			melonPreferences_Category.CreateEntry("BoxWidths", "3,10,20,32,42,52", "蛙哥箱每段宽度(逗号分隔)");
			melonPreferences_Category.CreateEntry("BoxHeights", "3,10,10,10,10,10", "蛙哥箱每段高度(逗号分隔)");
			melonPreferences_Category.CreateEntry("UpgradeCosts", "5,10,20,40,80", "蛙哥箱每级升级材料数(逗号分隔)");
			melonPreferences_Category.CreateEntry("CannibalInterval", 10, "吞噬季间隔(天)");
			melonPreferences_Category.CreateEntry("CannibalAbsorbPct", 10, "吞噬吸收(%)");
			melonPreferences_Category.CreateEntry("CannibalCap", 150, "吞噬三维属性上限");
			melonPreferences_Category.CreateEntry("BatteryInterval", 8, "电池吞噬季间隔(天)");
			melonPreferences_Category.CreateEntry("InspectInterval", 15, "治安部眼线检查间隔(天)");
			melonPreferences_Category.CreateEntry("GuMachinePrice", 3000, "养蛊机售价");
			melonPreferences_Category.CreateEntry("GuChargeDays", 3, "养蛊机充能天数");
			melonPreferences_Category.CreateEntry("GuForgeMult", 120, "炼蛊倍率(% 120=×1.2)");
			melonPreferences_Category.CreateEntry("GuForgeCap", 150, "炼蛊属性上限");
			melonPreferences_Category.CreateEntry("AiGenPrice", 5000, "AI生成器售价");
			melonPreferences_Category.CreateEntry("AiSuccessPct", 50, "AI抽卡成功率(%)");
			melonPreferences_Category.CreateEntry("AiStableCap", 75, "阉割版属性上限");
			melonPreferences_Category.CreateEntry("AiUnstableCap", 150, "不稳定版属性上限");
			melonPreferences_Category.CreateEntry("ProtectorPrice", 1500, "保护器核心售价");
			melonPreferences_Category.CreateEntry("DoctorBuybackPct", 80, "博士回收模组价(%)");
			melonPreferences_Category.CreateEntry("DoctorSupplyPricePct", 60, "博士廉价模组售价(%)");
			melonPreferences_Category.CreateEntry("DoctorSupplyCountMin", 3, "博士廉价模组数量下限");
			melonPreferences_Category.CreateEntry("DoctorSupplyCountMax", 5, "博士廉价模组数量上限");
			melonPreferences_Category.CreateEntry("ProtectorSupplyCount", 3, "博士保护器补货数量");
			try
			{
				BoxWidthsArr = PadToLast(ParseIntList(GetStr("BoxWidths", "3,10,20,32,42,52")), System.Math.Max(6, ContainerMaxStage + 1));
				BoxHeightsArr = PadToLast(ParseIntList(GetStr("BoxHeights", "3,10,10,10,10,10")), System.Math.Max(6, ContainerMaxStage + 1));
				UpgradeCostsArr = PadToLast(ParseIntList(GetStr("UpgradeCosts", "5,10,20,40,80")), System.Math.Max(5, ContainerMaxStage));
			}
			catch (System.Exception ex)
			{
				Core.LogMsg("[WagePerks] 容器表解析异常: " + ex.Message);
			}
			_hardMode = null;
		}
		catch (System.Exception ex2)
		{
			Core.LogMsg("[WagePerks] BuildConfig.InitPrefs 异常: " + ex2.Message);
		}
	}

	private static string GetStr(string key, string def)
	{
		try
		{
			return MelonPreferences.GetEntryValue<string>("WagesPerks", key);
		}
		catch
		{
			return def;
		}
	}

	private static int[] ParseIntList(string s)
	{
		System.Collections.Generic.List<int> list = new System.Collections.Generic.List<int>();
		try
		{
			string[] array = s.Split(',');
			for (int i = 0; i < array.Length; i++)
			{
				if (int.TryParse(array[i].Trim(), out var result))
				{
					list.Add(result);
				}
			}
		}
		catch
		{
		}
		return list.ToArray();
	}

	private static int[] PadToLast(int[] arr, int minLen)
	{
		if (arr.Length >= minLen)
		{
			return arr;
		}
		System.Collections.Generic.List<int> list = new System.Collections.Generic.List<int>(arr);
		int item = ((arr.Length != 0) ? arr[^1] : 0);
		while (list.Count < minLen)
		{
			list.Add(item);
		}
		return list.ToArray();
	}

	private static int GetInt(string key, int def)
	{
		try
		{
			return MelonPreferences.GetEntryValue<int>("WagesPerks", key);
		}
		catch
		{
			return def;
		}
	}
}
public class Core : MelonMod
{
	public static readonly System.Collections.Generic.List<string> NightReportQueue = new System.Collections.Generic.List<string>();

	public static bool DebugMode = true; // 开发状态默认开（用户规范：电脑里永远开发状态；仅用户说发布时改 false）

	public static readonly System.Random Rng = new System.Random();

	public static MelonLogger.Instance Log { get; private set; }

	public static event System.Action<string> DebugOutput;

	public static void AddNightReportLine(string line)
	{
		if (string.IsNullOrEmpty(line))
		{
			return;
		}
		try
		{
			NightReportQueue.Add(line);
		}
		catch
		{
		}
	}

	public override void OnInitializeMelon()
	{
		BuildConfig.InitPrefs();
		Log = base.LoggerInstance;
		Log.Msg("Wage's Perks v1.2.2 已加载 - 手动Patch模式");
		Log.Msg("【深空当铺】Wage's Perks QQ群：1109707341");
		ManualPatcher.Init(base.HarmonyInstance);
		try
		{
			ModCompat.LogLoadedConflicts();
		}
		catch
		{
		}
		try
		{
			DrJacksonFriendPerk.RegisterInventorStockHook();
		}
		catch (System.Exception ex)
		{
			Log.Msg("[博士之友] 注册失败 " + ex.Message);
		}
		try
		{
			ManualPatcher.TryPatchAllOverloads(typeof(PlayerStore), "AddDirectSellingItemToTable", null, "PostfixAddDirectSellingItemToTable");
		}
		catch
		{
		}
		try
		{
			ManualPatcher.TryPatch(typeof(StoreClientList), "PlaceInventorInventory", null, "PostfixPlaceInventorInventory", new System.Type[1] { typeof(bool) });
		}
		catch
		{
		}
		ApplyAllPatches();
		try
		{
			Log.Msg("[Patch] 蛙哥牛逼特性补丁应用成功（AddClient/DismissClient/NewDay）");
		}
		catch (System.Exception ex2)
		{
			Log.Msg("[Patch] 蛙哥牛逼特性补丁应用失败: " + ex2.Message);
		}
		try
		{
			if (StartingPerkList.Perks != null)
			{
				CustomStartingPerks.EnsureRegistered();
			}
		}
		catch
		{
		}
		PerkIconLoader.Initialize();
		Diagnostics.Register();
	}

	public override void OnUpdate()
	{
	}

	public override void OnGUI()
	{
		try
		{
			Diagnostics.OnGUI();
		}
		catch
		{
		}
		try
		{
			DestinyDice.DiceOnGUI();
		}
		catch
		{
		}
	}

	private static void SubscribeModHooks()
	{
		try
		{
			ModHook.add_OnGameLoadedNormal(DelegateSupport.ConvertDelegate<Il2CppSystem.Action>(new System.Action(OnModGameLoaded)));
			ModHook.add_OnShutterOpenedEarly(DelegateSupport.ConvertDelegate<Il2CppSystem.Action>(new System.Action(OnModShutterOpened)));
			try
			{
				DrJacksonFriendPerk.RegisterInventorStockHook();
			}
			catch
			{
			}
			LogMsg("[ModHook] 已订阅 OnGameLoadedNormal + OnShutterOpenedEarly（博士调度）");
		}
		catch (System.Exception ex)
		{
			LogMsg("[ModHook] 订阅失败: " + ex.Message);
		}
	}

	private static void OnModGameLoaded()
	{
		try
		{
			try
			{
				Patches.ForceInspectionToday();
			}
			catch (System.Exception ex)
			{
				LogMsg("[ModHook] OnGameLoaded强制检查失败: " + ex.Message);
			}
			try
			{
				Patches.ApplyBadLuck();
			}
			catch (System.Exception ex2)
			{
				LogMsg("[ModHook] OnGameLoaded霉运失败: " + ex2.Message);
			}
			if (DrJacksonFriendPerk.IsActive() && PlayerStore.Instance != null)
			{
				Patches.ScheduleJacksonToday();
			}
		}
		catch (System.Exception ex3)
		{
			LogMsg("[ModHook] OnGameLoaded安排博士失败: " + ex3.Message);
		}
	}

	private static void OnModShutterOpened()
	{
		try
		{
			try
			{
				AddictOfficerEvent.OnNewDay();
			}
			catch (System.Exception ex)
			{
				LogMsg("[AddictOfficer] 调度失败: " + ex.Message);
			}
			try
			{
				Patches.ForceInspectionToday();
			}
			catch (System.Exception ex3)
			{
				LogMsg("[ModHook] 开门强制检查失败: " + ex3.Message);
			}
			try
			{
				Patches.ApplyBadLuck();
			}
			catch (System.Exception ex4)
			{
				LogMsg("[ModHook] 开门霉运失败: " + ex4.Message);
			}
			if (!DrJacksonFriendPerk.IsActive() || PlayerStore.Instance == null)
			{
				return;
			}
			try
			{
				Il2CppSystem.Collections.Generic.List<string> futurStoreClientIdQueue = PlayerStore.Instance.futurStoreClientIdQueue;
				if (futurStoreClientIdQueue != null)
				{
					string.Join(",", futurStoreClientIdQueue.ToArray());
				}
			}
			catch
			{
			}
			Patches.ScheduleJacksonToday();
		}
		catch (System.Exception ex5)
		{
			LogMsg("[ModHook] OnShutterOpened安排博士失败: " + ex5.Message);
		}
	}

	private static void ApplyAllPatches()
	{
		try
		{
			System.Func<StoreClient> @delegate = StoreClientListWanted.CreateWanted7;
			StoreClientListDict.storeClientDict["wanted7"] = DelegateSupport.ConvertDelegate<Il2CppSystem.Func<StoreClient>>(@delegate);
		}
		catch (System.Exception ex)
		{
			LogMsg("[WagePerks] 注入 wanted7 失败: " + ex.Message);
		}
		try
		{
			System.Func<StoreClient> delegate2 = StoreClientListWanted.CreateWanted6;
			StoreClientListDict.storeClientDict["wanted6"] = DelegateSupport.ConvertDelegate<Il2CppSystem.Func<StoreClient>>(delegate2);
		}
		catch (System.Exception ex2)
		{
			LogMsg("[WagePerks] 注入 wanted6 失败: " + ex2.Message);
		}
		try
		{
			ManualPatcher.TryPatch(typeof(StartingPerkList), "InitStartingPerk", null, "PostfixInitStartingPerks");
			ManualPatcher.TryPatch(typeof(GameMaster), "NewGame", null, "PostfixOnNewGame");
			ManualPatcher.TryPatch(typeof(NewGameData), "HandleInitialItem", null, "HandleInitialItemPostfix");
			// 09-21 拆包实锤：四件唯一发放点 = PlayerStore.HandleSkipIntro（EmporiumEntry.Start L7742）→ 流浪者清+发挂此处（清完 InitialSave 不入档）
			ManualPatcher.TryPatch(typeof(PlayerStore), "HandleSkipIntro", null, "PostfixHandleSkipIntro", null, typeof(WandererPerk));
			ManualPatcher.TryPatch(typeof(StoreClientManager), "OnNewDay", null, "PostfixOnNewDay");
			ManualPatcher.TryPatch(typeof(PlayerStore), "BeginDay", null, "PostfixOnBeginDay");
			ManualPatcher.TryPatch(typeof(PlayerStore), "AddDirectSellingItemToTable", "PrefixAddDirectSellingItemToTable", null, null, typeof(WaterMerchantPerk));
			ManualPatcher.TryPatchByName(typeof(MachineBottlePrinter.__c__DisplayClass6_0), "Method_Internal_Void_String_Int32_0", "PrefixTryPrint", "PostfixTryPrint", typeof(WaterMerchantPerk));
			ManualPatcher.TryPatchByName(typeof(MachineFeedDispenser.__c__DisplayClass7_0), "_CreateFeedDispenser_b__3", "PrefixFeedDispenserB3", null, typeof(RobinCrusoePerk));
			ManualPatcher.TryPatch(typeof(PlayerStore), "AddDirectSellingItemToTable", "PrefixAddDirectSellingItemToTable", null, null, typeof(RetiredGunsmithPerk));
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
			ManualPatcher.TryPatchByName(typeof(BargainUIManager), "ComputeTradeRepMultiplier", null, "PostfixTradeRepMultiplier");
			ManualPatcher.TryPatch(typeof(BargainUIManager), "OfferMarkup", "PrefixOfferMarkup", "PostfixOfferMarkup", new System.Type[1] { typeof(int) });
			ManualPatcher.TryPatch(typeof(BargainUIManager), "OfferDiscount", "PrefixOfferDiscount", "PostfixOfferDiscount", new System.Type[1] { typeof(int) });
			ManualPatcher.TryPatch(typeof(BargainUIManager), "RecomputeTradeRepMultiplier", "PrefixRecomputeTradeRepMultiplier", null, new System.Type[0]);
			ManualPatcher.TryPatch(typeof(StoreClient), "ModBudget", "PrefixStoreClientModBudget", null, new System.Type[1] { typeof(int) });
			ManualPatcher.TryPatch(typeof(PlayerStore), "SellItem", null, "PostfixPlayerStoreSellItem", new System.Type[1] { typeof(GameItem) });
			ManualPatcher.TryPatch(typeof(BargainUIManager), "CloseUI", null, "PostfixBargainCloseUI");
			ManualPatcher.TryPatch(typeof(StartOfDayUIManager), "OpenUI", null, "PostfixStartOfDayOpenUI");
			ManualPatcher.TryPatch(typeof(StartOfDayUIManager), "ShowMorningReport", null, "PostfixStartOfDayShowMorningReport");
			ManualPatcher.TryPatch(typeof(StartOfDayUIManager), "OnStartDayButtonClicked", null, "PostfixStartOfDayButtonClicked");
			Diagnostics.ApplyPatches();
			ManualPatcher.TryPatch(typeof(RenderHandler), "LoadFromAtlas", "PrefixLoadFromAtlas");
			ManualPatcher.TryPatch(typeof(RenderHandler), "LoadFromAtlas", "PrefixLoadFromAtlas", null, null, typeof(LuckScoutBackpackUpgrade));
			ManualPatcher.TryPatch(typeof(RenderHandler), "LoadFromAtlas", "PrefixLoadFromAtlas", null, null, typeof(DestinyDice));
			ManualPatcher.TryPatch(typeof(RenderHandler), "LoadFromAtlas", "PrefixLoadFromAtlas", null, null, typeof(GuMachineSystem));
			ManualPatcher.TryPatch(typeof(RenderHandler), "LoadFromAtlas", "PrefixLoadFromAtlas", null, null, typeof(WageGirlSystem)); // 09-21 蛙娘贴图
			ManualPatcher.TryPatch(typeof(StoreEventManager), "OnDayStart", null, "OnDayStartPostfix", null, typeof(ModCannibalism));
			ManualPatcher.TryPatch(typeof(StoreEventManager), "OnDayStart", null, "OnDayStartPostfix", null, typeof(BatteryCannibalism));
			ManualPatcher.TryPatch(typeof(StoreEventManager), "OnDayStart", null, "OnDayStartPostfix", null, typeof(AddictOfficerEvent));
			ManualPatcher.TryPatch(typeof(StoreEventManager), "OnDayStart", null, "OnDayStartPostfix", null, typeof(DarkGridInspectorPerk)); // 09-20 设计稿：眼线独立挂（不依赖 AddictOfficerEvent 链）
			ManualPatcher.TryPatch(typeof(StoreEventManager), "OnDayStart", null, "OnDayStartPostfix", null, typeof(GuMachineSystem));
			ManualPatcher.TryPatch(typeof(StoreEventManager), "OnDayStart", null, "PostfixOnDayStart", null, typeof(WageGirlSystem)); // 09-21 蛙娘：全局常驻——每日六维衰减+首次发放（方法名 PostfixOnDayStart）
			ManualPatcher.TryPatch(typeof(StoreEventManager), "OnDayStart", null, "PostfixStoreEventOnDayStart", null, typeof(DestinyDice));
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
			LogMsg("[Patch] 命运骰子拖放吸收已注册");
			ManualPatcher.TryPatch(typeof(ContainerItemDirectory), "InitDirectory", null, "PostfixInitDirectory");
			ManualPatcher.TryPatch(typeof(AmenitiesItemDirectory), "InitDirectory", null, "PostfixInitDirectory");
			ManualPatcher.TryPatch(typeof(ModItemDirectory), "InitDirectory", null, "PostfixInitDirectory");
			LogMsg("[Patch] LoadFromAtlas自定义sprite拦截已注册");
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
			ManualPatcher.TryPatch(typeof(StoreClientManager), "OnNewDay", null, "PostfixOnNewDay", null, typeof(RobinCrusoePerk));
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
			ManualPatcher.TryPatch(typeof(PlayerStore), "StartNewGame", null, "PostfixStartNewGame", null, typeof(RobinCrusoePerk)); // 09-21 发放后清+重发（根治"清了白清"）
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
			LogMsg("[Patch] 应用补丁失败: " + ex3.Message);
		}
	}

	public static bool PerkActive(string perkId)
	{
		try
		{
			return StartingPerk.IsPerkActive(perkId);
		}
		catch
		{
			return false;
		}
	}

	public static void DebugLog(string msg)
	{
		try
		{
			Core.DebugOutput?.Invoke(msg);
		}
		catch
		{
		}
	}

	public static void LogMsg(string msg)
	{
		if (DebugMode)
		{
			Log?.Msg(msg);
		}
	}
}
