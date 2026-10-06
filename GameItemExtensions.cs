using Il2Cpp;

namespace WageSurvival;
internal static class GameItemExtensions
{
    internal static void Expel(this GameItem item)
    {
        try { item.parentInventory?.Expel(item); } catch { }
    }

    /// <summary>10-06 tag 权威封装收敛：委托 WageTag.GetInt（读实例态 modifiedState；无 tag/异常→defaultValue）。</summary>
    internal static int GetTagValue(this GameItem item, string tag, int defaultValue = 0)
        => WageAPI.WageTag.GetInt(item, tag, defaultValue);

    /// <summary>10-06 tag 权威封装收敛（缺陷修复）：原实现 tr?.SetInt(value) 缺 EnableTag 存在性——tag 不存在时静默失败；
    /// 改 WageTag.SetInt（无 tag 先 EnableTag → ModifyTag SetInt → 原版 SyncModifiedState 同步）。</summary>
    internal static void SetTagValue(this GameItem item, string tag, int value)
        => WageAPI.WageTag.SetInt(item, tag, value);
}
