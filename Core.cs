using Il2Cpp;
using MelonLoader;

[assembly: MelonInfo(typeof(WageSurvival.Core), "Wage Survival", "0.1.3", "jingdizhiwa123", null)]
[assembly: MelonGame("Questing Goose Studio", "Probably Stolen")]

namespace WageSurvival;
public class Core : MelonMod
{
    public static MelonLogger.Instance Log { get; private set; }

#if DEBUG
    public static bool DebugMode = true;
#else
    public static bool DebugMode = false;
#endif

    public override void OnInitializeMelon()
    {
        Log = base.LoggerInstance;
        Log.Msg("Wage Survival v0.1.3 已加载 - 阶段C：独立生存mod（WageAPI 内置）");
        Log.Msg("QQ群：1109707341");

        PatchRegistry.ApplyAll();
        // 10-04 内置：WageAPI 入口接入（落盘挂点 priority=0 + 读档标志），单文件自包含不依赖独立 WageAPI.dll
        try { WageAPI.Core.Init(Log); } catch (System.Exception ex) { Log.Msg("[WageAPI] Init 失败: " + ex.Message); }
    }

    public override void OnUpdate()
    {
        // 10-04 内置：WageAPI 读档轮询（LoadIfPending）
        try { WageAPI.Core.Update(); } catch { }
    }

    internal static void LogMsg(string msg) => Log.Msg(msg);
    internal static void AddNightReportLine(string line) { }
    internal static System.Random Rng = new System.Random();
}
