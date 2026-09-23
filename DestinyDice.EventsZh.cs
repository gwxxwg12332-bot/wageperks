using System;

using System.Collections.Generic;

using Il2Cpp;

using Il2CppInterop.Runtime;

using Il2CppInterop.Runtime.InteropTypes;

using MelonLoader;

using UnityEngine;

namespace JacksonPerks
{
    // ============================================================

    // 命运骰子 - 无内部空间版

    // 直接"吃掉"物品：拖物品到命运骰子上 → 销毁物品 + 累计价值

    // 每满400价值触发1个随机事件

    // ============================================================

    public static partial class DestinyDice

    {



        // ===== 事件汉化（identifier → 中文名/新闻标题/新闻描述） =====

        public static readonly Dictionary<string, string> EventZhName = new Dictionary<string, string>

        {

            ["contrabandCrackdown"] = "违禁品严打",

            ["upperLevelParty"] = "上层派对",

            ["foodRecall"] = "食品召回",

            ["terroristAttack"] = "恐怖袭击",

            ["goldenTicketHunt"] = "金票搜寻",

            ["factoryFarmRenovation"] = "工厂农场翻新",

            ["hospitalRenovation"] = "医院翻新",

            ["commandLeak"] = "命令泄露",

            ["robbedTrain"] = "火车被劫",

            ["securityBreach"] = "安全漏洞",

            ["expiredImmunivaxDumping"] = "过期疫苗倾倒",



            // ===== v1.1.2 报纸汉化补全（EventListNormal/Special/Unique/Innate 全部事件） =====

            ["badNutrifruitHarvest"] = "营养果歉收",

            ["seedShortage"] = "种子短缺",

            ["medicalSuppliesShortage"] = "医疗用品短缺",

            ["water_treatment_breakdown"] = "水处理故障",

            ["scheduled_treatment_maintenance"] = "水处理维护",

            ["miner_return_ice_asteroid"] = "冰小行星运抵",

            ["lower_level_mob_justice"] = "下层区骚乱",

            ["storeroom_excavated"] = "仓库重见天日",

            ["miningExpeditionReturn"] = "采矿远征归来",

            ["powerPlantMaintenance"] = "电厂维护",

            ["stationWideBlackout"] = "全站停电",

            ["cruiseShipDocked"] = "游轮靠岸",

            ["oxymoreWave"] = "氧潮来袭",

            ["evidenceLockerBreakIn"] = "证据柜被闯",

            ["wanted3"] = "三级通缉",

            ["rent_day"] = "房租日",

            ["mortgage_payment"] = "房贷还款",

            ["cartel_visit"] = "卡特尔来访",

            ["lower_level_alcohol_shortage"] = "下层区酒荒",

            ["construction_project"] = "建筑项目",

            ["counterfeitMedicalSupplies"] = "假药被查获",

            ["meteorShower"] = "陨石雨",

            ["nutrifruitHarvest"] = "营养果丰收",

            ["powerShortage"] = "电力短缺",

            ["seedHeist"] = "种子被劫",



            ["lotteryDraw"] = "彩票开奖",

            ["lottery_draw"] = "彩票开奖",



            // ===== v1.2.0 新增：MoreEvents 补给/装饰事件 + 蛙哥自制事件 =====

            ["factoryFarmRenovationResupply"] = "工厂农场翻新补给",

            ["hospitalRenovationResupply"] = "医院翻新补给",

            ["crazyManYellingAtTheSky"] = "太空狂人呐喊",

            ["security_undercover_patrol"] = "安保部换装巡逻",

            ["nutrifruit_new_release"] = "营养果新品发布会",

            ["dumping_grounds_cleanup"] = "垃圾场大清理",

            ["black_market_sweep"] = "黑市码头大扫荡",

            ["upper_level_luxury_expo"] = "上层区奢华展",



            // ===== v1.2.1 复合型事件 =====

            ["black_market_arms_flow"] = "黑市军火流入",

            ["upper_level_auction_week"] = "上层区拍卖周",

            ["nutrifruit_crisis_hoard"] = "营养果危机囤积",

            ["train_heist_aftermath"] = "火车劫案余波",

            ["wandering_merchant_legacy"] = "流浪商人遗赠",

            ["drunk_riot"] = "酒鬼闹事",

        };

