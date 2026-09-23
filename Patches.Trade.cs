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

	private static bool _inBudgetOverride = false;

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
			// 09-21 拆包实锤：用 IsItemOwned 判定买卖方向，替代 CurrentUITradeMode（竞态/残留）
			bool isSell = false;
			try { isSell = Il2Cpp.GeneralHelper.IsItemOwned(item); }
			catch (System.Exception ex) { isSell = CurrentUITradeMode == 2; Core.LogMsg("[交易标记] IsItemOwned判定失败，按UI模式回退: " + ex.Message); }
			if (BadReputationPerk.IsActive() && !BadReputationPerk.IsCleared())
			{
				if (isSell)
				{
					result = (long)((double)result * 0.8);
					TryAddBadReputationFeature(item, -20);
				}
				else if (!isSell)
				{
					result = (long)((double)result * 1.2);
					TryAddBadReputationFeature(item, 20);
				}
			}
			if (RobinCrusoePerk.IsActive())
			{
				if (!isSell)
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
				else if (isSell)
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
			if (!isSell)
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

	// 09-23 游戏版本适配：目标方法由 ComputeTradeRepMultiplier(单数, 返回 double)
	// 变为 ComputeTradeRepMultipliers(复数, 返回 ValueTuple<double,double>)。
	//
	// 拆包依据（_Demo_20260915_cpp2il\IsilDump\Assembly-CSharp\BargainUIManager.txt:8752 签名 +
	// UpdateRepGainText ISIL 180-298）：
	//   ComputeTradeRepMultipliers 内部对「两个方向」各算一次倍率，打包成元组返回；
	//   调用方 UpdateRepGainText 分别取 Item1/Item2 构造两条声誉收益行：
	//     Item1 → 卖出方向（ui_nego_title_selling）
	//     Item2 → 买入方向（ui_nego_title_buying）
	//   → 两个元素都是"声誉倍率"，故按特性整体缩放两者 = 忠实实现「声誉获取 ±25%」。
	// ⚠️ __result 必须用 Il2CppSystem.ValueTuple（IL2CPP 镜像类型），**不能用** System.ValueTuple（BCL 类型）。
	// 实测报错：Cannot assign method return type Il2CppSystem.ValueTuple`2 to __result type System.ValueTuple`2
	// —— 两者同名但分属不同程序集，Harmony 无法互赋。
	public static void PostfixTradeRepMultipliers(ref Il2CppSystem.ValueTuple<double, double> __result)
	{
		try
		{
			double factor = 1.0;
			if (SmilingFacePerk.IsActive())
			{
				factor = 0.75; // 笑面虎：声誉获取 -25%（混合特性代价）
			}
			else if (SmilingTigerPerk.IsActive())
			{
				factor = 1.25; // 童叟无欺：声誉 +25%
			}
			if (factor != 1.0)
			{
				__result = new Il2CppSystem.ValueTuple<double, double>(
					__result.Item1 * factor, __result.Item2 * factor);
			}
		}
		catch (System.Exception ex)
		{
			Core.LogMsg("[童叟无欺] 声誉补丁失败: " + ex.Message);
		}
	}
}
