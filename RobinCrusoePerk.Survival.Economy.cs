using System;
using HarmonyLib;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Il2Cpp;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace JacksonPerks;
internal static partial class RobinCrusoePerk
{

    // 爆发辅助：收入惩罚（按当日营业额 % 扣款，空营业额日=0；营业额=RecordRevenue 当日累计）
    internal static int ApplyIncomePunish(int pct)
    {
        try
        {
            int revenue = WageSaveStore.GetInt(PERK_ID, "revenue", 0);
            if (revenue <= 0 || pct <= 0) return 0;
            int amt = (int)(revenue * pct / 100.0);
            var ps = Il2Cpp.PlayerStore.Instance;
            if (ps != null && amt > 0)
            {
                int cash = Math.Max(0, ps.playerCash - amt);
                ps.playerCash = cash;
                WageSaveStore.SetInt(PERK_ID, "revenue", 0);
                return amt;
            }
        }
        catch (System.Exception ex) { Core.LogMsg("[RobinCrusoePerk.Survival] 异常: " + ex.Message); }
        return 0;
    }

    // 爆发辅助：损失 1 件小货物（白名单：食物/水（杂货主体）；不损工具/机器/容器/钥匙卡；AddictOfficer 同款 GetAllItems 遍历）
    internal static int LostSmallItem(int count)
    {
        try
        {
            var em = Il2Cpp.EmporiumEntry.Instance;
            if (em == null) return 0;
            var all = em.GetAllItems();
            if (all == null) return 0;
            int lost = 0;
            foreach (var it in all)
            {
                if (lost >= count) break;
                if (it == null) continue;
                string id = GetId(it);
                if (IsToolOrKeyOrContainer(it)) continue;       // 不损工具/钥匙/容器
                if (!IsFood(it) && !IsDrink(it)) continue;      // 白名单：食物/水
                try
                {
                    var inv = FindContainingInventory(it);
                    if (inv != null) { inv.Expel(it); lost++;  }
                }
                catch (System.Exception ex) { Core.LogMsg("[RobinCrusoePerk.Survival] 异常: " + ex.Message); }
            }
            return lost;
        }
        catch { return 0; }
    }

    // 爆发辅助：损失 1 件水/容器（嗓子冒烟爆发：打烊失手）
    private static void LostWaterItem()
    {
        try
        {
            var em = Il2Cpp.EmporiumEntry.Instance;
            if (em == null) return;
            var all = em.GetAllItems();
            if (all == null) return;
            foreach (var it in all)
            {
                if (it == null) continue;
                if (!IsDrink(it)) continue;
                try
                {
                    var inv = FindContainingInventory(it);
                    if (inv != null) { inv.Expel(it);  return; }
                }
                catch (System.Exception ex) { Core.LogMsg("[RobinCrusoePerk.Survival] 异常: " + ex.Message); }
            }
        }
        catch (System.Exception ex) { Core.LogMsg("[RobinCrusoePerk.Survival] 异常: " + ex.Message); }
    }

    // 找包含指定物品的库存（遍历店铺各库存容器 childItems）
    private static GameInventory FindContainingInventory(GameItem target)
    {
        try
        {
            var em = Il2Cpp.EmporiumEntry.Instance;
            if (em == null) return null;
            var invs = new Il2CppSystem.Collections.Generic.List<GameInventory>();
            try
            {
                if (em.frontInvinvElement != null) invs.Add(em.frontInvinvElement);
                if (em.backInvinvElement != null) invs.Add(em.backInvinvElement);
                if (em.hiddenElement != null) invs.Add(em.hiddenElement);
                if (em.afterhourInventory != null) invs.Add(em.afterhourInventory);
            }
            catch (System.Exception ex) { Core.LogMsg("[RobinCrusoePerk.Survival] 异常: " + ex.Message); }
            foreach (var inv in invs)
            {
                try
                {
                    if (inv == null || inv.childItems == null) continue;
                    for (int i = 0; i < inv.childItems.Count; i++)
                        if (inv.childItems[i] == target) return inv;
                }
                catch (System.Exception ex) { Core.LogMsg("[RobinCrusoePerk.Survival] 异常: " + ex.Message); }
            }
        }
        catch (System.Exception ex) { Core.LogMsg("[RobinCrusoePerk.Survival] 异常: " + ex.Message); }
        return null;
    }

    private static bool IsToolOrKeyOrContainer(GameItem item)
    {
        try
        {
            string id = GetId(item);
            if (item.IsTag("IMPORTANT_TAG")) return true;
            if (item.IsTag("MACHINE")) return true;
            if (item.IsTag("LOCKED")) return true;
            if (id != null && (id.Contains("storage") || id.Contains("key") || id.Contains("tool") || id.Contains("machine") || id.Contains("module"))) return true;
            return false;
        }
        catch { return false; }
    }

    private static bool IsGroceries(GameItem item)
    {
        try { return item.IsTag("GROCERY"); } catch { return false; }
    }

    private static bool IsRaw(GameItem item)
    {
        try { return item.IsTag("RAW"); } catch { return false; }
    }

    // 当日营业额累计（PostfixStoreClientOnDealAccepted BUY 分支调用；打烊收入惩罚后清零）
    internal static void RecordRevenue(int amount)
    {
        if (amount <= 0) return;
        WageSaveStore.SetInt(PERK_ID, "revenue", WageSaveStore.GetInt(PERK_ID, "revenue", 0) + amount);
    }

    internal static int GetTodayRevenue() => WageSaveStore.GetInt(PERK_ID, "revenue", 0);

    // 判定：物品是否在博士夜晚商店库存（afterhourInventory）里——没买不能吃（用户反馈修复）
    private static bool IsInDoctorNightInventory(GameItem item)
    {
        try
        {
            var em = Il2Cpp.EmporiumEntry.Instance;
            if (em == null || em.afterhourInventory == null || item == null) return false;
            long ptr = (long)item.Pointer;
            if (em.afterhourInventory.childItems != null)
            {
                for (int i = 0; i < em.afterhourInventory.childItems.Count; i++)
                {
                    var it = em.afterhourInventory.childItems[i];
                    if (it != null && (long)it.Pointer == ptr) return true;
                }
            }
        }
        catch (System.Exception ex) { Core.LogMsg("[RobinCrusoePerk.Survival] 异常: " + ex.Message); }
        return false;
    }

    // 基础价值（口径稳定：unitBaseValue；失败退 GetCurrentValue）
    private static int GetItemBaseValue(GameItem item)
    {
        try { return (int)item.unitBaseValue; } catch { }
        try { return (int)item.GetCurrentValue(); } catch { }
        return 1;
    }

}
