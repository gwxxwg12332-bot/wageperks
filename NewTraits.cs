using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace JacksonPerks;

// ============================================================
// 混合特性：刀尖舔血
// 违禁品买卖价+20%，检查频率+30%
// ============================================================
internal sealed class RiskTakerPerk : CustomStartingPerk
{
    internal const string PerkId = "刀尖舔血";

    internal override string Id => PerkId;
    internal override string DisplayName => LangHelper.T("刀尖舔血", "Blood Blade");
    internal override string Description => LangHelper.T("高风险高回报的赌徒特性（2点）。违禁品买卖价+20%，利润丰厚；代价是治安部永远盯着你——每天强制检查，连满信誉豁免也无效。吞噬季每 10 天降临：机器里的模组会互相吞噬融合，产出高级违禁品。利润越高，越可能翻车。", "High-risk high-reward gambler (2 points). Contraband price +20 percent, but Security is always watching: mandatory inspection every day — even max reputation won't spare you. Every 10 days, Cannibalism Season strikes: modules in machines devour each other, yielding high-grade contraband. Higher profit, higher risk.");
    internal override int Cost => 2; // 09-16 用户拍板：需要 2 特性点
    internal override int Type => 0; // 09-17 用户拍板：正面特性（绿色）；违禁品收益是主要面向，强制检查为伴随代价

    internal override void OnNewGame()
    {
    }

    internal static bool IsActive()
    {
        return Core.PerkActive(PerkId);
    }

    // 获取违禁品价格加成
    public static float GetContrabandPriceBonus()
    {
        return IsActive() ? 1.20f : 1.0f;
    }

    // 获取检查频率加成
    public static float GetInspectionChanceBonus()
    {
        return IsActive() ? 1.30f : 1.0f;
    }
}

// ============================================================
// 混合特性：好酒之徒
// 酒类买卖价+25%，打烊后可能宿醉
// ============================================================
internal sealed class WineLoverPerk : CustomStartingPerk
{
    internal const string PerkId = "好酒之徒";

    internal override string Id => PerkId;
    internal override string DisplayName => LangHelper.T("好酒之徒", "Wine Lover");
    internal override string Description => LangHelper.T("内行懂酒，酒类买卖价+25%，客户觉得你是懂行的人。但代价是打烊后可能'宿醉'：次日-1客户或议价-10%。白天懂酒，晚上头痛。", "Alcohol connoisseur. Alcohol price +25 percent. Possible hangover after closing.");
    internal override int Cost => 1;
    internal override int Type => 2; // 混合特性显示为黄色
    private static bool _hungover = false;

    internal override void OnNewGame()
    {
        _hungover = false;
    }

    internal static bool IsActive()
    {
        return Core.PerkActive(PerkId);
    }

    // 获取酒类价格加成
    public static float GetAlcoholPriceBonus()
    {
        return IsActive() ? 1.25f : 1.0f;
    }

    // 检查是否宿醉
    public static bool IsHungover()
    {
        return _hungover;
    }

    // 打烊后roll宿醉
    public static void RollHangover()
    {
        if (!IsActive()) return;
                // 用确定性随机数，读档后宿醉概率一致
                int day = StoreStation.GetDayCounter();
                if (DeterministicRandom.NextBool("wine_lover_hangover", day, 0.3)) // 30%概率宿醉
        {
            _hungover = true;
            // 保存宿醉状态到PlayerPrefs（读档后恢复）
            try { PerkStatePersistence.SetBool(PerkId, "hungover", true); } catch { }
        }
    }

    // 新的一天清除宿醉
    public static void ClearHangover()
    {
        if (_hungover)
        {
            _hungover = false;
            // 清除PlayerPrefs中的宿醉状态
            try { PerkStatePersistence.SetBool(PerkId, "hungover", false); } catch { }
        }
    }

    // 从PlayerPrefs恢复宿醉状态（读档时调用）
    public static void LoadHangoverState()
    {
        try
        {
            if (PerkStatePersistence.HasKey(PerkId, "hungover"))
            {
                _hungover = PerkStatePersistence.GetBool(PerkId, "hungover", false);
            }
        }
        catch { }
    }
}

