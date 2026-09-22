using System;
using HarmonyLib;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Il2Cpp;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace JacksonPerks;

// ============================================================
// 【空间站鲁滨逊】职业生存系统（startType=14）
// v4.2（2026-09-09，v4.2 终稿重构）：
//   - 饱食节点制：calorieBalance（卡，1单位=2200卡）替代 hunger 层数制，与原生 hunger(0-1000) 完全解耦
//   - 精神 5 档（昂扬/常态/低迷/低落/崩溃）：昂扬累计制（每2天+1%售价/+5%预算，封顶+5%/+25%，断档归零）
//   - 双击食物=摄入 cal（变质50%/腐烂20%）+ 已食用档 + 患病判定（变质10%/腐烂40%）
//   - 双击水=清零 thirstLevel（渴系统独立保留）
//   - 节点：濒饿(≤0且≥3天)/饥饿(≤0)/常态(1-5单位)/饱腹(>5单位)
//   - 粮仓充盈：余额≥7单位(15400卡) → 全店售价+5%
//   - 救场：连续≤0达5天 → 好心客户送食1-2份，不删档，归零
//   - 客流削减：低迷-1/低落-2/崩溃-4；禁外出：低落/崩溃
//   - 状态客户联动（Patches/Core 侧）：加价/概率权重/预算/出价
//
// 拆包锚点全部 [L1]（cheatsheet 2.3.9 / 2.3.10 / 2.3.12 / 2.5.16 / 4.6.8 / 4.6.9 / 设计AI v4.2）
// ============================================================
internal static partial class RobinCrusoePerk
{
    internal const string FOOD_Q_TAG = "WAGES_FOOD_Q";   // 0鲜 1正常 2变质 3腐烂；-1无
    internal const string EATEN_TAG = "WAGES_EATEN";      // 食用过标记（吃了一口就标，卖出-80%）
    internal const string CAL_LEFT_TAG = "WAGES_CAL_LEFT"; // 食物剩余卡路里（吃一口扣一口，不整份消失）

    
    internal const int DAILY_CAL = 2200;            // 1 单位 = 1 天需求（v4.2 拆包实锤）
    internal const int UNIT_CAL = 2200;             // 1 单位卡路里
    internal const int NORMAL_MAX_UNIT = 5;         // 常态上限（5 单位）
    internal const int GRANARY_UNIT = 7;            // 粮仓充盈（7 单位 = 15400 卡）
    internal static int SIP_ML => BuildConfig.SipMl;                // 一口水 = 200ml（CFG 可调）
    internal const int BOTTLE_ML = 1000; // 健康每日 +%（CFG 可调）
    internal static int GRANARY_DAYS => BuildConfig.GranaryDays;

    // 食物 15 + 莓果（wine_berry 酿酒莓实锤日志，用户要可食用；[L1] FoodItemDirectory + 运行日志）
    private static readonly HashSet<string> FOOD_IDS = new HashSet<string>
    {
        "beis_icecream", "processed_milk", "processed_juice", "processed_meat", "bottle_hot_sauce",
        "cat_bar", "li_eat_snackbar", "processed_cheese", "raw_meat", "small_raw_meat",
        "morsel", "small_morsel", "fat_meat", "meat_scrap", "cup_noodle",
        "wine_berry", "bloomberry"
    };
    // 水 14（[L1]）
    private static readonly HashSet<string> DRINK_IDS = new HashSet<string>
    {
        "bottled_water", "bottled_water_premium", "small_bottled_water", "large_bottled_water", "water_jug",
        "mini_bottle", "soda_red", "energy_drink", "nudka", "galaxy_blend",
        "red_beer", "empty_beer_bottle", "water_ration", "water_ration_small"
    };     // 豁出去了 违禁品收益+50%
    // 违禁品判定（拆包 2.5.29：CONTRABAND_ITEM_TAG 权威，InitContrabandItem 写；麻醉品=违禁品子集）
    internal static bool IsContrabandItem(GameItem item)
    {
        try { if (ContrabandHelper.IsContraband(item)) return true; } catch { }
        try { if (item.IsTag("CONTRABAND")) return true; } catch { }
        try { if (item.IsTag("CONTRABAND_ITEM_TAG")) return true; } catch { }
        return false;
    }

