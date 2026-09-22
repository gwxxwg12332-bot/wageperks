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
    internal override string Description => LangHelper.T("一位走南闯北的水商听闻你的店铺名声，决定每周第 3 天来你这拜访一次。他会带来海德拉净水器、水质扫描仪、高级滤水器、水瓶打印机和能量电池——在缺水的下层区，这些净水设备都是硬通货。水瓶打印机在你手上潜力无穷：用电子元件升级瓶型，满级可打印 6000ml 超大瓶；投入金属锭提升打印质量，质量达到 100 即可装出普通水，更高则优质水、纯水（鲁滨逊特性自带此功能）。选择此特性，水商每周第 3 天到访，售卖净水设备和能源。", "A well-traveled water merchant heard of your shop and visits every week on day 3, bringing Hydra purifiers, water scanners, advanced filters, bottle printers, and power cells--essential gear in the water-starved lower levels. The bottle printer is a gem in your hands: upgrade its bottle types with electronic components (max level prints 6000ml jugs), and feed it metal ingots to raise print quality - quality 100 yields regular water, higher yields premium and pure water (Robinson Crusoe perk has this built-in). Choose this perk: the water merchant arrives on day 3 of each week, selling purification gear and energy.");
    internal override int Cost => 5;
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

    // ===== 瓶印机打印增强（09-15 用户拍板）=====
    // B1 挂点：MachineBottlePrinter 嵌套闭包类 __c__DisplayClass6_0 的 TryPrint（Demo Cpp2IL 重命名 = Method_Internal_Void_String_Int32_0，实例方法，闭包含 outputGrid）
    // B2 替换：bottleId == "large_bottled_water"（原生 ≥9 大瓶档）→ water_jug 超大瓶（空瓶）
    // B3 装水：质量 ≥100 起（100-149→grade 2 基准水 / 150-199→grade 1 高质水 / ≥200→grade 0 纯水）；<100 空瓶
    // B4 双链并存：原生 BOTTLE_PRINTER_UPGRADE_COUNT_TAG（升瓶型）与 mod 质量 tag（升水质）互不干扰
    private static int _printPrevCount = -1; // 打印前输出格物品数（识别新瓶）

    public static void PrefixTryPrint(Il2Cpp.MachineBottlePrinter.__c__DisplayClass6_0 __instance, ref string bottleId, int cost)
    {
        try
        {
            // B2：大瓶档 → 超大瓶（空瓶）
            if (bottleId != null && bottleId == "large_bottled_water") bottleId = "water_jug";
            _printPrevCount = CountPrinterOutput(__instance);
        }
        catch { _printPrevCount = -1; }
    }

    public static void PostfixTryPrint(Il2Cpp.MachineBottlePrinter.__c__DisplayClass6_0 __instance)
    {
        try
        {
            var printer = __instance.machine;
            int quality = 0;
            try { quality = Il2Cpp.MachineryHelper.GetCurrentQualityBonus(printer); } catch { }
            int grade = -1;
            if (quality >= 200) grade = 0;        // 纯水（毕业）
            else if (quality >= 150) grade = 1;   // 高质水
            else if (quality >= 100) grade = 2;   // 基准水
            if (grade < 0) { _printPrevCount = -1; return; } // <100：不出水，保持空瓶
            var grid = __instance.outputGrid;
            if (grid == null || grid.childItems == null) { _printPrevCount = -1; return; }
            int start = _printPrevCount > 0 ? _printPrevCount : 0;
            for (int i = start; i < grid.childItems.Count; i++)
            {
                var it = grid.childItems[i];
                if (it == null) continue;
                string id = "";
                try { id = it.identifier ?? ""; } catch { }
                if (id != "small_bottled_water" && id != "bottled_water" && id != "water_jug") continue;
                try { Il2Cpp.WaterHelper.AddWater(it, grade, -1, false, 0, 1, true); } catch { }
            }
            _printPrevCount = -1;
        }
        catch { _printPrevCount = -1; }
    }

    private static int CountPrinterOutput(Il2Cpp.MachineBottlePrinter.__c__DisplayClass6_0 __instance)
    {
        try
        {
            var grid = __instance.outputGrid;
            if (grid == null || grid.childItems == null) return 0;
            return grid.childItems.Count;
        }
        catch { return -1; }
    }
}
