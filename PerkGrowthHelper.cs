using MelonLoader;
using System;

namespace JacksonPerks
{
    // 特性成长：每50天+1点+1槽，跨档累计
    public static class PerkGrowthHelper
    {
        private static bool _appliedThisRun = false;

        public static void OnDayStart()
        {
            try
            {
                int total = MelonPreferences.GetEntryValue<int>("WagesPerks", "PerkGrowthTotalDays");
                total++;
                MelonPreferences.SetEntryValue("WagesPerks", "PerkGrowthTotalDays", total);

                int bonusDay = total / 50;
                int applied = MelonPreferences.GetEntryValue<int>("WagesPerks", "PerkGrowthApplied");

                // 现金正数才记
                int cash = PlayerStore.Instance != null ? PlayerStore.Instance.playerCash : 0;
                if (cash > 0 && bonusDay > applied)
                {
                    MelonPreferences.SetEntryValue("WagesPerks", "PerkGrowthApplied", bonusDay);
                    Core.LogMsg("[特性成长] 攒满50天, applied=" + bonusDay + " total=" + total);
                }
            }
            catch (Exception ex) { Core.LogMsg("[特性成长] OnDayStart异常: " + ex.Message); }
        }

        public static void ApplyOnPerkUiOpen(PerkUIController ui)
        {
            if (_appliedThisRun) return;
            try
            {
                int bonusDay = MelonPreferences.GetEntryValue<int>("WagesPerks", "PerkGrowthApplied");
                if (bonusDay > 0)
                {
                    ui.maxPerkPoint += bonusDay;
                    ui.maxPerkCount += bonusDay;
                    Core.LogMsg("[特性成长] 开局应用 +" + bonusDay + "点 +" + bonusDay + "槽");
                }
                _appliedThisRun = true;
            }
            catch (Exception ex) { Core.LogMsg("[特性成长] Apply异常: " + ex.Message); }
        }
    }
}
