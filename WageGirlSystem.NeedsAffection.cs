using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace WagePerks;

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
    // 10-07 #10 好感重做：int×100 新键 affectionExact（两位小数精度 -200~200，免扩 WageSaveStore 无 double API）
    //   旧键 affection 保留门面（调用点 0 改动）；旧档首次读触发惰性迁移（幂等：新键已存在则直接用）
    internal static double GetAffectionExact()
    {
        try
        {
            if (WageSaveStore.HasKey(NS, K_AFF_EXACT) || _memStats.ContainsKey(K_AFF_EXACT))
                return GetStat(K_AFF_EXACT, 0) / 100.0;
            int old = GetStat(K_AFF, 0);
            if (old != 0 || WageSaveStore.HasKey(NS, K_AFF))
            {
                SetStat(K_AFF_EXACT, old * 100); // 旧档迁移（int×100）；下次读走新键=幂等
                return old;
            }
            return 0;
        }
        catch { return 0; }
    }
    internal static int GetAffection() => (int)GetAffectionExact(); // 旧门面：整数值（截断，保留原 int 语义；50 事件链/日结阈值不变）
    internal static void SetAffection(int v) => SetAffectionExact(v); // 旧门面：整数写精确
    internal static void SetAffectionExact(double v)
    {
        int iv = (int)Math.Round(v * 100);
        iv = Math.Max(-20000, Math.Min(20000, iv)); // clamp [-200,200]（#10 5 节点信赖上限 200；负值合法）
        SetStat(K_AFF_EXACT, iv);
        CheckAffection50Reward();
    }
    // #10 5 节点映射：节点1[-200,-101]敌意 / 节点2[-100,-1]冷淡 / 节点3[0,49]友好 / 节点4[50,99]亲密 / 节点5[100,200]信赖
    internal static int GetAffNode(double aff)
    {
        if (aff < -100) return 1;
        if (aff < 0) return 2;
        if (aff < 50) return 3;
        if (aff < 100) return 4;
        return 5;
    }

    // 10-06 H-1a（拆包 69VfzLzb9jFA83REUYVtB3）：好感50瞬间"三件套发放+移动启动"双落格并发→NRE。
    //   发放改延迟队列：CheckAffection50Reward 只设标记，实际发放由 Anim.Update 帧首经 TickGift50Delay 延迟 2 帧后执行（与移动落格完全错帧）
    internal static bool _pendingGift50 = false;
    private static int _gift50DelayFrames = 0; // H-1a：标记后延迟 2 帧发放
    internal static bool TickGift50Delay()
    {
        if (!_pendingGift50) return false;
        if (_gift50DelayFrames <= 0) return true; // 延迟期满，调用方执行 FlushPendingGift50
        _gift50DelayFrames--;
        return false;
    }
    // v1.3.1：好感首次>=50 送三件套（生成器×1+神经模组×2+保护器×1），防重复（WageSaveStore 标记）
    private static void CheckAffection50Reward()
    {
        try
        {
            if (GetAffection() < 50) return;
            if (WageSaveStore.GetInt("WageGirl", "gift50_sent", 0) != 0) return;
            _pendingGift50 = true; _gift50DelayFrames = 2; // 10-06 H-1a：只标记+设延迟，下一帧起倒数 2 帧后 Flush 发放
        }
        catch (System.Exception ex) { Core.LogMsg("[蛙娘] 好感50标记失败: " + ex.Message); }
    }
    // 10-06 H-1a：延迟队列执行点（Anim.Update 帧首调用；发放落格与移动落格不同帧，避开第三方 Prefix 叠挂崩溃）
    internal static void FlushPendingGift50()
    {
        try
        {
            if (!_pendingGift50) return;
            _pendingGift50 = false;
            if (GetAffection() < 50) return;
            if (WageSaveStore.GetInt("WageGirl", "gift50_sent", 0) != 0) return;
            WageSaveStore.SetInt("WageGirl", "gift50_sent", 1);
            // 10-05 恢复发放（10-03 止血禁用；用户反馈"50好感不送东西"）。
            // 止血原因=TryAcceptOnce MonoMod 补丁链疑似崩点——GiveRewardItem 已改 UncheckedAcceptAll 主通道规避。
            GiveRewardItem(GuMachineSystem.AI_GENERATOR_ID, 1);
            GiveRewardItem("system_capped_neural_core", 2); // 原生神经模组（capped版）
            GiveRewardItem(GuMachineSystem.PROTECTOR_ID, 1);
            Core.LogMsg("[蛙娘] 好感破50，三件套已发放（延迟队列 UncheckedAcceptAll 通道）");
        }
        catch (System.Exception ex) { Core.LogMsg("[蛙娘] 好感50三件套失败: " + ex.Message); }
    }
    private static void GiveRewardItem(string id, int count)
    {
        try
        {
            var em = EmporiumEntry.Instance; if (em == null || em.invElement == null) return;
            // 10-05 改 UncheckedAcceptAll 主通道（10-03 止血疑 TryAcceptOnce MonoMod 补丁链崩点；直接批量塞入规避）
            var l = new Il2CppSystem.Collections.Generic.List<GameItem>();
            for (int i = 0; i < count; i++)
            {
                GameItem it = null;
                try { it = DirectoryMaster.Item(id, true); } catch { }
                if (it != null) l.Add(it);
            }
            if (l.Count > 0)
            {
                try { em.invElement.UncheckedAcceptAll(l); } catch (System.Exception ex) { Core.LogMsg("[蛙娘] 三件套UncheckedAcceptAll异常: " + ex.Message); }
            }
        }
        catch (System.Exception ex) { Core.LogMsg("[蛙娘] GiveRewardItem异常: " + ex.Message); }
    }
    internal static int GetAllowance() => GetStat(K_ALLOWANCE, 0);
    internal static void SetAllowance(int v) => SetStat(K_ALLOWANCE, v);
    // 10-07 #10：好感等级文字（5 节点映射）
    private static string GetAffLevelText() {
        switch (GetAffNode(GetAffectionExact()))
        {
            case 1: return LangHelper.T("敌意", "Hostile");
            case 2: return LangHelper.T("冷淡", "Cold");
            case 3: return LangHelper.T("友好", "Friendly");
            case 4: return LangHelper.T("亲密", "Close");
            default: return LangHelper.T("信赖", "Trusted");
        }
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
            // 09-26 日切防御重洗白：镜像读档扫描（WageSaveStore.Lifecycle L60-81），
            // 已洗白物品若恢复违禁 tag 则重新洗白（不依赖蛙娘是否活跃——保护全店洗白物品）
            try
            {
                var em = Il2Cpp.EmporiumEntry.Instance;
                if (em != null)
                {
                    var all = em.GetAllItems();
                    foreach (var it in all)
                    {
                        try
                        {
                            if (it != null && it.IsTag("wage_washed") && Il2Cpp.ContrabandHelper.GetContrabandLevel(it) > 0)
                            {
                                Il2Cpp.ContrabandHelper.RemoveContrabandStatus(it);
                                Core.LogMsg("[蛙娘] 日切防御重洗白: " + it.identifier);
                            }
                        }
                        catch { }
                    }
                }
            }
            catch (Exception ex3) { Core.LogMsg("[蛙娘] 日切重洗白扫描异常: " + ex3.Message); }
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
                if (v <= 0) v = BuildConfig.WageGirlStatInit;
                SetStat(k, v - BuildConfig.WageGirlDailyDecay);
            }
            SetStat(K_ALLOWANCE_COUNT, 0); // 09-23 每天重置零花钱计数
            // 睡眠自然增长（过夜充电/睡觉恢复）
                        // 09-23 睡眠 debt 机制：先扣债（偷钱/销赃/偷拿熬夜）再自然恢复
            int sleepDebt = GetSleepDebt(); SetSleepDebt(0);
            SetStat(K_SLEEP, GetStat(K_SLEEP) - sleepDebt + BuildConfig.WageGirlSleepRecover);
            // 好感每日回落（不照顾）
            // 好感衰减：当天没互动 -1~2，六维低额外 -2~5（范围 CFG：WageGirlAffDecay*）
            int decay = 0;
            if (!WasFedToday()) decay += UnityEngine.Random.Range(BuildConfig.WageGirlAffDecayMin, BuildConfig.WageGirlAffDecayMax + 1); // 完全没互动
            if (IsAnyStatLow()) decay += UnityEngine.Random.Range(BuildConfig.WageGirlAffDecayLowMin, BuildConfig.WageGirlAffDecayLowMax + 1); // 六维低额外
            if (decay > 0) SetAffection(GetAffection() - decay);
            // 阶段 5：回归 / 自主偷拿 / 偷钱循环
            RunDayEvents();
        }
        catch (System.Exception ex) { Core.LogMsg("[WageGirlSystem.NeedsAffection] 异常: " + ex.Message); }
    }

    // 每日事件：回归（按原因分支）→（消失期不活动）→ 跑路检查 → 初次偷拿 → 日常偷拿 → 好物 → 偷钱循环
    private static void RunDayEvents()
    {
        try
        {
            if (!Exists()) return; // 09-23 Perk 化：未出现不活动（双保险，修复没选 Perk 也偷钱）
            if (GetStat(K_FENCE_PENDING) > 0 && GetStat(K_LEAVE) <= 0) { FenceReturn(); } // 孤立pending恢复：读档后遗留pending首个打烊自动结算
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
                else if (reason == 4) TravelReturn(false);
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
            TravelDailyTick(day); // v1.3.1【8】旅行期间每日扣口粮
            // 2) 消失期：不偷拿不偷钱
            if (leaveDay > 0 && day < leaveDay) return;
            // #10 拍板 3：节点1 敌意[-200,-101] = 随机跑路（2-4 天）+ 不互动（喂食入口另拦截）
            if (GetAffNode(GetAffectionExact()) == 1 && GetStat(K_LEAVE) <= 0)
            {
                int hostileDays = 2 + Core.Rng.Next(3); // 默认 2-4 天（可调）
                SetStat(K_LEAVE, day + hostileDays);
                SetStat(K_LEAVE_REASON, 5); // reason=5 敌意跑路（回归走普通分支）
                _curState = "away"; _curAnimSprites = _spAway; _stateFrameSec = 0.125f; _leavingTimer = 0.5f;
                ReportLine(LangHelper.T("蛙娘对你的好感跌到冰点，气冲冲地离家出走了（" + hostileDays + " 天后回来）", "Wage Girl is furious (affection at rock bottom) and stormed off (back in " + hostileDays + " days)"));
                return;
            }
            // 3) 跑路检查：连续 N 天任一六维 <阈值 → 离家出走 M 天（CFG：WageGirlRunaway*）
            int lowStreak = GetStat(K_STARVE);
            if (IsAnyStatLow())
            {
                lowStreak++;
                SetStat(K_STARVE, lowStreak);
            }
            else SetStat(K_STARVE, 0);
            if (lowStreak >= BuildConfig.WageGirlRunawayStreak)
            {
                SetStat(K_STARVE, 0);
                SetStat(K_LEAVE, day + BuildConfig.WageGirlRunawayDays);
                SetStat(K_LEAVE_REASON, 2);
                _curState = "away"; _curAnimSprites = _spAway; _stateFrameSec = 0.125f; _leavingTimer = 0.5f; // 播away挥手帧再移除
                ReportLine(LangHelper.T("蛙娘连续几天没吃好没睡好，离家出走了（" + BuildConfig.WageGirlRunawayDays + " 天后回来）", "Wage Girl ran away after days of neglect (back in " + BuildConfig.WageGirlRunawayDays + " days)"));
                return;
            }
            int lastSteal = GetStat(K_LAST_STEAL);
            // 4) 初次偷拿（lastSteal==0 → 初次偷 1 件 + N 钱，然后设 today；N=CFG WageGirlStealFirstAmount）
            if (lastSteal <= 0)
            {
                int firstAmt = BuildConfig.WageGirlStealFirstAmount;
                System.Collections.Generic.List<string> stolenNames0 = null;
                int stolen = StealItems("food", 1, "highest", out stolenNames0);
                ModCashN(-firstAmt);
                // 09-19 修：初次偷拿补夜报（原分支扣钱偷物后直接 return，无 ReportLine → 初次见面夜报缺失）
                if (stolen > 0 && stolenNames0 != null && stolenNames0.Count > 0)
                    ReportLine(LangHelper.T("蛙娘偷走了 " + firstAmt + " 块钱和" + string.Join("、", stolenNames0), "Wage Girl stole " + firstAmt + " credits and " + string.Join(", ", stolenNames0)));
                else
                    ReportLine(LangHelper.T("蛙娘偷走了 " + firstAmt + " 块钱", "Wage Girl stole " + firstAmt + " credits"));
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
            // 6) 好物：好感 ≥N 每 M 天带 1 件（CFG：WageGirlGiftAff/Interval）
            int lastGift = GetStat(K_LAST_GIFT);
            if (GetAffectionExact() >= BuildConfig.WageGirlGiftAff && day - lastGift >= BuildConfig.WageGirlGiftInterval)
            {
                GiveGift();
                SetStat(K_LAST_GIFT, day);
            }
            // 7) 偷钱循环——K_LEAVE 改"回归日"语义（day+1）
            // 零花钱 ≥ 第一档 当天 50% 不偷（档位 CFG：WageGirlAllowanceSteps）
            int allowNow = GetStat(K_ALLOWANCE);
            int allowanceFloor = BuildConfig.WageGirlAllowanceSteps.Length > 0 ? BuildConfig.WageGirlAllowanceSteps[0] : 100;
            if (allowNow >= allowanceFloor && UnityEngine.Random.Range(0, 2) == 0) {
                SetStat(K_LAST_STEAL, day);
            }
            else if (day - lastSteal >= BuildConfig.WageGirlStealInterval && GetAffectionExact() < BuildConfig.WageGirlStealNoStealAff)
            {
                double aff = GetAffectionExact();
                int steal = BuildConfig.WageGirlStealBaseMax - (int)((aff / 100.0) * BuildConfig.WageGirlStealAffReduction); // 好感越高偷得越少（0→100、100→10；基准固定 100 独立于 AffMax；负好感→偷得更多）
                ModCashN(-steal);
                SetStat(K_STEAL_AMT, steal);
                                SetSleepDebt(GetSleepDebt() + BuildConfig.WageGirlStealSleepDebt); // 偷钱外出熬夜 -N 睡眠（次日结算）
SetStat(K_LEAVE, day + 1); // 回归日 = 明天
                SetStat(K_LEAVE_REASON, 0);
                SetStat(K_LAST_STEAL, day);
                _curState = "away"; _curAnimSprites = _spAway; _stateFrameSec = 0.125f; _leavingTimer = 0.5f; // 播away挥手帧再移除
                ReportLine(LangHelper.T("蛙娘偷走了 " + steal + " 块钱，出门躲债去了（明天回来）", "Wage Girl stole " + steal + " credits and went out (back tomorrow)"));
            }
        }
        catch (System.Exception ex) { Core.LogMsg("[WageGirlSystem.NeedsAffection] 异常: " + ex.Message); }
    }

    // 任一六维 <阈值（跑路判定；CFG：WageGirlRunawayLowStat）
    private static bool IsAnyStatLow()
    {
        try
        {
            int low = BuildConfig.WageGirlRunawayLowStat;
            return GetStat(K_SAT) < low || GetStat(K_TH) < low || GetStat(K_HEALTH) < low
                || GetStat(K_MOOD) < low || GetStat(K_CLEAN) < low || GetStat(K_SLEEP) < low;
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
                        // 09-20 优化：礼物价值随好感提升（好感 0→500、100→700；#10 负好感→更廉价）
                        int minVal = 500 + (int)(GetAffectionExact() / 100.0 * 200);
                        if (g.unitValue < minVal) { pool.RemoveAt(idx); tries++; continue; } // 好物价值 ≥minVal
                        it = g; break;
                    }
                    catch { pool.RemoveAt(idx); tries++; }
                }
            }
            if (it == null) return;
            GameItem giftCrate = CreateSupplyCrate(it.unitValue, out long unusedFilled);
            if (giftCrate != null)
            {
                // 10-05 好物读档补发标记（拆包实锤：日结发放远离InitialSave→物品只在内存退出即丢；标记随档用于读档检测）
                try { ContainerUpgradeV2.AddTagInt(giftCrate, GIFT_TAG, 1); } catch { }
                AddToFront(giftCrate); ReportLine(LangHelper.T("蛙娘今天心情好，带回来一只物资箱！", "Wage Girl brought a supply crate today!"));
            }
            else
            {
                try { ContainerUpgradeV2.AddTagInt(it, GIFT_TAG, 1); } catch { }
                AddToFront(it); ReportLine(LangHelper.T("蛙娘今天心情好，带回来一件好东西！", "Wage Girl brought a nice gift today!"));
            }
        }
        catch (System.Exception ex) { Core.LogMsg("[WageGirlSystem.NeedsAffection] 异常: " + ex.Message); }
    }

    // ===== 10-05 好物读档补发（拆包实锤：好物日结发放远离 InitialSave→物品只在内存退出即丢；防重发标记K_LAST_GIFT已落盘=永久丢失）=====
    // 方案：GiveGift 发放物品打 GIFT_TAG 标记（随档）→ LoadGame 后 180 帧窗口检测：发过好物(K_LAST_GIFT>0)且后库无标记物品→补发
    // 对齐水瓶机质量读档恢复先例（PostfixLoadGameBottlePrinter + OnUpdateRestoreBottlePrinters 延迟帧重试）
    private const string GIFT_TAG = "WAGEGIRL_GIFT_TAG";
    private static int _giftRestoreFramesLeft = 0;

    // LoadGame Postfix（PatchRegistryTable 注册）：启动补发窗口
    public static void PostfixLoadGameGift()
    {
        try { _giftRestoreFramesLeft = 180; } catch { }
    }

    // Core 帧循环调用（Patches.Lifecycle）：延迟补发（读档数据轮询就绪后再判）
    public static void OnUpdateRestoreGift()
    {
        try
        {
            if (_giftRestoreFramesLeft <= 0) return;
            _giftRestoreFramesLeft--;
            if (GetStat(K_LAST_GIFT, 0) <= 0) { _giftRestoreFramesLeft = 0; return; } // 从未发过好物不补
            if (HasGiftInBackroom()) { _giftRestoreFramesLeft = 0; return; } // 后库已有标记好物（打烊入档正常）不补
            GiveGift(); // 补发（不重设 K_LAST_GIFT——SetStat 在 RunDayEvents 调用处，补发只补物品）
            _giftRestoreFramesLeft = 0;
            Core.LogMsg("[蛙娘] 读档补发好物（日结发放丢失修复）");
        }
        catch (System.Exception ex) { Core.LogMsg("[蛙娘] OnUpdateRestoreGift异常: " + ex.Message); }
    }

    // 后库（backInvinvElement 网格）是否已有标记好物
    private static bool HasGiftInBackroom()
    {
        try
        {
            var em = EmporiumEntry.Instance;
            if (em == null || em.backInvinvElement == null) return false;
            var inv = (GameInventory)em.backInvinvElement;
            if (inv == null || inv.childItems == null) return false;
            for (int i = 0; i < inv.childItems.Count; i++)
            {
                GameItem c = null;
                try { c = inv.childItems[i]; } catch { continue; }
                if (c != null && c.IsTag(GIFT_TAG)) return true;
            }
        }
        catch { }
        return false;
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
                SetStat(K_ALLOWANCE, amt); // 09-27 B5 修（总控拍板）：写 K_ALLOWANCE 当天标记——RunDayEvents 豁免/回归分支读它（此前恒 0 → 给零花钱后当天不偷断裂）
                // 09-23 新增：前三次加好感（好感 = 档位序数，CFG 改档位自动适配）
                int allowCount = GetStat(K_ALLOWANCE_COUNT);
                if (allowCount < 3) {
                    int[] steps = BuildConfig.WageGirlAllowanceSteps;
                    int idx = System.Array.IndexOf(steps, _allowanceSel);
                    int affGain = idx >= 0 ? idx + 1 : 1;
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
                int[] steps = BuildConfig.WageGirlAllowanceSteps;
                if (steps.Length == 0) return;
                int idx = System.Array.IndexOf(steps, _allowanceSel);
                if (idx < 0) idx = 0;
                _allowanceSel = steps[(idx + 1) % steps.Length];
            } catch { }
        }
}
