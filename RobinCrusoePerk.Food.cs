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

    // 药品：MEDICAL 标签（覆盖 22 个含 injector 系）；标签失败回退关键词

    // ===== 心情主动提升判定（v5.7 + 拆包回填 4-7：酒/烟/毒/彩票 identifier）=====
    // 酒精（拆包回填4）：red_beer / nudka / 发酵酒全系（ALCOHOL_VALUE 标签兜底）
    private static readonly HashSet<string> ALC_IDS = new HashSet<string>
    { "red_beer", "nudka", "empty_beer_bottle" };
    // 烟草（拆包回填5）：cig_red（CIGARETTE_TAG 兜底）
    private static readonly HashSet<string> TOBACCO_IDS = new HashSet<string> { "cig_red", "cig_red_stack" };
    // 麻醉品（拆包回填6）：oxycodone_pill / dream_dust / injector 系（NARCOTIC 兜底）
    private static readonly HashSet<string> NARC_IDS = new HashSet<string>
    { "oxycodone_pill", "dream_dust", "dream_cap_extract", "injector", "injector_pink", "injector_pure_white_s", "black_injector" };
    // 彩票（拆包回填7）：scratch / scratch_stack（LOTTERY_TICKET_TAG 兜底）
    private static readonly HashSet<string> LOTTERY_IDS = new HashSet<string> { "scratch", "scratch_stack" };

    // ===== 食物品质 + 卡路里 =====
    // 卡路里：游戏原生标签 CALORIE_VALUE_TAG（实锤 [L1]），读不到按 300 默认
    // 剩余卡路里（吃一口扣一口）：有 WAGES_CAL_LEFT 用标签值，无 = 满量
    // 变质/腐烂卡路里折算（v4.1/v4.2 实锤）：变质×50%、腐烂×20%、其余×100%


    // 09-14 诊断（双击连吃定位）：每次点击打印双击判定状态（OnEventPress 后），定位"第二次双击是否判定成功"
    // 喝酒（v5.7 心情+15；拆包 09-10 自酿酒价值分档）：酒瓶有剩余才给心情；喝一口减 ml（修"酒剩余0还能无限喝"）
    // 自酿酒（wine_quality_homebrew 条件）按价值心情分档（用户拍板 09-10）：<50 +10 / 50-149 +15 / 150-299 +20 / 300-999 +30；
    // ≥1000 顶级自酿：额外 睡眠+25 + 出门连续3天不受伤 + 拾荒次数+1（PerkStatePersistence 存档，runID 隔离）

    // 顶级自酿 buff 生效中（喝后当天+次日+第三日，PerkStatePersistence 存档）

    // 自酿酒判定（拆包 09-10 [L1]：ItemFeature.realCondition.identifier == wine_quality_homebrew；类别 CATEGORY_WINE_QUALITY）

    // 吃一口（v5.7 百分比制）：摄入卡÷22 = 饱食%（2200cal=100%）；变质×50% / 腐烂×20% 计入 + 健康-10 + 患病判定；品质不回退；变「已食用」档

    // ===== 09-13 拍板：非水饮品双击恢复 饱食/口渴，喝完消耗 1 件 =====
    private static readonly System.Collections.Generic.HashSet<string> BEVERAGE_IDS = new System.Collections.Generic.HashSet<string>
    { "soda_red", "energy_drink", "galaxy_blend", "processed_milk", "processed_juice" }; // 09-13 用户反馈：碳酸代乳(processed_milk)/代糖果汁(processed_juice)也走饮品链（饱食+口渴）
    // 09-19 拆包：饮料卡路里 = InitFoodItem 写 CALORIE_VALUE_TAG（milk=600/juice=450/blend=450）；soda_red/energy_drink 走 TransformEdible 只写 add_thirst=350（原生无卡）→ 补 mod 基准 350
    // ===== 09-13 拍板：零食（猫咪巧克力棒/轻食能量棒/合成奶酪）吃恢复饱食 + 心情+10 =====
    private static readonly System.Collections.Generic.HashSet<string> SNACK_IDS = new System.Collections.Generic.HashSet<string>
    { "cat_bar", "li_eat_snackbar", "processed_cheese" };
    // 空瓶：不消耗（装水用，09-13 拍板）

    // 喝水（09-11 用户拍板 5 档真实水质）：purity 分 5 档，每档独立 口渴/健康/患病/清洁；Remove 单位=ml（拆包实锤，修复 sip*1000 误删全瓶）

    // ===== 09-19 用户拍板：麻醉品按价值档位加睡眠（参考健康分档减半；药效倍率同享）=====
    // 麻醉品双击 = 心情 + 睡眠（<50 +15 / 50-149 +30 / 150-299 +45 / ≥300 +60）+ 消失

    // 药品：按价值分档恢复健康（用户拍板 09-10：健康上限100，高档药不溢出——<50 +30 / 50-149 +60 / 150-299 +90 / ≥300 +100 回满）
    // 口径：基础价值 unitBaseValue（稳定，不受加价/事件影响）；健康满 100 不消耗

    // ===== 自动喝水按质生效（09-11 用户拍板：与双击同 5 档；return false 接管原版"补口渴+减水"，仿原版 AutoSipFromContainer 逻辑）=====

    // ===== 博士廉价模组供货（养蛊机系统 09-15：30 天起每 3 天 3-5 个 overclock/ruined/corrupt，60% 价）=====
    private static int _doctorSupplyDay = -1;

    // ===== 物品面板 tooltip =====
    // 09-18 原生满卡虚高修复（拆包 B 方案）：吃过的食物原生行显示剩余卡——Prefix 临时改 CALORIE_VALUE_TAG 为剩余值，Postfix 恢复（外层 mod 行读回满卡，顺序安全）
    private static int _foodCalBackup = 0;
    private static bool _foodCalBackupValid = false;
    // ===== 09-18 喂食器按满卡算修复（拆包实锤）：b__3 搅拌转化前把吃过的食物 CALORIE_VALUE_TAG 改为剩余卡，b__3 原样按剩余转（食物随后被移除无需恢复） =====


    // ===== v5.8-8 觅食（店内翻找）：打烊结算概率，从食物池随机给背包，不依赖外出 =====
    private static readonly string[] FORAGE_FOODS = { "morsel", "small_morsel", "cat_bar", "processed_meat", "small_raw_meat", "raw_meat" };

    // ===== 内部 =====
    private const string FOOD_DECAY_DAY_TAG = "WAGES_FOOD_DECAY_DAY";



    // ===== 清洁系统 v1（09-13 用户拍板）：日用品双击恢复清洁 + 物品消失 =====
    // 白名单按 id（toothpaste/toilet_paper/shampoo/paper_towel）；排除 pack_condom/box_tampon（不在表内自然不触发）
    private static readonly System.Collections.Generic.Dictionary<string, int> DAILY_NEED_CLEAN = new System.Collections.Generic.Dictionary<string, int>
    {
        { "toothpaste", BuildConfig.CleanToothpaste },
        { "toilet_paper", BuildConfig.CleanToiletPaper },
        { "shampoo", BuildConfig.CleanShampoo },
        { "paper_towel", BuildConfig.CleanPaperTowel },
    };



}
