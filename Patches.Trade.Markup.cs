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
					// 交易防御：违禁品等级查询失败降级（isContraband 保持默认）
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
			long originalResult = result; // 09-24 clamp 下限用：保存原价
			// 09-21 拆包实锤：用 IsItemOwned 判定买卖方向，替代 CurrentUITradeMode（竞态/残留）
			bool isSell = false;
			try { isSell = Il2Cpp.GeneralHelper.IsItemOwned(item); }
			catch (System.Exception ex) { isSell = CurrentUITradeMode == 2; Core.LogMsg("[交易标记] IsItemOwned判定失败，按UI模式回退: " + ex.Message); }
			ApplyBadReputationMarkup(item, isSell, ref result);
			// 09-24 修复：博士之友/退休枪匠/水商/酿酒师买对应NPC商品 ×0.95（之前只加 friend_discount 标签不改价=无效）
			ApplyFriendMarkup(item, !isSell, ref result);
			if (ApplyRobinsonMarkup(item, isSell, ref result)) return;
			if (!isSell)
			{
				return;
			}
			bool flag = IsContrabandSafe(item);
			ApplyRiskWineMarkup(item, ref result, flag);
			ApplyFaceMarkup(item, ref result);
			// 09-24 修：所有卖出倍率叠加后 clamp 到原价 50% 下限（防负面特性叠太多价格崩到零）
			if (isSell && result < originalResult * 0.5) result = (long)(originalResult * 0.5);
		}
		catch
		{
			// 交易防御：风险特性激活判定失败降级（同一判定链）
		}
	}

	// ===================== 09-24 架构拆解：TryApplyTradeMarkup 按特性提取（行为零变化） =====================
	// 提取原则：不带新 try（异常上抛到主方法 catch，与原逻辑一致）；仅鲁滨逊 buy 命中 return 用 bool 承接。

	// ① 信誉扫地：卖出 ×0.8 / 买入 ×1.2 + 标签（-20/+20）
	private static void ApplyBadReputationMarkup(GameItem item, bool isSell, ref long result)
	{
		bool _brActive = BadReputationPerk.IsActive(); bool _brCleared = BadReputationPerk.IsCleared(); 
		if (_brActive && !_brCleared)
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
	}

	// ①.5 博士之友/退休枪匠/水商/酿酒师：买对应NPC的商品 ×0.95（复用 Friend.cs GetFriendBuyDiscount 判定，标签已由 PostfixUIInitBuyMode 加）
	private static void ApplyFriendMarkup(GameItem item, bool isBuy, ref long result)
	{
		try
		{
			if (!isBuy || item == null) return;
			float disc = GetFriendBuyDiscount(out _, out _);
			if (disc < 1f)
			{
				result = (long)((double)result * disc);
			}
		}
		catch
		{
			// 交易防御：朋友折扣判定失败降级（同一判定链）
		}
	}

	// ② 鲁滨逊：buy 食物/药品 ×2（命中 return true 结束整链）；sell 食物品质分档 + 卖出加成 + 违禁加成
	private static bool ApplyRobinsonMarkup(GameItem item, bool isSell, ref long result)
	{
		if (!RobinCrusoePerk.IsActive())
		{
			return false;
		}
		if (!isSell)
		{
			TryAddNodeBuffFeature(item);
			if (RobinCrusoePerk.IsFood(item))
			{
				result = (long)((double)result * 2.0);
				TryAddRobinsonBuyMarkup(item);
				return true;
			}
			if (RobinCrusoePerk.IsMedicine(item))
			{
				result = (long)((double)result * 2.0);
				TryAddRobinsonBuyMarkup(item);
				return true;
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
		return false;
	}

	// ③ 违禁品判定（3 重降级链，保持原逐段 try 防御）
	private static bool IsContrabandSafe(GameItem item)
	{
		bool flag = false;
		try
		{
			flag = ContrabandHelper.IsContraband(item);
		}
		catch
		{
			// 交易防御：违禁品判定失败降级（flag 保持默认）
		}
		if (!flag)
		{
			try
			{
				flag = item.IsTag("CONTRABAND");
			}
			catch
			{
				// 交易防御：违禁品标签判定失败降级
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
				// 交易防御：违禁品标签判定失败降级
			}
		}
		return flag;
	}

	// ④ 风险/酒鬼：违禁 ×1.2 / 酒类 ×1.25
	private static void ApplyRiskWineMarkup(GameItem item, ref long result, bool flag)
	{
		bool flag2 = TraitEffects.IsAlcohol(item);
		float num2 = 1f;
		bool flag3 = false;
		try
		{
			flag3 = RiskTakerPerk.IsActive();
		}
		catch
		{
			// 交易防御：风险特性激活判定失败降级
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
	}

	// ⑤ 笑面虎/童叟无欺：卖出价 ±25%
	private static void ApplyFaceMarkup(GameItem item, ref long result)
	{
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
					// 交易防御：物品唯一ID读取（同上）
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
		catch
		{
			// 交易防御：AddTradeLabel 前置判空失败跳过（标签不显示，价格不受影响）
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
				// 交易防御：物品唯一ID读取（同上）
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
					// 交易防御：特征容器遍历——已含特性检查失败跳过（防重复叠加）
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
					// 交易防御：特征容器遍历——已含特性检查失败跳过
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
					// 交易防御：特征容器遍历——已含特性检查失败跳过
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
					// 交易防御：特征容器遍历——已含特性检查失败跳过
				}
			}
			_tradeFeatureItems.Clear();
		}
		catch (System.Exception ex)
		{
			Core.LogMsg("[加价标签] 移除失败: " + ex.Message);
		}
	}

	// 09-24 实锤修复（运行时 Failed to patch）：原生返回 Il2CppSystem.ValueTuple，必须同命名空间才能被 Harmony 挂载；
	// 4b86809 曾声称修复但未落地（git show 无 Markup.cs 改动）——此为此前笑面虎/童叟无欺一直失效的真因。
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
