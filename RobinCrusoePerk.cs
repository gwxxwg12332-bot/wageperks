using System;
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
internal static class RobinCrusoePerk
{
    internal const string PERK_ID = "RobinCrusoe";
    internal const int START_TYPE = 14;
    internal const string FOOD_Q_TAG = "WAGES_FOOD_Q";   // 0鲜 1正常 2变质 3腐烂；-1无
    internal const string EATEN_TAG = "WAGES_EATEN";      // 食用过标记（吃了一口就标，卖出-80%）
    internal const string CAL_LEFT_TAG = "WAGES_CAL_LEFT"; // 食物剩余卡路里（吃一口扣一口，不整份消失）

    
    internal const int DAILY_CAL = 2200;            // 1 单位 = 1 天需求（v4.2 拆包实锤）
    internal const int UNIT_CAL = 2200;             // 1 单位卡路里
    internal const int NORMAL_MAX_UNIT = 5;         // 常态上限（5 单位）
    internal const int GRANARY_UNIT = 7;            // 粮仓充盈（7 单位 = 15400 卡）
    internal static int SIP_ML => BuildConfig.SipMl;                // 一口水 = 200ml（CFG 可调）
    internal const int BOTTLE_ML = 1000;            // 开局大瓶纯水容量 1000ml

    // ===== v5.8-8 节点效果池（最终锁定：六状态独立节点 + 主导节点 + 池子抽1锁定）=====
    // 效果 id 约定：client-2/client-1(客流) noOutside(禁外出) noScav(禁拾荒)
    //   sell±N(售价) bargain±N(议价) budget±N(预算) scav±N(拾荒次数)
    //   satD±N(饱食衰减) thD±N(口渴衰减) hD±N(健康衰减) hR±N(健康恢复)
    //   cleanD±N(清洁衰减) cleanR±N(清洁恢复) sleepR±N(睡眠恢复) socD±N(社交衰减) socR±N(社交恢复)
    //   wound±N(受伤概率) sick±N(患病概率) mood±N(心情) drop±N(掉率) healmood 转机；flavor=叙事转机
    internal const int NODE_NONE = -1;
    internal const int NODE_STARVING = 0, NODE_BELLY = 1, NODE_FED = 2;
    internal const int NODE_THIRSTY = 3, NODE_DRYMOUTH = 4, NODE_HYDRATED = 5;
    internal const int NODE_BEDRIDDEN = 6, NODE_SICKLY = 7, NODE_ROBUST = 8;
    internal const int NODE_DISHEVELED = 9, NODE_GRIMY = 10, NODE_SPOTLESS = 11;
    internal const int NODE_HEAVYEYES = 12, NODE_YAWNING = 13, NODE_RESTED = 14;
    internal const int NODE_DESERTED = 15, NODE_COLDSHOULDER = 16, NODE_SOCIABLE = 17;
    internal const int NODE_LISTLESS = 18, NODE_BROKEN = 19;

    internal sealed class NodeDef
    {
        public string Key, Name, Tag;
        public string NameEn, TagEn;   // v5.10 英语适配：游戏英文环境显示英文名/英文播报句（延迟求值，规避静态初始化时机）
        public int Sev;
        internal string DisplayName => LangHelper.IsEnglish() ? (NameEn ?? Name) : Name;
        internal string DisplayTag => LangHelper.IsEnglish() ? (TagEn ?? Tag) : Tag;
        public bool IsPositive;      // 正面节点：全良性池抽1、无爆发/恶性（v5.9 不被重构误伤）
        public string[] Lock;        // 基础锁定效果（节点自带，必然生效；负面=恶性 / 正面=增益）
        public string[] Pool;        // 抽取池（打烊进入节点随机抽 1、锁定；负面=纯恶性 / 正面=全良性）
        public string[] BurstPunish; // 爆发惩罚（负面节点进节点当天打烊触发 1 次；动钱/动货/收入%）
        public string CompBuff;      // 补偿 buff（负面节点进节点当天自动获得，Duration 制；良性确定性补偿）
        public int CompBuffDur;      // 补偿 buff 持续天数（2-3 天）
        public NodeDef(string key, string name, string tag, int sev, bool positive, string[] lockFx, string[] poolFx,
            string[] burstPunish = null, string compBuff = null, int compBuffDur = 0, string enName = null, string enTag = null)
        { Key = key; Name = name; Tag = tag; NameEn = enName; TagEn = enTag; Sev = sev; IsPositive = positive; Lock = lockFx; Pool = poolFx;
          BurstPunish = burstPunish; CompBuff = compBuff; CompBuffDur = compBuffDur; }
    }
    // v5.9 节点定义（P0 修正：良性移出池子=爆发确定性补偿；负面池=纯恶性抽1；正面维持全良性池）
    // severity 主导排序：饿疯/嗓子冒烟/躺板板 10 > 破罐破摔 9 > 蓬头垢面/眼皮千斤 8 >
    //   肚里打鼓/口干舌燥/病恹恹 7 > 门可罗雀/提不起劲 6 > 灰头土脸/哈欠连天 5 > 爱答不理 4 > 正面 1
    internal static readonly NodeDef[] NODES =
    {
        new NodeDef("starving",   LangHelper.T("饿疯", "Starving"),     LangHelper.T("翻出半块硬饼", "Found half a stale biscuit"),   10, false,
            new[]{"client-2","noOutside"}, new[]{"sick+20","lostItem","bargain-20"},
            new[]{"lostItem","sat+30"}, "eatEff", 2, enName: "Ravenous", enTag: "Found half a stale biscuit"),   // 爆发：抢食货架1份→饱食+30；补偿：饿狼代谢 吃食物+50%
        new NodeDef("belly",      LangHelper.T("肚里打鼓", "Belly Growling"), LangHelper.T("今天总算没饿晕", "At least I didn't pass out today"),  7,  false,
            new[]{"client-1"}, new[]{"sell-5","scav-1","thD+5","sick+10"},
            new[]{"income-20"}, "sell5", 2, enName: "Belly Rumbling", enTag: "Didn't pass out today"),            // 爆发：算错价 收入-20%；补偿：精打细算 卖出+5%
        new NodeDef("fed",        LangHelper.T("吃饱喝足", "Well-Fed"), LangHelper.T("今天状态真好", "Feeling great today"),    1,  true,
            new[]{"sell+5"}, new[]{"mood+3","bargain+5","flavor"}, enName: "Well-fed", enTag: "Feeling great today"),
        new NodeDef("thirsty",    LangHelper.T("嗓子冒烟", "Parched"), LangHelper.T("找见半瓶浑水", "Found half a bottle of murky water"),    10, false,
            new[]{"client-2","noOutside"}, new[]{"hD+10","satD+10","lostWaterItem"},
            new[]{"health-15"}, "thirstEff50", 2, enName: "Parched", enTag: "Found half a bottle of murky water"),      // 爆发：误喝脏水 健康-15；补偿：耐旱体质 口渴衰减-50%
        new NodeDef("drymouth",   LangHelper.T("口干舌燥", "Dry Mouth"), LangHelper.T("今天水还没断", "Still have water today"),    7,  false,
            new[]{"client-1"}, new[]{"sell-5","hR-5","cleanD+5","bargain-10"},
            new[]{"income-20"}, "thirstEff10", 2, enName: "Dry Mouth", enTag: "Water supply still holding"),      // 爆发：高价买水 收入-20%；补偿：省水习惯 口渴衰减-10%
        new NodeDef("hydrated",   LangHelper.T("透心凉", "Quenched"),   LangHelper.T("精神焕发", "Refreshed"),        1,  true,
            new[]{"bargain+10"}, new[]{"hR+5","flavor"}, enName: "Refreshed", enTag: "Refreshed and sharp"),
        new NodeDef("bedridden",  LangHelper.T("躺板板", "Bedridden"),   LangHelper.T("撑过今天算一天", "Survived another day"),  10, false,
            new[]{"client-2","noOutside","noScav"}, new[]{"sell-15","wound+30","mood-10"},
            new[]{"mood-15","healChance50"}, "drugEff", 3, enName: "Bedridden", enTag: "One day at a time"), // 爆发：濒死幻视 心情-15 + 50%送药；补偿：回光返照 药效+50%
        new NodeDef("sickly",     LangHelper.T("病恹恹", "Sickly"),   LangHelper.T("今天没那么糟", "Not so bad today"),    7,  false,
            new[]{"client-1","noOutside","noScav"}, new[]{"hR-5","bargain-10"},
            new[]{"clientToday-50"}, "mood2", 2, enName: "Sickly", enTag: "Not so bad today"),       // 爆发：开店晕倒 当日客流-50%；补偿：病中专注 每日心情+2
        new NodeDef("robust",     LangHelper.T("壮得像驴", "Robust"), LangHelper.T("精力充沛", "Full of energy"),        1,  true,
            new[]{"scav+2"}, new[]{"wound-20","flavor"}, enName: "Strong as an Ox", enTag: "Full of energy"),
        new NodeDef("disheveled", LangHelper.T("蓬头垢面", "Disheveled"), LangHelper.T("擦了下柜台", "Wiped the counter"),      8,  false,
            new[]{"sell-30"}, new[]{"bargain-10","statusClient-30","mood-5","hD+5"},
            new[]{"income-20"}, "mood3", 2, enName: "Disheveled", enTag: "Wiped the counter"),            // 爆发：客人捂鼻 收入-20%；补偿：松弛自洽 每日心情+3
        new NodeDef("grimy",      LangHelper.T("灰头土脸", "Grimy"), LangHelper.T("凑合能开门", "Good enough to open"),      5,  false,
            new[]{"sell-15"}, new[]{"bargain-5","mood-3","cleanR-3","hD+3"},
            new[]{"lostItem"}, "wearEff", 2, enName: "Grimy", enTag: "Good enough to open"),           // 爆发：打包手滑 损失1件；补偿：糙人抗造 健康衰减-50%
        new NodeDef("spotless",   LangHelper.T("窗明几净", "Spotless"), LangHelper.T("宾至如归", "Customers feel at home"),        1,  true,
            new[]{"sell+5"}, new[]{"budget+5","flavor"}, enName: "Spotless", enTag: "Come on in"),
        new NodeDef("heavyeyes",  LangHelper.T("眼皮千斤", "Heavy Eyes"), LangHelper.T("趴在柜台上打盹", "Dozing on the counter"),  8,  false,
            new[]{"bargain-20","noScav"}, new[]{"mood-10","socD+5","hD+5"},
            new[]{"income-30"}, "antiTheft", 2, enName: "Heavy Eyes", enTag: "Dozing at the counter"),        // 爆发：被小偷摸走 收入-30%；补偿：失眠警觉 偷窃-50%
        new NodeDef("yawning",    LangHelper.T("哈欠连天", "Yawning"), LangHelper.T("今天还能撑", "Still hanging in there"),      5,  false,
            new[]{"bargain-10","scav-1"}, new[]{"mood-5","sleepR-10","hD+3"},
            new[]{"income-10"}, "sleepR10", 2, enName: "Yawning", enTag: "Still hanging in there"),         // 爆发：账本看串行 收入-10%；补偿：补觉高效 睡眠恢复+10%
        new NodeDef("rested",     LangHelper.T("精神抖擞", "Rested"), LangHelper.T("状态在线", "In top form"),        1,  true,
            new[]{"bargain+10"}, new[]{"scav+1","flavor"}, enName: "Well-rested", enTag: "In top form"),
        new NodeDef("deserted",   LangHelper.T("门可罗雀", "Deserted"), LangHelper.T("总算没把客人赶跑", "At least didn't scare customers away"),6,  false,
            new[]{"bargain-15"}, new[]{"budget-10","mood-5","socR-3","statusClient-20"},
            new[]{"lostItem"}, "forage20", 2, enName: "Deserted", enTag: "Didn't scare anyone off"),          // 爆发：打烊发呆 损失1件；补偿：独狼专注 觅食+20%
        new NodeDef("coldshoulder",LangHelper.T("爱答不理", "Cold Shoulder"),LangHelper.T("今天还算正常", "A normal enough day"),    4,  false,
            new[]{"bargain-5"}, new[]{"budget-5","mood-3","socR-2","statusClient-10"},
            new[]{"income-10"}, "mood2", 2, enName: "Cold Shoulder", enTag: "Business as usual"),            // 爆发：冷淡脸 收入-10%；补偿：清静自处 每日心情+2
        new NodeDef("sociable",   LangHelper.T("宾至如归", "Sociable"), LangHelper.T("生意兴隆", "Business booming"),        1,  true,
            new[]{"bargain+10"}, new[]{"budget+5","flavor"}, enName: "Sociable", enTag: "Business booming"),
        new NodeDef("listless",   LangHelper.T("提不起劲", "Listless"), LangHelper.T("今天总算没更糟", "At least it's no worse"),  6,  false,
            new[]{"budget-5","bargain-5"}, new[]{"scav-1","wound+10","mood-3"},
            new[]{"income-10"}, "moodDamp", 2, enName: "Listless", enTag: "Could've been worse"),         // 爆发：摆烂一天 收入-10%；补偿：摆烂反弹 心情掉速减半
        new NodeDef("broken",     LangHelper.T("破罐破摔", "Broken"), LangHelper.T("撑过今天明天翻盘", "Survive today, bounce back tomorrow"),9,  false,
            new[]{"budget-15","bargain-15"}, new[]{"scav-2","wound+20","mood-5","drop-20"},
            new[]{"lostItem","moodEncChance30"}, "contraEff", 3, enName: "Broken", enTag: "Suffer today, bounce back tomorrow"), // 爆发：砸坏1件 + 30%自我消化；补偿：豁出去了 违禁品+50%
    };

    // 新三状态（用户拍板 09-09：清洁度/睡眠/社交）
    internal static int CLEAN_START => BuildConfig.CleanStart;      // 清洁度初始（CFG 可调）
    internal static int SLEEP_START => BuildConfig.SleepStart;      // 睡眠初始（CFG 可调）
    internal static int SOCIAL_START => BuildConfig.SocialStart;      // 社交初始（CFG 可调）
    internal static int DAILY_CLEAN_LOSS => BuildConfig.CleanDailyLoss;   // 清洁每日衰减（CFG 可调）
    internal static int DAILY_SLEEP_GAIN => BuildConfig.DailySleepGain;  // 睡眠打烊（CFG 可调）
    internal static int SLEEP_SCAV_LOSS => BuildConfig.SleepScavLoss;    // 外出拾荒睡眠 -%（CFG 可调）
    internal static int DAILY_SOCIAL_GAIN => BuildConfig.DailySocialGain;  // 社交每日 +（开店接待，CFG 可调）
    internal static int DAILY_SOCIAL_LOSS => BuildConfig.DailySocialLoss;  // 社交每日 -（独处，CFG 可调）

    // 绝境良性 buff（暗黑地牢式，非性格）：绝境节点自带正面补偿，效果内联在对应方法（觅食/恢复/药效），无长期状态
    // 饿疯了→觅食+30%（PostfixGetRandomScavengedItem）；饥饿→觅食+15%；虚弱→每日恢复+10%（PostfixOnNewDay）；
    // 病危→药效+50%（TreatWithMedicine）；状态饱满→售价+5% 拾荒+1 议价+5%（GetSellBonusPct/GetMoodScavBonus/GetBargainBonusPct）

    // 心情档位（v5.7 心情值替代精神 5 档）
    internal const int MOOD_HIGH = 0;    // ≥80
    internal const int MOOD_NORMAL = 1;  // 60-79
    internal const int MOOD_LOW = 2;     // 40-59
    internal const int MOOD_CRIT = 3;    // <40

    // 三状态阈值（v5.7）
    internal const int SATIETY_GOOD = 80;      // 饱食良好线
    internal const int THIRST_GOOD = 80;       // 口渴良好线
    internal const int HEALTH_GOOD = 80;       // 健康良好线
    internal const int NODE_BAD = 50;          // 节点分界线
    internal const int NODE_CRIT = 20;         // 濒危分界线
    internal static int MOOD_START => BuildConfig.MoodStart;        // 心情初始值（CFG 可调）
    internal static int DAILY_SAT_LOSS => BuildConfig.DailySatLoss;    // 饱食每日 -%（CFG 可调）
    internal static int DAILY_THIRST_LOSS => BuildConfig.DailyThirstLoss; // 口渴每日 -%（CFG 可调）
    internal static int DAILY_HEALTH_GAIN => BuildConfig.DailyHealthGain; // 健康每日 +%（CFG 可调）
    internal static int GRANARY_DAYS => BuildConfig.GranaryDays;       // 粮仓连续天数（CFG 可调）
    internal static int ELEV_EVERY => BuildConfig.ElevEvery;         // 昂扬结算间隔（CFG 可调）
    internal static int ELEV_MAX => BuildConfig.ElevMax;           // 昂扬累计封顶（CFG 可调）
    internal static int MOOD_UP => BuildConfig.MoodUp;            // 三项全好每日+（CFG 可调）
    internal static int MOOD_DOWN => BuildConfig.MoodDown;         // 任一项低每日-（CFG 可调）

