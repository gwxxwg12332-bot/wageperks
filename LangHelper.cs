namespace WageSurvival;

// ============================================================
// LangHelper 门面（2026-10-03 阶段C：转发 WageAPI.LangHelper——升级为真双语，替换原 zh 恒返回 stub）
// ============================================================
internal static class LangHelper
{
    internal static string T(string zh, string en)
        => WageAPI.LangHelper.T(zh, en);

    internal static string SafeT(string zh, string en)
        => WageAPI.LangHelper.SafeT(zh, en);

    internal static bool IsEnglish()
        => WageAPI.LangHelper.IsEnglish();
}
