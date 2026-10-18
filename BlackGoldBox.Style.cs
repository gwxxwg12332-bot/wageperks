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
    // 10-18 总控B：误应用防护会话级判据（对齐 JP ContainerUpgradeV2.Restore.cs）——同会话重复见只记一次、永不当场升级；
    //   旁键(style_mismatch_{loc}_{idx})只存"跨会话见过一次"布尔，"两次读档"语义才真正成立
    private static readonly System.Collections.Generic.HashSet<string> _sawMismatchThisSession = new System.Collections.Generic.HashSet<string>();
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

    // 换皮（SetSpriteQuick=字段+ResolveSpriteByName→LoadFromAtlas→set_sprite 主动刷新——拆包 10-10 实锤：
    //   SetSprite(GameItem.txt:2879-2899)纯字段写入不触发渲染=切换图片不刷新根因；SetSpriteQuick(:2923-3005)刷新链命中 WBG 已 Patch 的 LoadFromAtlas Prefix(BlackGoldBox.cs:54-65)→嵌入 sprite 可解析。
    //   ⚠️不选 SetSpriteAndShape(:3165-3290)=会重写 shape，撞 10-10 shape 污染修复(BE172475)；未决 [0x259]≠0 走 ItemSpriteModifier 复杂路径→实测即知）
    public static void ApplyStyle(GameItem box, string key)
    {
        try
        {
            box.SetSpriteQuick(BlackGoldBox.ATLAS, key);
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
        string locOut; int idxOut;
        return ReadPersistStyleEx(box, out locOut, out idxOut);
    }

    // 10-18 总控B：Ex 变体额外输出命中的 loc/idx（旁键 key 需要）——原 ReadPersistStyle 只返回 styleIdx
    public static int ReadPersistStyleEx(GameItem box, out string locOut, out int idxOut)
    {
        locOut = null; idxOut = -1;
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
                        if (s >= 0) { locOut = loc; idxOut = idx; return s; }
                    }
                }
            }
        }
        catch (Exception ex) { Core.LogMsg("[黑金典藏箱] ReadPersistStyle: " + ex.Message); }
        return -1;
    }

    // 10-18 总控B：取内部库存网格尺寸（contentWindow.childElement→GameGridInventory→inventoryShape）
    private static void GetGridWH(GameItem box, ref int w, ref int h)
    {
        try
        {
            var cw = box.contentWindow;
            if (cw == null || cw.childElement == null) return;
            var grid = cw.childElement.TryCast<GameGridInventory>();
            if (grid == null) return;
            try { if (grid.inventoryShape != null) { w = grid.inventoryShape.width; h = grid.inventoryShape.height; } } catch { }
        }
        catch { }
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
            // 10-10 #U4 实证埋点 v2（派活单 WBG_P1 子项1）：入口无条件打日志（v1 只在命中后打=盲区，无法区分"挂点未触发" vs "key 未命中"）
            //   identifier null 打印 NULL；uniqueId=0 打印 0；styleIdx 为 ReadPersistStyle 原始返回值（-1=key 未命中/非黑金箱）
            try
            {
                string idv = (__instance.identifier == null) ? "NULL" : __instance.identifier;
                long uid = 0;
                try { uid = __instance.uniqueId; } catch { }
                int probeIdx = ReadPersistStyle(__instance);
                Core.LogMsg("[黑金读档] identifier=" + idv + " uniqueId=" + uid + " styleIdx=" + probeIdx);
            }
            catch { }
            // 10-18 总控B：读档恢复**身份门**（对齐 JP P0 修复模式 ContainerUpgradeV2.Restore.cs）——
            //   style_ 键命中后不再直接恢复：身份校验=内部网格尺寸==黑金出厂 20×20（读档后 identifier/uniqueId/tag 全丢，
            //   尺寸是唯一随档可验证特征；探针实测 identifier=NULL/uniqueId=0 恒成立，identifier 判据证伪）。
            //   ⚠️与 JP 差异（取舍已拍板）：JP 段位尺寸是升级动作写的固定值；WBG 的 20×20 是出厂值，真箱被 JP 链改坏后≠20×20——
            //   本门会跳过其恢复（降级：样式不恢复+不自愈，可玩家手动切一次样式恢复写新键）；换取"普通容器占位不被
            //   SelfHeal/ApplyStyle 污染"（确定性必现 > 历史坏档低频；总控 B 取舍，A 移动钩子入 backlog）。
            string locOut = null; int idxOut = -1;
            int styleIdx = ReadPersistStyleEx(__instance, out locOut, out idxOut);
            if (styleIdx < 0 || styleIdx >= BlackGoldBox.StyleKeys.Length || locOut == null || idxOut < 0) return;
            int cw = 0, ch = 0; GetGridWH(__instance, ref cw, ref ch);
            string misKey = "style_mismatch_" + locOut + "_" + idxOut;
            string styleKey = "style_" + locOut + "_" + idxOut;
            if (cw == 20 && ch == 20)
            {
                // 尺寸匹配=黑金出厂形态 → 正常恢复 + 清旁键（对齐 JP 改进：成功即清旁键，防"二次确认"退化跨事件累积）
                try { if (WageAPI.WageSaveStore.GetInt("BlackGold", misKey, 0) != 0) WageAPI.WageSaveStore.SetInt("BlackGold", misKey, 0); } catch { }
                SelfHeal20x20(__instance); // 已 20×20 早退，保留兜底
                ApplyStyle(__instance, BlackGoldBox.StyleKeys[styleIdx]);
                Core.LogMsg("[黑金典藏箱] 读档恢复样式 → " + BlackGoldBox.StyleKeys[styleIdx]);
                return;
            }
            // 尺寸不符：不恢复（不换皮不自愈）+ 记旁键；**两次不符才清键**（对齐 JP 不可逆删除防护）
            int mis = 0; try { mis = WageAPI.WageSaveStore.GetInt("BlackGold", misKey, 0); } catch { }
            if (mis > 0 && !_sawMismatchThisSession.Contains(misKey))
            {
                _sawMismatchThisSession.Add(misKey);
                try
                {
                    WageAPI.WageSaveStore.SetInt("BlackGold", styleKey, -1);
                    WageAPI.WageSaveStore.SetInt("BlackGold", misKey, -1);
                }
                catch { }
                Core.LogMsg("[黑金典藏箱] 样式键×2不符(误应用防护确认): loc=" + locOut + " idx=" + idxOut + " 实际=" + cw + "x" + ch + " 期望=20x20 → 清键自愈");
            }
            else if (_sawMismatchThisSession.Add(misKey))
            {
                try { WageAPI.WageSaveStore.SetInt("BlackGold", misKey, 1); } catch { }
                Core.LogMsg("[黑金典藏箱] 样式键不符(误应用防护首次): loc=" + locOut + " idx=" + idxOut + " 实际=" + cw + "x" + ch + " 期望=20x20 → 跳过不恢复(待二次确认)");
            }
        }
        catch (Exception ex) { Core.LogMsg("[黑金典藏箱] PostfixSetContentWindow: " + ex.Message); }
    }

    // 10-13 Step4③：黑金读档自愈——出厂值 CreateInventoryWindow(20,20,true)=400 格（BlackGoldBox.cs:80）；
    //   妙妙箱恢复链前置路径（WP RobinCrusoePerk.ContainerUpgrade.Restore.cs）曾把内部网格改 10×10~52×10（SetFullRect 写 inventoryShape）
    //   → 本校验恢复 20×20。只动内部库存网格（inventoryShape），禁动 GameItem.shape 占地（3×3，铁律 ContainerUpgradeV2.Shape.cs:49-51）。
    private static void SelfHeal20x20(GameItem box)
    {
        try
        {
            var cw = box.contentWindow;
            if (cw == null || cw.childElement == null) return;
            var grid = cw.childElement.TryCast<GameGridInventory>();
            if (grid == null) return;
            int w = 0, h = 0;
            try { if (grid.inventoryShape != null) { w = grid.inventoryShape.width; h = grid.inventoryShape.height; } } catch { }
            if (w == 20 && h == 20) return;
            Core.LogMsg("[黑金典藏箱] 读档自愈: 内部网格 " + w + "×" + h + " → 恢复 20×20（" + (w * h) + " 格 → 400 格）");
            try { grid.SetShape(new string('0', 400), 20); } catch { try { grid.SetShape("", 20); } catch { } }
            try { grid.Validate(); } catch { }
        }
        catch (Exception ex) { Core.LogMsg("[黑金典藏箱] 读档自愈异常: " + ex.Message); }
    }
}
