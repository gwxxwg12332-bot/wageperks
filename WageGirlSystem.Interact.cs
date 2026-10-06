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

    // 加入延迟销毁列表（拖拽栈内调用）
    internal static void QueueDestroy(GameItem item)
    {
        try
        {
            if (item != null && !_pendingDestroy.Contains(item))
                _pendingDestroy.Add(item);
        }
        catch { }
    }

    // 帧尾统一处理销毁（FrameUpdate调用）
    internal static void ProcessPendingDestroy()
    {
        try
        {
            if (_pendingDestroy.Count == 0) return;
            foreach (var item in _pendingDestroy)
            {
                try { if (item != null) item.Destroy(); } catch { }
            }
            _pendingDestroy.Clear();
        }
        catch { }
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
        try { return RobinCrusoePerk.IsFood(item) || RobinCrusoePerk.IsDrink(item) || RobinCrusoePerk.IsDailyNeed(item) || IsContraband(item); } catch { return false; }
    }
    internal static bool ProvisionMode = false; // 按钮切换：拖食物到蛙娘身上 = 存口粮
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
                    int washCost = lvl * 50;
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
