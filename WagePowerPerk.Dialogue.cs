using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace JacksonPerks;

internal sealed partial class WagePowerPerk : CustomStartingPerk
{

    // 里程碑对话（购买达到一定数量）
    private static string GetMilestoneDialogue()
    {
        if (_totalPurchases >= 100)
        {
            return LangHelper.T("老板，你这都收了上百件货了，真是业界传奇啊！", "Boss, you've taken in over a hundred items! A legend of the trade!");
        }
        if (_totalPurchases >= 50)
        {
            return LangHelper.T("听说你这收了不少好东西，我也来凑凑热闹。", "Heard you've collected quite the treasures. Figured I'd join in.");
        }
        if (_totalPurchases >= 10)
        {
            return LangHelper.T("你这店开得有模有样的，以后我常来。", "Your shop's shaping up nicely. I'll be a regular.");
        }
        return null;
    }

    // 获取客户阵营key（用于选择阵营对话池）
    private static string GetFactionKey(StoreClient client)
    {
        if (client == null) return "lower";
        string faction = client.clientFaction ?? "";
        string id = (client.identifier ?? "").ToLowerInvariant();

        // Scav拾荒者没有独立faction字符串，用identifier识别
        if (id.StartsWith("scav")) return "scav";
        if (faction.Contains("UPPER")) return "upper";
        if (faction.Contains("SECURITY")) return "security";
        if (faction.Contains("TOURIST")) return "tourist";
        if (faction.Contains("BLACK_MARKET")) return "blackmarket";
        if (faction.Contains("CARTEL")) return "cartel";
        if (faction.Contains("REVOLUTION")) return "rev";
        if (faction.Contains("CHURCH")) return "church";
        // 下层（FACTION_LOWER_LEVEL / FACTION_MIDDLE / 未知）兜底
        return "lower";
    }

    // 根据阵营key获取对话池
    private static string[] GetFactionDialoguePool(string factionKey)
    {
        switch (factionKey)
        {
            case "scav": return null;  // 已删除底层人对话，保持原版
            case "upper": return UpperDialogues;
            case "security": return SecurityDialogues;
            case "tourist": return TouristDialogues;
            case "blackmarket": return BlackMarketDialogues;
            case "cartel": return CartelDialogues;
            case "rev": return RevDialogues;
            case "church": return ChurchDialogues;
            default: return null;  // 已删除底层人(Lower)对话，保持原版
        }
    }

