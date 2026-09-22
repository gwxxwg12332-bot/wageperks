using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace JacksonPerks;

// ============================================================
// 蛙娘系统（09-21 开工，话术 v9 拆包回填 9 项）
// 拍板：实体占地 2×3，全局常驻（不选任何特性也出现）
// 阶段 1：实体注册 + 六维状态 + 常驻面板 + 每日衰减 + 双击面板
// 阶段 2+：喂食/照顾好感、在场增益（预算×4+议价+50）、自动叫客+治安预判、
//          偷钱循环+自主偷拿、好物+销赃+跑路回归（后续迭代）
// ============================================================
public static partial class WageGirlSystem
{

    // 以下旧方法全部改成门面（调用点 0 改动）
    internal static int GetSleepDebt() => GetStat(K_SLEEP_DEBT, 0);
    internal static void SetSleepDebt(int v) => SetStat(K_SLEEP_DEBT, Math.Max(0, v));
    internal static int GetAffection() => GetStat(K_AFF, 0);
    internal static void SetAffection(int v) => SetStat(K_AFF, Math.Max(0, Math.Min(AFF_MAX, v)));
    internal static int GetAllowance() => GetStat(K_ALLOWANCE, 0);
    internal static void SetAllowance(int v) => SetStat(K_ALLOWANCE, v);

    // ===================== 常驻面板（照鲁滨逊 RefreshStatusPanel） =====================
    // 09-20 优化：好感等级文字
    private static string GetAffLevelText() {
        int aff = GetAffection();
        if (aff < 20) return LangHelper.T("刚认识", "Stranger");
        if (aff < 50) return LangHelper.T("熟了", "Familiar");
        if (aff < 80) return LangHelper.T("信任你", "Trusts you");
        return LangHelper.T("亲如家人", "Family");
    }
    // 09-20 优化：六维文字描述
    private static string GetStatText(string key) {
        int v = GetStat(key);
        if (v < 20) return LangHelper.T("很差", "Poor");
        if (v < 40) return LangHelper.T("不太好", "Bad");
        if (v < 70) return LangHelper.T("还行", "OK");
        if (v < 90) return LangHelper.T("不错", "Good");
        return LangHelper.T("很好", "Great");
    }
    // 09-20 优化：日常随机台词（按好感分档）
    private static string GetDailyLine() {
        int aff = GetAffection();
        int mood = GetStat(K_MOOD);
        int sat = GetStat(K_SAT);
        if (mood < 20) return new[] { LangHelper.T("她现在心情很差，好像在生气", "She is in a bad mood, seems angry"), LangHelper.T("她蹲在角落里，一脸不高兴", "She squats in the corner, looking unhappy") }[Core.Rng.Next(2)];
        if (sat < 20) return new[] { LangHelper.T("她饿坏了，在找吃的", "She is starving, looking for food"), LangHelper.T("她一直在盯着你的食物柜", "She keeps staring at your food cabinet") }[Core.Rng.Next(2)];
        string[] lines;
        if (aff < 20) {
            lines = new[] {
                LangHelper.T("她躲在角落，好像不太敢靠近你", "She hides in the corner, seems afraid to approach you"),
                LangHelper.T("她偷偷看了你一眼，又迅速低下头", "She glances at you secretly, then quickly looks down"),
                LangHelper.T("她好像在提防你，不太敢说话", "She seems wary of you, afraid to speak")
            };
        } else if (aff < 50) {
            lines = new[] {
                LangHelper.T("她在柜台附近晃悠，偶尔看看你", "She wanders near the counter, glances at you occasionally"),
                LangHelper.T("她打了个哈欠，好像有点无聊", "She yawns, seems a bit bored"),
                LangHelper.T("她今天心情不错，冲你点了点头", "She is in a good mood today, nods at you"),
                LangHelper.T("她好像在观察你", "She seems to be observing you")
            };
        } else if (aff < 80) {
            lines = new[] {
                LangHelper.T("她经常跑到你身边，好像很信任你", "She often comes to you, seems to trust you"),
                LangHelper.T("她今天心情很好，冲你笑了笑", "She is in a great mood today, smiles at you"),
                LangHelper.T("她好像在等你跟她说话", "She seems to be waiting for you to talk to her"),
                LangHelper.T("她凑过来蹭了蹭你的胳膊", "She nuzzles up against your arm")
            };
        } else {
            lines = new[] {
                LangHelper.T("她一直粘在你身边，像只小尾巴", "She sticks to you like a little tail"),
                LangHelper.T("她今天特别开心，一直在你身边转来转去", "She is extra happy today, spinning around you"),
                LangHelper.T("她靠在你身边，好像很安心", "She leans against you, seems peaceful"),
                LangHelper.T("她把脑袋靠在你肩膀上", "She rests her head on your shoulder")
            };
        }
        return lines[Core.Rng.Next(lines.Length)];
    }

