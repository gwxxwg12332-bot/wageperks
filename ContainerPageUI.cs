using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace JacksonPerks;

// ============================================================
// 【容器满级翻页】（用户拍板 09-12：Tab 键，双库存 Swap，InventoryPages 同款机制，拆包实锤）
// 机制：
//   - 满级容器（wb_stage >= 5）首次翻页时建 _buffer 内存网格（同尺寸，不挂 UI）
//   - 翻页 = Swap 内容（Snapshot 物品 + modifiedXOrigin/YOrigin 坐标 → Expel → 互换 → 先写坐标再 UncheckedAccept）
//   - UI 永远显示 _main（容器自己的网格），非显示页物品暂存 _buffer
// 存档（PlayerStore.SaveGame 链，拆包实锤：Harmony Prefix 在 ES3 序列化前）：
//   - Prefix MergeForSave：buffer（非显示页）→ main 空位（TryFindOneValidInventorySlot+TryAcceptOnce 落格链）
//   - Postfix SplitAfterSave：按 wb_page 把"非显示页"物品从 main 搬回 buffer（保持当前显示页）
//   - LoadGame 后 SplitOnLoad（挂 GameItem.SetContentWindow）：恢复 main 尺寸（幂等）→ 按 wb_page 分页
// 页 2 归属：wb_page2_ids tag（逗号分隔 GameItem.uniqueId，TagState.SetString 持久化，随档天然保存）
// Tab 键：对最近交互的满级容器翻页（wb_page 0=页1 / 1=页2）
// 兼容：不碰 wb_stage/wb_progress 升级链；满级后材料正常放入 main；读档恢复只动 main，Split 在其后
// ============================================================
public static class ContainerPageUI
{
    private const string PAGE2_TAG = "wb_page2_ids";

    // 容器 uniqueId → buffer 网格（运行期对象，读档重建）
    private static readonly Dictionary<long, GameGridInventory> _buffers = new Dictionary<long, GameGridInventory>();
    // 容器 uniqueId → 页2物品 uniqueId 集合（tag 持久化 + 运行时缓存）
    private static readonly Dictionary<long, HashSet<int>> _page2Ids = new Dictionary<long, HashSet<int>>();
    // 最近交互的满级容器（Tab 翻页目标）
    private static long _lastInteractContainer = 0;
    // 本次保存合并过的容器（AfterSave 用）
    private static readonly HashSet<long> _merged = new HashSet<long>();
    // 读档 Split 防重（每会话一次）
    private static readonly HashSet<IntPtr> _splitDone = new HashSet<IntPtr>();

    // ===================== 生命周期 =====================
    // PlayerStore.LoadGame Postfix：清运行期缓存（读档重建）
    public static void ResetOnLoad()
    {
        try
        {
            _splitDone.Clear();
            _lastInteractContainer = 0;
            _merged.Clear();
            _buffers.Clear();
            _page2Ids.Clear();
        }
        catch { }
    }

    // ===================== 判定 =====================
    public static bool IsPageable(GameItem c)
    {
        try
        {
            if (c == null) return false;
            if (c.IsTag("VOID_BEAD_TAG") || ContainerUpgradeV2.IsVoidBeadStorage(c)) return false;
            bool isWageBox = c.IsTag("CUSTOM_STORAGE_TAG");
            bool isCrusoe = ContainerUpgradeV2.IsUpgradeableContainer(c);
            if (!isWageBox && !isCrusoe) return false;
            return ContainerUpgradeV2.GetTagIntSafe(c, "wb_stage") >= ContainerUpgradeV2.MAX_STAGE;
        }
        catch { return false; }
    }

    // ===================== 交互记录（Tab 翻页目标） =====================
    public static void NoteInteraction(GameItem c)
    {
        try { if (IsPageable(c)) _lastInteractContainer = c.uniqueId; } catch { }
    }

