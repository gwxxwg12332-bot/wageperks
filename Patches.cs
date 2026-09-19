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

internal static class Patches
{
	private static int _inspectionTriggeredDay = -1;

	private static bool _keysDumped = false;

	public static int CurrentUITradeMode = 0;

	private static long _lastSellModeItemUid = -1L;

	private static long _lastBuyModeItemUid = -1L;

	private static int _lastScheduledDay = -1;

	private static bool _secVipOverride = false;

	internal static bool _inJacksonInject = false;

	internal static long _lastSellLogTick = 0L;

	private static readonly System.Collections.Generic.Dictionary<string, string> _cringeReplacements = new System.Collections.Generic.Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
	{
		{
			"好有故事感啊",
			LangHelper.T("哇，这地方太有赛博朋克那味儿了！老板，这东西多少钱？我买个纪念。", "Wow, this place reeks of cyberpunk! Boss, how much is this? I want a souvenir.")
		},
		{
			"你这店开了多久了",
			LangHelper.T("哇，这地方太有赛博朋克那味儿了！老板，这东西多少钱？我买个纪念。", "Wow, this place reeks of cyberpunk! Boss, how much is this? I want a souvenir.")
		},
		{
			"你们这的东西都好特别啊",
			LangHelper.T("哇，你们这的东西都好有特色！我都想买，就是预算不太够，老板能便宜点不？", "Wow, your stuff is so distinctive! I want it all, but my budget's tight - can you do a deal, boss?")
		},
		{
			"有生锈的废料之类的吗",
			LangHelper.T("老板，有那种废旧零件、废铁之类的吗？我带回去当摆件，朋友看了肯定觉得酷！", "Boss, got any old parts or scrap? I'll take them home as decor - my friends will think it's cool!")
		},
		{
			"我想多来几种",
			LangHelper.T("多来几种，我挑挑，回去送朋友也合适。", "Give me a few kinds - I'll pick. Good as gifts back home.")
		},
		{
			"哎呀老板你好呀",
			LangHelper.T("哇老板你好！我是来旅游的，你们这有什么特色好东西吗？我想买点回去当纪念品。", "Wow, hi boss! I'm a tourist - got anything special here? I want souvenirs.")
		},
		{
			"来点吃的、喝的",
			LangHelper.T("来点吃的喝的，再来点能解压的好东西？回去送人也合适。", "Some food and drinks, plus something stress-relieving? Great for gifts.")
		},
		{
			"收不收信用卡",
			LangHelper.T("老板，你们这收不收信用芯片啊？我现金不太够了。什么？不收？那算了，我就买这个便宜点的吧，多少钱？", "Boss, do you take credit chips? I'm short on cash. What? No? Fine, I'll take this cheaper one - how much?")
		},
		{
			"裱起来放家里",
			LangHelper.T("有没有小型武器？你懂的，我想裱起来放家里展示，朋友来了肯定觉得有品位！", "Got any small weapons? You know - I want to frame one for display. Guests will think I've got class!")
		}
	};

	private static readonly HashSet<string> _loggedDialogues = new HashSet<string>();

	private static readonly string[] GunsmithSellTradeLines = new string[6]
	{
		LangHelper.T("军需库清出来的老货，你给看看，都是压箱底的好东西。", "Old stock from the quartermaster's stores - take a look, all hidden gems."),
		LangHelper.T("这些东西我留着也用不上了，你识货就收下，价好说。", "I've got no use for these anymore. If you know your stuff, take them - price is negotiable."),
		LangHelper.T("当年攒下的家伙事儿，如今用不上了，便宜给你这些识货的。", "Gear I hoarded back in the day - no use now. Cheap for someone who appreciates it."),
		LangHelper.T("枪械配件、弹药，我这有的是，你挑挑，别跟我客气。", "Gun parts and ammo, I've got plenty. Take your pick, don't be shy."),
		LangHelper.T("上边淘汰下来的枪件，我修了修还能用，你看看值多少。", "Gun parts scrapped up top - I fixed them up, still work. See what they're worth."),
		LangHelper.T("这把老伙计跟了我三十年，如今也该找个识货的下家了。", "This old friend served me thirty years. Time to find it a worthy new home.")
	};

	private static readonly string[] GunsmithBuyTradeLines = new string[6]
	{
		LangHelper.T("帮我留意点好零件，我这把老家伙还等着换件呢。", "Keep an eye out for good parts - this old piece of mine needs replacements."),
		LangHelper.T("有枪械配件和弹药就给我留着，这周要用。", "Set aside any gun parts and ammo - need them this week."),
		LangHelper.T("我这缺几样配件，你收的时候帮我留意着点。", "Missing a few parts. When you take in goods, keep me in mind."),
		LangHelper.T("老规矩，有好货先想着我，我给的价不亏你。", "Same as always - think of me first for the good stuff. I pay fair."),
		LangHelper.T("零件、火药、模具，有合适的都给我留着。", "Parts, powder, molds - hold onto anything suitable."),
		LangHelper.T("上次那批枪件不错，这次有类似的再叫我。", "Last batch of gun parts was good. Call me if similar comes in.")
	};

	private static readonly string[] WaterSellTradeLines = new string[4]
	{
		LangHelper.T("新一批纯水，从净水厂直接运来的，你看看成色。", "Fresh batch of pure water, straight from the treatment plant. Check the quality."),
		LangHelper.T("这水干净，没有下层的怪味，你给个实在价。", "Clean water, no lower-level stink. Give me a fair price."),
		LangHelper.T("老顾客了，这桶水给你留的，看看要不要。", "You're a regular - saved this jug for you. Want it?"),
		LangHelper.T("纯水和优质水，我这都有，你挑挑看。", "Pure and premium water, I've got both. Take a look.")
	};

	private static readonly string[] WaterBuyTradeLines = new string[4]
	{
		LangHelper.T("有没有便宜的水源？只要能喝，我都要。", "Got any cheap water sources? If it's drinkable, I'll take it."),
		LangHelper.T("你收水吗？有好水源就给我留着。", "Do you buy water? Save any good sources for me."),
		LangHelper.T("这周的水不够卖，你有路子就匀我点。", "Out of water this week - spare me some if you have a source."),
		LangHelper.T("水质好点的水，你有就给我留着，价好说。", "Good quality water - save it for me, price is flexible.")
	};

	private static readonly string[] WineSellTradeLines = new string[4]
	{
		LangHelper.T("新到的一批精选葡萄，成色好得很，你酿酒会用得上。", "Fresh batch of select grapes, excellent quality - perfect for brewing."),
		LangHelper.T("这特级酵母我托人从上边弄来的，发酵力强，酿出来的酒品质高。", "Top-grade yeast smuggled from up top - strong fermentation, high-quality wine."),
		LangHelper.T("纯净水，没有下层区的怪味，酿酒用这个最合适。", "Pure water, no lower-level stink. Ideal for brewing."),
		LangHelper.T("这批原料质量好，你要是不要，我可就给别家了。", "Top-quality ingredients. If you don't take them, I'll find another buyer.")
	};

	private static readonly string[] WineBuyTradeLines = new string[5]
	{
		LangHelper.T("你酿的酒呢？拿出来我看看，价格好商量，绝对不让你亏。", "Show me your brew - price is flexible, I won't short you."),
		LangHelper.T("有自酿的好酒吗？果子酿的粮食酿的都行，我看看品质。", "Any good homebrew? Fruit or grain, doesn't matter - let me check the quality."),
		LangHelper.T("这周收的酒都卖完了，你酿的有富余就匀给我点。", "Sold out of wine this week - spare me some of your surplus."),
		LangHelper.T("老客户了，你酿的酒我信得过，有新酿的就叫我，价好说。", "You're a regular - I trust your brew. Call me when a new batch is ready, price is fair."),
		LangHelper.T("听说你又酿了批新酒？拿出来尝尝，品质好我给个公道价。", "Heard you've got a new batch? Let me taste it - good quality earns a fair price.")
	};

	private static readonly string[] DrDialogues = new string[5]
	{
		LangHelper.T("……是你啊。进来吧，这周的好货都给你备着了。", "...It's you. Come in - this week's good stock is set aside for you."),
		LangHelper.T("又见面了。我这儿的东西，别人可轻易摸不着。", "We meet again. My goods aren't easy for just anyone to get."),
		LangHelper.T("老朋友，今天给你带了几台像样的机器，你看看成色。", "Old friend - brought you a few decent machines today. Check them out."),
		LangHelper.T("你来得正好，这几样压箱底的设备，正好想着给你留的。", "Perfect timing - was saving these choice pieces of equipment for you."),
		LangHelper.T("老规矩，我这儿来的都是正经路子。这批货，你先挑。", "Same as always - everything here is legit. This batch, you pick first.")
	};

	private static readonly string[] DrSellTradeLines = new string[5]
	{
		LangHelper.T("熔炉模组和能量电池，刚从厂里匀出来，给你留着呢。", "Furnace modules and energy cells - just pulled from the factory, saved for you."),
		LangHelper.T("老朋友了，好东西自然先想着你。这批制造设备，你收不收？", "Old friend - good stuff goes to you first. Want this manufacturing gear?"),
		LangHelper.T("我这儿的东西都是别人拿不到的。熔炉、电池，你挑挑看。", "What I have, others can't get. Furnaces, batteries - take your pick."),
		LangHelper.T("设备我都替你验过了，能用。你收走，比从别处买划算得多。", "I've tested all this gear - it works. Buying from me beats anywhere else."),
		LangHelper.T("大机器和大储存，我这儿管够。你要的话，价好商量。", "Big machines and big storage - I've got plenty. Price is flexible.")
	};

	private static readonly string[] DrBuyTradeLines = new string[5]
	{
		LangHelper.T("有材料就给我留着，我那几台机器正等着喂料呢。", "Save materials for me - my machines are hungry."),
		LangHelper.T("你收材料的时候帮我留意着点，金属、元件我都要。", "When you take in materials, keep me in mind - metals and components, I'll take both."),
		LangHelper.T("老伙计，有好材料先想着我，我出的价比市场公道。", "Old friend - think of me first for good materials. I pay above market."),
		LangHelper.T("这周的料不够了，你有渠道就匀我点，下回算你优惠。", "Short on materials this week - spare me some if you have a source. I'll make it up next time."),
		LangHelper.T("废料也好，稀有金属也罢，只要是能熔的，我都要。", "Scrap or rare metals - if it can be smelted, I'll take it.")
	};

	private static bool _inNegociatedCalc = false;

	private static int _currentValueCallsInNegociated = 0;

	private static bool _inBudgetOverride = false;

	private static readonly HashSet<long> _moodBoostedClients = new HashSet<long>();

	private const string FRIEND_DISCOUNT_ID = "friend_discount";

	private static readonly System.Collections.Generic.Dictionary<long, GameItem> _discountedItems = new System.Collections.Generic.Dictionary<long, GameItem>();

	private static readonly HashSet<long> _nodeBuffItems = new HashSet<long>();

	private static readonly System.Collections.Generic.Dictionary<long, GameItem> _tradeFeatureItems = new System.Collections.Generic.Dictionary<long, GameItem>();

	private static System.Reflection.PropertyInfo _negocInstanceProp;

	private static System.Reflection.FieldInfo _negocInstanceField;

	private static readonly System.Collections.Generic.Dictionary<System.Type, System.Reflection.FieldInfo> _itemNameFieldCache = new System.Collections.Generic.Dictionary<System.Type, System.Reflection.FieldInfo>();

	private static readonly System.Collections.Generic.Dictionary<System.Type, System.Reflection.PropertyInfo> _textPropCache = new System.Collections.Generic.Dictionary<System.Type, System.Reflection.PropertyInfo>();

	private static bool _negocCacheInit = false;

	private static bool _pendingLoadGameRestore = false;

	private static int _loadGameRestoreDelayFrames = 0;

	internal static void FrameUpdate()
	{
		try
		{
			if (PerkUIController.Instance != null && PerkUIController.Instance.ui != null && PerkUIController.Instance.ui.activeSelf)
			{
				return;
			}
		}
		catch
		{
		}
		try
		{
			RobinCrusoePerk.HandleHotkeys();
		}
		catch
		{
		}
		try
		{
			LuckScoutBackpackUpgrade.ProcessPendingValidate();
		}
		catch
		{
		}
		try
		{
			LuckScoutBackpackUpgrade.OnUpdateRestore();
		}
		catch
		{
		}
		try
		{
			UpdatePendingLoadGameRestore();
		}
		catch
		{
		}
		try
		{
			LuckScoutPerk.OnUpdateGiveRetry();
		}
		catch
		{
		}
		try
		{
			FrogPowerPerk.OnUpdateBoxGiveRetry();
		}
		catch
		{
		}
		try
		{
			Diagnostics.OnUpdate();
		}
		catch
		{
		}
	}

	internal static void PostfixInputActionManagerUpdate(InputActionManager __instance)
	{
		if (__instance == null)
		{
			return;
		}
		FrameUpdate();
		if (_keysDumped)
		{
			return;
		}
		_keysDumped = true;
		try
		{
			Il2CppSystem.Collections.Generic.List<InputActionHandler> actionHandlers = __instance.actionHandlers;
			if (actionHandlers == null)
			{
				return;
			}
			for (int i = 0; i < actionHandlers.Count; i++)
			{
				InputActionHandler inputActionHandler = actionHandlers[i];
				if (inputActionHandler == null)
				{
					continue;
				}
				try
				{
					Il2CppSystem.Collections.Generic.List<KeyCode> keyListeners = inputActionHandler.keyListeners;
					if (keyListeners != null && keyListeners.Count > 0)
					{
						string[] array = new string[keyListeners.Count];
						for (int j = 0; j < keyListeners.Count; j++)
						{
							array[j] = keyListeners[j].ToString();
						}
						string.Join(",", array);
					}
				}
				catch
				{
				}
				try
				{
					_ = inputActionHandler.GetType().FullName;
				}
				catch
				{
				}
				try
				{
					_ = inputActionHandler.GetIl2CppType().FullName;
				}
				catch
				{
				}
			}
		}
		catch (System.Exception ex)
		{
			Core.LogMsg("[按键] Dump 异常: " + ex.Message);
		}
	}

	public static bool PrefixClientNoExposeInjector(ItemFeature itemFeature, ref bool __result)
	{
		try
		{
			if (itemFeature == null || itemFeature.realCondition == null || itemFeature.fakeCondition == null || itemFeature.realCondition.identifier == null)
			{
				__result = true;
				Core.LogMsg("[Bug修复] ClientNoExposeInjector: null 条件拦截，按无限制处理（true）");
				return false;
			}
		}
		catch (System.Exception ex)
		{
			Core.LogMsg("[Bug修复] PrefixClientNoExposeInjector异常: " + ex.Message);
		}
		return true;
	}