    // ===================== 每日结算（OnDayStart Postfix——同养蛊机挂点） =====================
    public static void PostfixOnDayStart()
    {
        try
        {
            // 全局发放：存档里未出现过 → 发 1 个蛙娘实体到背包（玩家自己摆出来）
            if (!Exists() && WageGirlPerk.IsActive()) // 09-23 Perk 化：选了「蛙娘」特性才发放（旧档已存在保留）
            {
                TryGiveToBackpack();
            }
            if (!Exists() && !WageGirlPerk.IsActive()) return; // 09-23 Perk 化：没选「蛙娘」且从未出现 → 跳过全部结算（修复未选也扣钱）
            // 09-20 修：SetExists调用退役——Exists()改实体优先，不再写K_EXIST防default_run污染
            // 六维每日衰减（睡眠除外——仿生女仆夜间自然恢复睡眠）
            foreach (var k in new[] { K_SAT, K_TH, K_HEALTH, K_MOOD, K_CLEAN })
            {
                int v = GetStat(k);
                if (v <= 0) v = STAT_INIT;
                SetStat(k, v - DAILY_DECAY);
            }
            SetStat(K_ALLOWANCE_COUNT, 0); // 09-23 每天重置零花钱计数
            // 睡眠自然增长（过夜充电/睡觉恢复）
                        // 09-23 睡眠 debt 机制：先扣债（偷钱/销赃/偷拿熬夜）再自然恢复
            int sleepDebt = GetSleepDebt(); SetSleepDebt(0);
            SetStat(K_SLEEP, GetStat(K_SLEEP) - sleepDebt + 15);
            // 好感每日回落（不照顾）
            // 好感衰减：当天没互动 -1~2，六维低额外 -2~5
            int decay = 0;
            if (!WasFedToday()) decay += UnityEngine.Random.Range(1, 3); // 完全没互动
            if (IsAnyStatLow()) decay += UnityEngine.Random.Range(2, 6); // 六维低额外
            if (decay > 0) SetAffection(GetAffection() - decay);
            // 阶段 5：回归 / 自主偷拿 / 偷钱循环
            RunDayEvents();
        }
        catch { }
    }

