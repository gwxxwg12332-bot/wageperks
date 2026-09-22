using System;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace JacksonPerks;

// 人神共愤：每天扣现金
internal sealed class HatedByAllPerk : CustomStartingPerk
{
    internal const string PerkId = "人神共愤";
    internal override string Id => PerkId;
    internal override string DisplayName => LangHelper.T("人神共愤", "Hated by All");
    internal override string Description => LangHelper.T(
        "人神共愤：每天自动扣现金（可扣成负数）。扣款上限 = 第 1 周 500 / 一周后 1500，再叠加天数×50（天数越多扣越狠）；存款超过 10000 时上限再 +1000。（你过去的所作所为已经传遍整个空间站——治安部的罚款、被你坑过的商户索赔、落魄顾客的纠缠、无赖的敲诈……每一天都有人登门，从你钱袋里刮走一笔，哪怕把你刮成负数也在所不惜。）",
        "Hated by All: lose cash every day (can go negative). Loss cap = 500 in week 1 / 1500 after, plus day×50 (more days = heavier loss); cap +1000 extra when cash over 10000. (Your past deeds are known across the station - security fines, claims from merchants you cheated, destitute customers hounding you, thugs extorting you... every day someone comes knocking to take a cut, even if it drags you into the red.)");
    internal override int Cost => -20; // 09-23 用户拍板：加重到 -20
    internal override int Type => 1;

    internal static bool IsActive()
    {
        return Core.PerkActive(PerkId);
    }

    internal override void OnNewGame() { }

    internal static void PostfixOnDayStart()
    {
        try
        {
            if (!IsActive()) return;
            var ps = PlayerStore.Instance;
            if (ps == null) return;

            int day = StoreStation.GetDayCounter();
            // 基础：第一周500，一周后1500
            int baseMax = day <= 7 ? 500 : 1500;
            // 天数加成：每天+50
            int dayBonus = day * 50;
            // 存款超过10000：每天+1000
            int wealthBonus = (ps.playerCash > 10000) ? 1000 : 0;
            int maxLoss = baseMax + dayBonus + wealthBonus;
            int cashLoss = DeterministicRandom.Next("hated_cash", day, 1, maxLoss + 1);

            // 扣现金（可以扣到负数）
            int cash = ps.playerCash;
            ps.playerCash = cash - cashLoss;

            // 夜报
            string msg = LangHelper.T(
                "人神共愤：现金 -" + cashLoss,
                "Hated by All: cash -" + cashLoss);
            try { ps.AddNightLog(msg, "#7FC97F"); } catch { }
        }
        catch { }
    }
}