// ============================================================
// 负面特性：童叟无欺（原笑面虎重做 09-19）
// 声誉增长速度 +25%，卖出商品收益 -25%
// ============================================================
internal sealed class SmilingTigerPerk : CustomStartingPerk
{
    internal const string PerkId = "童叟无欺";

    internal override string Id => PerkId;
    internal override string DisplayName => LangHelper.T("童叟无欺", "Honest Dealer");
    internal override string Description => LangHelper.T("做生意童叟无欺：声誉增长速度 +25%，客户更信任你；但你的售价也得公道——卖出商品收益 -25%。", "Honest dealing: reputation gain +25 percent, but you sell at fair prices - sale income -25 percent.");
    internal override int Cost => -10;
    internal override int Type => 1; // 负面红色
    internal override string[] IncompatibleIds => new[] { "笑面虎" }; // 与笑面虎互斥

    internal override void OnNewGame()
    {
    }

    internal static bool IsActive()
    {
        return Core.PerkActive(PerkId); // 笑面虎已是独立特性，不再双认
    }
}

// ============================================================
// 正面特性：笑面虎（与童叟无欺互斥）
// 所有商品售价 +25%（正面绿色）
// ============================================================
internal sealed class SmilingFacePerk : CustomStartingPerk
{
    internal const string PerkId = "笑面虎";

    internal override string Id => PerkId;
    internal override string DisplayName => LangHelper.T("笑面虎", "Smiling Tiger");
    internal override string Description => LangHelper.T("你总是笑脸迎人，顾客愿意为你的笑容多掏钱：所有商品售价 +25%。", "Always smiling, customers pay more for your goods: all item sale price +25 percent.");
    internal override int Cost => 1;
    internal override int Type => 0; // 正面绿色
    internal override string[] IncompatibleIds => new[] { "童叟无欺" }; // 与童叟无欺互斥

    internal override void OnNewGame()
    {
    }

    internal static bool IsActive()
    {
        return Core.PerkActive(PerkId);
    }
}

// ============================================================
// 补丁：新的一天处理好酒之徒宿醉
// ============================================================
// [HarmonyPatch(typeof(GameMaster), "OnNewDay")]
internal static class NewTraitsNewDayPatch
{
    static void Postfix()
    {
        // 好酒之徒：新的一天清除宿醉
        WineLoverPerk.ClearHangover();
    }
}

// ============================================================
// 补丁：打烊后处理好酒之徒宿醉
// ============================================================
// [HarmonyPatch(typeof(PlayerStore), "DismissCurrentClient")]
internal static class NewTraitsDismissPatch
{
    static void Postfix()
    {
        // 好酒之徒：最后一个客户走后roll宿醉（简化处理）
        // 实际应该在打烊时触发，这里简化
    }
}

// ============================================================
// 负面特性：招贼体质
// 治安部检查概率+20%，可疑客户更多（红色负面）
// ============================================================
internal sealed class ThiefMagnetPerk : CustomStartingPerk
{
    internal const string PerkId = "招贼体质";

    internal override string Id => PerkId;
    internal override string DisplayName => LangHelper.T("招贼体质", "Thief Magnet");
    internal override string Description => LangHelper.T("你天生招贼，可疑顾客特别爱光顾你的店。治安部检查概率+20%，小偷、骗子和可疑客户出现频率大幅上升。夜里锁门要锁好。", "Naturally attracts thieves. Inspection +20 percent, suspicious customers increased.");
    internal override int Cost => -2;   // 返还2点
    internal override int Type => 1;    // 负面特性显示为红色

    internal override void OnNewGame() { }

    internal static bool IsActive()
    {
        return Core.PerkActive(PerkId);
    }

}

// ============================================================
// 负面特性：霉运缠身
// 每天打烊后可能丢钱（红色负面）
// ============================================================
internal sealed class BadLuckPerk : CustomStartingPerk
{
    internal const string PerkId = "霉运缠身";

