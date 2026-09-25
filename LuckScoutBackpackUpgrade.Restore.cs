using System;

using System.Collections.Generic;

using System.Runtime.InteropServices;

using Il2Cpp;

using Il2CppInterop.Runtime;

using Il2CppInterop.Runtime.InteropTypes;

using HarmonyLib;

using MelonLoader;

using UnityEngine;



namespace JacksonPerks;
partial class LuckScoutBackpackUpgrade


{
    // ===== Restore =====



    // ===== 读档恢复：SetContentWindow后识别虚空珠并恢复SetShape =====

    public static void PostfixSetContentWindow(GameItem __instance)

    {

        try

        {

            if (__instance == null) return;

            if (!__instance.IsTag(BACKPACK_TAG)) return;

            // 加入_beadItems列表（去重）

            try { if (!_beadItems.Contains(__instance)) _beadItems.Add(__instance); } catch { }



            // 修改PixelWindow背景色

            try

            {

                var cw = __instance.contentWindow;

                if (cw != null)

                {

                    IntPtr winPtr = cw.Pointer;

                    IntPtr winBgPtr = (IntPtr)(winPtr.ToInt64() + 0x28); // handler节点

                    IntPtr winBgObjPtr = Marshal.ReadIntPtr(winBgPtr);

                    if (winBgObjPtr != IntPtr.Zero)

                    {

                        var winBgObj = new Il2CppSystem.Object(winBgObjPtr);

                        var winBgMono = winBgObj.Cast<MonoBehaviour>();

                        if (winBgMono != null)

                        {

                            var winImg = winBgMono.GetComponent<UnityEngine.UI.Image>();

                            if (winImg != null) winImg.color = new Color(0.12f, 0.12f, 0.15f, 1f);

                        }

                    }

                }

            } catch (Exception exw) { Core.LogMsg("[虚空珠] 读档改背景异常: " + exw.Message); }



            // 读档后恢复SetShape锁格子 + 更新标题

            int slots = GetTagInt(__instance, SLOTS_TAG);

            if (slots <= 0) slots = 1;


            try { if (__instance.contentWindow != null) __instance.contentWindow.titleString = LangHelper.T("虚空珠 (", "Void Bead (") + slots + LangHelper.T("/200格)", "/200 slots)"); } catch { }

            try

            {

                var cw = __instance.contentWindow;

                if (cw != null && cw.childElement != null)

                {

                    string invType = cw.childElement.GetType().Name;


                    var inv = cw.childElement.TryCast<GameGridInventory>();

                    if (inv != null)

                    {

                        ApplyLockedShape(inv, slots);


                    }

                    else

                    {

                        Core.LogMsg("[虚空珠] 读档警告: 内部库存类型" + invType + "不是GameGridInventory，Cast失败，无法恢复SetShape（将在打开窗口时双保险恢复）");

                    }

                }

            } catch (Exception exv) { Core.LogMsg("[虚空珠] 读档恢复SetShape异常: " + exv.Message); }




        }

        catch (Exception ex) { Core.LogMsg("[虚空珠] PostfixSetContentWindow异常: " + ex.Message); }

    }