    // ===================== buffer 管理（懒创建） =====================
    private static GameGridInventory GetBuffer(GameItem c)
    {
        try
        {
            if (c == null) return null;
            long uid = c.uniqueId;
            GameGridInventory b;
            if (_buffers.TryGetValue(uid, out b) && b != null) return b;
            var main = ContainerUpgradeV2.GetContainerGrid(c);
            if (main == null) return null;
            int w = 0, h = 0;
            ContainerUpgradeV2.GetShapeWH(main, ref w, ref h);
            if (w <= 0 || h <= 0) return null;
            b = new GameGridInventory(w, h);
            _buffers[uid] = b;
            return b;
        }
        catch { return null; }
    }

    // ===================== Swap（照 InventoryPages：先写坐标再 UncheckedAccept） =====================
    private static void SwapContents(GameGridInventory a, GameGridInventory b)
    {
        try
        {
            if (a == null || b == null) return;
            var listA = new List<(GameItem it, int x, int y)>();
            var listB = new List<(GameItem it, int x, int y)>();
            if (a.childItems != null)
                for (int i = a.childItems.Count - 1; i >= 0; i--) { var it = a.childItems[i]; if (it != null) { listA.Add((it, it.modifiedXOrigin, it.modifiedYOrigin)); a.Expel(it); } }
            if (b.childItems != null)
                for (int i = b.childItems.Count - 1; i >= 0; i--) { var it = b.childItems[i]; if (it != null) { listB.Add((it, it.modifiedXOrigin, it.modifiedYOrigin)); b.Expel(it); } }
            foreach (var e in listB) PlaceAt(a, e.it, e.x, e.y);
            foreach (var e in listA) PlaceAt(b, e.it, e.x, e.y);
        }
        catch { }
    }
    private static void PlaceAt(GameGridInventory grid, GameItem it, int x, int y)
    {
        try { it.modifiedXOrigin = x; it.modifiedYOrigin = y; grid.UncheckedAccept(it); }
        catch { try { grid.UncheckedAccept(it); } catch { } }
    }

    // ===================== 翻页 =====================
    public static bool FlipPage(GameItem c)
    {
        try
        {
            if (!IsPageable(c)) return false;
            var b = GetBuffer(c);
            var main = ContainerUpgradeV2.GetContainerGrid(c);
            if (b == null || main == null) return false;
            SwapContents(main, b);
            int page = 1 - ContainerUpgradeV2.GetTagIntSafe(c, "wb_page");
            ContainerUpgradeV2.SetTagIntValue(c, "wb_page", page);
            try { StoreUIManager.Instance.Notify(LangHelper.T("翻页 → 第" + (page + 1) + "页", "Page " + (page + 1)), "white"); } catch { }
            return true;
        }
        catch { return false; }
    }

