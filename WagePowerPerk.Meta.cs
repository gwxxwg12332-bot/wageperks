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
    internal const string PerkId = "蛙哥牛逼";

    internal override string Id => PerkId;
    internal override string DisplayName => LangHelper.T("蛙哥牛逼", "Wage Power");
    internal override string Description => LangHelper.T("蛙哥真传。开局赠送蛙哥妙妙箱（超大容量储物箱），并附赠仿生女仆蛙娘：喂食/照顾提升六维，在店时客户预算×4、议价+50%。喂她违禁品可洗白或销赃，克扣存小金库。妙妙箱拖螺丝或放箱内过夜自动升级（1/10/20/40/50），满级得第二个妙妙箱。", "Wage's legacy. Start with Wage's Wonder Box (extra-large storage) and his biomimetic maid: feed/care raise 6 stats, in-store budget x4 and bargain +50%. Feed contraband to launder or fence, kept money goes to savings. Wonder Box upgrades with screws (1/10/20/40/50), max level gives a second box.");
    internal override int Cost => BuildConfig.HardMode ? 7 : 5; // 09-23 用户拍板：普通5/硬爽7
    internal override int Type => 0;

    internal override void OnNewGame()
    {
        // 蛙哥牛逼开局自带蛙哥妙妙箱：与拾荒者信物同一原生机制。
        // 这里只重置发箱标记，实际给予由 NewGameData.HandleInitialItem Postfix 完成
        //（与游戏原生开局物品同一时机，读档不触发，从根本解决多刷/补发问题）
        _storageBoxGiven = false;
        try { WageSaveStore.SetBool(PerkId, "storageBoxGiven", false); } catch { }
    }
    
    
    /// <summary>
    /// 尝试给予蛙哥妙妙箱（参考ExtraPerks的InventoryGrant.TryGrant）
    /// </summary>
    public static void TryGiveStorageBox()
    {
        try
        {
            // 只在开新档触发（HandleInitialItem 原生钩子）；_storageBoxGiven 静态标志防进程内重复。
            // 注意：不再用持久化标记防重——之前它残留 true + OnNewGame 重置不可靠，会把箱子误拦。
            // HandleInitialItem 只在开新档调用（读档走 LoadGame 分支），天然不会多刷。
            if (_storageBoxGiven) return;
            if (!IsActive()) return;
            
            EmporiumEntry emporium = EmporiumEntry.Instance;
            if (emporium == null || emporium.backInvinvElement == null)
            {
                Core.LogMsg("[蛙哥牛逼] EmporiumEntry或backInvinvElement为null，入队重试");
                _pendingBoxGive = true;
                _pendingBoxFrames = 600;
                return;
            }
            
            
            // 创建蛙哥妙妙箱
            GameItem storageBox = CustomStorageContainer.CreateContainer();
            if (storageBox == null)
            {
                Core.LogMsg("[蛙哥牛逼] 创建蛙哥妙妙箱失败");
                return;
            }
            
            // 往箱子里放2个随机上锁的箱子和对应的卡
            try
            {
                CustomStorageContainer.FillWithRandomLockedBoxes(storageBox, 1);
            }
            catch (Exception ex)
            {
                Core.LogMsg("[蛙哥牛逼] 填充蛙哥妙妙箱失败: " + ex.Message);
            }
            
            // 标记为已拥有
            storageBox.DisableTag("TAG_NOT_PURCHASED", true);
            storageBox.DisableTag("not_purchased", true);
            
            // 找一个有效槽位并添加到后背包
            emporium.backInvinvElement.TryFindOneValidInventorySlot(storageBox, false);
            if (((GameInventory)emporium.backInvinvElement).UncheckedAccept(storageBox))
            {
                emporium.TransferOwnershipBackInv();
                emporium.TransferOwnedItemBackToInv();
                _storageBoxGiven = true;
                _pendingBoxGive = false;
                // 持久化发箱状态，读档后不再重复给（Bug2修复）
                try { WageSaveStore.SetBool(PerkId, "storageBoxGiven", true); } catch { }
            }
            else
            {
                Core.LogMsg("[蛙哥牛逼] 蛙哥妙妙箱添加到后背包失败");
            }
        }
        catch (Exception ex)
        {
            Core.LogMsg("[蛙哥牛逼] TryGiveStorageBox异常: " + ex.Message + "\n" + ex.StackTrace);
        }
    }

    // 获取玩家所有背包（后背包+计数器+展示柜+主存储）
    private static System.Collections.Generic.List<GameInventory> GetAllPlayerInventories()
    {
        var list = new System.Collections.Generic.List<GameInventory>();
        try
        {
            EmporiumEntry emporium = EmporiumEntry.Instance;
            if (emporium == null) return list;
            try { var v = emporium.backInvinvElement as GameInventory; if (v != null) list.Add(v); } catch { }
            try { var v = emporium.backInvinvElementCounter as GameInventory; if (v != null) list.Add(v); } catch { }
            try { var v = emporium.showcaseElement as GameInventory; if (v != null) list.Add(v); } catch { }
            try { var v = emporium.invElement as GameInventory; if (v != null) list.Add(v); } catch { }
        }
        catch { }
        return list;
    }

    internal static bool IsActive()
    {
        return Core.PerkActive(PerkId);
    }

    // 防重复处理标志
    private static string _lastProcessedClientId = null;
    
    // 蛙哥妙妙箱：新游戏开局时需要给玩家一个箱子
    public static bool _storageBoxGiven = false; // 是否已给予过蛙哥妙妙箱（只给一次）
    // ===== 蛙哥妙妙箱发放重试（09-12）：emporium 未就绪 → 帧轮询补发 =====
    private static bool _pendingBoxGive = false;
    private static int _pendingBoxFrames = 0;

    public static void OnUpdateBoxGiveRetry()
    {
        try
        {
            if (!_pendingBoxGive) return;
            if (_pendingBoxFrames-- <= 0) { _pendingBoxGive = false; return; } // 超时放弃
            EmporiumEntry emporium = EmporiumEntry.Instance;
            if (emporium == null || emporium.backInvinvElement == null) return; // 未就绪继续等
            TryGiveStorageBox();
        }
        catch { }
    }

    // ============================================================
    // 补丁: 客户添加时修改对话和物品
    // ============================================================
    // [HarmonyPatch(typeof(StoreClientManager), "AddClient")] // 已禁用，改用OnUpdate轮询
    // [HarmonyPatch(typeof(StoreClientManager), "AddClient")] // 已禁用，改用OnUpdate轮询
    public static class AddClientPatch
    {
        static void Postfix(StoreClient storeClient)
        {
            try
            {
                bool active = IsActive();
                string clientName = storeClient?.displayName ?? storeClient?.identifier ?? "null";

                if (!active) return;
                if (storeClient == null) return;

                string clientId = storeClient.identifier ?? storeClient.displayName ?? "";
                if (clientId == _lastProcessedClientId)
                {
                    return;
                }
                _lastProcessedClientId = clientId;

                ProcessClient(storeClient);
            }
            catch (Exception ex)
            {
                MelonLogger.Error("[蛙哥牛逼] AddClient Postfix失败: " + ex.Message + "\n" + ex.StackTrace);
            }
        }
    }

    // ============================================================
    // 统一的客户处理逻辑（AddClient和OnUpdate都调用这个）
    // ============================================================
    private static void ProcessClient(StoreClient storeClient)
    {
        if (storeClient == null) return;

        try
        {
            string clientName = storeClient.displayName ?? storeClient.identifier ?? "未知";
            string intent = storeClient.clientIntent.ToString();

            // 对话修改已完全禁用（用户要求全改回原版对话）
            // 特殊NPC由 SpecialNpcManager 单独处理随机商品
            if (SpecialNpcManager.IsSpecialNpc(storeClient))
            {
                return;
            }

            // 1. 根据客户意图处理物品（保留随机商品功能，仅禁用对话修改）
            if (storeClient.clientIntent == StoreClient.ClientIntent.SELL ||
                storeClient.clientIntent == StoreClient.ClientIntent.SELLNBUY)
            {
                AddRandomItemsToCounter(storeClient);

                if (storeClient.clientIntent == StoreClient.ClientIntent.SELLNBUY)
                {
                    SetRandomBuyItems(storeClient);
                }
            }
            else if (storeClient.clientIntent == StoreClient.ClientIntent.BUY)
            {
                SetRandomBuyItems(storeClient);
            }
            else
            { }
        }
        catch (Exception ex)
        {
            MelonLogger.Error("蛙哥牛逼 ProcessClient失败: " + ex.Message);
        }
    }


    // 上一个检测到的客户ID（用于OnUpdate检测客户变化）


    // 用反射获取当前客户
    private static StoreClient GetCurrentClient()
    {
        try
        {
            PlayerStore store = PlayerStore.Instance;
            if (store == null) return null;

            // 方式1：直接从PlayerStore获取currentClientInstance属性
            try
            {
                StoreClientInstance instance = store.currentClientInstance;
                if (instance != null && instance.storeClient != null) return instance.storeClient;
            }
            catch { }

            // 方式2：从PlayerStore的storeClientManager获取
            try
            {
                StoreClientManager manager = store.storeClientManager;
                if (manager != null)
                {
                    PropertyInfo prop = typeof(StoreClientManager).GetProperty("currentClientInstance",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (prop != null)
                    {
                        object instance = prop.GetValue(manager);
                        if (instance != null)
                        {
                            PropertyInfo clientProp = instance.GetType().GetProperty("storeClient",
                                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            if (clientProp != null)
                            {
                                StoreClient c = clientProp.GetValue(instance) as StoreClient;
                                if (c != null) return c;
                            }
                        }
                    }
                }
            }
            catch { }

            // 方式3：遍历PlayerStore的所有字段，查找StoreClient类型
            try
            {
                FieldInfo[] fields = typeof(PlayerStore).GetFields(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (FieldInfo f in fields)
                {
                    if (f.FieldType == typeof(StoreClient))
                    {
                        object val = f.GetValue(store);
                        if (val != null)
                        {
                            return val as StoreClient;
                        }
                    }
                }
            }
            catch { }

            // 方式4：遍历PlayerStore的所有属性，查找StoreClient类型
            try
            {
                PropertyInfo[] props = typeof(PlayerStore).GetProperties(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (PropertyInfo p in props)
                {
                    if (p.PropertyType == typeof(StoreClient))
                    {
                        object val = p.GetValue(store);
                        if (val != null)
                        {
                            return val as StoreClient;
                        }
                    }
                }
            }
            catch { }
        }
        catch
        {
            // 静默处理
        }
        return null;
    }

    // ============================================================
    // 补丁: 客户离开时清理柜台并记录交易
    // ============================================================
//     [HarmonyPatch(typeof(PlayerStore), "DismissCurrentClient")]
    public static class DismissClientPatch
    {
        static void Prefix()
        {
            if (!IsActive()) return;
            try
            {
                // 记录柜台上的物品（假设客户走了就是交易完成）
                PlayerStore instance = PlayerStore.Instance;
                if (instance != null)
                {
                    // 反射获取柜台上的物品列表
                    Type storeType = typeof(PlayerStore);
                    PropertyInfo[] props = storeType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    foreach (PropertyInfo prop in props)
                    {
                        if (prop.Name.Contains("Table") || prop.Name.Contains("Counter") || prop.Name.Contains("Selling"))
                        {
                            try
                            {
                                object val = prop.GetValue(instance);
                                if (val is Il2CppSystem.Collections.Generic.List<GameItem> itemList)
                                {
                                    for (int i = 0; i < itemList.Count; i++)
                                    {
                                        GameItem item = itemList[i];
                                        if (item != null)
                                        {
                                            string itemId = item.identifier ?? item.name ?? "";
                                            if (!string.IsNullOrEmpty(itemId))
                                            {
                                                RecordPurchase(itemId);
                                            }
                                        }
                                    }
                                }
                            }
                            catch { }
                        }
                    }
                }
            }
            catch
            { }
        }

        static void Postfix()
        {
            if (!IsActive()) return;
            try
            {
                PlayerStore.Instance?.RemoveNotOwnedItemFromTable();
            }
            catch
            { }
        }
    }

    // ============================================================
    // 补丁: 新的一天，更新游戏天数（用StoreClientManager.OnNewDay，GameMaster.OnNewDay不存在）
    // ============================================================
//     [HarmonyPatch(typeof(StoreClientManager), "OnNewDay")]
    public static class NewDayPatch
    {
        static void Postfix()
        {
            if (!IsActive()) return;
            try
            {
                _gameDay++;
            }
            catch
            { }
        }
    }
}
