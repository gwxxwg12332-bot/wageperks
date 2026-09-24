using System;
using System.Collections.Generic;
using Il2Cpp;

namespace JacksonPerks;

/// <summary>
/// 物品堆叠系统：原生 unitCount + 延迟队列
/// </summary>
internal static class StackSystem
{
    private const int MAX = 99;

    // 不可叠物品黑名单
    private static readonly HashSet<string> UnstackableIds = new()
    {
        "simple_backpack",
        "evidence_box", "med_box", "sec_box", "service_box", "eng_box",
        "wage_girl",
        "destiny_dice",
        "wage_box",
    };

    // 延迟队列
    private static readonly Queue<Action> Deferred = new Queue<Action>();
    public static void Defer(Action a) { lock (Deferred) Deferred.Enqueue(a); }
    public static void TickDeferred()
    {
        lock (Deferred)
        {
            while (Deferred.Count > 0)
            {
                try { Deferred.Dequeue()(); } catch { }
            }
        }
    }

    // 待合并物品（EndDrag 登记，下一帧处理）
    private static GameItem _pendingMergeFrom;
    private static GameItem _pendingMergeTo;

    public static int GetCount(GameItem item)
    {
        try { return Math.Max(1, item.unitCount); } catch { return 1; }
    }

    public static void SetCount(GameItem item, int n)
    {
        try { item.unitCount = Math.Max(1, Math.Min(n, MAX)); } catch { }
    }

    public static bool IsStackable(GameItem a, GameItem b)
    {
        try
        {
            if (a == null || b == null) return false;
            if (a.identifier != b.identifier) return false;
            if (IsUnstackable(a)) return false;
            // 吃过的/用过的不能和全新的叠
            if (a.IsTag("EATEN_TAG") || b.IsTag("EATEN_TAG")) return false;
            // 剩水量不同的水瓶不能叠
            try {
                if (a.IsTag("LIQUID_CONTAINER_TAG") || b.IsTag("LIQUID_CONTAINER_TAG")) {
                    int wa = RobinCrusoePerk.GetWaterMl(a);
                    int wb = RobinCrusoePerk.GetWaterMl(b);
                    if (wa != wb) return false;
                }
            } catch { }
            // 剩卡路里不同的食物不能叠
            try {
                if (RobinCrusoePerk.IsFood(a) && RobinCrusoePerk.IsFood(b)) {
                    int ca = RobinCrusoePerk.GetCalLeft(a);
                    int cb = RobinCrusoePerk.GetCalLeft(b);
                    if (ca != cb) return false;
                }
            } catch { }
            // 剩卡路里不同的食物不能叠
            try {
                if (RobinCrusoePerk.IsFood(a) || RobinCrusoePerk.IsFood(b)) {
                    int ca = RobinCrusoePerk.GetCalLeft(a);
                    int cb = RobinCrusoePerk.GetCalLeft(b);
                    if (ca != cb) return false;
                }
            } catch { }
            if (GetCount(a) + GetCount(b) > MAX) return false;
            return true;
        } catch { return false; }
    }

    public static bool IsUnstackable(GameItem item)
    {
        try
        {
            if (item == null) return true;
            if (item.contentWindow != null) return true;
            return UnstackableIds.Contains(item.identifier);
        } catch { return false; }
    }

    // EndDrag 登记待合并（不直接执行）
    public static void OnEndDrag(GameItem dragItem, GameItem targetItem)
    {
        try
        {
            Core.LogMsg("[堆叠] EndDrag: " + (dragItem != null ? dragItem.identifier : "null") + " -> " + (targetItem != null ? targetItem.identifier : "null"));
            if (!IsStackable(dragItem, targetItem)) return;
            _pendingMergeFrom = dragItem;
            _pendingMergeTo = targetItem;
        } catch { }
    }

    // 每帧 Tick：处理延迟队列 + 待合并
    public static void OnFrameTick()
    {
        TickDeferred();
        if (_pendingMergeFrom != null && _pendingMergeTo != null)
        {
            GameItem from = _pendingMergeFrom;
            GameItem to = _pendingMergeTo;
            _pendingMergeFrom = null;
            _pendingMergeTo = null;
            Defer(() => {
                try
                {
                    if (from == null || to == null) return;
                    if (!IsStackable(from, to)) return;
                    int newCount = GetCount(from) + GetCount(to);
                    SetCount(to, newCount);
                    try { from.Destroy(); } catch { }
                    Core.LogMsg("[堆叠] 合并完成: " + to.identifier + " ×" + newCount);
                } catch (System.Exception ex) { Core.LogMsg("[堆叠] 合并异常: " + ex.Message); }
            });
        }
    }

    // 消耗一个堆叠物品：返回 true=吃完了该 Destroy，false=还有剩不 Destroy
    public static bool ConsumeOne(GameItem item)
    {
        try
        {
            int count = GetCount(item);
            if (count > 1)
            {
                SetCount(item, count - 1);
                return false; // 没吃完，不 Destroy
            }
            try { item.Destroy(); } catch { }
            return true; // 吃完了
        } catch { return true; }
    }
}
