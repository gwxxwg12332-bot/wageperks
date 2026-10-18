using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace WagePerks;

public static partial class WageGirlSystem
{
    // 延迟销毁列表：拖拽栈内不直接Destroy，挂pending，帧尾统一Destroy
    // 修复：拖拽事件栈内item.Destroy() + 多mod叠加hook = use-after-free闪退
    private static List<GameItem> _pendingDestroy = new List<GameItem>();

    // 10-10 #39：随档清理（读档/新档各挂点调用——跨档残留引用=上档对象已随场景销毁，直接 Clear 不 Destroy）
    internal static void ClearPendingDestroy()
    {
        try { _pendingDestroy.Clear(); } catch (System.Exception ex) { Core.LogMsg("[WageGirlSystem.Interact] ClearPendingDestroy 异常: " + ex.Message); }
    }

    // 加入延迟销毁列表（拖拽栈内调用）
    internal static void QueueDestroy(GameItem item)
    {
        try
        {
            if (item != null && !_pendingDestroy.Contains(item))
                _pendingDestroy.Add(item);
        }
        catch (System.Exception ex) { Core.LogMsg("[WageGirlSystem.Interact] QueueDestroy 异常: " + ex.Message); }
    }

    // 帧尾统一处理销毁（FrameUpdate调用）
    internal static void ProcessPendingDestroy()
    {
        try
        {
            if (_pendingDestroy.Count == 0) return;
            foreach (var item in _pendingDestroy)
            {
                try { if (item != null) item.Destroy(); }
                catch (System.Exception ex) { Core.LogMsg("[WageGirlSystem.Interact] 帧尾Destroy失败: " + ex.Message); }
            }
            _pendingDestroy.Clear();
        }
        catch (System.Exception ex) { Core.LogMsg("[WageGirlSystem.Interact] ProcessPendingDestroy 异常: " + ex.Message); }
    }

