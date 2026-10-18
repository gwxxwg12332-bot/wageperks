using Il2Cpp;

namespace WagePerks;

// NPC 调度（排期 / 防重）
internal static class NpcHelper
{
    public static void Schedule(string npcId, int daysFromNow)
    {
        try
        {
            var ps = PlayerStore.Instance;
            if (ps == null) return;
            ps.QueueFuturClient(npcId, daysFromNow);
        }
        catch (System.Exception ex) { Core.LogMsg("[NpcHelper] 异常: " + ex.Message); }
    }

        // 10-19 P3 C组清理：ScheduleOnce() 已删（死方法零调用，派活单§六；如需恢复见 git 历史）
}