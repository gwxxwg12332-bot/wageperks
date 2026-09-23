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
					// 交易防御：物品唯一ID读取（同上）
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
					// 交易防御：物品唯一ID读取（同上）
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

}