    internal static bool IsFood(GameItem item)
    {
        if (item == null) return false;
        try { if (FOOD_IDS.Contains(GetId(item))) return true; } catch { }
        try
        {
            bool hasCal = item.IsTag("CALORIE_VALUE_TAG") || item.IsTag("CALORIE");
            if (!hasCal) return false;
            if (IsMedicine(item)) return false; // 09-18 饮料类放开（有卡路里的饮料也算食物，按真实卡路里）；药品仍排除
            string id = GetId(item);
            if (id.Contains("wine") || id.Contains("beer") || id.Contains("_seed") || id.Contains("seed_") || id.Contains("pill")) return false;
            return true;
        }
        catch { return false; }
    }
    internal static bool IsDrink(GameItem item)
    {
        if (item == null) return false;
        try { return DRINK_IDS.Contains(GetId(item)); } catch { return false; }
    }
    // 药品：MEDICAL 标签（覆盖 22 个含 injector 系）；标签失败回退关键词
    internal static bool IsMedicine(GameItem item)
    {
        if (item == null) return false;
        try { if (item.IsTag("MEDICAL")) return true; } catch { }
        try { if (item.IsTag("medical")) return true; } catch { }
        string id = GetId(item);
        string[] kw = { "pill", "injector", "bandage", "salve", "antitoxin", "stim", "blood_bag", "zerochew", "smelling_salt", "syringe" };
        foreach (string k in kw)
            if (id.Contains(k, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    // ===== 心情主动提升判定（v5.7 + 拆包回填 4-7：酒/烟/毒/彩票 identifier）=====
    // 酒精（拆包回填4）：red_beer / nudka / 发酵酒全系（ALCOHOL_VALUE 标签兜底）
    private static readonly HashSet<string> ALC_IDS = new HashSet<string>
    { "red_beer", "nudka", "empty_beer_bottle" };
    internal static bool IsAlc(GameItem item)
    {
        if (item == null) return false;
        try { if (ALC_IDS.Contains(GetId(item))) return true; } catch { }
        try { if (item.IsTag("ALCOHOL_VALUE")) return true; } catch { }
        return false;
    }
    // 烟草（拆包回填5）：cig_red（CIGARETTE_TAG 兜底）
    private static readonly HashSet<string> TOBACCO_IDS = new HashSet<string> { "cig_red", "cig_red_stack" };
    internal static bool IsTobacco(GameItem item)
    {
        if (item == null) return false;
        try { if (TOBACCO_IDS.Contains(GetId(item))) return true; } catch { }
        try { if (item.IsTag("CIGARETTE_TAG")) return true; } catch { }
        return false;
    }
    // 麻醉品（拆包回填6）：oxycodone_pill / dream_dust / injector 系（NARCOTIC 兜底）
    private static readonly HashSet<string> NARC_IDS = new HashSet<string>
    { "oxycodone_pill", "dream_dust", "dream_cap_extract", "injector", "injector_pink", "injector_pure_white_s", "black_injector" };
    internal static bool IsNarcotic(GameItem item)
    {
        if (item == null) return false;
        try { if (NARC_IDS.Contains(GetId(item))) return true; } catch { }
        try { if (item.IsTag("NARCOTIC")) return true; } catch { }
        return false;
    }
    // 彩票（拆包回填7）：scratch / scratch_stack（LOTTERY_TICKET_TAG 兜底）
    private static readonly HashSet<string> LOTTERY_IDS = new HashSet<string> { "scratch", "scratch_stack" };
    internal static bool IsLottery(GameItem item)
    {
        if (item == null) return false;
        try { if (LOTTERY_IDS.Contains(GetId(item))) return true; } catch { }
        try { if (item.IsTag("LOTTERY_TICKET_TAG")) return true; } catch { }
        return false;
    }

    // ===== 食物品质 + 卡路里 =====
    internal static int GetFoodQuality(GameItem item)
    {
        if (item == null) return -1;
        try
        {
            if (!item.IsTag(FOOD_Q_TAG)) return -1;
            var ts = item.GetTagReadonly(FOOD_Q_TAG);
            return ts != null ? ts.GetInt() : -1;
        }
        catch { return -1; }
    }
    internal static void SetFoodQuality(GameItem item, int value)
    {
        if (item == null) return;
        try
        {
            if (value < 0) { item.DisableTag(FOOD_Q_TAG, true); return; }
            if (!item.IsTag(FOOD_Q_TAG)) item.EnableTag(FOOD_Q_TAG, true);
            System.Action<TagState> sysAct = delegate (TagState state) { state.SetInt(value); };
            var il2cppAct = DelegateSupport.ConvertDelegate<Il2CppSystem.Action<TagState>>((System.Delegate)sysAct);
            item.ModifyTag(FOOD_Q_TAG, il2cppAct, false);
        }
        catch { }
    }
    // 卡路里：游戏原生标签 CALORIE_VALUE_TAG（实锤 [L1]），读不到按 300 默认
    internal static int GetCalorie(GameItem item)
    {
        if (item == null) return 0;
        try
        {
            if (item.IsTag("CALORIE_VALUE_TAG"))
            {
                var ts = item.GetTagReadonly("CALORIE_VALUE_TAG");
                if (ts != null) return Math.Max(0, ts.GetInt());
            }
        }
        catch { }
        try
        {
            if (item.IsTag("CALORIE"))
            {
                var ts = item.GetTagReadonly("CALORIE");
                if (ts != null) return Math.Max(0, ts.GetInt());
            }
        }
        catch { }
        return 300;
    }
    // 已食用判定：剩余卡路里 < 满量
    internal static bool IsEaten(GameItem item)
    {
        if (item == null || !IsFood(item)) return false;
        try
        {
            if (!item.IsTag(CAL_LEFT_TAG)) return false;
            return GetCalLeft(item) < GetCalorie(item);
        }
        catch { return false; }
    }
    // 剩余卡路里（吃一口扣一口）：有 WAGES_CAL_LEFT 用标签值，无 = 满量
    internal static int GetCalLeft(GameItem item)
    {
        if (item == null) return 0;
        try
        {
            if (item.IsTag(CAL_LEFT_TAG))
            {
                var ts = item.GetTagReadonly(CAL_LEFT_TAG);
                if (ts != null) return Math.Max(0, ts.GetInt());
            }
        }
        catch { }
        return GetCalorie(item);
    }
    internal static void SetCalLeft(GameItem item, int value)
    {
        if (item == null) return;
        try
        {
            if (value <= 0) { item.DisableTag(CAL_LEFT_TAG, true); return; }
            if (!item.IsTag(CAL_LEFT_TAG)) item.EnableTag(CAL_LEFT_TAG, true);
            System.Action<TagState> sysAct = delegate (TagState state) { state.SetInt(value); };
            var il2cppAct = DelegateSupport.ConvertDelegate<Il2CppSystem.Action<TagState>>((System.Delegate)sysAct);
            item.ModifyTag(CAL_LEFT_TAG, il2cppAct, false);
        }
        catch { }
    }
    // 变质/腐烂卡路里折算（v4.1/v4.2 实锤）：变质×50%、腐烂×20%、其余×100%
    internal static int GetEffectiveCal(GameItem item)
    {
        int q = GetFoodQuality(item);
        int cal = GetCalLeft(item);
        if (q >= 3) return (int)(cal * 0.2);
        if (q == 2) return (int)(cal * 0.5);
        return cal;
    }

    internal static int GetWaterMl(GameItem item)
    {
        if (item == null) return 0;
        // 优先读 LIQUID_CONTAINER_CURRENT tag（09-11 日志实锤：ModifyTag 扣 tag 成功但 GetCurrentCapacityML 读内层液体注册表恒 2000 不同步；原版喝水/UI 都以 tag 为权威）
        try
        {
            if (item.IsTag("LIQUID_CONTAINER_CURRENT"))
            {
                var ts = item.GetTagReadonly("LIQUID_CONTAINER_CURRENT");
                if (ts != null)
                {
                    try { return Math.Max(0, (int)ts.GetFloat()); } catch { return Math.Max(0, ts.GetInt()); }
                }
            }
        }
        catch { }
        try { return Math.Max(0, WaterHelper.GetCurrentCapacityML(item)); } catch { }
        return BOTTLE_ML;
    }
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

    // 09-14 诊断（双击连吃定位）：每次点击打印双击判定状态（OnEventPress 后），定位"第二次双击是否判定成功"
    // 喝酒（v5.7 心情+15；拆包 09-10 自酿酒价值分档）：酒瓶有剩余才给心情；喝一口减 ml（修"酒剩余0还能无限喝"）
    // 自酿酒（wine_quality_homebrew 条件）按价值心情分档（用户拍板 09-10）：<50 +10 / 50-149 +15 / 150-299 +20 / 300-999 +30；
    // ≥1000 顶级自酿：额外 睡眠+25 + 出门连续3天不受伤 + 拾荒次数+1（PerkStatePersistence 存档，runID 隔离）
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

    // 顶级自酿 buff 生效中（喝后当天+次日+第三日，PerkStatePersistence 存档）
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
        catch { }
        return false;
    }

    // 自酿酒判定（拆包 09-10 [L1]：ItemFeature.realCondition.identifier == wine_quality_homebrew；类别 CATEGORY_WINE_QUALITY）
    private static bool IsHomebrewWine(GameItem item)
    {
        try
        {
            var wf = item.FindItemFeatureByCategory("CATEGORY_WINE_QUALITY");
            if (wf == null) return false;
            if (wf.realCondition != null && wf.realCondition.identifier == "wine_quality_homebrew") return true;
            if (wf.fakeCondition != null && wf.fakeCondition.identifier == "wine_quality_homebrew") return true;
        }
        catch { }
        return false;
    }

    // 吃一口（v5.7 百分比制）：摄入卡÷22 = 饱食%（2200cal=100%）；变质×50% / 腐烂×20% 计入 + 健康-10 + 患病判定；品质不回退；变「已食用」档
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

    // ===== 09-13 拍板：非水饮品双击恢复 饱食/口渴，喝完消耗 1 件 =====
    private static readonly System.Collections.Generic.HashSet<string> BEVERAGE_IDS = new System.Collections.Generic.HashSet<string>
    { "soda_red", "energy_drink", "galaxy_blend", "processed_milk", "processed_juice" }; // 09-13 用户反馈：碳酸代乳(processed_milk)/代糖果汁(processed_juice)也走饮品链（饱食+口渴）
    private static bool IsBeverage(GameItem item)
    {
        try { return item != null && BEVERAGE_IDS.Contains((item.identifier ?? "").ToLowerInvariant()); } catch { return false; }
    }
    // 09-19 拆包：饮料卡路里 = InitFoodItem 写 CALORIE_VALUE_TAG（milk=600/juice=450/blend=450）；soda_red/energy_drink 走 TransformEdible 只写 add_thirst=350（原生无卡）→ 补 mod 基准 350
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
        catch { }
    }
    // ===== 09-13 拍板：零食（猫咪巧克力棒/轻食能量棒/合成奶酪）吃恢复饱食 + 心情+10 =====
    private static readonly System.Collections.Generic.HashSet<string> SNACK_IDS = new System.Collections.Generic.HashSet<string>
    { "cat_bar", "li_eat_snackbar", "processed_cheese" };
    private static bool IsSnack(GameItem item)
    {
        try { return item != null && SNACK_IDS.Contains((item.identifier ?? "").ToLowerInvariant()); } catch { return false; }
    }
    // 空瓶：不消耗（装水用，09-13 拍板）
    private static bool IsEmptyBottle(GameItem item)
    {
        try { return item != null && (item.identifier ?? "").ToLowerInvariant() == "empty_beer_bottle"; } catch { return false; }
    }

    // 喝水（09-11 用户拍板 5 档真实水质）：purity 分 5 档，每档独立 口渴/健康/患病/清洁；Remove 单位=ml（拆包实锤，修复 sip*1000 误删全瓶）
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

    // ===== 09-19 用户拍板：麻醉品按价值档位加睡眠（参考健康分档减半；药效倍率同享）=====
    // 麻醉品双击 = 心情 + 睡眠（<50 +15 / 50-149 +30 / 150-299 +45 / ≥300 +60）+ 消失
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
        catch { }
    }

