using System;
using Il2Cpp;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace WagePerks;

// ============================================================
// v1.3.1【8】蛙娘旅行（初版）：扩展 K_LEAVE/K_LEAVE_REASON 机制
//   reason=4：旅行3天，每天扣1份口粮（无食物自动提前回来）
//   回归：50%带钱200-500 / 20%带物品 / 30%好感+5
//   花200信用点可提前召回（立刻回来，收益减半）
// ============================================================
public static partial class WageGirlSystem
{
    internal const int TRAVEL_DAYS = 3;
    internal const int TRAVEL_CALLBACK_COST = 200;

    // 玩家点"出门旅行"按钮
    internal static void TryStartTravel()
    {
        try
        {
            if (!Exists()) return;
            if (GetStat(K_LEAVE) > 0) { ReportLine(LangHelper.T("蛙娘不在店里，返程后再旅行", "Wage Girl is out, wait for return")); return; }
            if (GetAffection() < 20) { ReportLine(LangHelper.T("好感不够，她不愿出远门（需好感≥20）", "Affection too low for travel (need ≥20)")); return; }
            // 检查口粮库存（需提前给予口粮）
            if (GetStat(K_PROVISION) < 1) { ReportLine(LangHelper.T("没有口粮，她不愿出远门（点给予口粮备1份食物）", "No provisions (give food first)")); return; }
            SetStat(K_PROVISION, GetStat(K_PROVISION) - 1);
            int day = CurrentDay();
            SetStat(K_LEAVE, day + TRAVEL_DAYS);
            SetStat(K_LEAVE_REASON, 4);
            _curState = "away"; _curAnimSprites = _spAway; _stateFrameSec = 0.125f; _leavingTimer = 0.5f;
            ReportLine(LangHelper.T("蛙娘出门旅行了（" + TRAVEL_DAYS + " 天后回来）", "Wage Girl went traveling (back in " + TRAVEL_DAYS + " days)"));
            Core.LogMsg("[蛙娘旅行] 出发");
        }
        catch (Exception ex) { Core.LogMsg("[蛙娘旅行] TryStartTravel异常: " + ex.Message); }
    }

    // 玩家花200信用点提前召回
    internal static void RecallTravel()
    {
        try
        {
            if (GetStat(K_LEAVE_REASON) != 4) return;
            var ps = PlayerStore.Instance; if (ps == null) return;
            if (ps.playerCash < TRAVEL_CALLBACK_COST) { ReportLine(LangHelper.T("钱不够，召不回她", "Not enough credits to recall")); return; }
            ps.playerCash -= TRAVEL_CALLBACK_COST;
            // 立刻回来（收益减半）
            SetStat(K_LEAVE, 0);
            SetStat(K_LEAVE_REASON, 0);
            TravelReturn(early: true);
            TryGiveToBackpack();
            ShowPanel();
        }
        catch (Exception ex) { Core.LogMsg("[蛙娘旅行] Recall异常: " + ex.Message); }
    }

    // 旅行期间每日 tick：扣1份口粮；无食物则提前结束
    internal static void TravelDailyTick(int day)
    {
        try
        {
            if (GetStat(K_LEAVE_REASON) != 4) return;
            if (GetStat(K_LEAVE) <= 0) return;
            if (GetStat(K_PROVISION) < 1)
            {
                // 10-01 修复：口粮耗尽只改 K_LEAVE 不回归 → 蛙娘永远停在 away 态
                // 正确链 = 结算奖励(TravelReturn) + 清旅行态 + 切回在店 + 放回场景 + 刷新面板
                SetStat(K_LEAVE, 0);
                SetStat(K_LEAVE_REASON, 0);
                TravelReturn(early: true);
                _curState = ""; _curAnimSprites = _spIdle; // 回 idle（状态链无"here"，空态=idle，Anim.cs:215/293 同款）
                TryGiveToBackpack();
                ShowPanel();
                ReportLine(LangHelper.T("口粮吃完了，她提前回来了", "She ran out of food and came back early"));
                Core.LogMsg("[蛙娘旅行] 口粮耗尽提前结束");
            }
            else { SetStat(K_PROVISION, GetStat(K_PROVISION) - 1); }
        }
        catch (Exception ex) { Core.LogMsg("[蛙娘旅行] DailyTick异常: " + ex.Message); }
    }

