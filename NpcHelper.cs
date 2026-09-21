using Il2Cpp;

namespace JacksonPerks;

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
        catch { }
    }

    public static void ScheduleOnce(string npcId, string tagKey, int firstDay, int currentDay)
    {
        try
        {
            if (currentDay < firstDay) return;
            if (PerkStatePersistence.GetInt("NpcSchedule", tagKey, 0) > 0) return;
            Schedule(npcId, 0);
            PerkStatePersistence.SetInt("NpcSchedule", tagKey, 1);
        }
        catch { }
    }
}