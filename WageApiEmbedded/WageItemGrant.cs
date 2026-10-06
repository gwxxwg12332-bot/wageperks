using System;
using Il2Cpp;

namespace WageAPI;

/// <summary>
/// WageItemGrant 统一发放入口（2026-10-06 试点，决策#9 落地）：
/// 封装"带确认的高层发放"替代手拼链（TryFindOneValidInventorySlot + UncheckedAcceptAll + TransferOwnershipBackInv）。
/// 学 ButterLab 优化版"高层封装"思路，不抄实现——优化版 TryAddToPlayerInv=void 无成功确认（失败静默不销毁=P0 坑），
/// 本入口：返回 bool 成功 + 失败 Destroy 清理 + 成功/失败都打日志。
/// 内部链=拆包实锤链（9 处手拼同源，派活单 20261006），不发明新链。
/// 【机器舱目标不走本入口】养蛊机产出（GuMachineSystem.Tick 机器舱 TryAcceptAllMid）——目标=机器舱非玩家后库，防后人误换。
/// 【行为等价】部分调用方原代码在成功后额外调 TransferOwnedItemBackToInv（LuckScout/容器升级），
/// 骰子处原代码只有 TransferOwnershipBackInv——用 includeOwnedTransfer 参数对齐各自原行为，禁止擅自改。
/// </summary>
public static class WageItemGrant
{
    /// <summary>
    /// 发放到玩家后库（backInvinvElement）。
    /// 成功=UncheckedAcceptAll 不抛异常 + 所有权转移完成；失败=Destroy 清理 + return false。
    /// </summary>
    /// <param name="item">发放物品（调用方负责 DisableTag/EnableTag 等标签处理，本方法不碰标签）</param>
    /// <param name="includeOwnedTransfer">是否额外调 TransferOwnedItemBackToInv（对齐各调用方原行为）</param>
    public static bool GrantToPlayerBackInv(GameItem item, bool includeOwnedTransfer = false)
    {
        try
        {
            if (item == null)
            {
                Core.Log?.Msg("[WageItemGrant] 发放失败：物品为 null");
                return false;
            }
            var emporium = EmporiumEntry.Instance;
            if (emporium == null || emporium.backInvinvElement == null)
            {
                Core.Log?.Msg("[WageItemGrant] 发放失败：当铺/后库未就绪，物品已销毁清理");
                SafeDestroy(item);
                return false;
            }
            try { emporium.backInvinvElement.TryFindOneValidInventorySlot(item, false); } catch { }
            bool ok = true;
            try
            {
                var l = new Il2CppSystem.Collections.Generic.List<GameItem>();
                l.Add(item);
                ((GameInventory)emporium.backInvinvElement).UncheckedAcceptAll(l);
            }
            catch { ok = false; }
            if (!ok)
            {
                Core.Log?.Msg("[WageItemGrant] 发放失败：UncheckedAcceptAll 异常（后库满？），物品已销毁清理");
                SafeDestroy(item);
                return false;
            }
            try { emporium.TransferOwnershipBackInv(); } catch { }
            if (includeOwnedTransfer)
            {
                try { emporium.TransferOwnedItemBackToInv(); } catch { }
            }
            Core.Log?.Msg("[WageItemGrant] 发放成功：" + SafeId(item));
            return true;
        }
        catch (Exception ex)
        {
            Core.Log?.Msg("[WageItemGrant] 发放异常: " + ex.Message + "，物品已销毁清理");
            SafeDestroy(item);
            return false;
        }
    }

    private static void SafeDestroy(GameItem item)
    {
        try { if (item != null) item.Destroy(); } catch { }
    }

    private static string SafeId(GameItem item)
    {
        try { return item != null ? item.identifier : "?"; } catch { return "?"; }
    }
}
