using System;
using System.Reflection;
using Il2Cpp;
using UnityEngine;

namespace WageBlackGold;
/// <summary>
/// 黑金典藏箱样式链：博士夜晚上架 / Shift+双击循环切换 / 样式持久化（WageSaveStore ns="BlackGold"）/ 读档恢复。
/// </summary>
public static class BlackGoldBoxStyle
{
    // ===== 博士夜晚商店上架 =====
    // 10-08 实测修正：原实现走 afterhourInventory+UncheckedAccept——但 TestRunner.cs:1058 铁证 afterhourInventory=外出拾荒背包（非博士柜台），
    // 加错库存（玩家看不到博士卖黑金箱）。对齐鲁滨逊先例（RobinCrusoePerk.AddDoctorNightGoods→MerchantHelper.AddItemToCounter→
    // PlayerStore.AddDirectSellingItemToTable 柜台链，Patches.Npc.Client.cs:279）改走上架柜台链。
    private static long _lastStockMs = 0; // 10-08 帧级防重：双挂点（ShowAfterhourInv+PlaceInventorInventory）同帧双双通过 HasBoxOnCounter（AddDirectSellingItemToTable 后 items 未立即更新）→ 2 秒内只上架一次
    private static bool AddToDoctorCounter()
    {
        try
        {
            long now = Environment.TickCount64;
            if (now - _lastStockMs < 2000) { Core.LogMsg("[黑金典藏箱] 博士上架防重跳过（2 秒窗口）"); return true; }
            PlayerStore ps = PlayerStore.Instance;
            if (ps == null) { Core.LogMsg("[黑金典藏箱] 博士上架跳过：PlayerStore 未就绪"); return false; }
            if (HasBoxOnCounter()) { Core.LogMsg("[黑金典藏箱] 博士上架防重跳过（柜台已有同 id）"); return true; }
            GameItem box = BlackGoldBox.CreateContainer();
            if (box == null) return false;
            ps.AddDirectSellingItemToTable(box, false, false, false, 0);
            _lastStockMs = now;
            Core.LogMsg("[黑金典藏箱] 博士夜晚柜台已上架黑金箱（价格 2000）");
            return true;
        }
        catch (Exception ex) { Core.LogMsg("[黑金典藏箱] AddToDoctorCounter: " + ex.Message); return false; }
    }

    public static void PostfixShowAfterhourInv(EmporiumEntry __instance)
    {
        AddToDoctorCounter();
    }

    // 10-08 对齐先例触发链：StoreClientList.PlaceInventorInventory（博士到访上货入口——Patches.Npc.Client.cs:279）
    public static void PostfixPlaceInventorInventory(bool isVisitingPlayerStore)
    {
        AddToDoctorCounter();
    }

    private static bool HasBoxOnCounter()
    {
        try
        {
            EmporiumEntry em = EmporiumEntry.Instance;
            if (em == null || em.frontInvinvElement == null || em.frontInvinvElement.items == null) return false;
            foreach (var it in em.frontInvinvElement.items)
            {
                if (it != null && string.Equals(it.identifier, BlackGoldBox.BOX_ID, StringComparison.Ordinal)) return true;
            }
        }
        catch { }
        return false;
    }

    private static bool HasBox(GameInventory inv)
    {
        try
        {
            if (inv == null || inv.childItems == null) return false;
            for (int i = 0; i < inv.childItems.Count; i++)
            {
                var it = inv.childItems[i];
                if (it == null) continue;
                try
                {
                    if (string.Equals(it.identifier, BlackGoldBox.BOX_ID, StringComparison.Ordinal)) return true;
                    // 读档后 identifier 丢失兜底：有内容窗口的容器近似判定（afterhourInventory 里 contentWindow 非空的容器≈黑金箱）
                    try { if (it.contentWindow != null) return true; } catch { }
                }
                catch { }
            }
        }
        catch { }
        return false;
    }

