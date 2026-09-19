using System;
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
public static class WageGirlSystem
{
    public const string ENTITY_ID = "wage_girl";
    public const string ICON_ATLAS = "custom_atlas";
    public const string ICON = "wage_girl_icon";
    public const string TAG = "WAGE_GIRL_TAG";
    private const string NS = "wage_girl";
    private const int STAT_MAX = 100;
    private const int STAT_INIT = 60;
    private const int DAILY_DECAY = 3;   // 每日六维衰减（未照顾）
    private const int AFF_MAX = 100;
    private const int AFF_DAILY_DROP = 1; // 好感每日回落（不照顾）

    private const string K_SAT = "sat", K_TH = "th", K_HEALTH = "health", K_MOOD = "mood", K_CLEAN = "clean", K_SLEEP = "sleep", K_SLEEP_DEBT = "sleepDebt";
    private const string K_AFF = "affection", K_LAST_STEAL = "lastStealDay", K_LEAVE = "leaveDay", K_STARVE = "starveStreak";
    private const string K_EXIST = "exists";
    private const string K_STEAL_AMT = "lastStealAmount"; // 上次偷钱额（回归带物比例用）
    private const string K_LAST_GIFT = "lastGiftDay";     // 好物周期（阶段 6）
    private const string K_FENCE_AMT = "fenceAmount";     // 待销赃累计价值（喂入违禁品累加，点「销赃」才带走）
    private const string K_FENCE_PENDING = "fencePending"; // 本次销赃额（点击销赃时锁定，回归后 FenceReturn 读）
    private const string K_FENCE_CAT = "fenceCat";         // 销赃带回类别 0=随机 1=食物饮品 2=日用品 3=武器工具（面板按钮循环切换）
    private const string K_LEAVE_REASON = "leaveReason";  // 消失原因 0=偷钱 1=销赃 2=跑路（阶段 6）
    private const int STEAL_INTERVAL = 7; // 偷钱周期（天）


    static WageGirlSystem()
    {
        // 09-19 删除占位图标：用 13 状态动画帧
    }

    // ===================== 状态读写（全局 NS 随档） =====================
    internal static int GetStat(string k) { try { return PerkStatePersistence.GetInt(NS, k, STAT_INIT); } catch { return STAT_INIT; } }
    internal static void SetStat(string k, int v)
    {
        try { PerkStatePersistence.SetInt(NS, k, Math.Max(0, Math.Min(STAT_MAX, v))); } catch { }
    }
    internal static int GetSleepDebt() { try { return PerkStatePersistence.GetInt(NS, K_SLEEP_DEBT, 0); } catch { return 0; } }
    internal static void SetSleepDebt(int v) { try { PerkStatePersistence.SetInt(NS, K_SLEEP_DEBT, Math.Max(0, v)); } catch { } }
    internal static int GetAffection() { try { return PerkStatePersistence.GetInt(NS, K_AFF, 0); } catch { return 0; } }
    internal static void SetAffection(int v) { try { PerkStatePersistence.SetInt(NS, K_AFF, Math.Max(0, Math.Min(AFF_MAX, v))); } catch { } }
    internal static bool Exists() { try { return PerkStatePersistence.GetInt(NS, K_EXIST, 0) == 1; } catch { return false; } }

    internal static void SetExists(bool v) { try { PerkStatePersistence.SetInt(NS, K_EXIST, v ? 1 : 0); } catch { } }

    // 蛙娘全部持久化 key（清 default_run 残留用）
    private static readonly string[] ALL_KEYS = new string[]
    {
        K_SAT, K_TH, K_HEALTH, K_MOOD, K_CLEAN, K_SLEEP, K_SLEEP_DEBT, K_AFF, K_LAST_STEAL, K_LEAVE,
        K_STARVE, K_EXIST, K_STEAL_AMT, K_LAST_GIFT, K_FENCE_AMT, K_FENCE_PENDING, K_FENCE_CAT, K_LEAVE_REASON
    };

