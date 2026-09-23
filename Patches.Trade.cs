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

	public static int CurrentUITradeMode = 0;

	private static long _lastSellModeItemUid = -1L;

	private static long _lastBuyModeItemUid = -1L;

	private static bool _inNegociatedCalc = false;

	private static int _currentValueCallsInNegociated = 0;

	internal static bool _inBudgetOverride = false; // 09-23 原 private；蛙娘 PostfixApplyBudgetModifier 防重入也需访问 → internal

	private static float _budgetDiagTime = 0f; // 成交预算诊断节流（发布前删）

	private static readonly HashSet<long> _moodBoostedClients = new HashSet<long>();

	private const string FRIEND_DISCOUNT_ID = "friend_discount";

	private static readonly System.Collections.Generic.Dictionary<long, GameItem> _discountedItems = new System.Collections.Generic.Dictionary<long, GameItem>();

	private static readonly HashSet<long> _nodeBuffItems = new HashSet<long>();

	private static readonly System.Collections.Generic.Dictionary<long, GameItem> _tradeFeatureItems = new System.Collections.Generic.Dictionary<long, GameItem>();

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
				// 交易防御：物品唯一ID读取（交互期物品可能已销毁，跳过保持原价）
			}
			if (num != _lastSellModeItemUid)
			{
				_lastSellModeItemUid = num;
				FixTradeItemName(item);
				TryAddTradeFeatureByUI(item);
			}
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
				// 交易防御：物品唯一ID读取（同上）
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

	public static void PostfixGameItemGetValue(GameItem __instance, ref long __result)
	{
		try
		{
			bool isSell = false;
			try { isSell = Il2Cpp.GeneralHelper.IsItemOwned(__instance); } catch { isSell = CurrentUITradeMode == 2; }
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
				// 交易防御：StoreClient 状态检查异常跳过
			}
			try
			{
				if (!isSell)
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
					// 交易防御：PlayerStore 当前客户状态检查异常跳过
				}
				if (storeClient != null && ((storeClient.identifier ?? "") == "inventor" || (storeClient.identifier ?? "") == "inventorStorage" || (storeClient.identifier ?? "") == "inventor_storage"))
				{
					__result = __result * BuildConfig.DoctorBuybackPct / 100;
				}
			}
			catch
			{
				// 交易防御：PlayerStore 当前客户状态检查异常跳过
			}
		}
		catch (System.Exception ex)
		{
			Core.LogMsg("[之友折扣-GetValue] 失败: " + ex.Message);
		}
	}

	public static void PostfixDealMakerBonus(ref int __result)
	{
		// 09-19 童叟无欺重做：移除议价 +25 效果（蛙娘在场 +50 在 WageGirlSystem）
	}

}