    // 药品：按价值分档恢复健康（用户拍板 09-10：健康上限100，高档药不溢出——<50 +30 / 50-149 +60 / 150-299 +90 / ≥300 +100 回满）
    // 口径：基础价值 unitBaseValue（稳定，不受加价/事件影响）；健康满 100 不消耗
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

    // ===== 自动喝水按质生效（09-11 用户拍板：与双击同 5 档；return false 接管原版"补口渴+减水"，仿原版 AutoSipFromContainer 逻辑）=====

    // ===== 博士廉价模组供货（养蛊机系统 09-15：30 天起每 3 天 3-5 个 overclock/ruined/corrupt，60% 价）=====
    private static int _doctorSupplyDay = -1;
    internal static void TryDoctorSupply()
    {
        try
        {
            int day = DeterministicSchedule.CurrentDay;
            if (day == _doctorSupplyDay) return; // 同日不重复
            _doctorSupplyDay = day;
            // 第 10 天卖养蛊机 / 第 30 天卖生成器 + 保护器（新物品注册后追加）
            if (day >= 30)
            {
                string[] cheap = { "system_module_overclock", "system_module_ruined", "system_module_corrupt" };
                int n = Core.Rng.Next(BuildConfig.DoctorSupplyCountMin, BuildConfig.DoctorSupplyCountMax + 1);
                int added = 0;
                for (int i = 0; i < n; i++)
                {
                    try
                    {
                        GameItem m = DirectoryMaster.Item(cheap[Core.Rng.Next(cheap.Length)], true);
                        if (m == null) continue;
                        long v = 0; try { v = m.GetValue(); } catch { }
                        int price = (int)(v * BuildConfig.DoctorSupplyPricePct / 100);
                        MerchantHelper.AddItemToCounter(m, price, false);
                        added++;
                    }
                    catch { }
                }
                Core.LogMsg("[养蛊机] 博士廉价模组供货 " + added + " 件（day " + day + "）");
            }
            // 养蛊机系统：博士夜晚商店卖新物品（防堆叠——柜台无同 id 才补）
            // 第 10 天起：养蛊机（wage_gu_machine，2000）——每天到访都补 1 个（柜台无则补）
            if (day >= 10 && !HasGoodOnFront("wage_gu_machine"))
            {
                try { if (MerchantHelper.AddItemToCounter("wage_gu_machine", 0, false) != null) Core.LogMsg("[养蛊机] 博士夜晚商店卖养蛊机（day " + day + "）"); } catch { }
            }
            // 第 30 天起：AI 生成器（wage_ai_generator，1500）——柜台无则补
            if (day >= 30 && !HasGoodOnFront("wage_ai_generator"))
            {
                try { if (MerchantHelper.AddItemToCounter("wage_ai_generator", 0, false) != null) Core.LogMsg("[养蛊机] 博士夜晚商店卖生成器（day " + day + "）"); } catch { }
            }
            // 30 天起：保护器核心 3 个（wage_protector_core，1500）——每次到访补足 3 个
            if (day >= 30)
            {
                int pc = 0;
                try { pc = CountGoodOnFront("wage_protector_core"); } catch { }
                for (int pi = pc; pi < BuildConfig.ProtectorSupplyCount; pi++)
                {
                    try { if (MerchantHelper.AddItemToCounter("wage_protector_core", 0, false) != null) Core.LogMsg("[养蛊机] 博士夜晚商店卖保护器（day " + day + "）"); } catch { }
                }
            }
        }
        catch (Exception ex) { Core.LogMsg("[养蛊机] TryDoctorSupply 异常: " + ex.Message); }
    }
    public static bool PrefixAutoSipFromContainer(GameItem item, GameCharacterItem GCI)
    {
        try
        {
            if (!IsActive() || item == null || GCI == null) return true;
            int ml = GetWaterMl(item);
            if (ml <= 0) return true;
            int purity = -1;
            try { purity = Il2Cpp.WaterHelper.GetWaterPurity(item); } catch { }
            int tier = purity >= 9900 ? 0 : purity >= 9600 ? 1 : purity >= 9200 ? 2 : purity >= 8800 ? 3 : 4;
            int hd  = new[] { 5, 2, 0, -5, -10 }[tier];
            int inf = new[] { 0, 0, 5, 15, 30 }[tier];
            int cg  = new[] { 5, 4, 3, 1, 0 }[tier];
            if (hd != 0) SetHealth(Math.Max(0, Math.Min(100, GetHealth() + hd)));
            if (cg > 0) SetClean(Math.Min(100, GetClean() + cg));
            if (inf > 0) TryInfect(inf / 100.0);
            // 补原版 AutoSipFromContainer（return false 后原版不执行）：sip = min(ml, 缺口渴量)；GCI.thirst += sip；Remove(item, sip)
            try
            {
                int cur = 0, mx = 0;
                try { cur = (int)GCI.currentThirst; } catch { }
                try { mx = (int)GCI.maxThirst; } catch { }
                int need = Math.Max(0, mx - cur);
                int sip = Math.Min(ml, need);
                if (sip > 0)
                {
                    try { GCI.currentThirst = cur + sip; } catch { }
                    try { Il2Cpp.WaterHelper.Remove(item, sip * 1000); } catch { } // 09-11 定案：µl 单位
                }
            }
            catch { }
            return false; // 拦原版：水质挂钩已由 mod 接管
        }
        catch { return true; }
    }

