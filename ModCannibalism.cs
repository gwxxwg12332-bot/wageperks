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

// 吞噬季（RiskTaker 特性）：机器模块互食系统
namespace JacksonPerks;

internal static class ModCannibalism
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

	// 09-23 修复「吞噬季将节点排除在吞噬范围之外」：节点不参与吞噬判定
	// 拆包实锤 [L1]：ModuleDirectory.txt:1051 "node_small" / :1067 "node_medium" 与 system_module_*、
	// chem_module、furnace_module_* 在同一张模组注册表里 → 节点在原生定义中就是模组（带 MODULE_TAG），
	// 会被 CollectMachines 收进候选池并被吞掉。
	// 只在吞噬季生效；不改 RobinCrusoePerk.IsExcludedModule（那个还被炼蛊器/模组 tooltip 共用，改了会动到别的机制）。
	private static bool IsNodeModule(string id)
	{
		if (string.IsNullOrEmpty(id)) return false;
		if (id == "node" || id == "node_small" || id == "node_medium") return true;
		return id.StartsWith("node_");
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
					if (!(text == "system_module_ruined") && !IsNodeModule(text))
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