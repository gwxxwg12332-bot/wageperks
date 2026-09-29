using Il2Cpp;
using UnityEngine;

namespace JacksonPerks;

public static class TurboBoostN
{
    public static void PostfixUpdateSprite(GameItem item, GameSlotInventory batterySlot)
    {
        try
        {
            int n = RobinCrusoePerk.GetTagIntSafe(item, "wage_turbo_n");
            Core.LogMsg("[涡轮N] UpdateSprite id=" + item.identifier + " n=" + n);
        }
        catch { }
    }

    private static int FindTurboNInMachine(GameItem machine)
    {
        try
        {
            var inv = machine?.contentWindow?.childElement?.TryCast<GameGridInventory>();
            if (inv == null) return 0;
            foreach (var it in inv.childItems)
            {
                if (it != null && it.identifier != null && it.identifier.StartsWith("turbo_booster_adv"))
                {
                    int n = RobinCrusoePerk.GetTagIntSafe(it, "wage_turbo_n");
                    if (n > 1) return n;
                }
            }
        }
        catch { }
        return 0;
    }

    public static void PostfixFill(string quality, GameItem container, int volume)
    {
        try
        {
            var machine = container?.parentInventory?.GetParentItem();
            int n = FindTurboNInMachine(machine);
            Core.LogMsg("[涡轮N] Fill n=" + n);
            if (n <= 1) return;
            for (int i = 0; i < n - 1; i++)
            {
                try { MachineMoistureFarm.Fill(quality, container, volume); } catch { }
            }
            Core.LogMsg("[涡轮N] 补产完成 " + (n - 1) + "次");
        }
        catch (System.Exception ex) { Core.LogMsg("[涡轮N] Fill异常: " + ex.Message); }
    }

    public static void PostfixPurifyContainer(GameItem machine, GameItem waterContainer, bool ignoreBonus)
    {
        try
        {
            int n = FindTurboNInMachine(machine);
            if (n <= 1) return;
            for (int i = 0; i < n - 1; i++)
            {
                try { MachinePurifier.PurifyContainer(machine, waterContainer, ignoreBonus); } catch { }
            }
            Core.LogMsg("[涡轮N] Purify补产 " + (n - 1) + "次");
        }
        catch (System.Exception ex) { Core.LogMsg("[涡轮N] Purify异常: " + ex.Message); }
    }

    public static void PostfixOnAgeWine(GameItem item)
    {
        try
        {
            var machine = item?.parentInventory?.GetParentItem();
            int n = FindTurboNInMachine(machine);
            if (n <= 1) return;
            for (int i = 0; i < n - 1; i++)
            {
                try { WineHelper.OnAgeWine(item); } catch { }
            }
            Core.LogMsg("[涡轮N] Wine补产 " + (n - 1) + "次");
        }
        catch (System.Exception ex) { Core.LogMsg("[涡轮N] Wine异常: " + ex.Message); }
    }

    public static void PostfixContinueProgress(GameItem machine)
    {
        try
        {
            int n = FindTurboNInMachine(machine);
            if (n <= 1) return;
            for (int i = 0; i < n - 1; i++)
            {
                try { MachineProgressHelper.ContinueProgressTypeMachine(machine); } catch { }
            }
            Core.LogMsg("[涡轮N] Progress补产 " + (n - 1) + "次");
        }
        catch (System.Exception ex) { Core.LogMsg("[涡轮N] Progress异常: " + ex.Message); }
    }

    public static void PostfixCreateTooltip(RichTextBuilder builder, GameItem item)
    {
        try
        {
            if (item == null) return;
            int n = RobinCrusoePerk.GetTagIntSafe(item, "wage_turbo_n");
            if (n > 1) { builder.AddLine("◆ 互食加持：额外加速 " + (n - 1) + " 次", bold: true); }
        }
        catch { }
    }
}
