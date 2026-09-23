using System;
using HarmonyLib;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Il2Cpp;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace JacksonPerks;
internal static partial class RobinCrusoePerk
{

    public static void PostfixDoubleClickAction(GameItem newItem, Vector2 mousePosition)
    {
        try
        {
            if (!IsActive() || newItem == null) return;
            if (Patches.CurrentUITradeMode != 0) return;
            // v5.7 双击位置不限（背包/柜台/存储容器均可吃喝，用户反馈"背包吃不了"修复）；仅交易模式拦截
            // 博士夜晚商店（afterhourInventory）的货没买不能吃/喝/用药（用户反馈"博士晚上的食品没买就能食用"）
            if (IsInDoctorNightInventory(newItem)) {  return; }
            // v1.1.6 未购买物品禁止吃喝用（拆包 09-12 [L1]：柜台 isOwend=false → SetItemOwned 去 IS_OWNED_TAG；权威读口 GeneralHelper.IsItemOwned=IsTag("IS_OWNED_TAG")。not_purchased 是 GameCharacterItem 静态常量非商品 tag，TAG_NOT_PURCHASED 不存在——原 IsTag 双查无效已删）
            if (!Il2Cpp.GeneralHelper.IsItemOwned(newItem)) { return; }
            // v5.7 心情主动提升：酒/烟/毒/彩票优先于吃喝（酒也是饮品，先判酒）
            // 09-13 统一双击使用类：效果触发 + 物品消耗 + 未购买拦截（IsItemOwned 已全局拦截）——酒/麻醉品/零食/饮品/日用品一条链全覆盖
            if (IsAlc(newItem)) { DrinkAlcohol(newItem); if (!IsEmptyBottle(newItem)) TryExpel(newItem); } // 酒：+15 心情后整件消失（空瓶保留装水）
            else if (IsTobacco(newItem)) BoostMood(10, LangHelper.T("抽烟", "Smoking"));
            else if (IsNarcotic(newItem)) UseNarcotic(newItem); // 09-19 麻醉品：心情+按价值档位加睡眠（原只 +20 心情）
            else if (IsLottery(newItem)) BoostMood(UnityEngine.Random.Range(10, 21), LangHelper.T("刮彩票", "Scratch Ticket"));
            // 09-13 拍板：非水饮品双击恢复 饱食+10/口渴+15（soda_red/energy_drink/galaxy_blend）
            else if (IsBeverage(newItem)) DrinkBeverage(newItem);
            // 09-13 拍板：零食（cat_bar/li_eat_snackbar/processed_cheese）吃恢复饱食 + 心情+10 + 整件消失
            else if (IsFood(newItem)) { if (IsSnack(newItem)) { BoostMood(BuildConfig.SnackMood, LangHelper.T("零食", "Snack")); EatBite(newItem); TryExpel(newItem); } else EatBite(newItem); AddBlood(30); } // 进食回血（09-17 卖血）
            else if (IsDrink(newItem)) { DrinkSip(newItem); AddBlood(50); } // 喝水回血（09-17 卖血）
            else if (IsMedicine(newItem)) TreatWithMedicine(newItem);
            // 09-13 清洁系统 v1：日用品双击恢复清洁（白名单按 id；满 100 不消耗给提示）
            else if (IsDailyNeed(newItem)) UseDailyNeed(newItem);

        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] 双击异常: " + ex.Message); }
    }

    private static void DrinkAlcohol(GameItem item)
    {
        if (IsEmptyBottle(item)) return; // 空瓶：不加心情、不消耗（装水用，09-13 拍板）
        int ml = GetWaterMl(item);
        // 09-13 拍板：酒类双击 = 心情+15 + 整件消失（不依赖 ml——修复 ItemSpawner 刷酒/无 ml 酒不加心情）
        int sip = Math.Min(SIP_ML, ml); // 一口 200ml（仿喝水）
        bool homebrew = IsHomebrewWine(item);
        int mood = BuildConfig.AlcoholMood;
        if (homebrew)
        {
            int bv = GetItemBaseValue(item);
            mood = bv >= 300 ? 30 : (bv >= 150 ? 20 : (bv >= 50 ? 15 : 10));
            if (bv >= 1000)
            {
                SetSleep(Math.Min(100, GetSleep() + 25)); // 顶级自酿额外睡眠 +25
                WageSaveStore.SetInt(PERK_ID, "hbuffDay", DeterministicSchedule.CurrentDay); // 存档：连续3天不受伤+拾荒+1
                try { StoreUIManager.Instance.Notify(LangHelper.T("顶级自酿：连续3天不受伤、拾荒次数+1", "Top Homebrew: 3d no wound, scav+1"), "green"); } catch { }
            }
        }
        BoostMood(mood, homebrew ? LangHelper.T("自酿酒", "Homebrew") : LangHelper.T("喝酒", "Drinking"));
        if (ml > 0) { try { WaterHelper.Remove(item, sip * 1000); } catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] 喝酒Remove异常 " + ex.Message); } }
        TryExpel(item); // 整件消失（09-13 用户拍板：双击酒类使用后消失）
        RefreshStatusPanel();
    }

