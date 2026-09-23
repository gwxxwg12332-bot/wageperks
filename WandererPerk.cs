using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace JacksonPerks;

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
            // 声名狼藉特性放行：不覆盖 playerCash
            if (!Core.PerkActive("声名狼藉")) {
                if (Core.Rng.Next(2) == 0) ps.playerCash = 0;
                else ps.playerCash = Core.Rng.Next(1, 601);
            }
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
            // 09-20 设计稿：4 随机从 WagePowerPerk.ItemPool（77 项，无文档类/机器容器占比合理）抽，不重复
            var pool = new System.Collections.Generic.List<string>(WagePowerPerk.ItemPool ?? new string[0]);
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
        catch (System.Exception ex) { Core.LogMsg("[NewTraits] 异常: " + ex.Message); }
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
        catch (System.Exception ex) { Core.LogMsg("[NewTraits] 异常: " + ex.Message); }
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
            catch (System.Exception ex) { Core.LogMsg("[NewTraits] 异常: " + ex.Message); }
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
                    catch (System.Exception ex) { Core.LogMsg("[NewTraits] 异常: " + ex.Message); }
                }
            }
        }
        catch (System.Exception ex) { Core.LogMsg("[NewTraits] 异常: " + ex.Message); }
    }


}
