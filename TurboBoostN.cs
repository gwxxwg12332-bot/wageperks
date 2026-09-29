using Il2Cpp;
using UnityEngine;

namespace JacksonPerks;

public static class TurboBoostN
{
    private static GameItem _lastMachine;
    private static int _lastN;

    public static void PostfixUpdateSprite(GameItem item, GameSlotInventory batterySlot)
    {
        try
        {
            int n = RobinCrusoePerk.GetTagIntSafe(item, "wage_turbo_n");
            Core.LogMsg("[涡轮N] UpdateSprite id=" + item.identifier + " n=" + n);
            if (n <= 1) return;
            GameItem machine = null;
            try { machine = item.parentInventory?.GetParentItem(); } catch { }
            _lastMachine = machine;
            _lastN = n;
            Core.LogMsg("[涡轮N] 记录 machine=" + (machine?.identifier ?? "null") + " N=" + n);
        }
        catch (System.Exception ex) { Core.LogMsg("[涡轮N] UpdateSprite异常: " + ex.Message); }
    }

    public static void PostfixFill(string quality, GameItem container, int volume)
    {
        Core.LogMsg("[涡轮N] Fill触发 lastN=" + _lastN);
        if (_lastN <= 1 || container == null) return;
        try
        {
            int remain = _lastN - 1;
            for (int i = 0; i < remain; i++)
            {
                try { MachineMoistureFarm.Fill(quality, container, volume); } catch { }
            }
            _lastN = 1;
            Core.LogMsg("[涡轮N] 补产完成 " + remain + "次");
        }
        catch (System.Exception ex) { Core.LogMsg("[涡轮N] Fill异常: " + ex.Message); }
    }

    public static void PostfixPurifyContainer(GameItem machine, GameItem waterContainer, bool ignoreBonus)
    {
        if (_lastN <= 1) return;
        try
        {
            int remain = _lastN - 1;
            for (int i = 0; i < remain; i++)
            {
                try { MachinePurifier.PurifyContainer(machine, waterContainer, ignoreBonus); } catch { }
            }
            _lastN = 1;
        }
        catch (System.Exception ex) { Core.LogMsg("[涡轮N] Purify异常: " + ex.Message); }
    }

    public static void PostfixOnAgeWine(GameItem item)
    {
        if (_lastN <= 1 || item == null) return;
        try
        {
            int remain = _lastN - 1;
            for (int i = 0; i < remain; i++)
            {
                try { WineHelper.OnAgeWine(item); } catch { }
            }
            _lastN = 1;
        }
        catch (System.Exception ex) { Core.LogMsg("[涡轮N] OnAgeWine异常: " + ex.Message); }
    }

    public static void PostfixContinueProgress(GameItem machine)
    {
        if (_lastN <= 1) return;
        try
        {
            int remain = _lastN - 1;
            for (int i = 0; i < remain; i++)
            {
                try { MachineProgressHelper.ContinueProgressTypeMachine(machine); } catch { }
            }
            _lastN = 1;
        }
        catch (System.Exception ex) { Core.LogMsg("[涡轮N] ContinueProgress异常: " + ex.Message); }
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