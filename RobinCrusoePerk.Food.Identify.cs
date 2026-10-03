using Il2Cpp;

namespace WagePerks;

// ============================================================
// RobinCrusoePerk 识别函数门面（2026-10-03 阶段B：提炼至 WageAPI.FoodIdentifyHelper 后方法体转发）
// 方法名/签名原样保留（兼容补丁反射依赖方法名存在，cheatsheet:60）——现有调用点零改动。
// ID 集合权威值在 WageAPI.FoodIdentifyHelper（拆包实锤；v0.1.2 复制版存在漂移，已按权威值修正）。
// ============================================================
internal static partial class RobinCrusoePerk
{
    internal static bool IsContrabandItem(GameItem item)
        => WageAPI.FoodIdentifyHelper.IsContrabandItem(item);

    internal static bool IsFood(GameItem item)
        => WageAPI.FoodIdentifyHelper.IsFood(item);

    internal static bool IsDrink(GameItem item)
        => WageAPI.FoodIdentifyHelper.IsDrink(item);

    internal static bool IsMedicine(GameItem item)
        => WageAPI.FoodIdentifyHelper.IsMedicine(item);

    internal static bool IsAlc(GameItem item)
        => WageAPI.FoodIdentifyHelper.IsAlc(item);

    internal static bool IsTobacco(GameItem item)
        => WageAPI.FoodIdentifyHelper.IsTobacco(item);

    internal static bool IsNarcotic(GameItem item)
        => WageAPI.FoodIdentifyHelper.IsNarcotic(item);

    internal static bool IsLottery(GameItem item)
        => WageAPI.FoodIdentifyHelper.IsLottery(item);

    internal static bool IsDailyNeed(GameItem item)
        => WageAPI.FoodIdentifyHelper.IsDailyNeed(item);
}
