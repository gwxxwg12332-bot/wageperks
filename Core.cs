using Il2Cpp;
using MelonLoader;

[assembly: MelonInfo(typeof(WageBlackGold.Core), "黑金典藏箱", "1.0.1", "井底之蛙", null)]
[assembly: MelonGame("Questing Goose Studio", "Probably Stolen")]

namespace WageBlackGold;
/// <summary>黑金典藏箱（赞助 mod 独立 DLL）——2026-10-08 用户拍板：独立新 mod，不属于 WagePerks/WageSurvival 内容。</summary>
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
        Log.Msg("黑金典藏箱 v1.0.1 已加载（独立赞助 mod：全物品收纳 / 免搜查 / 16 组样式切换 / 博士夜晚上架 2000）");
        Log.Msg("QQ群：1109707341");
    }

    public override void OnLateInitializeMelon()
    {
        // 挂点注册（延后到 OnLateInitializeMelon——与 WS 同因：mod 加载顺序时序安全）
        try { PatchRegistry.ApplyAll(); } catch (System.Exception ex) { Log.Msg("[黑金典藏箱] PatchRegistry 失败: " + ex.Message); }
        // 10-10 零风险清理批#3：WageAPI Init 让路（对齐 WS Core.cs:43-49 模板 + 探测方法 :64-78）——
        // WagePerks 在场 → 跳过 WageAPI.Core.Init（落盘挂点由宿主提供，消除"重复后置补丁共注册 2 次"红字；
        // 嵌入副本内层 IsHostLoaded 另兜底 WageSurvival 宿主场景）。PatchRegistry.ApplyAll 与素材预加载=业务补丁，两种情况下都执行（不让路）。
        if (WagePerksInstalled())
        {
            Log.Msg("[黑金典藏箱] 检测到 WagePerks，WageAPI 落盘 Init 让路（宿主提供持久化）；PatchRegistry/素材预加载照常");
        }
        else
        {
            // WageAPI 内嵌入口（落盘挂点 priority=0 + 读档标志——WageSaveStore 持久化依赖）
            try { WageAPI.Core.Init(Log); } catch (System.Exception ex) { Log.Msg("[黑金典藏箱] WageAPI Init 失败: " + ex.Message); }
        }
        // 素材预加载（防 RenderHandler.LoadFromAtlas 懒加载死锁）
        try { BlackGoldBox.LoadAllSprites(); } catch (System.Exception ex) { Log.Msg("[黑金典藏箱] 素材预加载失败: " + ex.Message); }
    }

    public override void OnUpdate()
    {
        // 无每帧逻辑（事件驱动——样式切换/上架/读档恢复全为事件挂点）
        // WageAPI 读档轮询驱动（LoadIfPending：宿主在场→转发宿主；独立→本地。单装/黑金+WS 场景样式读档恢复依赖此通道）
        try { WageAPI.Core.Update(); } catch { }
    }

    /// <summary>AppDomain 探测 WagePerks 程序集（10-10 零风险清理批#3：从 WS Core.cs:64-78 复制，让路判定用；WageSurvival 宿主场景由嵌入副本 IsHostLoaded 兜底）。</summary>
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

    internal static void LogMsg(string msg)
    {
        // 10-10 发布清洁：DebugMode 门控（对齐 WP Core.cs:163 先例——Release 编译诊断日志全关；Patch OK/FAIL 同关，报错走 Debug 版实测）
        if (DebugMode)
        {
            try { Log.Msg(msg); } catch { }
        }
    }
}