    // 旅行回归：50%带钱200-500 / 20%带物品 / 30%好感+5
    internal static void TravelReturn(bool early)
    {
        try
        {
            int roll = Core.Rng.Next(100);
            float mult = early ? 0.5f : 1.0f; // 提前召回收益减半
            if (roll < 50)
            {
                int money = (int)((200 + Core.Rng.Next(300)) * mult); // 200-500
                var ps = PlayerStore.Instance; if (ps != null) ps.playerCash += money;
                ReportLine(LangHelper.T("蛙娘旅行回来了，带了" + money + "块", "She came back from travel with " + money + " credits"));
            }
            else if (roll < 70)
            {
                // 20%带物品：进玩家背包（不是柜台），消赃物
                try
                {
                    var em = EmporiumEntry.Instance;
                    if (em != null && em.invElement != null)
                    {
                        string[] pool = { "energy_credit", "bottled_water", "processed_meat", "bandage_item", "metal_ingot" };
                        string pick = pool[Core.Rng.Next(pool.Length)];
                        GameItem it = DirectoryMaster.Item(pick, true);
                        if (it != null)
                        {
                            try { it.DisableTag("STOLEN_TAG"); } catch { }
                            try { it.DisableTag("CONTRABAND_TAG"); } catch { }
                            var slot = em.invElement.TryFindOneValidInventorySlot(it, false);
                            if (slot != null) { try { slot.TryAcceptOnce(); } catch { em.invElement.UncheckedAccept(it); } }
                            else em.invElement.UncheckedAccept(it);
                        }
                    }
                }
                catch { }
                ReportLine(LangHelper.T("蛙娘旅行回来了，带了件小礼物", "She came back from travel with a small gift"));
            }
            else
            {
                SetAffection(GetAffection() + 5);
                ReportLine(LangHelper.T("蛙娘旅行回来了，心情很好（好感+5）", "She came back from travel, in great mood (+5 affection)"));
            }
            Core.LogMsg("[蛙娘旅行] 回归 early=" + early);
        }
        catch (Exception ex) { Core.LogMsg("[蛙娘旅行] Return异常: " + ex.Message); }
    }

    // 玩家点"给予口粮"按钮：从仓库扣1份食物存入口粮库存
    internal static void GiveProvision()
    {
        try
        {
            if (!Exists()) return;
            if (!FindAndConsumeFood()) { ReportLine(LangHelper.T("背包没有食物", "No food in stock")); return; }
            SetStat(K_PROVISION, GetStat(K_PROVISION) + 1);
            ReportLine(LangHelper.T("已备好1份口粮（当前" + GetStat(K_PROVISION) + "份）", "Stored 1 provision (total: " + GetStat(K_PROVISION) + ")"));
            ShowPanel();
        }
        catch (Exception ex) { Core.LogMsg("[蛙娘旅行] GiveProvision异常: " + ex.Message); }
    }

    // 找玩家仓库一件食物删掉，成功返回true
    private static bool FindAndConsumeFood()
    {
        try
        {
            var em = EmporiumEntry.Instance; if (em == null) return false;
            var all = em.GetAllItems();
            foreach (var it in all)
            {
                if (it == null) continue;
                bool isFood = false;
                try { isFood = it.IsTag("FOOD_TAG"); } catch { }
                if (!isFood) continue;
                try { it.parentInventory?.Expel(it); } catch { }
                try { it.Destroy(); } catch { }
                Core.LogMsg("[蛙娘旅行] 消耗口粮: " + (it.identifier ?? "?"));
                return true;
            }
        }
        catch (Exception ex) { Core.LogMsg("[蛙娘旅行] FindFood异常: " + ex.Message); }
        return false;
    }
}
