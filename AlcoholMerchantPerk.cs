using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace JacksonPerks;

// 收酒商特性
// 解锁收酒商NPC，每周来一次，高价收购玩家自酿酒 + 售卖酿酒原料（葡萄/酵母/水）
internal sealed class AlcoholMerchantPerk : CustomStartingPerk
{
    internal const string PerkId = "酒商之友";

    internal override string Id => PerkId;
    internal override string DisplayName => LangHelper.T("酒商之友", "Alcohol Merchant Friend");
    internal override string Description => LangHelper.T("一位精明的收酒商每周第 6 天准时登门，专门收购你自酿的各类美酒，出价公道。他同时售卖优质酿酒原料——精选葡萄、特级酵母和纯净水源，助你酿出更好的酒。下层区的酒鬼们都等着你的佳酿，而这位收酒商就是你最好的合作伙伴。选择此特性，收酒商每周第 6 天到访，高价收购你的自酿酒并售卖酿酒原料。", "A shrewd wine buyer visits every week on day 6, specializing in purchasing your homemade wines at fair prices. He also sells quality brewing supplies - premium grapes, super yeast, and pure water to help you craft better wines. Drunks in the lower levels are waiting for your brew, and this buyer is your best partner. Choose this perk: the wine buyer arrives on day 6 of each week, buying your homemade wines at good prices and selling brewing supplies.");
    internal override int Cost => 2;
    internal override int Type => 0;

    // 上次酒商来访的天数
    private static int _lastVisitDay = -1;
    private static int VISIT_INTERVAL => BuildConfig.AlcoholVisitInterval; // 来访间隔（CFG 可调）
    internal override void OnNewGame()
    {
        _lastVisitDay = -1;
    }

    internal static bool IsActive()
    {
        return Core.PerkActive(PerkId);
    }

    // 检查是否应该让酒商今天来
    internal static bool ShouldVisitToday(int currentDay)
    {
        // CR-14 修复（09-23）：原来硬编码 % 7，配置项 AlcoholVisitInterval 改了不生效。
        // 改为读配置；默认 7 → 行为与原来完全一致（零回归）。
        // 错开偏移 6 取模，保证间隔改小（如 3）时偏移仍落在周期内，不会永远等不到。
        int interval = VISIT_INTERVAL > 0 ? VISIT_INTERVAL : 7;
        return (currentDay % interval) == (6 % interval);
    }

    // 记录酒商来访
    internal static void RecordVisit(int currentDay)
    {
        _lastVisitDay = currentDay;
    }

    // 强制酒商来访（测试mod QuickItemSpawner F7 反射调用，勿删）
    internal static void ForceVisit()
    {
        try
        {
            Il2Cpp.PlayerStore ps = Il2Cpp.PlayerStore.Instance;
            if (ps == null) { Core.LogMsg("[酒商] ForceVisit: PlayerStore为空"); return; }
            ps.QueueFuturClient("retired_winemaker", 0);
        }
        catch (Exception ex) { Core.LogMsg("[酒商] ForceVisit异常: " + ex.Message); }
    }

    // 生成酒商的售卖物品
    internal static List<string> GenerateItems()
    {
        return MerchantHelper.GenerateCardLockPairs();
    }

