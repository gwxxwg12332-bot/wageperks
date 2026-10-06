using System;
using HarmonyLib;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace WageSurvival;

// ============================================================
// 10-06 P1 拆分缺口修复：阶段C 边界内 4 挂点（WP RobinCrusoePerk.Food.Supply/Tooltip 复制，裁剪 WP 专属段）
// ① AutoSipFromContainer Prefix（自动喝水水质 5 档）
// ② CreateFoodItemTooltip P/Post（满卡虚高 tooltip——09-18 修复）
// ③ _CreateFeedDispenser_b__3 Prefix（饲料机满卡算）
// ④ CreateItemTooltip Postfix 食物/日用品/双击使用类（鼠王/机器/容器升级段=WP 专属已裁）
// 适配：IsActive→WS 全局 true；GetWaterMl/GetCalorie/GetFoodQuality→SurvivalHelpers；六维→SurvivalState（同 partial）
// ============================================================
internal static partial class SurvivalFood
{
    // 已食用判定：剩余卡路里 < 满量（WP Food.Attributes.cs:64 复制）
    internal static bool IsEaten(GameItem item)
    {
        if (item == null || !IsFood(item)) return false;
        try
        {
            if (!item.IsTag(CAL_LEFT_TAG)) return false;
            return GetCalLeft(item) < GetCalorie(item);
        }
        catch { return false; }
    }

    // ===== ① 自动喝水按质生效（09-11 用户拍板：与双击同 5 档；return false 接管原版"补口渴+减水"）=====
    public static bool PrefixAutoSipFromContainer(GameItem item, GameCharacterItem GCI)
    {
        try
        {
            if (!IsActive() || item == null || GCI == null) return true;
            int ml = GetWaterMl(item);
            if (ml <= 0) return true;
            int purity = -1;
            try { purity = Il2Cpp.WaterHelper.GetWaterPurity(item); } catch { }
            int tier = purity >= 9900 ? 0 : purity >= 9600 ? 1 : purity >= 9200 ? 2 : purity >= 8800 ? 3 : 4;
            int hd  = new[] { 5, 2, 0, -5, -10 }[tier];
            int inf = new[] { 0, 0, 5, 15, 30 }[tier];
            int cg  = new[] { 5, 4, 3, 1, 0 }[tier];
            if (hd != 0) SetHealth(Math.Max(0, Math.Min(100, GetHealth() + hd)));
            if (cg > 0) SetClean(Math.Min(100, GetClean() + cg));
            if (inf > 0) TryInfect(inf / 100.0);
            // 补原版 AutoSipFromContainer（return false 后原版不执行）：sip = min(ml, 缺口渴量)；GCI.thirst += sip；Remove(item, sip)
            try
            {
                int cur = 0, mx = 0;
                try { cur = (int)GCI.currentThirst; } catch { }
                try { mx = (int)GCI.maxThirst; } catch { }
                int need = Math.Max(0, mx - cur);
                int sip = Math.Min(ml, need);
                if (sip > 0)
                {
                    try { GCI.currentThirst = cur + sip; } catch { }
                    try { Il2Cpp.WaterHelper.Remove(item, sip * 1000); } catch { } // 09-11 定案：µl 单位
                }
            }
            catch (System.Exception ex) { Core.LogMsg("[WageSurvival] AutoSip 异常: " + ex.Message); }
            return false;
        }
        catch { return true; }
    }

