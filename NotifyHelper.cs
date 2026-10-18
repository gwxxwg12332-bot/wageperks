using Il2Cpp;

namespace WagePerks;

// 通知 / 夜报（统一 try/catch + 双语）
internal static class NotifyHelper
{
    public static void Notify(string zh, string en, string color = "white")
    {
        try { StoreUIManager.Instance.Notify(LangHelper.T(zh, en), color); } catch { }
    }

        // 10-19 P3 C组清理：NightLog() 已删（死方法零调用，派活单§六；如需恢复见 git 历史）

    // 已翻译文本直接夜报（门面模式用）
    public static void NightLogRaw(string line, string color = "#7FC97F")
    {
        try { StoreUIManager.Instance.Notify(line); } catch { }
        try { var ps = PlayerStore.Instance; if (ps != null) ps.AddNightLog(line, color); } catch { }
        try { Core.AddNightReportLine(line); } catch { }
    }
}