        public static readonly Dictionary<string, string> EventZhNews = new Dictionary<string, string>

        {

            ["contrabandCrackdown"] = "社区动态",

            ["upperLevelParty"] = "上层区消息",

            ["foodRecall"] = "社区动态",

            ["terroristAttack"] = "紧急通报",

            ["goldenTicketHunt"] = "社区动态",

            ["factoryFarmRenovation"] = "商业消息",

            ["hospitalRenovation"] = "商业消息",

            ["commandLeak"] = "机密消息",

            ["robbedTrain"] = "突发新闻",

            ["securityBreach"] = "警报",

            ["expiredImmunivaxDumping"] = "社区动态",



            ["badNutrifruitHarvest"] = "社区动态",

            ["seedShortage"] = "社区动态",

            ["medicalSuppliesShortage"] = "社区动态",

            ["water_treatment_breakdown"] = "警报",

            ["scheduled_treatment_maintenance"] = "社区动态",

            ["miner_return_ice_asteroid"] = "商业消息",

            ["lower_level_mob_justice"] = "紧急通报",

            ["storeroom_excavated"] = "社区动态",

            ["miningExpeditionReturn"] = "商业消息",

            ["powerPlantMaintenance"] = "商业消息",

            ["stationWideBlackout"] = "警报",

            ["cruiseShipDocked"] = "上层区消息",

            ["oxymoreWave"] = "紧急通报",

            ["evidenceLockerBreakIn"] = "警报",

            ["wanted3"] = "紧急通报",

            ["rent_day"] = "财务消息",

            ["mortgage_payment"] = "财务消息",

            ["cartel_visit"] = "警报",

            ["lower_level_alcohol_shortage"] = "社区动态",

            ["construction_project"] = "商业消息",

            ["counterfeitMedicalSupplies"] = "警报",

            ["meteorShower"] = "突发新闻",

            ["nutrifruitHarvest"] = "社区动态",

            ["powerShortage"] = "警报",

            ["seedHeist"] = "社区动态",



            ["lotteryDraw"] = "社区动态",

            ["lottery_draw"] = "社区动态",



            ["factoryFarmRenovationResupply"] = "商业消息",

            ["hospitalRenovationResupply"] = "商业消息",

            ["crazyManYellingAtTheSky"] = "社区动态",

            ["security_undercover_patrol"] = "社区动态",

            ["nutrifruit_new_release"] = "商业消息",

            ["dumping_grounds_cleanup"] = "社区动态",

            ["black_market_sweep"] = "警报",

            ["upper_level_luxury_expo"] = "上层区消息",



            ["black_market_arms_flow"] = "警报",

            ["upper_level_auction_week"] = "上层区消息",

            ["nutrifruit_crisis_hoard"] = "警报",

            ["train_heist_aftermath"] = "突发新闻",

            ["wandering_merchant_legacy"] = "社区动态",

            ["drunk_riot"] = "紧急通报",

        };

        public static readonly Dictionary<string, string> EventZhDesc = new Dictionary<string, string>

