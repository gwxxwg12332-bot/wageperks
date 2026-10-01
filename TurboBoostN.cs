using Il2Cpp;
using UnityEngine;

namespace WagePerks;

public static class TurboBoostN
{
    private static int _pendingN;
    private static GameItem _turboMachine;
    private static int _turboN;
    public static void PostfixTarget(GameItem __instance, GameItem targetItem)
    {
        try
        {
            if (__instance == null || targetItem == null) return;
            if (__instance.identifier == null || !__instance.identifier.StartsWith("turbo_booster_adv")) return;
            int n = RobinCrusoePerk.GetTagIntSafe(__instance, "wage_turbo_n");
            Core.LogMsg("[涡轮N] Target source=" + __instance.identifier + " target=" + targetItem.identifier + " n=" + n);
            if (n <= 1) return;
            _turboMachine = targetItem;
            _turboN = n;
        }
        catch (System.Exception ex) { Core.LogMsg("[涡轮N] Target异常: " + ex.Message); }
    }
    public static void PostfixUpdateSprite(GameItem item, GameSlotInventory batterySlot)
    {
        try
        {
            int n = RobinCrusoePerk.GetTagIntSafe(item, "wage_turbo_n");
            Core.LogMsg("[涡轮N] UpdateSprite id=" + item.identifier + " n=" + n);
            if (n > 1) _pendingN = n;
        }
        catch { }
    }

    private static int FindTurboNInMachine(GameItem machine)
    {
        try
        {
            var bs = MachineHelper.GetBatterySlot(machine);
            if (bs == null) return 0;
            foreach (var it in bs.childItems)
            {
                if (it != null && it.identifier != null && it.identifier.StartsWith("turbo_booster_adv"))
                {
                    int n = RobinCrusoePerk.GetTagIntSafe(it, "wage_turbo_n");
                    if (n > 1) return n;
                }
            }
        }
        catch (System.Exception ex) { Core.LogMsg("[涡轮N] FindTurboN异常: " + ex.Message); }
        return 0;
    }

    public static void PrefixFill(string quality, GameItem container, ref int volume)
    {
        try
        {
            if (_turboMachine == null || _turboN <= 1) return;
            var machine = container?.parentInventory?.GetParentItem();
            if (machine != _turboMachine) return;
            Core.LogMsg("[涡轮N] Fill放大 n=" + _turboN + " vol=" + volume);
            volume *= _turboN;
            _turboN = 0; _turboMachine = null;
        }
        catch (System.Exception ex) { Core.LogMsg("[涡轮N] Fill异常: " + ex.Message); }
    }

    private static bool _inTurboCatchUp = false;
    public static void PostfixPurifyContainer(GameItem machine, GameItem waterContainer, bool ignoreBonus)
    {
        if (_inTurboCatchUp) return;
        try
        {
            int n = FindTurboNInMachine(machine);
            if (n <= 1) return;
            _inTurboCatchUp = true;
            for (int i = 0; i < n - 1; i++)
            {
                try { MachinePurifier.PurifyContainer(machine, waterContainer, ignoreBonus); } catch { }
            }
            Core.LogMsg("[涡轮N] Purify补产 " + (n - 1) + "次");
        }
        catch (System.Exception ex) { Core.LogMsg("[涡轮N] Purify异常: " + ex.Message); }
        finally { _inTurboCatchUp = false; }
    }

    public static void PostfixOnAgeWine(GameItem item)
    {
        if (_inTurboCatchUp) return;
        try
        {
            var machine = item?.parentInventory?.GetParentItem();
            int n = FindTurboNInMachine(machine);
            if (n <= 1) return;
            _inTurboCatchUp = true;
            for (int i = 0; i < n - 1; i++)
            {
                try { WineHelper.OnAgeWine(item); } catch { }
            }
            Core.LogMsg("[涡轮N] Wine补产 " + (n - 1) + "次");
        }
        catch (System.Exception ex) { Core.LogMsg("[涡轮N] Wine异常: " + ex.Message); }
        finally { _inTurboCatchUp = false; }
    }

    public static void PostfixContinueProgress(GameItem machine)
    {
        if (_inTurboCatchUp) return;
        try
        {
            int n = FindTurboNInMachine(machine);
            if (n <= 1) return;
            _inTurboCatchUp = true;
            for (int i = 0; i < n - 1; i++)
            {
                try { MachineProgressHelper.ContinueProgressTypeMachine(machine); } catch { }
            }
            Core.LogMsg("[涡轮N] Progress补产 " + (n - 1) + "次");
        }
        catch (System.Exception ex) { Core.LogMsg("[涡轮N] Progress异常: " + ex.Message); }
        finally { _inTurboCatchUp = false; }
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
