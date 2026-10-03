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
        Log.Msg("WageAPI v1.0.0 已加载（内置 WagePerks.dll）- 多mod共享API层");
        Log.Msg("QQ群：1109707341");

        _harmony = new HarmonyLib.Harmony("com.wageapi");

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

    /// <summary>统一日志（WageAPI 内部用；DebugMode 门控同老 mod 习惯，开发版全开）。</summary>
    public static void LogMsg(string msg)
    {
        try { Log?.Msg(msg); } catch { }
    }
}
