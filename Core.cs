using Il2Cpp;
using MelonLoader;

[assembly: MelonInfo(typeof(WageSurvival.Core), "Wage Survival", "0.1.0", "jingdizhiwa123", null)]
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
        Log.Msg("Wage Survival v0.1.0 已加载 - 独立生存mod");
        Log.Msg("QQ群：1109707341");

        PatchRegistry.ApplyAll();
    }

    internal static void LogMsg(string msg) => Log.Msg(msg);
    internal static void AddNightReportLine(string line) { }
    internal static System.Random Rng = new System.Random();
}
