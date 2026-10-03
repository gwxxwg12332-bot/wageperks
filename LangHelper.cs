namespace WagePerks;

// ============================================================
// LangHelper 门面（2026-10-03 阶段B：提炼至 WageAPI 后旧类保留、方法体转发）
// 现有调用点零改动——签名与原实现完全一致（T/SafeT/IsEnglish）。
// ============================================================
internal static class LangHelper
{
    /// <summary>当前是否为英文环境（游戏设置语言非中文）</summary>
    public static bool IsEnglish()
    {
        return WageAPI.LangHelper.IsEnglish();
    }

    /// <summary>根据语言返回文本：中文或英文</summary>
    public static string T(string zh, string en)
    {
        return WageAPI.LangHelper.T(zh, en);
    }

    /// <summary>安全版 T：本地化系统未就绪时直接返回中文</summary>
    public static string SafeT(string zh, string en)
    {
        return WageAPI.LangHelper.SafeT(zh, en);
    }
}
