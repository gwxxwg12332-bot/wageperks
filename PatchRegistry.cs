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

public static class PatchRegistry
{

	public static void ApplyAll()
	{
		// 蛙哥工厂注入（阶段6前：保留业务逻辑，不进注册表）
		try
		{
			System.Func<StoreClient> @delegate = StoreClientListWanted.CreateWanted7;
			StoreClientListDict.storeClientDict["wanted7"] = DelegateSupport.ConvertDelegate<Il2CppSystem.Func<StoreClient>>(@delegate);
		}
		catch (System.Exception ex) { Core.LogMsg("[WagePerks] 注入 wanted7 失败: " + ex.Message); }
		try
		{
			System.Func<StoreClient> delegate2 = StoreClientListWanted.CreateWanted6;
			StoreClientListDict.storeClientDict["wanted6"] = DelegateSupport.ConvertDelegate<Il2CppSystem.Func<StoreClient>>(delegate2);
		}
		catch (System.Exception ex2) { Core.LogMsg("[WagePerks] 注入 wanted6 失败: " + ex2.Message); }
		try
		{
			System.Func<StoreClient> wb = () => { try { Core.LogMsg("[蛙哥] 工厂被调，创建前"); var c = StoreClientListWanted.CreateWanted7(); if (c != null) { c.identifier = "wage_brother"; c.displayName = LangHelper.T("蛙哥", "Wage Brother"); c.spriteName = "wage_brother_portrait"; try { c.SetBudget(1109707341, 1000); c.isWanted = false; c.isStealthWanted = false; c.reported = false; c.reportReward = 0; c.reputationPenalty = 0; c.wantedPosterId = null; c.realName = null; try { WageBrother.LoadPortrait(); } catch { } try { WageBrother.LoadCardSprite(); } catch { } c.isNoContributeToEvidence = true; c.noAcceptingContraband = false; c.clientIntent = StoreClient.ClientIntent.SELLNBUY;
                    try { c.mainDialogue.SetText("蛙哥", LangHelper.T("武器、药、吃的，我都收。", "Weapons, meds, food - I buy all.")); c.mainDialogue.nextDialogue = null; c.mainDialogue.SetEndAction(null); Core.LogMsg("[蛙哥] 台词已替换+nextDialogue清空"); } catch (System.Exception exdlg) { Core.LogMsg("[蛙哥] 台词替换失败: " + exdlg.Message); }
                    try { c.clientBuyingTagList = new Il2CppSystem.Collections.Generic.List<string>(); c.clientBuyingTagList.Add("WEAPON"); c.clientBuyingTagList.Add("NARCOTIC"); c.clientBuyingTagList.Add("FOOD"); c.clientBuyingTagList.Add("MEDICAL"); c.clientBuyingTagList.Add("LUXURY_ITEM"); try { c.clientBuyingIdList = new Il2CppSystem.Collections.Generic.List<string>(); } catch { } } catch { }
                    } catch { } } return c; } catch (System.Exception ex) { Core.LogMsg("[蛙哥] 工厂异常: " + ex.Message); return StoreClientListWanted.CreateWanted7(); } };
			StoreClientListDict.storeClientDict["wage_brother"] = DelegateSupport.ConvertDelegate<Il2CppSystem.Func<StoreClient>>(wb);
		}
		catch (System.Exception exwb) { Core.LogMsg("[WagePerks] 注入 wage_brother 失败: " + exwb.Message); }

		try
		{
			// 阶段6：统一注册入口——197条目声明式注册表
			PatchRegistryTable.ApplyAll();
		}
		catch (System.Exception ex3)
		{
			Core.LogMsg("[Patch] 应用补丁失败: " + ex3.Message);
		}
		LuckScoutPerk.InstallCompatGuards();
		ModCompat.LogLoadedConflicts();
		ManualPatcher.LogPatchSummary();
	}

	// [阶段6后废弃] 本区块及以下 Register* 方法已由 PatchRegistryTable.ApplyAll() 全权接管（PatchRegistry.ApplyAll 只调 PatchRegistryTable.ApplyAll，见 :65），零调用点死代码，勿在此注册新补丁
	// [阶段6后废弃区 · 2026-10-10 #47+#31 整区删除]
// 原因：ApplyAll 只调 PatchRegistryTable.ApplyAll()（:65 实锤），本区 Register* 方法零调用点
//   （RegisterCoreAndWanted/RegisterUnifiedDayStart/RegisterDiceNewsDrag/RegisterInitDirectoryAndStartUI/RegisterPerksTail
//    全库仅定义处命中——grep 实锤）——208 条死注册（149 有表制/Core 覆盖 + 57 无覆盖但同为死注册，
//    废弃区无活挂点，删除 0 功能丢失）。含 wanted6/7/wage_brother 工厂注入重复版——ApplyAll :42-60 有活跃内联版。
// 删除验证：编译 0 错误 + Patch自检 184 不变（死代码不注册）。
// 原内容存档：git 历史可查（commit 9d13ad5 之前）。

}