    internal override string Id => PerkId;
    internal override string DisplayName => LangHelper.T("霉运缠身", "Bad Luck");
    internal override string Description => LangHelper.T("你仿佛被诅咒了。每天新的一天开始时都会丢失一笔钱（100-1500信用点），财运尽散。命运在跟你开玩笑。", "Seems cursed. Lose 100-1500 credits every day as a new day begins.");
    internal override int Cost => -10;   // 返还10点（每天50-200平均125/天×永久）
    internal override int Type => 1;    // 负面特性显示为红色

    internal override void OnNewGame() { }

    internal static bool IsActive()
    {
        return Core.PerkActive(PerkId);
    }
}

// ============================================================
// 负面特性：信誉扫地
// 卖价-10%，买价+10%（红色负面）
// ============================================================
internal sealed class BadReputationPerk : CustomStartingPerk
{
    internal const string PerkId = "信誉扫地";

    internal override string Id => PerkId;
    internal override string DisplayName => LangHelper.T("信誉扫地", "Bad Reputation");
    internal override string Description => LangHelper.T("你的名声很差，顾客不信任你。卖东西价格-20%，买东西价格+20%——顾客总想趁火打劫。想翻身，先挽回名声。", "Poor reputation. Sell price -20 percent, buy price +20 percent.");
    internal override int Cost => -7;   // 返还7点（实际卖-20%/买+20%，4势力好感全20才解除，50天+）
    internal override int Type => 1;    // 负面特性显示为红色

    internal override void OnNewGame() { }

    internal static bool IsActive()
    {
        return Core.PerkActive(PerkId);
    }

    // 信誉扫地解除：每个势力好感达到一星（>=20）后，负面效果消失
    internal static bool IsCleared()
    {
        try
        {
            int total = 0, cleared = 0;
            StoreReputation[] factions = new StoreReputation[]
            {
                StoreReputation.GetSecFaction(),
                StoreReputation.GetRevFaction(),
                StoreReputation.GetBMFaction(),
                StoreReputation.GetULFaction()
            };
            foreach (StoreReputation fr in factions)
            {
                if (fr == null) continue;
                total++;
                int rep = 0;
                try { rep = fr.GetReputation(); } catch (Exception ex) { Core.LogMsg("[信誉扫地] 读取好感失败: " + ex.Message); }
                string fid = "";
                try { fid = fr.factionId ?? ""; } catch { }
                if (rep >= 20) cleared++;
            }
            bool all = total > 0 && cleared >= total;
            return all;
        }
        catch (Exception ex)
        {
            Core.LogMsg("[信誉扫地] 解除检查失败: " + ex.Message);
            return false;
        }
    }
}

// ============================================================
// 负面特性：治安部眼线
// 每15天治安部突击检查：全店（含暗格+海报夹层+货架表面）违禁品全没收（红色负面，Cost -15）
// 复用：AddictOfficerEvent.IsSmugglerBay / GetInnerInventory（已提 internal）、Core.LastNightReportLine、PerkStatePersistence
// ============================================================
internal sealed class DarkGridInspectorPerk : CustomStartingPerk
{
    internal const string PerkId = "治安部眼线";

    internal override string Id => PerkId;
    internal override string DisplayName => LangHelper.T("治安部眼线", "Security Informant");
    internal override string Description => LangHelper.T(
        "治安部的眼睛从未离开过你——你的一举一动都被记录在案。每15天他们上门突击检查，翻出暗格和海报夹层里的违禁品，一律没收，不限数量。满星信誉也拦不住他们。",
        "The Security Department's eyes never leave you - every move you make is on record. Every 15 days they raid your shop, uncovering contraband even in hidden compartments and behind posters - all confiscated, no limit. Full reputation won't stop them.");
    internal override int Cost => -15;   // 返还15点（最大负面，09-16 用户拍板）
    internal override int Type => 1;    // 负面特性显示为红色

    internal override void OnNewGame() { }