    // ===== 物品面板 tooltip =====
    // 09-18 原生满卡虚高修复（拆包 B 方案）：吃过的食物原生行显示剩余卡——Prefix 临时改 CALORIE_VALUE_TAG 为剩余值，Postfix 恢复（外层 mod 行读回满卡，顺序安全）
    private static int _foodCalBackup = 0;
    private static bool _foodCalBackupValid = false;
    public static void PrefixFoodTooltip(GameItem item)
    {
        try
        {
            _foodCalBackupValid = false;
            if (item == null || !IsActive()) return;
            bool eaten = false; try { eaten = item.IsTag(CAL_LEFT_TAG); } catch { }
            if (!eaten) return;
            int full = 0; try { full = GetCalorie(item); } catch { }
            int left = GetCalLeft(item);
            _foodCalBackup = full; _foodCalBackupValid = true;
            try { System.Action<TagState> sysAct = delegate (TagState state) { state.SetInt(left); }; var il2cppAct = DelegateSupport.ConvertDelegate<Il2CppSystem.Action<TagState>>((System.Delegate)sysAct); item.ModifyTag("CALORIE_VALUE_TAG", il2cppAct, false); } catch { }
        }
        catch { }
    }
    public static void PostfixFoodTooltip(GameItem item)
    {
        try
        {
            if (_foodCalBackupValid && item != null)
            {
                try { System.Action<TagState> sysAct = delegate (TagState state) { state.SetInt(_foodCalBackup); }; var il2cppAct = DelegateSupport.ConvertDelegate<Il2CppSystem.Action<TagState>>((System.Delegate)sysAct); item.ModifyTag("CALORIE_VALUE_TAG", il2cppAct, false); } catch { }
                _foodCalBackupValid = false;
            }
        }
        catch { }
    }
    // ===== 09-18 喂食器按满卡算修复（拆包实锤）：b__3 搅拌转化前把吃过的食物 CALORIE_VALUE_TAG 改为剩余卡，b__3 原样按剩余转（食物随后被移除无需恢复） =====
    public static void PrefixFeedDispenserB3(Il2Cpp.MachineFeedDispenser.__c__DisplayClass7_0 __instance)
    {
        try
        {
            if (__instance == null || !IsActive()) return;
            var grid = __instance.storageGrid;
            if (grid == null || grid.childItems == null) return;
            foreach (var m in grid.childItems)
            {
                if (m == null) continue;
                bool eaten = false; try { eaten = m.IsTag(CAL_LEFT_TAG); } catch { }
                if (!eaten) continue;
                int left = GetCalLeft(m);
                try { System.Action<TagState> sysAct = delegate (TagState state) { state.SetInt(left); }; var il2cppAct = DelegateSupport.ConvertDelegate<Il2CppSystem.Action<TagState>>((System.Delegate)sysAct); m.ModifyTag("CALORIE_VALUE_TAG", il2cppAct, false); } catch { }
            }
        }
        catch { }
    }

