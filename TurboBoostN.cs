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
            if (item == null || !item.IsTag("TURBO_READY_TAG")) return;
            int n = RobinCrusoePerk.GetTagIntSafe(item, "wage_turbo_n");
            if (n <= 1) return;

            // TODO: 反查涡轮所在机器需拆包GameInventory结构，暂用日志
            Core.LogMsg("[涡轮N] UPDATE n=" + n + " item=" + item.identifier + " parent=" + (item.parentInventory?.GetType()?.Name ?? "null"));
        }
        catch (System.Exception ex) { Core.LogMsg("[涡轮N] UpdateSprite异常: " + ex.Message); }
    }

    public static void PostfixPurifyContainer(GameItem machine, GameItem waterContainer, bool ignoreBonus)
    {
        if (!_active || _targetMachine == null) return;
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

    public static void PostfixContinueProgress(GameItem machine)
    {
        if (!_active || _targetMachine == null) return;
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

    private static void Finish()
    {
        Core.LogMsg("[涡轮N] 补产完成，清零context");
        _active = false;
        _targetMachine = null;
        _remaining = 0;
    }
}
