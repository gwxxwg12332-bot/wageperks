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

// 电池吞噬（Battery 特性）：电池自充电吞噬系统
namespace JacksonPerks;

internal static class BatteryCannibalism
{
	internal static string LastNewsLine;

	public static void OnDayStartPostfix()
	{
		try
		{
			// 09-21 新增：mod 版自我充能（绕过原生）
			TrySelfRecharge();
			System.Collections.Generic.List<GameItem> list = CollectBatteries();
			if (list == null || list.Count < 2)
			{
				return;
			}
			for (int num = list.Count - 1; num > 0; num--)
			{
				int index = Core.Rng.Next(num + 1);
				GameItem value = list[num];
				list[num] = list[index];
				list[index] = value;
			}
			System.Collections.Generic.List<string> list2 = new System.Collections.Generic.List<string>();
			for (int i = 0; i + 1 < list.Count; i += 2)
			{
				GameItem gameItem = list[i];
				GameItem gameItem2 = list[i + 1];
				if (BatteryScore(gameItem) == BatteryScore(gameItem2) || BatteryDim(gameItem) != BatteryDim(gameItem2))
				{
					if (Core.Rng.Next(2) == 0)
					{
						GameItem gameItem3 = gameItem;
						gameItem = gameItem2;
						gameItem2 = gameItem3;
					}
				}
				else if (BatteryScore(gameItem) < BatteryScore(gameItem2))
				{
					GameItem gameItem4 = gameItem;
					gameItem = gameItem2;
					gameItem2 = gameItem4;
				}
				System.Collections.Generic.List<string> list3 = new System.Collections.Generic.List<string>();
				int tagIntSafe = RobinCrusoePerk.GetTagIntSafe(gameItem, "power_source_item_energy");
				if (tagIntSafe > 0)
				{
					RobinCrusoePerk.AddTagInt(gameItem2, "power_source_item_energy", tagIntSafe);
					list3.Add("电量+" + tagIntSafe);
				}
				int tagIntSafe2 = RobinCrusoePerk.GetTagIntSafe(gameItem, "power_source_item_max_energy");
				if (tagIntSafe2 > 0)
				{
					RobinCrusoePerk.AddTagInt(gameItem2, "power_source_item_max_energy", tagIntSafe2);
					list3.Add("容量+" + tagIntSafe2);
				}
				int tagIntSafe3 = RobinCrusoePerk.GetTagIntSafe(gameItem, "power_source_item_recharge");
				if (tagIntSafe3 > 0)
				{
					RobinCrusoePerk.AddTagInt(gameItem2, "power_source_item_recharge", tagIntSafe3);
					list3.Add("自充+" + tagIntSafe3);
				}
				bool flag = false;
				try
				{
					flag = gameItem.IsTag("BREEDER_POWER_SOURCE_ITEM_TAG");
				}
				catch
				{
				}
				if (flag)
				{
					try
					{
						gameItem2.EnableTag("BREEDER_POWER_SOURCE_ITEM_TAG");
					}
					catch
					{
					}
					list3.Add("获得自充电");
				}
				long num2 = 0L;
				try
				{
					num2 = gameItem.unitBaseValue;
				}
				catch
				{
				}
				long num3 = num2 + tagIntSafe;
				if (num3 > 0)
				{
					try
					{
						gameItem2.unitBaseValue += num3;
					}
					catch
					{
					}
					list3.Add("价值+" + num3);
				}
				try
				{
					ContrabandHelper.InitContrabandItem(gameItem2, 3);
				}
				catch
				{
				}
				try
				{
					string text = gameItem2.shortDescription ?? "";
					if (!text.Contains("违禁原因"))
					{
						gameItem2.shortDescription = text + LangHelper.T("（违禁原因：能量融合改造的违禁电池）", " (Contraband: energy-fused illicit battery)");
					}
				}
				catch
				{
				}
				string name = ModCannibalism.GetName(gameItem2);
				string name2 = ModCannibalism.GetName(gameItem);
				try
				{
					gameItem.parentInventory?.Expel(gameItem);
				}
				catch
				{
				}
				try
				{
					gameItem.Destroy();
				}
				catch
				{
				}
				string text2 = ((list3.Count > 0) ? string.Join(" ", list3) : LangHelper.T("（无属性转移）", "(no stats transferred)"));
				string text3 = LangHelper.T(name + " 吞噬了 " + name2 + " · " + text2, name + " devoured " + name2 + " · " + text2);
				list2.Add(text3);
				try
				{
					StoreUIManager.Instance.Notify(text3);
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
					foreach (string item in list2)
					{
						instance.AddNightLog(item, "#7FC97F"); // 09-22 统一柔和绿
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
	}

	private static System.Collections.Generic.List<GameItem> CollectBatteries()
	{
		System.Collections.Generic.List<GameItem> list = new System.Collections.Generic.List<GameItem>();
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
			foreach (GameItem item in list2)
			{
				if (item == null || !RobinCrusoePerk.IsMachine(item))
				{
					continue;
				}
				string text = "";
				try
				{
					text = item.identifier ?? "";
				}
				catch
				{
				}
				if (text != "wage_gu_machine")
				{
					continue;
				}
				GameGridInventory guGrid = GuMachineSystem.GetGuGrid(item);
				if (guGrid == null || guGrid.childItems == null)
				{
					continue;
				}
				Il2CppSystem.Collections.Generic.List<GameItem>.Enumerator enumerator = guGrid.childItems.GetEnumerator();
				while (enumerator.MoveNext())
				{
					GameItem current3 = enumerator.Current;
					if (current3 != null)
					{
						bool flag = false;
						try
						{
							flag = current3.IsTag("power_source_item");
						}
						catch
						{
						}
						if (flag)
						{
							list.Add(current3);
						}
					}
				}
			}
		}
		catch
		{
		}
		return list;
	}

	private static int BatteryScore(GameItem b)
	{
		int num = 0;
		try
		{
			num += RobinCrusoePerk.GetTagIntSafe(b, "power_source_item_energy");
		}
		catch
		{
		}
		try
		{
			num += RobinCrusoePerk.GetTagIntSafe(b, "power_source_item_max_energy");
		}
		catch
		{
		}
		try
		{
			num += RobinCrusoePerk.GetTagIntSafe(b, "power_source_item_recharge");
		}
		catch
		{
		}
		try
		{
			num += (int)b.unitBaseValue;
		}
		catch
		{
		}
		return num;
	}

	private static int BatteryDim(GameItem b)
	{
		int num = 0;
		int num2 = 0;
		int num3 = 0;
		try
		{
			num = RobinCrusoePerk.GetTagIntSafe(b, "power_source_item_energy");
		}
		catch
		{
		}
		try
		{
			num2 = RobinCrusoePerk.GetTagIntSafe(b, "power_source_item_max_energy");
		}
		catch
		{
		}
		try
		{
			num3 = RobinCrusoePerk.GetTagIntSafe(b, "power_source_item_recharge");
		}
		catch
		{
		}
		if (num >= num2 && num >= num3)
		{
			return 0;
		}
		if (num2 >= num && num2 >= num3)
		{
			return 1;
		}
		if (num3 >= num && num3 >= num2)
		{
			return 2;
		}
		return 3;
	}

	// 09-21 新增：mod 版自我充能（绕过原生）
	private const string RECHARGE_TAG = "BREEDER_POWER_SOURCE_ITEM_TAG";
	private const string ENERGY_KEY = "power_source_item_energy";
	private const string MAX_ENERGY_KEY = "power_source_item_max_energy";
	private const string RECHARGE_KEY = "power_source_item_recharge";

	private static void TrySelfRecharge()
	{
		try
		{
			var batteries = GetAllRechargeableBatteries();
			if (batteries.Count == 0) return;
			int totalCharged = 0;
			foreach (var bat in batteries)
			{
				int charged = ChargeOneBattery(bat);
				totalCharged += charged;
			}
			if (totalCharged > 0)
			{
				NotifyHelper.NightLogRaw("自充电：" + batteries.Count + " 块电池 +" + totalCharged + " 电量");
				Core.LogMsg("[电池] 自充电: " + batteries.Count + " 块 +" + totalCharged + " 电量");
			}
		}
		catch (System.Exception ex) { Core.LogMsg("[电池] 自充电异常: " + ex.Message); }
	}

	private static System.Collections.Generic.List<GameItem> GetAllRechargeableBatteries()
	{
		var list = new System.Collections.Generic.List<GameItem>();
		try
		{
			var all = EmporiumEntry.Instance.GetAllItems();
			foreach (var item in all)
			{
				if (item == null) continue;
				try { if (!item.IsTag(RECHARGE_TAG)) continue; } catch { continue; }
				list.Add(item);
			}
		}
		catch (System.Exception ex) { Core.LogMsg("[BatteryCannibalism] 异常: " + ex.Message); }
		return list;
	}

	private static int ChargeOneBattery(GameItem bat)
	{
		try
		{
			int energy = TagHelper.GetInt(bat, ENERGY_KEY);
			int max = TagHelper.GetInt(bat, MAX_ENERGY_KEY);
			int recharge = TagHelper.GetInt(bat, RECHARGE_KEY);
			if (recharge <= 0) return 0;
			if (energy >= max) return 0;
			int newEnergy = System.Math.Min(max, energy + recharge);
			int charged = newEnergy - energy;
			TagHelper.SetInt(bat, ENERGY_KEY, newEnergy);
			return charged;
		}
		catch { return 0; }
	}
}