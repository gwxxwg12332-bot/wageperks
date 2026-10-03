using Il2Cpp;

namespace WageSurvival;
internal static class GameItemExtensions
{
    internal static void Expel(this GameItem item)
    {
        try { item.parentInventory?.Expel(item); } catch { }
    }

    internal static int GetTagValue(this GameItem item, string tag, int defaultValue = 0)
    {
        try
        {
            if (!item.IsTag(tag)) return defaultValue;
            var tr = item.GetTagReadonly(tag);
            if (tr == null) return defaultValue;
            try { return tr.GetInt(); } catch { return defaultValue; }
        }
        catch { return defaultValue; }
    }

    internal static void SetTagValue(this GameItem item, string tag, int value)
    {
        try
        {
            if (item.IsTag(tag))
            {
                var tr = item.GetTagReadonly(tag);
                tr?.SetInt(value);
            }
        }
        catch { }
    }
}
