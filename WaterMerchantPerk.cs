using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace WagePerks;

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

    // PlayerStore.StartNewGame Postfix：开局给吞噬瓶
    public static void PostfixStartNewGame()
    {
        try
        {
            if (!IsActive()) { Core.LogMsg("[水商之友] 开局赠送跳过: 水商之友特性未激活（选特性后再开新档）"); return; }
            var em = Il2Cpp.EmporiumEntry.Instance;
            if (em == null || em.backInvinvElement == null) { Core.LogMsg("[水商之友] 开局赠送跳过: EmporiumEntry/后库未就绪（延迟补发）"); _bottleGiveFramesLeft = 180; return; }
            // 09-26 v1.2.10 稳定版：吞噬瓶未完成，改送普通大水瓶
            var bottle = Il2Cpp.WaterPremadeHelper.AccurateHighQualityWater("large_bottled_water");
            if (bottle != null)
            {
                var slot = em.backInvinvElement.TryFindOneValidInventorySlot(bottle, false);
                if (slot != null) { slot.TryAcceptOnce(); }
                else { try { var l = new Il2CppSystem.Collections.Generic.List<GameItem>(); l.Add(bottle); ((Il2Cpp.GameInventory)em.backInvinvElement).UncheckedAcceptAll(l); } catch { } }
                Core.LogMsg("[水商之友] 开局赠送大水瓶");
            }
            // 10-04 cfg：开局自带满级水瓶打印机（6000ml+纯水）
            if (!TryGiveMaxBottlePrinter()) { _bottleGiveFramesLeft = 180; Core.LogMsg("[水商之友] 开局打印机未发放成功，启动180帧延迟补发"); }
        } catch (System.Exception ex) { Core.LogMsg("[水商之友] 赠送吞噬瓶失败: " + ex.Message); }
    }

    // 10-04 cfg StartMaxBottlePrinter：开局自带满级水瓶打印机（水商之友/鲁滨逊共用）
    // 满级=瓶型 BOTTLE_PRINTER_UPGRADE_COUNT_TAG≥9（B2 注释实锤：原生≥9大瓶档→替换 water_jug 6000ml）
    //      + 质量 TOTAL_PERCENTAGE_QUALITY_BONUS_INT=400（水商 Getter 无减半→400=纯水 grade0；鲁滨逊×0.5→200=纯水 grade0）
    // 克隆隔离：CloneLinked 改 tag 不污染共享实例（cheatsheet 30.3）
    internal static bool TryGiveMaxBottlePrinter()
    {
        try
        {
            if (!BuildConfig.StartMaxBottlePrinter) { Core.LogMsg("[水商之友] 打印机发放跳过: cfg StartMaxBottlePrinter 未开启"); return false; }
            if (!IsActive() && !RobinCrusoePerk.IsActive()) { Core.LogMsg("[水商之友] 打印机发放跳过: 水商之友/鲁滨逊均未激活"); return false; }
            EmporiumEntry em = Il2Cpp.EmporiumEntry.Instance;
            if (em == null || em.backInvinvElement == null) { Core.LogMsg("[水商之友] 打印机发放跳过: EmporiumEntry/后库未就绪"); return false; }
            GameItem src = Il2Cpp.DirectoryMaster.Item("bottle_printer", true);
            if (src == null) { Core.LogMsg("[水商之友] 打印机发放跳过: DirectoryMaster.Item(bottle_printer) 返回 null"); return false; }
            GameItem printer = src.CloneLinked();
            if (printer == null) { Core.LogMsg("[水商之友] 打印机发放跳过: CloneLinked 返回 null"); return false; }
            ContainerUpgradeV2.AddTagInt(printer, "BOTTLE_PRINTER_UPGRADE_COUNT_TAG", 9);
            // 10-06 统一通道：满级质量写独立 tag wageBottleQlty=400（与升级同 tag，读取端 GetBottleQuality 优先独立 tag；
            // 不再写聚合 tag TOTAL_PERCENTAGE_QUALITY_BONUS_INT——旧档聚合值由 GetBottleQuality 回退兼容，升级时迁移合并）
            ContainerUpgradeV2.AddTagInt(printer, "wageBottleQlty", 400);
            try { Il2Cpp.GeneralHelper.SetItemOwned(printer, true); } catch { }
            var slot = em.backInvinvElement.TryFindOneValidInventorySlot(printer, false);
            if (slot != null) { try { slot.TryAcceptOnce(); Core.LogMsg("[水商之友] cfg: 开局赠送满级水瓶打印机(6000ml+纯水)"); return true; } catch { } }
            try { var l = new Il2CppSystem.Collections.Generic.List<GameItem>(); l.Add(printer); ((Il2Cpp.GameInventory)em.backInvinvElement).UncheckedAcceptAll(l); Core.LogMsg("[水商之友] cfg: 开局赠送满级水瓶打印机(6000ml+纯水,兜底)"); return true; } catch { }
            Core.LogMsg("[水商之友] 打印机发放失败: 塞后库异常（TryAcceptOnce/UncheckedAcceptAll 均失败）");
            return false;
        }
        catch (System.Exception ex) { Core.LogMsg("[水商之友] 赠送满级水瓶打印机失败: " + ex.Message); return false; }
    }

    internal static bool IsActive()
    {
        return Core.PerkActive(PerkId);
    }

    // 检查是否应该让水商今天来
    internal static bool ShouldVisitToday(int currentDay)
    {
        // CR-14 修复（09-23）：原来硬编码 % 7，配置项 WaterVisitInterval 改了不生效。
        // 改为读配置；默认 7 → 行为与原来完全一致（零回归）。
        // 错开偏移 3 取模，保证间隔改小时偏移仍落在周期内，不会永远等不到。
        int interval = VISIT_INTERVAL > 0 ? VISIT_INTERVAL : 7;
        return (currentDay % interval) == (3 % interval);
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
        catch (System.Exception ex) { Core.LogMsg("[WaterMerchantPerk] 异常: " + ex.Message); }
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
                    GameItem w = WageAPI.WageItemFactory.Create(wid); // 10-06 收敛：WageItemFactory 权威创建（A 类纯创建）
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
            // 10-05 修复：优先读独立 tag wageBottleQlty（升级改写此 tag + 跨档恢复写回）；无则回退原版 Getter（兼容其他来源）
            try { quality = GetBottleQuality(printer); } catch { }
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

    // ===================== 10-05 水瓶机质量跨档修复（拆包实锤：场景机器 tags 不随档→读档归零）=====================
    // 升级写独立 tag wageBottleQlty + WageSaveStore 双写（wage_bottle_qN，N=场景顺序索引）；
    // 读档后遍历玩家库存网格按出现顺序恢复（虚空珠同款模式：PostfixLoadGame 立即试 + 帧重试）

    private static int _bottleRestoreFramesLeft = 0;
    // 10-05 开局发放帧重试（StartNewGame Postfix 时 EmporiumEntry 可能未建好→延迟补发）
    private static int _bottleGiveFramesLeft = 0;

    // 判级读端：独立 tag 优先（当档/跨档恢复后都在此），无则回退原版 Getter（兼容第三方写的聚合 tag）
    internal static int GetBottleQuality(GameItem printer)
    {
        try
        {
            int q = RobinCrusoePerk.GetTagIntSafe(printer, "wageBottleQlty");
            if (q > 0) return q;
        }
        catch { }
        try { return Il2Cpp.MachineryHelper.GetCurrentQualityBonus(printer); } catch { }
        return 0;
    }

    // 玩家所有库存网格（虚空珠同款：后背包/计数器/展示柜/主库存/暗格 + 递归容器）
    internal static List<GameInventory> GetPlayerInventories()
    {
        var allInvs = new List<GameInventory>();
        try
        {
            EmporiumEntry emporium = EmporiumEntry.Instance;
            if (emporium != null)
            {
                try { var v = emporium.backInvinvElement as GameInventory; if (v != null) allInvs.Add(v); } catch { }
                try { var v = emporium.backInvinvElementCounter as GameInventory; if (v != null) allInvs.Add(v); } catch { }
                try { var v = emporium.showcaseElement as GameInventory; if (v != null) allInvs.Add(v); } catch { }
                try { var v = emporium.invElement as GameInventory; if (v != null) allInvs.Add(v); } catch { }
                try { var v = emporium.hiddenElement as GameInventory; if (v != null) allInvs.Add(v); } catch { }

                var visited = new HashSet<IntPtr>();
                var stack = new Stack<GameInventory>(allInvs);
                while (stack.Count > 0)
                {
                    var inv = stack.Pop();
                    if (inv == null || inv.childItems == null) continue;
                    for (int i = 0; i < inv.childItems.Count; i++)
                    {
                        var it = inv.childItems[i];
                        if (it == null || !visited.Add(it.Pointer)) continue;
                        try
                        {
                            var cw = it.contentWindow;
                            if (cw == null || cw.childElement == null) continue;
                            var inner = cw.childElement.TryCast<GameGridInventory>();
                            if (inner != null && !allInvs.Contains(inner)) { allInvs.Add(inner); stack.Push(inner); }
                        }
                        catch { }
                    }
                }
            }
        }
        catch { }
        return allInvs;
    }

    // 水瓶机在玩家库存网格中的顺序索引（升级双写/读档恢复关联用；-1=不在库存网格（柜台/地上）→ 不双写）
    internal static int GetBottlePrinterIndex(GameItem target)
    {
        try
        {
            int n = 0;
            foreach (var inv in GetPlayerInventories())
            {
                if (inv == null || inv.childItems == null) continue;
                for (int i = 0; i < inv.childItems.Count; i++)
                {
                    var it = inv.childItems[i];
                    if (it == null) continue;
                    string id = ""; try { id = it.identifier ?? ""; } catch { }
                    if (id.ToLowerInvariant() == "bottle_printer")
                    {
                        if (it.Pointer == target.Pointer) return n;
                        n++;
                    }
                }
            }
        }
        catch { }
        return -1;
    }

    // 读档恢复：遍历库存网格按出现顺序把 WageSaveStore 双写值写回独立 tag
    private static bool RestoreAllBottlePrinters()
    {
        int n = 0;
        bool any = false;
        try
        {
            foreach (var inv in GetPlayerInventories())
            {
                if (inv == null || inv.childItems == null) continue;
                for (int i = 0; i < inv.childItems.Count; i++)
                {
                    var it = inv.childItems[i];
                    if (it == null) continue;
                    string id = ""; try { id = it.identifier ?? ""; } catch { }
                    if (id.ToLowerInvariant() != "bottle_printer") continue;
                    any = true;
                    int saved = WageSaveStore.GetInt("RobinCrusoe", "wage_bottle_q" + n, -1);
                    if (saved > 0)
                    {
                        int cur = RobinCrusoePerk.GetTagIntSafe(it, "wageBottleQlty");
                        if (cur < saved) { RobinCrusoePerk.SetTagIntValue(it, "wageBottleQlty", saved); Core.LogMsg("[水商] 水瓶机读档恢复质量: " + saved + " (idx=" + n + ")"); }
                    }
                    n++;
                }
            }
        }
        catch (Exception ex) { Core.LogMsg("[水商] 水瓶机恢复异常: " + ex.Message); }
        return any;
    }

    // 读档挂点：立即试一次 + 帧重试（容器内容延迟加载，虚空珠同款）
    public static void PostfixLoadGameBottlePrinter()
    {
        try
        {
            if (RestoreAllBottlePrinters()) { _bottleRestoreFramesLeft = 0; return; }
            _bottleRestoreFramesLeft = 180;
        }
        catch (Exception ex) { Core.LogMsg("[水商] 水瓶机恢复挂点异常: " + ex.Message); }
    }

    // Core 帧循环调用：延迟重试恢复
    public static void OnUpdateRestoreBottlePrinters()
    {
        try
        {
            // 10-05 开局发放帧重试（StartNewGame 时后库未就绪→延迟补发）
            if (_bottleGiveFramesLeft > 0)
            {
                _bottleGiveFramesLeft--;
                if (TryGiveMaxBottlePrinter()) { _bottleGiveFramesLeft = 0; Core.LogMsg("[水商之友] 延迟补发成功"); }
            }
            if (_bottleRestoreFramesLeft <= 0) return;
            _bottleRestoreFramesLeft--;
            if (RestoreAllBottlePrinters()) _bottleRestoreFramesLeft = 0;
        }
        catch { }
    }
}
