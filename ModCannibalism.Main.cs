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

internal static partial class ModCannibalism
{
	public static void OnDayStartPostfix()
	{
		try
		{
			// 10-13 B2 吞噬季独立触发（解除 RiskTaker 绑定）：
			//   面板购买开启（B2ServiceStore.IsCannibalOwned）→ 按玩家指定日期触发；
			//   未购买 → 不触发（v1.3.1 封存延续，旧档进行中状态不强制清）。
			if (!B2ServiceStore.IsCannibalOwned)
			{
				Core.LogMsg("[吞噬季] 未购买（B2 面板未开启），跳过今日互食");
				return;
			}
			int dayNow = DeterministicSchedule.CurrentDay;
			int num = -1; // 10-17 初始化防 CS0165（手动/自动分支互斥赋值，-1 不会越过下方 num>2 守卫）
			// 10-17 电话手动窗口：CannibalManualStartDay>0 优先（随时开始，持续 3 天；结束自动清零回归原调度）
			int manualStart = B2ServiceStore.CannibalManualStartDay;
			if (manualStart > 0)
			{
				num = dayNow - manualStart;
				if (num > 2)
				{
					B2ServiceStore.SetCannibalManualStartDay(0); // 手动窗口 3 天自然结束
					manualStart = 0;
				}
			}
			if (manualStart <= 0)
			{
				if (B2ServiceStore.CannibalMode == 0)
				{
					// 单次模式：第N天起持续 3 天（num=0 开始提示 / 1-2 互食 / 2 结束提示）
					int startDay = B2ServiceStore.CannibalDay;
					if (startDay <= 0 || dayNow < startDay) return;
					num = dayNow - startDay;
					if (num > 2) return;
				}
				else
				{
					// 周期模式：每N天自动触发（对齐原版 num 语义）
					int interval = B2ServiceStore.CannibalInterval > 0 ? B2ServiceStore.CannibalInterval : 10;
					num = dayNow % interval;
					if (num > 2) return;
				}
			}
			if (num == 0)
			{
				string text = LangHelper.T("吞噬季开始：机器里的模组将互相吞噬，持续 3 天", "Cannibalism Season begins: modules in machines will devour each other for 3 days");
				try
				{
					StoreUIManager.Instance.Notify(text);
				}
				catch
				{
				}
				LastNewsLine = text;
			}
			if (num > 2)
			{
				return;
			}
			System.Collections.Generic.List<System.Tuple<GameItem, System.Collections.Generic.List<GameItem>>> list = CollectMachines();
			System.Collections.Generic.List<string> list2 = new System.Collections.Generic.List<string>();
			foreach (System.Tuple<GameItem, System.Collections.Generic.List<GameItem>> item2 in list)
			{
				System.Collections.Generic.List<GameItem> item = item2.Item2;
				if (item == null || item.Count < 2)
				{
					continue;
				}
				GameItem gameItem = null;
				int num2 = -1;
				foreach (GameItem item3 in item)
				{
					int num3 = 0;
					num3 += RobinCrusoePerk.GetTagIntSafe(item3, "BONUS_PERCENTAGE_PERFORMANCE_INT");
					num3 += RobinCrusoePerk.GetTagIntSafe(item3, "BONUS_PERCENTAGE_EFFICIENCY_INT");
					num3 += RobinCrusoePerk.GetTagIntSafe(item3, "BONUS_PERCENTAGE_QUALITY_INT");
					if (num3 > num2)
					{
						num2 = num3;
						gameItem = item3;
					}
				}
				if (gameItem == null)
				{
					gameItem = item[0];
				}
				System.Collections.Generic.List<GameItem> list3 = new System.Collections.Generic.List<GameItem>();
				foreach (GameItem item4 in item)
				{
					if (item4 != gameItem)
					{
						list3.Add(item4);
					}
				}
				GameItem gameItem2 = list3[Core.Rng.Next(list3.Count)];
				int cannibalAbsorbPct = BuildConfig.CannibalAbsorbPct;
				System.Collections.Generic.List<string> list4 = new System.Collections.Generic.List<string>();
				string[] mOD_TAGS = MOD_TAGS;
				foreach (string text2 in mOD_TAGS)
				{
					int tagIntSafe = RobinCrusoePerk.GetTagIntSafe(gameItem2, text2);
					if (tagIntSafe <= 0)
					{
						continue;
					}
					int tagIntSafe2 = RobinCrusoePerk.GetTagIntSafe(gameItem, text2);
					int num4 = BuildConfig.CannibalCap - tagIntSafe2;
					if (num4 > 0)
					{
						int num5 = (int)((float)(tagIntSafe * cannibalAbsorbPct) / 100f);
						if (num5 > num4)
						{
							num5 = num4;
						}
						if (num5 > 0)
						{
							RobinCrusoePerk.AddTagInt(gameItem, text2, num5);
							RobinCrusoePerk.AddTagInt(gameItem, text2.Replace("BONUS_PERCENTAGE_", "CANNIBALISM_"), num5);
							list4.Add(text2.Replace("BONUS_PERCENTAGE_", "").Replace("_INT", "") + "+" + num5);
						}
					}
				}
				try
				{
					long currentValue = gameItem2.GetCurrentValue(useRetailMarkup: false, withChild: true, includeEvents: true, forceMarkup: false, 0L);
					if (currentValue > 0)
					{
						int num6 = (int)currentValue;
						try
						{
							ModuleEffectHelper.AdjustValue(gameItem, num6);
						}
						catch
						{
						}
						RobinCrusoePerk.AddTagInt(gameItem, "CANNIBALISM_VALUE", num6);
						list4.Add("价值+" + num6);
					}
				}
				catch
				{
				}
				try
				{
					ContrabandHelper.InitContrabandItem(gameItem, 3);
				}
				catch
				{
				}
				try
				{
					string text3 = gameItem.shortDescription ?? "";
					if (!text3.Contains("违禁原因"))
					{
						gameItem.shortDescription = text3 + LangHelper.T("（违禁原因：吞噬融合的强化模组）", " (Contraband: devour-fused empowered module)");
					}
				}
				catch
				{
				}
				string name = GetName(gameItem);
				string name2 = GetName(gameItem2);
				try
				{
					GameItem gameItem3 = null;
					try
					{
						gameItem3 = DirectoryMaster.Item("system_module_ruined");
					}
					catch
					{
					}
					if (gameItem3 != null)
					{
						try
						{
							GraphNodeStorage graphNodeStorage = gameItem2.parentInventory.Cast<GraphNodeStorage>();
							if (graphNodeStorage != null)
							{
								GraphUtils.TryAcceptAll(graphNodeStorage, gameItem3);
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
				try
				{
					gameItem2.Destroy(); // 销毁语义：直接Destroy
				}
				catch
				{
				}
				try
				{
					gameItem2.Destroy();
				}
				catch
				{
				}
				try
				{
					gameItem2.Destroy();
				}
				catch
				{
				}
				string text4 = ((list4.Count > 0) ? string.Join(" ", list4) : LangHelper.T("（无属性转移）", "(no stats transferred)"));
				string text5 = LangHelper.T(name + " 吞噬了 " + name2 + " · " + text4 + "（吸收 10%，留报废模组）", name + " devoured " + name2 + " · " + text4 + " (10% absorbed, A scrapped)");
				list2.Add(text5);
				try
				{
					StoreUIManager.Instance.Notify(text5);
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
					foreach (string item5 in list2)
					{
						instance.AddNightLog(item5, "#7FC97F");
					}
				}
				catch
				{
				}
			}
			if (num == 2)
			{
				string text6 = LangHelper.T("吞噬季今晚结束", "Cannibalism Season ends tonight");
				try
				{
					StoreUIManager.Instance.Notify(text6);
				}
				catch
				{
				}
				LastNewsLine = text6;
			}
		}
		catch
		{
		}
	}
	private static System.Collections.Generic.List<System.Tuple<GameItem, System.Collections.Generic.List<GameItem>>> CollectMachines()
	{
		System.Collections.Generic.List<System.Tuple<GameItem, System.Collections.Generic.List<GameItem>>> list = new System.Collections.Generic.List<System.Tuple<GameItem, System.Collections.Generic.List<GameItem>>>();
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
			try
			{
				_ = PlayerStore.Instance;
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
				GameGridInventory moduleInv = MachineHelper.GetModuleInv(item);
				if (moduleInv == null || moduleInv.childItems == null)
				{
					continue;
				}
				System.Collections.Generic.List<GameItem> list4 = new System.Collections.Generic.List<GameItem>();
				Il2CppSystem.Collections.Generic.List<GameItem>.Enumerator enumerator = moduleInv.childItems.GetEnumerator();
				while (enumerator.MoveNext())
				{
					GameItem current3 = enumerator.Current;
					bool flag = false;
					string text = "";
					try
					{
						text = ((current3 != null) ? (current3.identifier ?? "") : "");
					}
					catch
					{
					}
					if (!(text == "system_module_ruined") && !IsNodeModule(text))
					{
						try
						{
							flag = (current3?.IsTag("MODULE_TAG") ?? false) && !RobinCrusoePerk.IsExcludedModule(current3); // 09-20 P2-4 排除熔炉模组
						}
						catch
						{
							flag = false;
						}
						if (flag)
						{
							list4.Add(current3);
						}
					}
				}
				if (list4.Count >= 2)
				{
					list.Add(new System.Tuple<GameItem, System.Collections.Generic.List<GameItem>>(item, list4));
				}
			}
		}
		catch
		{
		}
		return list;
	}
}
