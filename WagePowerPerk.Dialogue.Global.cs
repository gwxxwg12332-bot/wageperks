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
                    catch (System.Exception ex) { Core.LogMsg("[WagePowerPerk.Dialogue.Global] 异常: " + ex.Message); }
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
}