    // 每日事件：回归（按原因分支）→（消失期不活动）→ 跑路检查 → 初次偷拿 → 日常偷拿 → 好物 → 偷钱循环
    private static void RunDayEvents()
    {
        try
        {
            if (!Exists()) return; // 09-23 Perk 化：未出现不活动（双保险，修复没选 Perk 也偷钱）
            int day = CurrentDay();
            int leaveDay = GetStat(K_LEAVE);
            // 1) 回归（到达回归日）→ 按原因分支 → 实体重新发放
            if (leaveDay > 0 && day >= leaveDay)
            {
                int reason = GetStat(K_LEAVE_REASON);
                SetStat(K_LEAVE, 0);
                SetStat(K_LEAVE_REASON, 0);
                // 喂钱带物优先：零花钱池 > 0 → 走带物
                int allow = GetStat(K_ALLOWANCE);
                if (allow > 0) {
                    GiveBackItem(allow);
                    SetStat(K_ALLOWANCE, 0);
                    ReportLine(LangHelper.T("蛙娘逛街回来了，带了点东西", "Wage Girl is back from shopping"));
                }
                else if (reason == 1) FenceReturn();
                else if (reason == 2) RunawayReturn();
                else
                {
                    int amt = GetStat(K_STEAL_AMT);
                    if (amt > 0) { GiveBackItem(amt); SetStat(K_STEAL_AMT, 0); }
                    else ReportLine(LangHelper.T("蛙娘回来了", "Wage Girl is back"));
                }
                TryGiveToBackpack(); // 实体重新发放（消失期实体已移除）
                return; // 回归日不触发其他事件
            }
            // 2) 消失期：不偷拿不偷钱
            if (leaveDay > 0 && day < leaveDay) return;
            // 3) 跑路检查：连续 3 天任一六维 <20 → 离家出走 14 天
            int lowStreak = GetStat(K_STARVE);
            if (IsAnyStatLow())
            {
                lowStreak++;
                SetStat(K_STARVE, lowStreak);
            }
            else SetStat(K_STARVE, 0);
            if (lowStreak >= 5) // 09-20 优化：3→5
            {
                SetStat(K_STARVE, 0);
                SetStat(K_LEAVE, day + 14);
                SetStat(K_LEAVE_REASON, 2);
                _curState = "away"; _curAnimSprites = _spAway; _stateFrameSec = 0.125f; _leavingTimer = 0.5f; // 播away挥手帧再移除
                ReportLine(LangHelper.T("蛙娘连续几天没吃好没睡好，离家出走了（14 天后回来）", "Wage Girl ran away after days of neglect (back in 14 days)"));
                return;
            }
            int lastSteal = GetStat(K_LAST_STEAL);
            // 4) 初次偷拿（lastSteal==0 → 初次偷 1 件 + 50 钱，然后设 today）
            if (lastSteal <= 0)
            {
                System.Collections.Generic.List<string> stolenNames0 = null;
                int stolen = StealItems("food", 1, "highest", out stolenNames0);
                ModCashN(-50); // 09-19 新档第一天必偷50（不管有没有东西）
                // 09-19 修：初次偷拿补夜报（原分支扣钱偷物后直接 return，无 ReportLine → 初次见面夜报缺失）
                if (stolen > 0 && stolenNames0 != null && stolenNames0.Count > 0)
                    ReportLine(LangHelper.T("蛙娘偷走了 50 块钱和" + string.Join("、", stolenNames0), "Wage Girl stole 50 credits and " + string.Join(", ", stolenNames0)));
                else
                    ReportLine(LangHelper.T("蛙娘偷走了 50 块钱", "Wage Girl stole 50 credits"));
                SetStat(K_LAST_STEAL, day);
                return;
            }
            // 心情>=80 自动归还偷的东西
                int sv = GetStat("stolenValue", 0);
                if (GetStat(K_MOOD) >= 80 && sv > 0) {
                    GiveBackItem(sv);
                    SetStat("stolenValue", 0);
                    ReportLine(LangHelper.T("蛙娘心情大好，把之前偷的东西都还回来了", "Wage Girl is in a great mood and returned everything she stole"));
                }
            // 5) 日常自主偷拿（状态触发）——给零花钱后当天不偷
            int allowNow2 = GetStat(K_ALLOWANCE);
            if (allowNow2 <= 0) {
            TrySnatch();
            } // 给零花钱后当天不偷
            // 6) 好物：好感 ≥50 每 7 天带 1 件
            int lastGift = GetStat(K_LAST_GIFT);
            if (GetAffection() >= 50 && day - lastGift >= 7)
            {
                GiveGift();
                SetStat(K_LAST_GIFT, day);
            }
            // 7) 偷钱循环（≥7 天）——K_LEAVE 改"回归日"语义（day+1）
            // 零花钱 ≥100 当天 50% 不偷
            int allowNow = GetStat(K_ALLOWANCE);
            if (allowNow >= 100 && UnityEngine.Random.Range(0, 2) == 0) {
                SetStat(K_LAST_STEAL, day);
            }
            else if (day - lastSteal >= STEAL_INTERVAL && GetAffection() < 80)
            {
                int aff = GetAffection();
                int steal = 100 - (int)((aff / 100f) * 90f); // 09-20 优化：好感越高偷得越少（0→100、100→10）
                ModCashN(-steal);
                SetStat(K_STEAL_AMT, steal);
                                SetSleepDebt(GetSleepDebt() + 20); // 偷钱外出熬夜 -20 睡眠（次日结算）
SetStat(K_LEAVE, day + 1); // 回归日 = 明天
                SetStat(K_LEAVE_REASON, 0);
                SetStat(K_LAST_STEAL, day);
                _curState = "away"; _curAnimSprites = _spAway; _stateFrameSec = 0.125f; _leavingTimer = 0.5f; // 播away挥手帧再移除
                ReportLine(LangHelper.T("蛙娘偷走了 " + steal + " 块钱，出门躲债去了（明天回来）", "Wage Girl stole " + steal + " credits and went out (back tomorrow)"));
            }
        }
        catch { }
    }

    // 任一六维 <20（跑路判定）
    private static bool IsAnyStatLow()
    {
        try
        {
            return GetStat(K_SAT) < 20 || GetStat(K_TH) < 20 || GetStat(K_HEALTH) < 20
                || GetStat(K_MOOD) < 20 || GetStat(K_CLEAN) < 20 || GetStat(K_SLEEP) < 20;
        }
        catch { return false; }
    }

