using MelonLoader;
using System;
using Il2Cpp;

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
                Core.LogMsg("[特性成长] OnDayStart触发");
                int total = MelonPreferences.GetEntryValue<int>("WagesPerks", "PerkGrowthTotalDays");
                total++;
                MelonPreferences.SetEntryValue("WagesPerks", "PerkGrowthTotalDays", total);
                Core.LogMsg("[特性成长] total=" + total);

                int bonusDay = total / 50;
                int applied = MelonPreferences.GetEntryValue<int>("WagesPerks", "PerkGrowthApplied");

                // 现金正数才记
                int cash = 0;
                try { dynamic ps = PlayerStore.Instance; cash = ps.playerCash; } catch { }
                Core.LogMsg("[特性成长] cash=" + cash + " bonusDay=" + bonusDay + " applied=" + applied);
                if (cash > 0 && bonusDay > applied)
                {
                    MelonPreferences.SetEntryValue("WagesPerks", "PerkGrowthApplied", bonusDay);
                    Core.LogMsg("[特性成长] 攒满50天, applied=" + bonusDay + " total=" + total);
                    MelonLogger.Msg("[Wage's Perks] 🎉 特性成长里程碑：已累计50天，下次新开局自动 +" + bonusDay + "点 +" + bonusDay + "槽！");
                }
            }
            catch (Exception ex) { Core.LogMsg("[特性成长] OnDayStart异常: " + ex.Message); }
        }

        public static void ApplyOnPerkUiOpen(dynamic ui)
        {
            if (_appliedThisRun) return;
            try
            {
                int bonusDay = MelonPreferences.GetEntryValue<int>("WagesPerks", "PerkGrowthApplied");
                Core.LogMsg("[特性成长] Apply被调 bonusDay=" + bonusDay);
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
