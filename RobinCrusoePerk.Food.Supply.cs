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

    internal static void TryDoctorSupply()
    {
        try
        {
            int day = DeterministicSchedule.CurrentDay;
            if (day == _doctorSupplyDay) return; // 同日不重复
            _doctorSupplyDay = day;
            // 第 10 天卖养蛊机 / 第 30 天卖生成器 + 保护器（新物品注册后追加）
            if (day >= 30)
            {
                string[] cheap = { "system_module_overclock", "system_module_ruined", "system_module_corrupt" };
                int n = Core.Rng.Next(BuildConfig.DoctorSupplyCountMin, BuildConfig.DoctorSupplyCountMax + 1);
                int added = 0;
                for (int i = 0; i < n; i++)
                {
                    try
                    {
                        GameItem m = DirectoryMaster.Item(cheap[Core.Rng.Next(cheap.Length)], true);
                        if (m == null) continue;
                        long v = 0; try { v = m.GetValue(); } catch { }
                        int price = (int)(v * BuildConfig.DoctorSupplyPricePct / 100);
                        MerchantHelper.AddItemToCounter(m, price, false);
                        added++;
                    }
                    catch { }
                }
                Core.LogMsg("[养蛊机] 博士廉价模组供货 " + added + " 件（day " + day + "）");
            }
            // 养蛊机系统：博士夜晚商店卖新物品（防堆叠——柜台无同 id 才补）
            // 第 10 天起：养蛊机（wage_gu_machine，2000）——每天到访都补 1 个（柜台无则补）
            if (day >= 10 && !HasGoodOnFront("wage_gu_machine"))
            {
                try { if (MerchantHelper.AddItemToCounter("wage_gu_machine", 0, false) != null) Core.LogMsg("[养蛊机] 博士夜晚商店卖养蛊机（day " + day + "）"); } catch { }
            }
            // 第 30 天起：AI 生成器（wage_ai_generator，1500）——柜台无则补
            if (day >= 30 && !HasGoodOnFront("wage_ai_generator"))
            {
                try { if (MerchantHelper.AddItemToCounter("wage_ai_generator", 0, false) != null) Core.LogMsg("[养蛊机] 博士夜晚商店卖生成器（day " + day + "）"); } catch { }
            }
            // 30 天起：保护器核心 3 个（wage_protector_core，1500）——每次到访补足 3 个
            if (day >= 30)
            {
                int pc = 0;
                try { pc = CountGoodOnFront("wage_protector_core"); } catch { }
                for (int pi = pc; pi < BuildConfig.ProtectorSupplyCount; pi++)
                {
                    try { if (MerchantHelper.AddItemToCounter("wage_protector_core", 0, false) != null) Core.LogMsg("[养蛊机] 博士夜晚商店卖保护器（day " + day + "）"); } catch { }
                }
            }
        }
        catch (Exception ex) { Core.LogMsg("[养蛊机] TryDoctorSupply 异常: " + ex.Message); }
    }

    public static bool PrefixAutoSipFromContainer(GameItem item, GameCharacterItem GCI)
    {
        try
        {
            if (!IsActive() || item == null || GCI == null) return true;
            int ml = GetWaterMl(item);
            if (ml <= 0) return true;
            int purity = -1;
            try { purity = Il2Cpp.WaterHelper.GetWaterPurity(item); } catch { }
            int tier = purity >= 9900 ? 0 : purity >= 9600 ? 1 : purity >= 9200 ? 2 : purity >= 8800 ? 3 : 4;
            int hd  = new[] { 5, 2, 0, -5, -10 }[tier];
            int inf = new[] { 0, 0, 5, 15, 30 }[tier];
            int cg  = new[] { 5, 4, 3, 1, 0 }[tier];
            if (hd != 0) SetHealth(Math.Max(0, Math.Min(100, GetHealth() + hd)));
            if (cg > 0) SetClean(Math.Min(100, GetClean() + cg));
            if (inf > 0) TryInfect(inf / 100.0);
            // 补原版 AutoSipFromContainer（return false 后原版不执行）：sip = min(ml, 缺口渴量)；GCI.thirst += sip；Remove(item, sip)
            try
            {
                int cur = 0, mx = 0;
                try { cur = (int)GCI.currentThirst; } catch { }
                try { mx = (int)GCI.maxThirst; } catch { }
                int need = Math.Max(0, mx - cur);
                int sip = Math.Min(ml, need);
                if (sip > 0)
                {
                    try { GCI.currentThirst = cur + sip; } catch { }
                    try { Il2Cpp.WaterHelper.Remove(item, sip * 1000); } catch { } // 09-11 定案：µl 单位
                }
            }
            catch { }
            return false; // 拦原版：水质挂钩已由 mod 接管
        }
        catch { return true; }
    }

    private static void ForageIndoor(int count, string tag)
    {
        try
        {
            for (int i = 0; i < count; i++)
            {
                string f = FORAGE_FOODS[UnityEngine.Random.Range(0, FORAGE_FOODS.Length)];
                if (DirectoryMaster.Has<GameItem>(f)) GiveToBackpack(f, 1);
            }
            try { StoreUIManager.Instance.Notify(LangHelper.T(tag + "：翻出食物 ×" + count, tag + ": found food x" + count), "green"); } catch { }
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] ForageIndoor 异常: " + ex.Message); }
    }

    private static bool TryExpel(GameItem item)
    {
        try
        {
            EmporiumEntry em = EmporiumEntry.Instance;
            if (em == null || item == null) return false;
            IntPtr targetPtr = item.Pointer;
            if (targetPtr == IntPtr.Zero) return false;
            // 1) 主背包 + 柜台（Pointer 比较，Il2Cpp 包装安全）
            foreach (GameInventory inv in new[] { (GameInventory)em.backInvinvElement, (GameInventory)em.frontInvinvElement })
            {
                if (inv == null || inv.childItems == null) continue;
                for (int i = 0; i < inv.childItems.Count; i++)
                {
                    var ci = inv.childItems[i];
                    if (ci != null && ci.Pointer == targetPtr) { inv.Expel(item); return true; }
                }
            }
            // 2) 递归容器内容库存（物品可能放在容器 UI 里双击食用）
            var visited = new HashSet<IntPtr>();
            var stack = new Stack<GameItem>();
            foreach (GameInventory inv in new[] { (GameInventory)em.backInvinvElement, (GameInventory)em.frontInvinvElement })
            {
                if (inv == null || inv.childItems == null) continue;
                for (int i = 0; i < inv.childItems.Count; i++)
                    if (inv.childItems[i] != null) stack.Push(inv.childItems[i]);
            }
            while (stack.Count > 0)
            {
                var c = stack.Pop();
                if (c == null || !visited.Add(c.Pointer)) continue;
                try
                {
                    var w = c.contentWindow;
                    if (w == null) continue;
                    var gi = (w.childElement != null) ? w.childElement.Cast<GameGridInventory>() : null;
                    if (gi == null || gi.childItems == null) continue;
                    for (int i = 0; i < gi.childItems.Count; i++)
                    {
                        var ci = gi.childItems[i];
                        if (ci == null) continue;
                        if (ci.Pointer == targetPtr) { gi.Expel(item); return true; }
                        stack.Push(ci);
                    }
                }
                catch { }
            }
            // 3) 兜底：原生销毁（吃完/喝完/用完=销毁，TryDestroyAll 签名为 List<GameItem>，MoreUpdate 拆包确认）
            try
            {
                var lst = new Il2CppSystem.Collections.Generic.List<GameItem>();
                lst.Add(item);
                GraphUtils.TryDestroyAll(lst);
                return true;
            }
            catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] TryDestroyAll 异常: " + ex.Message); }
            return false;
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] TryExpel 异常: " + ex.Message); return false; }
    }

    private static List<GameItem> CollectAllItems()
    {
        var result = new List<GameItem>();
        try
        {
            EmporiumEntry em = EmporiumEntry.Instance;
            if (em == null) return result;

            foreach (GameItem item in em.GetAllItems())
                if (item != null) result.Add(item);

            foreach (GameInventory inv in new[] { em.backInvinvElement, em.frontInvinvElement })
            {
                if (inv == null || inv.childItems == null) continue;
                for (int i = 0; i < inv.childItems.Count; i++)
                    if (inv.childItems[i] != null) result.Add(inv.childItems[i]);
            }

            var inner = new List<GameItem>(result);
            foreach (GameItem container in inner)
            {
                if (container == null) continue;
                try
                {
                    var w = container.contentWindow;
                    if (w == null) continue;
                    var gi = (w.childElement != null) ? w.childElement.Cast<GameGridInventory>() : null;
                    if (gi == null || gi.childItems == null) continue;
                    for (int i = 0; i < gi.childItems.Count; i++)
                        if (gi.childItems[i] != null) result.Add(gi.childItems[i]);
                }
                catch { }
            }
        }
        catch { }
        return result;
    }

    private static string GetId(GameItem item)
    {
        try { return (item.identifier ?? "").ToLowerInvariant(); }
        catch { return ""; }
    }

}
