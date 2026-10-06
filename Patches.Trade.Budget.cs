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
				int budget = WageAPI.WageTrade.GetBudget(__instance); // 10-06 收敛：WageTrade 权威读（负值→0，含原 10-05 CustomerCreditBoost 兜底语义）
				// 09-23 用户拍板恢复原生：不再人为抬下限，只保留特性倍率本身
				WageAPI.WageTrade.SetBudget(__instance, (long)((double)budget * (1.0 + (double)budgetBonusPct / 100.0)));
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
					// 09-23 用户拍板恢复原生：不再人为抬下限，只保留特性倍率本身
					int newCash = (int)((double)clientCash * (1.0 + (double)budgetBonusPct / 100.0));
					// 09-26 修：防其他mod干扰后变成负数
					if (newCash < 0) newCash = 0;
					__result.clientCash = newCash;
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
			{
			}
			if (!RobinCrusoePerk.IsActive())
			{
				return;
			}
			string mk = MoodKey(__instance);
			if (__instance != null && mk != null && _moodBoostedClients.Add(mk))
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
					// 交易防御：反射读取 clientIntent 字段失败（游戏版本差异，跳过）
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
				// 交易防御：鲁滨逊营收记录失败跳过（不影响交易主流程）
			}
		}
		catch
		{
			// 交易防御：鲁滨逊营收记录失败跳过
		}
	}

}
