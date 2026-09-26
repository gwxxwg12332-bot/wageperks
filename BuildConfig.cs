using System;
using System.Collections.Generic;
using MelonLoader;

namespace JacksonPerks;

public static class BuildConfig
{
	private static bool? _hardMode = null;

	// 09-23 改属性：每次访问从 CFG 读，改 CFG 不用重启
	private static int[] _boxWidthsCache;
	public static int[] BoxWidthsArr
	{
		get
		{
			try { _boxWidthsCache = PadToLast(ParseIntList(GetStr("BoxWidths", "3,10,20,32,42,52")), System.Math.Max(6, ContainerMaxStage + 1)); }
			catch { _boxWidthsCache = new int[6] { 3, 10, 20, 32, 42, 52 }; }
			return _boxWidthsCache;
		}
	}

	private static int[] _boxHeightsCache;
	public static int[] BoxHeightsArr
	{
		get
		{
			try { _boxHeightsCache = PadToLast(ParseIntList(GetStr("BoxHeights", "3,10,10,10,10,10")), System.Math.Max(6, ContainerMaxStage + 1)); }
			catch { _boxHeightsCache = new int[6] { 3, 10, 10, 10, 10, 10 }; }
			return _boxHeightsCache;
		}
	}

	private static int[] _upgradeCostsCache;
	public static int[] UpgradeCostsArr
	{
		get
		{
			try { _upgradeCostsCache = PadToLast(ParseIntList(GetStr("UpgradeCosts", "1,10,20,40,50")), System.Math.Max(5, ContainerMaxStage)); }
			catch { _upgradeCostsCache = new int[5] { 1, 10, 20, 40, 50 }; }
			return _upgradeCostsCache;
		}
	}

	// 09-20 CFG 开关：容器/机器减半（开局宽减半）
	public static bool ContainerHalfEnabled
	{
		get
		{
			try { return MelonPreferences.GetEntryValue<bool>("WagesPerks", "ContainerHalfEnabled"); }
			catch { return true; }
		}
	}

	// 09-22 CFG：精神错乱额外槽位数
	public static int MadnessExtraSlots
	{
		get
		{
			try { return MelonPreferences.GetEntryValue<int>("WagesPerks", "MadnessExtraSlots"); }
			catch { return 3; }
		}
	}

	// 09-20 CFG 开关：容器/机器/模板升级（拖 junk 升级）
	public static bool ContainerUpgradeEnabled
	{
		get
		{
			try { return MelonPreferences.GetEntryValue<bool>("WagesPerks", "ContainerUpgradeEnabled"); }
			catch { return true; }
		}
	}

	public static bool WageGirlAutoMove
	{
		get
		{
			try { return MelonPreferences.GetEntryValue<bool>("WagesPerks", "WageGirlAutoMove"); }
			catch { return true; }
		}
		set
		{
			try { MelonPreferences.SetEntryValue("WagesPerks", "WageGirlAutoMove", value); } catch { }
		}
	}


