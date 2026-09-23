using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace JacksonPerks;

internal sealed partial class WagePowerPerk : CustomStartingPerk
{
    private static string BuildDynamicTradeLine(StoreClient client)
    {
        try
        {
            string cat = GetClientMainItemCategory(client);
            string name = GetCategoryDialogueName(cat);
            bool isBuy = client.clientIntent == StoreClient.ClientIntent.BUY;

            if (isBuy)
            {
                string[] buys = new string[] {
                    LangHelper.T("我想买点" + name + "，你这有吗？", "I'd like to buy some " + name + ". Got any?"),
                    LangHelper.T("你这有" + name + "吗？我要收一些。", "Got " + name + "? I want to stock up."),
                    LangHelper.T("我需要" + name + "，价格合适我就都要。", "I need " + name + " - if the price is right I'll take it all."),
                    LangHelper.T("有" + name + "吗？我这次要的量不小。", "Got " + name + "? I need a fair amount this time."),
                    LangHelper.T("专门来买" + name + "的，你给个公道价。", "Came all the way to buy " + name + " - give me a fair price.")
                };
                return buys[Core.Rng.Next(buys.Length)];
            }
            else
            {
                string[] sells = new string[] {
                    LangHelper.T("这批" + name + "你看看，收不收？", "Take a look at this " + name + " - buying?"),
                    LangHelper.T("我带了点" + name + "来，你给个实在价。", "Brought you some " + name + " - give me a fair price."),
                    LangHelper.T("这" + name + "是我好不容易弄来的，你收不收？", "This " + name + " was hard to come by - taking it?"),
                    LangHelper.T("有点" + name + "要出手，你看看值多少。", "Got some " + name + " to offload - see what it's worth."),
                    LangHelper.T("老规矩，这批" + name + "你收了吧。", "Usual deal - take this " + name + " off my hands.")
                };
                return sells[Core.Rng.Next(sells.Length)];
            }
        }
        catch
        {
            return null;
        }
    }
    internal static void ModifyFirstDialogue(StoreClient client, string name)
    {
        try
        {
            
            // 对 BUY 客户设置购买需求标签（游戏 UI 显示客户想买什么，玩家不用看对话就懂）
            if (client.clientIntent == StoreClient.ClientIntent.BUY)
            {
                EnsureBuyTagsForClient(client);
            }
            
            // 生成全新的故事性对话
            string newDialogueText = GenerateStoryDialogue(client, name);

            // 返回null表示跳过（特殊NPC/剧情角色保留原对话，不覆盖）
            if (newDialogueText == null)
            {
                return;
            }

            // 空文本防护（Bug5修复）：文本为空或只有空白时跳过，避免对话开头空白
            if (string.IsNullOrWhiteSpace(newDialogueText))
            {
                Core.LogMsg("[蛙哥牛逼] 生成的对话为空，跳过: client=" + name);
                return;
            }


            // 说话人名字去空白，避免UI开头空白行（Bug5修复）
            string speakerName = (name ?? "").Trim();

            if (client.mainDialogue != null)
            {
                // 完全替换原对话，给玩家全新体验
                client.mainDialogue.SetText(speakerName, newDialogueText);
            }
            else
            {
                Dialogue newDialogue = new Dialogue();
                newDialogue.SetText(speakerName, newDialogueText);
                client.mainDialogue = newDialogue;
            }

            // 刷新UI，确保对话修改立即生效
            try
            {
                // 方式1：刷新NegociationUIManager
                if (NegociationUIManager.Instance != null)
                {
                    NegociationUIManager.Instance.DispatchUpdateForCurrentIntent();
                }
            }
            catch (Exception ex)
            {
                Core.LogMsg("[蛙哥牛逼] 刷新NegociationUIManager失败: " + ex.Message);
            }

            // 方式2：反射查找并调用所有可能的UI刷新方法
            try
            {
                var uiType = typeof(NegociationUIManager);
                var allMethods = uiType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var refreshCandidates = allMethods
                    .Where(m => (m.Name.Contains("Refresh") || m.Name.Contains("UpdateUI") ||
                                m.Name.Contains("Reload") || m.Name.Contains("Redraw") ||
                                m.Name.Contains("UpdateDialogue") || m.Name.Contains("ShowDialogue")) &&
                               m.GetParameters().Length == 0)
                    .ToList();

                foreach (var m in refreshCandidates)
                {
                    try
                    {
                        m.Invoke(NegociationUIManager.Instance, null);
                    }
                    catch (System.Exception ex) { Core.LogMsg("[WagePowerPerk.Dialogue.Trade] 异常: " + ex.Message); }
                }
            }
            catch (Exception ex)
            {
                Core.LogMsg("[蛙哥牛逼] 反射刷新UI失败: " + ex.Message);
            }
        }
        catch
        { }
    }
    internal static void AddRandomItemsToCounter(StoreClient client)
    {
        try
        {
            // 09-14 用户拍板：普通版取消"精选好货"赠送（硬爽版保留）
            if (!BuildConfig.HardMode) return;
            PlayerStore instance = PlayerStore.Instance;
            if (instance == null)
            {
                Core.LogMsg("[蛙哥牛逼] PlayerStore.Instance为null");
                return;
            }


            // 先清空柜台
            try { 
                instance.RemoveNotOwnedItemFromTable();
            } catch (Exception ex) {
                Core.LogMsg("[蛙哥牛逼] 清空柜台失败: " + ex.Message);
            }

            // 一个人卖一件物品（根据玩家现有资金过滤，确保能批发下来）
            int itemCount = 1;
            int added = 0;
            List<string> addedItems = new List<string>();
            // 蛙哥推荐物品累计总预算 = 玩家现金50%（给客户自带物品留空间，确保总价可批发）
            long remainingBudget = (long)(GetPlayerCash() * 0.5);
            if (remainingBudget < 50) remainingBudget = 50;

            for (int i = 0; i < itemCount && added < itemCount; i++)
            {
                // 使用按预算智能推荐选择物品（确保玩家买得起，累计不超过剩余预算）
                string itemId = SmartRandomItemByBudget(remainingBudget);

                // 容器：可开箱箱子用 CreateLootCrate 创建带原版内容；建筑模块（storage_bay等）跳过
                ItemCategory cat = GetItemCategory(itemId);
                if (cat == ItemCategory.Container && !IsLootableContainer(itemId))
                {
                    continue;
                }

                // 避免重复添加同一个物品
                if (addedItems.Contains(itemId))
                {
                    continue;
                }

                try
                {
                    if (!DirectoryMaster.Has<GameItem>(itemId))
                    {
                        continue;
                    }

                    GameItem item = null;
                    if (cat == ItemCategory.Container)
                    {
                        item = CreateLootCrate(itemId);
                    }
                    else
                    {
                        item = DirectoryMaster.Item(itemId, true);
                    }
                    if (item == null)
                    {
                        Core.LogMsg("[蛙哥牛逼] 创建物品失败: " + itemId);
                        continue;
                    }

                    // 清除赃物标签（避免客户卖的东西都是赃物）
                    try { item.DisableTag("stolen", true); } catch { }
                    try { item.DisableTag("TAG_STOLEN", true); } catch { }

                    // 独立实例售卖：DirectoryMaster.Item(identifier,true)已返回工厂新实例，不再CloneLinked——
                    // CloneLinked已Obsolete且克隆品丢失兑换回调委托（food_stamp双击兑换依赖+0x130/+0x138/+0x140回调）
                    // 直接用item，回调完整，食品卡兑换正常；isOwend=false正常收费；isStolen=false无盗窃
                    GameItem sellItem = item;
                    // 蛙哥送好货：isOwend=true 视为已拥有（免费拿走），并清除未拥有标签双保险（价值/品种不变）
                    try { sellItem.DisableTag("not_purchased", true); } catch { }
                    try { sellItem.DisableTag("TAG_NOT_PURCHASED", true); } catch { }
                    // 免费送=玩家已拥有：加 IS_OWNED_TAG（food_stamp等"使用类"物品双击兑换依赖此标签，正常购买流程会自动加，免费送绕过了购买）
                    try { sellItem.EnableTag("IS_OWNED_TAG", true); } catch { }
                    // 添加到柜台（isOwend=true：免费送）
                    instance.AddDirectSellingItemToTable(sellItem, true, false, false, 100);
                    // 双保险：AddDirectSellingItemToTable 内部可能对某些物品重新加 not_purchased 标签，调用后再强制清除一次
                    try { sellItem.DisableTag("not_purchased", true); } catch { }
                    try { sellItem.DisableTag("TAG_NOT_PURCHASED", true); } catch { }
                    try { sellItem.EnableTag("IS_OWNED_TAG", true); } catch { }
                    try { } catch { }
                    added++;
                    addedItems.Add(itemId);
                    // 扣减预算（保证蛙哥推荐物品累计总价可批发）
                    long wholesale = GetItemWholesalePrice(itemId);
                    if (wholesale > 0 && wholesale <= remainingBudget) remainingBudget -= wholesale;
                }
                catch (Exception ex)
                {
                    Core.LogMsg("[蛙哥牛逼] 添加" + itemId + "失败: " + ex.Message);
                }
            }

            // 如果一个都没加上，保底加1个raw_meat
            if (added == 0)
            {
                try
                {
                    GameItem meat = DirectoryMaster.Item("raw_meat", true);
                    if (meat != null)
                    {
                        try { meat.DisableTag("stolen", true); } catch { }
                        // 同上：不再CloneLinked（Obsolete+丢回调），直接用工厂产物
                        GameItem sellMeat = meat;
                        // 保底物品也免费送：清除未拥有标签 + isOwend=true
                        try { sellMeat.DisableTag("not_purchased", true); } catch { }
                        try { sellMeat.EnableTag("IS_OWNED_TAG", true); } catch { }
                        try { sellMeat.DisableTag("TAG_NOT_PURCHASED", true); } catch { }
                        instance.AddDirectSellingItemToTable(sellMeat, true, false, false, 100);
                        // 双保险：调用后再强制清除一次 not_purchased 标签
                        try { sellMeat.DisableTag("not_purchased", true); } catch { }
                        try { sellMeat.DisableTag("TAG_NOT_PURCHASED", true); } catch { }
                        added++;
                    }
                }
                catch (Exception ex)
                {
                    Core.LogMsg("[蛙哥牛逼] 保底添加失败: " + ex.Message);
                }
            }

        }
        catch
        { }
    }
}