        {

            ["contrabandCrackdown"] = "安保部门对辖区开展违禁品突击清查，风声很紧：违禁类商品难以出手，黑市渠道暂时收缩，相关货品价格波动剧烈。",

            ["upperLevelParty"] = "上层区举办盛大派对，阔佬们出手阔绰：奢侈品、宴会用品需求大增，高价商品更容易卖出好价钱。",

            ["foodRecall"] = "一批食品被检出问题并紧急召回：涉事食品价格大跌，可趁机低价囤货，其余食品类商品销量也会受影响。",

            ["terroristAttack"] = "辖区发生恐怖袭击，安保等级全面升级：安检严格、巡逻加强，治安类用品需求上升。",

            ["goldenTicketHunt"] = "有人在社区里藏了金票，居民疯狂翻找：杂物、箱柜类商品被大量抢购，出手容易。",

            ["factoryFarmRenovation"] = "工厂农场翻新工程启动：建材、机械和种植物资需求大涨，相关商品价格水涨船高。",

            ["hospitalRenovation"] = "医院翻新扩建工程开工：医疗物资、药品和设备需求激增，医用商品价格上涨。",

            ["commandLeak"] = "上层机密命令泄露，引发不小的混乱：居民情绪紧张，部分物资被抢购，供需关系被打乱。",

            ["robbedTrain"] = "一列运货火车被劫，大量货物流入黑市：黑市商品供应充足，倒卖利润可观。",

            ["securityBreach"] = "安保系统出现漏洞，巡逻力度减弱：治安类商品需求下降，出手价格走低。",

            ["expiredImmunivaxDumping"] = "过期疫苗被随意倾倒，引发民众不满：医疗类商品需求上升，卫生用品走俏。",



            ["badNutrifruitHarvest"] = "营养果大面积歉收：水果类商品价格看涨，供应紧张，进货渠道收窄。",

            ["seedShortage"] = "种子供应紧张：种植物资价格上涨，农业相关商品需求上升。",

            ["medicalSuppliesShortage"] = "医疗物资告急：药品和医疗用品价格飙升，供应缺口明显。",

            ["water_treatment_breakdown"] = "水处理站发生故障：净水设备、滤芯等商品需求大增，水相关物资价格上涨。",

            ["scheduled_treatment_maintenance"] = "水处理站例行维护：相关设备供应短期波动，维护类物资需求增加。",

            ["miner_return_ice_asteroid"] = "矿工从冰小行星满载而归：冰块、冷饮类物资供应增加，价格回落。",

            ["lower_level_mob_justice"] = "下层区爆发骚乱，治安形势紧张：安保、防身类商品需求上升，下层区交易风险增加。",

            ["storeroom_excavated"] = "一间废弃储藏室被挖开：里面发现不少存货，旧货和杂物类商品流入市场。",

            ["miningExpeditionReturn"] = "采矿队满载归来：矿石供应充足，矿类商品价格回落，进货机会多。",

            ["powerPlantMaintenance"] = "发电厂进入维护期：电力供应趋紧，电池、发电机类商品需求上升。",

            ["stationWideBlackout"] = "空间站发生大范围停电，一片漆黑：照明、电池类商品被抢购，价格飙升。",

            ["cruiseShipDocked"] = "豪华游轮靠站补给：阔佬们出手大方，奢侈品和高价商品容易脱手。",

            ["oxymoreWave"] = "罕见氧潮袭击空间站：氧气储备告急，氧气罐、呼吸面罩类商品价格大涨。",

            ["evidenceLockerBreakIn"] = "治安部证据柜被人闯入，重要证物失踪：安保加强，证据相关任务变多。",

            ["wanted3"] = "三级通缉犯在社区现身：安保高度戒备，防身类商品需求上升。",

            ["rent_day"] = "今天是交租日：租金已从账户扣除，今天现金会紧张，卖货回款更显重要。",

            ["mortgage_payment"] = "房贷还款日到了：一笔款项已扣除，注意留足现金周转。",

            ["cartel_visit"] = "卡特尔派人上门，来者不善：黑市生意可能受影响，违禁类交易要小心。",

            ["lower_level_alcohol_shortage"] = "一场大火烧毁了底层区的酿酒厂，库存损失惨重。酒鬼们开始躁动不安，有人已经在黑市门口排队。预计酒类价格将大幅上涨。",

            ["construction_project"] = "空间站启动新的建筑项目：建材、工具和机械类商品需求大增，相关价格水涨船高。",

            ["counterfeitMedicalSupplies"] = "一批假医疗用品被查出并追缴：药品市场动荡，真品医疗物资需求上升、价格走高。",

            ["meteorShower"] = "罕见陨石雨掠过空间站：陨石碎片散落各处，稀有矿物和纪念品市场火热。",

            ["nutrifruitHarvest"] = "营养果迎来大丰收：水果供应充足，价格回落，进货机会多。",

            ["powerShortage"] = "空间站电力短缺：电池、发电机和照明类商品被抢购，价格大涨。",

            ["seedHeist"] = "一批珍贵种子在运输途中被劫走：种子供应紧张，种植物资价格上涨。",



            ["lotteryDraw"] = "本周彩票开奖，有人一夜暴富：居民现金变多，消费意愿上升，好货容易出手。",

            ["lottery_draw"] = "本周彩票开奖，有人一夜暴富：居民现金变多，消费意愿上升，好货容易出手。",



            ["factoryFarmRenovationResupply"] = "应指挥部请求，补给船大批抵达，运载大量建材与物资，工厂翻新得以推进，材料与食品价格小幅回落。",

            ["hospitalRenovationResupply"] = "应指挥部请求，补给船大批抵达，运载大量建材与物资，医院翻新得以推进，医疗物资供应充足、价格回落。",

            ["crazyManYellingAtTheSky"] = "一位老人对着舷窗外面的飞船大喊大叫，还没弄明白太空里根本听不见声音。今日无事，唯有此新闻充数。",

            ["security_undercover_patrol"] = "安保部派出便衣巡逻员，暗中盯梢可疑交易。风声鹤唳之下，违禁品与武器买卖风险陡增，安保类装备需求上升、价格走高。",

            ["nutrifruit_new_release"] = "营养果公司举办盛大新品发布会，上层区的阔佬们纷纷抢购新口味零食，食品与零食类商品价格看涨。",

            ["dumping_grounds_cleanup"] = "空间站下令对垃圾场进行大规模清理，拾荒者们翻出大量废料与零件，市场上材料供应充足，价格回落。",

            ["black_market_sweep"] = "安保部突袭黑市码头，缴获大批走私货物并当众销毁。走私贩子纷纷蛰伏，货源紧缺，违禁品价格飙升。",

            ["upper_level_luxury_expo"] = "上层区正在举办奢华展会，享乐主义者们挥金如土，抢购珍稀好货，奢侈品价格一路走高。",



            ["black_market_arms_flow"] = "近期劫案中缴获的武器大量流入黑市，走私贩子急于低价出货，武器与违禁品价格回落。",

            ["upper_level_auction_week"] = "上层区举办为期一周的拍卖会，收藏家们争相竞拍珍稀好货，奢侈品价格走高，阔佬客户也更容易上门。",

            ["nutrifruit_crisis_hoard"] = "又一批营养果被召回引发恐慌，黑市投机者趁机囤积居奇，食品价格回落，违禁品价格飙升。",

            ["train_heist_aftermath"] = "火车劫案后，被劫的补给箱低价流入市场，安保部则加强了对门禁卡的管控，补给箱价格回落、门禁卡价格上涨。",

            ["wandering_merchant_legacy"] = "一位老流浪商人倒在了空间站门口，临终前把全部家当托付给当年帮助过他的当铺——一份礼物已送到你的柜台上。",

            ["drunk_riot"] = "下层区酒鬼群殴闹事，砸毁大量酒水，还伤及无辜路人，酒类库存受损、医疗物资需求上升。",

        };



