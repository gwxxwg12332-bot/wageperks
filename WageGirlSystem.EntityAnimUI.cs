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

    // ===================== 实体注册（Patches.PostfixInitDirectory 调） =====================
    public static void RegisterToDirectory(ItemDirectory dir)
    {
        try
        {
            if (dir == null) return;
            ((Directory<GameItem>)(object)dir).Add(ENTITY_ID, (Il2CppSystem.Func<GameItem>)(() => CreateWageGirl()));
        }
        catch (Exception ex) { Core.LogMsg("[蛙娘] 注册失败: " + ex.Message); }
    }

    private static GameItem CreateWageGirl()
    {
        try
        {
            GameItem it = ItemDirectory.CreateEmptyItem(null);
            if (it == null) return null;
            ApplyIcon(it);
            it.EnableTag(TAG);
            it.SetName(LangHelper.T("蛙娘", "Wage Girl"));
            it.identifier = ENTITY_ID; // 公开 setter（照骰子先例）——identifier 随档
            it.identifierName = "TYPE-STRING_" + ENTITY_ID; // 公开 setter
            it.shortDescription = LangHelper.T("蛙娘——蛙哥留下的仿生女仆。她会自己吃喝、干活，心情不好还会偷拿你的钱和货。但只要你好好照顾她，她会越来越信任你——从刚来时偷你100块，到后来只偷你个小零食；从站在角落不理你，到粘在你身边帮你抬价、叫客、销赃。双击打开她的状态面板。", "Wage Girl — an android maid left by Wage. She eats, works, and steals when moody. But take care of her, and she'll trust you more — from stealing 100 credits on day one to just a snack later; from hiding in the corner to standing by your side, boosting prices, calling customers, and fencing goods. Double-click to open her status panel.");
            it.longDescription = it.shortDescription;
            it.unitValue = 0; it.unitBaseValue = 0; // 09-19 价值归零：客户不买
            return it;
        }
        catch (Exception ex) { Core.LogMsg("[蛙娘] 创建失败: " + ex.Message); return null; }
    }

    private static void ApplyIcon(GameItem it)
    {
        try { it.SetSpriteAndShape(ICON_ATLAS, ICON); }
        catch { }
    }

    // 像素数组 → Texture2D → Sprite（照 GuMachineSystem.SpriteFromPixels；ppu=100）
    private static Sprite SpriteFromPixels(Color[] pixels, int w, int h)
    {
        try
        {
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.hideFlags = HideFlags.DontSave;
            tex.SetPixels(pixels);
            tex.Apply();
            Sprite sp = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
            sp.hideFlags = HideFlags.DontSave;
            return sp;
        }
        catch { return null; }
    }

    // 反射设字段（照 GuMachineSystem.SetField）
    private static void SetField(GameItem it, string field, object val)
    {
        try
        {
            var f = typeof(GameItem).GetField(field,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (f != null) f.SetValue(it, val);
        }
        catch { }
    }

    internal static void ShowPanel()
    {
        try
        {
            var mgr = Il2Cpp.CustomUIManager.Instance;
            if (mgr == null) return;
            if (mgr.IsOpen("wage_girl_panel")) mgr.CloseWindow("wage_girl_panel");
            var b = mgr.CreateWindow("wage_girl_panel", LangHelper.T("蛙娘 · 状态", "Wage Girl · Status"), "overlay");
            if (b == null) return;
            b.SetSize(300, 560).SetPosition(Vector2.zero);
            try
            {
                var w = mgr.GetWindow("wage_girl_panel");
                if (w != null)
                {
                    var rt = w.Rect;
                    rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1, 1);
                    rt.anchoredPosition = new Vector2(-16, -16);
                }
            }
            catch { }
            b.BeginColumn(4f);
            // 09-20 优化：顶部好感度显示
            b.AddLabel(LangHelper.T("好感度：" + GetAffection() + "/100（" + GetAffLevelText() + "）", "Affection: " + GetAffection() + "/100 (" + GetAffLevelText() + ")"), "wg_aff");
            b.AddLabel(LangHelper.T("💰 小金库：" + GetStat(K_SAVINGS) + " 块", "💰 Savings: " + GetStat(K_SAVINGS) + " credits"), "wg_savings");
            b.AddLabel(LangHelper.T("饱食 ", "Satiety ") + GetStat(K_SAT) + "/100  " + GetStatText(K_SAT), "wg_sat_l");
            b.AddProgressBar(GetStat(K_SAT) / 100f, "wg_sat");
            b.AddLabel(LangHelper.T("口渴 ", "Thirst ") + GetStat(K_TH) + "/100  " + GetStatText(K_TH), "wg_th_l");
            b.AddProgressBar(GetStat(K_TH) / 100f, "wg_th");
            b.AddLabel(LangHelper.T("健康 ", "Health ") + GetStat(K_HEALTH) + "/100  " + GetStatText(K_HEALTH), "wg_h_l");
            b.AddProgressBar(GetStat(K_HEALTH) / 100f, "wg_h");
            b.AddLabel(LangHelper.T("心情 ", "Mood ") + GetStat(K_MOOD) + "/100  " + GetStatText(K_MOOD), "wg_m_l");
            b.AddProgressBar(GetStat(K_MOOD) / 100f, "wg_m");
            b.AddLabel(LangHelper.T("清洁 ", "Cleanliness ") + GetStat(K_CLEAN) + "/100  " + GetStatText(K_CLEAN), "wg_c_l");
            b.AddProgressBar(GetStat(K_CLEAN) / 100f, "wg_c");
            b.AddLabel(LangHelper.T("睡眠 ", "Sleep ") + GetStat(K_SLEEP) + "/100  " + GetStatText(K_SLEEP), "wg_s_l");
            b.AddProgressBar(GetStat(K_SLEEP) / 100f, "wg_s");
            b.AddLabel(LangHelper.T("💬 " + GetDailyLine(), "💬 " + GetDailyLine()), "wg_daily_line"); // 09-20 优化：日常随机台词
            // 外出/离家中：不显示销赃按钮（人不在店里——09-22 用户拍板）
            int leaveChk = GetStat(K_LEAVE);
            bool isOutChk = leaveChk > 0 && CurrentDay() < leaveChk;
            if (!isOutChk)
            {
                // 销赃类别按钮（09-22 用户拍板：可选项，点击循环切换：随机/食物饮品/日用品/武器工具）
                try
                {
                    string[] cats = { LangHelper.T("物资箱", "Supply Crate"), LangHelper.T("食物饮品", "Food/Drink"), LangHelper.T("日用品", "Daily"), LangHelper.T("武器工具", "Weapon/Tool"), LangHelper.T("随机", "Random"), LangHelper.T("指挥卡", "Keycard"), LangHelper.T("医药品", "Medicine"), LangHelper.T("模板", "Module") };
                    int curCat = GetStat(K_FENCE_CAT);
                    var catBtnOnClick = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((System.Action)(() => { try { int c = GetStat(K_FENCE_CAT) + 1; if (c > 7) c = 0; SetStat(K_FENCE_CAT, c); ShowPanel(); } catch (Exception ex) { Core.LogMsg("[蛙娘] 类别切换异常: " + ex.Message); } }));
                    b.AddButton(LangHelper.T("销赃类别（可选择）：" + cats[curCat], "Fence type (selectable): " + cats[curCat]), catBtnOnClick, "wg_fence_cat_btn");
                }
                catch { }
                // 销赃按钮（09-22 用户拍板：喂入违禁品累计，点按钮才出发；按钮文本带待销价值）
                try
                {
                    int famt = GetStat(K_FENCE_AMT);
                    var fenceBtnOnClick = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((System.Action)(() => { try { TryFence(); } catch (Exception ex) { Core.LogMsg("[蛙娘] 销赃异常: " + ex.Message); } }));
                    b.AddButton(LangHelper.T("销赃（待销 " + famt + "）", "Fence (" + famt + ")"), fenceBtnOnClick, "wg_fence_btn");
                    // 09-23 改：违禁品模式切换按钮（洗白 ↔ 销赃）
                    int wmode = GetStat(K_WASH_MODE);
                    var modeBtnOnClick = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((System.Action)(() => { try { int m = GetStat(K_WASH_MODE) == 0 ? 1 : 0; SetStat(K_WASH_MODE, m); ShowPanel(); } catch (Exception ex) { Core.LogMsg("[蛙娘] 模式切换异常: " + ex.Message); } }));
                    b.AddButton(LangHelper.T("违禁品模式（当前：" + (wmode == 0 ? "洗白" : "销赃") + "）", "Contraband mode (current: " + (wmode == 0 ? "Launder" : "Fence") + ")"), modeBtnOnClick, "wg_mode_btn");
                    // 模式说明
                    b.AddLabel(LangHelper.T(wmode == 0 ? "拖违禁品给蛙娘 → 洗白（消除标签，按等级扣费）" : "拖违禁品给蛙娘 → 累计销赃（点「销赃」按钮出发）", wmode == 0 ? "Feed contraband → launder (remove tag, cost by level)" : "Feed contraband → accumulate fence (press Fence to go)"), "wg_mode_hint");
                }
                catch { }
            }
            // 喂钱按钮
            try {
                int sel = _allowanceSel;
                var allowanceBtnOnClick = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((System.Action)(() => { try { GiveAllowance(sel); } catch (Exception ex) { Core.LogMsg("[蛙娘] 喂钱异常: " + ex.Message); } }));
                b.AddButton(LangHelper.T("给零花钱：" + sel, "Allowance: " + sel), allowanceBtnOnClick, "wg_allowance_btn");
                var cycleBtnOnClick = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((System.Action)(() => { try { CycleAllowanceSel(); } catch { } }));
                b.AddButton(LangHelper.T("换档位（下档）", "Switch tier"), cycleBtnOnClick, "wg_allowance_cycle");
                // 09-23 照顾指南按钮
                var guideBtnOnClick = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((System.Action)(() => { try { ShowGuide(); } catch (Exception ex) { Core.LogMsg("[蛙娘] 指南异常: " + ex.Message); } }));
                b.AddButton(LangHelper.T("📖 照顾指南", "📖 Care Guide"), guideBtnOnClick, "wg_guide_btn");
                // 09-23 新增：妙妙箱升级说明

            } catch { }
            b.AddLabel(LangHelper.T("💡 好好照顾她，她会越来越信任你", "💡 Take good care of her, and she will trust you more"), "wg_note");
            // 外出/离家出走状态（阶段 5+6：偷钱/销赃 1 天外出，跑路 14 天）
            try
            {
                int leave = GetStat(K_LEAVE);
                int today = CurrentDay();
                if (leave > 0 && today < leave)
                {
                    int reason = GetStat(K_LEAVE_REASON);
                    int daysLeft = leave - today;
                    string st;
                    if (reason == 2)
                        st = daysLeft <= 1
                            ? LangHelper.T("（离家出走中——明天归来）", "(Ran away - back tomorrow)")
                            : LangHelper.T("（离家出走了——" + daysLeft + " 天后归来）", "(Ran away - back in " + daysLeft + " days)");
                    else if (reason == 1)
                        st = daysLeft <= 1
                            ? LangHelper.T("（外出销赃——明天归来）", "(Out fencing - back tomorrow)")
                            : LangHelper.T("（外出销赃——" + daysLeft + " 天后归来）", "(Out fencing - back in " + daysLeft + " days)");
                    else
                        st = LangHelper.T("（外出中——明天归来）", "(Out - back tomorrow)");
                    b.AddLabel(st, "wg_leave");
                }
            }
            catch { }
        }
        catch (Exception ex) { Core.LogMsg("[蛙娘] 面板异常: " + ex.Message); }
    }


    // ===================== 照顾指南窗口（09-23 用户拍板） =====================
    internal static void ShowGuide()
    {
        try
        {
            var mgr = Il2Cpp.CustomUIManager.Instance;
            if (mgr == null) return;
            if (mgr.IsOpen("wg_guide")) mgr.CloseWindow("wg_guide");
            var b = mgr.CreateWindow("wg_guide", LangHelper.T("蛙娘照顾指南", "Wage Girl Care Guide"), "overlay");
            if (b == null) return;
            b.SetSize(420, 560).SetPosition(Vector2.zero);
            try { var w = mgr.GetWindow("wg_guide"); if (w != null) { var rt = w.Rect; rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1, 1); rt.anchoredPosition = new Vector2(-16, -200); } } catch { }
            b.BeginColumn(4f);
            // 第1章
            b.AddLabel(LangHelper.T("【1】蛙娘是谁？", "[1] Who is Wage Girl?"), "wg_g1");
            b.AddLabel(LangHelper.T("她是你的仿生女仆伙伴。你喂她、照顾她，她帮你干活；你不管她，她就偷钱、离家出走。核心循环：喂好吃的→六维涨→好感涨→她更卖力帮你→生意更好。", "She is your android maid. Feed and care for her, she works for you; neglect her and she steals and leaves. Loop: feed well -> stats rise -> affection rises -> she works harder -> better business."), "wg_g1d");
            // 第2章
            b.AddLabel(LangHelper.T("【2】六维是什么？", "[2] The 6 Stats"), "wg_g2");
            b.AddLabel(LangHelper.T("饱食/口渴/健康/心情/清洁/睡眠。满了不闹，低于30出问题（饿了偷吃东西、渴了偷喝饮料、心情差偷东西）。每日衰减：前5项各-2，睡眠自然恢复。", "Satiety/Thirst/Health/Mood/Clean/Sleep. Full=happy, below 30=causes trouble (steals food/drinks/stuff). Daily decay: first 5 -2 each, sleep recovers naturally."), "wg_g2d");
            // 第3章
            b.AddLabel(LangHelper.T("【3】怎么喂她？", "[3] How to Feed"), "wg_g3");
            b.AddLabel(LangHelper.T("直接拖东西给她。食物→饱食+健康，吃一口剩一半；饮料→口渴（看水质），瓶子留着；日用品→清洁+心情；违禁品→洗白或销赃。脏水掉健康，换花样喂防腻。", "Drag items to her. Food -> satiety+health, eats one bite leaves half; Drink -> thirst (by water quality), bottle kept; Daily goods -> clean+mood; Contraband -> launder or fence. Dirty water hurts health, vary diet to avoid boredom."), "wg_g3d");
            // 第4章
            b.AddLabel(LangHelper.T("【4】怎么涨好感？", "[4] Raising Affection"), "wg_g4");
            b.AddLabel(LangHelper.T("喂好吃的+1~3，照顾清洁+2~5，给零花钱前3次+1~3。好感<30偷≤50，30-70偷≤100，>70偷≤200（很少偷），>90几乎不偷。", "Good food +1~3, clean her +2~5, allowance first 3 times +1~3. Affection <30 steals <=50, 30-70 <=100, >70 <=200 (rarely), >90 almost never steals."), "wg_g4d");
            // 第5章
            b.AddLabel(LangHelper.T("【5】她会做什么？", "[5] What She Does"), "wg_g5");
            b.AddLabel(LangHelper.T("销赃：拖违禁品给她，2天回来带干净货。偷东西：心情差/好感低会偷。偷钱：每5天一次。跑路：连续5天六维低，离家14天。", "Fence: feed contraband, she returns in 2 days with clean goods. Steals: bad mood/low affection. Steals money: every 5 days. Leaves: 5 days low stats, gone 14 days."), "wg_g5d");
            // 第6章
            b.AddLabel(LangHelper.T("【6】小金库", "[6] Savings"), "wg_g6");
            b.AddLabel(LangHelper.T("销赃克扣的跑腿费存小金库。给零花钱100/300/500三档，前3次加好感。跑腿费好感越高越低（15%→5%）。", "Fencing commission goes to savings. Allowance 100/300/500, first 3 times gain affection. Commission drops from 15% to 5% as affection rises."), "wg_g6d");
        }
        catch (Exception ex) { Core.LogMsg("[蛙娘] 指南异常: " + ex.Message); }
    }


    // ===================== 阶段 3：在场增益-议价 +50（GetDealMakerBonus Postfix——拆包 09-21 二次实锤：GetBargainSuccessChance 只被 tooltip 调用=纯显示；GetDealMakerBonus 是显示+实际判定共用唯一加成项，7 调用点覆盖 OfferMarkup/OfferBuyingMarkup/Blackmail） =====================
    public static void PostfixGetDealMakerBonus(ref int __result)
    {
        try
        {
            if (!Exists()) return; // 蛙娘未出现 → 无增益
            __result += 50;
            if (__result > 100) __result = 100;
        }
        catch { }
    }

    // ===================== 阶段 3：在场增益-预算 ×4（ApplyBudgetModifier Postfix——照鲁滨逊预算联动先例） =====================
    public static void PostfixApplyBudgetModifier(StoreClient __instance)
    {
        try
        {
            if (__instance == null) return;
            if (!Exists()) return; // 蛙娘未出现 → 无增益
            if (__instance.identifier == ENTITY_ID) return; // 蛙娘自己不是客户时不受益
            // 预算 ×4（+300%，话术 v9：OverrideBudget(GetBudget()*4)）
            int budget = __instance.GetBudget();
            // 09-20 优化：预算随好感分档（<30→1.5x、<60→2.5x、≥60→4x）
            int affB = GetAffection();
            float mult = affB < BuildConfig.WageGirlBudgetAffLow ? BuildConfig.WageGirlBudgetMultLow : (affB < BuildConfig.WageGirlBudgetAffMid ? BuildConfig.WageGirlBudgetMultMid : BuildConfig.WageGirlBudgetMultHigh);
            long newBudget = (long)(budget * mult);
            if (newBudget > BuildConfig.WageGirlBudgetCap) newBudget = BuildConfig.WageGirlBudgetCap;
            __instance.OverrideBudget((int)newBudget);
            __instance.clientCash = (int)newBudget;
            __instance.useClientBudget = true;
        }
        catch { }
    }

    // ===================== 阶段 2：拖放喂食/喝水/照顾（照命运骰子拖放吸收链） =====================
    public static bool PrefixMayTarget(GameItem __instance, GameItem targetItem, ref bool __result)
    {
        try
        {
            if (__instance == null || targetItem == null) return true;
            if (IsGirl(targetItem) && CanFeed(__instance)) { __result = true; return false; } // hover 可拖
        }
        catch { }
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
            if (TryFeed(__instance, targetItem)) return false; // 喂食成功：拦截原生放入
        }
        catch { }
        return true;
    }
    private static bool IsDragRelease()
    {
        try { var h = Il2Cpp.ItemMouseDragHandler.current; return h != null && h.IsDraggingItem; } catch { return false; }
    }
    private static bool IsGirl(GameItem it)
    {
        try { return it != null && it.identifier == ENTITY_ID; } catch { return false; }
    }
    private static bool CanFeed(GameItem item)
    {
        try { return RobinCrusoePerk.IsFood(item) || RobinCrusoePerk.IsDrink(item) || RobinCrusoePerk.IsDailyNeed(item) || IsContraband(item); } catch { return false; }
    }
    private static bool TryFeed(GameItem item, GameItem girl)
    {
        try
        {
            if (item == null) return false;

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
                    try { Il2Cpp.ContrabandHelper.RemoveContrabandStatus(item); } catch { }
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
                    try { item.Destroy(); } catch { try { item.parentInventory?.Expel(item); } catch { } }
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
            int gain = 0; int aff = 1; string msg = "";
            int curAff = GetAffection();
            // 分阶段好感获取：初期(0-30)+1~2，中期(30-70)+2~3，后期(70-100)+1
            int affBase = curAff < 30 ? 1 : (curAff < 70 ? 2 : 1);
            if (RobinCrusoePerk.IsDailyNeed(item)) { gain = 20; aff = curAff < 30 ? 2 : (curAff < 70 ? 4 : 2); msg = LangHelper.T("蛙娘洗得干干净净、心情大好！清洁 +20 心情 +10（照顾）", "Wage Girl cleaned up & cheered up! Cleanliness +20 Mood +10 (care)"); SetStat(K_CLEAN, GetStat(K_CLEAN) + gain); SetStat(K_MOOD, GetStat(K_MOOD) + 10); SetStat(K_HEALTH, GetStat(K_HEALTH) + 15); try { item.Destroy(); } catch { } } // 09-21 Bug1: 日用品用完消失
            else if (RobinCrusoePerk.IsFood(item)) {
                // 食物：GetCalLeft → bite=min(100,(cal+1)/2) → gain=round(bite/22)
                int cal = RobinCrusoePerk.GetCalLeft(item);
                if (cal <= 0) { item.Destroy(); return false; }
                int bite = cal <= 100 ? cal : Math.Max(100, (cal + 1) / 2);
                int left = cal - bite;
                gain = Math.Max(1, (int)Math.Round(bite / 22f));
                SetStat(K_SAT, Math.Min(100, GetStat(K_SAT) + gain));
                SetStat(K_HEALTH, Math.Max(0, GetStat(K_HEALTH) + 15)); // 进食健康+15
                aff = affBase;
                msg = LangHelper.T("蛙娘吃饱了！饱食 +" + gain, "Wage Girl ate! Satiety +" + gain);
                // 留食：吃完才 Destroy，剩→SetCalLeft+EATEN_TAG
                if (left <= 0) { item.Destroy(); }
                else { RobinCrusoePerk.SetCalLeft(item, left); try { item.EnableTag("EATEN_TAG", true); } catch { } }
            }
            else if (RobinCrusoePerk.IsDrink(item)) {
                // 水：GetWaterMl → sip=min(200,ml) → purity 5 档
                int ml = RobinCrusoePerk.GetWaterMl(item);
                if (ml <= 0) {
                    // 09-23 修复「无卡路里值的饮品统一按其价值恢复蛙娘的口渴值」
                    // 根因：酒/代饮品（red_beer / nudka / galaxy_blend 等）原生不写 LIQUID_CONTAINER_CURRENT，
                    // GetWaterMl()=0 → 旧代码 `item.Destroy(); return false;` → 物品凭空消失、口渴一点不回。
                    // 注：有卡路里的饮品会被上面的 IsFood 分支先接走（IsFood 要求 CALORIE_VALUE_TAG/CALORIE），
                    // 所以落到饮品分支且 ml=0 的基本都是无卡路里值的饮品 → 统一按「价值」恢复口渴。
                    // 档位沿用水质 5 档的量级（25/18/12/6/2），既有水/纯度机制完全不动。
                    long dval = 0;
                    try { dval = item.GetCurrentValue(); } catch { }
                    if (dval <= 0) { try { dval = item.unitValue; } catch { } }
                    gain = dval >= 300 ? 25 : dval >= 150 ? 18 : dval >= 60 ? 12 : dval >= 20 ? 6 : 2;
                    SetStat(K_TH, Math.Min(100, GetStat(K_TH) + gain));
                    aff = affBase;
                    msg = LangHelper.T("蛙娘喝了一杯！口渴 +" + gain + "（按价值 " + dval + "）", "Wage Girl had a drink! Thirst +" + gain + " (value " + dval + ")");
                    // 无水量可扣 → 整件喝完消失（否则同一件可无限刷口渴）
                    try { item.Destroy(); } catch { }
                }
                else {
                    int sip = Math.Min(200, ml);
                    int purity = -1; try { purity = Il2Cpp.WaterHelper.GetWaterPurity(item); } catch { }
                    int tier = purity >= 9900 ? 0 : purity >= 9600 ? 1 : purity >= 9200 ? 2 : purity >= 8800 ? 3 : 4;
                    gain = new[] { 25, 18, 12, 6, 2 }[tier];
                    int hd = new[] { 5, 2, 0, -5, -10 }[tier];
                    SetStat(K_TH, Math.Min(100, GetStat(K_TH) + gain));
                    if (hd != 0) SetStat(K_HEALTH, Math.Max(0, Math.Min(100, GetStat(K_HEALTH) + hd)));
                    aff = affBase;
                    string wname = new[] { "优质", "较好", "普通", "浑浊", "脏水" }[tier];
                    msg = LangHelper.T("蛙娘喝饱了！口渴 +" + gain + "（" + wname + "）", "Wage Girl drank! Thirst +" + gain + " (" + wname + ")");
                    // 不 Destroy，瓶子留（带剩余水）
                    try { WaterHelper.Remove(item, sip * 1000); } catch { }
                }
            }
            else return false;
            SetAffection(GetAffection() + aff);
            SetStat("lastFedDay", CurrentDay()); // 记录今天喂过
            try { StoreUIManager.Instance.Notify(msg, "green"); } catch { }
            try { if (Il2Cpp.CustomUIManager.Instance != null && Il2Cpp.CustomUIManager.Instance.IsOpen("wage_girl_panel")) ShowPanel(); } catch { }
            SetAnimMode(2); // 09-22 吃掉瞬间切偷动画（播完回待机）
            return true;
        }
        catch (Exception ex) { Core.LogMsg("[蛙娘] 喂食异常: " + ex.Message); return false; }
    }

    // 玩家归属判定（09-23 新增）：GeneralHelper.IsItemOwned ≡ item.IsTag("IS_OWNED_TAG")（原版归属标记 [L1]）
    // 交易模式下只许拿玩家自己的东西照顾蛙娘；读不到时按"非玩家所有"处理（保守，避免销毁客户的货）
    private static bool IsPlayerOwnedForCare(GameItem item)
    {
        try { if (item == null) return false; } catch { return false; }
        try { return Il2Cpp.GeneralHelper.IsItemOwned(item); } catch { }
        try { return item.IsTag("IS_OWNED_TAG"); } catch { }
        return false;
    }

    // ===================== 双击（全局——不依赖任何特性） =====================
    public static void PostfixDoubleClickAction(GameItem newItem, Vector2 mousePosition)
    {
        try
        {
            if (newItem == null) return;
            if (newItem.identifier != ENTITY_ID) return;
            // 09-23 修复「蛙娘只有在不营业时才可被照顾」：状态/照顾面板在营业期间同样可打开
            // （面板内按钮各自保留原有门控：销赃 TryFence / 洗白 仍禁止交易中使用）
            ShowPanel();
        }
        catch { }
    }

    // 发放前全范围查重：5 网格 + 容器内部已有蛙娘 → true
    private static bool ExistsInScene()
    {
        try
        {
            EmporiumEntry em = EmporiumEntry.Instance;
            if (em == null) return false;
            // 09-23 性能：原先每次调用都 new GameInventory[5] + foreach——本方法在热路径上（见 ExistsCached），
            // 每秒几十次分配纯属 GC 压力。改为 5 次直调，语义完全一致（短路顺序也保持原样）。
            if (FindGirlInInv((GameInventory)em.invElement)) return true;
            if (FindGirlInInv((GameInventory)em.backInvinvElement)) return true;
            if (FindGirlInInv((GameInventory)em.backInvinvElementCounter)) return true;
            if (FindGirlInInv((GameInventory)em.frontInvinvElement)) return true;
            if (FindGirlInInv((GameInventory)em.showcaseElement)) return true;
            if (FindGirlInInv((GameInventory)em.hiddenElement)) return true; // 09-23 补：海报后暗格
            return false;
        }
        catch { return false; }
    }

    private static bool FindGirlInInv(GameInventory inv)
    {
        if (inv == null || inv.childItems == null) return false;
        for (int i = 0; i < inv.childItems.Count; i++)
        {
            try
            {
                var it = inv.childItems[i];
                if (it == null) continue;
                if (it.identifier == ENTITY_ID) return true;
                if (it.contentWindow != null)
                {
                    var inner = AddictOfficerEvent.GetInnerInventory(it);
                    if (inner != null && FindGirlInInv(inner)) return true;
                }
            }
            catch { }
        }
        return false;
    }

    private static void TryGiveToBackpack()
    {
        try
        {
            EmporiumEntry em = EmporiumEntry.Instance;
            if (em == null) { Core.LogMsg("[蛙诊] GiveToBackpack: em null"); return; }
            if (em.backInvinvElement == null) { Core.LogMsg("[蛙诊] GiveToBackpack: backInv null"); return; }
            // 09-19 发放前全范围查重（5 网格+容器内部已有 → 不重复发）
            if (ExistsInScene()) { Core.LogMsg("[蛙诊] GiveToBackpack: ExistsInScene true"); return; }
            var inv = (GameInventory)em.backInvinvElement;
            GameItem item = DirectoryMaster.Item(ENTITY_ID, true);
            if (item == null) { Core.LogMsg("[蛙诊] GiveToBackpack: item null"); return; }
            // 照 GiveToBackpack 先例：TryFindOneValidInventorySlot → TryAcceptOnce（防同格重叠）；失败 UncheckedAccept 兜底
            var slot = em.backInvinvElement.TryFindOneValidInventorySlot(item, false);
            if (slot != null) { try { slot.TryAcceptOnce(); return; } catch (Exception) { } }
            inv.UncheckedAccept(item);
            Core.LogMsg("[蛙娘] 已发放实体到背包（全局常驻）");
        _returnTimer = 0.5f; _curState = ""; // 强制播return帧0.5s再切idle
        }
        catch (Exception ex) { Core.LogMsg("[蛙娘] 发放失败: " + ex.Message); }
    }

    // ===================== 动画系统（09-22 蛙娘动画帧集成，用户拍板 B：真移动+走动帧） =====================
    // 12 帧 base64（WageGirlAnimFrames.cs）→ 运行时解码 Texture2D → Sprite[]（64×96 超采样，Point 缩回 32×48）
    private static Sprite[] _spIdle, _spHappy, _spHungry, _spThirsty, _spSick, _spDirty, _spSleepy, _spAngry, _spShy, _spFull, _spAway, _spReturn, _spWalk;
    private static string _curState = "idle";
    private static float _stateFrameSec = 0.375f;
    private static float _leavingTimer = 0f;
    private static float _returnTimer = 0f; // 回归动画倒计时（return帧播完切idle） // 外出动画延迟（walk帧播完再移除实体）
    private static Sprite[] _curAnimSprites;
    private static int _frameIndex = 0;
    private static float _frameTimer = 0f;
    private static int _animMode = 0; // 0=待机 1=走动 2=偷
    private static float _animModeTimer = 0f;
    private static float _moveTimer = 0f;
    private static float _lastDiagTime = 0f;
    private static float _lastMoveDiagTime = 0f; // 09-22 TryMoveStep 诊断独立节流（用完删）
    private static bool _walking = false;    // 09-22 走停状态机：是否在走动
    private static int _stepsTaken = 0;      // 本轮已走步数
    private static int _walkSteps = 4;       // 本轮要走步数（随机 3-7）
    private static float _pauseTimer = 0f;   // 停顿计时
    private static float _pauseDuration = 4f;// 停顿时长（随机 3-6 秒）
    private static GridShape _girlShape; // 运行时初始化（Unity就绪后）
    private static Sprite _staticIconSprite; // 静态图标 32×48（modifiedShape=2×3 权威来源，09-20 拆包）
    private static readonly float[] _frameMs = { 0.5f, 0.2f, 0.15f }; // 待机/走动/偷（秒/帧）

    private static void EnsureSprites()
    {
        if (_spIdle != null && _spIdle.Length > 0 && _spIdle[0] != null) return; // 09-20 修：元素级防重入
        try
        {
            _spIdle = LoadSpriteGroup(WageGirlAnimFrames.Idle);
            _spHappy = LoadSpriteGroup(WageGirlAnimFrames.Happy);
            _spHungry = LoadSpriteGroup(WageGirlAnimFrames.Hungry);
            _spThirsty = LoadSpriteGroup(WageGirlAnimFrames.Thirsty);
            _spSick = LoadSpriteGroup(WageGirlAnimFrames.Sick);
            _spDirty = LoadSpriteGroup(WageGirlAnimFrames.Dirty);
            _spSleepy = LoadSpriteGroup(WageGirlAnimFrames.Sleepy);
            _spAngry = LoadSpriteGroup(WageGirlAnimFrames.Angry);
            _spShy = LoadSpriteGroup(WageGirlAnimFrames.Shy);
            _spFull = LoadSpriteGroup(WageGirlAnimFrames.Full);
            _spAway = LoadSpriteGroup(WageGirlAnimFrames.Away);
            _spReturn = LoadSpriteGroup(WageGirlAnimFrames.Return);
            _spWalk = LoadSpriteGroup(WageGirlAnimFrames.Walk);
            _curAnimSprites = _spIdle;
        }
        catch { }
    }

    private static System.Reflection.MethodInfo _loadImageMethod; // ImageConversion.LoadImage（IL2CPP 反射查找，DestinyDice 先例）

    private static Sprite[] LoadSpriteGroup(string[] b64s)
    {
        var arr = new Sprite[b64s.Length];
        EnsureLoadImageMethod();
        for (int i = 0; i < b64s.Length; i++)
        {
            try
            {
                byte[] bytes = Convert.FromBase64String(b64s[i]);
                var tex = new Texture2D(64, 96, TextureFormat.RGBA32, false);
                tex.filterMode = FilterMode.Point;
                if (_loadImageMethod != null)
                {
                    try
                    {
                        _loadImageMethod.Invoke(null, new object[] { tex, (Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<byte>)bytes });
                        tex.wrapMode = TextureWrapMode.Clamp;
                        arr[i] = Sprite.Create(tex, new Rect(0, 0, 64, 96), new Vector2(0.5f, 0.5f), 200f); // 09-22 回缩1倍：64×96 超采样 → 显示 32×48
                        continue;
                    }
                    catch { }
                }
                try { UnityEngine.Object.Destroy(tex); } catch { }
            }
            catch { }
        }
        return arr;
    }

    // 反射查找 ImageConversion.LoadImage（IL2CPP 不在标准命名空间——DestinyDice L175-213 先例；只找一次）
    private static void EnsureLoadImageMethod()
    {
        if (_loadImageMethod != null) return;
        try
        {
            Type icType = null;
            foreach (System.Reflection.Assembly a in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = a.GetTypes(); } catch (System.Reflection.ReflectionTypeLoadException ex) { types = ex.Types; }
                foreach (Type t in types)
                {
                    if (t != null && t.Name == "ImageConversion") { icType = t; break; }
                }
                if (icType != null) break;
            }
            if (icType != null)
            {
                // 09-20 修：GetMethod 精确参数匹配失败（IL2CPP 签名是 byte[]）→ 宽松遍历
                foreach (var m in icType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
                {
                    if (m.Name == "LoadImage") { var ps = m.GetParameters(); if (ps.Length == 2) { _loadImageMethod = m; break; } }
                }
            }
        }
        catch { }
    }

    // 静态图标（32×48，ppu100）：Idle0 帧 64×96 下采样 2:1 → SetSpriteAndShape 成功路径算 modifiedShape=2×3（09-20 拆包 L3484-3533）
    private static void EnsureStaticIcon()
    {
        try
        {
            if (_staticIconSprite != null) return;
            EnsureLoadImageMethod();
            if (_loadImageMethod == null) return;
            byte[] bytes = Convert.FromBase64String(WageGirlAnimFrames.Idle[0]);
            var tex = new Texture2D(64, 96, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            _loadImageMethod.Invoke(null, new object[] { tex, (Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<byte>)bytes });
            Color[] src = tex.GetPixels(); // 64×96
            var dst = new Color[32 * 48];
            for (int y = 0; y < 48; y++)
            {
                for (int x = 0; x < 32; x++)
                {
                    int s0 = (y * 2) * 64 + (x * 2);
                    Color c = (src[s0] + src[s0 + 1] + src[s0 + 64] + src[s0 + 65]) * 0.25f;
                    dst[y * 32 + x] = c;
                }
            }
            try { UnityEngine.Object.Destroy(tex); } catch { }
            var tex2 = new Texture2D(32, 48, TextureFormat.RGBA32, false);
            tex2.filterMode = FilterMode.Point;
            tex2.wrapMode = TextureWrapMode.Clamp;
            tex2.SetPixels(dst);
            tex2.Apply();
            Sprite sp = Sprite.Create(tex2, new Rect(0, 0, 32, 48), new Vector2(0.5f, 0.5f), 100f);
            sp.hideFlags = HideFlags.DontSave;
            _staticIconSprite = sp;
        }
        catch { }
    }

    // 拦截 RenderHandler.LoadFromAtlas：wage_girl_icon → 32×48 静态图标——09-20 拆包实锤 modifiedShape 权威，SetSpriteAndShape 成功即算 2×3
    public static bool PrefixLoadFromAtlas(string atlasPath, string name, ref Sprite __result)
    {
        try
        {
            if (atlasPath == ICON_ATLAS && name == ICON)
            {
                EnsureStaticIcon();
                if (_staticIconSprite != null) { __result = _staticIconSprite; return false; }
            }
        }
        catch { }
        return true;
    }

    // 动作切换（0 待机 / 1 走动 / 2 偷）
    private static string EvalState()
    {
        try {
            if (_animMode == 2) return "angry";
            if (_animMode == 1) return "walk";
            try { if (_walking) return "walk"; } catch { } // 平时走动用walk帧
            // 09-19 修：K_LEAVE>0 但蛙娘实体在店里（读档恢复）→ 不挥手，走正常状态
            try { if (GetStat(K_LEAVE) > 0 && _cachedGirlItem == null) return "away"; } catch { }
            int mood = GetStat(K_MOOD), health = GetStat(K_HEALTH), sat = GetStat(K_SAT), th = GetStat(K_TH), clean = GetStat(K_CLEAN), sleep = GetStat(K_SLEEP);
            int aff = GetAffection();
            if (mood <= 20) return "angry";
            if (health <= 30) return "sick";
            if (sat <= 30) return "hungry";
            if (th <= 30) return "thirsty";
            if (clean <= 30) return "dirty";
            if (sleep <= 30) return "sleepy";
            if (mood >= 70) return "happy";
            if (aff >= 80) return "shy";
            if (sat >= 80 && mood >= 60) return "full";
            return "idle";
        } catch { return "idle"; }
    }

    private static void SetAnimMode(int mode, bool force = false)
    {
        try
        {
            if (_animMode == mode && !force) return;
            _animMode = mode;
            _frameIndex = 0;
            _frameTimer = 0f;
            _animModeTimer = 0f;
            _curAnimSprites = mode == 1 ? _spWalk : mode == 2 ? _spAngry : _spIdle;
        }
        catch { }
    }

    // 09-23 性能：实体存在性检查的节流缓存（**只用于动画驱动**）
    // 问题：Exists() → ExistsInScene() 要遍历 5 个背包的全部物品，且 FindGirlInInv 会向
    //       contentWindow 内部背包**递归下钻** → 开销随物品总数线性（甚至更高）增长。
    //       后期背包/货架塞满时，每帧全量扫描 = 明显卡顿。
    // 处理：动画启停不需要每帧精度 → 每 15 帧（60fps 下 0.25s）重算一次，其余帧读缓存。
    // ⚠️ 仅在**动画驱动**这一条路径上用缓存；偷拿/补发等业务判定仍调 Exists()，保证精确语义不受影响。
    private const int EXISTS_CACHE_FRAMES = 15;
    private static bool _existsCache;
    private static int _existsCacheFrame = -100000;
    private static bool ExistsCached()
    {
        try
        {
            int f = Time.frameCount;
            if (f - _existsCacheFrame < EXISTS_CACHE_FRAMES) return _existsCache;
            _existsCacheFrame = f;
            _existsCache = Exists();
            return _existsCache;
        }
        catch { return Exists(); }
    }

    // 每帧驱动（09-23 阶段0：Patches.FrameUpdate 调用 ← InputActionManager.Update Postfix，禁用 MelonLoader OnUpdate）
    // 轻量 + 全异常防护 + 交易模式暂停——用户规范：每帧逻辑不做重操作
    private static int _lastTickFrame = -1;
    public static void OnUpdateTick()
    {
        try
        {
            // 同帧去重：InputActionManager.Update 每帧可被多次调用（多实例/多 Patch，09-12 实锤）
            // 本方法用 Time.deltaTime 累加计时器，重复调用会导致动画与移动加速——必须按帧号去重
            if (Time.frameCount == _lastTickFrame) return;
            _lastTickFrame = Time.frameCount;
        }
        catch { }
        try
        {
            if (!ExistsCached()) return;
            if (Patches.CurrentUITradeMode != 0) return; // 交易中不动画不移动
            float dt = Time.deltaTime;
            if (dt <= 0f) return; // 游戏暂停
            EnsureSprites();
            bool hasSprites = _curAnimSprites != null && _curAnimSprites.Length > 0;

            // 外出动画延迟：walk帧播完再移除实体
            if (_leavingTimer > 0f) {
                _leavingTimer -= dt;
                if (_leavingTimer <= 0f) { try { RemoveGirlFromScene(); } catch { } }
            }
            // 回归动画：return帧播完再切idle
            if (_returnTimer > 0f) {
                _returnTimer -= dt;
                _curState = "return";
                _curAnimSprites = _spReturn;
                _stateFrameSec = 0.125f;
                _frameTimer += dt;
                if (_frameTimer >= _stateFrameSec) { _frameTimer = 0f; if (_spReturn != null && _spReturn.Length > 1) _frameIndex = (_frameIndex + 1) % _spReturn.Length; }
                TryApplyAnimFrame();
                if (_returnTimer <= 0f) { _curState = ""; _frameIndex = 0; _frameTimer = 0f; }
            }
            else
            // 状态自动判定
            try {
                string ns = EvalState();
                if (ns != _curState) { _curState = ns; _frameIndex = 0; _frameTimer = 0f;
                    _curAnimSprites = ns == "happy" ? _spHappy : ns == "hungry" ? _spHungry : ns == "thirsty" ? _spThirsty : ns == "sick" ? _spSick : ns == "dirty" ? _spDirty : ns == "sleepy" ? _spSleepy : ns == "angry" ? _spAngry : ns == "shy" ? _spShy : ns == "full" ? _spFull : ns == "away" ? _spAway : ns == "return" ? _spReturn : ns == "walk" ? _spWalk : _spIdle;
                    _stateFrameSec = ns == "happy" ? 0.25f : ns == "hungry" ? 0.3f : ns == "dirty" ? 0.3f : ns == "sick" ? 0.5f : ns == "sleepy" ? 0.5f : ns == "angry" ? 0.2f : (ns == "away" || ns == "return" || ns == "walk") ? 0.125f : 2f; // 09-20 优化：idle 频率 0.375→2 秒
                }
            } catch { }
            // 帧相关（sprite 加载失败时跳过帧应用，不影响移动）
            if (hasSprites)
            {
                // 偷动作：播完自动回待机
                if (_animMode == 2)
                {
                    _animModeTimer += dt;
                    if (_animModeTimer >= _frameMs[2] * _curAnimSprites.Length)
                        SetAnimMode(0);
                }
                // 帧索引推进（按当前动作帧率）
                _frameTimer += dt;
                if (_frameTimer >= _stateFrameSec)
                {
                    _frameTimer = 0f;
                    if (_curAnimSprites.Length > 1)
                        _frameIndex = (_frameIndex + 1) % _curAnimSprites.Length;
                }
                // 09-22 帧应用：ApplyAnimationFrame 原生零调用方（拆包实锤）——mod 必须自己调；Prefix 会替换成 mod 帧
                TryApplyAnimFrame();
            }

            // 移动状态机（09-22 走一会停一会）：走动 3-7 步（每 2.5s 一步）→ 停 3-6 秒 → 再走；不依赖 sprite
            if (_walking)
            {
                _moveTimer += dt;
                if (_moveTimer >= 2.5f)
                {
                    _moveTimer = 0f;
                    _stepsTaken++;
                    if (TryMoveStep()) { SetAnimMode(1, true); }
                    else { SetAnimMode(0); }
                    if (_stepsTaken >= _walkSteps)
                    {
                        _walking = false; _pauseTimer = 0f; SetAnimMode(0);
                    }
                }
            }
            else
            {
                _pauseTimer += dt;
                if (_pauseTimer >= _pauseDuration)
                {
                    // 09-20 优化：前半好感（<50）不走动、不播放 walk 动画
                    if (GetAffection() < 50) { _pauseDuration = 10f; return; } // 好感<50：待在角落不动
                    _walking = true;
                    _stepsTaken = 0;
                    _walkSteps = 3 + Core.Rng.Next(0, 5);            // 走 3-7 步
                    _pauseDuration = 3f + (float)Core.Rng.Next(0, 4); // 停 3-6 秒
                }
            }
        }
        catch { }
    }

    // 09-22 帧应用：从网格找蛙娘实体 → Cast GameItemElement → 调 ApplyAnimationFrame（触发 Prefix 替换帧）
    // ApplyAnimationFrame 在原生无调用方（ISIL 全库 0 call），必须 mod 主动调用；实体每 2 秒重找（玩家可能移动/收起）
    private static GameItem _cachedGirlItem;
    private static int _cacheRefreshFrames = 0;
    // 09-23 性能：缓存 Cast 结果。原先每帧都做一次 `as` + `Cast<GameItemElement>()`（IL2CPP 互操作类型检查，
    // 每帧一次纯浪费）——元素引用只在 _cachedGirlItem 变化时才会变，故与它同步刷新。
    private static GameItemElement _cachedEl;
    private static void TryApplyAnimFrame()
    {
        try
        {
            // 09-21 修：拖拽时跳过蛙娘动画（避免干扰正在拖拽的物品）
            try { var drg = Il2Cpp.ItemMouseDragHandler.current; if (drg != null && drg.IsDraggingItem) return; } catch { }
            // 读档后旧引用已销毁（parentInventory==null）→ 立即重置重找
            try { if (_cachedGirlItem != null && _cachedGirlItem.parentInventory == null) { _cachedGirlItem = null; _cachedEl = null; _cacheRefreshFrames = 0; } } catch { _cachedGirlItem = null; _cachedEl = null; }
            if (_cachedGirlItem == null || _cacheRefreshFrames <= 0)
            {
                _cacheRefreshFrames = 120;
                _cachedGirlItem = FindGirlItem();
                _cachedEl = null; // 实体可能是新对象 → 丢掉旧 Cast 结果
                    _curState = ""; _frameIndex = 0; _frameTimer = 0f; // 读档后强制重新判定状态
                    if (_cachedGirlItem != null) { try { ApplyIcon(_cachedGirlItem); } catch { } } // 09-20 图标链修复：ApplyIcon 即写 modifiedShape=2×3
                    _curState = ""; _frameIndex = 0; _frameTimer = 0f; // 读档后强制重新判定状态
            }
            else _cacheRefreshFrames--;
            if (_cachedGirlItem == null) return;
            // 09-23 性能：Cast 只在 _cachedEl 为空时做（正常帧直接命中缓存）
            GameItemElement el = _cachedEl;
            if (el == null)
            {
                try { el = _cachedGirlItem as GameItemElement; } catch { }
                if (el == null) { try { el = _cachedGirlItem.Cast<GameItemElement>(); } catch { } }
                if (el != null) _cachedEl = el;
            }
            if (el == null)
            {
                // 09-23 读档/过天后物品重建——旧缓存 Cast 失败立即重找（不等 120 帧）——根治掉动态
                _cachedGirlItem = FindGirlItem();
                _cachedEl = null;
                if (_cachedGirlItem != null) {
                    // 读档后：游戏重建的蛙娘 sprite 是存档旧版 → 强制清旧发新
                    try { RemoveGirlFromScene(); } catch { }
                    try { TryGiveToBackpack(); } catch { }
                    _cachedGirlItem = FindGirlItem(); // 重新找新实体
                    _spIdle = null; _spHappy = null; _spHungry = null; _spThirsty = null; _spSick = null; _spDirty = null; _spSleepy = null; _spAngry = null; _spShy = null; _spFull = null; _spAway = null; _spReturn = null; _spWalk = null;
                    try { EnsureSprites(); } catch { }
                    _curState = ""; _frameIndex = 0; _frameTimer = 0f;
                }
                {
                    try { el = _cachedGirlItem as GameItemElement; } catch { }
                    if (el == null) { try { el = _cachedGirlItem.Cast<GameItemElement>(); } catch { } }
                    if (el != null) _cachedEl = el;
                }
            }
            if (el == null) return;
            Sprite f = _curAnimSprites[_frameIndex % _curAnimSprites.Length];
            // 09-20 修：第0帧null fallback——循环找下一个非null帧（IL2CPP首次类型延迟导致arr[0]=null）
            if (f == null) {
                for (int k = 1; k < _curAnimSprites.Length; k++) {
                    int idx = (_frameIndex + k) % _curAnimSprites.Length;
                    if (_curAnimSprites[idx] != null) { f = _curAnimSprites[idx]; break; }
                }
            }
            if (f == null) return;
            el.ApplyAnimationFrame(f);
        }
        catch { }
    }

    // 网格中查找蛙娘实体（4 货架；同 TryMoveStep 遍历源）
    private static GameItem FindGirlItem()
    {
        try
        {
            var em = EmporiumEntry.Instance;
            if (em == null) return null;
            GameInventory[] grids = new GameInventory[]
            {
                em.invElement as GameInventory,
                em.frontInvinvElement as GameInventory,
                em.showcaseElement as GameInventory,
                em.backInvinvElement as GameInventory
            };
            foreach (var gi in grids)
            {
                if (gi == null || gi.childItems == null) continue;
                for (int i = 0; i < gi.childItems.Count; i++)
                {
                    var c = gi.childItems[i];
                    if (c == null) continue;
                    if (c.identifier == ENTITY_ID) {
                        return c;
                    }
                }
            }
        }
        catch { }
        return null;
    }

    // 尝试移动一步：Expel + TryInventorySlot(目标格子编号) 落格（边界回弹 + 兜底放回，绝不丢实体）
    private static bool TryMoveStep()
    {
        if (!BuildConfig.WageGirlAutoMove) return false; // 09-23 自动移动开关
        try
        {
            var em = EmporiumEntry.Instance;
            if (em == null) return false;
            GameGridInventory inv = null;
            GameItem g = null;
            GameGridInventory[] grids = new GameGridInventory[]
            {
                em.invElement as GameGridInventory,
                em.frontInvinvElement as GameGridInventory,
                em.showcaseElement as GameGridInventory,
                em.backInvinvElement as GameGridInventory
            };
            foreach (var gi in grids)
            {
                if (gi == null || gi.childItems == null) continue;
                for (int i = 0; i < gi.childItems.Count; i++)
                {
                    var c = gi.childItems[i];
                    if (c == null) continue;
                    if (c.identifier == ENTITY_ID) { inv = gi; g = c; break; }
                }
                if (g != null) break;
            }
            if (inv == null || g == null) return false;

            if (_girlShape == null)
            {
                var gsb = new GridShapeBuilder();
                gsb.SetDataFill(2, 3);
                _girlShape = gsb.Build();
            }

            // 09-22 随机落格（拆包正确姿势）：requestedNum=数量(1) 不是格子号；随机格中心像素点(每格16px,+8中心)
            // → TryInventorySlot(item, 1, Vector2像素点, shape, null) 自动换算格位 → TryAcceptOnce 落位
            // 09-20 图标链修复后：modifiedShape=2×3 由 SetSpriteAndShape 保证，落格直接用 _girlShape（2×3）
            GridShape shape = _girlShape;
            // 网格宽高（拆包权威：inv.inventoryShape.width/height——格子数；兜底物品 shape）
            int gw = 0, gh = 0;
            GridShape invShape = null;
            try { invShape = inv.inventoryShape; if (invShape != null) { gw = invShape.width; gh = invShape.height; } } catch { }
            if (gw <= 0 || gh <= 0)
            {
                if (shape != null) { try { gw = shape.width; gh = shape.height; } catch { } }
            }
            // 09-22 诊断（用完删）：任何情况都打——区分 shape 为空 / 宽高为 0 / 盲试结果（独立节流防被 tick 诊断挡）
            if (Time.time - _lastMoveDiagTime > 5f)
            {
                _lastMoveDiagTime = Time.time;
            }
            if (gw > 0 && gh > 0)
            {
                int tryCount = 0, hitCount = 0;
                for (int t = 0; t < 8; t++)
                {
                    try
                    {
                        tryCount++;
                        int cx = Core.Rng.Next(0, gw);
                        int cy = Core.Rng.Next(0, gh);
                        // 09-23 拆包正确姿势：GridShapeBuilder(item.shape) + SetPosition(cx,cy) + 3参 TryInventorySlot + TryAcceptOnce
                        // （5参 Vector2 像素点版是陷阱——GetGridPosition 换算后 clamp 0 → 总左上角）
                        var b = new GridShapeBuilder(shape);
                        b.SetPosition(cx, cy);
                        var m = inv.TryInventorySlot(g, b.shape, null);
                        if (m != null && m.IsValid())
                        {
                            hitCount++;
                            m.TryAcceptOnce();
                            return true;
                        }
                    }
                    catch { }
                }
                if (Time.time - _lastMoveDiagTime > 5f)
                {
                    _lastMoveDiagTime = Time.time;
                }
            }
            // 兜底：Expel + TryFindOneValidInventorySlot（至少能动，可能左上角）
            if (!inv.Expel(g)) return false;
            var slot = inv.TryFindOneValidInventorySlot(g, false);
            if (slot != null && slot.IsValid())
            {
                slot.TryAcceptOnce();
                return true;
            }
            inv.UncheckedAccept(g); // 兜底放回（绝不丢实体）
            return false;
        }
        catch { return false; }
    }

    // ResolveSpriteByName Postfix：对蛙娘永远返回 mod 图标（拆包实锤：Validate 链从 spritePath 解析 sprite，拦截此入口根治旧图）
    public static void PostfixResolveSpriteByName(GameItemElement __instance, string name, ref Sprite __result)
    {
        try {
            if (__instance == null) return;
            if (__instance.identifier != ENTITY_ID && name != ICON) return;
            // 设成当前动画帧（和 ApplyAnimationFrame 一致）——不再设静态占位图标避免交替
            try { if (_curAnimSprites != null && _curAnimSprites.Length > 0) { var f = _curAnimSprites[_frameIndex % _curAnimSprites.Length]; if (f != null) { __result = f; return; } } } catch { }
            // 09-19 删除占位图标兜底
        } catch { }
    }

    public static void PrefixApplyAnimationFrame(GameItemElement __instance, ref Sprite frame)
    {
        try
        {
            if (__instance == null) return;
            // 拆包实锤：identifier 读档后丢失 → 加 IsTag(TAG) 兜底（TAG 随档）
            if (__instance.identifier != ENTITY_ID && !__instance.IsTag(TAG)) return;
            EnsureSprites();
            if (_curAnimSprites == null || _curAnimSprites.Length == 0)
            {
                if (Time.time - _lastDiagTime > 5f)
                {
                    _lastDiagTime = Time.time;
                }
                return;
            }
            Sprite s = _curAnimSprites[_frameIndex % _curAnimSprites.Length];
            if (s == null) {
                for (int k = 1; k < _curAnimSprites.Length; k++) {
                    int idx = (_frameIndex + k) % _curAnimSprites.Length;
                    if (_curAnimSprites[idx] != null) { s = _curAnimSprites[idx]; break; }
                }
            }
            if (s != null) frame = s;
        }
        catch { }
    }
}
