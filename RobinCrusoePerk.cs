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
    internal const int SIP_ML = 200;                // 一口水 = 200ml
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
    internal const int CLEAN_START = 100;      // 清洁度初始 100
    internal const int SLEEP_START = 100;      // 睡眠初始 100
    internal const int SOCIAL_START = 50;      // 社交初始 50
    internal const int DAILY_CLEAN_LOSS = 10;  // 清洁每日 -10%
    internal const int DAILY_SLEEP_GAIN = 30;  // 睡眠打烊 +30%
    internal const int SLEEP_SCAV_LOSS = 7;    // 外出拾荒睡眠 -7%（09-10 用户拍板：15% 太狠会触发禁拾荒自锁，3% 太轻，定为 7%）
    internal const int DAILY_SOCIAL_GAIN = 5;  // 社交每日 +5（开店接待）
    internal const int DAILY_SOCIAL_LOSS = 5;  // 客流削减日社交 -5（独处）

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
    internal const int MOOD_START = 60;        // 心情初始值
    internal const int DAILY_SAT_LOSS = 20;    // 饱食每日 -20%
    internal const int DAILY_THIRST_LOSS = 25; // 口渴每日 -25%
    internal const int DAILY_HEALTH_GAIN = 10; // 健康每日 +10% 自然恢复
    internal const int GRANARY_DAYS = 7;       // 粮仓：饱食≥80 连续 7 天
    internal const int ELEV_EVERY = 2;         // 昂扬：每 2 天结算一次
    internal const int ELEV_MAX = 5;           // 昂扬累计封顶 5 次
    internal const int MOOD_UP = 5;            // 三项全好 每日 +5
    internal const int MOOD_DOWN = 10;         // 任一项<60 每日 -10

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
    internal static void HandleHotkeys()
    {
        try
        {
            if (!UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Z)) return;
            if (!IsActive())
            {
                // 诊断：读档场景 startType 实况（用户确认开的是鲁滨逊存档但 IsActive=false）
                try
                {
                    var ps0 = PlayerStore.Instance;
                    var ng0 = Il2Cpp.NewGameData.Instance;
                }
                catch (Exception ex2) { Core.LogMsg("[空间站鲁滨逊] IsActive诊断异常: " + ex2.Message); }
                return;
            }
            if (PlayerStore.Instance == null) return; // 特性界面/主菜单阶段不响应
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
            if (IsDrink(item) || IsMedicine(item)) return false;
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
            b.SetSize(300, 430).SetPosition(Vector2.zero);
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
        try { return Math.Max(0, WaterHelper.GetCurrentCapacityML(item)); } catch { }
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
        return BOTTLE_ML;
    }

    // ===== 开局（HandleInitialItem Postfix 调用）=====
    internal static void TrySetupNewRun()
    {
        try
        {
            if (!IsActive()) return;
            PlayerStore ps = PlayerStore.Instance;
            if (ps != null) ps.playerCash = 360; // 默认600 × 0.6（资金 -40%）

            GiveToBackpack("processed_meat", 3);      // 口粮×3（三天量）
            GiveToBackpack("raw_meat", 2);            // 大肉×2（用户拍板 09-09：另加生肉）
            GivePureWaterToBackpack(3);               // 大瓶纯水×3（三天量）
            GiveToBackpack("bandage_item", 5);        // 绷带×5（bandage_item 正确 id）

            PerkStatePersistence.SetInt(PERK_ID, "sat", 100);        // v5.7 三状态初始
            PerkStatePersistence.SetInt(PERK_ID, "thirst", 100);
            PerkStatePersistence.SetInt(PERK_ID, "health", 100);
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
                if (!DirectoryMaster.Has<GameItem>("large_bottled_water")) return;
                GameItem item = DirectoryMaster.Item("large_bottled_water", true);
                if (item == null) continue;
                try { item.EnableTag("WAGES_START_WATER", true); } catch { } // 开局纯水标记：喝水双倍恢复口渴（用户拍板）
                GameItem spawn = item;
                try { GameItem cl = item.CloneLinked(); if (cl != null) spawn = cl; } catch { }
                try { WaterHelper.AddWater(spawn, 0, -1, false, 0, 1, true); } catch { }
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
    public static void PostfixDoubleClickAction(GameItem newItem, Vector2 mousePosition)
    {
        try
        {
            if (!IsActive() || newItem == null) return;
            if (Patches.CurrentUITradeMode != 0) return;
            // v5.7 双击位置不限（背包/柜台/存储容器均可吃喝，用户反馈"背包吃不了"修复）；仅交易模式拦截
            // 博士夜晚商店（afterhourInventory）的货没买不能吃/喝/用药（用户反馈"博士晚上的食品没买就能食用"）
            if (IsInDoctorNightInventory(newItem)) {  return; }
            // v5.7 心情主动提升：酒/烟/毒/彩票优先于吃喝（酒也是饮品，先判酒）
            if (IsAlc(newItem)) DrinkAlcohol(newItem);
            else if (IsTobacco(newItem)) BoostMood(10, LangHelper.T("抽烟", "Smoking"));
            else if (IsNarcotic(newItem)) BoostMood(25, LangHelper.T("麻醉品", "Narcotics"));
            else if (IsLottery(newItem)) BoostMood(UnityEngine.Random.Range(10, 21), LangHelper.T("刮彩票", "Scratch Ticket"));
            else if (IsFood(newItem)) EatBite(newItem);
            else if (IsDrink(newItem)) DrinkSip(newItem);
            else if (IsMedicine(newItem)) TreatWithMedicine(newItem);
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] 双击异常: " + ex.Message); }
    }

    // 喝酒（v5.7 心情+15；拆包 09-10 自酿酒价值分档）：酒瓶有剩余才给心情；喝一口减 ml（修"酒剩余0还能无限喝"）
    // 自酿酒（wine_quality_homebrew 条件）按价值心情分档（用户拍板 09-10）：<50 +10 / 50-149 +15 / 150-299 +20 / 300-999 +30；
    // ≥1000 顶级自酿：额外 睡眠+25 + 出门连续3天不受伤 + 拾荒次数+1（PerkStatePersistence 存档，runID 隔离）
    private static void DrinkAlcohol(GameItem item)
    {
        int ml = GetWaterMl(item);
        if (ml <= 0)
        {
            return;
        }
        int sip = Math.Min(SIP_ML, ml); // 一口 200ml（仿喝水）
        bool homebrew = IsHomebrewWine(item);
        int mood = 15;
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
        try { WaterHelper.Remove(item, sip * 1000); } catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] 喝酒Remove异常 " + ex.Message); }
        if (ml <= SIP_ML) TryExpel(item); // ★ 喝光：酒瓶消失（同食物/药品消耗链）
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

    // 喝水（v5.7 百分比制 + ml 自洽）：一口 200ml = 口渴总量 2000ml 的 10%；空瓶保留不消失；脏水→患病（健康-40 在 TryInfect 内）
    private static void DrinkSip(GameItem item)
    {
        int ml = GetWaterMl(item);
        if (ml <= 0) {  return; }
        int sip = Math.Min(SIP_ML, ml);
        bool isStartWater = false;
        try { isStartWater = item.IsTag("WAGES_START_WATER"); } catch { }
        // 原生水质 purity（拆包 09-10 修正 [L1]：GetPurityArrayIndex 档越高越纯（档5=最纯，采样 优质9914/普通9897/脏9023）
        // 原判 0-1 优质/4-5 脏方向反了（纯水被当脏水只回 5%）；修正：4-5 纯 / 0-1 脏；fallback 阈值按采样内插 9900/9100
        int pIdx = -1;
        int purity = -1;
        try { purity = Il2Cpp.WaterHelper.GetWaterPurity(item); } catch { }
        try { pIdx = Il2Cpp.WaterFeatureHelper.GetPurityArrayIndex(purity); } catch { }
        bool pure = isStartWater || (pIdx >= 0 && pIdx <= 1) || (pIdx < 0 && purity >= 9900);   // 日志实锤档位方向：档0=最纯(10000)、档5=最脏(<=8500)
        bool dirty = !isStartWater && ((pIdx >= 4 && pIdx <= 5) || (pIdx < 0 && purity <= 9100)); // 09-11 用户确认：只按原版真实水质判定
        int gain = pure ? 20 : (dirty ? 5 : 10); // 优质×2 / 脏×0.5 / 普通×1
        SetThirstPct(Math.Min(100, GetThirstPct() + gain)); // 200ml = 2000ml 总量 10%（ml 显示自洽）
        SetClean(Math.Min(100, GetClean() + 5));          // 用水=喝+洗：清洁度 +5%（用户拍板"用水恢复"）
        if (dirty) TryInfect(0.3); // 脏水患病概率 30%
        string wname = pure ? LangHelper.T("优质", "Pure") : (dirty ? LangHelper.T("脏", "Dirty") : LangHelper.T("普通", "Plain"));
        try { StoreUIManager.Instance.Notify(LangHelper.T("饮水 +" + gain + "% 口渴（" + wname + "）", "Drinking +" + gain + "% Thirst (" + wname + ")"), "white"); } catch { }
        try { WaterHelper.Remove(item, sip * 1000); } catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] Remove异常 " + ex.Message); }
        int verify = GetWaterMl(item);
        RefreshStatusPanel(); // 实时刷新常驻面板
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

    // ===== 自动喝水按质生效（拆包 09-10 [L1]：AutoSipFromContainer 原版不读品质；夜间自动喝脏水也致病）=====
    public static bool PrefixAutoSipFromContainer(GameItem item, GameCharacterItem GCI)
    {
        try
        {
            if (!IsActive() || item == null) return true;
            int purity = -1; int pIdx = -1;
            try { purity = Il2Cpp.WaterHelper.GetWaterPurity(item); } catch { }
            try { pIdx = Il2Cpp.WaterFeatureHelper.GetPurityArrayIndex(purity); } catch { }
            if ((pIdx >= 4 && pIdx <= 5) || (pIdx < 0 && purity <= 9100))
            {
                TryInfect(0.1); // 自动喝频次高：10% 概率（拆包提示勿每次必病）
                try { StoreUIManager.Instance.Notify(LangHelper.T("夜间喝了脏水，身体不适", "Drank dirty water at night..."), "red"); } catch { }
            }
        }
        catch { }
        return true; // 放原生继续补口渴
    }

    // ===== 物品面板 tooltip =====
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
            }
            else if (item.IsTag("CONTAINER_TAG") && !item.IsTag("VOID_BEAD_TAG") && !item.IsTag("CUSTOM_STORAGE_TAG"))
            {
                int cap = GetTagIntSafe(item, "wageUpgradeCap");
                if (cap > 0)
                    builder.AddLine(LangHelper.T("◆ 扩容：+" + cap + " 列（拖 junk +1 列/次）", "◆ Expand: +" + cap + " columns (drag junk +1 column/each)"),
                        true, (RenderHandler.ColorPalette)(-1), false, false, false, false, (RenderHandler.ColorPalette)(-1), (RenderHandler.ColorPalette)(-1), (RenderHandler.ColorPalette)(-1));
                else
                    builder.AddLine(LangHelper.T("◆ 扩容：拖 junk +1 列/次", "◆ Expand: drag junk +1 column/each"),
                        true, (RenderHandler.ColorPalette)(-1), false, false, false, false, (RenderHandler.ColorPalette)(-1), (RenderHandler.ColorPalette)(-1), (RenderHandler.ColorPalette)(-1));
            }
        }
        catch { }
    }

    // ===== 拾荒/受伤联动（v5.7：只挂心情一个口；病危禁拾荒；受伤一律不能拾荒）=====
    public static bool PrefixReceiveWound()
    {
        try
        {
            if (!IsActive()) return true;
            if (IsHomebrewWineBuffActive()) return false; // 顶级自酿 buff：连续3天不受伤（用户拍板 09-10）
            int pct = GetMoodWoundPct(); // ≥80 -20% / <40 +20%（受伤几率修正）
            float avoid = 0.3f * (1f + pct / 100f); // 免伤基底 30%：≥80→36%（更不易伤）/<40→24%（更容易伤）
            if (UnityEngine.Random.value < avoid) return false;
        }
        catch { }
        return true;
    }
    public static void PostfixCanScavenge(ref bool __result)
    {
        try
        {
            if (!IsActive()) return;
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
            __result = GetScavCap(); // 上限
        }
        catch { }
    }
    public static void PostfixGetScavTimeLeft(ref int __result)
    {
        try
        {
            if (!IsActive()) return;
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
            int cap = 5 + GetMoodScavBonus();
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
    private static void SetTagIntValue(GameItem item, string tag, int value)
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
            if (!IsActive() || __instance == null || targetItem == null) return true;
            // 拖动中 MayTarget 会被反复调用：匹配即放行（hover 可拖），升级/消耗留给松手时的 Target/MayHaveValidInventorySlot
            if ((IsMetalIngot(__instance) && (IsMachine(targetItem) || targetItem.IsTag("MODULE_TAG")))
                || (IsJunk(__instance) && targetItem.IsTag("CONTAINER_TAG") && !targetItem.IsTag("VOID_BEAD_TAG") && !targetItem.IsTag("CUSTOM_STORAGE_TAG")))
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
            if (!IsActive() || __instance == null || targetItem == null) return true;
            if (!IsDragRelease()) return true;
            if (IsMetalIngot(__instance) && (IsMachine(targetItem) || targetItem.IsTag("MODULE_TAG")))
            { if (TryUpgradeMachine(__instance, targetItem)) return false; }
            else if (IsJunk(__instance) && targetItem.IsTag("CONTAINER_TAG") && !targetItem.IsTag("VOID_BEAD_TAG") && !targetItem.IsTag("CUSTOM_STORAGE_TAG"))
            { if (TryUpgradeContainer(__instance, targetItem)) return false; }
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
            if (!__instance.IsTag("CONTAINER_TAG") || __instance.IsTag("VOID_BEAD_TAG") || __instance.IsTag("CUSTOM_STORAGE_TAG")) return true;
            if (!IsDragRelease()) return true;
            if (TryUpgradeContainer(item, __instance)) { __result = false; return false; }
        }
        catch { }
        return true;
    }
    private static bool IsMetalIngot(GameItem item)
    {
        try { return item != null && (item.identifier ?? "").ToLowerInvariant() == "metal_ingot"; } catch { return false; }
    }
    private static bool IsJunk(GameItem item)
    {
        try { return item != null && (item.identifier ?? "").ToLowerInvariant() == "junk"; } catch { return false; }
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
        private static readonly HashSet<string> ALL_MACHINE_IDS = new HashSet<string>(new string[] { "alarm_system", "moisture_farm", "water_purifier", "mirage_projector", "desequencer", "furnace", "wine_rack", "turbo_booster", "bottle_printer", "box_dispenser", "cassette_player", "animal_feeder", "recharger_base", "fridge", "blender", "chem_finisher", "deal_maker", "heating_plate", "hydroponic", "broken_machine" });
    private static bool IsMachine(GameItem item)
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
    // 容器升级：宽+1（保货）；消耗垃圾。容器内部库存 = item.inventory/contentWindow（inventoryShape 属性编译不可见→反射，同 ShrinkInv）
    private static bool TryUpgradeContainer(GameItem junk, GameItem container)
    {
        try
        {
            var grid = GetContainerGrid(container);
            if (grid == null) { Core.LogMsg("[空间站鲁滨逊] 容器升级失败：取不到内部库存 " + GetId(container)); return false; }
            int w = 0, h = 0;
            GetShapeWH(grid, ref w, ref h);
            if (w <= 0 || h <= 0) { Core.LogMsg("[空间站鲁滨逊] 容器升级失败：宽高异常 " + w + "x" + h); return false; }
            AddTagInt(container, "wageUpgradeCap", 1);
            try { if (container.IsTag("CONTAINER_TAG")) container.EnableTag("CONTAINER_TOOLTIP_TAG"); } catch { } // 拆包 2.5.32：容量行显示门控
            // 拆包 09-11 [L1]：数字重载 SetShape(w,h) 缺 ValidateBackground（背景不重建→视觉/容量不刷新，storage_bay 命名形状容器升级无效）
            // 改用字符串重载 SetShape(shape,width)（内部自动 ValidateBackground，虚空珠同路径）——全开放矩形 '0'=可放
            try { grid.SetShape(new string('0', (w + 1) * h), w + 1); } catch { try { grid.SetShape("", w + 1); } catch { } }
            try { grid.Validate(); } catch { }
            ConsumeOne(junk);
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
            try { PlayerPrefs.DeleteKey("RC.Odin.Done." + DeterministicSchedule.GetRunKey()); } catch { } // 奥丁：读档后允许重新调度（用户反馈 09-10 读档后消失）
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
                        if (!item.IsTag("CONTAINER_TAG") || item.IsTag("VOID_BEAD_TAG") || item.IsTag("CUSTOM_STORAGE_TAG")) continue;
                        int cap = GetTagIntSafe(item, "wageUpgradeCap");
                        if (cap <= 0) continue;
                        var grid = GetContainerGrid(item);
                        if (grid == null) continue;
                        int w = 0, h = 0;
                        GetShapeWH(grid, ref w, ref h);
                        if (w <= 0 || h <= 0) continue;
                        // 字符串重载（同 L1778：自动 ValidateBackground，防 storage_bay 升级后不刷新）
                        try { grid.SetShape(new string('0', (w + cap) * h), w + cap); } catch { try { grid.SetShape("", w + cap); } catch { } }
                        try { grid.Validate(); } catch { }
                        _rcRestoredContainers.Add(item.Pointer); // 防 SetContentWindow 重复恢复双加
                        restored++;
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
            if (!__instance.IsTag("CONTAINER_TAG") || __instance.IsTag("VOID_BEAD_TAG") || __instance.IsTag("CUSTOM_STORAGE_TAG")) return;
            int cap = GetTagIntSafe(__instance, "wageUpgradeCap");
            if (cap <= 0) return;
            var grid = GetContainerGrid(__instance);
            if (grid == null) return;
            int w = 0, h = 0;
            GetShapeWH(grid, ref w, ref h);
            if (w <= 0 || h <= 0) return;
            if (!_rcRestoredContainers.Add(__instance.Pointer)) return; // 已恢复过：跳过防双加
            // 字符串重载（同 L1778：自动 ValidateBackground）
            try { grid.SetShape(new string('0', (w + cap) * h), w + cap); } catch { try { grid.SetShape("", w + cap); } catch { } }
            try { grid.Validate(); } catch { }
        }
        catch { }
    }

    private static int GetTagIntSafe(GameItem item, string tag)
    {
        try { var t = item.GetTagReadonly(tag); if (t != null) return t.valueInt; } catch { }
        return 0;
    }
    private static void AddTagInt(GameItem item, string tag, int delta)
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
            if (!IsActive() || builder == null || item == null) return;
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
            if (!__1.IsTag("CONTAINER_TAG") || __1.IsTag("VOID_BEAD_TAG") || __1.IsTag("CUSTOM_STORAGE_TAG")) return;
            ShrinkInv(__0 as GameGridInventory, GetId(__1) + "(容器获得减半)");
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
                    if (item.IsTag("VOID_BEAD_TAG") || item.IsTag("CUSTOM_STORAGE_TAG")) continue;
                    if (!item.IsTag("CONTAINER_TAG") && !IsMachine(item)) continue;
                    var grid = GetContainerGrid(item);
                    if (grid != null) ShrinkInv(grid, GetId(item) + "(储存区/机器箱)");
                }
                catch { }
            }
        }
        catch { }
    }
    // 真减半：读运行时 inventoryShape（GridShape 接口，实际 GridShapeBuilder 实现）的 width/height → SetShape(半宽, 高)
    // 保底：现有物品数 +5 格；宽度下限 4（防极端容器）
    private static void ShrinkInv(GameGridInventory inv, string label)
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
    private static void ExecuteGameOverBy(string ending)
    {
        try
        {
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
            GiveToBackpack("bottled_water", 2);
            try { StoreUIManager.Instance.Notify(LangHelper.T("好心客户送来了 2 份水，先撑住", "A kind customer sent 2 waters — hang in there"), "green"); } catch { }
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] RescueWater 异常: " + ex.Message); }
    }
    private static void RescueMedicine()
    {
        try
        {
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
            if (day < 100) return false; // 前99天免租：跳过原生每周收租
            if (day % 100 != 0) return false; // 每100天收一次
            int term = day / 100; // 期数：第100天=1、第200天=2…
            int rent = 10000 + term * 5000; // 第100天=15000、第200天=20000、第300天=25000…
            var ps = Il2Cpp.PlayerStore.Instance;
            if (ps == null) return false;
            if (ps.playerCash >= rent)
            {
                ps.playerCash -= rent;
                try { StoreUIManager.Instance.Notify(LangHelper.T("交租 " + rent + "（第" + term + "期）", "Rent due: " + rent + " (term " + term + ")"), "green"); } catch { }
            }
            else
            {
                try { StoreUIManager.Instance.Notify(LangHelper.T("房东来收 " + rent + "，现金不足！", "The landlord is here for " + rent + " - not enough cash!"), "red"); } catch { }
            }
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

    // ===== 奥丁（革命商人）：第2天到店，与原生版本一致（拆包 2.3.13）=====
    // 用原生完整版 CreateWanted4（wanted4）：内部 b__4_1 自动发名片 card_rev + 电话簿解锁（Normal 版缺名片链）
    // 独立于 GenerateClient，前3天无随机客户拦截不误伤（拆包第4项实锤）
    private static void ScheduleOdin()
    {
        try
        {
            int day = DeterministicSchedule.CurrentDay;
            if (day < 2) return; // 第2天及以后补调度（读档后奥丁可能丢失，key 防重保证只补一次）
            string key = "RC.Odin.Done." + DeterministicSchedule.GetRunKey();
            if (PlayerPrefs.GetInt(key, 0) != 0) return; // 防读档重复
            PlayerPrefs.SetInt(key, 1);
            var scm = GetStoreClientManager();
            if (scm == null) return;
            var odin = Il2Cpp.StoreClientListWanted.CreateWanted4(); // 原生完整版：自动发名片+解锁电话簿
            if (odin == null) {  return; }
            scm.AddClient(odin);
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] ScheduleOdin 异常: " + ex.Message); }
    }
    // 取消奥丁电话冷却（cooldownDuration 5→0，拆包 [L1] StorePhoneClient@0x30）
    public static void PostfixCreateRevMerchant(Il2Cpp.StorePhoneClient __result)
    {
        try { if (__result != null) __result.cooldownDuration = 0; } catch { }
    }

    // ===== 租金显示同步为100天制（拆包 [L1]：日历/开始日/店内日历都读 dayUntilRent+rentValue）=====
    private static void SyncRentDisplay()
    {
        try
        {
            var ps = Il2Cpp.PlayerStore.Instance;
            if (ps == null) return;
            int day = DeterministicSchedule.CurrentDay;
            // 距下次收租：第100天=今天(0)，第99天=1天后，第101天=99天后…
            int rem = day % 100;
            ps.dayUntilRent = (rem == 0) ? 0 : (100 - rem);
            // 下期租金：day 1-100 → 15000（期1）；101-200 → 20000（期2）…
            int term = (day - 1) / 100 + 1;
            ps.rentValue = 10000 + term * 5000;
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
            int rem = day % 100;
            int due = rem == 0 ? 0 : 100 - rem;
            int term = (day - 1) / 100 + 1;
            int rent = 10000 + term * 5000;
            string dueTxt = due == 0 ? LangHelper.T("今天", "today") : (due == 1 ? LangHelper.T("明天", "tomorrow") : LangHelper.T(due + "天后", "in " + due + " days"));
            if (__instance.landlordTMP != null)
                __instance.landlordTMP.text = LangHelper.T("房租 " + rent + " 将于" + dueTxt + "收取（第" + term + "期）", "Rent " + rent + " due " + dueTxt + " (term " + term + ")");
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
            int rem = day % 100;
            int due = rem == 0 ? 0 : 100 - rem;
            int term = (day - 1) / 100 + 1;
            int rent = 10000 + term * 5000;
            string dueTxt = due == 0 ? LangHelper.T("今天", "today") : (due == 1 ? LangHelper.T("明天", "tomorrow") : LangHelper.T(due + "天后", "in " + due + " days"));
            string txt = LangHelper.T("房租 " + rent + " " + dueTxt + "收取（第" + term + "期）", "Rent " + rent + " due " + dueTxt + " (term " + term + ")");
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
            int rem = day % 100;
            int due = rem == 0 ? 0 : 100 - rem;
            int term = (day - 1) / 100 + 1;
            int rent = 10000 + term * 5000;
            string dueTxt = due == 0 ? LangHelper.T("今天", "today") : (due == 1 ? LangHelper.T("明天", "tomorrow") : LangHelper.T(due + "天后", "in " + due + " days"));
            if (__instance.rentReminderTMP != null)
                __instance.rentReminderTMP.text = LangHelper.T("房租 " + rent + " 将于" + dueTxt + "收取（第" + term + "期）", "Rent " + rent + " due " + dueTxt + " (term " + term + ")");
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
            ScheduleOdin(); // 奥丁：第2天直接到店（拆包 2.3.13 扩展）
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
            SetSatiety(sat); SetThirstPct(th); SetHealth(h);
            // 新三状态结算（v5.8-8）：清洁 -10 + 节点衰减/恢复；睡眠 打烊+30（拾荒当天已 -15）+ 节点睡眠恢复 + 补觉高效；社交 接待日+5/无客日-5 + 节点
            SetClean(Math.Max(0, Math.Min(100, GetClean() - DAILY_CLEAN_LOSS - FxNum("cleanD") + FxNum("cleanR"))));
            SetSleep(Math.Min(100, Math.Max(0, GetSleep() + DAILY_SLEEP_GAIN + FxNum("sleepR") + GetCompBuffSleepRestore()))); // sleepR 符号修正（拆包 09-10：'sleepR-10'=恢复-10，减号负负得正，改加号）
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
                    ps.AddNightLog(LangHelper.T("—— 鲁滨逊的账本 · 第 " + day + " 天 ——", "-- Robinson's Ledger · Day " + day + " --"), "#E8C99B");
                    ps.AddNightLog(nodeTxt + "｜" + moodTxt + granaryTxt, badNode ? "#FF8A8A" : "#FFFFFF");
                    ps.AddNightLog(LangHelper.T("口粮：新鲜 " + fresh + "｜变质 " + stale + "｜腐烂 " + rotten + "（共" + foodCount + "份可吃）", "Rations: fresh " + fresh + " | stale " + stale + " | rotten " + rotten + " (" + foodCount + " edible)"), "#FFFFFF");
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
}
