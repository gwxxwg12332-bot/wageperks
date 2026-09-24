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
{
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
    // 09-24 修（卡死根因）：删除同步循环跳天（EndDay/EndNight/OnDayEnd/BeginDay ×3）——
    // UI 按钮事件栈内同步重入原生日切状态机 3 轮，每轮再触发全部 OnDayStart Postfix + 双重结算（原生链+L82 显式）
    // → 实测卡死。昏迷语义改为：blood_rest=3 由正常每日结算自然递减（禁出门/禁采血 IsForcedRest 即时生效），
    // 不再一键跳过 3 天；恢复期结束强制回血安全线（TickBloodRest），虚弱永续循环同步根除。
    private static void ForceComaSkip()
    {
        try
        {
            var ps = Il2Cpp.PlayerStore.Instance;
            if (ps == null) return;
            _gameOverTriggered = false;
            try { Il2Cpp.StoreUIManager.Instance.CloseAllUI(); } catch { }
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
            if (WageSaveStore.GetInt(PERK_ID, "robinson_hard", 0) == 1) return; // 困难模式：无救助（濒饿直接 GameOver）
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
            if (WageSaveStore.GetInt(PERK_ID, "robinson_hard", 0) == 1) return; // 困难模式：无救助（濒渴直接 GameOver）
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
            if (WageSaveStore.GetInt(PERK_ID, "robinson_hard", 0) == 1) return; // 困难模式：无救助（病危直接 GameOver）
            GiveToBackpack("bandage_item", 1);
            try { StoreUIManager.Instance.Notify(LangHelper.T("好心客户送来了药品，快用上", "A kind customer sent medicine — use it now"), "green"); } catch { }
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] RescueMedicine 异常: " + ex.Message); }
    }

    // 救场：好心客户送食（1-2 份，不删档，lowDays 归零）
    private static void Rescue()
    {
        try
        {
            int n = UnityEngine.Random.value < 0.5f ? 1 : 2;
            GiveToBackpack("processed_meat", n);
            GivePureWaterToBackpack(1);
            WageSaveStore.SetInt(PERK_ID, "lowDays", 0);
            try { StoreUIManager.Instance.Notify(LangHelper.T("一位好心顾客送来了口粮和水……", "A kind customer brought rations and water..."), "green"); } catch { }
        }
        catch (System.Exception ex) { Core.LogMsg("[RobinCrusoePerk.Death] 异常: " + ex.Message); }
    }
}