    // 测试快捷键：强制酒商来访（按经验：客户货物在SetEndAction里添加，而非创建时）
    // 收酒商专属售卖：酿酒原料（葡萄/酵母/水），不卖酒；酒商通过clientBuyingTagList收购玩家的酒
    // 在 HandleSpecialNpcArrived（StartMainDialogue 时机，交易区就绪）调用，商品才能显示
    private static int _berrySellCount = 0;
    internal static void AddSpecialSellItems()
    {
        try
        {
            PlayerStore instance = PlayerStore.Instance;
            if (instance == null) { Core.LogMsg("[酒商] PlayerStore.Instance为null，无法添加售卖"); return; }

            int added = 0;

            // 酒类售卖列表：葡萄多放、不卖空酒瓶（wine_bottle），啤酒/红啤/超级酵母/优质水各一
            string[] sellWines = { "wine_yeast", "wine_superyeast", "bottled_water" }; // 收酒商模式：只卖制酒原料（酵母+水），不卖酒；葡萄已在上面单独售卖

            // 0. 葡萄：只卖小葡萄（cond=10，玩家酿酒用）
            for (int g = 0; g < 3; g++)
            {
                try
                {
                    GameItem grape = DirectoryMaster.Item("wine_berry", true);
                    if (grape == null) { Core.LogMsg("[酒商] 加葡萄 wine_berry 不存在(null)"); break; }
                    _berrySellCount++;
                    int cond = 10; // 小葡萄
                    try { Il2Cpp.WineHelper.InitBerry(grape); } catch { }
                    try { Il2Cpp.WineHelper.SetBerryCondition(grape, cond, 0); } catch { }
                    // 用通用方法添加到柜台（清标签+克隆+添加+再清标签）
                    GameItem sellGrape = MerchantHelper.AddItemToCounter(grape, 100, false);
                    if (sellGrape != null) { added++;  }
                }
                catch (Exception ex) { Core.LogMsg("[酒商] 加葡萄失败: " + ex.Message); }
            }

            // 1. 其他酒类（各种版本）
            foreach (string wid in sellWines)
            {
                try
                {
                    GameItem w = DirectoryMaster.Item(wid, true);
                    if (w == null) { Core.LogMsg("[酒商] 加酒 " + wid + " 不存在(null)"); continue; }
                    // 优质水（矿泉水瓶装优质水质）——酿造配方用水
                    if (wid == "bottled_water")
                    {
                        GameItem bw = null;
                        try { var foodDir = UnityEngine.Object.FindObjectOfType<Il2Cpp.FoodItemDirectory>(); if (foodDir != null) { bw = foodDir.Create("bottled_water"); } } catch { }
                        if (bw == null) { bw = DirectoryMaster.Item("bottled_water", true); }
                        if (bw != null) { try { Il2Cpp.WaterHelper.FillWithHighQualityWater(bw); } catch { } w = bw; }
                    }
                    // 补名：部分物品名称空（本地化缺），显示问号
                    if (string.IsNullOrEmpty(w.name))
                    {
                        string fb = GetFallbackName(wid);
                        if (fb != null) { try { Il2Cpp.GeneralHelper.SetCustomName(w, fb);  } catch { } }
                    }
                    // 用通用方法添加到柜台（清标签+克隆+添加+再清标签）
                    GameItem sellW = MerchantHelper.AddItemToCounter(w, 100, false);
                    if (sellW != null) { added++;  }
                }
                catch (Exception ex) { Core.LogMsg("[酒商] 加酒 " + wid + " 失败: " + ex.Message); }
            }

        }
        catch (Exception ex)
        {
            Core.LogMsg("[酒商] AddSpecialSellItems 失败: " + ex.Message + "\n" + ex.StackTrace);
        }
    }

    // 空名物品补中文名（本地化缺导致显示问号）
    private static string GetFallbackName(string id)
    {
        switch (id)
        {
            case "wine_berry": return LangHelper.T("莓果酿", "Berry Wine");
            case "wine_bottle": return LangHelper.T("酒瓶", "Wine Bottle");
            case "beer_case": return LangHelper.T("一箱啤酒", "Case of Beer");
            case "red_beer": return LangHelper.T("红魔鬼啤酒", "Red Devil Beer");
            case "empty_beer_bottle": return LangHelper.T("空啤酒瓶", "Empty Beer Bottle");
            case "wine_superyeast": return LangHelper.T("超级酵母", "Super Yeast");
            case "wine_yeast": return LangHelper.T("酿酒酵母", "Brewing Yeast");
            case "wine_yeast_infinite": return LangHelper.T("永续酵母", "Perpetual Yeast");
            case "wine_yeast_red": return LangHelper.T("红酵母", "Red Yeast");
            case "permit_gun_1": return LangHelper.T("枪支许可证（一级）", "Gun Permit (Tier 1)");
            case "permit_gun_2": return LangHelper.T("枪支许可证（二级）", "Gun Permit (Tier 2)");
            case "permit_gun_3": return LangHelper.T("枪支许可证（三级）", "Gun Permit (Tier 3)");
            case "blank_keycard": return LangHelper.T("空白钥匙卡", "Blank Keycard");
            default: return null;
        }
    }
}
