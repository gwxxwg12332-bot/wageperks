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
using WagePerks;
using MelonLoader;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace WagePerks;

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
				if (item.itemFeatures[i] != null && string.Equals(item.itemFeatures[i].identifier, "friend_discount", System.StringComparison.Ordinal))
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
				itemFeature.preExposeValueModifier = FriendDiscountPct();
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

	/// <summary>博士之友升级接线（阶段1）：友情价百分比 Lv1=-5 / Lv2=-12 / Lv3=-15。</summary>
	private static int FriendDiscountPct()
	{
		try
		{
			int lv = B2ServiceStore.GetEffectLevel(DrJacksonFriendPerk.PerkId);
			if (lv >= 3) return -15;
			if (lv >= 2) return -12;
			return -5;
		}
		catch { return -5; }
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
				if (item.itemFeatures[i] != null && string.Equals(item.itemFeatures[i].identifier, "friend_discount", System.StringComparison.Ordinal))
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
				itemFeature.preExposeValueModifier = FriendDiscountPct();
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

	/// <summary>三之友升级接线（阶段2）：水商/酒商/退休枪匠折扣 Lv1=-5 / Lv2=-10 / Lv3=-15。</summary>
	private static float MerchantDiscount(string perkId)
	{
		try
		{
			int lv = B2ServiceStore.GetEffectLevel(perkId);
			if (lv >= 3) return 0.85f;
			if (lv >= 2) return 0.90f;
			return 0.95f;
		}
		catch { return 0.95f; }
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
				return MerchantDiscount(RetiredGunsmithPerk.PerkId);
			}
			if (clientId == "retired_water_merchant" && WaterMerchantPerk.IsActive())
			{
				return MerchantDiscount(WaterMerchantPerk.PerkId);
			}
			if (clientId == "retired_winemaker" && AlcoholMerchantPerk.IsActive())
			{
				return MerchantDiscount(AlcoholMerchantPerk.PerkId);
			}
			if (DrJacksonFriendPerk.IsActive() && (clientName.Contains("博士") || clientId.Contains("jackson") || clientId.Contains("inventor")))
			{
				// 博士之友升级接线（阶段1）：Lv1=0.95（-5%）、Lv2=0.88（-12%）、Lv3=0.85（-15%）
				int lv = B2ServiceStore.GetEffectLevel(DrJacksonFriendPerk.PerkId);
				if (lv >= 3) return 0.85f;
				if (lv >= 2) return 0.88f;
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
