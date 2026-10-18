using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace WagePerks;

public static partial class WageGirlSystem
{
    public static void RegisterToDirectory(ItemDirectory dir)
    {
        try
        {
            if (dir == null) return;
            ((Directory<GameItem>)(object)dir).Add(ENTITY_ID, (Il2CppSystem.Func<GameItem>)(() => CreateWageGirl()));
        }
        catch (Exception ex) { Core.LogMsg("[蛙娘] 注册失败: " + ex.Message); }
    }
    private static GameItem CreateWageGirl()
    {
        try
        {
            GameItem it = ItemDirectory.CreateEmptyItem(null);
            if (it == null) return null;
            ApplyIcon(it);
            it.EnableTag(TAG);
            it.SetName(LangHelper.T("蛙娘", "Wage Girl"));
            it.identifier = ENTITY_ID; // 公开 setter（照骰子先例）——identifier 随档
            it.identifierName = "TYPE-STRING_" + ENTITY_ID; // 公开 setter
            it.shortDescription = LangHelper.T(
                "蛙娘——蛙哥留下的仿生女仆。\n" +
                "她会饿、会渴、会累、会闹脾气；饿、渴或心情差时会顺走货架上的食物、饮品和日用品，每 5 天还会偷一次钱（好感越高偷得越少，好感满 100 后不再偷）。心情大好时，她会把之前偷的都还回来。\n" +
                "好好照顾她，她就越来越向着你：在场时帮你抬价、销赃，好感够了还会替你打烊后整理货架、捡漏、代售柜台货，甚至代交客户的收购单。\n" +
                "连续几天没吃好没睡好，她会离家出走 14 天。\n" +
                "双击打开她的状态面板。",
                "Wage Girl — an android maid left behind by Wage.\n" +
                "She gets hungry, thirsty, tired, and moody; when she's starving, parched, or in a foul mood she'll quietly take food, drinks, and daily goods off your shelves — and every 5 days she steals some credits (the higher her affection, the less she takes; once affection hits 100 she stops entirely). In a great mood, she'll even return everything she stole.\n" +
                "Care for her and she turns to your side: she haggles and fences for you while in the store, and once she trusts you she'll tidy the shelves, salvage finds, auto-sell counter goods, and even fill customer orders after closing.\n" +
                "Neglect her for several days running and she'll run away for 14 days.\n" +
                "Double-click to open her status panel."); // 10-16 文案重设计 v2 drop-in：删"叫客"（未实现），偷窃机制按实测校正（每5天偷钱/好感满100不偷/心情好归还）
            it.longDescription = it.shortDescription;
            it.unitValue = 0; it.unitBaseValue = 0; // 09-19 价值归零：客户不买
            Core.LogMsg("[克隆核对] WageGirlEntity: id=" + (it.identifier ?? "NULL") + " val=" + it.unitValue); // 10-18 总控方案①取证（identifierName 无 get 只写，不读）
            return it;
        }
        catch (Exception ex) { Core.LogMsg("[蛙娘] 创建失败: " + ex.Message); return null; }
    }
    private static void ApplyIcon(GameItem it)
    {
        try { it.SetSpriteAndShape(ICON_ATLAS, ICON); }
        catch (System.Exception ex) { Core.LogMsg("[WageGirlSystem.Entity] 异常: " + ex.Message); }
    }
    private static Sprite SpriteFromPixels(Color[] pixels, int w, int h)
    {
        try
        {
            // 10-07 修复：统一走 WagePixelSprites.CreatePixelSprite（SetPixels 行序 Y 翻转）
            return WagePixelSprites.CreatePixelSprite(pixels, w, h, 100f);
        }
        catch { return null; }
    }
    private static void SetField(GameItem it, string field, object val)
    {
        try
        {
            var f = typeof(GameItem).GetField(field,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (f != null) f.SetValue(it, val);
        }
        catch (System.Exception ex) { Core.LogMsg("[WageGirlSystem.Entity] 异常: " + ex.Message); }
    }
    private static bool ExistsInScene()
    {
        try
        {
            EmporiumEntry em = EmporiumEntry.Instance;
            if (em == null) return false;
            // 09-23 性能：原先每次调用都 new GameInventory[5] + foreach——本方法在热路径上（见 ExistsCached），
            // 每秒几十次分配纯属 GC 压力。改为 5 次直调，语义完全一致（短路顺序也保持原样）。
            if (FindGirlInInv((GameInventory)em.invElement)) return true;
            if (FindGirlInInv((GameInventory)em.backInvinvElement)) return true;
            if (FindGirlInInv((GameInventory)em.backInvinvElementCounter)) return true;
            if (FindGirlInInv((GameInventory)em.frontInvinvElement)) return true;
            if (FindGirlInInv((GameInventory)em.showcaseElement)) return true;
            if (FindGirlInInv((GameInventory)em.hiddenElement)) return true; // 09-23 补：海报后暗格
            return false;
        }
        catch { return false; }
    }
    private static bool FindGirlInInv(GameInventory inv)
    {
        if (inv == null || inv.childItems == null) return false;
        for (int i = 0; i < inv.childItems.Count; i++)
        {
            try
            {
                var it = inv.childItems[i];
                if (it == null) continue;
                if (it.identifier == ENTITY_ID) return true;
                if (it.contentWindow != null)
                {
                    var inner = AddictOfficerEvent.GetInnerInventory(it);
                    if (inner != null && FindGirlInInv(inner)) return true;
                }
            }
            catch (System.Exception ex) { Core.LogMsg("[WageGirlSystem.Entity] 异常: " + ex.Message); }
        }
        return false;
    }
    // 10-15 B2 购买蛙娘卡 → 立即发放实体（防重由 TryGiveToBackpack 内部 ExistsInScene 保证）
    internal static void GiveEntityNow()
    {
        try { TryGiveToBackpack(); } catch (System.Exception ex) { Core.LogMsg("[WageGirlSystem] GiveEntityNow 异常: " + ex.Message); }
    }

    private static void TryGiveToBackpack()
    {
        try
        {
            EmporiumEntry em = EmporiumEntry.Instance;
            if (em == null) { Core.LogMsg("[蛙诊] GiveToBackpack: em null"); return; }
            if (em.backInvinvElement == null) { Core.LogMsg("[蛙诊] GiveToBackpack: backInv null"); return; }
            // 09-19 发放前全范围查重（5 网格+容器内部已有 → 不重复发）
            if (ExistsInScene()) { Core.LogMsg("[蛙诊] GiveToBackpack: ExistsInScene true"); return; }
            var inv = (GameInventory)em.backInvinvElement;
            GameItem item = DirectoryMaster.Item(ENTITY_ID, true);
            if (item == null) { Core.LogMsg("[蛙诊] GiveToBackpack: item null"); return; }
            // 照 GiveToBackpack 先例：TryFindOneValidInventorySlot → TryAcceptOnce（防同格重叠）；失败 UncheckedAccept 兜底
            var slot = em.backInvinvElement.TryFindOneValidInventorySlot(item, false);
            if (slot != null) { try { slot.TryAcceptOnce(); return; } catch (Exception) { } }
            try { var l = new Il2CppSystem.Collections.Generic.List<GameItem>(); l.Add(item); inv.UncheckedAcceptAll(l); } catch { }
            Core.LogMsg("[蛙娘] 已发放实体到背包（全局常驻）");
        _returnTimer = 0.5f; _curState = ""; // 强制播return帧0.5s再切idle
        }
        catch (Exception ex) { Core.LogMsg("[蛙娘] 发放失败: " + ex.Message); }
    }
    private static GameItem FindGirlItem()
    {
        try
        {
            var em = EmporiumEntry.Instance;
            if (em == null) return null;
            // 10-05 修复：旧实现硬编码4容器（漏 backInvCounter/hiddenElement + 不递归容器内部）→ 蛙娘放暗格/箱内找不到。
            // 10-18 审阅 P1 修正注释：GetAllItems 只扫当铺顶层（不递归容器内部）——改用 CollectAllItems（顶层+容器递归，深度8防环）
            foreach (var it in WageBrother.CollectAllItems())
            {
                if (it != null && it.identifier == ENTITY_ID) return it;
            }
        }
        catch (System.Exception ex) { Core.LogMsg("[WageGirlSystem.Entity] 异常: " + ex.Message); }
        return null;
    }
}
