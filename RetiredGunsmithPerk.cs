using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace JacksonPerks;

// 退休枪匠之友特性
// 解锁退休枪匠NPC，每周来一次，卖枪械模组和弹药
internal sealed class RetiredGunsmithPerk : CustomStartingPerk
{
    internal const string PerkId = "退休枪匠之友";

    internal override string Id => PerkId;
    internal override string DisplayName => LangHelper.T("退休枪匠之友", "Gunsmith Friend");
    internal override string Description => LangHelper.T("一位退休的老枪匠听闻你的名声，决定每周来你这拜访一次。他会带来手枪、冲锋枪、霰弹枪等武器，以及各类弹药，都是市面上难得一见的好货。选择此特性，退休枪匠每周到访一次，售卖枪械和弹药。", "A retired gunsmith heard of your reputation and decided to visit you once a week. He brings pistols, SMGs, shotguns and various ammunition - rare goods on the market. Selecting this perk makes the retired gunsmith visit weekly to sell guns and ammo.");
    internal override int Cost => 2;
    internal override int Type => 0;

    // 上次枪匠来访的天数
    private static int _lastVisitDay = -1;
    private static int VISIT_INTERVAL => BuildConfig.GunsmithVisitInterval; // 来访间隔（CFG 可调）

    // 枪匠售卖的物品
    internal override void OnNewGame()
    {
        _lastVisitDay = -1;
    }

    internal static bool IsActive()
    {
        return Core.PerkActive(PerkId);
    }

    // 检查是否应该让枪匠今天来
    internal static bool ShouldVisitToday(int currentDay)
    {
        // 每周第5天（星期5）到访，与其他特殊NPC错开
        return (currentDay % 7) == 5;
    }

    // 记录枪匠来访
    internal static void RecordVisit(int currentDay)
    {
        _lastVisitDay = currentDay;
    }

    // 生成枪匠的售卖物品
    internal static List<string> GenerateItems()
    {
        return MerchantHelper.GenerateCardLockPairs();
    }

    // ============================================================
    // printer 芯片源头拦截（拆包 2.5.42）：
    // 原版退休枪匠固定货/概率货含 printer_chip_* 系列 + printer_module_metal，
    // 走 PlayerStore.AddDirectSellingItemToTable 放柜台（与水商 evaporator 同入口）。
    // 玩家反馈这些芯片显示为空物品（无图标/无名称渲染）。
    // → 源头拦截：枪匠激活时，printer 芯片类不进柜台；保留实装配件（mag_*/laser2 等）不受误伤。
    // ============================================================
    public static bool PrefixAddDirectSellingItemToTable(GameItem gameItem)
    {
        try
        {
            if (!IsActive()) return true;
            if (gameItem == null) return true;
            string id = gameItem.identifier;
            if (string.IsNullOrEmpty(id)) return true;
            if (id.StartsWith("printer_chip_") || id == "printer_module_metal") return false;
        }
        catch { }
        return true;
    }

    // ============================================================
    // 枪匠专属售卖：手枪/冲锋枪/霰弹枪 + 弹药 + 配件 + 枪支许可
    // 在 HandleSpecialNpcArrived（OnNextClientArrived 时机）调用
    // ============================================================
    internal static void AddSpecialSellItems()
    {
        try
        {
            PlayerStore instance = PlayerStore.Instance;
            if (instance == null) { Core.LogMsg("[枪匠] PlayerStore.Instance为null，无法添加售卖"); return; }

            string[] sellItems = {
                // 武器
                "handmade_pistol", "heavy_handmade_pistol", "smg", "shotgun", "revolver",
                // 弹药
                "small_pistol_ammo", "small_pistol_ammo_p", "small_pistol_ammo_nl", "heavy_pistol_ammo", "heavy_pistol_ammo_p", "heavy_pistol_ammo_nl",
            };

            int added = 0;
            foreach (string wid in sellItems)
            {
                try
                {
                    GameItem w = DirectoryMaster.Item(wid, true);
                    if (w == null) { Core.LogMsg("[枪匠] 加 " + wid + " 不存在(null)"); continue; }
                    // 空名物品补中文名
                    if (string.IsNullOrEmpty(w.name))
                    {
                        string fb = GetFallbackName(wid);
                        if (fb != null) { try { Il2Cpp.GeneralHelper.SetCustomName(w, fb);  } catch { } }
                    }
                    // 用通用方法添加到柜台（清标签+克隆+添加+再清标签）
                    GameItem sellW = MerchantHelper.AddItemToCounter(w, 100, false);
                    if (sellW != null) { added++;  }
                }
                catch (Exception ex) { Core.LogMsg("[枪匠] 加 " + wid + " 失败: " + ex.Message); }
            }
        }
        catch (Exception ex)
        {
            Core.LogMsg("[枪匠] AddSpecialSellItems 失败: " + ex.Message + "\n" + ex.StackTrace);
        }
    }

    // 空名物品补中文名
    private static string GetFallbackName(string id)
    {
        switch (id)
        {
            default: return null;
        }
    }
}
