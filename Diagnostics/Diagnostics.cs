using System;
using Il2Cpp;

namespace JacksonPerks;

/// <summary>
/// 诊断统一总入口：所有诊断/测试/调试工具都在这里注册和分发。
/// - DebugMode=false（发布版）时整个 Diagnostics 全部 no-op，一个开关管住全部。
/// - 新增诊断：1) 在 Diagnostics/ 文件夹写一个类  2) 在 Register/OnUpdate/OnGUI 里加一行。
/// - 删除诊断：直接从 Register/OnUpdate/OnGUI 里删掉对应行，再删文件。
/// - 诊断类 Patch（纯诊断、只对开发有意义的）集中到 ApplyPatches()，发布版不注册。
/// </summary>
public static class Diagnostics
{
    /// <summary>初始化注册（Core.OnInitializeMelon 只调一次；DebugMode=false 直接返回）</summary>
    public static void Register()
    {
        // TestRunner 不受 DebugMode 门控：-runtests 自动测试在正式版(DebugMode=false)也要能跑
        try { TestRunner.Init(); } catch (Exception ex) { Core.LogMsg("[TestRunner] Init异常: " + ex.Message); }
        if (!Core.DebugMode) return;
        SafeInit("[RuntimeInspector]", RuntimeInspector.Init);
        SafeInit("[容器诊断]", ContainerDiagnostics.Init);
    }

    /// <summary>每帧更新分发（Core.OnUpdate 只调一次）</summary>
    public static void OnUpdate()
    {
        // TestRunner 不受 DebugMode 门控：-runtests 自动测试在正式版(DebugMode=false)也要能跑
        try { TestRunner.OnUpdate(); } catch { }
        if (!Core.DebugMode) return;
        SafeCall(RuntimeInspector.OnUpdate);
        SafeCall(ContainerDiagnostics.OnUpdate);
    }

    /// <summary>OnGUI 分发（Core.OnGUI 只调一次）</summary>
    public static void OnGUI()
    {
        if (!Core.DebugMode) return;
        SafeCall(RuntimeInspector.OnGUI);
    }

    /// <summary>诊断类 Harmony Patch（发布版不注册，避免刷日志/影响其他 mod）</summary>
    public static void ApplyPatches()
    {
        if (!Core.DebugMode) return;
        // 【已禁用】诊断 Patch 挂在特性选择核心方法上，疑为 IL2CPP 下 Harmony 注入导致选择流程异常
    }

    /// <summary>特性选择诊断：CanSelect 返回值 + 已选特性数（DebugMode only）</summary>
    public static void PostfixCanSelect(StartingPerkElement __instance, ref bool __result)
    {
        if (!Core.DebugMode) return;
        string idn = (__instance?.id ?? "null").Replace("\0", "");
        string perkState = __instance?.perk != null ? "perk已绑定" : "perk未绑定";
        string selectedCount = "?";
        try
        {
            var ng = Il2Cpp.NewGameData.Instance;
            if (ng != null && ng.startingPerks != null) selectedCount = ng.startingPerks.Count.ToString();
        }
        catch (Exception ex) { selectedCount = "读取失败:" + ex.Message; }
    }

    /// <summary>特性选择诊断：SelectPerk 是否被调用（DebugMode only）</summary>
    public static void PostfixSelectPerk(StartingPerkElement perk)
    {
        if (!Core.DebugMode) return;
        string idn = (perk?.id ?? "null").Replace("\0", "");
    }

    private static void SafeInit(string tag, Action init)
    {
        if (!Core.DebugMode) return;
        try { init(); }
        catch (Exception ex) { Core.LogMsg(tag + " 初始化失败: " + ex.Message); }
    }

    private static void SafeCall(Action act)
    {
        if (!Core.DebugMode) return;
        try { act(); }
        catch { }
    }
}