    internal static bool IsActive()
    {
        return Core.PerkActive(PerkId);
    }

    // ============ 每日调度（AddictOfficerEvent.OnDayStartPostfix 调用） ============
    // 09-20 设计稿：独立挂 StoreEventManager.OnDayStart Postfix（照吞噬/电池挂法，不依赖 AddictOfficerEvent 链）
    public static void OnDayStartPostfix()
    {
        try { OnNewDay(); } catch (System.Exception ex) { Core.LogMsg("[治安部眼线] OnDayStart失败: " + ex.Message); }
    }

    internal static new void OnNewDay()
    {
        try
        {
            if (!IsActive()) return;
            if (PlayerStore.Instance == null) return;
            int day = 1; try { day = StoreStation.GetDayCounter(); } catch { }
            if (day <= 0 || day % BuildConfig.InspectInterval != 0) return;
            if (day == PerkStatePersistence.GetInt("dark_grid_inspector", "last_trigger_day", -1)) return;
            PerkStatePersistence.SetInt("dark_grid_inspector", "last_trigger_day", day);
            RunInspection(day);
        }
        catch (Exception ex) { Core.LogMsg("[治安部眼线] 调度失败: " + ex.Message); }
    }

    // ============ 突击检查：全店（海报夹层+暗格内部+货架表面）违禁品全没收，不限件数 ============
    private static void RunInspection(int day)
    {
        var haul = new List<(GameItem item, GameInventory inv, int lvl)>();
        try
        {
            // 1. 海报/隐藏区（EmporiumEntry.hiddenElement）
            GameGridInventory hidden = EmporiumEntry.Instance.hiddenElement;
            if (hidden != null && hidden.childItems != null)
            {
                for (int i = 0; i < hidden.childItems.Count; i++)
                {
                    GameItem c = hidden.childItems[i];
                    if (c == null) continue;
                    try { int lvl = ContrabandHelper.GetContrabandLevel(c); if (lvl > 0) haul.Add((c, hidden, lvl)); } catch { }
                }
            }

            // 2. 容器/带舱机器内部违禁品（09-22 用户拍板"都翻"：暗格 smugger_bay + 蛙哥箱 CUSTOM_STORAGE_TAG +
            //    通用容器 CONTAINER_TAG + 带舱机器（养蛊机/AI生成器）——凡有内部库存一律翻，不限 smugger_bay）
            foreach (GameItem shopItem in EmporiumEntry.Instance.GetAllItems())
            {
                if (shopItem == null) continue;
                GameInventory inner = AddictOfficerEvent.GetInnerInventory(shopItem);
                if (inner == null || inner.childItems == null) continue;
                for (int i = 0; i < inner.childItems.Count; i++)
                {
                    GameItem c = inner.childItems[i];
                    if (c == null) continue;
                    // 去重：GetAllItems 递归返回容器内部物品时，同一件可能被翻两次
                    bool dup = false;
                    for (int d = 0; d < haul.Count; d++) { if (haul[d].item == c) { dup = true; break; } }
                    if (dup) continue;
                    try { int lvl = ContrabandHelper.GetContrabandLevel(c); if (lvl > 0) haul.Add((c, inner, lvl)); } catch { }
                }
            }

            // 3. 货架/柜台/展示柜/后背包表面直接违禁品（主要网格；暗格容器本身跳过——内部已在 2 处理）
            EmporiumEntry em = EmporiumEntry.Instance;
            if (em != null)
            {
                GameInventory[] surfaces = new GameInventory[]
                {
                    em.invElement as GameInventory,
                    em.backInvinvElement as GameInventory,
                    em.backInvinvElementCounter as GameInventory,
                    em.frontInvinvElement as GameInventory,
                    em.showcaseElement as GameInventory
                };
                foreach (GameInventory g in surfaces)
                {
                    if (g == null || g.childItems == null) continue;
                    for (int i = 0; i < g.childItems.Count; i++)
                    {
                        GameItem c = g.childItems[i];
                        if (c == null) continue;
                        if (AddictOfficerEvent.IsSmugglerBay(c)) continue;
                        try { int lvl = ContrabandHelper.GetContrabandLevel(c); if (lvl > 0) haul.Add((c, g, lvl)); } catch { }
                    }
                }
            }
        }
        catch (Exception ex) { Core.LogMsg("[治安部眼线] 扫描失败: " + ex.Message); }

        if (haul.Count == 0)
        {
            try { var _ps = PlayerStore.Instance; if (_ps != null) _ps.AddNightLog(LangHelper.T(
                "治安部突击检查！眼线把暗格和海报夹层翻了个底朝天，这次没搜到违禁品。",
                "Security raid! Informants tore through hidden compartments and behind posters - nothing found this time."), "#7FC97F"); } catch { } // ① 原生夜报（拆包：打开报告自动显示）
            Core.AddNightReportLine(LangHelper.T(
                "治安部突击检查！眼线把暗格和海报夹层翻了个底朝天，这次没搜到违禁品。",
                "Security raid! Informants tore through hidden compartments and behind posters - nothing found this time."));
            try { StoreUIManager.Instance.Notify(LangHelper.T("治安部突击检查！这次没搜到违禁品", "Security raid! Nothing found this time"), "yellow"); } catch { }
            return;
        }

        // 全没收（不限件数）
        int seized = 0;
        foreach (var s in haul)
        {
            try { s.inv.Expel(s.item); seized++; }
            catch (Exception ex) { Core.LogMsg("[治安部眼线] 没收失败: " + ex.Message); }
        }
        if (seized == 0) return;

        // 没收清单（最多列 5 种 + 其余计数）——写进第二天晨报/夜间报告
        var names = new List<string>();
        foreach (var s in haul)
        {
            string nm = "";
            try { nm = s.item.name ?? s.item.identifier ?? ""; } catch { }
            if (!string.IsNullOrEmpty(nm) && !names.Contains(nm)) names.Add(nm);
            if (names.Count >= 5) break;
        }
        string listStr = names.Count > 0 ? string.Join("、", names) : "";
        if (names.Count >= 5 && haul.Count > 5) listStr += " 等";

        try { var _ps = PlayerStore.Instance; if (_ps != null) _ps.AddNightLog(LangHelper.T(
            "治安部突击检查！眼线翻出了所有隐秘角落，没收了 " + seized + " 件违禁品" + (listStr.Length > 0 ? "：" + listStr : "") + "。",
            "Security raid! Informants confiscated " + seized + " contraband items" + (listStr.Length > 0 ? ": " + listStr : "") + " from hidden compartments."), "#7FC97F"); } catch { } // ① 原生夜报（09-22 统一柔和绿）
        Core.AddNightReportLine(LangHelper.T(
            "治安部突击检查！眼线翻出了所有隐秘角落，没收了 " + seized + " 件违禁品" + (listStr.Length > 0 ? "：" + listStr : "") + "。",
            "Security raid! Informants confiscated " + seized + " contraband items" + (listStr.Length > 0 ? ": " + listStr : "") + " from hidden compartments."));
        try { StoreUIManager.Instance.Notify(LangHelper.T("治安部突击检查！没收 " + seized + " 件违禁品", "Security raid! " + seized + " contraband confiscated"), "red"); } catch { }
    }
}