    // ===================== Tab 翻页（Core.OnUpdate 调） =====================
    public static void HandleTab()
    {
        try
        {
            if (!UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Tab)) return;
            if (_lastInteractContainer == 0) return;
            var c = FindContainer(_lastInteractContainer);
            if (c == null || !IsPageable(c)) return;
            FlipPage(c);
        }
        catch { }
    }
    private static GameItem FindContainer(long uid)
    {
        try
        {
            var emp = EmporiumEntry.Instance;
            if (emp == null) return null;
            var allInvs = CollectAllInvs(emp);
            var stack = new Stack<GameInventory>(allInvs);
            var visited = new HashSet<IntPtr>();
            while (stack.Count > 0)
            {
                var inv = stack.Pop();
                if (inv == null || inv.childItems == null) continue;
                for (int i = 0; i < inv.childItems.Count; i++)
                {
                    var it = inv.childItems[i];
                    if (it == null || !visited.Add(it.Pointer)) continue;
                    if (it.uniqueId == uid) return it;
                    try { var cw = it.contentWindow; if (cw != null && cw.childElement != null) { var inner = cw.childElement.Cast<GameGridInventory>(); if (inner != null && !allInvs.Contains(inner)) { allInvs.Add(inner); stack.Push(inner); } } } catch { }
                }
            }
        }
        catch { }
        return null;
    }

    // ===================== 页 2 归属（tag 持久化） =====================
    private static HashSet<int> GetPage2Set(GameItem c)
    {
        try
        {
            long uid = c.uniqueId;
            HashSet<int> s;
            if (_page2Ids.TryGetValue(uid, out s)) return s;
            s = new HashSet<int>();
            try { var t = c.GetTagReadonly(PAGE2_TAG); if (t != null) { string v = t.valueString; if (!string.IsNullOrEmpty(v)) { foreach (var p in v.Split(',')) { int id; if (int.TryParse(p.Trim(), out id)) s.Add(id); } } } } catch { }
            _page2Ids[uid] = s;
            return s;
        }
        catch { return new HashSet<int>(); }
    }
    private static void SavePage2Tag(GameItem c)
    {
        try
        {
            var s = GetPage2Set(c);
            string v = string.Join(",", s);
            if (!c.IsTag(PAGE2_TAG)) c.EnableTag(PAGE2_TAG, true);
            System.Action<TagState> sysAct = delegate (TagState state) { state.SetString(v); };
            var il2cppAct = DelegateSupport.ConvertDelegate<Il2CppSystem.Action<TagState>>((System.Delegate)sysAct);
            c.ModifyTag(PAGE2_TAG, il2cppAct, false);
        }
        catch { }
    }

    // ===================== 存档三钩子 =====================
    // SaveGame Prefix：buffer（非显示页）→ main 空位（ES3 只存 main）
    public static void MergeForSave()
    {
        try
        {
            _merged.Clear();
            var emp = EmporiumEntry.Instance;
            if (emp == null) return;
            var allInvs = CollectAllInvs(emp);
            var stack = new Stack<GameInventory>(allInvs);
            var visited = new HashSet<IntPtr>();
            while (stack.Count > 0)
            {
                var inv = stack.Pop();
                if (inv == null || inv.childItems == null) continue;
                for (int i = 0; i < inv.childItems.Count; i++)
                {
                    var it = inv.childItems[i];
                    if (it == null || !visited.Add(it.Pointer)) continue;
                    if (IsPageable(it))
                    {
                        long uid = it.uniqueId;
                        GameGridInventory b;
                        if (!_buffers.TryGetValue(uid, out b) || b == null) continue; // 没翻页过：无 buffer 无需合并
                        var main = ContainerUpgradeV2.GetContainerGrid(it);
                        if (main == null) continue;
                        MoveAll(b, main);
                        _merged.Add(uid);
                    }
                    try { var cw = it.contentWindow; if (cw != null && cw.childElement != null) { var inner = cw.childElement.Cast<GameGridInventory>(); if (inner != null && !allInvs.Contains(inner)) { allInvs.Add(inner); stack.Push(inner); } } } catch { }
                }
            }
        }
        catch { }
    }
    // SaveGame Postfix：按 wb_page 把"非显示页"物品从 main 搬回 buffer（保持当前显示页）
    public static void SplitAfterSave()
    {
        try
        {
            var emp = EmporiumEntry.Instance;
            if (emp != null)
            {
                var allInvs = CollectAllInvs(emp);
                var stack = new Stack<GameInventory>(allInvs);
                var visited = new HashSet<IntPtr>();
                while (stack.Count > 0)
                {
                    var inv = stack.Pop();
                    if (inv == null || inv.childItems == null) continue;
                    for (int i = 0; i < inv.childItems.Count; i++)
                    {
                        var it = inv.childItems[i];
                        if (it == null || !visited.Add(it.Pointer)) continue;
                        if (IsPageable(it) && _merged.Contains(it.uniqueId))
                        {
                            var b = GetBuffer(it);
                            var main = ContainerUpgradeV2.GetContainerGrid(it);
                            if (b != null && main != null) SplitByPage(it, main, b);
                        }
                        try { var cw = it.contentWindow; if (cw != null && cw.childElement != null) { var inner = cw.childElement.Cast<GameGridInventory>(); if (inner != null && !allInvs.Contains(inner)) { allInvs.Add(inner); stack.Push(inner); } } } catch { }
                    }
                }
            }
            _merged.Clear();
        }
        catch { _merged.Clear(); }
    }
    // GameItem.SetContentWindow Postfix：读档后恢复 main 尺寸（幂等）+ 按 wb_page 分页
    public static void SplitOnLoad(GameItem __instance)
    {
        try
        {
            if (__instance == null || !IsPageable(__instance)) return;
            if (!_splitDone.Add(__instance.Pointer)) return;
            // main 恢复（幂等：尺寸已对则跳过）——蛙哥箱/鲁滨逊容器各自恢复链
            if (__instance.IsTag("CUSTOM_STORAGE_TAG")) ContainerUpgradeV2.RestoreWageBoxShape(__instance);
            else ContainerUpgradeV2.RestoreCrusoeShape(__instance);
            var b = GetBuffer(__instance);
            var main = ContainerUpgradeV2.GetContainerGrid(__instance);
            if (b == null || main == null) return;
            SplitByPage(__instance, main, b);
        }
        catch { }
    }

    // ===================== 拆分（按 wb_page 恢复显示布局） =====================
    // page=0（显示页1）：main=页1（非 ids）、buffer=页2（ids）
    // page=1（显示页2）：main=页2（ids）、buffer=页1（非 ids）
    private static void SplitByPage(GameItem c, GameInventory main, GameInventory b)
    {
        try
        {
            var ids = GetPage2Set(c);
            int page = ContainerUpgradeV2.GetTagIntSafe(c, "wb_page");
            if (main == null || main.childItems == null || b == null) return;
            var toMove = new List<GameItem>();
            for (int i = main.childItems.Count - 1; i >= 0; i--)
            {
                var it = main.childItems[i];
                if (it == null) continue;
                bool isPage2 = ids.Contains(it.uniqueId);
                bool toBuffer = (page == 0) ? isPage2 : !isPage2;
                if (toBuffer) { toMove.Add(it); main.Expel(it); }
            }
            foreach (var it in toMove) { try { b.UncheckedAccept(it); } catch { try { main.UncheckedAccept(it); } catch { } } }
        }
        catch { }
    }

    // ===================== 移动工具 =====================
    // from → to 空位（TryFindOneValidInventorySlot + TryAcceptOnce 落格链，失败才 UncheckedAccept 兜底）
    private static void MoveAll(GameGridInventory from, GameGridInventory to)
    {
        try
        {
            if (from == null || from.childItems == null || to == null) return;
            var list = new List<GameItem>();
            for (int i = from.childItems.Count - 1; i >= 0; i--) { var it = from.childItems[i]; if (it != null) { list.Add(it); from.Expel(it); } }
            foreach (var it in list)
            {
                bool placed = false;
                try { var slot = to.TryFindOneValidInventorySlot(it, false); if (slot != null) { slot.TryAcceptOnce(); placed = true; } } catch { }
                if (!placed) { try { to.UncheckedAccept(it); } catch { } }
            }
        }
        catch { }
    }
    private static List<GameInventory> CollectAllInvs(EmporiumEntry emp)
    {
        var all = new List<GameInventory>();
        try { var v = emp.backInvinvElement as GameInventory; if (v != null) all.Add(v); } catch { }
        try { var v = emp.backInvinvElementCounter as GameInventory; if (v != null) all.Add(v); } catch { }
        try { var v = emp.showcaseElement as GameInventory; if (v != null) all.Add(v); } catch { }
        try { var v = emp.invElement as GameInventory; if (v != null) all.Add(v); } catch { }
        try { var v = emp.frontInvinvElement as GameInventory; if (v != null) all.Add(v); } catch { }
        return all;
    }
}
