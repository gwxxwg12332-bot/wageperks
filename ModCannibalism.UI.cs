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

internal static partial class ModCannibalism
{
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
