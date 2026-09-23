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
	private static int _inspectionTriggeredDay = -1;

	private static bool _keysDumped = false;

	public static bool PrefixHandleInspectionClient(StoreClientManager __instance)
	{
		try
		{
			_secVipOverride = RiskTakerPerk.IsActive();
		}
		catch
		{
		}
		return true;
	}

	public static void PostfixIsPerkUnlocked(string perkId, ref bool __result)
	{
		try
		{
			if (_secVipOverride && perkId == "SEC_VIP")
			{
				__result = false;
			}
		}
		catch
		{
		}
	}

	public static void PostfixHandleInspectionClient()
	{
		try
		{
			_secVipOverride = false;
		}
		catch
		{
		}
	}

	public static void ForceInspectionToday()
	{
		try
		{
			if (!RiskTakerPerk.IsActive() && !ThiefMagnetPerk.IsActive())
			{
				return;
			}
			int num = 1;
			try
			{
				num = StoreStation.GetDayCounter();
			}
			catch
			{
			}
			if (_inspectionTriggeredDay == num)
			{
				return;
			}
			bool num2 = RiskTakerPerk.IsActive();
			bool flag = ThiefMagnetPerk.IsActive();
			bool flag2 = num2;
			if (!num2 && flag)
			{
				flag2 = DeterministicRandom.NextBool("thief_magnet_inspection", num, 0.2);
			}
			if (!flag2)
			{
				return;
			}
			try
			{
				if (!RiskTakerPerk.IsActive() && !ThiefMagnetPerk.IsActive())
				{
					return;
				}
				PlayerStore instance = PlayerStore.Instance;
				if (instance == null || instance.secData == null)
				{
					return;
				}
				StoreClientManager storeClientManager = null;
				try
				{
					System.Reflection.PropertyInfo property = typeof(PlayerStore).GetProperty("storeClientManager", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
					if (property != null)
					{
						storeClientManager = property.GetValue(instance) as StoreClientManager;
					}
					if (storeClientManager == null)
					{
						System.Reflection.FieldInfo field = typeof(PlayerStore).GetField("storeClientManager", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
						if (field != null)
						{
							storeClientManager = field.GetValue(instance) as StoreClientManager;
						}
					}
				}
				catch (System.Exception ex)
				{
					Core.LogMsg("[刀尖舔血] 获取storeClientManager失败: " + ex.Message);
				}
				if (storeClientManager != null)
				{
					try
					{
						storeClientManager.dayUntilInspection = 0;
						storeClientManager.daySinceInspection = 100;
						storeClientManager.inspectionSeeded = true;
						storeClientManager.ResetInspection();
						storeClientManager.dayUntilInspection = 0;
						storeClientManager.daySinceInspection = 100;
						storeClientManager.inspectionSeeded = true;
						try
						{
							ContrabandHelper.StartInspection(instance.secData.GetInspectorPersonality());
							try
							{
								_inspectionTriggeredDay = StoreStation.GetDayCounter();
							}
							catch
							{
								_inspectionTriggeredDay = num;
							}
						}
						catch (System.Exception ex2)
						{
							Core.LogMsg("[治安检查] StartInspection失败: " + ex2.Message);
						}
						try
						{
							Il2CppSystem.Collections.Generic.List<StoreClient> clientStack = storeClientManager.clientStack;
							int num3 = clientStack?.Count ?? (-1);
							if (clientStack != null && num3 > 0)
							{
								for (int i = 0; i < num3; i++)
								{
									try
									{
										_ = clientStack[i];
									}
									catch
									{
									}
								}
							}
							return;
						}
						catch (System.Exception ex3)
						{
							Core.LogMsg("[治安检查] clientStack诊断失败: " + ex3.Message);
							return;
						}
					}
					catch (System.Exception ex4)
					{
						Core.LogMsg("[刀尖舔血] 设置检查条件失败: " + ex4.Message);
						return;
					}
				}
				try
				{
					ContrabandHelper.StartInspection(instance.secData.GetInspectorPersonality());
					try
					{
						_inspectionTriggeredDay = StoreStation.GetDayCounter();
					}
					catch
					{
						_inspectionTriggeredDay = num;
					}
				}
				catch (System.Exception ex5)
				{
					Core.LogMsg("[刀尖舔血] StartInspection失败: " + ex5.Message);
				}
			}
			catch (System.Exception ex6)
			{
				Core.LogMsg("[刀尖舔血] 触发检查失败: " + ex6.Message);
			}
		}
		catch (System.Exception ex7)
		{
			Core.LogMsg("[治安检查] 强制检查失败: " + ex7.Message);
		}
	}

	public static void ApplyBadLuck()
	{
		try
		{
			if (!BadLuckPerk.IsActive())
			{
				return;
			}
			PlayerStore instance = PlayerStore.Instance;
			if (instance == null)
			{
				Core.LogMsg("[霉运缠身] PlayerStore为null");
				return;
			}
		int dayCounter = StoreStation.GetDayCounter();
		// 阶段1：防重标记入统一层——强退后标记随 ES3 一起回退，重放当天会重新扣款，
		// 与现金回退保持一致（修"扣款后强退→读档→标记残留→当天不再扣"的逃罚漏洞）
		if (WageSaveStore.GetInt("BadLuck", "last_day", -1) == dayCounter)
			{
				return;
			}
			int num = DeterministicRandom.Next("bad_luck", dayCounter, 1, 201); // 09-22 改：100-1500 → 1-200
			bool flag = false;
			try
			{
				System.Reflection.PropertyInfo property = typeof(PlayerStore).GetProperty("playerCash", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
				if (property != null)
				{
					int num2 = (int)property.GetValue(instance);
					property.SetValue(instance, num2 - num);
					flag = true;
				}
			}
			catch (System.Exception ex)
			{
				Core.LogMsg("[霉运缠身] 属性扣款失败: " + ex.Message);
			}
			if (!flag)
			{
				try
				{
					System.Reflection.FieldInfo field = typeof(PlayerStore).GetField("playerCash", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
					if (field != null)
					{
						int num3 = (int)field.GetValue(instance);
						field.SetValue(instance, num3 - num);
						flag = true;
					}
				}
				catch (System.Exception ex2)
				{
					Core.LogMsg("[霉运缠身] 字段扣款失败: " + ex2.Message);
				}
			}
			if (flag)
			{
				WageSaveStore.SetInt("BadLuck", "last_day", dayCounter);
				try { var _ps = PlayerStore.Instance; if (_ps != null) _ps.AddNightLog("[霉运缠身] " + LangHelper.T("昨晚打烊时，有人趁夜色摸走了你", "Last night after closing, someone slipped in and took") + " " + num + " " + LangHelper.T("信用点。", "credits."), "#7FC97F"); } catch { } // ① 原生夜报（09-22 统一柔和绿）
				Core.AddNightReportLine("[霉运缠身] " + LangHelper.T("昨晚打烊时，有人趁夜色摸走了你", "Last night after closing, someone slipped in and took") + " " + num + " " + LangHelper.T("信用点。", "credits."));
			}
			else
			{
				Core.LogMsg("[霉运缠身] 扣钱失败：playerCash 属性/字段均未找到");
			}
		}
		catch (System.Exception ex3)
		{
			Core.LogMsg("[霉运缠身] 失败: " + ex3.Message);
		}
	}
}
