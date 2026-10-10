using System;
using System.Collections.Generic;

namespace WageSurvival;
internal static class Patches
{
    internal static int CurrentUITradeMode = 0;

    // 10-10 四问题批#3b：词条防重集合（对齐 WP Patches.Trade.cs:69 _nodeBuffItems 模式，进程级防御纵深）
    // SurvivalTrade.AddPriceLabel 已按 itemFeatures 同 id 判重（SurvivalTrade.cs:227-244 实锤）；
    // 本集合为"同一物品同一 labelId 只加一次"的进程级第二层（防跨状态周期/防清理时机竞态堆积）。
    internal static readonly HashSet<string> _nodeBuffItems = new HashSet<string>();

    // 10-10 四问题批#3b：原空实现补真实清理（调用点 SurvivalFxEval.cs:26）——清空集合防跨周期堆积
    internal static void ClearNodeBuffItems()
    {
        try { _nodeBuffItems.Clear(); } catch { }
    }
}