    // ===== 原版菜单"切换"按钮复用（拆包 AI 返工实锤：TriggerButton ISIL :3956=Call GameItem.ToggleSlotItem）=====
    // 10-08 用户拍板：参考原版箱子左键切换样式，放弃 Shift+双击（自研链已退役）。
    // 黑金箱：CanToggleSlotItem→true（菜单显示"切换"按钮）+ ToggleSlotItem→NextStyle（拦原版槽切换）；
    // 其他物品（妙妙箱/原版箱）return true 原样放行。
    public static bool PrefixCanToggleSlotItem(GameItem __instance, ref bool __result)
    {
        try
        {
            if (__instance == null) return true;
            Core.LogMsg("[黑金典藏箱] 进入 PrefixCanToggleSlotItem id=" + (__instance.identifier ?? "NULL")); // 10-08 二分：detour 是否挂
            if (IsBlackGold(__instance)) { __result = true; return false; }
        }
        catch { }
        return true;
    }

    public static bool PrefixToggleSlotItem(GameItem __instance)
    {
        try
        {
            if (__instance == null) return true;
            Core.LogMsg("[黑金典藏箱] 进入 PrefixToggleSlotItem id=" + (__instance.identifier ?? "NULL")); // 10-08 二分：detour 是否挂
            if (IsBlackGold(__instance))
            {
                NextStyle(__instance);
                return false; // 拦掉原版槽切换（黑金无槽语义，已消费为切样式）
            }
        }
        catch (Exception ex) { Core.LogMsg("[黑金典藏箱] PrefixToggleSlotItem: " + ex.Message); }
        return true;
    }

    // 10-08 拆包 AI v4 实锤：切换点击条件 canToggle=[0x203]=MayToggleSlotItem && CanToggleSlotItem（ItemContextHandler:2137-2149）
    // ——黑金无槽 May=false → [0x203]=0 → 按钮可见但点不了。补 May→true → canToggle=1 → 点击触发。
    public static bool PrefixMayToggleSlotItem(GameItem __instance, ref bool __result)
    {
        try
        {
            if (__instance == null) return true;
            Core.LogMsg("[黑金典藏箱] 进入 PrefixMayToggleSlotItem id=" + (__instance.identifier ?? "NULL"));
            if (IsBlackGold(__instance)) { __result = true; return false; }
        }
        catch { }
        return true;
    }

    private static bool IsBlackGold(GameItem item)
    {
        try { return string.Equals(item.identifier, BlackGoldBox.BOX_ID, StringComparison.Ordinal); }
        catch { return false; }
    }

    public static void NextStyle(GameItem box)
    {
        try
        {
            int idx = ReadPersistStyle(box);
            if (idx < 0) idx = 0;
            idx = (idx + 1) % BlackGoldBox.StyleKeys.Length;
            ApplyStyle(box, BlackGoldBox.StyleKeys[idx]);
            PersistStyle(box, idx);
            Core.LogMsg("[黑金典藏箱] 样式切换 → " + BlackGoldBox.StyleKeys[idx] + "（" + (idx + 1) + "/" + BlackGoldBox.StyleKeys.Length + "）");
        }
        catch (Exception ex) { Core.LogMsg("[黑金典藏箱] NextStyle: " + ex.Message); }
    }

    // 换皮（SetSprite 只改外观不碰 shape；强制 3×3 保险）
    public static void ApplyStyle(GameItem box, string key)
    {
        try
        {
            box.SetSprite(BlackGoldBox.ATLAS, key);
            try
            {
                var gsb = new GridShapeBuilder();
                gsb.SetDataFill(3, 3);
                box.SetShape(gsb.Build());
                box.modifiedShape = gsb.Build();
            }
            catch { }
        }
        catch (Exception ex) { Core.LogMsg("[黑金典藏箱] ApplyStyle: " + ex.Message); }
    }

    // ===== 样式持久化（WageSaveStore ns="BlackGold" key="style_"+loc+"_"+idx——妙妙箱 SetStagePersist loc+idx 方案）=====
    public static void PersistStyle(GameItem box, int styleIdx)
    {
        try
        {
            var e = EmporiumEntry.Instance;
            if (e == null || box == null) return;
            GameInventory[] invs = new GameInventory[] {
                e.backInvinvElement as GameInventory,
                e.backInvinvElementCounter as GameInventory,
                e.showcaseElement as GameInventory,
                e.invElement as GameInventory,
                e.frontInvinvElement as GameInventory,
                e.hiddenElement as GameInventory,
            };
            foreach (var inv in invs)
            {
                int idx = FindIndexInInv(inv, box);
                if (idx >= 0)
                {
                    string loc = LocKeyOf(inv);
                    if (loc != null) WageAPI.WageSaveStore.SetInt("BlackGold", "style_" + loc + "_" + idx, styleIdx);
                    return;
                }
            }
        }
        catch (Exception ex) { Core.LogMsg("[黑金典藏箱] PersistStyle: " + ex.Message); }
    }

