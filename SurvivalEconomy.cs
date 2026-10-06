using System;
using HarmonyLib;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Il2Cpp;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace WageSurvival;

// ===== 10-07 鲁滨逊完全归 WS WS-3：经济链 7 方法搬迁（WP Rent.cs/Survival.Patches.cs/Death.cs/ContainerUpgrade.cs 同源）=====
// 适配标注：
//   - PrefixCheckRentDay 用 WageAPI.WageSaveStore（WS 内置，PERK_ID="SurvivalGlobal" 阶段D 已随迁）
//   - PostfixPlaceSupplierInventory 用 MerchantHelper.AddItemToCounter(GameItem) 精简版（本文件内搬 WP 实现）
//   - CreateMachineryTooltip 不搬（养蛊机/瓶印机特性域——WP 保留挂点，回执标注）
internal static partial class SurvivalFood
{
    // ===== 房东批发商库存随机化（每日确定性 3-6 件）=====
    public static bool PrefixLandlordWholesaleStock()
    {
        try
        {
            if (!IsActive() || !SurvivalTrade.IsRobinsonRun()) return true;
            var ps = Il2Cpp.PlayerStore.Instance;
            if (ps == null) return false;
            int day = DeterministicSchedule.CurrentDay;
            int seed = (DeterministicSchedule.GetRunKey() + "_ld" + day).GetHashCode();
            var rng = new System.Random(seed);
            int n = rng.Next(3, 7);
            string[] pool = {
                "cat_bar", "li_eat_snackbar", "toilet_paper", "neuroactive_perfume",
                "processed_meat", "small_morsel", "morsel", "processed_juice", "cup_noodle", "raw_meat", "small_raw_meat", "processed_cheese", "meat_scrap",
                "bottled_water", "soda_red", "energy_drink", "nudka", "red_beer",
                "bandage_item"
            };
            for (int i = 0; i < n; i++)
            {
                string id = pool[rng.Next(pool.Length)];
                try
                {
                    var item = Il2Cpp.ItemSpawner.Spawn(id);
                    if (item != null) ps.AddDirectSellingItemToTable(item);
                }
                catch (System.Exception ex) { Core.LogMsg("[WageSurvival.Rent] 异常: " + ex.Message); }
            }
            return false;
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] PrefixLandlordWholesaleStock 异常: " + ex.Message); }
        return true;
    }
    // ===== 同行+供货商同来（每周日追加 CreateMerchant）=====
    public static void PostfixHandleSupplierClient()
    {
        try
        {
            if (!IsActive() || !SurvivalTrade.IsRobinsonRun()) return;
            int day = DeterministicSchedule.CurrentDay;
            if (day <= 0 || day % 7 != 0) return;
            var scm = GetStoreClientManager();
            if (scm == null) return;
            var merchant = Il2Cpp.StoreClientList.CreateMerchant();
            if (merchant == null) return;
            scm.AddClient(merchant);
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] PostfixHandleSupplierClient 异常: " + ex.Message); }
    }
    // ===== 房租改造：49/50 各 2500、100→10000、150→20000、200→50000、250→70000、250 后每 50 天封顶 70000 =====
    public static bool PrefixCheckRentDay()
    {
        try
        {
            if (!IsActive() || !SurvivalTrade.IsRobinsonRun()) return true;
            int day = DeterministicSchedule.CurrentDay;
            int rent = RentForDay(day);
            if (rent <= 0) return false;
            var ps = Il2Cpp.PlayerStore.Instance;
            if (ps == null) return false;
            if (WageAPI.WageSaveStore.GetInt(SaveStore.PERK_ID, "rent_paid_day", -1) == day) return false;
            bool enough = ps.playerCash >= rent;
            ps.playerCash -= rent;
            WageAPI.WageSaveStore.SetInt(SaveStore.PERK_ID, "rent_paid_day", day);
            if (enough)
                try { StoreUIManager.Instance.Notify(LangHelper.T("交租 " + rent + "（第" + day + "天）", "Rent due: " + rent + " (day " + day + ")"), "green"); } catch { }
            else
                try { StoreUIManager.Instance.Notify(LangHelper.T("房东来收 " + rent + "，现金不足，强制扣除！", "The landlord is here for " + rent + " - not enough cash, forcibly deducted!"), "red"); } catch { }
            return false;
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] PrefixCheckRentDay 异常: " + ex.Message); }
        return true;
    }
    // ===== 房东每周来卖货（day%7==4 且非 day18；merchant 陪房东一起来）=====
    public static void PostfixHandleNormalClient()
    {
        try
        {
            if (!IsActive() || !SurvivalTrade.IsRobinsonRun()) return;
            int day = DeterministicSchedule.CurrentDay;
            if (day % 7 != 4 || day == 18) return;
            var scm = GetStoreClientManager();
            if (scm == null) return;
            var landlord = Il2Cpp.StoreClientUniqueList.LandlordWholesale();
            if (landlord == null) return;
            scm.AddClient(landlord);
            try
            {
                var merchant = Il2Cpp.StoreClientList.CreateMerchant();
                if (merchant != null) { scm.AddClient(merchant); Core.LogMsg("[空间站鲁滨逊] 友商(merchant)已与房东同日加入队列"); }
            }
            catch (Exception mex) { Core.LogMsg("[空间站鲁滨逊] 生成友商异常: " + mex.Message); }
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] PostfixHandleNormalClient 异常: " + ex.Message); }
    }
    // ===== 失眠警觉：偷窃概率-50%（WS GetAntiTheftMult 已在 SurvivalCompBuff）=====
    public static bool PrefixHandleInsurance()
    {
        try { if (IsActive() && SurvivalTrade.IsRobinsonRun() && GetAntiTheftMult() < 1.0) { return false; } } catch { }
        return true;
    }
    // ===== 三种生存结局（starvation/thirst/disease 自定义文案）=====
    // 10-07 修复：首参 ref string→string（原方法 ExecuteGameOver(string) 值参——Harmony 首参必须原样匹配；方法体只读 ending 不改，值参安全）
    public static bool PrefixExecuteGameOver(string ending)
    {
        try
        {
            string reason = null, desc = null;
            if (ending == "starvation")
            {
                reason = LangHelper.T("你饿死了", "You starved to death");
                desc = LangHelper.T("连续多日没有进食，身体终于撑不住了。账本翻到最后一页，笔尖在纸面划出一道长长的墨痕……", "Days without food finally caught up. The ledger turns to its last page, a long ink streak trailing off the paper...");
            }
            else if (ending == "thirst")
            {
                reason = LangHelper.T("你渴死了", "You died of thirst");
                desc = LangHelper.T("喉咙干得像砂纸，嘴唇开裂。最后一滴水从杯沿滑落，你没能接住它。", "Your throat is like sandpaper, lips cracked. The last drop slips off the rim — you miss it.");
            }
            else if (ending == "disease")
            {
                reason = LangHelper.T("你病死了", "You died of illness");
                desc = LangHelper.T("病菌在体内肆虐，高烧不退。药瓶就在柜台里，可你已经没有力气伸手去拿……", "Disease ravages your body, the fever won't break. The medicine bottle sits in the counter, but you lack the strength to reach it...");
            }
            else return true;
            var go = Il2Cpp.GameOverUIManager.Instance;
            if (go != null)
            {
                go.DisplayUI(reason, desc, false);
            }
            return false;
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] PrefixExecuteGameOver 异常: " + ex.Message); }
        return true;
    }
    // ===== 供货商卖水药食物（PlaceSupplierInventory 上货入口；同日不重复）=====
    private static int _supplierGoodsDay = -1;
    public static void PostfixPlaceSupplierInventory()
    {
        try
        {
            if (!IsActive() || !SurvivalTrade.IsRobinsonRun()) return;
            int day = DeterministicSchedule.CurrentDay;
            if (day == _supplierGoodsDay) return;
            _supplierGoodsDay = day;
            string[] sellItems = {
                "bottled_water", "small_bottled_water", "water_ration",
                "raw_meat", "processed_meat", "cup_noodle", "bandage_item", "nutrient_tablet",
            };
            int added = 0;
            try
            {
                GameItem hq = Il2Cpp.WaterPremadeHelper.AccurateHighQualityWater("large_bottled_water");
                if (hq != null) { AddItemToCounter(hq, 100, false); added++; }
            }
            catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] 供货商加大瓶高品质水失败: " + ex.Message); }
            foreach (string wid in sellItems)
            {
                try
                {
                    GameItem w = DirectoryMaster.Item(wid, true);
                    if (w == null) { Core.LogMsg("[空间站鲁滨逊] 供货商加 " + wid + " 不存在"); continue; }
                    AddItemToCounter(w, 100, false);
                    added++;
                }
                catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] 供货商加 " + wid + " 失败: " + ex.Message); }
            }
            Core.LogMsg("[空间站鲁滨逊] 供货商已追加水/药/食物 " + added + " 件（day " + day + "）");
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] PostfixPlaceSupplierInventory 异常: " + ex.Message); }
    }

    // ===== 私有辅助 =====
    private static StoreClientManager GetStoreClientManager()
    {
        try
        {
            var ps = Il2Cpp.PlayerStore.Instance;
            if (ps == null) return null;
            return ps.storeClientManager;
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival.Rent] 异常: " + ex.Message); }
        return null;
    }
    private static int RentForDay(int day)
    {
        if (day == 49 || day == 50) return 2500;
        if (day == 100) return 10000;
        if (day == 150) return 20000;
        if (day == 200) return 50000;
        if (day == 250) return 70000;
        if (day > 250 && day % 50 == 0) return 70000;
        return 0;
    }
    private static int NextRentDay(int day)
    {
        if (day < 49) return 49;
        if (day < 50) return 50;
        if (day < 100) return 100;
        if (day < 150) return 150;
        if (day < 200) return 200;
        if (day < 250) return 250;
        return ((day / 50) + 1) * 50;
    }
    // MerchantHelper.AddItemToCounter(GameItem) 精简版（WP MerchantHelper.cs:61-91 同源——创建→清标签→上桌→清标签）
    private static GameItem AddItemToCounter(GameItem item, int heat = 0, bool isOwend = false, bool ignorePerk = false)
    {
        try
        {
            PlayerStore instance = PlayerStore.Instance;
            if (instance == null) { Core.LogMsg("[WageSurvival.MerchantHelper] PlayerStore.Instance为null"); return null; }
            if (item == null) return null;
            ClearAllBadTags(item);
            GameItem sellItem = item;
            ClearAllBadTags(sellItem);
            instance.AddDirectSellingItemToTable(sellItem, isOwend, false, ignorePerk, heat);
            ClearAllBadTags(sellItem);
            return sellItem;
        }
        catch (Exception ex)
        {
            Core.LogMsg("[WageSurvival.MerchantHelper] AddItemToCounter(item)异常: " + ex.Message);
            return null;
        }
    }
    private static void ClearAllBadTags(GameItem item)
    {
        if (item == null) return;
        try { item.DisableTag("stolen", true); } catch { }
        try { item.DisableTag("TAG_STOLEN", true); } catch { }
        try { item.DisableTag("STOLEN_VALUE_INT", true); } catch { }
    }
}
