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

	private static bool _pendingLoadGameRestore = false;

	private static int _loadGameRestoreDelayFrames = 0;

	internal static void FrameUpdate()
	{
		try
		{
			// 09-23 阶段1：统一存储层轮询加载（LoadGame Postfix 只设标志，延迟 N 帧后实际读文件）
			// 必须放在 PerkUI guard 之前——加载是初始化性质，不能被"特性选择界面打开"阻塞
			WageSaveStore.LoadIfPending();
		}
		catch
		{
			// 每帧防御：读档轮询异常跳过（LoadIfPending 自身有日志兜底），不阻塞帧循环
		}
		try
		{
			if (PerkUIController.Instance != null && PerkUIController.Instance.ui != null && PerkUIController.Instance.ui.activeSelf)
			{
				return;
			}
		}
		catch
		{
			// 每帧防御：PerkUI 状态检查（特性选择界面 guard，异常=UI未就绪，跳过本帧判断）
		}
		try
		{
			RobinCrusoePerk.HandleHotkeys();
		}
		catch
		{
			// 每帧防御：鲁滨逊热键处理异常跳过，不阻塞帧循环
		}
		try
		{
			LuckScoutBackpackUpgrade.ProcessPendingValidate();
		}
		catch
		{
			// 每帧防御：背包升级延迟校验异常跳过
		}
		try
		{
			LuckScoutBackpackUpgrade.OnUpdateRestore();
		}
		catch
		{
			// 每帧防御：背包升级读档恢复异常跳过
		}
		try
		{
			UpdatePendingLoadGameRestore();
		}
		catch
		{
			// 每帧防御：读档 30 帧延迟恢复异常跳过（容器未就绪属预期，下帧重试）
		}
		try
		{
			LuckScoutPerk.OnUpdateGiveRetry();
		}
		catch
		{
			// 每帧防御：寻宝者补发重试异常跳过
		}
		try
		{
			WagePowerPerk.OnUpdateBoxGiveRetry();
		}
		catch
		{
			// 每帧防御：蛙哥储物箱补发重试异常跳过
		}
		try
		{
			// 09-23 阶段0：WageGirl anim+move driver 从 Core.OnUpdate 迁来（硬约束#4：每帧逻辑必须走 InputActionManager.Update Postfix）
			WageGirlSystem.OnUpdateTick();
		}
		catch
		{
			// 每帧防御：蛙娘动画/移动驱动异常跳过（单帧渲染问题不崩溃游戏）
		}
		try
		{
			Diagnostics.OnUpdate();
		}
		catch
		{
			// 每帧防御：诊断面板刷新异常跳过
		}
	}

	internal static void PostfixInputActionManagerUpdate(InputActionManager __instance)
	{
		if (__instance == null)
		{
			return;
		}
		FrameUpdate();
		if (_keysDumped)
		{
			return;
		}
		_keysDumped = true;
		try
		{
			Il2CppSystem.Collections.Generic.List<InputActionHandler> actionHandlers = __instance.actionHandlers;
			if (actionHandlers == null)
			{
				return;
			}
			for (int i = 0; i < actionHandlers.Count; i++)
			{
				InputActionHandler inputActionHandler = actionHandlers[i];
				if (inputActionHandler == null)
				{
					continue;
				}
				try
				{
					Il2CppSystem.Collections.Generic.List<KeyCode> keyListeners = inputActionHandler.keyListeners;
					if (keyListeners != null && keyListeners.Count > 0)
					{
						string[] array = new string[keyListeners.Count];
						for (int j = 0; j < keyListeners.Count; j++)
						{
							array[j] = keyListeners[j].ToString();
						}
						string.Join(",", array);
					}
				}
				catch
				{
					// 防御：个别 input handler 的 keyListeners 结构异常跳过（不影响其余 handler）
				}
				try
				{
					_ = inputActionHandler.GetType().FullName;
				}
				catch
				{
					// 防御：handler 类型名读取异常跳过
				}
				try
				{
					_ = inputActionHandler.GetIl2CppType().FullName;
				}
				catch
				{
					// 防御：handler 遍历防御（单个异常不中断整体按键扫描）
				}
			}
		}
		catch (System.Exception ex)
		{
			Core.LogMsg("[按键] Dump 异常: " + ex.Message);
		}
	}

	public static void PostfixInitStartingPerks()
	{
		try
		{
			CustomStartingPerks.EnsureRegistered();
			Core.LogMsg("[特性] 已注册所有自定义特性");
		}
		catch (System.Exception ex)
		{
			Core.LogMsg("[特性] 注册失败: " + ex.Message);
		}
	}

	public static void PostfixOnNewGame()
	{
		try
		{
			WageGirlSystem.CleanDefaultRunOnNewGame(); // 09-22 蛙娘：新档清 default_run 残留（防串档/新档误判已存在）
			WageGirlSystem.ResetForNewGame(); // 09-20 蛙娘：新档硬重置状态（根治初次偷拿不触发——lastStealDay残留）
			RobinCrusoePerk.CleanDefaultRunOnNewGame(); // 09-22 鲁滨逊：新档清 default_run 残留（防未保存档残留串新档）
			WagePowerPerk.ResetState();
			CustomStartingPerks.NotifyNewGame();
		}
		catch (System.Exception ex)
		{
			Core.LogMsg("[特性] 新游戏补丁失败: " + ex.Message);
		}
	}

	// ============================================================
	// 阶段2 统一生命周期入口
	// ------------------------------------------------------------
	// PostfixUnifiedDayStart：挂 StoreEventManager.OnDayStart（唯一每日权威信号）。
	//   拆包依据：BeginDay → StoreStation.StartDay → OnDayStart → OnNewDay → SecData → CommissaryData，
	//   是**同一条链**；而 StoreClientManager.OnNewDay 只有 8 字节（私有计数自增），且 [rbx+0x88] 为 null 时
	//   会被静默 return（OnDayStart 侧为 Interrupt，可靠）→ 权威信号取 OnDayStart。
	// PostfixSaveGame：挂 PlayerStore.SaveGame，priority 用默认 400（**高于** WageSaveStore 的 0），
	//   保证各特性先写内存、统一落盘最后执行。特性契约：OnSaveGame 只许写内存，禁止自己 Flush。
	// ============================================================
	public static void PostfixUnifiedDayStart()
	{
		try
		{
			CustomStartingPerks.NotifyDayStart();
		}
		catch (System.Exception ex)
		{
			Core.LogMsg("[特性] 统一每日驱动失败: " + ex.Message);
		}
	}

	public static void PostfixSaveGame()
	{
		try
		{
			CustomStartingPerks.NotifySaveGame();
		}
		catch (System.Exception ex)
		{
			Core.LogMsg("[特性] 统一存档驱动失败: " + ex.Message);
		}
	}

	public static void HandleInitialItemPostfix()
	{
		try
		{
			try
			{
				WagePowerPerk._storageBoxGiven = false;
			}
			catch
			{
				// 防御：初始发放重置——蛙哥储物箱标记重置失败不阻断后续发放
			}
			try
			{
				DestinyDicePerk._diceGiven = false;
			}
			catch
			{
				// 防御：初始发放重置——命运骰标记重置失败不阻断后续发放
			}
			try
			{
				LuckScoutPerk.ResetGiveFlag();
			}
			catch
			{
				// 防御：初始发放重置——寻宝者工具包标记重置失败不阻断后续发放
			}
			if (WagePowerPerk.IsActive() && !WagePowerPerk._storageBoxGiven)
			{
				WagePowerPerk.TryGiveStorageBox();
			}
			if (LuckScoutPerk.IsActive())
			{
				LuckScoutPerk.TryGiveKit();
			}
			try
			{
				DestinyDicePerk.GiveIfActive();
			}
			catch
			{
				// 防御：命运骰补发失败跳过（GiveIfActive 内部已断言）
			}
			try
			{
				RobinCrusoePerk.TrySetupNewRun();
			}
			catch
			{
				// 防御：鲁滨逊新档初始化失败跳过（不影响其他特性发放）
			}
		}
		catch (System.Exception ex)
		{
			Core.LogMsg("[蛙哥牛逼] HandleInitialItemPostfix异常: " + ex.Message);
		}
	}

	public static void PostfixOnBeginDay()
	{
		try
		{
			RobinCrusoePerk.TickWantedSupplierDaily();
			ForceInspectionToday();
			ApplyBadLuck();
			if (DrJacksonFriendPerk.IsActive() && PlayerStore.Instance != null)
			{
				ScheduleJacksonToday();
			}
		}
		catch (System.Exception ex)
		{
			Core.LogMsg("[博士之友] BeginDay失败: " + ex.Message);
		}
	}

	public static void PostfixOnNewDay()
	{
		try
		{
			WineLoverPerk.ClearHangover();
			SpecialNpcManager.OnNewDay();
			try
			{
				if (DrJacksonFriendPerk.IsActive() && PlayerStore.Instance != null)
				{
					ScheduleJacksonToday();
				}
			}
			catch (System.Exception ex)
			{
				Core.LogMsg("[博士之友] QueueFuturClient失败: " + ex.Message);
			}
			ForceInspectionToday();
			ApplyBadLuck();
		}
		catch (System.Exception ex2)
		{
			Core.LogMsg("[特性] 新的一天补丁失败: " + ex2.Message);
		}
	}

	public static void PostfixPerkUiOpen(PerkUIController __instance)
	{
		try
		{
			MadnessRoller.ResetLock();
			CustomStartingPerks.EnsureRegistered();
			Core.LogMsg("[特性UI] OpenUI时确保特性已注册");
			CustomStartingPerks.EnsurePickerElements(__instance);
		}
		catch (System.Exception ex)
		{
			Core.LogMsg("[特性UI] 打开失败: " + ex.Message);
		}
	}

	// 09-24 保险丝：原生 OpenUI 内部调 LocHelper.Get() 可能崩溃（异步本地化未就绪）
	// Finalizer 拦住异常，不让整个特性界面死掉
	public static System.Exception FinalizerPerkUiOpen(PerkUIController __instance, System.Exception __exception)
	{
		if (__exception != null)
		{
			Core.LogMsg("[特性UI] OpenUI 原生异常已被 Finalizer 兜住: " + __exception.Message);
			return null; // 返回 null = 吞掉异常，不扩散
		}
		return null;
	}

	public static System.Exception FinalizerPerkUiOnChange(PerkUIController __instance, System.Exception __exception)
	{
		if (__exception != null)
		{
			Core.LogMsg("[特性UI] OnChange 原生异常已被 Finalizer 兜住: " + __exception.Message);
			return null;
		}
		return null;
	}

	public static void PostfixIconLoaderStart(StartingPerkIconLoader __instance)
	{
		try
		{
			bool flag = false;
			CustomStartingPerk[] all;
			try
			{
				System.Reflection.FieldInfo field = typeof(StartingPerkIconLoader).GetField("perkIcons", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
				if (field != null)
				{
					object value = field.GetValue(null);
					if (value != null)
					{
						System.Reflection.PropertyInfo property = value.GetType().GetProperty("Item");
						all = CustomStartingPerks.All;
						foreach (CustomStartingPerk customStartingPerk in all)
						{
							try
							{
								string text = customStartingPerk.Id.Replace("\0", "").Trim();
								if (PerkIconLoader.HasCustomIcon(customStartingPerk.Id))
								{
									Sprite perkIcon = PerkIconLoader.GetPerkIcon(customStartingPerk.Id);
									if (perkIcon != null && property != null)
									{
										property.SetValue(value, perkIcon, new object[1] { text });
									}
								}
							}
							catch (System.Exception ex)
							{
								MelonLogger.Error("[特性图标] 注入图标失败 " + customStartingPerk.Id + ": " + ex.Message);
							}
						}
						flag = true;
					}
				}
			}
			catch (System.Exception ex2)
			{
				Core.LogMsg("[特性图标] 字典方式失败: " + ex2.Message);
			}
			if (flag)
			{
				return;
			}
			System.Reflection.PropertyInfo property2 = __instance.GetType().GetProperty("perkIconList", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
			if (property2 == null)
			{
				return;
			}
			object obj = property2.GetValue(__instance);
			if (obj == null)
			{
				obj = System.Activator.CreateInstance(typeof(Il2CppSystem.Collections.Generic.List<PerkIconEntry>));
				property2.SetValue(__instance, obj);
			}
			System.Reflection.MethodInfo method = obj.GetType().GetMethod("Add");
			if (method == null)
			{
				return;
			}
			all = CustomStartingPerks.All;
			foreach (CustomStartingPerk customStartingPerk2 in all)
			{
				try
				{
					string perkName = customStartingPerk2.Id.Replace("\0", "").Trim();
					if (PerkIconLoader.HasCustomIcon(customStartingPerk2.Id))
					{
						Sprite perkIcon2 = PerkIconLoader.GetPerkIcon(customStartingPerk2.Id);
						if (perkIcon2 != null)
						{
							PerkIconEntry perkIconEntry = new PerkIconEntry();
							perkIconEntry.perkName = perkName;
							perkIconEntry.Sprite = perkIcon2;
							method.Invoke(obj, new object[1] { perkIconEntry });
						}
					}
				}
				catch (System.Exception ex3)
				{
					MelonLogger.Error("[特性图标] 注入图标失败 " + customStartingPerk2.Id + ": " + ex3.Message);
				}
			}
		}
		catch (System.Exception ex4)
		{
			MelonLogger.Error("[特性图标] StartingPerkIconLoaderStartPatch失败: " + ex4.Message + "\n" + ex4.StackTrace);
		}
	}

	public static void PostfixStartingPerkElementStart(StartingPerkElement __instance)
	{
		try
		{
			CustomStartingPerks.EnsureElement(__instance);
		}
		catch (System.Exception ex)
		{
			Core.LogMsg("[特性UI] 元素启动失败: " + ex.Message);
		}
	}

	public static void PostfixOnLoadGame()
	{
		try
		{
			try { WageGirlSystem.ClearMemStats(); } catch { } // 09-20 修：读档清蛙娘内存缓存
			try { RobinCrusoePerk.ClearMemBlood(); } catch { } // 09-20 修：读档清鲁滨逊血量缓存
			try { RobinCrusoePerk.ClearWantedQueued(); } catch { } // 09-20 修：读档清供应商排期标记
			try { WageSaveStore.OnLoadGame(); } catch { } // 09-23 阶段1：统一存储层读档标志（实际加载走 FrameUpdate 轮询）
			_pendingLoadGameRestore = true;
			_loadGameRestoreDelayFrames = 30;
		}
		catch (System.Exception ex)
		{
			Core.LogMsg("[LoadGame] PostfixOnLoadGame异常: " + ex.Message);
		}
	}

	public static void UpdatePendingLoadGameRestore()
	{
		if (!_pendingLoadGameRestore)
		{
			return;
		}
		if (_loadGameRestoreDelayFrames > 0)
		{
			_loadGameRestoreDelayFrames--;
			return;
		}
		_pendingLoadGameRestore = false;
		try
		{
			NewStartTypeUI.RecheckIfPending(); // 09-22 runID 已恢复：清 IsMarkedRun 挂起标记（后续判定自然重判）
			if (WagePowerPerk.IsActive())
			{
				WagePowerPerk.LoadState();
			}
			if (WineLoverPerk.IsActive())
			{
				WineLoverPerk.LoadHangoverState();
			}
			try { WageGirlSystem.OnGameLoadedReset(); } catch { } // 09-23 恢复：ModHook 禁用后此逻辑未随迁（蛙娘实体/动画帧缓存读档重置 + Leave 兜底校验）
			if (DrJacksonFriendPerk.IsActive() && PlayerStore.Instance != null)
			{
				ScheduleJacksonToday();
			}
		}
		catch (System.Exception ex)
		{
			Core.LogMsg("[博士之友] LoadGame QueueFuturClient失败: " + ex.Message);
		}
	}

	public static bool PrefixLoadFromAtlas(string atlasPath, string name, ref Sprite __result)
	{
		try
		{
			if (name == "custom_storage_box_sprite")
			{
				Sprite customSprite = CustomStorageContainer.GetCustomSprite();
				if (customSprite != null)
				{
					__result = customSprite;
					return false;
				}
			}
		}
		catch (System.Exception ex)
		{
			Core.LogMsg("[自定义sprite] PrefixLoadFromAtlas异常: " + ex.Message);
		}
		return true;
	}

	public static void PrefixOnPointerClick(StartingPerkElement __instance)
	{
		try
		{
			// 09-22 精神错乱：锁定后不能再点
			if (MadnessRoller.IsLocked) return;
			// 09-22 精神错乱：点击精神错乱特性自动抽取
			if (__instance != null && __instance.id == "精神错乱")
			{
				MadnessRoller.Roll(PerkUIController.Instance);
				return;
			}
			NewGameData instance = NewGameData.Instance;
			if (instance != null && !instance.isInMainMenu)
			{
				instance.isInMainMenu = true;
			}
		}
		catch (System.Exception ex)
		{
			Core.LogMsg("[选点] Prefix异常: " + ex.Message);
		}
	}

	public static void PostfixOnPointerClick(StartingPerkElement __instance)
	{
	}
}