// ============================================================
// 中立特性：流浪者（Wanderer）
// 开局：现金 50%→0 / 50%→1~600 随机；6 件随机物品（1 工具 + 1 日用品 + 4 完全随机），替换原版发放
// 免费不占点、两难度都有（09-17 用户拍板：原生 id 池）
// ============================================================
internal sealed class WandererPerk : CustomStartingPerk
{
    internal const string PerkId = "流浪者";

    internal override string Id => PerkId;
    internal override string DisplayName => LangHelper.T("流浪者", "Wanderer");
    internal override string Description => LangHelper.T(
        "你两手空空地来到空间站——开局现金随机（可能一文不名，也可能小有积蓄），随身只有 6 件随机物品（必含 1 件工具 + 1 件日用品），原版开局物资不会给你。",
        "You arrive at the station empty-handed - starting cash is random (maybe nothing, maybe a little), and you carry only 6 random items (1 tool + 1 household item included). Original starting supplies are not given.");
    internal override int Cost => 0;   // 免费不占点
    internal override int Type => 2;   // 09-17 用户拍板：中立特性（黄色）

    internal override void OnNewGame() { }

    internal static bool IsActive() => Core.PerkActive(PerkId);

    // 09-17 拍板：原生 id 池（拆包给的工具/日用品）
    // 09-20 拍板：全新工具池（全去 magnifier/labeler/logo_checker/stamp_guide——4 样已清，不再发出）
    private static readonly string[] TOOL_IDS = { "screwdriver", "welder", "wire_cutter", "flashlight", "toolbox", "aquascan", "uv_filter", "metal_scanner", "aug_scanner", "black_lamp" };
    // 09-20 拍板：日用品池（cigarette_color 保留 + 12 扩充；cigarette_guide 去）
    private static readonly string[] HOUSEHOLD_IDS = { "cigarette_color", "shampoo", "toothpaste", "paper_towel", "toilet_paper", "box_tampon", "pack_condom", "packet_red_cigarette", "skincare_cream", "neuroactive_perfume", "pheromone_perfume", "salve", "rubbing_alcohol" };
    // 09-20 M1 拍板：随机池过滤文档/书/笔记/指南类（工具/日用品判定不变——仅全物品池过滤）
    private static readonly string[] DOCUMENT_IDS = {
        "tutorial_book", "wanted_paper", "joe_card",
        "cigarette_guide", "stamp_guide", "logo_checker",
        "mentor_note", "mentor_notes", "tutorial_note"
    };
    private static bool IsDocumentId(string id)
    {
        if (string.IsNullOrEmpty(id)) return true;
        string i = id.ToLowerInvariant();
        if (System.Array.IndexOf(DOCUMENT_IDS, i) >= 0) return true;
        return i.Contains("book") || i.Contains("guide") || i.Contains("paper")
            || i.Contains("note") || i.Contains("mentor");
    }