        // 英文标题兜底：identifier 未命中/为空时，按 newsName 英文原文翻译（覆盖 TRANSPORT SHIP MISSING 等填充新闻）

        public static readonly Dictionary<string, string> TitleZhName = new Dictionary<string, string>

        {

            ["TRANSPORT SHIP MISSING"] = "运输船失踪",

            ["Attack On Our Soil!"] = "本土遇袭！",

            ["Command Database Leaked!"] = "命令数据库泄露！",

            ["Crazy Man Yelling At Space"] = "太空狂人呐喊",

            ["Golden Ticket Hunt"] = "金票搜寻",

            ["High Stakes Robbery!"] = "高额劫案！",

            ["Hospital Renovation Resupply Arrives"] = "医院翻新补给抵达",

            ["Immunivax™ Dumping"] = "过期疫苗倾倒",

            ["Lower Level Hospital Renovation"] = "下层区医院翻新",

            ["New Virus Found In Nutrifruit!"] = "营养果中发现新病毒！",

            ["Nutrifruit Co. Factory Farm Renovation"] = "营养果公司工厂翻新",

            ["Nutrifruit Renovation Resupply Arrives"] = "营养果翻新补给抵达",

            ["Security Starts War On Contraband"] = "安保部严打违禁品",

            ["Upcoming Upper Level Party!"] = "上层区派对在即！",

            ["LOWER LEVEL CELLARS RUN DRY!"] = "下层区酒窖告急！",

            ["SECURITY GOES UNDERCOVER!"] = "安保部换装巡逻！",

            ["NUTRIFRUIT™ NEW FLAVOR LAUNCH!"] = "营养果新品发布会！",

            ["DUMPING GROUNDS CLEANUP BEGINS!"] = "垃圾场大清理开始！",

            ["BLACK MARKET PORT SWEPT!"] = "黑市码头被扫荡！",

            ["UPPER LEVEL LUXURY EXPO!"] = "上层区奢华展！",

            ["BLACK MARKET ARMS FLOW!"] = "黑市军火流入！",

            ["UPPER LEVEL AUCTION WEEK!"] = "上层区拍卖周！",

            ["NUTRIFRUIT CRISIS AND BLACK MARKET HOARDING!"] = "营养果危机与黑市囤积！",

            ["TRAIN HEIST AFTERMATH!"] = "火车劫案余波！",

            ["WANDERING MERCHANT'S LEGACY!"] = "流浪商人遗赠！",

            ["DRUNKS RIOT IN THE LOWER LEVEL!"] = "下层区酒鬼闹事！",

        };

