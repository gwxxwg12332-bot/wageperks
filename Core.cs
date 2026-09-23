using Il2Cpp;
using MelonLoader;

[assembly: MelonInfo(typeof(JacksonPerks.Core), "Wage's Perks", "1.2.9", "jingdizhiwa123", null)]
[assembly: MelonGame("Questing Goose Studio", "Probably Stolen")]

namespace JacksonPerks;
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
		Log.Msg("Wage's Perks v1.2.9 已加载 - 手动Patch模式");
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