		// ===== 兼容层开关 =====
		public static bool CompatTradeClamp { get { try { return MelonPreferences.GetEntryValue<bool>("WagesPerks", "CompatTradeClamp"); } catch { return true; } } }
		public static bool CompatBudgetRestore { get { try { return MelonPreferences.GetEntryValue<bool>("WagesPerks", "CompatBudgetRestore"); } catch { return true; } } }
		public static bool CompatRepClamp { get { try { return MelonPreferences.GetEntryValue<bool>("WagesPerks", "CompatRepClamp"); } catch { return true; } } }
		public static bool CompatWineNameProtect { get { try { return MelonPreferences.GetEntryValue<bool>("WagesPerks", "CompatWineNameProtect"); } catch { return true; } } }
		public static bool CompatPhoneLocalization { get { try { return MelonPreferences.GetEntryValue<bool>("WagesPerks", "CompatPhoneLocalization"); } catch { return true; } } }
	// ===== 蛙娘系统 CFG（09-23 全部数值可配置，默认=原设计稿数值） =====
	// 客户预算增益（好感分档：原生预算×倍率，≤0不覆盖防归零）
	public static float WageGirlBudgetMultLow => GetFloat("WageGirlBudgetMultLow", 1.5f);
	public static float WageGirlBudgetMultMid => GetFloat("WageGirlBudgetMultMid", 2.5f);
	public static float WageGirlBudgetMultHigh => GetFloat("WageGirlBudgetMultHigh", 4f);
	public static int WageGirlBudgetAffLow => GetInt("WageGirlBudgetAffLow", 30);
	public static int WageGirlBudgetAffMid => GetInt("WageGirlBudgetAffMid", 60);
	public static long WageGirlBudgetCap => 2147483646L;
	// 六维
	public static int WageGirlStatInit => GetInt("WageGirlStatInit", 60);
	public static int WageGirlStatMax => GetInt("WageGirlStatMax", 100);
	public static int WageGirlDailyDecay => GetInt("WageGirlDailyDecay", 2);
	public static int WageGirlSleepRecover => GetInt("WageGirlSleepRecover", 15);
	// 好感
	public static int WageGirlAffMax => GetInt("WageGirlAffMax", 100);
	public static int WageGirlAffDecayMin => GetInt("WageGirlAffDecayMin", 1);
	public static int WageGirlAffDecayMax => GetInt("WageGirlAffDecayMax", 2);
	public static int WageGirlAffDecayLowMin => GetInt("WageGirlAffDecayLowMin", 2);
	public static int WageGirlAffDecayLowMax => GetInt("WageGirlAffDecayLowMax", 5);
	// 偷钱
	public static int WageGirlStealInterval => GetInt("WageGirlStealInterval", 5);
	public static int WageGirlStealFirstAmount => GetInt("WageGirlStealFirstAmount", 50);
	public static int WageGirlStealBaseMax => GetInt("WageGirlStealBaseMax", 100);
	public static int WageGirlStealAffReduction => GetInt("WageGirlStealAffReduction", 90);
	public static int WageGirlStealSleepDebt => GetInt("WageGirlStealSleepDebt", 20);
	// 偷拿（自主偷吃偷喝）
	public static int WageGirlSnatchValueLow => GetInt("WageGirlSnatchValueLow", 50);
	public static int WageGirlSnatchValueMid => GetInt("WageGirlSnatchValueMid", 100);
	public static int WageGirlSnatchValueHigh => GetInt("WageGirlSnatchValueHigh", 200);
	public static int WageGirlSnatchSleepDebt => GetInt("WageGirlSnatchSleepDebt", 10);
	public static int WageGirlStealNoStealAff => GetInt("WageGirlStealNoStealAff", 80);
	// 洗白费用（每件违禁品）
	public static int WageGirlWashCostPerItem => GetInt("WageGirlWashCostPerItem", 50);
	// 跑路
	public static int WageGirlRunawayLowStat => GetInt("WageGirlRunawayLowStat", 20);
	public static int WageGirlRunawayStreak => GetInt("WageGirlRunawayStreak", 5);
	public static int WageGirlRunawayDays => GetInt("WageGirlRunawayDays", 14);
	// 销赃
	public static int WageGirlFenceDays => GetInt("WageGirlFenceDays", 2);
	public static int WageGirlFenceFeeBasePct => GetInt("WageGirlFenceFeeBasePct", 15);
	public static int WageGirlFenceFeeMinPct => GetInt("WageGirlFenceFeeMinPct", 5);
	public static int WageGirlFenceSleepDebt => GetInt("WageGirlFenceSleepDebt", 20);
	// 零花钱档位（逗号分隔）
	public static int[] WageGirlAllowanceSteps => ParseIntList(GetStr("WageGirlAllowanceSteps", "100,300,500"));
	// 销赃 margin（09-26 C口径：期望产出≥投入；超额=蛙娘渠道溢价，不扣玩家钱/不动小金库）
	public static int WageGirlFenceAffBonusPct => GetInt("WageGirlFenceAffBonusPct", 20);    // 好感加成上限(%)
	public static int WageGirlFenceMarginBasePct => GetInt("WageGirlFenceMarginBasePct", 10); // 基准加成(%)
	public static int WageGirlFenceMarginLow => GetInt("WageGirlFenceMarginLow", -10);        // 随机下界(%)
	public static int WageGirlFenceMarginHigh => GetInt("WageGirlFenceMarginHigh", 30);       // 随机上界(%)
	// 好物（好感达标每 N 天带 1 件）
	public static int WageGirlGiftAff => GetInt("WageGirlGiftAff", 50);
	public static int WageGirlGiftInterval => GetInt("WageGirlGiftInterval", 7);

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

