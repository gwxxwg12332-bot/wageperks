using Il2Cpp;
using UnityEngine;

namespace JacksonPerks;

public static class TurboBoostN
{
    private static GameItem _targetMachine;
    private static int _remaining;
    private static bool _active;

    public static void PostfixUpdateSprite(GameItem item, GameSlotInventory batterySlot)
    {
        try
        {
            int n = RobinCrusoePerk.GetTagIntSafe(item, "wage_turbo_n");
            Core.LogMsg("[涡轮N] UpdateSprite id=" + item.identifier + " n=" + n);
            if (n <= 1) return;
            GameItem machine = null;
            try { machine = item.parentInventory?.GetParentItem(); } catch { }
            _remaining = n - 1;
            _active = true;
            _targetMachine = machine;
            if (machine != null)
            {
                string mid = machine.identifier ?? "";
                if (mid == "alarm_system" || mid == "mirage_projector") { _active = false; return; }
                Core.LogMsg("[涡轮N] machine=" + mid + " N=" + n);
            }
            else
            {
                Core.LogMsg("[涡轮N] machine=null N=" + n);
            }
        }
        catch (System.Exception ex) { Core.LogMsg("[涡轮N] UpdateSprite异常: " + ex.Message); }
    }

    public static void PostfixFill(string quality, GameItem container, int volume)
    {
        Core.LogMsg("[涡轮N] Fill active=" + _active);
        if (!_active || container == null) return;
        try
        {
            for (int i = 0; i < _remaining; i++)
            {
                try { MachineMoistureFarm.Fill(quality, container, volume); } catch { }
            }
            Finish();
        }
        catch (System.Exception ex) { Core.LogMsg("[涡轮N] Fill异常: " + ex.Message); Finish(); }
    }

    public static void PostfixPurifyContainer(GameItem machine, GameItem waterContainer, bool ignoreBonus)
    {
        if (!_active) return;
        try
        {
            for (int i = 0; i < _remaining; i++)
            {
                try { MachinePurifier.PurifyContainer(machine, waterContainer, ignoreBonus); } catch { }
            }
            Finish();
        }
        catch (System.Exception ex) { Core.LogMsg("[涡轮N] Purify异常: " + ex.Message); Finish(); }
    }

    public static void PostfixOnAgeWine(GameItem item)
    {
        if (!_active || item == null) return;
        try
        {
            for (int i = 0; i < _remaining; i++)
            {
                try { WineHelper.OnAgeWine(item); } catch { }
            }
            Finish();
        }
        catch (System.Exception ex) { Core.LogMsg("[涡轮N] OnAgeWine异常: " + ex.Message); Finish(); }
    }

    public static void PostfixContinueProgress(GameItem machine)
    {
        if (!_active) return;
        try
        {
            for (int i = 0; i < _remaining; i++)
            {
                try { MachineProgressHelper.ContinueProgressTypeMachine(machine); } catch { }
            }
            Finish();
        }
        catch (System.Exception ex) { Core.LogMsg("[涡轮N] ContinueProgress异常: " + ex.Message); Finish(); }
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

    private static void Finish()
    {
        Core.LogMsg("[涡轮N] 补产完成");
        _active = false;
        _targetMachine = null;
        _remaining = 0;
    }
}