using Il2Cpp;
using MelonLoader;

[assembly: MelonInfo(typeof(WageSurvival.Core), "Wage Survival", "0.1.9", "jingdizhiwa123", null)]
[assembly: MelonGame("Questing Goose Studio", "Probably Stolen")]

namespace WageSurvival;
public class Core : MelonMod
{
    public static MelonLogger.Instance Log { get; private set; }

    /// <summary>10-05 让路模式：检测到 WagePerks 已加载 → 本 mod 变空壳（零挂点，WagePerks 接管）。</summary>
    internal static bool _yieldMode = false;

#if DEBUG
    public static bool DebugMode = true;
#else
    public static bool DebugMode = false;
#endif

    public override void OnInitializeMelon()
    {
        Log = base.LoggerInstance;
        Log.Msg("Wage Survival v0.1.9 已加载 - 阶段C：独立生存mod（WageAPI 内置）；10-06 双装让路时序修复+Z键面板非鲁滨逊档保留");
        Log.Msg("QQ群：1109707341");
        // 10-06 修复：挂点注册全部延后到 OnLateInitializeMelon。
        // 原因（实锤）：原 OnInitializeMelon 内做 WagePerksInstalled() 探测存在加载顺序时序风险——
        // WS 的 OnInitializeMelon 先于 WP 执行时，WagePerks 尚未进 AppDomain → 探测失败 → ApplyAll 全量注册；
        // 随后 WP 也全量 → 双挂点叠加（WageSaveStore SaveGame/LoadGame/EndDay 各注册 2 次，
        // 实证 = XIAOWO 日志 ERROR "来源 MOD：com.wageapi，共注册 2 次"）→ 存档双写/读档双轮询/面板特性被双接管。
        // OnLateInitializeMelon 在全部 mod 的 OnInitializeMelon 完成之后才调用 → 探测 100% 可靠。
    }

    public override void OnLateInitializeMelon()
    {
        // 10-07 鲁滨逊完全归 WS（memos LtUy6CjmHARSnNJ6MoYkqp / YJdTSrCfBnEJiDvKcY759K）：
        // 让路模式从 ApplyGlobalConsumeOnly（仅 4 挂点）升级为 ApplyAll 全量挂载——
        // WP 侧 Survival 挂点（60 条行为修饰 + 开局 6 + 交易 6）按实施单分批删除后，双装时 WS=鲁滨逊唯一宿主。
        // 交易组不再避让（决策 1：交易归 WS——WS SurvivalTrade 唯一宿主）。
        // 迁移订阅在 ApplyAll 内挂载（幂等）——本分支不再手挂（防重复订阅）。
        if (WagePerksInstalled())
        {
            _yieldMode = true;
            Log.Msg("[WageSurvival] 检测到 WagePerks，让路模式升级：ApplyAll 全量挂载（鲁滨逊完全归 WS——生存/交易/开局全链 WS 接管，WP 已删 Survival 挂点无双挂）");
            PatchRegistry.ApplyAll();
            return;
        }

        PatchRegistry.ApplyAll();
        // 10-04 内置：WageAPI 入口接入（落盘挂点 priority=0 + 读档标志），单文件自包含不依赖独立 WageAPI.dll
        try { WageAPI.Core.Init(Log); } catch (System.Exception ex) { Log.Msg("[WageAPI] Init 失败: " + ex.Message); }
    }

    public override void OnUpdate()
    {
        // 10-05 让路模式：读档轮询也停（防双 SaveStore 双落盘读写）
        if (_yieldMode) return;
        // 10-04 内置：WageAPI 读档轮询（LoadIfPending）
        try { WageAPI.Core.Update(); } catch { }
    }

    /// <summary>AppDomain 探测 WagePerks 程序集（先例照 LuckScoutPerk.CompatGuard；程序集名权威值 WagePerks.csproj AssemblyName）。</summary>
    internal static bool WagePerksInstalled()
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

    internal static void LogMsg(string msg) => Log.Msg(msg);
    // 10-07 阶段D D-1：夜报实装（原空实现=WS 夜报线全丢）——原版原生夜报通道（柔和绿 #7FC97F，对齐 WP Core.AddNightReportLine 语义）
    internal static void AddNightReportLine(string line)
    {
        if (string.IsNullOrEmpty(line)) return;
        try
        {
            var ps = PlayerStore.Instance;
            if (ps != null) ps.AddNightLog(line, "#7FC97F");
        }
        catch { }
    }
    internal static System.Random Rng = new System.Random();
}