    // ===== ② FoodItemHelper.CreateFoodItemTooltip Prefix/Postfix：tooltip 前临时改 CALORIE_VALUE_TAG 为剩余值，Postfix 恢复 =====
    public static void PrefixFoodTooltip(GameItem item)
    {
        try
        {
            _foodCalBackupValid = false;
            if (item == null || !IsActive()) return;
            bool eaten = false; try { eaten = item.IsTag(CAL_LEFT_TAG); } catch { }
            if (!eaten) return;
            int full = 0; try { full = GetCalorie(item); } catch { }
            int left = GetCalLeft(item);
            _foodCalBackup = full; _foodCalBackupValid = true;
            try { System.Action<TagState> sysAct = delegate (TagState state) { state.SetInt(left); }; var il2cppAct = DelegateSupport.ConvertDelegate<Il2CppSystem.Action<TagState>>((System.Delegate)sysAct); item.ModifyTag("CALORIE_VALUE_TAG", il2cppAct, false); } catch { }
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival] FoodTooltip 异常: " + ex.Message); }
    }

    public static void PostfixFoodTooltip(GameItem item)
    {
        try
        {
            if (_foodCalBackupValid && item != null)
            {
                try { System.Action<TagState> sysAct = delegate (TagState state) { state.SetInt(_foodCalBackup); }; var il2cppAct = DelegateSupport.ConvertDelegate<Il2CppSystem.Action<TagState>>((System.Delegate)sysAct); item.ModifyTag("CALORIE_VALUE_TAG", il2cppAct, false); } catch { }
                _foodCalBackupValid = false;
            }
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival] FoodTooltip 异常: " + ex.Message); }
    }

    // ===== ③ 饲料机：搅拌转化前把吃过的食物 CALORIE_VALUE_TAG 改为剩余卡（09-18 拆包实锤 b__3 原样按剩余转）=====
    public static void PrefixFeedDispenserB3(Il2Cpp.MachineFeedDispenser.__c__DisplayClass7_0 __instance)
    {
        try
        {
            if (__instance == null || !IsActive()) return;
            var grid = __instance.storageGrid;
            if (grid == null || grid.childItems == null) return;
            foreach (var m in grid.childItems)
            {
                if (m == null) continue;
                bool eaten = false; try { eaten = m.IsTag(CAL_LEFT_TAG); } catch { }
                if (!eaten) continue;
                int left = GetCalLeft(m);
                try { System.Action<TagState> sysAct = delegate (TagState state) { state.SetInt(left); }; var il2cppAct = DelegateSupport.ConvertDelegate<Il2CppSystem.Action<TagState>>((System.Delegate)sysAct); m.ModifyTag("CALORIE_VALUE_TAG", il2cppAct, false); } catch { }
            }
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival] 饲料机 异常: " + ex.Message); }
    }

