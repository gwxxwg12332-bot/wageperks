using System;
using Il2Cpp;
using MelonLoader;

namespace JacksonPerks;

/// <summary>
/// 商人特性通用工具类
/// 抽象4个商人特性（酒商/博士/枪匠/水商）的重复代码：
/// 创建物品→清除标签→AddDirectSellingItemToTable→清除标签（不再CloneLinked）
/// </summary>
internal static class MerchantHelper
{
    /// <summary>
    /// 通用：创建物品→清除所有坏标签→添加到柜台→再清除标签
    /// </summary>
    /// <param name="itemId">物品ID</param>
    /// <param name="heat">赃物热度（0=干净）</param>
    /// <param name="isOwend">是否已拥有（false=正常售卖，需要购买）</param>
    /// <returns>添加到柜台的物品，失败返回null</returns>
    internal static GameItem AddItemToCounter(string itemId, int heat = 0, bool isOwend = false)
    {
        try
        {
            PlayerStore instance = PlayerStore.Instance;
            if (instance == null) { Core.LogMsg("[MerchantHelper] PlayerStore.Instance为null"); return null; }
            if (!DirectoryMaster.Has<GameItem>(itemId)) {  return null; }

            GameItem item = DirectoryMaster.Item(itemId, true);
            if (item == null) { Core.LogMsg("[MerchantHelper] 创建物品失败: " + itemId); return null; }

            // 1. 创建后清除所有坏标签
            ClearAllBadTags(item);

            // 2. 直接用工厂产物（不再CloneLinked：Obsolete且克隆品丢失兑换回调委托；DirectoryMaster.Item已返回新实例）
            GameItem sellItem = item;

            // 3. 克隆后再清除标签（克隆可能继承标签）
            ClearAllBadTags(sellItem);

            // 4. 添加到柜台
            instance.AddDirectSellingItemToTable(sellItem, isOwend, false, false, heat);

            // 5. 添加后再清除标签（AddDirectSellingItemToTable内部可能重新加标签）
            ClearAllBadTags(sellItem);

            return sellItem;
        }
        catch (Exception ex)
        {
            Core.LogMsg("[MerchantHelper] AddItemToCounter异常(" + itemId + "): " + ex.Message);
            return null;
        }
    }

    /// <summary>
    /// 通用：已创建物品→清除所有坏标签→添加到柜台→再清除标签
    /// 适用于需要先自定义物品（如补名、设值）再添加的场景
    /// </summary>
    internal static GameItem AddItemToCounter(GameItem item, int heat = 0, bool isOwend = false)
    {
        try
        {
            PlayerStore instance = PlayerStore.Instance;
            if (instance == null) { Core.LogMsg("[MerchantHelper] PlayerStore.Instance为null"); return null; }
            if (item == null) return null;

            // 1. 清除所有坏标签
            ClearAllBadTags(item);

            // 2. 直接用传入物品（不再CloneLinked：Obsolete且丢兑换回调；调用方传入的已是新实例）
            GameItem sellItem = item;

            // 3. 克隆后再清除标签
            ClearAllBadTags(sellItem);

            // 4. 添加到柜台
            instance.AddDirectSellingItemToTable(sellItem, isOwend, false, false, heat);

            // 5. 添加后再清除标签
            ClearAllBadTags(sellItem);

            return sellItem;
        }
        catch (Exception ex)
        {
            Core.LogMsg("[MerchantHelper] AddItemToCounter(item)异常: " + ex.Message);
            return null;
        }
    }

    /// <summary>
    /// 清除所有赃物相关标签（注意：不清除not_purchased标签！）
    /// 包括：stolen, TAG_STOLEN, STOLEN_VALUE_INT(int标签)
    /// 经验：not_purchased标签不应手动清除，应保留让游戏交易流程自己处理；
    /// 手动清除会导致状态不一致（isOwend=false未拥有 + not_purchased被清除=已购买），交易后物品被销毁
    /// </summary>
    internal static void ClearAllBadTags(GameItem item)
    {
        if (item == null) return;
        try { item.DisableTag("stolen", true); } catch { }
        try { item.DisableTag("TAG_STOLEN", true); } catch { }
        try { item.DisableTag("STOLEN_VALUE_INT", true); } catch { }  // 赃物热度int标签（经验4922）
        // 注意：不清除not_purchased标签！保留让游戏交易流程自己处理
    }

    /// <summary>
    /// 验证物品是否干净（不带赃物/未拥有标签）
    /// </summary>
    internal static bool IsItemClean(GameItem item)
    {
        if (item == null) return false;
        try
        {
            bool hasStolen = item.IsTag("stolen") || item.IsTag("TAG_STOLEN");
            var heatTag = item.GetTagReadonly("STOLEN_VALUE_INT");
            bool hasHeat = heatTag != null && heatTag.GetInt() > 0;
            bool hasNotPurchased = item.IsTag("not_purchased") || item.IsTag("TAG_NOT_PURCHASED");
            return !hasStolen && !hasHeat && !hasNotPurchased;
        }
        catch { return false; }
    }

    // 生成解锁卡+对应箱子（配对出售）+ 枪支许可证 + 空白卡
    // 三个商人（水商/酒商/枪匠）共用此逻辑
    internal static System.Collections.Generic.List<string> GenerateCardLockPairs()
    {
        System.Collections.Generic.List<string> items = new System.Collections.Generic.List<string>();
        string[][] pairs = {
            new string[] { "eng_keycard", "eng_box" },
            new string[] { "med_keycard", "med_box" },
            new string[] { "sec_keycard", "sec_box" },
            new string[] { "ser_keycard", "service_box" },
            new string[] { "cmd_keycard", "expedition_box" },
            new string[] { "sci_keycard", "evidence_box" },
            new string[] { "sup_keycard", "toolbox" }
        };
        int pairsCount = Core.Rng.Next(3, 6);
        for (int i = 0; i < pairsCount; i++)
        {
            int idx = Core.Rng.Next(pairs.Length);
            items.Add(pairs[idx][0]);
            items.Add(pairs[idx][1]);
        }
        string[] permits = { "permit_gun_1", "permit_gun_2", "permit_gun_3" };
        items.Add(permits[Core.Rng.Next(permits.Length)]);
        if (Core.Rng.Next(2) == 0) items.Add("blank_keycard");
        return items;
    }
}
