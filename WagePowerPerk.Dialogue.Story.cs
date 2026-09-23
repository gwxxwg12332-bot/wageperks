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
}
