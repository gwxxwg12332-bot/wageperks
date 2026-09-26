using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace JacksonPerks;

public static partial class WageGirlSystem
{
    // ===== 偷拿核心 =====


    // 日常自主偷拿：饥饿/口渴/心情差自拿对应类恢复；心情好随机顺走（件数/价值按好感）
    private static void TrySnatch()
    {
        try
        {
            // 09-22 优化：好感≥N → 100% 不偷（害羞档位；CFG：WageGirlStealNoStealAff）
            if (GetAffection() >= BuildConfig.WageGirlStealNoStealAff) return;
            int sat = GetStat(K_SAT), th = GetStat(K_TH), mood = GetStat(K_MOOD);
            string mode = null; string msg = null;
            if (sat < 30) { mode = "food"; msg = "蛙娘饿坏了，偷吃了你的食物"; }
            else if (th < 30) { mode = "drink"; msg = "蛙娘渴坏了，偷喝了你的饮品"; }
            else if (mood < 30) { if (GetStat(K_CLEAN) >= 100) return; mode = "care"; msg = "蛙娘心情很差，拿走了你的日用品"; }
            else if (mood >= 60) { return; } // 09-22 砍：心情好不偷（回归只带回来给玩家）
            else return;
            int aff = GetAffection();
            int count = aff < 30 ? 1 : (aff < 70 ? 2 : 3);
            string valueMode = "random"; // 09-22 改：不按价值偷，随机挑
            int stolen = StealItems(mode, count, valueMode, out var stolenNames);
            if (stolen > 0)
            {
                SetSleepDebt(GetSleepDebt() + BuildConfig.WageGirlSnatchSleepDebt); // 偷拿熬夜 -N 睡眠（次日结算；CFG）
                if (mode == "food") SetStat(K_SAT, GetStat(K_SAT) + 30);
                else if (mode == "drink") SetStat(K_TH, GetStat(K_TH) + 30);
                else if (mode == "care") { SetStat(K_MOOD, GetStat(K_MOOD) + 20); SetStat(K_CLEAN, GetStat(K_CLEAN) + 10); }
                string sn = (stolenNames != null && stolenNames.Count > 0) ? "：" + string.Join("、", stolenNames) : "";
                ReportLine(LangHelper.T(msg + sn + "（" + stolen + " 件）", msg + " (" + (stolenNames != null ? string.Join(", ", stolenNames) : "") + ", " + stolen + " items)"));
            }
        }
        catch (System.Exception ex) { Core.LogMsg("[WageGirlSystem.StealAI_Fence] 异常: " + ex.Message); }
    }

    // 真饮品判定：水量 > 0 或有卡路里值才算喝得到东西（空瓶/无记录的饮品不算）
    private static bool HasDrinkContent(GameItem it)
    {
        try { if (it == null) return false; } catch { return false; }
        try { if (RobinCrusoePerk.GetWaterMl(it) > 0) return true; } catch { }
        try { if (RobinCrusoePerk.GetCalorie(it) > 0) return true; } catch { }
        return false;
    }

    // 从店里找目标偷拿：mode 限定类别；count 件数；valueMode highest/random/low
    private static int StealItems(string mode, int count, string valueMode, out System.Collections.Generic.List<string> stolenNames)
    {
        stolenNames = new System.Collections.Generic.List<string>();
        try
        {
            EmporiumEntry em = EmporiumEntry.Instance;
            if (em == null) return 0;
            var invs = new GameInventory[] {
                (GameInventory)em.frontInvinvElement,
                (GameInventory)em.showcaseElement,
                (GameInventory)em.invElement
            };
            var candidates = new System.Collections.Generic.List<GameItem>();
            foreach (var inv in invs)
            {
                if (inv == null || inv.childItems == null) continue;
                for (int i = 0; i < inv.childItems.Count; i++)
                {
                    var it = inv.childItems[i];
                    if (it == null) continue;
                    if (it.identifier == ENTITY_ID) continue;

                    // 09-22 白名单制：只偷三个白名单内的物品（不靠排除词）
                    bool allow = false;
                    if (mode == "food") allow = RobinCrusoePerk.IsFood(it);
                    // 09-23 修复「没有饮品的时候蛙娘依然会偷喝」：DRINK_IDS 含 empty_beer_bottle
                    // 这类空容器、以及部分饮品无水量/无卡路里记录 → 会被 IsDrink 判成饮品而"偷喝"，
                    // 实际一口都喝不到。加"真饮品"校验：店里没有能喝的东西 → 候选为空 → 不偷不报。
                    else if (mode == "drink") allow = RobinCrusoePerk.IsDrink(it) && HasDrinkContent(it);
                    else if (mode == "care") allow = RobinCrusoePerk.IsDailyNeed(it);
                    else allow = RobinCrusoePerk.IsFood(it) || RobinCrusoePerk.IsDrink(it) || RobinCrusoePerk.IsDailyNeed(it);
                    if (!allow) continue;
                    // 偷拿价值上限：好感<30 偷≤Low，30-70 偷≤Mid，>70 偷≤High（CFG：WageGirlSnatchValue*）
                    int aff = GetAffection();
                    int maxVal = aff < 30 ? BuildConfig.WageGirlSnatchValueLow : (aff < 70 ? BuildConfig.WageGirlSnatchValueMid : BuildConfig.WageGirlSnatchValueHigh);
                    if (it.unitValue > maxVal) continue;
                    candidates.Add(it);
                }
            }
            if (candidates.Count == 0) return 0;
            var picked = new System.Collections.Generic.List<GameItem>();
            if (valueMode == "highest")
            {
                GameItem best = candidates[0];
                foreach (var c in candidates) if (c.unitValue > best.unitValue) best = c;
                picked.Add(best);
            }
            else if (valueMode == "low")
            {
                var pool = new System.Collections.Generic.List<GameItem>(candidates);
                pool.Sort((a, b) => a.unitValue.CompareTo(b.unitValue));
                for (int i = 0; i < Math.Min(count, pool.Count); i++) picked.Add(pool[i]);
            }
            else
            {
                var pool = new System.Collections.Generic.List<GameItem>(candidates);
                for (int i = 0; i < Math.Min(count, pool.Count); i++)
                {
                    int idx = Core.Rng.Next(pool.Count);
                    picked.Add(pool[idx]);
                    pool.RemoveAt(idx);
                }
            }
            int stolen = 0; long stolenVal = 0;
            foreach (var p in picked)
            {
                try { stolenVal += p.unitValue; } catch { }
                try { p.Destroy(); stolen++; } catch { try { if (p.parentInventory != null) { p.parentInventory.Expel(p); stolen++; } } catch { } }
            }
            try { SetStat("stolenValue", GetStat("stolenValue", 0) + (int)Math.Min(stolenVal, int.MaxValue)); } catch { }
            return stolen;
        }
        catch { stolenNames = null; return 0; }
    }
}