	public static int ContainerMaxStage => GetInt("ContainerMaxStage", 5);

	public static void InitPrefs()
	{
		try
		{
			MelonPreferences_Category melonPreferences_Category = MelonPreferences.CreateCategory("WagesPerks", "Wage's Perks");
					melonPreferences_Category.CreateEntry("HardMode", default_value: false, "硬爽模式：稀有率上限50% / 拾荒+10 / 神经模组进均匀池 / 博士夜卖受限模组 / 开局精选好货");
		melonPreferences_Category.CreateEntry("ContainerHalfEnabled", default_value: true, "容器/机器开局减半（关=不减半）");
		melonPreferences_Category.CreateEntry("MadnessExtraSlots", default_value: 3, "精神错乱额外槽位数");
		melonPreferences_Category.CreateEntry("ContainerUpgradeEnabled", default_value: true, "容器/机器升级（关=不升级）");
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
			melonPreferences_Category.CreateEntry("ContainerMaxStage", 5, "蛙哥箱段位上限");
			// ===== 蛙娘系统（09-23 全部数值可配置） =====
			melonPreferences_Category.CreateEntry("WageGirlBudgetMultLow", 1.5f, "蛙娘预算倍率·低好感(<30)");
			melonPreferences_Category.CreateEntry("WageGirlBudgetMultMid", 2.5f, "蛙娘预算倍率·中好感(30-60)");
			melonPreferences_Category.CreateEntry("WageGirlBudgetMultHigh", 4f, "蛙娘预算倍率·高好感(≥60)");
			melonPreferences_Category.CreateEntry("WageGirlBudgetAffLow", 30, "蛙娘预算倍率·低档好感阈值");
			melonPreferences_Category.CreateEntry("WageGirlBudgetAffMid", 60, "蛙娘预算倍率·中档好感阈值");
			melonPreferences_Category.CreateEntry("WageGirlStatInit", 60, "蛙娘六维初始值");
			melonPreferences_Category.CreateEntry("WageGirlStatMax", 100, "蛙娘六维上限");
			melonPreferences_Category.CreateEntry("WageGirlDailyDecay", 2, "蛙娘每日六维衰减(未照顾)");
			melonPreferences_Category.CreateEntry("WageGirlSleepRecover", 15, "蛙娘打烊睡眠恢复");
			melonPreferences_Category.CreateEntry("WageGirlAffMax", 100, "蛙娘好感上限");
			melonPreferences_Category.CreateEntry("WageGirlAffDecayMin", 1, "蛙娘好感每日衰减下限(没互动)");
			melonPreferences_Category.CreateEntry("WageGirlAffDecayMax", 2, "蛙娘好感每日衰减上限(没互动)");
			melonPreferences_Category.CreateEntry("WageGirlAffDecayLowMin", 2, "蛙娘好感额外衰减下限(六维低)");
			melonPreferences_Category.CreateEntry("WageGirlAffDecayLowMax", 5, "蛙娘好感额外衰减上限(六维低)");
			melonPreferences_Category.CreateEntry("WageGirlStealInterval", 5, "蛙娘偷钱周期(天)");
			melonPreferences_Category.CreateEntry("WageGirlStealFirstAmount", 50, "蛙娘初次偷钱额");
			melonPreferences_Category.CreateEntry("WageGirlStealBaseMax", 100, "蛙娘偷钱基数(好感0时)");
			melonPreferences_Category.CreateEntry("WageGirlStealAffReduction", 90, "蛙娘偷钱好感减免(好感100→减90)");
			melonPreferences_Category.CreateEntry("WageGirlStealSleepDebt", 20, "蛙娘偷钱熬夜睡眠债");
			melonPreferences_Category.CreateEntry("WageGirlSnatchValueLow", 50, "蛙娘偷拿价值上限·低好感(<30)");
			melonPreferences_Category.CreateEntry("WageGirlSnatchValueMid", 100, "蛙娘偷拿价值上限·中好感(30-70)");
			melonPreferences_Category.CreateEntry("WageGirlSnatchValueHigh", 200, "蛙娘偷拿价值上限·高好感(≥70)");
			melonPreferences_Category.CreateEntry("WageGirlSnatchSleepDebt", 10, "蛙娘偷拿熬夜睡眠债");
			melonPreferences_Category.CreateEntry("WageGirlStealNoStealAff", 80, "蛙娘好感≥此值不再偷钱/偷拿");
			melonPreferences_Category.CreateEntry("WageGirlWashCostPerItem", 50, "蛙娘洗白每件违禁品费用");
			melonPreferences_Category.CreateEntry("WageGirlRunawayLowStat", 20, "蛙娘跑路·六维低于此值计1天");
			melonPreferences_Category.CreateEntry("WageGirlRunawayStreak", 5, "蛙娘跑路·连续天数门槛");
			melonPreferences_Category.CreateEntry("WageGirlRunawayDays", 14, "蛙娘跑路·离家天数");
			melonPreferences_Category.CreateEntry("WageGirlFenceDays", 2, "蛙娘销赃外出天数");
			melonPreferences_Category.CreateEntry("WageGirlFenceFeeBasePct", 15, "蛙娘销赃跑腿费起点(%)");
			melonPreferences_Category.CreateEntry("WageGirlFenceFeeMinPct", 5, "蛙娘销赃跑腿费下限(%)");
			melonPreferences_Category.CreateEntry("WageGirlFenceSleepDebt", 20, "蛙娘销赃熬夜睡眠债");
			melonPreferences_Category.CreateEntry("WageGirlAllowanceSteps", "100,300,500", "蛙娘零花钱档位(逗号分隔)");
			melonPreferences_Category.CreateEntry("WageGirlFenceAffBonusPct", 20, "蛙娘销赃好感加成上限(%)");
			melonPreferences_Category.CreateEntry("WageGirlFenceMarginBasePct", 10, "蛙娘销赃基准加成(%)");
			melonPreferences_Category.CreateEntry("WageGirlFenceMarginLow", -10, "蛙娘销赃随机浮动下限(%)");
			melonPreferences_Category.CreateEntry("WageGirlFenceMarginHigh", 30, "蛙娘销赃随机浮动上限(%)");
			melonPreferences_Category.CreateEntry("WageGirlGiftAff", 50, "蛙娘好物·好感门槛");
			melonPreferences_Category.CreateEntry("WageGirlGiftInterval", 7, "蛙娘好物·间隔天数");
			melonPreferences_Category.CreateEntry("BoxWidths", "3,10,20,32,42,52", "蛙哥箱每段宽度(逗号分隔)");
			melonPreferences_Category.CreateEntry("BoxHeights", "3,10,10,10,10,10", "蛙哥箱每段高度(逗号分隔)");
			melonPreferences_Category.CreateEntry("UpgradeCosts", "1,10,20,40,50", "蛙哥箱每级升级材料数(逗号分隔)");
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
			// 09-23 容器表改属性，InitPrefs 不再一次性赋值
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

	private static float GetFloat(string key, float def)
	{
		try { return MelonPreferences.GetEntryValue<float>("WagesPerks", key); }
		catch { return def; }
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
