using System.Collections.Generic;
using MelonLoader;

namespace WagePerks;

// 10-19 WP 配置菜单：186 项配置元数据表（key/分组/类型/默认/状态/说明）
// 生成自 BuildConfig.InitPrefs CreateEntry（权威源）+ 设计稿_WP侧配置菜单_配置项分类与失效分析（分组/状态分类）
// 前置修订已应用：GranaryDays S4->S1（FxEval.cs:168 售价+5% 活代码）；CleanDailyLoss S4->S3（消费在未注册 PostfixOnNewDay）
// 状态码：S0 生效 / S1 鲁滨逊专属（仍生效）/ S3 挂点已删（失效-已移交WS）/ S4 常量空转（失效）/ S4b 仅文案（失效）/ S5 死配置（失效）/ S6 内部自动
internal sealed class WageConfigItem
{
    internal string Key;
    internal string Group;
    internal string Type; // int/bool/float/string
    internal string Default; // 序列化默认值
    internal string Status; // S0/S1/S3/S4/S4b/S5/S6
    internal string Desc;
    internal bool IsBroken => Status == "S3" || Status == "S4" || Status == "S4b" || Status == "S5"; // 34 条失效
}

internal static class WageConfigMeta
{
    internal static readonly List<WageConfigItem> Items = new List<WageConfigItem>
    {
        new WageConfigItem { Key = "ContainerUpgradeEnabled", Group = "A 通用/开局", Type = "bool", Default = "true", Status = "S0", Desc = "容器/机器升级（关=不升级）" },
        new WageConfigItem { Key = "HardMode", Group = "A 通用/开局", Type = "bool", Default = "false", Status = "S0", Desc = "硬爽模式：稀有率上限50% / 拾荒+10 / 神经模组进均匀池 / 博士夜卖受…" },
        new WageConfigItem { Key = "PerkGrowthApplied", Group = "A 通用/开局", Type = "int", Default = "0", Status = "S6", Desc = "特性成长：已应用到第几个50天（自动）" },
        new WageConfigItem { Key = "PerkGrowthLastDay", Group = "A 通用/开局", Type = "int", Default = "0", Status = "S5", Desc = "特性成长：上次记录天数（自动）" },
        new WageConfigItem { Key = "PerkGrowthTotalDays", Group = "A 通用/开局", Type = "int", Default = "0", Status = "S6", Desc = "特性成长：全局累计天数（自动）" },
        new WageConfigItem { Key = "StartMaxBottlePrinter", Group = "A 通用/开局", Type = "bool", Default = "false", Status = "S0", Desc = "开局自带满级水瓶打印机(水商之友/鲁滨逊,6000ml+纯水)" },
        new WageConfigItem { Key = "BaseChance", Group = "B 拾荒与稀有率", Type = "int", Default = "0", Status = "S0", Desc = "稀有率初始(%)" },
        new WageConfigItem { Key = "LevelupEvery", Group = "B 拾荒与稀有率", Type = "int", Default = "15", Status = "S0", Desc = "每N次拾荒升1级" },
        new WageConfigItem { Key = "LuckMaxChance", Group = "B 拾荒与稀有率", Type = "int", Default = "25", Status = "S0", Desc = "稀有物发现几率上限(%)——标准版" },
        new WageConfigItem { Key = "LuckMaxChanceHard", Group = "B 拾荒与稀有率", Type = "int", Default = "50", Status = "S0", Desc = "稀有物发现几率上限(%)——硬爽版" },
        new WageConfigItem { Key = "NeuralChanceHard", Group = "B 拾荒与稀有率", Type = "int", Default = "50", Status = "S1", Desc = "硬爽博士夜受限模组概率(%)" },
        new WageConfigItem { Key = "NeuralChanceNormal", Group = "B 拾荒与稀有率", Type = "int", Default = "3", Status = "S1", Desc = "普通博士夜受限模组概率(%)" },
        new WageConfigItem { Key = "NeuralRollChance", Group = "B 拾荒与稀有率", Type = "int", Default = "2", Status = "S0", Desc = "普通拾荒独立roll受限模组(%)" },
        new WageConfigItem { Key = "UpgradeChance", Group = "B 拾荒与稀有率", Type = "int", Default = "2", Status = "S0", Desc = "每级稀有率+(%)" },
        new WageConfigItem { Key = "BoxHeights", Group = "C 容器与蛙哥箱", Type = "string", Default = "3,10,10,10,10,10", Status = "S0", Desc = "蛙哥箱每段高度(逗号分隔)" },
        new WageConfigItem { Key = "BoxWidths", Group = "C 容器与蛙哥箱", Type = "string", Default = "3,10,20,32,42,52", Status = "S0", Desc = "蛙哥箱每段宽度(逗号分隔)" },
        new WageConfigItem { Key = "ContainerHalfEnabled", Group = "C 容器与蛙哥箱", Type = "bool", Default = "true", Status = "S5", Desc = "容器/机器开局减半（关=不减半）" },
        new WageConfigItem { Key = "ContainerMaxStage", Group = "C 容器与蛙哥箱", Type = "int", Default = "5", Status = "S0", Desc = "蛙哥箱段位上限" },
        new WageConfigItem { Key = "UpgradeCosts", Group = "C 容器与蛙哥箱", Type = "string", Default = "1,10,20,40,50", Status = "S0", Desc = "蛙哥箱每级升级材料数(逗号分隔)" },
        new WageConfigItem { Key = "CleanDailyLoss", Group = "D 生存六维（鲁滨逊）", Type = "int", Default = "2", Status = "S3", Desc = "清洁每日衰减量" },
        new WageConfigItem { Key = "CleanPaperTowel", Group = "D 生存六维（鲁滨逊）", Type = "int", Default = "45", Status = "S4b", Desc = "纸巾恢复清洁" },
        new WageConfigItem { Key = "CleanScavCost", Group = "D 生存六维（鲁滨逊）", Type = "int", Default = "2", Status = "S3", Desc = "拾荒清洁消耗" },
        new WageConfigItem { Key = "CleanShampoo", Group = "D 生存六维（鲁滨逊）", Type = "int", Default = "45", Status = "S4b", Desc = "洗涤剂恢复清洁" },
        new WageConfigItem { Key = "CleanStart", Group = "D 生存六维（鲁滨逊）", Type = "int", Default = "100", Status = "S1", Desc = "清洁初始值" },
        new WageConfigItem { Key = "CleanToiletPaper", Group = "D 生存六维（鲁滨逊）", Type = "int", Default = "30", Status = "S4b", Desc = "厕纸恢复清洁" },
        new WageConfigItem { Key = "CleanToothpaste", Group = "D 生存六维（鲁滨逊）", Type = "int", Default = "15", Status = "S4b", Desc = "牙膏恢复清洁" },
        new WageConfigItem { Key = "DailyHealthGain", Group = "D 生存六维（鲁滨逊）", Type = "int", Default = "10", Status = "S3", Desc = "健康每日恢复" },
        new WageConfigItem { Key = "DailySatLoss", Group = "D 生存六维（鲁滨逊）", Type = "int", Default = "20", Status = "S3", Desc = "饱食每日衰减" },
        new WageConfigItem { Key = "DailySleepGain", Group = "D 生存六维（鲁滨逊）", Type = "int", Default = "30", Status = "S3", Desc = "打烊睡眠恢复" },
        new WageConfigItem { Key = "DailySocialGain", Group = "D 生存六维（鲁滨逊）", Type = "int", Default = "5", Status = "S4", Desc = "社交每日+（接待）" },
        new WageConfigItem { Key = "DailySocialLoss", Group = "D 生存六维（鲁滨逊）", Type = "int", Default = "5", Status = "S3", Desc = "社交每日-（独处）" },
        new WageConfigItem { Key = "DailyThirstLoss", Group = "D 生存六维（鲁滨逊）", Type = "int", Default = "25", Status = "S3", Desc = "口渴每日衰减" },
        new WageConfigItem { Key = "ElevEvery", Group = "D 生存六维（鲁滨逊）", Type = "int", Default = "2", Status = "S3", Desc = "昂扬结算间隔(天)" },
        new WageConfigItem { Key = "ElevMax", Group = "D 生存六维（鲁滨逊）", Type = "int", Default = "5", Status = "S1", Desc = "昂扬累计封顶" },
        new WageConfigItem { Key = "GranaryDays", Group = "D 生存六维（鲁滨逊）", Type = "int", Default = "7", Status = "S1", Desc = "粮仓连续天数门槛" },
        new WageConfigItem { Key = "MoodDown", Group = "D 生存六维（鲁滨逊）", Type = "int", Default = "10", Status = "S3", Desc = "任一项低每日心情-" },
        new WageConfigItem { Key = "MoodStart", Group = "D 生存六维（鲁滨逊）", Type = "int", Default = "60", Status = "S1", Desc = "心情初始值" },
        new WageConfigItem { Key = "MoodUp", Group = "D 生存六维（鲁滨逊）", Type = "int", Default = "5", Status = "S3", Desc = "三项全好每日心情+" },
        new WageConfigItem { Key = "SipMl", Group = "D 生存六维（鲁滨逊）", Type = "int", Default = "200", Status = "S3", Desc = "一口喝水量(ml)" },
        new WageConfigItem { Key = "SleepScavLoss", Group = "D 生存六维（鲁滨逊）", Type = "int", Default = "7", Status = "S3", Desc = "拾荒睡眠消耗" },
        new WageConfigItem { Key = "SleepStart", Group = "D 生存六维（鲁滨逊）", Type = "int", Default = "100", Status = "S1", Desc = "睡眠初始值" },
        new WageConfigItem { Key = "SocialStart", Group = "D 生存六维（鲁滨逊）", Type = "int", Default = "50", Status = "S1", Desc = "社交初始值" },
        new WageConfigItem { Key = "ButcherVisitDay", Group = "E 鲁滨逊电话与NPC", Type = "int", Default = "13", Status = "S1", Desc = "胡安首次到店天(0-based)" },
        new WageConfigItem { Key = "CallArriveDays", Group = "E 鲁滨逊电话与NPC", Type = "int", Default = "2", Status = "S1", Desc = "电话叫货到店天数" },
        new WageConfigItem { Key = "CallCooldownDays", Group = "E 鲁滨逊电话与NPC", Type = "int", Default = "3", Status = "S1", Desc = "电话冷却天数" },
        new WageConfigItem { Key = "CrusoeEffHalfEnabled", Group = "E 鲁滨逊电话与NPC", Type = "bool", Default = "true", Status = "S1", Desc = "鲁滨逊机器效率减半开关（关=效率不减半）" },
        new WageConfigItem { Key = "CrusoeEffHalfRatio", Group = "E 鲁滨逊电话与NPC", Type = "float", Default = "0.5", Status = "S1", Desc = "鲁滨逊机器效率减半比例（0.5=减半；1=不减；0.8=八成）" },
        new WageConfigItem { Key = "DoctorSupplyCountMax", Group = "E 鲁滨逊电话与NPC", Type = "int", Default = "5", Status = "S1", Desc = "博士廉价模组数量上限" },
        new WageConfigItem { Key = "DoctorSupplyCountMin", Group = "E 鲁滨逊电话与NPC", Type = "int", Default = "3", Status = "S1", Desc = "博士廉价模组数量下限" },
        new WageConfigItem { Key = "DoctorSupplyPricePct", Group = "E 鲁滨逊电话与NPC", Type = "int", Default = "60", Status = "S1", Desc = "博士廉价模组售价(%)" },
        new WageConfigItem { Key = "LiBeiwenVisitDay", Group = "E 鲁滨逊电话与NPC", Type = "int", Default = "20", Status = "S1", Desc = "李北文首次到店天(0-based)" },
        new WageConfigItem { Key = "AlcoholVisitInterval", Group = "F NPC 来访", Type = "int", Default = "7", Status = "S0", Desc = "酒商来访间隔(天)" },
        new WageConfigItem { Key = "DoctorBuybackPct", Group = "F NPC 来访", Type = "int", Default = "80", Status = "S0", Desc = "博士回收模组价(%)" },
        new WageConfigItem { Key = "DoctorExtraItems", Group = "F NPC 来访", Type = "int", Default = "8", Status = "S0", Desc = "博士夜店加货数量" },
        new WageConfigItem { Key = "DoctorVisitInterval", Group = "F NPC 来访", Type = "int", Default = "7", Status = "S0", Desc = "博士来访间隔(天)" },
        new WageConfigItem { Key = "WageBrotherVisitInterval", Group = "F NPC 来访", Type = "int", Default = "7", Status = "S0", Desc = "蛙哥到访间隔(天)" },
        new WageConfigItem { Key = "WaterVisitInterval", Group = "F NPC 来访", Type = "int", Default = "7", Status = "S0", Desc = "水商来访间隔(天)" },
        new WageConfigItem { Key = "AlcoholMood", Group = "G 饮食与洗浴", Type = "int", Default = "15", Status = "S5", Desc = "酒类心情恢复" },
        new WageConfigItem { Key = "BeverageSatiety", Group = "G 饮食与洗浴", Type = "int", Default = "10", Status = "S5", Desc = "饮品饱食恢复" },
        new WageConfigItem { Key = "BeverageThirst", Group = "G 饮食与洗浴", Type = "int", Default = "15", Status = "S5", Desc = "饮品口渴恢复" },
        new WageConfigItem { Key = "NarcoticMood", Group = "G 饮食与洗浴", Type = "int", Default = "20", Status = "S5", Desc = "麻醉品心情恢复" },
        new WageConfigItem { Key = "SnackMood", Group = "G 饮食与洗浴", Type = "int", Default = "10", Status = "S5", Desc = "零食心情恢复" },
        new WageConfigItem { Key = "WageBathMoodBonus", Group = "G 饮食与洗浴", Type = "int", Default = "10", Status = "S0", Desc = "口渴满优质水洗澡额外心情" },
        new WageConfigItem { Key = "WageBathSleepBonus", Group = "G 饮食与洗浴", Type = "int", Default = "10", Status = "S0", Desc = "口渴满优质水洗澡额外睡眠" },
        new WageConfigItem { Key = "WageGirlAffDecayLowMax", Group = "H 蛙娘·六维与好感", Type = "int", Default = "5", Status = "S0", Desc = "蛙娘好感额外衰减上限(六维低)" },
        new WageConfigItem { Key = "WageGirlAffDecayLowMin", Group = "H 蛙娘·六维与好感", Type = "int", Default = "2", Status = "S0", Desc = "蛙娘好感额外衰减下限(六维低)" },
        new WageConfigItem { Key = "WageGirlAffDecayMax", Group = "H 蛙娘·六维与好感", Type = "int", Default = "2", Status = "S0", Desc = "蛙娘好感每日衰减上限(没互动)" },
        new WageConfigItem { Key = "WageGirlAffDecayMin", Group = "H 蛙娘·六维与好感", Type = "int", Default = "1", Status = "S0", Desc = "蛙娘好感每日衰减下限(没互动)" },
        new WageConfigItem { Key = "WageGirlAffMax", Group = "H 蛙娘·六维与好感", Type = "int", Default = "200", Status = "S0", Desc = "蛙娘好感上限（5节点信赖=200；clamp[-200,200]）" },
        new WageConfigItem { Key = "WageGirlDailyDecay", Group = "H 蛙娘·六维与好感", Type = "int", Default = "2", Status = "S0", Desc = "蛙娘每日六维衰减(未照顾)" },
        new WageConfigItem { Key = "WageGirlMemorialDays", Group = "H 蛙娘·六维与好感", Type = "int", Default = "30", Status = "S0", Desc = "蛙娘纪念日·连续照顾天数门槛" },
        new WageConfigItem { Key = "WageGirlSleepRecover", Group = "H 蛙娘·六维与好感", Type = "int", Default = "15", Status = "S0", Desc = "蛙娘打烊睡眠恢复" },
        new WageConfigItem { Key = "WageGirlStatInit", Group = "H 蛙娘·六维与好感", Type = "int", Default = "60", Status = "S0", Desc = "蛙娘六维初始值" },
        new WageConfigItem { Key = "WageGirlStatMax", Group = "H 蛙娘·六维与好感", Type = "int", Default = "100", Status = "S0", Desc = "蛙娘六维上限" },
        new WageConfigItem { Key = "WageGirlBudgetAffLow", Group = "I 蛙娘·预算", Type = "int", Default = "30", Status = "S0", Desc = "蛙娘预算倍率·低档好感阈值" },
        new WageConfigItem { Key = "WageGirlBudgetAffMid", Group = "I 蛙娘·预算", Type = "int", Default = "60", Status = "S0", Desc = "蛙娘预算倍率·中档好感阈值" },
        new WageConfigItem { Key = "WageGirlBudgetMultHigh", Group = "I 蛙娘·预算", Type = "float", Default = "4", Status = "S0", Desc = "蛙娘预算倍率·高好感(≥60)" },
        new WageConfigItem { Key = "WageGirlBudgetMultLow", Group = "I 蛙娘·预算", Type = "float", Default = "1.5", Status = "S5", Desc = "蛙娘预算倍率·低好感(<30)" },
        new WageConfigItem { Key = "WageGirlBudgetMultMid", Group = "I 蛙娘·预算", Type = "float", Default = "2.5", Status = "S0", Desc = "蛙娘预算倍率·中好感(30-60)" },
        new WageConfigItem { Key = "WageGirlSnatchSleepDebt", Group = "J 蛙娘·偷窃", Type = "int", Default = "10", Status = "S0", Desc = "蛙娘偷拿熬夜睡眠债" },
        new WageConfigItem { Key = "WageGirlSnatchValueHigh", Group = "J 蛙娘·偷窃", Type = "int", Default = "200", Status = "S0", Desc = "蛙娘偷拿价值上限·高好感(≥70)" },
        new WageConfigItem { Key = "WageGirlSnatchValueLow", Group = "J 蛙娘·偷窃", Type = "int", Default = "50", Status = "S0", Desc = "蛙娘偷拿价值上限·低好感(<30)" },
        new WageConfigItem { Key = "WageGirlSnatchValueMid", Group = "J 蛙娘·偷窃", Type = "int", Default = "100", Status = "S0", Desc = "蛙娘偷拿价值上限·中好感(30-70)" },
        new WageConfigItem { Key = "WageGirlStealAffReduction", Group = "J 蛙娘·偷窃", Type = "int", Default = "90", Status = "S0", Desc = "蛙娘偷钱好感减免(好感100→减90)" },
        new WageConfigItem { Key = "WageGirlStealBaseMax", Group = "J 蛙娘·偷窃", Type = "int", Default = "100", Status = "S0", Desc = "蛙娘偷钱基数(好感0时)" },
        new WageConfigItem { Key = "WageGirlStealFirstAmount", Group = "J 蛙娘·偷窃", Type = "int", Default = "50", Status = "S0", Desc = "蛙娘初次偷钱额" },
        new WageConfigItem { Key = "WageGirlStealInterval", Group = "J 蛙娘·偷窃", Type = "int", Default = "5", Status = "S0", Desc = "蛙娘偷钱周期(天)" },
        new WageConfigItem { Key = "WageGirlStealNoStealAff", Group = "J 蛙娘·偷窃", Type = "int", Default = "100", Status = "S0", Desc = "蛙娘好感≥此值不再偷钱/偷拿（信赖节点）" },
        new WageConfigItem { Key = "WageGirlStealSleepDebt", Group = "J 蛙娘·偷窃", Type = "int", Default = "20", Status = "S0", Desc = "蛙娘偷钱熬夜睡眠债" },
        new WageConfigItem { Key = "ConsignStaleDays", Group = "K 蛙娘·寄卖", Type = "int", Default = "3", Status = "S0", Desc = "寄卖流拍周期(天)：挂满 N 个结算周期未成交按 8 折强制成交" },
        new WageConfigItem { Key = "WageGirlConsignFakeComplaintPct", Group = "K 蛙娘·寄卖", Type = "int", Default = "10", Status = "S0", Desc = "寄卖假货投诉概率(%)" },
        new WageConfigItem { Key = "WageGirlConsignStolenReturnPct", Group = "K 蛙娘·寄卖", Type = "int", Default = "5", Status = "S0", Desc = "寄卖赃物退货概率(%)" },
        new WageConfigItem { Key = "WageGirlSellAutoSellOn", Group = "K 蛙娘·寄卖", Type = "bool", Default = "true", Status = "S0", Desc = "蛙娘寄卖总开关（关=寄卖按钮灰、拖入拒收）" },
        new WageConfigItem { Key = "WageGirlSellDailyQuota", Group = "K 蛙娘·寄卖", Type = "int", Default = "10", Status = "S0", Desc = "蛙娘每日寄卖件数上限（10-18：手动寄卖连拖，默认10）" },
        new WageConfigItem { Key = "WageGirlSellMode", Group = "K 蛙娘·寄卖", Type = "int", Default = "0", Status = "S0", Desc = "蛙娘代售模式（0=黑名单·默认 1=白名单）" },
        new WageConfigItem { Key = "WageGirlSellPickOrder", Group = "K 蛙娘·寄卖", Type = "int", Default = "0", Status = "S5", Desc = "已废弃：寄卖改为手动挑选，本项不再生效" },
        new WageConfigItem { Key = "WageGirlSellRulesText", Group = "K 蛙娘·寄卖", Type = "string", Default = "", Status = "S0", Desc = "蛙娘代售规则文本（高级：分号分隔；P:=免售 A:=可售，如 P:tag:MOD…" },
        new WageConfigItem { Key = "WageGirlConnAffCostMonthlyCap", Group = "L 蛙娘·门路", Type = "int", Default = "5", Status = "S0", Desc = "门路每月好感扣点上限" },
        new WageConfigItem { Key = "WageGirlConnCrimeCapLv3", Group = "L 蛙娘·门路", Type = "int", Default = "30000", Status = "S0", Desc = "门路Lv3直售豁免犯罪值硬顶" },
        new WageConfigItem { Key = "WageGirlConnEnabled", Group = "L 蛙娘·门路", Type = "bool", Default = "true", Status = "S0", Desc = "蛙娘门路总开关（10-18：假货原价/违禁品免罪，关=降级纯寄卖）" },
        new WageConfigItem { Key = "WageGirlConnGainCapLv1", Group = "L 蛙娘·门路", Type = "int", Default = "8000", Status = "S0", Desc = "门路Lv1每局累计增益硬顶" },
        new WageConfigItem { Key = "WageGirlConnGainCapLv2", Group = "L 蛙娘·门路", Type = "int", Default = "30000", Status = "S0", Desc = "门路Lv2每局累计增益硬顶" },
        new WageConfigItem { Key = "WageGirlConnGainCapLv3", Group = "L 蛙娘·门路", Type = "int", Default = "80000", Status = "S0", Desc = "门路Lv3每局累计增益硬顶" },
        new WageConfigItem { Key = "WageGirlConnLv1Daily", Group = "L 蛙娘·门路", Type = "int", Default = "1", Status = "S0", Desc = "门路Lv1每日额度(件)" },
        new WageConfigItem { Key = "WageGirlConnLv1FeePct", Group = "L 蛙娘·门路", Type = "int", Default = "20", Status = "S0", Desc = "门路Lv1抽成(%)" },
        new WageConfigItem { Key = "WageGirlConnLv1LeakPct", Group = "L 蛙娘·门路", Type = "int", Default = "6", Status = "S0", Desc = "门路Lv1走漏风声概率(%)" },
        new WageConfigItem { Key = "WageGirlConnLv1RunTotal", Group = "L 蛙娘·门路", Type = "int", Default = "20", Status = "S0", Desc = "门路Lv1每局总量(件)" },
        new WageConfigItem { Key = "WageGirlConnLv1ValueCap", Group = "L 蛙娘·门路", Type = "int", Default = "600", Status = "S0", Desc = "门路Lv1单件原价上限" },
        new WageConfigItem { Key = "WageGirlConnLv2Daily", Group = "L 蛙娘·门路", Type = "int", Default = "2", Status = "S0", Desc = "门路Lv2每日额度(件)" },
        new WageConfigItem { Key = "WageGirlConnLv2FeePct", Group = "L 蛙娘·门路", Type = "int", Default = "15", Status = "S0", Desc = "门路Lv2抽成(%)" },
        new WageConfigItem { Key = "WageGirlConnLv2LeakPct", Group = "L 蛙娘·门路", Type = "int", Default = "3", Status = "S0", Desc = "门路Lv2走漏风声概率(%)" },
        new WageConfigItem { Key = "WageGirlConnLv2RunTotal", Group = "L 蛙娘·门路", Type = "int", Default = "50", Status = "S0", Desc = "门路Lv2每局总量(件)" },
        new WageConfigItem { Key = "WageGirlConnLv2ValueCap", Group = "L 蛙娘·门路", Type = "int", Default = "1200", Status = "S0", Desc = "门路Lv2单件原价上限" },
        new WageConfigItem { Key = "WageGirlConnLv3Daily", Group = "L 蛙娘·门路", Type = "int", Default = "3", Status = "S0", Desc = "门路Lv3每日额度(件)" },
        new WageConfigItem { Key = "WageGirlConnLv3FeePct", Group = "L 蛙娘·门路", Type = "int", Default = "10", Status = "S0", Desc = "门路Lv3抽成(%)" },
        new WageConfigItem { Key = "WageGirlConnLv3LeakPct", Group = "L 蛙娘·门路", Type = "int", Default = "0", Status = "S0", Desc = "门路Lv3走漏风声概率(%)" },
        new WageConfigItem { Key = "WageGirlConnLv3RunTotal", Group = "L 蛙娘·门路", Type = "int", Default = "100", Status = "S0", Desc = "门路Lv3每局总量(件)" },
        new WageConfigItem { Key = "WageGirlConnLv3ValueCap", Group = "L 蛙娘·门路", Type = "int", Default = "2500", Status = "S0", Desc = "门路Lv3单件原价上限" },
        new WageConfigItem { Key = "WageGirlFenceAffBonusPct", Group = "M 蛙娘·销赃", Type = "int", Default = "20", Status = "S0", Desc = "蛙娘销赃好感加成上限(%)" },
        new WageConfigItem { Key = "WageGirlFenceBoxCount", Group = "M 蛙娘·销赃", Type = "string", Default = "1,3", Status = "S5", Desc = "销赃物资箱数量范围(min,max)（D+ 已改固定 nBox=round(ta…" },
        new WageConfigItem { Key = "WageGirlFenceBoxValue", Group = "M 蛙娘·销赃", Type = "int", Default = "300", Status = "S0", Desc = "销赃锁箱外在价值标价/箱（D+：nBox=round(target/该值) 最小…" },
        new WageConfigItem { Key = "WageGirlFenceCardAffThreshold", Group = "M 蛙娘·销赃", Type = "int", Default = "50", Status = "S0", Desc = "蛙娘销赃带钥匙卡的好感门槛（低于不送卡）" },
        new WageConfigItem { Key = "WageGirlFenceCardCount", Group = "M 蛙娘·销赃", Type = "string", Default = "1,3", Status = "S0", Desc = "销赃指挥卡张数范围(min,max)" },
        new WageConfigItem { Key = "WageGirlFenceDays", Group = "M 蛙娘·销赃", Type = "int", Default = "2", Status = "S0", Desc = "蛙娘销赃外出天数" },
        new WageConfigItem { Key = "WageGirlFenceInjectorCount", Group = "M 蛙娘·销赃", Type = "string", Default = "1,2", Status = "S0", Desc = "销赃免疫宁支数范围(min,max)" },
        new WageConfigItem { Key = "WageGirlFenceItemCount", Group = "M 蛙娘·销赃", Type = "string", Default = "3,8", Status = "S0", Desc = "销赃差额/拆件数量范围(min,max)" },
        new WageConfigItem { Key = "WageGirlFenceMarginBasePct", Group = "M 蛙娘·销赃", Type = "int", Default = "10", Status = "S0", Desc = "蛙娘销赃基准加成(%)" },
        new WageConfigItem { Key = "WageGirlFenceMarginHigh", Group = "M 蛙娘·销赃", Type = "int", Default = "30", Status = "S0", Desc = "蛙娘销赃随机浮动上限(%)" },
        new WageConfigItem { Key = "WageGirlFenceMarginLow", Group = "M 蛙娘·销赃", Type = "int", Default = "-10", Status = "S0", Desc = "蛙娘销赃随机浮动下限(%)" },
        new WageConfigItem { Key = "WageGirlFenceSleepDebt", Group = "M 蛙娘·销赃", Type = "int", Default = "20", Status = "S0", Desc = "蛙娘销赃熬夜睡眠债" },
        new WageConfigItem { Key = "WageGirlAllowanceSteps", Group = "N 蛙娘·其它", Type = "string", Default = "100,300,500", Status = "S0", Desc = "蛙娘零花钱档位(逗号分隔)" },
        new WageConfigItem { Key = "WageGirlAutoMove", Group = "N 蛙娘·其它", Type = "bool", Default = "false", Status = "S0", Desc = "蛙娘自动走动（关=蛙娘原地待机）✅10-02绕过TryInventorySlot…" },
        new WageConfigItem { Key = "WageGirlEventChancePct", Group = "N 蛙娘·其它", Type = "int", Default = "10", Status = "S0", Desc = "蛙娘事件·非保底日概率%" },
        new WageConfigItem { Key = "WageGirlEventGuaranteeDays", Group = "N 蛙娘·其它", Type = "int", Default = "5", Status = "S0", Desc = "蛙娘事件·保底间隔天数(好感≥40且在场)" },
        new WageConfigItem { Key = "WageGirlGiftAff", Group = "N 蛙娘·其它", Type = "int", Default = "50", Status = "S0", Desc = "蛙娘好物·好感门槛" },
        new WageConfigItem { Key = "WageGirlGiftInterval", Group = "N 蛙娘·其它", Type = "int", Default = "7", Status = "S0", Desc = "蛙娘好物·间隔天数" },
        new WageConfigItem { Key = "WageGirlRunawayDays", Group = "N 蛙娘·其它", Type = "int", Default = "14", Status = "S0", Desc = "蛙娘跑路·离家天数" },
        new WageConfigItem { Key = "WageGirlRunawayLowStat", Group = "N 蛙娘·其它", Type = "int", Default = "20", Status = "S0", Desc = "蛙娘跑路·六维低于此值计1天" },
        new WageConfigItem { Key = "WageGirlRunawayStreak", Group = "N 蛙娘·其它", Type = "int", Default = "5", Status = "S0", Desc = "蛙娘跑路·连续天数门槛" },
        new WageConfigItem { Key = "WageGirlSavingsUnlockChannel", Group = "N 蛙娘·其它", Type = "int", Default = "5000", Status = "S0", Desc = "蛙娘小金库·渠道解锁门槛(余额≥此值一次性解锁)" },
        new WageConfigItem { Key = "WageGirlSavingsUnlockOutfit", Group = "N 蛙娘·其它", Type = "int", Default = "3000", Status = "S0", Desc = "蛙娘小金库·换装解锁门槛(余额≥此值一次性解锁)" },
        new WageConfigItem { Key = "WageGirlWashCostPerLevel", Group = "N 蛙娘·其它", Type = "int", Default = "50", Status = "S0", Desc = "蛙娘洗白·每级违禁品消耗" },
        new WageConfigItem { Key = "WageGirlWashMasterMult", Group = "N 蛙娘·其它", Type = "float", Default = "0.5", Status = "S0", Desc = "蛙娘洗白·Lv3洗白大师系数(0.5=减半)" },
        new WageConfigItem { Key = "WageBrotherCustomBuyIds", Group = "O 蛙哥", Type = "string", Default = "", Status = "S0", Desc = "蛙哥自定义收购物品id（逗号分隔，蛙哥牛逼Lv2+生效）" },
        new WageConfigItem { Key = "WageBrotherCustomBuyTags", Group = "O 蛙哥", Type = "string", Default = "", Status = "S0", Desc = "蛙哥自定义收购标签（逗号分隔，白名单校验，蛙哥牛逼Lv2+生效）" },
        new WageConfigItem { Key = "WageBrotherOrderCd", Group = "O 蛙哥", Type = "int", Default = "3", Status = "S0", Desc = "点货签冷却(天)" },
        new WageConfigItem { Key = "WageBrotherOrderPrice", Group = "O 蛙哥", Type = "int", Default = "800", Status = "S0", Desc = "点货签售价" },
        new WageConfigItem { Key = "WageBrotherOrderTargetTags", Group = "O 蛙哥", Type = "string", Default = "", Status = "S0", Desc = "点货签品类标签（逗号分隔，蛙哥到店携带对应点货签）" },
        new WageConfigItem { Key = "ThiefMagnetBlackMarketChance", Group = "P 特性·招贼体质", Type = "float", Default = "1.0", Status = "S0", Desc = "招贼体质·黑市客户每日上门概率(默认1.0=必来)" },
        new WageConfigItem { Key = "ThiefMagnetBlackMarketEnabled", Group = "P 特性·招贼体质", Type = "bool", Default = "true", Status = "S0", Desc = "招贼体质·黑市买家每日上门开关" },
        new WageConfigItem { Key = "ThiefMagnetDailyTheftCount", Group = "P 特性·招贼体质", Type = "int", Default = "1", Status = "S0", Desc = "招贼体质·每日丢物品数量(按价值取前N)" },
        new WageConfigItem { Key = "ThiefMagnetDailyTheftEnabled", Group = "P 特性·招贼体质", Type = "bool", Default = "true", Status = "S0", Desc = "招贼体质·每日丢价值最高物品开关" },
        new WageConfigItem { Key = "ThiefMagnetInspectionChance", Group = "P 特性·招贼体质", Type = "float", Default = "0.2", Status = "S0", Desc = "招贼体质·治安检查概率(默认0.2=20%)" },
        new WageConfigItem { Key = "ThiefMagnetVisitEnabled", Group = "P 特性·招贼体质", Type = "bool", Default = "true", Status = "S0", Desc = "招贼体质·小偷上门跳脸+次日回卖总开关" },
        new WageConfigItem { Key = "MadnessExcludedPerks", Group = "Q 特性·精神错乱", Type = "string", Default = "", Status = "S0", Desc = "精神错乱排除特性ID(逗号分隔)。mod特性写中文id(如:声名狼藉)；原版特性…" },
        new WageConfigItem { Key = "MadnessExtraSlots", Group = "Q 特性·精神错乱", Type = "int", Default = "3", Status = "S5", Desc = "精神错乱额外槽位数" },
        new WageConfigItem { Key = "InspectInterval", Group = "R 特性·治安眼线", Type = "int", Default = "15", Status = "S0", Desc = "治安部眼线检查间隔(天)" },
        new WageConfigItem { Key = "DestinyDiceAllowSellingItems", Group = "S 命运之骰", Type = "bool", Default = "true", Status = "S0", Desc = "命运之骰允许丢入在售物品(默认开=现行为；关=柜台/货架待售品也拒绝丢入)" },
        new WageConfigItem { Key = "DiceTriggerValue", Group = "S 命运之骰", Type = "int", Default = "400", Status = "S0", Desc = "骰子触发累计值" },
        new WageConfigItem { Key = "DiceUninstallRefund", Group = "S 命运之骰", Type = "int", Default = "50", Status = "S0", Desc = "卸载返还(%)" },
        new WageConfigItem { Key = "AiGenPrice", Group = "T B2 服务与机器", Type = "int", Default = "4000", Status = "S0", Desc = "AI生成器售价" },
        new WageConfigItem { Key = "AiStableCap", Group = "T B2 服务与机器", Type = "int", Default = "75", Status = "S5", Desc = "阉割版属性上限" },
        new WageConfigItem { Key = "AiUnstableCap", Group = "T B2 服务与机器", Type = "int", Default = "150", Status = "S5", Desc = "不稳定版属性上限" },
        new WageConfigItem { Key = "BatteryInterval", Group = "T B2 服务与机器", Type = "int", Default = "8", Status = "S5", Desc = "电池吞噬季间隔(天)" },
        new WageConfigItem { Key = "CannibalAbsorbPct", Group = "T B2 服务与机器", Type = "int", Default = "10", Status = "S0", Desc = "吞噬吸收(%)" },
        new WageConfigItem { Key = "CannibalCap", Group = "T B2 服务与机器", Type = "int", Default = "150", Status = "S0", Desc = "吞噬三维属性上限" },
        new WageConfigItem { Key = "CannibalInterval", Group = "T B2 服务与机器", Type = "int", Default = "10", Status = "S0", Desc = "吞噬季间隔(天)" },
        new WageConfigItem { Key = "CannibalismSeasonEnabled", Group = "T B2 服务与机器", Type = "bool", Default = "false", Status = "S5", Desc = "吞噬季总开关(默认关)" },
        new WageConfigItem { Key = "GuMachinePrice", Group = "T B2 服务与机器", Type = "int", Default = "3000", Status = "S0", Desc = "养蛊机售价" },
        new WageConfigItem { Key = "ProtectorPrice", Group = "T B2 服务与机器", Type = "int", Default = "1500", Status = "S0", Desc = "保护器核心售价" },
        new WageConfigItem { Key = "ProtectorSupplyCount", Group = "T B2 服务与机器", Type = "int", Default = "3", Status = "S5", Desc = "博士保护器补货数量" },
        new WageConfigItem { Key = "StoryDayFrogM3", Group = "U 剧情", Type = "int", Default = "30", Status = "S0", Desc = "剧情mod：蛙哥纵轴M3归来/永诀触发天数（需≥2线走完N3）" },
        new WageConfigItem { Key = "StoryDayN1", Group = "U 剧情", Type = "int", Default = "15", Status = "S0", Desc = "剧情mod：节点N1触发天数" },
        new WageConfigItem { Key = "StoryDayN2", Group = "U 剧情", Type = "int", Default = "22", Status = "S0", Desc = "剧情mod：节点N2触发天数" },
        new WageConfigItem { Key = "StoryDayN3", Group = "U 剧情", Type = "int", Default = "29", Status = "S0", Desc = "剧情mod：节点N3触发天数" },
        new WageConfigItem { Key = "StoryDayN4", Group = "U 剧情", Type = "int", Default = "36", Status = "S0", Desc = "剧情mod：节点N4触发天数" },
        new WageConfigItem { Key = "StoryDayNellis", Group = "U 剧情", Type = "int", Default = "20", Status = "S0", Desc = "剧情mod：Nellis（技术派）A2抉择触发天数" },
        new WageConfigItem { Key = "StoryFavorAwaken", Group = "U 剧情", Type = "int", Default = "5", Status = "S0", Desc = "剧情mod：Eleanor觉醒线好感阈值（默认5=走N1-A即觉醒）" },
        new WageConfigItem { Key = "StoryRepCollapse", Group = "U 剧情", Type = "int", Default = "40", Status = "S0", Desc = "剧情mod：治安部垮台阈值（低于此值）" },
        new WageConfigItem { Key = "StoryRepVictory", Group = "U 剧情", Type = "int", Default = "60", Status = "S0", Desc = "剧情mod：结局胜利声望阈值" },
        new WageConfigItem { Key = "StorySettleDay60", Group = "U 剧情", Type = "int", Default = "60", Status = "S0", Desc = "剧情mod：第60天五线结算" },
        new WageConfigItem { Key = "StorySettleDay90", Group = "U 剧情", Type = "int", Default = "90", Status = "S0", Desc = "剧情mod：第90天隐藏结算（蛙娘路径）" },
        new WageConfigItem { Key = "CompatBudgetRestore", Group = "V 兼容层", Type = "bool", Default = "true", Status = "S0", Desc = "兼容：客户预算兜底恢复" },
        new WageConfigItem { Key = "CompatPhoneLocalization", Group = "V 兼容层", Type = "bool", Default = "true", Status = "S5", Desc = "兼容：电话簿本地化" },
        new WageConfigItem { Key = "CompatTradeClamp", Group = "V 兼容层", Type = "bool", Default = "true", Status = "S0", Desc = "兼容：交易价格兜底（负价→0）" },
        new WageConfigItem { Key = "CompatWineNameProtect", Group = "V 兼容层", Type = "bool", Default = "true", Status = "S0", Desc = "兼容：酒瓶自定义名保留" },
    };

    // 启动自检：InitPrefs 的 CreateEntry 键 vs 元数据键 双向比对，缺失打 LogError（防新增配置忘加元数据）
    internal static void SelfCheck()
    {
        try
        {
            var set = new System.Collections.Generic.HashSet<string>();
            foreach (var it in Items) set.Add(it.Key);
            int missMeta = 0;
            var cat = BuildConfig.Cat;
            if (cat != null)
            {
                foreach (var e in cat.Entries)
                {
                    try { if (e != null && e.Identifier != null && !set.Contains(e.Identifier)) { Core.LogError("[WP配置] 元数据缺失: " + e.Identifier); missMeta++; } } catch { }
                }
            }
            Core.LogMsg("[WP配置] 元数据自检: 共 " + Items.Count + " 项, 缺失 " + missMeta);
        }
        catch (System.Exception ex) { Core.LogMsg("[WP配置] 元数据自检异常: " + ex.Message); }
    }
}