    private static bool IsHomebrewWineBuffActive()
    {
        try
        {
            if (!IsActive()) return false;
            int start = WageSaveStore.GetInt(PERK_ID, "hbuffDay", -1);
            if (start < 0) return false;
            int now = DeterministicSchedule.CurrentDay;
            return now - start <= 2;
        }
        catch (System.Exception ex) { Core.LogMsg("[RobinCrusoePerk.Food.Consume] 异常: " + ex.Message); }
        return false;
    }

    private static bool IsHomebrewWine(GameItem item)
    {
        try
        {
            var wf = item.FindItemFeatureByCategory("CATEGORY_WINE_QUALITY");
            if (wf == null) return false;
            if (wf.realCondition != null && wf.realCondition.identifier == "wine_quality_homebrew") return true;
            if (wf.fakeCondition != null && wf.fakeCondition.identifier == "wine_quality_homebrew") return true;
        }
        catch (System.Exception ex) { Core.LogMsg("[RobinCrusoePerk.Food.Consume] 异常: " + ex.Message); }
        return false;
    }

    private static void EatBite(GameItem item)
    {
        int cal = GetCalLeft(item);
        if (cal <= 0) { TryExpel(item); return; }
        int q = GetFoodQuality(item);
        // 09-21 修：饱食满了不吃
        if (GetSatiety() >= 100) return;
        // 09-21 修：只吃需要的量，不吃一半
        int need = (100 - GetSatiety()) * 22;  // 最多还需要多少卡
        int bite = System.Math.Min(cal, need);
        bite = System.Math.Max(bite, 50);  // 至少吃一口
        int left = cal - bite;
        int effCal = q >= 3 ? (int)(bite * 0.2) : (q == 2 ? (int)(bite * 0.5) : bite);
        effCal = (int)(effCal * GetEatEffMult()); // v5.9 饿狼代谢：吃食物效果+50%（CompBuff）
        int gain = Math.Max(1, (int)Math.Round(effCal / 22f)); // 2200cal=100%：每 100 卡≈4.5%（修正：原 /100 差 4.5 倍）
        SetSatiety(Math.Min(100, GetSatiety() + gain));
        if (q >= 2)
        {
            SetHealth(Math.Max(0, GetHealth() - 10));   // 变质/腐烂：健康-10（品质惩罚，独立于生病事件）
            TryInfect(q == 3 ? 0.4 : 0.1);              // 变质10% / 腐烂40%患病（患病→健康-40）
        }
        try { StoreUIManager.Instance.Notify(LangHelper.T("进食 +" + gain + "% 饱食（" + effCal + " 卡）", "Eating +" + gain + "% Satiety (" + effCal + " kcal)"), "white"); } catch { }
        if (left <= 0)
        {
            bool removed = TryExpel(item);
            Core.LogMsg("[空间站鲁滨逊] 吃完了一份食物（饱食+" + gain + "%），移除" + (removed ? "成功" : "失败（TryExpel 未找到物品位置）"));
        }
        else
        {
            SetCalLeft(item, left);
            try { item.EnableTag(EATEN_TAG, true); } catch { }
        }
        RefreshStatusPanel(); // 实时刷新常驻面板
    }

    private static bool IsBeverage(GameItem item)
    {
        try { return item != null && BEVERAGE_IDS.Contains((item.identifier ?? "").ToLowerInvariant()); } catch { return false; }
    }

    private static int GetBeverageCalories(GameItem item)
    {
        try { if (item.IsTag("CALORIE_VALUE_TAG") || item.IsTag("CALORIE")) return GetCalorie(item); } catch { }
        string id = ""; try { id = (item.identifier ?? "").ToLowerInvariant(); } catch { }
        if (id == "soda_red" || id == "energy_drink") return 350;
        return GetCalorie(item);
    }

    private static void DrinkBeverage(GameItem item)
    {
        try
        {
            // 09-19 修复：按真实卡路里恢复饱食（cal/22=饱食%，同 EatBite 换算）——原固定 +10% 未按原生卡路里
            int cal = GetBeverageCalories(item);
            int gain = Math.Max(1, (int)Math.Round(cal / 22f));
            SetSatiety(Math.Min(100, GetSatiety() + gain));
            SetThirstPct(Math.Min(100, GetThirstPct() + BuildConfig.BeverageThirst));
            try { StoreUIManager.Instance.Notify(LangHelper.T("饮品 +" + gain + "% 饱食 +" + BuildConfig.BeverageThirst + "% 口渴（" + cal + " 卡）", "Beverage +" + gain + "% Satiety +" + BuildConfig.BeverageThirst + "% Thirst (" + cal + " kcal)"), "green"); } catch { }
            TryExpel(item); // 饮料喝完消失（消耗 1 件）
            RefreshStatusPanel();
        }
        catch (System.Exception ex) { Core.LogMsg("[RobinCrusoePerk.Food.Consume] 异常: " + ex.Message); }
    }

