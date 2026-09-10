using System;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace JacksonPerks;

// 修复：假酒卖给上层人时卖不了（点了没反应）
// 根因：BarterHelper.DoesTraderAcceptThisItemAsPayment 对假酒返回0（不接受），交易按钮失效
// 修复：Postfix，当物品是酒类且返回值<=0时，强制返回1（接受）
internal static class CounterfeitWineTradeFix
{
    public static void Postfix(GameItem item, GameCharacterItem trader, ref double __result)
    {
        try
        {
            // 只处理返回值<=0（不接受）的情况
            if (__result > 0) return;
            if (item == null) return;

            // 判断是否是酒类（精确匹配，排除葡萄/酵母等原料）
            string itemId = item.identifier ?? "";
            string itemName = item.name ?? "";
            // 酒类白名单：成品酒，排除 wine_berry(葡萄)/wine_yeast(酵母)/wine_superyeast(超级酵母)
            bool isAlcohol = itemId == "wine_bottle" || itemId == "red_beer" || itemId == "nudka"
                              || itemId == "beer" || itemId == "alcohol"
                              || itemId.EndsWith("_wine") || itemId.EndsWith("_beer")
                              || itemName.Contains("酒") || itemName.Contains("啤")
                              || itemName.Contains("wine") || itemName.Contains("beer");

            if (isAlcohol)
            {
                // 强制返回1（接受），让交易按钮可用
                __result = 1.0;
            }
        }
        catch (Exception ex)
        {
            Core.LogMsg("[假酒修复] Postfix异常: " + ex.Message);
        }
    }
}