    // ============ PlayerStore.StartNewGame Postfix（09-17 流浪者：替换原版发放） ============
    public static void PostfixStartNewGame()
    {
        try
        {
            if (!IsActive()) return;
            var ps = Il2Cpp.PlayerStore.Instance;
            if (ps == null) return;
            // 1. 现金随机：50% → 0；50% → 1~600 均匀
            if (Core.Rng.Next(2) == 0) ps.playerCash = 0;
            else ps.playerCash = Core.Rng.Next(1, 601);
            // 09-21 拆包实锤：四件唯一发放点 = PlayerStore.HandleSkipIntro（EmporiumEntry.Start L7742，晚于本 Postfix）
            // 清除 + 6 件发放已迁移到 PostfixHandleSkipIntro（L7742 后 → InitialSave 不入档）
        }
        catch (Exception ex) { Core.LogMsg("[流浪者] PostfixStartNewGame 异常: " + ex.Message); }
    }

    // 09-21 拆包实锤：四件（magnifier/labeler/topical_bandage_item/fanny_pack）+ 指南类唯一发放点 = PlayerStore.HandleSkipIntro
    // 时序：L7706 StartNewGame → L7715 HandleInitialItem → L7726 newspaper → L7742 HandleSkipIntro → L7744 → L7751 InitialSave
    // 根因：旧 Postfix（StartNewGame/HandleInitialItem）都跑在 L7742 之前——清完被 HandleSkipIntro 补发，InitialSave 写档
    // 修复：HandleSkipIntro Postfix 清 invElement 全清（四件+指南）→ 重发 6 件——清完 InitialSave 不入档 ✓
    public static void PostfixHandleSkipIntro()
    {
        try
        {
            if (!IsActive()) return;
            ClearBackpack();
            GiveRandomItems();
        }
        catch (Exception ex) { Core.LogMsg("[流浪者] PostfixHandleSkipIntro 异常: " + ex.Message); }
    }