    public static int ReadPersistStyle(GameItem box)
    {
        try
        {
            var e = EmporiumEntry.Instance;
            if (e == null || box == null) return -1;
            GameInventory[] invs = new GameInventory[] {
                e.backInvinvElement as GameInventory,
                e.backInvinvElementCounter as GameInventory,
                e.showcaseElement as GameInventory,
                e.invElement as GameInventory,
                e.frontInvinvElement as GameInventory,
                e.hiddenElement as GameInventory,
            };
            foreach (var inv in invs)
            {
                int idx = FindIndexInInv(inv, box);
                if (idx >= 0)
                {
                    string loc = LocKeyOf(inv);
                    if (loc != null)
                    {
                        int s = WageAPI.WageSaveStore.GetInt("BlackGold", "style_" + loc + "_" + idx, -1);
                        if (s >= 0) return s;
                    }
                }
            }
        }
        catch (Exception ex) { Core.LogMsg("[黑金典藏箱] ReadPersistStyle: " + ex.Message); }
        return -1;
    }

    private static int FindIndexInInv(GameInventory inv, GameItem box)
    {
        try
        {
            if (inv == null || inv.childItems == null || box == null) return -1;
            for (int i = 0; i < inv.childItems.Count; i++)
                if (inv.childItems[i] != null && inv.childItems[i].Pointer == box.Pointer) return i;
        }
        catch { }
        return -1;
    }

    private static string LocKeyOf(GameInventory inv)
    {
        try
        {
            var e = EmporiumEntry.Instance;
            if (e == null || inv == null) return null;
            if (inv == (e.backInvinvElement as GameInventory)) return "back";
            if (inv == (e.backInvinvElementCounter as GameInventory)) return "backC";
            if (inv == (e.showcaseElement as GameInventory)) return "show";
            if (inv == (e.invElement as GameInventory)) return "main";
            if (inv == (e.frontInvinvElement as GameInventory)) return "front";
            if (inv == (e.hiddenElement as GameInventory)) return "hid";
        }
        catch { }
        return null;
    }

    // 读档恢复样式（GameItem.SetContentWindow Postfix——原版读档重建内容窗口时；400 格内容由原版 DecodeNodes 原生恢复）
    public static void PostfixSetContentWindow(GameItem __instance)
    {
        try
        {
            if (__instance == null) return;
            // 持久化 key 命中=黑金箱（读档后场景物品 identifier/tag 全丢——不依赖它们，按 loc+idx 反查）
            // 非黑金箱无 style_ 持久化 → ReadPersistStyle=-1 → return（天然过滤）
            int styleIdx = ReadPersistStyle(__instance);
            if (styleIdx < 0 || styleIdx >= BlackGoldBox.StyleKeys.Length) return;
            ApplyStyle(__instance, BlackGoldBox.StyleKeys[styleIdx]);
            // 10-10 #U4 实证埋点（派活单 WBG_P1 子项1）：读档恢复分支打印 identifier/uniqueId——一次重启实证读档保持性
            //   （identifier null 打印 NULL；uniqueId=0 打印 0——与创建实证 :161 同格式；styleIdx 为命中值）
            try
            {
                string idv = (__instance.identifier == null) ? "NULL" : __instance.identifier;
                long uid = 0;
                try { uid = __instance.uniqueId; } catch { }
                Core.LogMsg("[黑金读档] identifier=" + idv + " uniqueId=" + uid + " styleIdx=" + styleIdx);
            }
            catch { }
            Core.LogMsg("[黑金典藏箱] 读档恢复样式 → " + BlackGoldBox.StyleKeys[styleIdx]);
        }
        catch (Exception ex) { Core.LogMsg("[黑金典藏箱] PostfixSetContentWindow: " + ex.Message); }
    }
}