    // 状态客户 identifier（cheatsheet 2.3.12 实锤 + 工厂打标补充）
    private static readonly HashSet<string> STATUS_CLIENT_IDS = new HashSet<string>
    {
        "thirstyspacer", "hungryspacer", "spacerchef", "spacermedical", "sickchildcaretaker",
        "desperate", "wornout", "sicklowers"
    };

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
    };

    // ===== 三状态百分比制 + 心情（v5.7，PerkStatePersistence，runID 隔离）=====
    internal static int GetSatiety() => PerkStatePersistence.GetInt(PERK_ID, "sat", 100);          // 饱食 0-100
    internal static int GetThirstPct() => PerkStatePersistence.GetInt(PERK_ID, "thirst", 100);    // 口渴 0-100
    internal static int GetHealth() => PerkStatePersistence.GetInt(PERK_ID, "health", 100);       // 健康 0-100
    internal static int GetMood() => PerkStatePersistence.GetInt(PERK_ID, "mood", MOOD_START);    // 心情 0-100
    internal static int GetGranaryDays() => PerkStatePersistence.GetInt(PERK_ID, "granary", 0);   // 粮仓连续天数
    internal static int GetElevStreak() => PerkStatePersistence.GetInt(PERK_ID, "elevStreak", 0); // 昂扬连续天数
    internal static int GetElevCount() => PerkStatePersistence.GetInt(PERK_ID, "elevCount", 0);   // 昂扬累计（封顶5）
    internal static int GetStarveDays() => PerkStatePersistence.GetInt(PERK_ID, "starveDays", 0); // 濒饿持续（饱食<20或口渴<20）
    internal static int GetCritDays() => PerkStatePersistence.GetInt(PERK_ID, "critDays", 0);     // 病危持续（健康<20）
    internal static int GetThirstDeathDays() => PerkStatePersistence.GetInt(PERK_ID, "thirstDeath", 0); // 渴死持续（口渴<20）
    internal static void SetSatiety(int v) { PerkStatePersistence.SetInt(PERK_ID, "sat", v); InvalidateTradeCaches(); }
    internal static void SetThirstPct(int v) { PerkStatePersistence.SetInt(PERK_ID, "thirst", v); InvalidateTradeCaches(); }
    internal static void SetHealth(int v) { PerkStatePersistence.SetInt(PERK_ID, "health", v); InvalidateTradeCaches(); }
    internal static void SetMood(int v) { PerkStatePersistence.SetInt(PERK_ID, "mood", v); InvalidateTradeCaches(); }
    // 新三状态（v5.7+ 用户拍板：清洁度/睡眠/社交）
    internal static int GetClean() => PerkStatePersistence.GetInt(PERK_ID, "clean", CLEAN_START);       // 清洁 0-100
    internal static int GetSleep() => PerkStatePersistence.GetInt(PERK_ID, "sleep", SLEEP_START);       // 睡眠 0-100
    internal static int GetSocial() => PerkStatePersistence.GetInt(PERK_ID, "social", SOCIAL_START);    // 社交 0-100
    internal static void SetClean(int v) { PerkStatePersistence.SetInt(PERK_ID, "clean", v); InvalidateTradeCaches(); }
    internal static void SetSleep(int v) { PerkStatePersistence.SetInt(PERK_ID, "sleep", v); InvalidateTradeCaches(); }
    internal static void SetSocial(int v) { PerkStatePersistence.SetInt(PERK_ID, "social", v); InvalidateTradeCaches(); }

    // ===== Z 键调出/关闭状态面板（用户拍板；特性界面/主菜单不响应，硬约束守护）=====
    // 09-12 实锤：InputActionManager.Update 每帧可被多次调用（多实例/多Patch）→ 必须同帧去重，否则一次按键开→关双翻转，面板打不开
    private static int _zKeyFrame = -1;
    internal static void HandleHotkeys()
    {
        try
        {
            if (!UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Z)) return;
            int _zFrame = UnityEngine.Time.frameCount;
            if (_zFrame == _zKeyFrame) return; // 同帧已处理（去重，防双钩子/双实例重复翻转）
            _zKeyFrame = _zFrame;
            if (!IsActive()) return;
            if (Il2Cpp.EmporiumEntry.Instance == null) return; // 主菜单/未进档：纯读判空（PlayerStore getter null 时新建，禁用）
            var mgr = Il2Cpp.CustomUIManager.Instance;
            if (mgr == null) {  return; }
            if (mgr.IsOpen("rc_status")) {  mgr.CloseWindow("rc_status"); }
            else {  RefreshStatusPanel(); }
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] HandleHotkeys 异常: " + ex.Message); }
    }

    // ===== 激活判定（职业，双来源）=====
    internal static bool IsActive()
    {
        try
        {
            var ps = PlayerStore.Instance;
            if (ps != null && (int)ps.startType == START_TYPE) return true;
            var ng = Il2Cpp.NewGameData.Instance;
            if (ng != null && (int)ng.startType == START_TYPE) return true;
        }
        catch { }
        return false;
    }

    // ===== v5.8-8 节点系统：六状态独立档位 → 主导节点（多节点取最严重）=====
    private static int SatietyNode()
    {
        int s = GetSatiety();
        if (s < NODE_CRIT) return NODE_STARVING;
        if (s < NODE_BAD) return NODE_BELLY;
        if (s >= SATIETY_GOOD) return NODE_FED;
        return NODE_NONE;
    }
    private static int ThirstNode()
    {
        int t = GetThirstPct();
        if (t < NODE_CRIT) return NODE_THIRSTY;
        if (t < NODE_BAD) return NODE_DRYMOUTH;
        if (t >= THIRST_GOOD) return NODE_HYDRATED;
        return NODE_NONE;
    }
    private static int HealthNode()
    {
        int h = GetHealth();
        if (h < NODE_CRIT) return NODE_BEDRIDDEN;
        if (h < 40) return NODE_SICKLY;
        if (h >= HEALTH_GOOD) return NODE_ROBUST;
        return NODE_NONE;
    }
    private static int CleanNode()
    {
        int c = GetClean();
        if (c < 20) return NODE_DISHEVELED;
        if (c < 50) return NODE_GRIMY;
        if (c >= 80) return NODE_SPOTLESS;
        return NODE_NONE;
    }
    private static int SleepNode()
    {
        int s = GetSleep();
        if (s < 20) return NODE_HEAVYEYES;
        if (s < 50) return NODE_YAWNING;
        if (s >= 80) return NODE_RESTED;
        return NODE_NONE;
    }
    private static int SocialNode()
    {
        int s = GetSocial();
        if (s < 30) return NODE_DESERTED;
        if (s < 50) return NODE_COLDSHOULDER;
        if (s >= 80) return NODE_SOCIABLE;
        return NODE_NONE;
    }
    private static int MoodNode()
    {
        int m = GetMood();
        if (m < 40) return NODE_BROKEN;
        if (m < 60) return NODE_LISTLESS;
        return NODE_NONE;
    }
    // 主导节点：同时触发的节点中 severity 最高者（多节点不叠加，取最严重）
    internal static int GetDominantNode()
    {
        int best = NODE_NONE, bestSev = 0;
        int[] cand = { SatietyNode(), ThirstNode(), HealthNode(), CleanNode(), SleepNode(), SocialNode(), MoodNode() };
        foreach (int n in cand)
        {
            if (n < 0 || n >= NODES.Length) continue;
            int s = NODES[n].Sev;
            if (s > bestSev) { bestSev = s; best = n; }
        }
        return best;
    }
    // 当前锁定节点（打烊进入时锁定，节点期不变；PerkStatePersistence runID 隔离）
    internal static string GetStoredNodeKey() => PerkStatePersistence.GetString(PERK_ID, "nodeKey", "");
    internal static int GetNodeFxIdx() => PerkStatePersistence.GetInt(PERK_ID, "nodeFxIdx", -1);
    internal static string GetNodeFx()
    {
        int i = GetNodeFxIdx();
        NodeDef d = CurrentNode();
        return d != null && i >= 0 && i < d.Pool.Length ? d.Pool[i] : "";
    }
    private static NodeDef CurrentNode()
    {
        string key = GetStoredNodeKey();
        if (string.IsNullOrEmpty(key)) return null;
        foreach (NodeDef d in NODES) if (d.Key == key) return d;
        return null;
    }
    // 打烊调用：主导节点变化（进入/离开）→ 从该节点池随机抽 1 条并锁定；仍在同一节点 → 保持锁定
    // v5.9：nodeKey 变化当次先触发爆发事件（RollBurst：惩罚 + CompBuff 挂载/刷新），再抽池子（负面=纯恶性 / 正面=全良性）
    internal static void RollNodeFx()
    {
        int dom = GetDominantNode();
        string key = dom >= 0 ? NODES[dom].Key : "";
        if (key == GetStoredNodeKey()) return;   // 节点未变：锁定保持，不重抽、不爆发
        PerkStatePersistence.SetString(PERK_ID, "nodeKey", key);
        if (dom >= 0 && NODES[dom].Pool.Length > 0)
        {
            NodeDef d = NODES[dom];
            if (!d.IsPositive) RollBurst(d);     // v5.9：进负面节点当天触发爆发（惩罚+CompBuff）
            int idx = UnityEngine.Random.Range(0, d.Pool.Length);
            PerkStatePersistence.SetInt(PERK_ID, "nodeFxIdx", idx);
        }
        else PerkStatePersistence.SetInt(PERK_ID, "nodeFxIdx", -1);
    }
    // ===== v5.9 爆发事件（进负面节点当天打烊 1 次：大惩罚 + CompBuff 确定性补偿；同一节点停留多天不重复）=====
    private static void RollBurst(NodeDef d)
    {
        try
        {
            if (d == null || d.IsPositive) return;
            string desc = "";
            if (d.BurstPunish != null)
            {
                foreach (string p in d.BurstPunish)
                {
                    if (p == "lostItem") { int n = LostSmallItem(1); desc += n > 0 ? LangHelper.T("损失货物 ", "Lost goods ") : LangHelper.T("（无货可失）", "(nothing to lose)"); }
                    else if (p == "lostWaterItem") { LostWaterItem(); desc += LangHelper.T("水器损坏 ", "Water container damaged "); }
                    else if (p == "income-30" || p == "income-20" || p == "income-10") { int pct = Math.Abs(int.Parse(p.Substring(6))); int amt = ApplyIncomePunish(pct); desc += LangHelper.T("收入-" + pct + "%（-" + amt + "） ", "Income -" + pct + "% (-" + amt + ") "); }
                    else if (p == "health-15") { SetHealth(Math.Max(0, GetHealth() - 15)); desc += LangHelper.T("健康-15 ", "Health -15 "); }
                    else if (p == "sat+30") { SetSatiety(Math.Min(100, GetSatiety() + 30)); desc += LangHelper.T("饱食+30 ", "Satiety +30 "); }
                    else if (p == "mood-15") { SetMood(Math.Max(0, GetMood() - 15)); desc += LangHelper.T("心情-15 ", "Mood -15 "); }
                    else if (p == "clientToday-50") { _burstClientCut = 50; desc += LangHelper.T("今日客流-50% ", "Today's customers -50% "); }
                    else if (p == "healChance50")
                    {
                        if (UnityEngine.Random.value < 0.5f) { SetHealth(Math.Min(100, GetHealth() + 10)); desc += LangHelper.T("好心人送药+10 ", "Kind customer sends medicine +10 "); }
                    }
                    else if (p == "moodEncChance30")
                    {
                        if (UnityEngine.Random.value < 0.3f) { SetMood(Math.Min(100, GetMood() + 5)); desc += LangHelper.T("自我消化+5 ", "Self-soothe +5 "); }
                    }
                }
            }
            // CompBuff 挂载：同 buff 在身时刷新剩余天数、不叠加数值（P0 修正）
            if (!string.IsNullOrEmpty(d.CompBuff) && d.CompBuffDur > 0)
            {
                int cur = GetCompBuffDays(d.CompBuff);
                if (cur < d.CompBuffDur) SetCompBuffDays(d.CompBuff, d.CompBuffDur);
                string cbTxt = GetCompBuffLabel(d.CompBuff);
                desc += (cbTxt.Length > 0 ? cbTxt : d.CompBuff) + " " + d.CompBuffDur + LangHelper.T("天 ", "d ");
            }
            try { StoreUIManager.Instance.Notify(LangHelper.T("爆发·", "Burst·") + d.DisplayName + LangHelper.T("：", ": ") + desc.Trim(), "red"); } catch { }
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] RollBurst 异常: " + ex.Message); }
    }
    // 当日客流削减标记（病恹恹爆发：当日客流-50%，打烊结算后清零）
    internal static int _burstClientCut = 0;
    // ===== BUG-001 09-11：交易价格计算链缓存 =====
    // 卡顿根因（拆包实锤）：hover/批量转移每件物品算价 → GetCurrentValue Postfix → TryApplyTradeMarkup → GetTradeBuffDisplay/GetSellBonusPct 每次重建节点文本
    // 节点状态只在 Set*/打烊结算/抽取时变 → Set* 里失效缓存，价格计算链直接读缓存
    private static string _tradeBuffCache = null;   // 报价面板文本（null = 需重建）
    private static int _sellBonusCache = -999;      // 售价加成（-999 = 失效）
    private static int _budgetBonusCache = -999;    // 预算加成
    private static int _bargainBonusCache = -999;   // 议价加成
    internal static void InvalidateTradeCaches()
    {
        _tradeBuffCache = null;
        _sellBonusCache = -999;
        _budgetBonusCache = -999;
        _bargainBonusCache = -999;
        Patches.ClearNodeBuffItems(); // 面板 feature 防重集合一并清：新状态周期内所有物品重新刷新显示文本
    }

    // 效果数值查询（v5.8-8 修正：Lock 效果按当前节点实时聚合，不依赖打烊锁定——面板显示与实际生效永远一致）
    private static int FxVal(string fx, string prefix)
    {
        if (string.IsNullOrEmpty(fx) || !fx.StartsWith(prefix)) return 0;
        int n; return int.TryParse(fx.Substring(prefix.Length), out n) ? n : 0;
    }
    internal static int FxNum(string prefix)
    {
        int v = 0;
        foreach (int n in AllActiveNodes()) // Lock 基础效果：实时
        {
            if (n < 0 || n >= NODES.Length) continue;
            foreach (string f in NODES[n].Lock) v += FxVal(f, prefix);
        }
        NodeDef d = CurrentNode(); // 抽取项：仅当锁定节点仍为主导节点时生效（打烊抽、离开重抽）
        if (d != null)
        {
            int dom = GetDominantNode();
            if (dom >= 0 && d.Key == NODES[dom].Key)
            {
                string cur = GetNodeFx();
                if (!string.IsNullOrEmpty(cur)) v += FxVal(cur, prefix);
            }
        }
        return v;
    }
    internal static bool FxBool(string fx)
    {
        foreach (int n in AllActiveNodes()) // Lock 基础效果：实时
        {
            if (n < 0 || n >= NODES.Length) continue;
            if (Array.IndexOf(NODES[n].Lock, fx) >= 0) return true;
        }
        NodeDef d = CurrentNode(); // 抽取项
        if (d != null)
        {
            int dom = GetDominantNode();
            if (dom >= 0 && d.Key == NODES[dom].Key && GetNodeFx() == fx) return true;
        }
        return false;
    }
    // 当前所有激活节点（六状态 + 心情；中间档无节点）
    private static int[] AllActiveNodes()
    {
        return new int[] { SatietyNode(), ThirstNode(), HealthNode(), CleanNode(), SleepNode(), SocialNode(), MoodNode() };
    }
    // 报价面板节点 buff 文本（用户拍板 v2：只显示影响交易的效果 sell/bargain/budget + 末尾总百分比）
    internal static string GetTradeBuffDisplay()
    {
        try
        {
            if (_tradeBuffCache != null) return _tradeBuffCache; // BUG-001：缓存命中直接返回
            var sb = new System.Text.StringBuilder();
            foreach (int n in AllActiveNodes())
            {
                if (n < 0 || n >= NODES.Length) continue;
                NodeDef d = NODES[n];
                string fxDesc = "";
                foreach (string f in d.Lock) { if (IsTradeFx(f)) { string lb = FxLabel(f); if (lb.Length > 0) fxDesc += lb + " "; } }
                if (d.Key == GetStoredNodeKey()) // 主导节点追加抽取项（仅交易相关）
                {
                    string cur = GetNodeFx();
                    if (!string.IsNullOrEmpty(cur) && IsTradeFx(cur)) { string lb = FxLabel(cur); if (lb.Length > 0) fxDesc += lb + " "; }
                }
                if (fxDesc.Length == 0) continue;
                sb.Append(d.DisplayName + "·" + fxDesc.Trim() + " ");
            }
            // 末尾总百分比（与实际生效同源）
            int sellB = GetSellBonusPct(), bargainB = GetBargainBonusPct(), budgetB = GetBudgetBonusPct();
            string total = LangHelper.T("总：", "Total: ");
            if (sellB != 0) total += LangHelper.T("售价", "Sell ") + (sellB > 0 ? "+" : "") + sellB + "% ";
            if (bargainB != 0) total += LangHelper.T("议价", "Bargaining ") + (bargainB > 0 ? "+" : "") + bargainB + "% ";
            if (budgetB != 0) total += LangHelper.T("预算", "Budget ") + (budgetB > 0 ? "+" : "") + budgetB + "% ";
            if (total.Length > 2) sb.Append("｜" + total.Trim());
            _tradeBuffCache = sb.ToString().Trim(); // BUG-001：写缓存
            return _tradeBuffCache;
        }
        catch { return ""; }
    }
    // 交易相关效果才显示在报价面板（售价/议价/预算；客流/外出/拾荒/健康/心情等不显示）
    private static bool IsTradeFx(string fx)
    {
        return fx.StartsWith("sell") || fx.StartsWith("bargain") || fx.StartsWith("budget");
    }
    // 效果中文标签（面板/播报）
    private static string FxLabel(string fx)
    {
        if (string.IsNullOrEmpty(fx)) return "";
        switch (fx)
        {
            case "client-2": return LangHelper.T("客流-2", "Customers -2");
            case "client-1": return LangHelper.T("客流-1", "Customers -1");
            case "noOutside": return LangHelper.T("禁外出", "No Outside");
            case "noScav": return LangHelper.T("禁拾荒", "No Scavenging");
            case "flavor": return "";
            case "heal+10": return LangHelper.T("好心人送药", "Kind customer sends medicine");
            case "moodEnc+5": return LangHelper.T("路人鼓励", "Passerby cheers you up");
        }
        if (fx.StartsWith("sell")) return LangHelper.T("售价" + fx.Substring(4) + "%", "Sell " + fx.Substring(4) + "%");
        if (fx.StartsWith("bargain")) return LangHelper.T("议价" + fx.Substring(7) + "%", "Bargaining " + fx.Substring(7) + "%");
        if (fx.StartsWith("budget")) return LangHelper.T("预算" + fx.Substring(6) + "%", "Budget " + fx.Substring(6) + "%");
        if (fx.StartsWith("scav")) return LangHelper.T("拾荒" + fx.Substring(4) + "次", "Scavenging " + fx.Substring(4));
        if (fx.StartsWith("mood")) return LangHelper.T("心情" + fx.Substring(4), "Mood " + fx.Substring(4));
        if (fx.StartsWith("satD")) return LangHelper.T("饱食衰减" + fx.Substring(4) + "%", "Satiety loss " + fx.Substring(4) + "%");
        if (fx.StartsWith("thD")) return LangHelper.T("口渴衰减" + fx.Substring(3) + "%", "Thirst loss " + fx.Substring(3) + "%");
        if (fx.StartsWith("hD")) return LangHelper.T("健康衰减" + fx.Substring(2) + "%", "Health loss " + fx.Substring(2) + "%");
        if (fx.StartsWith("hR")) return LangHelper.T("健康恢复" + fx.Substring(2) + "%", "Health recovery " + fx.Substring(2) + "%");
        if (fx.StartsWith("cleanD")) return LangHelper.T("清洁衰减" + fx.Substring(6) + "%", "Cleanliness loss " + fx.Substring(6) + "%");
        if (fx.StartsWith("cleanR")) return LangHelper.T("清洁恢复" + fx.Substring(6) + "%", "Cleanliness recovery " + fx.Substring(6) + "%");
        if (fx.StartsWith("sleepR")) return LangHelper.T("睡眠恢复" + fx.Substring(6) + "%", "Sleep recovery " + fx.Substring(6) + "%");
        if (fx.StartsWith("socD")) return LangHelper.T("社交衰减" + fx.Substring(4) + "%", "Social loss " + fx.Substring(4) + "%");
        if (fx.StartsWith("socR")) return LangHelper.T("社交恢复" + fx.Substring(4) + "%", "Social recovery " + fx.Substring(4) + "%");
        if (fx.StartsWith("wound")) return LangHelper.T("受伤" + fx.Substring(5) + "%", "Injury " + fx.Substring(5) + "%");
        if (fx.StartsWith("sick")) return LangHelper.T("患病" + fx.Substring(4) + "%", "Illness " + fx.Substring(4) + "%");
        if (fx == "lostItem") return LangHelper.T("损失货物", "Lost goods");
        if (fx == "lostWaterItem") return LangHelper.T("水器损坏", "Water container damaged");
        if (fx.StartsWith("statusClient")) return LangHelper.T("状态客户-" + fx.Substring(13) + "%", "Customers from status -" + fx.Substring(13) + "%");
        if (fx.StartsWith("drop")) return LangHelper.T("掉落率" + fx.Substring(4) + "%", "Drop rate " + fx.Substring(4) + "%");
        return fx;
    }
    // 心情档位（v5.7 心情值替代精神 5 档）
    internal static int GetMoodTier()
    {
        int m = GetMood();
        if (m >= SATIETY_GOOD) return MOOD_HIGH;
        if (m >= 60) return MOOD_NORMAL;
        if (m >= 40) return MOOD_LOW;
        return MOOD_CRIT;
    }
    // 售价加成：粮仓 +5% + 昂扬累计 + 节点（sell±N，如蓬头垢面锁 sell-30 / 吃饱喝足锁 sell+5）
    internal static int GetSellBonusPct()
    {
        if (_sellBonusCache != -999) return _sellBonusCache; // BUG-001：缓存
        int bonus = 0;
        if (GetGranaryDays() >= GRANARY_DAYS) bonus += 5;
        bonus += Math.Min(ELEV_MAX, GetElevCount());
        bonus += FxNum("sell");
        bonus += (int)GetCompBuffSellBonus(); // v5.9 精打细算：卖出+5%（肚里打鼓爆发补偿）
        _sellBonusCache = bonus;
        return bonus;
    }
    // 客户预算：max(心情, 昂扬) + 节点负向（budget±N 叠加；正向取更高）
    internal static int GetBudgetBonusPct()
    {
        if (_budgetBonusCache != -999) return _budgetBonusCache; // BUG-001：缓存
        int moodB = 0, m = GetMood();
        if (m >= SATIETY_GOOD) moodB = 15;
        else if (m >= 40) moodB = -5;
        else moodB = -15;
        int elevB = Math.Min(25, GetElevCount() * 5);
        int baseB = Math.Max(moodB, elevB);
        int nodeB = FxNum("budget");
        _budgetBonusCache = nodeB < 0 ? baseB + nodeB : Math.Max(baseB, nodeB);
        return _budgetBonusCache;
    }
    // 客流削减（节点池：client-2 / client-1 锁定；病恹恹爆发当日另按 50% 隔一skip一）
    internal static int GetClientReduction() => -FxNum("client");
    internal static int GetBurstClientCut() => _burstClientCut;  // 病恹恹爆发：当日客流-50% 标记（打烊结算后清零）
    // 禁外出（节点池：noOutside 锁定）
    internal static bool IsForbiddenOutside() => FxBool("noOutside");
    // 禁拾荒（节点池：noScav 锁定）
    internal static bool IsForbiddenScavenge() => FxBool("noScav");
    // 拾荒次数：心情 ≥80 +2 / <40 -2 + 节点（scav±N，如哈欠锁 scav-1 / 壮得像驴锁 scav+2）
    internal static int GetMoodScavBonus()
    {
        int b = FxNum("scav");
        int t = GetMoodTier();
        if (t == MOOD_HIGH) b += 2;
        if (t == MOOD_CRIT) b -= 2;
        return b;
    }
    // 议价成功率：max(心情≥80+15, 社交≥80+10) + 节点（bargain±N）
    internal static int GetBargainBonusPct()
    {
        if (_bargainBonusCache != -999) return _bargainBonusCache; // BUG-001：缓存
        int socialB = GetSocial() >= 80 ? 10 : 0;
        int moodB = GetMood() >= 80 ? 15 : 0;
        int b = Math.Max(socialB, moodB);
        b += FxNum("bargain");
        _bargainBonusCache = b;
        return b;
    }
    internal static int GetMoodScavDropPct() // 拾荒掉落率：≥80 +20% / <40 -20% + 节点（drop-20 破罐破摔池恶）
    {
        int t = GetMoodTier();
        int v = t == MOOD_HIGH ? 20 : t == MOOD_CRIT ? -20 : 0;
        return v + FxNum("drop");
    }
    internal static int GetMoodWoundPct()    // 受伤几率：≥80 -20% / <40 +20% + 节点（wound±N）
    {
        int t = GetMoodTier();
        int v = t == MOOD_HIGH ? -20 : t == MOOD_CRIT ? 20 : 0;
        return v + FxNum("wound");
    }
    internal static int GetSickChanceAdd() => FxNum("sick");  // 患病概率（节点 sick±N，每日结算用）

    // ===== v5.9 CompBuff（补偿 buff，Duration 制：进负面节点当天自动获得、按天倒计时、到期移除、期间实时生效）=====
    internal static readonly string[] COMP_BUFF_KEYS = { "eatEff","thirstEff50","thirstEff10","wearEff","drugEff","antiTheft","moodDamp","forage20","sell5","mood2","mood3","sleepR10","contraEff" };
    internal static int GetCompBuffDays(string eff) => PerkStatePersistence.GetInt(PERK_ID, "cb_" + eff, 0);
    internal static void SetCompBuffDays(string eff, int days) { PerkStatePersistence.SetInt(PERK_ID, "cb_" + eff, days > 0 ? days : 0); InvalidateTradeCaches(); }
    internal static void TickCompBuffs() { foreach (string k in COMP_BUFF_KEYS) { int d = GetCompBuffDays(k); if (d > 0) SetCompBuffDays(k, d - 1); } }
    internal static double GetEatEffMult() => GetCompBuffDays("eatEff") > 0 ? 1.5 : 1.0;          // 饿狼代谢 吃食物+50%
    internal static double GetThirstEffMult() { if (GetCompBuffDays("thirstEff50") > 0) return 0.5; if (GetCompBuffDays("thirstEff10") > 0) return 0.9; return 1.0; } // 耐旱-50%/省水-10%
    internal static double GetWearEffMult() => GetCompBuffDays("wearEff") > 0 ? 0.5 : 1.0;        // 糙人抗造 健康衰减-50%
    internal static double GetDrugEffMult() => GetCompBuffDays("drugEff") > 0 ? 1.5 : 1.0;        // 回光返照 药效+50%
    internal static double GetMoodDampMult() => GetCompBuffDays("moodDamp") > 0 ? 0.5 : 1.0;       // 摆烂反弹 心情掉速减半
    internal static double GetContraEffMult() => GetCompBuffDays("contraEff") > 0 ? 1.5 : 1.0;     // 豁出去了 违禁品收益+50%
    // 违禁品判定（拆包 2.5.29：CONTRABAND_ITEM_TAG 权威，InitContrabandItem 写；麻醉品=违禁品子集）
    internal static bool IsContrabandItem(GameItem item)
    {
        try { if (ContrabandHelper.IsContraband(item)) return true; } catch { }
        try { if (item.IsTag("CONTRABAND")) return true; } catch { }
        try { if (item.IsTag("CONTRABAND_ITEM_TAG")) return true; } catch { }
        return false;
    }
    // ===== v5.9 失眠警觉：偷窃概率-50%（拆包 2.5.29：原生偷窃=PlayerStore.HandleInsurance，EndNight 链）=====
    public static bool PrefixHandleInsurance()
    {
        try { if (IsActive() && GetAntiTheftMult() < 1.0) {  return false; } } catch { }
        return true;
    }
    internal static double GetAntiTheftMult() => GetCompBuffDays("antiTheft") > 0 ? 0.5 : 1.0;     // 失眠警觉 偷窃概率-50%
    internal static int GetForageBonus() => GetCompBuffDays("forage20") > 0 ? 20 : 0;              // 独狼专注 觅食+20%
    internal static int GetCompBuffMoodBonus() { int v = 0; if (GetCompBuffDays("mood2") > 0) v += 2; if (GetCompBuffDays("mood3") > 0) v += 3; return v; } // 病中专注/松弛自洽/清静自处
    internal static double GetCompBuffSellBonus() => GetCompBuffDays("sell5") > 0 ? 5.0 : 0.0;     // 精打细算 卖出+5%
    internal static int GetCompBuffSleepRestore() => GetCompBuffDays("sleepR10") > 0 ? 10 : 0;     // 补觉高效 睡眠恢复+10%
    internal static string GetCompBuffLabel(string eff)
    {
        switch (eff)
        {
            case "eatEff": return LangHelper.T("饿狼代谢", "Ravenous Metabolism");
            case "thirstEff50": return LangHelper.T("耐旱体质", "Drought Resistance");
            case "thirstEff10": return LangHelper.T("省水习惯", "Water-Saving Habit");
            case "wearEff": return LangHelper.T("糙人抗造", "Tough Constitution");
            case "drugEff": return LangHelper.T("回光返照", "Last Gasp");
            case "antiTheft": return LangHelper.T("失眠警觉", "Insomniac Vigilance");
            case "moodDamp": return LangHelper.T("摆烂反弹", "Slacker Rebound");
            case "forage20": return LangHelper.T("独狼专注", "Lone Wolf Focus");
            case "sell5": return LangHelper.T("精打细算", "Penny Pincher");
            case "mood2": return LangHelper.T("心情+2", "Mood +2");
            case "mood3": return LangHelper.T("心情+3", "Mood +3");
            case "sleepR10": return LangHelper.T("补觉高效", "Efficient Napping");
            case "contraEff": return LangHelper.T("豁出去了", "Whatever It Takes");
        }
        return "";
    }
    // 爆发辅助：收入惩罚（按当日营业额 % 扣款，空营业额日=0；营业额=RecordRevenue 当日累计）
    internal static int ApplyIncomePunish(int pct)
    {
        try
        {
            int revenue = PerkStatePersistence.GetInt(PERK_ID, "revenue", 0);
            if (revenue <= 0 || pct <= 0) return 0;
            int amt = (int)(revenue * pct / 100.0);
            var ps = Il2Cpp.PlayerStore.Instance;
            if (ps != null && amt > 0)
            {
                int cash = Math.Max(0, ps.playerCash - amt);
                ps.playerCash = cash;
                PerkStatePersistence.SetInt(PERK_ID, "revenue", 0);
                return amt;
            }
        }
        catch { }
        return 0;
    }
    // 爆发辅助：损失 1 件小货物（白名单：食物/水（杂货主体）；不损工具/机器/容器/钥匙卡；AddictOfficer 同款 GetAllItems 遍历）
    internal static int LostSmallItem(int count)
    {
        try
        {
            var em = Il2Cpp.EmporiumEntry.Instance;
            if (em == null) return 0;
            var all = em.GetAllItems();
            if (all == null) return 0;
            int lost = 0;
            foreach (var it in all)
            {
                if (lost >= count) break;
                if (it == null) continue;
                string id = GetId(it);
                if (IsToolOrKeyOrContainer(it)) continue;       // 不损工具/钥匙/容器
                if (!IsFood(it) && !IsDrink(it)) continue;      // 白名单：食物/水
                try
                {
                    var inv = FindContainingInventory(it);
                    if (inv != null) { inv.Expel(it); lost++;  }
                }
                catch { }
            }
            return lost;
        }
        catch { return 0; }
    }
    // 爆发辅助：损失 1 件水/容器（嗓子冒烟爆发：打烊失手）
    private static void LostWaterItem()
    {
        try
        {
            var em = Il2Cpp.EmporiumEntry.Instance;
            if (em == null) return;
            var all = em.GetAllItems();
            if (all == null) return;
            foreach (var it in all)
            {
                if (it == null) continue;
                if (!IsDrink(it)) continue;
                try
                {
                    var inv = FindContainingInventory(it);
                    if (inv != null) { inv.Expel(it);  return; }
                }
                catch { }
            }
        }
        catch { }
    }
    // 找包含指定物品的库存（遍历店铺各库存容器 childItems）
    private static GameInventory FindContainingInventory(GameItem target)
    {
        try
        {
            var em = Il2Cpp.EmporiumEntry.Instance;
            if (em == null) return null;
            var invs = new Il2CppSystem.Collections.Generic.List<GameInventory>();
            try
            {
                if (em.frontInvinvElement != null) invs.Add(em.frontInvinvElement);
                if (em.backInvinvElement != null) invs.Add(em.backInvinvElement);
                if (em.hiddenElement != null) invs.Add(em.hiddenElement);
                if (em.afterhourInventory != null) invs.Add(em.afterhourInventory);
            }
            catch { }
            foreach (var inv in invs)
            {
                try
                {
                    if (inv == null || inv.childItems == null) continue;
                    for (int i = 0; i < inv.childItems.Count; i++)
                        if (inv.childItems[i] == target) return inv;
                }
                catch { }
            }
        }
        catch { }
        return null;
    }
    private static bool IsToolOrKeyOrContainer(GameItem item)
    {
        try
        {
            string id = GetId(item);
            if (item.IsTag("IMPORTANT_TAG")) return true;
            if (item.IsTag("MACHINE")) return true;
            if (item.IsTag("LOCKED")) return true;
            if (id != null && (id.Contains("storage") || id.Contains("key") || id.Contains("tool") || id.Contains("machine") || id.Contains("module"))) return true;
            return false;
        }
        catch { return false; }
    }
    private static bool IsGroceries(GameItem item)
    {
        try { return item.IsTag("GROCERY"); } catch { return false; }
    }
    private static bool IsRaw(GameItem item)
    {
        try { return item.IsTag("RAW"); } catch { return false; }
    }
    // 当日营业额累计（PostfixStoreClientOnDealAccepted BUY 分支调用；打烊收入惩罚后清零）
    internal static void RecordRevenue(int amount)
    {
        if (amount <= 0) return;
        PerkStatePersistence.SetInt(PERK_ID, "revenue", PerkStatePersistence.GetInt(PERK_ID, "revenue", 0) + amount);
    }
    internal static int GetTodayRevenue() => PerkStatePersistence.GetInt(PERK_ID, "revenue", 0);

    // ===== 物品判定 =====
    // 食物判定：硬编码清单 + 原生卡路里兜底（覆盖水培莓果/营养果/异种肉等遗漏）
    // 兜底规则：有 CALORIE_VALUE_TAG/CALORIE 标签 且 非饮品/药品/酒/种子 = 食物
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
    // 心情提升（上限 100）
    internal static void BoostMood(int amount, string reason)
    {
        try
        {
            int m = Math.Min(100, GetMood() + amount);
            SetMood(m);
            try { StoreUIManager.Instance.Notify(LangHelper.T("心情 +" + amount + "（" + reason + "）", "Mood +" + amount + " (" + reason + ")"), "yellow"); } catch { }
            RefreshStatusPanel(); // 心情实时刷新常驻面板
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] BoostMood 异常: " + ex.Message); }
    }

    // ===== v5.7 玩家常驻状态面板（拆包回填4：CustomUIManager overlay + 进度条，重建法最稳）=====
    // 饱食/口渴/健康进度条 + 具体数值（用户要求直观数值），心情与加成 label
    internal static void RefreshStatusPanel()
    {
        try
        {
            var mgr = Il2Cpp.CustomUIManager.Instance;
            if (mgr == null) return;
            if (mgr.IsOpen("rc_status")) mgr.CloseWindow("rc_status");
            var b = mgr.CreateWindow("rc_status", LangHelper.T("鲁滨逊 · 生存状态", "Robinson · Survival"), "overlay");
            if (b == null) return;
            int sat = GetSatiety(), th = GetThirstPct(), h = GetHealth();
            // 显示具体单位：饱食 100%=2200 kcal（v5.7 锁定）、口渴 100%=2000 ml（与喝水 200ml=10% 自洽）
            int satCal = (int)(sat * 22f);
            int thMl = (int)(th * 20f);
            b.SetSize(300, 500).SetPosition(Vector2.zero);
            // 固定右上角（09-10 用户拍板：锚点(1,1) pivot(1,1) 右上角内侧 16px，不随分辨率变化）
            try
            {
                var w = mgr.GetWindow("rc_status");
                if (w != null)
                {
                    var rt = w.Rect;
                    rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1, 1);
                    rt.anchoredPosition = new Vector2(-16, -16);
                }
            }
            catch { }
            b.BeginColumn(4f);
            b.AddLabel(LangHelper.T("饱食 ", "Satiety ") + satCal + "/2200 kcal", "sat_l");
            b.AddProgressBar(sat / 100f, "sat");
            b.AddLabel(LangHelper.T("口渴 ", "Thirst ") + thMl + "/2000 ml", "th_l");
            b.AddProgressBar(th / 100f, "th");
            b.AddLabel(LangHelper.T("健康 ", "Health ") + h + "/100", "h_l");
            b.AddProgressBar(h / 100f, "h");
            b.AddLabel(LangHelper.T("血量 ", "Blood ") + GetBlood() + "/6000", "blood_l");
            b.AddProgressBar(GetBlood() / (float)BLOOD_MAX, "blood");
            var sellBtnOnClick = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((System.Action)(() => { try { TrySellBlood(); } catch (Exception ex) { Core.LogMsg("[鲁滨逊] 面板卖血异常: " + ex.Message); } }));
            b.AddButton(LangHelper.T("卖血 -500ml", "Sell Blood -500ml"), sellBtnOnClick, "sell_blood_btn"); // 09-20 用户拍板：面板按钮为唯一采血入口（替代采血包）
            if (IsForcedRest()) b.AddLabel(LangHelper.T("昏迷中 · 剩余 " + PerkStatePersistence.GetInt(PERK_ID, "blood_rest", 0) + " 天", "Coma - " + PerkStatePersistence.GetInt(PERK_ID, "blood_rest", 0) + "d left"), "blood_rest_l");
            else if (IsBloodWeak()) b.AddLabel(LangHelper.T("虚弱（血量过低）", "Too weak (low blood)"), "blood_weak_l");
            // 新三状态（v5.7+ 用户拍板）：清洁/睡眠/社交 进度条+数值
            int clean = GetClean(), sleep = GetSleep(), social = GetSocial();
            b.AddLabel(LangHelper.T("清洁 ", "Cleanliness ") + clean + "/100", "clean_l");
            b.AddProgressBar(clean / 100f, "clean");
            b.AddLabel(LangHelper.T("睡眠 ", "Sleep ") + sleep + "/100", "sleep_l");
            b.AddProgressBar(sleep / 100f, "sleep");
            b.AddLabel(LangHelper.T("社交 ", "Social ") + social + "/100", "social_l");
            b.AddProgressBar(social / 100f, "social");
            // v5.8-8：逐节点状态显示（六状态 + 心情，每个当前节点一行：名称 + 锁定/抽取效果）
            b.AddLabel(LangHelper.T("── 节点状态 ──", "── Node Status ──"), "node");
            int[] allNodes = { SatietyNode(), ThirstNode(), HealthNode(), CleanNode(), SleepNode(), SocialNode(), MoodNode() };
            string domKey = GetStoredNodeKey();
            foreach (int n in allNodes)
            {
                if (n < 0 || n >= NODES.Length) continue;
                NodeDef d = NODES[n];
                string fxDesc = "";
                foreach (string f in d.Lock) { string lb = FxLabel(f); if (lb.Length > 0) fxDesc += lb + " "; }
                if (d.Key == domKey) // 主导节点：追加本次抽取效果
                {
                    string cur = GetNodeFx();
                    if (!string.IsNullOrEmpty(cur) && cur != "flavor") { string lb = FxLabel(cur); if (lb.Length > 0) fxDesc += lb + " "; }
                }
                string line = d.DisplayName;
                if (fxDesc.Length > 0) line += "｜" + fxDesc.Trim();
                b.AddLabel(line, "node");
            }
            int sellB = GetSellBonusPct(), budB = GetBudgetBonusPct(), moodNow = GetMood();
            string buffs = "";
            if (sellB > 0) buffs += LangHelper.T("售价+", "Sell +") + sellB + "% ";
            if (budB > 0) buffs += LangHelper.T("预算+", "Budget +") + budB + "% ";
            b.AddLabel(LangHelper.T("心情 ", "Mood ") + moodNow + (buffs.Trim().Length > 0 ? "｜" + buffs.Trim() : ""), "mood");
            b.End();
            b.Show();
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] 状态面板异常: " + ex.Message); }
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

    // 09-22 新档防 default_run 残留污染：鲁滨逊开局全部持久化 key（TrySetupNewRun 18 + 运行期固定 3 + 补偿天数 cb_* 13）
    private static readonly string[] ALL_KEYS = new string[]
    {
        "robinson_hard","sat","thirst","health","blood","mood","granary","elevStreak","elevCount",
        "starveDays","thirstDeath","critDays","clean","sleep","social","nodeKey","nodeFxIdx","deals",
        "revenue","blood_rest","hbuffDay",
        "cb_eatEff","cb_thirstEff50","cb_thirstEff10","cb_wearEff","cb_drugEff","cb_antiTheft","cb_moodDamp",
        "cb_forage20","cb_sell5","cb_mood2","cb_mood3","cb_sleepR10","cb_contraEff"
    };
    internal static void CleanDefaultRunOnNewGame()
    {
        try { PerkStatePersistence.CleanDefaultRun(PERK_ID, ALL_KEYS); } catch { }
    }
    // ===== 开局（HandleInitialItem Postfix 调用）=====
    internal static void TrySetupNewRun()
    {
        try
        {
            if (!IsActive()) return;
            WandererPerk.ClearBackpack(); // 09-20 设计稿：清原版发放（invElement+dossier）——先清后发，防误清自己物资
            bool hard = Il2Cpp.NewGameData.Instance != null && Il2Cpp.NewGameData.Instance.hardMode; // 原生困难模式开关（开局界面）
            PerkStatePersistence.SetInt(PERK_ID, "robinson_hard", hard ? 1 : 0); // 随档（原生 hardMode 退出重进重置，存 mod 状态）
            PlayerStore ps = PlayerStore.Instance;
            if (ps != null)
            {
                // 09-20 设计稿：金钱随机——hard 清零；普通 50%→0 / 50%→1~600（原固定 360）
                if (hard) ps.playerCash = 0;
                else if (Core.Rng.Next(2) == 0) ps.playerCash = 0;
                else ps.playerCash = Core.Rng.Next(1, 601);
            }

            // 09-21 物资发放移到 PostfixStartNewGame（StartNewGame 在原版发放之后触发：先清原版 4 件+文档再发，根治清太早）
            // HardMode：无开局物资

            PerkStatePersistence.SetInt(PERK_ID, "sat", 100);        // v5.7 三状态初始
            PerkStatePersistence.SetInt(PERK_ID, "thirst", 100);
            PerkStatePersistence.SetInt(PERK_ID, "health", 100);
            PerkStatePersistence.SetInt(PERK_ID, "blood", BLOOD_MAX); // 卖血：开局满血 6000ml（09-17）
            PerkStatePersistence.SetInt(PERK_ID, "mood", MOOD_START);
            PerkStatePersistence.SetInt(PERK_ID, "granary", 0);
            PerkStatePersistence.SetInt(PERK_ID, "elevStreak", 0);
            PerkStatePersistence.SetInt(PERK_ID, "elevCount", 0);
            PerkStatePersistence.SetInt(PERK_ID, "starveDays", 0);
            PerkStatePersistence.SetInt(PERK_ID, "thirstDeath", 0);
            PerkStatePersistence.SetInt(PERK_ID, "critDays", 0);
            PerkStatePersistence.SetInt(PERK_ID, "clean", CLEAN_START);   // 新三状态初始（用户拍板）
            PerkStatePersistence.SetInt(PERK_ID, "sleep", SLEEP_START);
            PerkStatePersistence.SetInt(PERK_ID, "social", SOCIAL_START);
            PerkStatePersistence.SetString(PERK_ID, "nodeKey", "");       // v5.8-8 节点池：开局清空，首次打烊抽取
            PerkStatePersistence.SetInt(PERK_ID, "nodeFxIdx", -1);
            PerkStatePersistence.SetInt(PERK_ID, "deals", 0);             // 接待计数
            SyncRentDisplay(); // 开局第一天就同步租金显示字段（拆包：日历读 dayUntilRent+rentValue）
            RefreshStatusPanel(); // 开局建常驻面板
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] TrySetupNewRun 异常: " + ex.Message); }
    }

    // ===== 09-21 开局物资：PlayerStore.StartNewGame Postfix（发放后清——原版 4 件+文档在 StartNewGame 发放，HandleInitialItem 清太早白清）=====
    internal static void PostfixStartNewGame()  // PlayerStore.StartNewGame Postfix（Core.cs 注册）
    {
        try
        {
            if (!IsActive()) return;
            WandererPerk.ClearBackpack(); // 清原版发放（magnifier/labeler/topical_bandage_item/fanny_pack + dossier 文档）
            bool hard = Il2Cpp.NewGameData.Instance != null && Il2Cpp.NewGameData.Instance.hardMode; // 09-20 修：不依赖PerkStatePersistence时序——直接读原生开局开关（偶尔StartNewGame先跑导致读旧档残留0）
            if (!hard) GiveStartingGoods();
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] PostfixStartNewGame 异常: " + ex.Message); }
    }

    private static void GiveStartingGoods()
    {
        GiveToBackpack("processed_meat", 3);      // 口粮×3（三天量）
        GiveToBackpack("raw_meat", 2);            // 大肉×2（用户拍板 09-09：另加生肉）
        GivePureWaterToBackpack(3);               // 大瓶纯水×3（三天量）
        GiveToBackpack("bandage_item", 5);        // 绷带×5（bandage_item 正确 id）
    }

    private static void GiveToBackpack(string id, int count)
    {
        try
        {
            EmporiumEntry em = EmporiumEntry.Instance;
            if (em == null || em.backInvinvElement == null) return;
            var inv = (GameInventory)em.backInvinvElement;
            for (int i = 0; i < count; i++)
            {
                GameItem item = DirectoryMaster.Item(id, true);
                if (item == null) continue;
                // 重叠bug修复（用户拍板 09-09：参考 QuickItemSpawner F3 先例）：
                // 正确链 = TryFindOneValidInventorySlot(item) → slot.TryAcceptOnce()（slot 持有格子坐标，真正落格）；
                // 之前丢弃 slot 直接 UncheckedAccept → 不设坐标 → 同格重叠。TryAcceptOnce 失败才 UncheckedAccept 兜底。
                var slot = em.backInvinvElement.TryFindOneValidInventorySlot(item, false);
                if (slot != null) { try { slot.TryAcceptOnce(); continue; } catch { } }
                inv.UncheckedAccept(item);
            }
        }
        catch { }
    }

    private static void GivePureWaterToBackpack(int count)
    {
        try
        {
            EmporiumEntry em = EmporiumEntry.Instance;
            if (em == null || em.backInvinvElement == null) return;
            var inv = (GameInventory)em.backInvinvElement;
            for (int i = 0; i < count; i++)
            {
                GameItem item = Il2Cpp.WaterPremadeHelper.AccurateHighQualityWater("large_bottled_water"); // 09-20 设计稿：直接生成带水大瓶（删 DirectoryMaster.Item+AddWater 链——工厂产物 AddWater 静默失败 → 空瓶）
                GameItem spawn = item;
                try { spawn.DisableTag("stolen", true); } catch { }
                // 同 GiveToBackpack：slot.TryAcceptOnce 真正落格，防重叠
                var slot = em.backInvinvElement.TryFindOneValidInventorySlot(spawn, false);
                if (slot != null) { try { slot.TryAcceptOnce(); continue; } catch { } }
                inv.UncheckedAccept(spawn);
            }
        }
        catch { }
    }

    // 09-11 用户确认：取消"前3天无随机客户"设定（PrefixHandleNormalClient/AugClient/AnyClient 已删，只保留次要客户永久拦截）
    public static bool PrefixHandleMinorClient()
    {
        // 09-10 用户拍板：次要客户（拾荒客/上层医生等）永久删掉，不限前3天
        try { if (IsActive()) return false; }
        catch { }
        return true;
    }

    // ===== 客流减量（精神档位：低迷-1 / 低落-2 / 崩溃-4；v5.9 病恹恹爆发当日客流-50% 隔一skip一）=====
    private static int _pickSkipToday = 0;
    private static bool _burstSkipFlip = false;
    public static bool PrefixPickClient()
    {
        try
        {
            if (!IsActive()) return true;
            int skip = GetClientReduction();
            if (_pickSkipToday < skip) { _pickSkipToday++; return false; }
            if (_burstClientCut >= 50) { _burstSkipFlip = !_burstSkipFlip; if (_burstSkipFlip) return false; } // 病恹恹爆发：当日客流-50%
        }
        catch { }
        return true;
    }

    // ===== 禁外出（低落/崩溃，含生病）=====
    public static bool PrefixOpenGoOutsideConfirm()
    {
        try { if (IsActive() && IsForbiddenOutside()) return false; }
        catch { }
        return true;
    }

    // ===== 腐烂/已食用食物客户拒买 =====
    private static int _rejectLogCount = 0;
    public static bool PrefixCanClientExposeAnyFeature(GameItem gameItem)
    {
        try
        {
            if (IsActive() && gameItem != null && IsFood(gameItem))
            {
                bool reject = false;
                if (GetFoodQuality(gameItem) >= 3) { reject = true; }
                else if (IsEaten(gameItem)) { reject = true; }
                if (reject)
                {
                    _rejectLogCount++;
                    if (_rejectLogCount % 60 == 1)
                    return false;
                }
            }
        }
        catch { }
        return true;
    }

    // ===== 吃过的食物放不上柜台 =====
    public static bool PrefixAddedItemToWeightedArea(GameItem gameItem)
    {
        try
        {
            if (IsActive() && gameItem != null && IsFood(gameItem) && IsEaten(gameItem))
            {
                return false;
            }
        }
        catch { }
        return true;
    }

    // ===== 双击食用（吃一口/喝一口/吃药治病）=====
    // 判定：物品是否在博士夜晚商店库存（afterhourInventory）里——没买不能吃（用户反馈修复）
    private static bool IsInDoctorNightInventory(GameItem item)
    {
        try
        {
            var em = Il2Cpp.EmporiumEntry.Instance;
            if (em == null || em.afterhourInventory == null || item == null) return false;
            long ptr = (long)item.Pointer;
            if (em.afterhourInventory.childItems != null)
            {
                for (int i = 0; i < em.afterhourInventory.childItems.Count; i++)
                {
                    var it = em.afterhourInventory.childItems[i];
                    if (it != null && (long)it.Pointer == ptr) return true;
                }
            }
        }
        catch { }
        return false;
    }
    // ===== 面板卖血按钮（09-20 用户拍板：替代采血包双击——唯一采血入口；删采血包物品+双击链）=====
    public static bool TrySellBlood()
    {
        try
        {
            if (!IsActive()) return false;
            if (Patches.CurrentUITradeMode != 0) return false;          // 交易模式不抽
            if (IsBloodWeak() || IsForcedRest())
            {
                try { StoreUIManager.Instance.Notify(LangHelper.T("身体虚弱/恢复期，无法抽血", "Too weak - cannot draw blood"), "red"); } catch { }
                return false;
            }
            int blood = GetBlood();
            if (blood < 500)
            {
                try { StoreUIManager.Instance.Notify(LangHelper.T("血量不足 500cc，无法抽血", "Not enough blood (need 500cc)"), "red"); } catch { }
                return false;
            }
            AddBlood(-500);
            // 09-20 用户拍板：抽血过多当场昏迷 3 天（血量 <3000 立即触发；血袋照常产出——抽血成功的代价）；昏迷当天立即禁出门/禁采血（IsForcedRest 即时生效）
            if (IsBloodWeak())
            {
                PerkStatePersistence.SetInt(PERK_ID, "blood_rest", 3);
                try { StoreUIManager.Instance.Notify(LangHelper.T("你因为失血过多昏迷了三天", "You passed out from blood loss - 3-day coma"), "red"); } catch { }
                try { var _ps = PlayerStore.Instance; if (_ps != null) _ps.AddNightLog(LangHelper.T("[鲁滨逊] 你因为失血过多昏迷了三天", "[Robinson] Passed out from blood loss - 3-day coma"), "#7FC97F"); } catch { } // ① 原生夜报（09-22 统一柔和绿）
                Core.AddNightReportLine(LangHelper.T("[鲁滨逊] 你因为失血过多昏迷了三天", "[Robinson] Passed out from blood loss - 3-day coma"));
                ForceComaSkip(); // 09-20 拍板：昏迷当天立刻强制过夜 ×3（跳过 3 天）
                try { RefreshStatusPanel(); } catch { }
            }
            bool bag = false;
            try
            {
                // 09-20 M2 拍板：产普通血袋 blood_bag（价值 200，走原生医疗品销路）；删 blue_blood_bag 路径
                GameItem bb = DirectoryMaster.Item("blood_bag", true);
                if (bb != null)
                {
                    try { bb.SetValue(200); } catch { }
                    var em = EmporiumEntry.Instance;
                    if (em != null && em.backInvinvElement != null)
                    {
                        var slot = em.backInvinvElement.TryFindOneValidInventorySlot(bb, false);
                        if (slot != null) { try { slot.TryAcceptOnce(); bag = true; } catch { } }
                        if (!bag) { try { ((GameInventory)em.backInvinvElement).UncheckedAccept(bb); bag = true; } catch { } }
                    }
                }
            }
            catch { }
            if (!bag) Core.LogMsg("[卖血] blood_bag 不存在或发放失败");
            try { Il2Cpp.HealthData.ReceiveMinorWound(); } catch { }
            try { StoreUIManager.Instance.Notify(LangHelper.T("抽血 500cc → 血袋（价值 200，血量 " + GetBlood() + "/6000）", "Drew 500cc -> blood bag (worth 200, blood " + GetBlood() + "/6000)"), "green"); } catch { }
            RefreshStatusPanel();
            return true;
        }
        catch { }
        return false;
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
                PerkStatePersistence.SetInt(PERK_ID, "hbuffDay", DeterministicSchedule.CurrentDay); // 存档：连续3天不受伤+拾荒+1
                try { StoreUIManager.Instance.Notify(LangHelper.T("顶级自酿：连续3天不受伤、拾荒次数+1", "Top Homebrew: 3d no wound, scav+1"), "green"); } catch { }
            }
        }
        BoostMood(mood, homebrew ? LangHelper.T("自酿酒", "Homebrew") : LangHelper.T("喝酒", "Drinking"));
        if (ml > 0) { try { WaterHelper.Remove(item, sip * 1000); } catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] 喝酒Remove异常 " + ex.Message); } }
        TryExpel(item); // 整件消失（09-13 用户拍板：双击酒类使用后消失）
        RefreshStatusPanel();
    }

    // ===== 机器初始耗电 +2（拆包 09-10：GetMachinePowerUsage 是统一耗电读口；Postfix 兜底全机器，鲁滨逊职业内生效）=====
    public static void PostfixGetMachinePowerUsage(ref int __result)
    {
        try
        {
            if (!IsActive()) return;
            __result += 2;
        }
        catch { }
    }

    // 基础价值（口径稳定：unitBaseValue；失败退 GetCurrentValue）
    private static int GetItemBaseValue(GameItem item)
    {
        try { return (int)item.unitBaseValue; } catch { }
        try { return (int)item.GetCurrentValue(); } catch { }
        return 1;
    }

    // 顶级自酿 buff 生效中（喝后当天+次日+第三日，PerkStatePersistence 存档）
    private static bool IsHomebrewWineBuffActive()
    {
        try
        {
            if (!IsActive()) return false;
            int start = PerkStatePersistence.GetInt(PERK_ID, "hbuffDay", -1);
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
        int bite = cal <= 100 ? cal : Math.Max(100, (cal + 1) / 2);
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

    // ===== 供货商卖水药食物（拆包 09-10：PlaceSupplierInventory 是 supplier 上货入口；仿水商 MerchantHelper.AddItemToCounter 追加，同日不重复）=====
    private static int _supplierGoodsDay = -1;
    public static void PostfixPlaceSupplierInventory()
    {
        try
        {
            if (!IsActive()) return;
            int day = DeterministicSchedule.CurrentDay;
            if (day == _supplierGoodsDay) return; // 同日不重复追加
            _supplierGoodsDay = day;
            string[] sellItems = {
                "bottled_water",        // 瓶装水
                "small_bottled_water",  // 小瓶水
                "water_ration",         // 水配给
                "raw_meat",             // 生肉
                "processed_meat",       // 加工肉
                "cup_noodle",           // 杯面
                "bandage_item",         // 绷带
                "nutrient_tablet",      // 营养片
            };
            int added = 0;
            // 大瓶高品质水（拆包 09-10：WaterPremadeHelper.AccurateHighQualityWater(size) 一行生成指定品质大瓶）
            try
            {
                GameItem hq = Il2Cpp.WaterPremadeHelper.AccurateHighQualityWater("large_bottled_water");
                if (hq != null) { MerchantHelper.AddItemToCounter(hq, 100, false); added++; }
            }
            catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] 供货商加大瓶高品质水失败: " + ex.Message); }
            foreach (string wid in sellItems)
            {
                try
                {
                    GameItem w = DirectoryMaster.Item(wid, true);
                    if (w == null) { Core.LogMsg("[空间站鲁滨逊] 供货商加 " + wid + " 不存在"); continue; }
                    MerchantHelper.AddItemToCounter(w, 100, false);
                    added++;
                }
                catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] 供货商加 " + wid + " 失败: " + ex.Message); }
            }
            Core.LogMsg("[空间站鲁滨逊] 供货商已追加水/药/食物 " + added + " 件（day " + day + "）");
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] PostfixPlaceSupplierInventory 异常: " + ex.Message); }
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

    // ===== 卖血系统（09-17 用户拍板：面板卖血按钮 500cc→血袋+轻伤；受伤扣血；虚弱<3000；睡觉/喝水/进食回血）=====
    internal const int BLOOD_MAX = 6000;
    internal static int GetBlood() { try { return PerkStatePersistence.GetInt(PERK_ID, "blood", BLOOD_MAX); } catch { return BLOOD_MAX; } }
    internal static void SetBlood(int v) { try { PerkStatePersistence.SetInt(PERK_ID, "blood", Math.Max(0, Math.Min(BLOOD_MAX, v))); } catch { } }
    internal static int AddBlood(int delta)
    {
        int b = Math.Max(0, Math.Min(BLOOD_MAX, GetBlood() + delta));
        SetBlood(b);
        if (GetBlood() <= 0) { try { ExecuteGameOverBy("blood_loss"); } catch { } } // 09-20 拍板：失血归零立即死亡（日常/跳天通用）
        return b;
    }
    internal static bool IsBloodWeak() { try { return GetBlood() < 3000; } catch { return false; } }
    // 09-20 M5 拍板：虚弱强化——强制休息期判定（休息中禁采血/禁出门）
    internal static bool IsForcedRest()
    {
        try { return PerkStatePersistence.GetInt(PERK_ID, "blood_rest", 0) > 0; }
        catch { return false; }
    }
    // 09-20 M5：虚弱强制休息 3 天 → 结束 ±20% 血量（默认 50/50）；每日结算调用
    internal static void TickBloodRest()
    {
        try
        {
            int rest = PerkStatePersistence.GetInt(PERK_ID, "blood_rest", 0);
            if (rest > 0)
            {
                rest--;
                PerkStatePersistence.SetInt(PERK_ID, "blood_rest", rest);
                if (rest == 0)
                {
                    bool good = Core.Rng.Next(2) == 0;
                    int delta = (int)(BLOOD_MAX * 0.2f); // 1200
                    AddBlood(good ? delta : -delta);
                    try { StoreUIManager.Instance.Notify(LangHelper.T("身体恢复期结束：血量" + (good ? "+" : "-") + delta + "（" + GetBlood() + "/6000）", "Recovery over: blood " + (good ? "+" : "-") + delta + " (" + GetBlood() + "/6000)"), good ? "green" : "red"); } catch { }
                }
                else
                {
                    try { StoreUIManager.Instance.Notify(LangHelper.T("身体虚弱，强制休息（剩余 " + rest + " 天）", "Too weak - forced rest (" + rest + "d left)"), "red"); } catch { }
                }
                RefreshStatusPanel();
            }
            else if (IsBloodWeak())
            {
                PerkStatePersistence.SetInt(PERK_ID, "blood_rest", 3);
                try { StoreUIManager.Instance.Notify(LangHelper.T("身体虚弱到极限，强制休息 3 天", "At your limit - forced 3-day rest"), "red"); } catch { }
                try { var _ps = PlayerStore.Instance; if (_ps != null) _ps.AddNightLog(LangHelper.T("[鲁滨逊] 血量过低，强制休息 3 天", "[Robinson] Too weak - forced 3-day rest"), "#7FC97F"); } catch { } // ① 原生夜报（09-22 统一柔和绿）
                Core.AddNightReportLine(LangHelper.T("[鲁滨逊] 血量过低，强制休息 3 天", "[Robinson] Too weak - forced 3-day rest"));
                RefreshStatusPanel();
            }
        }
        catch { }
    }

    public static bool PrefixReceiveWound()
    {
        try
        {
            if (!IsActive()) return true;
            if (IsHomebrewWineBuffActive()) return false; // 顶级自酿 buff：连续3天不受伤（用户拍板 09-10）
            int pct = GetMoodWoundPct(); // ≥80 -20% / <40 +20%（受伤几率修正）
            float avoid = 0.3f * (1f + pct / 100f); // 免伤基底 30%：≥80→36%（更不易伤）/<40→24%（更容易伤）
            if (IsBloodWeak()) avoid -= 0.3f; // 卖血虚弱（<3000）：受伤概率 +30%（09-17）
            if (UnityEngine.Random.value < avoid) return false;
        }
        catch { }
        return true;
    }
    // 受伤扣血（09-17 卖血）：轻伤 -200 / 重伤 -500（ReceiveMinorWound/MajorWound Postfix）
    public static void PostfixReceiveMinorWound()
    {
        try { if (!IsActive()) return; AddBlood(-200); RefreshStatusPanel(); } catch { }
    }
    public static void PostfixReceiveMajorWound()
    {
        try { if (!IsActive()) return; AddBlood(-500); RefreshStatusPanel(); } catch { }
    }
    public static void PostfixCanScavenge(ref bool __result)
    {
        try
        {
            if (!IsActive()) return;
            if (IsBloodWeak() || IsForcedRest()) { __result = false; return; } // 09-20 M5：虚弱/恢复期禁出门
            // 三处同 cap（拆包 2.13.11.4：GetMaxScavAttempts/GetScavTimeLeft/CanScavenge 独立复制，须一致）
            __result = GetScavAttempts() < GetScavCap();
        }
        catch { }
    }
    public static void PostfixGetMaxScavAttempts(ref int __result)
    {
        try
        {
            if (!IsActive()) return;
            if (IsBloodWeak() || IsForcedRest()) { __result = 0; return; } // 09-20 M5
            __result = GetScavCap(); // 上限
        }
        catch { }
    }
    public static void PostfixGetScavTimeLeft(ref int __result)
    {
        try
        {
            if (!IsActive()) return;
            if (IsBloodWeak() || IsForcedRest()) { __result = 0; return; } // 09-20 M5
            // UI 显示：剩余 = 上限 - 已用（不为负，数字整对）
            __result = Math.Max(0, GetScavCap() - GetScavAttempts());
        }
        catch { }
    }
    // 拾荒次数 cap（v5.7）：受伤=0、病危=0；心情 ≥80 +2 / <40 -2；基底 5
    private static int GetScavCap()
    {
        try
        {
            // 捡漏直觉不再额外加拾荒次数（用户拍板 09-10：该加成有缩减 bug，特性不影响次数）
            // 09-12 硬爽版：捡漏直觉加回 +10（鲁滨逊走 GetScavCap 单一读口，LuckScout 侧 Postfix 已排除鲁滨逊防双加）
            // 09-20 回归修复：受伤且伤口未稳定 → cap 0 → 禁拾荒（打绷带 isWoundStable=true → cap 恢复，09-09 已拆实锤）
            if (IsWounded() && !IsWoundStable()) return 0;
            int cap = 5 + GetMoodScavBonus();
            if (BuildConfig.HardMode && LuckScoutPerk.IsActive()) cap += 10;
            if (IsHomebrewWineBuffActive()) cap += 1; // 顶级自酿 buff：拾荒次数+1（用户拍板 09-10）
            cap = Math.Max(1, cap);
            return cap;
        }
        catch { }
        return 5;
    }
    // 拾荒池多加官方药品+食物（用户拍板 09-09）：每次成功拾荒 60% 概率额外翻出 1 件（官方已开放）
    private static readonly string[] SCROUNGE_FOODS = {
        "morsel", "small_morsel", "cat_bar", "processed_meat", "small_raw_meat", "raw_meat",
        "processed_cheese", "cup_noodle", "processed_juice", "li_eat_snackbar", "processed_milk"
    };
    private static readonly string[] SCROUNGE_MEDS = {
        "bandage_item", "hemostatic_bandage_item", "topical_bandage_item", "phagimycin_pill",
        "med_bottle_blue", "med_bottle_red", "salve", "blood_bag"
    };
    public static void PostfixGetRandomScavengedItem(Il2CppSystem.Collections.Generic.List<GameItem> __result)
    {
        try
        {
            if (!IsActive()) return;
            if (__result == null) return;
            if (UnityEngine.Random.Range(0f, 1f) > 0.6f) return; // 60% 概率
            bool food = UnityEngine.Random.Range(0, 2) == 0;
            string[] pool = food ? SCROUNGE_FOODS : SCROUNGE_MEDS;
            string id = pool[UnityEngine.Random.Range(0, pool.Length)];
            GameItem item = DirectoryMaster.Item(id, true);
            if (item == null) return;
            __result.Add(item);
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] 拾荒额外掉落异常: " + ex.Message); }
    }
    // ===== 模板 0.5 总系数（用户拍板 09-09：模块/模板贡献减半；升级累加不受影响——MoreUpdate 写 TOTAL 标签，减半在读取端）=====
    // 拆包 2.5.26 [L1]：产出量走 GetCurrentPerformanceBonus/QualityBonus；处理速度走 ApplyBasicModuleEffect 内直读 TOTAL 标签（Getter 不覆盖）；
    // 原版模块走 ModifyTempStatFromBaseByPercentage；三个 TAG 减半后模块升级（AddVirtualBonus 累加）仍正常
    public static void PostfixGetCurrentPerformanceBonus(GameItem gameItem, ref int __result)
    {
        try
        {
            if (!IsActive() || gameItem == null) return;
            if (__result > 0) __result = Math.Max(0, (int)(__result * 0.5));
            // 机器升级（wageUpgradePct 独立 tag，不被模块聚合覆盖）：+1%/次，不减半
            int up = GetTagIntSafe(gameItem, "wageUpgradePct");
            if (up > 0) __result += up;
        }
        catch { }
    }
    public static void PostfixGetCurrentQualityBonus(GameItem gameItem, ref int __result)
    {
        try
        {
            if (!IsActive() || gameItem == null) return;
            if (__result > 0) __result = Math.Max(0, (int)(__result * 0.5));
            int up = GetTagIntSafe(gameItem, "wageUpgradePct");
            if (up > 0) __result += up;
        }
        catch { }
    }
    public static void PostfixApplyBasicModuleEffect(GameInventory invModule, GameItem item, GameItem system)
    {
        try
        {
            if (!IsActive() || item == null) return;
            var ts = item.GetTagReadonly("CURRENT_PROCESSING_SPEED_TAG");
            if (ts != null && ts.valueInt > 0) SetTagIntValue(item, "CURRENT_PROCESSING_SPEED_TAG", Math.Max(0, (int)(ts.valueInt * 0.5)));
            // v5.9 效率升级（用户拍板 09-09：金属锭拖机器 +1%，无限叠加）：速度 = 原×0.5 + 机器效率升级数
            // wageUpgradeEff 直接加（不减半），写 item 无则取 system
            GameItem target = item;
            if (target.GetTagReadonly("wageUpgradeEff") == null && system != null) target = system;
            var eff = target.GetTagReadonly("wageUpgradeEff");
            if (eff != null && eff.valueInt > 0 && ts != null && ts.valueInt > 0)
                SetTagIntValue(target, "CURRENT_PROCESSING_SPEED_TAG", Math.Max(0, (int)(ts.valueInt * 0.5) + eff.valueInt));
        }
        catch { }
    }
    public static void PostfixModifyTempStatFromBaseByPercentage(GameItem item, int percentage)
    {
        try
        {
            if (!IsActive() || item == null) return;
            foreach (string t in new[] { "TEMP_PERCENTAGE_PERFORMANCE_INT", "TEMP_PERCENTAGE_EFFICIENCY_INT", "TEMP_PERCENTAGE_QUALITY_INT" })
            {
                var ts = item.GetTagReadonly(t);
                if (ts != null && ts.valueInt > 0) SetTagIntValue(item, t, Math.Max(0, (int)(ts.valueInt * 0.5)));
            }
        }
        catch { }
    }
    internal static void SetTagIntValue(GameItem item, string tag, int value)
    {
        try
        {
            if (!item.IsTag(tag)) item.EnableTag(tag, true);
            System.Action<TagState> sysAct = delegate (TagState state) { state.SetInt(value); };
            var il2cppAct = DelegateSupport.ConvertDelegate<Il2CppSystem.Action<TagState>>((System.Delegate)sysAct);
            item.ModifyTag(tag, il2cppAct, false);
        }
        catch { }
    }
    // ===== 金属锭/垃圾升级系统（用户拍板 09-09：拖 metal_ingot 到机器/模板 = 性能/效率/质量三维各 +1% 无限叠加；
    // 拖 junk 到容器 = 容量 +1 列宽，无限叠加。拖放拦截仿虚空珠模式）=====
    // 机器/模板升级 = 目标 TOTAL_PERCENTAGE_PERFORMANCE/QUALITY_BONUS_INT 各 +2 → Getter ×0.5 后实 +1（升级不受减半）；
    // 效率 = wageUpgradeEff +1 → ApplyBasicModuleEffect 速度直接 +1。容器 = wageUpgradeCap +1 → SetShape 宽+1（读档恢复照虚空珠）。
    public static bool PrefixMayTarget(GameItem __instance, GameItem targetItem, ref bool __result)
    {
        try
        {
            bool robC = IsActive();
            // 09-15 水商之友：非鲁滨逊档但水商之友激活 + 目标是瓶印机 → 允许金属锭升级
            if ((!robC && !(WaterMerchantPerk.IsActive() && IsBottlePrinter(targetItem))) || __instance == null || targetItem == null) return true;
            // 拖动中 MayTarget 会被反复调用：匹配即放行（hover 可拖），升级/消耗留给松手时的 Target/MayHaveValidInventorySlot
            if ((IsMetalIngot(__instance) && !IsMoreUpdateOwnedMachine(targetItem) && (IsMachine(targetItem) || targetItem.IsTag("MODULE_TAG")))
                || (IsJunk(__instance) && ContainerUpgradeV2.IsUpgradeableContainer(targetItem)))
            { __result = true; return false; }
        }
        catch { }
        return true;
    }
    public static bool PrefixCanTarget(GameItem __instance, GameItem targetItem, ref bool __result)
    {
        return PrefixMayTarget(__instance, targetItem, ref __result);
    }
    public static bool PrefixTarget(GameItem __instance, GameItem targetItem)
    {
        try
        {
            bool robC = IsActive();
            // 09-15 水商之友：非鲁滨逊档但水商之友激活 + 目标是瓶印机 → 允许金属锭升级
            if ((!robC && !(WaterMerchantPerk.IsActive() && IsBottlePrinter(targetItem))) || __instance == null || targetItem == null) return true;
            if (!IsDragRelease()) return true;
            if (IsMetalIngot(__instance) && !IsMoreUpdateOwnedMachine(targetItem) && (IsMachine(targetItem) || targetItem.IsTag("MODULE_TAG")))
            { if (TryUpgradeMachine(__instance, targetItem)) return false; }
            else if (IsJunk(__instance) && ContainerUpgradeV2.IsUpgradeableContainer(targetItem))
            {
                    if (TryUpgradeContainer(__instance, targetItem)) return false;
            }
        }
        catch { }
        return true;
    }
    // 容器升级：拖 junk 到容器物品（CONTAINER_TAG）→ 不放进去，升级容量 +1 列
    public static bool PrefixMayHaveValidInventorySlot(GameItem __instance, GameItem item, ref bool __result)
    {
        try
        {
            if (!IsActive() || __instance == null || item == null) return true;
            if (!IsJunk(item)) return true;
            if (!ContainerUpgradeV2.IsUpgradeableContainer(__instance)) return true;
            if (!IsDragRelease()) return true;
            if (TryUpgradeContainer(item, __instance)) { __result = false; return false; }
        }
        catch { }
        return true;
    }
    // ===== MoreUpdate（MoreDeviceUpgrades v0.2.0）兼容让路（09-14）=====
    // 动态探测 MoreUpdate 已加载时，我方金属锭升级对它的 4 台配方机器让路（放行给 MoreUpdate 独占），
    // 消除"同一拖放双响应、metal_ingot 双消耗"。未装 MoreUpdate 时我方行为完全不变。
    private static bool? _moreUpdateLoaded;
    private static bool IsMoreUpdateLoaded()
    {
        try
        {
            if (_moreUpdateLoaded == null)
            {
                bool found = false;
                var asms = System.AppDomain.CurrentDomain.GetAssemblies();
                if (asms != null)
                    foreach (var a in asms)
                    {
                        if (a == null) continue;
                        string n = "";
                        try { n = a.GetName().Name ?? ""; } catch { }
                        if (n == "MoreDeviceUpgrades") { found = true; break; }
                    }
                _moreUpdateLoaded = found;
            }
            return _moreUpdateLoaded.Value;
        }
        catch { return false; }
    }
    // MoreUpdate 配方机器 ∩ 我方机器全集（拆包实锤：furnace/water_purifier/wine_rack/mirage_projector 有 metal_ingot 配方）
    private static readonly HashSet<string> MOREUPDATE_METAL_INGOT_MACHINES = new HashSet<string>(
        new[] { "furnace", "water_purifier", "wine_rack", "mirage_projector" });
    private static bool IsMoreUpdateOwnedMachine(GameItem target)
    {
        try
        {
            if (!IsMoreUpdateLoaded() || target == null) return false;
            string id = (target.identifier ?? "").ToLowerInvariant();
            if (!MOREUPDATE_METAL_INGOT_MACHINES.Contains(id)) return false;
            // 接力（09-14 拍板）：MoreUpdate 升满（cap reached）后我方接管继续升级——升满判定按拆包实锤
            if (id == "furnace" && GetTagIntSafe(target, "MOD_FURNACE_INGOT_UPGRADES") >= 10) return false;
            if (id == "water_purifier" && GetTagIntSafe(target, "MOD_PURIFIER_INGOT_UPGRADES") >= 10) return false;
            if (id == "wine_rack" && GetTagIntSafe(target, "MOD_WINERACK_WINE_WIDTH") >= 6) return false;
            // mirage_projector 永不升满（int.MaxValue）→ 始终归 MoreUpdate
            return true;
        }
        catch { return false; }
    }
    private static bool IsMetalIngot(GameItem item)
    {
        try { return item != null && (item.identifier ?? "").ToLowerInvariant() == "metal_ingot"; } catch { return false; }
    }
    private static bool IsJunk(GameItem item)
    {
        try { return item != null && (item.identifier ?? "").ToLowerInvariant() == "junk"; } catch { return false; }
    }
    // 水瓶打印机（水商之友专属升级目标——09-15 用户拍板：水商之友可升级瓶印机质量）
    private static bool IsBottlePrinter(GameItem item)
    {
        try { return item != null && (item.identifier ?? "").ToLowerInvariant() == "bottle_printer"; } catch { return false; }
    }
    private static bool IsDragRelease()
    {
        try
        {
            var dragHandler = Il2Cpp.ItemMouseDragHandler.current;
            if (dragHandler == null || !dragHandler.IsDraggingItem) return false;
            if (UnityEngine.Input.GetMouseButton(0)) return false; // 按住=拖动中；松手才触发
            return true;
        }
        catch { return false; }
    }
    // 机器白名单全集（拆包 2.5.30 [L1]：STANDARD_MACHINE_TAG 仅 8 台；"所有机器可升级"→ 自定义全集判定）
        internal static readonly HashSet<string> ALL_MACHINE_IDS = new HashSet<string>(new string[] { "alarm_system", "moisture_farm", "water_purifier", "mirage_projector", "desequencer", "furnace", "wine_rack", "turbo_booster", "bottle_printer", "box_dispenser", "cassette_player", "animal_feeder", "recharger_base", "fridge", "blender", "chem_finisher", "deal_maker", "heating_plate", "hydroponic", "broken_machine" });
    internal static bool IsMachine(GameItem item)
    {
        try
        {
            if (item == null) return false;
            if (item.IsTag("STANDARD_MACHINE_TAG")) return true;
            string id = (item.identifier ?? "").ToLowerInvariant();
            return ALL_MACHINE_IDS.Contains(id);
        }
        catch { return false; }
    }
    // 机器/模板升级：消耗金属锭。机器=独立 tag wageUpgradePct/wageUpgradeEff（STANDARD 机器 Getter Postfix 不减半加回，
    // 避免被模块聚合重写覆盖；非 STANDARD 机器无聚合读 TOTAL_PERCENTAGE_* → 走模板路径）；
    // 模板=TOTAL 性能/质量 +2（聚合读模板 tag 后 Getter ×0.5，实 +1）
    private static bool TryUpgradeMachine(GameItem ingot, GameItem target)
    {
        try
        {
            if (IsMoreUpdateOwnedMachine(target)) return false; // MoreUpdate 兼容让路：配方机器归 MoreUpdate 独占
            // 09-15 瓶印机专用升级：只写质量（TOTAL_PERCENTAGE_QUALITY_BONUS_INT +2，水商之友 Getter 无减半 → 实 +2/次；鲁滨逊 ×0.5 → 实 +1/次）
            if (IsBottlePrinter(target))
            {
                AddTagInt(target, "TOTAL_PERCENTAGE_QUALITY_BONUS_INT", 2);
                ConsumeOne(ingot);
                return true;
            }
            bool isMachine = IsMachine(target);
            if (isMachine && target.IsTag("STANDARD_MACHINE_TAG"))
            {
                AddTagInt(target, "wageUpgradePct", 1);
                AddTagInt(target, "wageUpgradeEff", 1);
            }
            else
            {
                // 模板 / 非 STANDARD 机器：写 TOTAL_PERCENTAGE_*（此类机器无模块聚合重写覆盖）
                AddTagInt(target, "TOTAL_PERCENTAGE_PERFORMANCE_BONUS_INT", 2);
                AddTagInt(target, "TOTAL_PERCENTAGE_QUALITY_BONUS_INT", 2);
            }
            ConsumeOne(ingot);
            return true;
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] 机器升级异常: " + ex.Message); return false; }
    }
    // 容器升级（v2 段位制，用户拍板 09-12）：段0-5，junk 消耗 5/10/20/40/80，满级后 junk 正常放入
    // 段位换算：宽 = floor(wb_orig_w × (50% + 30%k))；段0=开局减半(50%)，段5=原宽2倍(200%)
    private static bool TryUpgradeContainer(GameItem junk, GameItem container)
    {
        try
        {
            if (ContainerUpgradeV2.ConsumedThisFrame(container.Pointer)) return true; // 同帧已消耗：防双计数（MayHaveValidInventorySlot+Target 双挂点）
            var grid = GetContainerGrid(container);
            if (grid == null) { Core.LogMsg("[空间站鲁滨逊] 容器升级失败：取不到内部库存 " + GetId(container)); return false; }
            int w = 0, h = 0;
            GetShapeWH(grid, ref w, ref h);
            if (w <= 0 || h <= 0) { Core.LogMsg("[空间站鲁滨逊] 容器升级失败：宽高异常 " + w + "x" + h); return false; }
            int stage = ContainerUpgradeV2.GetTagIntSafe(container, "wb_stage");
            if (stage >= ContainerUpgradeV2.MAX_STAGE) return false; // 满级：junk 正常放入（不再消耗）
            ContainerUpgradeV2.ConsumeOne(junk); // 逐颗消耗：每拖 1 个 junk 立即扣 1
            int progress = ContainerUpgradeV2.GetTagIntSafe(container, "wb_progress") + 1;
            int need = ContainerUpgradeV2.UPGRADE_COSTS[stage];
            if (progress < need)
            {
                ContainerUpgradeV2.SetTagIntValue(container, "wb_progress", progress);
                try { StoreUIManager.Instance.Notify(LangHelper.T("储存区 升级进度 " + progress + "/" + need, "Storage progress " + progress + "/" + need), "white"); } catch { }
                return true; // 已消耗，拦截放入
            }
            int origW = ContainerUpgradeV2.GetTagIntSafe(container, "wb_orig_w");
            int targetW;
            if (origW > 0)
                targetW = ContainerUpgradeV2.GetCrusoeTargetWidth(origW, stage + 1); // 减半容器：恢复语义 50%→200%
            else
                targetW = w + 1; // 未减半容器（玩家装备腰包 fanny_pack 等）：每段 +1 列（3→4→5...）
            ContainerUpgradeV2.AddTagInt(container, "wb_stage", 1);
            ContainerUpgradeV2.SetTagIntValue(container, "wb_progress", 0); // 达标升段，进度清零重计
            try { PerkStatePersistence.SetInt(PERK_ID, "wage_stage_u" + container.uniqueId, stage + 1); } catch { } // 09-14 双写：场景位置 tags 不随档，PlayerPrefs 兜底
            try { if (ContainerUpgradeV2.IsUpgradeableContainer(container)) container.EnableTag("CONTAINER_TOOLTIP_TAG"); } catch { } // 拆包 2.5.32：容量行显示门控
            // 字符串重载（自动 ValidateBackground，虚空珠同路径）——全开放矩形 '0'=可放
            try { grid.SetShape(new string('0', targetW * h), targetW); } catch { try { grid.SetShape("", targetW); } catch { } }
            try { grid.Validate(); } catch { }
            try { StoreUIManager.Instance.Notify(LangHelper.T((stage + 1) >= ContainerUpgradeV2.MAX_STAGE ? "储存区满级！容量翻倍（宽 " + targetW + "）" : "储存区升级！段位 " + (stage + 1) + "/" + ContainerUpgradeV2.MAX_STAGE + "（宽 " + targetW + "）", (stage + 1) >= ContainerUpgradeV2.MAX_STAGE ? "Storage MAX! 2x capacity (width " + targetW + ")" : "Storage upgraded! Stage " + (stage + 1) + "/" + ContainerUpgradeV2.MAX_STAGE + " (width " + targetW + ")"), "white"); } catch { }
            try { Core.LogMsg("[容器v2] " + GetId(container) + " 升段 stage=" + (stage + 1) + " w=" + w + "->" + targetW + " origW=" + origW); } catch { }
            return true;
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] 容器升级异常: " + ex.Message); return false; }
    }
    private static GameGridInventory GetContainerGrid(GameItem item)
    {
        try
        {
            var cw = item.contentWindow;
            if (cw != null && cw.childElement != null) { var v = cw.childElement.Cast<GameGridInventory>(); if (v != null) return v; }
        }
        catch { }
        return null;
    }
    // 容器宽高：inventoryShape 类型化直读（拆包 09-10 [L1]：GameGridInventory.inventoryShape public / GridShapeBuilder.width/height public）
    private static void GetShapeWH(GameGridInventory inv, ref int w, ref int h)
    {
        try
        {
            if (inv == null || inv.inventoryShape == null) return;
            w = inv.inventoryShape.width;
            h = inv.inventoryShape.height;
        }
        catch { }
    }
    private static void ConsumeOne(GameItem item)
    {
        try
        {
            int c = item.unitCount - 1;
            if (c <= 0) item.Destroy(); else item.SetUnitCount(c);
        }
        catch { try { item.Destroy(); } catch { } }
    }
    // 读档恢复容器升级（照虚空珠 PostfixLoadGame：SetShape 不存档，按 wageUpgradeCap 重设）
    // 保存点快照：SaveGame 时存 6 维生存状态（读档恢复用；key 带 runID 自动隔离）
    public static void PostfixSaveGame()
    {
        try
        {
            if (!IsActive()) return;
            PerkStatePersistence.SetInt(PERK_ID, "saved_sat", GetSatiety());
            PerkStatePersistence.SetInt(PERK_ID, "saved_th", GetThirstPct());
            PerkStatePersistence.SetInt(PERK_ID, "saved_hp", GetHealth());
            PerkStatePersistence.SetInt(PERK_ID, "saved_clean", GetClean());
            PerkStatePersistence.SetInt(PERK_ID, "saved_sleep", GetSleep());
            PerkStatePersistence.SetInt(PERK_ID, "saved_social", GetSocial());
        }
        catch { }
    }

    // 【09-10 容器升级读档丢失修复】防双恢复：LoadGame 恢复成功 or SetContentWindow 恢复成功都标记，避免 SetShape(w+cap) 重复执行双加
    private static readonly HashSet<IntPtr> _rcRestoredContainers = new HashSet<IntPtr>();

    public static void PostfixLoadGame_IngotContainer()
    {
        try
        {
            _rcRestoredContainers.Clear(); // 读档：清容器恢复防重集合（新会话重新恢复）
            PerkStatePersistence.ResetCache(); // 读档切档：清 runID 缓存，防 key 前缀串用导致状态节点全回默认（用户反馈 09-10）
            // 恢复保存点生存状态（SaveGame 快照）——"退出本天未保存重新进"当天扣减（拾荒-7等）应随读档回滚
            try
            {
                if (IsActive() && PerkStatePersistence.HasKey(PERK_ID, "saved_sleep"))
                {
                    SetSatiety(PerkStatePersistence.GetInt(PERK_ID, "saved_sat", 100));
                    SetThirstPct(PerkStatePersistence.GetInt(PERK_ID, "saved_th", 100));
                    SetHealth(PerkStatePersistence.GetInt(PERK_ID, "saved_hp", 100));
                    SetClean(PerkStatePersistence.GetInt(PERK_ID, "saved_clean", CLEAN_START));
                    SetSleep(PerkStatePersistence.GetInt(PERK_ID, "saved_sleep", SLEEP_START));
                    SetSocial(PerkStatePersistence.GetInt(PERK_ID, "saved_social", SOCIAL_START));
                }
            }
            catch { }
            EmporiumEntry emporium = EmporiumEntry.Instance;
            if (emporium == null) return;
            var allInvs = new System.Collections.Generic.List<GameInventory>();
            try { var v = emporium.backInvinvElement as GameInventory; if (v != null) allInvs.Add(v); } catch { }
            try { var v = emporium.backInvinvElementCounter as GameInventory; if (v != null) allInvs.Add(v); } catch { }
            try { var v = emporium.showcaseElement as GameInventory; if (v != null) allInvs.Add(v); } catch { }
            try { var v = emporium.invElement as GameInventory; if (v != null) allInvs.Add(v); } catch { }
            try { var v = emporium.frontInvinvElement as GameInventory; if (v != null) allInvs.Add(v); } catch { } // 09-10 补 frontInv（前台）——升级的包放前台时恢复漏找
            try { var v = emporium.hiddenElement as GameInventory; if (v != null) allInvs.Add(v); } catch { } // 09-14 补 hiddenElement（海报后边 2×2）——位置方案
            var visited = new HashSet<IntPtr>();
            var stack = new Stack<GameInventory>(allInvs);
            while (stack.Count > 0)
            {
                var inv = stack.Pop();
                if (inv == null || inv.childItems == null) continue;
                for (int i = 0; i < inv.childItems.Count; i++)
                {
                    var it = inv.childItems[i];
                    if (it == null || !visited.Add(it.Pointer)) continue;
                    try
                    {
                        var cw = it.contentWindow;
                        if (cw == null || cw.childElement == null) continue;
                        var inner = cw.childElement.Cast<GameGridInventory>();
                        if (inner != null && !allInvs.Contains(inner)) { allInvs.Add(inner); stack.Push(inner); }
                    }
                    catch { }
                }
            }
            int restored = 0;
            foreach (var inv in allInvs)
            {
                if (inv == null || inv.childItems == null) continue;
                foreach (var item in inv.childItems)
                {
                    if (item == null) continue;
                    try
                    {
                        // 09-14 位置方案：hiddenElement（海报后边）物品按索引 PlayerPrefs 强恢复（tag 全丢无法识别）
                        int _hidx = ContainerUpgradeV2.FindBoxInHidden(item);
                        int _hstage = ContainerUpgradeV2.GetHiddenStageByIndex(_hidx);
                        if (_hstage > 0) { ContainerUpgradeV2.RestoreWageBoxToStage(item, _hstage); _rcRestoredContainers.Add(item.Pointer); restored++; continue; }
                        if (ContainerUpgradeV2.IsWageBox(item))
                        {
                            try { if (!item.IsTag("CUSTOM_STORAGE_TAG")) item.EnableTag("CUSTOM_STORAGE_TAG"); } catch { } // 老档箱子补打 tag（09-13：缺 tag 导致升级挂点不识别）
                            ContainerUpgradeV2.RestoreWageBoxShape(item); // 蛙哥箱子：按段位恢复（含老档满级迁移）
                            _rcRestoredContainers.Add(item.Pointer);
                            restored++;
                            continue;
                        }
                        if (!item.IsTag("CONTAINER_TAG") || item.IsTag("VOID_BEAD_TAG") || ContainerUpgradeV2.IsVoidBeadStorage(item) || ContainerUpgradeV2.IsExcludedContainer(item)) continue;
                        // 容器v2：按段位恢复（含老档 wageUpgradeCap>0 → 满级迁移）；未升级老档保持现状
                        if (ContainerUpgradeV2.RestoreCrusoeShape(item)) { _rcRestoredContainers.Add(item.Pointer); restored++; }
                    }
                    catch { }
                }
            }
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] 读档恢复容器异常: " + ex.Message); }
    }

    // ===== 容器升级读档恢复（主修，抄虚空珠 PostfixSetContentWindow 挂点）：窗口构建后识别升级容器并恢复 SetShape =====
    public static void PostfixSetContentWindow_IngotContainer(GameItem __instance)
    {
        try
        {
            if (__instance == null || !IsActive()) return;
            // 09-14 位置方案：海报后边 hiddenElement 物品 tag 全丢 → 按索引 PlayerPrefs 强恢复（优先于 IsWageBox）
            try
            {
                int _hidx = ContainerUpgradeV2.FindBoxInHidden(__instance);
                int _hstage = ContainerUpgradeV2.GetHiddenStageByIndex(_hidx);
                if (_hstage > 0) { ContainerUpgradeV2.RestoreWageBoxToStage(__instance, _hstage); _rcRestoredContainers.Add(__instance.Pointer); return; }
            }
            catch { }
            if (ContainerUpgradeV2.IsWageBox(__instance))
            {
                try { if (!__instance.IsTag("CUSTOM_STORAGE_TAG")) __instance.EnableTag("CUSTOM_STORAGE_TAG"); } catch { } // 老档补打
                if (_rcRestoredContainers.Add(__instance.Pointer)) ContainerUpgradeV2.RestoreWageBoxShape(__instance); // 蛙哥箱子
                return;
            }
            if (!__instance.IsTag("CONTAINER_TAG") || __instance.IsTag("VOID_BEAD_TAG") || ContainerUpgradeV2.IsVoidBeadStorage(__instance) || ContainerUpgradeV2.IsExcludedContainer(__instance)) return;
            if (!ContainerUpgradeV2.HasTag(__instance, "wb_stage") && GetTagIntSafe(__instance, "wageUpgradeCap") <= 0) return; // 未升级老档不恢复
            if (!_rcRestoredContainers.Add(__instance.Pointer)) return; // 已恢复过：跳过防双加
            ContainerUpgradeV2.RestoreCrusoeShape(__instance);
        }
        catch { }
    }

    internal static int GetTagIntSafe(GameItem item, string tag)
    {
        try { var t = item.GetTagReadonly(tag); if (t != null) return t.valueInt; } catch { }
        return 0;
    }
    internal static void AddTagInt(GameItem item, string tag, int delta)
    {
        try
        {
            int v = GetTagIntSafe(item, tag);
            SetTagIntValue(item, tag, v + delta);
        }
        catch { }
    }

    // ===== 机器 tooltip 升级提示（拆包 2.5.31/2.5.32 复核：机器悬停 = MachineryHelper.CreateMachineryTooltip(RichTextBuilder, GameItem)
    // 2 参 public static；AddTooltipModuleBonus 真实签名 = string×3（默认值），非 int×3——挂入口 CreateMachineryTooltip 最省事）=====
    public static void PostfixCreateMachineryTooltip(RichTextBuilder builder, GameItem item)
    {
        try
        {
            bool robC = IsActive();
            // 09-15 养蛊机/生成器 tooltip（全局机器，不绑定职业）：充能/抽卡进度可视化
            if (item != null && (item.identifier == GuMachineSystem.GU_MACHINE_ID || item.identifier == GuMachineSystem.AI_GENERATOR_ID))
            {
                if (builder == null) return;
                if (item.identifier == GuMachineSystem.GU_MACHINE_ID)
                {
                    int charge = GetTagIntSafe(item, GuMachineSystem.GU_CHARGE_TAG);
                    builder.AddLine(LangHelper.T(
                        "◆ 充能 " + charge + "/3（打烊 +1，满 3 自动炼蛊·需舱内≥2模组）",
                        "◆ Charge " + charge + "/3 (+1 at close, auto-forge at 3, needs ≥2 modules)"), bold: true);
                }
                else
                {
                    // 09-19 P3：显示当前模式（读舱内保护器实时判定）+ 失败结果提示
                    bool hasProt = false;
                    try
                    {
                        var ggrid = GuMachineSystem.GetGuGrid(item);
                        if (ggrid != null && ggrid.childItems != null)
                            foreach (var m in ggrid.childItems)
                                if (m != null) { string mid = ""; try { mid = m.identifier ?? ""; } catch { } if (mid == GuMachineSystem.PROTECTOR_ID) { hasProt = true; break; } }
                    }
                    catch { }
                    string mode = hasProt
                        ? LangHelper.T("阉割版（100%成功，上限75%）", "Stable (100% success, cap 75%)")
                        : LangHelper.T("不稳定版（50%成功，失败产报废模组）", "Unstable (50% success, fail -> scrap module)");
                    builder.AddLine(LangHelper.T(
                        "◆ 打烊自动抽卡（舱内≥2模组）· 当前：" + mode,
                        "◆ Auto-draw at close (≥2 modules) · Now: " + mode), bold: true);
                }
                return;
            }
            if ((!robC && !(WaterMerchantPerk.IsActive() && IsBottlePrinter(item))) || builder == null || item == null) return;
            // 09-15 瓶印机专属 tooltip：显示质量实际加成（读 Getter 自动适配两职业倍率）
            if (IsBottlePrinter(item))
            {
                int q = 0;
                try { q = Il2Cpp.MachineryHelper.GetCurrentQualityBonus(item); } catch { }
                builder.AddLine(LangHelper.T(
                    "◆ 金属锭升级：质量 +" + q + "%（拖 metal_ingot 继续 +2%）",
                    "◆ Ingot upgrade: Quality +" + q + "% (drag metal_ingot +2%/each)"), bold: true);
                return;
            }
            if (!IsMachine(item)) return;
            int pct = GetTagIntSafe(item, "wageUpgradePct");
            int effv = GetTagIntSafe(item, "wageUpgradeEff");
            if (pct <= 0 && effv <= 0)
            {
                builder.AddLine(LangHelper.T("◆ 金属锭升级：拖 metal_ingot 到机器 +1%/次（性能/效率/质量）", "◆ Ingot upgrade: drag metal_ingot to machine +1%/each (Perf/Eff/Quality)"), bold: true);
                return;
            }
            builder.AddLine(LangHelper.T("◆ 金属锭升级：性能+" + pct + "% 效率+" + effv + "%（拖 metal_ingot 继续 +1%）", "◆ Ingot upgrade: Perf +" + pct + "% Eff +" + effv + "% (keep dragging metal_ingot +1%)"), bold: true);
        }
        catch { }
    }

    // ===== 面板显示 0.5（用户拍板 09-09："机器面板显示的数字"；拆包：CreateModuleTooltip/AddModuleStatLine 直读 tag
    // 不经 Getter → 面板显示原值、产出已减半，两者不同源。此 Postfix 在统计显示行统一减半性能/效率/质量，面板=实际）=====
    public static void PostfixAddModuleStatLine(string statName, ref int baseValue, ref int tempValue)
    {
        try
        {
            if (!IsActive()) return;
            if (statName == null) return;
            // statName 可能是 tag 名或本地化显示名，双匹配（大小写不敏感）
            string n = statName.ToLowerInvariant();
            if (n.Contains("performance") || n.Contains("efficiency") || n.Contains("quality")
                || n.Contains("性能") || n.Contains("效率") || n.Contains("质量"))
            {
                if (baseValue > 0) baseValue = Math.Max(0, (int)(baseValue * 0.5));
                if (tempValue > 0) tempValue = Math.Max(0, (int)(tempValue * 0.5));
            }
        }
        catch { }
    }
    // ===== 机器 0.5 总系数（用户拍板 09-09；拆包 2.5.27 [L1]：逐台 Postfix ×0.5 含基础，不写 tag——写 -50 只有 2 条路径天然减半）=====
    // water_recycler 效率：ApplyPerformanceWaterRecyclerEffect(GameItem) 后效率 tag ×0.5（含 50 基础；船舶系统）
    public static void PostfixApplyPerformanceWaterRecyclerEffect(GameItem __0)
    {
        try
        {
            if (!IsActive() || __0 == null) return;
            var ts = __0.GetTagReadonly("WATER_RECYCLER_CURRENT_EFFICIENCY_INT");
            if (ts != null && ts.valueInt > 0) SetTagIntValue(__0, "WATER_RECYCLER_CURRENT_EFFICIENCY_INT", Math.Max(0, (int)(ts.valueInt * 0.5)));
        }
        catch { }
    }
    // ===== 容器获得即减半（用户拍板 09-09：改挂点——任何容器/机器储存区物品获得时减半，可拖 junk 升级恢复）=====
    // ContainerHelper.InitContainerItem = 容器物品初始化统一入口（custom_storage_box 走它；官方容器同链）
    public static void PostfixInitContainerItem(GameInventory __0, GameItem __1)
    {
        try
        {
            if (!IsActive() || __0 == null || __1 == null) return;
            if (!__1.IsTag("CONTAINER_TAG") || __1.IsTag("VOID_BEAD_TAG") || ContainerUpgradeV2.IsWageBox(__1) || ContainerUpgradeV2.IsVoidBeadStorage(__1) || ContainerUpgradeV2.IsExcludedContainer(__1)) return;
            if (ContainerUpgradeV2.HasTag(__1, "wb_stage")) return; // 容器v2：已有段位（读档/已减半）→ 不重复减半
            ShrinkInv(__0 as GameGridInventory, GetId(__1) + "(容器获得减半)", __1);
            try { __1.EnableTag("CONTAINER_TOOLTIP_TAG"); } catch { } // 容量行显示门控（拆包 2.5.32）
        }
        catch { }
    }

    // ===== 容器开局容量减半（用户拍板 09-09 修正2：玩家主背包恢复原样；减半对象=机器上储存区/机器箱子/背包内容器物品）=====
    public static void PostfixEmporiumEntryStart(EmporiumEntry __instance)
    {
        try
        {
            if (!IsActive() || __instance == null) return;
            // 遍历柜台+背包内所有容器物品（machine_bay/机器内嵌箱子/custom_storage_box）与机器内部库存，减半
            ShrinkContainerItems(__instance.frontInvinvElement as GameInventory);
            ShrinkContainerItems(__instance.backInvinvElement as GameInventory);
            ShrinkContainerItems(__instance.invElement as GameInventory);
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] 容器减半异常: " + ex.Message); }
    }
    // 遍历库存内的容器物品，对其内部库存减半（储存箱子/柜子等）
    private static void ShrinkContainerItems(GameInventory inv)
    {
        try
        {
            if (inv == null || inv.childItems == null) return;
            var list = new System.Collections.Generic.List<GameItem>();
            foreach (var item in inv.childItems) { if (item != null) list.Add(item); }
            foreach (var item in list)
            {
                try
                {
                    if (item.IsTag("VOID_BEAD_TAG") || ContainerUpgradeV2.IsWageBox(item) || ContainerUpgradeV2.IsVoidBeadStorage(item) || ContainerUpgradeV2.IsExcludedContainer(item)) continue;
                    if (!item.IsTag("CONTAINER_TAG") && !IsMachine(item)) continue;
                    if (ContainerUpgradeV2.HasTag(item, "wb_stage")) continue; // 容器v2：已按段位管理，不重复减半
                    var grid = GetContainerGrid(item);
                    if (grid != null) ShrinkInv(grid, GetId(item) + "(储存区/机器箱)", item);
                }
                catch { }
            }
        }
        catch { }
    }
    // 真减半：读运行时 inventoryShape（GridShape 接口，实际 GridShapeBuilder 实现）的 width/height → SetShape(半宽, 高)
    // 保底：现有物品数 +5 格；宽度下限 4（防极端容器）
    private static void ShrinkInv(GameGridInventory inv, string label, GameItem item = null)
    {
        if (inv == null) return;
        // 拆包 2.5.32 六：容器容量行显示 = CONTAINER_TAG + CONTAINER_TOOLTIP_TAG（EnableTag 后原生 tooltip 自动显示容量）
        try
        {
            var shape = inv.inventoryShape;
            if (shape == null) {  return; }
            int w = shape.width, h = shape.height;
            if (w <= 0 || h <= 0) { Core.LogMsg("[空间站鲁滨逊] " + label + " 宽高异常(" + w + "x" + h + ")，跳过减半"); return; }
            int items = 0;
            try { items = inv.childItems != null ? inv.childItems.Count : 0; } catch { }
            int nw = Math.Max(4, w / 2);
            int cap = nw * h;
            if (items + 5 > cap) nw = Math.Max(4, (int)Math.Ceiling((items + 5) / (double)h)); // 保底不丢货
            // 容器v2：减半 = 段0（50%）；记录段位 + 官方原宽（读档按段位重设）
            if (item != null) { ContainerUpgradeV2.SetTagIntValue(item, "wb_stage", 0); ContainerUpgradeV2.SetTagIntValue(item, "wb_orig_w", w); }
            // 字符串重载（同 L1778：自动 ValidateBackground；全开放矩形 '0'=可放）
            inv.SetShape(new string('0', nw * h), nw);
            try { inv.Validate(); } catch { }
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] " + label + " 减半异常: " + ex.Message); }
    }
    // ===== 机器 0.5 总系数（拆包 2.5.28 补齐）=====
    // moisture_farm 产出量：MachineMoistureFarm.GetOutputVolume(GameItem)→int ×0.5（含基础）
    public static void PostfixGetOutputVolume(ref int __result)
    {
        try { if (IsActive() && __result > 0) __result = Math.Max(1, (int)(__result * 0.5)); } catch { }
    }
    // water_purifier 基础半：WaterHelper.RemoveContaminantFromContainer 返回移除量 ×0.5（净化慢一半；加成半已被模板0.5覆盖）
    public static void PostfixRemoveContaminantFromContainer(ref int __result)
    {
        try { if (IsActive() && __result > 0) __result = Math.Max(1, (int)(__result * 0.5)); } catch { }
    }
    // 拾荒消耗睡眠（v5.7+：外出拾荒睡眠 -15%，疲劳影响拾荒次数/效率）
    public static void PostfixScavengeDumpingGrounds()
    {
        try
        {
            if (!IsActive()) return;
            SetSleep(Math.Max(0, GetSleep() - SLEEP_SCAV_LOSS));
            // 09-13 用户拍板 v1 定稿：拾荒每次 -2 清洁（单一场景）
            SetClean(Math.Max(0, GetClean() - BuildConfig.CleanScavCost));
        }
        catch { }
    }
    // ===== 房东批发商库存随机化（拆包 [L1] 2.5.20.2：4件硬编码在闭包 b__31_0，Prefix 替换跳原生）=====
    public static bool PrefixLandlordWholesaleStock()
    {
        try
        {
            if (!IsActive()) return true; // 非职业走原生
            var ps = Il2Cpp.PlayerStore.Instance;
            if (ps == null) return false;
            int day = DeterministicSchedule.CurrentDay;
            int seed = (DeterministicSchedule.GetRunKey() + "_ld" + day).GetHashCode(); // 同日确定性（读档不换货），System.Random 不污染 Unity RNG
            var rng = new System.Random(seed);
            int n = rng.Next(3, 7); // 3-6 件
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
                catch { }
            }
            return false; // 跳过原生 4 件
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] PrefixLandlordWholesaleStock 异常: " + ex.Message); }
        return true;
    }
    // ===== 同行+供货商同来（拆包 [L1] 2.3.14：原生每周日 HandleSupplierClient→CreateSupplier；Postfix 追加 CreateMerchant）=====
    public static void PostfixHandleSupplierClient()
    {
        try
        {
            if (!IsActive()) return;
            int day = DeterministicSchedule.CurrentDay;
            if (day <= 0 || day % 7 != 0) return; // 每周日（7/14/21…）
            var scm = GetStoreClientManager();
            if (scm == null) return;
            var merchant = Il2Cpp.StoreClientList.CreateMerchant();
            if (merchant == null) return;
            scm.AddClient(merchant); // AddClient 追加不替换（拆包：同天可加多个）
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] PostfixHandleSupplierClient 异常: " + ex.Message); }
    }
    // ===== 三种生存结局（拆包 2.5.21：ending 全集14个无 starvation/thirst/disease，Prefix 自定义 reason/desc + DisplayUI(…,false)）=====
    public static bool PrefixExecuteGameOver(ref string ending)
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
            else return true; // 其他 ending 走原生
            var go = Il2Cpp.GameOverUIManager.Instance;
            if (go != null)
            {
                go.DisplayUI(reason, desc, false);
            }
            return false; // 跳过原生（无这三个分支）
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] PrefixExecuteGameOver 异常: " + ex.Message); }
        return true;
    }
    // 09-20 昏迷强制过夜：死亡事件标志（三死/失血 → ExecuteGameOverBy 置位 → ForceComaSkip 停止跳天）
    private static bool _gameOverTriggered = false;

    // 09-20 拍板：昏迷当天立刻强制过夜 ×3（实际日期 +3，跳过 3 天）；跳天中死亡立即停止
    private static void ForceComaSkip()
    {
        try
        {
            var ps = Il2Cpp.PlayerStore.Instance;
            if (ps == null) return;
            _gameOverTriggered = false;
            for (int i = 0; i < 3; i++)
            {
                try { Il2Cpp.StoreUIManager.Instance.CloseAllUI(); } catch { }
                try { ps.EndDay(); } catch { }
                try { ps.EndNight(); } catch { }
                try { var sem = Il2Cpp.StoreStation.instance != null ? Il2Cpp.StoreStation.instance.storeEventManager : null; if (sem != null) sem.OnDayEnd(); } catch { }
                try { ps.BeginDay(); } catch { }
                // 防双兜底：若原生链未触发鲁滨逊每日结算（StoreClientManager.OnNewDay Postfix），blood_rest 未递减 → 显式结算一次
                try
                {
                    int rest = PerkStatePersistence.GetInt(PERK_ID, "blood_rest", 0);
                    if (rest > 0 && rest == 3 - i) PostfixOnNewDay();
                }
                catch { }
                if (_gameOverTriggered) return; // 跳天中死亡（三死/失血）→ 立即停止
            }
            try { ps.SaveGame(); } catch { } // 3 天无死亡才存档
            RefreshStatusPanel();
        }
        catch (Exception ex) { Core.LogMsg("[鲁滨逊] 昏迷跳天异常: " + ex.Message); }
    }
    private static void ExecuteGameOverBy(string ending)
    {
        try
        {
            _gameOverTriggered = true; // 09-20 昏迷跳天循环检测：任何死亡事件（三死/失血）立即置标志停止跳天
            var ps = Il2Cpp.PlayerStore.Instance;
            if (ps != null) { ps.ExecuteGameOver(ending);  }
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] ExecuteGameOverBy 异常: " + ex.Message); }
    }

    // ===== 救场安全网（v5.7：濒饿5天送食 / 病危3天送药，不删档）=====
    private static void RescueFeed()
    {
        try
        {
            if (PerkStatePersistence.GetInt(PERK_ID, "robinson_hard", 0) == 1) return; // 困难模式：无救助（濒饿直接 GameOver）
            int n = UnityEngine.Random.Range(1, 3); // 1-2 份
            // 2026-09-09 修复：按更缺的送（口渴更缺送水，否则送食）——避免濒饿送食物、濒渴送错
            bool giveWater = GetThirstPct() < GetSatiety();
            for (int i = 0; i < n; i++) GiveToBackpack(giveWater ? "bottled_water" : "processed_meat", 1);
            try { StoreUIManager.Instance.Notify(LangHelper.T("好心客户送来了 " + n + " 份" + (giveWater ? "水" : "食物") + "，先撑住", "A kind customer sent " + n + " " + (giveWater ? "waters" : "food") + " — hang in there"), "green"); } catch { }
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] RescueFeed 异常: " + ex.Message); }
    }
    // 2026-09-09 新增：濒渴第3天送水（渴死第4天判定前，救场缓刑）
    private static void RescueWater()
    {
        try
        {
            if (PerkStatePersistence.GetInt(PERK_ID, "robinson_hard", 0) == 1) return; // 困难模式：无救助（濒渴直接 GameOver）
            // 09-20 用户拍板：送水参照开局——带水瓶子（普通瓶 bottled_water + 普通质量水 grade=2；原 GiveToBackpack 是空瓶）
            EmporiumEntry em2 = EmporiumEntry.Instance;
            if (em2 != null && em2.backInvinvElement != null)
            {
                var inv2 = (GameInventory)em2.backInvinvElement;
                for (int wi = 0; wi < 2; wi++)
                {
                    GameItem witem = DirectoryMaster.Item("bottled_water", true); // 09-20 用户拍板：普通瓶+普通质量水（grade=2 基准）；不用大瓶/高质水
                    if (witem == null) witem = Il2Cpp.WaterPremadeHelper.AccurateHighQualityWater("bottled_water"); // 兜底：至少带水普通瓶
                    else { try { Il2Cpp.WaterHelper.AddWater(witem, 2, -1, false, 0, 1, true); } catch { } }
                    try { witem.DisableTag("stolen", true); } catch { }
                    var wslot = em2.backInvinvElement.TryFindOneValidInventorySlot(witem, false);
                    if (wslot != null) { try { wslot.TryAcceptOnce(); continue; } catch { } }
                    inv2.UncheckedAccept(witem);
                }
            }
            try { StoreUIManager.Instance.Notify(LangHelper.T("好心客户送来了 2 份水，先撑住", "A kind customer sent 2 waters — hang in there"), "green"); } catch { }
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] RescueWater 异常: " + ex.Message); }
    }
    private static void RescueMedicine()
    {
        try
        {
            if (PerkStatePersistence.GetInt(PERK_ID, "robinson_hard", 0) == 1) return; // 困难模式：无救助（病危直接 GameOver）
            GiveToBackpack("bandage_item", 1);
            try { StoreUIManager.Instance.Notify(LangHelper.T("好心客户送来了药品，快用上", "A kind customer sent medicine — use it now"), "green"); } catch { }
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] RescueMedicine 异常: " + ex.Message); }
    }
    // 受伤判定：woundState > 0（[L1] PlayerStore.healthData@0x2B8 → HealthData.woundState@0x24；IsSeriouslyWounded=woundState>5）
    private static bool IsWounded()
    {
        try
        {
            var ps = Il2Cpp.PlayerStore.Instance;
            if (ps == null || ps.healthData == null) return false;
            return ps.healthData.woundState > 0;
        }
        catch { }
        return false;
    }
    // 伤口稳定判定（打绷带/治疗后 isWoundStable=true，与捡漏直觉同语义；[L1] 原版 HealthData 字段）
    private static bool IsWoundStable()
    {
        try
        {
            var ps = Il2Cpp.PlayerStore.Instance;
            if (ps == null || ps.healthData == null) return false;
            return ps.healthData.isWoundStable;
        }
        catch { }
        return false;
    }
    // ===== 房租改造：前99天免租，每100天收一次递增房租（第100天15000、第200天20000、第300天25000…）拆包 2.5.20 =====
    public static bool PrefixCheckRentDay()
    {
        try
        {
            if (!IsActive()) return true;
            int day = DeterministicSchedule.CurrentDay;
            int rent = RentForDay(day);
            if (rent <= 0) return false; // 非收租日：跳过原生每周收租
            var ps = Il2Cpp.PlayerStore.Instance;
            if (ps == null) return false;
            // A1 防重（09-17）：读档重放当天 CheckRentDay → 不重复扣租（照 ApplyBadLuck per-runID 先例）
            string _runId = ""; try { _runId = ps.runID ?? ""; } catch { }
            string _rentKey = "WagesRentPaidDay_Run_" + _runId;
            if (UnityEngine.PlayerPrefs.GetInt(_rentKey, -1) == day) return false;
            bool enough = ps.playerCash >= rent;
            ps.playerCash -= rent; // 09-17 强制扣（现金不足也扣成负数——用户拍板）
            UnityEngine.PlayerPrefs.SetInt(_rentKey, day); // A1 防重记录
            if (enough)
                try { StoreUIManager.Instance.Notify(LangHelper.T("交租 " + rent + "（第" + day + "天）", "Rent due: " + rent + " (day " + day + ")"), "green"); } catch { }
            else
                try { StoreUIManager.Instance.Notify(LangHelper.T("房东来收 " + rent + "，现金不足，强制扣除！", "The landlord is here for " + rent + " - not enough cash, forcibly deducted!"), "red"); } catch { }
            return false; // 不走原生收租链（原生是每周涨租模式）
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] PrefixCheckRentDay 异常: " + ex.Message); }
        return true;
    }

    // ===== 房东每周来卖货（拆包 2.5.20：原生仅 day18 一次；仿 XIAOWO 4.5.6 每周生成 LandlordWholesale）=====
    // 触发日：第4天起每周（day%7==4：第4/11/18/25…天）；day18 原生已有房东，mod 跳过避免双房东
    public static void PostfixHandleNormalClient()
    {
        try
        {
            if (!IsActive()) return;
            int day = DeterministicSchedule.CurrentDay;
            if (day % 7 != 4 || day == 18) return; // 每周四（第4天起），day18 交给原生
            var scm = GetStoreClientManager();
            if (scm == null) return;
            var landlord = Il2Cpp.StoreClientUniqueList.LandlordWholesale();
            if (landlord == null) return;
            scm.AddClient(landlord);
            // 友商（merchant）陪房东一起来（拆包 09-10：原版收租日 CheckRentDay 内生成，被 PrefixCheckRentDay 拦截 → 改由每周房东日补生成；merchant 只买玩家非违禁品）
            try
            {
                var merchant = Il2Cpp.StoreClientList.CreateMerchant();
                if (merchant != null) { scm.AddClient(merchant); Core.LogMsg("[空间站鲁滨逊] 友商(merchant)已与房东同日加入队列"); }
            }
            catch (Exception mex) { Core.LogMsg("[空间站鲁滨逊] 生成友商异常: " + mex.Message); }
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] PostfixHandleNormalClient 异常: " + ex.Message); }
    }
    private static StoreClientManager GetStoreClientManager()
    {
        try
        {
            var ps = Il2Cpp.PlayerStore.Instance;
            if (ps == null) return null;
            return ps.storeClientManager;
        }
        catch { }
        return null;
    }


    // ===== 租金表（09-17 用户拍板：49/50 各2500、100→10000、150→20000、200→50000、250→70000、250后每50天封顶70000；不足强制扣负）=====
    private static int RentForDay(int day)
    {
        if (day == 49 || day == 50) return 2500;
        if (day == 100) return 10000;
        if (day == 150) return 20000;
        if (day == 200) return 50000;
        if (day == 250) return 70000;
        if (day > 250 && day % 50 == 0) return 70000;
        return 0; // 非收租日
    }
    private static int NextRentDay(int day)
    {
        if (day < 49) return 49;
        if (day < 50) return 50;
        if (day < 100) return 100;
        if (day < 150) return 150;
        if (day < 200) return 200;
        if (day < 250) return 250;
        return ((day / 50) + 1) * 50; // 250 后每 50 天
    }
    // ===== 租金显示同步为100天制（拆包 [L1]：日历/开始日/店内日历都读 dayUntilRent+rentValue）=====
    private static void SyncRentDisplay()
    {
        try
        {
            var ps = Il2Cpp.PlayerStore.Instance;
            if (ps == null) return;
            int day = DeterministicSchedule.CurrentDay;
            int nextDay = NextRentDay(day);
            ps.dayUntilRent = nextDay - day;
            ps.rentValue = RentForDay(nextDay);
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] SyncRentDisplay 异常: " + ex.Message); }
    }

    // 日历收租行强制显示 + 自定义文案（拆包：temporaryRent==0 时原生隐藏；landlordTMP@0x138 / landlordNoticeBox@0x148）
    public static void PostfixOnCalendarButtonClicked(Il2Cpp.AdvCalendarUIManager __instance)
    {
        try
        {
            if (!IsActive() || __instance == null) return;
            var ps = Il2Cpp.PlayerStore.Instance;
            if (ps == null) return;
            if (ps.IsPropertyPaid) return; // 房产已付清不显示
            int day = DeterministicSchedule.CurrentDay;
            int nextDay = NextRentDay(day);
            int due = nextDay - day;
            int rent = RentForDay(nextDay);
            string dueTxt = due == 0 ? LangHelper.T("今天", "today") : (due == 1 ? LangHelper.T("明天", "tomorrow") : LangHelper.T(due + "天后", "in " + due + " days"));
            if (__instance.landlordTMP != null)
                __instance.landlordTMP.text = LangHelper.T("房租 " + rent + " 将于" + dueTxt + "收取", "Rent " + rent + " due " + dueTxt);
            if (__instance.landlordNoticeBox != null) __instance.landlordNoticeBox.SetActive(true);
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] PostfixOnCalendarButtonClicked 异常: " + ex.Message); }
    }

    // 店内日历（墙上日历 StoreCalendar.Update，拆包：原生用 GetRentDayCounter 每周显示）→ 覆盖为100天制（dayTMP@0x18）
    public static void PostfixStoreCalendarUpdate(Il2Cpp.StoreCalendar __instance)
    {
        try
        {
            if (!IsActive() || __instance == null) return;
            var ps = Il2Cpp.PlayerStore.Instance;
            if (ps == null) return;
            if (ps.IsPropertyPaid) return;
            int day = DeterministicSchedule.CurrentDay;
            int nextDay = NextRentDay(day);
            int due = nextDay - day;
            int rent = RentForDay(nextDay);
            string dueTxt = due == 0 ? LangHelper.T("今天", "today") : (due == 1 ? LangHelper.T("明天", "tomorrow") : LangHelper.T(due + "天后", "in " + due + " days"));
            string txt = LangHelper.T("房租 " + rent + " " + dueTxt + "收取", "Rent " + rent + " due " + dueTxt);
            if (__instance.dayTMP != null && __instance.dayTMP.text != txt) // 防每帧重复 set
                __instance.dayTMP.text = txt;
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] PostfixStoreCalendarUpdate 异常: " + ex.Message); }
    }

    // 开始新一天界面收租提醒：强制显示 + 100天制文案（拆包：原生仅 GetRentDayCounter()≤2 才显示）
    public static void PostfixStartOfDayInitPanel(Il2Cpp.StartOfDayUIManager __instance)
    {
        try
        {
            if (!IsActive() || __instance == null) return;
            var ps = Il2Cpp.PlayerStore.Instance;
            if (ps == null) return;
            if (ps.IsPropertyPaid) return;
            int day = DeterministicSchedule.CurrentDay;
            int nextDay = NextRentDay(day);
            int due = nextDay - day;
            int rent = RentForDay(nextDay);
            string dueTxt = due == 0 ? LangHelper.T("今天", "today") : (due == 1 ? LangHelper.T("明天", "tomorrow") : LangHelper.T(due + "天后", "in " + due + " days"));
            if (__instance.rentReminderTMP != null)
                __instance.rentReminderTMP.text = LangHelper.T("房租 " + rent + " 将于" + dueTxt + "收取", "Rent " + rent + " due " + dueTxt);
            if (__instance.rentReminder != null)
                __instance.rentReminder.SetActive(true);
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] PostfixStartOfDayInitPanel 异常: " + ex.Message); }
    }

    private static int GetScavAttempts()
    {
        try
        {
            var ps = Il2Cpp.PlayerStore.Instance;
            if (ps == null) return 0;
            return ps.scavengingAttempts;
        }
        catch { }
        return 0;
    }

    // ===== 每日结算（OnNewDay Postfix）=====
    public static void PostfixOnNewDay()
    {
        try
        {
            if (!IsActive()) return;
            _pickSkipToday = 0;
            SyncRentDisplay(); // 租金显示同步为100天制（拆包：日历读 dayUntilRent+rentValue）

            // ===== v5.9 节点池抽取（nodeKey 变化 → 先爆发 RollBurst 再抽池；正面节点无爆发）=====
            RollNodeFx();

            // ===== v5.9 三状态自然变化（eatEff 只影响吃食物、thirstEff 影响口渴衰减、wearEff 影响健康衰减）=====
            int sat = Math.Max(0, GetSatiety() - DAILY_SAT_LOSS - FxNum("satD"));   // 饱食 -20% + 节点衰减（嗓子冒烟 satD+10）
            int thBase = (int)(DAILY_THIRST_LOSS * GetThirstEffMult());             // 口渴 -25% × 耐旱/省水系数（v5.9 CompBuff）
            int th = Math.Max(0, GetThirstPct() - thBase - FxNum("thD"));           // 口渴 -25% + 节点衰减（肚里打鼓 thD+5）
            int h0 = GetHealth();
            int healthGain = DAILY_HEALTH_GAIN + FxNum("hR");   // 健康 +10% + 节点恢复修正（病恹恹 hR-5 / 透心凉 hR+5）
            int hDecay = (int)(FxNum("hD") * GetWearEffMult());                     // 健康衰减 × 糙人抗造系数（v5.9 CompBuff）
            int h = Math.Min(100, Math.Max(0, h0 + healthGain - hDecay));           // 健康自然变化 + 衰减修正
            if (IsBloodWeak()) h = Math.Max(0, h - 10); // 卖血虚弱（<3000）：健康衰减加速（09-17）
            SetSatiety(sat); SetThirstPct(th); SetHealth(h);
            // 新三状态结算（v5.8-8）：清洁 -10 + 节点衰减/恢复；睡眠 打烊+30（拾荒当天已 -15）+ 节点睡眠恢复 + 补觉高效；社交 接待日+5/无客日-5 + 节点
            SetClean(Math.Max(0, Math.Min(100, GetClean() - DAILY_CLEAN_LOSS - FxNum("cleanD") + FxNum("cleanR"))));
            SetSleep(Math.Min(100, Math.Max(0, GetSleep() + DAILY_SLEEP_GAIN + FxNum("sleepR") + GetCompBuffSleepRestore()))); // sleepR 符号修正（拆包 09-10：'sleepR-10'=恢复-10，减号负负得正，改加号）
            AddBlood(100); // 睡觉回血（09-17 卖血）
            TickBloodRest(); // 09-20 M5：虚弱强制休息 3 天 → 结束 ±20%
            int deals = PerkStatePersistence.GetInt(PERK_ID, "deals", 0);
            int social = GetSocial() + (deals > 0 ? DAILY_SOCIAL_GAIN : -DAILY_SOCIAL_LOSS) + FxNum("socD") + FxNum("socR");
            SetSocial(Math.Max(0, Math.Min(100, social)));
            PerkStatePersistence.SetInt(PERK_ID, "deals", 0); // 接待计数清零

            // ===== v5.9 CompBuff 每日递减（Duration 制：到期移除）=====
            TickCompBuffs();

            // ===== 心情结算（v5.7 拍板：三项≥80→+5；任一项<60→-10；60-79→不掉不涨；仅看饱/渴/健三项）+ 节点心情 + CompBuff 心情 =====
            int mood = GetMood();
            bool satOK = sat >= SATIETY_GOOD, thOK = th >= THIRST_GOOD, hOK = h >= HEALTH_GOOD;
            if (satOK && thOK && hOK) mood = Math.Min(100, mood + MOOD_UP);
            else if (sat < 60 || th < 60 || h < 60) mood = Math.Max(0, mood - (int)(MOOD_DOWN * GetMoodDampMult())); // 摆烂反弹：-10→-5（v5.9）
            mood = Math.Max(0, Math.Min(100, mood + FxNum("mood")));
            mood = Math.Min(100, mood + GetCompBuffMoodBonus()); // 病中专注/松弛自洽/清静自处 每日心情+（v5.9 CompBuff）
            SetMood(mood);

            // ===== 患病（节点池 sick+20：饿疯池恶，每日结算概率患病）=====
            if (GetSickChanceAdd() > 0)
            {
                TryInfect(GetSickChanceAdd() / 100.0);
            }

            // ===== v5.9 觅食（店内翻找，打烊结算概率；躺板板30% / 清洁<50 15% + 独狼专注觅食+20%）=====
            int hNow = GetHealth(), cNow = GetClean();
            int forageBonus = GetForageBonus(); // 独狼专注：觅食概率+20%（v5.9 CompBuff）
            if (hNow < NODE_CRIT && UnityEngine.Random.value < (0.30f + forageBonus / 100f))      // 躺板板（健康<20）：30% 翻出 1-2 份食物
                ForageIndoor(UnityEngine.Random.value < 0.5f ? 2 : 1, LangHelper.T("躺板板翻找", "Bedridden rummaging"));
            else if (cNow < 50 && UnityEngine.Random.value < (0.15f + forageBonus / 100f))         // 清洁<50（蓬头垢面+灰头土脸）：15% 翻出 1 份
                ForageIndoor(1, LangHelper.T("店内翻找", "In-store rummaging"));

            // ===== 连续计数（濒饿/渴/病危）=====
            bool starving = sat < NODE_CRIT || th < NODE_CRIT;          // 濒饿：饱食<20 或 口渴<20
            int sd5 = starving ? GetStarveDays() + 1 : 0;
            int td5 = th < NODE_CRIT ? GetThirstDeathDays() + 1 : 0;    // 渴死独立（口渴<20 连续）
            int cd5 = h < NODE_CRIT ? GetCritDays() + 1 : 0;            // 病危：健康<20 连续
            PerkStatePersistence.SetInt(PERK_ID, "starveDays", sd5);
            PerkStatePersistence.SetInt(PERK_ID, "thirstDeath", td5);
            PerkStatePersistence.SetInt(PERK_ID, "critDays", cd5);

            // ===== 救场安全网（v5.7：濒饿5天送食 / 病危3天送药；2026-09-09 修复：渴死4天快于濒饿救场5天 → 渴死前第3天补送水）=====
            if (sd5 == 5) RescueFeed();
            else if (td5 >= 3 && sd5 < 5) RescueWater();                       // 濒渴第3天送水（第4天渴死前；饿优先）
            else if (cd5 == 3 && sd5 < 5 && td5 < 3) RescueMedicine();         // 病危第3天送药（第4天病死前）

            // ===== 三种生存死亡（用户拍板 09-09 方案A：饿5/渴4/病4，持续天数；救场当天豁免、送食/药后仍恶化才死）=====
            if (sd5 > 5) { ExecuteGameOverBy("starvation"); return; }          // 濒饿第5天送食，仍持续（第6天起）饿死
            if (td5 >= 4 && sd5 < 5) { ExecuteGameOverBy("thirst"); return; }  // 濒渴 4 天渴死（饿优先；救场赶不上属设计）
            if (cd5 > 3 && sd5 < 5 && td5 < 4) { ExecuteGameOverBy("disease"); return; } // 病危第3天送药，仍持续（第4天起）病死

            // ===== 粮仓充盈（v5.7：饱食≥80 连续 7 天，断档归零）=====
            int gd = sat >= SATIETY_GOOD ? GetGranaryDays() + 1 : 0;
            PerkStatePersistence.SetInt(PERK_ID, "granary", gd);
            // ===== 昂扬累计（v5.7：饱食≥80 且健康≥80 每2天 +1%售价 +5%预算，封顶5，封顶后断档维持）=====
            int es = (sat >= SATIETY_GOOD && h >= HEALTH_GOOD) ? GetElevStreak() + 1 : 0;
            int ec = GetElevCount();
            if (es >= ELEV_EVERY)
            {
                ec = Math.Min(ELEV_MAX, ec + 1);
                es = 0;
            }
            PerkStatePersistence.SetInt(PERK_ID, "elevStreak", es);
            PerkStatePersistence.SetInt(PERK_ID, "elevCount", ec);
            _burstClientCut = 0; _burstSkipFlip = false; // v5.9 病恹恹爆发客流-50% 当日标记清零

            // ===== 食物衰减 + 口粮统计 =====
            int fresh = 0, stale = 0, rotten = 0;
            int foodCount = DecayFoodsAndCount(out fresh, out stale, out rotten);

            // ===== 播报（v5.8-8：锁定节点名 + 叙事标签 + 锁定效果 + 加成）=====
            int day = StoreStation.GetDayCounter();
            int sellB = GetSellBonusPct();
            int budB = GetBudgetBonusPct();
            NodeDef dn = CurrentNode();
            string nodeTxt;
            bool badNode = false;
            if (dn == null) nodeTxt = LangHelper.T("状态平稳（饱食" + sat + " 口渴" + th + " 健康" + h + "）", "Stable (" + sat + " satiety / " + th + " thirst / " + h + " health)");
            else
            {
                badNode = dn.Sev >= 4;
                string fxDesc = "";
                foreach (string f in dn.Lock) { string lb = FxLabel(f); if (lb.Length > 0) fxDesc += lb + " "; }
                string cur = GetNodeFx();
                if (!string.IsNullOrEmpty(cur) && cur != "flavor") { string lb = FxLabel(cur); if (lb.Length > 0) fxDesc += lb + " "; }
                nodeTxt = dn.DisplayName + LangHelper.T("：", ": ") + dn.DisplayTag + (fxDesc.Length > 0 ? "｜" + fxDesc.Trim() : "") + LangHelper.T("（饱食" + sat + " 口渴" + th + " 健康" + h + "）", " (" + sat + " satiety / " + th + " thirst / " + h + " health)");
            }
            string moodTxt = LangHelper.T("心情 ", "Mood ") + mood + (sellB > 0 || budB > 0 ? "｜" + LangHelper.T("售价+" + sellB + "% 预算+" + budB + "%", "Sell +" + sellB + "% Budget +" + budB + "%") : "");
            string granaryTxt = gd >= GRANARY_DAYS ? LangHelper.T("｜★粮仓充盈 售价+5%", "| Granary full, Sell +5%") : (gd > 0 ? LangHelper.T("｜粮仓 " + gd + "/7 天", "| Granary " + gd + "/7 days") : "");
            try
            {
                var ps = PlayerStore.Instance;
                if (ps != null)
                {
                    ps.AddNightLog(LangHelper.T("—— 鲁滨逊的账本 · 第 " + day + " 天 ——", "-- Robinson's Ledger · Day " + day + " --"), "#7FC97F"); // 09-22 统一柔和绿
                    ps.AddNightLog(nodeTxt + "｜" + moodTxt + granaryTxt, "#7FC97F"); // 09-22 统一柔和绿
                    ps.AddNightLog(LangHelper.T("口粮：新鲜 " + fresh + "｜变质 " + stale + "｜腐烂 " + rotten + "（共" + foodCount + "份可吃）", "Rations: fresh " + fresh + " | stale " + stale + " | rotten " + rotten + " (" + foodCount + " edible)"), "#7FC97F"); // 09-22 统一柔和绿
                }
            }
            catch { }
            try { StoreUIManager.Instance.Notify(LangHelper.T("第" + day + "天：", "Day " + day + ": ") + nodeTxt + "｜" + moodTxt, "white"); } catch { }
            RefreshStatusPanel(); // 每日结算刷新常驻面板
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] PostfixOnNewDay 异常: " + ex.Message); }
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
    // ===== 接待计数（OnDealAccepted Postfix 调用，社交结算用）=====
    internal static void RecordDeal()
    {
        try { if (IsActive()) PerkStatePersistence.SetInt(PERK_ID, "deals", PerkStatePersistence.GetInt(PERK_ID, "deals", 0) + 1); } catch { }
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

    // 救场：好心客户送食（1-2 份，不删档，lowDays 归零）
    private static void Rescue()
    {
        try
        {
            int n = UnityEngine.Random.value < 0.5f ? 1 : 2;
            GiveToBackpack("processed_meat", n);
            GivePureWaterToBackpack(1);
            PerkStatePersistence.SetInt(PERK_ID, "lowDays", 0);
            try { StoreUIManager.Instance.Notify(LangHelper.T("一位好心顾客送来了口粮和水……", "A kind customer brought rations and water..."), "green"); } catch { }
        }
        catch { }
    }

    // ===== 物品归属判定 =====
    private static bool _ownedInited = false;
    private static System.Reflection.MethodInfo _isItemOwned = null;
    // 物品是否在玩家背包或柜台（指针比较，柜台陈列/待售物品也判定为玩家可控）
    private static bool IsInBackpackOrCounter(GameItem item)
    {
        try
        {
            EmporiumEntry em = EmporiumEntry.Instance;
            if (em == null || item == null) return false;
            IntPtr targetPtr = item.Pointer;
            if (targetPtr == IntPtr.Zero) return false;
            foreach (GameInventory inv in new[] { (GameInventory)em.backInvinvElement, (GameInventory)em.frontInvinvElement })
            {
                if (inv == null || inv.childItems == null) continue;
                for (int i = 0; i < inv.childItems.Count; i++)
                {
                    var ci = inv.childItems[i];
                    if (ci != null && ci.Pointer == targetPtr) return true;
                }
            }
        }
        catch { }
        return false;
    }

    private static bool IsItemOwned(GameItem item)
    {
        if (item == null) return false;
        if (!_ownedInited)
        {
            _ownedInited = true;
            try
            {
                var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
                foreach (var assembly in assemblies)
                {
                    if (assembly.GetName().Name != "Assembly-CSharp") continue;
                    var type = assembly.GetType("GeneralHelper");
                    if (type == null)
                    {
                        var types = assembly.GetTypes();
                        foreach (var t2 in types) { if (t2.Name == "GeneralHelper") { type = t2; break; } }
                    }
                    if (type == null) break;
                    var methods = type.GetMethods(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                    foreach (var mi in methods)
                    {
                        if (mi.Name == "IsItemOwned" && mi.GetParameters().Length == 1)
                        { _isItemOwned = mi; break; }
                    }
                    break;
                }

            }
            catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] IsItemOwned 查找异常 " + ex.Message); }
        }
        if (_isItemOwned == null) return true;
        try { return (bool)_isItemOwned.Invoke(null, new object[] { item }); }
        catch { return true; }
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

    // ===== 状态客户判定（identifier 包含匹配，小写）=====
    internal static bool IsStatusClient(StoreClient client)
    {
        if (client == null) return false;
        try
        {
            string id = (client.identifier ?? "").ToLowerInvariant();
            foreach (string s in STATUS_CLIENT_IDS)
                if (id.Contains(s)) return true;
            // 工厂打标补充（desperate/wornOut/sickLowers 等未知 identifier）
            if (_statusClientSet.Contains(client)) return true;
        }
        catch { }
        return false;
    }
    internal static string GetStatusClientKind(StoreClient client)
    {
        if (client == null) return "";
        try
        {
            string id = (client.identifier ?? "").ToLowerInvariant();
            if (id.Contains("thirsty")) return "thirsty";
            if (id.Contains("hungry") || id.Contains("chef")) return "hungry";
            if (id.Contains("spacermedical")) return "injured";
            if (id.Contains("sick")) return "sick";
            if (_statusClientSet.Contains(client)) return _statusClientKind.GetValueOrDefault(client, "");
        }
        catch { }
        return "";
    }
    private static readonly HashSet<StoreClient> _statusClientSet = new HashSet<StoreClient>();
    private static readonly Dictionary<StoreClient, string> _statusClientKind = new Dictionary<StoreClient, string>();

    // 工厂 Postfix 打标（状态客户 7 工厂：thirsty/hungry/injured/sick×3/desperate/wornOut）
    // v5.9：节点 statusClient-N（蓬头垢面-30/门可罗雀-20/爱答不理-10）→ 该概率降级为普通客户（无加价/减价）
    public static void PostfixStatusClientFactory(StoreClient __result, string kind)
    {
        try
        {
            if (__result == null || !IsActive()) return;
            int cut = FxNum("statusClient");
            if (cut < 0 && UnityEngine.Random.value < (-cut) / 100.0f)
            {
                if (_statusClientSet.Contains(__result)) { _statusClientSet.Remove(__result); _statusClientKind.Remove(__result); }
                return; // 本轮降级为普通客户
            }
            _statusClientSet.Add(__result);
            if (!string.IsNullOrEmpty(kind)) _statusClientKind[__result] = kind;
        }
        catch { }
    }

    // ===== 状态客户加价系数（TryApplyTradeMarkup mode==2 调用）=====
    // 饥饿→食物×1.2 / 口渴→水×1.25 / 受伤→药×1.3 / 生病→药×1.4 / 其他品类×0.85
    internal static double GetStatusClientMarkup(StoreClient client, GameItem item)
    {
        if (client == null || item == null || !IsStatusClient(client)) return 1.0;
        string kind = GetStatusClientKind(client);
        bool food = IsFood(item), drink = IsDrink(item), med = IsMedicine(item);
        switch (kind)
        {
            case "thirsty": return drink ? 1.25 : 0.85;
            case "hungry": return food ? 1.20 : 0.85;
            case "injured": return med ? 1.30 : 0.85;
            case "sick": return med ? 1.40 : 0.85;
            default: return 1.0;
        }
    }

    // ===== T1 状态客户工厂打标包装（Core 注册 7 工厂 Postfix）=====
    // 状态客户无独立状态字段（拆包实锤），用工厂 Postfix 记录对象引用 + kind，
    // 供 IsStatusClient / GetStatusClientKind / GetStatusClientMarkup 判定
    public static void PostfixCreateThirstySpacer(StoreClient __result) => PostfixStatusClientFactory(__result, "thirsty");
    public static void PostfixCreateHungrySpacer(StoreClient __result) => PostfixStatusClientFactory(__result, "hungry");
    public static void PostfixCreateSpacerChef(StoreClient __result) => PostfixStatusClientFactory(__result, "hungry");
    public static void PostfixCreateInjuredSpacer(StoreClient __result) => PostfixStatusClientFactory(__result, "injured");
    public static void PostfixCreateSickChildCaretaker(StoreClient __result) => PostfixStatusClientFactory(__result, "sick");
    public static void PostfixCreateDesperateAddict(StoreClient __result) => PostfixStatusClientFactory(__result, "desperate");
    public static void PostfixCreateWornOutSpacer(StoreClient __result) => PostfixStatusClientFactory(__result, "wornOut");
    public static void PostfixCreateSickLowers(StoreClient __result) => PostfixStatusClientFactory(__result, "sick");

    // ===== T1 概率联动说明（v4.2 验收第13条）=====
    // T1 需求池 = 静态 List<工厂委托>，GetRandomT1XxxClient 用 RNG.GetRandomInt 均匀随机选一个（ISIL 实锤）。
    // 委托身份在 IL2CPP 二进制层不可枚举（ISIL 读不到），无法精确把状态客户占比乘 1.2/0.85——
    // 需运行时枚举池内委托（UnityExplorer）或拆生成链更深层才能精确落点。本轮不实装，避免猜测。
    // ============================================================
    // 胡安(wanted7)/李北文(wanted6)供应商（09-12 并入鲁滨逊职业；09-13 屠夫→上层厨师→胡安：卖食物）
    // 复用原版 wanted2（屠夫实体改造）/ wanted6（李北文）通缉犯实体，不自建 identifier；
    // 电话端仿原版红魔鬼/GP矿业双通道（StorePhoneClient）；名片简化=到店直接解锁电话簿（phoneState=4）；
    // 拨号即叫货（跳过原版 PhoneDialogList 对话选项——wanted 无电话对话定义）
    // ============================================================
    private const long BUTCHER_PHONE_NUMBER = 8800;   // 胡安电话（原屠夫/上层厨师，避开原版 8376/8815/56371/4615/3319/51189）
    private const long LI_BEIWEN_PHONE_NUMBER = 8801; // 李北文电话
    private static int BUTCHER_FIRST_VISIT_DAY => BuildConfig.ButcherVisitDay;   // 胡安首次上门（CFG 可调） 0-based，第14天=13；原14永不命中）
    private static int LI_BEIWEN_FIRST_VISIT_DAY => BuildConfig.LiBeiwenVisitDay; // 李北文首次上门（CFG 可调）
    private static int CALL_TO_ARRIVE_DAYS => BuildConfig.CallArriveDays;        // 电话叫货到店天数（CFG 可调）
    private static int CALL_COOLDOWN_DAYS => BuildConfig.CallCooldownDays;         // 电话冷却天数（CFG 可调）
    private static int _wantedSupplierScheduledDay = -1; // 防同日重复调度
    // 09-13 胡安货单相关：到店 SetBarterOffer 食物报价
    private static readonly string[] CHEF_FOOD_IDS = { "raw_meat", "processed_meat", "fat_meat", "small_raw_meat", "morsel", "small_morsel", "processed_cheese", "meat_scrap", "cup_noodle", "processed_juice", "energy_drink" };

    // 屠夫/李北文供应商每日调度（v1.1.6 统一挂 PlayerStore.BeginDay 可靠挂点——PostfixOnBeginDay 调用：
    // 原挂 StoreClientManager.OnNewDay 触发时机不可靠，且跳天数工具不走 BeginDay/OnNewDay 无法测试；
    // 单入口保证冷却只扣一次）
    internal static void TickWantedSupplierDaily()
    {
        try
        {
            if (!IsActive()) return;
            WantedSupplierSchedule(StoreStation.GetDayCounter());
            TickWantedPhoneCooldown();
            TryRegisterWantedPhones();
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] TickWantedSupplierDaily 异常: " + ex.Message); }
    }

    // 每日调度：第14/21天固定排首次上门（PostfixOnBeginDay 调用）
    internal static void WantedSupplierSchedule(int day)
    {
        try
        {
            if (!IsActive() || PlayerStore.Instance == null) return;
            if (day == _wantedSupplierScheduledDay) return; // 同日只排一次
            if (day == BUTCHER_FIRST_VISIT_DAY) { QueueWantedClient("wanted7"); _wantedSupplierScheduledDay = day; }
            else if (day == LI_BEIWEN_FIRST_VISIT_DAY) { QueueWantedClient("wanted6"); _wantedSupplierScheduledDay = day; }
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] WantedSupplierSchedule 异常: " + ex.Message); }
    }

    private static void QueueWantedClient(string id)
    {
        try
        {
            if (id == "wanted7") { } // wanted7=胡安 原版字典已有（拆包 09-13 [L1]）
            PlayerStore ps = PlayerStore.Instance;
            if (ps == null || ps.storeClientManager == null) return;
            try { ps.storeClientManager.RemoveDuplicateClientsByIdentifier(id); } catch { } // 防原版随机 wanted 同天撞车
            ps.QueueFuturClient(id, 0);
            Core.LogMsg("[空间站鲁滨逊] 已预约" + (id == "wanted7" ? "胡安" : "李北文") + "当天到店（" + id + "）");
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] QueueWantedClient(" + id + ") 异常: " + ex.Message); }
    }

    // 工厂创建即设显示名（Core 注册 Postfix StoreClientList.CreateWanted2/CreateWanted6）：
    // 拆包 09-13 [L1]：ResolveClientNames 在 CreateClientInstance 实例化时读 displayName，实例化后设则头顶名/横幅已锁定——
    // 提前到工厂 Postfix，让 displayName 在实例化前即为"胡安"，所有显示点（对话/电话簿/头顶/横幅）一致
    public static void PostfixCreateWanted6(Il2Cpp.StoreClient __result)
    {
        try { if (__result != null) __result.displayName = LangHelper.T("李北文", "Li Beiwen"); } catch { }
    }

    // 到店处理（Patches.PostfixSpecialNpcStartDialogue 调用）：解锁电话簿 + 上货
    internal static void WantedSupplierOnArrived(StoreClient client)
    {
        try
        {
            if (!IsActive() || client == null) return;
            string id = client.identifier;
            if (id == "wanted7") { try { client.displayName = LangHelper.T("胡安", "Juan"); } catch { } int visits = PerkStatePersistence.GetInt("juan", "visit", 0) + 1; PerkStatePersistence.SetInt("juan", "visit", visits); int lv = visits <= 2 ? 0 : (visits <= 5 ? 1 : 2); try { client.SetBarterOffer(BuildJuanOffer(lv)); } catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] 胡安报价异常: " + ex.Message); } Core.LogMsg("[空间站鲁滨逊] 胡安到店#" + visits + " Lv" + lv + "（" + (lv == 0 ? "基础" : (lv == 1 ? "中档" : "高档")) + "）"); UnlockWantedPhone(BUTCHER_PHONE_NUMBER, LangHelper.T("胡安", "Juan")); }
            else if (id == "wanted6") { try { client.displayName = LangHelper.T("李北文", "Li Beiwen"); } catch { } UnlockWantedPhone(LI_BEIWEN_PHONE_NUMBER, LangHelper.T("李北文", "Li Beiwen")); AddLiBeiwenGoods(); }
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] WantedSupplierOnArrived 异常: " + ex.Message); }
    }

    // 名片简化：到店直接解锁电话簿（phoneState=4），不发实体名片物品（自建物品 id 需注册 sprite/名称，有空物品风险）
    private static void UnlockWantedPhone(long number, string name)
    {
        try
        {
            var pc = Il2Cpp.StorePhoneClient.GetPhoneClientByNumber(number);
            if (pc == null) { Core.LogMsg("[空间站鲁滨逊] 电话端未注册 " + number); return; }
            if ((int)pc.phoneState < 4)
            {
                pc.phoneState = (Il2Cpp.StorePhoneClient.PhoneState)4; // Regular：电话簿显示 + 可拨
                pc.cooldownDuration = CALL_COOLDOWN_DAYS;
                pc.dialedBefore = true; // 拆包 09-12 [L1]：ShownInPhoneBook=(state∈{4,5} && dialedBefore≠0)，原设 false 导致电话簿不显示
                try { StoreUIManager.Instance.Notify(LangHelper.T(name + "的电话已存入电话簿，拨号即可叫货", name + "'s number saved. Dial to order supplies."), "green"); } catch { }
                Core.LogMsg("[空间站鲁滨逊] " + name + " 电话簿解锁（" + number + "）");
            }
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] UnlockWantedPhone 异常: " + ex.Message); }
    }

    // 胡安货单：到店 SetBarterOffer 食物报价（原版 CreateBarterFoodMultiOffer）
    // 胡安三档货单（09-13 用户拍板：随到店次数升级价值；Lv0 前2次 / Lv1 3-5次 / Lv2 6次+）
    // 拆包 09-13 [L1]：offerSets Action 模式 = DirectoryMaster.Item 创建 + PlayerStore.AddDirectSellingItemToTable 进交易台
    private static Il2Cpp.BarterOffer BuildJuanOffer(int lv)
    {
        var offer = Il2Cpp.BarterOfferList.CreateBarterFoodMultiOffer(); // 09-13 修复：用原版工厂构造（字段完整），防 IsBarterAcceptable NPE
        try
        {
            var ids = new System.Collections.Generic.List<string>();
            if (lv <= 0) { ids.Add("processed_meat"); ids.Add("processed_meat"); ids.Add("large_bottled_water"); }
            else if (lv == 1) { ids.Add("processed_meat"); ids.Add("processed_meat"); ids.Add("processed_cheese"); ids.Add("processed_cheese"); ids.Add("galaxy_blend"); ids.Add("galaxy_blend"); ids.Add("large_bottled_water"); }
            else { ids.Add("processed_meat"); ids.Add("processed_meat"); ids.Add("processed_meat"); ids.Add("processed_cheese"); ids.Add("processed_cheese"); ids.Add("processed_cheese"); ids.Add("galaxy_blend"); ids.Add("galaxy_blend"); ids.Add("galaxy_blend"); ids.Add("soda_red"); ids.Add("soda_red"); ids.Add("energy_drink"); ids.Add("energy_drink"); ids.Add("large_bottled_water"); ids.Add("large_bottled_water"); }
            var sysAction = new System.Action(() =>
            {
                foreach (var fid in ids)
                {
                    try { var item = Il2Cpp.DirectoryMaster.Item(fid); if (item != null) Il2Cpp.PlayerStore.Instance.AddDirectSellingItemToTable(item, false, false, false, 0); } catch { }
                }
            });
            var action = Il2CppInterop.Runtime.DelegateSupport.ConvertDelegate<Il2CppSystem.Action>(sysAction);
            try { offer.offerSets.Clear(); } catch { } // 清掉原版3组食物报价，只留我们的档位货单
            offer.offerSets.Add(action);
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] BuildJuanOffer 异常: " + ex.Message); }
        return offer;
    }

    // 李北文货单：梦尘×5 + 奥克莫吸×5
    private static void AddLiBeiwenGoods()
    {
        try
        {
            int added = 0;
            for (int i = 0; i < 5; i++)
            {
                try { if (MerchantHelper.AddItemToCounter("dream_dust", 0, false) != null) added++; } catch { }
            }
            for (int i = 0; i < 5; i++)
            {
                try { if (MerchantHelper.AddItemToCounter("oxycodone_pill", 0, false) != null) added++; } catch { }
            }
            Core.LogMsg("[空间站鲁滨逊] 李北文已上货 " + added + " 件（梦尘×5+奥克莫吸×5）");
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] AddLiBeiwenGoods 异常: " + ex.Message); }
    }

    // ============================================================
    // 博士夜晚商店专属货（09-12 用户拍板，鲁滨逊职业内独立改动，不牵扯博士之友特性）：
    // 原版货不动；不再追加机器/储存（白天博士到访的机器/储存逻辑不动）
    // 1) 加卖食物水：罐头 processed_meat ×2 + 大瓶纯水 large_bottled_water ×1（防堆叠）
    // 2) 每次拜访独立 roll：3% 出受限神经模组 system_capped_neural_core（09-13 用户拍板：未受限已删）
    // ============================================================
    internal static void AddDoctorNightGoods()
    {
        try
        {
            if (!IsActive()) return; // 鲁滨逊职业专属
            int added = 0;

            // 食物水：鲁滨逊职业即有（不牵扯博士之友特性，柜台无同 id 才补，防堆叠）
            if (!HasGoodOnFront("processed_meat"))
            {
                for (int i = 0; i < 2; i++)
                {
                    try { if (MerchantHelper.AddItemToCounter("processed_meat", 0, false) != null) added++; } catch { }
                }
            }
            if (!HasGoodOnFront("large_bottled_water"))
            {
                try
                {
                    GameItem hq = Il2Cpp.WaterPremadeHelper.AccurateHighQualityWater("large_bottled_water"); // 09-20 设计稿：带水大瓶（删 DirectoryMaster.Item+AddWater 链——工厂产物 AddWater 静默失败 → 空瓶）
                    if (hq != null) { if (MerchantHelper.AddItemToCounter(hq, 0, false) != null) added++; }
                }
                catch { }
            }

            // 神经模组概率：落实到博士之友特性（09-12 用户拍板：特性激活才 roll）
            // 标准版：独立 roll 3% 受限；09-13 用户拍板：未受限神经模组全删（不再生成）
            // 硬爽版：50% 受限（09-12 用户拍板；防堆叠保留）
            if (DrJacksonFriendPerk.IsActive())
            {
                float neuralChance = (BuildConfig.HardMode ? BuildConfig.NeuralChanceHard : BuildConfig.NeuralChanceNormal) / 100f;
            if (UnityEngine.Random.value < neuralChance)
                {
                    if (!HasGoodOnFront("system_capped_neural_core"))
                        try { if (MerchantHelper.AddItemToCounter("system_capped_neural_core", 0, false) != null) added++; } catch { }
                }
            }

            if (added > 0) Core.LogMsg("[空间站鲁滨逊] 博士夜晚商店加货 " + added + " 件（食物水/神经模组）");
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] AddDoctorNightGoods 异常: " + ex.Message); }
    }

    // 柜台是否已有同 id 的货（夜晚商店加货防堆叠；与 DrJacksonFriendPerk.CounterHasOnFront 同逻辑）
    private static bool HasGoodOnFront(string itemId)
    {
        try
        {
            EmporiumEntry em = EmporiumEntry.Instance;
            if (em == null || em.frontInvinvElement == null || em.frontInvinvElement.items == null) return false;
            foreach (var it in em.frontInvinvElement.items)
            {
                if (it != null && it.identifier == itemId) return true;
            }
        }
        catch { }
        return false;
    }

    // 柜台同 id 数量（博士夜晚商店保护器补足用）
    private static int CountGoodOnFront(string itemId)
    {
        try
        {
            EmporiumEntry em = EmporiumEntry.Instance;
            if (em == null || em.frontInvinvElement == null || em.frontInvinvElement.items == null) return 0;
            int n = 0;
            foreach (var it in em.frontInvinvElement.items)
            {
                if (it != null && it.identifier == itemId) n++;
            }
            return n;
        }
        catch { }
        return 0;
    }

    // 电话端注册（Core 注册 Postfix StorePhoneClient.InitPhoneClientDict）
    public static void PostfixInitPhoneClientDict(Il2CppSystem.Collections.Generic.Dictionary<long, Il2Cpp.StorePhoneClient> __result)
    {
        try
        {
            if (!IsActive() || __result == null) return;
            if (__result.ContainsKey(BUTCHER_PHONE_NUMBER) && __result.ContainsKey(LI_BEIWEN_PHONE_NUMBER)) return; // 已注册
            if (!__result.ContainsKey(BUTCHER_PHONE_NUMBER))
            {
                var butcher = new Il2Cpp.StorePhoneClient();
                butcher.phoneClientType = Il2Cpp.StorePhoneClient.PhoneClientType.Supplier;
                butcher.phoneState = Il2Cpp.StorePhoneClient.PhoneState.None;
                butcher.displayName = LangHelper.T("胡安", "Juan");
                butcher.locID = "name_juan";
                butcher.dialogFuncId = "";
                butcher.cooldownDuration = CALL_COOLDOWN_DAYS;
                butcher.currentCooldown = 0;
                butcher.dialedBefore = false;
                __result.Add(BUTCHER_PHONE_NUMBER, butcher);
            }
            if (!__result.ContainsKey(LI_BEIWEN_PHONE_NUMBER))
            {
                var libw = new Il2Cpp.StorePhoneClient();
                libw.phoneClientType = Il2Cpp.StorePhoneClient.PhoneClientType.Supplier;
                libw.phoneState = Il2Cpp.StorePhoneClient.PhoneState.None;
                libw.displayName = LangHelper.T("李北文", "Li Beiwen");
                libw.locID = "name_li_bei_wen";
                libw.dialogFuncId = "";
                libw.cooldownDuration = CALL_COOLDOWN_DAYS;
                libw.currentCooldown = 0;
                libw.dialedBefore = false;
                __result.Add(LI_BEIWEN_PHONE_NUMBER, libw);
            }
            Core.LogMsg("[空间站鲁滨逊] 电话端已注册 胡安8800/李北文8801");
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] PostfixInitPhoneClientDict 异常: " + ex.Message); }
    }

    // 兜底补注册：InitPhoneClientDict 若在特性未激活时跑过，每日结算用 PlayerStore.PhoneClientDict 补
    private static void TryRegisterWantedPhones()
    {
        try
        {
            if (!IsActive()) return;
            PlayerStore ps = PlayerStore.Instance;
            if (ps == null || ps.PhoneClientDict == null) return;
            if (!ps.PhoneClientDict.ContainsKey(BUTCHER_PHONE_NUMBER) || !ps.PhoneClientDict.ContainsKey(LI_BEIWEN_PHONE_NUMBER))
            {
                PostfixInitPhoneClientDict(ps.PhoneClientDict);
            }
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] TryRegisterWantedPhones 异常: " + ex.Message); }
    }

    // ===== 09-13 用户拍板：枪械改装全局关闭（恢复"王尔德枪匠未解锁"状态）+ 定制单删除 =====
    // A. Postfix GunHelper.InitGun：拆包 09-13 [L1] moddable=true → SetGameItemType("MODDABLE")，模组系统按 type 判定可装——
    // 全局移除 MODDABLE type → 所有枪不可加零件（不显示可加零件）
    public static void PostfixInitGun(Il2Cpp.GameItem __0)
    {
        try
        {
            if (__0 == null) return;
            var types = __0.GetGameItemType();
            if (types != null && types.Contains("MODDABLE"))
                __0.RemoveGameItemType("MODDABLE");
        }
        catch { }
    }
    // C. Prefix StoreClientListGun 订单生成：拦截定制单（拆包 09-13 [L1]：原生有 null 防护——客户端照常来店但无定制要求）
    public static bool PrefixBlockGunOrder() { return false; }
    // ===== 09-13 修复：举报/击毙 wanted 后电话停用（不能再叫货）=====
    private static readonly System.Collections.Generic.HashSet<long> _wantedPhoneRemoved = new System.Collections.Generic.HashSet<long>();
    private static void MarkWantedPhoneRemoved(string identifier)
    {
        try
        {
            // 09-13：8800=胡安（wanted7，原版常客）；8801=李北文（wanted6）。屠夫 wanted2 已下线电话（原版随机出现，mod 不管）
            long num;
            string label;
            if (identifier == "wanted7") { num = BUTCHER_PHONE_NUMBER; label = "胡安"; }
            else if (identifier == "wanted6") { num = LI_BEIWEN_PHONE_NUMBER; label = "李北文"; }
            else return;
            _wantedPhoneRemoved.Add(num);
            try
            {
                var pc = Il2Cpp.StorePhoneClient.GetPhoneClientByNumber(num);
                if (pc != null) pc.phoneState = Il2Cpp.StorePhoneClient.PhoneState.None; // 电话簿消失 + 拨号空号
            }
            catch { }
            Core.LogMsg("[空间站鲁滨逊] " + label + " 已被举报/击毙，电话停用");
        }
        catch { }
    }
    // 举报（Core 注册 Postfix WantedElement.OnArrested——通缉 UI 举报回调，identifier 字段实锤）
    public static void PostfixWantedElementOnArrested(Il2Cpp.WantedElement __instance)
    {
        try
        {
            if (__instance == null || __instance.identifier == null) return;
            if (__instance.identifier == "wanted6") // 李北文可举报（通缉犯）；胡安 wanted7 保持原版行为
                MarkWantedPhoneRemoved(__instance.identifier);
        }
        catch { }
    }
    // 击毙（Core 注册 Postfix AugHelper.CleanupKill——枪战对话击杀处理；拆包 09-13 [L1]：
    // 客户击杀=枪战对话（SecShootoutDialog），KillCurrentEntity 不在客户链（仅 Debug/Survival 调）。
    // 客户到店 id 由 PrefixStoreUIManagerOnGenericArrived 记录，击杀时用它标记停用电话）
    private static string _lastArrivedClientId = null;
    public static void PrefixStoreUIManagerOnGenericArrived(string id)
    {
        try { _lastArrivedClientId = id; } catch { }
    }
    public static void PostfixAugHelperCleanupKill()
    {
        try
        {
            string id = _lastArrivedClientId;
            if (id == "wanted7" || id == "wanted6")
                MarkWantedPhoneRemoved(id);
        }
        catch { }
    }
    // B. Prefix DirectoryMaster.Item：枪械模组 id 重定向无害物品（拆包 09-13 [L1]：全游戏物品创建统一入口，商店/奖励/全量随机都走它；
    // 模组物品无按 id 点名生成，只可能经全量池随机进入游戏 → 此处拦截全覆盖；重定向而非 null 防崩）
    private static System.Collections.Generic.HashSet<string> _gunModIds = null;
    private static readonly string[] _gunModIdFallback = {
        "rds_view", "rds_view2", "rds_makeshift_view", "silencer_view", "silencer2_view",
        "barrel_view", "compensator_view", "grip_view", "stock_view"
    };
    private static bool IsGunModIdentifier(string id)
    {
        try
        {
            if (string.IsNullOrEmpty(id)) return false;
            if (_gunModIds == null)
            {
                _gunModIds = new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
                foreach (var s in _gunModIdFallback) _gunModIds.Add(s);
                // 运行时枚举 GunModDirectory 注册表补全（失败兜底硬编码）
                try
                {
                    var ids = Il2Cpp.DirectoryMaster.GetIdentifierList<object>("GunModDirectory");
                    if (ids != null) { foreach (var s in ids) { if (!string.IsNullOrEmpty(s)) _gunModIds.Add(s); } }
                }
                catch { }
            }
            return _gunModIds.Contains(id);
        }
        catch { return false; }
    }
    public static bool PrefixDirectoryMasterItem(ref string identifier, bool isOwned)
    {
        try
        {
            if (identifier != null && IsGunModIdentifier(identifier))
                identifier = "scrap_metal"; // 重定向无害废金属（防崩；模组物品不再生成到任何池）
        }
        catch { }
        return true;
    }

    // 电话簿显示名修正（Core 注册 Postfix ContactElement.OnInit）：原生读 locID，覆盖为 displayName（胡安/李北文）
    public static void PostfixOnContactInit(Il2Cpp.ContactElement __instance, long targetNumber)
    {
        try
        {
            if (__instance == null || __instance.titleTMP == null) return;
            var pc = Il2Cpp.StorePhoneClient.GetPhoneClientByNumber(targetNumber);
            if (pc == null || string.IsNullOrEmpty(pc.displayName)) return;
            __instance.titleTMP.text = pc.displayName;
        }
        catch { }
    }

    // 接听拦截（Core 注册 Prefix PhoneUIManager.WillAnswerCall）：未解锁 → 空号提示
    public static bool PrefixWillAnswerCall(long number)
    {
        try
        {
            if (!IsActive()) return true;
            if (number != BUTCHER_PHONE_NUMBER && number != LI_BEIWEN_PHONE_NUMBER) return true; // 非我们号码放行原版
            var pc = Il2Cpp.StorePhoneClient.GetPhoneClientByNumber(number);
            if (pc == null || (int)pc.phoneState < 4)
            {
                try { Il2Cpp.PhoneUIManager.Instance.NoNumber(); } catch { }
                return false; // 未解锁：空号
            }
            return true; // 已解锁：放行原版
        }
        catch { return true; }
    }

    // 拨号即叫货（Core 注册 Prefix PhoneUIManager.StartPhoneDialog）：接通瞬间自动排期 + 冷却，拦掉原版对话
    public static bool PrefixStartPhoneDialog(long currentNumber)
    {
        try
        {
            if (!IsActive()) return true;
            string id = null, name = null;
            if (currentNumber == BUTCHER_PHONE_NUMBER) { id = "wanted7"; name = LangHelper.T("胡安", "Juan"); }
            else if (currentNumber == LI_BEIWEN_PHONE_NUMBER) { id = "wanted6"; name = LangHelper.T("李北文", "Li Beiwen"); }
            else return true;
            if (id == "wanted7") { }
            // 09-13 修复：举报/击毙后电话停用（不能再叫货）
            if (_wantedPhoneRemoved.Contains(currentNumber))
            {
                try { StoreUIManager.Instance.Notify(LangHelper.T(name + "已被举报/击毙，无法再叫货", name + " has been reported/killed, cannot order."), "red"); } catch { }
                try { Il2Cpp.PhoneUIManager.Instance.StopCall(); } catch { }
                return false;
            }
            var pc = Il2Cpp.StorePhoneClient.GetPhoneClientByNumber(currentNumber);
            if (pc != null && (pc.currentCooldown > 0 || (int)pc.phoneState == 5))
            {
                try { StoreUIManager.Instance.Notify(LangHelper.T(name + "还在忙，过几天再打", name + " is busy, call again in a few days."), "red"); } catch { }
                try { Il2Cpp.PhoneUIManager.Instance.StopCall(); } catch { } // 清拨号状态，防卡死（拆包 09-12 [L1]）
                return false; // 冷却中：占线
            }
            PlayerStore ps = PlayerStore.Instance;
            if (ps == null || ps.storeClientManager == null) return true;
            try { ps.storeClientManager.RemoveDuplicateClientsByIdentifier(id); } catch { }
            ps.QueueFuturClient(id, CALL_TO_ARRIVE_DAYS); // 排 2 天到店
            if (pc != null)
            {
                pc.phoneState = Il2Cpp.StorePhoneClient.PhoneState.Cooldown; // 冷却
                pc.currentCooldown = CALL_COOLDOWN_DAYS;
                pc.cooldownDuration = CALL_COOLDOWN_DAYS;
                pc.dialedBefore = true;
            }
            try { StoreUIManager.Instance.Notify(LangHelper.T("已预约" + name + " " + CALL_TO_ARRIVE_DAYS + " 天后到店", name + " will arrive in " + CALL_TO_ARRIVE_DAYS + " days."), "green"); } catch { }
            Core.LogMsg("[空间站鲁滨逊] 电话叫货成功：" + name + " " + CALL_TO_ARRIVE_DAYS + " 天后到店");
            try { Il2Cpp.PhoneUIManager.Instance.StopCall(); } catch { } // 清拨号状态，防卡死（拆包 09-12 [L1]：StartCalling 已挂 0x64=1，原版由 StartDialogue 收尾，被拦需 StopCall 清理）
            return false; // 拦掉原版对话显示
        }
        catch { return true; }
    }

    // 电话冷却自管（PostfixOnNewDay 调用；原版 OnNewDay 是否遍历递减不确定，自己维护最稳）
    private static void TickWantedPhoneCooldown()
    {
        try
        {
            if (!IsActive()) return;
            TickOnePhoneCooldown(BUTCHER_PHONE_NUMBER);
            TickOnePhoneCooldown(LI_BEIWEN_PHONE_NUMBER);
        }
        catch { }
    }
    private static void TickOnePhoneCooldown(long number)
    {
        try
        {
            var pc = Il2Cpp.StorePhoneClient.GetPhoneClientByNumber(number);
            if (pc == null) return;
            if (pc.currentCooldown > 0)
            {
                pc.currentCooldown--;
                if (pc.currentCooldown <= 0 && (int)pc.phoneState == 5)
                    pc.phoneState = Il2Cpp.StorePhoneClient.PhoneState.Regular; // 冷却结束恢复可拨
            }
        }
        catch { }
    }

}
