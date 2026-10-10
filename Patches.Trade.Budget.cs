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

}
