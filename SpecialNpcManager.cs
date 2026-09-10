using System;
using System.Collections.Generic;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace JacksonPerks;

// ============================================================
// 特殊NPC管理器（v3 参考XIAOWO官方蓝图方式重写）
// 退休枪匠/水商/酒商 —— 用游戏官方蓝图创建客户（人+货全对）
// 每天通过 HandleContentUnlockClient 调度，TryAddClient 添加
// ============================================================
internal static class SpecialNpcManager
{
    // 官方客户蓝图（人+货都由游戏官方定义）
    private static readonly (string id, string perkId, string npcName, Func<StoreClient> factory)[] OfficialNpcs = {
        ("retired_gunsmith",       "退休枪匠之友", LangHelper.T("退休枪匠", "Retired Gunsmith"), () => StoreClientListSpec.CreateRetiredGunsmith()),
        ("retired_water_merchant", "水商之友",     LangHelper.T("水商", "Water Merchant"),     () => StoreClientListSpec.CreateRetiredWaterMerchant()),
        ("retired_winemaker",      "酒商之友",     LangHelper.T("酒商", "Winemaker"),     () => StoreClientListSpec.CreateRetiredWinemaker()),
    };

    // 已添加过的特殊NPC记录（用于日志）
    private static HashSet<string> _addedThisSession = new HashSet<string>();
    private static HashSet<string> _specialNpcProcessed = new HashSet<string>();

    // 防重复调度：记录上次调度的daily key（runID + "|" + currentDay）
    private static string _lastScheduledDailyKey = string.Empty;

    // ============================================================
    // 每天调度（推荐）：在OnNewDay补丁调用，用QueueFuturClient方式
    // 游戏自动管理生成时机，客户会正确进店
    // ============================================================
    internal static void ScheduleDailyNpcs()
    {
        try
        {
            if (PlayerStore.Instance == null)
            {
                Core.LogMsg("[特殊NPC] PlayerStore.Instance为null，跳过每日调度");
                return;
            }

            // 防重复调度：同一存档同一天只调度一次（参考XIAOWO实现）
            string dailyKey = DeterministicSchedule.GetDailyKey();
            if (string.Equals(_lastScheduledDailyKey, dailyKey, StringComparison.Ordinal))
            {
                return;
            }
            _lastScheduledDailyKey = dailyKey;

            int day = DeterministicSchedule.CurrentDay;

            // 退休枪匠
            // 退休枪匠（每周5到访，ShouldVisitToday控制周期）
            if (RetiredGunsmithPerk.IsActive() && RetiredGunsmithPerk.ShouldVisitToday(day))
            {
                QueueOfficialNpc("retired_gunsmith", LangHelper.T("退休枪匠", "Retired Gunsmith"));
            }
            // 水商
            // 水商（每周3到访）
            if (WaterMerchantPerk.IsActive() && WaterMerchantPerk.ShouldVisitToday(day))
            {
                QueueOfficialNpc("retired_water_merchant", LangHelper.T("水商", "Water Merchant"));
            }
            // 酒商
            // 酒商（每周6到访）
            if (AlcoholMerchantPerk.IsActive() && AlcoholMerchantPerk.ShouldVisitToday(day))
            {
                QueueOfficialNpc("retired_winemaker", LangHelper.T("酒商", "Winemaker"));
            }
        }
        catch (Exception ex)
        {
            MelonLogger.Error("[特殊NPC] ScheduleDailyNpcs失败: " + ex.Message + "\n" + ex.StackTrace);
        }
    }

    // 用未来客户队列添加官方NPC（游戏自动管理生成时机）
    private static void QueueOfficialNpc(string clientId, string npcName)
    {
        try
        {
            PlayerStore.Instance.QueueFuturClient(clientId, 0);
        }
        catch (Exception ex)
        {
            MelonLogger.Error("[特殊NPC] QueueFuturClient " + npcName + "(" + clientId + ")失败: " + ex.Message);
        }
    }

    // ============================================================
    // 每天调度（备用）：参考XIAOWO，在HandleContentUnlockClient的Postfix调用
    // ============================================================
    internal static void HandleContentUnlockPostfix(StoreClientManager manager)
    {
        try
        {
            if (manager == null || PlayerStore.Instance == null)
            {
                return;
            }
            int day = GetCurrentDay();
            foreach (var npc in OfficialNpcs)
            {
                bool active = IsPerkActive(npc.perkId);
                if (!active) continue;

                // 周期判断（参考XIAOWO：按周几到访，TryAddClient去重）
                bool shouldVisit = npc.id switch
                {
                    "retired_gunsmith" => RetiredGunsmithPerk.ShouldVisitToday(day),
                    "retired_water_merchant" => WaterMerchantPerk.ShouldVisitToday(day),
                    "retired_winemaker" => AlcoholMerchantPerk.ShouldVisitToday(day),
                    _ => true
                };
                if (!shouldVisit)
                {
                    continue;
                }

                TryAddOfficialClient(manager, npc.id, npc.npcName, npc.factory);
            }
        }
        catch (Exception ex)
        {
            MelonLogger.Error("[特殊NPC] HandleContentUnlockPostfix失败: " + ex.Message);
        }
    }

    // ============================================================
    // 用官方蓝图创建客户并添加到客户栈
    // ============================================================
    private static void TryAddOfficialClient(StoreClientManager manager, string clientId, string npcName, Func<StoreClient> factory)
    {
        try
        {
            // 防重复：客户栈里已有则跳过
            if (ContainsClient(manager, clientId))
            {
                return;
            }

            StoreClient client = factory();
            if (client == null)
            {
                return;
            }

            manager.TryAddClient(client, true);

            if (!_addedThisSession.Contains(clientId))
            {
                _addedThisSession.Add(clientId);
            }
        }
        catch (Exception ex)
        {
            MelonLogger.Error("[特殊NPC] 添加" + npcName + "(" + clientId + ")失败: " + ex.Message + "\n" + ex.StackTrace);
        }
    }

