using System;
using System.Collections.Generic;

namespace JacksonPerks;

// 事件总线（Subscribe / Publish）
internal static class EventBus
{
    private static readonly Dictionary<string, List<Action>> _subs = new();

    public static void Subscribe(string eventName, Action callback)
    {
        if (!_subs.ContainsKey(eventName)) _subs[eventName] = new List<Action>();
        _subs[eventName].Add(callback);
    }

    public static void Publish(string eventName)
    {
        if (!_subs.ContainsKey(eventName)) return;
        foreach (var cb in _subs[eventName])
        {
            try { cb(); } catch { }
        }
    }
}