    // 根据玩家状态获取对话（经济/经验/声誉综合判断）
    private static string GetPlayerStatusDialogue()
    {
        try
        {
            PlayerEconomy economy = GetPlayerEconomy();
            PlayerLevel level = GetPlayerLevel();
            PlayerReputation reputation = GetPlayerReputation();

            // 随机选择一个维度来生成对话
            int dim = Core.Rng.Next(3);

            if (dim == 0)
            {
                // 经济维度
                if (economy == PlayerEconomy.Poor)
                    return PoorPlayerDialogues[Core.Rng.Next(PoorPlayerDialogues.Length)];
                if (economy == PlayerEconomy.Rich || economy == PlayerEconomy.Loaded)
                    return RichPlayerDialogues[Core.Rng.Next(RichPlayerDialogues.Length)];
            }
            else if (dim == 1)
            {
                // 经验维度
                if (level == PlayerLevel.Newbie)
                    return NewbiePlayerDialogues[Core.Rng.Next(NewbiePlayerDialogues.Length)];
                if (level == PlayerLevel.Veteran || level == PlayerLevel.Legend)
                    return VeteranPlayerDialogues[Core.Rng.Next(VeteranPlayerDialogues.Length)];
            }
            else
            {
                // 声誉维度
                if (reputation == PlayerReputation.Shady)
                    return ShadyPlayerDialogues[Core.Rng.Next(ShadyPlayerDialogues.Length)];
                if (reputation == PlayerReputation.Reputable || reputation == PlayerReputation.Legendary)
                    return ReputablePlayerDialogues[Core.Rng.Next(ReputablePlayerDialogues.Length)];
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    // 按客户意图选择阵营对话池（BUY/SELL 必须用对应意图池，避免买卖台词混淆）
    private static string[] GetFactionIntentPool(string factionKey, StoreClient.ClientIntent intent)
    {
        if (intent == StoreClient.ClientIntent.BUY)
        {
            if (factionKey == "upper") return UpperBuyerDialogues;
            if (factionKey == "tourist") return TouristBuyerDialogues;
            return BuyerDialogues;
        }
        if (intent == StoreClient.ClientIntent.SELL)
        {
            if (factionKey == "upper") return UpperSellerDialogues;
            if (factionKey == "tourist") return TouristSellerDialogues;
            return SellerDialogues;
        }
        // 其他意图（SELLNBUY 等）用原混合池
        switch (factionKey)
        {
            case "scav": return null;  // 已删除底层人对话，保持原版
            case "upper": return UpperDialogues;
            case "security": return SecurityDialogues;
            case "tourist": return TouristDialogues;
            case "blackmarket": return BlackMarketDialogues;
            case "cartel": return CartelDialogues;
            case "rev": return RevDialogues;
            case "church": return ChurchDialogues;
            default: return null;  // 已删除底层人(Lower)对话，保持原版
        }
    }

    // 英文模式对话：已由各中文对话数组 LangHelper.T 化后按语言切换，旧 _EN 三池删除

    // 根据客户信息生成故事性对话
    private static string GenerateStoryDialogue(StoreClient client, string clientName)
    {
        try
        {
            // 0. 跳过有剧情/固定对话的特殊NPC，保留它们的原版第一句对话
            // 包括：剧情角色（博士/王尔德/鉴定/奥丁）、房东、同行/竞争者、收租人、债主等
            // 这些NPC的第一句对话有特殊意义（催租/挑衅/剧情推进），不应该被随机对话覆盖
            if (clientName.Contains("博士") || clientName.Contains("Jackson") ||
                clientName.Contains("王尔德") || clientName.Contains("Wilde") ||
                clientName.Contains("鉴定") || clientName.Contains("Appraisal") || clientName.Contains("appraisal") ||
                clientName.Contains("奥丁") || clientName.Contains("Odin") || clientName.Contains("哈拉尔德森") || clientName.Contains("Haraldsson") ||
                // 房东/收租/债主类NPC（第一句对话是催租/讨债，有特殊意义）
                clientName.Contains("房东") || clientName.Contains("收租") || clientName.Contains("地主") ||
                clientName.Contains("债主") || clientName.Contains("讨债") || clientName.Contains("催租") ||
                clientName.Contains("landlord") || clientName.Contains("Landlord") ||
                clientName.Contains("rent") || clientName.Contains("Rent") ||
                clientName.Contains("creditor") || clientName.Contains("Creditor") ||
                clientName.Contains("debt") || clientName.Contains("Debt") ||
                // 同行/竞争者类NPC（第一句对话是挑衅/比较，有特殊意义）
                clientName.Contains("同行") || clientName.Contains("竞争者") || clientName.Contains("对手") ||
                clientName.Contains("竞争") || clientName.Contains("隔壁") || clientName.Contains("对面") ||
                clientName.Contains("rival") || clientName.Contains("Rival") ||
                clientName.Contains("competitor") || clientName.Contains("Competitor") ||
                // identifier匹配（更准确）
                client.identifier.Contains("landlord") || client.identifier.Contains("rent") ||
                client.identifier.Contains("rival") || client.identifier.Contains("competitor") ||
                client.identifier.Contains("creditor") || client.identifier.Contains("debt"))
            {
                return null;
            }

            // 0.1-0.13 全面排除有特殊身份的NPC（保守策略：只有最普通的买家/卖家才用随机对话）
            string clientIdLower = (client.identifier ?? "").ToLowerInvariant();
            
            // 0.4 执法/治安/安保类NPC
            if (clientName.Contains("警察") || clientName.Contains("治安") || clientName.Contains("警官") ||
                clientName.Contains("巡捕") || clientName.Contains("保安") || clientName.Contains("安保") ||
                clientName.Contains("执法") || clientName.Contains("稽查") || clientName.Contains("搜查") ||
                clientIdLower.Contains("police") || clientIdLower.Contains("security") ||
                clientIdLower.Contains("guard") || clientIdLower.Contains("officer") ||
                clientIdLower.Contains("inspection") || clientIdLower.Contains("inspector"))
            { return null; }
            
            // 0.5 医疗/药品类NPC
            if (clientName.Contains("医生") || clientName.Contains("护士") || clientName.Contains("大夫") ||
                clientName.Contains("诊所") || clientName.Contains("医院") || clientName.Contains("药剂师") ||
                clientName.Contains("药师") || clientName.Contains("医药") ||
                clientIdLower.Contains("doctor") || clientIdLower.Contains("nurse") ||
                clientIdLower.Contains("medic") || clientIdLower.Contains("clinic") ||
                clientIdLower.Contains("hospital") || clientIdLower.Contains("pharmacist"))
            { return null; }
            
            // 0.6 法律/金融/官方职位类NPC
            if (clientName.Contains("律师") || clientName.Contains("会计师") || clientName.Contains("金融") ||
                clientName.Contains("银行") || clientName.Contains("保险") || clientName.Contains("顾问") ||
                clientName.Contains("经纪人") || clientName.Contains("中介") || clientName.Contains("代理") ||
                clientIdLower.Contains("lawyer") || clientIdLower.Contains("accountant") ||
                clientIdLower.Contains("banker") || clientIdLower.Contains("agent") ||
                clientIdLower.Contains("broker") || clientIdLower.Contains("consultant"))
            { return null; }
            
            // 0.7 技术/工程/科研类NPC
            if (clientName.Contains("工程师") || clientName.Contains("技术员") || clientName.Contains("机械师") ||
                clientName.Contains("维修工") || clientName.Contains("科学家") || clientName.Contains("研究员") ||
                clientName.Contains("学者") || clientName.Contains("教授") || clientName.Contains("黑客") ||
                clientIdLower.Contains("engineer") || clientIdLower.Contains("mechanic") ||
                clientIdLower.Contains("scientist") || clientIdLower.Contains("researcher") ||
                clientIdLower.Contains("hacker") || clientIdLower.Contains("professor"))
            { return null; }
            
            // 0.8 军人/安保/武装类NPC
            if (clientName.Contains("士兵") || clientName.Contains("军官") || clientName.Contains("退伍军人") ||
                clientName.Contains("雇佣兵") || clientName.Contains("保镖") || clientName.Contains("武装") ||
                clientIdLower.Contains("soldier") || clientIdLower.Contains("mercenary") ||
                clientIdLower.Contains("veteran") || clientIdLower.Contains("bodyguard") ||
                clientIdLower.Contains("military"))
            { return null; }
            
            // 0.9 商人/贸易商/经销商类NPC
            if (clientName.Contains("商人") || clientName.Contains("贸易商") || clientName.Contains("经销商") ||
                clientName.Contains("代理商") || clientName.Contains("供货商") || clientName.Contains("批发商") ||
                clientIdLower.Contains("merchant") || clientIdLower.Contains("trader") ||
                clientIdLower.Contains("dealer") || clientIdLower.Contains("supplier") ||
                clientIdLower.Contains("wholesaler"))
            { return null; }
            
            // 0.10 派系代表/特殊组织NPC
            if (clientName.Contains("治安部") || clientName.Contains("反抗军") || clientName.Contains("黑市") ||
                clientName.Contains("派系") || clientName.Contains("组织") || clientName.Contains("帮会") ||
                clientIdLower.Contains("faction") || clientIdLower.Contains("guild") ||
                clientIdLower.Contains("syndicate") || clientIdLower.Contains("cartel"))
            { return null; }
            
            // 0.11 任务/委托/悬赏类NPC
            if (clientName.Contains("任务") || clientName.Contains("委托") || clientName.Contains("悬赏") ||
                clientIdLower.Contains("quest") || clientIdLower.Contains("mission") ||
                clientIdLower.Contains("bounty") || clientIdLower.Contains("contract"))
            { return null; }
            
            // 0.12 特殊事件NPC（小偷/可疑顾客/神秘人等）
            if (clientName.Contains("小偷") || clientName.Contains("可疑") || clientName.Contains("神秘") ||
                clientName.Contains("陌生人") || clientName.Contains("流浪汉") || clientName.Contains("乞丐") ||
                clientIdLower.Contains("thief") || clientIdLower.Contains("suspicious") ||
                clientIdLower.Contains("mysterious") || clientIdLower.Contains("stranger") ||
                clientIdLower.Contains("beggar") || clientIdLower.Contains("homeless"))
            { return null; }
            
            // 0.13 特殊identifier前缀（游戏内部标记的特殊NPC）
            if (clientIdLower.StartsWith("story_") || clientIdLower.StartsWith("quest_") ||
                clientIdLower.StartsWith("event_") || clientIdLower.StartsWith("special_") ||
                clientIdLower.StartsWith("unique_") || clientIdLower.StartsWith("boss_"))
            { return null; }

            // 英文模式：对话数组已全量 LangHelper.T 双语，无需提前返回，走完整对话逻辑

            // 1. 特殊客户优先用特殊对话
            bool isSpecial = clientName.Contains("博士") || clientName.Contains("Jackson") ||
                            clientName.Contains("王尔德") || clientName.Contains("Wilde");
            if (isSpecial)
            {
                return SpecialDialogues[Core.Rng.Next(SpecialDialogues.Length)];
            }

            // 1.5 矿工对话已删除（保持原版）
            // 1.55 下层医生对话已删除（保持原版）
            if (clientName.Contains("医生") || clientName.Contains("大夫") || clientName.Contains("诊所") ||
                clientName.Contains("doctor") || clientName.Contains("Doctor") || clientName.Contains("medic") || clientName.Contains("Medic"))
            {
                return LowerDoctorDialogues[Core.Rng.Next(LowerDoctorDialogues.Length)];
            }

                    // 1.58 退休枪匠用专属对话（显示名或ID匹配）
            if (clientName.Contains("退休枪匠") || clientName.Contains("枪匠") ||
                client.identifier.Contains("retired_gunsmith") || client.identifier.Contains("gunsmith"))
            {
                return GunsmithDialogues[Core.Rng.Next(GunsmithDialogues.Length)];
            }
    // 1.6 水商/水贩用专属对话
            if (clientName.Contains("水商") || clientName.Contains("水贩") ||
                client.identifier.Contains("water_merchant"))
            {
                return WaterMerchantDialogues[Core.Rng.Next(WaterMerchantDialogues.Length)];
            }

            // 1.75 下层农场主对话已删除（保持原版）
            // 1.7 酒商/酒贩用专属对话
            if (clientName.Contains("酒商") || clientName.Contains("酒贩") ||
                client.identifier.Contains("winemaker"))
            {
                return AlcoholMerchantDialogues[Core.Rng.Next(AlcoholMerchantDialogues.Length)];
            }

            // 1.8 上层人用专属对话（区分买家和卖家，天龙人风格）
            // 30%概率是破产的上层人（曾经风光现在落魄，还端着架子）
            string factionKey = GetFactionKey(client);

            // 1.85 游客用专属对话（来下层区观光，买纪念品，对什么都好奇）
            if (factionKey == "tourist")
            {
                if (client.clientIntent == StoreClient.ClientIntent.BUY)
                {
                    return TouristBuyerDialogues[Core.Rng.Next(TouristBuyerDialogues.Length)];
                }
                return TouristSellerDialogues[Core.Rng.Next(TouristSellerDialogues.Length)];
            }

            if (factionKey == "upper")
            {
                // 上层药商专属对话（偷药卖，医疗系统腐败，药品管制）
                string clientId = (client.identifier ?? "").ToLowerInvariant();
                bool isPharma = clientName.Contains("药") || clientName.Contains("医药") ||
                                clientId.Contains("pharma") || clientId.Contains("pharm") || clientId.Contains("drug") ||
                                clientId.Contains("medic") || clientId.Contains("chemist") || clientId.Contains("apothecary");
                if (isPharma)
                {
                    if (client.clientIntent == StoreClient.ClientIntent.BUY)
                    {
                        return UpperPharmaBuyerDialogues[Core.Rng.Next(UpperPharmaBuyerDialogues.Length)];
                    }
                    return UpperPharmaSellerDialogues[Core.Rng.Next(UpperPharmaSellerDialogues.Length)];
                }

                // 上层厨师专属对话（偷食材/调料卖，买特殊食材/厨具，美食家气质）
                bool isChef = clientName.Contains("厨") || clientName.Contains("厨师") || clientName.Contains("大厨") ||
                                clientId.Contains("chef") || clientId.Contains("cook") || clientId.Contains("culinary");
                if (isChef)
                {
                    if (client.clientIntent == StoreClient.ClientIntent.BUY)
                    {
                        return UpperChefBuyerDialogues[Core.Rng.Next(UpperChefBuyerDialogues.Length)];
                    }
                    return UpperChefSellerDialogues[Core.Rng.Next(UpperChefSellerDialogues.Length)];
                }

                // 上层代买水商专属对话（卖上层高级水，买下层便宜水，水资源垄断）
                bool isWaterMerchant = clientName.Contains("水商") || clientName.Contains("水贩") || clientName.Contains("代买水") ||
                                clientId.Contains("water") || clientId.Contains("aquatic") || clientId.Contains("hydro");
                if (isWaterMerchant)
                {
                    if (client.clientIntent == StoreClient.ClientIntent.BUY)
                    {
                        return UpperWaterMerchantBuyerDialogues[Core.Rng.Next(UpperWaterMerchantBuyerDialogues.Length)];
                    }
                    return UpperWaterMerchantSellerDialogues[Core.Rng.Next(UpperWaterMerchantSellerDialogues.Length)];
                }

                bool isBankrupt = Core.Rng.Next(100) < 30;
                if (isBankrupt)
                {
                    if (client.clientIntent == StoreClient.ClientIntent.BUY)
                    {
                        return BankruptUpperBuyerDialogues[Core.Rng.Next(BankruptUpperBuyerDialogues.Length)];
                    }
                    return BankruptUpperSellerDialogues[Core.Rng.Next(BankruptUpperSellerDialogues.Length)];
                }
                else
                {
                    if (client.clientIntent == StoreClient.ClientIntent.BUY)
                    {
                        return UpperBuyerDialogues[Core.Rng.Next(UpperBuyerDialogues.Length)];
                    }
                    return UpperSellerDialogues[Core.Rng.Next(UpperSellerDialogues.Length)];
                }
            }

            // 2. 15%概率触发里程碑对话
            string milestoneDialogue = GetMilestoneDialogue();
            if (milestoneDialogue != null && Core.Rng.Next(100) < 15)
            {
                return milestoneDialogue;
            }

            // 3. 45%概率用阵营对话（底层人已删除，返回null时跳过）
            // BUY/SELL 意图必须用对应意图池，避免“买货客户说卖货台词”的混乱
            string[] factionPool = GetFactionIntentPool(factionKey, client.clientIntent);
            if (factionPool != null && Core.Rng.Next(100) < 45)
            {
                return factionPool[Core.Rng.Next(factionPool.Length)];
            }

            // 3.5. 30%概率用玩家状态对话（明确买卖意图时跳过，避免台词与意图不符）
            if (client.clientIntent != StoreClient.ClientIntent.BUY &&
                client.clientIntent != StoreClient.ClientIntent.SELL &&
                Core.Rng.Next(100) < 30)
            {
                string statusDialogue = GetPlayerStatusDialogue();
                if (statusDialogue != null)
                {
                    return statusDialogue;
                }
            }

            // 4. 根据剧情阶段选择对话池（明确买卖意图时跳过，避免混合台词）
            string phase = GetStoryPhase();
            string[] phaseDialogues = null;
            if (phase == "新手期") phaseDialogues = NewbieDialogues;
            else if (phase == "成长期") phaseDialogues = GrowthDialogues;
            else if (phase == "名声期") phaseDialogues = FamousDialogues;

            // 15%概率用剧情阶段对话
            if (client.clientIntent != StoreClient.ClientIntent.BUY &&
                client.clientIntent != StoreClient.ClientIntent.SELL &&
                phaseDialogues != null && Core.Rng.Next(100) < 15)
            {
                return phaseDialogues[Core.Rng.Next(phaseDialogues.Length)];
            }

            // 5. 剩余用意图专属对话（卖/买）或通用对话
            string[] dialoguePool;
            if (client.clientIntent == StoreClient.ClientIntent.SELL)
            {
                dialoguePool = SellerDialogues;
            }
            else if (client.clientIntent == StoreClient.ClientIntent.BUY)
            {
                dialoguePool = BuyerDialogues;
            }
            else
            {
                dialoguePool = GeneralDialogues;
            }

            // 随机选择一条
            string dialogue = dialoguePool[Core.Rng.Next(dialoguePool.Length)];

            // 10%概率在对话中加入客户名字，增加代入感
            if (Core.Rng.Next(10) == 0)
            {
                dialogue = LangHelper.T("我是", "I'm ") + clientName + LangHelper.T("，", ", ") + dialogue;
            }

            return dialogue;
        }
        catch
        {
            return LangHelper.T("看货吧。", "Just take a look.");
        }
    }

    // 矿工专属对话 - 卖家（刚下完矿来卖东西，疲惫麻木，赛博朋克底层风格）
    private static readonly string[] MinerSellerDialogues = {
        LangHelper.T("……老板，收东西不。刚下井，身上还都是灰。这是我从矿里带出来的，你看看。", "...Boss, buying? Just came up from the mine, still covered in dust. Brought this out of the shaft. Take a look."),
        LangHelper.T("咳……咳咳。老板，别嫌我脏，刚从井下上来。这东西是我在矿道深处捡的，你给个价。", "Cough... boss, don't mind the dirt, just came up from the pit. Found this deep in the tunnels. Name a price."),
        LangHelper.T("……累死了。老板，这是我这个月攒的，你收了吧。我得赶紧回去睡，明天还要下井。", "...Exhausted. Boss, this is what I saved up this month. Take it. I need to sleep - back down the shaft tomorrow."),
        LangHelper.T("咳……老板，你看这个。矿上淘汰的，还能用。我偷拿出来的，你别声张。", "Cough... boss, look at this. Retired from the mine, still works. I smuggled it out - keep quiet."),
        LangHelper.T("……老板，急用钱。孩子病了，我得凑医药费。这是我能拿出来的最好的东西了，你看着给。", "...Boss, I need cash fast. My kid's sick, gotta scrape together medicine money. This is the best I can offer. Whatever you think."),
        LangHelper.T("咳……咳咳。老板，这是我从废矿里淘的，差点没出来。你给个实在价，我下次有好货还来。", "Cough... boss, pulled this out of an abandoned mine, almost didn't make it out. Fair price and I'll bring better next time."),
        LangHelper.T("……老板，别问哪来的。矿上的事，你懂的。给个价，合适我就走。", "...Boss, don't ask where it's from. Mine business - you know how it is. Price it, fair and I'm gone."),
        LangHelper.T("累死了……老板，这是我这个月的份。你收了吧，我还得去接孩子放学。", "So tired... boss, this is my monthly quota. Take it - I still need to pick up my kid from school."),
        LangHelper.T("咳……老板，你看这个成色。我在井下待了十二个小时才弄到的。你给个好价，不然我真白干了。", "Cough... boss, look at the quality. Twelve hours underground for this. Pay well, or it was all for nothing."),
        LangHelper.T("……老板，我跟你说，这活不是人干的。但是没办法，得吃饭。这东西你收了吧。", "...Boss, this work isn't for humans. But there's no choice - a man's gotta eat. Take it."),
        LangHelper.T("咳……咳咳。老板，刚上井，脸都没洗。你别嫌弃，这货是好货。你看看值多少。", "Cough... boss, just surfaced, face unwashed. Don't judge - this is good stuff. See what it's worth."),
        LangHelper.T("……老板，急用钱。房租到期了，再不交就要被赶出去了。这是我能拿出来的全部了。", "...Boss, need cash. Rent's due - miss it and I'm out on the street. This is everything I've got."),
        LangHelper.T("累死了……老板，这是我从矿里带出来的。你收了吧，我得赶紧回去，老婆还等着我吃饭呢。", "Exhausted... boss, brought this from the mine. Take it - my wife's waiting for me to eat."),
        LangHelper.T("咳……老板，你看这个。矿上出事了，死了三个人，这是从死人身上拿的。你别忌讳，给个价。", "Cough... boss, look. Accident at the mine - three dead. Took this off a corpse. Don't be squeamish, name a price."),
        LangHelper.T("……老板，我跟你说，这活再干几年我就得尘肺了。但是没办法，得挣钱。这东西你收了吧。", "...Boss, a few more years of this and I'll have black lung. But there's no choice - gotta earn. Take it.")
    };

    // 矿工专属对话 - 买家（刚下完矿来买东西，疲惫麻木，赛博朋克底层风格）
    // 矿工专属对话 - 买家（刚下完矿来买烟酒，疲惫麻木，赛博朋克底层风格，空间站环境）
    private static readonly string[] MinerBuyerDialogues = {
        LangHelper.T("……老板，有烟吗？刚从井下爬上来，肺里全是灰，得抽一根压压。", "...Boss, got smokes? Just crawled up from the pit, lungs full of dust. Need one to settle."),
        LangHelper.T("咳……咳咳。老板，有酒吗？今天矿道又塌了，埋了两个，我得喝点压压惊。", "Cough... boss, got booze? Tunnel collapsed again today, buried two men. Need a drink to steady my nerves."),
        LangHelper.T("……老板，有吃的吗？干了十四个小时，胃里空得能听见回声。", "...Boss, got food? Worked fourteen hours - my stomach's echoing."),
        LangHelper.T("累死了……老板，有止痛药吗？腰疼得直不起来，明天还得下井，不去就扣钱。", "Exhausted... boss, got painkillers? My back won't straighten, but I mine tomorrow anyway - skip a day, lose pay."),
        LangHelper.T("咳……老板，有绷带吗？手上被掘进机划了个口子，血止不住，得包一下。", "Cough... boss, got bandages? The cutter ripped my hand open - can't stop the bleeding, need it wrapped."),
        LangHelper.T("……老板，有便宜的酒吗？发工资了，想喝点好的，但是别太贵，还得留钱交房租。", "...Boss, cheap booze? Payday, want something decent - but nothing pricey, rent's still due."),
        LangHelper.T("咳……咳咳。老板，有烟吗？最好是劲大的那种，淡的抽着没劲，压不住井下的味。", "Cough... boss, smokes? Strong ones if you've got them - light ones can't mask the mine smell."),
        LangHelper.T("……老板，有吃的吗？能放得住的那种，我带回去给孩子吃，他长身体，不能总吃营养膏。", "...Boss, food? Something that keeps - taking it home for my kid. He's growing, can't live on nutrient paste."),
        LangHelper.T("累死了……老板，有什么能解乏的吗？这活真不是人干的，但是没办法，空间站里除了下井没别的活。", "Exhausted... boss, anything to pick me up? This work isn't for humans, but on this station there's nothing but the mines."),
        LangHelper.T("咳……老板，有酒吗？今天差点被埋在井下，我得喝点庆祝一下还活着。明天的事明天再说。", "Cough... boss, got booze? Almost got buried down there today - need a drink to celebrate being alive. Tomorrow's problem for tomorrow."),
        LangHelper.T("……老板，有烟吗？给我来两包，一包自己抽，一包给工头。不给他塞点，他给我派最危险的矿道。", "...Boss, two packs of smokes - one for me, one for the foreman. No bribe, and he assigns me the deadliest tunnels."),
        LangHelper.T("咳……咳咳。老板，有吃的吗？便宜点的，能填饱肚子就行。营养果也行，虽然难吃，但是顶饿。", "Cough... boss, food? Something cheap that fills the belly. Even nutrient fruit - tastes awful but it lasts."),
        LangHelper.T("……老板，有止痛药吗？老毛病了，不吃疼得睡不着。医保不报这个，只能自己买。", "...Boss, painkillers? Old injury - can't sleep without them. Insurance won't cover it, so I buy my own."),
        LangHelper.T("累死了……老板，有酒吗？今天发工资，想喝点，但是别太贵，还得留钱给孩子交学费。", "Exhausted... boss, got booze? Payday, want a drink - nothing fancy, still gotta save for the kid's school fees."),
        LangHelper.T("咳……老板，有烟吗？刚上井，累得要死，得抽一根。这破地方，除了烟和酒，没什么能让人觉得还活着。", "Cough... boss, smokes? Just surfaced, dead tired, need one. In this dump, only smokes and booze remind you you're alive."),
        LangHelper.T("……老板，有酒吗？跟你说个事，今天矿上又死人了，才十九岁，刚来三个月。我得喝点，不然睡不着。", "...Boss, got booze? Something to tell you - another miner died today. Only nineteen, three months on the job. Need a drink or I can't sleep."),
    };

    // 水商/水贩专属对话（卖水的商人，更符合身份）
    internal static readonly string[] WaterMerchantDialogues = {
        LangHelper.T("老板，最近水价又涨了，你这还要货不？", "Boss, water prices went up again. Still buying?"),
        LangHelper.T("哎，老板，跟你说个事。最近供水紧张，我这水可是好不容易弄来的。", "Hey boss, listen. Water supply's tight lately - this was hard to get."),
        LangHelper.T("老板你好啊，我又来了。这次的水质量不错，你尝尝？", "Hello boss, back again. This batch is good quality - have a taste?"),
        LangHelper.T("嗨，老板，最近生意怎么样？我这水卖得可好了。", "Hi boss, how's business? My water sells like crazy."),
        LangHelper.T("老板，跟你商量个事。下次多给你留点好水，你给个实在价？", "Boss, let's make a deal - I'll save you better water next time, you give me a fair price?"),
        LangHelper.T("哎，老板，你这店缺水不？我这有批好水，便宜给你。", "Hey boss, running low on water? I've got a good batch, cheap for you."),
        LangHelper.T("老板，我跟你说，最近安保查得严，我这水可是冒风险弄来的。", "Boss, security's been cracking down - I risked a lot for this water."),
        LangHelper.T("嗨，老板，好久不见啊。最近水价波动大，你这还要不要？", "Hi boss, long time no see. Prices are swinging - still interested?"),
        LangHelper.T("老板，先喝口水，别急。我跟你说，这批水可是上等货。", "Boss, have some water first, no rush. This batch is top tier."),
        LangHelper.T("哎，老板，我这水可是从上层区弄来的，你给个好价？", "Hey boss, this water came from the upper level - pay well?"),
        LangHelper.T("老板，最近缺水缺得厉害，我这货可是抢手货，你要不要？", "Boss, water's scarcer than ever - this is hot merchandise. Want it?"),
        LangHelper.T("嗨，老板，又是我。这次多带了点，你能收多少？", "Hi boss, me again. Brought extra this time - how much can you take?"),
        LangHelper.T("老板，跟你说句实在话，这水我本来想留着自己用的，但是最近手头紧……", "Boss, honest truth - was going to keep this water for myself, but money's tight..."),
        LangHelper.T("哎，老板，你这店要是没水可不行啊，我这刚好有一批。", "Hey boss, a shop without water won't survive - lucky I've got a batch."),
        LangHelper.T("老板，我这水可是经过过滤的，比那些脏水强多了，你看看？", "Boss, this water's filtered - way better than that dirty stuff. Take a look?")
    };

    // 酒商/酒贩专属对话（卖酒的商人，更符合身份）
    internal static readonly string[] AlcoholMerchantDialogues = {
        LangHelper.T("老板，听说你这当铺酿的酒有两下子？我跑了大半个下层区，专门来收你的酒。", "Boss, heard your shop brews something special? Crossed half the lower level just to buy your wine."),
        LangHelper.T("哎，老板，跟你说个事。我收酒多年，你酿的酒在下层区可是小有名气啊。", "Hey boss, let me tell you - I've bought wine for years, and yours has quite a name down here."),
        LangHelper.T("老板你好啊，我又来了。这次带了点好原料，顺便看看你又酿了什么好酒。", "Hello boss, back again. Brought some quality ingredients - curious what you've brewed since."),
        LangHelper.T("嗨，老板，最近生意怎么样？我收的酒都快供不上酒鬼们了，你这有货没？", "Hi boss, business good? I can't keep the drunks supplied - you got stock?"),
        LangHelper.T("老板，跟你商量个事。你酿的酒我看看，价格好说，绝对不让你亏。", "Boss, let's talk. Show me your brew - price is flexible, you won't lose."),
        LangHelper.T("哎，老板，你这店缺酿酒原料不？我这有精选葡萄和特级酵母，便宜给你。", "Hey boss, need brewing supplies? I've got prime grapes and top-grade yeast, cheap for you."),
        LangHelper.T("老板，我跟你说，最近下层区的私酿越来越少了，你这酒留着也是留着，不如卖给我。", "Boss, homebrew's getting scarce down here. Your wine's just sitting there - might as well sell it to me."),
        LangHelper.T("嗨，老板，好久不见啊。最近酒不好收，你酿的酒要是有富余，就匀给我点。", "Hi boss, long time no see. Wine's hard to source lately - if you've got surplus, share some with me."),
        LangHelper.T("老板，先别急，我跟你说。你这酒的品质我懂，绝对给你个公道价。", "Boss, hold on - I know wine quality, and I'll give you a fair price for sure."),
        LangHelper.T("哎，老板，我这收的酒都转手卖给酒鬼们了，你酿的酒要是够好，不愁卖。", "Hey boss, I resell everything to the drunks - brew well and selling won't be a problem."),
        LangHelper.T("老板，最近酒鬼们都馋酒，你酿的有富余就卖给我，省得放着占地方。", "Boss, the drunks are thirsty these days. Sell me your surplus instead of letting it gather dust."),
        LangHelper.T("嗨，老板，又是我。这次带了纯净水和好酵母，你酿的酒呢？拿出来看看。", "Hi boss, me again. Brought pure water and good yeast - where's your brew? Let's see it."),
        LangHelper.T("老板，跟你说句实在话，我收过这么多酒，你酿的算是最对我胃口的。", "Boss, straight talk - of all the wine I've bought, yours suits my taste best."),
        LangHelper.T("哎，老板，你这店要是只卖东西不酿酒可太可惜了，你酿的酒有富余就卖给我。", "Hey boss, a shop like yours that doesn't brew is a waste. Sell me your surplus."),
        LangHelper.T("老板，我这收酒讲究的就是个品质，你酿的酒经过陈酿了吧？拿出来我看看。", "Boss, quality's everything when I buy wine. Yours is aged, right? Let me see it.")
    };

    // 下层农场主卖家专属对话（在下层区种植作物，卖农产品/种子/肥料，朴实接地气，赛博朋克底层麻木风格）
    // 下层农场主卖家专属对话（空间站水培种植，只卖营养果，朴实麻木，赛博朋克底层风格）
    private static readonly string[] FarmerSellerDialogues = {
        LangHelper.T("老板，刚从水培架上摘的营养果，还带着水珠呢。你尝尝，虽然没什么味道，但是顶饿。", "Boss, nutrient fruit fresh off the hydroponic rack, still dewy. Taste it - bland, but it fills you up."),
        LangHelper.T("哎，老板，今年水培的营养果产量低，营养液太贵了。这些是我能拿出来的最好的了。", "Hey boss, low yield this year - nutrient solution's too pricey. This is the best I can offer."),
        LangHelper.T("老板你好，我是下边种植区的。这些营养果是我自己水培的，没打药。难吃是难吃了点，但是维生没问题。", "Hello boss, I'm from the farming district below. These are my own hydroponic fruit, no chemicals. Tastes bad, sure, but it keeps you alive."),
        LangHelper.T("嗨，老板，最近营养液又涨价了，培一架子果成本越来越高。你给个实在价呗？", "Hi boss, nutrient solution keeps rising - rack costs more every season. Fair price, eh?"),
        LangHelper.T("老板，跟你说个事。我这营养果是用过滤水培的，比那些用脏水培的强多了。虽然吃着没什么味道，但是有营养啊。", "Boss, listen - I grow these in filtered water, way better than the dirty-water stuff. Tasteless, but nutritious."),
        LangHelper.T("哎，老板，你这收不收转基因营养果？上面不让种，我们下边偷偷培的，个头大，产量高。难吃，但是顶饿。", "Hey boss, taking GM nutrient fruit? Forbidden up top, so we grow it in secret down here - big, high-yield. Tastes bad, fills you up."),
        LangHelper.T("老板，我跟你说，最近治安部查得严，说我们水培的营养果不合规。我这可是冒风险拿来的。", "Boss, security's cracking down - says our hydroponic fruit doesn't meet regs. I risked a lot bringing this."),
        LangHelper.T("嗨，老板，好久不见啊。最近水培架闹菌，我这是好不容易保住的一点营养果。", "Hi boss, long time no see. Fungus hit the racks - barely saved this little bit."),
        LangHelper.T("老板，先尝尝，别急。我这营养果是自然熟的，比那些催熟的强。虽然吃着没什么味道，但是咱们下层人就靠这个维生。", "Boss, taste it first, no rush. Naturally ripened - better than the force-grown stuff. Tasteless, but this is what keeps us lower-level folks alive."),
        LangHelper.T("哎，老板，你这店缺不缺新鲜营养果？我这每天都能供货，长期合作给你优惠。", "Hey boss, need fresh nutrient fruit? I can supply daily - long-term deal, I'll cut you a discount."),
        LangHelper.T("老板，最近下边的水都被污染了，能培出营养果就不错了。你别嫌它难吃，这可是咱们维生的东西。", "Boss, the water below's contaminated - growing anything is a win. Don't knock the taste; this is our lifeline."),
        LangHelper.T("嗨，老板，又是我。这次带了点稀罕货，上面淘汰下来的品种，我偷偷弄来的苗培的。比普通的强点，但是也就那样。", "Hi boss, me again. Rare stuff this time - a breed the uppers phased out. Snuck the seedlings out and grew them. A bit better than standard, nothing special."),
        LangHelper.T("老板，跟你说句实在话，水培营养果赚不了几个钱，但是我除了培果啥也不会。这玩意上层人看不上，但是咱们下层人离了它活不了。", "Boss, honest truth - nutrient fruit barely pays. But it's all I know. The uppers sneer at it, but we can't live without it."),
        LangHelper.T("哎，老板，你这要是收的话，我下次给你带点蘑菇，种植区角落里长的，纯天然。比这营养果好吃多了，但是没这玩意顶饿。", "Hey boss, if you're buying, next time I'll bring mushrooms - wild from the farming district corners, all natural. Tastier than this, but not as filling."),
        LangHelper.T("老板，我这营养果可是早上刚摘的，皮上还带着霜呢。你摸摸，还凉着呢。别看它不起眼，这可是好东西，难吃但是能维生啊。", "Boss, picked these this morning - still frosted. Feel them, still cold. Don't judge by looks - good stuff, ugly but life-sustaining."),
        LangHelper.T("……老板，跟你说个事。我家那口子病了，诊所的药太贵。我把这茬营养果都拿来了，你给个实在价，我得凑药钱。", "...Boss, something to tell you. My other half's sick, clinic meds are robbery. Brought the whole harvest - fair price, I need medicine money."),
        LangHelper.T("老板，你知道吗？上层区的人连营养果是什么都不知道。他们吃的都是合成食品，比这好吃一百倍。但是咱们下层人，能吃上新鲜营养果就不错了。", "Boss, you know the uppers don't even know what nutrient fruit is? They eat synth food, a hundred times better. Down here, fresh fruit is already a blessing."),
    };

    // 下层农场主买家专属对话（买工具/种子/肥料/水，朴实接地气）
    // 下层农场主买家专属对话（只收种子，空间站水培种植，朴实麻木）
    private static readonly string[] FarmerBuyerDialogues = {
        LangHelper.T("老板，你这有没有好点的种子？我那批种子出芽率太低了，三粒才出一粒。", "Boss, got better seeds? My batch barely germinates - one in three."),
        LangHelper.T("哎，老板，你这有没有上层区下来的种子？贵点也行，产量高，出芽率也高。", "Hey boss, got seeds from the upper level? Pricier is fine - higher yield, better germination."),
        LangHelper.T("老板你好，我是下边种植区的。你这营养果种子怎么卖？我想多整点，扩一架子。", "Hello boss, farming district here. How much for nutrient fruit seeds? Want to expand a rack."),
        LangHelper.T("嗨，老板，最近种子价格涨得厉害，你这有没有便宜点的？我要得多，给个批发价。", "Hi boss, seed prices are climbing. Got anything cheaper? Buying bulk - wholesale me."),
        LangHelper.T("老板，跟你说个事。我那老品种产量太低了，你这有没有新品种的种子？", "Boss, listen - my old strain's yield is terrible. Got any new varieties?"),
        LangHelper.T("哎，老板，你这有没有转基因种子？上层区不让卖，你这要是有我全要了。产量高，个头大。", "Hey boss, got GM seeds? Forbidden up top - if you have them, I'll take everything. High yield, big fruit."),
        LangHelper.T("老板，我跟你说，去年的种子出芽率只有三成，你这能不能保证出芽率？不能保证我不敢买。", "Boss, last year's seeds only hit thirty percent. Can you guarantee germination? If not, I'm not buying."),
        LangHelper.T("嗨，老板，好久不见啊。我那水培架想扩种，你这有没有多点的营养果种子？", "Hi boss, long time no see. Expanding my racks - got plenty of nutrient fruit seeds?"),
        LangHelper.T("老板，先看看，别急。我想买点耐旱的种子，下层区浇水太费劲了，能省点是点。", "Boss, take your time. Want drought-resistant seeds - watering down here's a chore, every drop counts."),
        LangHelper.T("哎，老板，你这有没有抗虫的种子？种植区闹虫灾，普通种子根本活不了，全被啃了。", "Hey boss, pest-resistant seeds? Bug plague in the district - normal seeds get eaten alive."),
        LangHelper.T("老板，最近想整点新品种，你这有没有上层区淘汰下来的种子？便宜点就行，我试试。", "Boss, looking for new varieties - got any phased-out seeds from the upper level? Cheap's fine, I'll experiment."),
        LangHelper.T("嗨，老板，又是我。这次想买点蔬菜种子，光培营养果不行，得换着种，不然土壤都退化了。", "Hi boss, me again. Want vegetable seeds - can't grow only nutrient fruit, the soil's degrading."),
        LangHelper.T("老板，跟你说句实在话，种子是种地的根本，我宁愿多花点钱买好种子，也不买便宜货浪费架子。", "Boss, straight talk - seeds are everything to a farmer. I'd rather pay more for good ones than waste rack space on junk."),
        LangHelper.T("哎，老板，你这有没有育苗盘？种子直接撒基质里出芽率太低了，用育苗盘能高点。", "Hey boss, got seedling trays? Direct sowing germinates poorly - trays help a lot."),
        LangHelper.T("老板，我想买点水培架的配件，我那架子坏了几个，想修修。你这有没有？", "Boss, need hydroponic rack parts - a few of mine broke and I want to fix them. Got any?"),
        LangHelper.T("……老板，跟你说个事。我家那口子病了，但是种植架不能停，停了就没收入了。我想买点好种子，这茬要是能丰收，药钱就有着落了。", "...Boss, listen. My other half's sick, but the racks can't stop - no harvest, no income. Want good seeds; a good crop means medicine money."),
        LangHelper.T("老板，你知道吗？上层区的人吃的都是合成食品，连种子是什么都不知道。但是咱们下层人，种子就是命根子，有种子就有饭吃。", "Boss, the uppers eat synth food - they don't even know what a seed is. Down here, seeds are life itself: seeds mean food."),
    };

    // 全局对话修改（所有玩家都有，不需要选蛙哥牛逼特性）
    // 只修改对话，不处理物品添加
    internal static void GlobalModifyDialogue(StoreClient client, string clientName)
    {
        try
        {
            // 只跳过有剧情的特殊NPC（博士/王尔德/退休枪匠），保留它们的剧情对话
            // 普通商人（水商/水贩/酒商/酒贩）给更符合身份的对话
            // 跳过有剧情的特殊NPC（博士/王尔德/鉴定委员会），保留它们的剧情对话
            if (clientName.Contains("博士") || clientName.Contains("Jackson") ||
                clientName.Contains("王尔德") || clientName.Contains("Wilde") ||
                clientName.Contains("鉴定") || clientName.Contains("Appraisal") || clientName.Contains("appraisal") ||
                clientName.Contains("奥丁") || clientName.Contains("Odin") || clientName.Contains("哈拉尔德森") || clientName.Contains("Haraldsson"))
            {
                return;
            }

            // 生成对话（使用和蛙哥牛逼相同的对话库，但概率稍微调整，更自然）
            string newDialogueText = GenerateGlobalDialogue(client, clientName);
            if (newDialogueText == null)
            {
                return;
            }
            // 空文本防护（Bug5修复）：文本为空或只有空白时跳过，避免对话开头空白
            if (string.IsNullOrWhiteSpace(newDialogueText))
            {
                return;
            }
            // 说话人名字去空白，避免UI开头空白行（Bug5修复）
            string speakerName = (clientName ?? "").Trim();

            if (client.mainDialogue != null)
            {
                client.mainDialogue.SetText(speakerName, newDialogueText);
            }
            else
            {
                Dialogue newDialogue = new Dialogue();
                newDialogue.SetText(speakerName, newDialogueText);
                client.mainDialogue = newDialogue;
            }

            // 刷新UI，确保对话修改立即生效
            try
            {
                if (NegociationUIManager.Instance != null)
                {
                    NegociationUIManager.Instance.DispatchUpdateForCurrentIntent();
                }
            }
            catch (Exception ex)
            {
                Core.LogMsg("[全局对话] 刷新NegociationUIManager失败: " + ex.Message);
            }

            // 反射查找并调用所有可能的UI刷新方法
            try
            {
                var uiType = typeof(NegociationUIManager);
                var allMethods = uiType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var refreshCandidates = allMethods
                    .Where(m => (m.Name.Contains("Refresh") || m.Name.Contains("UpdateUI") ||
                                m.Name.Contains("Reload") || m.Name.Contains("Redraw") ||
                                m.Name.Contains("UpdateDialogue") || m.Name.Contains("ShowDialogue")) &&
                               m.GetParameters().Length == 0)
                    .ToList();

                foreach (var m in refreshCandidates)
                {
                    try
                    {
                        m.Invoke(NegociationUIManager.Instance, null);
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                Core.LogMsg("[全局对话] 反射刷新UI失败: " + ex.Message);
            }
        }
        catch (Exception ex)
        {
            Core.LogMsg("[全局对话] 修改对话失败: " + ex.Message);
        }
    }

    // 全局对话生成（概率调整：60%阵营对话，20%卖家/买家对话，20%通用对话）
    // 全局对话生成（根据NPC交易物品类别和访问次数生成对话）
    private static string GenerateGlobalDialogue(StoreClient client, string clientName)
    {
        try
        {
            // 记录NPC访问次数，判断是否是第一次来
            string clientId = client.identifier ?? clientName;
            bool isFirstTime = RecordClientVisit(clientId);
            int visitCount = GetClientVisitCount(clientId);

            // 获取NPC交易物品的主要类别
            string itemCategory = GetClientMainItemCategory(client);


            // ============================================================
            // 根据物品类别判断NPC身份（优先于名字判断）
            // ============================================================

            // 矿石矿工对话已删除（保持原版）
            // 水矿工/水商（区分买家和卖家）
            if (itemCategory == "水")
            {
                if (client.clientIntent == StoreClient.ClientIntent.BUY)
                {
                    // 买水的用上层代买水商买家对话
                    return UpperWaterMerchantBuyerDialogues[Core.Rng.Next(UpperWaterMerchantBuyerDialogues.Length)];
                }
                // 卖水的用水商卖家对话
                return WaterMerchantDialogues[Core.Rng.Next(WaterMerchantDialogues.Length)];
            }
            // 化学家（买卖化学药品）
            if (itemCategory == "化学药品")
            {
                if (client.clientIntent == StoreClient.ClientIntent.BUY)
                {
                    return UpperPharmaBuyerDialogues[Core.Rng.Next(UpperPharmaBuyerDialogues.Length)];
                }
                return UpperPharmaSellerDialogues[Core.Rng.Next(UpperPharmaSellerDialogues.Length)];
            }

            // ============================================================
            // 按NPC名字判断（保留原有逻辑）
            // ============================================================

            // 矿工对话已删除（保持原版）
            // 下层医生对话已删除（保持原版）
            if (clientName.Contains("医生") || clientName.Contains("大夫") || clientName.Contains("诊所") ||
                clientName.Contains("doctor") || clientName.Contains("Doctor") || clientName.Contains("medic") || clientName.Contains("Medic"))
            {
                return LowerDoctorDialogues[Core.Rng.Next(LowerDoctorDialogues.Length)];
            }

                    // 退休枪匠用专属对话（显示名或ID匹配）
            if (clientName.Contains("退休枪匠") || clientName.Contains("枪匠") ||
                clientId.Contains("retired_gunsmith") || clientId.Contains("gunsmith"))
            {
                return GunsmithDialogues[Core.Rng.Next(GunsmithDialogues.Length)];
            }
    // 水商/水贩用专属对话
            if (clientName.Contains("水商") || clientName.Contains("水贩") ||
                clientId.Contains("water_merchant"))
            {
                return WaterMerchantDialogues[Core.Rng.Next(WaterMerchantDialogues.Length)];
            }

            // 酒商/酒贩用专属对话
            if (clientName.Contains("酒商") || clientName.Contains("酒贩") ||
                clientId.Contains("winemaker"))
            {
                return AlcoholMerchantDialogues[Core.Rng.Next(AlcoholMerchantDialogues.Length)];
            }

            // 上层人用专属对话（区分买家和卖家，天龙人风格）
            string factionKey = GetFactionKey(client);

            // 游客用专属对话
            if (factionKey == "tourist")
            {
                if (client.clientIntent == StoreClient.ClientIntent.BUY)
                {
                    return TouristBuyerDialogues[Core.Rng.Next(TouristBuyerDialogues.Length)];
                }
                return TouristSellerDialogues[Core.Rng.Next(TouristSellerDialogues.Length)];
            }

            if (factionKey == "upper")
            {
                bool isBankrupt = Core.Rng.Next(100) < 30;
                if (isBankrupt)
                {
                    if (client.clientIntent == StoreClient.ClientIntent.BUY)
                    {
                        return BankruptUpperBuyerDialogues[Core.Rng.Next(BankruptUpperBuyerDialogues.Length)];
                    }
                    return BankruptUpperSellerDialogues[Core.Rng.Next(BankruptUpperSellerDialogues.Length)];
                }
                else
                {
                    if (client.clientIntent == StoreClient.ClientIntent.BUY)
                    {
                        return UpperBuyerDialogues[Core.Rng.Next(UpperBuyerDialogues.Length)];
                    }
                    return UpperSellerDialogues[Core.Rng.Next(UpperSellerDialogues.Length)];
                }
            }

            // 60%概率用阵营对话（底层人已删除，返回null时跳过）
            string[] factionPool = GetFactionDialoguePool(factionKey);
            if (factionPool != null && Core.Rng.Next(100) < 60)
            {
                return factionPool[Core.Rng.Next(factionPool.Length)];
            }

            // 20%概率用卖家/买家专属对话（动态：说要买什么就说什么，按客户实际交易物品生成）
            if (client.clientIntent == StoreClient.ClientIntent.SELL ||
                client.clientIntent == StoreClient.ClientIntent.SELLNBUY)
            {
                return BuildDynamicTradeLine(client);
            }
            if (client.clientIntent == StoreClient.ClientIntent.BUY)
            {
                return BuildDynamicTradeLine(client);
            }

            // 20%概率用通用对话
            return GeneralDialogues[Core.Rng.Next(GeneralDialogues.Length)];
        }
        catch
        {
            return null;
        }
    }

    // ============================================================
    // 动态买卖对话（说要买什么就说什么，按客户实际交易物品类别生成）
    // 避免写死商品名（如"书包"）造成出戏
    // ============================================================
    private static string GetCategoryDialogueName(string cat)
    {
        switch (cat)
        {
            case "矿石": return LangHelper.T("矿石", "ore");
            case "水": return LangHelper.T("净水", "water");
            case "化学药品": return LangHelper.T("药品和化学原料", "meds and chemicals");
            case "武器": return LangHelper.T("枪械和弹药", "guns and ammo");
            case "食品": return LangHelper.T("食物", "food");
            case "电子模组": return LangHelper.T("电子设备和模组", "electronics and modules");
            case "种子农业": return LangHelper.T("种子和农资", "seeds and farm supplies");
            case "酒精饮料": return LangHelper.T("酒水", "drinks");
            default: return LangHelper.T("货", "goods");
        }
    }

    private static string BuildDynamicTradeLine(StoreClient client)
    {
        try
        {
            string cat = GetClientMainItemCategory(client);
            string name = GetCategoryDialogueName(cat);
            bool isBuy = client.clientIntent == StoreClient.ClientIntent.BUY;

            if (isBuy)
            {
                string[] buys = new string[] {
                    LangHelper.T("我想买点" + name + "，你这有吗？", "I'd like to buy some " + name + ". Got any?"),
                    LangHelper.T("你这有" + name + "吗？我要收一些。", "Got " + name + "? I want to stock up."),
                    LangHelper.T("我需要" + name + "，价格合适我就都要。", "I need " + name + " - if the price is right I'll take it all."),
                    LangHelper.T("有" + name + "吗？我这次要的量不小。", "Got " + name + "? I need a fair amount this time."),
                    LangHelper.T("专门来买" + name + "的，你给个公道价。", "Came all the way to buy " + name + " - give me a fair price.")
                };
                return buys[Core.Rng.Next(buys.Length)];
            }
            else
            {
                string[] sells = new string[] {
                    LangHelper.T("这批" + name + "你看看，收不收？", "Take a look at this " + name + " - buying?"),
                    LangHelper.T("我带了点" + name + "来，你给个实在价。", "Brought you some " + name + " - give me a fair price."),
                    LangHelper.T("这" + name + "是我好不容易弄来的，你收不收？", "This " + name + " was hard to come by - taking it?"),
                    LangHelper.T("有点" + name + "要出手，你看看值多少。", "Got some " + name + " to offload - see what it's worth."),
                    LangHelper.T("老规矩，这批" + name + "你收了吧。", "Usual deal - take this " + name + " off my hands.")
                };
                return sells[Core.Rng.Next(sells.Length)];
            }
        }
        catch
        {
            return null;
        }
    }

    internal static void ModifyFirstDialogue(StoreClient client, string name)
    {
        try
        {
            
            // 对 BUY 客户设置购买需求标签（游戏 UI 显示客户想买什么，玩家不用看对话就懂）
            if (client.clientIntent == StoreClient.ClientIntent.BUY)
            {
                EnsureBuyTagsForClient(client);
            }
            
            // 生成全新的故事性对话
            string newDialogueText = GenerateStoryDialogue(client, name);

            // 返回null表示跳过（特殊NPC/剧情角色保留原对话，不覆盖）
            if (newDialogueText == null)
            {
                return;
            }

            // 空文本防护（Bug5修复）：文本为空或只有空白时跳过，避免对话开头空白
            if (string.IsNullOrWhiteSpace(newDialogueText))
            {
                Core.LogMsg("[蛙哥牛逼] 生成的对话为空，跳过: client=" + name);
                return;
            }


            // 说话人名字去空白，避免UI开头空白行（Bug5修复）
            string speakerName = (name ?? "").Trim();

            if (client.mainDialogue != null)
            {
                // 完全替换原对话，给玩家全新体验
                client.mainDialogue.SetText(speakerName, newDialogueText);
            }
            else
            {
                Dialogue newDialogue = new Dialogue();
                newDialogue.SetText(speakerName, newDialogueText);
                client.mainDialogue = newDialogue;
            }

            // 刷新UI，确保对话修改立即生效
            try
            {
                // 方式1：刷新NegociationUIManager
                if (NegociationUIManager.Instance != null)
                {
                    NegociationUIManager.Instance.DispatchUpdateForCurrentIntent();
                }
            }
            catch (Exception ex)
            {
                Core.LogMsg("[蛙哥牛逼] 刷新NegociationUIManager失败: " + ex.Message);
            }

            // 方式2：反射查找并调用所有可能的UI刷新方法
            try
            {
                var uiType = typeof(NegociationUIManager);
                var allMethods = uiType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var refreshCandidates = allMethods
                    .Where(m => (m.Name.Contains("Refresh") || m.Name.Contains("UpdateUI") ||
                                m.Name.Contains("Reload") || m.Name.Contains("Redraw") ||
                                m.Name.Contains("UpdateDialogue") || m.Name.Contains("ShowDialogue")) &&
                               m.GetParameters().Length == 0)
                    .ToList();

                foreach (var m in refreshCandidates)
                {
                    try
                    {
                        m.Invoke(NegociationUIManager.Instance, null);
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                Core.LogMsg("[蛙哥牛逼] 反射刷新UI失败: " + ex.Message);
            }
        }
        catch
        { }
    }

    // 卖家：添加随机售卖物品到柜台（完整版：3-5个随机物品，智能推荐）
    internal static void AddRandomItemsToCounter(StoreClient client)
    {
        try
        {
            // 09-14 用户拍板：普通版取消"精选好货"赠送（硬爽版保留）
            if (!BuildConfig.HardMode) return;
            PlayerStore instance = PlayerStore.Instance;
            if (instance == null)
            {
                Core.LogMsg("[蛙哥牛逼] PlayerStore.Instance为null");
                return;
            }


            // 先清空柜台
            try { 
                instance.RemoveNotOwnedItemFromTable();
            } catch (Exception ex) {
                Core.LogMsg("[蛙哥牛逼] 清空柜台失败: " + ex.Message);
            }

            // 一个人卖一件物品（根据玩家现有资金过滤，确保能批发下来）
            int itemCount = 1;
            int added = 0;
            List<string> addedItems = new List<string>();
            // 蛙哥推荐物品累计总预算 = 玩家现金50%（给客户自带物品留空间，确保总价可批发）
            long remainingBudget = (long)(GetPlayerCash() * 0.5);
            if (remainingBudget < 50) remainingBudget = 50;

            for (int i = 0; i < itemCount && added < itemCount; i++)
            {
                // 使用按预算智能推荐选择物品（确保玩家买得起，累计不超过剩余预算）
                string itemId = SmartRandomItemByBudget(remainingBudget);

                // 容器：可开箱箱子用 CreateLootCrate 创建带原版内容；建筑模块（storage_bay等）跳过
                ItemCategory cat = GetItemCategory(itemId);
                if (cat == ItemCategory.Container && !IsLootableContainer(itemId))
                {
                    continue;
                }

                // 避免重复添加同一个物品
                if (addedItems.Contains(itemId))
                {
                    continue;
                }

                try
                {
                    if (!DirectoryMaster.Has<GameItem>(itemId))
                    {
                        continue;
                    }

                    GameItem item = null;
                    if (cat == ItemCategory.Container)
                    {
                        item = CreateLootCrate(itemId);
                    }
                    else
                    {
                        item = DirectoryMaster.Item(itemId, true);
                    }
                    if (item == null)
                    {
                        Core.LogMsg("[蛙哥牛逼] 创建物品失败: " + itemId);
                        continue;
                    }

                    // 清除赃物标签（避免客户卖的东西都是赃物）
                    try { item.DisableTag("stolen", true); } catch { }
                    try { item.DisableTag("TAG_STOLEN", true); } catch { }

                    // 独立实例售卖：DirectoryMaster.Item(identifier,true)已返回工厂新实例，不再CloneLinked——
                    // CloneLinked已Obsolete且克隆品丢失兑换回调委托（food_stamp双击兑换依赖+0x130/+0x138/+0x140回调）
                    // 直接用item，回调完整，食品卡兑换正常；isOwend=false正常收费；isStolen=false无盗窃
                    GameItem sellItem = item;
                    // 蛙哥送好货：isOwend=true 视为已拥有（免费拿走），并清除未拥有标签双保险（价值/品种不变）
                    try { sellItem.DisableTag("not_purchased", true); } catch { }
                    try { sellItem.DisableTag("TAG_NOT_PURCHASED", true); } catch { }
                    // 免费送=玩家已拥有：加 IS_OWNED_TAG（food_stamp等"使用类"物品双击兑换依赖此标签，正常购买流程会自动加，免费送绕过了购买）
                    try { sellItem.EnableTag("IS_OWNED_TAG", true); } catch { }
                    // 添加到柜台（isOwend=true：免费送）
                    instance.AddDirectSellingItemToTable(sellItem, true, false, false, 100);
                    // 双保险：AddDirectSellingItemToTable 内部可能对某些物品重新加 not_purchased 标签，调用后再强制清除一次
                    try { sellItem.DisableTag("not_purchased", true); } catch { }
                    try { sellItem.DisableTag("TAG_NOT_PURCHASED", true); } catch { }
                    try { sellItem.EnableTag("IS_OWNED_TAG", true); } catch { }
                    try { } catch { }
                    added++;
                    addedItems.Add(itemId);
                    // 扣减预算（保证蛙哥推荐物品累计总价可批发）
                    long wholesale = GetItemWholesalePrice(itemId);
                    if (wholesale > 0 && wholesale <= remainingBudget) remainingBudget -= wholesale;
                }
                catch (Exception ex)
                {
                    Core.LogMsg("[蛙哥牛逼] 添加" + itemId + "失败: " + ex.Message);
                }
            }

            // 如果一个都没加上，保底加1个raw_meat
            if (added == 0)
            {
                try
                {
                    GameItem meat = DirectoryMaster.Item("raw_meat", true);
                    if (meat != null)
                    {
                        try { meat.DisableTag("stolen", true); } catch { }
                        // 同上：不再CloneLinked（Obsolete+丢回调），直接用工厂产物
                        GameItem sellMeat = meat;
                        // 保底物品也免费送：清除未拥有标签 + isOwend=true
                        try { sellMeat.DisableTag("not_purchased", true); } catch { }
                        try { sellMeat.EnableTag("IS_OWNED_TAG", true); } catch { }
                        try { sellMeat.DisableTag("TAG_NOT_PURCHASED", true); } catch { }
                        instance.AddDirectSellingItemToTable(sellMeat, true, false, false, 100);
                        // 双保险：调用后再强制清除一次 not_purchased 标签
                        try { sellMeat.DisableTag("not_purchased", true); } catch { }
                        try { sellMeat.DisableTag("TAG_NOT_PURCHASED", true); } catch { }
                        added++;
                    }
                }
                catch (Exception ex)
                {
                    Core.LogMsg("[蛙哥牛逼] 保底添加失败: " + ex.Message);
                }
            }

        }
        catch
        { }
    }
}