        public static readonly Dictionary<string, string> TitleZhDesc = new Dictionary<string, string>

        {

            ["TRANSPORT SHIP MISSING"] = "一艘运输船在航线中失去联系，货运班次被迫调整，市场供应预期收紧，相关物资价格或现波动。",

            ["Attack On Our Soil!"] = "一次蓄意袭击威胁到我们的土地与家园，安保部门已全面戒备，防身与安保类物资需求上升。",

            ["Command Database Leaked!"] = "指挥部的机密数据库遭到泄露，大量内部情报流入黑市，局势紧张，相关交易风险升高。",

            ["Crazy Man Yelling At Space"] = "一个疯子在空间站里对着太空大喊大叫，保安把他拖走了。今天没什么大事，也许这能给你的巡逻日添点乐子。",

            ["Golden Ticket Hunt"] = "皮莉·蓬卡糖果公司宣布在糖果里藏了金票，居民们疯狂翻找糖果包装，零食类商品被抢购一空。",

            ["High Stakes Robbery!"] = "一列运输列车遭遇高额劫案，大量补给箱与门禁卡被洗劫一空，黑市上突然多出不少来路不明的货。",

            ["Hospital Renovation Resupply Arrives"] = "应指挥部请求，补给船大批抵达，载着医院翻新所需的大量物资，建材与医疗物资供应充足。",

            ["Immunivax™ Dumping"] = "医疗部在一次仓储检查中扔掉了数百支过期的免疫疫苗，卫生隐患引发民众不满，医疗物资需求上升。",

            ["Lower Level Hospital Renovation"] = "下层区医院正在进行大规模翻新，两名客户被分流到附近诊所，医疗类物资需求上升。",

            ["New Virus Found In Nutrifruit!"] = "多名营养果™顾客报告食物中毒症状。若你持有受影响的营养果，建议立即处理——营养果类商品价格大跌。",

            ["Nutrifruit Co. Factory Farm Renovation"] = "著名水培公司营养果公司正在翻新其主力工厂农场！本地不安情绪蔓延，营养果供应短期波动。",

            ["Nutrifruit Renovation Resupply Arrives"] = "应指挥部请求，补给船大批抵达，载着营养果工厂翻新所需物资，种植物资供应充足。",

            ["Security Starts War On Contraband"] = "空间站指挥部发布新指令，命令安保部尽可能收缴违禁品，风声很紧，违禁类交易风险大增。",

            ["Upcoming Upper Level Party!"] = "一位本地享乐主义者开始为俱乐部的大型派对发送邀请函。专家表示这会让奢侈品消费迎来一波高峰。",

            ["LOWER LEVEL CELLARS RUN DRY!"] = "一场大火烧毁了底层区的酿酒厂，库存损失惨重。酒鬼们开始躁动不安，有人已经在黑市门口排队。预计酒类价格将大幅上涨。",

            ["SECURITY GOES UNDERCOVER!"] = "安保部派出便衣巡逻员，暗中盯梢可疑交易。风声鹤唳之下，违禁品与武器买卖风险陡增，安保类装备需求上升、价格走高。",

            ["NUTRIFRUIT™ NEW FLAVOR LAUNCH!"] = "营养果公司举办盛大新品发布会，上层区的阔佬们纷纷抢购新口味零食，食品与零食类商品价格看涨。",

            ["DUMPING GROUNDS CLEANUP BEGINS!"] = "空间站下令对垃圾场进行大规模清理，拾荒者们翻出大量废料与零件，市场上材料供应充足，价格回落。",

            ["BLACK MARKET PORT SWEPT!"] = "安保部突袭黑市码头，缴获大批走私货物并当众销毁。走私贩子纷纷蛰伏，货源紧缺，违禁品价格飙升。",

            ["UPPER LEVEL LUXURY EXPO!"] = "上层区正在举办奢华展会，享乐主义者们挥金如土，抢购珍稀好货，奢侈品价格一路走高。",

            ["BLACK MARKET ARMS FLOW!"] = "近期劫案中缴获的武器大量流入黑市，走私贩子急于低价出货，武器与违禁品价格回落。",

            ["UPPER LEVEL AUCTION WEEK!"] = "上层区举办为期一周的拍卖会，收藏家们争相竞拍珍稀好货，奢侈品价格走高，阔佬客户也更容易上门。",

            ["NUTRIFRUIT CRISIS AND BLACK MARKET HOARDING!"] = "又一批营养果被召回引发恐慌，黑市投机者趁机囤积居奇，食品价格回落，违禁品价格飙升。",

            ["TRAIN HEIST AFTERMATH!"] = "火车劫案后，被劫的补给箱低价流入市场，安保部则加强了对门禁卡的管控，补给箱价格回落、门禁卡价格上涨。",

            ["WANDERING MERCHANT'S LEGACY!"] = "一位老流浪商人倒在了空间站门口，临终前把全部家当托付给当年帮助过他的当铺——一份礼物已送到你的柜台上。",

            ["DRUNKS RIOT IN THE LOWER LEVEL!"] = "下层区酒鬼群殴闹事，砸毁大量酒水，还伤及无辜路人，酒类库存受损、医疗物资需求上升。",

        };