    public static void PostfixCreateItemTooltip(RichTextBuilder builder, GameItem item)
    {
        try
        {
            if (!IsActive() || builder == null || item == null) return;
            if (IsFood(item))
            {
                int q = GetFoodQuality(item);
                if (q < 0) q = 0;
                string[] names = { LangHelper.T("新鲜", "Fresh"), LangHelper.T("正常", "Normal"), LangHelper.T("变质", "Spoiled"), LangHelper.T("腐烂", "Rotten") };
                int calLeft = GetCalLeft(item);
                int calFull = GetCalorie(item);
                string eaten = IsEaten(item) ? LangHelper.T("（已食用）", " (Eaten)") : "";
                builder.AddLine(LangHelper.T("品质：", "Quality: ") + names[Math.Min(3, Math.Max(0, q))] + eaten + LangHelper.T("（剩余 " + calLeft + "/" + calFull + " 卡）", " (" + calLeft + "/" + calFull + " kcal left)"),
                    true, (RenderHandler.ColorPalette)(-1), false, false, false, false, (RenderHandler.ColorPalette)(-1), (RenderHandler.ColorPalette)(-1), (RenderHandler.ColorPalette)(-1));
                string priceNote = "";
                if (q >= 3) priceNote = LangHelper.T("售价：-100%（腐烂）", "Sell: -100% (Rotten)");
                else if (q == 2) priceNote = LangHelper.T("售价：-90%（变质）", "Sell: -90% (Spoiled)");
                else if (IsEaten(item)) priceNote = LangHelper.T("售价：-80%（已食用）", "Sell: -80% (Eaten)");
                else if (q <= 0) priceNote = LangHelper.T("售价：+30%（新鲜）", "Sell: +30% (Fresh)");
                if (priceNote.Length > 0)
                    builder.AddLine(priceNote,
                        true, (RenderHandler.ColorPalette)(-1), false, false, false, false, (RenderHandler.ColorPalette)(-1), (RenderHandler.ColorPalette)(-1), (RenderHandler.ColorPalette)(-1));
                if (q == 2 || q >= 3)
                    builder.AddLine(LangHelper.T(q >= 3 ? "食用：40% 患病风险（卡路里按20%恢复）" : "食用：10% 患病风险（卡路里按50%恢复）", q >= 3 ? "Eating: 40% illness risk (calories at 20%)" : "Eating: 10% illness risk (calories at 50%)"),
                        true, (RenderHandler.ColorPalette)(-1), false, false, false, false, (RenderHandler.ColorPalette)(-1), (RenderHandler.ColorPalette)(-1), (RenderHandler.ColorPalette)(-1));
            }
            else if (IsMachine(item) || item.IsTag("MODULE_TAG"))
            {
                // 升级提示（用户拍板：显示在机器上储存区/机器箱子/模板）
                int pct = GetTagIntSafe(item, "wageUpgradePct");
                int effv = GetTagIntSafe(item, "wageUpgradeEff");
                if (pct > 0 || effv > 0)
                    builder.AddLine(LangHelper.T("◆ 升级：性能+" + pct + "% 效率+" + effv + "%（拖 metal_ingot +1%/次）", "◆ Upgrade: Perf +" + pct + "% Eff +" + effv + "% (drag metal_ingot +1%/each)"),
                        true, (RenderHandler.ColorPalette)(-1), false, false, false, false, (RenderHandler.ColorPalette)(-1), (RenderHandler.ColorPalette)(-1), (RenderHandler.ColorPalette)(-1));
                else
                    builder.AddLine(LangHelper.T("◆ 升级：拖 metal_ingot 到机器/模板 +1%/次（性能/效率/质量）", "◆ Upgrade: drag metal_ingot to machine/template +1%/each (Perf/Eff/Quality)"),
                        true, (RenderHandler.ColorPalette)(-1), false, false, false, false, (RenderHandler.ColorPalette)(-1), (RenderHandler.ColorPalette)(-1), (RenderHandler.ColorPalette)(-1));
                // 吞噬叠加记录（吞噬季：该模组吸收的属性累计——仅模组有 CANNIBALISM_* tag，机器读到 0 不显示）
                int cp = GetTagIntSafe(item, "CANNIBALISM_PERFORMANCE_INT");
                int ce = GetTagIntSafe(item, "CANNIBALISM_EFFICIENCY_INT");
                int cq = GetTagIntSafe(item, "CANNIBALISM_QUALITY_INT");
                int cv = GetTagIntSafe(item, "CANNIBALISM_VALUE");
                if (cp > 0 || ce > 0 || cq > 0 || cv > 0)
                    builder.AddLine(LangHelper.T("◆ 吞噬叠加：性能+" + cp + "% 效率+" + ce + "% 质量+" + cq + "% 价值+" + cv, "◆ Devoured: Perf +" + cp + "% Eff +" + ce + "% Qual +" + cq + "% Value +" + cv),
                        true, (RenderHandler.ColorPalette)(-1), false, false, false, false, (RenderHandler.ColorPalette)(-1), (RenderHandler.ColorPalette)(-1), (RenderHandler.ColorPalette)(-1));
            }
            else if (ContainerUpgradeV2.IsUpgradeableContainer(item))
            {
                int stage = ContainerUpgradeV2.GetTagIntSafe(item, "wb_stage");
                if (stage >= ContainerUpgradeV2.MAX_STAGE)
                    builder.AddLine(LangHelper.T("◆ 储存区：满级（容量×2）· 拖 junk 可正常放入", "◆ Storage: MAX (2× capacity) · drag junk to store"),
                        true, (RenderHandler.ColorPalette)(-1), false, false, false, false, (RenderHandler.ColorPalette)(-1), (RenderHandler.ColorPalette)(-1), (RenderHandler.ColorPalette)(-1));
                else
                    {
                        int progress = ContainerUpgradeV2.GetTagIntSafe(item, "wb_progress");
                        int need = ContainerUpgradeV2.UPGRADE_COSTS[Math.Min(stage, ContainerUpgradeV2.MAX_STAGE - 1)];
                        builder.AddLine(LangHelper.T("◆ 储存区：段位 " + stage + "/" + ContainerUpgradeV2.MAX_STAGE + " · 升级进度 " + progress + "/" + need + "（拖 junk 升级）", "◆ Storage: Stage " + stage + "/" + ContainerUpgradeV2.MAX_STAGE + " · progress " + progress + "/" + need + " (drag junk to upgrade)"),
                            true, (RenderHandler.ColorPalette)(-1), false, false, false, false, (RenderHandler.ColorPalette)(-1), (RenderHandler.ColorPalette)(-1), (RenderHandler.ColorPalette)(-1));
                    }
            }
            // 09-13 清洁系统 v1：日用品面板显示"双击恢复清洁"
            else if (IsDailyNeed(item))
            {
                string id = (item.identifier ?? "").ToLowerInvariant();
                if (DAILY_NEED_CLEAN.TryGetValue(id, out int _gain))
                    builder.AddLine(LangHelper.T("双击使用：清洁 +" + _gain, "Double-click: Cleanliness +" + _gain),
                        true, (RenderHandler.ColorPalette)(-1), false, false, false, false, (RenderHandler.ColorPalette)(-1), (RenderHandler.ColorPalette)(-1), (RenderHandler.ColorPalette)(-1));
            }
            // 09-13 双击使用类效果面板显示：饮品/零食/酒/麻醉品
            else if (IsBeverage(item))
                builder.AddLine(LangHelper.T("双击使用：饱食+10 口渴+15（消耗1件）", "Double-click: Satiety +10 Thirst +15 (consumed)"),
                    true, (RenderHandler.ColorPalette)(-1), false, false, false, false, (RenderHandler.ColorPalette)(-1), (RenderHandler.ColorPalette)(-1), (RenderHandler.ColorPalette)(-1));
            else if (IsSnack(item))
                builder.AddLine(LangHelper.T("双击使用：饱食 + 心情+10（消耗1件）", "Double-click: Satiety + Mood +10 (consumed)"),
                    true, (RenderHandler.ColorPalette)(-1), false, false, false, false, (RenderHandler.ColorPalette)(-1), (RenderHandler.ColorPalette)(-1), (RenderHandler.ColorPalette)(-1));
            else if (IsAlc(item) && !IsEmptyBottle(item))
                builder.AddLine(LangHelper.T("双击饮用：心情+15（消耗1件）", "Double-click drink: Mood +15 (consumed)"),
                    true, (RenderHandler.ColorPalette)(-1), false, false, false, false, (RenderHandler.ColorPalette)(-1), (RenderHandler.ColorPalette)(-1), (RenderHandler.ColorPalette)(-1));
            else if (IsNarcotic(item))
                builder.AddLine(LangHelper.T("双击使用：心情+20（消耗1件）", "Double-click: Mood +20 (consumed)"),
                    true, (RenderHandler.ColorPalette)(-1), false, false, false, false, (RenderHandler.ColorPalette)(-1), (RenderHandler.ColorPalette)(-1), (RenderHandler.ColorPalette)(-1));
        }
        catch { }
    }