    // 09-20 B3：发 6 件（1 工具 + 1 日用品 + 4 随机）抽方法——PostfixStartNewGame / HandleInitialItemPostfix 兜底复用
    internal static void GiveRandomItems()
    {
        try
        {
            string tool = RandomFromPool(TOOL_IDS);
            string house = RandomFromPool(HOUSEHOLD_IDS);
            if (tool != null) GiveToBackpack(tool);
            if (house != null) GiveToBackpack(house);
            // 09-20 设计稿：4 随机从 FrogPowerPerk.ItemPool（77 项，无文档类/机器容器占比合理）抽，不重复
            var pool = new System.Collections.Generic.List<string>(FrogPowerPerk.ItemPool ?? new string[0]);
            int given = 0, guard = 0;
            while (given < 4 && pool.Count > 0 && guard < 20)
            {
                guard++;
                int idx = Core.Rng.Next(pool.Count);
                string id = pool[idx];
                pool.RemoveAt(idx);
                if (id == "topical_bandage_item" || id == "fanny_pack" || IsDocumentId(id)) continue; // 09-20 拍板：4 随机过滤绷带/腰包/文档（局部过滤，不动 ItemPool 本体）
                if (GiveToBackpack(id) != null) given++;
            }
        }
        catch { }
    }

    private static string RandomFromPool(string[] pool)
    {
        if (pool == null || pool.Length == 0) return null;
        var list = new System.Collections.Generic.List<string>(pool);
        while (list.Count > 0)
        {
            int idx = Core.Rng.Next(list.Count);
            string id = list[idx];
            list.RemoveAt(idx);
            try { var _it = DirectoryMaster.Item(id); if (_it != null) return id; } catch { } // 09-20 拆包：Has 只查已初始化字典（Tool/Amenities/Misc 懒加载未初始化→false 漏判）；Item 触发目录初始化
        }
        return null;
    }

