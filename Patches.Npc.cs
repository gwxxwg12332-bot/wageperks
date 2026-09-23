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

internal static partial class Patches
{

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
			// CR-14 同类问题：原来硬编码 7，配置项 DoctorVisitInterval 改了不生效 → 改为读配置（默认 7，零回归）
			int jacksonInterval = BuildConfig.DoctorVisitInterval > 0 ? BuildConfig.DoctorVisitInterval : 7;
			if (_lastScheduledDay >= 0 && dayCounter - _lastScheduledDay < jacksonInterval)
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

	// 阶段2 CR-15 真凶修复（09-23 实测）：
	// _lastScheduledDay 是 static 且从未重置 → 第一档玩到 day30 后开第二档，
	// day1 - day30 = -29 < 间隔 → ScheduleJacksonToday 直接 return false，博士永不被排期。
	// 必须由"开新档"权威挂点 PlayerStore.StartNewGame 触发重置。
	internal static void ResetJacksonSchedule()
	{
		_lastScheduledDay = -1;
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
				// 09-21 封存：成瘾警官事件 OnClientArrived 关闭
			}
			catch
			{
			}
			if (!flag && !IsSecurityClient(currentClient) && currentClient.clientIntent == StoreClient.ClientIntent.BUY)
			{
				WagePowerPerk.EnsureBuyTagsForClient(currentClient);
			}
			if (WagePowerPerk.IsActive() && !flag && !IsSecurityClient(currentClient) && (currentClient.clientIntent == StoreClient.ClientIntent.SELL || currentClient.clientIntent == StoreClient.ClientIntent.SELLNBUY))
			{
				WagePowerPerk.AddRandomItemsToCounter(currentClient);
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
			return WagePowerPerk.GunsmithDialogues[Core.Rng.Next(WagePowerPerk.GunsmithDialogues.Length)];
		case "retired_water_merchant":
			return WagePowerPerk.WaterMerchantDialogues[Core.Rng.Next(WagePowerPerk.WaterMerchantDialogues.Length)];
		case "retired_winemaker":
			return WagePowerPerk.AlcoholMerchantDialogues[Core.Rng.Next(WagePowerPerk.AlcoholMerchantDialogues.Length)];
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
}
