using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace JacksonPerks;

public static partial class WageGirlSystem
{
    public static bool PrefixMayTarget(GameItem __instance, GameItem targetItem, ref bool __result)
    {
        try
        {
            if (__instance == null || targetItem == null) return true;
            if (IsGirl(targetItem) && CanFeed(__instance) && IsPlayerOwnedForCare(__instance)) { __result = true; return false; } // hover 可拖（只允许玩家自己的物品）
        }
        catch (System.Exception ex) { Core.LogMsg("[WageGirlSystem.Interact] 异常: " + ex.Message); }
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
            if (__instance == null || targetItem == null) return true;
            if (!IsGirl(targetItem)) return true;
            if (!IsDragRelease()) return true;
            if (!IsPlayerOwnedForCare(__instance)) return true; // 货架商品不能喂
            if (TryFeed(__instance, targetItem)) return false; // 喂食成功：拦截原生放入
        }
        catch (System.Exception ex) { Core.LogMsg("[WageGirlSystem.Interact] 异常: " + ex.Message); }
        return true;
    }
    private static bool IsDragRelease()
    {
        try { var h = Il2Cpp.ItemMouseDragHandler.current; return h != null && h.IsDraggingItem; } catch { return false; }
    }
    private static bool IsGirl(GameItem it)
    {
        // 09-26 修：identifier 读档丢失兜底（拆包实锤见 Anim.cs L452 同款 TAG 兜底）
        try { return it != null && (it.identifier == ENTITY_ID || it.IsTag(TAG)); } catch { return false; }
    }
    private static bool CanFeed(GameItem item)
    {
        try { return RobinCrusoePerk.IsFood(item) || RobinCrusoePerk.IsDrink(item) || RobinCrusoePerk.IsDailyNeed(item) || IsContraband(item); } catch { return false; }
    }
    private static bool TryFeed(GameItem item, GameItem girl)
    {
        try
        {
            if (item == null) return false;

            // 09-23 修复「蛙娘只有在不营业时才可被照顾」：旧代码在交易 UI 打开时一律 return false，
            // 而营业期间柜台接客几乎全程开交易 UI → 照顾（喂食/喝水/清洁）实际只在打烊后可用。
            // 改为：违禁品分支保持"交易中禁止"（与原行为一致），照顾分支放行（见下方归属校验）。
            // 09-23 改：违禁品 → 按模式分流（洗白 / 销赃）
            if (IsContraband(item))
            {
                if (Patches.CurrentUITradeMode != 0) return false; // 交易中不洗白/不销赃（保持原行为）
                int mode = GetStat(K_WASH_MODE);
                if (mode == 0) {
                    // 洗白模式
                    int lvl = 0;
                    try { lvl = Il2Cpp.ContrabandHelper.GetContrabandLevel(item); } catch { lvl = 1; }
                    if (lvl <= 0) lvl = 1;
                    int washCost = lvl * 50;
                    int savings = GetStat(K_SAVINGS);
                    if (savings < washCost) {
                        ReportLine(LangHelper.T("蛙娘：小金库余额不足（洗白需要 " + washCost + " 块）", "Wage Girl: not enough savings (need " + washCost + ")"));
                        try { Il2Cpp.StoreUIManager.Instance.Notify(LangHelper.T("小金库余额不足", "Not enough savings"), "orange"); } catch { }
                        return false;
                    }
                    SetStat(K_SAVINGS, savings - washCost);
                    try { Il2Cpp.ContrabandHelper.RemoveContrabandStatus(item); try { item.EnableTag("wage_washed", true); } catch { } } catch { }
                    try { item.shortDescription = (item.shortDescription ?? "") + LangHelper.T("【被蛙哥的大手洗白】", "[Laundered by Wage's big hand]"); } catch { }
                    string itemName = ModCannibalism.GetName(item);
                    ReportLine(LangHelper.T("蛙娘把 " + itemName + " 洗白了（L" + lvl + " -" + washCost + " 块小金库）", "Wage Girl laundered " + itemName + " (L" + lvl + " -" + washCost + " savings)"));
                    try { if (Il2Cpp.CustomUIManager.Instance != null && Il2Cpp.CustomUIManager.Instance.IsOpen("wage_girl_panel")) ShowPanel(); } catch { }
                    SetAnimMode(2);
                    return true;
                } else {
                    // 销赃模式（累计待销赃）
                    long v = 0;
                    try { v = (int)item.GetCurrentValue(); } catch { }
                    if (v <= 0) { try { v = item.unitValue; } catch { } }
                    if (v <= 0) return false;
                    bool _destroyed = false;
                    try { item.Destroy(); _destroyed = true; } catch (Exception _exD) { Core.LogMsg("[蛙娘销赃] Destroy失败: " + _exD.Message); try { item.parentInventory?.Expel(item); _destroyed = true; } catch (Exception _exE) { Core.LogMsg("[蛙娘销赃] Expel也失败: " + _exE.Message); } }
                    Core.LogMsg("[蛙娘销赃] 吃掉 " + (item.identifier ?? "?") + " v=" + v + " destroyed=" + _destroyed);
                    int cur = GetStat(K_FENCE_AMT);
                    int total = cur + (int)v;
                    SetStat(K_FENCE_AMT, total);
                    ReportLine(LangHelper.T("蛙娘吃下了违禁品（累计 " + total + " 价值待销赃——点面板「销赃」出发）", "Wage Girl devoured contraband (" + total + " to fence - press Fence)"));
                    try { if (Il2Cpp.CustomUIManager.Instance != null && Il2Cpp.CustomUIManager.Instance.IsOpen("wage_girl_panel")) ShowPanel(); } catch { }
                    SetAnimMode(2);
                    return true;
                }
            }
            // 09-23：营业期间允许照顾，但只许使用玩家自己的物品——
            // 依据 [L1] GeneralHelper.IsItemOwned(item) ≡ item.IsTag("IS_OWNED_TAG")（原版归属标记，Patches.cs:1984 已用它判买卖方向）。
            // 买入模式下拖的是客户的货，喂掉会让原生交易 UI 持有已销毁物品 → 拦掉。
            if (Patches.CurrentUITradeMode != 0 && !IsPlayerOwnedForCare(item)) return false;
            int gain = 0; int aff = 1; string msg = "";
            int curAff = GetAffection();
            // 分阶段好感获取：初期(0-30)+1~2，中期(30-70)+2~3，后期(70-100)+1
            int affBase = curAff < 30 ? 1 : (curAff < 70 ? 2 : 1);
            if (RobinCrusoePerk.IsDailyNeed(item)) { gain = 20; aff = curAff < 30 ? 2 : (curAff < 70 ? 4 : 2); msg = LangHelper.T("蛙娘洗得干干净净、心情大好！清洁 +20 心情 +10（照顾）", "Wage Girl cleaned up & cheered up! Cleanliness +20 Mood +10 (care)"); SetStat(K_CLEAN, GetStat(K_CLEAN) + gain); SetStat(K_MOOD, GetStat(K_MOOD) + 10); SetStat(K_HEALTH, GetStat(K_HEALTH) + 15); try { item.Destroy(); } catch { } } // 09-26 删 return true：走公共收尾（好感/提示/面板刷新一次补齐）
            else if (RobinCrusoePerk.IsFood(item)) {
                // 普通食物：GetCalLeft → bite=min(100,(cal+1)/2) → gain=round(bite/22)
                int cal2 = RobinCrusoePerk.GetCalLeft(item);
                if (cal2 <= 0) { item.Destroy(); return true; }
                int bite = cal2 <= 100 ? cal2 : Math.Max(100, (cal2 + 1) / 2);
                int left = cal2 - bite;
                gain = Math.Max(1, (int)Math.Round(bite / 22f));
                SetStat(K_SAT, Math.Min(100, GetStat(K_SAT) + gain));
                SetStat(K_HEALTH, Math.Max(0, GetStat(K_HEALTH) + 15));
                aff = affBase;
                msg = LangHelper.T("蛙娘吃饱了！饱食 +" + gain, "Wage Girl ate! Satiety +" + gain);
                // 吃完才 Destroy，剩→SetCalLeft+EATEN_TAG
                if (left <= 0) { item.Destroy(); }
                else { RobinCrusoePerk.SetCalLeft(item, left); try { item.EnableTag("EATEN_TAG", true); } catch { } }
            }
            else if (RobinCrusoePerk.IsDrink(item)) {
                // 水：GetWaterMl → sip=min(200,ml) → purity 5 档
                int ml = RobinCrusoePerk.GetWaterMl(item);
                string _id = ""; try { _id = item.identifier; } catch { }
                Core.LogMsg("[蛙娘喂水] id=" + _id + " ml=" + ml); // 诊断日志，用完删
                if (ml <= 0) {
                    // 09-26 修：ml<=0 时只有酒能喂，其他（空瓶/空水瓶）不能喂
                    string id = "";
                    try { id = item.identifier; } catch { }
                    bool isAlcohol = id == "red_beer" || id == "nudka" || id == "galaxy_blend" || id == "whiskey" || id == "vodka";
                    if (!isAlcohol) return false; // 空瓶/空水瓶：不能喂

                    // 酒 → 按价值回口渴，喝完销毁酒瓶
                    long dval = 0;
                    try { dval = item.GetCurrentValue(); } catch { }
                    if (dval <= 0) { try { dval = item.unitValue; } catch { } }
                    gain = dval >= 300 ? 25 : dval >= 150 ? 18 : dval >= 60 ? 12 : dval >= 20 ? 6 : 2;
                    SetStat(K_TH, Math.Min(100, GetStat(K_TH) + gain));
                    aff = affBase;
                    msg = LangHelper.T("蛙娘喝了一杯！口渴 +" + gain + "（按价值 " + dval + "）", "Wage Girl had a drink! Thirst +" + gain + " (value " + dval + ")");
                    try { item.Destroy(); } catch { } // 酒喝完销毁酒瓶
                }
                else {
                    int sip = Math.Min(200, ml);
                    int purity = -1; try { purity = Il2Cpp.WaterHelper.GetWaterPurity(item); } catch { }
                    int tier = purity >= 9900 ? 0 : purity >= 9600 ? 1 : purity >= 9200 ? 2 : purity >= 8800 ? 3 : 4;
                    gain = new[] { 25, 18, 12, 6, 2 }[tier];
                    int hd = new[] { 5, 2, 0, -5, -10 }[tier];
                    SetStat(K_TH, Math.Min(100, GetStat(K_TH) + gain));
                    if (hd != 0) SetStat(K_HEALTH, Math.Max(0, Math.Min(100, GetStat(K_HEALTH) + hd)));
                    aff = affBase;
                    string wname = new[] { "优质", "较好", "普通", "浑浊", "脏水" }[tier];
                    msg = LangHelper.T("蛙娘喝饱了！口渴 +" + gain + "（" + wname + "）", "Wage Girl drank! Thirst +" + gain + " (" + wname + ")");
                    // 不 Destroy，瓶子留（带剩余水）
                    try { WaterHelper.Remove(item, sip * 1000); } catch { }
                }
            }
            else return false;
            SetAffection(GetAffection() + aff);
            SetStat("lastFedDay", CurrentDay()); // 记录今天喂过
            try { StoreUIManager.Instance.Notify(msg); } catch { }
            try { if (Il2Cpp.CustomUIManager.Instance != null && Il2Cpp.CustomUIManager.Instance.IsOpen("wage_girl_panel")) ShowPanel(); } catch { }
            SetAnimMode(2); // 09-22 吃掉瞬间切偷动画（播完回待机）
            return true;
        }
        catch (Exception ex) { Core.LogMsg("[蛙娘] 喂食异常: " + ex.Message); return false; }
    }
    private static bool IsPlayerOwnedForCare(GameItem item)
    {
        try { if (item == null) return false; } catch { return false; }
        try { return Il2Cpp.GeneralHelper.IsItemOwned(item); } catch { }
        try { return item.IsTag("IS_OWNED_TAG"); } catch { }
        return false;
    }
    private static float _lastDoubleClickLog = 0;
    public static void PostfixDoubleClickAction(GameItem newItem, Vector2 mousePosition)
    {
        // 09-24 双击粘滞节流：1 秒内只打一次日志（原生 DoubleClickAction 可能每帧调多次）
        if (UnityEngine.Time.time - _lastDoubleClickLog > 1f)
        {
            _lastDoubleClickLog = UnityEngine.Time.time;
            Core.LogMsg("[双击] WageGirl Postfix 触发: " + (newItem != null ? (newItem.name + " id=" + newItem.identifier) : "null"));
        }
        try
        {
            if (newItem == null) return;
            if (!IsGirl(newItem)) return; // 09-26 修：identifier 读档丢失兜底（同 IsGirl TAG 兜底）
            ShowPanel();
        }
        catch (System.Exception ex) { Core.LogMsg("[WageGirlSystem.Interact] 异常: " + ex.Message); }
    }
}
