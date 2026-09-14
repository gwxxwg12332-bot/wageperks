using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace JacksonPerks;

// 水商之友特性
// 解锁水商NPC，每周来一次，卖水相关物品
internal sealed class WaterMerchantPerk : CustomStartingPerk
{
    internal const string PerkId = "水商之友";

    internal override string Id => PerkId;
    internal override string DisplayName => LangHelper.T("水商之友", "Water Merchant Friend");
    internal override string Description => LangHelper.T("一位走南闯北的水商听闻你的店铺名声，决定每周来你这拜访一次。他会带来海德拉净水器、水质扫描仪、高级滤水器、水瓶打印机和能量电池——在缺水的下层区，这些净水设备都是硬通货。选择此特性，水商每周到访一次，售卖净水设备和能源。", "A well-traveled water merchant heard of your shop and visits once a week, bringing Hydra purifiers, water scanners, advanced filters, bottle printers, and power cells--essential gear in the water-starved lower levels. Choose this perk: the water merchant visits weekly, selling purification gear and energy.");
    internal override int Cost => 2;
    internal override int Type => 0;

    // 上次水商来访的天数
    private static int _lastVisitDay = -1;
    private static int VISIT_INTERVAL => BuildConfig.WaterVisitInterval; // 来访间隔（CFG 可调）

    // 水商售卖的物品
    private static readonly string[] WaterItems = {
        // 解锁卡和对应箱子（配对）
        "eng_keycard","eng_box",
        "med_keycard","med_box",
        "sec_keycard","sec_box",
        "ser_keycard","service_box",
        "cmd_keycard","expedition_box",
        "sci_keycard","evidence_box",
        "sup_keycard","toolbox",
        // 单独解锁卡
        "blank_keycard","business_permit",
        "permit_gun_1","permit_gun_2","permit_gun_3",
        // 额外箱子
        "med_box","sec_box","expedition_box","evidence_box","toolbox"
    };
    internal override void OnNewGame()
    {
        _lastVisitDay = -1;
    }

    internal static bool IsActive()
    {
        return Core.PerkActive(PerkId);
    }

    // 检查是否应该让水商今天来
    internal static bool ShouldVisitToday(int currentDay)
    {
        // 每周第3天（星期3）到访，与其他特殊NPC错开
        return (currentDay % 7) == 3;
    }

    // 记录水商来访
    internal static void RecordVisit(int currentDay)
    {
        _lastVisitDay = currentDay;
    }

    // 生成水商的售卖物品
    internal static List<string> GenerateItems()
    {
        return MerchantHelper.GenerateCardLockPairs();
    }

    // ============================================================
    // 水商专属售卖：海德拉净水器 + 水检测器 + 高级滤水器 + 水瓶打印机 + 电池
    // （无药片、无检测试纸，按用户要求）
    // 在 HandleSpecialNpcArrived（OnNextClientArrived 时机，交易区就绪）调用，商品才能显示
    // ============================================================
    // ============================================================
    // evaporator 源头拦截（拆包 2.5.40）：
    // 原版退休水商 CreateEvaporator 无条件放柜台，进柜台唯一入口 =
    // PlayerStore.AddDirectSellingItemToTable（柜台商品走 PlayerStore 库存树，
    // 旧移除遍历 EmporiumEntry.GetAllItems 不在范围内 → 永远找不到）。
    // → 源头拦截：水商之友激活时 evaporator 不进柜台，不受时序/库存范围影响。
    // ============================================================
    public static bool PrefixAddDirectSellingItemToTable(GameItem gameItem)
    {
        try
        {
            if (!IsActive()) return true;
            if (gameItem == null) return true;
            if (gameItem.identifier == "evaporator") return false;
        }
        catch { }
        return true;
    }
    internal static void AddSpecialSellItems()
    {
        try
        {
            PlayerStore instance = PlayerStore.Instance;
            if (instance == null) { Core.LogMsg("[水商] PlayerStore.Instance为null，无法添加售卖"); return; }

            // 水商售卖清单

            string[] sellItems = {
                "portable_water_purifier", // 海德拉科技微型净水器
                "aquascan",                // 水质扫描仪（水检测机）
                "water_filter_adv",        // 高级滤水器
                "bottle_printer",          // 水瓶打印机
                "energy_credit",           // 能量电池
            };

            int added = 0;
            foreach (string wid in sellItems)
            {
                try
                {
                    GameItem w = DirectoryMaster.Item(wid, true);
                    if (w == null) { Core.LogMsg("[水商] 加 " + wid + " 不存在(null)"); continue; }
                    // 补名：部分物品名称空（本地化缺），显示问号
                    if (string.IsNullOrEmpty(w.name))
                    {
                        string fb = GetFallbackName(wid);
                        if (fb != null) { try { Il2Cpp.GeneralHelper.SetCustomName(w, fb);  } catch { } }
                    }
                    // 用通用方法添加到柜台（清标签+克隆+添加+再清标签）
                    GameItem sellW = MerchantHelper.AddItemToCounter(w, 100, false);
                    added++;
                }
                catch (Exception ex) { Core.LogMsg("[水商] 加 " + wid + " 失败: " + ex.Message); }
            }
        }
        catch (Exception ex)
        {
            Core.LogMsg("[水商] AddSpecialSellItems 失败: " + ex.Message + "\n" + ex.StackTrace);
        }
    }

    // 空名物品补中文名（本地化缺导致显示问号）
    private static string GetFallbackName(string id)
    {
        switch (id)
        {
            case "portable_water_purifier": return LangHelper.T("海德拉科技微型净水器", "HydraTech Micro Purifier");
            case "aquascan": return LangHelper.T("水质扫描仪", "Water Quality Scanner");
            case "water_filter_adv": return LangHelper.T("高级滤水器", "Advanced Water Filter");
            case "bottle_printer": return LangHelper.T("水瓶打印机", "Bottle Printer");
            case "energy_credit": return LangHelper.T("能量电池", "Energy Cell");
            default: return null;
        }
    }
}