    // ===== v5.8-8 觅食（店内翻找）：打烊结算概率，从食物池随机给背包，不依赖外出 =====
    private static readonly string[] FORAGE_FOODS = { "morsel", "small_morsel", "cat_bar", "processed_meat", "small_raw_meat", "raw_meat" };
    private static void ForageIndoor(int count, string tag)
    {
        try
        {
            for (int i = 0; i < count; i++)
            {
                string f = FORAGE_FOODS[UnityEngine.Random.Range(0, FORAGE_FOODS.Length)];
                if (DirectoryMaster.Has<GameItem>(f)) GiveToBackpack(f, 1);
            }
            try { StoreUIManager.Instance.Notify(LangHelper.T(tag + "：翻出食物 ×" + count, tag + ": found food x" + count), "green"); } catch { }
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] ForageIndoor 异常: " + ex.Message); }
    }

    // ===== 内部 =====
    private const string FOOD_DECAY_DAY_TAG = "WAGES_FOOD_DECAY_DAY";

    private static int GetFoodDecayDay(GameItem item)
    {
        if (item == null) return 0;
        try
        {
            if (item.IsTag(FOOD_DECAY_DAY_TAG))
            {
                var ts = item.GetTagReadonly(FOOD_DECAY_DAY_TAG);
                if (ts != null) return ts.GetInt();
            }
        }
        catch { }
        return StoreStation.GetDayCounter();
    }
    private static void SetFoodDecayDay(GameItem item, int day)
    {
        if (item == null) return;
        try
        {
            if (!item.IsTag(FOOD_DECAY_DAY_TAG)) item.EnableTag(FOOD_DECAY_DAY_TAG, true);
            System.Action<TagState> sysAct = delegate (TagState state) { state.SetInt(day); };
            var il2cppAct = DelegateSupport.ConvertDelegate<Il2CppSystem.Action<TagState>>((System.Delegate)sysAct);
            item.ModifyTag(FOOD_DECAY_DAY_TAG, il2cppAct, false);
        }
        catch { }
    }

