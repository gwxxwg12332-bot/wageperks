using Il2Cpp;
using MelonLoader;

[assembly: MelonInfo(typeof(WagePerks.Core), "Wage's Perks", "1.3.4", "jingdizhiwa123", null)]
[assembly: MelonGame("Questing Goose Studio", "Probably Stolen")]

namespace WagePerks;
public class Core : MelonMod
{
	public static readonly System.Collections.Generic.List<string> NightReportQueue = new System.Collections.Generic.List<string>();

#if DEBUG
	public static bool DebugMode = true; // 开发版：日志开
#else
	public static bool DebugMode = false; // 发布版：日志关
#endif

	// 09-22 用户拍板：全局物品黑名单（从所有我们的池子排除——蛙娘回归带物/流浪者随机/好物销赃等）
	// 稀有电子元件 rare_electronic（MaterialDirectory L611 实锤）不进入任何我们的池子
	public static readonly string[] ExcludedItemIds = { "rare_electronic" };

	public static readonly System.Random Rng = new System.Random();

	public static MelonLogger.Instance Log { get; private set; }

	public static event System.Action<string> DebugOutput;

	public static void AddNightReportLine(string line)
	{
		if (string.IsNullOrEmpty(line))
		{
			return;
		}
		try
		{
			NightReportQueue.Add(line);
		}
		catch
		{
		}
	}

	public override void OnInitializeMelon()
	{
		BuildConfig.InitPrefs();
		Log = base.LoggerInstance;
		Log.Msg("Wage's Perks v1.3.4 已加载 - 手动Patch模式");
		Log.Msg("【深空当铺】Wage's Perks QQ群：1109707341");
		ManualPatcher.Init(base.HarmonyInstance);
		try
		{
			DrJacksonFriendPerk.RegisterInventorStockHook();
		}
		catch (System.Exception ex)
		{
			Log.Msg("[博士之友] 注册失败 " + ex.Message);
		}
		try
		{
			ManualPatcher.TryPatchAllOverloads(typeof(PlayerStore), "AddDirectSellingItemToTable", null, "PostfixAddDirectSellingItemToTable");
		}
		catch
		{
		}
		try
		{
			ManualPatcher.TryPatch(typeof(StoreClientList), "PlaceInventorInventory", null, "PostfixPlaceInventorInventory", new System.Type[1] { typeof(bool) });
		}
		catch
		{
		}
		PatchRegistry.ApplyAll();
		// 1.3.2fix2 内置：WageAPI 入口接入（落盘挂点 priority=0 + 读档标志），原独立 WageAPI.dll 不再需要
		try { WageAPI.Core.Init(Log); } catch (System.Exception ex) { Log.Msg("[WageAPI] Init 失败: " + ex.Message); }
		// 10-03 阶段B：读档数据就绪事件（WageAPI 30帧后触发）→ 驱动引用恢复 + 防御重洗白（原 WageSaveStore.LoadIfPending 阶段2逻辑迁来）
		try { WageAPI.WageSaveStore.GameLoaded += Patches.OnStoreGameLoaded; } catch (System.Exception ex) { Log.Msg("[读档] GameLoaded 订阅失败: " + ex.Message); }
		// 预加载所有自定义图标（修复懒加载死锁）
		try { GuMachineSystem.LoadAllIcons(); } catch (System.Exception ex) { Log.Msg("[图标预加载] 养蛊机异常: " + ex.Message); }
		try { WageBrother.LoadCardSprite(); } catch (System.Exception ex) { Log.Msg("[图标预加载] 服务卡异常: " + ex.Message); }
		try { WageBrother.LoadAllIcons(); } catch (System.Exception ex) { Log.Msg("[图标预加载] 许可/充电器异常: " + ex.Message); }
		try
		{
			Log.Msg("[Patch] 蛙哥牛逼特性补丁应用成功（AddClient/DismissClient/NewDay）");
		}
		catch (System.Exception ex2)
		{
			Log.Msg("[Patch] 蛙哥牛逼特性补丁应用失败: " + ex2.Message);
		}
		try
		{
			if (StartingPerkList.Perks != null)
			{
				CustomStartingPerks.EnsureRegistered();
			}
		}
		catch
		{
		}
		PerkIconLoader.Initialize();
		Diagnostics.Register();
	}

	public override void OnUpdate()
	{
		// 1.3.2fix2 内置：WageAPI 读档轮询（LoadIfPending）——原 WageAPI.Core.OnUpdate 迁入
		try { WageAPI.Core.Update(); } catch { }
	}

	public override void OnGUI()
	{
		try
		{
			Diagnostics.OnGUI();
		}
		catch
		{
		}
		try
		{
			DestinyDice.DiceOnGUI();
		}
		catch
		{
		}
	}

	public static bool PerkActive(string perkId)
	{
		// 09-27 主菜单 guard（学何小鲁框架 PerkActiveMainMenuGuard；ISIL 实锤 get_Instance=场景查找不 new）：
		// 主菜单无真实 PlayerStore → 直接短路 false，避免 IsPerkActive 内部访问 PlayerStore 状态触发懒加载/异常路径
		try
		{
			if (PlayerStore.Instance == null) return false;
		}
		catch
		{
			return false;
		}
		try
		{
			return StartingPerk.IsPerkActive(perkId);
		}
		catch
		{
			return false;
		}
	}

	public static void DebugLog(string msg)
	{
		try
		{
			Core.DebugOutput?.Invoke(msg);
		}
		catch
		{
		}
	}

	public static void LogMsg(string msg)
	{
		if (DebugMode)
		{
			Log?.Msg(msg);
		}
	}
}
