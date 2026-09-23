using System;
using HarmonyLib;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Il2Cpp;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace JacksonPerks;
internal static partial class RobinCrusoePerk
{

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
        catch { }
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
        catch { }
    }

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
        catch { }
    }

    public static void PostfixCreateItemTooltip(RichTextBuilder builder, GameItem item)
    {
        try
        {
            if (!IsActive() || builder == null || item == null) return;
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
            else if (IsMachine(item) || item.IsTag("MODULE_TAG"))
            {
                // 升级提示（用户拍板：显示在机器上储存区/机器箱子/模板）
                int pct = GetTagIntSafe(item, "wageUpgradePct");
                int effv = GetTagIntSafe(item, "wageUpgradeEff");
                if (pct > 0 || effv > 0)
                    builder.AddLine(LangHelper.T("◆ 升级：性能+" + pct + "% 效率+" + effv + "%（拖 metal_ingot +1%/次）", "◆ Upgrade: Perf +" + pct + "% Eff +" + effv + "% (drag metal_ingot +1%/each)"),
                        true, (RenderHandler.ColorPalette)(-1), false, false, false, false, (RenderHandler.ColorPalette)(-1), (RenderHandler.ColorPalette)(-1), (RenderHandler.ColorPalette)(-1));
                else
                    builder.AddLine(LangHelper.T("◆ 升级：拖 metal_ingot 到机器/模板 +1%/次（性能/效率/质量）", "◆ Upgrade: drag metal_ingot to machine/template +1%/each (Perf/Eff/Quality)"),
                        true, (RenderHandler.ColorPalette)(-1), false, false, false, false, (RenderHandler.ColorPalette)(-1), (RenderHandler.ColorPalette)(-1), (RenderHandler.ColorPalette)(-1));
                // 吞噬叠加记录（吞噬季：该模组吸收的属性累计——仅模组有 CANNIBALISM_* tag，机器读到 0 不显示）
                int cp = GetTagIntSafe(item, "CANNIBALISM_PERFORMANCE_INT");
                int ce = GetTagIntSafe(item, "CANNIBALISM_EFFICIENCY_INT");
                int cq = GetTagIntSafe(item, "CANNIBALISM_QUALITY_INT");
                int cv = GetTagIntSafe(item, "CANNIBALISM_VALUE");
                if (cp > 0 || ce > 0 || cq > 0 || cv > 0)
                    builder.AddLine(LangHelper.T("◆ 吞噬叠加：性能+" + cp + "% 效率+" + ce + "% 质量+" + cq + "% 价值+" + cv, "◆ Devoured: Perf +" + cp + "% Eff +" + ce + "% Qual +" + cq + "% Value +" + cv),
                        true, (RenderHandler.ColorPalette)(-1), false, false, false, false, (RenderHandler.ColorPalette)(-1), (RenderHandler.ColorPalette)(-1), (RenderHandler.ColorPalette)(-1));
            }
            else if (ContainerUpgradeV2.IsUpgradeableContainer(item))
            {
                int stage = ContainerUpgradeV2.GetTagIntSafe(item, "wb_stage");
                if (stage >= ContainerUpgradeV2.MAX_STAGE)
                    builder.AddLine(LangHelper.T("◆ 储存区：满级（容量×2）· 拖 junk 可正常放入", "◆ Storage: MAX (2× capacity) · drag junk to store"),
                        true, (RenderHandler.ColorPalette)(-1), false, false, false, false, (RenderHandler.ColorPalette)(-1), (RenderHandler.ColorPalette)(-1), (RenderHandler.ColorPalette)(-1));
                else
                    {
                        int progress = ContainerUpgradeV2.GetTagIntSafe(item, "wb_progress");
                        int need = ContainerUpgradeV2.UPGRADE_COSTS[Math.Min(stage, ContainerUpgradeV2.MAX_STAGE - 1)];
                        builder.AddLine(LangHelper.T("◆ 储存区：段位 " + stage + "/" + ContainerUpgradeV2.MAX_STAGE + " · 升级进度 " + progress + "/" + need + "（拖 junk 升级）", "◆ Storage: Stage " + stage + "/" + ContainerUpgradeV2.MAX_STAGE + " · progress " + progress + "/" + need + " (drag junk to upgrade)"),
                            true, (RenderHandler.ColorPalette)(-1), false, false, false, false, (RenderHandler.ColorPalette)(-1), (RenderHandler.ColorPalette)(-1), (RenderHandler.ColorPalette)(-1));
                    }
            }
            // 09-13 清洁系统 v1：日用品面板显示"双击恢复清洁"
            else if (IsDailyNeed(item))
            {
                string id = (item.identifier ?? "").ToLowerInvariant();
                if (DAILY_NEED_CLEAN.TryGetValue(id, out int _gain))
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
        catch { }
    }

}
