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
			if (item == null || item.itemFeatures == null) return;
			// 09-28 自愈：清历史堆积的 wages_node_buff（09-26 前无判重遗留）
			bool excludedType = item.IsTag("destiny_dice_tag") || LuckScoutBackpackUpgrade.IsBead(item) || ContainerUpgradeV2.IsWageBox(item);
			int keepMax = excludedType ? 0 : 1;
			int found = 0;
			for (int j = item.itemFeatures.Count - 1; j >= 0; j--)
			{
				if (item.itemFeatures[j] != null && item.itemFeatures[j].identifier == "wages_node_buff") { found++; if (found > keepMax) item.itemFeatures.RemoveAt(j); }
			}
			if (excludedType || !RobinCrusoePerk.IsActive()) return;
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
				// 交易防御：物品指针读取（交互期物品销毁防御）
			}
			// 09-26 修：Pointer 判重——同一物品只加一次（identifier 比较对虚空珠匹配不上，改指针级）
			if (num != 0L && _nodeBuffItems.Contains(num))
			{
				return;
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

	private static void TryAddRobinsonBuyMarkup(GameItem item)
	{
		try
		{
			if (item == null || item.itemFeatures == null) return;
			// 09-28 自愈：清历史堆积的 wages_robin_buy
			bool excludedType = item.IsTag("destiny_dice_tag") || LuckScoutBackpackUpgrade.IsBead(item) || ContainerUpgradeV2.IsWageBox(item);
			int keepMax = excludedType ? 0 : 1;
			int found = 0;
			for (int j = item.itemFeatures.Count - 1; j >= 0; j--)
			{
				if (item.itemFeatures[j] != null && item.itemFeatures[j].identifier == "wages_robin_buy") { found++; if (found > keepMax) item.itemFeatures.RemoveAt(j); }
			}
			if (excludedType) return;
			string text = LangHelper.T("鲁滨逊·口粮双倍价", "Robinson·Ration x2");
			long num = 0L;
			try
			{
				num = item.Pointer.ToInt64();
			}
			catch
			{
				// 交易防御：物品指针读取（交互期物品销毁防御）
			}
			// 10-07 修复（玩家"价格对但词条不显示"根因）：删除 _nodeBuffItems 判重——buy 路径先调 TryAddNodeBuffFeature
			//   把物品登记进 _nodeBuffItems（:166），此处再用同表判重=自己拦自己 → wages_robin_buy 词条永远加不上。
			//   防重由下方 itemFeatures 循环检查（identifier=="wages_robin_buy"）负责，无需额外登记表。
			for (int i = 0; i < item.itemFeatures.Count; i++)
			{
				if (item.itemFeatures[i] != null && item.itemFeatures[i].identifier == "wages_robin_buy")
				{
					item.itemFeatures[i].publicDisplay = text;
					item.itemFeatures[i].actualDisplay = text;
					// 10-07 P1-6：旧条目同步补渲染条件（原 0%/false 词条区不显示）
					item.itemFeatures[i].preExposeValueModifier = 100;
					item.itemFeatures[i].usePreExposeValue = true;
					return;
				}
			}
			ItemFeature itemFeature = new ItemFeature();
			itemFeature.identifier = "wages_robin_buy";
			itemFeature.featureType = ItemFeature.FeatureType.TemporaryBuying;
			itemFeature.valueStage = ItemFeature.ValueStage.Market;
			itemFeature.valueModifier = 0;
			// 10-07 P1-6（玩家"药/食物双倍购入词条缺失"）：原 0%/usePreExposeValue=false → 词条区按 percent 渲染时跳过（无行可显）。
			// 对齐 AddTradeLabel 可显示模式：+100% 表达 ×2，publicDisplay 自定义文本"鲁滨逊·口粮双倍价"。
			itemFeature.preExposeValueModifier = 100;
			itemFeature.usePreExposeValue = true;
			itemFeature.initiallyShown = true;
itemFeature.isFeatureMatch = true;
itemFeature.isFeatureExposed = true;
			itemFeature.isExposable = true;
			itemFeature.isFeatureDiscovered = false;
			itemFeature.publicDisplay = text;
			itemFeature.actualDisplay = text;
			item.itemFeatures.Add(itemFeature);
			if (num != 0L)
			{
				_nodeBuffItems.Add(num); // 09-26 补：新建分支也登记指针（判重闭环）
			}
		}
		catch
		{
			// 交易防御：特征容器判空/骰子标记检查失败跳过
		}
	}

}