    private static bool IsSnack(GameItem item)
    {
        try { return item != null && SNACK_IDS.Contains((item.identifier ?? "").ToLowerInvariant()); } catch { return false; }
    }

    private static bool IsEmptyBottle(GameItem item)
    {
        try { return item != null && (item.identifier ?? "").ToLowerInvariant() == "empty_beer_bottle"; } catch { return false; }
    }

    private static void DrinkSip(GameItem item)
    {
        int ml = GetWaterMl(item);
        if (ml <= 0) {  return; }
        int sip = Math.Min(SIP_ML, ml);
        int purity = -1;
        try { purity = Il2Cpp.WaterHelper.GetWaterPurity(item); } catch { }
        int tier = purity >= 9900 ? 0 : purity >= 9600 ? 1 : purity >= 9200 ? 2 : purity >= 8800 ? 3 : 4; // 0优质 1较好 2普通 3浑浊 4脏水
        int gain = new[] { 25, 18, 12, 6, 2 }[tier];
        int hd   = new[] { 5, 2, 0, -5, -10 }[tier];
        int inf  = new[] { 0, 0, 5, 15, 30 }[tier];
        // 09-13 用户拍板：喝水不再恢复清洁（移除 +5% 清洁，cg 全 0）
        SetThirstPct(Math.Min(100, GetThirstPct() + gain));
        if (hd != 0) SetHealth(Math.Max(0, Math.Min(100, GetHealth() + hd)));
        if (inf > 0) TryInfect(inf / 100.0);
        string wname = new[] { LangHelper.T("优质", "Pure"), LangHelper.T("较好", "Good"), LangHelper.T("普通", "Plain"), LangHelper.T("浑浊", "Cloudy"), LangHelper.T("脏水", "Dirty") }[tier];
        try { StoreUIManager.Instance.Notify(LangHelper.T("饮水 +" + gain + "% 口渴（" + wname + "）", "Drinking +" + gain + "% Thirst (" + wname + ")"), "white"); } catch { }
        // 09-11 日志定案：Remove 参数单位=µl（Remove(200000) 实测扣 200ml 无超量保护）；sip*1000 = 正确扣量
        try { WaterHelper.Remove(item, sip * 1000); } catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] Remove异常 " + ex.Message); }
        RefreshStatusPanel(); // 实时刷新常驻面板
    }

    private static void UseNarcotic(GameItem item)
    {
        try
        {
            int bv = GetItemBaseValue(item);
            int slp = bv >= 300 ? 60 : (bv >= 150 ? 45 : (bv >= 50 ? 30 : 15));
            slp = (int)(slp * GetDrugEffMult()); // 回光返照：药效+50%
            SetMood(Math.Min(100, GetMood() + BuildConfig.NarcoticMood));
            SetSleep(Math.Min(100, GetSleep() + slp));
            TryExpel(item);
            try { StoreUIManager.Instance.Notify(LangHelper.T("麻醉品：心情 +" + BuildConfig.NarcoticMood + " 睡眠 +" + slp + "%", "Narcotic: Mood +" + BuildConfig.NarcoticMood + " Sleep +" + slp + "%"), "green"); } catch { }
            RefreshStatusPanel();
        }
        catch (System.Exception ex) { Core.LogMsg("[RobinCrusoePerk.Food.Consume] 异常: " + ex.Message); }
    }

    private static void TreatWithMedicine(GameItem item)
    {
        int h = GetHealth();
        if (h >= 100)
        {
            return;
        }
        bool consumed = TryExpel(item);
        int bv = GetItemBaseValue(item);
        int heal = bv >= 300 ? 100 : (bv >= 150 ? 90 : (bv >= 50 ? 60 : 30));
        heal = (int)(heal * GetDrugEffMult()); // v5.9 回光返照：药效+50%（CompBuff）
        SetHealth(Math.Min(100, h + heal));
        try { StoreUIManager.Instance.Notify(LangHelper.T("用药：健康 +" + heal + "%", "Medicine: Health +" + heal + "%"), "green"); } catch { }
        RefreshStatusPanel(); // 实时刷新常驻面板
    }

    private static void UseDailyNeed(GameItem item)
    {
        try
        {
            string id = (item.identifier ?? "").ToLowerInvariant();
            if (!DAILY_NEED_CLEAN.TryGetValue(id, out int gain)) return;
            int c = GetClean();
            if (c >= 100)
            {
                try { StoreUIManager.Instance.Notify(LangHelper.T("清洁已满，不需要使用日用品", "Cleanliness full, no need"), "white"); } catch { }
                return; // 满 100 不消耗
            }
            SetClean(Math.Min(100, c + gain));
            try { StoreUIManager.Instance.Notify(LangHelper.T("清洁 +" + gain, "Cleanliness +" + gain), "green"); } catch { }
            TryExpel(item); // 物品从库存消失（消耗 1 件）
            RefreshStatusPanel();
        }
        catch (System.Exception ex) { Core.LogMsg("[RobinCrusoePerk.Food.Consume] 异常: " + ex.Message); }
    }

}