    public static bool PrefixMayTarget(GameItem __instance, GameItem targetItem, ref bool __result)
    {
        try
        {
            if (__instance == null || targetItem == null) return true;
            if (IsGirl(targetItem) && CanFeed(__instance) && IsPlayerOwnedForCare(__instance)) { __result = true; return false; } // hover 可拖（只允许玩家自己的物品）
        }
        catch (System.Exception ex) { Core.LogMsg("[WageGirlSystem.Interact] 异常: " + ex.Message); }
        return true;
    }
    public static bool PrefixCanTarget(GameItem __instance, GameItem targetItem, ref bool __result)
    {
        return PrefixMayTarget(__instance, targetItem, ref __result);
    }
    public static bool PrefixTarget(GameItem __instance, GameItem targetItem)
    {
        try
        {
            if (__instance == null || targetItem == null) return true;
            if (!IsGirl(targetItem)) return true;
            if (!IsDragRelease()) return true;
            if (!IsPlayerOwnedForCare(__instance)) return true; // 货架商品不能喂
            if (TryFeed(__instance, targetItem)) return false; // 喂食成功：拦截原生放入
        }
        catch (System.Exception ex) { Core.LogMsg("[WageGirlSystem.Interact] 异常: " + ex.Message); }
        return true;
    }
    private static bool IsDragRelease()
    {
        try { var h = Il2Cpp.ItemMouseDragHandler.current; return h != null && h.IsDraggingItem; } catch { return false; }
    }
    private static bool IsGirl(GameItem it)
    {
        // 09-26 修：identifier 读档丢失兜底（拆包实锤见 Anim.cs L452 同款 TAG 兜底）
        try { return it != null && (it.identifier == ENTITY_ID || it.IsTag(TAG)); } catch { return false; }
    }
    private static bool CanFeed(GameItem item)
    {
        try
        {
            // 10-18 修复「普通物品拖不到蛙娘身上/拒绝交互」：PrefixMayTarget 的 hover 可拖判定用 CanFeed，
            //   原实现只认食物/饮料/日常/违禁品 → 寄卖/交换/口粮模式下普通物品 hover 不亮、拖不动。
            //   模式开启时对玩家拥有的物品放行（具体收不收/定价由 TryFeed 内部按模式裁决）。
            if (ConsignMode || ExchangeMode || ProvisionMode)
                return IsPlayerOwnedForCare(item);
            return RobinCrusoePerk.IsFood(item) || RobinCrusoePerk.IsDrink(item) || RobinCrusoePerk.IsDailyNeed(item) || IsContraband(item);
        }
        catch { return false; }
    }
    internal static bool ProvisionMode = false; // 按钮切换：拖食物到蛙娘身上 = 存口粮
    internal static bool ExchangeMode = false; // 10-16 M10：按钮切换：拖物品给蛙娘 = 以物易物（每日 1 次）
    internal static bool ConsignMode = false;  // 2026-10-18 §2：按钮切换：拖物品给蛙娘 = 寄卖（吞噬瞬间写记录，次日结算）
    private static bool TryFeed(GameItem item, GameItem girl)
    {
        try
        {
            if (item == null) return false;
            if (GetStat(K_LEAVE) > 0) return false; // 10-02 修：外出中（leave>0）禁止喂食/洗白/照顾六维

            // 口粮拖拽模式：拖食物到蛙娘身上 → 存入口粮库存，不喂食
            if (ProvisionMode)
            {
                bool isFood = false; try { isFood = RobinCrusoePerk.IsFood(item); } catch { }
                if (isFood)
                {
                    SetStat(K_PROVISION, GetStat(K_PROVISION) + 1);
                    QueueDestroy(item); // 延迟销毁：拖拽栈内不直接Destroy，帧尾统一处理（防多mod叠加hook use-after-free闪退）
                    ProvisionMode = false;
                    ReportLine(LangHelper.T("已收1份口粮（当前" + GetStat(K_PROVISION) + "份）", "Stored 1 provision (total: " + GetStat(K_PROVISION) + ")"));
                    ShowPanel();
                    return true;
                }
                ProvisionMode = false; // 拖的不是食物：退出模式走正常喂食
            }
            // 10-16 M10：以物易物模式（面板按钮切换；每日 1 次；投入价值 1~500 非违禁品；回礼系数按好感档 + 喜好命中 +0.2）
            if (ExchangeMode)
            {
                ExchangeMode = false; // 先退模式：判定失败不锁死后续拖拽
                if (IsContraband(item))
                {
                    ReportLine(LangHelper.T("蛙娘：违禁品不交换，请拖给她洗白或销赃", "Wage Girl: contraband is not for barter, launder or fence it instead"));
                    return true;
                }
                long exchVal = 0;
                try { exchVal = item.GetCurrentValue(); } catch { }
                if (exchVal <= 0) { try { exchVal = item.unitValue; } catch { } }
                if (exchVal <= 0 || exchVal > 500)
                {
                    ReportLine(LangHelper.T("蛙娘：只收价值 1~500 的普通物品（" + exchVal + " 不合格）", "Wage Girl: only takes items worth 1~500 (" + exchVal + " rejected)"));
                    return true;
                }
                if (GetStat(K_EXCHANGE_DAY) == CurrentDay())
                {
                    ReportLine(LangHelper.T("蛙娘：今天已经交换过啦，明天再来", "Wage Girl: traded once today, come back tomorrow"));
                    return true;
                }
                double exchMult = GetAffectionExact() < 30 ? 0.8 : GetAffectionExact() < 60 ? 1.0 : 1.2; // 好感档
                int likeNow = GetStat(K_LIKE);
                if (likeNow >= 0 && likeNow <= 2 && ItemLikeCategory(item) == likeNow) exchMult += 0.2; // 喜好命中
                long giftVal = (long)System.Math.Round(exchVal * exchMult);
                QueueDestroy(item);
                SetStat(K_EXCHANGE_DAY, CurrentDay());
                GiveExchangeGift(giftVal);
                ReportLine(LangHelper.T("蛙娘收下交换物（价值 " + exchVal + "，好感系数 ×" + exchMult.ToString("0.0") + "），回礼价值约 " + giftVal, "Wage Girl traded (value " + exchVal + ", affection x" + exchMult.ToString("0.0") + "), return gift ~" + giftVal));
                ShowPanel();
                return true;
            }
            // 2026-10-18 §2/§3：寄卖模式（面板按钮切换；好感≥50；每日上限 DailyQuota；吞噬瞬间写 Consign.<seq>）
            // 裁决表（§3.2 九行 + 审阅 20261018 补 P2-17 食物行）在 QueueDestroy 之前逐项判定：命中拒收/定价后吞噬
            if (ConsignMode)
            {
                // P0-1（审阅 20261018）：规则名单一次性读入——旧自动代售段删后 BeginEvaluate 无调用点，名单层 _rulesLoaded=false 恒失效
                //   （Mode=1 白名单 → 全部 NotAllowed 锁死；Mode=0 黑名单 → 免售名单无效）
                try { WageGirlSellRules.BeginEvaluate(); } catch { }
                // 总开关（2.2 配置平移：关=拖入拒收；模式不可用 → 退模式，P0-2 审阅）
                if (!WageGirlSellRules.AutoSellOn)
                {
                    ConsignMode = false;
                    ReportLine(LangHelper.T("蛙娘：寄卖总开关是关着的", "Wage Girl: consignment is turned off"));
                    return true;
                }
                // 门槛（2.2 第 4 条）：好感 ≥50（节点4+；P1-4 审阅：原 ==4 在好感≥100 节点5 时误锁）
                if (GetAffNode(GetAffectionExact()) < 4)
                {
                    ConsignMode = false;
                    ReportLine(LangHelper.T("蛙娘：感情还不够深，还不放心让我帮你卖东西", "Wage Girl: not close enough to trust me with consignment"));
                    return true;
                }
                // 每日上限（2.2 第 5 条；额度满 → 退模式，P0-2 审阅）
                int todayFeed = CurrentDay();
                if (WageGirlConsign.CountFedToday(todayFeed) >= WageGirlSellRules.DailyQuota)
                {
                    ConsignMode = false;
                    ReportLine(LangHelper.T("蛙娘：今天的寄卖额度用完了，明天再来", "Wage Girl: daily consignment quota reached, come back tomorrow"));
                    return true;
                }
                // ===== §3.2 裁决表（命中拒收保持模式——玩家可连续拖，P0-2 审阅） =====
                // 1) 店货守卫【10-18 拆包定案后删除】：not_purchased/TAG_NOT_PURCHASED 双查是误伤源+死代码——
                //    ① TAG_NOT_PURCHASED 原版 0 注册幽灵 tag（恒 false）；② 柜台货（isOwend=false）在拖拽入口
                //    PrefixTarget:69 已被 IsPlayerOwnedForCare 拦截，根本进不了 TryFeed→白嫖通道不存在；
                //    ③ 残留 not_purchased 的物品（mod 13 处发放都在 DisableTag 清它=确实有物品带）会被误拒=「很多物品不能收取」。
                //    寄卖只收能拖进来的（=玩家拥有的）物品；防白嫖由 :69 兜底。
                // 2) 10-18 用户拍板：违禁品不再强制走销赃——洗白 / 销赃 / 寄卖 三条路并存，由玩家拖拽时选的模式决定
                //    违禁品寄卖按「来路不干净」口径折价（见下方第 6 行），避免变成高价洗钱通道
                // 3) 10-18 用户拍板：寄卖不限定物品——特殊 tag / 食物 / 饮料 / 白名单模式一律不再拒收
                //    仅保留两条硬拦：① unitValue<=0（无价可估）② 免售名单（玩家意图）；店货守卫见上（已删）
                // 4) unitValue <= 0
                long uv = 0; try { uv = (int)item.unitValue; } catch { }
                if (uv <= 0)
                {
                    ReportLine(LangHelper.T("不值钱", "Worthless"));
                    return true;
                }
                // 5) 只保留「免售名单」；Resolve 的 NotAllowed（白名单模式）在手动寄卖下不生效
                {
                    var dec = WageGirlSellRules.Resolve(item, out string why);
                    if (dec == SellDecision.Protected)
                    {
                        ReportLine(LangHelper.T("你把它标记为免售了", "Marked as no-sell"));
                        return true;
                    }
                }
                // 6) 赃物 / 违禁品：门路白手套（§8.1-1）可用 → 按原价收（flags=C，不折价、不退货风险）；
                //    否则维持 60% + S（10-18 用户拍板：违禁品与赃物同口径折价，防高价洗钱）
                string flags = "";
                long baseVal = uv;
                bool contraband = false;
                try { contraband = Il2Cpp.ContrabandHelper.GetContrabandLevel(item) > 0; } catch { }
                if (ItemAuthenticity.IsStolen(item) || contraband)
                {
                    int connFee = 0; string connWhy = "";
                    bool connOk = false;
                    try { connOk = WageGirlConn.TryUse(item, true, out connFee, out connWhy); } catch { }
                    if (connOk)
                    {
                        flags += "C"; // 门路白手套：原价收
                        ReportLine(LangHelper.T("门路白手套：按原价收（门路费 " + connFee + "%）", "Connections: full price (fee " + connFee + "%)"));
                    }
                    else
                    {
                        baseVal = (long)Math.Round(uv * 0.6);
                        if (baseVal < 1) baseVal = 1;
                        flags += "S";
                        ReportLine(contraband
                            ? LangHelper.T("违禁品按赃物口径折价收", "Contraband accepted at stolen-item rate")
                            : LangHelper.T("来路不干净，卖不上价", "Sketchy provenance, lower price"));
                    }
                }
                // 7) 假货：以假乱真（§8.1-2）可用 → 不折价（flags=F0，不触发投诉风险）；
                //    否则维持 (100+mod)% + F；已走 C 的违禁品假货只补标志不再重复判定/扣额度
                //    P1-7 审阅：S+F 叠加 = 赃物 60% 后再乘假货 modifier（不再覆盖；日报标注见 WageGirlConsign.cs）
                string pct = "";
                if (ItemAuthenticity.IsFake(item))
                {
                    if (flags.Contains("C"))
                    {
                        flags += "+F0"; // 违禁品门路已原价，假货不重复判定
                    }
                    else
                    {
                        int connFee2 = 0; string connWhy2 = "";
                        bool connOk2 = false;
                        try { connOk2 = WageGirlConn.TryUse(item, false, out connFee2, out connWhy2); } catch { }
                        if (connOk2)
                        {
                            flags += "F0"; // 门路假货：不折价
                            pct = "100";
                            ReportLine(LangHelper.T("以假乱真：按原价收（门路费 " + connFee2 + "%）", "Connections: fake at full price (fee " + connFee2 + "%)"));
                        }
                        else
                        {
                            int mod = ItemAuthenticity.FakeModifier(item);
                            baseVal = (long)Math.Round(baseVal * (100 + mod) / 100.0);
                            if (baseVal < 1) baseVal = 1;
                            if (!flags.Contains("S")) flags += "F"; else flags += "+F";
                            pct = (100 + mod).ToString();
                            ReportLine(LangHelper.T("买家看不出来算你运气", "Hope the buyer doesn't notice"));
                        }
                    }
                }
                // 9) 其余：baseValue = unitValue，flags 为空（已在上方默认）
                // 写入记录（吞噬瞬间快照；P0-3 审阅：写入失败不销毁，物品退回）+ 销毁
                string itemName = ModCannibalism.GetName(item);
                string itemId = ""; try { itemId = item.identifier ?? ""; } catch { }
                int seq = WageGirlConsign.Add(itemId, itemName, baseVal, flags, todayFeed, pct);
                if (seq < 0)
                {
                    ReportLine(LangHelper.T("寄卖记录失败，物品已退回", "Failed to record consignment, item returned"));
                    return true; // 不 QueueDestroy——防资产凭空消失（P0-3）
                }
                QueueDestroy(item);
                ReportLine(LangHelper.T("蛙娘收下了「" + itemName + "」（寄卖估值 " + baseVal + (string.IsNullOrEmpty(flags) ? "" : "，带" + flags + "标志") + "）", "Wage Girl took \"" + itemName + "\" for consignment (est. " + baseVal + (string.IsNullOrEmpty(flags) ? "" : ", flagged " + flags) + ")"));
                ShowPanel(); // 保持 ConsignMode=true（P0-2：可连续拖多件，面板刷新计数）
                return true;
            }
            // 09-23 修复「蛙娘只有在不营业时才可被照顾」：旧代码在交易 UI 打开时一律 return false，
            // 而营业期间柜台接客几乎全程开交易 UI → 照顾（喂食/喝水/清洁）实际只在打烊后可用。
            // 改为：违禁品分支保持"交易中禁止"（与原行为一致），照顾分支放行（见下方归属校验）。
            // 09-23 改：违禁品 → 按模式分流（洗白 / 销赃）
            if (IsContraband(item))
            {
                if (Patches.CurrentUITradeMode != 0) return false; // 交易中不洗白/不销赃（保持原行为）
                int mode = GetStat(K_WASH_MODE);
                if (mode == 0) {
                    // 洗白模式
                    int lvl = 0;
                    try { lvl = Il2Cpp.ContrabandHelper.GetContrabandLevel(item); } catch { lvl = 1; }
                    if (lvl <= 0) lvl = 1;
                    int washCost = lvl * BuildConfig.WageGirlWashCostPerLevel;
                    // 10-16 M7：蛙娘卡 Lv3 洗白大师 → 消耗减半（成本侧质变，不进议价/销赃乘法链）
                    if (B2ServiceStore.GetEffectLevel(WageGirlPerk.PerkId) >= 3)
                        washCost = (int)(washCost * BuildConfig.WageGirlWashMasterMult);
                    int savings = GetStat(K_SAVINGS);
                    if (savings < washCost) {
                        ReportLine(LangHelper.T("蛙娘：小金库余额不足（洗白需要 " + washCost + " 块）", "Wage Girl: not enough savings (need " + washCost + ")"));
                        try { Il2Cpp.StoreUIManager.Instance.Notify(LangHelper.T("小金库余额不足", "Not enough savings"), "orange"); } catch { }
                        return false;
                    }
                    SetStat(K_SAVINGS, savings - washCost);
                    try { Il2Cpp.ContrabandHelper.RemoveContrabandStatus(item); try { item.EnableTag("wage_washed", true); } catch { } } catch { }
                    try { item.shortDescription = (item.shortDescription ?? "") + LangHelper.T("【被蛙哥的大手洗白】", "[Laundered by Wage's big hand]"); } catch { }
                    string itemName = ModCannibalism.GetName(item);
                    ReportLine(LangHelper.T("蛙娘把 " + itemName + " 洗白了（L" + lvl + " -" + washCost + " 块小金库）", "Wage Girl laundered " + itemName + " (L" + lvl + " -" + washCost + " savings)"));
                    try { if (Il2Cpp.CustomUIManager.Instance != null && Il2Cpp.CustomUIManager.Instance.IsOpen("wage_girl_panel")) ShowPanel(); } catch { }
                    SetAnimMode(2);
                    return true;
                } else {
                    // 销赃模式（累计待销赃）
                    long v = 0;
                    try { v = (int)item.GetCurrentValue(); } catch { }
                    if (v <= 0) { try { v = item.unitValue; } catch { } }
                    if (v <= 0) return false;
                    bool _destroyed = false;
                    QueueDestroy(item); _destroyed = true; // 延迟销毁：拖拽栈内不直接Destroy，帧尾统一处理
                    Core.LogMsg("[蛙娘销赃] 吃掉 " + (item.identifier ?? "?") + " v=" + v + " destroyed=" + _destroyed);
                    int cur = GetStat(K_FENCE_AMT);
                    int total = cur + (int)v;
                    SetStat(K_FENCE_AMT, total);
                    ReportLine(LangHelper.T("蛙娘吃下了违禁品（累计 " + total + " 价值待销赃——点面板「销赃」出发）", "Wage Girl devoured contraband (" + total + " to fence - press Fence)"));
                    try { if (Il2Cpp.CustomUIManager.Instance != null && Il2Cpp.CustomUIManager.Instance.IsOpen("wage_girl_panel")) ShowPanel(); } catch { }
                    SetAnimMode(2);
                    return true;
                }
            }
            // 09-23：营业期间允许照顾，但只许使用玩家自己的物品——
            // 依据 [L1] GeneralHelper.IsItemOwned(item) ≡ item.IsTag("IS_OWNED_TAG")（原版归属标记，Patches.cs:1984 已用它判买卖方向）。
            // 买入模式下拖的是客户的货，喂掉会让原生交易 UI 持有已销毁物品 → 拦掉。
            if (Patches.CurrentUITradeMode != 0 && !IsPlayerOwnedForCare(item)) return false;
            // #10 拍板 3：节点1 敌意[-200,-101] = 不互动（喂食/照顾拒绝）
            if (GetAffNode(GetAffectionExact()) == 1)
            {
                ReportLine(LangHelper.T("蛙娘正在气头上，拒绝你的照顾", "She is furious and refuses your care"));
                try { Il2Cpp.StoreUIManager.Instance.Notify(LangHelper.T("好感跌到冰点，她不愿接受照顾", "Affection too low, she refuses care"), "orange"); } catch { }
                return false;
            }
            int gain = 0; double aff = 1; string msg = ""; string notifColor = ""; // 10-07 C：Notify 颜色（优质水绿/浑浊橙/脏水红）
            // #10 5 节点映射：节点1/2（冷淡/敌意）=1、节点3（友好）=2、节点4/5（亲密/信赖）=1（阶段制保底，酒分支用）
            int curNode = GetAffNode(GetAffectionExact());
            int affBase = curNode <= 2 ? 1 : (curNode == 3 ? 2 : 1);
            // 10-06 D3（525BFCC1 拍板）：喂食/喂水改按物品价值给好感（价值越高加越多）+ 双倍难度
            // 10-07 #10 拍板 4：两位小数档（300+/2.00、150+/1.50、60+/1.00、20+/0.50、1-19/0）+ 价值 0 物品 -1.00（防刷）
            double ValueAff(GameItem it)
            {
                long v = 0; try { v = it.GetCurrentValue(); } catch { }
                if (v <= 0) { try { v = it.unitValue; } catch { } }
                if (v <= 0) return -1.00;
                if (v >= 300) return 2.00;
                if (v >= 150) return 1.50;
                if (v >= 60) return 1.00;
                if (v >= 20) return 0.50;
                return 0.00;
            }
            if (RobinCrusoePerk.IsDailyNeed(item)) { gain = 20; aff = curNode <= 2 ? 2 : (curNode <= 4 ? 4 : 2); msg = LangHelper.T("蛙娘洗得干干净净、心情大好！清洁 +20 心情 +10（照顾）", "Wage Girl cleaned up & cheered up! Cleanliness +20 Mood +10 (care)"); SetStat(K_CLEAN, GetStat(K_CLEAN) + gain); SetStat(K_MOOD, GetStat(K_MOOD) + 10); SetStat(K_HEALTH, GetStat(K_HEALTH) + 15); QueueDestroy(item); } // 延迟销毁：拖拽栈内不直接Destroy，帧尾统一处理
            else if (RobinCrusoePerk.IsFood(item)) {
                // 普通食物：GetCalLeft → bite=min(100,(cal+1)/2) → gain=round(bite/22)
                int cal2 = RobinCrusoePerk.GetCalLeft(item);
                if (cal2 <= 0) { QueueDestroy(item); return true; }
                int bite = cal2 <= 100 ? cal2 : Math.Max(100, (cal2 + 1) / 2);
                int left = cal2 - bite;
                gain = Math.Max(1, (int)Math.Round(bite / 22f));
                SetStat(K_SAT, Math.Min(100, GetStat(K_SAT) + gain));
                SetStat(K_HEALTH, Math.Max(0, GetStat(K_HEALTH) + 15));
                aff = ValueAff(item); // 10-06 D3：按价值好感+双倍难度（原 affBase 阶段制）
                msg = LangHelper.T("蛙娘吃饱了！饱食 +" + gain, "Wage Girl ate! Satiety +" + gain);
                // 吃完才 Destroy，剩→SetCalLeft+EATEN_TAG
                if (left <= 0) { QueueDestroy(item); }
                else { RobinCrusoePerk.SetCalLeft(item, left); try { item.EnableTag("EATEN_TAG", true); } catch { } }
            }
            else if (RobinCrusoePerk.IsDrink(item)) {
                // 水：GetWaterMl → sip=min(200,ml) → purity 5 档
                int ml = RobinCrusoePerk.GetWaterMl(item);
                if (ml <= 0) {
                    // 09-26 修：ml<=0 时只有酒能喂，其他（空瓶/空水瓶）不能喂
                    string id = "";
                    try { id = item.identifier; } catch { }
                    bool isAlcohol = id == "red_beer" || id == "nudka" || id == "galaxy_blend" || id == "whiskey" || id == "vodka";
                    if (!isAlcohol) return false; // 空瓶/空水瓶：不能喂

                    // 酒 → 按价值回口渴，喝完销毁酒瓶
                    long dval = 0;
                    try { dval = item.GetCurrentValue(); } catch { }
                    if (dval <= 0) { try { dval = item.unitValue; } catch { } }
                    gain = dval >= 300 ? 25 : dval >= 150 ? 18 : dval >= 60 ? 12 : dval >= 20 ? 6 : 2;
                    SetStat(K_TH, Math.Min(100, GetStat(K_TH) + gain));
                    aff = affBase;
                    msg = LangHelper.T("蛙娘喝了一杯！口渴 +" + gain + "（按价值 " + dval + "）", "Wage Girl had a drink! Thirst +" + gain + " (value " + dval + ")");
                    QueueDestroy(item); // 延迟销毁：酒喝完销毁酒瓶
                }
                else {
                    int sip = Math.Min(200, ml);
                    int purity = -1; try { purity = Il2Cpp.WaterHelper.GetWaterPurity(item); } catch { }
                    int tier = purity >= 9900 ? 0 : purity >= 9600 ? 1 : purity >= 9200 ? 2 : purity >= 8800 ? 3 : 4;
                    int baseGain = new[] { 25, 18, 12, 6, 2 }[tier];
                    gain = (int)Math.Round(baseGain * (sip / 200f));
                    if (gain < 1) gain = 1;
                    int hd = new[] { 5, 2, 0, -5, -10 }[tier];
                    string wname = new[] { "优质", "较好", "普通", "浑浊", "脏水" }[tier];
                    // 10-06 芷昕方案（用户拍板）：蛙娘口渴满喝水=洗澡（同步 WP 洗澡方案）——
                    //   清洁按质（20/12/6/2/-5）；优质额外 +心情+睡眠；浑浊/脏水额外 -心情；脏水额外 -健康；仍扣对应水量
                    if (GetStat(K_TH) >= 100)
                    {
                        aff = ValueAff(item); // 10-06 D3：按价值好感+双倍难度
                        int cg = new[] { 20, 12, 6, 2, -5 }[tier];
                        SetStat(K_CLEAN, Math.Max(0, Math.Min(100, GetStat(K_CLEAN) + cg)));
                        if (tier == 0)
                        {
                            SetStat(K_MOOD, Math.Min(100, GetStat(K_MOOD) + BuildConfig.WageBathMoodBonus));
                            SetStat(K_SLEEP, Math.Min(100, GetStat(K_SLEEP) + BuildConfig.WageBathSleepBonus));
                            notifColor = "green"; // 10-07 C：优质水绿 Notify
                        }
                        else if (tier == 3) { SetStat(K_MOOD, Math.Max(0, GetStat(K_MOOD) - 2)); notifColor = "orange"; }
                        else if (tier == 4) { SetStat(K_MOOD, Math.Max(0, GetStat(K_MOOD) - 5)); SetStat(K_HEALTH, Math.Max(0, Math.Min(100, GetStat(K_HEALTH) - 5))); notifColor = "red"; } // 10-07 C：脏水红 Notify
                        msg = cg >= 0 ? LangHelper.T("蛙娘口渴满了，洗澡：清洁 +" + cg + "（" + wname + "）", "Wage Girl full, bathing: Cleanliness +" + cg + " (" + wname + ")")
                                     : LangHelper.T("蛙娘口渴满了，脏水洗澡：清洁 " + cg + "（" + wname + "）", "Wage Girl full, dirty bath: Cleanliness " + cg + " (" + wname + ")");
                        try { WaterHelper.Remove(item, sip * 1000); } catch { }
                    }
                    else
                    {
                        aff = ValueAff(item); // 10-06 D3：按价值好感+双倍难度
                        SetStat(K_TH, Math.Min(100, GetStat(K_TH) + gain));
                        if (hd != 0) SetStat(K_HEALTH, Math.Max(0, Math.Min(100, GetStat(K_HEALTH) + hd)));
                        msg = LangHelper.T("蛙娘喝饱了！口渴 +" + gain + "（" + wname + "）", "Wage Girl drank! Thirst +" + gain + " (" + wname + ")");
                        // 不 Destroy，瓶子留（带剩余水）
                        try { WaterHelper.Remove(item, sip * 1000); } catch { }
                    }
                }
            }
            else return false;
            // 10-16 M5：今日喜好命中 → 好感 ×2、心情 +10（喜好类别 0=酒 1=食物 2=日用品；类别判定与喂食分支同序）
            int like = GetStat(K_LIKE);
            if (like >= 0 && like <= 2 && ItemLikeCategory(item) == like)
            {
                aff *= 2;
                SetStat(K_MOOD, Math.Min(100, GetStat(K_MOOD) + 10));
                msg = msg + LangHelper.T("（她超喜欢！好感×2 心情+10）", " (She loves it! Affection x2, Mood +10)");
            }
            SetAffectionExact(GetAffectionExact() + aff); // 10-07 #10：两位小数精确写入（int×100 存储）
            SetStat("lastFedDay", CurrentDay()); // 记录今天喂过
            // 10-07 测试反馈 C（玩家"水洗面板提示未显示"）：Notify 带颜色（优质水绿/脏水红）+ null 检查（打烊无交易 UI 时
            //   StoreUIManager.Instance 可能为 null → 原 try/catch 吞异常=静默无提示）+ 日志确认是否走到
            try
            {
                var storeUi = Il2Cpp.StoreUIManager.Instance;
                if (storeUi != null)
                {
                    if (string.IsNullOrEmpty(notifColor)) storeUi.Notify(msg);
                    else storeUi.Notify(msg, notifColor);
                }
                else { Core.LogMsg("[蛙娘] 喂食 Notify 跳过：StoreUIManager 为空（打烊场景） msg=" + msg); }
            }
            catch (System.Exception exN) { Core.LogMsg("[蛙娘] 喂食 Notify 异常: " + exN.Message); }
            try { if (Il2Cpp.CustomUIManager.Instance != null && Il2Cpp.CustomUIManager.Instance.IsOpen("wage_girl_panel")) ShowPanel(); } catch { }
            SetAnimMode(2); // 09-22 吃掉瞬间切偷动画（播完回待机）
            return true;

            // 10-16 M5：喜好类别判定（与喂食分支同序：日用品→食物→酒；零件无喂食交互路径→不入选，类目按文档标注修正）
            int ItemLikeCategory(GameItem it)
            {
                if (it == null) return -1;
                try { if (RobinCrusoePerk.IsDailyNeed(it)) return 2; } catch { }
                try { if (RobinCrusoePerk.IsFood(it)) return 1; } catch { }
                string iid = ""; try { iid = it.identifier; } catch { }
                if (iid == "red_beer" || iid == "nudka" || iid == "galaxy_blend" || iid == "whiskey" || iid == "vodka") return 0;
                return -1;
            }
        }
        catch (Exception ex) { Core.LogMsg("[蛙娘] 喂食异常: " + ex.Message); return false; }
    }
    private static bool IsPlayerOwnedForCare(GameItem item)
    {
        try { if (item == null) return false; } catch { return false; }
        try { return Il2Cpp.GeneralHelper.IsItemOwned(item); } catch { }
        try { return item.IsTag("IS_OWNED_TAG"); } catch { }
        return false;
    }
    private static float _lastDoubleClickLog = 0;
    public static void PostfixDoubleClickAction(GameItem newItem, Vector2 mousePosition)
    {
        // 09-24 双击粘滞节流：1 秒内只打一次日志（原生 DoubleClickAction 可能每帧调多次）
        if (UnityEngine.Time.time - _lastDoubleClickLog > 1f)
        {
            _lastDoubleClickLog = UnityEngine.Time.time;
            Core.LogMsg("[双击] WageGirl Postfix 触发: " + (newItem != null ? (newItem.name + " id=" + newItem.identifier) : "null"));
        }
        try
        {
            if (newItem == null) return;
            if (!IsGirl(newItem)) return; // 09-26 修：identifier 读档丢失兜底（同 IsGirl TAG 兜底）
            ShowPanel();
        }
        catch (System.Exception ex) { Core.LogMsg("[WageGirlSystem.Interact] 异常: " + ex.Message); }
    }
}