        // 全局汉化兜底：任何走 GetDisplayName 的事件都按identifier汉化（骰子触发+游戏原生事件全覆盖）




        // 全局汉化：任何事件排期（QueueFuturEvent）时改写字段（报纸 newsName/newsDescription 全覆盖）

        // 游戏原生事件+骰子触发+其他mod事件，只要走排期就汉化






        // ===== 报纸翻页（v1.1.2：最多4条/页，方向键翻页） =====

        // Il2Cpp方法List参数必须用 Il2CppSystem.Collections.Generic.List（编译类型匹配）

        public static bool _newsOpen;                       // 报纸是否打开

        public static Il2CppSystem.Collections.Generic.List<Il2Cpp.StoreEvent> _newsAllEvents; // 完整事件列表

        public static int _newsPage;                        // 当前页（0基）

        public static bool _newsPaging;                     // 翻页中（防止覆盖完整列表）


        private static UnityEngine.GameObject _newsPrevBtn; // 上一页按钮（UGUI）

        private static UnityEngine.GameObject _newsNextBtn; // 下一页按钮（UGUI）

        private static bool _newsBtnCreated;                // 按钮已创建标志



        // 报纸翻页可见按钮（Core.OnGUI 调用）：◀上一页 / 页码 / 下一页▶




        // 创建/显示翻页按钮（UGUI）：挂报纸面板下，跟随显隐




