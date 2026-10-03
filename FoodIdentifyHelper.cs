using Il2Cpp;

namespace WageSurvival;

// ============================================================
// FoodIdentifyHelper 门面（2026-10-03 阶段C：转发 WageAPI.FoodIdentifyHelper 权威判定）
// ID 集合权威值在 WageAPI（拆包实锤；v0.1.2 复制版存在漂移已修正——如烟草 cig_red、麻醉品 oxycodone_pill 系、彩票 scratch）。
// IsClean = 清洁用品判定 → 权威日用品集（DAILY_NEED_KEYS，拆包 11 项）。
// ============================================================
public static class FoodIdentifyHelper
{
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

    internal static bool IsClean(GameItem item)
        => WageAPI.FoodIdentifyHelper.IsDailyNeed(item);

    internal static bool IsDailyNeed(GameItem item)
        => WageAPI.FoodIdentifyHelper.IsDailyNeed(item);
}