    // 检查玩家所有库存（4 主背包 + 递归容器）是否已有虚空珠储物袋（09-13 多刷根治：发放前判定，位置无关）
    public static bool HasAnyVoidBeadStorage()
    {
        try
        {
            var allInvs = new System.Collections.Generic.List<GameInventory>();
            EmporiumEntry emporium = EmporiumEntry.Instance;
            if (emporium == null) return false;
            try { var v = emporium.backInvinvElement as GameInventory; if (v != null) allInvs.Add(v); } catch { }
            try { var v = emporium.backInvinvElementCounter as GameInventory; if (v != null) allInvs.Add(v); } catch { }
            try { var v = emporium.showcaseElement as GameInventory; if (v != null) allInvs.Add(v); } catch { }
            try { var v = emporium.invElement as GameInventory; if (v != null) allInvs.Add(v); } catch { }
            try { var v = emporium.frontInvinvElement as GameInventory; if (v != null) allInvs.Add(v); } catch { }
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
                    if (it.IsTag(BACKPACK_TAG) || it.identifier == "void_bead_storage") return true;
                    try
                    {
                        var cw = it.contentWindow;
                        if (cw == null || cw.childElement == null) continue;
                        var inner = cw.childElement.TryCast<GameGridInventory>();
                        if (inner != null && !allInvs.Contains(inner)) { allInvs.Add(inner); stack.Push(inner); }
                    }
                    catch (System.Exception ex) { Core.LogMsg("[LuckScoutBackpackUpgrade] 异常: " + ex.Message); }
                }
            }
        }
        catch (System.Exception ex) { Core.LogMsg("[LuckScoutBackpackUpgrade] 异常: " + ex.Message); }
        return false;
    }

    // Core.OnUpdate 挂钩：每帧轻量重试（容器内容加载完成后一次成功即停）
    public static void OnUpdateRestore()
    {
        try
        {
            if (_restoreFramesLeft <= 0) return;
            _restoreFramesLeft--;
            if (RestoreAllBeadsInPlayerInventories()) _restoreFramesLeft = 0;
        }
        catch (System.Exception ex) { Core.LogMsg("[LuckScoutBackpackUpgrade] 异常: " + ex.Message); }
    }

    public static void PostfixLoadGame()
    {
        // 旧层 runID 缓存刷新已收拢至 WageSaveStore（兼容读取入口自动 EnsureLegacyFresh），此处不再手写
        try
        {
            if (RestoreAllBeadsInPlayerInventories()) { _restoreFramesLeft = 0; return; }
            _restoreFramesLeft = 180; // 容器内容延迟加载：每帧重试，最多约3秒（成功即停；原600帧递归遍历全库存造成读档后10秒内每帧开销，交易时叠加卡顿）
        }
        catch (Exception ex) { Core.LogMsg("[虚空珠] PostfixLoadGame异常: " + ex.Message); }
    }

    private static bool RestoreAllBeadsInPlayerInventories()
    {
        int restored = 0;

        // 收集玩家所有背包（后背包+计数器+展示柜+主存储）
        var allInvs = new System.Collections.Generic.List<GameInventory>();
        try
        {
            EmporiumEntry emporium = EmporiumEntry.Instance;
            if (emporium != null)
            {
                try { var v = emporium.backInvinvElement as GameInventory; if (v != null) allInvs.Add(v); } catch { }
                try { var v = emporium.backInvinvElementCounter as GameInventory; if (v != null) allInvs.Add(v); } catch { }
                try { var v = emporium.showcaseElement as GameInventory; if (v != null) allInvs.Add(v); } catch { }
                try { var v = emporium.invElement as GameInventory; if (v != null) allInvs.Add(v); } catch { }
                try { var v = emporium.hiddenElement as GameInventory; if (v != null) allInvs.Add(v); } catch { } // 09-23 补：海报后暗格

                // 递归容器内容库存：虚空珠可能放在容器里（用户反馈：容器回档需拖垃圾升级才恢复）
                // 根因：容器内的虚空珠不在 4 个主背包里，LoadGame 恢复遍历漏掉 → 升级操作碰一下才 ApplyLockedShape
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
                        catch (System.Exception ex) { Core.LogMsg("[LuckScoutBackpackUpgrade] 异常: " + ex.Message); }
                    }
                }
            }
        } catch { }

        foreach (var inv in allInvs)
        {
            if (inv == null || inv.childItems == null) continue;
            foreach (var item in inv.childItems)
            {
                if (item == null) continue;
                try
                {
                    if (!item.IsTag(BACKPACK_TAG)) continue;
                    int slots = GetTagInt(item, SLOTS_TAG);
                    if (slots <= 0) slots = 1;
                    var cw = item.contentWindow;
                    if (cw == null || cw.childElement == null) continue;
                    var gridInv = cw.childElement.TryCast<GameGridInventory>();
                    if (gridInv == null) continue;
                    ApplyLockedShape(gridInv, slots);
                    try { cw.titleString = LangHelper.T("虚空珠 (", "Void Bead (") + slots + LangHelper.T("/200格)", "/200 slots)"); } catch { }
                    restored++;
                }
                catch (Exception exi) { Core.LogMsg("[虚空珠] LoadGame恢复单个虚空珠异常: " + exi.Message); }
            }
        }
        return restored > 0;
    }



    // 每帧调用

    internal static void ProcessPendingValidate()
    {
        try
        {
            if (_pendingShapes.Count == 0) return;
            for (int i = _pendingShapes.Count - 1; i >= 0; i--)
            {
                var pe = _pendingShapes[i];
                var item = pe.Item1;
                if (item == null) { _pendingShapes.RemoveAt(i); continue; }
                var cw = item.contentWindow;
                if (cw == null || cw.childElement == null) continue; // 容器未就绪，下帧再试
                var inv = cw.childElement.TryCast<GameGridInventory>();
                if (inv != null)
                {
                    // 【09-13 不降级保护】pending 目标不得低于当前已升级 slots（读档重建入队 1 不能覆盖升级后的 6）
                    int target = Math.Max(pe.Item2, GetTagInt(item, SLOTS_TAG));
                    ApplyLockedShape(inv, target);
                    try { cw.titleString = LangHelper.T("虚空珠 (", "Void Bead (") + target + LangHelper.T("/200格)", "/200 slots)"); } catch { }
                }
                _pendingShapes.RemoveAt(i);
            }
        }
        catch (System.Exception ex) { Core.LogMsg("[LuckScoutBackpackUpgrade] 异常: " + ex.Message); }
    }
}
