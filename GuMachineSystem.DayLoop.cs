using System;
using Il2Cpp;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace JacksonPerks;

public static partial class GuMachineSystem
{
    public static void OnDayStartPostfix()
    {
        try
        {
            int day = DeterministicSchedule.CurrentDay; Core.LogMsg("[养蛊机] OnDayStart day=" + day);
            // 09-19 修复：养蛊机充能原切 ModHook.OnHandlingNightlyServicesLate——钩子从未注册且 Demo 版 ModHook 不触发（09-17 注释"事件 0 触发"）
            // → 挂回 OnDayStart Postfix（与电池互吞/AI 抽卡同挂点，时序一致）
            var guList = FindGuMachines(); Core.LogMsg("[养蛊机] FindGuMachines=" + guList.Count); foreach (var gu in guList)
            {
                TryGuMachineTick(gu, day);
            }
            foreach (var gen in FindAiGenerators())
            {
                TryAiGeneratorTick(gen, day);
            }
        }
        catch (System.Exception ex) { Core.LogMsg("[GuMachineSystem.DayLoop] 异常: " + ex.Message); }
    }
    private static System.Collections.Generic.List<GameItem> FindGuMachines()
    {
        var result = new System.Collections.Generic.List<GameItem>();
        var seen = new System.Collections.Generic.HashSet<GameItem>();
        try
        {
            // 路1：全物品遍历（覆盖仓库/其他mod储存区）
            var all = Il2Cpp.PlayerStore.Instance.FindAllItem(true);
            if (all != null) foreach (var it in all)
            {
                if (it != null && it.identifier == GU_MACHINE_ID && seen.Add(it)) result.Add(it);
            }
            // 路2：Emporium四容器（柜台区，防遗漏）
            var allInvs = Il2Cpp.EmporiumEntry.Instance;
            if (allInvs != null)
            {
                GameInventory[] invs = {
                    allInvs.backInvinvElement as GameInventory,
                    allInvs.showcaseElement as GameInventory,
                    allInvs.invElement as GameInventory,
                    allInvs.frontInvinvElement as GameInventory,
                };
                foreach (var inv in invs)
                {
                    if (inv == null || inv.childItems == null) continue;
                    foreach (var it in inv.childItems)
                    {
                        if (it != null && it.identifier == GU_MACHINE_ID && seen.Add(it)) result.Add(it);
                    }
                }
            }
            Core.LogMsg("[养蛊] FindGuMachines=" + result.Count);
        }
        catch (System.Exception ex) { Core.LogMsg("[GuMachineSystem.DayLoop] 异常: " + ex.Message); }
        return result;
    }
    internal static GameGridInventory GetGuGrid(GameItem gu) // internal：供 BatteryCannibalism 收集养蛊机舱内电池
    {
        try
        {
            if (gu == null || gu.contentWindow == null || gu.contentWindow.childElement == null) return null;
            return gu.contentWindow.childElement.TryCast<GameGridInventory>();
        }
        catch { return null; }
    }
    private static void LockGuModules(GameGridInventory grid)
    {
        try
        {
            if (grid == null || grid.childItems == null) return;
            foreach (var m in grid.childItems)
            {
                if (m == null) continue;
                bool isMod = false; try { isMod = m.IsTag("MODULE_TAG") && !RobinCrusoePerk.IsExcludedModule(m); } catch { }
                if (isMod) { try { m.EnableTag("MODULE_STUCK_TAG"); } catch { } }
            }
        }
        catch (System.Exception ex) { Core.LogMsg("[GuMachineSystem.DayLoop] 异常: " + ex.Message); }
    }
    private static void UnlockGuModules(GameGridInventory grid)
    {
        try
        {
            if (grid == null || grid.childItems == null) return;
            foreach (var m in grid.childItems)
            {
                if (m == null) continue;
                try { if (m.IsTag("MODULE_STUCK_TAG")) m.DisableTag("MODULE_STUCK_TAG"); } catch { }
            }
        }
        catch (System.Exception ex) { Core.LogMsg("[GuMachineSystem.DayLoop] 异常: " + ex.Message); }
    }
}
