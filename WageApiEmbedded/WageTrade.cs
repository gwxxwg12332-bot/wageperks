using System;

namespace WageAPI;

/// <summary>
/// 交易缓存桥（WS 内置版，10-16）。
/// 双装时：WS 写状态键（转发 WP WageSaveStore）只刷 WS 缓存，WP 交易算价（GetSellBonusPct）/生存面板仍读 WP 静态缓存
/// → 残留旧节点值（玩家实测：面板"节点+10%"但"总售价-10%"，购买价值不对）。
/// 通过反射调 WP（WagePerks.dll）的 WageAPI.WageTrade.InvalidateSurvivalCaches 同步失效 WP 缓存。
/// 单装 WS（无宿主）时 Type 解析失败 → 无害。
/// </summary>
public static class WageTrade
{
    /// <summary>失效宿主（WP）生存交易缓存（售价/预算/议价 + 报价面板文本）。</summary>
    public static void InvalidateSurvivalCaches()
    {
        try
        {
            var t = Type.GetType("WageAPI.WageTrade, WagePerks");
            if (t == null) return;
            var m = t.GetMethod("InvalidateSurvivalCaches", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (m == null) return;
            m.Invoke(null, null);
        }
        catch { /* 单装/宿主未加载：无害 */ }
    }
}
