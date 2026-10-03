using MelonLoader;
using System;
using Il2Cpp;

namespace WageSurvival
{
    // 特性成长：每50天+1点+1槽，跨档累计（全局生效）
    internal static class SurvivalGrowthHelper
    {
        private static bool _appliedThisRun = false;

        public static void OnDayStart()
        {
            try
            {
                int total = SaveStore.GetInt("growthTotalDays", 0);
                total++;
                SaveStore.SetInt("growthTotalDays", total);

                int bonusDay = total / 50;
                int applied = SaveStore.GetInt("growthApplied", 0);

                // 现金正数才记
                int cash = 0;
                try { cash = PlayerStore.Instance.playerCash; } catch { }
                Core.LogMsg($"[特性成长] total={total} bonusDay={bonusDay} applied={applied} cash={cash}");

                if (cash > 0 && bonusDay > applied)
                {
                    SaveStore.SetInt("growthApplied", bonusDay);
                    Core.LogMsg($"[特性成长] 攒满50天! applied={bonusDay} total={total}");
                    try { StoreUIManager.Instance.Notify($"🎉 特性成长里程碑：累计{total}天，下次新开局自动 +{bonusDay}点 +{bonusDay}槽", "green"); } catch { }
                }
            }
            catch (Exception ex) { Core.LogMsg("[特性成长] OnDayStart异常: " + ex.Message); }
        }

        public static void ApplyOnPerkUiOpen(Il2Cpp.PerkUIController __instance)
        {
            if (_appliedThisRun) return;
            try
            {
                int bonusDay = SaveStore.GetInt("growthApplied", 0);
                Core.LogMsg($"[特性成长] Apply被调 bonusDay={bonusDay}");
                if (bonusDay > 0)
                {
                    __instance.maxPerkPoint += bonusDay;
                    __instance.maxPerkCount += bonusDay;
                    Core.LogMsg($"[特性成长] 开局应用 +{bonusDay}点 +{bonusDay}槽");
                }
                _appliedThisRun = true;
            }
            catch (Exception ex) { Core.LogMsg("[特性成长] Apply异常: " + ex.Message); }
        }
    }
}