	public static bool PrefixGetClientExposeDialog(ItemFeature __instance, ref Dialogue __result)
	{
		try
		{
			if (__instance == null || __instance.realCondition == null || __instance.fakeCondition == null)
			{
				__result = null;
				return false;
			}
		}
		catch (System.Exception ex)
		{
			Core.LogMsg("[Bug修复] PrefixGetClientExposeDialog异常: " + ex.Message);
		}
		return true;
	}

	public static void PostfixUIInitSellMode(GameItem item)
	{
		CurrentUITradeMode = 2;
		if (item != null)
		{
			long num = 0L;
			try
			{
				num = item.uniqueId;
			}
			catch
			{
			}
			if (num != _lastSellModeItemUid)
			{
				_lastSellModeItemUid = num;
				FixTradeItemName(item);
				TryAddTradeFeatureByUI(item);
			}
		}
	}

	private static void TryAddTradeFeatureByUI(GameItem item)
	{
		try
		{
			if (item != null)
			{
				bool isContraband = false;
				try
				{
					try { isContraband = ContrabandHelper.GetContrabandLevel(item) > 0; } catch { isContraband = item.IsTag("contraband"); }
				}
				catch
				{
				}
				bool isAlcohol = TraitEffects.IsAlcohol(item);
				TryAddTradeFeature(item, isContraband, isAlcohol);
			}
		}
		catch (System.Exception ex)
		{
			Core.LogMsg("[加价标签UI] 失败: " + ex.Message);
		}
	}

	public static void PostfixUIInitBuyMode(GameItem item)
	{
		CurrentUITradeMode = 1;
		if (item != null)
		{
			long num = 0L;
			try
			{
				num = item.uniqueId;
			}
			catch
			{
			}
			if (num != _lastBuyModeItemUid)
			{
				_lastBuyModeItemUid = num;
				FixTradeItemName(item);
				TryApplyFriendDiscountFeature(item);
			}
		}
	}

	public static void PostfixUIClose()
	{
		CurrentUITradeMode = 0;
		_lastSellModeItemUid = -1L;
		_lastBuyModeItemUid = -1L;
		RemoveFriendDiscountFeatures();
		RemoveTradeFeatures();
	}

	public static void PostfixInitStartingPerks()
	{
		try
		{
			CustomStartingPerks.EnsureRegistered();
			Core.LogMsg("[特性] 已注册所有自定义特性");
		}
		catch (System.Exception ex)
		{
			Core.LogMsg("[特性] 注册失败: " + ex.Message);
		}
	}

	public static void PostfixOnNewGame()
	{
		try
		{
			try
			{
				PerkStatePersistence.ResetCache();
			}
			catch
			{
			}
			WageGirlSystem.CleanDefaultRunOnNewGame(); // 09-22 蛙娘：新档清 default_run 残留（防串档/新档误判已存在）
			WageGirlSystem.ResetForNewGame(); // 09-20 蛙娘：新档硬重置状态（根治初次偷拿不触发——lastStealDay残留）
			RobinCrusoePerk.CleanDefaultRunOnNewGame(); // 09-22 鲁滨逊：新档清 default_run 残留（防未保存档残留串新档）
			FrogPowerPerk.ResetState();
			CustomStartingPerks.NotifyNewGame();
		}
		catch (System.Exception ex)
		{
			Core.LogMsg("[特性] 新游戏补丁失败: " + ex.Message);
		}
	}

	public static void HandleInitialItemPostfix()
	{
		try
		{
			try
			{
				FrogPowerPerk._storageBoxGiven = false;
			}
			catch
			{
			}
			try
			{
				DestinyDicePerk._diceGiven = false;
			}
			catch
			{
			}
			try
			{
				LuckScoutPerk.ResetGiveFlag();
			}
			catch
			{
			}
			if (FrogPowerPerk.IsActive() && !FrogPowerPerk._storageBoxGiven)
			{
				FrogPowerPerk.TryGiveStorageBox();
			}
			if (LuckScoutPerk.IsActive())
			{
				try
				{
					PerkStatePersistence.ResetCache();
				}
				catch
				{
				}
				LuckScoutPerk.TryGiveKit();
			}
			try
			{
				DestinyDicePerk.GiveIfActive();
			}
			catch
			{
			}
			try
			{
				RobinCrusoePerk.TrySetupNewRun();
			}
			catch
			{
			}
			try
			{
				// 09-20 B3：流浪者兜底——鲁滨逊等其他特性发放后全清 + 重发 6 件（HandleInitialItem 晚于各特性发放）
				// 09-21 已迁移 WandererPerk.PostfixHandleSkipIntro（HandleSkipIntro L7742 后清——唯一正确时机）
			}
			catch
			{
			}
		}
		catch (System.Exception ex)
		{
			Core.LogMsg("[蛙哥牛逼] HandleInitialItemPostfix异常: " + ex.Message);
		}
	}

	private static bool HasJacksonQueued()
	{
		try
		{
			PlayerStore instance = PlayerStore.Instance;
			if (instance == null)
			{
				return false;
			}
			Il2CppSystem.Collections.Generic.List<string> futurStoreClientIdQueue = instance.futurStoreClientIdQueue;
			if (futurStoreClientIdQueue != null)
			{
				for (int i = 0; i < futurStoreClientIdQueue.Count; i++)
				{
					if (futurStoreClientIdQueue[i] == "inventorStorage" || futurStoreClientIdQueue[i] == "inventor_storage")
					{
						return true;
					}
				}
			}
			StoreClientManager storeClientManager = instance.storeClientManager;
			if (storeClientManager != null)
			{
				Il2CppSystem.Collections.Generic.List<StoreClient> clientStack = storeClientManager.clientStack;
				if (clientStack != null)
				{
					for (int j = 0; j < clientStack.Count; j++)
					{
						StoreClient storeClient = clientStack[j];
						if (storeClient != null && storeClient.identifier != null && (storeClient.identifier == "inventorStorage" || storeClient.identifier == "inventor_storage"))
						{
							return true;
						}
					}
				}
			}
		}
		catch
		{
		}
		return false;
	}

	private static void EnsureJacksonIntroduced()
	{
		try
		{
			StoreClientData storeClientData = PlayerStore.GetStoreClientData();
			if (storeClientData != null && !storeClientData.isJacksonIntroduced)
			{
				storeClientData.isJacksonIntroduced = true;
			}
		}
		catch (System.Exception ex)
		{
			Core.LogMsg("[博士之友] 设置isJacksonIntroduced失败: " + ex.Message);
		}
	}

	internal static bool ScheduleJacksonToday()
	{
		try
		{
			if (!DrJacksonFriendPerk.IsActive())
			{
				return false;
			}
			if (PlayerStore.Instance == null)
			{
				return false;
			}
			int dayCounter = StoreStation.GetDayCounter();
			if (HasJacksonQueued())
			{
				return false;
			}
			if (_lastScheduledDay >= 0 && dayCounter - _lastScheduledDay < 7)
			{
				return false;
			}
			_lastScheduledDay = dayCounter;
			EnsureJacksonIntroduced();
			try
			{
				PlayerStore.Instance.QueueFuturClient("inventorStorage", 1);
			}
			catch (System.Exception ex)
			{
				Core.LogMsg("[博士之友] QueueFuturClient 失败: " + ex.Message);
				return false;
			}
			return true;
		}
		catch (System.Exception ex2)
		{
			Core.LogMsg("[博士之友] ScheduleJacksonToday失败: " + ex2.Message);
			return false;
		}
	}

	public static void PostfixOnBeginDay()
	{
		try
		{
			RobinCrusoePerk.TickWantedSupplierDaily();
			ForceInspectionToday();
			ApplyBadLuck();
			if (DrJacksonFriendPerk.IsActive() && PlayerStore.Instance != null)
			{
				ScheduleJacksonToday();
			}
		}
		catch (System.Exception ex)
		{
			Core.LogMsg("[博士之友] BeginDay失败: " + ex.Message);
		}
	}

	public static bool PrefixHandleInspectionClient(StoreClientManager __instance)
	{
		try
		{
			_secVipOverride = RiskTakerPerk.IsActive();
		}
		catch
		{
		}
		return true;
	}

	public static void PostfixIsPerkUnlocked(string perkId, ref bool __result)
	{
		try
		{
			if (_secVipOverride && perkId == "SEC_VIP")
			{
				__result = false;
			}
		}
		catch
		{
		}
	}

	public static void PostfixHandleInspectionClient()
	{
		try
		{
			_secVipOverride = false;
		}
		catch
		{
		}
	}