        static UnityEngine.GameObject CreateNewsButton(UnityEngine.Transform parent, string name, string text, UnityEngine.Vector2 pos, UnityEngine.Vector2 size, System.Action onClick)

        {

            var go = new UnityEngine.GameObject(name);

            var rt = go.AddComponent<UnityEngine.RectTransform>();

            rt.SetParent(parent, false);

            rt.anchorMin = new UnityEngine.Vector2(0f, 0f);

            rt.anchorMax = new UnityEngine.Vector2(0f, 0f);

            rt.pivot = new UnityEngine.Vector2(0f, 0f);

            rt.anchoredPosition = pos;

            rt.sizeDelta = size;

            var img = go.AddComponent<UnityEngine.UI.Image>();

            img.color = new UnityEngine.Color(0.08f, 0.08f, 0.12f, 0.92f);

            var btn = go.AddComponent<UnityEngine.UI.Button>();

            var txtGo = new UnityEngine.GameObject("Text");

            txtGo.transform.SetParent(go.transform, false);

            var txtRt = txtGo.AddComponent<UnityEngine.RectTransform>();

            txtRt.anchorMin = UnityEngine.Vector2.zero;

            txtRt.anchorMax = UnityEngine.Vector2.one;

            txtRt.offsetMin = UnityEngine.Vector2.zero;

            txtRt.offsetMax = UnityEngine.Vector2.zero;

            var tmp = txtGo.AddComponent<UnityEngine.UI.Text>();

            tmp.text = text;

            tmp.fontSize = 20;

            tmp.alignment = UnityEngine.TextAnchor.MiddleCenter;

            tmp.color = UnityEngine.Color.white;

            btn.onClick.AddListener(DelegateSupport.ConvertDelegate<UnityEngine.Events.UnityAction>(onClick));

            return go;

        }












        // 应用当前页（最终版）：完全复刻原生 PopulateUI 六步流程（ISIL NewsUIManager.txt 实锤）

        // PopulateUI = GetActiveEvents→过滤→隐藏4layout→按count选layout→ActivateLayout(激活layout+elements全隐藏+ClearElement)→填充循环(SetActive(true)+PopulateUI)

        // 之前只调SelectLayout→element全隐藏且无填充循环→空白。这里六步全部手动复刻。




        // Patch NewsUIManager.PopulateUI Postfix：报纸填充时直接调 GetActiveEvents 取全量事件列表

        // （GetActiveEvents 是报纸数据源，返回全量；原生在填充层截断到4条——这里存全量供翻页）



        // 报纸连载注入钩子（独立mod SerialNewsMod 注册）：在报纸渲染前向事件列表注入连载内容

        public static System.Action<Il2CppSystem.Collections.Generic.List<Il2Cpp.StoreEvent>> NewsSerialInjector = null;






        // 取第page页切片（页大小4——原生layout4只支持4条，翻页切换数据源）








        // Patch NewsUIManager.ToggleUI Postfix：记录打开/关闭状态




        // Patch NewsUIManager.CloseUI Postfix：强制关闭状态




        // Patch InputActionManager.Update Postfix：方向键翻页（轻量检查，报纸打开才动作）


}

}
