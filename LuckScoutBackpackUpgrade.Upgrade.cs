using System;

using System.Collections.Generic;

using System.Runtime.InteropServices;

using Il2Cpp;

using Il2CppInterop.Runtime;

using Il2CppInterop.Runtime.InteropTypes;

using HarmonyLib;

using MelonLoader;

using UnityEngine;



namespace WagePerks;
partial class LuckScoutBackpackUpgrade


{
    // ===== Upgrade =====



    // 09-26 批量升级：清空防抖记录（批量循环调用时用）
    internal static void ClearDebounce()
    {
        try { _lastConsumeByBead.Clear(); } catch { }
    }

    internal static bool DoUpgrade(GameItem junk, GameItem bead, bool applyShape = true)

    {

        if (junk == null || bead == null) return false;

        // 09-19 per-bead 防抖（同一珠 0.5s 防重扣；不同珠互不误伤）；10-05 键改 identifier（Pointer 复用风险）
        DateTime _last;
        if (_lastConsumeByBead.TryGetValue(bead.identifier, out _last) && (DateTime.UtcNow - _last).TotalSeconds < 0.5) return false;

        try

        {

            int slots = GetTagInt(bead, SLOTS_TAG);


            if (slots >= MAX_SLOTS)
            {
                Core.LogMsg("[虚空珠] 失败: 已满级 slots=" + slots);
                // 满级：不能升级，但"碰一下"也恢复形状（读档后容器内虚空珠可能漏恢复）
                try
                {
                    var cw = bead.contentWindow;
                    if (cw != null && cw.childElement != null)
                    {
                        var inv = cw.childElement.TryCast<GameGridInventory>();
                        if (inv != null) ApplyLockedShape(inv, slots);
                    }
                } catch { }
                return false;
            }

            int newSlots = slots + 1;

            SetTagInt(bead, SLOTS_TAG, newSlots);



            // 更新窗口标题（升级面板）

            try { if (bead.contentWindow != null) bead.contentWindow.titleString = LangHelper.T("虚空珠 (", "Void Bead (") + newSlots + LangHelper.T("/200格) · 拖垃圾升级", "/200 slots) · drag junk to upgrade"); } catch { }



            // 升级后用 SetShape 开放新格子（10-05 批量 applyShape=false→循环后统一调一次,防连续 SetShape+Validate 重排悬垂）

            if (applyShape)

            try

            {

                var cw = bead.contentWindow;

                if (cw != null && cw.childElement != null)

                {

                    var inv = cw.childElement.TryCast<GameGridInventory>();

                    if (inv != null) ApplyLockedShape(inv, newSlots);

                }

            } catch { }



            int count = junk.unitCount - 1;

            if (count <= 0) { lock (_pendingDestroy) { _pendingDestroy.Add(junk); } } // 10-05 延迟销毁:已入库存物品Destroy让原版刷新链悬垂→C++崩溃;帧尾统一销毁

            else junk.SetUnitCount(count);

            _lastConsume = DateTime.UtcNow;

            _lastConsumeByBead[bead.identifier] = DateTime.UtcNow; // 09-19 per-bead 记录


            return true;

        }

        catch (Exception ex) { Core.LogMsg("[虚空珠] 升级异常: " + ex.Message); return false; }

    }



    // ===== 工具方法 =====

    internal static bool IsBead(GameItem item)

    {

        try { return item != null && item.IsTag(BACKPACK_TAG); }

        catch { return false; }

    }



    internal static bool IsJunk(GameItem item)

    {

        try { return item != null && item.identifier == MATERIAL_ID; }

        catch { return false; }

    }



    private static void SetTagInt(GameItem item, string tag, int value)

    {

        try

        {

            item.EnableTag(tag, true);

            System.Action<TagState> del = st => st.SetInt(value);

            var converted = DelegateSupport.ConvertDelegate<Il2CppSystem.Action<TagState>>(del);

            item.ModifyTag(tag, converted, false);

        }

        catch (Exception ex) { Core.LogMsg("[虚空珠] 写标签失败: " + ex.Message); }

    }



    private static int GetTagInt(GameItem item, string tag)

    {

        try

        {

            if (item == null || !item.IsTag(tag)) return 0;

            var ts = item.GetTagReadonly(tag);

            return (ts != null) ? ts.GetInt() : 0;

        }

        catch { return 0; }

    }



    // ===== 升级：拖垃圾到虚空珠 =====

    public static bool PrefixMayHaveValidInventorySlot(GameItem __instance, GameItem item, ref bool __result)

    {

        try

        {

            // 升级用：垃圾拖到虚空珠不放入（走升级流程）

            if (IsJunk(item) && IsBead(__instance)) { __result = false; return false; }

            if (IsJunk(__instance) && IsBead(item)) { __result = false; return false; }

            // 格子限制由 SetShape 原生处理，不需要Patch

        } catch { }

        return true;

    }

    // ===== 10-05 延迟销毁+批量shape =====
    internal static void FlushPendingDestroy()
    {
        try
        {
            List<GameItem> todo;
            lock (_pendingDestroy) { todo = new List<GameItem>(_pendingDestroy); _pendingDestroy.Clear(); }
            foreach (var it in todo) { try { if (it != null) it.Destroy(); } catch { } }
        }
        catch { }
    }

    // 批量循环后统一应用一次形状（防连续 SetShape+Validate 重排悬垂）
    internal static void ApplyBeadShape(GameItem bead)
    {
        try
        {
            if (bead == null) return;
            int slots = GetTagInt(bead, SLOTS_TAG);
            var cw = bead.contentWindow;
            if (cw != null && cw.childElement != null)
            {
                var inv = cw.childElement.TryCast<GameGridInventory>();
                if (inv != null) ApplyLockedShape(inv, slots);
            }
        }
        catch (Exception ex) { Core.LogMsg("[虚空珠] ApplyBeadShape异常: " + ex.Message); }
    }
}
