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

	private static System.Reflection.PropertyInfo _negocInstanceProp;

	private static System.Reflection.FieldInfo _negocInstanceField;

	private static readonly System.Collections.Generic.Dictionary<System.Type, System.Reflection.FieldInfo> _itemNameFieldCache = new System.Collections.Generic.Dictionary<System.Type, System.Reflection.FieldInfo>();

	private static readonly System.Collections.Generic.Dictionary<System.Type, System.Reflection.PropertyInfo> _textPropCache = new System.Collections.Generic.Dictionary<System.Type, System.Reflection.PropertyInfo>();

	private static bool _negocCacheInit = false;

	public static void PostfixGetLocalizedPerkTable(string key, ref string __result)
	{
		try
		{
			if (string.IsNullOrEmpty(key))
			{
				return;
			}
			string text = null;
			bool flag = false;
			bool flag2 = false;
			if (key.StartsWith("perk_"))
			{
				string text2 = key.Substring(5);
				if (text2.EndsWith("_name"))
				{
					text = text2.Substring(0, text2.Length - 5);
					flag = true;
				}
				else if (text2.EndsWith("_desc"))
				{
					text = text2.Substring(0, text2.Length - 5);
					flag2 = true;
				}
			}
			if (text != null && CustomStartingPerks.Find(text) != null && CustomStartingPerks.TryGetLoc(text, out var displayName, out var description))
			{
				if (flag && !string.IsNullOrEmpty(displayName))
				{
					__result = displayName;
				}
				else if (flag2 && !string.IsNullOrEmpty(description))
				{
					__result = description;
				}
			}
		}
		catch (System.Exception ex)
		{
			Core.LogMsg("[本地化] 补丁失败: " + ex.Message);
		}
	}

	public static void PostfixGameItemGetDisplayName(GameItem __instance, ref string __result)
	{
		try
		{
			if (__instance == null)
			{
				return;
			}
			string identifier = __instance.identifier;
			if (identifier == "wine_bottle")
			{
				// 09-26 修：任何自定义名都保留（原生 CUSTOM_NAME_TAG + name 字段双保险），不再只保两种莓果酿
				// 实锤：HasCustomName(item)=IsTag("CUSTOM_NAME_TAG")，GetCustomName=GetTagReadonly("CUSTOM_NAME_TAG")（ISIL GeneralHelper.txt:2932/3003）
				try
				{
					if (Il2Cpp.GeneralHelper.HasCustomName(__instance)) // 原生自定义名标签（别的 mod 走原生机制时命中）
					{
						string cn = Il2Cpp.GeneralHelper.GetCustomName(__instance);
						if (!string.IsNullOrEmpty(cn)) { __result = cn; return; }
					}
				}
				catch { }
				string name = __instance.name;
				if (!string.IsNullOrEmpty(name)
					&& name != "Wine Bottle" && name != "酒瓶" && name != "Empty Wine Bottle" && name != "空酒瓶")
				{
					__result = name; // 原生酿酒名 / 别的 mod 直接写 name 字段的酒名
					return;
				}
				__result = LangHelper.T("酒瓶", "Wine Bottle"); // 兜底：普通空酒瓶走翻译
			}
			else
			{
				string itemDisplayNameOverride = GetItemDisplayNameOverride(identifier);
				if (itemDisplayNameOverride != null)
				{
					__result = itemDisplayNameOverride;
				}
			}
		}
		catch (System.Exception ex)
		{
			Core.LogMsg("[GetDisplayName] 异常: " + ex.Message);
		}
	}

	private static string GetItemDisplayNameOverride(string id)
	{
		return id switch
		{
			"wine_bloomberry" => LangHelper.T("荧光莓果酿", "Glowberry Wine"), 
			"wine_gloomberry" => LangHelper.T("暗影莓果酿", "Gloomberry Wine"), 
			"wine_berry" => LangHelper.T("莓果酿", "Berry Wine"), 
			"wine_bottle" => LangHelper.T("酒瓶", "Wine Bottle"), 
			"beer_case" => LangHelper.T("一箱啤酒", "Case of Beer"), 
			"red_beer" => LangHelper.T("红魔鬼啤酒", "Red Devil Beer"), 
			"empty_beer_bottle" => LangHelper.T("空啤酒瓶", "Empty Beer Bottle"), 
			"wine_superyeast" => LangHelper.T("超级酵母", "Super Yeast"), 
			"wine_yeast" => LangHelper.T("酿酒酵母", "Brewing Yeast"), 
			"wine_yeast_infinite" => LangHelper.T("永续酵母", "Perpetual Yeast"), 
			"wine_yeast_red" => LangHelper.T("红酵母", "Red Yeast"), 
			"permit_gun_1" => LangHelper.T("枪支许可证（一级）", "Gun Permit (Tier 1)"), 
			"permit_gun_2" => LangHelper.T("枪支许可证（二级）", "Gun Permit (Tier 2)"), 
			"permit_gun_3" => LangHelper.T("枪支许可证（三级）", "Gun Permit (Tier 3)"), 
			"blank_keycard" => LangHelper.T("空白钥匙卡", "Blank Keycard"), 
			_ => null, 
		};
	}

	public static void PostfixGetDisplayNameFromID(string itemID, ref string __result)
	{
		try
		{
			if (string.IsNullOrEmpty(itemID))
			{
				return;
			}
			string itemDisplayNameOverride = GetItemDisplayNameOverride(itemID);
			if (itemDisplayNameOverride != null)
			{
				switch (itemID)
				{
				case "beer_case":
				case "wine_berry":
				case "wine_gloomberry":
					__result = itemDisplayNameOverride;
					break;
				}
			}
		}
		catch
		{
		}
	}

	public static void PostfixGetLocalizedItem(string key, ref string __result)
	{
		try
		{
			if (string.IsNullOrEmpty(key))
			{
				return;
			}
			if (!key.Contains("beer_case") && !key.Contains("wine_") && !key.Contains("permit_gun"))
			{
				key.Contains("blank_keycard");
			}
			if (string.IsNullOrEmpty(__result) || __result.Trim() == "?")
			{
				string localizedItemOverride = GetLocalizedItemOverride(key);
				if (localizedItemOverride != null)
				{
					__result = localizedItemOverride;
				}
			}
		}
		catch
		{
		}
	}

	private static string GetLocalizedItemOverride(string key)
	{
		if (string.IsNullOrEmpty(key))
		{
			return null;
		}
		string itemDisplayNameOverride = GetItemDisplayNameOverride(key);
		if (itemDisplayNameOverride != null)
		{
			return itemDisplayNameOverride;
		}
		string[] array = new string[14]
		{
			"wine_yeast_infinite", "wine_yeast_red", "wine_bloomberry", "wine_gloomberry", "empty_beer_bottle", "permit_gun_1", "permit_gun_2", "permit_gun_3", "blank_keycard", "wine_yeast",
			"wine_berry", "wine_bottle", "beer_case", "red_beer"
		};
		foreach (string text in array)
		{
			if (key.IndexOf(text, System.StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return GetItemDisplayNameOverride(text);
			}
		}
		return null;
	}

	public static void PostfixGetLocalizedName(string key, ref string __result)
	{
		TryCoverLocalizationKey(key, ref __result);
	}

	public static void PostfixGetLocalizedUI(string key, ref string __result)
	{
		TryCoverLocalizationKey(key, ref __result);
	}

	private static void TryCoverLocalizationKey(string key, ref string __result)
	{
		try
		{
			if (!string.IsNullOrEmpty(key) && (key.Contains("beer_case") || key.Contains("wine_") || key.Contains("permit_gun") || key.Contains("blank_keycard")) && (string.IsNullOrEmpty(__result) || __result.Trim() == "?"))
			{
				string localizedItemOverride = GetLocalizedItemOverride(key);
				if (localizedItemOverride != null)
				{
					__result = localizedItemOverride;
				}
			}
		}
		catch
		{
		}
	}

	private static void InitNegocCache()
	{
		if (!_negocCacheInit)
		{
			System.Type typeFromHandle = typeof(NegociationUIManager);
			_negocInstanceProp = typeFromHandle.GetProperty("Instance", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
			_negocInstanceField = typeFromHandle.GetField("Instance", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
			_negocCacheInit = true;
		}
	}

	private static void FixTradeItemName(GameItem item)
	{
		try
		{
			if (item == null)
			{
				return;
			}
			string itemDisplayNameOverride = GetItemDisplayNameOverride(item.identifier);
			if (itemDisplayNameOverride == null)
			{
				return;
			}
			InitNegocCache();
			object obj = null;
			if (_negocInstanceProp != null)
			{
				obj = _negocInstanceProp.GetValue(null);
			}
			if (obj == null && _negocInstanceField != null)
			{
				obj = _negocInstanceField.GetValue(null);
			}
			if (obj == null)
			{
				return;
			}
			System.Type type = obj.GetType();
			if (!_itemNameFieldCache.TryGetValue(type, out var value))
			{
				value = type.GetField("itemName", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
				_itemNameFieldCache[type] = value;
			}
			if (value == null)
			{
				return;
			}
			object value2 = value.GetValue(obj);
			if (value2 != null)
			{
				System.Type type2 = value2.GetType();
				if (!_textPropCache.TryGetValue(type2, out var value3))
				{
					value3 = type2.GetProperty("text");
					_textPropCache[type2] = value3;
				}
				if (value3 != null)
				{
					value3.SetValue(value2, itemDisplayNameOverride);
				}
			}
		}
		catch (System.Exception ex)
		{
			Core.LogMsg("[名称修复] 失败: " + ex.Message);
		}
	}

	public static void PostfixGenerateLocalizedString(object tableEntryReference, ref string __result)
	{
		try
		{
			if (tableEntryReference == null)
			{
				return;
			}
			string text = null;
			try
			{
				System.Reflection.PropertyInfo property = tableEntryReference.GetType().GetProperty("Key");
				if (property != null)
				{
					text = property.GetValue(tableEntryReference) as string;
				}
			}
			catch
			{
			}
			if (!string.IsNullOrEmpty(text) && (text.Contains("beer_case") || text.Contains("wine_") || text.Contains("permit_gun") || text.Contains("blank_keycard")) && (string.IsNullOrEmpty(__result) || __result.Trim() == "?"))
			{
				string localizedItemOverride = GetLocalizedItemOverride(text);
				if (localizedItemOverride != null)
				{
					__result = localizedItemOverride;
				}
			}
		}
		catch
		{
		}
	}
}