    private static System.Collections.Generic.List<string> GetAllItemIds()
    {
        var ids = new System.Collections.Generic.List<string>();
        try
        {
            var list = DirectoryMaster.GetIdentifierList<GameItem>(null);
            if (list != null)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    string id = list[i];
                    if (string.IsNullOrEmpty(id)) continue;
                    if (System.Array.IndexOf(TOOL_IDS, id) >= 0) continue;
                    if (System.Array.IndexOf(HOUSEHOLD_IDS, id) >= 0) continue;
                    if (IsDocumentId(id)) continue; // 09-20 M1：随机池排除文档/书/笔记/指南类
                    if (System.Array.IndexOf(Core.ExcludedItemIds, id) >= 0) continue; // 09-22：全局黑名单（rare_electronic 等）不进任何我们的池子
                    ids.Add(id);
                }
            }
        }
        catch { }
        return ids;
    }

    private static string SafeId(GameItem it)
    {
        try { if (it == null) return "null"; var s = it.identifier; if (!string.IsNullOrEmpty(s)) return s; } catch { }
        try { var n = it.name; if (!string.IsNullOrEmpty(n)) return n; } catch { }
        return "?";
    }

    private static GameItem GiveToBackpack(string id)
    {
        try
        {
            GameItem item = DirectoryMaster.Item(id, true); // 09-20 拆包：Item 触发目录懒加载（Has 只查已初始化字典）
            if (item == null) return null;
            var em = EmporiumEntry.Instance;
            if (em == null || em.invElement == null) return null; // 09-20 用户拍板：流浪者 6 件发桌面（invElement，与原生四件同位置）；原发背包
            var slot = em.invElement.TryFindOneValidInventorySlot(item, false);
            if (slot != null) { try { slot.TryAcceptOnce(); return item; } catch { } }
            ((GameInventory)em.invElement).UncheckedAccept(item);
            return item;
        }
        catch { return null; }
    }

    // 09-22 精确黑名单：只清原版开局四件 + 文档/指南类，其他 mod 赠品保留（误伤根因修复）
    private static bool ShouldClear(GameItem it)
    {
        try
        {
            var id = it.identifier;
            if (string.IsNullOrEmpty(id)) return false;
            if (id == "magnifier" || id == "labeler" || id == "topical_bandage_item" || id == "fanny_pack") return true;
            if (System.Array.IndexOf(DOCUMENT_IDS, id) >= 0) return true;
            return false;
        }
        catch { return false; }
    }

    // 09-20 B3 修复：清三处（dossier 0x178 / invElement 0x30 / backInvinvElement 0x98）全清——原版物品+其他特性物资兜底清除
    // 09-20 设计稿双保险：ExpelAll（批量 RemoveAll）→ 残留逐个 Expel（parent 检查不过的失败）→ 残留 Destroy 兜底
    // 09-22 精确黑名单：只清四件+文档/指南（ShouldClear），其他 mod 赠品保留——不再全量 Clear
    internal static void ClearBackpack()
    {
        try
        {
            var em = EmporiumEntry.Instance;
            if (em == null) return;
            var invs = new System.Collections.Generic.List<GameInventory>();
            try { var i = em.invElement as GameInventory; if (i != null) invs.Add(i); } catch { }
            try { var b = em.backInvinvElement as GameInventory; if (b != null) invs.Add(b); } catch { }
            // dossier（0x178 档案夹容器，GameItem 类型）——拆包实锤公开属性 em.dossier；内部网格走 contentWindow.inventory（AddictOfficerEvent.GetInnerInventory 先例）
            try
            {
                var d = em.dossier;
                if (d != null)
                {
                    var di = AddictOfficerEvent.GetInnerInventory(d);
                    if (di != null) invs.Add(di);
                }
            }
            catch { }
            foreach (var inv in invs)
            {
                if (inv == null || inv.childItems == null) { continue; }
                // 09-22 精确黑名单：目标（四件/文档）Destroy+移除；非目标（其他 mod 赠品）保留——不再全量 Clear 防误清
                for (int di = inv.childItems.Count - 1; di >= 0; di--)
                {
                    try
                    {
                        var dit = inv.childItems[di];
                        if (dit == null) { try { inv.childItems.RemoveAt(di); } catch { } continue; }
                        if (!ShouldClear(dit)) continue;
                        try { dit.Destroy(); } catch { }
                        try { inv.childItems.RemoveAt(di); } catch { }
                    }
                    catch { }
                }
            }
        }
        catch { }
    }


}

// ============================================================
// 伙伴型特性：蛙娘（09-23 Perk 化——Cost 3 用户拍板）
// 喂食/照顾提升六维与好感；在场客户预算×4、议价+50；销赃；偷钱/跑路
// ============================================================
internal sealed class WageGirlPerk : CustomStartingPerk
{
    internal const string PerkId = "蛙娘";

    internal override string Id => PerkId;
    internal override string DisplayName => LangHelper.T("蛙娘", "Wage Girl");
    internal override string Description => LangHelper.T("伙伴型特性（3点）。蛙哥留下的仿生女仆实体：喂食/照顾提升她的饱食、口渴、健康、心情、清洁、睡眠六维。她在店时客户预算×4、议价成功率+50%；喂她违禁品可点面板「销赃」外出两天带回干净货。但心情差会偷你的钱和货，连续不照顾会跑路14天。收益与风险并存。", "Partner perk (3 points). A biomimetic maid left by Wage: feed & care raise her 6 stats. While present, customer budget x4 & bargain +50 percent; feed her contraband then Fence to bring back clean goods in 2 days. But bad mood makes her steal your money and goods, and neglect makes her leave for 14 days. High reward, real risk.");
    internal override int Cost => 3; // 09-23 用户拍板：综合考量 3 点
    internal override int Type => 0; // 正面特性（收益为主，偷钱为伴随代价）

    internal override void OnNewGame()
    {
    }

    internal static bool IsActive()
    {
        return Core.PerkActive(PerkId);
    }
}
