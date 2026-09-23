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
{            // 开局大瓶纯水容量 1000ml

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
    };  // 社交每日 -（独处，CFG 可调）

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
    internal const int NODE_CRIT = 20;         // 任一项低每日-（CFG 可调）

    // 状态客户 identifier（cheatsheet 2.3.12 实锤 + 工厂打标补充）
    private static readonly HashSet<string> STATUS_CLIENT_IDS = new HashSet<string>
    {
        "thirstyspacer", "hungryspacer", "spacerchef", "spacermedical", "sickchildcaretaker",
        "desperate", "wornout", "sicklowers"
    };
}
