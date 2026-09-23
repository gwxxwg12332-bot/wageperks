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
			// 09-23 奥丁（wanted4）：声望低时压价更狠
			if (storeClient.identifier == "wanted4")
			{
				try
				{
					var rep = StoreReputation.GetStoreReputation("FACTION_REVOLUTION");
					int revRep = rep != null ? (int)rep.GetReputationExact() : 0;
					if (revRep <= -99)
					{
						storeClient.sellPriceModifier = -50;
						Core.LogMsg("[奥丁] 声望" + revRep + " → 压价 -50%");
					}
				}
				catch (System.Exception ex) { Core.LogMsg("[Patches.Npc.Client] 异常: " + ex.Message); }
			}
		}
		catch (System.Exception ex) { Core.LogMsg("[Patches.Npc.Client] 异常: " + ex.Message); }
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
	public static void PostfixOnHandleContentUnlockClient(StoreClientManager __instance)
	{
		if (__instance != null)
		{
			SpecialNpcManager.HandleContentUnlockPostfix(__instance);
		}
	}
}