    private static int DecayFoodsAndCount(out int fresh, out int stale, out int rotten)
    {
        fresh = 0; stale = 0; rotten = 0;
        try
        {
            int today = StoreStation.GetDayCounter();
            var items = CollectAllItems();
            foreach (GameItem item in items)
            {
                if (item == null || !IsFood(item)) continue;
                int q = GetFoodQuality(item);
                if (q < 0) q = 0;
                int last = GetFoodDecayDay(item);
                if (today - last >= 2 && q < 3) { q++; SetFoodQuality(item, q); SetFoodDecayDay(item, today); }
                if (q <= 0) fresh++;
                else if (q == 1) { stale++; fresh++; }
                else if (q == 2) stale++;
                else rotten++;
            }
        }
        catch { }
        return fresh + stale;
    }
    private static void TryInfect(double chance)
    {
        try
        {
            if (UnityEngine.Random.value < (float)chance)
            {
                SetHealth(Math.Max(0, GetHealth() - 40)); // v5.7 生病事件：健康-40（拆包回填1：原生无生病系统，mod 自建）
                RefreshStatusPanel(); // 生病实时刷新常驻面板（状态行 buff 跟随）
            }
        }
        catch { }
    }

    // ===== 清洁系统 v1（09-13 用户拍板）：日用品双击恢复清洁 + 物品消失 =====
    // 白名单按 id（toothpaste/toilet_paper/shampoo/paper_towel）；排除 pack_condom/box_tampon（不在表内自然不触发）
    private static readonly System.Collections.Generic.Dictionary<string, int> DAILY_NEED_CLEAN = new System.Collections.Generic.Dictionary<string, int>
    {
        { "toothpaste", BuildConfig.CleanToothpaste },
        { "toilet_paper", BuildConfig.CleanToiletPaper },
        { "shampoo", BuildConfig.CleanShampoo },
        { "paper_towel", BuildConfig.CleanPaperTowel },
    };
    internal static bool IsDailyNeed(GameItem item) // 09-21 改 internal：蛙娘喂食照顾共用判定
    {
        try { return item != null && DAILY_NEED_CLEAN.ContainsKey((item.identifier ?? "").ToLowerInvariant()); } catch { return false; }
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
        catch { }
    }

