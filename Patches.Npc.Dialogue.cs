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
	public static void PostfixSpecialNpcStartDialogue(StoreUIManager __instance)
	{
		try
		{
			StoreClient currentClient = SpecialNpcManager.GetCurrentClient();
			if (currentClient == null)
			{
				return;
			}
			if (currentClient.displayName == null && currentClient.identifier == null)
			{
				LangHelper.T("未知", "Unknown");
			}
			bool flag = SpecialNpcManager.IsSpecialNpc(currentClient);
			if (flag)
			{
				SpecialNpcManager.HandleSpecialNpcArrived(currentClient);
			}
			try
			{
				string identifier = currentClient.identifier;
				if (identifier == "wanted7" || identifier == "wanted6")
				{
					RobinCrusoePerk.WantedSupplierOnArrived(currentClient);
				}
			}
			catch
			{
			}
			try
			{
				// 09-21 封存：成瘾警官事件 OnClientArrived 关闭
			}
			catch
			{
			}
			if (!flag && !IsSecurityClient(currentClient) && currentClient.clientIntent == StoreClient.ClientIntent.BUY)
			{
				WagePowerPerk.EnsureBuyTagsForClient(currentClient);
			}
			if (WagePowerPerk.IsActive() && !flag && !IsSecurityClient(currentClient) && (currentClient.clientIntent == StoreClient.ClientIntent.SELL || currentClient.clientIntent == StoreClient.ClientIntent.SELLNBUY))
			{
				WagePowerPerk.AddRandomItemsToCounter(currentClient);
			}
		}
		catch (System.Exception ex)
		{
			Core.LogMsg("[客户到达] OnNextClientArrived补丁失败: " + ex.Message);
		}
	}
	public static void PrefixDisplayClientText(Dialogue dialogue)
	{
		try
		{
			if (dialogue == null)
			{
				return;
			}
			StoreClient currentClient = SpecialNpcManager.GetCurrentClient();
			if (currentClient == null)
			{
				return;
			}
			if (!SpecialNpcManager.IsSpecialNpc(currentClient))
			{
				FixCringeDialogue(dialogue, currentClient);
				return;
			}
			string identifier = currentClient.identifier;
			string title = currentClient.displayName ?? identifier;
			if (currentClient.mainDialogue != null && dialogue == currentClient.mainDialogue)
			{
				string specialNpcGreeting = GetSpecialNpcGreeting(identifier);
				if (specialNpcGreeting != null)
				{
					dialogue.SetText(title, specialNpcGreeting);
				}
				return;
			}
			string text = dialogue.plainText ?? "";
			if (text.Contains("收不收") || text.Contains("想买") || text.Contains("要买") || text.Contains("有没有") || text.Contains("买点") || text.Contains("看看") || text.Contains("卖") || text.Contains("买") || text.Contains("收") || text.Contains("钱") || text.Contains("价") || text.Contains("东西"))
			{
				bool isBuying = currentClient.clientIntent == StoreClient.ClientIntent.BUY;
				string specialNpcTradeLine = GetSpecialNpcTradeLine(identifier, isBuying);
				if (specialNpcTradeLine != null)
				{
					dialogue.SetText(title, specialNpcTradeLine);
				}
			}
		}
		catch (System.Exception ex)
		{
			Core.LogMsg("[交易台词] 替换失败: " + ex.Message);
		}
	}
	private static void FixCringeDialogue(Dialogue dialogue, StoreClient client)
	{
		try
		{
			string title = client.displayName ?? LangHelper.T("未知", "Unknown");
			string text = client.identifier ?? LangHelper.T("未知", "Unknown");
			string text2 = dialogue.plainText ?? "";
			if (string.IsNullOrEmpty(text2))
			{
				return;
			}
			string item = text + "|" + text2;
			if (!_loggedDialogues.Contains(item))
			{
				_loggedDialogues.Add(item);
				if (text2.Length > 150)
				{
					_ = text2.Substring(0, 150) + "...";
				}
			}
			foreach (System.Collections.Generic.KeyValuePair<string, string> cringeReplacement in _cringeReplacements)
			{
				if (text2.Contains(cringeReplacement.Key, System.StringComparison.OrdinalIgnoreCase))
				{
					dialogue.SetText(title, cringeReplacement.Value);
					break;
				}
			}
		}
		catch (System.Exception ex)
		{
			Core.LogMsg("[对话修复] 失败: " + ex.Message);
		}
	}
	private static string GetSpecialNpcGreeting(string id)
	{
		switch (id)
		{
		case "retired_gunsmith":
			return WagePowerPerk.GunsmithDialogues[Core.Rng.Next(WagePowerPerk.GunsmithDialogues.Length)];
		case "retired_water_merchant":
			return WagePowerPerk.WaterMerchantDialogues[Core.Rng.Next(WagePowerPerk.WaterMerchantDialogues.Length)];
		case "retired_winemaker":
			return WagePowerPerk.AlcoholMerchantDialogues[Core.Rng.Next(WagePowerPerk.AlcoholMerchantDialogues.Length)];
		case "inventorStorage":
		case "inventor_storage":
			return DrDialogues[Core.Rng.Next(DrDialogues.Length)];
		default:
			return null;
		}
	}
	private static string GetSpecialNpcTradeLine(string id, bool isBuying)
	{
		switch (id)
		{
		case "retired_gunsmith":
			if (!isBuying)
			{
				return GunsmithSellTradeLines[Core.Rng.Next(GunsmithSellTradeLines.Length)];
			}
			return GunsmithBuyTradeLines[Core.Rng.Next(GunsmithBuyTradeLines.Length)];
		case "retired_water_merchant":
			if (!isBuying)
			{
				return WaterSellTradeLines[Core.Rng.Next(WaterSellTradeLines.Length)];
			}
			return WaterBuyTradeLines[Core.Rng.Next(WaterBuyTradeLines.Length)];
		case "retired_winemaker":
			if (!isBuying)
			{
				return WineSellTradeLines[Core.Rng.Next(WineSellTradeLines.Length)];
			}
			return WineBuyTradeLines[Core.Rng.Next(WineBuyTradeLines.Length)];
		case "inventorStorage":
		case "inventor_storage":
			if (!isBuying)
			{
				return DrSellTradeLines[Core.Rng.Next(DrSellTradeLines.Length)];
			}
			return DrBuyTradeLines[Core.Rng.Next(DrBuyTradeLines.Length)];
		default:
			return null;
		}
	}
}
