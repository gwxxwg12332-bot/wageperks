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

	public static void PostfixInitDirectory(ItemDirectory __instance)
	{
		try
		{
			CustomStorageContainer.RegisterToDirectory(__instance);
		}
		catch (System.Exception ex)
		{
			Core.LogMsg("[自定义储物箱] PostfixInitDirectory: " + ex.Message);
		}
		try
		{
			LuckScoutBackpackUpgrade.RegisterToDirectory(__instance);
		}
		catch (System.Exception ex2)
		{
			Core.LogMsg("[虚空珠] PostfixInitDirectory: " + ex2.Message);
		}
		try
		{
			GuMachineSystem.RegisterToDirectory(__instance);
			WageGirlSystem.RegisterToDirectory(__instance); // 09-21 蛙娘实体
		}
		catch (System.Exception ex3)
		{
			Core.LogMsg("[养蛊机] PostfixInitDirectory: " + ex3.Message);
		}
		try
		{
		}
		catch (System.Exception ex4)
		{
			Core.LogMsg("[采血包] PostfixInitDirectory: " + ex4.Message);
		}
	}

	public static void PostfixTradeSheetFoundryShop(object __result)
	{
		DrJacksonFriendPerk.FoundryShopPatch.Postfix(__result);
	}

	public static void PostfixTradeSheetEnergyFarmShop(object __result)
	{
		DrJacksonFriendPerk.EnergyFarmShopPatch.Postfix(__result);
	}

	public static void PrefixEmporiumShowAfterhourInv(EmporiumEntry __instance)
	{
		DrJacksonFriendPerk.EmporiumEntryShowAfterhourPatch.Prefix(__instance);
	}

	public static bool PrefixCreateAcidBottle(ref GameItem __result)
	{
		__result = null;
		return false;
	}

	public static bool PrefixCreateBaseBottle(ref GameItem __result)
	{
		__result = null;
		return false;
	}

	public static void PostfixStartOfDayOpenUI(StartOfDayUIManager __instance)
	{
		TryAppendNightReport(__instance);
	}

	public static void PostfixStartOfDayButtonClicked(StartOfDayUIManager __instance)
	{
		TryAppendNightReport(__instance);
	}

	public static void PostfixStartOfDayShowMorningReport(StartOfDayUIManager __instance)
	{
		TryAppendNightReport(__instance);
	}

	private static void TryAppendNightReport(StartOfDayUIManager __instance)
	{
		try
		{
			if (Core.NightReportQueue.Count == 0)
			{
				return;
			}
						string text = string.Join("\n", Core.NightReportQueue.ToArray());
			if (__instance == null || __instance.startOfDayTMPPrefab == null || __instance.contentGroupObject == null)
			{
				Core.LogMsg("[夜间报告] 报告UI未就绪，无法追加（队列保留不丢）"); // 09-20 拆包：Clear 移到判空后——UI 未就绪时不清队列（原清丢内容）
				return;
			}
			Core.NightReportQueue.Clear();
			GameObject gameObject = UnityEngine.Object.Instantiate(__instance.startOfDayTMPPrefab, __instance.contentGroupObject.transform);
			if (!(gameObject != null))
			{
				return;
			}
			gameObject.SetActive(value: true);
			bool flag = false;
			try
			{
				Component[] array = gameObject.GetComponentsInChildren<Component>(includeInactive: true);
				if (array != null)
				{
					foreach (Component component in array)
					{
						if (component == null)
						{
							continue;
						}
						string text2 = "";
						try
						{
							text2 = component.GetIl2CppType().FullName ?? "";
						}
						catch
						{
						}
						if (string.IsNullOrEmpty(text2))
						{
							try
							{
								text2 = component.GetType().Name ?? "";
							}
							catch
							{
							}
						}
						if (!text2.Contains("TextMeshProUGUI") && !text2.Contains("TMP_Text"))
						{
							continue;
						}
						bool flag2 = false;
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
									string text3 = methodInfo.Name ?? "";
									if (text3 == "set_text" || text3 == "SetText")
									{
										System.Reflection.ParameterInfo[] parameters = methodInfo.GetParameters();
										if (parameters != null && parameters.Length == 1)
										{
											methodInfo.Invoke(component, new object[1] { text });
											flag2 = true;
											break;
										}
									}
								}
							}
						}
						catch (System.Exception ex)
						{
							Core.LogMsg("[夜间报告] set_text 调用失败: " + ex.Message);
						}
						if (flag2)
						{
							flag = true;
							break;
						}
					}
				}
			}
			catch (System.Exception ex2)
			{
				Core.LogMsg("[夜间报告] 反射设置文本失败: " + ex2.Message);
			}
			if (flag)
			{
				return;
			}
			Core.LogMsg("[夜间报告] 未找到TMP文本组件，无法设置文本");
			try
			{
				Component[] array2 = gameObject.GetComponentsInChildren<Component>(includeInactive: true);
				if (array2 == null)
				{
					return;
				}
				foreach (Component component2 in array2)
				{
					if (component2 == null)
					{
						continue;
					}
					try
					{
						if (component2.GetIl2CppType().FullName == null)
						{
							_ = component2.GetType().Name;
						}
					}
					catch
					{
					}
					try
					{
						if (component2.gameObject != null)
						{
							_ = component2.gameObject.name;
						}
					}
					catch
					{
					}
				}
			}
			catch (System.Exception ex3)
			{
				Core.LogMsg("[夜间报告] 诊断dump失败: " + ex3.Message);
			}
		}
		catch (System.Exception ex4)
		{
			Core.LogMsg("[夜间报告] 追加失败: " + ex4.Message);
		}
	}
	public static void PrefixModReputation(ref double modValue)
	{
		try
		{
			if (modValue < 0) modValue = modValue / 2.0; // 精确减半：-3→-1.5、-5→-2.5、-7→-3.5、-10→-5
		}
		catch (System.Exception ex)
		{
			Core.LogMsg("[信誉减半] Prefix异常: " + ex.Message);
		}
	}


	// 09-22 制卡降上城区声望根因修复：mod 违禁品跳过"客户曝光"链（ClientExposeFeature）
	// 曝光链（拆包实锤）：PlacedItemForBuying → CanClientExposeAnyFeature → ClientExposeFeature →
	//   CanClientExposeThisFeature → 对话 + StoreReputation.ModReputation(客户faction, -4, true) + ExposeFeature(词条移除)
	// mod 违禁品（wage_ 前缀 / 吞噬融合 CANNIBALISM_VALUE / 电池融合 BREEDER_POWER_SOURCE_ITEM_TAG）
	// 被客户浏览即曝光 → 扣该客户 faction 声望 -4 + 词条划掉。跳过曝光：不扣声望、词条保留、违禁品打标保留。
	public static bool PrefixClientExposeFeature(GameItem gameItem)
	{
		try
		{
			if (gameItem == null) return true;
			string id = "";
			try { id = gameItem.identifier ?? ""; } catch { }
			bool isWage = id.StartsWith("wage_") || gameItem.IsTag("CANNIBALISM_VALUE") || gameItem.IsTag("BREEDER_POWER_SOURCE_ITEM_TAG");
			if (isWage)
			{
				Core.LogMsg("[声望修复] " + id + " 是 mod 物品，跳过客户曝光（不再扣声望/划词条）");
				return false;
			}
		}
		catch (System.Exception ex)
		{
			Core.LogMsg("[声望修复] Prefix异常: " + ex.Message);
		}
		return true;
	}
	// 诊断（用完删）：static ModReputation(String,int,bool) 日志——验证曝光扣声望走 static 版（value=-4）
	public static void PrefixModReputationStatic(string factionId, int value)
	{
		try { Core.LogMsg("[声望诊断] ModReputationStatic faction=" + factionId + " value=" + value); } catch { }
	}
}
