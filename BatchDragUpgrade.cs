using Il2Cpp;

namespace JacksonPerks;

/// <summary>
/// 批量拖拽升级：框选多个物品拖到容器上，一次性全部吸收/升级。
/// 挂 ItemMultiSelectHandler.EndGroupDrag，Prefix 识别目标+收集物品，Postfix 循环调用现有单拖方法。
/// </summary>
internal static class BatchDragUpgrade
{
    private static readonly System.Collections.Generic.List<GameItem> _pending = new();
    private static GameItem _target = null;
    private static bool _armed = false;

    public static void PrefixEndGroupDrag(Il2Cpp.ItemMultiSelectHandler __instance)
    {
        try
        {
            Core.LogMsg("[批量升级] Prefix 触发");
            _pending.Clear();
            _target = null;
            _armed = false;

            // 读 hoverItem（拖拽目标容器）
            GameItem hover = null;
            try { hover = __instance.hoverItem; } catch { }
            if (hover == null) { Core.LogMsg("[批量升级] hoverItem 为 null"); return; }

            // 识别目标类型
            bool isDice = false, isBox = false, isBead = false;
            try { isDice = DestinyDice.IsDice(hover); } catch { }
            try { isBox = ContainerUpgradeV2.IsWageBox(hover); } catch { }
            try { isBead = LuckScoutBackpackUpgrade.IsBead(hover); } catch { }

            if (!isDice && !isBox && !isBead) return; // 不是目标容器，不处理

            // 遍历 selectedItems，排除目标本身
            var selected = __instance.selectedItems;
            if (selected == null) { Core.LogMsg("[批量升级] selectedItems 为 null"); return; }
            Core.LogMsg("[批量升级] selectedItems 数量=" + selected.Count);

            foreach (var el in selected)
            {
                try
                {
                    GameItem item = el;
                    if (item == null || item.Pointer == hover.Pointer) continue; // 排除自己
                    _pending.Add(item);
                }
                catch { }
            }

            if (_pending.Count == 0) return;

            _target = hover;
            _armed = true;
            Core.LogMsg("[批量升级] 识别目标: " + hover.identifier + " 待吸收 " + _pending.Count + " 件");
        }
        catch (System.Exception ex)
        {
            Core.LogMsg("[批量升级] Prefix 异常: " + ex.Message);
            _armed = false;
            _pending.Clear();
            _target = null;
        }
    }

    public static void PostfixEndGroupDrag(Il2Cpp.ItemMultiSelectHandler __instance)
    {
        if (!_armed) return;
        try
        {
            if (_target == null || _pending.Count == 0) return;

            int ok = 0, fail = 0;
            // 09-26 批量升级：清空虚空珠防抖（否则 0.5s 内只能吃一个）
            try { LuckScoutBackpackUpgrade.ClearDebounce(); } catch { }
            foreach (var item in _pending)
            {
                try
                {
                    // 骰子：DestinyDice.AbsorbItem(target, item)
                    if (DestinyDice.IsDice(_target))
                    {
                        DestinyDice.AbsorbItem(_target, item);
                        ok++;
                        continue;
                    }

                    // 妙妙箱：ContainerUpgradeV2.ConsumeNutsDirectly(target, item)
                    if (ContainerUpgradeV2.IsWageBox(_target))
                    {
                        if (ContainerUpgradeV2.ConsumeNutsDirectly(_target, item)) ok++;
                        else fail++;
                        continue;
                    }

                    // 虚空珠：LuckScoutBackpackUpgrade.DoUpgrade(item, target)
                    if (LuckScoutBackpackUpgrade.IsBead(_target))
                    {
                        // 每个物品调用前清空防抖（否则上一个成功后写了记录，下一个被拦）
                        try { LuckScoutBackpackUpgrade.ClearDebounce(); } catch { }
                        if (LuckScoutBackpackUpgrade.DoUpgrade(item, _target)) ok++;
                        else fail++;
                        continue;
                    }
                }
                catch (System.Exception ex)
                {
                    fail++;
                    Core.LogMsg("[批量升级] 单件失败: " + (item != null ? item.identifier : "null") + " - " + ex.Message);
                }
            }

            Core.LogMsg("[批量升级] 完成: 成功 " + ok + " 失败 " + fail);

            // 刷新目标容器 UI
            try { _target.SyncModifiedState(); } catch { }
        }
        catch (System.Exception ex)
        {
            Core.LogMsg("[批量升级] Postfix 异常: " + ex.Message);
        }
        finally
        {
            _armed = false;
            _pending.Clear();
            _target = null;
        }
    }

    public static System.Exception FinalizerEndGroupDrag(Il2Cpp.ItemMultiSelectHandler __instance, System.Exception __exception)
    {
        // 兜底：不管怎么炸，重置临时状态
        _armed = false;
        _pending.Clear();
        _target = null;
        if (__exception != null)
        {
            Core.LogMsg("[批量升级] Finalizer 兜住异常: " + __exception.Message);
            return null; // 吞掉，不崩游戏
        }
        return null;
    }
}
