using System;
using Il2Cpp;

namespace WageAPI;

/// <summary>
/// WageItemGrant 统一发放入口（2026-10-06 试点，二期 10-06 升级）：
/// 封装"带确认的高层发放"替代手拼链。返回 bool 成功 + 失败 Destroy 清理 + 成功/失败都打日志。
/// 禁止 TryAddToPlayerInv（void 无确认=P0）。
///
/// 【二期防重叠契约（拆包实锤，鲁滨逊 09-09 重叠 bug 修复原型）】
/// 正确链 = TryFindOneValidInventorySlot(item) → slot.TryAcceptOnce()（slot 持有格子坐标，真正落格）；
/// 之前丢弃 slot 直接 UncheckedAccept → 不设坐标 → 同格重叠。TryAcceptOnce 失败才 UncheckedAcceptAll 兜底。
/// ——统一入口强制此链，禁止直接 UncheckedAcceptAll（复发重叠 bug）。
///
/// 【所有权转移参数化（对齐各调用方原行为，禁止擅自改）】
/// 原代码分三种：①无所有权转移（鲁滨逊物资/蛙娘退回/WS血袋/WS状态发放）→ transferOwnership=false
/// ②只 TransferOwnershipBackInv（骰子）→ GrantToPlayerBackInv 默认参数=transferOwnership:true + owned:false
/// ③两调用（LuckScout/容器升级）→ includeOwnedTransfer:true = transferOwnership:true + owned:true
///
/// 【目标库存】GrantToPlayerBackInv = 玩家后库专用（backInvinvElement）；GrantToInventory = 通用（显式传目标库存）。
/// 【机器舱/博士库存目标不走本入口】养蛊机产出=机器舱网格（TryAcceptAllMid）；博士神经模组=博士夜晚库存（反射已有 bool 确认）。
/// </summary>
public static class WageItemGrant
{
    /// <summary>发放到玩家后库（backInvinvElement）。默认=TransferOwnershipBackInv 仅调用（对齐骰子原行为）；includeOwnedTransfer=true 加 TransferOwnedItemBackToInv（对齐 LuckScout/容器升级）。</summary>
    public static bool GrantToPlayerBackInv(GameItem item, bool includeOwnedTransfer = false)
    {
        try
        {
            var emporium = EmporiumEntry.Instance;
            if (emporium == null || emporium.backInvinvElement == null)
            {
                Core.Log?.Msg("[WageItemGrant] 发放失败：当铺/后库未就绪，物品已销毁清理");
                SafeDestroy(item);
                return false;
            }
            return GrantToInventory(item, emporium.backInvinvElement, transferOwnership: true, transferOwnedItem: includeOwnedTransfer);
        }
        catch (Exception ex)
        {
            Core.Log?.Msg("[WageItemGrant] 发放异常: " + ex.Message + "，物品已销毁清理");
            SafeDestroy(item);
            return false;
        }
    }

    /// <summary>
    /// 发放到指定库存（通用）。成功=TryAcceptOnce 落格（或 UncheckedAcceptAll 兜底）不抛异常；失败=Destroy 清理 + return false。
    /// 防重叠契约：TryFindOneValidInventorySlot → slot.TryAcceptOnce()（真正落格）→ 失败才 UncheckedAcceptAll 兜底。
    /// 所有权转移默认不调（对齐鲁滨逊/蛙娘退回原行为）；需转移时显式传参。
    /// </summary>
    /// <param name="item">发放物品（调用方负责 DisableTag/EnableTag/SetItemOwned 等标签处理，本方法不碰标签）</param>
    /// <param name="inv">目标库存（GameGridInventory——TryFindOneValidInventorySlot 定义在子类；UncheckedAcceptAll 走父类 GameInventory）</param>
    /// <param name="transferOwnership">是否调 TransferOwnershipBackInv</param>
    /// <param name="transferOwnedItem">是否额外调 TransferOwnedItemBackToInv</param>
    public static bool GrantToInventory(GameItem item, GameGridInventory inv, bool transferOwnership = false, bool transferOwnedItem = false)
    {
        try
        {
            if (item == null)
            {
                Core.Log?.Msg("[WageItemGrant] 发放失败：物品为 null");
                return false;
            }
            if (inv == null)
            {
                Core.Log?.Msg("[WageItemGrant] 发放失败：目标库存为 null，物品已销毁清理");
                SafeDestroy(item);
                return false;
            }
            bool ok = false;
            // 防重叠契约：先 TryFindOneValidInventorySlot → slot.TryAcceptOnce()（slot 持格子坐标真正落格）
            var slot = inv.TryFindOneValidInventorySlot(item, false);
            if (slot != null)
            {
                try { slot.TryAcceptOnce(); ok = true; } catch { ok = false; }
            }
            // TryAcceptOnce 失败/无槽 → UncheckedAcceptAll 兜底（防重叠契约允许的兜底路径）
            if (!ok)
            {
                try
                {
                    var l = new Il2CppSystem.Collections.Generic.List<GameItem>();
                    l.Add(item);
                    ((GameInventory)inv).UncheckedAcceptAll(l);
                    ok = true;
                }
                catch { ok = false; }
            }
            if (!ok)
            {
                Core.Log?.Msg("[WageItemGrant] 发放失败：TryAcceptOnce/UncheckedAcceptAll 均失败（库存满？），物品已销毁清理");
                SafeDestroy(item);
                return false;
            }
            if (transferOwnership || transferOwnedItem)
            {
                try
                {
                    var emporium = EmporiumEntry.Instance;
                    if (emporium != null)
                    {
                        if (transferOwnership) { try { emporium.TransferOwnershipBackInv(); } catch { } }
                        if (transferOwnedItem) { try { emporium.TransferOwnedItemBackToInv(); } catch { } }
                    }
                }
                catch { }
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