    // 好物：95% 好物池（价值 ≥500 普通物品）+ 5% mod 物品（ItemPool），放入柜台
    private static void GiveGift()
    {
        try
        {
            GameItem it = null;
            bool modGift = Core.Rng.Next(100) < 5;
            if (modGift)
            {
                var mpool = WagePowerPerk.ItemPool;
                if (mpool != null && mpool.Length > 0)
                {
                    int idx = Core.Rng.Next(mpool.Length);
                    try { it = DirectoryMaster.Item(mpool[idx], true); } catch { }
                }
            }
            else
            {
                var ids = DirectoryMaster.GetIdentifierList<GameItem>(null);
                if (ids == null || ids.Count == 0) return;
                var pool = new System.Collections.Generic.List<string>();
                for (int i = 0; i < ids.Count; i++)
                {
                    if (string.IsNullOrEmpty(ids[i]) || ids[i] == ENTITY_ID) continue;
                    if (System.Array.IndexOf(Core.ExcludedItemIds, ids[i]) >= 0) continue;
                    pool.Add(ids[i]);
                }
                int tries = 0;
                while (tries < 12 && pool.Count > 0)
                {
                    int idx = Core.Rng.Next(pool.Count);
                    string id = pool[idx];
                    try
                    {
                        var g = DirectoryMaster.Item(id, true);
                        if (g == null) { pool.RemoveAt(idx); tries++; continue; }
                        if (g.IsTag("STANDARD_MACHINE_TAG") || g.IsTag("CONTAINER_TAG")) { pool.RemoveAt(idx); tries++; continue; }
                        // 09-20 优化：礼物价值随好感提升（好感 0→500、100→700）
                        int minVal = 500 + (GetAffection() / 100) * 200;
                        if (g.unitValue < minVal) { pool.RemoveAt(idx); tries++; continue; } // 好物价值 ≥minVal
                        it = g; break;
                    }
                    catch { pool.RemoveAt(idx); tries++; }
                }
            }
            if (it == null) return;
            GameItem giftCrate = CreateSupplyCrate(it.unitValue);
            if (giftCrate != null) { AddToFront(giftCrate); ReportLine(LangHelper.T("蛙娘今天心情好，带回来一只物资箱！", "Wage Girl brought a supply crate today!")); }
            else { AddToFront(it); ReportLine(LangHelper.T("蛙娘今天心情好，带回来一件好东西！", "Wage Girl brought a nice gift today!")); }
        }
        catch { }
    }

    // 销赃回归：按所选类别拆成多件带回（每件 ≤ 单件目标、最接近；总价值 ≤ 目标×1.3；跑腿费 10% 起随好感降）
    
        private static void GiveAllowance(int amt)
        {
            try {
                var ps = Il2Cpp.PlayerStore.Instance;
                if (ps == null) return;
                if (ps.playerCash < amt) { ReportLine(LangHelper.T("蛙娘想拿零花钱，但你钱不够……", "Wage Girl wants allowance, but you are broke...")); return; }
                ps.playerCash -= amt;
                // 09-23 改：零花钱进小金库（不出去逛街）
                SetStat(K_SAVINGS, GetStat(K_SAVINGS) + amt);
                // 09-23 新增：前三次加好感（按档位）
                int allowCount = GetStat(K_ALLOWANCE_COUNT);
                if (allowCount < 3) {
                    int affGain = _allowanceSel == 100 ? 1 : (_allowanceSel == 300 ? 2 : 3);
                    SetAffection(GetAffection() + affGain);
                    SetStat(K_ALLOWANCE_COUNT, allowCount + 1);
                    ReportLine(LangHelper.T("蛙娘把 " + amt + " 块零花钱存进小金库（好感 +" + affGain + "，今日第 " + (allowCount+1) + "/3 次）", "Wage Girl saved " + amt + " credits (affection +" + affGain + ", today " + (allowCount+1) + "/3)"));
                } else {
                    ReportLine(LangHelper.T("蛙娘把 " + amt + " 块零花钱存进小金库（今日已给三次，不再加好感）", "Wage Girl saved " + amt + " credits (no more affection today)"));
                }
                try { if (Il2Cpp.CustomUIManager.Instance != null && Il2Cpp.CustomUIManager.Instance.IsOpen("wage_girl_panel")) ShowPanel(); } catch { }
            } catch { }
        }

        private static void CycleAllowanceSel()
        {
            try {
                int idx = System.Array.IndexOf(ALLOWANCE_STEPS, _allowanceSel);
                _allowanceSel = ALLOWANCE_STEPS[(idx + 1) % ALLOWANCE_STEPS.Length];
            } catch { }
        }
}
