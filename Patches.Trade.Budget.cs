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
					// 10-10 数值边界修复批#2（P1）：先 clamp 再窄化——原 (int) 直接窄化，超上限 double→int 回绕成小正数（完全错误预算，比负数更隐蔽）；clamp 后窄化两分支都兜住
					// 常量与子项1 同值（1000 万语义一致；WageAPI.NumClamp 统一工具留 P3 债）
					const double BUDGET_CAP = 10_000_000.0;
					double calc = (double)clientCash * (1.0 + (double)budgetBonusPct / 100.0);
					if (calc > BUDGET_CAP) calc = BUDGET_CAP;
					int newCash = (int)System.Math.Max(0, calc);
					__result.clientCash = newCash;
				}
			}
		}
		catch (System.Exception ex)
		{
			Core.LogMsg("[空间站鲁滨逊] clientCash联动异常: " + ex.Message);
		}
	}

	// 10-10 四问题批#3a：以下 5 个交易死方法已删（10-07 鲁滨逊交易归 WS；表 PatchRegistryTable.cs 无对应条目，仅被废弃区 PatchRegistry.cs 死代码引用）
	//   已删清单（见 git diff / 本文件删除区）：
	//   - PrefixBargainUIManagerOfferBuyingMarkup（原 :92，WS SurvivalTrade.PrefixBargainUIManagerOfferBuyingMarkup 承接）
	//   - PostfixGetDealMakerBonus（原 :107，Patches 版死——注意 WageGirlSystem.Budget.cs:11 蛙娘同名活方法未动）
	//   - PostfixItemFeatureListBargainBuyingMarkup（原 :122，WS 承接）
	//   - PostfixItemFeatureGetActualDisplay（原 :140，WS 承接）
	//   - PostfixStoreClientOnDealAccepted（原 :156，WS SurvivalTrade.PostfixStoreClientOnDealAccepted 承接）

	// 10-06 议价加成可见性已删（原 PostfixItemFeatureGetActualDisplay——WS SurvivalTrade.PostfixItemFeatureGetActualDisplay 承接，10-08 已部署）

	// ===== FIX17（拆包回传 10-15）：客户预算显示追加「原版预算+加成来源」 =====
	// 拆包实锤：NegociationUIManager.clientBudget(0xA8 TMP)=客户预算唯一显示位；RefreshUI() 唯一写点
	//   （触发面 11 个：Update 0.2s 定时 + OpenUI/ToggleUI + Buy/Sell 6 + CycleBarterOfferSet + StoreClientMono.OnClick + ItemFeature.ExposeFeature）；
	//   显示值=StoreClient.clientBudget(0x34).ToString()——我方 OverrideBudget/ModBudget 直写后 TMP 自动显示加成值；
	//   本 Postfix 只追加来源说明（public 0xA8 直接读写）。⚠️ RefreshUI 开头无客户分支会清空 TMP（dump :10747-10762）→ 判空。
	public static void PostfixRefreshUI(NegociationUIManager __instance)
	{
		try
		{
			if (__instance == null || __instance.clientBudget == null) return;
			StoreClient client = GetCurrentTradingClient();
			if (client == null || string.IsNullOrEmpty(client.identifier)) return;
			string src = BuildBudgetSourceText(client);
			if (src.Length == 0) return; // 无加成客户保持原版显示
			string cur = __instance.clientBudget.text;
			if (string.IsNullOrEmpty(cur)) return;
			__instance.clientBudget.text = cur + src;
		}
		catch (System.Exception ex) { Core.LogMsg("[预算显示] 异常: " + ex.Message); }
	}

	// 来源判定与 WageGirlSystem.Budget.cs PostfixApplyBudgetModifier 同序（互斥 return：宿醉 → 干燥 → 好感；蛙哥固定不乘好感）
	private static string BuildBudgetSourceText(StoreClient client)
	{
		try
		{
			int cur = client.clientBudget;
			int orig = 0;
			bool hasOrig = CompatibilityPatches.TryGetOrigBudget(client, out orig);
			if (client.identifier == "wage_brother")
			{
				if (hasOrig && orig != cur)
					return LangHelper.T("（原版 " + orig + " → 蛙哥固定预算）", " (orig " + orig + " -> Wage fixed budget)");
				return LangHelper.T("（蛙哥固定预算）", " (Wage fixed budget)");
			}
			if (WineLoverPerk.IsHungover()) // 酒徒宿醉：预算-10%
				return LangHelper.T("（原版 " + (hasOrig ? orig.ToString() : cur.ToString()) + " → " + cur + "，-10% 酒徒宿醉）", " (orig " + (hasOrig ? orig.ToString() : cur.ToString()) + " -> " + cur + ", -10% hungover)");
			if (DryAirPerk.IsActive() && WageGirlSystem.BuysWaterOrBooze(client)) // 干燥空气：水酒客户+25%
				return LangHelper.T("（原版 " + (hasOrig ? orig.ToString() : cur.ToString()) + " → " + cur + "，+25% 干燥空气）", " (orig " + (hasOrig ? orig.ToString() : cur.ToString()) + " -> " + cur + ", +25% dry air)");
			if (WageGirlSystem.Exists()) // 蛙娘在场：好感分档（与 Budget.cs:44-53 同源）
			{
				double affB = WageGirlSystem.GetAffectionExact();
				float mult = affB < BuildConfig.WageGirlBudgetAffLow ? BuildConfig.WageGirlBudgetMultLow
					: (affB < BuildConfig.WageGirlBudgetAffMid ? BuildConfig.WageGirlBudgetMultMid : BuildConfig.WageGirlBudgetMultHigh);
				string multDesc = "";
				if (B2ServiceStore.GetEffectLevel(WagePowerPerk.PerkId) >= 3 && affB >= BuildConfig.WageGirlBudgetAffMid)
					multDesc = LangHelper.T("，蛙哥牛逼Lv3", ", WageBrother Lv3");
				int o = hasOrig ? orig : (int)(cur / mult);
				return LangHelper.T("（原版 " + o + " → " + cur + "，蛙娘好感 ×" + mult + multDesc + "，好感 " + (int)affB + "）", " (orig " + o + " -> " + cur + ", affection x" + mult + multDesc + ", aff " + (int)affB + ")");
			}
			return "";
		}
		catch (System.Exception ex) { Core.LogMsg("[预算显示] 来源判定异常: " + ex.Message); return ""; }
	}

}
