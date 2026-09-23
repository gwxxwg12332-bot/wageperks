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
	private static bool HasJacksonQueued()
	{
		try
		{
			PlayerStore instance = PlayerStore.Instance;
			if (instance == null)
			{
				return false;
			}
			Il2CppSystem.Collections.Generic.List<string> futurStoreClientIdQueue = instance.futurStoreClientIdQueue;
			if (futurStoreClientIdQueue != null)
			{
				for (int i = 0; i < futurStoreClientIdQueue.Count; i++)
				{
					if (futurStoreClientIdQueue[i] == "inventorStorage" || futurStoreClientIdQueue[i] == "inventor_storage")
					{
						return true;
					}
				}
			}
			StoreClientManager storeClientManager = instance.storeClientManager;
			if (storeClientManager != null)
			{
				Il2CppSystem.Collections.Generic.List<StoreClient> clientStack = storeClientManager.clientStack;
				if (clientStack != null)
				{
					for (int j = 0; j < clientStack.Count; j++)
					{
						StoreClient storeClient = clientStack[j];
						if (storeClient != null && storeClient.identifier != null && (storeClient.identifier == "inventorStorage" || storeClient.identifier == "inventor_storage"))
						{
							return true;
						}
					}
				}
			}
		}
		catch
		{
		}
		return false;
	}
	private static void EnsureJacksonIntroduced()
	{
		try
		{
			StoreClientData storeClientData = PlayerStore.GetStoreClientData();
			if (storeClientData != null && !storeClientData.isJacksonIntroduced)
			{
				storeClientData.isJacksonIntroduced = true;
			}
		}
		catch (System.Exception ex)
		{
			Core.LogMsg("[博士之友] 设置isJacksonIntroduced失败: " + ex.Message);
		}
	}
	internal static bool ScheduleJacksonToday()
	{
		try
		{
			if (!DrJacksonFriendPerk.IsActive())
			{
				return false;
			}
			if (PlayerStore.Instance == null)
			{
				return false;
			}
			int dayCounter = StoreStation.GetDayCounter();
			if (HasJacksonQueued())
			{
				return false;
			}
			// CR-14 同类问题：原来硬编码 7，配置项 DoctorVisitInterval 改了不生效 → 改为读配置（默认 7，零回归）
			int jacksonInterval = BuildConfig.DoctorVisitInterval > 0 ? BuildConfig.DoctorVisitInterval : 7;
			if (_lastScheduledDay >= 0 && dayCounter - _lastScheduledDay < jacksonInterval)
			{
				return false;
			}
			_lastScheduledDay = dayCounter;
			EnsureJacksonIntroduced();
			try
			{
				PlayerStore.Instance.QueueFuturClient("inventorStorage", 1);
			}
			catch (System.Exception ex)
			{
				Core.LogMsg("[博士之友] QueueFuturClient 失败: " + ex.Message);
				return false;
			}
			return true;
		}
		catch (System.Exception ex2)
		{
			Core.LogMsg("[博士之友] ScheduleJacksonToday失败: " + ex2.Message);
			return false;
		}
	}
	internal static void ResetJacksonSchedule()
	{
		_lastScheduledDay = -1;
	}
	public static bool PrefixOnHandleJacksonStorage(StoreClientManager __instance)
	{
		try
		{
			return DrJacksonFriendPerk.HandleJacksonStoragePatch.Prefix(__instance);
		}
		catch (System.Exception ex)
		{
			Core.LogMsg("[博士之友] 频率控制失败: " + ex.Message);
			return true;
		}
	}
}