    // 09-22 新档防串档：清 default_run 的蛙娘残留（A 档开局 runID 空时写的一次性 key 残留 → 新档误读误判）
    internal static void CleanDefaultRunOnNewGame()
    {
        try { PerkStatePersistence.CleanDefaultRun(NS, ALL_KEYS); } catch { }
    }

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
            it.shortDescription = LangHelper.T("蛙娘——蛙哥（Wage）留下的仿生女仆实体：会自己吃喝、干活，心情不好还会偷拿你的钱和货。照顾好她，她会帮你叫客、抬价、销赃。双击打开状态面板。", "Wage Girl - a biomimetic maid entity left by Wage: she eats and works on her own, and when moody she steals your money and goods. Take care of her and she'll call customers, boost prices and fence for you. Double-click to open her status panel.");
            it.longDescription = it.shortDescription;
            it.unitValue = 0; it.unitBaseValue = 0; // 09-19 价值归零：客户不买
            // 2×3 占地（09-21 用户拍板）
            try { var gsb = new GridShapeBuilder(); gsb.SetDataFill(2, 3); it.SetShape(gsb.Build()); } catch { }
            return it;
        }
        catch (Exception ex) { Core.LogMsg("[蛙娘] 创建失败: " + ex.Message); return null; }
    }

    private static void ApplyIcon(GameItem it)
    {
        try { it.SetSpriteAndShape(ICON_ATLAS, ICON); }
        catch { try { it.SetSpriteAndShape("custom_atlas", "custom_storage_box_sprite"); } catch { } }
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

    // ===================== 常驻面板（照鲁滨逊 RefreshStatusPanel） =====================
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
            b.AddLabel(LangHelper.T("饱食 ", "Satiety ") + GetStat(K_SAT) + "/100", "wg_sat_l");
            b.AddProgressBar(GetStat(K_SAT) / 100f, "wg_sat");
            b.AddLabel(LangHelper.T("口渴 ", "Thirst ") + GetStat(K_TH) + "/100", "wg_th_l");
            b.AddProgressBar(GetStat(K_TH) / 100f, "wg_th");
            b.AddLabel(LangHelper.T("健康 ", "Health ") + GetStat(K_HEALTH) + "/100", "wg_h_l");
            b.AddProgressBar(GetStat(K_HEALTH) / 100f, "wg_h");
            b.AddLabel(LangHelper.T("心情 ", "Mood ") + GetStat(K_MOOD) + "/100", "wg_m_l");
            b.AddProgressBar(GetStat(K_MOOD) / 100f, "wg_m");
            b.AddLabel(LangHelper.T("清洁 ", "Cleanliness ") + GetStat(K_CLEAN) + "/100", "wg_c_l");
            b.AddProgressBar(GetStat(K_CLEAN) / 100f, "wg_c");
            b.AddLabel(LangHelper.T("睡眠 ", "Sleep ") + GetStat(K_SLEEP) + "/100", "wg_s_l");
            b.AddProgressBar(GetStat(K_SLEEP) / 100f, "wg_s");
            // 外出/离家中：不显示销赃按钮（人不在店里——09-22 用户拍板）
            int leaveChk = PerkStatePersistence.GetInt(NS, K_LEAVE, 0);
            bool isOutChk = leaveChk > 0 && CurrentDay() < leaveChk;
            if (!isOutChk)
            {
                // 销赃类别按钮（09-22 用户拍板：可选项，点击循环切换：随机/食物饮品/日用品/武器工具）
                try
                {
                    string[] cats = { LangHelper.T("随机", "Random"), LangHelper.T("食物饮品", "Food/Drink"), LangHelper.T("日用品", "Daily"), LangHelper.T("武器工具", "Weapon/Tool"), LangHelper.T("物资箱", "Supply Crate"), LangHelper.T("指挥卡", "Keycard"), LangHelper.T("医药品", "Medicine"), LangHelper.T("模板", "Module") };
                    int curCat = PerkStatePersistence.GetInt(NS, K_FENCE_CAT, 0);
                    var catBtnOnClick = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((System.Action)(() => { try { int c = PerkStatePersistence.GetInt(NS, K_FENCE_CAT, 0) + 1; if (c > 7) c = 0; PerkStatePersistence.SetInt(NS, K_FENCE_CAT, c); ShowPanel(); } catch (Exception ex) { Core.LogMsg("[蛙娘] 类别切换异常: " + ex.Message); } }));
                    b.AddButton(LangHelper.T("销赃类别：" + cats[curCat], "Fence type: " + cats[curCat]), catBtnOnClick, "wg_fence_cat_btn");
                }
                catch { }
                // 销赃按钮（09-22 用户拍板：喂入违禁品累计，点按钮才出发；按钮文本带待销价值）
                try
                {
                    int famt = PerkStatePersistence.GetInt(NS, K_FENCE_AMT, 0);
                    var fenceBtnOnClick = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((System.Action)(() => { try { TryFence(); } catch (Exception ex) { Core.LogMsg("[蛙娘] 销赃异常: " + ex.Message); } }));
                    b.AddButton(LangHelper.T("销赃（待销 " + famt + "）", "Fence (" + famt + ")"), fenceBtnOnClick, "wg_fence_btn");
                }
                catch { }
            }
            b.AddLabel(LangHelper.T("蛙娘特性（3点）：喂食/照顾提升六维与好感；状态低会偷钱偷拿，连续不佳跑路14天。在场：客户预算×4、议价+50。", "Wage Girl perk (3 pts): feed & care raise stats & affection; low stats trigger stealing, neglect triggers 14-day leave. Present: budget x4, bargain +50."), "wg_note");
            // 外出/离家出走状态（阶段 5+6：偷钱/销赃 1 天外出，跑路 14 天）
            try
            {
                int leave = PerkStatePersistence.GetInt(NS, K_LEAVE, 0);
                int today = CurrentDay();
                if (leave > 0 && today < leave)
                {
                    int reason = PerkStatePersistence.GetInt(NS, K_LEAVE_REASON, 0);
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
            long newBudget = (long)budget * 4;
            if (newBudget > 2147483646L) newBudget = 2147483646L;
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
            try { if (item.IsTag("TAG_NOT_PURCHASED") || item.IsTag("not_purchased")) return false; } catch { } // 未拥有物品不吃
            if (Patches.CurrentUITradeMode != 0) return false;
            // 09-22 阶段 6：违禁品 → 像命运骰子一样吃掉（销毁）→ 累计待销赃（点面板「销赃」才出发）
            if (IsContraband(item))
            {
                // 09-22 用户拍板：按"预估价值"累计（GetCurrentValue 优先——命运骰子吃物品同读口；失败退 unitValue）
                long v = 0;
                try { v = (int)item.GetCurrentValue(); } catch { }
                if (v <= 0) { try { v = item.unitValue; } catch { } }
                if (v <= 0) return false;
                try { item.Destroy(); } catch { try { item.parentInventory?.Expel(item); } catch { } }
                int cur = PerkStatePersistence.GetInt(NS, K_FENCE_AMT, 0);
                int total = cur + (int)v;
                PerkStatePersistence.SetInt(NS, K_FENCE_AMT, total);
                ReportLine(LangHelper.T("蛙娘吃下了违禁品（累计 " + total + " 价值待销赃——点面板「销赃」出发）", "Wage Girl devoured contraband (" + total + " to fence - press Fence)"));
                try { if (Il2Cpp.CustomUIManager.Instance != null && Il2Cpp.CustomUIManager.Instance.IsOpen("wage_girl_panel")) ShowPanel(); } catch { }
                SetAnimMode(2); // 09-22 吃掉瞬间切偷动画（播完回待机）
                return true;
            }
            int gain = 0; int aff = 1; string msg = "";
            if (RobinCrusoePerk.IsDailyNeed(item)) { gain = 20; aff = 2; msg = LangHelper.T("蛙娘洗得干干净净、心情大好！清洁 +20 心情 +10（照顾）", "Wage Girl cleaned up & cheered up! Cleanliness +20 Mood +10 (care)"); SetStat(K_CLEAN, GetStat(K_CLEAN) + gain); SetStat(K_MOOD, GetStat(K_MOOD) + 10); SetStat(K_HEALTH, GetStat(K_HEALTH) + 15); }
            else if (RobinCrusoePerk.IsFood(item)) { gain = 25; aff = 1; msg = LangHelper.T("蛙娘吃饱了！饱食 +25", "Wage Girl ate! Satiety +25"); SetStat(K_SAT, GetStat(K_SAT) + gain); SetStat(K_HEALTH, GetStat(K_HEALTH) + 15); }
            else if (RobinCrusoePerk.IsDrink(item)) { gain = 25; aff = 1; msg = LangHelper.T("蛙娘喝饱了！口渴 +25", "Wage Girl drank! Thirst +25"); SetStat(K_TH, GetStat(K_TH) + gain); SetStat(K_HEALTH, GetStat(K_HEALTH) + 15); }
            else return false;
            SetAffection(GetAffection() + aff);
            // 消耗源物品（吃掉）：Destroy → Expel 兜底（照命运骰子吸收）
            try { item.Destroy(); } catch { try { item.parentInventory?.Expel(item); } catch { } }
            try { StoreUIManager.Instance.Notify(msg, "green"); } catch { }
            try { if (Il2Cpp.CustomUIManager.Instance != null && Il2Cpp.CustomUIManager.Instance.IsOpen("wage_girl_panel")) ShowPanel(); } catch { }
            SetAnimMode(2); // 09-22 吃掉瞬间切偷动画（播完回待机）
            return true;
        }
        catch (Exception ex) { Core.LogMsg("[蛙娘] 喂食异常: " + ex.Message); return false; }
    }

    // ===================== 双击（全局——不依赖任何特性） =====================
    public static void PostfixDoubleClickAction(GameItem newItem, Vector2 mousePosition)
    {
        try
        {
            if (newItem == null) return;
            if (newItem.identifier != ENTITY_ID) return;
            if (Patches.CurrentUITradeMode != 0) return;
            ShowPanel();
        }
        catch { }
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
            SetExists(true); // 09-22 每天幂等写——防 default_run 残留（exists 只在首次分支写会永久残留，跨档污染）
            // 六维每日衰减（睡眠除外——仿生女仆夜间自然恢复睡眠）
            foreach (var k in new[] { K_SAT, K_TH, K_HEALTH, K_MOOD, K_CLEAN })
                SetStat(k, GetStat(k) - DAILY_DECAY);
            // 睡眠自然增长（过夜充电/睡觉恢复）
                        // 09-23 睡眠 debt 机制：先扣债（偷钱/销赃/偷拿熬夜）再自然恢复
            int sleepDebt = GetSleepDebt(); SetSleepDebt(0);
            SetStat(K_SLEEP, GetStat(K_SLEEP) - sleepDebt + 15);
            // 好感每日回落（不照顾）
            SetAffection(GetAffection() - AFF_DAILY_DROP);
            // 阶段 5：回归 / 自主偷拿 / 偷钱循环
            RunDayEvents();
        }
        catch { }
    }

    // ===================== 阶段 5：偷钱循环 + 自主偷拿 + 回归（话术 v9） =====================
    private static int CurrentDay()
    {
        try { return Il2Cpp.StoreStation.GetDayCounter(); } catch { return 1; }
    }

    // 夜报三件套（09-17 统一规范：原生 AddNightLog + mod 队列 + 弹窗）
    private static void ReportLine(string line)
    {
        try { var ps = Il2Cpp.PlayerStore.Instance; if (ps != null) ps.AddNightLog(line, "#7FC97F"); } catch { } // 09-22 统一柔和绿
        try { Core.AddNightReportLine(line); } catch { }
        try { Il2Cpp.StoreUIManager.Instance.Notify(line); } catch { }
    }

    private static void ModCashN(int n)
    {
        try { var ps = Il2Cpp.PlayerStore.Instance; if (ps != null) ps.ModCash(n); } catch { }
    }

    // 每日事件：回归（按原因分支）→（消失期不活动）→ 跑路检查 → 初次偷拿 → 日常偷拿 → 好物 → 偷钱循环
    private static void RunDayEvents()
    {
        try
        {
            if (!Exists()) return; // 09-23 Perk 化：未出现不活动（双保险，修复没选 Perk 也偷钱）
            int day = CurrentDay();
            int leaveDay = PerkStatePersistence.GetInt(NS, K_LEAVE, 0);
            // 1) 回归（到达回归日）→ 按原因分支 → 实体重新发放
            if (leaveDay > 0 && day >= leaveDay)
            {
                int reason = PerkStatePersistence.GetInt(NS, K_LEAVE_REASON, 0);
                PerkStatePersistence.SetInt(NS, K_LEAVE, 0);
                PerkStatePersistence.SetInt(NS, K_LEAVE_REASON, 0);
                if (reason == 1) FenceReturn();
                else if (reason == 2) RunawayReturn();
                else
                {
                    int amt = PerkStatePersistence.GetInt(NS, K_STEAL_AMT, 0);
                    if (amt > 0) { GiveBackItem(amt); PerkStatePersistence.SetInt(NS, K_STEAL_AMT, 0); }
                    else ReportLine(LangHelper.T("蛙娘回来了", "Wage Girl is back"));
                }
                TryGiveToBackpack(); // 实体重新发放（消失期实体已移除）
                return; // 回归日不触发其他事件
            }
            // 2) 消失期：不偷拿不偷钱
            if (leaveDay > 0 && day < leaveDay) return;
            // 3) 跑路检查：连续 3 天任一六维 <20 → 离家出走 14 天
            int lowStreak = PerkStatePersistence.GetInt(NS, K_STARVE, 0);
            if (IsAnyStatLow())
            {
                lowStreak++;
                PerkStatePersistence.SetInt(NS, K_STARVE, lowStreak);
            }
            else PerkStatePersistence.SetInt(NS, K_STARVE, 0);
            if (lowStreak >= 3)
            {
                PerkStatePersistence.SetInt(NS, K_STARVE, 0);
                PerkStatePersistence.SetInt(NS, K_LEAVE, day + 14);
                PerkStatePersistence.SetInt(NS, K_LEAVE_REASON, 2);
                _curState = "away"; _curAnimSprites = _spAway; _stateFrameSec = 0.125f; _leavingTimer = 0.5f; // 播away挥手帧再移除
                ReportLine(LangHelper.T("蛙娘连续几天没吃好没睡好，离家出走了（14 天后回来）", "Wage Girl ran away after days of neglect (back in 14 days)"));
                return;
            }
            int lastSteal = PerkStatePersistence.GetInt(NS, K_LAST_STEAL, 0);
            // 4) 初次偷拿（lastSteal==0 → 初次偷 1 件 + 50 钱，然后设 today）
            if (lastSteal <= 0)
            {
                System.Collections.Generic.List<string> stolenNames0 = null;
                int stolen = StealItems("random", 1, "highest", out stolenNames0);
                ModCashN(-50); // 09-19 新档第一天必偷50（不管有没有东西）
                // 09-19 修：初次偷拿补夜报（原分支扣钱偷物后直接 return，无 ReportLine → 初次见面夜报缺失）
                if (stolen > 0 && stolenNames0 != null && stolenNames0.Count > 0)
                    ReportLine(LangHelper.T("蛙娘偷走了 50 块钱和" + string.Join("、", stolenNames0), "Wage Girl stole 50 credits and " + string.Join(", ", stolenNames0)));
                else
                    ReportLine(LangHelper.T("蛙娘偷走了 50 块钱", "Wage Girl stole 50 credits"));
                PerkStatePersistence.SetInt(NS, K_LAST_STEAL, day);
                return;
            }
            // 心情>=80 自动归还偷的东西
            try {
                int sv = PerkStatePersistence.GetInt(NS, "stolenValue", 0);
                if (GetStat(K_MOOD) >= 80 && sv > 0) {
                    GiveBackItem(sv);
                    PerkStatePersistence.SetInt(NS, "stolenValue", 0);
                    ReportLine(LangHelper.T("蛙娘心情大好，把之前偷的东西都还回来了", "Wage Girl is in a great mood and returned everything she stole"));
                }
            } catch { }
            // 5) 日常自主偷拿（状态触发）
            TrySnatch();
            // 6) 好物：好感 ≥50 每 7 天带 1 件
            int lastGift = PerkStatePersistence.GetInt(NS, K_LAST_GIFT, 0);
            if (GetAffection() >= 50 && day - lastGift >= 7)
            {
                GiveGift();
                PerkStatePersistence.SetInt(NS, K_LAST_GIFT, day);
            }
            // 7) 偷钱循环（≥7 天）——K_LEAVE 改"回归日"语义（day+1）
            if (day - lastSteal >= STEAL_INTERVAL)
            {
                int aff = GetAffection();
                int steal = 50 + (int)((aff / 100f) * 450f); // 好感 0→50、100→500
                ModCashN(-steal);
                PerkStatePersistence.SetInt(NS, K_STEAL_AMT, steal);
                                SetSleepDebt(GetSleepDebt() + 20); // 偷钱外出熬夜 -20 睡眠（次日结算）
PerkStatePersistence.SetInt(NS, K_LEAVE, day + 1); // 回归日 = 明天
                PerkStatePersistence.SetInt(NS, K_LEAVE_REASON, 0);
                PerkStatePersistence.SetInt(NS, K_LAST_STEAL, day);
                _curState = "away"; _curAnimSprites = _spAway; _stateFrameSec = 0.125f; _leavingTimer = 0.5f; // 播away挥手帧再移除
                ReportLine(LangHelper.T("蛙娘偷走了 " + steal + " 块钱，出门躲债去了（明天回来）", "Wage Girl stole " + steal + " credits and went out (back tomorrow)"));
            }
        }
        catch { }
    }

    // 日常自主偷拿：饥饿/口渴/心情差自拿对应类恢复；心情好随机顺走（件数/价值按好感）
    private static void TrySnatch()
    {
        try
        {
            int sat = GetStat(K_SAT), th = GetStat(K_TH), mood = GetStat(K_MOOD);
            string mode = null; string msg = null;
            if (sat < 30) { mode = "food"; msg = "蛙娘饿坏了，偷吃了你的食物"; }
            else if (th < 30) { mode = "drink"; msg = "蛙娘渴坏了，偷喝了你的饮品"; }
            else if (mood < 30) { mode = "care"; msg = "蛙娘心情很差，拿走了你的日用品"; }
            else if (mood >= 60) { mode = "random"; msg = "蛙娘心情不错，顺走了你点小东西"; }
            else return;
            int aff = GetAffection();
            int count = aff < 30 ? 1 : (aff < 70 ? 2 : 3);
            string valueMode = mood >= 60 ? "low" : "highest"; // 心情好偷低值/心情差偷高值
            int stolen = StealItems(mode, count, valueMode, out var stolenNames);
            if (stolen > 0)
            {
                SetSleepDebt(GetSleepDebt() + 10); // 偷拿熬夜 -10 睡眠（次日结算）
                if (mode == "food") SetStat(K_SAT, GetStat(K_SAT) + 30);
                else if (mode == "drink") SetStat(K_TH, GetStat(K_TH) + 30);
                else if (mode == "care") { SetStat(K_MOOD, GetStat(K_MOOD) + 20); SetStat(K_CLEAN, GetStat(K_CLEAN) + 10); }
                string sn = (stolenNames != null && stolenNames.Count > 0) ? "：" + string.Join("、", stolenNames) : "";
                ReportLine(LangHelper.T(msg + sn + "（" + stolen + " 件）", msg + " (" + (stolenNames != null ? string.Join(", ", stolenNames) : "") + ", " + stolen + " items)"));
            }
        }
        catch { }
    }

    // 从店里找目标偷拿：mode 限定类别；count 件数；valueMode highest/random/low
    private static int StealItems(string mode, int count, string valueMode, out System.Collections.Generic.List<string> stolenNames)
    {
        stolenNames = new System.Collections.Generic.List<string>();
        try
        {
            EmporiumEntry em = EmporiumEntry.Instance;
            if (em == null) return 0;
            var invs = new GameInventory[] {
                (GameInventory)em.frontInvinvElement,
                (GameInventory)em.showcaseElement,
                (GameInventory)em.invElement
            };
            var candidates = new System.Collections.Generic.List<GameItem>();
            foreach (var inv in invs)
            {
                if (inv == null || inv.childItems == null) continue;
                for (int i = 0; i < inv.childItems.Count; i++)
                {
                    var it = inv.childItems[i];
                    if (it == null) continue;
                    if (it.identifier == ENTITY_ID) continue;
                    if (it.IsTag("STANDARD_MACHINE_TAG") || it.IsTag("CONTAINER_TAG")) continue;
                    if (mode == "food" && !RobinCrusoePerk.IsFood(it)) continue;
                    if (mode == "drink" && !RobinCrusoePerk.IsDrink(it)) continue;
                    if (mode == "care" && !RobinCrusoePerk.IsDailyNeed(it)) continue;
                    candidates.Add(it);
                }
            }
            if (candidates.Count == 0) return 0;
            var picked = new System.Collections.Generic.List<GameItem>();
            if (valueMode == "highest")
            {
                GameItem best = candidates[0];
                foreach (var c in candidates) if (c.unitValue > best.unitValue) best = c;
                picked.Add(best);
            }
            else if (valueMode == "low")
            {
                var pool = new System.Collections.Generic.List<GameItem>(candidates);
                pool.Sort((a, b) => a.unitValue.CompareTo(b.unitValue));
                for (int i = 0; i < Math.Min(count, pool.Count); i++) picked.Add(pool[i]);
            }
            else
            {
                var pool = new System.Collections.Generic.List<GameItem>(candidates);
                for (int i = 0; i < Math.Min(count, pool.Count); i++)
                {
                    int idx = Core.Rng.Next(pool.Count);
                    picked.Add(pool[idx]);
                    pool.RemoveAt(idx);
                }
            }
            int stolen = 0; long stolenVal = 0;
            foreach (var p in picked)
            {
                try { stolenVal += p.unitValue; } catch { }
                try { p.Destroy(); stolen++; } catch { try { if (p.parentInventory != null) { p.parentInventory.Expel(p); stolen++; } } catch { } }
            }
            try { PerkStatePersistence.SetInt(NS, "stolenValue", PerkStatePersistence.GetInt(NS, "stolenValue", 0) + (int)Math.Min(stolenVal, int.MaxValue)); } catch { }
            return stolen;
        }
        catch { stolenNames = null; return 0; }
    }

    // 回归带物：按偷钱额 × 好感比例预算，随机生成物品（单件/累计价值 ≤ 预算）放入柜台
    private static void GiveBackItem(int stealAmt)
    {
        try
        {
            int aff = GetAffection();
            float ratio = 0.2f + (aff / 100f) * 0.6f; // 好感 0→20%、100→80%
            long budget = Math.Max(1, (int)(stealAmt * ratio));
            EmporiumEntry em = EmporiumEntry.Instance;
            var ids = DirectoryMaster.GetIdentifierList<GameItem>(null);
            if (ids == null || ids.Count == 0) { ReportLine(LangHelper.T("蛙娘回来了（没带什么值钱的东西）", "Wage Girl is back (empty-handed)")); return; }
            var pool = new System.Collections.Generic.List<string>();
            for (int i = 0; i < ids.Count; i++)
            {
                if (string.IsNullOrEmpty(ids[i]) || ids[i] == ENTITY_ID) continue;
                if (System.Array.IndexOf(Core.ExcludedItemIds, ids[i]) >= 0) continue; // 全局黑名单（rare_electronic 等）
                pool.Add(ids[i]);
            }
            long spent = 0;
            var names = new System.Collections.Generic.List<string>();
            for (int i = 0; i < 2 && pool.Count > 0 && spent < budget; i++)
            {
                GameItem it = null; string id = null; int tries = 0;
                while (tries < 12 && pool.Count > 0)
                {
                    int idx = Core.Rng.Next(pool.Count);
                    id = pool[idx];
                    try
                    {
                        it = DirectoryMaster.Item(id, true);
                        if (it == null) { pool.RemoveAt(idx); tries++; continue; }
                        if (it.IsTag("STANDARD_MACHINE_TAG") || it.IsTag("CONTAINER_TAG")) { pool.RemoveAt(idx); tries++; continue; }
                        long v = it.unitValue;
                        if (v <= 0 || v > budget - spent) { pool.RemoveAt(idx); tries++; continue; } // 超预算/无价值 → 换
                        break;
                    }
                    catch { pool.RemoveAt(idx); tries++; }
                }
                if (it == null) continue;
                spent += it.unitValue;
                try
                {
                    if (em != null && em.frontInvinvElement != null)
                    {
                        var inv = (GameInventory)em.frontInvinvElement;
                        var slot = em.frontInvinvElement.TryFindOneValidInventorySlot(it, false);
                        if (slot != null) { try { slot.TryAcceptOnce(); } catch { } }
                        else inv.UncheckedAccept(it);
                        names.Add(ModCannibalism.GetName(it));
                    }
                }
                catch { }
            }
            if (names.Count > 0) ReportLine(LangHelper.T("蛙娘回来了，带了点东西回来：" + string.Join("、", names), "Wage Girl is back with: " + string.Join(", ", names)));
            else ReportLine(LangHelper.T("蛙娘回来了（没带什么值钱的东西）", "Wage Girl is back (empty-handed)"));
        }
        catch { }
    }

    // 偷钱消失：从场景所有网格 + 容器内部移除蛙娘实体（回归时 TryGiveToBackpack 重发）
    private static void RemoveGirlFromScene()
    {
        try
        {
            EmporiumEntry em = EmporiumEntry.Instance;
            if (em == null) return;
            var invs = new GameInventory[] {
                (GameInventory)em.invElement,
                (GameInventory)em.backInvinvElement,
                (GameInventory)em.backInvinvElementCounter,
                (GameInventory)em.frontInvinvElement,
                (GameInventory)em.showcaseElement
            };
            foreach (var inv in invs) RemoveGirlFromInv(inv);
            // 兜底：全量找蛙娘移除（含 5 网格外/容器内部漏网）
            try
            {
                var all = PlayerStore.Instance.FindAllItem();
                if (all != null)
                {
                    for (int i = 0; i < all.Count; i++)
                    {
                        var it = all[i];
                        if (it == null || it.identifier != ENTITY_ID) continue;
                        try { it.parentInventory?.Expel(it); } catch { }
                        try { it.Destroy(); } catch { }
                    }
                }
            }
            catch { }
        }
        catch { }
    }

    // 单个网格内移除蛙娘（含顶层容器 contentWindow 内部递归）
    private static void RemoveGirlFromInv(GameInventory inv)
    {
        if (inv == null || inv.childItems == null) return;
        for (int i = inv.childItems.Count - 1; i >= 0; i--)
        {
            try
            {
                var it = inv.childItems[i];
                if (it == null) continue;
                if (it.identifier == ENTITY_ID)
                {
                    try { it.parentInventory?.Expel(it); } catch { }
                    try { it.Destroy(); } catch { }
                    try { inv.childItems.RemoveAt(i); } catch { }
                    continue;
                }
                if (it.contentWindow != null)
                {
                    var inner = AddictOfficerEvent.GetInnerInventory(it);
                    if (inner != null) RemoveGirlFromInv(inner);
                }
            }
            catch { }
        }
    }

    // 发放前全范围查重：5 网格 + 容器内部已有蛙娘 → true
    private static bool ExistsInScene()
    {
        try
        {
            EmporiumEntry em = EmporiumEntry.Instance;
            if (em == null) return false;
            var invs = new GameInventory[] {
                (GameInventory)em.invElement,
                (GameInventory)em.backInvinvElement,
                (GameInventory)em.backInvinvElementCounter,
                (GameInventory)em.frontInvinvElement,
                (GameInventory)em.showcaseElement
            };
            foreach (var inv in invs)
            {
                if (FindGirlInInv(inv)) return true;
            }
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
            if (em == null) { return; }
            if (em.backInvinvElement == null) { return; }
            // 09-19 发放前全范围查重（5 网格+容器内部已有 → 不重复发）
            if (ExistsInScene()) { return; }
            var inv = (GameInventory)em.backInvinvElement;
            GameItem item = DirectoryMaster.Item(ENTITY_ID, true);
            if (item == null) { return; }
            // 照 GiveToBackpack 先例：TryFindOneValidInventorySlot → TryAcceptOnce（防同格重叠）；失败 UncheckedAccept 兜底
            var slot = em.backInvinvElement.TryFindOneValidInventorySlot(item, false);
            if (slot != null) { try { slot.TryAcceptOnce(); return; } catch (Exception) { } }
            inv.UncheckedAccept(item);
            Core.LogMsg("[蛙娘] 已发放实体到背包（全局常驻）");
        _returnTimer = 0.5f; _curState = ""; // 强制播return帧0.5s再切idle
        }
        catch (Exception ex) { Core.LogMsg("[蛙娘] 发放失败: " + ex.Message); }
    }

    // ===================== 阶段 6：好物 / 销赃 / 跑路回归（09-22 用户拍板并行） =====================

    // 违禁品判定（原生读口，全等级覆盖）
    private static bool IsContraband(GameItem it)
    {
        try { return Il2Cpp.ContrabandHelper.GetContrabandLevel(it) > 0; } catch { return false; }
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
                var mpool = FrogPowerPerk.ItemPool;
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
                        if (g.unitValue < 500) { pool.RemoveAt(idx); tries++; continue; } // 好物价值 ≥500
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

    // 面板「销赃」按钮：带走当前累计待销赃价值，消失 2 天（第 2 天整天消失、第 3 天回）
    private static void TryFence()
    {
        try
        {
            if (!Exists()) return;
            if (Patches.CurrentUITradeMode != 0) { try { Il2Cpp.StoreUIManager.Instance.Notify(LangHelper.T("交易模式下不能销赃", "Can't fence while trading"), "orange"); } catch { } return; }
            int leave = PerkStatePersistence.GetInt(NS, K_LEAVE, 0);
            if (leave > 0 && CurrentDay() < leave) { try { Il2Cpp.StoreUIManager.Instance.Notify(LangHelper.T("蛙娘不在店里", "Wage Girl is out"), "orange"); } catch { } return; }
            int amt = PerkStatePersistence.GetInt(NS, K_FENCE_AMT, 0);
            if (amt <= 0) { try { Il2Cpp.StoreUIManager.Instance.Notify(LangHelper.T("没有违禁品可销——先拖违禁品给蛙娘吃掉", "No contraband to fence - feed her contraband first"), "orange"); } catch { } return; }
            PerkStatePersistence.SetInt(NS, K_FENCE_PENDING, amt);
            PerkStatePersistence.SetInt(NS, K_FENCE_AMT, 0);
                        SetSleepDebt(GetSleepDebt() + 20); // 销赃外出熬夜 -20 睡眠（次日结算）
PerkStatePersistence.SetInt(NS, K_LEAVE, CurrentDay() + 2);
            PerkStatePersistence.SetInt(NS, K_LEAVE_REASON, 1);
            _curState = "away"; _curAnimSprites = _spAway; _stateFrameSec = 0.125f; _leavingTimer = 0.5f; // 播away挥手帧再移除
            ReportLine(LangHelper.T("蛙娘带着 " + amt + " 价值的货出去销赃了（后天回来）", "Wage Girl took " + amt + " worth of goods to fence (back in 2 days)"));
            try { if (Il2Cpp.CustomUIManager.Instance != null && Il2Cpp.CustomUIManager.Instance.IsOpen("wage_girl_panel")) ShowPanel(); } catch { }
        }
        catch (Exception ex) { Core.LogMsg("[蛙娘] 销赃异常: " + ex.Message); }
    }

    // 销赃回归：按所选类别拆成多件带回（每件 ≤ 单件目标、最接近；总价值 ≤ 目标×1.3；跑腿费 10% 起随好感降）
    private static void FenceReturn()
    {
        try
        {
            int amt = PerkStatePersistence.GetInt(NS, K_FENCE_PENDING, 0);
            PerkStatePersistence.SetInt(NS, K_FENCE_PENDING, 0);
            if (amt <= 0) { ReportLine(LangHelper.T("蛙娘销赃回来了", "Wage Girl is back from fencing")); return; }
            int aff = GetAffection();
            float fee = 0.10f - (aff / 1000f);
            if (fee < 0f) fee = 0f;
            long target = (long)(amt * (1f - fee));
            if (target < 1) target = 1;
            int cat = PerkStatePersistence.GetInt(NS, K_FENCE_CAT, 0);
            // cat==4 物资箱：CreateLootCrate 随机箱 + 内部按 ItemPool 填充到目标价值
            if (cat == 4)
            {
                GameItem crate = CreateSupplyCrate(target);
                if (crate != null) { AddToFront(crate); ReportLine(LangHelper.T("蛙娘销赃回来了，带了一只物资箱", "Wage Girl fenced and brought a supply crate")); }
                else ReportLine(LangHelper.T("蛙娘销赃回来了（没弄到箱子）", "Wage Girl is back (no crate)"));
                return;
            }
            // cat==5 指挥卡：cmd_keycard + 差额按随机物品补足
            if (cat == 5)
            {
                GameItem kc = null;
                try { kc = DirectoryMaster.Item("cmd_keycard", true); } catch { }
                long kcVal = 0;
                var names5 = new System.Collections.Generic.List<string>();
                if (kc != null) { AddToFront(kc); kcVal = kc.unitValue; names5.Add(LangHelper.T("指挥卡","Keycard")); }
                long remain = target - kcVal;
                int n5 = (int)Math.Max(1, Math.Min(5, remain / 500));
                long per5 = remain / Math.Max(1, n5);
                long spent5 = 0;
                for (int i = 0; i < n5 && spent5 < remain; i++)
                {
                    GameItem it5 = FindItemNearValue(Math.Min(per5, remain - spent5), 0, false);
                    if (it5 == null) break;
                    AddToFront(it5); spent5 += it5.unitValue; names5.Add(ModCannibalism.GetName(it5));
                }
                if (names5.Count > 0) ReportLine(LangHelper.T("蛙娘销赃回来了，带了：" + string.Join("、", names5), "Wage Girl fenced and brought: " + string.Join(", ", names5)));
                else ReportLine(LangHelper.T("蛙娘销赃回来了", "Wage Girl is back"));
                return;
            }
            // cat==6 医药品：必带免疫宁(large_purple_injector) + 差额补医疗物品
            if (cat == 6)
            {
                GameItem im = null;
                try { im = DirectoryMaster.Item("large_purple_injector", true); } catch { }
                long imVal = 0;
                var names6 = new System.Collections.Generic.List<string>();
                if (im != null) { AddToFront(im); imVal = im.unitValue; names6.Add(LangHelper.T("免疫宁","Immunity Shot")); }
                long remain6 = target - imVal;
                int n6 = (int)Math.Max(1, Math.Min(5, remain6 / 500));
                long per6 = remain6 / Math.Max(1, n6);
                long spent6 = 0;
                for (int i = 0; i < n6 && spent6 < remain6; i++)
                {
                    GameItem it6 = FindItemNearValue(Math.Min(per6, remain6 - spent6), 0, false);
                    if (it6 == null) break;
                    AddToFront(it6); spent6 += it6.unitValue; names6.Add(ModCannibalism.GetName(it6));
                }
                if (names6.Count > 0) ReportLine(LangHelper.T("蛙娘销赃回来了，带了：" + string.Join("、", names6), "Wage Girl fenced and brought: " + string.Join(", ", names6)));
                else ReportLine(LangHelper.T("蛙娘销赃回来了", "Wage Girl is back"));
                return;
            }
            // cat==7 模板：按价值拆件带回（复用 FindItemNearValue，模块优先）
            if (cat == 7)
            {
                int n7 = (int)Math.Max(1, Math.Min(5, target / 500));
                long per7 = target / Math.Max(1, n7);
                long spent7 = 0; var names7 = new System.Collections.Generic.List<string>();
                for (int i = 0; i < n7 && spent7 < target; i++)
                {
                    GameItem it7 = FindItemNearValue(Math.Min(per7, target - spent7), 7, false);
                    if (it7 == null) break;
                    AddToFront(it7); spent7 += it7.unitValue; names7.Add(ModCannibalism.GetName(it7));
                }
                if (names7.Count > 0) ReportLine(LangHelper.T("蛙娘销赃回来了，带了：" + string.Join("、", names7), "Wage Girl fenced and brought: " + string.Join(", ", names7)));
                else ReportLine(LangHelper.T("蛙娘销赃回来了", "Wage Girl is back"));
                return;
            }
            // 拆件：每 500 价值 1 件（1-5 件）；单件目标 = 总目标/件数
            int n = (int)Math.Max(1, Math.Min(5, target / 500));
            long perTarget = target / n;
            long spent = 0;
            var names = new System.Collections.Generic.List<string>();
            for (int i = 0; i < n && spent < target; i++)
            {
                long itemTarget = Math.Min(perTarget, target - spent);
                GameItem it = FindItemNearValue(itemTarget, cat, false); // 单件 ≤ 单件目标、最接近
                if (it == null) break;
                long v = it.unitValue;
                if (spent + v > target * 1.3) break; // 累计防超
                AddToFront(it);
                spent += v;
                names.Add(ModCannibalism.GetName(it));
            }
            if (names.Count > 0) ReportLine(LangHelper.T("蛙娘销赃回来了，带了：" + string.Join("、", names), "Wage Girl fenced and brought: " + string.Join(", ", names)));
            else ReportLine(LangHelper.T("蛙娘销赃回来了（没找到合适的货）", "Wage Girl is back (no good goods found)"));
        }
        catch { }
    }

    // 跑路回归：带最低维度对应类别礼物 + 先偷 1 件
    private static void RunawayReturn()
    {
        try
        {
            int sat = GetStat(K_SAT), th = GetStat(K_TH), health = GetStat(K_HEALTH), mood = GetStat(K_MOOD), clean = GetStat(K_CLEAN), sleep = GetStat(K_SLEEP);
            string mode = "food"; int min = sat;
            if (th < min) { min = th; mode = "drink"; }
            if (health < min) { min = health; mode = "medicine"; }
            if (mood < min) { min = mood; mode = "care"; }
            if (clean < min) { min = clean; mode = "care"; }
            if (sleep < min) { min = sleep; mode = "sleep"; }
            GameItem gift = FindCategoryItem(mode);
            if (gift != null)
                {
                    GameItem giftBox = CreateSupplyCrate(gift.unitValue);
                    if (giftBox != null) AddToFront(giftBox); else AddToFront(gift);
                    ReportLine(LangHelper.T("蛙娘回来了，带了份物资箱补偿你", "Wage Girl is back with a supply crate to make up"));
                }
            else ReportLine(LangHelper.T("蛙娘回来了", "Wage Girl is back"));
            int stolen = StealItems("random", 1, "highest", out var _);
            if (stolen > 0) ReportLine(LangHelper.T("……然后顺手偷了你 1 件东西", "...then swiped one of your things"));
        }
        catch { }
    }

    // 按类别找物品（跑路回归礼物用）
    private static GameItem FindCategoryItem(string mode)
    {
        try
        {
            var ids = DirectoryMaster.GetIdentifierList<GameItem>(null);
            if (ids == null || ids.Count == 0) return null;
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
                    bool match = false;
                    if (mode == "food" && RobinCrusoePerk.IsFood(g)) match = true;
                    else if (mode == "drink" && RobinCrusoePerk.IsDrink(g)) match = true;
                    else if (mode == "care" && RobinCrusoePerk.IsDailyNeed(g)) match = true;
                    else if (mode == "medicine" && (RobinCrusoePerk.IsFood(g) || RobinCrusoePerk.IsDailyNeed(g))) match = true;
                    else if (mode == "sleep") match = true; // 睡眠类无对应 → 随机 1 件
                    if (match) return g;
                    pool.RemoveAt(idx); tries++;
                }
                catch { pool.RemoveAt(idx); tries++; }
            }
            return null;
        }
        catch { return null; }
    }

    // ===================== 物品信息缓存（销赃精确匹配用；首次销赃回归时构建一次，此后复用） =====================
    private class ItemInfo
    {
        public long Value;
        public bool FoodDrink;
        public bool Daily;
        public bool WeaponTool;
        public bool Module;
    }
    private static System.Collections.Generic.Dictionary<string, ItemInfo> _itemInfoCache;
    private static void EnsureItemCache()
    {
        if (_itemInfoCache != null) return;
        try
        {
            _itemInfoCache = new System.Collections.Generic.Dictionary<string, ItemInfo>();
            var ids = DirectoryMaster.GetIdentifierList<GameItem>(null);
            if (ids == null) return;
            foreach (var id in ids)
            {
                try
                {
                    if (string.IsNullOrEmpty(id) || id == ENTITY_ID) continue;
                    if (System.Array.IndexOf(Core.ExcludedItemIds, id) >= 0) continue;
                    var g = DirectoryMaster.Item(id, true);
                    if (g == null) continue;
                    if (IsContraband(g)) continue; // 09-22 销赃带回过滤违禁品（只带合法货）
                    var info = new ItemInfo();
                    // 预估价值（GetCurrentValue 优先——与销赃累计口径一致；失败退 unitValue）
                    try { info.Value = (int)g.GetCurrentValue(); } catch { }
                    if (info.Value <= 0) { try { info.Value = g.unitValue; } catch { } }
                    info.FoodDrink = RobinCrusoePerk.IsFood(g) || RobinCrusoePerk.IsDrink(g);
                    info.Daily = RobinCrusoePerk.IsDailyNeed(g);
                    info.WeaponTool = IsWeaponOrTool(id);
            try {
                info.Module = false;
                if (g.IsTag("MODULE_TAG") && id != "system_module_ruined" && id != GuMachineSystem.AI_MODULE_ID) {
                    int p = RobinCrusoePerk.GetTagIntSafe(g, "BONUS_PERCENTAGE_PERFORMANCE_INT");
                    int e = RobinCrusoePerk.GetTagIntSafe(g, "BONUS_PERCENTAGE_EFFICIENCY_INT");
                    int q = RobinCrusoePerk.GetTagIntSafe(g, "BONUS_PERCENTAGE_QUALITY_INT");
                    info.Module = (p + e + q) > 0; // 三维至少一个>0
                }
            } catch { }
                    _itemInfoCache[id] = info;
                    try { g.Destroy(); } catch { }
                }
                catch { }
            }
        }
        catch { }
    }

    // 武器/工具类别判定（id 关键词匹配，照 IsToolOrKeyOrContainer 先例）
    private static bool IsWeaponOrTool(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        string i = id.ToLowerInvariant();
        return i.Contains("weapon") || i.Contains("tool") || i.Contains("knife") || i.Contains("gun")
            || i.Contains("pistol") || i.Contains("shotgun") || i.Contains("smg") || i.Contains("rifle")
            || i.Contains("tazer") || i.Contains("stun") || i.Contains("grenade") || i.Contains("baton")
            || i.Contains("machete") || i.Contains("sword") || i.Contains("axe") || i.Contains("c4")
            || i.Contains("screwdriver") || i.Contains("welder") || i.Contains("flashlight") || i.Contains("scanner")
            || i.Contains("hammer") || i.Contains("wrench") || i.Contains("surgery") || i.Contains("combat");
    }

    // 类别匹配（cat 0=随机 1=食物饮品 2=日用品 3=武器工具）
    private static bool CategoryMatch(ItemInfo info, int cat)
    {
        if (info == null) return false;
        if (cat == 0) return true;
        if (cat == 1) return info.FoodDrink;
        if (cat == 2) return info.Daily;
        if (cat == 3) return info.WeaponTool;
        if (cat == 7) return info.Module;
        return true;
    }

    // 找价值最接近 target 的普通物品（精确遍历缓存；geq=true 要求 ≥ target；false 要求 ≤ target）
    private static GameItem FindItemNearValue(long target, int cat, bool geq)
    {
        try
        {
            EnsureItemCache();
            if (_itemInfoCache == null) return null;
            string bestId = null; long bestDiff = long.MaxValue;
            foreach (var kv in _itemInfoCache)
            {
                var info = kv.Value;
                if (info == null || info.Value <= 0) continue;
                if (!CategoryMatch(info, cat)) continue;
                if (geq && info.Value < target) continue;
                if (!geq && info.Value > target) continue;
                long diff = Math.Abs(info.Value - target);
                if (diff < bestDiff) { bestDiff = diff; bestId = kv.Key; }
            }
            if (bestId == null) return null;
            return DirectoryMaster.Item(bestId, true);
        }
        catch { return null; }
    }

    // 物品放入柜台（frontInvinvElement）
    private static void AddToFront(GameItem it)
    {
        try
        {
            if (it == null) return;
            EmporiumEntry em = EmporiumEntry.Instance;
            if (em == null || em.backInvinvElement == null) return;
            var inv = (GameInventory)em.backInvinvElement;
            inv.UncheckedAccept(it); // 主仓库(后库)
        }
        catch { }
    }

    // 物资箱：CreateLootCrate 随机箱 + 内部按 ItemPool 填充到目标价值
    private static GameItem CreateSupplyCrate(long targetValue)
    {
        try
        {
            string[] boxes = { "evidence_box", "med_box", "sec_box", "service_box", "eng_box" };
            string bid = boxes[Core.Rng.Next(boxes.Length)];
            GameItem crate = CustomStorageContainer.CreateLootCrate(bid);
            if (crate == null) return null;
            GameInventory inv = null;
            try
            {
                var cw = crate.contentWindow;
                Core.LogMsg("[箱诊] crate id=" + crate.identifier + " cw=" + (cw==null?"null":"ok"));
                if (cw != null)
                {
                    var prop = cw.GetType().GetProperty("inventory", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    inv = prop != null ? (prop.GetValue(cw) as GameInventory) : null;
                    Core.LogMsg("[箱诊] inv=" + (inv==null?"null":"ok"));
                }
            }
            catch { }
            if (inv != null)
            {
                long spent = 0; int tries = 0;
                bool wantContraband = Core.Rng.Next(100) < 5; // 好物95% / 违禁5%
                var pool = new System.Collections.Generic.List<string>(FrogPowerPerk.ItemPool);
                while (spent < targetValue && tries < 40 && pool.Count > 0)
                {
                    int idx = Core.Rng.Next(pool.Count);
                    string id = pool[idx]; pool.RemoveAt(idx);
                    GameItem it = null;
                    try { it = DirectoryMaster.Item(id, true); } catch { }
                    if (it == null) { tries++; continue; }
                    bool isContra = false;
                    try { isContra = ContrabandHelper.GetContrabandLevel(it) > 0; } catch { }
                    if (wantContraband != isContra) { tries++; continue; } // 分流不符跳过
                    try { inv.UncheckedAccept(it); spent += it.unitValue; } catch { tries++; }
                }
            }
            return crate;
        }
        catch { return null; }
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
    private static readonly float[] _frameMs = { 0.5f, 0.2f, 0.15f }; // 待机/走动/偷（秒/帧）

    private static void EnsureSprites()
    {
        if (_spIdle != null) return;
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
        // 反射查找 ImageConversion.LoadImage（IL2CPP 不在标准命名空间——DestinyDice L175-213 先例；只找一次）
        if (_loadImageMethod == null)
        {
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
                    _loadImageMethod = icType.GetMethod("LoadImage",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                        null, new Type[] { typeof(Texture2D), typeof(Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<byte>) }, null);
            }
            catch { }
        }
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

    // 动作切换（0 待机 / 1 走动 / 2 偷）
    private static string EvalState()
    {
        try {
            if (_animMode == 2) return "angry";
            if (_animMode == 1) return "walk";
            try { if (_walking) return "walk"; } catch { } // 平时走动用walk帧
            // 09-19 修：K_LEAVE>0 但蛙娘实体在店里（读档恢复）→ 不挥手，走正常状态
            try { if (PerkStatePersistence.GetInt(NS, K_LEAVE, 0) > 0 && _cachedGirlItem == null) return "away"; } catch { }
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

    // 每帧驱动（Core.OnUpdate 调用；轻量 + 全异常防护 + 交易模式暂停——用户规范：OnUpdate 不做重操作）
    public static void OnUpdateTick()
    {
        try
        {
            // 09-22 诊断（用完删）：5秒节流看 OnUpdateTick 状态
            if (Time.time - _lastDiagTime > 5f)
            {
                _lastDiagTime = Time.time;
                string elState = "n/a";
                try
                {
                    var it = _cachedGirlItem != null ? _cachedGirlItem : FindGirlItem();
                    if (it == null) elState = "not-found";
                    else
                    {
                        GameItemElement te = null;
                        try { te = it as GameItemElement; } catch { }
                        if (te == null) { try { te = it.Cast<GameItemElement>(); } catch { } }
                        elState = te != null ? "element-cast-ok" : "not-element";
                    }
                }
                catch { }
            }
            if (!Exists()) return;
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
                    _stateFrameSec = ns == "happy" ? 0.25f : ns == "hungry" ? 0.3f : ns == "dirty" ? 0.3f : ns == "sick" ? 0.5f : ns == "sleepy" ? 0.5f : ns == "angry" ? 0.2f : (ns == "away" || ns == "return" || ns == "walk") ? 0.125f : 0.375f;
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
    private static void TryApplyAnimFrame()
    {
        try
        {
            // 确保 _girlShape 初始化（Unity就绪后）
            try { if (_girlShape == null) { var gsb = new GridShapeBuilder(); gsb.SetDataFill(2, 3); _girlShape = gsb.Build(); } } catch { }
            // 读档后旧引用已销毁（parentInventory==null）→ 立即重置重找
            try { if (_cachedGirlItem != null && _cachedGirlItem.parentInventory == null) { _cachedGirlItem = null; _cacheRefreshFrames = 0; } } catch { _cachedGirlItem = null; }
            if (_cachedGirlItem == null || _cacheRefreshFrames <= 0)
            {
                _cacheRefreshFrames = 120;
                _cachedGirlItem = FindGirlItem();
                    _curState = ""; _frameIndex = 0; _frameTimer = 0f; // 读档后强制重新判定状态
                    if (_cachedGirlItem != null) { try { ApplyIcon(_cachedGirlItem); } catch { } try { if (_girlShape != null) _cachedGirlItem.SetShape(_girlShape); } catch { } } // 先ApplyIcon再SetShape(2x3)——ApplyIcon会覆盖shape
                    _curState = ""; _frameIndex = 0; _frameTimer = 0f; // 读档后强制重新判定状态
            }
            else _cacheRefreshFrames--;
            if (_cachedGirlItem == null) return;
            GameItemElement el = null;
            try { el = _cachedGirlItem as GameItemElement; } catch { }
            if (el == null) { try { el = _cachedGirlItem.Cast<GameItemElement>(); } catch { } }
            if (el == null)
            {
                // 09-23 读档/过天后物品重建——旧缓存 Cast 失败立即重找（不等 120 帧）——根治掉动态
                _cachedGirlItem = FindGirlItem();
                try { if (_cachedGirlItem != null && _girlShape != null) _cachedGirlItem.SetShape(_girlShape); } catch { }
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
                }
            }
            if (el == null) return;
            Sprite f = _curAnimSprites[_frameIndex % _curAnimSprites.Length];
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
                        // 09-19 修：读档后 shape 变 1×1 → 强制 2×3
                        try { if (_girlShape == null) { var gsb = new GridShapeBuilder(); gsb.SetDataFill(2, 3); _girlShape = gsb.Build(); } c.SetShape(_girlShape); } catch { }
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
            // 09-19 修：强制用 2×3（读档后 g.shape 可能变 1×1）
            if (_girlShape == null) { try { var gsb = new GridShapeBuilder(); gsb.SetDataFill(2, 3); _girlShape = gsb.Build(); } catch { } }
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

    // ApplyAnimationFrame Prefix：蛙娘替换帧（美术方案——原生 Tick 调此方法时替换；帧索引由 OnUpdateTick 推进）
    // 读档后一次性重置（OnGameLoadedNormal hook）——不每帧调，避免闪烁/拖不动
    public static void OnGameLoadedReset()
    {
        try {
            _cachedGirlItem = null; _cacheRefreshFrames = 0; _curState = ""; _frameIndex = 0; _frameTimer = 0f;
            try { EnsureSprites(); } catch { } // 读档后确保动画帧已加载
        } catch { }
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
            if (s != null) frame = s;
        }
        catch { }
    }
}

// ===================== 蛙娘占位像素图标（32×32 透明底，绿色蛙身+眼睛+腮红） =====================
internal static class WageGirlIcons
{
    public static Color[] Pixels()
    {
        const int S = 32;
        var px = new Color[S * S];
        // 透明底
        for (int i = 0; i < px.Length; i++) px[i] = new Color(0, 0, 0, 0);
        Color body = new Color(0.36f, 0.72f, 0.42f, 1f);   // 蛙绿
        Color dark = new Color(0.12f, 0.34f, 0.20f, 1f);   // 深描边
        Color eyeW = new Color(0.95f, 0.98f, 0.95f, 1f);   // 眼白
        Color eyeB = new Color(0.10f, 0.10f, 0.12f, 1f);   // 瞳孔
        Color blush = new Color(0.95f, 0.55f, 0.60f, 1f);  // 腮红

        void Set(int x, int y, Color c) { if (x >= 0 && x < S && y >= 0 && y < S) px[y * S + x] = c; }
        // 身体（圆头 + 方身）：中心 16, 行 6-25
        for (int y = 5; y <= 25; y++)
        {
            int half = (y < 14) ? 9 : 8; // 头圆身方
            int x0 = 16 - half, x1 = 16 + half;
            if (y >= 14) { x0 = 7; x1 = 24; }
            for (int x = x0; x <= x1; x++) Set(x, y, body);
        }
        // 深色描边：左右各 1 列 + 底边
        for (int y = 5; y <= 25; y++) { Set(6, y, dark); Set(25, y, dark); }
        for (int x = 6; x <= 25; x++) { Set(x, 25, dark); Set(x, 5, dark); }
        // 眼睛（两枚 3×3 白 + 1px 瞳孔）
        for (int y = 10; y <= 12; y++) { for (int x = 11; x <= 13; x++) Set(x, y, eyeW); for (int x = 19; x <= 21; x++) Set(x, y, eyeW); }
        Set(12, 11, eyeB); Set(20, 11, eyeB);
        // 腮红（两枚 2×2）
        Set(9, 17, blush); Set(10, 17, blush); Set(9, 18, blush); Set(10, 18, blush);
        Set(21, 17, blush); Set(22, 17, blush); Set(21, 18, blush); Set(22, 18, blush);
        // 嘴（微笑 3px）
        Set(15, 20, dark); Set(16, 21, dark); Set(17, 20, dark);
        return px;
    }
}
