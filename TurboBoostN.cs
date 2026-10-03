using Il2Cpp;
using UnityEngine;

namespace WagePerks;

public static class TurboBoostN
{
    private static int _pendingN;
    private static GameItem _turboMachine;
    private static int _turboN;

    // 10-02 修：吞噬涡轮二次失效——每次拖拽Prefix补CURRENT_CHARGE=15+EnableTag READY（在原生CheckCan前）
    // 根因：吞噬设15+(N-1)*15只够第一次，b__7消耗后READY永久丢（回原位不再触发b__4）→二次拖拽0产出
    public static void PrefixTarget(GameItem __instance, GameItem targetItem)
    {
        try
        {
            if (__instance == null || targetItem == null) return;
            if (__instance.identifier == null || !__instance.identifier.StartsWith("turbo_booster_adv")) return;
            int n = RobinCrusoePerk.GetTagIntSafe(__instance, "wage_turbo_n");
            if (n <= 1) return; // 普通涡轮不干预
            // 补能+READY——让原生CheckCan通过，机器正常工作一次
            RobinCrusoePerk.SetTagIntValue(__instance, "CURRENT_CHARGE_TAG", 15);
            __instance.EnableTag("TURBO_READY_TAG");
            try { MachineTurboBoosterAdv.UpdateSprite(__instance, null); } catch { }
            // 10-02 修：标记必须在Prefix设——补产Postfix在原生Target内部执行，PostfixTarget设太晚
            _turboMachine = targetItem;
            _turboN = n;
            Core.LogMsg("[涡轮N] Prefix补能+设标记 n=" + n + " id=" + __instance.identifier + " target=" + targetItem.identifier);
        }
        catch (System.Exception ex) { Core.LogMsg("[涡轮N] Prefix补能异常: " + ex.Message); }
    }

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
            if (machine != _turboMachine || _turboN <= 1) return;
            int n = _turboN;
            _inTurboCatchUp = true;
            for (int i = 0; i < n - 1; i++)
            {
                try { MachinePurifier.PurifyContainer(machine, waterContainer, ignoreBonus); } catch { }
            }
            Core.LogMsg("[涡轮N] Purify补产 " + (n - 1) + "次");
            _turboN = 0; _turboMachine = null;
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
            if (machine != _turboMachine || _turboN <= 1) return;
            int n = _turboN;
            _inTurboCatchUp = true;
            for (int i = 0; i < n - 1; i++)
            {
                try { WineHelper.OnAgeWine(item); } catch { }
            }
            Core.LogMsg("[涡轮N] Wine补产 " + (n - 1) + "次");
            _turboN = 0; _turboMachine = null;
        }
        catch (System.Exception ex) { Core.LogMsg("[涡轮N] Wine异常: " + ex.Message); }
        finally { _inTurboCatchUp = false; }
    }

    public static void PostfixContinueProgress(GameItem machine)
    {
        if (_inTurboCatchUp) return;
        try
        {
            if (machine != _turboMachine || _turboN <= 1) return;
            int n = _turboN;
            _inTurboCatchUp = true;
            for (int i = 0; i < n - 1; i++)
            {
                try { MachineProgressHelper.ContinueProgressTypeMachine(machine); } catch { }
            }
            Core.LogMsg("[涡轮N] Progress补产 " + (n - 1) + "次");
            _turboN = 0; _turboMachine = null;
        }
        catch (System.Exception ex) { Core.LogMsg("[涡轮N] Progress异常: " + ex.Message); }
        finally { _inTurboCatchUp = false; }
    }

    // 10-02 熔炉补产：OnMachineActioned统一收尾→补(N-1)*4个scrap_metal
    // 根因：熔炉产出链全内联在b__5放置回调，无独立API可挂
    public static void PostfixOnMachineActioned(GameItem machine, GameInventory moduleGrid)
    {
        if (_inTurboCatchUp) return;
        try
        {
            if (machine != _turboMachine || _turboN <= 1) return;
            if (machine.identifier != "furnace") return; // 仅熔炉（其他机器已有各自挂点）
            int n = _turboN;
            // 找输出容器：首选machine.contentWindow.childElement（和GuMachine同模式），兜底moduleGrid
            GameGridInventory output = null;
            try {
                if (machine.contentWindow != null)
                {
                    var cw = machine.contentWindow;
                    Core.LogMsg("[涡轮N] Furnace窗口: childElement=" + (cw.childElement!=null?cw.childElement.GetType().Name:"null"));
                    output = cw.childElement.TryCast<GameGridInventory>();
                }
            } catch (System.Exception ex) { Core.LogMsg("[涡轮N] Furnace找窗口异常: " + ex.Message); }
            if (output == null && moduleGrid != null) { output = moduleGrid.TryCast<GameGridInventory>(); Core.LogMsg("[涡轮N] Furnace用moduleGrid兜底"); }
            if (output == null) { Core.LogMsg("[涡轮N] Furnace补产失败：找不到输出容器"); return; }
            Core.LogMsg("[涡轮N] Furnace输出容器=" + output.GetType().Name);
            _inTurboCatchUp = true;
            int count = (n - 1) * 4; // 原生每次产4个scrap
            for (int i = 0; i < count; i++)
            {
                try {
                    var scrap = Il2Cpp.DirectoryMaster.Item("scrap_metal", true);
                    if (scrap != null) Il2Cpp.GraphUtils.TryAcceptAllMid(output, scrap, -1);
                } catch { }
            }
            Core.LogMsg("[涡轮N] Furnace补产 " + count + " scrap");
            _turboN = 0; _turboMachine = null;
        }
        catch (System.Exception ex) { Core.LogMsg("[涡轮N] Furnace异常: " + ex.Message); }
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
