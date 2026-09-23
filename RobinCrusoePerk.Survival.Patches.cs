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

    // ===== v5.9 失眠警觉：偷窃概率-50%（拆包 2.5.29：原生偷窃=PlayerStore.HandleInsurance，EndNight 链）=====
    public static bool PrefixHandleInsurance()
    {
        try { if (IsActive() && GetAntiTheftMult() < 1.0) {  return false; } } catch { }
        return true;
    }

    // ===== 客流减量（精神档位：低迷-1 / 低落-2 / 崩溃-4；v5.9 病恹恹爆发当日客流-50% 隔一skip一）=====
    private static int _pickSkipToday = 0;
    private static bool _burstSkipFlip = false;
    public static bool PrefixPickClient()
    {
        try
        {
            if (!IsActive()) return true;
            int skip = GetClientReduction();
            if (_pickSkipToday < skip) { _pickSkipToday++; return false; }
            if (_burstClientCut >= 50) { _burstSkipFlip = !_burstSkipFlip; if (_burstSkipFlip) return false; } // 病恹恹爆发：当日客流-50%
        }
        catch (System.Exception ex) { Core.LogMsg("[RobinCrusoePerk.Survival] 异常: " + ex.Message); }
        return true;
    }

    // ===== 禁外出（低落/崩溃，含生病）=====
    public static bool PrefixOpenGoOutsideConfirm()
    {
        try { if (IsActive() && IsForbiddenOutside()) return false; }
        catch (System.Exception ex) { Core.LogMsg("[RobinCrusoePerk.Survival] 异常: " + ex.Message); }
        return true;
    }

    // ===== 腐烂/已食用食物客户拒买 =====
    private static int _rejectLogCount = 0;
    public static bool PrefixCanClientExposeAnyFeature(GameItem gameItem)
    {
        try
        {
            if (IsActive() && gameItem != null && IsFood(gameItem))
            {
                bool reject = false;
                if (GetFoodQuality(gameItem) >= 3) { reject = true; }
                else if (IsEaten(gameItem)) { reject = true; }
                if (reject)
                {
                    _rejectLogCount++;
                    if (_rejectLogCount % 60 == 1)
                    return false;
                }
            }
        }
        catch (System.Exception ex) { Core.LogMsg("[RobinCrusoePerk.Survival] 异常: " + ex.Message); }
        return true;
    }

    // ===== 吃过的食物放不上柜台 =====
    public static bool PrefixAddedItemToWeightedArea(GameItem gameItem)
    {
        try
        {
            if (IsActive() && gameItem != null && IsFood(gameItem) && IsEaten(gameItem))
            {
                return false;
            }
        }
        catch (System.Exception ex) { Core.LogMsg("[RobinCrusoePerk.Survival] 异常: " + ex.Message); }
        return true;
    }

    // ===== 每日结算（OnNewDay Postfix）=====
    public static void PostfixOnNewDay()
    {
        try
        {
            if (!IsActive()) return;
            _pickSkipToday = 0;
            SyncRentDisplay(); // 租金显示同步为100天制（拆包：日历读 dayUntilRent+rentValue）

            // ===== v5.9 节点池抽取（nodeKey 变化 → 先爆发 RollBurst 再抽池；正面节点无爆发）=====
            RollNodeFx();

            // ===== v5.9 三状态自然变化（eatEff 只影响吃食物、thirstEff 影响口渴衰减、wearEff 影响健康衰减）=====
            int sat = Math.Max(0, GetSatiety() - DAILY_SAT_LOSS - FxNum("satD"));   // 饱食 -20% + 节点衰减（嗓子冒烟 satD+10）
            int thBase = (int)(DAILY_THIRST_LOSS * GetThirstEffMult());             // 口渴 -25% × 耐旱/省水系数（v5.9 CompBuff）
            int th = Math.Max(0, GetThirstPct() - thBase - FxNum("thD"));           // 口渴 -25% + 节点衰减（肚里打鼓 thD+5）
            int h0 = GetHealth();
            int healthGain = DAILY_HEALTH_GAIN + FxNum("hR");   // 健康 +10% + 节点恢复修正（病恹恹 hR-5 / 透心凉 hR+5）
            int hDecay = (int)(FxNum("hD") * GetWearEffMult());                     // 健康衰减 × 糙人抗造系数（v5.9 CompBuff）
            int h = Math.Min(100, Math.Max(0, h0 + healthGain - hDecay));           // 健康自然变化 + 衰减修正
            if (IsBloodWeak()) h = Math.Max(0, h - 10); // 卖血虚弱（<3000）：健康衰减加速（09-17）
            SetSatiety(sat); SetThirstPct(th); SetHealth(h);
            // 新三状态结算（v5.8-8）：清洁 -10 + 节点衰减/恢复；睡眠 打烊+30（拾荒当天已 -15）+ 节点睡眠恢复 + 补觉高效；社交 接待日+5/无客日-5 + 节点
            SetClean(Math.Max(0, Math.Min(100, GetClean() - DAILY_CLEAN_LOSS - FxNum("cleanD") + FxNum("cleanR"))));
            SetSleep(Math.Min(100, Math.Max(0, GetSleep() + DAILY_SLEEP_GAIN + FxNum("sleepR") + GetCompBuffSleepRestore()))); // sleepR 符号修正（拆包 09-10：'sleepR-10'=恢复-10，减号负负得正，改加号）
            AddBlood(100); // 睡觉回血（09-17 卖血）
            TickBloodRest(); // 09-20 M5：虚弱强制休息 3 天 → 结束 ±20%
            int deals = WageSaveStore.GetInt(PERK_ID, "deals", 0);
            int social = GetSocial() + (deals > 0 ? DAILY_SOCIAL_GAIN : -DAILY_SOCIAL_LOSS) + FxNum("socD") + FxNum("socR");
            SetSocial(Math.Max(0, Math.Min(100, social)));
            WageSaveStore.SetInt(PERK_ID, "deals", 0); // 接待计数清零

            // ===== v5.9 CompBuff 每日递减（Duration 制：到期移除）=====
            TickCompBuffs();

            // ===== 心情结算（v5.7 拍板：三项≥80→+5；任一项<60→-10；60-79→不掉不涨；仅看饱/渴/健三项）+ 节点心情 + CompBuff 心情 =====
            int mood = GetMood();
            bool satOK = sat >= SATIETY_GOOD, thOK = th >= THIRST_GOOD, hOK = h >= HEALTH_GOOD;
            if (satOK && thOK && hOK) mood = Math.Min(100, mood + MOOD_UP);
            else if (sat < 60 || th < 60 || h < 60) mood = Math.Max(0, mood - (int)(MOOD_DOWN * GetMoodDampMult())); // 摆烂反弹：-10→-5（v5.9）
            mood = Math.Max(0, Math.Min(100, mood + FxNum("mood")));
            mood = Math.Min(100, mood + GetCompBuffMoodBonus()); // 病中专注/松弛自洽/清静自处 每日心情+（v5.9 CompBuff）
            SetMood(mood);

            // ===== 患病（节点池 sick+20：饿疯池恶，每日结算概率患病）=====
            if (GetSickChanceAdd() > 0)
            {
                TryInfect(GetSickChanceAdd() / 100.0);
            }

            // ===== v5.9 觅食（店内翻找，打烊结算概率；躺板板30% / 清洁<50 15% + 独狼专注觅食+20%）=====
            int hNow = GetHealth(), cNow = GetClean();
            int forageBonus = GetForageBonus(); // 独狼专注：觅食概率+20%（v5.9 CompBuff）
            if (hNow < NODE_CRIT && UnityEngine.Random.value < (0.30f + forageBonus / 100f))      // 躺板板（健康<20）：30% 翻出 1-2 份食物
                ForageIndoor(UnityEngine.Random.value < 0.5f ? 2 : 1, LangHelper.T("躺板板翻找", "Bedridden rummaging"));
            else if (cNow < 50 && UnityEngine.Random.value < (0.15f + forageBonus / 100f))         // 清洁<50（蓬头垢面+灰头土脸）：15% 翻出 1 份
                ForageIndoor(1, LangHelper.T("店内翻找", "In-store rummaging"));

            // ===== 连续计数（濒饿/渴/病危）=====
            bool starving = sat < NODE_CRIT || th < NODE_CRIT;          // 濒饿：饱食<20 或 口渴<20
            int sd5 = starving ? GetStarveDays() + 1 : 0;
            int td5 = th < NODE_CRIT ? GetThirstDeathDays() + 1 : 0;    // 渴死独立（口渴<20 连续）
            int cd5 = h < NODE_CRIT ? GetCritDays() + 1 : 0;            // 病危：健康<20 连续
            WageSaveStore.SetInt(PERK_ID, "starveDays", sd5);
            WageSaveStore.SetInt(PERK_ID, "thirstDeath", td5);
            WageSaveStore.SetInt(PERK_ID, "critDays", cd5);

            // ===== 救场安全网（v5.7：濒饿5天送食 / 病危3天送药；2026-09-09 修复：渴死4天快于濒饿救场5天 → 渴死前第3天补送水）=====
            if (sd5 == 5) RescueFeed();
            else if (td5 >= 3 && sd5 < 5) RescueWater();                       // 濒渴第3天送水（第4天渴死前；饿优先）
            else if (cd5 == 3 && sd5 < 5 && td5 < 3) RescueMedicine();         // 病危第3天送药（第4天病死前）

            // ===== 三种生存死亡（用户拍板 09-09 方案A：饿5/渴4/病4，持续天数；救场当天豁免、送食/药后仍恶化才死）=====
            if (sd5 > 5) { ExecuteGameOverBy("starvation"); return; }          // 濒饿第5天送食，仍持续（第6天起）饿死
            if (td5 >= 4 && sd5 < 5) { ExecuteGameOverBy("thirst"); return; }  // 濒渴 4 天渴死（饿优先；救场赶不上属设计）
            if (cd5 > 3 && sd5 < 5 && td5 < 4) { ExecuteGameOverBy("disease"); return; } // 病危第3天送药，仍持续（第4天起）病死

            // ===== 粮仓充盈（v5.7：饱食≥80 连续 7 天，断档归零）=====
            int gd = sat >= SATIETY_GOOD ? GetGranaryDays() + 1 : 0;
            WageSaveStore.SetInt(PERK_ID, "granary", gd);
            // ===== 昂扬累计（v5.7：饱食≥80 且健康≥80 每2天 +1%售价 +5%预算，封顶5，封顶后断档维持）=====
            int es = (sat >= SATIETY_GOOD && h >= HEALTH_GOOD) ? GetElevStreak() + 1 : 0;
            int ec = GetElevCount();
            if (es >= ELEV_EVERY)
            {
                ec = Math.Min(ELEV_MAX, ec + 1);
                es = 0;
            }
            WageSaveStore.SetInt(PERK_ID, "elevStreak", es);
            WageSaveStore.SetInt(PERK_ID, "elevCount", ec);
            _burstClientCut = 0; _burstSkipFlip = false; // v5.9 病恹恹爆发客流-50% 当日标记清零

            // ===== 食物衰减 + 口粮统计 =====
            int fresh = 0, stale = 0, rotten = 0;
            int foodCount = DecayFoodsAndCount(out fresh, out stale, out rotten);

            // ===== 播报（v5.8-8：锁定节点名 + 叙事标签 + 锁定效果 + 加成）=====
            int day = StoreStation.GetDayCounter();
            int sellB = GetSellBonusPct();
            int budB = GetBudgetBonusPct();
            NodeDef dn = CurrentNode();
            string nodeTxt;
            bool badNode = false;
            if (dn == null) nodeTxt = LangHelper.T("状态平稳（饱食" + sat + " 口渴" + th + " 健康" + h + "）", "Stable (" + sat + " satiety / " + th + " thirst / " + h + " health)");
            else
            {
                badNode = dn.Sev >= 4;
                string fxDesc = "";
                foreach (string f in dn.Lock) { string lb = FxLabel(f); if (lb.Length > 0) fxDesc += lb + " "; }
                string cur = GetNodeFx();
                if (!string.IsNullOrEmpty(cur) && cur != "flavor") { string lb = FxLabel(cur); if (lb.Length > 0) fxDesc += lb + " "; }
                nodeTxt = dn.DisplayName + LangHelper.T("：", ": ") + dn.DisplayTag + (fxDesc.Length > 0 ? "｜" + fxDesc.Trim() : "") + LangHelper.T("（饱食" + sat + " 口渴" + th + " 健康" + h + "）", " (" + sat + " satiety / " + th + " thirst / " + h + " health)");
            }
            string moodTxt = LangHelper.T("心情 ", "Mood ") + mood + (sellB > 0 || budB > 0 ? "｜" + LangHelper.T("售价+" + sellB + "% 预算+" + budB + "%", "Sell +" + sellB + "% Budget +" + budB + "%") : "");
            string granaryTxt = gd >= GRANARY_DAYS ? LangHelper.T("｜★粮仓充盈 售价+5%", "| Granary full, Sell +5%") : (gd > 0 ? LangHelper.T("｜粮仓 " + gd + "/7 天", "| Granary " + gd + "/7 days") : "");
            try
            {
                var ps = PlayerStore.Instance;
                if (ps != null)
                {
                    ps.AddNightLog(LangHelper.T("—— 鲁滨逊的账本 · 第 " + day + " 天 ——", "-- Robinson's Ledger · Day " + day + " --"), "#7FC97F"); // 09-22 统一柔和绿
                    ps.AddNightLog(nodeTxt + "｜" + moodTxt + granaryTxt, "#7FC97F"); // 09-22 统一柔和绿
                    ps.AddNightLog(LangHelper.T("口粮：新鲜 " + fresh + "｜变质 " + stale + "｜腐烂 " + rotten + "（共" + foodCount + "份可吃）", "Rations: fresh " + fresh + " | stale " + stale + " | rotten " + rotten + " (" + foodCount + " edible)"), "#7FC97F"); // 09-22 统一柔和绿
                }
            }
            catch (System.Exception ex) { Core.LogMsg("[RobinCrusoePerk.Survival] 异常: " + ex.Message); }
            try { StoreUIManager.Instance.Notify(LangHelper.T("第" + day + "天：", "Day " + day + ": ") + nodeTxt + "｜" + moodTxt, "white"); } catch { }
            RefreshStatusPanel(); // 每日结算刷新常驻面板
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] PostfixOnNewDay 异常: " + ex.Message); }
    }

    // ===== 接待计数（OnDealAccepted Postfix 调用，社交结算用）=====
    internal static void RecordDeal()
    {
        try { if (IsActive()) WageSaveStore.SetInt(PERK_ID, "deals", WageSaveStore.GetInt(PERK_ID, "deals", 0) + 1); } catch { }
    }

}