    private static bool TryExpel(GameItem item)
    {
        try
        {
            EmporiumEntry em = EmporiumEntry.Instance;
            if (em == null || item == null) return false;
            IntPtr targetPtr = item.Pointer;
            if (targetPtr == IntPtr.Zero) return false;
            // 1) 主背包 + 柜台（Pointer 比较，Il2Cpp 包装安全）
            foreach (GameInventory inv in new[] { (GameInventory)em.backInvinvElement, (GameInventory)em.frontInvinvElement })
            {
                if (inv == null || inv.childItems == null) continue;
                for (int i = 0; i < inv.childItems.Count; i++)
                {
                    var ci = inv.childItems[i];
                    if (ci != null && ci.Pointer == targetPtr) { inv.Expel(item); return true; }
                }
            }
            // 2) 递归容器内容库存（物品可能放在容器 UI 里双击食用）
            var visited = new HashSet<IntPtr>();
            var stack = new Stack<GameItem>();
            foreach (GameInventory inv in new[] { (GameInventory)em.backInvinvElement, (GameInventory)em.frontInvinvElement })
            {
                if (inv == null || inv.childItems == null) continue;
                for (int i = 0; i < inv.childItems.Count; i++)
                    if (inv.childItems[i] != null) stack.Push(inv.childItems[i]);
            }
            while (stack.Count > 0)
            {
                var c = stack.Pop();
                if (c == null || !visited.Add(c.Pointer)) continue;
                try
                {
                    var w = c.contentWindow;
                    if (w == null) continue;
                    var gi = (w.childElement != null) ? w.childElement.Cast<GameGridInventory>() : null;
                    if (gi == null || gi.childItems == null) continue;
                    for (int i = 0; i < gi.childItems.Count; i++)
                    {
                        var ci = gi.childItems[i];
                        if (ci == null) continue;
                        if (ci.Pointer == targetPtr) { gi.Expel(item); return true; }
                        stack.Push(ci);
                    }
                }
                catch { }
            }
            // 3) 兜底：原生销毁（吃完/喝完/用完=销毁，TryDestroyAll 签名为 List<GameItem>，MoreUpdate 拆包确认）
            try
            {
                var lst = new Il2CppSystem.Collections.Generic.List<GameItem>();
                lst.Add(item);
                GraphUtils.TryDestroyAll(lst);
                return true;
            }
            catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] TryDestroyAll 异常: " + ex.Message); }
            return false;
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] TryExpel 异常: " + ex.Message); return false; }
    }

    private static List<GameItem> CollectAllItems()
    {
        var result = new List<GameItem>();
        try
        {
            EmporiumEntry em = EmporiumEntry.Instance;
            if (em == null) return result;

            foreach (GameItem item in em.GetAllItems())
                if (item != null) result.Add(item);

            foreach (GameInventory inv in new[] { em.backInvinvElement, em.frontInvinvElement })
            {
                if (inv == null || inv.childItems == null) continue;
                for (int i = 0; i < inv.childItems.Count; i++)
                    if (inv.childItems[i] != null) result.Add(inv.childItems[i]);
            }

            var inner = new List<GameItem>(result);
            foreach (GameItem container in inner)
            {
                if (container == null) continue;
                try
                {
                    var w = container.contentWindow;
                    if (w == null) continue;
                    var gi = (w.childElement != null) ? w.childElement.Cast<GameGridInventory>() : null;
                    if (gi == null || gi.childItems == null) continue;
                    for (int i = 0; i < gi.childItems.Count; i++)
                        if (gi.childItems[i] != null) result.Add(gi.childItems[i]);
                }
                catch { }
            }
        }
        catch { }
        return result;
    }

    private static string GetId(GameItem item)
    {
        try { return (item.identifier ?? "").ToLowerInvariant(); }
        catch { return ""; }
    }
}
