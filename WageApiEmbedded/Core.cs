using HarmonyLib;
using Il2Cpp;
using MelonLoader;

namespace WageAPI;

/// <summary>
/// WageAPI 入口（2026-10-03 提炼自 Wage's Perks）：
/// 多 mod 共用共享 API 层（LangHelper / WageSaveStore / FoodIdentifyHelper / ModRegistry）。
/// 零依赖铁律：只引 Unity/Harmony/Il2CppInterop/MelonLoader + 游戏程序集，不引任何 mod。
/// 生命周期职责（评审补丁 P1-5）：统一落盘挂点（SaveGame/LoadGame/EndDay Postfix，priority=0 最后跑）+ 读档轮询驱动。
/// 
/// 【1.3.2fix2 内置改造】本文件从独立 DLL 内置进 WagePerks.dll（WageApiEmbedded\Core.cs）：
/// - 去掉 MelonMod/MelonInfo/MelonGame（一个 DLL 只能有一个 MelonMod 入口，入口归 WagePerks.Core）
/// - OnInitializeMelon → Init(logger)（由 WagePerks.Core.OnInitializeMelon 调用）
/// - OnUpdate → Update()（由 WagePerks.Core.OnUpdate 调用）
/// 独立工程 WageAPI\Core.cs 保留原 MelonMod 形态（供 WageSurvival v0.1.3+ 未来独立使用）。
/// </summary>
public static class Core
{
    public static MelonLogger.Instance Log { get; private set; }

    private static HarmonyLib.Harmony _harmony;

    public static void Init(MelonLogger.Instance logger)
    {
        Log = logger;
        Log.Msg("WageAPI v1.0.0 已加载（内置 WageSurvival.dll）- 多mod共享API层");
        Log.Msg("QQ群：1109707341");

        _harmony = new HarmonyLib.Harmony("com.wageapi");

        // ===== 双注册防护（10-04 用户实测 XIAOWO 冲突检测红字）=====
        // WagePerks.dll 也内置 WageAPI（同 Harmony id "com.wageapi"），SaveGame/EndDay/LoadGame 落盘挂点
        // 已由其注册（priority=0 最后跑）。同装时若本程序集也注册 → 三方冲突检测报"重复后置补丁共注册 2 次"。
        // WageSaveStore 已实现宿主转发（WagePerks 在场 → Get/Set/Flush/事件全走 WagePerks 实例），
        // 数据由 WagePerks 挂点统一落盘；本程序集不再重复注册。WagePerks 不在场（WageSurvival 独立）→ 正常注册。
        if (IsWagePerksLoaded())
        {
            Log.Msg("[WageAPI] 检测到 WagePerks 已加载（含内置 WageAPI 统一落盘），跳过 SaveGame/EndDay/LoadGame 挂点注册（防双注册）");
            return;
        }

        // ===== 统一落盘通道（原 Wage's Perks PatchRegistryTable:92/93 迁入）=====
        // priority=0（最后跑）语义必须保留：各系统 SaveGame Postfix（如 Patches.Lifecycle 的 400）
        // 先写内存，WageAPI 最后统一原子落盘。同方法双 Postfix 顺序由 Harmony priority 保证。
        try
        {
            var origSave = AccessTools.Method(typeof(PlayerStore), "SaveGame");
            _harmony.Patch(origSave, postfix: new HarmonyMethod(AccessTools.Method(typeof(WageSaveStore), nameof(WageSaveStore.PostfixSaveGame)), 0));
            Log.Msg("[WageAPI] Patch OK: PlayerStore.SaveGame → Flush（priority=0 最后跑）");
        }
        catch (System.Exception ex) { Log.Msg("[WageAPI] Patch FAIL: SaveGame " + ex.Message); }

        try
        {
            var origEndDay = AccessTools.Method(typeof(PlayerStore), "EndDay");
            _harmony.Patch(origEndDay, postfix: new HarmonyMethod(AccessTools.Method(typeof(WageSaveStore), nameof(WageSaveStore.PostfixEndDay)), 0));
            Log.Msg("[WageAPI] Patch OK: PlayerStore.EndDay → Flush（打烊落盘第二通道）");
        }
        catch (System.Exception ex) { Log.Msg("[WageAPI] Patch FAIL: EndDay " + ex.Message); }

        try
        {
            var origLoad = AccessTools.Method(typeof(PlayerStore), "LoadGame");
            _harmony.Patch(origLoad, postfix: new HarmonyMethod(AccessTools.Method(typeof(WageSaveStore), nameof(WageSaveStore.OnLoadGame))));
            Log.Msg("[WageAPI] Patch OK: PlayerStore.LoadGame → 读档标志（实际加载走轮询）");
        }
        catch (System.Exception ex) { Log.Msg("[WageAPI] Patch FAIL: LoadGame " + ex.Message); }
    }

    public static void Update()
    {
        try { WageSaveStore.LoadIfPending(); } catch { }
    }

    /// <summary>检测 WagePerks.dll 是否已加载（内置 WageAPI 宿主）。双注册防护用。</summary>
    private static bool IsWagePerksLoaded()
    {
        try
        {
            foreach (var a in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                if (a == null) continue;
                string n = "";
                try { n = a.GetName().Name ?? ""; } catch { }
                if (n == "WagePerks") return true;
            }
        }
        catch { }
        return false;
    }

    /// <summary>统一日志（WageAPI 内部用；DebugMode 门控同老 mod 习惯，开发版全开）。</summary>
    public static void LogMsg(string msg)
    {
        try { Log?.Msg(msg); } catch { }
    }
}