	public static void ForceInspectionToday()
	{
		try
		{
			if (!RiskTakerPerk.IsActive() && !ThiefMagnetPerk.IsActive())
			{
				return;
			}
			int num = 1;
			try
			{
				num = StoreStation.GetDayCounter();
			}
			catch
			{
			}
			if (_inspectionTriggeredDay == num)
			{
				return;
			}
			bool num2 = RiskTakerPerk.IsActive();
			bool flag = ThiefMagnetPerk.IsActive();
			bool flag2 = num2;
			if (!num2 && flag)
			{
				flag2 = DeterministicRandom.NextBool("thief_magnet_inspection", num, 0.2);
			}
			if (!flag2)
			{
				return;
			}
			try
			{
				if (!RiskTakerPerk.IsActive() && !ThiefMagnetPerk.IsActive())
				{
					return;
				}
				PlayerStore instance = PlayerStore.Instance;
				if (instance == null || instance.secData == null)
				{
					return;
				}
				StoreClientManager storeClientManager = null;
				try
				{
					System.Reflection.PropertyInfo property = typeof(PlayerStore).GetProperty("storeClientManager", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
					if (property != null)
					{
						storeClientManager = property.GetValue(instance) as StoreClientManager;
					}
					if (storeClientManager == null)
					{
						System.Reflection.FieldInfo field = typeof(PlayerStore).GetField("storeClientManager", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
						if (field != null)
						{
							storeClientManager = field.GetValue(instance) as StoreClientManager;
						}
					}
				}
				catch (System.Exception ex)
				{
					Core.LogMsg("[刀尖舔血] 获取storeClientManager失败: " + ex.Message);
				}
				if (storeClientManager != null)
				{
					try
					{
						storeClientManager.dayUntilInspection = 0;
						storeClientManager.daySinceInspection = 100;
						storeClientManager.inspectionSeeded = true;
						storeClientManager.ResetInspection();
						storeClientManager.dayUntilInspection = 0;
						storeClientManager.daySinceInspection = 100;
						storeClientManager.inspectionSeeded = true;
						try
						{
							ContrabandHelper.StartInspection(instance.secData.GetInspectorPersonality());
							try
							{
								_inspectionTriggeredDay = StoreStation.GetDayCounter();
							}
							catch
							{
								_inspectionTriggeredDay = num;
							}
						}
						catch (System.Exception ex2)
						{
							Core.LogMsg("[治安检查] StartInspection失败: " + ex2.Message);
						}
						try
						{
							Il2CppSystem.Collections.Generic.List<StoreClient> clientStack = storeClientManager.clientStack;
							int num3 = clientStack?.Count ?? (-1);
							if (clientStack != null && num3 > 0)
							{
								for (int i = 0; i < num3; i++)
								{
									try
									{
										_ = clientStack[i];
									}
									catch
									{
									}
								}
							}
							return;
						}
						catch (System.Exception ex3)
						{
							Core.LogMsg("[治安检查] clientStack诊断失败: " + ex3.Message);
							return;
						}
					}
					catch (System.Exception ex4)
					{
						Core.LogMsg("[刀尖舔血] 设置检查条件失败: " + ex4.Message);
						return;
					}
				}
				try
				{
					ContrabandHelper.StartInspection(instance.secData.GetInspectorPersonality());
					try
					{
						_inspectionTriggeredDay = StoreStation.GetDayCounter();
					}
					catch
					{
						_inspectionTriggeredDay = num;
					}
				}
				catch (System.Exception ex5)
				{
					Core.LogMsg("[刀尖舔血] StartInspection失败: " + ex5.Message);
				}
			}
			catch (System.Exception ex6)
			{
				Core.LogMsg("[刀尖舔血] 触发检查失败: " + ex6.Message);
			}
		}
		catch (System.Exception ex7)
		{
			Core.LogMsg("[治安检查] 强制检查失败: " + ex7.Message);
		}
	}

	public static void ApplyBadLuck()
	{
		try
		{
			if (!BadLuckPerk.IsActive())
			{
				return;
			}
			PlayerStore instance = PlayerStore.Instance;
			if (instance == null)
			{
				Core.LogMsg("[霉运缠身] PlayerStore为null");
				return;
			}
			int dayCounter = StoreStation.GetDayCounter();
			string text = instance.runID ?? "";
			string key = "WagesBadLuckDay_Run_" + text;
			if (PlayerPrefs.GetInt(key, -1) == dayCounter)
			{
				return;
			}
			int num = DeterministicRandom.Next("bad_luck", dayCounter, 100, 1501);
			bool flag = false;
			try
			{
				System.Reflection.PropertyInfo property = typeof(PlayerStore).GetProperty("playerCash", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
				if (property != null)
				{
					int num2 = (int)property.GetValue(instance);
					property.SetValue(instance, num2 - num);
					flag = true;
				}
			}
			catch (System.Exception ex)
			{
				Core.LogMsg("[霉运缠身] 属性扣款失败: " + ex.Message);
			}
			if (!flag)
			{
				try
				{
					System.Reflection.FieldInfo field = typeof(PlayerStore).GetField("playerCash", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
					if (field != null)
					{
						int num3 = (int)field.GetValue(instance);
						field.SetValue(instance, num3 - num);
						flag = true;
					}
				}
				catch (System.Exception ex2)
				{
					Core.LogMsg("[霉运缠身] 字段扣款失败: " + ex2.Message);
				}
			}
			if (flag)
			{
				PlayerPrefs.SetInt(key, dayCounter);
				try { var _ps = PlayerStore.Instance; if (_ps != null) _ps.AddNightLog("[霉运缠身] " + LangHelper.T("昨晚打烊时，有人趁夜色摸走了你", "Last night after closing, someone slipped in and took") + " " + num + " " + LangHelper.T("信用点。", "credits."), "#7FC97F"); } catch { } // ① 原生夜报（09-22 统一柔和绿）
				Core.AddNightReportLine("[霉运缠身] " + LangHelper.T("昨晚打烊时，有人趁夜色摸走了你", "Last night after closing, someone slipped in and took") + " " + num + " " + LangHelper.T("信用点。", "credits."));
			}
			else
			{
				Core.LogMsg("[霉运缠身] 扣钱失败：playerCash 属性/字段均未找到");
			}
		}
		catch (System.Exception ex3)
		{
			Core.LogMsg("[霉运缠身] 失败: " + ex3.Message);
		}
	}

	public static void PostfixOnNewDay()
	{
		try
		{
			WineLoverPerk.ClearHangover();
			SpecialNpcManager.OnNewDay();
			try
			{
				if (DrJacksonFriendPerk.IsActive() && PlayerStore.Instance != null)
				{
					ScheduleJacksonToday();
				}
			}
			catch (System.Exception ex)
			{
				Core.LogMsg("[博士之友] QueueFuturClient失败: " + ex.Message);
			}
			ForceInspectionToday();
			ApplyBadLuck();
		}
		catch (System.Exception ex2)
		{
			Core.LogMsg("[特性] 新的一天补丁失败: " + ex2.Message);
		}
	}

	public static bool PrefixOnHandleJacksonStorage(StoreClientManager __instance)
	{
		try
		{
			return DrJacksonFriendPerk.HandleJacksonStoragePatch.Prefix(__instance);
		}
		catch (System.Exception ex)
		{
			Core.LogMsg("[博士之友] 频率控制失败: " + ex.Message);
			return true;
		}
	}

	public static void PostfixOnAddClient(StoreClient storeClient)
	{
		if (storeClient == null)
		{
			return;
		}
		try
		{
			DrJacksonFriendPerk.AddClientPatch.Postfix(storeClient);
		}
		catch (System.Exception ex)
		{
			Core.LogMsg("[博士之友] AddClient Postfix失败: " + ex.Message);
		}
		try
		{
			NormalizeSpecialNpcName(storeClient);
		}
		catch (System.Exception ex2)
		{
			Core.LogMsg("[特殊NPC] 统一名字失败: " + ex2.Message);
		}
		try
		{
			if (!(storeClient.identifier == "retired_winemaker"))
			{
				return;
			}
			storeClient.clientIntent = StoreClient.ClientIntent.SELLNBUY;
			long num = 0L;
			try
			{
				Il2CppSystem.Collections.Generic.List<GameItem> invItems = EmporiumEntry.Instance.GetInvItems();
				if (invItems != null)
				{
					Il2CppSystem.Collections.Generic.List<GameItem>.Enumerator enumerator = invItems.GetEnumerator();
					while (enumerator.MoveNext())
					{
						GameItem current = enumerator.Current;
						if (current == null)
						{
							continue;
						}
						string identifier = current.identifier;
						if (!string.IsNullOrEmpty(identifier) && (identifier.Contains("wine") || identifier.Contains("beer") || identifier.Contains("alcohol") || identifier == "wine_bottle"))
						{
							try
							{
								num += current.GetCurrentValue(useRetailMarkup: false, withChild: true, includeEvents: true, forceMarkup: false, 0L);
							}
							catch
							{
							}
						}
					}
				}
			}
			catch
			{
			}
			int num2 = (int)System.Math.Min((double)num * 1.5, 2147483646.0);
			if (num2 < 1000)
			{
				num2 = 1000;
			}
			storeClient.OverrideBudget(num2);
			storeClient.clientCash = num2;
			storeClient.useClientBudget = true;
			string[] array = new string[7] { "beer_case", "red_beer", "wine_bottle", "wine_berry", "wine_bloomberry", "wine_gloomberry", "empty_beer_bottle" };
			if (storeClient.clientBuyingIdList == null)
			{
				storeClient.clientBuyingIdList = new Il2CppSystem.Collections.Generic.List<string>();
			}
			string[] array2 = array;
			foreach (string item in array2)
			{
				if (!storeClient.clientBuyingIdList.Contains(item))
				{
					storeClient.clientBuyingIdList.Add(item);
				}
			}
			string[] array3 = new string[7] { "alcohol", "wine", "beer", "drink", "beverage", "liquor", "booze" };
			if (storeClient.clientBuyingTagList == null)
			{
				storeClient.clientBuyingTagList = new Il2CppSystem.Collections.Generic.List<string>();
			}
			array2 = array3;
			foreach (string item2 in array2)
			{
				if (!storeClient.clientBuyingTagList.Contains(item2))
				{
					storeClient.clientBuyingTagList.Add(item2);
				}
			}
			if (storeClient.clientBlackIdList != null)
			{
				array2 = array;
				foreach (string item3 in array2)
				{
					storeClient.clientBlackIdList.Remove(item3);
				}
			}
			if (storeClient.clientBlackTagList != null)
			{
				array2 = array3;
				foreach (string item4 in array2)
				{
					storeClient.clientBlackTagList.Remove(item4);
				}
			}
		}
		catch (System.Exception ex3)
		{
			Core.LogMsg("[酒商] 设置预算失败: " + ex3.Message);
		}
	}

	private static void NormalizeSpecialNpcName(StoreClient client)
	{
		if (client != null)
		{
			string identifier = client.identifier;
			string text = null;
			switch (identifier)
			{
			case "retired_gunsmith":
				text = LangHelper.T("退休枪匠", "Retired Gunsmith");
				break;
			case "retired_water_merchant":
				text = LangHelper.T("水商", "Water Merchant");
				break;
			case "retired_winemaker":
				text = LangHelper.T("酒商", "Winemaker");
				break;
			}
			if (text != null && client.displayName != text)
			{
				_ = client.displayName;
				client.displayName = text;
			}
		}
	}

	private static bool IsSecurityClient(StoreClient c)
	{
		try
		{
			if (c == null)
			{
				return false;
			}
			if (c.isSecurity)
			{
				return true;
			}
			switch (c.identifier ?? "")
			{
			case "patrolOfficer":
			case "security_inspector":
			case "lazy_security_inspector":
			case "shady_security_inspector":
			case "security_officer":
			case "security_requisition_officer":
			case "off_duty_officer":
			case "private_security_contractor":
			case "officer_lun":
				return true;
			}
		}
		catch
		{
		}
		return false;
	}

	internal static void PostfixAddDirectSellingItemToTable(PlayerStore __instance, GameItem gameItem)
	{
		if (_inJacksonInject || __instance == null)
		{
			return;
		}
		try
		{
			string text = "";
			try
			{
				if (__instance.currentClientInstance != null)
				{
					StoreClient clientBlueprint = __instance.currentClientInstance.GetClientBlueprint();
					if (clientBlueprint != null)
					{
						text = clientBlueprint.identifier ?? "";
					}
				}
			}
			catch
			{
			}
			if (System.Environment.TickCount64 - _lastSellLogTick > 5000)
			{
				_lastSellLogTick = System.Environment.TickCount64;
			}
			if (text != "inventor" && text != "inventorStorage" && text != "inventor_storage")
			{
				return;
			}
			_inJacksonInject = true;
			try
			{
				DrJacksonFriendPerk.AddJacksonGoodsToCounter(null);
			}
			finally
			{
				_inJacksonInject = false;
			}
		}
		catch (System.Exception ex)
		{
			Core.LogMsg("[博士之友] PostfixAddDirectSellingItemToTable 异常: " + ex.Message);
		}
	}

	internal static void PostfixPlaceInventorInventory(bool isVisitingPlayerStore)
	{
		try
		{
			RobinCrusoePerk.AddDoctorNightGoods();
			try
			{
				RobinCrusoePerk.TryDoctorSupply();
			}
			catch
			{
			}
		}
		catch (System.Exception ex)
		{
			Core.LogMsg("[博士之友] PostfixPlaceInventorInventory 异常: " + ex.Message);
		}
	}

	public static void PostfixSpecialNpcStartDialogue(StoreUIManager __instance)
	{
		try
		{
			StoreClient currentClient = SpecialNpcManager.GetCurrentClient();
			if (currentClient == null)
			{
				return;
			}
			if (currentClient.displayName == null && currentClient.identifier == null)
			{
				LangHelper.T("未知", "Unknown");
			}
			bool flag = SpecialNpcManager.IsSpecialNpc(currentClient);
			if (flag)
			{
				SpecialNpcManager.HandleSpecialNpcArrived(currentClient);
			}
			try
			{
				string identifier = currentClient.identifier;
				if (identifier == "wanted7" || identifier == "wanted6")
				{
					RobinCrusoePerk.WantedSupplierOnArrived(currentClient);
				}
			}
			catch
			{
			}
			try
			{
				AddictOfficerEvent.OnClientArrived(currentClient);
			}
			catch
			{
			}
			if (!flag && !IsSecurityClient(currentClient) && currentClient.clientIntent == StoreClient.ClientIntent.BUY)
			{
				FrogPowerPerk.EnsureBuyTagsForClient(currentClient);
			}
			if (FrogPowerPerk.IsActive() && !flag && !IsSecurityClient(currentClient) && (currentClient.clientIntent == StoreClient.ClientIntent.SELL || currentClient.clientIntent == StoreClient.ClientIntent.SELLNBUY))
			{
				FrogPowerPerk.AddRandomItemsToCounter(currentClient);
			}
		}
		catch (System.Exception ex)
		{
			Core.LogMsg("[客户到达] OnNextClientArrived补丁失败: " + ex.Message);
		}
	}

	public static void PrefixDisplayClientText(Dialogue dialogue)
	{
		try
		{
			if (dialogue == null)
			{
				return;
			}
			StoreClient currentClient = SpecialNpcManager.GetCurrentClient();
			if (currentClient == null)
			{
				return;
			}
			if (!SpecialNpcManager.IsSpecialNpc(currentClient))
			{
				FixCringeDialogue(dialogue, currentClient);
				return;
			}
			string identifier = currentClient.identifier;
			string title = currentClient.displayName ?? identifier;
			if (currentClient.mainDialogue != null && dialogue == currentClient.mainDialogue)
			{
				string specialNpcGreeting = GetSpecialNpcGreeting(identifier);
				if (specialNpcGreeting != null)
				{
					dialogue.SetText(title, specialNpcGreeting);
				}
				return;
			}
			string text = dialogue.plainText ?? "";
			if (text.Contains("收不收") || text.Contains("想买") || text.Contains("要买") || text.Contains("有没有") || text.Contains("买点") || text.Contains("看看") || text.Contains("卖") || text.Contains("买") || text.Contains("收") || text.Contains("钱") || text.Contains("价") || text.Contains("东西"))
			{
				bool isBuying = currentClient.clientIntent == StoreClient.ClientIntent.BUY;
				string specialNpcTradeLine = GetSpecialNpcTradeLine(identifier, isBuying);
				if (specialNpcTradeLine != null)
				{
					dialogue.SetText(title, specialNpcTradeLine);
				}
			}
		}
		catch (System.Exception ex)
		{
			Core.LogMsg("[交易台词] 替换失败: " + ex.Message);
		}
	}

	private static void FixCringeDialogue(Dialogue dialogue, StoreClient client)
	{
		try
		{
			string title = client.displayName ?? LangHelper.T("未知", "Unknown");
			string text = client.identifier ?? LangHelper.T("未知", "Unknown");
			string text2 = dialogue.plainText ?? "";
			if (string.IsNullOrEmpty(text2))
			{
				return;
			}
			string item = text + "|" + text2;
			if (!_loggedDialogues.Contains(item))
			{
				_loggedDialogues.Add(item);
				if (text2.Length > 150)
				{
					_ = text2.Substring(0, 150) + "...";
				}
			}
			foreach (System.Collections.Generic.KeyValuePair<string, string> cringeReplacement in _cringeReplacements)
			{
				if (text2.Contains(cringeReplacement.Key, System.StringComparison.OrdinalIgnoreCase))
				{
					dialogue.SetText(title, cringeReplacement.Value);
					break;
				}
			}
		}
		catch (System.Exception ex)
		{
			Core.LogMsg("[对话修复] 失败: " + ex.Message);
		}
	}

	private static string GetSpecialNpcGreeting(string id)
	{
		switch (id)
		{
		case "retired_gunsmith":
			return FrogPowerPerk.GunsmithDialogues[Core.Rng.Next(FrogPowerPerk.GunsmithDialogues.Length)];
		case "retired_water_merchant":
			return FrogPowerPerk.WaterMerchantDialogues[Core.Rng.Next(FrogPowerPerk.WaterMerchantDialogues.Length)];
		case "retired_winemaker":
			return FrogPowerPerk.AlcoholMerchantDialogues[Core.Rng.Next(FrogPowerPerk.AlcoholMerchantDialogues.Length)];
		case "inventorStorage":
		case "inventor_storage":
			return DrDialogues[Core.Rng.Next(DrDialogues.Length)];
		default:
			return null;
		}
	}

	private static string GetSpecialNpcTradeLine(string id, bool isBuying)
	{
		switch (id)
		{
		case "retired_gunsmith":
			if (!isBuying)
			{
				return GunsmithSellTradeLines[Core.Rng.Next(GunsmithSellTradeLines.Length)];
			}
			return GunsmithBuyTradeLines[Core.Rng.Next(GunsmithBuyTradeLines.Length)];
		case "retired_water_merchant":
			if (!isBuying)
			{
				return WaterSellTradeLines[Core.Rng.Next(WaterSellTradeLines.Length)];
			}
			return WaterBuyTradeLines[Core.Rng.Next(WaterBuyTradeLines.Length)];
		case "retired_winemaker":
			if (!isBuying)
			{
				return WineSellTradeLines[Core.Rng.Next(WineSellTradeLines.Length)];
			}
			return WineBuyTradeLines[Core.Rng.Next(WineBuyTradeLines.Length)];
		case "inventorStorage":
		case "inventor_storage":
			if (!isBuying)
			{
				return DrSellTradeLines[Core.Rng.Next(DrSellTradeLines.Length)];
			}
			return DrBuyTradeLines[Core.Rng.Next(DrBuyTradeLines.Length)];
		default:
			return null;
		}
	}

	public static void PostfixOnHandleContentUnlockClient(StoreClientManager __instance)
	{
		if (__instance != null)
		{
			SpecialNpcManager.HandleContentUnlockPostfix(__instance);
		}
	}

	public static void PostfixPerkUiOpen(PerkUIController __instance)
	{
		try
		{
			CustomStartingPerks.EnsureRegistered();
			Core.LogMsg("[特性UI] OpenUI时确保特性已注册");
			CustomStartingPerks.EnsurePickerElements(__instance);
		}
		catch (System.Exception ex)
		{
			Core.LogMsg("[特性UI] 打开失败: " + ex.Message);
		}
	}

	public static void PostfixIconLoaderStart(StartingPerkIconLoader __instance)
	{
		try
		{
			bool flag = false;
			CustomStartingPerk[] all;
			try
			{
				System.Reflection.FieldInfo field = typeof(StartingPerkIconLoader).GetField("perkIcons", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
				if (field != null)
				{
					object value = field.GetValue(null);
					if (value != null)
					{
						System.Reflection.PropertyInfo property = value.GetType().GetProperty("Item");
						all = CustomStartingPerks.All;
						foreach (CustomStartingPerk customStartingPerk in all)
						{
							try
							{
								string text = customStartingPerk.Id.Replace("\0", "").Trim();
								if (PerkIconLoader.HasCustomIcon(customStartingPerk.Id))
								{
									Sprite perkIcon = PerkIconLoader.GetPerkIcon(customStartingPerk.Id);
									if (perkIcon != null && property != null)
									{
										property.SetValue(value, perkIcon, new object[1] { text });
									}
								}
							}
							catch (System.Exception ex)
							{
								MelonLogger.Error("[特性图标] 注入图标失败 " + customStartingPerk.Id + ": " + ex.Message);
							}
						}
						flag = true;
					}
				}
			}
			catch (System.Exception ex2)
			{
				Core.LogMsg("[特性图标] 字典方式失败: " + ex2.Message);
			}
			if (flag)
			{
				return;
			}
			System.Reflection.PropertyInfo property2 = __instance.GetType().GetProperty("perkIconList", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
			if (property2 == null)
			{
				return;
			}
			object obj = property2.GetValue(__instance);
			if (obj == null)
			{
				obj = System.Activator.CreateInstance(typeof(Il2CppSystem.Collections.Generic.List<PerkIconEntry>));
				property2.SetValue(__instance, obj);
			}
			System.Reflection.MethodInfo method = obj.GetType().GetMethod("Add");
			if (method == null)
			{
				return;
			}
			all = CustomStartingPerks.All;
			foreach (CustomStartingPerk customStartingPerk2 in all)
			{
				try
				{
					string perkName = customStartingPerk2.Id.Replace("\0", "").Trim();
					if (PerkIconLoader.HasCustomIcon(customStartingPerk2.Id))
					{
						Sprite perkIcon2 = PerkIconLoader.GetPerkIcon(customStartingPerk2.Id);
						if (perkIcon2 != null)
						{
							PerkIconEntry perkIconEntry = new PerkIconEntry();
							perkIconEntry.perkName = perkName;
							perkIconEntry.Sprite = perkIcon2;
							method.Invoke(obj, new object[1] { perkIconEntry });
						}
					}
				}
				catch (System.Exception ex3)
				{
					MelonLogger.Error("[特性图标] 注入图标失败 " + customStartingPerk2.Id + ": " + ex3.Message);
				}
			}
		}
		catch (System.Exception ex4)
		{
			MelonLogger.Error("[特性图标] StartingPerkIconLoaderStartPatch失败: " + ex4.Message + "\n" + ex4.StackTrace);
		}
	}

	public static void PostfixGetLocalizedPerkTable(string key, ref string __result)
	{
		try
		{
			if (string.IsNullOrEmpty(key))
			{
				return;
			}
			string text = null;
			bool flag = false;
			bool flag2 = false;
			if (key.StartsWith("perk_"))
			{
				string text2 = key.Substring(5);
				if (text2.EndsWith("_name"))
				{
					text = text2.Substring(0, text2.Length - 5);
					flag = true;
				}
				else if (text2.EndsWith("_desc"))
				{
					text = text2.Substring(0, text2.Length - 5);
					flag2 = true;
				}
			}
			if (text != null && CustomStartingPerks.Find(text) != null && CustomStartingPerks.TryGetLoc(text, out var displayName, out var description))
			{
				if (flag && !string.IsNullOrEmpty(displayName))
				{
					__result = displayName;
				}
				else if (flag2 && !string.IsNullOrEmpty(description))
				{
					__result = description;
				}
			}
		}
		catch (System.Exception ex)
		{
			Core.LogMsg("[本地化] 补丁失败: " + ex.Message);
		}
	}

	public static void PostfixStartingPerkElementStart(StartingPerkElement __instance)
	{
		try
		{
			CustomStartingPerks.EnsureElement(__instance);
		}
		catch (System.Exception ex)
		{
			Core.LogMsg("[特性UI] 元素启动失败: " + ex.Message);
		}
	}

	public static void PrefixGameItemGetNegociatedValue(GameItem __instance)
	{
		_inNegociatedCalc = true;
		_currentValueCallsInNegociated = 0;
	}

	public static void PostfixGameItemGetNegociatedValue(GameItem __instance, ref long __result)
	{
		_inNegociatedCalc = false;
		if (_currentValueCallsInNegociated == 0)
		{
			TryApplyTradeMarkup(__instance, ref __result);
		}
	}

	public static void PrefixGameItemGetCurrentValue(GameItem __instance)
	{
		if (_inNegociatedCalc)
		{
			_currentValueCallsInNegociated++;
		}
	}

	public static void PostfixGameItemGetCurrentValue(GameItem __instance, ref long __result)
	{
		TryApplyTradeMarkup(__instance, ref __result);
	}

	private static void TryApplyTradeMarkup(GameItem item, ref long result)
	{
		try
		{
			if (BadReputationPerk.IsActive() && !BadReputationPerk.IsCleared())
			{
				if (CurrentUITradeMode == 2)
				{
					result = (long)((double)result * 0.8);
					TryAddBadReputationFeature(item, -20);
				}
				else if (CurrentUITradeMode == 1)
				{
					result = (long)((double)result * 1.2);
					TryAddBadReputationFeature(item, 20);
				}
			}
			if (RobinCrusoePerk.IsActive())
			{
				if (CurrentUITradeMode == 1)
				{
					TryAddNodeBuffFeature(item);
					if (RobinCrusoePerk.IsFood(item))
					{
						result = (long)((double)result * 2.0);
						TryAddRobinsonBuyMarkup(item);
						return;
					}
					if (RobinCrusoePerk.IsMedicine(item))
					{
						result = (long)((double)result * 2.0);
						TryAddRobinsonBuyMarkup(item);
						return;
					}
				}
				else if (CurrentUITradeMode == 2)
				{
					TryAddNodeBuffFeature(item);
					if (RobinCrusoePerk.IsFood(item))
					{
						int foodQuality = RobinCrusoePerk.GetFoodQuality(item);
						switch (foodQuality)
						{
						case 3:
							result = 0L;
							break;
						case 2:
							result = (long)((double)result * 0.1);
							break;
						default:
							if (RobinCrusoePerk.IsEaten(item))
							{
								result = (long)((double)result * 0.2);
							}
							else if (foodQuality <= 0)
							{
								result = (long)((double)result * 1.3);
							}
							break;
						}
						TryAddFoodQualityFeature(item, foodQuality);
					}
					double num = 1.0 + (double)RobinCrusoePerk.GetSellBonusPct() / 100.0;
					if (num > 1.0)
					{
						result = (long)((double)result * num);
					}
					double contraEffMult = RobinCrusoePerk.GetContraEffMult();
					if (contraEffMult > 1.0 && RobinCrusoePerk.IsContrabandItem(item))
					{
						result = (long)((double)result * contraEffMult);
					}
				}
			}
			if (CurrentUITradeMode != 2)
			{
				return;
			}
			bool flag = false;
			try
			{
				flag = ContrabandHelper.IsContraband(item);
			}
			catch
			{
			}
			if (!flag)
			{
				try
				{
					flag = item.IsTag("CONTRABAND");
				}
				catch
				{
				}
			}
			if (!flag)
			{
				try
				{
					flag = item.IsTag("CONTRABAND_ITEM_TAG");
				}
				catch
				{
				}
			}
			bool flag2 = TraitEffects.IsAlcohol(item);
			float num2 = 1f;
			bool flag3 = false;
			try
			{
				flag3 = RiskTakerPerk.IsActive();
			}
			catch
			{
			}
			if (flag3 && flag)
			{
				num2 *= 1.2f;
			}
			if (WineLoverPerk.IsActive() && flag2)
			{
				num2 *= 1.25f;
			}
			if (num2 > 1f)
			{
				result = (long)((float)result * num2);
				TryAddTradeFeature(item, flag, flag2);
			}
			// 笑面虎/童叟无欺：卖出价 ±25%（互斥保证不同时生效；面板+成交+预算全通）
			double faceMult = 1.0;
			string faceLabelId = null;
			string faceLabel = null;
			if (SmilingFacePerk.IsActive()) { faceMult = 1.25; faceLabelId = "smiling_face_markup"; faceLabel = LangHelper.T("笑面虎加价", "Smiling Face Markup"); }
			else if (SmilingTigerPerk.IsActive()) { faceMult = 0.75; faceLabelId = "honest_dealer_discount"; faceLabel = LangHelper.T("童叟无欺折让", "Honest Dealer Discount"); }
			if (faceMult != 1.0)
			{
				result = (long)((double)result * faceMult);
				int facePct = SmilingFacePerk.IsActive() ? 25 : -25;
					AddTradeLabel(item, faceLabelId, faceLabel, ItemFeature.FeatureType.TemporarySelling, facePct);
			}
		}
		catch
		{
		}
	}

	private static StoreClient GetCurrentTradingClient()
	{
		try
		{
			return PlayerStore.Instance?.currentClientInstance?.storeClient;
		}
		catch
		{
			return null;
		}
	}

	public static void PostfixStoreClientApplyBudgetModifier(StoreClient __instance)
	{
		try
		{
			if (__instance == null || !RobinCrusoePerk.IsActive() || _inBudgetOverride)
			{
				return;
			}
			int budgetBonusPct = RobinCrusoePerk.GetBudgetBonusPct();
			if (budgetBonusPct <= 0)
			{
				return;
			}
			_inBudgetOverride = true;
			try
			{
				int budget = __instance.GetBudget();
				__instance.SetBudget((int)((double)budget * (1.0 + (double)budgetBonusPct / 100.0)));
			}
			finally
			{
				_inBudgetOverride = false;
			}
		}
		catch (System.Exception ex)
		{
			Core.LogMsg("[空间站鲁滨逊] 预算联动异常: " + ex.Message);
		}
	}

	public static void PostfixStoreClientManagerPickClient(StoreClient __result)
	{
		try
		{
			if (__result != null && RobinCrusoePerk.IsActive())
			{
				int budgetBonusPct = RobinCrusoePerk.GetBudgetBonusPct();
				if (budgetBonusPct > 0 && !__result.useClientBudget)
				{
					int clientCash = __result.clientCash;
					__result.clientCash = (int)((double)clientCash * (1.0 + (double)budgetBonusPct / 100.0));
				}
			}
		}
		catch (System.Exception ex)
		{
			Core.LogMsg("[空间站鲁滨逊] clientCash联动异常: " + ex.Message);
		}
	}

	public static void PrefixBargainUIManagerOfferBuyingMarkup(ref int percent)
	{
		try
		{
			if (RobinCrusoePerk.IsActive() && RobinCrusoePerk.GetMood() >= 80)
			{
				percent += 15;
			}
		}
		catch (System.Exception ex)
		{
			Core.LogMsg("[空间站鲁滨逊] 出价上移异常: " + ex.Message);
		}
	}

	public static void PostfixGetDealMakerBonus(ref int __result)
	{
		try
		{
			if (RobinCrusoePerk.IsActive())
			{
				__result += RobinCrusoePerk.GetBargainBonusPct();
			}
		}
		catch (System.Exception ex)
		{
			Core.LogMsg("[空间站鲁滨逊] 谈判成功率异常: " + ex.Message);
		}
	}

	public static void PostfixItemFeatureListBargainBuyingMarkup(ref ItemFeature __result)
	{
		try
		{
			if (RobinCrusoePerk.IsActive() && __result != null && RobinCrusoePerk.GetMood() >= 80 && __result.valueModifier > 0)
			{
				__result.valueModifier = (int)((float)__result.valueModifier * 1.15f);
			}
		}
		catch (System.Exception ex)
		{
			Core.LogMsg("[空间站鲁滨逊] 加价接受率异常: " + ex.Message);
		}
	}

	public static void PostfixStoreClientOnDealAccepted(StoreClient __instance)
	{
		try
		{
			if (!RobinCrusoePerk.IsActive())
			{
				return;
			}
			if (__instance != null && _moodBoostedClients.Add((long)__instance.Pointer))
			{
				RobinCrusoePerk.BoostMood(5, LangHelper.T("成交一单", "Deal closed"));
			}
			RobinCrusoePerk.RecordDeal();
			try
			{
				PlayerStore instance = PlayerStore.Instance;
				StoreClientInstance storeClientInstance = null;
				if (instance != null)
				{
					storeClientInstance = instance.currentClientInstance;
				}
				if (storeClientInstance == null || __instance == null)
				{
					return;
				}
				int num = 0;
				try
				{
					System.Reflection.FieldInfo field = storeClientInstance.GetType().GetField("clientIntent", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
					if (field == null)
					{
						field = storeClientInstance.GetType().GetField("intent", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
					}
					if (field != null)
					{
						num = (int)field.GetValue(storeClientInstance);
					}
				}
				catch
				{
				}
				if (num != 1)
				{
					return;
				}
				System.Reflection.FieldInfo field2 = storeClientInstance.GetType().GetField("item", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
				if (field2 == null)
				{
					field2 = storeClientInstance.GetType().GetField("currentItem", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
				}
				if (field2 != null && field2.GetValue(storeClientInstance) is GameItem gameItem)
				{
					try
					{
						RobinCrusoePerk.RecordRevenue((int)gameItem.GetCurrentValue(useRetailMarkup: false, withChild: true, includeEvents: true, forceMarkup: false, 0L));
						return;
					}
					catch
					{
						return;
					}
				}
			}
			catch
			{
			}
		}
		catch
		{
		}
	}

	public static void PostfixGameItemGetValue(GameItem __instance, ref long __result)
	{
		try
		{
			try
			{
				if (__instance != null)
				{
					int tagIntSafe = RobinCrusoePerk.GetTagIntSafe(__instance, "CANNIBALISM_VALUE");
					if (tagIntSafe > 0)
					{
						__result += tagIntSafe;
					}
				}
			}
			catch
			{
			}
			try
			{
				if (CurrentUITradeMode != 2)
				{
					return;
				}
				StoreClient storeClient = null;
				try
				{
					if (PlayerStore.Instance != null && PlayerStore.Instance.currentClientInstance != null)
					{
						storeClient = PlayerStore.Instance.currentClientInstance.GetClientBlueprint();
					}
				}
				catch
				{
				}
				if (storeClient != null && ((storeClient.identifier ?? "") == "inventor" || (storeClient.identifier ?? "") == "inventorStorage" || (storeClient.identifier ?? "") == "inventor_storage"))
				{
					__result = __result * BuildConfig.DoctorBuybackPct / 100;
				}
			}
			catch
			{
			}
		}
		catch (System.Exception ex)
		{
			Core.LogMsg("[之友折扣-GetValue] 失败: " + ex.Message);
		}
	}

	public static void ApplyFriendDiscountToItem(GameItem item)
	{
		try
		{
			if (item == null || item.itemFeatures == null)
			{
				return;
			}
			bool flag = false;
			for (int i = 0; i < item.itemFeatures.Count; i++)
			{
				if (item.itemFeatures[i] != null && item.itemFeatures[i].identifier == "friend_discount")
				{
					flag = true;
					break;
				}
			}
			if (!flag)
			{
				ItemFeature itemFeature = new ItemFeature();
				itemFeature.identifier = "friend_discount";
				itemFeature.featureType = ItemFeature.FeatureType.TemporaryBuying;
				itemFeature.valueStage = ItemFeature.ValueStage.Market;
				itemFeature.valueModifier = 0;
				itemFeature.preExposeValueModifier = -5;
				itemFeature.usePreExposeValue = true;
				itemFeature.initiallyShown = true;
itemFeature.isFeatureMatch = true;
itemFeature.isFeatureExposed = true;
				itemFeature.isExposable = true;
				itemFeature.isFeatureDiscovered = false;
				itemFeature.publicDisplay = LangHelper.T("友情价", "Friend Price");
				itemFeature.actualDisplay = LangHelper.T("友情价", "Friend Price");
				item.itemFeatures.Add(itemFeature);
				long num = 0L;
				try
				{
					num = item.uniqueId;
				}
				catch
				{
				}
				if (num != 0L && !_discountedItems.ContainsKey(num))
				{
					_discountedItems[num] = item;
				}
			}
		}
		catch (System.Exception ex)
		{
			Core.LogMsg("[友情价] 添加失败: " + ex.Message);
		}
	}

	private static void TryApplyFriendDiscountFeature(GameItem item)
	{
		try
		{
			if (item == null || GetFriendBuyDiscount(out var _, out var _) >= 1f || item.itemFeatures == null)
			{
				return;
			}
			bool flag = false;
			for (int i = 0; i < item.itemFeatures.Count; i++)
			{
				if (item.itemFeatures[i] != null && item.itemFeatures[i].identifier == "friend_discount")
				{
					flag = true;
					break;
				}
			}
			if (!flag)
			{
				ItemFeature itemFeature = new ItemFeature();
				itemFeature.identifier = "friend_discount";
				itemFeature.featureType = ItemFeature.FeatureType.TemporaryBuying;
				itemFeature.valueStage = ItemFeature.ValueStage.Market;
				itemFeature.valueModifier = 0;
				itemFeature.preExposeValueModifier = -5;
				itemFeature.usePreExposeValue = true;
				itemFeature.initiallyShown = true;
itemFeature.isFeatureMatch = true;
itemFeature.isFeatureExposed = true;
				itemFeature.isExposable = true;
				itemFeature.isFeatureDiscovered = false;
				itemFeature.publicDisplay = LangHelper.T("友情价", "Friend Price");
				itemFeature.actualDisplay = LangHelper.T("友情价", "Friend Price");
				item.itemFeatures.Add(itemFeature);
				long num = 0L;
				try
				{
					num = item.uniqueId;
				}
				catch
				{
				}
				if (num != 0L && !_discountedItems.ContainsKey(num))
				{
					_discountedItems[num] = item;
				}
			}
		}
		catch (System.Exception ex)
		{
			Core.LogMsg("[友情价] 添加失败: " + ex.Message);
		}
	}

	private static void RemoveFriendDiscountFeatures()
	{
		try
		{
			foreach (System.Collections.Generic.KeyValuePair<long, GameItem> discountedItem in _discountedItems)
			{
				if (discountedItem.Value != null && discountedItem.Value.IsAlreadyContainFeatureWithId("friend_discount"))
				{
					discountedItem.Value.RemoveItemFeatureByID("friend_discount");
				}
			}
			_discountedItems.Clear();
		}
		catch (System.Exception ex)
		{
			Core.LogMsg("[之友折扣] 移除标签失败: " + ex.Message);
		}
	}

	private static void TryAddFoodQualityFeature(GameItem item, int fq)
	{
		try
		{
			if (item == null || item.itemFeatures == null || !RobinCrusoePerk.IsActive())
			{
				return;
			}
			bool flag = RobinCrusoePerk.IsEaten(item);
			string text = "";
			if (fq >= 3)
			{
				text = LangHelper.T("售价：-100%（腐烂）", "Price: -100% (Rotten)");
			}
			else if (fq == 2)
			{
				text = LangHelper.T("售价：-90%（变质）", "Price: -90% (Spoiled)");
			}
			else if (flag)
			{
				text = LangHelper.T("售价：-80%（已食用）", "Price: -80% (Partially Eaten)");
			}
			else if (fq <= 0)
			{
				text = LangHelper.T("售价：+30%（新鲜）", "Price: +30% (Fresh)");
			}
			if (text.Length == 0)
			{
				return;
			}
			for (int i = 0; i < item.itemFeatures.Count; i++)
			{
				if (item.itemFeatures[i] != null && item.itemFeatures[i].identifier == "wages_food_quality")
				{
					item.itemFeatures[i].publicDisplay = text;
					item.itemFeatures[i].actualDisplay = text;
					return;
				}
			}
			ItemFeature itemFeature = new ItemFeature();
			itemFeature.identifier = "wages_food_quality";
			itemFeature.featureType = ItemFeature.FeatureType.TemporarySelling;
			itemFeature.valueStage = ItemFeature.ValueStage.Market;
			itemFeature.valueModifier = 0;
			itemFeature.preExposeValueModifier = 0;
			itemFeature.usePreExposeValue = false;
			itemFeature.initiallyShown = true;
itemFeature.isFeatureMatch = true;
itemFeature.isFeatureExposed = true;
			itemFeature.isExposable = true;
			itemFeature.isFeatureDiscovered = false;
			itemFeature.publicDisplay = text;
			itemFeature.actualDisplay = text;
			item.itemFeatures.Add(itemFeature);
		}
		catch (System.Exception ex)
		{
			Core.LogMsg("[食物品质] feature失败: " + ex.Message);
		}
	}

	internal static void ClearNodeBuffItems()
	{
		_nodeBuffItems.Clear();
	}

	private static void TryAddNodeBuffFeature(GameItem item)
	{
		try
		{
			if (item == null || item.itemFeatures == null || item.IsTag("destiny_dice_tag") || !RobinCrusoePerk.IsActive())
			{
				return;
			}
			string tradeBuffDisplay = RobinCrusoePerk.GetTradeBuffDisplay();
			if (tradeBuffDisplay.Length == 0)
			{
				return;
			}
			long num = 0L;
			try
			{
				num = item.Pointer.ToInt64();
			}
			catch
			{
			}
			for (int i = 0; i < item.itemFeatures.Count; i++)
			{
				if (item.itemFeatures[i] != null && item.itemFeatures[i].identifier == "wages_node_buff")
				{
					item.itemFeatures[i].publicDisplay = tradeBuffDisplay;
					item.itemFeatures[i].actualDisplay = tradeBuffDisplay;
					if (num != 0L)
					{
						_nodeBuffItems.Add(num);
					}
					return;
				}
			}
			ItemFeature itemFeature = new ItemFeature();
			itemFeature.identifier = "wages_node_buff";
			itemFeature.featureType = ItemFeature.FeatureType.TemporarySelling;
			itemFeature.valueStage = ItemFeature.ValueStage.Market;
			itemFeature.valueModifier = 0;
			itemFeature.preExposeValueModifier = 0;
			itemFeature.usePreExposeValue = false;
			itemFeature.initiallyShown = true;
itemFeature.isFeatureMatch = true;
itemFeature.isFeatureExposed = true;
			itemFeature.isExposable = true;
			itemFeature.isFeatureDiscovered = false;
			itemFeature.publicDisplay = tradeBuffDisplay;
			itemFeature.actualDisplay = tradeBuffDisplay;
			item.itemFeatures.Add(itemFeature);
			if (num != 0L)
			{
				_nodeBuffItems.Add(num);
			}
		}
		catch (System.Exception ex)
		{
			Core.LogMsg("[空间站鲁滨逊] 节点buff feature失败: " + ex.Message);
		}
	}

	private static void TryAddTradeFeature(GameItem item, bool isContraband, bool isAlcohol)
	{
		try
		{
			if (item == null)
			{
				return;
			}
			string text = "";
			string text2 = "";
			if (RiskTakerPerk.IsActive() && isContraband)
			{
				text = "risk_taker_markup";
				text2 = LangHelper.T("刀尖舔血加价", "Blade's Edge Markup");
			}
			else if (WineLoverPerk.IsActive() && isAlcohol)
			{
				text = "wine_lover_markup";
				text2 = LangHelper.T("好酒之徒加价", "Wine Lover Markup");
			}
			if (text == "" || item.itemFeatures == null)
			{
				return;
			}
			bool flag = false;
			for (int i = 0; i < item.itemFeatures.Count; i++)
			{
				if (item.itemFeatures[i] != null && item.itemFeatures[i].identifier == text)
				{
					flag = true;
					break;
				}
			}
			if (!flag)
			{
				ItemFeature itemFeature = new ItemFeature();
				itemFeature.identifier = text;
				itemFeature.featureType = ItemFeature.FeatureType.TemporarySelling;
				itemFeature.valueStage = ItemFeature.ValueStage.Market;
				itemFeature.valueModifier = 0;
				int tradePct = (text == "risk_taker_markup") ? 20 : 25;
				itemFeature.preExposeValueModifier = tradePct;
				itemFeature.usePreExposeValue = true;
				itemFeature.initiallyShown = true;
itemFeature.isFeatureMatch = true;
itemFeature.isFeatureExposed = true;
				itemFeature.isExposable = true;
				itemFeature.isFeatureDiscovered = false;
				itemFeature.publicDisplay = text2;
				itemFeature.actualDisplay = text2;
				item.itemFeatures.Add(itemFeature);
				long num = 0L;
				try
				{
					num = item.uniqueId;
				}
				catch
				{
				}
				if (num != 0L && !_tradeFeatureItems.ContainsKey(num))
				{
					_tradeFeatureItems[num] = item;
				}
			}
		}
		catch (System.Exception ex)
		{
			Core.LogMsg("[标签-添加] 失败: " + ex.Message);
		}
	}

	// 通用价格标签：面板"市场与商人"区显示原因行（仿 TryAddTradeFeature/TryAddRobinsonBuyMarkup）
	private static void AddTradeLabel(GameItem item, string labelId, string display, ItemFeature.FeatureType ft, int percent = 0)
	{
		try
		{
			if (item == null || string.IsNullOrEmpty(labelId) || item.itemFeatures == null) return;
			for (int i = 0; i < item.itemFeatures.Count; i++)
			{
				if (item.itemFeatures[i] != null && item.itemFeatures[i].identifier == labelId)
				{
					item.itemFeatures[i].publicDisplay = display;
					item.itemFeatures[i].actualDisplay = display;
					return;
				}
			}
			ItemFeature f = new ItemFeature();
			f.identifier = labelId;
			f.featureType = ft;
			f.valueStage = ItemFeature.ValueStage.Market;
			f.valueModifier = 0;
			f.preExposeValueModifier = percent;
			f.usePreExposeValue = percent != 0;
			f.initiallyShown = true;
f.isFeatureMatch = true;
f.isFeatureExposed = true;
			f.isExposable = true;
			f.isFeatureDiscovered = false;
			f.publicDisplay = display;
			f.actualDisplay = display;
			item.itemFeatures.Add(f);
		}
		catch { }
	}

	private static void TryAddRobinsonBuyMarkup(GameItem item)
	{
		try
		{
			if (item == null || item.itemFeatures == null || item.IsTag("destiny_dice_tag"))
			{
				return;
			}
			string text = LangHelper.T("鲁滨逊·口粮双倍价", "Robinson·Ration x2");
			for (int i = 0; i < item.itemFeatures.Count; i++)
			{
				if (item.itemFeatures[i] != null && item.itemFeatures[i].identifier == "wages_robin_buy")
				{
					item.itemFeatures[i].publicDisplay = text;
					item.itemFeatures[i].actualDisplay = text;
					return;
				}
			}
			ItemFeature itemFeature = new ItemFeature();
			itemFeature.identifier = "wages_robin_buy";
			itemFeature.featureType = ItemFeature.FeatureType.TemporaryBuying;
			itemFeature.valueStage = ItemFeature.ValueStage.Market;
			itemFeature.valueModifier = 0;
			itemFeature.preExposeValueModifier = 0;
			itemFeature.usePreExposeValue = false;
			itemFeature.initiallyShown = true;
itemFeature.isFeatureMatch = true;
itemFeature.isFeatureExposed = true;
			itemFeature.isExposable = true;
			itemFeature.isFeatureDiscovered = false;
			itemFeature.publicDisplay = text;
			itemFeature.actualDisplay = text;
			item.itemFeatures.Add(itemFeature);
		}
		catch
		{
		}
	}

	private static void TryAddBadReputationFeature(GameItem item, int modifier)
	{
		try
		{
			if (item == null || item.itemFeatures == null)
			{
				return;
			}
			for (int i = 0; i < item.itemFeatures.Count; i++)
			{
				if (item.itemFeatures[i] != null && item.itemFeatures[i].identifier == "bad_reputation")
				{
					item.itemFeatures[i].preExposeValueModifier = modifier;
					item.itemFeatures[i].usePreExposeValue = true;
					return;
				}
			}
			ItemFeature itemFeature = new ItemFeature();
			itemFeature.identifier = "bad_reputation";
			itemFeature.featureType = ((modifier < 0) ? ItemFeature.FeatureType.TemporarySelling : ItemFeature.FeatureType.TemporaryBuying);
			itemFeature.valueStage = ItemFeature.ValueStage.Market;
			itemFeature.valueModifier = 0;
			itemFeature.preExposeValueModifier = modifier;
			itemFeature.usePreExposeValue = true;
			itemFeature.initiallyShown = true;
itemFeature.isFeatureMatch = true;
itemFeature.isFeatureExposed = true;
			itemFeature.isExposable = true;
			itemFeature.isFeatureDiscovered = false;
			itemFeature.publicDisplay = LangHelper.T("信誉扫地", "Bad Reputation");
			itemFeature.actualDisplay = LangHelper.T("信誉扫地", "Bad Reputation");
			item.itemFeatures.Add(itemFeature);
			long num = 0L;
			try
			{
				num = item.uniqueId;
			}
			catch
			{
			}
			if (num != 0L && !_tradeFeatureItems.ContainsKey(num))
			{
				_tradeFeatureItems[num] = item;
			}
		}
		catch (System.Exception ex)
		{
			Core.LogMsg("[信誉扫地] 加标签失败: " + ex.Message);
		}
	}

	private static void RemoveTradeFeatures()
	{
		try
		{
			foreach (System.Collections.Generic.KeyValuePair<long, GameItem> tradeFeatureItem in _tradeFeatureItems)
			{
				if (tradeFeatureItem.Value == null)
				{
					continue;
				}
				try
				{
					if (tradeFeatureItem.Value.IsAlreadyContainFeatureWithId("刀尖舔血"))
					{
						tradeFeatureItem.Value.RemoveItemFeatureByID("刀尖舔血");
					}
				}
				catch
				{
				}
				try
				{
					if (tradeFeatureItem.Value.IsAlreadyContainFeatureWithId("好酒之徒"))
					{
						tradeFeatureItem.Value.RemoveItemFeatureByID("好酒之徒");
					}
				}
				catch
				{
				}
				try
				{
					if (tradeFeatureItem.Value.IsAlreadyContainFeatureWithId("信誉扫地"))
					{
						tradeFeatureItem.Value.RemoveItemFeatureByID("信誉扫地");
					}
				}
				catch
				{
				}
				try
				{
					if (tradeFeatureItem.Value.IsAlreadyContainFeatureWithId("bad_reputation"))
					{
						tradeFeatureItem.Value.RemoveItemFeatureByID("bad_reputation");
					}
				}
				catch
				{
				}
			}
			_tradeFeatureItems.Clear();
		}
		catch (System.Exception ex)
		{
			Core.LogMsg("[加价标签] 移除失败: " + ex.Message);
		}
	}

	private static float GetFriendBuyDiscount(out string clientName, out string clientId)
	{
		clientName = "";
		clientId = "";
		try
		{
			StoreClient currentClient = SpecialNpcManager.GetCurrentClient();
			if (currentClient == null)
			{
				return 1f;
			}
			clientId = currentClient.identifier ?? "";
			clientName = currentClient.displayName ?? "";
			if (clientId == "retired_gunsmith" && RetiredGunsmithPerk.IsActive())
			{
				return 0.95f;
			}
			if (clientId == "retired_water_merchant" && WaterMerchantPerk.IsActive())
			{
				return 0.95f;
			}
			if (clientId == "retired_winemaker" && AlcoholMerchantPerk.IsActive())
			{
				return 0.95f;
			}
			if (DrJacksonFriendPerk.IsActive() && (clientName.Contains("博士") || clientId.Contains("jackson") || clientId.Contains("inventor")))
			{
				return 0.95f;
			}
		}
		catch (System.Exception ex)
		{
			Core.LogMsg("[之友折扣] 判断失败: " + ex.Message);
		}
		return 1f;
	}

	public static void PostfixDealMakerBonus(ref int __result)
	{
		// 09-19 童叟无欺重做：移除议价 +25 效果（蛙娘在场 +50 在 WageGirlSystem）
	}

	public static void PostfixTradeRepMultiplier(ref double __result)
	{
		try
		{
			if (SmilingFacePerk.IsActive())
			{
				__result *= 0.75; // 笑面虎：声誉获取 -25%（混合特性代价）
			}
			else if (SmilingTigerPerk.IsActive())
			{
				__result *= 1.25; // 童叟无欺：声誉 +25%
			}
		}
		catch (System.Exception ex)
		{
			Core.LogMsg("[童叟无欺] 声誉补丁失败: " + ex.Message);
		}
	}

	public static void PostfixGameItemGetDisplayName(GameItem __instance, ref string __result)
	{
		try
		{
			if (__instance == null)
			{
				return;
			}
			string identifier = __instance.identifier;
			if (identifier == "wine_bottle")
			{
				string name = __instance.name;
				if (name != null)
				{
					if (name.Contains("荧光莓果酿"))
					{
						__result = "荧光莓果酿";
						return;
					}
					if (name.Contains("暗影莓果酿"))
					{
						__result = "暗影莓果酿";
						return;
					}
				}
				__result = LangHelper.T("酒瓶", "Wine Bottle");
			}
			else
			{
				string itemDisplayNameOverride = GetItemDisplayNameOverride(identifier);
				if (itemDisplayNameOverride != null)
				{
					__result = itemDisplayNameOverride;
				}
			}
		}
		catch (System.Exception ex)
		{
			Core.LogMsg("[GetDisplayName] 异常: " + ex.Message);
		}
	}

	private static string GetItemDisplayNameOverride(string id)
	{
		return id switch
		{
			"wine_bloomberry" => LangHelper.T("荧光莓果酿", "Glowberry Wine"), 
			"wine_gloomberry" => LangHelper.T("暗影莓果酿", "Gloomberry Wine"), 
			"wine_berry" => LangHelper.T("莓果酿", "Berry Wine"), 
			"wine_bottle" => LangHelper.T("酒瓶", "Wine Bottle"), 
			"beer_case" => LangHelper.T("一箱啤酒", "Case of Beer"), 
			"red_beer" => LangHelper.T("红魔鬼啤酒", "Red Devil Beer"), 
			"empty_beer_bottle" => LangHelper.T("空啤酒瓶", "Empty Beer Bottle"), 
			"wine_superyeast" => LangHelper.T("超级酵母", "Super Yeast"), 
			"wine_yeast" => LangHelper.T("酿酒酵母", "Brewing Yeast"), 
			"wine_yeast_infinite" => LangHelper.T("永续酵母", "Perpetual Yeast"), 
			"wine_yeast_red" => LangHelper.T("红酵母", "Red Yeast"), 
			"permit_gun_1" => LangHelper.T("枪支许可证（一级）", "Gun Permit (Tier 1)"), 
			"permit_gun_2" => LangHelper.T("枪支许可证（二级）", "Gun Permit (Tier 2)"), 
			"permit_gun_3" => LangHelper.T("枪支许可证（三级）", "Gun Permit (Tier 3)"), 
			"blank_keycard" => LangHelper.T("空白钥匙卡", "Blank Keycard"), 
			_ => null, 
		};
	}

	public static void PostfixGetDisplayNameFromID(string itemID, ref string __result)
	{
		try
		{
			if (string.IsNullOrEmpty(itemID))
			{
				return;
			}
			string itemDisplayNameOverride = GetItemDisplayNameOverride(itemID);
			if (itemDisplayNameOverride != null)
			{
				switch (itemID)
				{
				case "beer_case":
				case "wine_berry":
				case "wine_gloomberry":
					__result = itemDisplayNameOverride;
					break;
				}
			}
		}
		catch
		{
		}
	}

	public static void PostfixGetLocalizedItem(string key, ref string __result)
	{
		try
		{
			if (string.IsNullOrEmpty(key))
			{
				return;
			}
			if (!key.Contains("beer_case") && !key.Contains("wine_") && !key.Contains("permit_gun"))
			{
				key.Contains("blank_keycard");
			}
			if (string.IsNullOrEmpty(__result) || __result.Trim() == "?")
			{
				string localizedItemOverride = GetLocalizedItemOverride(key);
				if (localizedItemOverride != null)
				{
					__result = localizedItemOverride;
				}
			}
		}
		catch
		{
		}
	}

	private static string GetLocalizedItemOverride(string key)
	{
		if (string.IsNullOrEmpty(key))
		{
			return null;
		}
		string itemDisplayNameOverride = GetItemDisplayNameOverride(key);
		if (itemDisplayNameOverride != null)
		{
			return itemDisplayNameOverride;
		}
		string[] array = new string[14]
		{
			"wine_yeast_infinite", "wine_yeast_red", "wine_bloomberry", "wine_gloomberry", "empty_beer_bottle", "permit_gun_1", "permit_gun_2", "permit_gun_3", "blank_keycard", "wine_yeast",
			"wine_berry", "wine_bottle", "beer_case", "red_beer"
		};
		foreach (string text in array)
		{
			if (key.IndexOf(text, System.StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return GetItemDisplayNameOverride(text);
			}
		}
		return null;
	}

	public static void PostfixGetLocalizedName(string key, ref string __result)
	{
		TryCoverLocalizationKey(key, ref __result);
	}

	public static void PostfixGetLocalizedUI(string key, ref string __result)
	{
		TryCoverLocalizationKey(key, ref __result);
	}

	private static void TryCoverLocalizationKey(string key, ref string __result)
	{
		try
		{
			if (!string.IsNullOrEmpty(key) && (key.Contains("beer_case") || key.Contains("wine_") || key.Contains("permit_gun") || key.Contains("blank_keycard")) && (string.IsNullOrEmpty(__result) || __result.Trim() == "?"))
			{
				string localizedItemOverride = GetLocalizedItemOverride(key);
				if (localizedItemOverride != null)
				{
					__result = localizedItemOverride;
				}
			}
		}
		catch
		{
		}
	}

	private static void InitNegocCache()
	{
		if (!_negocCacheInit)
		{
			System.Type typeFromHandle = typeof(NegociationUIManager);
			_negocInstanceProp = typeFromHandle.GetProperty("Instance", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
			_negocInstanceField = typeFromHandle.GetField("Instance", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
			_negocCacheInit = true;
		}
	}

	private static void FixTradeItemName(GameItem item)
	{
		try
		{
			if (item == null)
			{
				return;
			}
			string itemDisplayNameOverride = GetItemDisplayNameOverride(item.identifier);
			if (itemDisplayNameOverride == null)
			{
				return;
			}
			InitNegocCache();
			object obj = null;
			if (_negocInstanceProp != null)
			{
				obj = _negocInstanceProp.GetValue(null);
			}
			if (obj == null && _negocInstanceField != null)
			{
				obj = _negocInstanceField.GetValue(null);
			}
			if (obj == null)
			{
				return;
			}
			System.Type type = obj.GetType();
			if (!_itemNameFieldCache.TryGetValue(type, out var value))
			{
				value = type.GetField("itemName", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
				_itemNameFieldCache[type] = value;
			}
			if (value == null)
			{
				return;
			}
			object value2 = value.GetValue(obj);
			if (value2 != null)
			{
				System.Type type2 = value2.GetType();
				if (!_textPropCache.TryGetValue(type2, out var value3))
				{
					value3 = type2.GetProperty("text");
					_textPropCache[type2] = value3;
				}
				if (value3 != null)
				{
					value3.SetValue(value2, itemDisplayNameOverride);
				}
			}
		}
		catch (System.Exception ex)
		{
			Core.LogMsg("[名称修复] 失败: " + ex.Message);
		}
	}

	public static void PostfixGenerateLocalizedString(object tableEntryReference, ref string __result)
	{
		try
		{
			if (tableEntryReference == null)
			{
				return;
			}
			string text = null;
			try
			{
				System.Reflection.PropertyInfo property = tableEntryReference.GetType().GetProperty("Key");
				if (property != null)
				{
					text = property.GetValue(tableEntryReference) as string;
				}
			}
			catch
			{
			}
			if (!string.IsNullOrEmpty(text) && (text.Contains("beer_case") || text.Contains("wine_") || text.Contains("permit_gun") || text.Contains("blank_keycard")) && (string.IsNullOrEmpty(__result) || __result.Trim() == "?"))
			{
				string localizedItemOverride = GetLocalizedItemOverride(text);
				if (localizedItemOverride != null)
				{
					__result = localizedItemOverride;
				}
			}
		}
		catch
		{
		}
	}

	public static void PostfixOnLoadGame()
	{
		try
		{
			try { WageGirlSystem.ClearMemStats(); } catch { } // 09-20 修：读档清蛙娘内存缓存
			try { RobinCrusoePerk.ClearMemBlood(); } catch { } // 09-20 修：读档清鲁滨逊血量缓存
			try { RobinCrusoePerk.ClearWantedQueued(); } catch { } // 09-20 修：读档清供应商排期标记
			_pendingLoadGameRestore = true;
			_loadGameRestoreDelayFrames = 30;
		}
		catch (System.Exception ex)
		{
			Core.LogMsg("[LoadGame] PostfixOnLoadGame异常: " + ex.Message);
		}
	}

	public static void UpdatePendingLoadGameRestore()
	{
		if (!_pendingLoadGameRestore)
		{
			return;
		}
		if (_loadGameRestoreDelayFrames > 0)
		{
			_loadGameRestoreDelayFrames--;
			return;
		}
		_pendingLoadGameRestore = false;
		try
		{
			NewStartTypeUI.RecheckIfPending(); // 09-22 runID 已恢复：清 IsMarkedRun 挂起标记（后续判定自然重判）
			if (FrogPowerPerk.IsActive())
			{
				FrogPowerPerk.LoadState();
			}
			if (WineLoverPerk.IsActive())
			{
				WineLoverPerk.LoadHangoverState();
			}
			if (DrJacksonFriendPerk.IsActive() && PlayerStore.Instance != null)
			{
				ScheduleJacksonToday();
			}
		}
		catch (System.Exception ex)
		{
			Core.LogMsg("[博士之友] LoadGame QueueFuturClient失败: " + ex.Message);
		}
	}

	public static void PostfixInitDirectory(ItemDirectory __instance)
	{
		try
		{
			CustomStorageContainer.RegisterToDirectory(__instance);
		}
		catch (System.Exception ex)
		{
			Core.LogMsg("[自定义储物箱] PostfixInitDirectory: " + ex.Message);
		}
		try
		{
			LuckScoutBackpackUpgrade.RegisterToDirectory(__instance);
		}
		catch (System.Exception ex2)
		{
			Core.LogMsg("[虚空珠] PostfixInitDirectory: " + ex2.Message);
		}
		try
		{
			GuMachineSystem.RegisterToDirectory(__instance);
			WageGirlSystem.RegisterToDirectory(__instance); // 09-21 蛙娘实体
		}
		catch (System.Exception ex3)
		{
			Core.LogMsg("[养蛊机] PostfixInitDirectory: " + ex3.Message);
		}
		try
		{
		}
		catch (System.Exception ex4)
		{
			Core.LogMsg("[采血包] PostfixInitDirectory: " + ex4.Message);
		}
	}

	public static void PostfixTradeSheetFoundryShop(object __result)
	{
		DrJacksonFriendPerk.FoundryShopPatch.Postfix(__result);
	}

	public static void PostfixTradeSheetEnergyFarmShop(object __result)
	{
		DrJacksonFriendPerk.EnergyFarmShopPatch.Postfix(__result);
	}

	public static void PrefixEmporiumShowAfterhourInv(EmporiumEntry __instance)
	{
		DrJacksonFriendPerk.EmporiumEntryShowAfterhourPatch.Prefix(__instance);
	}

	public static bool PrefixCreateAcidBottle(ref GameItem __result)
	{
		__result = null;
		return false;
	}

	public static bool PrefixCreateBaseBottle(ref GameItem __result)
	{
		__result = null;
		return false;
	}

	public static void PostfixStartOfDayOpenUI(StartOfDayUIManager __instance)
	{
		TryAppendNightReport(__instance);
	}

	public static void PostfixStartOfDayButtonClicked(StartOfDayUIManager __instance)
	{
		TryAppendNightReport(__instance);
	}

	public static void PostfixStartOfDayShowMorningReport(StartOfDayUIManager __instance)
	{
		TryAppendNightReport(__instance);
	}

	private static void TryAppendNightReport(StartOfDayUIManager __instance)
	{
		try
		{
			if (Core.NightReportQueue.Count == 0)
			{
				return;
			}
						string text = string.Join("\n", Core.NightReportQueue.ToArray());
			if (__instance == null || __instance.startOfDayTMPPrefab == null || __instance.contentGroupObject == null)
			{
				Core.LogMsg("[夜间报告] 报告UI未就绪，无法追加（队列保留不丢）"); // 09-20 拆包：Clear 移到判空后——UI 未就绪时不清队列（原清丢内容）
				return;
			}
			Core.NightReportQueue.Clear();
			GameObject gameObject = UnityEngine.Object.Instantiate(__instance.startOfDayTMPPrefab, __instance.contentGroupObject.transform);
			if (!(gameObject != null))
			{
				return;
			}
			gameObject.SetActive(value: true);
			bool flag = false;
			try
			{
				Component[] array = gameObject.GetComponentsInChildren<Component>(includeInactive: true);
				if (array != null)
				{
					foreach (Component component in array)
					{
						if (component == null)
						{
							continue;
						}
						string text2 = "";
						try
						{
							text2 = component.GetIl2CppType().FullName ?? "";
						}
						catch
						{
						}
						if (string.IsNullOrEmpty(text2))
						{
							try
							{
								text2 = component.GetType().Name ?? "";
							}
							catch
							{
							}
						}
						if (!text2.Contains("TextMeshProUGUI") && !text2.Contains("TMP_Text"))
						{
							continue;
						}
						bool flag2 = false;
						try
						{
							System.Reflection.MethodInfo[] methods = component.GetType().GetMethods();
							if (methods != null)
							{
								foreach (System.Reflection.MethodInfo methodInfo in methods)
								{
									if (methodInfo == null)
									{
										continue;
									}
									string text3 = methodInfo.Name ?? "";
									if (text3 == "set_text" || text3 == "SetText")
									{
										System.Reflection.ParameterInfo[] parameters = methodInfo.GetParameters();
										if (parameters != null && parameters.Length == 1)
										{
											methodInfo.Invoke(component, new object[1] { text });
											flag2 = true;
											break;
										}
									}
								}
							}
						}
						catch (System.Exception ex)
						{
							Core.LogMsg("[夜间报告] set_text 调用失败: " + ex.Message);
						}
						if (flag2)
						{
							flag = true;
							break;
						}
					}
				}
			}
			catch (System.Exception ex2)
			{
				Core.LogMsg("[夜间报告] 反射设置文本失败: " + ex2.Message);
			}
			if (flag)
			{
				return;
			}
			Core.LogMsg("[夜间报告] 未找到TMP文本组件，无法设置文本");
			try
			{
				Component[] array2 = gameObject.GetComponentsInChildren<Component>(includeInactive: true);
				if (array2 == null)
				{
					return;
				}
				foreach (Component component2 in array2)
				{
					if (component2 == null)
					{
						continue;
					}
					try
					{
						if (component2.GetIl2CppType().FullName == null)
						{
							_ = component2.GetType().Name;
						}
					}
					catch
					{
					}
					try
					{
						if (component2.gameObject != null)
						{
							_ = component2.gameObject.name;
						}
					}
					catch
					{
					}
				}
			}
			catch (System.Exception ex3)
			{
				Core.LogMsg("[夜间报告] 诊断dump失败: " + ex3.Message);
			}
		}
		catch (System.Exception ex4)
		{
			Core.LogMsg("[夜间报告] 追加失败: " + ex4.Message);
		}
	}

	public static bool PrefixLoadFromAtlas(string atlasPath, string name, ref Sprite __result)
	{
		try
		{
			if (name == "custom_storage_box_sprite")
			{
				Sprite customSprite = CustomStorageContainer.GetCustomSprite();
				if (customSprite != null)
				{
					__result = customSprite;
					return false;
				}
			}
		}
		catch (System.Exception ex)
		{
			Core.LogMsg("[自定义sprite] PrefixLoadFromAtlas异常: " + ex.Message);
		}
		return true;
	}

	public static void PrefixOnPointerClick(StartingPerkElement __instance)
	{
		try
		{
			NewGameData instance = NewGameData.Instance;
			if (instance != null && !instance.isInMainMenu)
			{
				instance.isInMainMenu = true;
			}
		}
		catch (System.Exception ex)
		{
			Core.LogMsg("[选点] Prefix异常: " + ex.Message);
		}
	}

	public static void PostfixOnPointerClick(StartingPerkElement __instance)
	{
	}
	public static void PrefixModReputation(ref double modValue)
	{
		try
		{
			if (modValue < 0) modValue = modValue / 2.0; // 精确减半：-3→-1.5、-5→-2.5、-7→-3.5、-10→-5
		}
		catch (System.Exception ex)
		{
			Core.LogMsg("[信誉减半] Prefix异常: " + ex.Message);
		}
	}


	// 09-22 制卡降上城区声望根因修复：mod 违禁品跳过"客户曝光"链（ClientExposeFeature）
	// 曝光链（拆包实锤）：PlacedItemForBuying → CanClientExposeAnyFeature → ClientExposeFeature →
	//   CanClientExposeThisFeature → 对话 + StoreReputation.ModReputation(客户faction, -4, true) + ExposeFeature(词条移除)
	// mod 违禁品（wage_ 前缀 / 吞噬融合 CANNIBALISM_VALUE / 电池融合 BREEDER_POWER_SOURCE_ITEM_TAG）
	// 被客户浏览即曝光 → 扣该客户 faction 声望 -4 + 词条划掉。跳过曝光：不扣声望、词条保留、违禁品打标保留。
	public static bool PrefixClientExposeFeature(GameItem gameItem)
	{
		try
		{
			if (gameItem == null) return true;
			string id = "";
			try { id = gameItem.identifier ?? ""; } catch { }
			bool isWage = id.StartsWith("wage_") || gameItem.IsTag("CANNIBALISM_VALUE") || gameItem.IsTag("BREEDER_POWER_SOURCE_ITEM_TAG");
			if (isWage)
			{
				Core.LogMsg("[声望修复] " + id + " 是 mod 物品，跳过客户曝光（不再扣声望/划词条）");
				return false;
			}
		}
		catch (System.Exception ex)
		{
			Core.LogMsg("[声望修复] Prefix异常: " + ex.Message);
		}
		return true;
	}
	// 诊断（用完删）：static ModReputation(String,int,bool) 日志——验证曝光扣声望走 static 版（value=-4）
	public static void PrefixModReputationStatic(string factionId, int value)
	{
		try { Core.LogMsg("[声望诊断] ModReputationStatic faction=" + factionId + " value=" + value); } catch { }
	}}internal static class ModCannibalism
{
	private static readonly string[] MOD_TAGS = new string[3] { "BONUS_PERCENTAGE_PERFORMANCE_INT", "BONUS_PERCENTAGE_EFFICIENCY_INT", "BONUS_PERCENTAGE_QUALITY_INT" };

	internal static string LastNewsLine = null;

	private static bool _nightTabInserted = false;

	public static void OnDayStartPostfix()
	{
		try
		{
			if (!RiskTakerPerk.IsActive())
			{
				return;
			}
			int num = DeterministicSchedule.CurrentDay % BuildConfig.CannibalInterval;
			if (num == 0)
			{
				string text = LangHelper.T("吞噬季开始：机器里的模组将互相吞噬，持续 3 天", "Cannibalism Season begins: modules in machines will devour each other for 3 days");
				try
				{
					StoreUIManager.Instance.Notify(text);
				}
				catch
				{
				}
				LastNewsLine = text;
			}
			if (num > 2)
			{
				return;
			}
			System.Collections.Generic.List<System.Tuple<GameItem, System.Collections.Generic.List<GameItem>>> list = CollectMachines();
			System.Collections.Generic.List<string> list2 = new System.Collections.Generic.List<string>();
			foreach (System.Tuple<GameItem, System.Collections.Generic.List<GameItem>> item2 in list)
			{
				System.Collections.Generic.List<GameItem> item = item2.Item2;
				if (item == null || item.Count < 2)
				{
					continue;
				}
				GameItem gameItem = null;
				int num2 = -1;
				foreach (GameItem item3 in item)
				{
					int num3 = 0;
					num3 += RobinCrusoePerk.GetTagIntSafe(item3, "BONUS_PERCENTAGE_PERFORMANCE_INT");
					num3 += RobinCrusoePerk.GetTagIntSafe(item3, "BONUS_PERCENTAGE_EFFICIENCY_INT");
					num3 += RobinCrusoePerk.GetTagIntSafe(item3, "BONUS_PERCENTAGE_QUALITY_INT");
					if (num3 > num2)
					{
						num2 = num3;
						gameItem = item3;
					}
				}
				if (gameItem == null)
				{
					gameItem = item[0];
				}
				System.Collections.Generic.List<GameItem> list3 = new System.Collections.Generic.List<GameItem>();
				foreach (GameItem item4 in item)
				{
					if (item4 != gameItem)
					{
						list3.Add(item4);
					}
				}
				GameItem gameItem2 = list3[Core.Rng.Next(list3.Count)];
				int cannibalAbsorbPct = BuildConfig.CannibalAbsorbPct;
				System.Collections.Generic.List<string> list4 = new System.Collections.Generic.List<string>();
				string[] mOD_TAGS = MOD_TAGS;
				foreach (string text2 in mOD_TAGS)
				{
					int tagIntSafe = RobinCrusoePerk.GetTagIntSafe(gameItem2, text2);
					if (tagIntSafe <= 0)
					{
						continue;
					}
					int tagIntSafe2 = RobinCrusoePerk.GetTagIntSafe(gameItem, text2);
					int num4 = BuildConfig.CannibalCap - tagIntSafe2;
					if (num4 > 0)
					{
						int num5 = (int)((float)(tagIntSafe * cannibalAbsorbPct) / 100f);
						if (num5 > num4)
						{
							num5 = num4;
						}
						if (num5 > 0)
						{
							RobinCrusoePerk.AddTagInt(gameItem, text2, num5);
							RobinCrusoePerk.AddTagInt(gameItem, text2.Replace("BONUS_PERCENTAGE_", "CANNIBALISM_"), num5);
							list4.Add(text2.Replace("BONUS_PERCENTAGE_", "").Replace("_INT", "") + "+" + num5);
						}
					}
				}
				try
				{
					long currentValue = gameItem2.GetCurrentValue(useRetailMarkup: false, withChild: true, includeEvents: true, forceMarkup: false, 0L);
					if (currentValue > 0)
					{
						int num6 = (int)currentValue;
						try
						{
							ModuleEffectHelper.AdjustValue(gameItem, num6);
						}
						catch
						{
						}
						RobinCrusoePerk.AddTagInt(gameItem, "CANNIBALISM_VALUE", num6);
						list4.Add("价值+" + num6);
					}
				}
				catch
				{
				}
				try
				{
					ContrabandHelper.InitContrabandItem(gameItem, 3);
				}
				catch
				{
				}
				try
				{
					string text3 = gameItem.shortDescription ?? "";
					if (!text3.Contains("违禁原因"))
					{
						gameItem.shortDescription = text3 + LangHelper.T("（违禁原因：吞噬融合的强化模组）", " (Contraband: devour-fused empowered module)");
					}
				}
				catch
				{
				}
				string name = GetName(gameItem);
				string name2 = GetName(gameItem2);
				try
				{
					GameItem gameItem3 = null;
					try
					{
						gameItem3 = DirectoryMaster.Item("system_module_ruined");
					}
					catch
					{
					}
					if (gameItem3 != null)
					{
						try
						{
							GraphNodeStorage graphNodeStorage = gameItem2.parentInventory.Cast<GraphNodeStorage>();
							if (graphNodeStorage != null)
							{
								GraphUtils.TryAcceptAll(graphNodeStorage, gameItem3);
							}
						}
						catch
						{
						}
					}
				}
				catch
				{
				}
				try
				{
					gameItem2.parentInventory?.Expel(gameItem2);
				}
				catch
				{
				}
				try
				{
					gameItem2.Destroy();
				}
				catch
				{
				}
				try
				{
					gameItem2.Destroy();
				}
				catch
				{
				}
				string text4 = ((list4.Count > 0) ? string.Join(" ", list4) : LangHelper.T("（无属性转移）", "(no stats transferred)"));
				string text5 = LangHelper.T(name + " 吞噬了 " + name2 + " · " + text4 + "（吸收 10%，留报废模组）", name + " devoured " + name2 + " · " + text4 + " (10% absorbed, A scrapped)");
				list2.Add(text5);
				try
				{
					StoreUIManager.Instance.Notify(text5);
				}
				catch
				{
				}
				LastNewsLine = list2[list2.Count - 1];
				try
				{
					PlayerStore instance = PlayerStore.Instance;
					if (instance == null)
					{
						continue;
					}
					foreach (string item5 in list2)
					{
						instance.AddNightLog(item5, "#7FC97F");
					}
				}
				catch
				{
				}
			}
			if (num == 2)
			{
				string text6 = LangHelper.T("吞噬季今晚结束", "Cannibalism Season ends tonight");
				try
				{
					StoreUIManager.Instance.Notify(text6);
				}
				catch
				{
				}
				LastNewsLine = text6;
			}
		}
		catch
		{
		}
	}

	private static System.Collections.Generic.List<System.Tuple<GameItem, System.Collections.Generic.List<GameItem>>> CollectMachines()
	{
		System.Collections.Generic.List<System.Tuple<GameItem, System.Collections.Generic.List<GameItem>>> list = new System.Collections.Generic.List<System.Tuple<GameItem, System.Collections.Generic.List<GameItem>>>();
		try
		{
			System.Collections.Generic.List<GameItem> list2 = new System.Collections.Generic.List<GameItem>();
			try
			{
				Il2CppSystem.Collections.Generic.List<GameItem> list3 = PlayerStore.Instance.FindAllItem();
				if (list3 != null)
				{
					Il2CppSystem.Collections.Generic.List<GameItem>.Enumerator enumerator = list3.GetEnumerator();
					while (enumerator.MoveNext())
					{
						GameItem current = enumerator.Current;
						if (current != null)
						{
							list2.Add(current);
						}
					}
				}
			}
			catch
			{
			}
			try
			{
				_ = PlayerStore.Instance;
			}
			catch
			{
			}
			foreach (GameItem item in list2)
			{
				if (item == null || !RobinCrusoePerk.IsMachine(item))
				{
					continue;
				}
				GameGridInventory moduleInv = MachineHelper.GetModuleInv(item);
				if (moduleInv == null || moduleInv.childItems == null)
				{
					continue;
				}
				System.Collections.Generic.List<GameItem> list4 = new System.Collections.Generic.List<GameItem>();
				Il2CppSystem.Collections.Generic.List<GameItem>.Enumerator enumerator = moduleInv.childItems.GetEnumerator();
				while (enumerator.MoveNext())
				{
					GameItem current3 = enumerator.Current;
					bool flag = false;
					string text = "";
					try
					{
						text = ((current3 != null) ? (current3.identifier ?? "") : "");
					}
					catch
					{
					}
					if (!(text == "system_module_ruined"))
					{
						try
						{
							flag = (current3?.IsTag("MODULE_TAG") ?? false) && !RobinCrusoePerk.IsExcludedModule(current3); // 09-20 P2-4 排除熔炉模组
						}
						catch
						{
							flag = false;
						}
						if (flag)
						{
							list4.Add(current3);
						}
					}
				}
				if (list4.Count >= 2)
				{
					list.Add(new System.Tuple<GameItem, System.Collections.Generic.List<GameItem>>(item, list4));
				}
			}
		}
		catch
		{
		}
		return list;
	}

	public static void PostfixCreateModuleTooltip(RichTextBuilder builder, GameItem item)
	{
		if (item != null && item.IsTag("SCRAPPED_MODULE_TAG"))
		{
			try
			{
				builder.AddLine(LangHelper.T("◆ 报废模组（已失去功能）", "◆ Scrapped module (non-functional)"));
				return;
			}
			catch
			{
				return;
			}
		}
		try
		{
			if (builder != null && item != null && item.IsTag("MODULE_TAG") && !RobinCrusoePerk.IsExcludedModule(item)) // 09-20 P2-4 排除熔炉模组
			{
				int tagIntSafe = RobinCrusoePerk.GetTagIntSafe(item, "CANNIBALISM_PERFORMANCE_INT");
				int tagIntSafe2 = RobinCrusoePerk.GetTagIntSafe(item, "CANNIBALISM_EFFICIENCY_INT");
				int tagIntSafe3 = RobinCrusoePerk.GetTagIntSafe(item, "CANNIBALISM_QUALITY_INT");
				int tagIntSafe4 = RobinCrusoePerk.GetTagIntSafe(item, "CANNIBALISM_VALUE");
				if (tagIntSafe > 0 || tagIntSafe2 > 0 || tagIntSafe3 > 0 || tagIntSafe4 > 0)
				{
					builder.AddLine(LangHelper.T("◆ 吞噬叠加：性能+" + tagIntSafe + "% 效率+" + tagIntSafe2 + "% 质量+" + tagIntSafe3 + "% 价值+" + tagIntSafe4, "◆ Devoured: Perf +" + tagIntSafe + "% Eff +" + tagIntSafe2 + "% Qual +" + tagIntSafe3 + "% Value +" + tagIntSafe4));
				}
			}
		}
		catch
		{
		}
	}

	private static void TryWriteCannibalismTabLine(AdvCalendarUIManager __instance, string sectionField)
	{
		try
		{
			if (string.IsNullOrEmpty(LastNewsLine) || _nightTabInserted)
			{
				return;
			}
			string lastNewsLine = LastNewsLine;
			LastNewsLine = null;
			GameObject gameObject = null;
			try
			{
				gameObject = Traverse.Create(__instance).Field(sectionField).GetValue<GameObject>();
			}
			catch
			{
			}
			GameObject original = null;
			try
			{
				StartOfDayUIManager instance = StartOfDayUIManager.Instance;
				if (instance != null)
				{
					original = instance.startOfDayTMPPrefab;
				}
			}
			catch
			{
			}
			try
			{
				PlayerStore.Instance?.AddNightLog(lastNewsLine, "#7FC97F");
			}
			catch
			{
			}
			_nightTabInserted = true;
			GameObject gameObject2 = UnityEngine.Object.Instantiate(original, gameObject.transform);
			if (gameObject2 == null)
			{
				_nightTabInserted = true;
				return;
			}
			gameObject2.SetActive(value: true);
			try
			{
				Component[] array = gameObject2.GetComponentsInChildren<Component>(includeInactive: true);
				if (array != null)
				{
					foreach (Component component in array)
					{
						if (component == null)
						{
							continue;
						}
						string text = "";
						try
						{
							text = component.GetIl2CppType().FullName ?? "";
						}
						catch
						{
						}
						if (string.IsNullOrEmpty(text))
						{
							try
							{
								text = component.GetType().Name ?? "";
							}
							catch
							{
							}
						}
						if (!text.Contains("TextMeshProUGUI") && !text.Contains("TMP_Text"))
						{
							continue;
						}
						bool flag = false;
						try
						{
							System.Reflection.MethodInfo[] methods = component.GetType().GetMethods();
							if (methods != null)
							{
								foreach (System.Reflection.MethodInfo methodInfo in methods)
								{
									if (methodInfo == null)
									{
										continue;
									}
									string text2 = methodInfo.Name ?? "";
									if (text2 == "set_text" || text2 == "SetText")
									{
										System.Reflection.ParameterInfo[] parameters = methodInfo.GetParameters();
										if (parameters != null && parameters.Length == 1)
										{
											methodInfo.Invoke(component, new object[1] { lastNewsLine });
											flag = true;
											break;
										}
									}
								}
							}
						}
						catch
						{
						}
						if (flag)
						{
							break;
						}
					}
				}
			}
			catch
			{
			}
			_nightTabInserted = true;
		}
		catch
		{
		}
	}

	public static void PostfixOnNightlyReportButtonClicked(AdvCalendarUIManager __instance)
	{
		TryWriteCannibalismTabLine(__instance, "nightlyReportSection");
	}

	public static void PostfixOpenUIFromNightlyReport(AdvCalendarUIManager __instance)
	{
		TryWriteCannibalismTabLine(__instance, "nightlyReportSection");
	}

	public static void PostfixOnStatusButtonClicked(AdvCalendarUIManager __instance)
	{
		TryWriteCannibalismTabLine(__instance, "statusSection");
	}

	public static void PostfixNewsPopulateUI()
	{
		try
		{
			if (string.IsNullOrEmpty(LastNewsLine))
			{
				return;
			}
			NewsUIManager instance = NewsUIManager.Instance;
			if (instance == null)
			{
				return;
			}
			GameObject gameObject = null;
			if (instance.layout4 != null && instance.layout4.activeSelf)
			{
				gameObject = instance.layout4;
			}
			else if (instance.layout3 != null && instance.layout3.activeSelf)
			{
				gameObject = instance.layout3;
			}
			else if (instance.layout2 != null && instance.layout2.activeSelf)
			{
				gameObject = instance.layout2;
			}
			else if (instance.layout1 != null && instance.layout1.activeSelf)
			{
				gameObject = instance.layout1;
			}
			if (gameObject == null)
			{
				return;
			}
			Transform transform = gameObject.transform.Find("WageCannibalismNews");
			if (transform != null)
			{
				Text component = transform.GetComponent<Text>();
				if (component != null)
				{
					component.text = LastNewsLine;
				}
				return;
			}
			GameObject gameObject2 = new GameObject("WageCannibalismNews");
			RectTransform rectTransform = gameObject2.AddComponent<RectTransform>();
			rectTransform.SetParent(gameObject.transform, worldPositionStays: false);
			rectTransform.anchorMin = new Vector2(0f, 0f);
			rectTransform.anchorMax = new Vector2(1f, 0f);
			rectTransform.pivot = new Vector2(0.5f, 0f);
			rectTransform.anchoredPosition = new Vector2(0f, 6f);
			rectTransform.sizeDelta = new Vector2(0f, 30f);
			Text text = gameObject2.AddComponent<Text>();
			text.text = LastNewsLine;
			text.fontSize = 16;
			text.alignment = TextAnchor.LowerLeft;
			text.color = new Color(0.9f, 0.7f, 0.3f, 1f);
			try
			{
				text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
			}
			catch
			{
			}
		}
		catch
		{
		}
	}

	internal static string GetName(GameItem it)
	{
		try
		{
			string displayName = it.GetDisplayName();
			if (!string.IsNullOrEmpty(displayName))
			{
				return displayName;
			}
		}
		catch
		{
		}
		try
		{
			string identifier = it.identifier;
			if (!string.IsNullOrEmpty(identifier))
			{
				return identifier;
			}
		}
		catch
		{
		}
		return "?";
	}
}
internal static class BatteryCannibalism
{
	internal static string LastNewsLine;

	public static void OnDayStartPostfix()
	{
		try
		{
			System.Collections.Generic.List<GameItem> list = CollectBatteries();
			if (list == null || list.Count < 2)
			{
				return;
			}
			for (int num = list.Count - 1; num > 0; num--)
			{
				int index = Core.Rng.Next(num + 1);
				GameItem value = list[num];
				list[num] = list[index];
				list[index] = value;
			}
			System.Collections.Generic.List<string> list2 = new System.Collections.Generic.List<string>();
			for (int i = 0; i + 1 < list.Count; i += 2)
			{
				GameItem gameItem = list[i];
				GameItem gameItem2 = list[i + 1];
				if (BatteryScore(gameItem) == BatteryScore(gameItem2) || BatteryDim(gameItem) != BatteryDim(gameItem2))
				{
					if (Core.Rng.Next(2) == 0)
					{
						GameItem gameItem3 = gameItem;
						gameItem = gameItem2;
						gameItem2 = gameItem3;
					}
				}
				else if (BatteryScore(gameItem) < BatteryScore(gameItem2))
				{
					GameItem gameItem4 = gameItem;
					gameItem = gameItem2;
					gameItem2 = gameItem4;
				}
				System.Collections.Generic.List<string> list3 = new System.Collections.Generic.List<string>();
				int tagIntSafe = RobinCrusoePerk.GetTagIntSafe(gameItem, "power_source_item_energy");
				if (tagIntSafe > 0)
				{
					RobinCrusoePerk.AddTagInt(gameItem2, "power_source_item_energy", tagIntSafe);
					list3.Add("电量+" + tagIntSafe);
				}
				int tagIntSafe2 = RobinCrusoePerk.GetTagIntSafe(gameItem, "power_source_item_max_energy");
				if (tagIntSafe2 > 0)
				{
					RobinCrusoePerk.AddTagInt(gameItem2, "power_source_item_max_energy", tagIntSafe2);
					list3.Add("容量+" + tagIntSafe2);
				}
				int tagIntSafe3 = RobinCrusoePerk.GetTagIntSafe(gameItem, "power_source_item_recharge");
				if (tagIntSafe3 > 0)
				{
					RobinCrusoePerk.AddTagInt(gameItem2, "power_source_item_recharge", tagIntSafe3);
					list3.Add("自充+" + tagIntSafe3);
				}
				bool flag = false;
				try
				{
					flag = gameItem.IsTag("BREEDER_POWER_SOURCE_ITEM_TAG");
				}
				catch
				{
				}
				if (flag)
				{
					try
					{
						gameItem2.EnableTag("BREEDER_POWER_SOURCE_ITEM_TAG");
					}
					catch
					{
					}
					list3.Add("获得自充电");
				}
				long num2 = 0L;
				try
				{
					num2 = gameItem.unitBaseValue;
				}
				catch
				{
				}
				long num3 = num2 + tagIntSafe;
				if (num3 > 0)
				{
					try
					{
						gameItem2.unitBaseValue += num3;
					}
					catch
					{
					}
					list3.Add("价值+" + num3);
				}
				try
				{
					ContrabandHelper.InitContrabandItem(gameItem2, 3);
				}
				catch
				{
				}
				try
				{
					string text = gameItem2.shortDescription ?? "";
					if (!text.Contains("违禁原因"))
					{
						gameItem2.shortDescription = text + LangHelper.T("（违禁原因：能量融合改造的违禁电池）", " (Contraband: energy-fused illicit battery)");
					}
				}
				catch
				{
				}
				string name = ModCannibalism.GetName(gameItem2);
				string name2 = ModCannibalism.GetName(gameItem);
				try
				{
					gameItem.parentInventory?.Expel(gameItem);
				}
				catch
				{
				}
				try
				{
					gameItem.Destroy();
				}
				catch
				{
				}
				string text2 = ((list3.Count > 0) ? string.Join(" ", list3) : LangHelper.T("（无属性转移）", "(no stats transferred)"));
				string text3 = LangHelper.T(name + " 吞噬了 " + name2 + " · " + text2, name + " devoured " + name2 + " · " + text2);
				list2.Add(text3);
				try
				{
					StoreUIManager.Instance.Notify(text3);
				}
				catch
				{
				}
				LastNewsLine = list2[list2.Count - 1];
				try
				{
					PlayerStore instance = PlayerStore.Instance;
					if (instance == null)
					{
						continue;
					}
					foreach (string item in list2)
					{
						instance.AddNightLog(item, "#7FC97F"); // 09-22 统一柔和绿
					}
				}
				catch
				{
				}
			}
		}
		catch
		{
		}
	}

	private static System.Collections.Generic.List<GameItem> CollectBatteries()
	{
		System.Collections.Generic.List<GameItem> list = new System.Collections.Generic.List<GameItem>();
		try
		{
			System.Collections.Generic.List<GameItem> list2 = new System.Collections.Generic.List<GameItem>();
			try
			{
				Il2CppSystem.Collections.Generic.List<GameItem> list3 = PlayerStore.Instance.FindAllItem();
				if (list3 != null)
				{
					Il2CppSystem.Collections.Generic.List<GameItem>.Enumerator enumerator = list3.GetEnumerator();
					while (enumerator.MoveNext())
					{
						GameItem current = enumerator.Current;
						if (current != null)
						{
							list2.Add(current);
						}
					}
				}
			}
			catch
			{
			}
			foreach (GameItem item in list2)
			{
				if (item == null || !RobinCrusoePerk.IsMachine(item))
				{
					continue;
				}
				string text = "";
				try
				{
					text = item.identifier ?? "";
				}
				catch
				{
				}
				if (text != "wage_gu_machine")
				{
					continue;
				}
				GameGridInventory guGrid = GuMachineSystem.GetGuGrid(item);
				if (guGrid == null || guGrid.childItems == null)
				{
					continue;
				}
				Il2CppSystem.Collections.Generic.List<GameItem>.Enumerator enumerator = guGrid.childItems.GetEnumerator();
				while (enumerator.MoveNext())
				{
					GameItem current3 = enumerator.Current;
					if (current3 != null)
					{
						bool flag = false;
						try
						{
							flag = current3.IsTag("power_source_item");
						}
						catch
						{
						}
						if (flag)
						{
							list.Add(current3);
						}
					}
				}
			}
		}
		catch
		{
		}
		return list;
	}

	private static int BatteryScore(GameItem b)
	{
		int num = 0;
		try
		{
			num += RobinCrusoePerk.GetTagIntSafe(b, "power_source_item_energy");
		}
		catch
		{
		}
		try
		{
			num += RobinCrusoePerk.GetTagIntSafe(b, "power_source_item_max_energy");
		}
		catch
		{
		}
		try
		{
			num += RobinCrusoePerk.GetTagIntSafe(b, "power_source_item_recharge");
		}
		catch
		{
		}
		try
		{
			num += (int)b.unitBaseValue;
		}
		catch
		{
		}
		return num;
	}

	private static int BatteryDim(GameItem b)
	{
		int num = 0;
		int num2 = 0;
		int num3 = 0;
		try
		{
			num = RobinCrusoePerk.GetTagIntSafe(b, "power_source_item_energy");
		}
		catch
		{
		}
		try
		{
			num2 = RobinCrusoePerk.GetTagIntSafe(b, "power_source_item_max_energy");
		}
		catch
		{
		}
		try
		{
			num3 = RobinCrusoePerk.GetTagIntSafe(b, "power_source_item_recharge");
		}
		catch
		{
		}
		if (num >= num2 && num >= num3)
		{
			return 0;
		}
		if (num2 >= num && num2 >= num3)
		{
			return 1;
		}
		if (num3 >= num && num3 >= num2)
		{
			return 2;
		}
		return 3;
	}
}