    // ============================================================
    // 检查客户栈中是否已有该客户
    // ============================================================
    private static bool ContainsClient(StoreClientManager manager, string clientId)
    {
        try
        {
            Il2CppSystem.Collections.Generic.List<StoreClient> stack = manager.clientStack;
            if (stack == null) return false;

            for (int i = 0; i < stack.Count; i++)
            {
                StoreClient c = stack[i];
                if (c != null && !string.IsNullOrEmpty(c.identifier) && c.identifier == clientId)
                {
                    return true;
                }
            }
        }
        catch { }
        return false;
    }

    // ============================================================
    // 检查特性是否激活
    // ============================================================
    private static bool IsPerkActive(string perkId)
    {
        try
        {
            return Core.PerkActive(perkId);
        }
        catch
        {
            return false;
        }
    }

    // ============================================================
    // 重置会话记录（新游戏时调用）
    // ============================================================
    internal static void ResetSession()
    {
        _addedThisSession.Clear();
        _specialNpcProcessed.Clear();

    }

    // ============================================================
    // 判断是否官方特殊NPC
    // ============================================================
    internal static bool IsSpecialNpc(StoreClient client)
    {
        if (client == null || client.identifier == null) return false;
        string id = client.identifier;
        return id == "retired_gunsmith" || id == "retired_water_merchant" || id == "retired_winemaker" || id == "inventorStorage" || id == "inventor_storage";
    }

    // 获取当前客户
    internal static StoreClient GetCurrentClient()
    {
        try
        {
            PlayerStore instance = PlayerStore.Instance;
            if (instance == null || instance.currentClientInstance == null) return null;
            return instance.currentClientInstance.GetClientBlueprint();
        }
        catch
        {
            return null;
        }
    }

    // ============================================================
    // 特殊NPC进店（StartMainDialogue补丁触发，吸取XIAOWO经验：补丁事件驱动，不用OnUpdate轮询）
    // 客户进店对话开始、交易区就绪时，为其添加随机商品（从蛙哥特性的随机商品池调用）
    // 与蛙哥特性分开：蛙哥管普通客户，这里管特殊NPC
    // ============================================================
    internal static void HandleSpecialNpcArrived(StoreClient client)
    {
        try
        {
            if (client == null || !IsSpecialNpc(client)) return;

            string id = client.identifier;
            if (!_specialNpcProcessed.Add(id)) return; // 本次会话已处理过（XIAOWO式防重复）

            // 从蛙哥特性的随机商品池调用（分开实现，特殊NPC也带随机商品）
            // 酒商专属：荧光莓果酿+卡箱（不走蛙哥池，蛙哥池排除容器且会清空柜台）
            if (id == "retired_winemaker")
            {
                // 只在该特性激活时加专属售卖；未激活保持原版（Bug：没点特性也会出现酒商带专属货）
                if (AlcoholMerchantPerk.IsActive())
                    AlcoholMerchantPerk.AddSpecialSellItems();
            }
            else if (id == "retired_gunsmith")
            {
                if (RetiredGunsmithPerk.IsActive())
                    RetiredGunsmithPerk.AddSpecialSellItems();
            }
            else if (id == "retired_water_merchant")
            {
                if (WaterMerchantPerk.IsActive())
                    WaterMerchantPerk.AddSpecialSellItems();
            }
            else if (id == "inventorStorage" || id == "inventor_storage")
            {
                // 博士：只在该特性激活时带三件套（大机器+大储存+神经模组）
                if (DrJacksonFriendPerk.IsActive())
                {
                    DrJacksonFriendPerk.AddJacksonGoodsToCounter(client);
                }
            }
            else
            {
                // 普通NPC：出随机物品（从蛙哥随机商品池调用，仅在蛙哥牛逼激活时）
                if (FrogPowerPerk.IsActive())
                {
                    FrogPowerPerk.AddRandomItemsToCounter(client);
                }
            }
        }
        catch (Exception ex)
        {
            Core.LogMsg("[特殊NPC] HandleSpecialNpcArrived失败: " + ex.Message);
        }
    }

    // 兼容：保留OnUpdate方法签名（Core不再调用，避免误用轮询）
    internal static void OnUpdate()
    {
        // 已废弃：改用 StartMainDialogue 补丁触发（XIAOWO经验，OnUpdate会卡）
    }

    // ============================================================
    // 兼容方法（旧代码引用，新方案不使用pending机制）
    // ============================================================
    internal static int GetCurrentDay()
    {
        try
        {
            return StoreStation.GetDayCounter();
        }
        catch
        {
            return 1;
        }
    }

    internal static bool HasPendingNpc()
    {
        return false; // 新方案无pending机制
    }

    internal static string GetPendingNpcType()
    {
        return "";
    }

    internal static void OnNewDay()
    {
        // 每天重置特殊NPC处理记录（博士天天来需要每天重新放货，酒商/枪匠周周期也依赖）
        _specialNpcProcessed.Clear();
    }
    internal static void ModifyClientAsSpecialNpc(StoreClient client)
    {
        // 新方案不用改名字方式
    }

    internal static List<string> GetPendingNpcItems()
    {
        return null;
    }

    internal static void ClearPendingNpc()
    {
    }
}