    // ===== ④ CreateItemTooltip Postfix：食物品质/双击使用类显示（鼠王/机器/容器升级=WP 专属已裁）=====
    public static void PostfixCreateItemTooltip(RichTextBuilder builder, GameItem item)
    {
        try
        {
            if (!IsActive() || builder == null || item == null) return;
            string itemId = (item.identifier ?? "").ToLowerInvariant();
            if (!IsFood(item) && !IsDailyNeed(item) && !IsBeverage(item) && !IsSnack(item) && !(IsAlc(item)) && !IsNarcotic(item)) return;
            if (IsFood(item))
            {
                int q = GetFoodQuality(item);
                if (q < 0) q = 0;
                string[] names = { LangHelper.T("新鲜", "Fresh"), LangHelper.T("正常", "Normal"), LangHelper.T("变质", "Spoiled"), LangHelper.T("腐烂", "Rotten") };
                int calLeft = GetCalLeft(item);
                int calFull = GetCalorie(item);
                string eaten = IsEaten(item) ? LangHelper.T("（已食用）", " (Eaten)") : "";
                builder.AddLine(LangHelper.T("品质：", "Quality: ") + names[Math.Min(3, Math.Max(0, q))] + eaten + LangHelper.T("（剩余 " + calLeft + "/" + calFull + " 卡）", " (" + calLeft + "/" + calFull + " kcal left)"),
                    true, (RenderHandler.ColorPalette)(-1), false, false, false, false, (RenderHandler.ColorPalette)(-1), (RenderHandler.ColorPalette)(-1), (RenderHandler.ColorPalette)(-1));
                string priceNote = "";
                if (q >= 3) priceNote = LangHelper.T("售价：-100%（腐烂）", "Sell: -100% (Rotten)");
                else if (q == 2) priceNote = LangHelper.T("售价：-90%（变质）", "Sell: -90% (Spoiled)");
                else if (IsEaten(item)) priceNote = LangHelper.T("售价：-80%（已食用）", "Sell: -80% (Eaten)");
                else if (q <= 0) priceNote = LangHelper.T("售价：+30%（新鲜）", "Sell: +30% (Fresh)");
                if (priceNote.Length > 0)
                    builder.AddLine(priceNote,
                        true, (RenderHandler.ColorPalette)(-1), false, false, false, false, (RenderHandler.ColorPalette)(-1), (RenderHandler.ColorPalette)(-1), (RenderHandler.ColorPalette)(-1));
                if (q == 2 || q >= 3)
                    builder.AddLine(LangHelper.T(q >= 3 ? "食用：40% 患病风险（卡路里按20%恢复）" : "食用：10% 患病风险（卡路里按50%恢复）", q >= 3 ? "Eating: 40% illness risk (calories at 20%)" : "Eating: 10% illness risk (calories at 50%)"),
                        true, (RenderHandler.ColorPalette)(-1), false, false, false, false, (RenderHandler.ColorPalette)(-1), (RenderHandler.ColorPalette)(-1), (RenderHandler.ColorPalette)(-1));
            }
            // 09-13 清洁系统 v1：日用品面板显示"双击恢复清洁"
            else if (IsDailyNeed(item))
            {
                if (DAILY_NEED_CLEAN.TryGetValue(itemId, out int _gain))
                    builder.AddLine(LangHelper.T("双击使用：清洁 +" + _gain, "Double-click: Cleanliness +" + _gain),
                        true, (RenderHandler.ColorPalette)(-1), false, false, false, false, (RenderHandler.ColorPalette)(-1), (RenderHandler.ColorPalette)(-1), (RenderHandler.ColorPalette)(-1));
            }
            // 09-13 双击使用类效果面板显示：饮品/零食/酒/麻醉品
            else if (IsBeverage(item))
                builder.AddLine(LangHelper.T("双击使用：饱食+10 口渴+15（消耗1件）", "Double-click: Satiety +10 Thirst +15 (consumed)"),
                    true, (RenderHandler.ColorPalette)(-1), false, false, false, false, (RenderHandler.ColorPalette)(-1), (RenderHandler.ColorPalette)(-1), (RenderHandler.ColorPalette)(-1));
            else if (IsSnack(item))
                builder.AddLine(LangHelper.T("双击使用：饱食 + 心情+10（消耗1件）", "Double-click: Satiety + Mood +10 (consumed)"),
                    true, (RenderHandler.ColorPalette)(-1), false, false, false, false, (RenderHandler.ColorPalette)(-1), (RenderHandler.ColorPalette)(-1), (RenderHandler.ColorPalette)(-1));
            else if (IsAlc(item) && !IsEmptyBottle(item))
                builder.AddLine(LangHelper.T("双击饮用：心情+15（消耗1件）", "Double-click drink: Mood +15 (consumed)"),
                    true, (RenderHandler.ColorPalette)(-1), false, false, false, false, (RenderHandler.ColorPalette)(-1), (RenderHandler.ColorPalette)(-1), (RenderHandler.ColorPalette)(-1));
            else if (IsNarcotic(item))
                builder.AddLine(LangHelper.T("双击使用：心情+20（消耗1件）", "Double-click: Mood +20 (consumed)"),
                    true, (RenderHandler.ColorPalette)(-1), false, false, false, false, (RenderHandler.ColorPalette)(-1), (RenderHandler.ColorPalette)(-1), (RenderHandler.ColorPalette)(-1));
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival] FoodTooltip 异常: " + ex.Message); }
    }
}
