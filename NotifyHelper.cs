using Il2Cpp;

namespace JacksonPerks;

// 通知 / 夜报（统一 try/catch + 双语）
internal static class NotifyHelper
{
    public static void Notify(string zh, string en, string color = "white")
    {
        try { StoreUIManager.Instance.Notify(LangHelper.T(zh, en), color); } catch { }
    }

    public static void NightLog(string zh, string en, string color = "#7FC97F")
    {
        try { NightLogRaw(LangHelper.T(zh, en), color); } catch { }
    }

    // 已翻译文本直接夜报（门面模式用）
    public static void NightLogRaw(string line, string color = "#7FC97F")
    {
        try { StoreUIManager.Instance.Notify(line); } catch { }
        try { var ps = PlayerStore.Instance; if (ps != null) ps.AddNightLog(line, color); } catch { }
        try { Core.AddNightReportLine(line); } catch { }
    }
}
