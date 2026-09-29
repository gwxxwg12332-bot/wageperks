using Il2Cpp;
using UnityEngine;

namespace JacksonPerks;

// v1.3.1 涡轮N次立即产出（集水器/酒架反查父物品待拆包，先做净水器/解序器）
public static class TurboBoostN
{
    private static GameItem _targetMachine;
    private static int _remaining;
    private static bool _active;

    public static void PostfixUpdateSprite(GameItem item, GameSlotInventory batterySlot)
    {
        try
        {
            Core.LogMsg("[涡轮N] UpdateSprite触发 id=" + (item?.identifier) + " n=" + RobinCrusoePerk.GetTagIntSafe(item, "wage_turbo_n"));
            int n = RobinCrusoePerk.GetTagIntSafe(item, "wage_turbo_n");
            if (n <= 1) return;

            GameItem machine = null;
            try { machine = item.parentInventory?.GetParentItem(); } catch { }
            try { machine = item.parentInventory?.GetParentItem(); } catch { }
            _remaining = n - 1; _active = true; _targetMachine = machine;
            if (machine != null) { string mid = machine.identifier ?? ""; if (mid == "alarm_system" || mid == "mirage_projector") { _active = false; return; } Core.LogMsg("[涡轮N] 放机器 " + mid + " N=" + n + " 补" + _remaining + "次"); } else { Core.LogMsg("[涡轮N] machine=null n=" + n + " 待Fill匹配"); }







        catch (System.Exception ex) { Core.LogMsg("[涡轮N] UpdateSprite异常: " + ex.Message); }
    }

    public static void PostfixFill(string quality, GameItem container, int volume)
    {
        Core.LogMsg("[涡轮N] FillPostfix active=" + _active); if (!_active || container == null) return;
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
        Core.LogMsg("[涡轮N] PurifyPostfix active=" + _active); if (!_active || _targetMachine == null) return;
        try
        {
            if (machine != _targetMachine) return;
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
        if (!_active || _targetMachine == null || item == null) return;
        try
        {
            if (item.parentInventory?.GetParentItem() != _targetMachine) return;
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
        Core.LogMsg("[涡轮N] PurifyPostfix active=" + _active); if (!_active || _targetMachine == null) return;
        try
        {
            if (machine != _targetMachine) return;
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
        Core.LogMsg("[涡轮N] 补产完成，清零context");
        _active = false;
        _targetMachine = null;
        _remaining = 0;
    }
}
