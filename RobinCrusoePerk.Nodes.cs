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
    internal static string GetStoredNodeKey() => WageSaveStore.GetString(PERK_ID, "nodeKey", "");
    internal static int GetNodeFxIdx() => WageSaveStore.GetInt(PERK_ID, "nodeFxIdx", -1);
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
        WageSaveStore.SetString(PERK_ID, "nodeKey", key);
        if (dom >= 0 && NODES[dom].Pool.Length > 0)
        {
            NodeDef d = NODES[dom];
            if (!d.IsPositive) RollBurst(d);     // v5.9：进负面节点当天触发爆发（惩罚+CompBuff）
            int idx = UnityEngine.Random.Range(0, d.Pool.Length);
            WageSaveStore.SetInt(PERK_ID, "nodeFxIdx", idx);
        }
        else WageSaveStore.SetInt(PERK_ID, "nodeFxIdx", -1);
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
        var activeNodes = AllActiveNodes();
        
        
        
        foreach (int n in activeNodes) // Lock 基础效果：实时
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
            // 09-21 Bug3：删掉前面的遍历循环，只保留总加成（tooltip 太长）
            var sb = new System.Text.StringBuilder();
            int sellB = GetSellBonusPct(), bargainB = GetBargainBonusPct(), budgetB = GetBudgetBonusPct();
            string total = LangHelper.T("总：", "Total: ");
            if (sellB != 0) total += LangHelper.T("售价", "Sell ") + (sellB > 0 ? "+" : "") + sellB + "% ";
            if (bargainB != 0) total += LangHelper.T("议价", "Bargaining ") + (bargainB > 0 ? "+" : "") + bargainB + "% ";
            if (budgetB != 0) total += LangHelper.T("预算", "Budget ") + (budgetB > 0 ? "+" : "") + budgetB + "% ";
            if (total.Length > 2) sb.Append(total.Trim());
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
        if (_sellBonusCache != -999) return _sellBonusCache;
        int granary = (GetGranaryDays() >= GRANARY_DAYS) ? 5 : 0;
        int elev = Math.Min(ELEV_MAX, GetElevCount());
        int fxSell = FxNum("sell");
        int comp = (int)GetCompBuffSellBonus();
        // 09-21 修：心情加成（>=60 → +10, <40 → -10）
        int moodSell = GetMood() >= 60 ? 10 : (GetMood() < 40 ? -10 : 0);
        int bonus = granary + elev + fxSell + comp + moodSell;
        
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
    internal static int GetCompBuffDays(string eff) => WageSaveStore.GetInt(PERK_ID, "cb_" + eff, 0);
    internal static void SetCompBuffDays(string eff, int days) { WageSaveStore.SetInt(PERK_ID, "cb_" + eff, days > 0 ? days : 0); InvalidateTradeCaches(); }
    internal static void TickCompBuffs() { foreach (string k in COMP_BUFF_KEYS) { int d = GetCompBuffDays(k); if (d > 0) SetCompBuffDays(k, d - 1); } }
    internal static double GetEatEffMult() => GetCompBuffDays("eatEff") > 0 ? 1.5 : 1.0;          // 饿狼代谢 吃食物+50%
    internal static double GetThirstEffMult() { if (GetCompBuffDays("thirstEff50") > 0) return 0.5; if (GetCompBuffDays("thirstEff10") > 0) return 0.9; return 1.0; } // 耐旱-50%/省水-10%
    internal static double GetWearEffMult() => GetCompBuffDays("wearEff") > 0 ? 0.5 : 1.0;        // 糙人抗造 健康衰减-50%
    internal static double GetDrugEffMult() => GetCompBuffDays("drugEff") > 0 ? 1.5 : 1.0;        // 回光返照 药效+50%
    internal static double GetMoodDampMult() => GetCompBuffDays("moodDamp") > 0 ? 0.5 : 1.0;       // 摆烂反弹 心情掉速减半
    internal static double GetContraEffMult() => GetCompBuffDays("contraEff") > 0 ? 1.5 : 1.0;
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
}
